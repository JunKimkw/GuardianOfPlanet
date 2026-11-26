using System.Linq;
using UnityEngine;

// 행성 버프 창 A (2026-11-26 사용자 "버프는 A", 미리보기 그대로 = UIGen/buff)
// - 예전 버프 페이지(Canvas/Tabs Above Buttons/Buffs)가 열리면 그 위를 덮어요
// - 줄 = PlanetBuffManager.Buffs 순서 (= 열리는 순서, 앞 버프 Lv.5 면 다음이 열림). 별 5개 = 레벨 10 (별 하나 2레벨, 홀수는 반 흐림)
// - 올리기 = 예전 확인 창 없이 바로 PlanetBuff.Upgrade (부품을 써요). 부품이 모자라면 메시지
public partial class RemasterUI
{
    GenView bfView;
    GameObject bfPage;
    string bfKey;
    static readonly Color BfStarOn = new Color32(0xff, 0xe1, 0x4a, 0xff), BfStarHalf = new Color(1f, 0.882f, 0.29f, 0.5f), BfStarOff = new Color32(0x3e, 0x4b, 0x63, 0xff);

    void UpdateBuffPage()
    {
        if(bfPage == null) { bfPage = GenFindPage("Buffs", "Tabs Above Buttons"); if(bfPage == null) return; }
        if(!bfPage.activeInHierarchy) { if(bfView != null) { GenClose(bfView); bfView = null; } return; }
        if(bfView == null || bfView.layer == null) BuildBuff();
        else RefreshBuff(false);
    }

    PlanetBuff[] BfList() => PlanetBuffManager.instance != null && PlanetBuffManager.instance.Buffs != null ? PlanetBuffManager.instance.Buffs : new PlanetBuff[0];

    void BuildBuff()
    {
        bfKey = null;
        bfView = GenBuild("buff", "BuffA", true, (RectTransform)bfPage.transform);
        if(bfView == null) return;
        var v = bfView;
        GenHit(v, "close", () => GenPageBack(bfPage));
        var list = BfList();
        for(int j = 0; j < 8 && j < list.Length; j++) { int jj = j; GenHit(v, "b" + j + ".buy", () => BuffBuy(jj), 2f); }
        RefreshBuff(true);
    }

    static string BfVal(PlanetBuff b, int level)
    {
        float v = b.MaxOf() / 10f * level;
        switch(b.buffType) {
            case PlanetBuff.BuffType.AttackSpeedIncrease: return "+" + v.ToString("0.#") + "시간";
            case PlanetBuff.BuffType.BulletSpeedIncrease: return "-" + (v * 100f).ToString("0.#") + "%";
            default: return "+" + (v * 100f).ToString("0.#") + "%";
        }
    }

    void RefreshBuff(bool force)
    {
        var v = bfView; var list = BfList();
        if(v == null || v.layer == null) return;
        string key = GameManager.GetParts() + "_" + string.Join(",", list.Select(b => b != null ? b.Level : -1));
        if(!force && key == bfKey) return;
        bfKey = key;
        GenText(v, "parts", GameManager.GetParts().ToString("N0"));
        for(int j = 0; j < 8; j++) {
            bool on = j < list.Length && list[j] != null;
            GenShow(v, "b" + j, on);
            if(!on) continue;
            var b = list[j];
            bool locked = j > 0 && list[j - 1] != null && list[j - 1].Level < 5;
            bool max = b.Level >= 10;
            float ra = locked ? 0.55f : 1f;   // 잠긴 줄은 흐리게 (미리보기 opacity .55)
            GenAlpha(v, "b" + j, ra);
            for(int k = 0; k < 5; k++) {
                bool full = b.Level >= (k + 1) * 2, half = !full && b.Level == k * 2 + 1;
                var c = full ? BfStarOn : half ? BfStarHalf : BfStarOff; c.a *= ra;
                if(v.texts.TryGetValue("b" + j + ".s" + k, out var st) && st.Count > 0) st[0].color = c;
            }
            GenText(v, "b" + j + ".now", BfVal(b, b.Level));
            GenShow(v, "b" + j + ".arr", !max);
            GenShow(v, "b" + j + ".next", !max);
            if(!max) GenText(v, "b" + j + ".next", BfVal(b, b.Level + 1));
            GenShow(v, "b" + j + ".buy", !max && !locked);
            GenShow(v, "b" + j + ".max", max && !locked);
            GenShow(v, "b" + j + ".lock", locked);
            if(!max) GenText(v, "b" + j + ".price", b.getBuffPriceParts().ToString());
        }
    }

    void BuffBuy(int j)
    {
        var list = BfList();
        if(j >= list.Length || list[j] == null) return;
        var b = list[j];
        if(j > 0 && list[j - 1] != null && list[j - 1].Level < 5) return;
        if(b.Level >= 10) return;
        if(GameManager.GetParts() < b.getBuffPriceParts()) { ShowMessage("부품이 부족해요", $"부품 {b.getBuffPriceParts()}개가 필요해요"); return; }
        b.Upgrade();
        if(SoundManager.Instance != null) SoundManager.Instance.Invoke("playBuffSFX", 0.05f);
        RefreshBuff(true);
    }
}
