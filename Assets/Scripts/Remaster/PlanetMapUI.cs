using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 행성 지도 (2026-11-25 사용자가 고른 A안 '항로 지도', 미리보기: 작업 폴더 previews/planet_ui_A.png)
//  (2026-11-25 사용자: "미리보기랑 글씨 크기·효과·배치·이미지·배경까지 완전히 똑같이")
//  - 미리보기 HTML(tools/ui_preview/build.js)의 숫자를 그대로 옮겨요: 미리보기 1px = 게임 3단위 (가로 360px = 1080)
//  - 판·버튼·배경 그림은 미리보기를 그린 크롬으로 똑같이 찍은 그림 (Resources/PlanetMap, tools/ui_preview/render_sprites.js)
//  - 글자는 미원체 + 외곽선·그림자 (미리보기의 text-shadow 와 같은 두께)
//  - 행성 색(평행·심연·잠김)은 미리보기의 CSS filter 계산을 행성 그림 픽셀에 그대로 적용
//  - 옛날 모험 화면(StagesMap)을 덮고 그 안에 만들어요 (씬은 안 건드려요). planet_map_new = 0 이면 옛날 화면
//  - 출격은 옛날 '전투 시작' 버튼과 같은 순서 (행성 정보 → 맵 → StageManager.Move → 모험 화면 닫기)
public partial class RemasterUI
{
    const float K = 3f;                 // 미리보기 1px = 3단위
    const float NavH = 64f;             // 아래 메뉴 높이 (px)
    const float NodeGap = 140f;         // 행성 사이 세로 간격 (px)
    // (2026-11-25 사용자: 첫 행성이 지금 행성이면 아래 진행 칸·무기·규칙이 '이어서 하기' 줄에 가려요) 첫 행성 가운데를 줄 위로 충분히 올려요
    //  지금 행성 아래 정보 약 156px (이름판·진행 칸·무기 띠·규칙 띠) + 줄 60px + 여유
    //  (2026-11-25 사용자: 맨 아래가 허전해요) 첫 행성 아래에 출발점 '지구'를 둬요 → 첫 행성은 300, 지구는 160
    const float MapBottomPad = 300f, MapTopPad = 160f;

    GameObject stagesMap;
    RectTransform mapRoot, mapFrame, mapContent, mapTop, mapQuick, sheetLayer, mapNav;
    ScrollRect mapScroll;
    Image mapBg;
    int mapDim = -1;
    bool mapWasOpen;
    RectTransform shipRt, curRing;
    Vector2 shipBase;
    float mapTime;
    Sprite softCircle, softLine, dotSprite, smallRound;

    static readonly Color Navy = Hex("#0b1030");

    // RemasterUI.Update 에서 매 프레임
    void UpdatePlanetMap()
    {
        var sm = StageManager.instance;
        if(sm == null || sm.scrollRect == null) return;
        if(stagesMap == null) {
            var t = sm.scrollRect.transform.parent != null ? sm.scrollRect.transform.parent.parent : null;   // Planets → Universe → StagesMap
            if(t == null) return;
            stagesMap = t.gameObject;
        }
        if(Tuning.Get("planet_map_new", 1f) <= 0f) {
            if(mapRoot != null) mapRoot.gameObject.SetActive(false);
            return;
        }
        bool open = stagesMap.activeInHierarchy && mgr != null && mgr.Loaded;
        if(open && !mapWasOpen) OpenPlanetMap();
        mapWasOpen = open;
        if(!open || mapRoot == null) return;
        mapTime += Time.unscaledDeltaTime;
        if(shipRt != null) shipRt.anchoredPosition = shipBase + new Vector2(0, Mathf.Sin(mapTime * 2.2f) * 8f);
        if(curRing != null) curRing.localRotation = Quaternion.Euler(0, 0, -mapTime * 10f);
    }

    void OpenPlanetMap()
    {
        if(!EnsureBuilt()) return;
        var sm = StageManager.instance;
        if(mapRoot == null) BuildMapShell();
        mapRoot.SetAsLastSibling();
        mapRoot.gameObject.SetActive(true);
        int dim = sm.currentPlanet != null ? PlanetDimension.Of(Mathf.Max(0, sm.PlanetIndex), sm.AbyssMode) : 0;
        if(!DimOpen(dim)) dim = 0;
        ShowDim(dim);
        CloseSheet();
    }

    #region 그리기 도구 (미리보기 px 좌표: 부모의 왼쪽 위가 0,0 · 아래로 +)
    static Color Hex(string h, float a = 1f) { ColorUtility.TryParseHtmlString(h, out var c); c.a = a; return c; }

