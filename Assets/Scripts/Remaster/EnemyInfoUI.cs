using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 적 정보 창 (2026-11-28 사용자: "적 정보 보여 주는 UI 도 바꾸자" + "행성에도 적 정보 버튼" + "마리 수는 빼고 방어력" = previews/dungeon_enemy.png, dungeon_enemy_preview.js)
// - 던전(보라 판) · 행성(파란 판) 같은 창. 위 = 적 칸(던전은 웨이브 번호 · ×마리, 보스 = 빨간 칸 + BOSS), 누른 칸 = 노란 테두리
//   가운데 = 고른 적 크게 (색 빛) + 웨이브·보스 알약, 아래 = 능력치 4칸 (체력 · 이동 속도 · 기절 저항 · 방어력)
// - 처음엔 첫 적이 골라져 있어요 (예전 창은 누르기 전 자리표시 숫자 650,000,000 · 750 이 보였어요)
// - 행성 적 체력·방어력은 실제 전투와 같은 공식 (EnemyManager.PlanetStats) 으로 계산해요
public partial class RemasterUI
{
    public class EnInfo { public Sprite img; public int count; public bool boss; public string tag; public double hp, armor; public float speed, stun; }
    RectTransform enLayer;
    List<EnInfo> enList;
    int enSel;
    bool enDungeon;
    string enTitle, enChip, enRow;
    Color enGlow;
    RectTransform enHost;

    public void ShowEnemyInfo(RectTransform host, bool dungeon, string title, string chip, string row, List<EnInfo> list, Color glow)
    {
        if(host == null || list == null || list.Count == 0) return;
        enHost = host; enDungeon = dungeon; enTitle = title; enChip = chip; enRow = row; enList = list; enGlow = glow; enSel = 0;
        if(softCircle == null) { softCircle = MakeHalo(128); softLine = MakeSoftLine(); dotSprite = MakeDot(32); smallRound = MakeRounded(32, 9); }
        BuildEnemyInfo(true);
    }

    void CloseEnemyInfo()
    {
        if(enLayer != null) Destroy(enLayer.gameObject);
        enLayer = null;
    }

