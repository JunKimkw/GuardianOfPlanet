using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 리마스터 UI: 씬을 건드리지 않고 코드로 만드는 화면들 (기준 해상도 1080x2048, 가로폭 맞춤 = 기존 UI와 동일)
// 겉모습은 기존 UI와 같은 Layer Lab GUI Pro-SuperCasual 이미지를 써요 (Resources/RemasterUI 에 복사본).
// 모든 글자는 가운데 정렬 + 자동 크기(넘치지 않게)로 만들어요.
public partial class RemasterUI : MonoBehaviour
{
    AugmentManager mgr;
    Canvas canvas;
    RectTransform root, hudRoot, modalRoot, toastRoot;
    TMP_FontAsset font;
    Sprite rounded;
    int openModals;
    public bool Busy => openModals > 0;

    // Layer Lab 다크 테마 색
    static readonly Color PopupTint = new Color(0.22f, 0.24f, 0.4f);      // 흰 팝업에 입히는 남색
    static readonly Color TextDim = new Color(0.74f, 0.76f, 0.92f);
    static readonly Color TextGold = new Color(1f, 0.84f, 0.3f);
    static readonly Color LockedTint = new Color(0.42f, 0.42f, 0.5f);

    // 버튼 색 (Layer Lab Button01 이미지 이름)
    const string BBlue = "Blue", BGreen = "Green", BYellow = "Yellow", BGray = "Gray", BRed = "Red", BPurple = "Purple";

    void Awake()
    {
        mgr = GetComponent<AugmentManager>();
    }

