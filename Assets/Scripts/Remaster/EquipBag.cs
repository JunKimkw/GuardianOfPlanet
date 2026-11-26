using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 장비 가방 (2026-11-25 사용자 결정): 같은 장비도 한 칸씩 따로, 장비마다 능력치가 무작위 (×1.0 ~ ×2.0)
// - 장비 종류(프리팹 Equips) = '도감'. 가진 장비 하나하나는 그 Equips 의 복사본이에요 (레벨·강화 기록·무작위 값을 따로 가져요)
//   그래서 강화·장착·능력치 표시 같은 예전 코드는 그대로 장비 하나씩 돌아가요
// - EquipManager.All_Equips = 가방에 있는 장비 칸들. 도감 칸(뽑기·합성 미리보기용)은 Catalog 에 따로 (화면에 안 나와요)
// - 무작위 값: 능력치마다 따로 ×1.0 ~ ×2.0. 평균 ×1.5 가 등급 사다리(EquipBalance)와 같아요
// - 가방 크기 equip_bag_size (기본 100). 가득 차면 다이아·뽑기권 뽑기를 막아요 (결제 상품은 넘쳐도 줘요)
[System.Serializable]
public class EquipItemSave
{
    public string uid;
    public string id;
    public int level;
    public List<string> upgrade = new List<string>();
    public List<float> rolls = new List<float>();
    public bool equipped;
}

public static class EquipBag
{
    public const int StatCount = 9;   // damage, damagePercent, AttackSpeed, BulletSpeed, CritChance, CritDamage, StunPercent, ExtraEXP, ExtraMoney
    public const int UpgradeSlots = 21;

    public static readonly List<EquipFrame> Catalog = new List<EquipFrame>();
    static Transform holder;
    // 복사본·도감 칸을 두는 곳 (꺼진 오브젝트라 화면에 안 나와요)
    public static Transform Holder {
        get {
            if(holder == null) { var go = new GameObject("EquipBag (remaster)"); go.SetActive(false); holder = go.transform; }
            return holder;
        }
    }

    static EquipManager M => EquipManager.Instance;
    public static int Size => Mathf.Max(1, Tuning.GetInt("equip_bag_size", 100));
    public static int Count => M != null ? M.All_Equips.Count(f => f != null) : 0;
    public static bool HasRoom(int n) => Count + n <= Size;

    public static Equips AssetOf(Equips e) => e == null ? null : e.source != null ? e.source : e;

    // 이 장비(종류)를 몇 개 가졌나
    public static int CountOf(Equips e)
    {
        if(e == null || M == null) return 0;
        int c = 0;
        foreach(var f in M.All_Equips) if(f != null && f.equipData != null && f.equipData.id == e.id) c++;
        return c;
    }

    public static float[] RandomRolls()
    {
        var r = new float[StatCount];
        float lo = Tuning.Get("equip_roll_min", 1f), hi = Tuning.Get("equip_roll_max", 2f);
        for(int k = 0; k < StatCount; k++) r[k] = Random.Range(lo, hi);
        return r;
    }
    static float[] MiddleRolls() { var r = new float[StatCount]; for(int k = 0; k < StatCount; k++) r[k] = 1.5f; return r; }

    // 장비 하나 만들어서 가방에 넣기
    public static EquipFrame Create(Equips asset, string uid, float[] rolls, int level, List<string> upgrade)
    {
        if(asset == null || M == null) return null;
        asset = AssetOf(asset);
        var clone = Object.Instantiate(asset, Holder);
        clone.name = asset.name;
        clone.source = asset;
        clone.uid = string.IsNullOrEmpty(uid) ? System.Guid.NewGuid().ToString("N") : uid;
        clone.rolls = rolls != null && rolls.Length >= StatCount ? rolls : RandomRolls();
        clone.AmountOfEquip = 1;
        clone.Level = Mathf.Clamp(level, 0, 20);
        clone.UpgradeIndex = new List<string>();
        for(int j = 0; j < UpgradeSlots; j++) clone.UpgradeIndex.Add(upgrade != null && j < upgrade.Count ? upgrade[j] ?? "" : "");
        clone.setEquipInfo();
        clone.setExtraOptionStats();
        clone.setFinalOptionStats();
        return M.CreateEquip(clone);
    }

    // 뽑기·합성: 도감 칸(또는 가진 장비) → 새 장비 하나
    public static EquipFrame Grant(EquipFrame pick) => pick == null ? null : Create(pick.equipData, null, null, 0, null);

    // 가방에서 빼기 (장착 중이면 벗기고, 장비 세트 목록에서도 빼요)
    public static void Remove(EquipFrame f)
    {
        if(f == null || M == null) return;
        var em = EquipingManager.Instance;
        if(em != null) {
            var slot = SlotOf(f.equipData.TypeOfEquip);
            if(slot != null && slot.equipment == f) em.UnequipItem(f.equipData.TypeOfEquip);
        }
        M.All_Equips.Remove(f);
        foreach(var s in new[] { M.Sorting_Ring, M.Sorting_Necklace, M.Sorting_Book, M.Sorting_Accessory, M.Sorting_Relics })
            if(s != null) s.Have_Items.Remove(f);
        if(M.Equip_Sets != null)
            foreach(var set in M.Equip_Sets)
                for(int j = 0; j < set.equipSet.Count; j++) if(set.equipSet[j] == f) set.equipSet[j] = null;
        if(M.SelectedEquip == f) M.SelectedEquip = null;
        if(f.equipData != null && f.equipData.source != null) Object.Destroy(f.equipData.gameObject);
        Object.Destroy(f.gameObject);
    }

