using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 뽑기 연출 = 별똥별 (2026-11-27 사용자: 장면 그림 previews/gacha_fx.png 의 B "별똥별로, 좋은 게 나오면 별똥별 색·개수가 다르게")
// 1 밤하늘: 화면이 어두워지고 작은 별들
// 2 별똥별: 수 = 일반·레어 1 · 에픽 2 · 유니크 3 · 전설 4 · 고대 5, 색은 떨어지는 순서대로 한 등급씩 올라가 마지막 = 가장 좋은 등급 (가장 크게)
//    부딪힐 때마다 충격파 링 + 반짝이 + 흔들림
// 3 마지막 쾅: 번쩍 + 큰 흔들림 + 반짝이 사방
// 4 큰 카드 (1회 · 또는 여러 번에서 대박이면 가장 좋은 것) + 대박이면 '전설!' 띠가 화면을 가로질러요
// 5 여러 번: 카드가 하나씩 날아가 자리에 (좋은 것 마지막) · 대박 도장 (일반 에픽/유니크, 스페셜 유니크/전설+, 강화석 전설/고대)
// 6 한 번 더 · 확인. 연출 중 화면을 누르면 바로 결과로
// 그림: 게임 것 (빛줄기 mg_rays · 빛무리 mg_burst · 반짝이 mg_spark · 번쩍 mg_flash · 등급 칸·띠·버튼) + 코드로 그린 별똥별 꼬리·충격파 링·띠
// gacha_fx 0 이면 예전 연출 (GachaManager.StartAnimation)
public partial class RemasterUI
{
    public static bool GachaFxBusy;   // 연출 중 (뽑기 창 숨김 · 자동 분해 기다림)
    public static bool GachaFxOn => Tuning.Get("gacha_fx", 1f) > 0f;
    bool fxSkip, fxNoSkip;   // fxNoSkip = 지금은 눌러도 안 건너뛰는 구간 (전설·고대의 금빛 강림 ~ 카드)

    struct FxItem { public Sprite sp; public int g; public string name, sub; }

    // GachaManager.StartAnimation 에서 불러요 (장비는 이미 가방에 들어간 뒤)
    public void PlayGachaFx(bool equip, List<EquipFrame> got, int[] stoneNum, GachaData data, Sprite chest)
    {
        if(!EnsureBuilt()) return;
        var items = new List<FxItem>();
        if(equip && got != null) {
            foreach(var f in got) {
                if(f == null || f.equipData == null) continue;
                var e = f.equipData;
                string part = e.TypeOfEquip switch { Equips.Type.Ring => "반지", Equips.Type.Necklace => "목걸이", Equips.Type.Book => "고서", Equips.Type.Accessory => "장신구", _ => "유물" };
                items.Add(new FxItem { sp = e.Sprite_Equip, g = Mathf.Clamp((int)e.Grade, 0, 5), name = e.EquipName, sub = part });
            }
        } else if(stoneNum != null) {
            for(int i = 0; i < stoneNum.Length && i < 6; i++) if(stoneNum[i] > 0) items.Add(new FxItem { sp = null, g = i, name = MgName[i] + " 강화석", sub = "+" + stoneNum[i].ToString("N0") });
        }
        if(items.Count == 0) { Debug.LogWarning("[뽑기 연출] 결과 없음"); return; }
        items = items.OrderBy(x => x.g).ToList();   // 좋은 것이 마지막
        var type = data != null ? data.gacha : GachaData.GachaType.Normal;
        int big, huge;   // 대박 · 초대박 등급
        if(type == GachaData.GachaType.Normal || type == GachaData.GachaType.Normalx10) { big = 2; huge = 3; }
        else if(type == GachaData.GachaType.Special || type == GachaData.GachaType.Specialx10) { big = 3; huge = 4; }
        else { big = 4; huge = 5; }
        GachaFxBusy = true;
        StartCoroutine(GachaFxRun(items, big, huge, chest, data));
    }