    #region 기본 틀
    bool EnsureBuilt()
    {
        if(canvas != null) return true;
        font = FindFont();
        if(font == null) return false;
        rounded = MakeRounded(64, 18);

        var cgo = new GameObject("RemasterCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2048);
        scaler.matchWidthOrHeight = 0f;
        cgo.AddComponent<GraphicRaycaster>();
        root = cgo.GetComponent<RectTransform>();

        hudRoot = Stretch("HUD", root);
        // 옛날 UI 아래에 그리는 캔버스 (2026-11-25 사용자: 행성 레벨 바는 일시정지·설정 창의 회색 배경 뒤로)
        //  옛날 UI 캔버스는 화면 위 그리기 순서 1·2, 우리 UI 는 20 → 이건 0
        var ugo = new GameObject("RemasterUnderCanvas", typeof(RectTransform));
        ugo.transform.SetParent(transform, false);
        var under = ugo.AddComponent<Canvas>();
        under.renderMode = RenderMode.ScreenSpaceOverlay;
        under.sortingOrder = Mathf.RoundToInt(Tuning.Get("planet_hud_sort", 0f));
        var uscaler = ugo.AddComponent<CanvasScaler>();
        uscaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        uscaler.referenceResolution = new Vector2(1080, 2048);
        uscaler.matchWidthOrHeight = 0f;
        ugo.AddComponent<GraphicRaycaster>();
        underRoot = Stretch("UnderHUD", ugo.GetComponent<RectTransform>());
        modalRoot = Stretch("Modals", root);
        toastRoot = Stretch("Toasts", root);
        BuildHud();
        return true;
    }

    // 기존 폰트 에셋들은 미리 구운 글자만 있어서(정적) 새 문구의 글자가 □로 깨져요.
    // 원본 미원 폰트에서 필요한 글자를 그때그때 만드는 동적 폰트를 써요.
    static TMP_FontAsset dynFont;
    TMP_FontAsset FindFont()
    {
        if(dynFont != null) return dynFont;
        try {
            var src = Resources.Load<Font>("Fonts/MiwonDyn");
            if(src != null) {
                dynFont = TMP_FontAsset.CreateFontAsset(src, 72, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if(dynFont != null) {
                    dynFont.name = "MiwonDynamic";
                    var mat = dynFont.material;
                    if(mat != null) {
                        mat.EnableKeyword("OUTLINE_ON");
                        if(mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0.2f);
                        if(mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", new Color(0.05f, 0.05f, 0.1f, 1f));
                    }
                    var def = TMP_Settings.defaultFontAsset;
                    if(def != null && def != dynFont) dynFont.fallbackFontAssetTable = new List<TMP_FontAsset> { def };
                    return dynFont;
                }
            }
        } catch(Exception e) { Debug.LogWarning("[RemasterUI] 동적 폰트 생성 실패: " + e.Message); }

        var inf = InfiniteStage.Instance;
        if(inf != null) {
            if(inf.currentStageText_Failed != null && inf.currentStageText_Failed.font != null) return inf.currentStageText_Failed.font;
            if(inf.stageText != null && inf.stageText.font != null) return inf.stageText.font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    static Sprite MakeRounded(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        for(int y = 0; y < size; y++) {
            for(int x = 0; x < size; x++) {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    // ---- 스킨 이미지
    readonly Dictionary<string, Sprite> skinCache = new Dictionary<string, Sprite>();
    Sprite Skin(string name)
    {
        if(skinCache.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("RemasterUI/" + name);
        skinCache[name] = s;
        return s;
    }

    // 9-slice 이미지를 붙여요. 칸이 테두리보다 작으면 테두리를 비율대로 줄여서 찌그러지지 않게 해요.
    void ApplySprite(Image img, Sprite sp, Vector2 size)
    {
        if(sp == null) { img.sprite = rounded; img.type = Image.Type.Sliced; return; }
        img.sprite = sp;
        var b = sp.border;
        if(b.sqrMagnitude > 0f) {
            img.type = Image.Type.Sliced;
            float need = Mathf.Max((b.x + b.z) / Mathf.Max(1f, size.x), (b.y + b.w) / Mathf.Max(1f, size.y));
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, need * 1.02f);
        } else {
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    // 단색 둥근 상자 (장식용)
    Image Box(Transform parent, Vector2 size, Vector2 pos, Color color, string name = "Box")
    {
        var rt = Rect(name, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    // 스킨 이미지 상자
    Image Frame(Transform parent, string sprite, Vector2 size, Vector2 pos, Color tint, string name = "Frame")
    {
        var rt = Rect(name, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        ApplySprite(img, Skin(sprite), size);
        img.color = tint;
        return img;
    }

    // 목록 한 줄 / 안쪽 칸 (어두운 남색 프레임)
    Image Row(Transform parent, Vector2 size, Vector2 pos, string name = "Row") => Frame(parent, "ItemFrame02_Single_Navy", size, pos, Color.white, name);

    // 색을 입히는 카드 테두리 (흰 바탕 + 검은 외곽선 → 색만 바꿔 써요)
    Image ColorCard(Transform parent, Vector2 size, Vector2 pos, Color color, string name = "Card") => Frame(parent, "CardFrame03_n_Bg", size, pos, color, name);

    TextMeshProUGUI Label(Transform parent, string text, Vector2 size, Vector2 pos, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        var rt = Rect("Text", parent, size, pos);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMax = fontSize;
        t.fontSizeMin = Mathf.Max(14f, fontSize * 0.5f);
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    // Layer Lab 제목 장식:  ◆ ──── 제목 ──── ◆
    TextMeshProUGUI Title(Transform parent, string text, float y, float fontSize = 56f, float width = 860f)
    {
        var t = Label(parent, text, new Vector2(width - 260f, fontSize * 1.5f), new Vector2(0, y), fontSize, Color.white, FontStyles.Bold);
        float textW = Mathf.Min(width - 260f, t.GetPreferredValues(text, width - 260f, fontSize * 1.5f).x);
        float lineW = Mathf.Max(30f, Mathf.Min(150f, (width - textW) / 2f - 60f));
        for(int s = -1; s <= 1; s += 2) {
            float inner = textW / 2f + 24f;
            Box(parent, new Vector2(lineW, 6f), new Vector2(s * (inner + lineW / 2f), y), Color.white, "TitleLine");
            var d = Box(parent, new Vector2(18f, 18f), new Vector2(s * (inner + lineW + 16f), y), Color.white, "TitleDot");
            d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        }
        return t;
    }

    Button Btn(Transform parent, string text, Vector2 size, Vector2 pos, string style, Action onClick, float fontSize = 44f)
    {
        var rt = Rect("Button", parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        var b = rt.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.85f);
        b.colors = cb;
        b.onClick.AddListener(() => onClick?.Invoke());
        // 버튼 이미지 아래쪽 두께(그림자)를 빼고 위쪽 면의 가운데에 글자를 둬요
        var label = Label(rt, text, new Vector2(size.x - 36f, size.y * 0.72f), new Vector2(0, size.y * 0.04f), fontSize, Color.white, FontStyles.Bold);
        label.name = "Label";
        SetBtnStyle(b, style);
        return b;
    }

    void SetBtnStyle(Button b, string style)
    {
        var img = b.GetComponent<Image>();
        var size = ((RectTransform)b.transform).sizeDelta;
        string sprite = (size.y >= 110f ? "Button01_l_" : "Button01_s_") + style;
        var sp = Skin(sprite) ?? Skin("Button01_l_" + style);
        ApplySprite(img, sp, size);
        img.color = Color.white;
    }

    Image Icon(Transform parent, string icon, Vector2 size, Vector2 pos)
    {
        var rt = Rect("Icon", parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        var sp = Resources.Load<Sprite>("AugmentIcons/" + icon);
        if(sp != null) img.sprite = sp;
        else {
            // 아직 그림이 없는 증강·계열은 이름 첫 글자로 대신해요 (그림은 P7에서)
            img.sprite = rounded; img.type = Image.Type.Sliced; img.color = new Color(1, 1, 1, 0.2f);
            string glyph = IconGlyph(icon);
            if(glyph.Length > 0) Label(rt, glyph, size, Vector2.zero, size.y * 0.62f, Color.white, FontStyles.Bold);
        }
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    static string IconGlyph(string icon)
    {
        if(string.IsNullOrEmpty(icon)) return "";
        if(icon.StartsWith("tag_")) {
            foreach(var t in AugmentDefs.MainTags)
                if(AugmentDefs.TagIcon(t) == icon) return AugmentDefs.TagName(t).Substring(0, 1);
            return "";
        }
        var def = AugmentDefs.Get(icon);
        return def != null && def.name.Length > 0 ? def.name.Substring(0, 1) : "";
    }

    Image SkinIcon(Transform parent, string sprite, Vector2 size, Vector2 pos)
    {
        var rt = Rect("Icon", parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        var sp = Skin(sprite);
        if(sp != null) { img.sprite = sp; img.preserveAspect = true; }
        else img.color = Color.clear;
        img.raycastTarget = false;
        return img;
    }

    // 색 테두리 안에 증강 아이콘 (Layer Lab 아이템 칸 모양)
    Image IconTile(Transform parent, string icon, float size, Vector2 pos, Color color)
    {
        var card = ColorCard(parent, new Vector2(size, size), pos, color, "IconTile");
        Icon(card.transform, icon, new Vector2(size * 0.74f, size * 0.74f), new Vector2(0, size * 0.02f));
        return card;
    }

    // 모달: 화면 어둡게 + 가운데 남색 팝업
    // 열린 창을 전부 닫고 게임을 다시 움직여요 (개발용 종결 "발동": 바로 화면을 볼 수 있게, 2026-11-20)
    void CloseAllModals()
    {
        for(int i = modalRoot.childCount - 1; i >= 0; i--) Destroy(modalRoot.GetChild(i).gameObject);
        openModals = 0;
        mgr.ResumeAllUI();
        RefreshHUD();
    }

    RectTransform OpenModal(Vector2 size, out Action close)
    {
        EnsureBuilt();
        var dim = Stretch("Modal", modalRoot);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0.02f, 0.03f, 0.08f, 0.78f);
        var panel = Frame(dim, "Popup01_Basic_White_Bg", size, Vector2.zero, PopupTint, "Panel");
        // 화면이 짧은 기기(태블릿 등)에서는 팝업 전체를 줄여서 넘치지 않게
        float h = root.rect.height > 0 ? root.rect.height : 2048f;
        float fit = Mathf.Min(1f, (h - 60f) / size.y);
        var rt = panel.rectTransform;
        openModals++;
        bool closed = false;
        close = () => {
            if(closed) return;
            closed = true;
            openModals = Mathf.Max(0, openModals - 1);
            Destroy(dim.gameObject);
            RefreshHUD();
        };
        StartCoroutine(PopIn(rt, fit));
        return rt;
    }

    IEnumerator PopIn(RectTransform rt, float fit)
    {
        float t = 0;
        rt.localScale = Vector3.one * fit * 0.85f;
        while(t < 0.2f && rt != null) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.2f);
            // 살짝 튕기는 등장
            float s = k < 0.7f ? Mathf.Lerp(0.85f, 1.04f, k / 0.7f) : Mathf.Lerp(1.04f, 1f, (k - 0.7f) / 0.3f);
            rt.localScale = new Vector3(s * fit, s * fit, 1);
            yield return null;
        }
        if(rt != null) rt.localScale = new Vector3(fit, fit, 1);
    }
    #endregion

    #region HUD
    RectTransform hud;
    RectTransform planetHud;          // 행성 한 판: 레벨 + 증강 보기 (2026-11-25)
    TextMeshProUGUI planetHudLevel;
    RectTransform planetHudFill;
    Image planetBarBg, planetHudFillImg;
    int planetHudShownLevel = -1;
    bool planetBarStyled;
    RectTransform underRoot;          // 옛날 UI 아래 캔버스 (행성 레벨 바)
    TextMeshProUGUI hudStardust, hudStack;
    readonly TextMeshProUGUI[] hudTag = new TextMeshProUGUI[3];
    readonly Image[] hudTile = new Image[3];
    readonly AugTag?[] hudTileTag = new AugTag?[3];

    // 많이 모은 계열 순서 (같으면 기본 순서). 아무것도 없으면 연쇄·저주·증식
    List<AugTag> TopTags(int n)
    {
        var list = new List<AugTag>(AugmentDefs.MainTags);
        list.Sort((a, b) => {
            int c = mgr.TagCount(b).CompareTo(mgr.TagCount(a));
            return c != 0 ? c : System.Array.IndexOf(AugmentDefs.MainTags, a).CompareTo(System.Array.IndexOf(AugmentDefs.MainTags, b));
        });
        if(list.Count > n) list.RemoveRange(n, list.Count - n);
        return list;
    }
    Button hudRebirthBtn;
    bool hudRebirthReady;

    void BuildHud()
    {
        // 화면 왼쪽 가운데보다 조금 위 (적이 오는 길을 덜 가리도록 작게)
        hud = Rect("ExpeditionHud", hudRoot, new Vector2(184, 500), Vector2.zero);
        hud.anchorMin = hud.anchorMax = new Vector2(0f, 0.6f);
        hud.pivot = new Vector2(0f, 0.5f);
        hud.anchoredPosition = new Vector2(10, 0);
        var bg = hud.gameObject.AddComponent<Image>();
        ApplySprite(bg, Skin("ItemFrame02_Single_Navy"), hud.sizeDelta);
        bg.color = new Color(1f, 1f, 1f, 0.88f);

        Label(hud, "원정", new Vector2(160, 42), new Vector2(0, 212), 32, Color.white, FontStyles.Bold);
        SkinIcon(hud, "ItemIcon_Star_Purple", new Vector2(40, 40), new Vector2(-56, 168));
        hudStardust = Label(hud, "", new Vector2(110, 40), new Vector2(22, 168), 28, TextGold, FontStyles.Bold);
        // 가장 많이 모은 계열 3개 (아이콘은 RefreshHUD 에서 바뀌어요)
        for(int i = 0; i < 3; i++)
            hudTag[i] = Label(hud, "", new Vector2(90, 46), new Vector2(30, 110 - i * 58), 32, Color.white, FontStyles.Bold);
        hudStack = Label(hud, "", new Vector2(164, 56), new Vector2(0, -60), 22, TextDim);
        Btn(hud, "증강", new Vector2(160, 76), new Vector2(0, -132), BBlue, ShowAugmentList, 30);
        hudRebirthBtn = Btn(hud, "환생", new Vector2(160, 76), new Vector2(0, -210), BGray, ShowRebirth, 30);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 개발용: 증강 테스트 창 (출시 빌드에는 없어요)
        Btn(hud, "테스트", new Vector2(160, 66), new Vector2(0, -292), BPurple, ShowDebugAugments, 28);
#endif

        // 행성 한 판 HUD (2026-11-25 사용자: 골드와 남은 적 사이에 레벨 바, 그 밑에 작게 '증강 보기')
        //  위치는 옛날 UI(골드 글자·남은 적 글자)의 화면 위치를 따라가요 (PlacePlanetHud)
        //  (2026-11-25 사용자) 증강 버튼은 바 밑이 아니라 바 오른쪽 같은 줄에 (위에서 내려오는 적을 덜 가리게)
        //  (2026-11-25 사용자) 모양은 원정(무한 스테이지) 왼쪽 위 레벨 바(Player.expbar)의 그림을 그대로 써요 (StylePlanetBar)
        //  [ Lv.7 ▓▓▓▓▓▓▓░░░░ ][증강]   (2026-11-25 사용자: 레벨이 너무 작아 → 굵은 바 + 큰 외곽선 글자, 레벨업이면 톡 커져요)
        planetHud = Rect("PlanetHud", underRoot, new Vector2(470, 70), Vector2.zero);
        planetBarBg = Box(planetHud, new Vector2(340, 50), new Vector2(-55, 0), new Color(0.06f, 0.08f, 0.16f, 0.92f), "LevelBarBg");
        planetHudFillImg = Box(planetBarBg.transform, new Vector2(330, 40), Vector2.zero, new Color(0.45f, 1f, 0.55f), "LevelBarFill");
        planetHudFill = planetHudFillImg.rectTransform;
        planetHudFill.anchorMin = planetHudFill.anchorMax = planetHudFill.pivot = new Vector2(0f, 0.5f);
        planetHudFill.anchoredPosition = new Vector2(5f, 0f);
        planetHudLevel = Label(planetBarBg.transform, "Lv.0", new Vector2(200, 48), new Vector2(-55, 1), 36, Color.white, FontStyles.Bold);
        planetHudLevel.alignment = TextAlignmentOptions.MidlineLeft;
        planetHudLevel.outlineWidth = 0.28f;
        planetHudLevel.outlineColor = new Color32(10, 14, 30, 255);
        // (2026-11-25 사용자: 퍼센트 글자는 어색해요 → 뺐어요, 채움만으로)
        Btn(planetHud, "증강", new Vector2(100, 56), new Vector2(178, 0), BBlue, ShowAugmentList, 26);
        planetHud.gameObject.SetActive(false);
    }

    float hudTimer;

    // 레벨업하면 바가 톡 커졌다 돌아오고 채움이 잠깐 하얗게 (0.3초)
    IEnumerator PlanetBarPop()
    {
        float t = 0f;
        Color baseCol = planetHudFillImg.color;
        while(t < 0.3f && planetHud != null) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI);
            planetBarBg.rectTransform.localScale = Vector3.one * (1f + 0.15f * k);
            planetHudFillImg.color = Color.Lerp(baseCol, Color.white, 0.6f * k);
            yield return null;
        }
        planetBarBg.rectTransform.localScale = Vector3.one;
        planetHudFillImg.color = baseCol;
    }

    // 원정 왼쪽 위 레벨 바(Player.expbar)의 바탕·채움 그림과 레벨 동그라미·글꼴을 가져와요 (처음 한 번)
    void StylePlanetBar()
    {
        if(planetBarStyled || Player.instance == null || Player.instance.expbar == null) return;
        planetBarStyled = true;
        var sl = Player.instance.expbar;
        var fillSrc = sl.fillRect != null ? sl.fillRect.GetComponent<Image>() : null;
        Image bgSrc = null;
        foreach(var img in sl.GetComponentsInChildren<Image>(true))
            if(img != fillSrc && (sl.handleRect == null || img.transform != sl.handleRect)) { bgSrc = img; break; }
        if(bgSrc != null && bgSrc.sprite != null) { planetBarBg.sprite = bgSrc.sprite; planetBarBg.type = Image.Type.Sliced; planetBarBg.color = bgSrc.color; }
        if(fillSrc != null && fillSrc.sprite != null) { planetHudFillImg.sprite = fillSrc.sprite; planetHudFillImg.type = Image.Type.Sliced; planetHudFillImg.color = fillSrc.color; }
        var lt = Player.instance.LevelText;
        if(lt != null) {
            if(lt.font != null) {   // 글꼴만 (뱃지 그림은 안 써요). 글꼴을 바꾸면 외곽선이 풀려서 다시 걸어요
                planetHudLevel.font = lt.font;
                planetHudLevel.outlineWidth = 0.28f;
                planetHudLevel.outlineColor = new Color32(10, 14, 30, 255);
            }
        }
    }

    // 레벨 바 자리: 보이는 골드 글자와 남은 적 글자의 가운데 (둘 중 하나만 찾으면 그 아래)
    static readonly Vector3[] uiCorners = new Vector3[4];
    static bool ScreenCenterOf(RectTransform rt, out Vector2 screen)
    {
        screen = default;
        if(rt == null || !rt.gameObject.activeInHierarchy) return false;
        rt.GetWorldCorners(uiCorners);
        var c = rt.GetComponentInParent<Canvas>();
        var root = c != null ? c.rootCanvas : null;
        Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        screen = RectTransformUtility.WorldToScreenPoint(cam, (uiCorners[0] + uiCorners[2]) * 0.5f);
        return true;
    }
    void PlacePlanetHud()
    {
        Vector2 a = default, b = default;
        bool hasA = false, hasB = false;
        var um = UIManager.instance;
        if(um != null && um.gold_Text != null)
            foreach(var g in um.gold_Text) if(g != null && ScreenCenterOf(g.rectTransform, out a)) { hasA = true; break; }
        var sm = StageManager.instance;
        if(sm != null && sm.RemainedMonster_Text != null) hasB = ScreenCenterOf(sm.RemainedMonster_Text.rectTransform, out b);
        Vector2 screen = hasA && hasB ? (a + b) * 0.5f : hasB ? b + new Vector2(0f, -Screen.height * 0.05f) : hasA ? a + new Vector2(0f, -Screen.height * 0.05f) : new Vector2(Screen.width * 0.5f, Screen.height * 0.85f);
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(underRoot, screen, null, out Vector2 local))
            planetHud.anchoredPosition = local + new Vector2(Tuning.Get("planet_hud_dx", 0f), Tuning.Get("planet_hud_dy", 0f));
    }

    // 다른 창이 떠 있는지: 화면 가운데를 옛날 UI(우리 캔버스 밖, 화면 위에 그리는 캔버스)가 덮고 있으면 창이 떠 있는 걸로 봐요.
    //  평소 원정 화면 가운데는 게임 화면(적·우주선)이라 UI가 없어요. 끄려면 tuning hud_hide_on_window = 0
    static readonly List<RaycastResult> rayHits = new List<RaycastResult>();
    bool OtherWindowOpen()
    {
        if(Tuning.Get("hud_hide_on_window", 1f) <= 0f) return false;
        var es = EventSystem.current;
        if(es == null) return false;
        var ped = new PointerEventData(es) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
        rayHits.Clear();
        es.RaycastAll(ped, rayHits);
        foreach(var h in rayHits) {
            if(!(h.module is GraphicRaycaster gr)) continue;
            var c = gr.GetComponent<Canvas>();
            if(c != null && c.renderMode == RenderMode.WorldSpace) continue;
            if(h.gameObject == null || h.gameObject.transform.IsChildOf(transform)) continue;   // 우리 UI 는 빼요
            var gfx = h.gameObject.GetComponent<Graphic>();
            if(gfx != null && gfx.color.a < 0.05f) continue;   // (2026-11-25) 투명한 판(터치 영역 같은 것)은 창이 아니에요. 예전엔 이것 때문에 원정 칸이 계속 숨을 수 있었어요
            return true;
        }
        return false;
    }

    // 행성 창(2-1 ~ 2-8 고르는 창)에 '증강' 버튼: 그 행성에서 클리어까지 모은 증강 보기·초기화 (2026-11-25 사용자: 행성 창에서도 보고 싶어요)
    //  처음엔 옛날 UI 창 안에 붙였는데 배율이 달라 크게 깨졌어요 → 우리 UI(hudRoot)에 만들고, 맵 버튼 묶음의 화면 위치(오른쪽 위)를 따라가요
    RectTransform planetPopupBtn;
    TextMeshProUGUI planetPopupLabel;
    static readonly Vector3[] gridCorners = new Vector3[4];
    bool UpdatePlanetPopupButton()
    {
        var sm = StageManager.instance;
        var cp = sm != null ? sm.currentPlanet : null;
        var grid = cp != null && cp.LevelMap_Pfb_Parent != null ? cp.LevelMap_Pfb_Parent as RectTransform : null;
        bool open = grid != null && grid.gameObject.activeInHierarchy && !sm.isInStage;
        if(!open) {
            if(planetPopupBtn != null && planetPopupBtn.gameObject.activeSelf) planetPopupBtn.gameObject.SetActive(false);
            return false;
        }
        if(planetPopupBtn == null) {
            var btn = Btn(hudRoot, "증강", new Vector2(230, 80), Vector2.zero, BBlue, () => ShowPlanetRunList(), 30);
            planetPopupBtn = btn.GetComponent<RectTransform>();
            planetPopupLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
        }
        if(!planetPopupBtn.gameObject.activeSelf) planetPopupBtn.gameObject.SetActive(true);
        // 맵 버튼 묶음 오른쪽 위 → 화면 좌표 → 우리 캔버스 좌표
        grid.GetWorldCorners(gridCorners);
        var oldCanvas = grid.GetComponentInParent<Canvas>();
        var root = oldCanvas != null ? oldCanvas.rootCanvas : null;
        Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, gridCorners[2]);
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, screen, null, out Vector2 local))
            planetPopupBtn.anchoredPosition = local + new Vector2(-120f, 50f);   // 묶음 오른쪽 끝에 맞추고 조금 위 ('행성 정보' 줄)
        var run = mgr.PlanetRunOf(sm.planets.IndexOf(cp));
        if(planetPopupLabel != null) planetPopupLabel.text = run != null ? $"증강 Lv.{run.level}" : "증강 없음";   // 가장 멀리 깬 맵의 저장
        return true;
    }

    // 행성에 기억된 증강 보기 (행성 밖에서)
    void ShowPlanetRunList() { var sm = StageManager.instance; if(sm != null && sm.currentPlanet != null) ShowPlanetRunList(sm.planets.IndexOf(sm.currentPlanet), false); }
    void ShowPlanetRunList(int pi, bool abyss, Action after = null)
    {
        var sm = StageManager.instance;
        var cp = sm != null && pi >= 0 && pi < sm.planets.Count ? sm.planets[pi] : null;
        if(cp == null) return;
        var run = mgr.PlanetRunOf(pi, abyss);
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(900, 1300), out Action close);
        Title(p, $"{PlanetDimension.Prefix(abyss ? PlanetDimension.Abyss : PlanetDimension.Of(pi, false))}{cp.PlanetName}의 증강", 580, 52, 860);
        if(run == null) {
            Label(p, "아직 기억된 증강이 없어요.\n맵을 클리어하면 그때까지 고른 증강이 남아요.", new Vector2(820, 200), new Vector2(0, 200), 32, TextDim);
        } else {
            string w = AugmentDefs.WeaponName((SideWeapon)run.left) + (run.right >= 0 ? " / " + AugmentDefs.WeaponName((SideWeapon)run.right) : "");
            Label(p, $"행성 Lv.{run.level}   ·   무기: 기본총 / {w}", new Vector2(840, 50), new Vector2(0, 505), 30, TextDim);
            var sb = new System.Text.StringBuilder();
            for(int i = 0; i < run.augIds.Count && i < run.augLevels.Count; i++) {
                var d = AugmentDefs.Get(run.augIds[i]);
                if(d == null) continue;
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(AugmentDefs.GradeColor(d.grade))}>{d.name}</color> <color=#FFD54A>★{run.augLevels[i]}</color>");
                sb.Append(i % 2 == 0 ? "      " : "\n");
            }
            Label(p, sb.Length > 0 ? sb.ToString() : "(증강 없음)", new Vector2(820, 760), new Vector2(0, 60), 30, Color.white);
        }
        Btn(p, "닫기", new Vector2(340, 120), new Vector2(run != null ? -190 : 0, -540), BGray, () => { close(); mgr.ResumeFromUI(); after?.Invoke(); }, 42);
        if(run != null) {
            bool armed = false;
            Button reset = null;
            reset = Btn(p, "증강 초기화", new Vector2(340, 120), new Vector2(190, -540), BRed, () => {
                if(!armed) {
                    armed = true;
                    var t = reset.GetComponentInChildren<TextMeshProUGUI>();
                    if(t != null) t.text = "정말 초기화?";
                    return;
                }
                mgr.ForgetPlanetRun(pi, abyss);
                close();
                mgr.ResumeFromUI();
                after?.Invoke();
                Toast($"{cp.PlanetName} 행성의 증강을 초기화했어요", Color.white, true);
            }, 36);
        }
    }
    void Update()
    {
        if(mgr != null && mgr.Loaded) { UpdatePlanetMap(); UpdateWeaponJob(); UpdateEquipSetBar(); UpdateEquipSortBar(); UpdateAutoDismantle(); UpdateGachaSheet();   // 뽑기 = 원래 그림으로 밑에서 올라오는 판 (GachaSheetUI.cs)
            // 새로 덮는 페이지 창(뽑기·던전·도감·버프·랭킹·설정)은 꺼 둬요 — 2026-11-27 사용자 "다른 창은 다 원래대로 돌려 두고, 원래 UI 를 이용해서".
            //  코드는 남겨 두고 ui_new_pages 1 이면 다시 켜져요 (켜고 끄면 플레이 다시 시작)
            if(Tuning.Get("ui_new_pages", 0f) > 0f) { UpdateGachaPage(); UpdateDungeonPage(); UpdateCollectionPage(); UpdateBuffPage(); UpdateRankingPage(); UpdateSettingsPage(); }
        }   // 무기 전직 (WeaponJobUI.cs)   // 행성 지도 (A안, PlanetMapUI.cs)
        hudTimer -= Time.unscaledDeltaTime;
        if(hudTimer > 0) return;
        hudTimer = 0.25f;
        if(mgr == null || !mgr.Loaded) return;
        if(!EnsureBuilt()) return;
        bool planetPopup = UpdatePlanetPopupButton();
        bool show = InfiniteStage.Instance != null && InfiniteStage.Instance.IsRunning
                    && (StageManager.instance == null || !StageManager.instance.isInStage)
                    && (StartScreenScript.Instance == null || !StartScreenScript.Instance.gameObject.activeInHierarchy)
                    && !planetPopup && !OtherWindowOpen();   // (2026-11-25 사용자) 행성 창 같은 다른 창이 뜨면 원정·출격 무기 칸을 숨겨요
        if(hud.gameObject.activeSelf != show) hud.gameObject.SetActive(show);
        // 행성 화면이면 계속 (2026-11-25 사용자: 일시정지·승리·패배 화면에서도 안 없애도 돼요). 옛날 UI 아래 캔버스라 그 창들의 배경 뒤로 가요
        bool planetShow = mgr.InPlanetRun && StageManager.instance != null && (StageManager.instance.isInStage || StageManager.instance.planetScreen);   // 행성 화면이 뜨는 순간부터 (예전엔 전투 시작 2초 뒤)
        if(planetHud != null) {
            if(planetHud.gameObject.activeSelf != planetShow) planetHud.gameObject.SetActive(planetShow);
            if(planetShow) {
                StylePlanetBar();
                float prog = Mathf.Clamp01(mgr.PlanetLevelProgress);
                planetHudLevel.text = $"Lv.{mgr.PlanetLevel}";
                planetHudFill.sizeDelta = new Vector2(330f * prog, 40f);
                if(planetHudShownLevel >= 0 && mgr.PlanetLevel > planetHudShownLevel) StartCoroutine(PlanetBarPop());   // 레벨업: 톡
                planetHudShownLevel = mgr.PlanetLevel;
                PlacePlanetHud();
            }
        }
        if(show) RefreshHUD();
        else if(weaponHudShown) RefreshWeaponHud(false);
    }

    public void RefreshHUD()
    {
        if(hud == null || mgr == null || !mgr.Loaded) return;
        hudStardust.text = $"{mgr.Stardust:N0}";
        RefreshWeaponHud(hud.gameObject.activeSelf);
        var top = TopTags(3);
        for(int i = 0; i < 3; i++) {
            if(hudTileTag[i] != top[i]) {
                if(hudTile[i] != null) Destroy(hudTile[i].gameObject);
                hudTile[i] = IconTile(hud, AugmentDefs.TagIcon(top[i]), 50, new Vector2(-46, 110 - i * 58), AugmentDefs.TagColor(top[i]));
                hudTileTag[i] = top[i];
            }
            hudTag[i].text = mgr.TagCount(top[i]).ToString();
        }
        float stack = mgr.CurseStackTotal;
        hudStack.text = stack > 0 ? $"누적 피해\n+{stack * 100:0.#}%" : $"처치 {mgr.ExpeditionKills:N0}";
        bool can = mgr.CanRebirth;
        if(can != hudRebirthReady) {
            hudRebirthReady = can;
            SetBtnStyle(hudRebirthBtn, can ? BYellow : BGray);
        }
    }
    #endregion

    #region 출격 무기 표시 (오른쪽)
    RectTransform weaponHud;
    string weaponHudKey;
    bool weaponHudShown;

    // 지금 싸우는 무기 목록: 기본총 + 왼쪽·오른쪽 + 추가 무장
    void RefreshWeaponHud(bool show)
    {
        weaponHudShown = show;
        if(!show) {
            if(weaponHud != null) weaponHud.gameObject.SetActive(false);
            return;
        }
        var rows = new List<(string text, Color color)>();
        rows.Add(("기본총", new Color(0.55f, 0.6f, 0.75f)));
        if(mgr.DebugPickMode) {
            foreach(var w in mgr.OwnedWeapons()) if(mgr.IsEquipped(w)) AddWeaponRow(rows, w, "");   // 개발용: 고른 무기
        } else if(mgr.DebugEquipAll) {
            foreach(var w in mgr.OwnedWeapons()) AddWeaponRow(rows, w, "");   // 개발용: 전부 출격
        } else {
            AddWeaponRow(rows, mgr.LoadoutLeft, "");
            AddWeaponRow(rows, mgr.LoadoutRight, "");
        }
        AddWeaponRow(rows, mgr.ExtraWeapon, "<size=70%>추가</size> ");
        var sb = new System.Text.StringBuilder();
        foreach(var r in rows) sb.Append(r.text).Append('|');
        string key = sb.ToString();
        if(weaponHud != null && key == weaponHudKey) {
            if(!weaponHud.gameObject.activeSelf) weaponHud.gameObject.SetActive(true);
            return;
        }
        weaponHudKey = key;
        if(weaponHud != null) Destroy(weaponHud.gameObject);

        float rowH = rows.Count > 6 ? 54f : 70f, step = rowH + 6f;
        float h = 64f + rows.Count * step + 8f;
        weaponHud = Rect("WeaponHud", hudRoot, new Vector2(200, h), Vector2.zero);
        weaponHud.anchorMin = weaponHud.anchorMax = new Vector2(1f, 0.6f);
        weaponHud.pivot = new Vector2(1f, 0.5f);
        weaponHud.anchoredPosition = new Vector2(-10, 0);
        var bg = weaponHud.gameObject.AddComponent<Image>();
        ApplySprite(bg, Skin("ItemFrame02_Single_Navy"), weaponHud.sizeDelta);
        bg.color = new Color(1f, 1f, 1f, 0.88f);
        bg.raycastTarget = false;
        Label(weaponHud, "출격 무기", new Vector2(180, 40), new Vector2(0, h / 2 - 34), 28, Color.white, FontStyles.Bold);
        for(int i = 0; i < rows.Count; i++) {
            float y = h / 2 - 64f - rowH / 2 - i * step;
            var card = ColorCard(weaponHud, new Vector2(180, rowH), new Vector2(0, y), rows[i].color, "Weapon");
            card.raycastTarget = false;
            Label(card.transform, rows[i].text, new Vector2(166, rowH - 8), new Vector2(0, 1), 26, Color.white, FontStyles.Bold);
        }
    }

    void AddWeaponRow(List<(string, Color)> rows, SideWeapon w, string prefix, string suffix = "")
    {
        w = mgr.ShownWeapon(w);   // 합체 재료(B)로 들어간 칸은 대신 들어온 A 로 보여요
        if(w == SideWeapon.None || !mgr.Owned(w)) return;
        var tag = AugmentDefs.WeaponTag(w);
        Color c = tag.HasValue ? AugmentDefs.TagColor(tag.Value) : AugmentDefs.TagColor(AugTag.Split);
        string lv = suffix.Length > 0 ? suffix : $" <size=70%>Lv.{mgr.WeaponLevel(w)}</size>";
        int f = mgr.FusionOf(w);
        string name = f >= 0 ? $"<color=#E9A6FF>{AugmentDefs.Fusions[f].name}</color>" : AugmentDefs.WeaponName(w);   // 합체 무기는 합체 이름
        rows.Add(($"{prefix}{name}{lv}", c * new Color(0.85f, 0.85f, 0.85f, 1f)));
    }
    #endregion

    #region 토스트
    readonly List<RectTransform> toasts = new List<RectTransform>();

    public void Toast(string text, Color color, bool big = false)
    {
        if(!EnsureBuilt()) return;
        var box = Frame(toastRoot, "ItemFrame02_Single_Navy", new Vector2(900, big ? 124 : 96), Vector2.zero, new Color(1, 1, 1, 0.95f), "Toast");
        var rt = box.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.78f);
        Label(rt, text, new Vector2(850, big ? 100 : 76), Vector2.zero, big ? 42 : 34, color, big ? FontStyles.Bold : FontStyles.Normal);
        toasts.Insert(0, rt);
        for(int i = 0; i < toasts.Count; i++)
            if(toasts[i] != null) toasts[i].anchoredPosition = new Vector2(0, -i * 112);
        StartCoroutine(ToastLife(rt, big ? 3f : 2.2f));
    }

    IEnumerator ToastLife(RectTransform rt, float life)
    {
        var cg = rt.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        float t = 0;
        while(t < life) {
            t += Time.unscaledDeltaTime;
            cg.alpha = t < 0.15f ? t / 0.15f : t > life - 0.4f ? (life - t) / 0.4f : 1f;
            yield return null;
        }
        toasts.Remove(rt);
        Destroy(rt.gameObject);
    }
    #endregion

    #region 증강 뽑기 카드
    RectTransform pullPanel;
    Action pullClose;
    Coroutine pullRoutine;

    // 증강 고르기 (2026-11-25 사용자: "3개의 선택지 중에서 고르게. 고민도 하고 후회도 하고 '이건 잘 골랐네'도 하게")
    //  3장(aug_choices) 중 하나를 눌러 고르고 '받기'로 확정. 카드마다 오른쪽 리롤(자리마다 한 번).
    //  aug_auto_seconds(15초) 동안 안 고르면 고른 카드(없으면 첫 카드)를 받아요 (방치 중에도 진행)
    //  (2026-11-25 사용자) 고른 카드는 빨간 테두리 + "선택" 표시로, 등급은 테두리가 아니라 카드 자체 바탕으로 구분:
    //   실버 = 차가운 회청색 / 골드 = 금빛 바탕 + 위쪽 금색 띠 / 프리즘 = 무지개로 흐르는 바탕. 그림은 임시 (P8)
    static Color GradeCardBg(AugGrade g) => g == AugGrade.Gold ? new Color(0.46f, 0.32f, 0.07f) : g == AugGrade.Prism ? new Color(0.35f, 0.2f, 0.5f) : new Color(0.27f, 0.31f, 0.4f);
    static Color GradeNameColor(AugGrade g) => g == AugGrade.Gold ? new Color(1f, 0.86f, 0.4f) : Color.white;
    static readonly Color PickRed = new Color(1f, 0.2f, 0.22f);
    static readonly Color WeaponCardBg = new Color(0.03f, 0.33f, 0.42f);       // 추가 무기 카드 바탕 (청록)
    static readonly Color WeaponCardRibbon = new Color(1f, 0.45f, 0.12f);      // 추가 무기 카드 리본 (주황)

    public void ShowPull(List<AugmentDef> defs, List<int> newLevels, List<bool> rerolled, string rerollLabel, Action<int> onAccept, Action<int> onReroll)
    {
        if(!EnsureBuilt() || defs.Count == 0) { onAccept?.Invoke(0); return; }
        if(pullPanel == null) pullPanel = OpenModal(new Vector2(900, 1500), out pullClose);
        for(int i = pullPanel.childCount - 1; i >= 0; i--) Destroy(pullPanel.GetChild(i).gameObject);
        if(pullRoutine != null) StopCoroutine(pullRoutine);

        bool done = false;
        int picked = -1;
        Action<int> accept = idx => {
            if(done) return;
            done = true;
            if(pullRoutine != null) StopCoroutine(pullRoutine);
            pullClose?.Invoke();
            pullPanel = null;
            onAccept?.Invoke(Mathf.Clamp(idx, 0, defs.Count - 1));
        };

        Title(pullPanel, mgr.InPlanetRun ? $"레벨 업! Lv.{mgr.PlanetLevel}" : "증강 선택", 690, 58);
        Label(pullPanel, "하나를 골라요", new Vector2(780, 46), new Vector2(0, 625), 32, TextDim);

        int n = defs.Count;
        var cards = new RectTransform[n];
        var rings = new GameObject[n];
        var groups = new CanvasGroup[n];
        var bgs = new Image[n];
        var ribbons = new Image[n];   // 추가 무기 카드 리본 (반짝여요)
        var grades = new AugGrade[n];
        Action refresh = null;
        for(int i = 0; i < n; i++) {
            var def = defs[i];
            int lv = i < newLevels.Count ? newLevels[i] : 1;
            int idx = i;
            grades[i] = def.grade;
            float y = 395f - i * 330f;

            // 고른 표시: 카드 뒤 빨간 테두리 + 왼쪽 위 "선택" 딱지 (처음엔 숨김)
            var ring = Box(pullPanel, new Vector2(716, 336), new Vector2(-80, y), PickRed, "PickRing");
            rings[i] = ring.gameObject;

            // 카드: 어두운 테두리 + 등급별 바탕 (2026-11-25 사용자: 테두리 말고 카드 자체로 등급 구분)
            var card = Rect("ChoiceCard", pullPanel, new Vector2(690, 310), new Vector2(-80, y));
            cards[i] = card;
            groups[i] = card.gameObject.AddComponent<CanvasGroup>();
            ColorCard(card, new Vector2(690, 310), Vector2.zero, new Color(0.16f, 0.15f, 0.22f), "ChoiceFrame");
            bool weaponCard = def.grantWeapon != SideWeapon.None;   // 행성 '추가 무기' 카드는 따로 꾸며요 (2026-11-25 사용자)
            var bg = Box(card, new Vector2(660, 280), Vector2.zero, weaponCard ? WeaponCardBg : GradeCardBg(def.grade), "GradeBg");
            bgs[i] = bg;
            if(weaponCard) {
                // 청록 바탕 + 주황 리본 "★ 추가 무기 ★" + 왼쪽 큰 원에 무기 이름
                var ribbon = Box(card, new Vector2(660, 52), new Vector2(0, 114), WeaponCardRibbon, "WeaponRibbon");
                ribbons[i] = ribbon;
                Label(ribbon.transform, "★  추가 무기  ★", new Vector2(620, 46), Vector2.zero, 32, Color.white, FontStyles.Bold);
                var orb = Box(card, new Vector2(176, 176), new Vector2(-230, -14), WeaponCardRibbon, "WeaponOrb");
                Box(orb.transform, new Vector2(150, 150), Vector2.zero, new Color(0.04f, 0.2f, 0.28f), "WeaponOrbIn");
                Label(orb.transform, AugmentDefs.WeaponName(def.grantWeapon), new Vector2(150, 120), Vector2.zero, 34, Color.white, FontStyles.Bold);
                Label(card, AugmentDefs.WeaponName(def.grantWeapon), new Vector2(440, 60), new Vector2(95, 50), 50, new Color(0.55f, 1f, 1f), FontStyles.Bold);
                Label(card, "두 번째 무기로 바로 같이 쏴요", new Vector2(440, 40), new Vector2(95, -4), 28, TextGold, FontStyles.Bold);
                Label(card, "이 행성에서만 · 고르면 더는 안 나와요", new Vector2(440, 70), new Vector2(95, -70), 24, new Color(0.8f, 0.92f, 1f));
            } else {
            if(def.grade == AugGrade.Gold) Box(card, new Vector2(660, 46), new Vector2(0, 117), new Color(1f, 0.8f, 0.3f, 0.85f), "GoldBand");
            if(def.grade == AugGrade.Prism) Box(card, new Vector2(660, 46), new Vector2(0, 117), new Color(1f, 1f, 1f, 0.25f), "PrismBand");

            IconTile(card, def.icon, 170, new Vector2(-230, -6), AugmentDefs.GradeColor(def.grade));
            string chip = def.planetOnly ? "능력치 (행성)" : def.fusion >= 0 ? $"합체 · {AugmentDefs.Fusions[def.fusion].name}" : def.tag == AugTag.Weapon ? $"무기 · {AugmentDefs.WeaponName(def.requiredWeapon)}" : $"{AugmentDefs.TagName(def.tag)} · {AugmentDefs.GodName(def.tag)}";
            Color headCol = def.grade == AugGrade.Gold ? new Color(0.28f, 0.18f, 0.02f) : Color.white;   // 금색 띠 위는 진한 글자
            Label(card, $"{AugmentDefs.GradeName(def.grade)}  ·  {chip}", new Vector2(620, 40), new Vector2(0, 117), 26, headCol, FontStyles.Bold);
            Label(card, def.name, new Vector2(440, 56), new Vector2(95, 58), 42, GradeNameColor(def.grade), FontStyles.Bold);
            Label(card, lv <= 1 ? "NEW" : $"★{lv}  (최대 ★{def.maxLevel})", new Vector2(440, 34), new Vector2(95, 16), 26, lv <= 1 ? new Color(0.5f, 1f, 0.6f) : TextGold, FontStyles.Bold);
            Label(card, def.describe(lv), new Vector2(440, 110), new Vector2(95, -68), 26, new Color(0.88f, 0.9f, 1f));
            }
            var tag = Box(card, new Vector2(120, 44), new Vector2(-280, 128), PickRed, "PickTag");
            Label(tag.transform, "선택", new Vector2(116, 40), Vector2.zero, 26, Color.white, FontStyles.Bold);
            tag.gameObject.name = "PickTag";

            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { if(done) return; picked = idx; refresh(); });

            bool used = i < rerolled.Count && rerolled[i];   // 자리마다 한 번만 (2026-11-25 사용자)
            var rb = Btn(pullPanel, used ? "리롤\n완료" : "리롤", new Vector2(150, 200), new Vector2(355, y), used ? BGray : BPurple, () => { if(!done && !used) onReroll?.Invoke(idx); }, used ? 28 : 34);
            rb.interactable = !used;
        }
        Button take = null;
        refresh = () => {
            for(int i = 0; i < n; i++) {
                bool on = i == picked;
                rings[i].SetActive(on);
                var t = cards[i].Find("PickTag");
                if(t != null) t.gameObject.SetActive(on);
                cards[i].localScale = on ? Vector3.one * 1.03f : Vector3.one;
                rings[i].transform.localScale = cards[i].localScale;
                groups[i].alpha = picked < 0 || on ? 1f : 0.55f;   // 고르면 나머지는 흐리게
            }
            if(take != null) take.interactable = picked >= 0;
        };

        var countdown = Label(pullPanel, "", new Vector2(780, 44), new Vector2(0, -700), 28, TextDim);
        Label(pullPanel, rerollLabel, new Vector2(780, 40), new Vector2(0, -505), 28, TextDim);   // 남은 리롤 (카드 옆 버튼이 같이 써요)
        take = Btn(pullPanel, "받기", new Vector2(420, 130), new Vector2(0, -600), BGreen, () => { if(picked >= 0) accept(picked); }, 48);
        refresh();
        pullRoutine = StartCoroutine(PullCountdown(bgs, grades, ribbons, countdown, () => picked, accept));
    }

    IEnumerator PullCountdown(Image[] bgs, AugGrade[] grades, Image[] ribbons, TextMeshProUGUI countdown, Func<int> picked, Action<int> accept)
    {
        float left = Mathf.Max(3f, Tuning.Get("aug_auto_seconds", 15f));
        while(left > 0) {
            for(int i = 0; i < bgs.Length; i++)   // 프리즘: 바탕이 무지개로 흘러요
                if(ribbons[i] != null) {   // 추가 무기 카드: 리본이 반짝이고 바탕이 숨 쉬듯 밝아졌다 어두워져요
                    float w = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f + i);
                    ribbons[i].color = Color.Lerp(WeaponCardRibbon, new Color(1f, 0.85f, 0.3f), w);
                    if(bgs[i] != null) bgs[i].color = Color.Lerp(WeaponCardBg, new Color(0.08f, 0.5f, 0.6f), w * 0.6f);
                } else if(grades[i] == AugGrade.Prism && bgs[i] != null) bgs[i].color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.25f + i * 0.2f, 1f), 0.55f, 0.55f);
            countdown.text = picked() >= 0 ? $"{Mathf.CeilToInt(left)}초 뒤 고른 증강을 받아요" : $"{Mathf.CeilToInt(left)}초 뒤 첫 번째 증강을 받아요";
            left -= Time.unscaledDeltaTime;
            yield return null;
        }
        accept(Mathf.Max(0, picked()));
    }
    #endregion

    #region 알림 / 무기 선택
    public void ShowNotice(string title, string body, Action onClose)
    {
        if(!EnsureBuilt()) { onClose?.Invoke(); return; }
        var p = OpenModal(new Vector2(900, 940), out Action close);
        Label(p, title, new Vector2(780, 130), new Vector2(0, 345), 52, Color.white, FontStyles.Bold);
        var body_ = Row(p, new Vector2(800, 480), new Vector2(0, 0), "Body");
        Label(body_.transform, body, new Vector2(740, 430), Vector2.zero, 36, TextDim);
        Btn(p, "확인", new Vector2(380, 130), new Vector2(0, -360), BGreen, () => { close(); onClose?.Invoke(); }, 48);
    }

    public void ShowWeaponChoice(string title, List<SideWeapon> options, Action<SideWeapon> onPick)
    {
        if(!EnsureBuilt() || options.Count == 0) { onPick?.Invoke(options.Count > 0 ? options[0] : SideWeapon.None); return; }
        float h = 320 + options.Count * 150;
        var p = OpenModal(new Vector2(820, h), out Action close);
        Title(p, title, h / 2 - 90, 46, 780);
        for(int i = 0; i < options.Count; i++) {
            var w = options[i];
            var tag = AugmentDefs.WeaponTag(w);
            float y = h / 2 - 230 - i * 150;
            var row = ColorCard(p, new Vector2(640, 130), new Vector2(0, y), tag.HasValue ? AugmentDefs.TagColor(tag.Value) : AugmentDefs.TagColor(AugTag.Split), "Weapon");
            Label(row.transform, $"{AugmentDefs.WeaponName(w)}  Lv.{mgr.WeaponLevel(w)}", new Vector2(580, 90), new Vector2(0, 2), 42, Color.white, FontStyles.Bold);
            var b = row.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => { close(); onPick?.Invoke(w); });
        }
    }

