using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 운석 상점 칸 (2026-11-21). 씬에 따로 만들지 않고, 실행할 때 튕김총 칸을 복제해서 만들어요 (CreateFrom).
// 상점 목록(Contents)은 세로 자동 정렬이라 맨 아래에 붙어요. 그림(아이콘)은 아직 튕김총 그림이에요 (P7 에서 바꿔요).
public class MeteorShop : WeaponShopBase
{
    public static MeteorShop instance;
    [HideInInspector] public MeteorGun weapon;

    public override WeaponBase Weapon => weapon;
    protected override int UnlockPlanetIndex => 9;   // 10번째 행성 클리어
    protected override string StatLabel => "운석 데미지의";
    protected override string Intro => "하늘에서 적 위로 운석을 떨어뜨려\n그 구역에 피해를 주는 무기";

    // 패시브 설명 (튕김총 칸의 패시브 7개 자리에 그대로 넣어요. 레벨 조건 10/50/100/200/300/500/750 은 칸에 따로 써 있어요)
    static readonly string[] PassiveTexts = {
        "운석 데미지 10 증가",
        "한 번에 떨어지는 운석 1개 증가",
        "레벨마다 운석 데미지 1.2% 증가",
        "운석 장전 시간 25% 감소",
        "운석 범위 30% 증가",
        "떨어진 자리가 2초 동안 불타요",
        "운석 데미지 400% 증가",
    };

    void Awake() => instance = this;

    protected override void ApplyPassives(int lv)
    {
        var w = weapon;
        if(w == null) return;
        w.ExtraDamagePercent = 0;
        if(Passive(0, 10)) w.ExtraDamage = 10;
        if(Passive(1, 50)) w.ExtraMeteor = true;
        if(Passive(2, 100)) w.ExtraDamagePercent += PerLevelFrom100(lv, 0.012f);
        if(Passive(3, 200)) w.ExtraAttackSpeedScale = 0.25f;
        if(Passive(4, 300)) w.BigRadius = true;
        if(Passive(5, 500)) w.Burning = true;
        if(Passive(6, 750)) w.ExtraDamagePercent += 4f;
    }

    // src(튕김총 칸)를 복제해서 운석 칸을 만들어요.
    // 꺼 둔 임시 부모 아래에서 복제해서 복제본의 Awake(튕김총 칸 Instance 덮어쓰기 등)가 먼저 돌지 않게 하고,
    // 튕김총 칸 스크립트를 떼고 운석 칸 스크립트를 붙여 UI 연결(글자·잠금·패시브)을 그대로 옮겨요.
    // 강화 버튼은 원래 튕김총 칸의 LevelUP 을 부르게 씬에 연결돼 있어서 운석 칸의 LevelUP 으로 다시 이어요.
    public static MeteorShop CreateFrom(WeaponShopBase src)
    {
        if(src == null) return null;
        var holder = new GameObject("MeteorShopTemp");
        holder.SetActive(false);
        var clone = Instantiate(src.gameObject, holder.transform);
        clone.name = "MeteorShop";
        var old = clone.GetComponent(src.GetType()) as WeaponShopBase;
        var m = clone.AddComponent<MeteorShop>();
        if(old != null) {
            m.LevelText = old.LevelText;
            m.WeaponStat = old.WeaponStat;
            m.priceText = old.priceText;
            m.LockCover = old.LockCover;
            m.LockText = old.LockText;
            m.passiveObjects = old.passiveObjects != null ? new List<WeaponPassive>(old.passiveObjects) : new List<WeaponPassive>();
            // old 를 부르던 버튼 → m
            foreach(var b in clone.GetComponentsInChildren<Button>(true)) {
                bool rewire = false;
                for(int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                    if(b.onClick.GetPersistentTarget(i) == old) { b.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off); rewire = true; }
                if(rewire) b.onClick.AddListener(m.LevelUP);
            }
            DestroyImmediate(old);
        }
        // 패시브 설명 (WeaponPassive.Awake 가 말풍선 글자로 옮겨요 → 켜기 전에 넣어요)
        for(int i = 0; i < m.passiveObjects.Count && i < PassiveTexts.Length; i++) {
            var p = m.passiveObjects[i];
            if(p == null) continue;
            p.passiveExplain = PassiveTexts[i];
            if(p.PassiveCover != null) p.PassiveCover.SetActive(true);
        }
        // 칸 안의 무기 이름 글자 바꾸기 (튕김총 → 운석)
        foreach(var tmp in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
            if(tmp != null && tmp.text != null && (tmp.text.Contains("튕김총") || tmp.text.Contains("추적 우주선")))
                tmp.text = tmp.text.Replace("튕김총", "운석").Replace("추적 우주선", "운석");
        clone.transform.SetParent(src.transform.parent, false);
        clone.transform.SetAsLastSibling();
        Destroy(holder);
        return m;
    }
}