    // 부모 왼쪽 위 기준 상자
    static RectTransform PxBox(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = Rect(name, parent, new Vector2(w * K, h * K), Vector2.zero);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * K, -y * K);
        return rt;
    }
    // 부모 왼쪽 위 기준, 가운데 (cx, cy)
    static RectTransform PxAt(string name, Transform parent, float cx, float cy, float w, float h)
    {
        var rt = Rect(name, parent, new Vector2(w * K, h * K), Vector2.zero);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx * K, -cy * K);
        return rt;
    }

    readonly Dictionary<string, Sprite> mapSprites = new Dictionary<string, Sprite>();
    Sprite MapSprite(string name)
    {
        if(mapSprites.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("PlanetMap/" + name);
        mapSprites[name] = s;
        return s;
    }
    // 크롬으로 찍은 판 그림: (x, y, w, h) = 미리보기의 상자, pad = 그림에 들어 있는 그림자·빛 여백
    Image Plate(Transform parent, string sprite, float x, float y, float w, float h, float pad, string name = null)
    {
        var rt = PxBox(name ?? sprite, parent, x - pad, y - pad, w + pad * 2, h + pad * 2);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = MapSprite(sprite);
        if(img.sprite != null && img.sprite.border.sqrMagnitude > 0f) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
        img.raycastTarget = false;
        return img;
    }
    // 아이콘 (가운데 cx, cy, 크기 size px). 'RemasterUI/' 그림
    Image PxIcon(Transform parent, string icon, float cx, float cy, float size, float shadow = 0f)
    {
        var rt = PxAt("Icon", parent, cx, cy, size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Skin(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        if(img.sprite == null) img.color = Color.clear;
        if(shadow > 0f) { var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = Navy; sh.effectDistance = new Vector2(0, -shadow * K); }
        return img;
    }

    // ---- 글자: 미원체 + 미리보기의 외곽선(text-shadow 테두리 w px) + 아래 그림자((w+2)px, 검정 35%)
    TMP_FontAsset mapFont;
    readonly Dictionary<string, Material> mapMats = new Dictionary<string, Material>();
    TMP_FontAsset MapFont()
    {
        if(mapFont != null) return mapFont;
        try {
            var src = Resources.Load<Font>("Fonts/MiwonDyn");
            // 외곽선이 두꺼워서 여백(16)을 크게: 외곽선 0.25em 까지 (미리보기 가장 두꺼운 것 2px/15px = 0.13em)
            if(src != null) mapFont = TMP_FontAsset.CreateFontAsset(src, 64, 16, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            if(mapFont != null) {
                mapFont.name = "MiwonMap";
                var fb = FindFont();
                if(fb != null && fb != mapFont) mapFont.fallbackFontAssetTable = new List<TMP_FontAsset> { fb };
            }
        } catch(Exception e) { Debug.LogWarning("[행성 지도] 글꼴 만들기 실패: " + e.Message); }
        if(mapFont == null) mapFont = font;
        return mapFont;
    }
    Material TextMat(Color outline, float em, bool shadow, float shadowEm)
    {
        string key = $"{ColorUtility.ToHtmlStringRGBA(outline)}_{em:0.000}_{shadow}_{shadowEm:0.000}";
        if(mapMats.TryGetValue(key, out var m)) return m;
        var f = MapFont();
        m = new Material(f.material);
        float pad = 16f, size = 64f, gs = pad + 1f;
        // (2026-11-25 사용자: 글씨가 너무 굵어) 외곽선을 planet_map_outline(0.5)배로, 아래 그림자는 끄기 (planet_map_text_shadow 0)
        float w = Mathf.Clamp(em * size / pad * Tuning.Get("planet_map_outline", 0.5f), 0f, 0.66f);   // 외곽선 굵기 (안쪽 반은 글자를 덮지 않게 그만큼 글자를 부풀려요)
        shadow = shadow && Tuning.Get("planet_map_text_shadow", 0f) > 0f;
        if(em > 0f) {
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", outline);
            m.SetFloat("_OutlineWidth", w);
            m.SetFloat("_FaceDilate", w * 0.5f);
        } else {
            m.DisableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_FaceDilate", 0f);
        }
        if(shadow) {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.35f));
            m.SetFloat("_UnderlayOffsetX", 0f);
            m.SetFloat("_UnderlayOffsetY", -Mathf.Clamp(shadowEm * size / gs, 0f, 1f));
            m.SetFloat("_UnderlayDilate", Mathf.Clamp(w, 0f, 1f));
            m.SetFloat("_UnderlaySoftness", 0f);
        } else m.DisableKeyword("UNDERLAY_ON");
        ShaderUtilities.UpdateShaderRatios(m);
        mapMats[key] = m;
        return m;
    }

    // 글자. (cx, cy) = 글자 가운데 (align 이 Left 면 왼쪽 끝 x, Right 면 오른쪽 끝 x). size = 미리보기 font-size px, ow = 외곽선 px
    TextMeshProUGUI PxText(Transform parent, string text, float cx, float cy, float size, Color color, float ow = 0f, Color? outline = null, TextAlignmentOptions align = TextAlignmentOptions.Center, float boxW = 0f)
    {
        float w = boxW > 0f ? boxW : Mathf.Max(20f, MeasureText(text, size) + 8f);
        float x = align == TextAlignmentOptions.Left ? cx + w / 2f : align == TextAlignmentOptions.Right ? cx - w / 2f : cx;
        var rt = PxAt("Text", parent, x, cy, w, size * 1.6f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = MapFont();
        t.fontSharedMaterial = TextMat(outline ?? Navy, ow / Mathf.Max(1f, size), ow > 0f, (ow + 2f) / Mathf.Max(1f, size));
        t.text = text;
        t.fontSize = size * K;
        t.color = color;
        t.alignment = align == TextAlignmentOptions.Left ? TextAlignmentOptions.MidlineLeft : align == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.enableAutoSizing = false;
        t.raycastTarget = false;
        t.extraPadding = true;
        return t;
    }
    TextMeshProUGUI measure;
    // 글자 폭 (미리보기 px)
    float MeasureText(string text, float size)
    {
        if(measure == null) {
            var rt = Rect("Measure", mapRoot != null ? (Transform)mapRoot : transform, Vector2.one, new Vector2(-99999, -99999));
            measure = rt.gameObject.AddComponent<TextMeshProUGUI>();
            measure.font = MapFont();
            measure.textWrappingMode = TextWrappingModes.NoWrap;
            measure.enabled = false;
        }
        measure.fontSize = size * K;
        return measure.GetPreferredValues(text, 99999f, 99999f).x / K;
    }
    #endregion

    #region 그림 만들기 (부드러운 원·선, CSS 필터)
    // radial-gradient(색, transparent 65%) 와 같은 원
    static Sprite MakeHalo(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float r = size / 2f;
        for(int y = 0; y < size; y++)
            for(int x = 0; x < size; x++) {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                float a = Mathf.Clamp01(1f - d / 0.65f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    // 가로로 가운데가 진한 선 (빛나는 길)
    static Sprite MakeSoftLine()
    {
        var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[4 * 64];
        for(int y = 0; y < 64; y++) {
            float d = Mathf.Abs(y + 0.5f - 32f) / 32f;
            byte a = (byte)(Mathf.Exp(-d * d * 4.5f) * 255);
            for(int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new UnityEngine.Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
    }
    static Sprite MakeDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float r = size / 2f;
        for(int y = 0; y < size; y++)
            for(int x = 0; x < size; x++) {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    // CSS border: N px dashed 원 (지금 행성 고리)
    static Sprite MakeDashRing(int size, float radius, float width, int dashes)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float c = size / 2f;
        for(int y = 0; y < size; y++)
            for(int x = 0; x < size; x++) {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(width / 2f - Mathf.Abs(d - radius) + 0.5f);
                float ang = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f) * dashes;
                if(ang - Mathf.Floor(ang) > 0.5f) a = 0f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    readonly Dictionary<int, Sprite> ringCache = new Dictionary<int, Sprite>();
    // 고리: 지름 diam px, 굵기 bw px
    Sprite Ring(float diam, float bw)
    {
        int key = Mathf.RoundToInt(diam * 10 + bw);
        if(ringCache.TryGetValue(key, out var s)) return s;
        int size = 256;
        float scale = size / (diam + bw);   // 미리보기 1px = 그림 이만큼
        int dashes = Mathf.Max(8, Mathf.RoundToInt(Mathf.PI * diam / (bw * 6f)));
        s = MakeDashRing(size, diam / 2f * scale, bw * scale, dashes);
        ringCache[key] = s;
        return s;
    }

    // ---- CSS filter (미리보기와 같은 계산, sRGB). 행성 그림 픽셀에 바로 적용해요
    enum PlanetLook { Normal, Parallel, Abyss, Locked, Far, ParallelLocked, ParallelFar, AbyssLocked, AbyssFar }
    readonly Dictionary<string, Sprite> lookCache = new Dictionary<string, Sprite>();
    Sprite PlanetSprite(Sprite src, PlanetLook look)
    {
        if(src == null || look == PlanetLook.Normal) return src;
        string key = src.GetInstanceID() + "_" + look;
        if(lookCache.TryGetValue(key, out var s)) return s;
        s = src;
        try {
            var r = src.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var rtex = RenderTexture.GetTemporary(src.texture.width, src.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src.texture, rtex);
            var prev = RenderTexture.active;
            RenderTexture.active = rtex;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new UnityEngine.Rect(r.x, r.y, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rtex);
            var px = tex.GetPixels();
            for(int i = 0; i < px.Length; i++) px[i] = ApplyLook(px[i], look);
            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
        } catch(Exception e) { Debug.LogWarning("[행성 지도] 행성 색 바꾸기 실패: " + e.Message); }
        lookCache[key] = s;
        return s;
    }
    static Color ApplyLook(Color c, PlanetLook look)
    {
        switch(look) {
            case PlanetLook.Parallel: return Saturate(HueRotate(c, 150f), 1.3f);
            case PlanetLook.ParallelLocked: return Saturate(Bright(Saturate(HueRotate(c, 150f), 1.3f), 0.2f), 0.2f);
            case PlanetLook.ParallelFar: return Saturate(Bright(Saturate(HueRotate(c, 150f), 1.3f), 0.15f), 0f);
            case PlanetLook.Abyss: return AbyssLook(c);
            case PlanetLook.AbyssLocked: return Bright(AbyssLook(c), 0.3f);
            case PlanetLook.AbyssFar: return Bright(AbyssLook(c), 0.25f);
            case PlanetLook.Locked: return Saturate(Bright(c, 0.2f), 0.2f);
            case PlanetLook.Far: return Saturate(Bright(c, 0.15f), 0f);
        }
        return c;
    }
    // grayscale(1) sepia(1) saturate(5) hue-rotate(-28deg) brightness(.7) contrast(1.35)
    static Color AbyssLook(Color c) => Contrast(Bright(HueRotate(Saturate(Sepia(Gray(c)), 5f), -28f), 0.7f), 1.35f);
    static Color Mat(Color c, float a, float b, float d, float e, float f, float g, float h, float i, float j)
    {
        return new Color(Mathf.Clamp01(a * c.r + b * c.g + d * c.b), Mathf.Clamp01(e * c.r + f * c.g + g * c.b), Mathf.Clamp01(h * c.r + i * c.g + j * c.b), c.a);
    }
    static Color Gray(Color c) => Mat(c, 0.2126f, 0.7152f, 0.0722f, 0.2126f, 0.7152f, 0.0722f, 0.2126f, 0.7152f, 0.0722f);
    static Color Sepia(Color c) => Mat(c, 0.393f, 0.769f, 0.189f, 0.349f, 0.686f, 0.168f, 0.272f, 0.534f, 0.131f);
    static Color Saturate(Color c, float s) => Mat(c, 0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s, 0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s, 0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s);
    static Color HueRotate(Color c, float deg)
    {
        float r = deg * Mathf.Deg2Rad, co = Mathf.Cos(r), si = Mathf.Sin(r);
        return Mat(c,
            0.213f + co * 0.787f - si * 0.213f, 0.715f - co * 0.715f - si * 0.715f, 0.072f - co * 0.072f + si * 0.928f,
            0.213f - co * 0.213f + si * 0.143f, 0.715f + co * 0.285f + si * 0.140f, 0.072f - co * 0.072f - si * 0.283f,
            0.213f - co * 0.213f - si * 0.787f, 0.715f - co * 0.715f + si * 0.715f, 0.072f + co * 0.928f + si * 0.072f);
    }
    static Color Bright(Color c, float b) => new Color(Mathf.Clamp01(c.r * b), Mathf.Clamp01(c.g * b), Mathf.Clamp01(c.b * b), c.a);
    static Color Contrast(Color c, float k) => new Color(Mathf.Clamp01(k * c.r + 0.5f - 0.5f * k), Mathf.Clamp01(k * c.g + 0.5f - 0.5f * k), Mathf.Clamp01(k * c.b + 0.5f - 0.5f * k), c.a);
    #endregion

    #region 틀 (배경 · 위 재화·차원 탭 · 지도 · 이어서 하기 · 아래 메뉴)
    void BuildMapShell()
    {
        softCircle = MakeHalo(128);
        softLine = MakeSoftLine();
        dotSprite = MakeDot(32);
        smallRound = MakeRounded(32, 9);
        MapFont();
        mapRoot = Stretch("RemasterPlanetMap", stagesMap.transform);
        // 옛날 캔버스는 가로·세로 반반 맞춤이라 가로 길이가 기기마다 달라요 → 가로 1080 기준 틀을 만들어 크기를 맞춰요
        var fit = mapRoot.gameObject.AddComponent<RemasterWidthFit>();
        mapFrame = Rect("Frame", mapRoot, new Vector2(1080, 2340), Vector2.zero);
        fit.target = mapFrame;
        fit.Apply();

        // 배경 (미리보기 배경 그림, 화면을 덮게)
        var bgRt = Stretch("Bg", mapFrame);
        var bgBlock = bgRt.gameObject.AddComponent<Image>();
        bgBlock.color = Hex("#0a0f33");
        bgBlock.raycastTarget = true;   // 아래 옛날 화면을 못 누르게
        var art = Rect("BgArt", bgRt, new Vector2(1080, 2340), Vector2.zero);
        mapBg = art.gameObject.AddComponent<Image>();
        mapBg.raycastTarget = false;
        var arf = art.gameObject.AddComponent<AspectRatioFitter>();
        arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        arf.aspectRatio = 1080f / 2340f;

        // 지도 (아래 메뉴 위까지)
        var vp = Stretch("Viewport", mapFrame);
        vp.offsetMin = new Vector2(0, (NavH + 16f) * K);
        vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.001f);
        mapScroll = vp.gameObject.AddComponent<ScrollRect>();
        mapScroll.horizontal = false;
        mapScroll.vertical = true;
        mapScroll.movementType = ScrollRect.MovementType.Elastic;
        mapScroll.scrollSensitivity = 40f;
        mapScroll.viewport = vp;

        // 위 (재화·차원 탭): 화면 위에 붙어요. 안쪽은 미리보기 px 좌표
        mapTop = Rect("Top", mapFrame, new Vector2(1080, 100 * K), Vector2.zero);
        mapTop.anchorMin = mapTop.anchorMax = new Vector2(0.5f, 1f);
        mapTop.pivot = new Vector2(0.5f, 1f);
        mapTop.anchoredPosition = Vector2.zero;
        // 이어서 하기: 미리보기 top 640 ~ 700 → 화면 아래에서 80px
        mapQuick = Rect("Quick", mapFrame, new Vector2(1080, 60 * K), Vector2.zero);
        mapQuick.anchorMin = mapQuick.anchorMax = new Vector2(0.5f, 0f);
        mapQuick.pivot = new Vector2(0.5f, 0f);
        mapQuick.anchoredPosition = new Vector2(0, (NavH + 16f) * K);
        // 행성 창 자리 (어둡게 + 아래에서 올라온 판)
        sheetLayer = Stretch("Sheet", mapFrame);
        sheetLayer.gameObject.SetActive(false);
        // 아래 메뉴
        mapNav = Rect("Nav", mapFrame, new Vector2(1080, NavH * K), Vector2.zero);
        mapNav.anchorMin = mapNav.anchorMax = new Vector2(0.5f, 0f);
        mapNav.pivot = new Vector2(0.5f, 0f);
        mapNav.anchoredPosition = Vector2.zero;
    }

    void CloseStagesMap()
    {
        CloseSheet();
        if(stagesMap != null) stagesMap.SetActive(false);
        if(SoundManager.Instance != null) { SoundManager.Instance.playClickSFX(); SoundManager.Instance.playMainTitleMusic(); }
    }

    static void Clear(Transform t) { for(int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }

    // 위: 재화 3칸 (left 10, top 10, 높이 30, 사이 6) + 차원 탭 3개 (left 12, top 50, 108×44, 사이 6)
    //  (mapTop 은 화면 가운데 기준 폭 1080 이라 왼쪽 위 = 미리보기 0,0)
    void BuildTop(int dim)
    {
        Clear(mapTop);
        float x = 10f;
        var stone = UpgradeStoneManager.instance != null ? UpgradeStoneManager.instance.getStone(UpgradeStone.TypeOfStone.Normal) : null;
        var res = new List<(string icon, float size, string text)> {
            ("ItemIcon_Money_Coin", 24f, GameManager.instance != null ? GameManager.instance.MoneyStringForInfiniteStage(GameManager.GetMoney()) : "0"),
            ("ItemIcon_Gem_Diamond_Purple", 24f, GameManager.GetDiamond().ToString("N0")),
            ("Ore", 22f, (stone != null ? stone.StoneAmount : 0).ToString("N0")),
        };
        foreach(var r in res) {
            float tw = MeasureText(r.text, 15f);
            float w = 2f + 3f + 24f + 4f + tw + 10f + 2f;   // 테두리·안쪽 여백·아이콘·사이·글자
            Plate(mapTop, "res", x, 10f, w, 30f, 1f);
            PxIcon(mapTop, r.icon, x + 2f + 3f + 12f, 25f, r.size);
            PxText(mapTop, r.text, x + 2f + 3f + 24f + 4f, 25f, 15f, Color.white, 0f, null, TextAlignmentOptions.Left);
            x += w + 6f;
        }
        string[] names = { "일반", "평행", "심연" };
        string[] keys = { "normal", "parallel", "abyss" };
        for(int d = 0; d < 3; d++) {
            int dd = d;
            float tx = 12f + d * 114f, ty = 50f;
            bool on = d == dim, open = DimOpen(d);
            if(on) ty -= 2f;   // translateY(-2px)
            if(on) Plate(mapTop, "tab_on_" + keys[d], tx, ty, 108f, 44f, 22f);
            else Plate(mapTop, "tab_off", tx, ty, 108f, 44f, 4f);
            var hit = PxBox("Tab" + d, mapTop, tx, ty, 108f, 44f);
            hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            hit.gameObject.AddComponent<Button>().onClick.AddListener(() => OnDimTab(dd));
            // 글자: [자물쇠] 이름 [작은 숫자] 를 가운데로
            string sub = "";
            if(open && d != PlanetDimension.Abyss) {
                int done = 0, n = DimCount(d);
                for(int k = 0; k < n; k++) if(PlanetDone(d, PlanetOf(d, k))) done++;
                sub = $"{done}/{n}";
            }
            bool lockIcon = !open;
            float wName = MeasureText(names[d], 16f), wSub = sub.Length > 0 ? MeasureText(sub, 11f) : 0f;
            float total = (lockIcon ? 16f + 5f : 0f) + wName + (sub.Length > 0 ? 5f + wSub : 0f);
            float cx = tx + 54f - total / 2f, cy = ty + 22f;
            if(lockIcon) { PxIcon(mapTop, "Icon_Lock03", cx + 8f, cy, 16f); cx += 21f; }
            PxText(mapTop, names[d], cx, cy, 16f, Color.white, 2f, Navy, TextAlignmentOptions.Left);
            cx += wName + 5f;
            if(sub.Length > 0) { var st = PxText(mapTop, sub, cx, cy + 1f, 11f, Color.white, 1f, Navy, TextAlignmentOptions.Left); st.alpha = 0.85f; }
        }
    }

    // 아래 메뉴: 미리보기와 같은 아이콘 5개 (상점 · 장비 · 모험 · 함선 · 설정). 가운데 '모험'이 튀어나와요
    //  누르면 지도를 닫고 옛날 로비의 그 버튼을 눌러 줘요 (모험 = 지도 닫기)
    void BuildNav(int dim)
    {
        Clear(mapNav);
        var navBg = Plate(mapNav, "nav_bg", 0f, 0f, 360f, NavH, 0f);
        navBg.raycastTarget = true;
        var bottom = GameObject.Find("Bottom Buttons");
        Button Old(string name) { var t = bottom != null ? bottom.transform.Find(name) : null; return t != null ? t.GetComponent<Button>() : null; }
        var menu = GameObject.Find("PopUp Button");
        var items = new (string icon, Button target)[] {
            ("ItemIcon_Shop", Old("Shop")), ("ItemIcon_Gear_Sword", Old("Equips")), ("ItemIcon_Earth", null),
            ("ItemIcon_Battle", Old("Upgrade Space Ship")), ("ItemIcon_Gear", menu != null ? menu.GetComponent<Button>() : null),
        };
        float[] cx = { 34.2f, 102.6f, 180f, 257.4f, 325.8f };
        string navKey = dim == PlanetDimension.Abyss ? "red" : dim == PlanetDimension.Parallel ? "purple" : "blue";
        for(int i = 0; i < items.Length; i++) {
            var it = items[i];
            if(i == 2) {
                Plate(mapNav, "nav_on_" + navKey, cx[i] - 37f, -9f, 74f, 68f, 6f);
                PxIcon(mapNav, it.icon, cx[i], 15.5f, 34f);
                PxText(mapNav, "모험", cx[i], 42.5f, 15f, Color.white, 1f, Navy);
                var hit = PxBox("Adventure", mapNav, cx[i] - 37f, -9f, 74f, 68f);
                hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
                hit.gameObject.AddComponent<Button>().onClick.AddListener(CloseStagesMap);
                continue;
            }
            var cell = PxBox("Menu" + i, mapNav, cx[i] - 28f, 7f, 56f, 50f);
            cell.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            var icon = PxIcon(cell, it.icon, 28f, 25f, 28f);
            icon.color = new Color(1, 1, 1, 0.55f);
            var target = it.target;
            cell.gameObject.AddComponent<Button>().onClick.AddListener(() => {
                CloseStagesMap();
                if(target != null) target.onClick.Invoke();
            });
        }
    }
    #endregion

    #region 차원·잠금
    static int DimCount(int dim)
    {
        var sm = StageManager.instance;
        int loop = Mathf.Clamp(PlanetDimension.LoopStart, 0, sm.planets.Count);
        return dim == PlanetDimension.Normal ? loop : sm.planets.Count - loop;
    }
    static int PlanetOf(int dim, int k) => dim == PlanetDimension.Normal ? k : PlanetDimension.LoopStart + k;

    // 평행 우주: 일반 행성 planet_parallel_need(14)개를 다 깨면 (예전에 평행 맵을 깬 적이 있으면 그대로 열려요). 0 이면 처음부터
    static bool DimOpen(int dim)
    {
        var sm = StageManager.instance;
        if(dim == PlanetDimension.Normal || AugmentManager.TestOpenAll) return true;
        int loop = PlanetDimension.LoopStart;
        if(dim == PlanetDimension.Parallel) {
            int need = Tuning.GetInt("planet_parallel_need", 14);
            if(need <= 0) return true;
            int last = Mathf.Min(need, loop) - 1;
            if(last >= 0 && last < sm.planets.Count && StageManager.AllCleared(sm.planets[last])) return true;
            for(int i = loop; i < sm.planets.Count; i++) if(sm.planets[i].StagesOfThisPlanet.Length > 0 && sm.planets[i].StagesOfThisPlanet[0].StageCleared) return true;
            return false;
        }
        for(int i = loop; i < sm.planets.Count; i++) if(AugmentManager.AbyssOpen(i)) return true;
        return false;
    }
    static string DimLockText(int dim) => dim == PlanetDimension.Parallel
        ? $"평행 우주는 일반 행성 {Tuning.GetInt("planet_parallel_need", 14)}개를 다 깨면 열려요"
        : "심연은 평행 행성을 하나 다 깨면 열려요";

    static bool PlanetOpen(int dim, int k)
    {
        var sm = StageManager.instance;
        int p = PlanetOf(dim, k);
        if(p < 0 || p >= sm.planets.Count) return false;
        if(AugmentManager.TestOpenAll && dim != PlanetDimension.Normal) return true;
        if(dim == PlanetDimension.Abyss) return AugmentManager.AbyssOpen(p);
        if(k == 0) return DimOpen(dim);
        return StageManager.AllCleared(sm.planets[p - 1]);
    }
    bool MapCleared(int dim, int p, int m)
    {
        var maps = StageManager.instance.planets[p].StagesOfThisPlanet;
        if(m < 0 || m >= maps.Length) return false;
        return dim == PlanetDimension.Abyss ? mgr.AbyssMapCleared(p, m) : maps[m].StageCleared;
    }
    int ClearedMaps(int dim, int p)
    {
        int n = 0, len = StageManager.instance.planets[p].StagesOfThisPlanet.Length;
        for(int m = 0; m < len; m++) if(MapCleared(dim, p, m)) n++;
        return n;
    }
    bool PlanetDone(int dim, int p) => ClearedMaps(dim, p) >= StageManager.instance.planets[p].StagesOfThisPlanet.Length;
    int NextMap(int dim, int p)
    {
        int len = StageManager.instance.planets[p].StagesOfThisPlanet.Length;
        for(int m = 0; m < len; m++) if(!MapCleared(dim, p, m)) return m;
        return Mathf.Max(0, len - 1);
    }
    bool MapOpen(int dim, int p, int m) => m == 0 || MapCleared(dim, p, m - 1) || (AugmentManager.TestOpenAll && dim != PlanetDimension.Normal);
    // 이 차원의 '지금 행성' = 열렸고 아직 다 못 깬 가장 앞 행성 (다 깼으면 마지막 열린 행성)
    int CurrentK(int dim)
    {
        int n = DimCount(dim), lastOpen = 0;
        for(int k = 0; k < n; k++) {
            if(!PlanetOpen(dim, k)) continue;
            lastOpen = k;
            if(!PlanetDone(dim, PlanetOf(dim, k))) return k;
        }
        return lastOpen;
    }
    static string WeaponOf(int p)
    {
        var w = AugmentManager.PlanetWeapon(p);
        return w != SideWeapon.None ? AugmentDefs.WeaponName(w) : "자유 선택";
    }
    string PlanetTitle(int dim, int p) => PlanetDimension.Prefix(dim) + StageManager.instance.planets[p].PlanetName;
    string LockReason(int dim, int k)
    {
        var sm = StageManager.instance;
        int p = PlanetOf(dim, k);
        if(dim == PlanetDimension.Abyss) return $"평행 {sm.planets[p].PlanetName}를 깨면 열려요";
        if(k == 0) return DimLockText(dim);
        return $"{PlanetTitle(dim, p - 1)}를 다 깨면 열려요";
    }
    #endregion

    #region 지도
    void OnDimTab(int d)
    {
        if(!DimOpen(d)) { Toast(DimLockText(d), Color.white, true); return; }
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        ShowDim(d);
    }

    void ShowDim(int dim)
    {
        mapDim = dim;
        mapBg.sprite = MapSprite(dim == PlanetDimension.Abyss ? "bg_abyss" : dim == PlanetDimension.Parallel ? "bg_parallel" : "bg_normal");
        mapBg.transform.parent.GetComponent<Image>().color = dim == PlanetDimension.Abyss ? Hex("#070103") : dim == PlanetDimension.Parallel ? Hex("#14062b") : Hex("#0a0f33");
        BuildTop(dim);
        BuildNav(dim);
        BuildRoute(dim);
    }

    // 행성 k 의 가운데 (지도 안 px, 위가 0). 미리보기: 짝수 x 97, 홀수 x 256, 아래에서 위로
    Vector2 NodePx(int k, float contentH) => new Vector2(k % 2 == 0 ? 97f : 256f, contentH - (MapBottomPad + k * NodeGap));

    void BuildRoute(int dim)
    {
        if(mapContent != null) Destroy(mapContent.gameObject);
        shipRt = null; curRing = null;
        int n = DimCount(dim);
        float ch = MapBottomPad + Mathf.Max(0, n - 1) * NodeGap + MapTopPad;   // px
        mapContent = Rect("Content", mapScroll.viewport, new Vector2(1080, ch * K), Vector2.zero);
        mapContent.anchorMin = mapContent.anchorMax = new Vector2(0.5f, 0f);
        mapContent.pivot = new Vector2(0.5f, 0f);
        mapContent.anchoredPosition = Vector2.zero;
        mapScroll.content = mapContent;
        int cur = CurrentK(dim);
        var acc = PlanetDimension.Accent(dim);
        Color travelled = dim == PlanetDimension.Abyss ? Hex("#ff3b4f") : Hex("#ffd23f");

        // 길: 빛(acc 28%, 굵기 12 + 흐림 3) · 점선(흰 40%, 점 3px 간격 11px) · 지나온 길(굵기 5 + 빛 6)
        var route = PxBox("Route", mapContent, 0, 0, 360, ch);
        for(int k = 0; k + 1 < n; k++) {
            Vector2 a = NodePx(k, ch), b = NodePx(k + 1, ch);
            var pts = new List<Vector2>();
            Vector2 c1 = a + new Vector2(0, -80f), c2 = b + new Vector2(0, 80f);   // 미리보기: C a.x,a.y-80 b.x,b.y+80
            for(int i = 0; i <= 40; i++) {
                float t = i / 40f, u = 1 - t;
                pts.Add(u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b);
            }
            Stroke(route, pts, 22f, new Color(acc.r, acc.g, acc.b, 0.28f), softLine);
            Dots(route, pts, 3f, 11f, new Color(1, 1, 1, 0.4f));
            if(k < cur) {
                Stroke(route, pts, 18f, new Color(travelled.r, travelled.g, travelled.b, 0.55f), softLine);
                Stroke(route, pts, 5f, travelled, smallRound);
            }
        }
        BuildHome(route, dim, ch, travelled);
        for(int k = 0; k < n; k++) BuildNode(dim, k, cur, NodePx(k, ch));
        BuildQuick(dim, cur);

        // 지금 행성이 화면 가운데쯤 오게
        Canvas.ForceUpdateCanvases();
        float viewH = mapScroll.viewport.rect.height;
        // (2026-11-25 사용자: 첫 행성이 지금 행성이면 지구 아래 '출발'이 잘려요 → 살짝 내려서) 지금 행성을 화면 아래에서 planet_map_focus(55%) 높이에
        float target = (MapBottomPad + cur * NodeGap) * K - viewH * Mathf.Clamp(Tuning.Get("planet_map_focus", 0.55f), 0.2f, 0.8f);
        float range = Mathf.Max(1f, ch * K - viewH);
        mapScroll.verticalNormalizedPosition = Mathf.Clamp01(target / range);
    }

    // 출발점: 우리 행성 '지구' (k = -1 자리, 오른쪽). 첫 행성까지 금색 길 (모험을 시작했으니 언제나 지나온 길)
    void BuildHome(Transform route, int dim, float ch, Color travelled)
    {
        Vector2 a = NodePx(-1, ch), b = NodePx(0, ch);
        var pts = new List<Vector2>();
        Vector2 c1 = a + new Vector2(0, -80f), c2 = b + new Vector2(0, 80f);
        for(int i = 0; i <= 40; i++) {
            float t = i / 40f, u = 1 - t;
            pts.Add(u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b);
        }
        var acc = PlanetDimension.Accent(dim);
        Stroke(route, pts, 22f, new Color(acc.r, acc.g, acc.b, 0.28f), softLine);
        Stroke(route, pts, 18f, new Color(travelled.r, travelled.g, travelled.b, 0.55f), softLine);
        Stroke(route, pts, 5f, travelled, smallRound);

        var node = PxBox("Home", mapContent, a.x, a.y, 0f, 0f);
        Halo(node, 0, 0, 150f, new Color(80 / 255f, 160 / 255f, 1f, 0.35f));
        var ring = PxAt("Ring", node, 0, 0, 100f, 100f).gameObject.AddComponent<Image>();
        ring.sprite = Ring(98f, 2f); ring.color = new Color(0.6f, 0.85f, 1f, 0.45f); ring.raycastTarget = false;
        var img = PxAt("Img", node, 0, 0, 76f, 76f).gameObject.AddComponent<Image>();
        img.sprite = Skin("HomeEarth"); img.preserveAspect = true;
        PxIcon(node, "PictoIcon_Flag", 38f - 14f, -38f - 10f + 14f, 28f, 2f).color = Hex("#ffd23f");
        float y = 38f - 6f;
        y += NamePlate(node, "plate_blue", "지구", 0f, y, 15f, Color.white);
        Chip(node, "chip_yellow", null, "출발", 0f, y + 5f, Hex("#3a2000"), true);
    }

    // 선 (점들을 잇는 짧은 판)
    void Stroke(Transform parent, List<Vector2> pts, float width, Color color, Sprite sprite)
    {
        for(int i = 0; i + 1 < pts.Count; i++) {
            Vector2 a = pts[i], b = pts[i + 1];
            float len = Vector2.Distance(a, b);
            if(len < 0.01f) continue;
            var rt = PxAt("Seg", parent, (a.x + b.x) / 2f, (a.y + b.y) / 2f, len + (sprite == smallRound ? width : 0.6f), width);
            rt.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            if(sprite == smallRound) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 9f / Mathf.Max(1f, width * K / 2f); }
            img.color = color;
            img.raycastTarget = false;
        }
    }
    // 점선 (dasharray 2 9 + 둥근 끝 → 점 지름 size, 간격 gap)
    void Dots(Transform parent, List<Vector2> pts, float size, float gap, Color color)
    {
        float acc = 0f, next = gap * 0.5f;
        for(int i = 0; i + 1 < pts.Count; i++) {
            Vector2 a = pts[i], b = pts[i + 1];
            float len = Vector2.Distance(a, b);
            while(next <= acc + len) {
                var p = Vector2.Lerp(a, b, (next - acc) / Mathf.Max(0.001f, len));
                var rt = PxAt("Dot", parent, p.x, p.y, size + 1f, size + 1f);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = dotSprite;
                img.color = color;
                img.raycastTarget = false;
                next += gap;
            }
            acc += len;
        }
    }

    Image Halo(Transform parent, float cx, float cy, float size, Color color)
    {
        var rt = PxAt("Halo", parent, cx, cy, size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = softCircle;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 작은 띠 (칩): 높이 24, 글자 13px, 아이콘 16 (왼쪽 여백 5) 또는 글자만 (여백 9). 폭을 돌려줘요
    float Chip(Transform parent, string sprite, string icon, string text, float x, float y, Color? textColor = null, bool centerX = false)
    {
        float tw = MeasureText(text, 13f);
        float w = 1.5f + (icon != null ? 5f + 16f + 4f : 9f) + tw + 9f + 1.5f;
        if(centerX) x -= w / 2f;
        Plate(parent, sprite, x, y, w, 24f, 1f);
        float cx = x + 1.5f + (icon != null ? 5f : 9f);
        if(icon != null) { PxIcon(parent, icon, cx + 8f, y + 12f, 16f); cx += 20f; }
        PxText(parent, text, cx, y + 12f, 13f, textColor ?? Color.white, 0f, null, TextAlignmentOptions.Left);
        return w;
    }

    // 진행 칸 8개 (14×8, 사이 4, 보스 맵은 안쪽 빨간 테두리 2px)
    void Pips(Transform parent, int dim, int p, float cx, float top)
    {
        var maps = StageManager.instance.planets[p].StagesOfThisPlanet;
        int n = maps.Length, next = NextMap(dim, p);
        bool all = PlanetDone(dim, p);
        float total = n * 14f + (n - 1) * 4f, x = cx - total / 2f;
        Color clr = dim == PlanetDimension.Abyss ? Hex("#ff6a3d") : Hex("#3ee07a");
        Color nxt = dim == PlanetDimension.Abyss ? Hex("#ff3b4f") : Hex("#ffd23f");
        for(int m = 0; m < n; m++) {
            bool c = MapCleared(dim, p, m);
            bool isNext = !all && m == next;
            Color col = c ? clr : isNext ? nxt : new Color(1, 1, 1, 0.16f);
            float px = x + m * 18f;
            if(isNext) Halo(parent, px + 7f, top + 4f, 34f, new Color(nxt.r, nxt.g, nxt.b, 0.6f));   // box-shadow 0 0 8px
            var img = PxBox("Pip", parent, px, top, 14f, 8f).gameObject.AddComponent<Image>();
            img.sprite = smallRound; img.type = Image.Type.Sliced; img.color = maps[m].bossPrefab.Length > 0 ? Hex("#ff4a5a") : col; img.raycastTarget = false;
            if(maps[m].bossPrefab.Length > 0) {
                var inner = PxBox("PipIn", parent, px + 2f, top + 2f, 10f, 4f).gameObject.AddComponent<Image>();
                inner.sprite = smallRound; inner.type = Image.Type.Sliced; inner.pixelsPerUnitMultiplier = 3f; inner.raycastTarget = false;
                inner.color = col.a < 0.5f ? Hex("#262b52") : col;
            }
        }
    }

    // 이름판: 글자 size px, 안쪽 여백 3/12/4, 테두리 2.5 → 높이 = 글자줄 + 12. 높이를 돌려줘요
    float NamePlate(Transform parent, string sprite, string text, float cx, float top, float size, Color textColor, string checkIcon = null)
    {
        float tw = MeasureText(text, size) + (checkIcon != null ? 4f + 14f : 0f);
        float w = tw + 24f + 5f, h = size * 1.2f + 12f;
        Plate(parent, sprite, cx - w / 2f, top, w, h, 4f);
        float x = cx - tw / 2f;
        PxText(parent, text, x, top + h / 2f, size, textColor, sprite == "plate_dim" ? 0f : 2f, Navy, TextAlignmentOptions.Left);
        if(checkIcon != null) PxIcon(parent, checkIcon, x + tw - 7f, top + h / 2f + 1f, 14f);
        return h;
    }

    void BuildNode(int dim, int k, int cur, Vector2 c)
    {
        var sm = StageManager.instance;
        int p = PlanetOf(dim, k);
        if(p >= sm.planets.Count) return;
        var planet = sm.planets[p];
        bool open = PlanetOpen(dim, k);
        bool current = k == cur && open;
        bool done = open && PlanetDone(dim, p);
        bool conquered = dim == PlanetDimension.Abyss && mgr.AbyssConquered(p);
        bool far = !open && k > cur + 1;
        bool abyss = dim == PlanetDimension.Abyss, par = dim == PlanetDimension.Parallel;
        var acc = PlanetDimension.Accent(dim);

        // 노드: 이 안의 (0,0) = 행성 가운데 (미리보기 px)
        var node = PxBox("Planet" + (p + 1), mapContent, c.x, c.y, 0f, 0f);
        if(far) node.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f;
        float size = current ? 112f : far ? 64f : 80f;

        // 빛·고리
        if(current) {
            Halo(node, 0, 0, abyss ? 210f : 200f, abyss ? new Color(1f, 40 / 255f, 60 / 255f, 0.5f) : par ? new Color(200 / 255f, 90 / 255f, 1f, 0.45f) : new Color(80 / 255f, 1f, 170 / 255f, 0.38f));
            var ring = PxAt("Ring", node, 0, 0, 148f + 3f, 148f + 3f);
            var ri = ring.gameObject.AddComponent<Image>();
            ri.sprite = Ring(148f, 3f);
            ri.color = abyss ? new Color(1f, 90 / 255f, 90 / 255f, 0.8f) : new Color(1f, 230 / 255f, 120 / 255f, 0.75f);
            ri.raycastTarget = false;
            curRing = ring;
        } else if(conquered) Halo(node, 0, 0, 150f, new Color(1f, 200 / 255f, 60 / 255f, 0.38f));
        else if(done) Halo(node, 0, 0, 140f, abyss ? new Color(1f, 0.4f, 0.3f, 0.3f) : new Color(80 / 255f, 160 / 255f, 1f, 0.32f));
        else if(open) Halo(node, 0, 0, 140f, new Color(acc.r, acc.g, acc.b, 0.28f));
        else if(!far && !abyss) {
            var ring = PxAt("Ring", node, 0, 0, 98f + 2f, 98f + 2f).gameObject.AddComponent<Image>();
            ring.sprite = Ring(98f, 2f); ring.color = new Color(1, 1, 1, 0.25f); ring.raycastTarget = false;
        }

        // 행성 그림 (미리보기와 같은 색 필터)
        PlanetLook look = abyss ? (open ? PlanetLook.Abyss : far ? PlanetLook.AbyssFar : PlanetLook.AbyssLocked)
                        : par ? (open ? PlanetLook.Parallel : far ? PlanetLook.ParallelFar : PlanetLook.ParallelLocked)
                        : (open ? PlanetLook.Normal : far ? PlanetLook.Far : PlanetLook.Locked);
        var img = PxAt("Img", node, 0, 0, size, size).gameObject.AddComponent<Image>();
        img.sprite = PlanetSprite(planet.PlanetSprite, look);
        img.preserveAspect = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => {
            if(!open) return;   // 잠긴 이유는 행성 아래에 써 있어요 (문구는 최소한으로, 2026-11-25 사용자)
            if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
            OpenSheet(dim, p);
        });
        if(!open && !far) {
            var lk = PxIcon(node, "Icon_Lock03", 0, 0, 30f);
            if(abyss) lk.color = new Color(1f, 0.55f, 0.5f);
        }
        if(done && !conquered && !current) PxIcon(node, "ItemIcon_Trophy_Gold", size / 2f + 14f - 16f, -size / 2f - 14f + 16f, 32f, 2f);
        if(conquered) PxIcon(node, "ItemIcon_Badge_Crown", size / 2f + 16f - 19f, -size / 2f - 18f + 19f, 38f, 2f);

        // 내 우주선 + '현재 위치' (심연 화면엔 없어요, 미리보기 그대로)
        //  (2026-11-25 사용자: 잘려요) 미리보기는 행성이 오른쪽이라 왼쪽에 붙였는데, 행성이 왼쪽이면 오른쪽에 붙여요
        if(current && !abyss) {
            float side = c.x < 180f ? -1f : 1f;   // 1 = 왼쪽에 (미리보기), -1 = 오른쪽에
            var ship = PxAt("Ship", node, side * (-56f - 48f + 23f), -56f + 2f + 23f, 46f, 46f);
            var si = ship.gameObject.AddComponent<Image>();
            si.sprite = Skin("Ship_LVL_1"); si.preserveAspect = true; si.raycastTarget = false;
            ship.localRotation = Quaternion.Euler(0, 0, 28f * side);
            var glow = ship.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(79 / 255f, 195 / 255f, 1f, 0.35f);
            glow.effectDistance = new Vector2(6, 6);
            shipRt = ship; shipBase = ship.anchoredPosition;
            float chipW = 1.5f + 9f + MeasureText("현재 위치", 13f) + 9f + 1.5f;
            Chip(node, "chip_yellow", null, "현재 위치", side > 0 ? -56f - 78f : 56f + 78f - chipW, -56f + 52f, Hex("#3a2000"));
        }

        // 아래: 이름판 (행성 아래 -6px) → 진행 칸·띠
        float y = size / 2f - 6f;
        string title = !open && far ? (abyss ? "봉인" : "???") : PlanetTitle(dim, p);
        string plateSprite = !open ? "plate_dim" : conquered ? "plate_gold" : abyss && current ? "plate_red" : "plate_blue";
        float pSize = current ? (abyss ? 17f : 18f) : 15f;
        y += NamePlate(node, plateSprite, title, 0f, y, pSize, open ? Color.white : Hex("#9aa0b8"), done && !conquered && !current ? "PictoIcon_Check_1" : null);
        if(!open) {
            if(!far) PxText(node, LockReason(dim, k), 0f, y + 4f + 7f, 12f, abyss ? Hex("#ffb3ae") : Hex("#aab3d6"), 1f, abyss ? Hex("#1a0004") : Navy);
            return;
        }
        if(current && abyss) {
            y += 6f;
            var chips = new[] { "체력 " + PlanetDimension.HpText(dim, p), $"규칙 {PlanetDimension.Rules(p, NextMap(dim, p), dim).Count}개", "정예" };
            float tot = -4f;
            foreach(var t in chips) tot += 1.5f * 2f + 9f * 2f + MeasureText(t, 13f) + 4f;
            float x = -tot / 2f;
            foreach(var t in chips) x += Chip(node, "chip_abyss", null, t, x, y) + 4f;
            y += 24f + 6f;
            Pips(node, dim, p, 0f, y);
            return;
        }
        if(current) {
            y += 6f;
            Pips(node, dim, p, 0f, y);
            y += 8f + 6f;
            Chip(node, "chip_dark", "ItemIcon_Gear_Sword", $"{WeaponOf(p)} 행성", 0f, y, null, true);
            if(par) {
                y += 24f + 4f;
                Chip(node, "chip_purple", null, "규칙 · " + PlanetDimension.RulesShort(PlanetDimension.Rules(p, NextMap(dim, p), dim)), 0f, y, null, true);
            }
            return;
        }
        if(conquered) {
            y += 5f;
            var w = AugmentManager.PlanetWeapon(p);
            string t = w != SideWeapon.None ? $"정복! {AugmentDefs.WeaponName(w)} +{Mathf.RoundToInt(Tuning.Get("planet_abyss_conquer_dmg", 0.2f) * 100)}%" : $"정복! 모든 무기 +{Mathf.RoundToInt(Tuning.Get("planet_abyss_conquer_free", 0.05f) * 100)}%";
            Chip(node, "chip_gold", "ItemIcon_Gear_Sword", t, 0f, y, Hex("#ffe27a"), true);
            return;
        }
        if(!done) { y += 5f; Pips(node, dim, p, 0f, y); }
    }

    // 이어서 하기 (미리보기: 336×60, 행성 44, 글자 12/20, 출격 버튼 112×46)
    void BuildQuick(int dim, int cur)
    {
        Clear(mapQuick);
        int p = PlanetOf(dim, cur);
        var sm = StageManager.instance;
        if(p >= sm.planets.Count || !PlanetOpen(dim, cur)) return;
        int m = NextMap(dim, p);
        bool all = PlanetDone(dim, p);
        bool abyss = dim == PlanetDimension.Abyss;
        var q = PxBox("Bar", mapQuick, 12f, 0f, 336f, 60f);   // 이 안은 바 왼쪽 위 기준
        q.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);   // 아래 지도를 못 누르게
        Plate(q, abyss ? "quick_abyss" : "quick_normal", 0f, 0f, 336f, 60f, 7f);
        float x = 3f + 10f;
        if(abyss) {
            PxIcon(q, "ItemIcon_Chest_Skull", x + 21f, 30f, 42f);
            x += 42f + 10f;
            PxText(q, "심연 정복 보상", x, 30f - 15f, 12f, Hex("#ffb3ae"), 0f, null, TextAlignmentOptions.Left);
            PxText(q, "자원 ×5 + 그 행성 무기", x, 30f + 2f, 14f, Color.white, 1f, Hex("#1a0004"), TextAlignmentOptions.Left);
            PxText(q, "피해 +20% 영구", x, 30f + 20f, 14f, Color.white, 1f, Hex("#1a0004"), TextAlignmentOptions.Left);
            QuickButton(q, "btn_red_m", "ItemIcon_Skull", all ? "다시" : "도전", 336f - 3f - 8f - 96f, 96f, Hex("#5a0010"), () => Launch(dim, p, m));
            return;
        }
        var thumb = PxAt("Thumb", q, x + 22f, 30f, 44f, 44f).gameObject.AddComponent<Image>();
        thumb.sprite = PlanetSprite(sm.planets[p].PlanetSprite, dim == PlanetDimension.Parallel ? PlanetLook.Parallel : PlanetLook.Normal);
        thumb.preserveAspect = true; thumb.raycastTarget = false;
        x += 44f + 10f;
        PxText(q, all ? "다시 하기" : "이어서 하기", x, 18f, 12f, Hex("#8fd8ff"), 0f, null, TextAlignmentOptions.Left);
        // 이름이 길면 (평행 베헤모스 1-1) 출격 버튼 앞까지 들어가게 글자를 줄여요
        string qt = $"{PlanetTitle(dim, p)} {sm.planets[p].StagesOfThisPlanet[m].StageName}";
        float avail = 336f - 3f - 8f - 112f - 8f - x;
        float qs = 20f * Mathf.Min(1f, avail / Mathf.Max(1f, MeasureText(qt, 20f)));
        PxText(q, qt, x, 37f, qs, Color.white, 2f, Navy, TextAlignmentOptions.Left);
        QuickButton(q, "btn_yellow_m", "ItemIcon_Gear_Sword", "출격", 336f - 3f - 8f - 112f, 112f, Hex("#6a2e00"), () => Launch(dim, p, m));
    }
    void QuickButton(Transform parent, string sprite, string icon, string text, float x, float w, Color ol, Action onClick)
    {
        Plate(parent, sprite, x, 7f, w, 46f, 7f);
        float tw = MeasureText(text, 22f), total = 24f + 8f + tw, cx = x + w / 2f - total / 2f;
        PxIcon(parent, icon, cx + 12f, 30f, 24f);
        PxText(parent, text, cx + 32f, 30f, 22f, Color.white, 2f, ol, TextAlignmentOptions.Left);
        var hit = PxBox("Go", parent, x, 7f, w, 46f);
        hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        hit.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
    }
    #endregion

    #region 행성 창 (아래에서 올라오는 판, 미리보기 ② 그대로)
    int sheetDim, sheetPlanet, sheetMap;

    void CloseSheet()
    {
        if(sheetLayer == null) return;
        Clear(sheetLayer);
        sheetLayer.gameObject.SetActive(false);
    }

    void OpenSheet(int dim, int p, int map = -1)
    {
        sheetDim = dim; sheetPlanet = p;
        sheetMap = map >= 0 ? map : NextMap(dim, p);
        BuildSheet();
    }

    void BuildSheet()
    {
        Clear(sheetLayer);
        sheetLayer.gameObject.SetActive(true);
        var sm = StageManager.instance;
        int dim = sheetDim, p = sheetPlanet, m = sheetMap;
        var planet = sm.planets[p];
        var maps = planet.StagesOfThisPlanet;
        bool abyss = dim == PlanetDimension.Abyss, par = dim == PlanetDimension.Parallel;

        // 어둡게 (rgba(3,6,25,.6)), 누르면 닫혀요. 아래 메뉴는 그대로 보여요
        var dimImg = Stretch("Dim", sheetLayer).gameObject.AddComponent<Image>();
        dimImg.color = new Color(3 / 255f, 6 / 255f, 25 / 255f, 0.6f);
        var dimBtn = dimImg.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(CloseSheet);

        // 판: 360×548, 아래 메뉴 바로 위 (화면이 짧으면 줄여요)
        var holder = Rect("Holder", sheetLayer, new Vector2(1080, 548 * K), Vector2.zero);
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0f);
        holder.pivot = new Vector2(0.5f, 0f);
        holder.anchoredPosition = new Vector2(0, NavH * K);
        float room = mapFrame.sizeDelta.y - NavH * K - 40f;
        // (2026-11-25 사용자: 화면을 너무 덮어요) planet_sheet_scale(0.88)배로 작게, 메뉴 위 planet_sheet_gap(10px) 띄워서
        float sheetK = Mathf.Clamp(Tuning.Get("planet_sheet_scale", 0.88f), 0.5f, 1f);
        holder.anchoredPosition = new Vector2(0, (NavH + Tuning.Get("planet_sheet_gap", 10f)) * K);
        holder.localScale = Vector3.one * Mathf.Min(sheetK, room / ((548f + 70f) * K));
        var sheet = PxBox("Panel", holder, 0f, 0f, 360f, 548f);
        var sp = sheet.gameObject.AddComponent<Image>();
        sp.sprite = MapSprite(abyss ? "sheet_abyss" : par ? "sheet_parallel" : "sheet_normal");
        sp.raycastTarget = true;   // 판을 눌러도 안 닫히게
        var s = sheet.transform;

        // 행성 (left 14, top -64, 136) + 빛 200
        Halo(s, 14f + 68f, -64f + 68f, 200f, abyss ? new Color(1f, 40 / 255f, 60 / 255f, 0.5f) : par ? new Color(200 / 255f, 90 / 255f, 1f, 0.45f) : new Color(80 / 255f, 1f, 170 / 255f, 0.38f));
        var pimg = PxBox("Planet", s, 14f, -64f, 136f, 136f).gameObject.AddComponent<Image>();
        pimg.sprite = PlanetSprite(planet.PlanetSprite, abyss ? PlanetLook.Abyss : par ? PlanetLook.Parallel : PlanetLook.Normal);
        pimg.preserveAspect = true; pimg.raycastTarget = false;
        // 이름 (left 160, top 16, 30px 외곽선 3) + 띠 2개 (top 58)
        // (2026-11-25 사용자: 닫기 X 가 이름을 가려요) 이름이 길면 (심연 베헤모스, 평행 헤파이스토스) X 앞까지 들어가게 글자를 줄여요
        string title = PlanetTitle(dim, p);
        float titleRoom = 360f - 14f - 40f - 8f - 160f;
        float titleSize = 30f * Mathf.Min(1f, titleRoom / Mathf.Max(1f, MeasureText(title, 30f)));
        PxText(s, title, 160f, 16f + 18f, titleSize, Color.white, 3f * titleSize / 30f, Navy, TextAlignmentOptions.Left);
        float cx = 160f;
        cx += Chip(s, "chip_dark", "ItemIcon_Gear_Sword", $"{WeaponOf(p)} 행성", cx, 58f) + 5f;
        Chip(s, "chip_dark", null, $"{ClearedMaps(dim, p)}/{maps.Length} 클리어", cx, 58f);
        // 닫기 (right 14, top -18, 40×40)
        Plate(s, "btn_close", 360f - 14f - 40f, -18f, 40f, 40f, 7f);
        var close = PxBox("Close", s, 360f - 14f - 40f, -18f, 40f, 40f);
        close.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(CloseSheet);

        // 맵 선택 (left 22, top 90, 14px)
        PxText(s, "맵 선택", 22f, 90f + 8f, 14f, Hex("#9fb4ff"), 0f, null, TextAlignmentOptions.Left);
        // 맵 8개: 뱀 모양 (left 20, top 140 기준. 위 줄 x 52·124·196·268 y 0, 아래 줄 오른쪽부터 y 78)
        float ox = 20f, oy = 140f;
        var curve = new List<Vector2>();   // 268,0 → C 312,0 312,78 → 268,78
        for(int i = 0; i <= 12; i++) {
            float t = i / 12f, u = 1 - t;
            Vector2 a = new Vector2(268, 0), c1 = new Vector2(312, 0), c2 = new Vector2(312, 78), b = new Vector2(268, 78);
            curve.Add(new Vector2(ox, oy) + u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b);
        }
        var path = new List<Vector2> { new Vector2(ox + 52f, oy) };
        path.AddRange(curve);
        path.Add(new Vector2(ox + 52f, oy + 78f));
        Stroke(s, path, 8f, new Color(1, 1, 1, 0.22f), smallRound);
        // 지나온 길 (깬 맵까지 초록 굵기 5 + 빛 4)
        int walkedTo = Mathf.Min(maps.Length - 1, ClearedMaps(dim, p));
        if(walkedTo > 0) {
            var wp = new List<Vector2> { MapNodePx(0, ox, oy) };
            for(int i = 1; i <= walkedTo; i++) {
                if(i == 4) wp.AddRange(curve);
                else wp.Add(MapNodePx(i, ox, oy));
            }
            Stroke(s, wp, 14f, new Color(62 / 255f, 224 / 255f, 122 / 255f, 0.5f), softLine);
            Stroke(s, wp, 5f, Hex("#3ee07a"), smallRound);
        }
        for(int i = 0; i < maps.Length && i < 8; i++) MapNode(s, dim, p, i, m, ox, oy, maps[i]);

        // 고른 맵 정보 (left 16, top 262, 328×164, 안쪽 여백 12/14)
        var st = maps[m];
        Plate(s, "info", 16f, 262f, 328f, 164f, 0f);
        float ix = 16f + 2f + 14f, iy = 262f + 2f + 12f;
        PxText(s, st.StageName, ix, iy + 14f, 24f, Color.white, 2f, Navy, TextAlignmentOptions.Left);
        float afterName = ix + MeasureText(st.StageName, 24f) + 8f;
        if(st.bossPrefab.Length > 0) Chip(s, "chip_red", null, "보스 맵", afterName, iy + 2f);
        var rules = PlanetDimension.Rules(p, m, dim);
        string right = rules.Count > 0 ? PlanetDimension.RulesShort(rules) : $"약 {Mathf.RoundToInt(Tuning.Get("planet_map_seconds", 45f) + m * Tuning.Get("planet_map_seconds_step", 6f))}초";
        PxText(s, right, 16f + 328f - 2f - 14f, iy + 14f, 12f, Hex("#9fb4ff"), 0f, null, TextAlignmentOptions.Right);
        float c1x = ix, c2x = ix + 143f + 10f, r1 = iy + 29f + 10f + 11f, r2 = r1 + 22f + 8f;
        int enemies = StageManager.EnemyTargetOf(planet, st, dim);
        Kv(s, "ItemIcon_Battle", 22f, "출현 적 ", enemies.ToString(), c1x, r1);
        Kv(s, "PictoIcon_Fire", 20f, "대군 웨이브 ", $"{Tuning.GetInt("planet_horde_count", 2)}번", c2x, r1);
        Kv(s, "PictoIcon_Boss", 22f, "보스 ", st.bossPrefab.Length > 0 ? $"{st.bossPrefab.Length}마리" : "없음", c1x, r2);
        int stars = Mathf.Clamp(1 + m / 2 + dim, 1, 5);
        Kv(s, "ItemIcon_Damage", 22f, "난이도 ", "<color=#FFD23F>" + new string('★', stars) + new string('☆', 5 - stars) + "</color>", c2x, r2);
        var div = PxBox("Divider", s, ix, r2 + 11f + 10f, 328f - 4f - 28f, 2f).gameObject.AddComponent<Image>();
        div.color = new Color(1, 1, 1, 0.08f); div.raycastTarget = false;
        float rr = r2 + 11f + 10f + 2f + 10f + 12f;
        sm.RewardOf(p, m, dim, out long gold, out int stone, out int dia, out bool first);
        float rx = ix;
        PxText(s, "보상", rx, rr, 13f, Hex("#9fb4ff"), 0f, null, TextAlignmentOptions.Left);
        rx += MeasureText("보상", 13f) + 12f;
        rx = Reward(s, "ItemIcon_Money_Coin", 24f, GameManager.instance != null ? GameManager.instance.MoneyStringForInfiniteStage(gold) : gold.ToString(), rx, rr, null) + 12f;
        rx = Reward(s, null, 22f, stone.ToString(), rx, rr, StoneSprite(st.stonePrice)) + 12f;
        rx = Reward(s, "ItemIcon_Gem_Diamond_Purple", 24f, first ? dia.ToString() : "받음", rx, rr, null) + 2f;
        if(first) {
            float cw = MeasureText("첫 클리어", 10f) + 18f + 3f;
            Plate(s, "chip_purple", rx, rr - 9f, cw, 18f, 1f);
            PxText(s, "첫 클리어", rx + cw / 2f, rr, 10f, Color.white);
        }

        // 저장된 증강 (left 16, top 434): 책 26 + 글자 14 + '보기' 64×32
        var run = mgr.PlanetRunOf(p, abyss);
        float ay = 434f + 16f;
        PxIcon(s, "ItemIcon_Book_1_Purple", 16f + 13f, ay, 26f);
        string runText = run != null ? $"저장된 증강 <color=#FFFFFF><size=114%>Lv.{run.level} · {run.augIds.Count}개</size></color>" : (abyss && !mgr.AbyssConquered(p) ? "정복하면 무기 피해 +20% 영구" : "저장된 증강 없음");
        PxText(s, runText, 16f + 26f + 8f, ay, 14f, Hex("#cfd6f5"), 0f, null, TextAlignmentOptions.Left);
        Plate(s, "btn_blue_s", 344f - 64f, ay - 16f, 64f, 32f, 7f);
        PxText(s, "보기", 344f - 32f, ay - 0.5f, 15f, Color.white, 2f, Hex("#1a3f9e"));   // 밝은 버튼에 맞춘 외곽선
        var see = PxBox("See", s, 344f - 64f, ay - 16f, 64f, 32f);
        see.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        see.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowPlanetRunList(p, abyss, () => { if(sheetLayer.gameObject.activeSelf) BuildSheet(); }));

        // 출격 (left 16, top 476, 328×56, 칼 30 + 28px)
        Plate(s, abyss ? "btn_red_l" : "btn_yellow_l", 16f, 476f, 328f, 56f, 7f);
        string goText = abyss ? "도전!" : "출격!";
        float gw = 30f + 8f + MeasureText(goText, 28f), gx = 180f - gw / 2f;
        PxIcon(s, abyss ? "ItemIcon_Skull" : "ItemIcon_Gear_Sword", gx + 15f, 476f + 28f, 30f);
        PxText(s, goText, gx + 38f, 476f + 28f, 28f, Color.white, 2f, abyss ? Hex("#5a0010") : Hex("#6a2e00"), TextAlignmentOptions.Left);
        var go = PxBox("Go", s, 16f, 476f, 328f, 56f);
        go.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        var gb = go.gameObject.AddComponent<Button>();
        gb.onClick.AddListener(() => Launch(dim, p, m));
        gb.interactable = MapOpen(dim, p, m);
    }

    static Vector2 MapNodePx(int i, float ox, float oy)
    {
        float[] xs = { 52f, 124f, 196f, 268f };
        return i < 4 ? new Vector2(ox + xs[i], oy) : new Vector2(ox + xs[7 - i], oy + 78f);
    }

    // 맵 칸 하나 (52, 고른 칸 62 노랑+빛, 깬 칸 초록+체크, 잠긴 칸 자물쇠, 보스 맵 해골)
    void MapNode(Transform s, int dim, int p, int i, int selected, float ox, float oy, StageData st)
    {
        var c = MapNodePx(i, ox, oy);
        bool cleared = MapCleared(dim, p, i), open = MapOpen(dim, p, i), sel = i == selected;
        bool abyss = dim == PlanetDimension.Abyss;
        float sz = sel ? 62f : 52f;
        string sprite = sel ? (abyss ? "node_next_red" : "node_next") : cleared ? "node_green" : open ? "node_yellow" : "node_lock";
        Plate(s, sprite, c.x - sz / 2f, c.y - sz / 2f, sz, sz, sel ? 26f : 6f);
        if(!open) {
            PxIcon(s, "Icon_Lock03", c.x, c.y - 7f, 16f).color = new Color(1, 1, 1, 0.7f);
            PxText(s, st.StageName, c.x, c.y + 9f, 13f, Hex("#7d84a6"));
        } else {
            Color ol = sel ? (abyss ? Hex("#5a0010") : Hex("#6a3a00")) : cleared ? Hex("#0b4a1c") : Hex("#6a3a00");
            PxText(s, st.StageName, c.x, c.y, sel ? 20f : 17f, Color.white, 2f, ol);
        }
        if(st.bossPrefab.Length > 0) PxIcon(s, "ItemIcon_Skull", c.x + sz / 2f + 9f - 12f, c.y - sz / 2f - 10f + 12f, 24f, 2f);
        if(cleared) PxIcon(s, "PictoIcon_Check_1", c.x - sz / 2f - 7f + 9f, c.y + sz / 2f + 7f - 9f, 18f, 1f);
        var hit = PxAt("Map" + i, s, c.x, c.y, sz, sz);
        hit.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        int mi = i;
        hit.gameObject.AddComponent<Button>().onClick.AddListener(() => {
            if(!MapOpen(dim, p, mi)) return;   // 자물쇠로 보여요
            if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
            sheetMap = mi;
            BuildSheet();
        });
    }

    // 정보 한 줄: 아이콘 + "이름 " (14px #cfd6f5) + 값 (16px 흰색)
    void Kv(Transform s, string icon, float iconSize, string label, string value, float x, float cy)
    {
        PxIcon(s, icon, x + iconSize / 2f, cy, iconSize);
        x += iconSize + 6f;
        PxText(s, label, x, cy, 14f, Hex("#cfd6f5"), 0f, null, TextAlignmentOptions.Left);
        x += MeasureText(label.TrimEnd(), 14f) + (label.EndsWith(" ") ? 4f : 0f);   // 끝 띄어쓰기는 폭에 안 잡혀요
        PxText(s, value, x, cy, 16f, Color.white, 0f, null, TextAlignmentOptions.Left);
    }
    // 보상 하나: 아이콘 + 값 (16px). 끝 x 를 돌려줘요
    float Reward(Transform s, string icon, float iconSize, string value, float x, float cy, Sprite sprite)
    {
        if(sprite != null) {
            var img = PxAt("Icon", s, x + iconSize / 2f, cy, iconSize, iconSize).gameObject.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        } else if(icon != null) PxIcon(s, icon, x + iconSize / 2f, cy, iconSize);
        x += iconSize + 4f;
        PxText(s, value, x, cy, 16f, Color.white, 0f, null, TextAlignmentOptions.Left);
        return x + MeasureText(value, 16f);
    }

    // 강화석 그림: 옛날 결과 창의 강화석 칸에서 가져와요
    readonly Dictionary<UpgradeStone.TypeOfStone, Sprite> stoneSprites = new Dictionary<UpgradeStone.TypeOfStone, Sprite>();
    Sprite StoneSprite(UpgradeStone.TypeOfStone type)
    {
        if(stoneSprites.TryGetValue(type, out var s)) return s;
        var sm = StageManager.instance;
        int i = (int)type;
        if(sm != null && sm.stoneObjs != null && i >= 0 && i < sm.stoneObjs.Length && sm.stoneObjs[i] != null)
            foreach(var img in sm.stoneObjs[i].GetComponentsInChildren<Image>(true))
                if(img.name == "Stone Image") { s = img.sprite; break; }
        if(s == null) s = Skin("Ore");
        stoneSprites[type] = s;
        return s;
    }
    #endregion

    // 출격: 옛날 '전투 시작'과 같은 순서
    void Launch(int dim, int p, int m)
    {
        var sm = StageManager.instance;
        if(sm == null || p < 0 || p >= sm.planets.Count) return;
        if(!MapOpen(dim, p, m)) return;
        var planet = sm.planets[p];
        sm.AbyssMode = dim == PlanetDimension.Abyss;
        planet.SetPlanetInformation();   // 지금 행성 (옛날 행성 창 내용도 채워요, 안 보여요)
        sm.SetmapInfo(m);
        CloseSheet();
        if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
        sm.Move();
        if(stagesMap != null) stagesMap.SetActive(false);
    }
}

// 가로 1080 기준 틀을 부모 크기에 맞춰요 (화면 회전·해상도 바뀜에도)
public class RemasterWidthFit : MonoBehaviour
{
    public RectTransform target;
    Vector2 last;
    void LateUpdate() { var p = (RectTransform)transform; if(p.rect.size != last) Apply(); }
    public void Apply()
    {
        var p = (RectTransform)transform;
        last = p.rect.size;
        if(target == null || p.rect.width <= 0f) return;
        float k = p.rect.width / 1080f;
        target.localScale = new Vector3(k, k, 1f);
        target.sizeDelta = new Vector2(1080f, p.rect.height / k);
    }
}