    // 기본총 룰렛 = 슬롯머신 (2026-11-25 사용자: 슬롯머신 이미지처럼, 기계는 하나)
    //  '돌리기!'를 누르면 릴 3개가 위에서 아래로 돌다가 왼쪽부터 차례로 멈춰요. 보통 칸은 세 릴이 같은 그림, 대박은 7 7 7, 꽝은 그림이 안 맞아요.
    //  그림: GUI Pro 아이콘 (Resources/RemasterUI 로 복사), 몸통·전구는 코드로 그린 임시 그림 (다듬기는 P8)
    static readonly Color SlotBody = new Color(0.22f, 0.12f, 0.38f);
    static readonly Color SlotArch = new Color(0.34f, 0.15f, 0.52f);
    static readonly Color SlotNeon = new Color(1f, 0.4f, 0.88f);
    static readonly Color SlotBulbOn = new Color(1f, 0.86f, 0.35f);
    static readonly Color SlotBulbOff = new Color(0.45f, 0.3f, 0.2f);
    const float SlotH = 165f;          // 릴 그림 한 칸 높이
    const float SlotWinH = 330f;       // 릴 창 높이

    static string SlotIcon(string id)
    {
        switch(id) {
            case "dmg": return "Icon_Sword01";
            case "speed": return "ItemIcon_Energy_Blue";
            case "shot": return "ItemIcon_Battle";
            case "pierce": return "Icon_Arrow_Next2";
            case "crit": return "ItemIcon_Damage";
            case "size": return "ItemIcon_Gem_Circle";
            case "dud": return "ItemIcon_Skull";
        }
        return null;
    }
    static string SlotShort(string id)
    {
        switch(id) {
            case "dmg": return "공격력";
            case "speed": return "연사";
            case "shot": return "탄 +1";
            case "pierce": return "관통";
            case "crit": return "치명타";
            case "size": return "탄 크기";
            case "jackpot": return "대박";
            case "dud": return "꽝";
        }
        return id;
    }