    static float FxBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; t -= 1f; return 1f + c3 * t * t * t + c1 * t * t; }   // 살짝 넘쳤다 돌아오기
    Color FxCol(int g, float a = 1f) { var c = Hex(MgGlow[Mathf.Clamp(g, 0, 5)]); c.a = a; return c; }
    float FxDt() => fxSkip ? 1f : Time.unscaledDeltaTime;   // 건너뛰면 한 번에

    // 별똥별 연출 (2026-11-27 사용자: B 별똥별, 좋은 게 나오면 별똥별 색·개수가 다르게)
    //  별똥별 수 = 1 + (가장 좋은 등급 - 레어) → 일반·레어 1 · 에픽 2 · 유니크 3 · 전설 4 · 고대 5
    //  색은 떨어지는 순서대로 한 등급씩 올라가 마지막이 가장 좋은 등급 (마지막이 가장 크고 쾅)
    IEnumerator GachaFxRun(List<FxItem> items, int big, int huge, Sprite chest, GachaData data)
    {
        MapFont();
        fxSkip = false;
        for(int i = modalRoot.childCount - 1; i >= 0; i--) if(modalRoot.GetChild(i).name == "GachaFx") Destroy(modalRoot.GetChild(i).gameObject);
        var layer = Stretch("GachaFx", modalRoot);
        layer.SetAsLastSibling();
        // 밤하늘 (2026-11-27 사용자: 배경을 제안 그림 B 처럼): 화면 전체를 가운데가 밝은 남색 하늘로 (뒤 게임 화면은 안 보여요)
        var sky = Stretch("Sky", layer);
        var skyImg = sky.gameObject.AddComponent<Image>();
        skyImg.sprite = FxSkyTex(); skyImg.color = new Color(1f, 1f, 1f, 0f);   // 뒤 화면 터치도 막아요
        var shake = Rect("Shake", layer, new Vector2(1080, 780 * K), Vector2.zero);
        var box = Rect("Box", shake, new Vector2(1080, 780 * K), Vector2.zero);
        float fit = MgFit(1f);
        box.localScale = new Vector3(fit, fit, 1f);
        int best = items.Max(x => x.g);
        bool single = items.Count == 1, jackpot = best >= big;
        const float cy = 330f;   // 떨어지는 곳 = 화면 가운데쯤 (미리보기 px)

        // 별: 화면 밖까지 넉넉히 · 반짝반짝
        var stars = PxBox("Stars", box, -60f, -60f, 480f, 900f);
        var starList = new List<Image>();
        for(int i = 0; i < 90; i++) {
            float z = Random.Range(2.5f, 7f);
            var s = FxImg(stars, MapSprite("mg_spark"), Random.Range(0f, 480f), Random.Range(0f, 900f), z, z, new Color(1f, 1f, 1f, Random.Range(0.3f, 0.9f)));
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            starList.Add(s);
        }
        StartCoroutine(FxTwinkleStars(starList));
        // 누르면 건너뛰기: 연출 동안 맨 위에 투명한 판 (결과가 나오면 치워서 버튼이 눌리게)
        var skipHit = Stretch("Skip", layer);
        skipHit.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        // 구간 스킵 (2026-11-27 사용자 "ㄱㄱ"): 별똥별 동안 누르면 → 일반~유니크는 바로 카드 / 전설·고대는 금빛 강림 시작으로
        //  금빛 강림 → 불꽃·신의 각성 → 카드는 끝까지 보여 줘요. 10회 큰 카드·카드 정리는 누르면 바로
        fxNoSkip = false;
        skipHit.gameObject.AddComponent<Button>().onClick.AddListener(() => { if(!fxNoSkip) fxSkip = true; });
        float t = 0f;
        while(t < 0.4f) { t += FxDt(); skyImg.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.4f)); yield return null; }
        skyImg.color = Color.white;
        { float w0 = 0f; while(w0 < 0.5f && !fxSkip) { w0 += Time.unscaledDeltaTime; yield return null; } }   // 밤하늘을 잠깐 (길게, 사용자)

        // 별똥별들 (왼쪽 위 · 오른쪽 위 번갈아), 사이 0.32초
        // (2026-11-27 사용자: 유니크부터는 다 똑같이 — 이 연출이 나오면 유니크 이상인 건 알지만 어떤 등급인지는 몰라요)
        //  유니크 이상 = 별똥별 3개(하늘 → 보라 → 금빛) + 금빛 강림. 그 뒤 전설·고대만 추가 연출 하나씩
        bool golden = best >= 4;   // 금빛 강림 = 전설·고대 공통 (2026-11-27 사용자: 유니크는 특별 연출 필요 없어)
        int n = golden ? 4 : 1 + Mathf.Max(0, best - 1);   // 전설·고대 = 하늘 → 보라 → 노랑(유니크와 똑같이) → 금빛 강림 (2026-11-27 사용자: 노란 별도 떨어뜨려 줘)   // 유니크도 3개 (하늘 → 보라 → 금) → 개수로는 유니크인지 전설 이상인지 몰라요
        for(int i = 0; i < n; i++) {
            bool last = i == n - 1;
            // 구간 스킵 (2026-11-27 사용자): 유니크 이상은 누르면 '노란 별똥별'로 건너뛰고 거기서부터 끝까지 (노란 별까지는 셋이 똑같아서 긴장감이 남아요)
            bool yellow = best >= 3 && i == 2;
            if(fxSkip && !last && !yellow) continue;   // 건너뛰면 남은 별똥별은 안 떨어뜨려요
            if(yellow) {
                if(fxSkip) { yield return null; yield return null; }   // 이미 떨어지던 별똥별은 한 번에 끝나게
                fxSkip = false; fxNoSkip = true;
            }
            int g = golden ? (last ? 3 : 1 + i) : best <= 1 ? best : Mathf.Min(best, 1 + i);   // 노란(유니크) 별똥별까지는 유니크와 같아요   // 전설·고대도 마지막 별똥별 색은 금(유니크)
            Vector2 hit = last ? new Vector2(180f, cy) : new Vector2(180f + Random.Range(-70f, 70f), cy + Random.Range(-60f, 40f));
            // 금빛 강림 (전설·고대 공통): 금빛 하늘 + "신의 빛이 내려와요…" → 금빛 빛기둥 → 큰 금빛 별똥별이 빛기둥 따라 똑바로 천천히
            bool divine = last && golden;
            if(divine) yield return FxAncientIntro(box, shake, layer, hit);
            StartCoroutine(FxMeteor(box, shake, hit, g, last ? (divine ? 2.8f : 2.2f) : 1.3f + 0.1f * i, i % 2 == 0, last, layer, divine));
            float w = 0f; float gap = last ? (divine ? 1.65f : 1.25f) : 0.55f;   // 사이 0.55초 · 마지막은 떨어지는 시간 + 부딪힘 (길게, 사용자)
            while(w < gap && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; }
        }

        // 마지막 쾅: 번쩍 · 큰 흔들림 · 반짝이 사방
        var flash = Stretch("Flash", layer);
        var fimg = flash.gameObject.AddComponent<Image>();
        fimg.sprite = MapSprite("mg_flash"); fimg.raycastTarget = false;
        fimg.color = jackpot ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        StartCoroutine(FxFade(fimg, jackpot ? 0.45f : 0.3f));
        StartCoroutine(FxShake(shake, jackpot ? 18f : 9f, jackpot ? 0.45f : 0.25f));
        if(SoundManager.Instance != null) { if(jackpot) SoundManager.Instance.playAchieveSFX(); else SoundManager.Instance.playBuffSFX(); }
        int sparks = best >= huge ? 28 : jackpot ? 20 : 12;
        // (2026-11-27 사용자: 별 임팩트가 부자연스러워 → 사방으로 튀는 반짝이는 빼요)
        { float w0 = 0f; while(w0 < 0.45f && !fxSkip) { w0 += Time.unscaledDeltaTime; yield return null; } }
        // 전설·고대만 한 번 더 (사용자: 마지막 별똥별 뒤 전설이랑 고대는 다른 연출을 하나씩 더)
        if(best == 4) yield return FxLegendBurst(box, shake, layer, new Vector2(180f, cy));
        else if(best >= 5) yield return FxAncientAwaken(box, shake, layer, new Vector2(180f, cy));

        // 카드 뒤집기는 짧게라도 보여 줘요 (앞에서 건너뛰었어도)
        fxSkip = false;
        // 큰 카드 (1회 · 또는 여러 번에서 대박일 때 가장 좋은 것을 먼저 크게)
        var top = items[items.Count - 1];
        RectTransform bigGroup = null;
        if(single || jackpot) {
            bigGroup = PxBox("Big", box, 0f, 0f, 360f, 780f);
            // 뒤 빛줄기는 움직이지 않아요 (2026-11-27 사용자: "360도로 퍼지는 임팩트가 또 막 움직여")
            const float S = 100f;   // 카드 크기 (사용자: 너무 크게 말고 적당히 — 140 → 100)
            Plate(bigGroup, "mg_rays", 180f - 230f, cy - 230f, 460f, 460f, 0f).color = FxCol(top.g, 0.5f);
            Plate(bigGroup, "mg_burst", 180f - 90f, cy - 90f, 180f, 180f, 0f).color = FxCol(top.g, 0.75f);
            var card = PxBox("Card", bigGroup, 180f - S / 2f, cy - S / 2f, S, S);
            yield return FxReveal(bigGroup, card, top, S, big, huge, Vector2.zero, true);
            float bandTop = cy + S / 2f + 26f;
            var bt = GameFont(PxText(bigGroup, $"{MgName[top.g]} · {top.sub}", 180f, bandTop + 15.5f, 16f, Color.white, 1.3f, MgEdge), true, MgEdge);
            float bw = bt.GetPreferredValues(bt.text).x / K + 44f;
            Plate(bigGroup, "mg_band_" + MgKey[top.g], 180f - bw / 2f, bandTop, bw, 31f, 0f).transform.SetSiblingIndex(bt.transform.GetSiblingIndex());
            GameFont(PxText(bigGroup, top.name, 180f, bandTop + 54f, 20f, Color.white, 1.6f, MgEdge), true, MgEdge);
            if(jackpot) yield return FxCutIn(bigGroup, $"{MgName[top.g]}!", cy - 150f, FxCol(top.g), top.g >= huge);
            fxNoSkip = false;   // 여기부터는 다시 누르면 넘어가요
            if(!single) {
                // 잠깐 보여 주고 (누르면 바로) 모두 보기로
                GameFont(PxText(bigGroup, "누르면 모두 보기", 180f, cy + 200f, 12f, Hex("#cfe2ff")), false);
                float w = 0f; while(w < 2.6f && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; }
                Destroy(bigGroup.gameObject); bigGroup = null;
                foreach(var gph in fxDivineRings) if(gph != null) StartCoroutine(FxFadeGraphic(gph, 0.3f));
                fxDivineRings.Clear();
            }
        }
        fxNoSkip = false;
        fxSkip = false;   // 카드 정리는 한 장씩 (다시 누르면 남은 카드가 한 번에)

        // 여러 번: 카드가 가운데에서 하나씩 날아가 자리에 (좋은 것 마지막)
        float by;
        if(!single) {
            int nn = Mathf.Min(items.Count, 10), cols = 5;
            float s = 58f, gap = 8f, x0 = 180f - (cols * s + (cols - 1) * gap) / 2f, y0 = cy - 60f;
            for(int i = 0; i < nn; i++) {
                var it = items[i];
                float x = x0 + (i % cols) * (s + gap), y = y0 + (i / cols) * (s + 34f);
                var card = PxBox("Card", box, x, y, s, s);
                StartCoroutine(FxReveal(box, card, it, s, big, huge, new Vector2(180f * K - card.anchoredPosition.x - s * K / 2f, -(cy * K) - card.anchoredPosition.y + s * K / 2f), false));   // 뒷면으로 날아가 뒤집혀요
                GameFont(PxText(box, MgName[it.g], x + s / 2f, y + s + 12f, 11f, FxCol(it.g), 1f, MgEdge), true, MgEdge);
                if(SoundManager.Instance != null && !fxSkip) SoundManager.Instance.playClickSFX();
                float w2 = 0f; while(w2 < 0.12f && !fxSkip) { w2 += Time.unscaledDeltaTime; yield return null; }
            }
            if(items.Count > nn) GameFont(PxText(box, $"+{items.Count - nn}개 더", 180f, y0 + 2f * (s + 34f) + 6f, 12f, Hex("#e6efff")), false);
            string head = best >= huge ? "초대박!!" : jackpot ? "대박!" : "뽑기 결과";
            var ht = GameFont(PxText(box, head, 180f, cy - 140f, jackpot ? 34f : 26f, jackpot ? FxCol(best) : Color.white, 2.2f, MgEdge), true, MgEdge);
            StartCoroutine(FxPop(ht.rectTransform, 1.3f));
            by = y0 + 2f * (s + 34f) + 24f;
        } else by = cy + 50f + 26f + 54f + 36f;   // 카드 아래 → 띠 → 이름 → 버튼
        // 화면 여기저기 반짝이는 별은 빼요 (사용자: 장비 있는 곳에만 빛나게)

        // 한 번 더 · 확인 (건너뛰기 판은 치워요)
        if(skipHit != null) Destroy(skipHit.gameObject);
        Plate(box, "mg_btn_p2", 55f, by, 120f, 46f, 3f);
        GameFont(PxText(box, "한 번 더", 115f, by + 23f, 16f, Color.white, 1.3f, MgEdge), true, MgEdge);
        JobHit(box, "Again", 55f, by, 120f, 46f, () => { bool test = fxTest; FxClose(layer); if(test) DebugGachaFx(fxTestBest, fxTestCount); else FxAgain(data); });   // 시험이면 같은 시험을 다시
        Plate(box, "mg_btn_y2", 185f, by, 120f, 46f, 3f);
        GameFont(PxText(box, "확인", 245f, by + 23f, 18f, Color.white, 1.3f, MgEdge), true, MgEdge);
        JobHit(box, "Ok", 185f, by, 120f, 46f, () => { if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX(); FxClose(layer); });
    }

    // 가운데 기준 그림 (미리보기 px)
    Image FxImg(Transform p, Sprite s, float cx, float cy, float w, float h, Color c)
    {
        var im = PxAt("I", p, cx, cy, w, h).gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.raycastTarget = false;
        return im;
    }

    // 별똥별 하나 (2026-11-27 사용자: 더 크고 화려하게): 꼬리 3겹(바깥 빛 · 색 · 하얀 심) + 머리(빛무리 · 도는 별 · 십자 빛)
    //  떨어지는 동안 꼬리에서 불티가 흩날려요 (전설·고대는 무지개 불티). 마지막 별똥별은 조금 느리고 부딪힐 때 등급 색으로 화면 번쩍
    //  size: 보통 1.4 · 마지막 2.2. fromLeft = 왼쪽 위에서
    IEnumerator FxMeteor(RectTransform box, RectTransform shake, Vector2 hit, int g, float size, bool fromLeft, bool last, RectTransform layer, bool divine = false)
    {
        Color col = divine ? new Color(1f, 0.85f, 0.35f) : FxCol(g);
        Vector2 dir = divine ? new Vector2(0f, 1f) : new Vector2(fromLeft ? 0.62f : -0.62f, 1f).normalized;   // 미리보기 px (아래 +). 고대 = 똑바로 아래로
        Vector2 start = hit - dir * 560f;
        float ang = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;   // 화면 각도 (위 +)
        var parts = new List<RectTransform>();
        RectTransform Tail(float len, float wid, Color c) { var im = FxImg(box, FxTrailTex(), start.x, start.y, len, wid, c); var rt = im.rectTransform; rt.pivot = new Vector2(1f, 0.5f); rt.localRotation = Quaternion.Euler(0f, 0f, ang); parts.Add(rt); return rt; }
        Tail(380f * size, 54f * size, new Color(col.r, col.g, col.b, 0.35f));   // 바깥 빛
        Tail(330f * size, 24f * size, new Color(col.r, col.g, col.b, 0.95f));   // 색
        Tail(220f * size, 8f * size, Color.white);                                // 하얀 심
        var halo = FxImg(box, MapSprite("mg_burst"), start.x, start.y, 120f * size, 120f * size, new Color(col.r, col.g, col.b, 0.95f)); parts.Add(halo.rectTransform);
        var cross = FxImg(box, MapSprite("mg_spark"), start.x, start.y, 78f * size, 78f * size, new Color(1f, 1f, 1f, 0.7f)); parts.Add(cross.rectTransform);
        var star = FxImg(box, MapSprite("mg_spark"), start.x, start.y, 48f * size, 48f * size, Color.white); parts.Add(star.rectTransform);
        cross.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        float t = 0f, dur = divine ? 1.45f : last ? 1.05f : 0.75f, emit = 0f;   // 길게 (사용자). 고대는 더 천천히
        while(t < dur) {
            float dt = FxDt();
            t += dt;
            float k = Mathf.Clamp01(t / dur), e = k * k;   // 점점 빨라져요
            Vector2 p = Vector2.Lerp(start, hit, e);
            var ap = new Vector2(p.x * K, -p.y * K);
            foreach(var rt in parts) if(rt != null) rt.anchoredPosition = ap;
            star.rectTransform.Rotate(0f, 0f, 600f * Time.unscaledDeltaTime);
            cross.rectTransform.Rotate(0f, 0f, -260f * Time.unscaledDeltaTime);
            float pulse = 1f + 0.12f * Mathf.Sin(t * 40f);
            halo.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            // 불티: 머리 뒤에서 흩날려요
            emit += Time.unscaledDeltaTime;
            while(!fxSkip && emit > 0.016f) {
                emit -= 0.016f;
                // 불티 색: 전설 = 불꽃(빨강~노랑), 고대 = 금빛·하양 신성한 빛, 그 밖 = 하양~등급 색
                Color c2 = divine ? Color.Lerp(Color.white, new Color(1f, 0.8f, 0.3f), Random.Range(0f, 1f))
                    : g == 4 ? Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(1f, 0.9f, 0.3f), Random.value)
                    : Color.Lerp(Color.white, col, Random.Range(0.3f, 1f));
                StartCoroutine(FxEmber(box, p - dir * Random.Range(0f, 30f) + Random.insideUnitCircle * 10f * size, -dir, c2, Random.Range(8f, 18f) * size));
            }
            yield return null;
        }
        foreach(var rt in parts) if(rt != null) Destroy(rt.gameObject);
        // 부딪힘: 링 두 겹 + 반짝이 + 흔들림 (마지막은 화면이 등급 색으로 번쩍)
        if(SoundManager.Instance != null && !last) SoundManager.Instance.playCoinSFX();
        StartCoroutine(FxShake(shake, (last ? 10f : 6f) * size, last ? 0.35f : 0.2f));
        StartCoroutine(FxRing(box, hit, col, 190f * size, last ? 0.65f : 0.45f));
        StartCoroutine(FxRing(box, hit, Color.white, 120f * size, 0.35f));
        if(last) {
            StartCoroutine(FxRing(box, hit, col, 330f * size, 0.9f));
            if(divine) StartCoroutine(FxRing(box, hit, Color.white, 260f * size, 0.75f));   // 금빛 강림은 링 한 겹 더
            if(divine) FxDivineEnd();
            var cf = Stretch("ColorFlash", layer);
            var ci = cf.gameObject.AddComponent<Image>(); ci.raycastTarget = false; ci.sprite = MapSprite("mg_flash");
            ci.color = new Color(col.r, col.g, col.b, 0.55f);
            StartCoroutine(FxFadeColor(ci, 0.5f));
        }
        int sp = last ? 0 : 10;
        // 부딪힐 때 반짝이도 빼요 (링만)
    }
    // ---- 전설: 불꽃 각성 (금빛 쾅 다음, 2026-11-27 사용자: 더 화려하게)
    //  하늘이 붉게 물들고 → 심장 박동 쿵·쿵·쾅! (붉은 → 주황 → 하얀 충격파, 점점 세게) → 마지막 쾅에 카드 뒤 붉은 빛무리가 피어나고
    //  떨어진 곳에서 불티가 기둥처럼 위로 솟구쳐요. 쾅과 함께 곧바로 카드 (기다리지 않아요)
    IEnumerator FxLegendBurst(RectTransform box, RectTransform shake, RectTransform layer, Vector2 c)
    {
        Color red = new Color(1f, 0.22f, 0.18f), ember = new Color(1f, 0.55f, 0.2f);
        // 붉은 하늘
        var tint = Stretch("RedSky", layer);
        tint.SetSiblingIndex(shake.GetSiblingIndex());   // 하늘 위 · 내용 아래
        var ti = tint.gameObject.AddComponent<Image>(); ti.sprite = MapSprite("mg_flash"); ti.raycastTarget = false; ti.color = new Color(red.r, red.g, red.b, 0f);
        float t = 0f;
        while(t < 0.3f) { t += FxDt(); ti.color = new Color(red.r, red.g, red.b, 0.3f * Mathf.Clamp01(t / 0.3f)); yield return null; }
        // 쿵 · 쿵 · 쾅!
        Color[] ringCol = { red, ember, Color.white };
        float[] ringSize = { 380f, 480f, 640f }, shakeAmp = { 10f, 14f, 22f }, gap = { 0.12f, 0.12f, 0f }, flashA = { 0.35f, 0.45f, 0.7f };
        for(int k = 0; k < 3; k++) {
            // 박동마다 붉은 불꽃 별똥별이 꽂혀요 (2026-11-27 사용자: 전설도 살짝 바꿔 줘) — 왼쪽 · 오른쪽 · 마지막은 양쪽에서 동시에
            if(k < 2) StartCoroutine(FxMeteor(box, shake, c + new Vector2(k == 0 ? -18f : 18f, -10f), 4, 1.5f + 0.2f * k, k == 0, false, layer));
            else { StartCoroutine(FxMeteor(box, shake, c + new Vector2(-14f, 0f), 4, 2f, true, false, layer)); StartCoroutine(FxMeteor(box, shake, c + new Vector2(14f, 0f), 4, 2f, false, false, layer)); }
            { float wm = 0f; while(wm < 0.74f) { wm += FxDt(); yield return null; } }   // 꽂히는 순간에 쿵
            var fl = Stretch("RedPulse", layer); var fi = fl.gameObject.AddComponent<Image>(); fi.raycastTarget = false; fi.sprite = MapSprite("mg_flash");
            fi.color = k == 2 ? new Color(1f, 0.85f, 0.7f, flashA[k]) : new Color(red.r, red.g, red.b, flashA[k]);
            StartCoroutine(FxFadeColor(fi, k == 2 ? 0.5f : 0.3f));
            StartCoroutine(FxRing(box, c, ringCol[k], ringSize[k], k == 2 ? 0.8f : 0.55f));
            if(k == 2) StartCoroutine(FxRing(box, c, red, 470f, 0.65f));
            StartCoroutine(FxShake(shake, shakeAmp[k], k == 2 ? 0.5f : 0.25f));
            StartCoroutine(FxPulseScale(box, k == 2 ? 1.06f : 1.03f, 0.2f));   // 화면이 쿵 하고 살짝 커졌다 돌아와요
            if(SoundManager.Instance != null && !fxSkip) SoundManager.Instance.playAchieveSFX();
            float w = 0f; while(w < gap[k] && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; }
        }
        // 마지막 쾅: 카드 뒤 붉은 빛무리 + 불티 기둥 (기다리지 않고 바로 카드)
        var halo = Plate(box, "mg_burst", c.x - 200f, c.y - 200f, 400f, 400f, 0f); halo.color = new Color(red.r, red.g, red.b, 0f);
        fxDivineRings.Add(halo);   // 1회는 카드 뒤에 남고, 여러 번은 카드 정리할 때 사라져요
        StartCoroutine(FxLegendBloom(ti, halo, red));
        StartCoroutine(FxFireColumn(box, c));
    }
    IEnumerator FxLegendBloom(Image sky, Image halo, Color red)
    {
        float t = 0f;
        while(t < 0.7f) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.7f), e = FxBack(k);
            if(halo != null) { halo.color = new Color(red.r, red.g, red.b, 0.8f * k); halo.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, e); }
            if(sky != null) sky.color = new Color(red.r, red.g, red.b, 0.3f * (1f - k * 0.6f));   // 붉은 하늘은 옅게 남아요
            yield return null;
        }
        if(halo != null) halo.rectTransform.localScale = Vector3.one;
        if(sky != null) StartCoroutine(FxFadeGraphic(sky, 1.2f));
    }
    // 불티 기둥: 떨어진 곳에서 불티가 위로 솟구쳐요 (빨강 → 노랑), 0.8초
    IEnumerator FxFireColumn(RectTransform box, Vector2 c)
    {
        float t = 0f, emit = 0f;
        while(t < 0.8f && !fxSkip) {
            t += Time.unscaledDeltaTime; emit += Time.unscaledDeltaTime;
            while(emit > 0.012f) {
                emit -= 0.012f;
                Color col = Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(1f, 0.9f, 0.3f), Random.value);
                var p = c + new Vector2(Random.Range(-46f, 46f), Random.Range(-10f, 20f));
                StartCoroutine(FxEmberUp(box, p, col, Random.Range(10f, 24f)));
            }
            yield return null;
        }
    }
    IEnumerator FxEmberUp(RectTransform box, Vector2 p, Color c, float size)
    {
        var im = FxImg(box, MapSprite("mg_spark"), p.x, p.y, size, size, c);
        var rt = im.rectTransform;
        Vector2 st = rt.anchoredPosition;
        float rise = Random.Range(160f, 320f) * K, sway = Random.Range(-30f, 30f) * K, life = Random.Range(0.5f, 0.9f), spin = Random.Range(-300f, 300f), t = 0f;
        while(rt != null && t < life) {
            t += Time.unscaledDeltaTime;
            float k = t / life;
            rt.anchoredPosition = st + new Vector2(sway * k, rise * (1f - (1f - k) * (1f - k)));
            rt.localScale = Vector3.one * (1f - k * 0.8f);
            rt.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            im.color = new Color(c.r, c.g, c.b, 1f - k);
            yield return null;
        }
        if(rt != null) Destroy(rt.gameObject);
    }
    // 화면이 쿵 하고 살짝 커졌다 돌아와요
    IEnumerator FxPulseScale(RectTransform rt, float peak, float dur)
    {
        if(rt == null) yield break;
        Vector3 s0 = rt.localScale; float t = 0f;
        while(rt != null && t < dur) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur), m = k < 0.3f ? Mathf.Lerp(1f, peak, k / 0.3f) : Mathf.Lerp(peak, 1f, (k - 0.3f) / 0.7f);
            rt.localScale = s0 * m;
            yield return null;
        }
        if(rt != null) rt.localScale = s0;
    }
    // ---- 고대: 신의 각성 (금빛 쾅 다음) — 화면이 하얗게 물들어 잠깐 고요 → 걷히며 거대한 주황 빛무리·빛줄기(멈춘 채) + 글자
    IEnumerator FxAncientAwaken(RectTransform box, RectTransform shake, RectTransform layer, Vector2 c)
    {
        // (2026-11-27 사용자: 고대가 전설에 비해 연출이 너무 없어) — 하늘의 문이 열려요
        //  1 정적: 화면이 어둠에 잠겨요 → 2 빛의 틈: 가운데 금빛 틈이 세로로 갈라져요 → 3 문이 열림: 틈이 좌우로 벌어지며 화면이 하얗게
        //  → 4 섬광: 가장 큰 흔들림 → 5 강림: 하양이 걷히며 카드 + 충격파 3겹 + 카드 뒤 큰 금빛 빛무리 + 화면 전체에서 금빛 알갱이가 떠올라요
        Color gold = new Color(1f, 0.82f, 0.3f), orange = new Color(1f, 0.6f, 0.15f);
        // 1 정적 (어둠은 내용 위, 앞으로 그릴 틈은 그 위)
        var darkRt = PxBox("Dark", box, -300f, -300f, 960f, 1380f);
        var dark = darkRt.gameObject.AddComponent<Image>(); dark.raycastTarget = false; dark.color = new Color(0f, 0f, 0.02f, 0f);
        float t = 0f;
        while(t < 0.45f) { t += FxDt(); dark.color = new Color(0f, 0f, 0.02f, 0.9f * Mathf.Clamp01(t / 0.45f)); yield return null; }
        { float w = 0f; while(w < 0.3f && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; } }
        // 2 빛의 틈: 세로로 쭉
        var glow = FxImg(box, FxPillarTex(), c.x, c.y, 40f, 20f, new Color(gold.r, gold.g, gold.b, 0.9f));
        var slit = FxImg(box, FxPillarTex(), c.x, c.y, 8f, 20f, Color.white);
        if(SoundManager.Instance != null && !fxSkip) SoundManager.Instance.playBuffSFX();
        t = 0f;
        while(t < 0.35f) {
            t += FxDt();
            float k = Mathf.Clamp01(t / 0.35f), e = 1f - (1f - k) * (1f - k);
            glow.rectTransform.sizeDelta = new Vector2(40f * K, Mathf.Lerp(20f, 1000f, e) * K);
            slit.rectTransform.sizeDelta = new Vector2(8f * K, Mathf.Lerp(20f, 1000f, e) * K);
            yield return null;
        }
        { float w = 0f; while(w < 0.2f && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; } }
        // 3 문이 열려요: 틈이 좌우로 벌어지며 화면이 하얗게
        var wl = Stretch("WhiteOut", layer); var wi = wl.gameObject.AddComponent<Image>(); wi.raycastTarget = false; wi.color = new Color(1f, 1f, 0.95f, 0f);
        t = 0f;
        while(t < 0.45f) {
            t += FxDt();
            float k = Mathf.Clamp01(t / 0.45f), e = k * k;
            glow.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(40f, 520f, e) * K, 1000f * K);
            slit.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(8f, 300f, e) * K, 1000f * K);
            wi.color = new Color(1f, 1f, 0.95f, 0.97f * e);
            yield return null;
        }
        // 4 섬광
        Destroy(darkRt.gameObject); Destroy(glow.gameObject); Destroy(slit.gameObject);
        StartCoroutine(FxShake(shake, 26f, 0.7f));
        if(SoundManager.Instance != null && !fxSkip) { SoundManager.Instance.playAchieveSFX(); SoundManager.Instance.Invoke("playAchieveSFX", 0.25f); }
        { float w = 0f; while(w < 0.15f && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; } }
        // 5 강림: 기다리지 않고 바로 카드 (하양 속에서 드러나요)
        var halo = Plate(box, "mg_burst", c.x - 240f, c.y - 240f, 480f, 480f, 0f); halo.color = new Color(gold.r, gold.g, gold.b, 0f);
        fxDivineRings.Add(halo);   // 1회는 카드 뒤에 남고, 여러 번은 카드 정리할 때 사라져요
        StartCoroutine(FxAwakenBloom(wl, wi, halo, gold));
        StartCoroutine(FxAwakenRings(box, c, gold, orange));
        StartCoroutine(FxHolyMotes(box));
    }
    IEnumerator FxAwakenBloom(RectTransform wl, Image wi, Image halo, Color gold)
    {
        float t = 0f;
        while(t < 0.8f && wi != null) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.8f), e = FxBack(k);
            wi.color = new Color(1f, 1f, 0.95f, 0.97f * (1f - k));
            if(halo != null) { halo.color = new Color(gold.r, gold.g, gold.b, 0.9f * k); halo.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, e); }
            yield return null;
        }
        if(wl != null) Destroy(wl.gameObject);
        if(halo != null) halo.rectTransform.localScale = Vector3.one;
    }
    // 충격파 3겹: 금 → 하양 → 주황, 0.18초 간격
    IEnumerator FxAwakenRings(RectTransform box, Vector2 c, Color gold, Color orange)
    {
        Color[] cols = { gold, Color.white, orange }; float[] sizes = { 520f, 700f, 900f };
        for(int i = 0; i < 3; i++) {
            StartCoroutine(FxRing(box, c, cols[i], sizes[i], 0.9f));
            float w = 0f; while(w < 0.18f) { w += Time.unscaledDeltaTime; yield return null; }
        }
    }
    // 화면 전체에서 금빛 빛 알갱이가 천천히 떠올라요 (1.6초)
    IEnumerator FxHolyMotes(RectTransform box)
    {
        float t = 0f, emit = 0f;
        while(t < 1.6f) {
            t += Time.unscaledDeltaTime; emit += Time.unscaledDeltaTime;
            while(emit > 0.03f) {
                emit -= 0.03f;
                Color col = Color.Lerp(new Color(1f, 0.85f, 0.4f), Color.white, Random.value * 0.6f);
                StartCoroutine(FxEmberUp(box, new Vector2(Random.Range(10f, 350f), Random.Range(420f, 790f)), col, Random.Range(8f, 18f)));
            }
            yield return null;
        }
    }

    // ---- 금빛 강림 (유니크 이상 공통, 부딪히기 전)
    readonly List<Graphic> fxDivine = new List<Graphic>();   // 빛기둥 · 금빛 하늘 · 글자 (부딪히면 사라져요)
    readonly List<Graphic> fxDivineRings = new List<Graphic>();   // 신성한 고리 (1회는 카드 뒤에 남고, 여러 번은 카드 정리할 때 사라져요)
    IEnumerator FxAncientIntro(RectTransform box, RectTransform shake, RectTransform layer, Vector2 c)
    {
        Color gold = new Color(1f, 0.82f, 0.3f);
        fxDivine.Clear(); fxDivineRings.Clear();
        // 1 하늘이 금빛으로 + 글자
        var tint = Stretch("Gold", layer);
        tint.SetSiblingIndex(shake.GetSiblingIndex());   // 하늘 위 · 내용 아래
        var ti = tint.gameObject.AddComponent<Image>(); ti.sprite = MapSprite("mg_flash"); ti.raycastTarget = false; ti.color = new Color(gold.r, gold.g, gold.b, 0f);
        var title = GameFont(PxText(box, "", 180f, c.y - 190f, 22f, gold, 1.8f, MgEdge), true, MgEdge);   // 문구는 뺐어요 (사용자)
        title.alpha = 0f;
        float t = 0f;
        while(t < 0.7f) { t += FxDt(); float k = Mathf.Clamp01(t / 0.7f); ti.color = new Color(gold.r, gold.g, gold.b, 0.28f * k); title.alpha = k; yield return null; }
        // (번개는 뺐어요 — 2026-11-27 사용자 "번개를 그냥 없애 줘". FxBolt 는 남겨 둬요)
        { float w = 0f; while(w < 0.4f && !fxSkip) { w += Time.unscaledDeltaTime; yield return null; } }
        // 3 금빛 빛기둥 (하늘에서 떨어질 곳까지). 고리는 뺐어요 (사용자: 원이 이상해)
        // 빛기둥 (2026-11-27 사용자: 끝이 잘린 느낌 → 위아래 끝이 부드럽게 사라지는 전용 그림 FxPillarTex + 바닥 금빛 빛무리로 이어 줘요)
        float top = -80f, bot = c.y + 70f, mid = (top + bot) / 2f, len = bot - top;
        var pool = FxImg(box, MapSprite("mg_burst"), c.x, c.y, 220f, 220f, new Color(gold.r, gold.g, gold.b, 0f));
        var pil = FxImg(box, FxPillarTex(), c.x, mid, 30f, len, new Color(gold.r, gold.g, gold.b, 0f));
        var core = FxImg(box, FxPillarTex(), c.x, mid, 10f, len, new Color(1f, 1f, 1f, 0f));
        var prt = pil.rectTransform; var crt = core.rectTransform;
        fxDivine.Add(pil); fxDivine.Add(core); fxDivine.Add(pool); fxDivine.Add(ti); fxDivine.Add(title);
        t = 0f;
        while(t < 0.6f) {
            t += FxDt();
            float k = Mathf.Clamp01(t / 0.6f);
            prt.sizeDelta = new Vector2(Mathf.Lerp(30f, 150f, k) * K, prt.sizeDelta.y);
            crt.sizeDelta = new Vector2(Mathf.Lerp(10f, 40f, k) * K, crt.sizeDelta.y);
            pil.color = new Color(gold.r, gold.g, gold.b, 0.85f * k); core.color = new Color(1f, 1f, 1f, 0.9f * k);
            pool.color = new Color(gold.r, gold.g, gold.b, 0.8f * k); pool.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, k);
            yield return null;
        }
        if(SoundManager.Instance != null && !fxSkip) SoundManager.Instance.playAchieveSFX();
    }
    // 부딪힌 뒤: 빛기둥·금빛 하늘·글자는 사라지고, 고리는 카드 뒤에 멈춘 채 남아요
    void FxDivineEnd()
    {
        foreach(var gph in fxDivine) if(gph != null) StartCoroutine(FxFadeGraphic(gph, 0.6f));
        fxDivine.Clear();
    }
    IEnumerator FxFadeGraphic(Graphic gph, float dur)
    {
        float t = 0f, a0 = gph.color.a;
        while(gph != null && t < dur) { t += Time.unscaledDeltaTime; var c = gph.color; c.a = a0 * (1f - t / dur); gph.color = c; yield return null; }
        if(gph != null) Destroy(gph.gameObject);
    }
    // 번개 (2026-11-27 사용자: 코드로 그린 지그재그는 AI 같아 → 게임 그림체 만화 번개 그림 fx_bolt, render_sprites.js)
    //  위(a)에서 아래 끝(b)까지 늘려 세우고, 위에서 아래로 쭉 내리꽂힌 뒤 번쩍·깜빡·번쩍하고 사라져요. 좌우는 무작위로 뒤집어요
    void FxBolt(RectTransform box, Vector2 a, Vector2 b, Color col)
    {
        float h = Mathf.Max(120f, b.y - a.y + 16f), w = h * 0.36f;   // 그림 비율 144 : 384
        bool flip = Random.value < 0.5f;
        float tipX = (flip ? 1f - 0.24f : 0.24f) * w;                 // 그림 아래 끝(뾰족한 곳) = 폭의 24%
        var rt = PxBox("Bolt", box, b.x - tipX, b.y - h + 8f, w, h);
        var im = rt.gameObject.AddComponent<Image>();
        im.sprite = MapSprite("fx_bolt"); im.raycastTarget = false;
        rt.pivot = new Vector2(0.5f, 1f);   // 위 가운데 기준으로 아래로 자라요
        rt.anchoredPosition += new Vector2(w * 0.5f * K, 0f);
        if(flip) rt.localScale = new Vector3(-1f, 1f, 1f);
        StartCoroutine(FxBoltLife(im, flip));
    }
    IEnumerator FxBoltLife(Image im, bool flip)
    {
        var rt = im.rectTransform; float sx = flip ? -1f : 1f, t = 0f;
        while(rt != null && t < 0.45f) {
            t += Time.unscaledDeltaTime;
            float grow = Mathf.Clamp01(t / 0.07f);   // 0.07초 동안 위에서 아래로 쭉
            rt.localScale = new Vector3(sx, grow, 1f);
            float a = t < 0.12f ? 1f : t < 0.16f ? 0.25f : t < 0.24f ? 1f : 1f - (t - 0.24f) / 0.21f;   // 번쩍 · 깜빡 · 번쩍 · 사라짐
            im.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            yield return null;
        }
        if(rt != null) Destroy(rt.gameObject);
    }

    // 불티 하나: 꼬리 쪽으로 조금 날아가며 작아지고 사라져요
    IEnumerator FxEmber(RectTransform box, Vector2 p, Vector2 back, Color c, float size)
    {
        var im = FxImg(box, MapSprite("mg_spark"), p.x, p.y, size, size, c);
        var rt = im.rectTransform;
        Vector2 st = rt.anchoredPosition, v = (back * Random.Range(20f, 60f) + Random.insideUnitCircle * 25f) * K;
        float t = 0f, life = Random.Range(0.3f, 0.55f), spin = Random.Range(-400f, 400f);
        while(rt != null && t < life) {
            t += Time.unscaledDeltaTime;
            float k = t / life;
            rt.anchoredPosition = st + new Vector2(v.x, -v.y) * k;
            rt.localScale = Vector3.one * (1f - k);
            rt.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            im.color = new Color(c.r, c.g, c.b, 1f - k);
            yield return null;
        }
        if(rt != null) Destroy(rt.gameObject);
    }
    IEnumerator FxFadeColor(Image im, float dur)
    {
        float t = 0f, a0 = im != null ? im.color.a : 0f;
        while(im != null && t < dur) { t += Time.unscaledDeltaTime; var c = im.color; c.a = a0 * (1f - t / dur); im.color = c; yield return null; }
        if(im != null) Destroy(im.gameObject);
    }
    // 충격파 링: 커지며 사라져요
    IEnumerator FxRing(RectTransform box, Vector2 c, Color col, float maxSize, float dur)
    {
        var ring = FxImg(box, FxRingTex(), c.x, c.y, maxSize, maxSize, col);
        var rt = ring.rectTransform;
        float t = 0f;
        while(rt != null && t < dur) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur), e = 1f - (1f - k) * (1f - k);
            rt.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, e);
            ring.color = new Color(col.r, col.g, col.b, 1f - k);
            yield return null;
        }
        if(rt != null) Destroy(rt.gameObject);
    }
    // 화면을 가로지르는 띠 '전설!' (기울어진 등급 색 띠가 왼쪽에서 쓱)
    IEnumerator FxCutIn(RectTransform parent, string s, float cy, Color col, bool huge)
    {
        var band = FxImg(parent, FxBandTex(), 180f, cy, 460f, huge ? 90f : 74f, new Color(col.r, col.g, col.b, 0.92f));
        var brt = band.rectTransform; brt.localRotation = Quaternion.Euler(0f, 0f, 6f);
        var tx = GameFont(PxText(parent, s, 180f, cy, huge ? 46f : 40f, Color.white, 2.4f, MgEdge), true, MgEdge);
        var trt = tx.rectTransform; trt.localRotation = Quaternion.Euler(0f, 0f, 6f);
        Vector2 bh = brt.anchoredPosition, th = trt.anchoredPosition, off = new Vector2(-420f * K, 0f);
        float t = 0f;
        while(t < 0.22f) {
            t += FxDt();
            float e = FxBack(Mathf.Clamp01(t / 0.22f));
            brt.anchoredPosition = Vector2.LerpUnclamped(bh + off, bh, e);
            trt.anchoredPosition = Vector2.LerpUnclamped(th - off * 1.3f, th, e);
            yield return null;
        }
        brt.anchoredPosition = bh; trt.anchoredPosition = th;
        if(SoundManager.Instance != null) SoundManager.Instance.playAchieveSFX();
        StartCoroutine(FxPop(trt, 1.2f));
    }

    // 별 반짝임 (각자 다른 빠르기)
    IEnumerator FxTwinkleStars(List<Image> list)
    {
        var a0 = list.Select(x => x.color.a).ToArray();
        var ph = list.Select(_ => Random.Range(0f, 6.28f)).ToArray();
        var sp = list.Select(_ => Random.Range(1.5f, 4.5f)).ToArray();
        float t = 0f;
        while(list.Count > 0 && list[0] != null) {
            t += Time.unscaledDeltaTime;
            for(int i = 0; i < list.Count; i++) if(list[i] != null) { var c = list[i].color; c.a = a0[i] * (0.55f + 0.45f * Mathf.Sin(t * sp[i] + ph[i])); list[i].color = c; }
            yield return null;
        }
    }

    // ---- 코드로 그린 그림 (한 번만 만들어요)
    static Sprite fxTrail, fxRing, fxBand, fxSky, fxWhite, fxPillar;
    // 빛기둥: 가운데가 진하고 양옆으로 부드럽게, 위 20% 는 서서히 나타나고 아래 30% 는 둥글게 사라져요 (끝이 잘려 보이지 않게)
    static Sprite FxPillarTex()
    {
        if(fxPillar != null) return fxPillar;
        int w = 32, h = 256; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for(int y = 0; y < h; y++) for(int x = 0; x < w; x++) {
            float u = (y + 0.5f) / h;   // 텍스처 y: 0 = 아래
            float vert = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.3f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / 0.2f));
            float hor = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f), 1.4f);
            px[y * w + x] = new Color32(255, 255, 255, (byte)(vert * hor * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        return fxPillar = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
    // 하얀 둥근 네모 (카드가 뒤집힐 때 번쩍)
    static Sprite FxWhiteTex()
    {
        if(fxWhite != null) return fxWhite;
        int n = 64; float r = 12f; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for(int y = 0; y < n; y++) for(int x = 0; x < n; x++) {
            float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (n - r))), dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (n - r)));
            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        return fxWhite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
    // 밤하늘: 위 40% 쯤이 가장 밝은 남색(#1b2560) → 가장자리 진한 남색(#0a0f33) (제안 그림 B 와 같은 색)
    static Sprite FxSkyTex()
    {
        if(fxSky != null) return fxSky;
        int w = 64, h = 128; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        Color a = new Color32(0x1b, 0x25, 0x60, 0xff), b = new Color32(0x0a, 0x0f, 0x33, 0xff);
        for(int y = 0; y < h; y++) for(int x = 0; x < w; x++) {
            float dx = (x + 0.5f) / w - 0.5f, dy = (1f - (y + 0.5f) / h) - 0.4f;   // 텍스처 y 는 아래가 0
            float d = Mathf.Sqrt((dx / 0.6f) * (dx / 0.6f) + (dy / 0.7f) * (dy / 0.7f));
            px[y * w + x] = Color.Lerp(a, b, Mathf.Clamp01(d));
        }
        tex.SetPixels32(px); tex.Apply();
        return fxSky = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
    // 꼬리: 오른쪽(머리)으로 갈수록 진하고, 가운데 줄이 진한 가로 띠
    static Sprite FxTrailTex()
    {
        if(fxTrail != null) return fxTrail;
        int w = 128, h = 16; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for(int y = 0; y < h; y++) for(int x = 0; x < w; x++) {
            float a = Mathf.Pow(x / (w - 1f), 1.6f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y - (h - 1) / 2f) / (h / 2f)), 1.5f);
            px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        return fxTrail = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
    // 링: 테두리만 부드럽게 빛나는 원
    static Sprite FxRingTex()
    {
        if(fxRing != null) return fxRing;
        int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for(int y = 0; y < n; y++) for(int x = 0; x < n; x++) {
            float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f)) / (n / 2f);
            float a = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.07f, 2f)) + 0.25f * Mathf.Exp(-Mathf.Pow((d - 0.7f) / 0.18f, 2f));
            px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        return fxRing = Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
    // 띠: 양 끝이 흐려지는 가로 띠
    static Sprite FxBandTex()
    {
        if(fxBand != null) return fxBand;
        int w = 128, h = 32; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for(int y = 0; y < h; y++) for(int x = 0; x < w; x++) {
            float ex = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / 22f), ey = Mathf.Clamp01(Mathf.Min(y, h - 1 - y) / 3f);
            px[y * w + x] = new Color32(255, 255, 255, (byte)(ex * ey * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        return fxBand = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // [개발] 연출 시험 (G = 전설 · H = 고대, Shift 를 같이 누르면 1회, 아니면 10회): 실제로 뽑지 않아요 (다이아·뽑기권·가방 그대로)
    //  마지막 카드 = 그 등급, 나머지는 일반~유니크 무작위. 스페셜 뽑기 기준 (대박 유니크 · 초대박 전설 이상)
    bool fxTest; int fxTestBest, fxTestCount;
    public void DebugGachaFx(int best, int count)
    {
        var em = EquipManager.Instance; var gm = GachaManager.Instance;
        if(em == null || gm == null || GachaFxBusy) return;
        var list = new List<EquipFrame>();
        for(int i = 0; i < count; i++) {
            int g = i == count - 1 ? best : Random.Range(0, Mathf.Min(best, 4));
            // 그 등급에 그 부위 장비가 없을 수 있어요 (고대는 적어요) → 다른 부위로
            EquipFrame f = null; int t0 = Random.Range(0, 5);
            for(int k = 0; k < 5 && f == null; k++) { try { f = em.getRandomItem_Grade((Equips.MaterialClass)g, (Equips.Type)((t0 + k) % 5)); } catch(System.ArgumentOutOfRangeException) { } }
            if(f != null) list.Add(f);
        }
        fxTest = true; fxTestBest = best; fxTestCount = count;
        var chest = gm.GachaBox_Sprites != null && gm.GachaBox_Sprites.Length > 1 ? gm.GachaBox_Sprites[1] : null;
        PlayGachaFx(true, list, null, GachaDataOf(count > 1 ? GachaData.GachaType.Specialx10 : GachaData.GachaType.Special), chest);
    }

    void FxClose(RectTransform layer)
    {
        fxTest = false;
        if(layer != null) Destroy(layer.gameObject);
        GachaFxBusy = false;
        gsKey = null;   // 뽑기 창 (최근 장비) 새로
        var em = EquipManager.Instance;
        if(em != null) em.ChangeEquipsUI();
    }

    // 한 번 더: 같은 뽑기 (뽑기권 먼저, 없으면 다이아 — 예전 규칙)
    void FxAgain(GachaData d)
    {
        var gm = GachaManager.Instance;
        if(gm == null || d == null) return;
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        var ty = d.gacha;
        int tk = ty == GachaData.GachaType.Normal || ty == GachaData.GachaType.Normalx10 ? gm.NormalGachaTicket
            : ty == GachaData.GachaType.Special || ty == GachaData.GachaType.Specialx10 ? gm.SpecialGachaTicket : gm.StoneGachaTicket;
        int need = ty == GachaData.GachaType.UpgradeStonex10 ? 20 : ty == GachaData.GachaType.Normalx10 || ty == GachaData.GachaType.Specialx10 ? 10 : 1;
        if(tk < need && GameManager.GetDiamond() < d.Price) { ShowMessage("다이아가 부족해요", $"다이아 {d.Price:N0}개가 필요해요 (지금 {GameManager.GetDiamond():N0}개)"); return; }
        gm.BuyDirect(d);
    }

    // 카드 공개 (2026-11-27 사용자: 카드가 나올 때도 화려하게): 등급 색이 비치는 뒷면(mg_q_등급)이 톡 나와(여러 번이면 가운데에서 날아와)
    //  떨다가 → 휙 뒤집히며(가로로 접혔다 펴짐) 하얗게 번쩍 + 충격파 링 + 반짝이. 대박이면 도장
    IEnumerator FxReveal(RectTransform parent, RectTransform card, FxItem it, float s, int big, int huge, Vector2 flyFrom, bool single)
    {
        var backRt = PxBox("Back", card, -s * 0.5f, -s * 0.5f, s * 2f, s * 2f);   // mg_q 그림은 빛 여백 포함 (칸의 2배)
        var back = backRt.gameObject.AddComponent<Image>(); back.sprite = MapSprite("mg_q_" + MgKey[it.g]); back.raycastTarget = false;
        var front = PxBox("Front", card, 0f, 0f, s, s);
        FxCard(front, it, s);
        front.gameObject.SetActive(false);
        // 1 나오기 (여러 번 = 가운데에서 날아와요)
        if(flyFrom != Vector2.zero) yield return FxFly(card, flyFrom, 0.22f);
        else yield return FxPopIn(card, 0.25f);
        // 2 떨기 (1회만 조금 길게)
        float t = 0f, shakeT = single ? 0.35f : 0.06f;
        Vector2 home = card.anchoredPosition;
        while(t < shakeT) { t += FxDt(); card.anchoredPosition = home + Random.insideUnitCircle * 2.2f * K; yield return null; }
        card.anchoredPosition = home;
        // 3 뒤집기: 가로로 접혔다가 앞면으로 펴져요
        t = 0f;
        while(t < 0.09f) { t += FxDt(); card.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.09f), 1f + 0.08f * t / 0.09f, 1f); yield return null; }
        backRt.gameObject.SetActive(false); front.gameObject.SetActive(true);
        var flash = PxBox("Flash", card, 0f, 0f, s, s).gameObject.AddComponent<Image>();
        flash.sprite = FxWhiteTex(); flash.color = Color.white; flash.raycastTarget = false;   // 하얀 둥근 판 (칸 모양)
        t = 0f;
        while(t < 0.14f) { t += FxDt(); float e = FxBack(Mathf.Clamp01(t / 0.14f)); card.localScale = new Vector3(e, 1.08f - 0.08f * Mathf.Clamp01(t / 0.14f), 1f); yield return null; }
        card.localScale = Vector3.one;
        StartCoroutine(FxFade(flash, 0.3f));
        // 4 카드 뒤에서만 빛나요 (2026-11-27 사용자: "별 임팩트가 부자연스러워, 장비 UI 있는 곳에만 빛나게 뒷배경으로")
        //    등급 색 빛무리가 카드 뒤에서 확 커졌다가 은은하게 남아요 (좋을수록 크고 진하게). 사방으로 튀는 반짝이는 없어요
        var c = new Vector2((card.anchoredPosition.x / K) + s / 2f, -(card.anchoredPosition.y / K) + s / 2f);
        float gs = s * (it.g >= big ? 2.1f : single ? 1.9f : 1.5f), ga = it.g >= big ? 0.85f : it.g >= 2 ? 0.6f : 0.4f;
        var glow = Plate(parent, "mg_burst", c.x - gs / 2f, c.y - gs / 2f, gs, gs, 0f);
        glow.color = FxCol(it.g, ga);
        glow.transform.SetSiblingIndex(card.GetSiblingIndex());   // 카드 바로 뒤
        StartCoroutine(FxGlowIn(glow, ga));
        if(SoundManager.Instance != null && !fxSkip) { if(it.g >= big) SoundManager.Instance.playAchieveSFX(); else SoundManager.Instance.playClickSFX(); }
        if(it.g >= big && !single) FxStamp(parent, it.g >= huge ? "초대박!" : "대박!", c.x + s / 2f - 4f, c.y - s / 2f + 4f, 13f, it.g >= huge);
    }

    // 카드 뒤 빛: 0.6배에서 1.25배로 확 커졌다가 1배로 (밝기도 한 번 세게 → 원래대로), 그 뒤엔 멈춰 있어요
    IEnumerator FxGlowIn(Image glow, float a)
    {
        var rt = glow.rectTransform; float t = 0f;
        while(rt != null && t < 0.45f) {
            t += FxDt();
            float k = Mathf.Clamp01(t / 0.45f), sc = k < 0.4f ? Mathf.Lerp(0.6f, 1.25f, k / 0.4f) : Mathf.Lerp(1.25f, 1f, (k - 0.4f) / 0.6f);
            rt.localScale = new Vector3(sc, sc, 1f);
            var col = glow.color; col.a = Mathf.Min(1f, a * (k < 0.4f ? 1.4f : Mathf.Lerp(1.4f, 1f, (k - 0.4f) / 0.6f))); glow.color = col;
            yield return null;
        }
        if(rt != null) { rt.localScale = Vector3.one; var col = glow.color; col.a = a; glow.color = col; }
    }

    // 카드: 등급 칸 + 그림 (강화석은 그림 대신 '+개수')
    void FxCard(RectTransform card, FxItem it, float s)
    {
        Plate(card, "mg_cell_" + MgKey[it.g], 0f, 0f, s, s, 0f);
        if(it.sp != null) {
            var irt = PxBox("Icon", card, s * 0.14f, s * 0.12f, s * 0.72f, s * 0.72f);
            var im = irt.gameObject.AddComponent<Image>();
            im.sprite = it.sp; im.preserveAspect = true; im.raycastTarget = false;
        } else GameFont(PxText(card, it.sub, s / 2f, s / 2f, s * 0.3f, Color.white, 1.2f, MgEdge), true, MgEdge);
    }
    // 도장 '대박!' (기울여서 톡)
    void FxStamp(RectTransform box, string s, float cx, float cy, float size, bool huge)
    {
        var t = GameFont(PxText(box, s, cx, cy, size, huge ? Hex("#ff8d00") : Hex("#ffe14a"), size * 0.09f, MgEdge), true, MgEdge);
        t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
        StartCoroutine(FxPop(t.rectTransform, 1.6f));
    }
    IEnumerator FxPop(RectTransform rt, float peak)
    {
        float t = 0f;
        while(rt != null && t < 0.22f) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.22f), s = k < 0.4f ? Mathf.Lerp(1f, peak, k / 0.4f) : Mathf.Lerp(peak, 1f, (k - 0.4f) / 0.6f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if(rt != null) rt.localScale = Vector3.one;
    }
    IEnumerator FxPopIn(RectTransform rt, float dur)
    {
        float t = 0f;
        while(rt != null && t < dur) {
            t += FxDt();
            float s = FxBack(Mathf.Clamp01(t / dur));
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if(rt != null) rt.localScale = Vector3.one;
    }
    // 가운데(상자 자리)에서 제자리로 날아가며 커지기
    IEnumerator FxFly(RectTransform rt, Vector2 offsetFromCenter, float dur)
    {
        Vector2 home = rt.anchoredPosition, from = home + offsetFromCenter;
        float t = 0f;
        while(rt != null && t < dur) {
            t += FxDt();
            float k = Mathf.Clamp01(t / dur), e = FxBack(k);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, home, e);
            float s = Mathf.LerpUnclamped(0.2f, 1f, e);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if(rt != null) { rt.anchoredPosition = home; rt.localScale = Vector3.one; }
    }
    IEnumerator FxFade(Image im, float dur)
    {
        float t = 0f;
        while(im != null && t < dur) { t += Time.unscaledDeltaTime; im.color = new Color(1f, 1f, 1f, 1f - t / dur); yield return null; }
        if(im != null) Destroy(im.gameObject);
    }
    IEnumerator FxShake(RectTransform rt, float amp, float dur)
    {
        float t = 0f;
        while(rt != null && t < dur) { t += Time.unscaledDeltaTime; rt.anchoredPosition = Random.insideUnitCircle * amp * K * (1f - t / dur); yield return null; }
        if(rt != null) rt.anchoredPosition = Vector2.zero;
    }
    IEnumerator FxSpin(RectTransform rt, float speed)
    {
        while(rt != null) { rt.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime); yield return null; }
    }
    // 반짝이 하나: 가운데에서 바깥으로 날아가며 돌고 사라져요
    IEnumerator FxSpark(RectTransform box, Vector2 c, Color col, float angle)
    {
        float size = Random.Range(14f, 30f), dist = Random.Range(110f, 210f), life = Random.Range(0.5f, 0.8f);
        var img = Plate(box, "mg_spark", c.x - size / 2f, c.y - size / 2f, size, size, size * 6f / 26f);
        img.color = Color.Lerp(Color.white, col, 0.6f);
        var rt = img.rectTransform;
        Vector2 start = rt.anchoredPosition, dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        float t = 0f, spin = Random.Range(-360f, 360f);
        while(rt != null && t < life) {
            t += Time.unscaledDeltaTime;
            float k = t / life, e = 1f - (1f - k) * (1f - k);
            rt.anchoredPosition = start + dir * dist * e * K;
            rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.2f, k);
            rt.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - k * k);
            yield return null;
        }
        if(rt != null) Destroy(rt.gameObject);
    }
}