    void BuildEnemyInfo(bool slide)
    {
        if(enLayer != null) Destroy(enLayer.gameObject);
        MapFont();
        enLayer = Stretch("EnemyInfo", enHost);
        enLayer.SetAsLastSibling();
        var dim = Stretch("Dim", enLayer).gameObject.AddComponent<Image>();
        dim.color = new Color(6 / 255f, 2 / 255f, 20 / 255f, 0.62f);
        var db = dim.gameObject.AddComponent<Button>(); db.transition = Selectable.Transition.None; db.onClick.AddListener(CloseEnemyInfo);
        // 창 328 × 520 (미리보기 left 16 · top 118), 화면 가운데
        var holder = Rect("Holder", enLayer, new Vector2(328f * K, 520f * K), Vector2.zero);
        float room = enHost.rect.height - 60f;
        // 창 크기 (2026-11-28 사용자: 전체적으로 좀 줄여 줘) enemy_info_scale(0.85)배
        float hs = Mathf.Min(Mathf.Clamp(Tuning.Get("enemy_info_scale", 0.85f), 0.5f, 1f), room / ((520f + 30f) * K));
        holder.localScale = new Vector3(hs, hs, 1f);
        var s = PxBox("Panel", holder, 0f, 0f, 328f, 520f);
        s.anchorMin = s.anchorMax = new Vector2(0f, 1f);
        var pi = s.gameObject.AddComponent<Image>(); pi.sprite = MapSprite(enDungeon ? "dg_sheet" : "sheet_normal"); pi.raycastTarget = true;
        string box = enDungeon ? "dg_info" : "info";

        // 리본 제목 · 닫기
        float rw = MeasureText("적 정보", 18f) + 44f;
        Plate(s, "mg_title", 164f - rw / 2f, -18f, rw, 36f, 0f);
        BtnText(s, "적 정보", 164f, 0f, 18f, TextAlignmentOptions.Center, 0f, Hex("#10142a"));
        Plate(s, "btn_close", 328f - 10f - 38f, -16f, 38f, 38f, 7f);
        var cl = PxBox("Close", s, 328f - 10f - 38f, -16f, 38f, 38f);
        cl.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        cl.gameObject.AddComponent<Button>().onClick.AddListener(CloseEnemyInfo);
        // 이름 · 칩 · 줄 이름
        PxText(s, enTitle, 16f, 38f, 16f, Color.white, 2f, Navy, TextAlignmentOptions.Left);
        float cw = 1.5f + 9f + MeasureText(enChip, 13f) + 9f + 1.5f;
        Chip(s, "chip_dark", null, enChip, 328f - 16f - cw, 31f);   // (2026-11-28 사용자: 조금 내려 줘) 26 → 31
        PxText(s, enRow, 16f, 67f, 11f, Hex(enDungeon ? "#c9b8ff" : "#9fb4ff"), 0f, null, TextAlignmentOptions.Left);

        // 적 칸 (58, 사이 6, 가운데 정렬 · 6개 넘으면 좁혀요)
        int n = enList.Count;
        float tile = 58f, step = Mathf.Min(64f, (328f - 14f - tile) / Mathf.Max(1, n - 1));
        float x0 = 164f - ((n - 1) * step + tile) / 2f;
        for(int i = 0; i < n; i++) {
            var e = enList[i]; int ii = i;
            float x = x0 + i * step, y = 84f;
            if(i == enSel) Plate(s, "en_tile_sel", x, y, tile, tile, 12f);
            Plate(s, e.boss ? "en_tile_boss" : "en_tile", x, y, tile, tile, 0f);
            var im = PxBox("Img", s, x + tile * 0.08f, y + tile * 0.06f, tile * 0.84f, tile * 0.84f).gameObject.AddComponent<Image>();
            im.sprite = e.img; im.preserveAspect = true; im.raycastTarget = false; if(e.img == null) im.color = Color.clear;
            if(e.count > 0) PxText(s, "×" + e.count, x + tile - 3f, y + tile - 9f, 12f, Color.white, 2f, Navy, TextAlignmentOptions.Right);
            if(e.boss) {
                Plate(s, "en_pill", x + tile / 2f - 20f, y - 11f, 40f, 16f, 0f);
                PxText(s, "BOSS", x + tile / 2f, y - 3f, 10f, Color.white, 1f, Hex("#5a0010"));
            }
            var hit = PxBox("Pick" + i, s, x, y, tile, tile);
            hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            hit.gameObject.AddComponent<Button>().onClick.AddListener(() => { if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX(); enSel = ii; BuildEnemyInfo(false); });
        }

        // 고른 적 크게
        var sel = enList[Mathf.Clamp(enSel, 0, n - 1)];
        Plate(s, box, 16f, 160f, 296f, 170f, 0f);
        Halo(s, 164f, 160f + 100f, 230f, new Color(enGlow.r, enGlow.g, enGlow.b, 0.33f));
        // 크기는 그림 크기에 맞춰요 (2026-11-28 사용자: 작은 몬스터를 너무 크게 하지 마요) — 목록에서 가장 큰 그림 = 150, 나머지는 그 비율 (최소 enemy_info_min 60)
        float maxDim = enList.Max(x => x.img != null ? Mathf.Max(x.img.rect.width, x.img.rect.height) : 0f);
        float myDim = sel.img != null ? Mathf.Max(sel.img.rect.width, sel.img.rect.height) : maxDim;
        float bs = maxDim > 0f ? Mathf.Clamp(150f * myDim / maxDim, Tuning.Get("enemy_info_min", 60f), 150f) : 150f;
        var big = PxBox("Big", s, 164f - bs / 2f, 170f + 150f - bs, bs, bs).gameObject.AddComponent<Image>();   // 발이 같은 높이에 오게 아래 맞춤
        big.sprite = sel.img; big.preserveAspect = true; big.raycastTarget = false; if(sel.img == null) big.color = Color.clear;
        if(!string.IsNullOrEmpty(sel.tag)) {
            float tw = MeasureText(sel.tag, 12f) + 18f;
            Plate(s, "en_pill", 28f, 170f, tw, 22f, 0f);
            PxText(s, sel.tag, 28f + tw / 2f, 181f, 12f, Color.white, 1f, Hex("#5a0010"));
        }
        if(sel.count > 0) PxText(s, "×" + sel.count, 312f - 12f, 181f, 13f, Color.white, 2f, Navy, TextAlignmentOptions.Right);

        // 능력치 4칸 (148 × 64)
        string spd = sel.speed < 0.3f ? "느림" : sel.speed < 0.6f ? "보통" : "빠름";
        EnStat(s, box, 12f, 342f, "ItemIcon_Heart_Red", "체력", KNum(sel.hp), null);
        EnStat(s, box, 168f, 342f, "ResourceBar_Single_Icon_Energy", "이동 속도", sel.speed.ToString("0.##"), spd);
        EnStat(s, box, 12f, 414f, "Icon_Timer", "기절 저항", $"{sel.stun:0.#}%", null);
        EnStat(s, box, 168f, 414f, "Icon_Shield", "방어력", sel.armor > 0 ? KNum(sel.armor) : "없음", null);

        // 화면 가운데 (아래 기준이 아니라 가운데), 처음 열 때 아래에서 올라와요
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f); holder.pivot = new Vector2(0.5f, 0.5f);
        holder.anchoredPosition = Vector2.zero;
        CrispTexts(enLayer, true);
        if(slide) StartCoroutine(SlideIn(holder, hs));
    }

    void EnStat(Transform s, string box, float x, float y, string icon, string label, string val, string sub)
    {
        Plate(s, box, x, y, 148f, 64f, 0f);
        PxIcon(s, icon, x + 10f + 15f, y + 32f, 30f);
        PxText(s, label, x + 48f, y + 16f, 11f, Hex(enDungeon ? "#c9b8ff" : "#9fb4ff"), 0f, null, TextAlignmentOptions.Left);
        PxText(s, val, x + 48f, y + 40f, 19f, Color.white, 2f, Navy, TextAlignmentOptions.Left);
        if(sub != null) PxText(s, sub, x + 148f - 10f, y + 41f, 11f, Hex("#9fe8ff"), 0f, null, TextAlignmentOptions.Right);
    }

    // ---- 던전: 웨이브 순서 (DungeonEnimes[i] × spawnAmount[i])
    void ShowDungeonEnemies(DungeonData d, int colorIndex)
    {
        if(d == null || d.DungeonEnimes == null || dsFrame == null) return;
        var list = new List<EnInfo>();
        for(int i = 0; i < d.DungeonEnimes.Length; i++) {
            var e = d.DungeonEnimes[i];
            if(e == null) continue;
            bool boss = e._typeofEnemy == Enemy.typeofEnemy.DungeonBoss;
            int c = d.spawnAmount != null && i < d.spawnAmount.Length ? d.spawnAmount[i] : 0;
            list.Add(new EnInfo { img = e.RawImage, count = c, boss = boss, tag = $"{i + 1}웨이브" + (boss ? " · 보스" : ""), hp = e.EnemyHealth, armor = 0, speed = e.enemyMovingSpeed, stun = e.StunResistence });
        }
        ShowEnemyInfo(dsFrame, true, DnName(d), $"출현 {d.getTotalEnemy()}마리 · 웨이브 {list.Count}번", "웨이브 순서 (눌러서 보기)", list, Hex(DsCol[Mathf.Clamp(colorIndex, 0, 4)]));
    }

    // ---- 행성: 나오는 적 종류 + 보스. 체력·방어력 = EnemyManager.PlanetStats 와 같은 공식
    void ShowPlanetEnemies(int dim, int p, int m)
    {
        var sm = StageManager.instance;
        if(sm == null || p < 0 || p >= sm.planets.Count || mapFrame == null) return;
        var planet = sm.planets[p]; var st = planet.StagesOfThisPlanet[m];
        int stage = StageManager.EquivStageOf(p, m);
        int target = StageManager.EnemyTargetOf(planet, st, dim);
        var rules = PlanetDimension.Rules(p, m, dim);
        double common = PlanetDimension.HpMul(dim, p) * PlanetDimension.RuleHp(rules) * RemasterBalance.NumberScale(stage)
                        * Mathf.Pow(Mathf.Max(1f, Tuning.Get("planet_map_hp_growth", 1f)), m)
                        * Mathf.Min(1f, Tuning.Get("planet_early_hp_base", 1f) + p * Tuning.Get("planet_early_hp_step", 0.15f));
        var list = new List<EnInfo>();
        foreach(var e in (st.enemiesPrefab ?? new Enemy[0]).Where(x => x != null).GroupBy(x => x.RawImage).Select(g => g.First())) {
            float variety = 0.9f + 0.2f * (((int)e.typePool & 0xff) / 255f);
            double hp = RemasterBalance.EnemyHp(stage) * variety * common * Tuning.Get("planet_hp_mul", 1f);
            list.Add(new EnInfo { img = e.RawImage, count = 0, boss = false, tag = null, hp = hp, armor = Armor.ForStage(stage, false), speed = e.enemyMovingSpeed * PlanetDimension.RuleSpeed(rules), stun = e.StunResistence });
        }
        foreach(var b in (st.bossPrefab ?? new Enemy[0]).Where(x => x != null)) {
            double hp = RemasterBalance.EnemyHp(stage) * System.Math.Max(RemasterBalance.BossMul, target * (double)Tuning.Get("planet_boss_hp_share", 0.3f)) * common * Tuning.Get("planet_boss_hp_mul", 1f);
            list.Add(new EnInfo { img = b.RawImage != null ? b.RawImage : BossSprite(b), count = 1, boss = true, tag = "보스", hp = hp, armor = Armor.ForStage(stage, true), speed = b.enemyMovingSpeed * PlanetDimension.RuleSpeed(rules), stun = b.StunResistence });
        }
        int bosses = st.bossPrefab != null ? st.bossPrefab.Length : 0;
        ShowEnemyInfo(mapFrame, false, $"{PlanetTitle(dim, p)} {st.StageName}", bosses > 0 ? $"출현 {target}마리 · 보스 {bosses}" : $"출현 {target}마리", "나오는 적 (눌러서 보기)", list,
            dim == PlanetDimension.Abyss ? Hex("#ff3b4f") : dim == PlanetDimension.Parallel ? Hex("#c27dff") : Hex("#36c6ff"));
    }
}