    void SlotSymbol(Transform parent, string id, Vector2 pos)
    {
        var cell = Rect("Sym", parent, new Vector2(190, SlotH), pos);
        if(id == "jackpot") {
            Label(cell, "7", new Vector2(170, 130), new Vector2(0, 16), 120, new Color(1f, 0.18f, 0.25f), FontStyles.Bold);
        } else {
            var img = Rect("Icon", cell, new Vector2(100, 100), new Vector2(0, 20)).gameObject.AddComponent<Image>();
            img.sprite = Skin(SlotIcon(id));
            img.preserveAspect = true;
            if(img.sprite == null) img.color = Color.clear;
            else if(id == "pierce") img.color = new Color(1f, 0.55f, 0.15f);   // 흰 화살표라 주황으로
        }
        Label(cell, SlotShort(id), new Vector2(190, 38), new Vector2(0, -56), 28, new Color(0.28f, 0.18f, 0.4f), FontStyles.Bold);
    }

    public void ShowRoulette(string[] ids, string result, Action onDone)
    {
        if(!EnsureBuilt()) { onDone?.Invoke(); return; }
        var p = OpenModal(new Vector2(900, 1280), out Action close);

        // 몸통 + 위쪽 간판(아치)
        ColorCard(p, new Vector2(820, 900), new Vector2(0, -60), SlotBody, "SlotBody");
        var arch = ColorCard(p, new Vector2(700, 230), new Vector2(0, 470), SlotArch, "SlotArch");
        Label(arch.transform, "기본총 룰렛", new Vector2(640, 90), new Vector2(0, 22), 64, SlotNeon, FontStyles.Bold);
        Label(arch.transform, "JACKPOT 777", new Vector2(640, 50), new Vector2(0, -56), 32, TextGold, FontStyles.Bold);

        // 전구: 간판 둘레 + 몸통 양옆
        var bulbs = new List<Image>();
        for(int i = 0; i < 13; i++) {
            float a = Mathf.Lerp(168f, 12f, i / 12f) * Mathf.Deg2Rad;
            bulbs.Add(Box(p, new Vector2(30, 30), new Vector2(Mathf.Cos(a) * 375f, 445f + Mathf.Sin(a) * 150f), SlotBulbOn, "Bulb"));
        }
        for(int i = 0; i < 7; i++)
            for(int side = -1; side <= 1; side += 2)
                bulbs.Add(Box(p, new Vector2(26, 26), new Vector2(side * 388f, 340f - i * 125f), SlotBulbOn, "Bulb"));

        // 릴 3개 (창 안에서 그림 띠가 위아래로 움직여요)
        var frame = Box(p, new Vector2(710, 390), new Vector2(0, 150), new Color(0.1f, 0.05f, 0.18f), "ReelFrame");
        var strips = new RectTransform[3];
        int n = ids.Length;
        for(int r = 0; r < 3; r++) {
            var win = Box(frame.transform, new Vector2(210, SlotWinH), new Vector2((r - 1) * 225f, 0f), new Color(0.97f, 0.95f, 1f), "Reel").rectTransform;
            win.gameObject.AddComponent<RectMask2D>();
            float stripH = SlotH * n * 3;
            var strip = Rect("Strip", win, new Vector2(200, stripH), Vector2.zero);
            strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0.5f, 1f);
            for(int j = 0; j < n * 3; j++) SlotSymbol(strip, ids[j % n], new Vector2(0, stripH / 2f - SlotH * (j + 0.5f)));
            strips[r] = strip;
            SetSlotStrip(strip, 0f, n);
        }
        Box(frame.transform, new Vector2(700, 6), Vector2.zero, new Color(1f, 0.2f, 0.3f, 0.55f), "PayLine");   // 가운데 당첨 줄