    public static EquipingData SlotOf(Equips.Type t)
    {
        var em = EquipingManager.Instance;
        if(em == null) return null;
        switch(t) {
            case Equips.Type.Ring: return em.Ring;
            case Equips.Type.Necklace: return em.Necklace;
            case Equips.Type.Accessory: return em.Accessory;
            case Equips.Type.Relics: return em.Relics;
            default: return em.Book;
        }
    }
    public static bool IsEquipped(EquipFrame f) { var s = f != null ? SlotOf(f.equipData.TypeOfEquip) : null; return s != null && s.equipment == f; }

    // 합성 재료: 같은 등급 장비 중 장착 안 한 것, 강화 낮은 것, 무작위 값 낮은 것부터. keepLast 는 마지막에 (고른 장비)
    // 2026-11-26: 같은 장비 → 같은 등급 (부위 상관없이), 결과도 같은 등급 (MergeManager)
    public static List<EquipFrame> MergeMaterials(Equips e, EquipFrame keepLast)
    {
        if(e == null || M == null) return new List<EquipFrame>();
        return M.All_Equips.Where(f => f != null && f.equipData != null && f.equipData.Grade == e.Grade && !IsEquipped(f))
            .OrderBy(f => f == keepLast ? 1 : 0).ThenBy(f => f.equipData.Level).ThenBy(f => f.equipData.Quality01).ToList();
    }
    // 개수만 (장비 창이 매 프레임 불러요 - 정렬·잠재력 계산 없이, 2026-11-27 점검)
    public static int MergeCount(Equips e) => e == null || M == null ? 0 : M.All_Equips.Count(f => f != null && f.equipData != null && f.equipData.Grade == e.Grade && !IsEquipped(f));

    // ── 저장 ──
    public static void Save(GameData d)
    {
        d.equipItems = new List<EquipItemSave>();
        if(M == null) return;
        foreach(var f in M.All_Equips) {
            if(f == null || f.equipData == null) continue;
            var e = f.equipData;
            d.equipItems.Add(new EquipItemSave {
                uid = e.uid, id = e.id, level = e.Level,
                upgrade = new List<string>(e.UpgradeIndex ?? new List<string>()),
                rolls = new List<float>(e.rolls ?? MiddleRolls()),
                equipped = IsEquipped(f),
            });
        }
    }

    static Dictionary<string, Equips> assets;
    static Equips FindAsset(string id)
    {
        if(assets == null || assets.Count == 0) {
            assets = new Dictionary<string, Equips>();
            foreach(var c in Catalog) if(c != null && c.equipData != null && !string.IsNullOrEmpty(c.equipData.id)) assets[c.equipData.id] = c.equipData;
        }
        return id != null && assets.TryGetValue(id, out var a) ? a : null;
    }

    public static void Load(GameData d)
    {
        if(M == null) return;
        foreach(var f in M.All_Equips.ToList()) Remove(f);
        M.All_Equips.Clear();
        if(d.equipItems != null) {
            foreach(var s in d.equipItems) {
                var asset = FindAsset(s.id);
                if(asset == null) continue;
                var f = Create(asset, s.uid, s.rolls != null && s.rolls.Count >= StatCount ? s.rolls.ToArray() : MiddleRolls(), s.level, s.upgrade);
                if(f != null && s.equipped) Equip(f);
            }
            return;
        }
        // 예전 저장 (종류마다 개수 하나): 가진 개수만큼 만들고 무작위 값은 모두 ×1.5 (중간). 강화 레벨·장착은 첫 번째에
        foreach(var c in Catalog) {
            var asset = c.equipData;
            if(asset == null || d.Equip_Amount == null) continue;
            d.Equip_Amount.TryGetValue(asset.id, out int amount);
            if(amount <= 0) continue;
            int level = 0; if(d.Equip_Level != null) d.Equip_Level.TryGetValue(asset.id, out level);
            var upg = new List<string>();
            for(int j = 0; j < 20; j++) { string v = null; if(d.Equip_UpgradeIndex != null) d.Equip_UpgradeIndex.TryGetValue(asset.id + "UpgradeIndex" + j, out v); upg.Add(v ?? ""); }
            bool eq = false; if(d.Equip_IsEquiped != null) d.Equip_IsEquiped.TryGetValue(asset.id, out eq);
            for(int k = 0; k < amount; k++) {
                var f = Create(asset, null, MiddleRolls(), k == 0 ? level : 0, k == 0 ? upg : null);
                if(f != null && k == 0 && eq) Equip(f);
            }
        }
    }

    static void Equip(EquipFrame f)
    {
        if(EquipingManager.Instance == null) return;
        EquipingManager.Instance.SetEquipingItem(f);
        f.isEquiped = true;
    }

    // 장비 세트(1세트·2세트) 칸 찾기: 새 저장은 uid, 예전 저장은 종류 id
    public static EquipFrame Find(string key)
    {
        if(M == null || string.IsNullOrEmpty(key) || key == "null") return null;
        var byUid = M.All_Equips.FirstOrDefault(f => f != null && f.equipData != null && f.equipData.uid == key);
        if(byUid != null) return byUid;
        return M.All_Equips.Where(f => f != null && f.equipData != null && f.equipData.id == key)
            .OrderByDescending(f => IsEquipped(f)).ThenByDescending(f => f.equipData.Level).FirstOrDefault();
    }
}
