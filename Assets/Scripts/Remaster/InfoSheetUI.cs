using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 정보 창 A3 (2026-11-28 사용자 "A3 로 해 줘" = previews/info2.png, tools/ui_preview/info_preview.js 의 A3)
// - 원래 정보 창(Canvas/Tabs/Player Info)의 그림을 빌려 밑에서 올라오는 판으로 다시 놓아요. 좌표 = 미리보기 게임 단위 그대로 (1080 × 2048, 화면 아래 기준)
// - 위: 프로필(사진 · 이름 · ID 복사 · 레벨 막대) · 기록 6칸(전투력 · 최고 스테이지 · 처치 · 플레이 · 환생 · 별의 먼지) · 장착 장비 5칸 + 세트 효과 · 출격 무기
// - 아래: 능력치 4 × 2 (최종 값 · 이름 · 포인트 Lv · 올리기 = StatsManager.UpgradeDirect, 확인 창 없이) · 남은 포인트 · 초기화(1억 골드, 묻는 창)
// - 원래 창은 투명하게(CanvasGroup). info_sheet 0 이면 원래 창
public partial class RemasterUI
{
    const float IfT = 410f, IfH = 2048f, IfBot = 180f;
    GameObject ifPage;
    RectTransform ifLayer, ifRoot;
    CanvasGroup ifPageCg;
    string ifKey;
    float ifNext;   // 다시 그리기는 0.5초에 한 번까지 (경험치·처치 수가 계속 바뀌어도). 직접 누른 건 ifKey = null 로 바로
    readonly Dictionary<string, Sprite> ifSpr = new Dictionary<string, Sprite>();
    const string IfP = "Popup05_Topbar_Divided/";

    void UpdateInfoSheet()
    {
        if(Tuning.Get("info_sheet", 1f) <= 0f) return;
        if(ifPage == null) { ifPage = GenFindPage("Player Info", "Tabs"); if(ifPage == null) return; }
        if(!ifPage.activeInHierarchy) {
            if(ifLayer != null) { Destroy(ifLayer.gameObject); ifLayer = null; ifRoot = null; ifKey = null; }
            return;
        }
        if(ifPageCg == null) { ifPageCg = ifPage.GetComponent<CanvasGroup>(); if(ifPageCg == null) ifPageCg = ifPage.AddComponent<CanvasGroup>(); }
        if(ifPageCg.alpha != 0f) { ifPageCg.alpha = 0f; ifPageCg.blocksRaycasts = false; }
        if(ifLayer == null) IfMakeLayer();
        IfRefresh();
    }

    Sprite IfS(string p)
    {
        if(ifSpr.TryGetValue(p, out var s)) return s;
        var t = ifPage.transform.Find(IfP + p);
        var im = t != null ? t.GetComponent<Image>() : null;
        s = im != null ? im.sprite : null;
        if(s == null) Debug.LogWarning("[정보 창] 그림 없음: " + p);
        ifSpr[p] = s;
        return s;
    }

    void IfMakeLayer()
    {
        var host = (RectTransform)ifPage.transform.parent;
        ifLayer = Stretch("InfoSheet", host);
        ifLayer.SetSiblingIndex(ifPage.transform.GetSiblingIndex() + 1);
        var dim = Stretch("Dim", ifLayer);
        dim.offsetMin = new Vector2(0f, IfBot);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(IfClose);
        ifRoot = Rect("Sheet", ifLayer, new Vector2(1080f, IfH - IfT), Vector2.zero);
        ifRoot.anchorMin = ifRoot.anchorMax = new Vector2(0.5f, 0f);
        ifRoot.pivot = new Vector2(0.5f, 0f);
        float hc = host.rect.height > 0f ? host.rect.height : IfH, wc = host.rect.width > 0f ? host.rect.width : 1080f;
        float s = Mathf.Min(1f, (hc - 20f) / (IfH - IfT), wc / 1080f);
        ifKey = null;
        StartCoroutine(SlideIn(ifRoot, s));   // 아래에서 올라와요 (모든 창)
    }