        var resultText = Label(p, "", new Vector2(780, 70), new Vector2(0, -130), 42, TextGold, FontStyles.Bold);
        Label(p, "기본총 능력치 뽑기", new Vector2(780, 40), new Vector2(0, -200), 26, TextDim);
        Button spin = null;
        Action go = () => {
            if(spin == null) return;
            Destroy(spin.gameObject);
            spin = null;
            StartCoroutine(SpinSlots(ids, result, strips, bulbs, resultText, p, close, onDone));
        };
        spin = Btn(p, "돌리기!", new Vector2(440, 140), new Vector2(0, -360), BRed, go, 58);
        StartCoroutine(SlotAutoSpin(spin, go));
    }

    // 안 누르면 rl_auto_seconds(5초) 뒤 저절로 돌아요 (2026-11-25 사용자). 버튼에 남은 초를 보여 줘요
    System.Collections.IEnumerator SlotAutoSpin(Button spin, Action go)
    {
        float left = Mathf.Max(1f, Tuning.Get("rl_auto_seconds", 5f));
        var label = spin != null ? spin.GetComponentInChildren<TextMeshProUGUI>() : null;
        while(left > 0f) {
            if(spin == null) yield break;   // 이미 눌렀어요
            if(label != null) label.text = $"돌리기! ({Mathf.CeilToInt(left)})";
            left -= Time.unscaledDeltaTime;
            yield return null;
        }
        if(spin != null) go();
    }

    // 띠 위치: pos 가 늘수록 그림이 아래로 내려가요. 창 가운데 칸 = 2n - (pos mod n) (가운데 묶음이라 위아래 이웃 칸도 늘 있어요)
    static void SetSlotStrip(RectTransform strip, float pos, int n)
    {
        float c = 2 * n - Mathf.Repeat(pos, n);
        strip.anchoredPosition = new Vector2(0f, SlotH * (c + 0.5f) - SlotWinH / 2f);
    }

    System.Collections.IEnumerator SpinSlots(string[] ids, string result, RectTransform[] strips, List<Image> bulbs, TextMeshProUGUI resultText, RectTransform p, Action close, Action onDone)
    {
        int n = ids.Length;
        // 릴마다 멈출 그림: 보통은 셋 다 결과, 꽝은 [다른 그림, 해골, 또 다른 그림]
        var stops = new string[] { result, result, result };
        if(result == "dud") {
            var pool = new List<string>();
            foreach(var id in ids) if(id != "dud" && id != "jackpot") pool.Add(id);
            int a = UnityEngine.Random.Range(0, pool.Count), b = (a + UnityEngine.Random.Range(1, pool.Count)) % pool.Count;
            stops[0] = pool[a]; stops[1] = "dud"; stops[2] = pool[b];
        }
        float total = Mathf.Max(0.8f, Tuning.Get("rl_spin_seconds", 2.2f));
        var dur = new float[] { total * 0.6f, total * 0.8f, total };
        var end = new float[3];
        for(int r = 0; r < 3; r++) {
            int k = Mathf.Max(0, Array.IndexOf(ids, stops[r]));
            end[r] = (3 + r) * n + (n - k) % n;   // 몇 바퀴 돌고 결과 칸에서 멈춰요
        }
        float t = 0f, blink = 0f;
        bool phase = false;
        while(t < total) {
            if(p == null) yield break;
            t += Time.unscaledDeltaTime;
            for(int r = 0; r < 3; r++) {
                float u = Mathf.Clamp01(t / dur[r]);
                float e = 1f - Mathf.Pow(1f - u, 3f);   // 끝으로 갈수록 느려져요
                SetSlotStrip(strips[r], end[r] * e, n);
            }
            blink += Time.unscaledDeltaTime;
            if(blink >= 0.1f) {
                blink = 0f;
                phase = !phase;
                for(int i = 0; i < bulbs.Count; i++) bulbs[i].color = (i % 2 == 0) == phase ? SlotBulbOn : SlotBulbOff;
            }
            yield return null;
        }
        if(p == null) yield break;
        for(int r = 0; r < 3; r++) SetSlotStrip(strips[r], end[r], n);

        // 당첨 연출: 전구가 다 같이 깜빡여요 (대박은 더 많이)
        int flashes = result == "jackpot" ? 8 : result == "dud" ? 0 : 3;
        for(int f = 0; f < flashes * 2; f++) {
            if(p == null) yield break;
            foreach(var b in bulbs) b.color = f % 2 == 0 ? SlotBulbOn : SlotBulbOff;
            yield return new WaitForSecondsRealtime(0.09f);
        }
        if(p == null) yield break;
        foreach(var b in bulbs) b.color = SlotBulbOn;
        resultText.text = AugmentManager.RouletteName(result).Replace("\n", " ");
        resultText.color = result == "dud" ? TextDim : TextGold;
        Btn(p, "확인", new Vector2(440, 140), new Vector2(0, -360), BGreen, () => { close(); onDone?.Invoke(); }, 56);
    }
    #endregion

    #region 원정 준비 (출격 무기 + 축복)
    public void ShowSetup(Action onDone)
    {
        if(!EnsureBuilt()) { onDone?.Invoke(); return; }
        var p = OpenModal(new Vector2(980, 1540), out Action close);
        SideWeapon left = mgr.LoadoutLeft, right = mgr.LoadoutRight;
        int blessing = mgr.Blessing;
        if(blessing >= 0 && !mgr.BlessingUnlocked((AugTag)blessing)) blessing = -1;

        Title(p, "원정 준비", 680, 62, 940);
        Label(p, "축복을 고르고 출발하세요", new Vector2(860, 48), new Vector2(0, 610), 32, TextDim);

        // ---- 출격 무기 (2026-11-25 사용자 결정: 기본총 하나로 출발 → 정한 스테이지에 무기 카드 3장 중 하나)
        //  예전엔 여기서 왼쪽·오른쪽 무기를 골랐어요. 이제 칸은 안내만 (이미 고른 무기가 있으면 보여 줘요)
        var weaponBox = Row(p, new Vector2(900, 420), new Vector2(0, 370), "WeaponBox");
        Label(weaponBox.transform, "출격 무기", new Vector2(840, 56), new Vector2(0, 160), 40, Color.white, FontStyles.Bold);
        var owned = mgr.OwnedWeapons();
        var leftCard = ColorCard(weaponBox.transform, new Vector2(260, 210), new Vector2(-280, 20), LockedTint, "Left");
        var leftText = Label(leftCard.transform, "", new Vector2(220, 170), new Vector2(0, 2), 34, Color.white, FontStyles.Bold);
        var center = ColorCard(weaponBox.transform, new Vector2(240, 210), new Vector2(0, 20), new Color(0.55f, 0.6f, 0.75f), "MainGun");
        Label(center.transform, "기본총\n(고정)", new Vector2(200, 170), new Vector2(0, 2), 36, Color.white, FontStyles.Bold);
        var rightCard = ColorCard(weaponBox.transform, new Vector2(260, 210), new Vector2(280, 20), LockedTint, "Right");
        var rightText = Label(rightCard.transform, "", new Vector2(220, 170), new Vector2(0, 2), 34, Color.white, FontStyles.Bold);
        Label(weaponBox.transform, owned.Count == 0 ? "무기 상점에서 무기를 사면 원정 중에 고를 수 있어요" : "정한 스테이지가 되면 가진 무기 카드 3장 중 하나를 골라요", new Vector2(840, 44), new Vector2(0, -150), 28, TextDim);
        if(left != SideWeapon.None) SetSlot(leftCard, leftText, "왼쪽", left);
        else leftText.text = $"왼쪽\n<size=80%>{AugmentManager.WeaponSlotStage(0)}스테이지에\n열려요</size>";
        if(right != SideWeapon.None) SetSlot(rightCard, rightText, "오른쪽", right);
        else rightText.text = $"오른쪽\n<size=80%>{AugmentManager.WeaponSlotStage(1)}스테이지에\n열려요</size>";

        // ---- 축복
        var blessBox = Row(p, new Vector2(900, 620), new Vector2(0, -170), "BlessBox");
        Label(blessBox.transform, "축복", new Vector2(840, 56), new Vector2(0, 268), 40, Color.white, FontStyles.Bold);
        // 9계열의 신 3×3. 신의 관문을 처음 이기면 열려요
        var tags = AugmentDefs.MainTags;
        var cards = new Image[tags.Length];
        Action refreshCards = null;
        bool anyUnlocked = false;
        for(int i = 0; i < tags.Length; i++) {
            var tag = tags[i];
            int tagIdx = (int)tag;
            bool unlocked = mgr.BlessingUnlocked(tag);
            anyUnlocked |= unlocked;
            var card = ColorCard(blessBox.transform, new Vector2(270, 150), new Vector2(-285 + (i % 3) * 285, 160 - (i / 3) * 158), unlocked ? AugmentDefs.TagColor(tag) : LockedTint, "Blessing");
            cards[i] = card;
            var face = Row(card.transform, new Vector2(244, 126), new Vector2(0, 2), "Face");
            if(unlocked) IconTile(face.transform, AugmentDefs.TagIcon(tag), 76, new Vector2(-72, 0), AugmentDefs.TagColor(tag));
            else SkinIcon(face.transform, "Icon_Lock03", new Vector2(56, 56), new Vector2(-72, 0));
            Label(face.transform, AugmentDefs.GodName(tag), new Vector2(150, 44), new Vector2(42, 22), 26, unlocked ? AugmentDefs.TagColor(tag) : TextDim, FontStyles.Bold);
            string sub = unlocked ? $"{AugmentDefs.TagName(tag)} ×2" : $"{AugmentDefs.BlessingGate(tag)}번째 관문";
            Label(face.transform, sub, new Vector2(150, 40), new Vector2(42, -24), 22, TextDim);
            if(unlocked) {
                var b = card.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { blessing = blessing == tagIdx ? -1 : tagIdx; refreshCards(); });
            }
        }
        Label(blessBox.transform, anyUnlocked ? "고른 신의 계열 증강이 2배 잘 나와요 · 다시 누르면 해제" : "신의 관문(50스테이지마다)을 처음 이기면 그 신의 축복이 열려요", new Vector2(840, 44), new Vector2(0, -262), 26, TextDim);
        refreshCards = () => {
            for(int i = 0; i < tags.Length; i++) {
                if(!mgr.BlessingUnlocked(tags[i])) continue;
                bool on = blessing == (int)tags[i];
                // 고른 카드는 밝고 크게, 나머지는 살짝 어둡게
                cards[i].color = on ? AugmentDefs.TagColor(tags[i]) : AugmentDefs.TagColor(tags[i]) * new Color(0.55f, 0.55f, 0.55f, 1f);
                cards[i].rectTransform.localScale = on ? Vector3.one * 1.06f : Vector3.one;
            }
        };
        refreshCards();

        Btn(p, "출발!", new Vector2(440, 142), new Vector2(0, -620), BGreen, () => {
            mgr.SetBlessing(blessing);
            close();
            onDone?.Invoke();
        }, 58);
    }

    void SetSlot(Image card, TextMeshProUGUI label, string side, SideWeapon w)
    {
        if(w == SideWeapon.None) {
            label.text = $"{side}\n<size=80%>비어 있음</size>";
            card.color = LockedTint;
        } else {
            label.text = $"<size=75%>{side}</size>\n{AugmentDefs.WeaponName(w)}\n<size=75%>Lv.{mgr.WeaponLevel(w)}</size>";
            var tag = AugmentDefs.WeaponTag(w);
            card.color = tag.HasValue ? AugmentDefs.TagColor(tag.Value) : AugmentDefs.TagColor(AugTag.Split);
        }
    }
    #endregion

    #region 개발용 증강 테스트 창 (F10)
    static readonly AugTag[] DebugTags = {
        AugTag.Chain, AugTag.Curse, AugTag.Split, AugTag.Crit, AugTag.Plague, AugTag.Thunder,
        AugTag.Frost, AugTag.Gravity, AugTag.Guard, AugTag.Treasure, AugTag.Weapon
    };

    // 계열마다 화면에서 눈으로 확인할 것
    static string DebugHint(AugTag t)
    {
        switch(t) {
            case AugTag.Chain: return "적을 잡으면 주황 폭발 · 잔불은 주황 원이 깜빡여요";
            case AugTag.Curse: return "왼쪽 원정 칸의 '누적 피해 %'가 올라가요";
            case AugTag.Split: return "파란 선(파편) · 탄 크기·개수 · 화면 끝에서 튕김";
            case AugTag.Crit: return "치명타 숫자가 자주 뜨고 · 낮은 체력 적이 바로 죽어요";
            case AugTag.Plague: return "초록 선으로 독이 옮고 · 독 피해 숫자가 계속 떠요";
            case AugTag.Thunder: return "노란 선이 적 사이로 튀고 · 위에서 낙뢰가 떨어져요";
            case AugTag.Frost: return "적이 느려지고 · 하늘색으로 멈춰요(빙결)";
            case AugTag.Gravity: return "보라색 원(블랙홀)이 적을 모아요 · 적이 뭉쳐요";
            case AugTag.Guard: return "적이 우주선에 닿을 때(실드가 깎일 때) 확인해요";
            case AugTag.Treasure: return "실드·리롤·무기 칸 같은 보상이에요";
            default: return "출격한 무기 것만 동작해요 · 프리즘 [합체] 카드 +1 = 바로 합체 · 합체 증강은 합체 뒤에만 동작";
        }
    }

    AugTag debugTag = AugTag.Chain;

    // [개발] 무기 고르기: 출격할 무기와 합체 무기를 직접 골라요 (테스트 창 위에 떠요)
    void ShowDebugWeapons(Action onClose)
    {
        var p = OpenModal(new Vector2(980, 1640), out Action close);
        BuildDebugWeapons(p, () => { close(); onClose?.Invoke(); });
    }

    void BuildDebugWeapons(RectTransform p, Action close)
    {
        for(int i = p.childCount - 1; i >= 0; i--) Destroy(p.GetChild(i).gameObject);
        Title(p, "[개발] 무기 고르기", 730, 50, 940);
        Label(p, mgr.DebugPickMode ? "고른 무기만 싸워요 (없는 무기는 Lv.1 로 받아요)" : "지금은 원정 준비에서 고른 무기로 싸워요\n무기를 누르면 고른 무기만 싸워요", new Vector2(900, 90), new Vector2(0, 640), 26, TextDim);

        Label(p, "무기 (누르면 켜고 끄기)", new Vector2(900, 50), new Vector2(0, 560), 30, Color.white, FontStyles.Bold);
        var weapons = new List<SideWeapon>();
        foreach(SideWeapon w in Enum.GetValues(typeof(SideWeapon))) if(w != SideWeapon.None) weapons.Add(w);
        for(int i = 0; i < weapons.Count; i++) {
            var w = weapons[i];
            int row = i / 3, col = i % 3;
            bool on = mgr.DebugPicked(w);
            int f = mgr.FusionOf(w);
            string label = AugmentDefs.WeaponName(w) + (f >= 0 ? $"\n<size=70%>({AugmentDefs.Fusions[f].name})</size>" : "");
            float y = 480 - row * 118;   // 무기 10개 (3칸 4줄, 2026-11-21 운석 추가)
            Btn(p, label, new Vector2(290, 80), new Vector2((col - 1) * 300f, y), on ? BGreen : BGray, () => { mgr.DebugToggleWeapon(w); BuildDebugWeapons(p, close); }, 28);
            Btn(p, "증강 보기", new Vector2(290, 42), new Vector2((col - 1) * 300f, y - 64), BBlue, () => ShowDebugWeaponAugs(w, () => BuildDebugWeapons(p, close)), 22);
        }

        Label(p, "합체 무기 (누르면 합체 · 다시 누르면 풀기)", new Vector2(900, 50), new Vector2(0, 10), 30, Color.white, FontStyles.Bold);
        for(int i = 0; i < AugmentDefs.Fusions.Length; i++) {
            int f = i;
            var fd = AugmentDefs.Fusions[i];
            bool on = mgr.FusionOwned(f) && mgr.DebugPicked(fd.a);
            int row = i / 3, col = i % 3;
            string label = $"{fd.name}\n<size=65%>{AugmentDefs.FusionPair(fd)}</size>";
            float y = -60 - row * 105;   // 합체 11개 (3칸 4줄) 가 아래 버튼과 안 겹치게
            Btn(p, label, new Vector2(290, 68), new Vector2((col - 1) * 300f, y), on ? BPurple : BGray, () => { mgr.DebugPickFusion(f); BuildDebugWeapons(p, close); }, 22);
            Btn(p, "증강 보기", new Vector2(290, 32), new Vector2((col - 1) * 300f, y - 52), BBlue, () => ShowDebugWeaponAugs(fd.a, () => BuildDebugWeapons(p, close)), 20);
        }

        Btn(p, "전부 출격", new Vector2(290, 110), new Vector2(-310, -620), BGreen, () => { mgr.DebugPickAll(); BuildDebugWeapons(p, close); }, 32);
        Btn(p, "모두 끄기", new Vector2(290, 110), new Vector2(0, -620), BRed, () => { mgr.DebugPickClear(false); BuildDebugWeapons(p, close); }, 32);
        Btn(p, "원래대로", new Vector2(290, 110), new Vector2(310, -620), BBlue, () => { mgr.DebugPickClear(true); BuildDebugWeapons(p, close); }, 32);
        Btn(p, "닫기", new Vector2(440, 100), new Vector2(0, -740), BGray, () => close(), 36);
    }

    // [개발] 연습장 (P 키, 테스트 창 버튼): 멈춘 적을 세워 두고 무기·합체 무기를 눌러서 하나씩 바꿔 봐요
    public void ShowDebugArena()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var p = OpenModal(new Vector2(980, 1730), out Action close);
        BuildDebugArena(p, close);
#endif
    }

    void BuildDebugArena(RectTransform p, Action close)
    {
        for(int i = p.childCount - 1; i >= 0; i--) Destroy(p.GetChild(i).gameObject);
        Action refresh = () => BuildDebugArena(p, close);
        Title(p, "[개발] 연습장", 730, 50, 940);
        Label(p, AugmentManager.DebugArena ? "<color=#7CFF8A>켜짐</color>: 작은 적이 멈춰 있고, 지금 무기로 약 15대에 죽으면 빈자리에 새로 나와요" : "적 수를 누르면 시작해요. 작은 적이 멈춰 있고, 죽으면 새로 나와요",
              new Vector2(900, 50), new Vector2(0, 655), 26, TextDim);

        // 적
        var small = new Vector2(176, 76);
        Btn(p, "적\n20", small, new Vector2(-370, 575), BGreen, () => { mgr.DebugArenaStart(20); refresh(); }, 26);
        Btn(p, "적\n40", small, new Vector2(-185, 575), BGreen, () => { mgr.DebugArenaStart(40); refresh(); }, 26);
        Btn(p, "적\n70", small, new Vector2(0, 575), BGreen, () => { mgr.DebugArenaStart(70); refresh(); }, 26);
        Btn(p, "보스\n추가", small, new Vector2(185, 575), BRed, () => mgr.DebugArenaBoss(), 26);
        Btn(p, "연습장\n끄기", small, new Vector2(370, 575), BGray, () => { mgr.DebugArenaStop(); refresh(); }, 26);
        // 적 무적 (2026-11-21 사용자 요청): 켜면 적이 안 죽고 체력이 다시 차요 (위성 레이저처럼 오래 맞혀야 보이는 효과 확인용)
        Btn(p, AugmentManager.DebugArenaInvincible ? "적 무적\n켜짐" : "적 무적\n꺼짐", small, new Vector2(-370, 485), AugmentManager.DebugArenaInvincible ? BGreen : BGray,
            () => { AugmentManager.DebugArenaInvincible = !AugmentManager.DebugArenaInvincible; refresh(); }, 26);
        Btn(p, $"속도\nx{GameManager.DisplaySpeed:0.#}", small, new Vector2(-185, 485), BYellow, () => { DevTools.CycleSpeed(); refresh(); }, 26);
        Btn(p, DevTools.DpsOn ? "DPS 기록\n켜짐" : "DPS 기록\n꺼짐", small, new Vector2(0, 485), DevTools.DpsOn ? BGreen : BGray, () => { DevTools.ToggleDps(); refresh(); }, 26);
        Btn(p, "무기 전부\n열기", small, new Vector2(185, 485), BYellow, () => { mgr.DebugUnlockAllWeapons(); refresh(); }, 26);
        // 보스만 (2026-11-25 사용자: 튕김총 보스 되튕김 확인용, 작은 적 없이 멈춘 보스 1마리)
        Btn(p, "보스만", small, new Vector2(370, 485), BRed, () => { mgr.DebugArenaBossOnly(); refresh(); }, 26);

        // 무기 (누르면 그 무기 하나만)
        var solo = mgr.DebugSoloWeapon();
        Label(p, "무기 (누르면 이 무기 하나만 싸워요)", new Vector2(900, 50), new Vector2(0, 405), 28, Color.white, FontStyles.Bold);
        var weapons = new List<SideWeapon>();
        foreach(SideWeapon w in Enum.GetValues(typeof(SideWeapon))) if(w != SideWeapon.None) weapons.Add(w);
        for(int i = 0; i < weapons.Count; i++) {
            var w = weapons[i];
            bool on = solo == w && mgr.FusionOf(w) < 0;
            Btn(p, AugmentDefs.WeaponName(w), new Vector2(290, 76), new Vector2((i % 3 - 1) * 300f, 335 - (i / 3) * 84), on ? BGreen : BGray,
                () => { mgr.DebugSolo(w); refresh(); }, 26);
        }

        // 합체 무기 (누르면 그 합체 무기 하나만)
        Label(p, "합체 무기 (누르면 이 합체 무기 하나만)", new Vector2(900, 50), new Vector2(0, 0), 28, Color.white, FontStyles.Bold);
        for(int i = 0; i < AugmentDefs.Fusions.Length; i++) {
            int f = i;
            var fd = AugmentDefs.Fusions[i];
            bool on = solo == fd.a && mgr.FusionOf(fd.a) == f;
            Btn(p, $"{fd.name}\n<size=65%>{AugmentDefs.FusionPair(fd)}</size>", new Vector2(290, 76), new Vector2((i % 3 - 1) * 300f, -70 - (i / 3) * 88),
                on ? BPurple : BGray, () => { mgr.DebugSoloFusion(f); refresh(); }, 22);
        }

        // 측정 모드 (2026-11-22): 기본 무기 10개를 자동으로 바꿔 가며 DPS 를 Tuning/dps_measure.txt 에 남겨요
        //  (회차 3번 · 무기마다 미리 10초 돌리고 20초 재기. 상황 하나에 약 3분)
        Label(p, mgr.Measuring ? "<color=#7CFF8A>측정 중</color> (상황 하나 = 무기 10개 × 3회, 약 3분)" : "측정 모드: 무기 10개 × 3회 자동 측정 (증강은 지워요)", new Vector2(900, 40), new Vector2(0, -490), 24, TextDim);
        // 2026-11-22: 상황 6개 + 전부 + 전용 증강 켜고 끄기 + 멈추기 (5개씩 2줄)
        var mbtn = new Vector2(176, 66);
        Btn(p, "측정\n움직이는 적", mbtn, new Vector2(-370, -545), BGreen, () => { CloseAllModals(); mgr.DebugMeasure(0); }, 20);
        Btn(p, "측정\n멈춘 적 40", mbtn, new Vector2(-185, -545), BGreen, () => { CloseAllModals(); mgr.DebugMeasure(1); }, 20);
        Btn(p, "측정\n보스", mbtn, new Vector2(0, -545), BRed, () => { CloseAllModals(); mgr.DebugMeasure(2); }, 20);
        Btn(p, "측정\n단단한 적 40", mbtn, new Vector2(185, -545), BGreen, () => { CloseAllModals(); mgr.DebugMeasure(3); }, 20);
        Btn(p, "측정\n흩어진 7", mbtn, new Vector2(370, -545), BGreen, () => { CloseAllModals(); mgr.DebugMeasure(4); }, 20);
        Btn(p, "측정\n우주선 근처 12", mbtn, new Vector2(-370, -617), BGreen, () => { CloseAllModals(); mgr.DebugMeasure(5); }, 20);
        Btn(p, "측정 전부\n(6상황, 약 20분)", mbtn, new Vector2(-185, -617), BPurple, () => { CloseAllModals(); mgr.DebugMeasure(6); }, 20);
        // 전용 증강 단계 (누를 때마다 다음 단계): 위 버튼들로 한 상황씩 잴 때 쓰여요
        Btn(p, $"전용 증강\n{AugmentManager.MeasureAugNames[AugmentManager.MeasureAugStage]}", mbtn, new Vector2(0, -617), AugmentManager.MeasureAugStage > 0 ? BGreen : BGray,
            () => { AugmentManager.MeasureAugStage = (AugmentManager.MeasureAugStage + 1) % AugmentManager.MeasureAugNames.Length; refresh(); }, 20);
        Btn(p, "측정\n멈추기", mbtn, new Vector2(185, -617), mgr.Measuring ? BYellow : BGray, () => { mgr.DebugMeasureStop(); refresh(); }, 20);
        // 한 번 누르면 증강 5단계(없음·일부·1레벨·중간·최대) × 6상황을 전부 차례로 (약 1시간 40분)
        Btn(p, "측정 전체\n증강 5단계 (1시간 40분)", mbtn, new Vector2(370, -617), BRed, () => { CloseAllModals(); mgr.DebugMeasure(7); }, 17);
        // 기록이 빠진 칸(제대로 잰 회차가 모자란 칸)만 다시 · 속도 비교 x1 vs x5 (2026-11-22)
        // 합체 확인 (2026-11-23 사용자: 합체 확인 약 20분). 예전 "빈 칸만 다시"(mode 8) 자리
        Btn(p, "합체 확인\n(tuning 회차)", new Vector2(176, 60), new Vector2(-376, -688), BYellow, () => { CloseAllModals(); mgr.DebugMeasure(18); }, 17);
        Btn(p, "속도 비교\n(약 7분)", new Vector2(176, 60), new Vector2(-188, -688), BBlue, () => { CloseAllModals(); mgr.DebugMeasure(9); }, 17);
        // 초반·후반 확인 (2026-11-22 사용자: 초반형·후반형 나누기): 증강 없음·셋 다 최대 × 3상황 × 2회
        Btn(p, "초반·후반 확인\n(약 50분)", new Vector2(176, 60), new Vector2(0, -688), BGreen, () => { CloseAllModals(); mgr.DebugMeasure(16); }, 16);
        // 증강 하나씩 (2026-11-22): 전용 증강 30개를 하나씩 최대로 · 3상황 · 2회
        Btn(p, "증강 하나씩\n(약 1시간 15분)", new Vector2(176, 60), new Vector2(188, -688), BPurple, () => { CloseAllModals(); mgr.DebugMeasure(15); }, 15);
        // 밤새 측정 (2026-11-22 사용자: 자는 동안 6시간쯤): 속도 비교 → 기본 무기 5단계 → 합체 무기 → 종결
        Btn(p, "밤새 측정\n(약 6시간 25분)", new Vector2(176, 60), new Vector2(376, -688), BRed, () => { CloseAllModals(); mgr.DebugMeasure(10); }, 15);
        Btn(p, "지금 무기\n증강 보기", new Vector2(290, 80), new Vector2(-310, -775), BBlue, () => {
            var cur = mgr.DebugSoloWeapon();
            if(cur == SideWeapon.None) { Toast("[개발] 먼저 무기를 하나 골라 주세요", Color.white); return; }
            ShowDebugWeaponAugs(cur, refresh);
        }, 28);
        // 종결 발동 (2026-11-21 사용자 요청): 지금 고른 합체 무기의 종결을 바로 (창을 닫고 발동). 게임 중엔 U 키
        int curFusion = mgr.CurrentUltFusion();
        Btn(p, curFusion >= 0 ? $"종결 발동\n<size=70%>{AugmentDefs.UltNames[curFusion]}</size>" : "종결 발동\n<size=70%>(합체 무기만)</size>",
            new Vector2(290, 80), new Vector2(0, -775), curFusion >= 0 ? BYellow : BGray, () => { CloseAllModals(); mgr.DebugTriggerCurrentUlt(); }, 26);
        Btn(p, "닫기", new Vector2(290, 80), new Vector2(310, -775), BGray, () => close(), 34);
        Label(p, "P 연습장 · U 종결 발동 · F7 게임 속도 · F12 스크린샷", new Vector2(900, 40), new Vector2(0, -840), 24, TextDim);
    }

    // [개발] 무기 하나의 증강만: 전용 3개 + 합체 카드 + 합체 전용 증강
    void ShowDebugWeaponAugs(SideWeapon w, Action onClose)
    {
        var p = OpenModal(new Vector2(980, 1700), out Action close);
        BuildDebugWeaponAugs(p, w, () => { close(); onClose?.Invoke(); });
    }

    void BuildDebugWeaponAugs(RectTransform p, SideWeapon w, Action close)
    {
        string viewName = "WeaponAugView_" + w;
        float keepY = SavedScrollY(p, viewName);   // 같은 무기를 다시 그릴 땐 스크롤 위치 그대로
        for(int i = p.childCount - 1; i >= 0; i--) Destroy(p.GetChild(i).gameObject);
        Action refresh = () => BuildDebugWeaponAugs(p, w, close);
        int fused = mgr.FusionOf(w);
        Title(p, $"[개발] {AugmentDefs.WeaponName(w)} 증강", 780, 50, 940);
        string state = (mgr.IsEquipped(w) ? "<color=#7CFF8A>출격 중</color>" : "<color=#FF8A8A>출격 안 함 (증강이 동작 안 해요)</color>")
                     + (fused >= 0 ? $" · <color=#E9A6FF>합체: {AugmentDefs.Fusions[fused].name}</color>" : "");
        Label(p, state, new Vector2(900, 50), new Vector2(0, 700), 28, Color.white);

        // 합체했으면 실제 뽑기처럼 합체 전 무기 전용 증강·다른 합체는 빼고 지금 합체 것만 (2026-11-21 사용자: 합체했으면 합체 전 증강은 안 나와야)
        var defs = fused >= 0 ? new List<AugmentDef>() : new List<AugmentDef>(AugmentDefs.WeaponAugs(w));
        for(int f = 0; f < AugmentDefs.Fusions.Length; f++) {
            var fd = AugmentDefs.Fusions[f];
            if(fused >= 0 && f != fused) continue;
            if(fd.a != w && fd.b != w) continue;   // 양쪽 합체: B 무기 창에도 카드·합체 전용 증강·종결을 보여 줘요
            defs.Add(AugmentDefs.Get(fd.card));
            foreach(var id in fd.augs) defs.Add(AugmentDefs.Get(id));
            foreach(var id in AugmentDefs.FusionSilvers[f]) defs.Add(AugmentDefs.Get(id));   // 합체 실버 증강 (2026-11-24)
            defs.Add(AugmentDefs.Get(AugmentDefs.UltIds[f]));   // 종결 증강
        }

        const float rowH = 150f, step = 158f, viewH = 1180f;
        float contentH = Mathf.Max(viewH, defs.Count * step);
        var viewport = Rect(viewName, p, new Vector2(940, viewH), new Vector2(0, 655 - viewH / 2));
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, new Vector2(920, contentH), new Vector2(0, (viewH - contentH) / 2));
        RestoreScrollY(content, keepY, viewH, contentH);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        for(int i = 0; i < defs.Count; i++) {
            var def = defs[i];
            int lv = mgr.Level(def.id);
            bool max = lv >= def.maxLevel;
            bool waiting = def.fusion >= 0 && !def.fusionCard && !mgr.FusionOwned(def.fusion);   // 합체 전용인데 아직 합체 안 함
            bool ultLocked = def.ultimate && !(mgr.FusionOwned(def.fusion) && mgr.AllFusionAugs(def.fusion));   // 종결: 합체 전용 3개를 모은 뒤 뽑기에 나와요
            float y = contentH / 2 - rowH / 2 - i * step;
            var row = Row(content, new Vector2(900, rowH), new Vector2(0, y), "Aug");
            IconTile(row.transform, def.icon, 96, new Vector2(-386, 0), AugmentDefs.GradeColor(def.grade));
            string lvText = lv > 0 ? $"<color=#FFD54A>★{lv}/{def.maxLevel}</color>" : $"<color=#6B6F8A>없음</color>";
            string kind = def.fusionCard ? "<color=#E9A6FF>[합체 카드]</color> " : def.ultimate ? "<color=#FFD54A>[종결]</color> " + (ultLocked ? "<color=#6B6F8A>(전용 3개 뒤)</color> " : "") : waiting ? "<color=#6B6F8A>(합체 후 동작)</color> " : "";
            var name = Label(row.transform, $"{kind}{def.name}  <size=75%>{AugmentDefs.GradeName(def.grade)}</size>  {lvText}", new Vector2(470, 50), new Vector2(-60, 42), 28, Color.white, FontStyles.Bold);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            var desc = Label(row.transform, def.describe(Mathf.Max(1, lv)), new Vector2(470, 84), new Vector2(-60, -24), 22, TextDim);
            desc.alignment = TextAlignmentOptions.MidlineLeft;
            var b1 = Btn(row.transform, def.fusionCard ? "합체" : "+1", new Vector2(120, 110), new Vector2(255, 0), max ? BGray : BGreen, () => { mgr.DebugAddAugment(def, false); refresh(); }, 32);
            b1.interactable = !max;
            if(def.ultimate) {
                // 종결 증강: 두 번째 버튼은 "지금 발동" (게이지 기다리지 않고 바로 보기)
                int uf = def.fusion;
                var bu = Btn(row.transform, "발동", new Vector2(120, 110), new Vector2(385, 0), lv > 0 ? BYellow : BGray, () => { CloseAllModals(); mgr.DebugTriggerUltimate(uf); }, 30);
                bu.interactable = lv > 0;
            } else {
                var b2 = Btn(row.transform, "최대", new Vector2(120, 110), new Vector2(385, 0), max ? BGray : BPurple, () => { mgr.DebugAddAugment(def, true); refresh(); }, 30);
                b2.interactable = !max && !def.fusionCard;
            }
        }

        Btn(p, mgr.IsEquipped(w) ? "출격 끄기" : "출격 켜기", new Vector2(290, 110), new Vector2(-310, -720), mgr.IsEquipped(w) ? BRed : BGreen, () => { mgr.DebugToggleWeapon(w); refresh(); }, 32);
        Btn(p, "전용 3개 최대", new Vector2(290, 110), new Vector2(0, -720), BPurple, () => {
            foreach(var d in AugmentDefs.WeaponAugs(w)) mgr.DebugAddAugment(d, true);
            refresh();
        }, 30);
        Btn(p, "닫기", new Vector2(290, 110), new Vector2(310, -720), BGray, () => close(), 36);
    }

    public void ShowDebugAugments()
    {
        if(!EnsureBuilt() || mgr == null || !mgr.Loaded) return;
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(980, 1820), out Action close);
        BuildDebugAugments(p, () => { close(); mgr.ResumeFromUI(); });
    }

    // 스크롤 위치 기억 (2026-11-20): 창을 다시 그리기 전에 목록의 위치를 읽고, 새 목록에 되돌려요. 없으면 NaN (맨 위)
    static float SavedScrollY(RectTransform p, string viewName)
    {
        var v = p.Find(viewName);
        var c = v != null ? v.Find("Content") as RectTransform : null;
        return c != null ? c.anchoredPosition.y : float.NaN;
    }

    static void RestoreScrollY(RectTransform content, float y, float viewH, float contentH)
    {
        if(float.IsNaN(y)) return;
        float top = (viewH - contentH) / 2f, bottom = (contentH - viewH) / 2f;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, top, Mathf.Max(top, bottom)));
    }

    void BuildDebugAugments(RectTransform p, Action close)
    {
        string viewName = "DebugView_" + debugTag;
        float keepY = SavedScrollY(p, viewName);   // 같은 계열을 다시 그릴 땐 스크롤 위치 그대로
        for(int i = p.childCount - 1; i >= 0; i--) Destroy(p.GetChild(i).gameObject);
        Title(p, "[개발] 증강 테스트", 780, 50, 940);

        // 계열 버튼 (6 + 5)
        for(int i = 0; i < DebugTags.Length; i++) {
            var t = DebugTags[i];
            int row = i / 6, col = i % 6;
            int perRow = row == 0 ? 6 : DebugTags.Length - 6;
            float x = (col - (perRow - 1) / 2f) * 152f;
            string label = AugmentDefs.IsMain(t) ? $"{AugmentDefs.TagName(t)} {mgr.TagCount(t)}" : AugmentDefs.TagName(t);
            var b = Btn(p, label, new Vector2(144, 76), new Vector2(x, 690 - row * 86), t == debugTag ? BYellow : BBlue, () => { debugTag = t; BuildDebugAugments(p, close); }, 26);
        }

        // 고른 계열: 세트 단계 + 확인할 것
        string setLine = "";
        if(AugmentDefs.IsMain(debugTag)) {
            int c = mgr.TagCount(debugTag);
            var sb = new System.Text.StringBuilder();
            foreach(int tier in AugmentDefs.SetTiers)
                sb.Append(c >= tier ? "<color=#FFD54A>●</color>" : "<color=#6B6F8A>○</color>").Append(tier).Append(' ');
            setLine = $"세트 {sb}\n";
        }
        var info = Row(p, new Vector2(900, 116), new Vector2(0, 500), "Info");
        Label(info.transform, $"{setLine}<size=85%>{DebugHint(debugTag)}</size>", new Vector2(860, 104), Vector2.zero, 28, Color.white);

        // 증강 목록 (스크롤)
        var defs = AugmentDefs.All.FindAll(d => d.tag == debugTag);
        if(debugTag == AugTag.Weapon) {
            // 출격한 무기 것을 위로 (순서는 그대로)
            var on = defs.FindAll(d => mgr.IsEquipped(d.requiredWeapon));
            on.AddRange(defs.FindAll(d => !mgr.IsEquipped(d.requiredWeapon)));
            defs = on;
        }
        const float rowH = 150f, step = 158f, viewH = 960f;
        float contentH = Mathf.Max(viewH, defs.Count * step);
        var viewport = Rect(viewName, p, new Vector2(940, viewH), new Vector2(0, 432 - viewH / 2));
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, new Vector2(920, contentH), new Vector2(0, (viewH - contentH) / 2));
        RestoreScrollY(content, keepY, viewH, contentH);
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        for(int i = 0; i < defs.Count; i++) {
            var def = defs[i];
            int lv = mgr.Level(def.id);
            bool max = lv >= def.maxLevel;
            float y = contentH / 2 - rowH / 2 - i * step;
            var row = Row(content, new Vector2(900, rowH), new Vector2(0, y), "Aug");
            IconTile(row.transform, def.icon, 96, new Vector2(-386, 0), AugmentDefs.GradeColor(def.grade));
            string lvText = lv > 0 ? $"<color=#FFD54A>★{lv}/{def.maxLevel}</color>" : $"<color=#6B6F8A>없음</color>";
            var name = Label(row.transform, $"{def.name}  <size=75%>{AugmentDefs.GradeName(def.grade)}</size>  {lvText}", new Vector2(470, 50), new Vector2(-60, 42), 28, Color.white, FontStyles.Bold);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            var desc = Label(row.transform, def.describe(Mathf.Max(1, lv)), new Vector2(470, 84), new Vector2(-60, -24), 22, TextDim);
            desc.alignment = TextAlignmentOptions.MidlineLeft;
            var b1 = Btn(row.transform, "+1", new Vector2(120, 110), new Vector2(255, 0), max ? BGray : BGreen, () => { mgr.DebugAddAugment(def, false); BuildDebugAugments(p, close); }, 34);
            b1.interactable = !max;
            var b2 = Btn(row.transform, "최대", new Vector2(120, 110), new Vector2(385, 0), max ? BGray : BPurple, () => { mgr.DebugAddAugment(def, true); BuildDebugAugments(p, close); }, 30);
            b2.interactable = !max;
        }

        // 아래 버튼
        Btn(p, "이 계열 전부", new Vector2(290, 120), new Vector2(-310, -640), BGreen, () => {
            foreach(var d in defs) mgr.DebugAddAugment(d, false);
            BuildDebugAugments(p, close);
        }, 32);
        Btn(p, "모두 지우기", new Vector2(290, 120), new Vector2(0, -640), BRed, () => { mgr.DebugClearAugments(); BuildDebugAugments(p, close); }, 32);
        Btn(p, "닫기", new Vector2(290, 120), new Vector2(310, -640), BGray, () => close(), 36);
        // (2026-11-25) 4칸으로: 마지막에 "실제 한 판" (테스트 설정을 다 끄고 1스테이지부터, End 키)
        Btn(p, "무기 전부\n열기", new Vector2(215, 96), new Vector2(-345, -750), BYellow, () => { mgr.DebugUnlockAllWeapons(); BuildDebugAugments(p, close); }, 26);
        Btn(p, "무기 고르기\n(합체 포함)", new Vector2(215, 96), new Vector2(-115, -750), BPurple, () => ShowDebugWeapons(() => BuildDebugAugments(p, close)), 24);
        Btn(p, "연습장\n(P 키)", new Vector2(215, 96), new Vector2(115, -750), BGreen, () => { close(); ShowDebugArena(); }, 26);
        Btn(p, "실제 한 판\n(End 키)", new Vector2(215, 96), new Vector2(345, -750), BRed, () => { close(); mgr.DebugRealRun(); }, 24);
        // 아래 줄: 작은 버튼 5개 (글자는 버튼에 맞게 자동으로 줄어요)
        var small = new Vector2(176, 76);
        Btn(p, AugmentManager.DebugTestMode ? "테스트\n켜짐" : "테스트\n꺼짐", small, new Vector2(-370, -840), AugmentManager.DebugTestMode ? BGreen : BGray, () => { mgr.DebugToggleTestMode(); BuildDebugAugments(p, close); }, 26);
        Btn(p, AugmentManager.DebugManyEnemies ? "적 3배\n켜짐" : "적 3배\n꺼짐", small, new Vector2(-185, -840), AugmentManager.DebugManyEnemies ? BGreen : BGray, () => { AugmentManager.DebugManyEnemies = !AugmentManager.DebugManyEnemies; BuildDebugAugments(p, close); }, 26);
        Btn(p, "보스\n소환", small, new Vector2(0, -840), BRed, () => mgr.DebugSpawnBoss(), 26);
        Btn(p, $"속도\nx{GameManager.DisplaySpeed:0.#}", small, new Vector2(185, -840), BYellow, () => { DevTools.CycleSpeed(); BuildDebugAugments(p, close); }, 26);
        Btn(p, DevTools.DpsOn ? "DPS 기록\n켜짐" : "DPS 기록\n꺼짐", small, new Vector2(370, -840), DevTools.DpsOn ? BGreen : BGray, () => { DevTools.ToggleDps(); BuildDebugAugments(p, close); }, 26);
        Label(p, "F7 게임 속도 · F12 스크린샷 · F2 +20스테이지 · Home 1스테이지 · F9 골드 +10억 · Insert 부품 +100 · F5 원정 준비", new Vector2(900, 40), new Vector2(0, -896), 24, TextDim);
    }
    #endregion

    #region 증강 목록
    public void ShowAugmentList()
    {
        if(!EnsureBuilt() || mgr == null || !mgr.Loaded) return;
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(980, 1680), out Action close);
        bool planet = mgr.InPlanetRun;
        Title(p, planet ? "이 행성의 증강" : "이번 원정의 증강", 760, 54, 940);

        string bless = planet ? $"행성 Lv.{mgr.PlanetLevel}" : mgr.Blessing >= 0 ? AugmentDefs.GodName((AugTag)mgr.Blessing) : "없음";
        string weapons = $"{AugmentDefs.WeaponName(mgr.ShownWeapon(mgr.LoadoutLeft))} / 기본총 / {AugmentDefs.WeaponName(mgr.ShownWeapon(mgr.LoadoutRight))}";
        if(mgr.ExtraWeapon != SideWeapon.None) weapons += $" + {AugmentDefs.WeaponName(mgr.ShownWeapon(mgr.ExtraWeapon))}";
        Label(p, planet ? $"{bless}\n무기: {weapons}" : $"축복: {bless}\n무기: {weapons}", new Vector2(880, 96), new Vector2(0, 670), 30, TextDim);

        // 스크롤 영역: 세트 → 조합 계열 → 가진 증강 (설명 칸 위까지)
        var ids = mgr.OwnedOrder;
        var ownedTags = new List<AugTag>();
        foreach(var t in AugmentDefs.MainTags) if(mgr.TagCount(t) > 0) ownedTags.Add(t);
        ownedTags.Sort((a, b) => mgr.TagCount(b).CompareTo(mgr.TagCount(a)));
        var combos = new List<int>();
        for(int i = 0; i < AugmentDefs.Combos.Length; i++) {
            var c = AugmentDefs.Combos[i];
            if(mgr.TagCount(c.a) > 0 && mgr.TagCount(c.b) > 0) combos.Add(i);
        }
        combos.Sort((a, b) => mgr.ComboTier(b).CompareTo(mgr.ComboTier(a)));
        const int cols = 5;
        int gridRows = (ids.Count + cols - 1) / cols;
        const float headH = 64f, tagH = 168f, comboH = 104f, cellStep = 178f, emptyH = 80f;
        float contentH = headH + Mathf.Max(1, ownedTags.Count) * (ownedTags.Count > 0 ? tagH : emptyH)
                       + headH + Mathf.Max(1, combos.Count) * (combos.Count > 0 ? comboH : emptyH)
                       + headH + Mathf.Max(1, gridRows) * (gridRows > 0 ? cellStep : emptyH) + 20f;

        const float viewH = 1080f;
        var viewport = Rect("ListView", p, new Vector2(940, viewH), new Vector2(0, 620 - viewH / 2));
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, new Vector2(920, contentH), new Vector2(0, (viewH - contentH) / 2));
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        float cursor = 0f;   // 위에서부터 내려가며 놓아요
        Func<float, float> place = h => { float y = contentH / 2 - cursor - h / 2; cursor += h; return y; };
        string on = "<color=#FFD54A>●</color> ", off = "<color=#6B6F8A>○</color> ";

        Label(content, "계열 세트 (2 / 4 / 6 / 8)", new Vector2(880, 52), new Vector2(0, place(headH)), 34, Color.white, FontStyles.Bold);
        if(ownedTags.Count == 0)
            Label(content, "같은 계열 증강을 2개 모으면 세트 효과가 켜져요", new Vector2(880, 60), new Vector2(0, place(emptyH)), 28, TextDim);
        foreach(var tag in ownedTags) {
            int c = mgr.TagCount(tag);
            var sb = new System.Text.StringBuilder();
            foreach(int tier in AugmentDefs.SetTiers)
                sb.Append(c >= tier ? on : off).Append(tier).Append(": ").Append(AugmentDefs.SynergyText(tag, tier)).Append(tier < 8 ? "\n" : "");
            var row = Row(content, new Vector2(900, tagH - 10), new Vector2(0, place(tagH)), "Set");
            IconTile(row.transform, AugmentDefs.TagIcon(tag), 72, new Vector2(-394, 20), AugmentDefs.TagColor(tag));
            Label(row.transform, $"{AugmentDefs.TagName(tag)} {c}", new Vector2(130, 50), new Vector2(-394, -44), 30, AugmentDefs.TagColor(tag), FontStyles.Bold);
            var setText = Label(row.transform, sb.ToString(), new Vector2(720, 148), new Vector2(80, 0), 25, TextDim);
            setText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        Label(content, "조합 계열", new Vector2(880, 52), new Vector2(0, place(headH)), 34, Color.white, FontStyles.Bold);
        if(combos.Count == 0)
            Label(content, "서로 다른 두 계열을 각각 2개씩 모으면 켜져요", new Vector2(880, 60), new Vector2(0, place(emptyH)), 28, TextDim);
        foreach(int i in combos) {
            var c = AugmentDefs.Combos[i];
            int tier = mgr.ComboTier(i);
            int need = tier == 0 ? 2 : 4;
            string state = tier > 0 ? on + $"<color=#FFFFFF>{c.god} · {c.name}{(tier == 2 ? " 2단계" : "")}</color>" : off + $"{c.god} · {c.name}";
            string effect = tier == 2 ? c.tier2 : c.tier1;
            string req = tier == 2 ? "" : $"  <size=80%>({AugmentDefs.TagName(c.a)} {mgr.TagCount(c.a)}/{need} · {AugmentDefs.TagName(c.b)} {mgr.TagCount(c.b)}/{need})</size>";
            var row = Row(content, new Vector2(900, comboH - 8), new Vector2(0, place(comboH)), "Combo");
            Label(row.transform, $"{state}{req}\n{effect}", new Vector2(860, 88), Vector2.zero, 26, tier > 0 ? Color.white : TextDim);
        }

        Label(content, $"가진 증강 ({ids.Count})", new Vector2(880, 52), new Vector2(0, place(headH)), 34, Color.white, FontStyles.Bold);
        var descBox = Row(p, new Vector2(900, 150), new Vector2(0, -560), "Desc");
        var desc = Label(descBox.transform, ids.Count == 0 ? "아직 증강이 없어요.\n스테이지를 넘으면 하나씩 얻어요." : "아이콘을 누르면 설명이 보여요", new Vector2(850, 130), Vector2.zero, 30, TextDim);
        if(ids.Count == 0) place(emptyH);
        float gridTop = cursor;
        for(int i = 0; i < ids.Count; i++) {
            var def = AugmentDefs.Get(ids[i]);
            if(def == null) continue;
            int lv = mgr.Level(def.id);
            int r = i / cols, col = i % cols;
            float y = contentH / 2 - gridTop - cellStep / 2 - r * cellStep;
            var cell = ColorCard(content, new Vector2(160, 168), new Vector2(-344 + col * 172, y), AugmentDefs.GradeColor(def.grade), "Aug");
            var face = Row(cell.transform, new Vector2(140, 148), new Vector2(0, 2), "Face");
            Icon(face.transform, def.icon, new Vector2(88, 88), new Vector2(0, 22));
            Label(face.transform, $"{def.name}\n<color=#FFD54A>★{lv}</color>", new Vector2(132, 54), new Vector2(0, -44), 20, Color.white, FontStyles.Bold);
            var b = cell.gameObject.AddComponent<Button>();
            string tagLine = def.tag == AugTag.Weapon ? $"무기 전용 · {AugmentDefs.WeaponName(def.requiredWeapon)}" : AugmentDefs.TagName(def.tag);
            b.onClick.AddListener(() => desc.text = $"<color=#FFFFFF><b>{def.name}</b></color> <color=#FFD54A>★{lv}</color> <size=80%>({tagLine})</size>\n{def.describe(lv)}");
        }

        string stack = $"처치 {mgr.ExpeditionKills:N0}";
        if(mgr.CurseStackTotal > 0) stack += $"   ·   누적 피해 +{mgr.CurseStackTotal * 100:0.#}%";
        Label(p, stack, new Vector2(860, 48), new Vector2(0, -665), 30, AugmentDefs.TagColor(AugTag.Curse), FontStyles.Bold);
        if(!planet) {
            Btn(p, "닫기", new Vector2(340, 120), new Vector2(0, -750), BGray, () => { close(); mgr.ResumeFromUI(); }, 42);
            return;
        }
        // 행성 게임 중: 보기만 (2026-11-25 사용자: 증강 초기화는 게임 중엔 말고, 게임 시작 전 행성 창의 '증강'에서)
        Btn(p, "닫기", new Vector2(340, 120), new Vector2(0, -750), BGray, () => { close(); mgr.ResumeFromUI(); }, 42);
    }
    #endregion

    #region 환생
    public void ShowRebirth()
    {
        if(!EnsureBuilt() || mgr == null || !mgr.Loaded) return;
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(980, 1600), out Action close);
        BuildRebirth(p, close);
    }

    void BuildRebirth(RectTransform p, Action close)
    {
        for(int i = p.childCount - 1; i >= 0; i--) Destroy(p.GetChild(i).gameObject);
        int stage = InfiniteStage.Instance != null ? InfiniteStage.Instance.CurrentStage : 1;
        Title(p, "환생", 720, 64, 940);
        Label(p, $"현재 {stage:N0}스테이지   ·   최고 {Mathf.Max(mgr.BestStage, stage):N0}스테이지", new Vector2(860, 48), new Vector2(0, 648), 32, TextDim);
        SkinIcon(p, "ItemIcon_Star_Purple", new Vector2(64, 64), new Vector2(-230, 578));
        Label(p, $"별의 먼지 {mgr.Stardust:N0}개", new Vector2(420, 60), new Vector2(30, 578), 42, TextGold, FontStyles.Bold);

        var info = Row(p, new Vector2(900, 210), new Vector2(0, 420), "Info");
        if(mgr.CanRebirth)
            Label(info.transform, $"지금 환생하면 별의 먼지 <color=#FFD54A>{mgr.RebirthGain:N0}개</color>를 받아요.\n증강과 스테이지는 초기화되고,\n무기·장비·골드는 그대로 남아요.", new Vector2(840, 180), Vector2.zero, 32, Color.white);
        else
            Label(info.transform, $"{mgr.RebirthThreshold:N0}스테이지부터 환생할 수 있어요\n(지금 {stage:N0}스테이지)\n환생하면 별의 먼지로 영구 강화를 해요.", new Vector2(840, 180), Vector2.zero, 32, TextDim);

        Title(p, "환생 트리", 275, 42, 900);
        // 갈래별 순서로 보여 줘요 (뽑기 → 출발 → 원정 → 보상). 칸이 많아서 스크롤
        string[] branchIcons = { "bless_split", "bless_chain", "bless_curse", "bless_split" };
        var order = new List<int>();
        for(int br = 0; br < AugmentManager.BranchNames.Length; br++)
            for(int n = 0; n < AugmentManager.TreeNames.Length; n++)
                if(AugmentManager.TreeBranch[n] == br) order.Add(n);

        // 스크롤 영역: 제목 아래(230)부터 버튼 위(-585)까지
        const float viewH = 815f, rowH = 150f, step = 162f;
        float contentH = order.Count * step;
        var viewport = Rect("TreeView", p, new Vector2(920, viewH), new Vector2(0, 230 - viewH / 2));
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.001f);   // 드래그를 받기 위한 투명 배경
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, new Vector2(920, contentH), new Vector2(0, (viewH - contentH) / 2));
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        for(int i = 0; i < order.Count; i++) {
            int node = order[i];
            float y = contentH / 2 - rowH / 2 - i * step;
            var row = Row(content, new Vector2(900, rowH), new Vector2(0, y), "Node");
            int lv = mgr.TreeLevel(node);
            bool max = lv >= AugmentManager.TreeMaxOf(node);
            bool star = node == AugmentManager.StarPowerNode;
            bool open = mgr.TreeUnlocked(node);
            int br = AugmentManager.TreeBranch[node];
            string lvText = star && AugmentManager.TreeMaxOf(node) >= 99 ? $"Lv.{lv}" : $"Lv.{lv}/{AugmentManager.TreeMaxOf(node)}";
            IconTile(row.transform, branchIcons[br], 96, new Vector2(-366, 0), !open ? new Color(0.35f, 0.35f, 0.4f) : max || star ? TextGold : new Color(0.55f, 0.6f, 0.9f));
            Label(row.transform, $"<color=#9FB4FF>[{AugmentManager.BranchNames[br]}]</color> {AugmentManager.TreeNames[node]}  <color=#FFD54A>{lvText}</color>", new Vector2(450, 54), new Vector2(-55, 34), 30, Color.white, FontStyles.Bold);
            string desc = open ? mgr.TreeDesc(node) : $"먼저 '{AugmentManager.TreeNames[AugmentManager.TreeRequires[node]]}'를 찍어야 열려요";
            Label(row.transform, desc, new Vector2(450, 70), new Vector2(-55, -32), 23, TextDim);
            string label = !open ? "잠김" : max ? "최대" : $"강화\n<size=80%>먼지 {mgr.TreeCost(node):N0}</size>";
            var b = Btn(row.transform, label, new Vector2(220, 124), new Vector2(320, 0), !open || max ? BGray : BBlue, () => {
                if(mgr.UpgradeTree(node)) BuildRebirth(p, close);
                else Toast("별의 먼지가 부족해요", Color.white);
            }, 30);
            b.interactable = open && !max;
        }

        var rb = Btn(p, "환생하기", new Vector2(420, 140), new Vector2(-215, -665), mgr.CanRebirth ? BYellow : BGray, () => {
            var c = OpenModal(new Vector2(820, 600), out Action closeConfirm);
            Label(c, "정말 환생할까요?", new Vector2(720, 90), new Vector2(0, 185), 50, Color.white, FontStyles.Bold);
            Label(c, $"별의 먼지 <color=#FFD54A>{mgr.RebirthGain:N0}개</color>를 받고\n1스테이지부터 다시 출발해요.", new Vector2(720, 140), new Vector2(0, 40), 34, TextDim);
            Btn(c, "취소", new Vector2(290, 130), new Vector2(-170, -175), BGray, () => closeConfirm(), 42);
            Btn(c, "환생", new Vector2(290, 130), new Vector2(170, -175), BYellow, () => {
                closeConfirm();
                close();
                mgr.ResumeFromUI();
                mgr.DoRebirth();
            }, 42);
        }, 46);
        rb.interactable = mgr.CanRebirth;
        Btn(p, "닫기", new Vector2(340, 140), new Vector2(235, -665), BGray, () => { close(); mgr.ResumeFromUI(); }, 42);
    }
    #endregion
}