    void IfClose()
    {
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        if(ifPage != null) ifPage.SetActive(false);
        var bb = GameObject.Find("Bottom Buttons");
        var bgp = bb != null ? bb.GetComponent<ButtonGroup>() : null;
        if(bgp != null && bgp.tabButtons != null) bgp.resetAllTabs();
    }

    // ---- 게임 단위 그리기 (미리보기와 같은 좌표: 화면 1080 × 2048 왼쪽 위 기준)
    RectTransform IfRect(float x, float y, float w, float h)
    {
        var rt = Rect("E", ifRoot, new Vector2(w, h), Vector2.zero);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -(y - IfT));
        return rt;
    }
    Image IfBox(Sprite s, float x, float y, float w, float h, Color c)
    {
        var im = IfRect(x, y, w, h).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        if(s != null && s.border.sqrMagnitude > 0f) { im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 1f; }
        return im;
    }
    Image IfPic(Sprite s, float x, float y, float w, float h, Color c)
    {
        var im = IfRect(x, y, w, h).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = s != null ? c : Color.clear; im.raycastTarget = false; im.preserveAspect = true;
        return im;
    }
    TextMeshProUGUI IfTx(string s, float x, float y, float size, Color c, int align = 0)
    {
        float w = 1000f, h = size * 1.4f;
        var rt = IfRect(align == 0 ? x - w / 2f : align == 1 ? x : x - w, y - h / 2f, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = MapFont(); t.text = s; t.fontSize = size; t.richText = true;
        t.alignment = align == 0 ? TextAlignmentOptions.Midline : align == 1 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
        GameFont(t, true);
        t.color = c;
        return t;
    }
    void IfHit(float x, float y, float w, float h, UnityEngine.Events.UnityAction act)
    {
        var rt = IfRect(x, y, w, h);
        rt.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        rt.gameObject.AddComponent<Button>().onClick.AddListener(act);
    }

    // ---- 데이터
    static readonly (string name, StatsData.TypeofStat type, string icon)[] IfStats = {
        ("데미지", StatsData.TypeofStat.Damage, "Stats/Group_List/Damage/Icon"),
        ("공격 속도", StatsData.TypeofStat.Attack_Speed, "Stats/Group_List/Attack Speed/Icon"),
        ("총알 속도", StatsData.TypeofStat.Attack_Bullet_Speed, "Stats/Group_List/Bullet Speed/Icon"),
        ("치명타 확률", StatsData.TypeofStat.CritChance, "Stats/Group_List/Crit Chance/Icon"),
        ("치명타 데미지", StatsData.TypeofStat.CritDamage, "Stats/Group_List/Crit Damage/Icon"),
        ("스턴 확률", StatsData.TypeofStat.StunPercent, "Stats/Group_List/Stun Percent/Icon"),
        ("골드 획득", StatsData.TypeofStat.ExtraMoney, "Stat Point/Stats Upgrade/Viewport/contents/Extra Gold/Stat UI/Icon"),
        ("경험치 획득", StatsData.TypeofStat.ExtraEXP, "Stat Point/Stats Upgrade/Viewport/contents/Extra EXP/Stat UI/Icon"),
    };
    static string IfFinal(StatsData.TypeofStat t)
    {
        var p = Player.instance; var sm = StatsManager.instance;
        if(p == null) return "-";
        switch(t) {
            case StatsData.TypeofStat.Damage: return p.FinalAttack_Damage.ToString("N0");
            case StatsData.TypeofStat.Attack_Speed: return "+" + (p.FinalAttack_SpeedPercent * 100f).ToString("0.#") + "%";
            case StatsData.TypeofStat.Attack_Bullet_Speed: return "+" + (p.FinalAttack_Bullet_SpeedPercent * 100f).ToString("0.#") + "%";
            case StatsData.TypeofStat.CritChance: return Mathf.Clamp(p.FinalCriticalChance, 0f, 100f).ToString("0.#") + "%";
            case StatsData.TypeofStat.CritDamage: return "+" + (p.FinalCriticalDamage * 100f + 100f).ToString("0.#") + "%";
            case StatsData.TypeofStat.StunPercent: return p.FinalStunPower.ToString("0.#") + "%";
            default: return "+" + ((sm != null ? sm.getStatAmount(t) : 0f) * 100f).ToString("0.#") + "%";   // 골드·경험치 = 포인트로 올린 만큼
        }
    }
    // 도감: 만난 몬스터 / 전체
    static string IfCollection()
    {
        var cm = CollectionManager.Instance;
        if(cm == null || cm.CollectionDatas == null) return "-";
        int found = cm.CollectionDatas.Count(d => d != null && d.grade != CollectionData.Grade.Unknown), total = cm.CollectionDatas.Count(d => d != null);
        return found + " / " + total;
    }
    static List<EquipFrame> IfEquipped()
    {
        var list = new List<EquipFrame>();
        foreach(Equips.Type t in new[] { Equips.Type.Ring, Equips.Type.Necklace, Equips.Type.Book, Equips.Type.Accessory, Equips.Type.Relics }) {
            var slot = EquipBag.SlotOf(t);
            list.Add(slot != null ? slot.equipment : null);
        }
        return list;
    }
    List<(string name, int lv)> IfWeapons()
    {
        var list = new List<(string, int)>();
        var wm = WeaponManager.instance;
        var main = wm != null ? wm.Weapons.OfType<MainWeapon>().FirstOrDefault() : null;
        list.Add(("기본총", main != null ? main.UpgradeLevel : 0));
        if(mgr != null) foreach(var w0 in new[] { mgr.LoadoutLeft, mgr.LoadoutRight, mgr.ExtraWeapon }) {
            var w = mgr.ShownWeapon(w0);
            if(w == SideWeapon.None || !mgr.Owned(w)) continue;
            int f = mgr.FusionOf(w);
            list.Add((f >= 0 ? AugmentDefs.Fusions[f].name : AugmentDefs.WeaponName(w), mgr.WeaponLevel(w)));
        }
        return list;
    }

    void IfRefresh()
    {
        var p = Player.instance; var sm = StatsManager.instance;
        if(p == null || ifRoot == null) return;
        if(ifKey != null && Time.unscaledTime < ifNext) return;
        ifNext = Time.unscaledTime + 0.5f;
        var eq = IfEquipped();
        string key = $"{p.PlayerName}|{p.PlayerID}|{p.Lvl}|{p.exp}|{p.TotalDamagePower}|{(InfiniteStage.Instance != null ? InfiniteStage.Instance.CurrentStage : 0)}|{(EnemyManager.Instance != null ? EnemyManager.Instance.totalKilledEnemy : 0)}|{(GameManager.instance != null ? (int)(GameManager.instance.totalPlayedTime / 60) : 0)}"
            + $"|{(mgr != null ? mgr.RebirthCount + "," + mgr.Stardust + "," + mgr.BestStage : "")}|{string.Join(",", eq.Select(e => e != null ? e.GetInstanceID() : 0))}|{(sm != null ? sm.statPoint + ":" + string.Join(",", IfStats.Select(s => sm.getStatLevel(s.type))) : "")}"
            + $"|{string.Join(",", IfWeapons().Select(w => w.name + w.lv))}|{IfFinal(StatsData.TypeofStat.Damage)}|{IfCollection()}";
        if(key == ifKey) return;
        ifKey = key;
        for(int i = ifRoot.childCount - 1; i >= 0; i--) Destroy(ifRoot.GetChild(i).gameObject);
        IfBuild(eq);
    }

    void IfBuild(List<EquipFrame> eq)
    {
        var p = Player.instance; var sm = StatsManager.instance;
        Color W = Color.white, dimText = Hex("#cfe4ff"), frameBlue = Hex("#056dd8");
        const float T = IfT;
        float sh = IfH - IfBot - 24f - T;
        // 판 · 지붕 · 제목 · 닫기
        IfBox(IfS("Bg"), 0f, T, 1080f, sh, Hex("#0796f8")).raycastTarget = true;
        IfBox(IfS("Top"), 6f, T + 4f, 1068f, 130f, Hex("#0e7ff2"));
        IfBox(IfS("Top/LIght"), 6f, T + 4f, 1068f, 60f, new Color(1f, 1f, 1f, 0.275f));
        IfTx("함선 정보", 540f, T + 68f, 48f, W);
        IfPic(IfS("Button_Close"), 950f, T + 20f, 94f, 96f, W);
        IfPic(IfS("Button_Close/Icon"), 975f, T + 42f, 44f, 44f, W);
        IfHit(950f, T + 20f, 94f, 96f, IfClose);

        // 프로필
        float px = 60f, py = T + 160f;
        IfBox(IfS("Middle/ProfileFrame_Round_l/Bg"), px, py, 200f, 200f, Hex("#0e69f2"));
        var userImg = ifPage.transform.Find(IfP + "Middle/ProfileFrame_Round_l/Bg/Image");
        IfPic(userImg != null && userImg.GetComponent<Image>() != null ? userImg.GetComponent<Image>().sprite : null, px + 28f, py + 28f, 144f, 144f, W);
        IfBox(IfS("Middle/ProfileFrame_Round_l/Border"), px - 3f, py - 3f, 206f, 206f, Hex("#1c1f30"));
        IfTx(UserNameManager.Display(p.PlayerName), px + 240f, py + 40f, 46f, W, 1);
        var idt = IfTx("ID  " + p.PlayerID, px + 240f, py + 100f, 28f, dimText, 1);
        float idw = idt.GetPreferredValues(idt.text, 99999f, 99999f).x;
        float mx = px + 240f + idw + 16f;
        IfPic(IfS("Middle/Text_UserID/Area/ButtonIcon"), mx, py + 80f, 40f, 40f, Hex("#032c7c"));
        IfPic(IfS("Middle/Text_UserID/Area/ButtonIcon/Icon"), mx + 3f, py + 83f, 34f, 33f, W);
        IfHit(mx - 10f, py + 70f, 60f, 60f, () => { GUIUtility.systemCopyBuffer = p.PlayerID; if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX(); Toast("ID 를 복사했어요", Color.white); });
        float req = Mathf.Max(1f, p.getEXPRequired()), frac = Mathf.Clamp01(p.exp / req);
        IfBox(IfS("Middle/Slider_Level/Bg"), px + 290f, py + 146f, 560f, 44f, Hex("#1e1e2a"));
        if(frac > 0f) IfBox(IfS("Middle/Slider_Level/Bg/FillArea/Fill"), px + 296f, py + 151f, Mathf.Max(20f, 548f * frac), 34f, Hex("#3386ff"));
        IfTx($"{frac * 100f:0}% · {p.exp:N0} / {req:N0}", px + 570f, py + 168f, 24f, W);
        IfPic(IfS("Middle/Slider_Level/LevelMark"), px + 236f, py + 132f, 66f, 73f, Hex("#3b67f6"));
        IfTx(p.Lvl.ToString(), px + 269f, py + 168f, 32f, W);

        // 기록 6칸
        float ry = T + 390f, cw = 320f;
        int stage = InfiniteStage.Instance != null ? InfiniteStage.Instance.CurrentStage : 0;
        double hours = GameManager.instance != null ? GameManager.instance.totalPlayedTime / 3600.0 : 0;
        var recs = new (Sprite ic, float iw, float ih, string v, string l, Color tint)[] {
            (IfS("Stats/Group_List/Total Power/Icon"), 64f, 64f, p.TotalDamagePower.ToString("N0"), "전투력", Hex("#e08a00")),
            (IfS("Middle/InfiniteStage/Icon"), 62f, 52f, Mathf.Max(mgr != null ? mgr.BestStage : 0, stage).ToString("N0"), "최고 스테이지", frameBlue),
            (IfS("Middle/Killed Monster/Icon"), 58f, 48f, (EnemyManager.Instance != null ? EnemyManager.Instance.totalKilledEnemy : 0).ToString("N0"), "처치", frameBlue),
            (IfS("Stats/Group_List/Played Time/Icon"), 58f, 50f, hours >= 1 ? $"{(int)hours:N0}시간" : $"{(int)(hours * 60)}분", "플레이", frameBlue),
            (Skin("ItemIcon_Star_Purple"), 56f, 56f, (mgr != null ? mgr.RebirthCount : 0) + "회", "환생", frameBlue),
            (Skin("ItemIcon_Book_1_Purple"), 56f, 56f, IfCollection(), "도감 수집", frameBlue),   // 별의 먼지 대신 (2026-11-28 사용자)
        };
        var frame = IfS("Stats/Group_List/Damage");
        for(int i = 0; i < recs.Length; i++) {
            float x = 40f + (i % 3) * (cw + 20f), y = ry + (i / 3) * 106f;
            var r = recs[i];
            IfBox(frame, x, y, cw, 96f, r.tint);
            IfPic(r.ic, x + 12f, y + 48f - r.ih / 2f, r.iw, r.ih, W);
            IfTx(r.v, x + 22f + r.iw, y + 36f, 32f, W, 1);
            IfTx(r.l, x + 22f + r.iw, y + 72f, 22f, dimText, 1);
        }

        // 장착 장비 5칸 + 세트 효과
        float ey = ry + 226f;
        IfTx("장착 장비", 60f, ey + 22f, 30f, W, 1);
        string setLine = "세트 효과 없음";
        if(EquipSets.Best(out var st, out int now) && now >= 2) {
            int size = EquipSets.SizeOf(st);
            setLine = $"{EquipSets.SetName(st)} {now}/{size} · <color=#ffe14a>{EquipSets.TierText(st, Mathf.Min(now, size))}</color>";
        }
        IfTx(setLine, 1020f, ey + 22f, 26f, Hex("#e6f4ff"), 2);
        for(int i = 0; i < 5; i++) {
            float x = 40f + i * 204f, y = ey + 48f, s = 168f;
            var f = i < eq.Count ? eq[i] : null;
            if(f != null && f.equipData != null) {
                IfPic(MapSprite("mg_cell_" + GenCellKey[Mathf.Clamp((int)f.equipData.Grade, 0, 5)]), x, y, s, s, W).preserveAspect = false;
                IfPic(f.equipData.Sprite_Equip, x + s * 0.14f, y + s * 0.12f, s * 0.72f, s * 0.72f, W);
            } else IfPic(MapSprite("mg_cell_normal"), x, y, s, s, new Color(1f, 1f, 1f, 0.3f)).preserveAspect = false;
        }

        // 출격 무기
        float wy = ey + 232f;
        IfTx("출격 무기", 60f, wy + 22f, 30f, W, 1);
        var ws = IfWeapons();
        for(int i = 0; i < 3 && i < ws.Count; i++) {
            float x = 40f + i * 340f;
            IfBox(frame, x, wy + 46f, 320f, 70f, Hex("#0b4fb0"));
            var nt = IfTx(ws[i].name, x + 22f, wy + 81f, 28f, W, 1);
            float nw = nt.GetPreferredValues(ws[i].name, 99999f, 99999f).x;
            if(nw > 190f) nt.fontSize *= 190f / nw;   // 긴 이름은 줄여요
            IfTx("Lv." + ws[i].lv, x + 300f, wy + 81f, 28f, Hex("#ffe14a"), 2);
        }

        // 능력치 4 × 2
        float hy = wy + 140f;
        IfTx("능력치", 60f, hy + 26f, 30f, W, 1);
        var gray = IfS("Stat Point/Stats UI/Stats Point");
        IfBox(gray, 600f, hy, 210f, 56f, W);
        IfTx($"포인트 <color=#ffe14a>{(sm != null ? sm.statPoint : 0)}</color>", 705f, hy + 28f, 26f, W);
        IfBox(IfS("Stat Point/Stats UI/Reset"), 830f, hy, 210f, 56f, W);
        IfTx("초기화", 935f, hy + 28f, 26f, W);
        IfHit(830f, hy, 210f, 56f, IfReset);
        float y0 = hy + 70f, tw = 235f, th = 150f;
        var upBtn = IfS("Stat Point/Stats Upgrade/Viewport/contents/Damage/UP");
        var upArrow = IfS("Stat Point/Stats Upgrade/Viewport/contents/Damage/UP/Image");
        for(int i = 0; i < IfStats.Length; i++) {
            var sd = IfStats[i];
            float x = 40f + (i % 4) * (tw + 20f), y = y0 + (i / 4) * (th + 12f);
            int lv = sm != null ? sm.getStatLevel(sd.type) : 0, cost = sm != null ? sm.NextCost(sd.type) : -1;
            IfBox(frame, x, y, tw, th, frameBlue);
            IfPic(IfS(sd.icon), x + 10f, y + 10f, 48f, 48f, W);
            var vt = IfTx(IfFinal(sd.type), x + 66f, y + 34f, 28f, Hex("#7dff9a"), 1);
            float vw = vt.GetPreferredValues(vt.text, 99999f, 99999f).x;
            if(vw > 160f) vt.fontSize *= 160f / vw;
            IfTx(sd.name, x + 12f, y + 80f, 20f, dimText, 1);
            IfTx("Lv." + lv, x + 12f, y + 118f, 22f, Hex("#ffe14a"), 1);
            bool can = cost >= 0 && sm != null && sm.statPoint >= cost;
            IfBox(upBtn, x + tw - 118f, y + 94f, 108f, 48f, can ? W : new Color(0.6f, 0.6f, 0.65f));
            IfPic(upArrow, x + tw - 110f, y + 101f, 32f, 32f, can ? W : new Color(1f, 1f, 1f, 0.4f));
            IfTx(cost < 0 ? "MAX" : cost + "P", x + tw - 46f, y + 118f, 22f, W);
            var t2 = sd.type;
            IfHit(x + tw - 118f, y + 94f, 108f, 48f, () => IfUpgrade(t2));
        }
    }

    void IfUpgrade(StatsData.TypeofStat t)
    {
        var sm = StatsManager.instance;
        if(sm == null) return;
        int cost = sm.NextCost(t);
        if(cost < 0) return;
        if(sm.statPoint < cost) { ShowMessage("포인트가 모자라요", $"포인트 {cost}개가 필요해요 (지금 {sm.statPoint}개)\n레벨이 오르면 포인트를 받아요"); return; }
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        sm.UpgradeDirect(t);
        if(Player.instance != null) Player.instance.setStats();   // 최종 능력치 바로 다시 계산
        ifKey = null;
    }

    void IfReset()
    {
        var sm = StatsManager.instance;
        if(sm == null) return;
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        if(sm.UsedPoint <= 0) { ShowMessage("초기화", "아직 쓴 포인트가 없어요"); return; }
        // 비용은 글 대신 금화 배지 + '지금 골드 / 1억' 막대로 (2026-11-28 사용자: 글 읽기 귀찮아)
        long money = GameManager.GetMoney();
        if(money < 100000000) { ShowMessage("골드가 모자라요", "능력치 초기화", icon: "ItemIcon_Money_Coin", have: money, need: 100000000); return; }
        ShowMessage("능력치 초기화", $"쓴 포인트 {sm.UsedPoint}개를 모두 돌려받아요", "초기화", () => { sm.Reset(); ifKey = null; },
            icon: "ItemIcon_Money_Coin", have: money, need: 100000000, cancel: "취소");
    }
}
