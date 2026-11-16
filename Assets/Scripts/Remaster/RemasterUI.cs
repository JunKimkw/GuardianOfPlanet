using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 리마스터 UI: 씬을 건드리지 않고 코드로 만드는 화면들 (기준 해상도 1080x2048, 가로폭 맞춤 = 기존 UI와 동일)
// 겉모습은 기존 UI와 같은 Layer Lab GUI Pro-SuperCasual 이미지를 써요 (Resources/RemasterUI 에 복사본).
// 모든 글자는 가운데 정렬 + 자동 크기(넘치지 않게)로 만들어요.
public class RemasterUI : MonoBehaviour
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
        else { img.sprite = rounded; img.type = Image.Type.Sliced; img.color = new Color(1, 1, 1, 0.2f); }
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
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
    TextMeshProUGUI hudStardust, hudStack;
    readonly TextMeshProUGUI[] hudTag = new TextMeshProUGUI[3];
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
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        string[] icons = { "bless_chain", "bless_curse", "bless_split" };
        for(int i = 0; i < 3; i++) {
            float y = 110 - i * 58;
            IconTile(hud, icons[i], 50, new Vector2(-46, y), AugmentDefs.TagColor(tags[i]));
            hudTag[i] = Label(hud, "", new Vector2(90, 46), new Vector2(30, y), 32, Color.white, FontStyles.Bold);
        }
        hudStack = Label(hud, "", new Vector2(164, 56), new Vector2(0, -60), 22, TextDim);
        Btn(hud, "증강", new Vector2(160, 76), new Vector2(0, -132), BBlue, ShowAugmentList, 30);
        hudRebirthBtn = Btn(hud, "환생", new Vector2(160, 76), new Vector2(0, -210), BGray, ShowRebirth, 30);
    }

    float hudTimer;
    void Update()
    {
        hudTimer -= Time.unscaledDeltaTime;
        if(hudTimer > 0) return;
        hudTimer = 0.25f;
        if(mgr == null || !mgr.Loaded) return;
        if(!EnsureBuilt()) return;
        bool show = InfiniteStage.Instance != null && InfiniteStage.Instance.IsRunning
                    && (StageManager.instance == null || !StageManager.instance.isInStage)
                    && (StartScreenScript.Instance == null || !StartScreenScript.Instance.gameObject.activeInHierarchy);
        if(hud.gameObject.activeSelf != show) hud.gameObject.SetActive(show);
        if(show) RefreshHUD();
    }

    public void RefreshHUD()
    {
        if(hud == null || mgr == null || !mgr.Loaded) return;
        hudStardust.text = $"{mgr.Stardust:N0}";
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        for(int i = 0; i < 3; i++) hudTag[i].text = mgr.TagCount(tags[i]).ToString();
        float stack = mgr.CurseStackTotal;
        hudStack.text = stack > 0 ? $"누적 피해\n+{stack * 100:0.#}%" : $"처치 {mgr.ExpeditionKills:N0}";
        bool can = mgr.CanRebirth;
        if(can != hudRebirthReady) {
            hudRebirthReady = can;
            SetBtnStyle(hudRebirthBtn, can ? BYellow : BGray);
        }
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

    public void ShowPull(AugmentDef def, int newLevel, string rerollLabel, Action onAccept, Action onReroll)
    {
        if(!EnsureBuilt()) { onAccept?.Invoke(); return; }
        if(pullPanel == null) pullPanel = OpenModal(new Vector2(880, 1300), out pullClose);
        for(int i = pullPanel.childCount - 1; i >= 0; i--) Destroy(pullPanel.GetChild(i).gameObject);
        if(pullRoutine != null) StopCoroutine(pullRoutine);

        bool done = false;
        Action accept = () => {
            if(done) return;
            done = true;
            if(pullRoutine != null) StopCoroutine(pullRoutine);
            pullClose?.Invoke();
            pullPanel = null;
            onAccept?.Invoke();
        };

        Title(pullPanel, "증강 획득", 570, 58);
        Label(pullPanel, "스테이지를 넘으면 증강을 얻어요 (갈수록 드물게)", new Vector2(760, 46), new Vector2(0, 505), 30, TextDim);

        // 카드: 등급 색 테두리 + 남색 안쪽
        var frame = ColorCard(pullPanel, new Vector2(620, 820), new Vector2(0, 40), AugmentDefs.GradeColor(AugGrade.Silver), "CardFrame");
        var inner = Row(frame.transform, new Vector2(580, 780), new Vector2(0, 4), "CardInner");
        var skip = frame.gameObject.AddComponent<Button>();
        var gradeLabel = Label(inner.transform, AugmentDefs.GradeName(AugGrade.Silver), new Vector2(520, 60), new Vector2(0, 336), 44, AugmentDefs.GradeColor(AugGrade.Silver), FontStyles.Bold);

        var content = Rect("Content", inner.transform, new Vector2(580, 700), new Vector2(0, -30));
        var cg = content.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        var tagChip = ColorCard(content, new Vector2(360, 64), new Vector2(0, 262), AugmentDefs.TagColor(def.tag), "TagChip");
        Label(tagChip.transform, $"{AugmentDefs.TagName(def.tag)} · {AugmentDefs.GodName(def.tag)}", new Vector2(320, 48), new Vector2(0, 2), 30, Color.white, FontStyles.Bold);
        IconTile(content, def.icon, 250, new Vector2(0, 80), AugmentDefs.GradeColor(def.grade));
        Label(content, def.name, new Vector2(540, 76), new Vector2(0, -100), 56, Color.white, FontStyles.Bold);
        Label(content, newLevel <= 1 ? "NEW" : $"★{newLevel}  (최대 ★{def.maxLevel})", new Vector2(520, 48), new Vector2(0, -160), 34, newLevel <= 1 ? new Color(0.5f, 1f, 0.6f) : TextGold, FontStyles.Bold);
        Label(content, def.describe(newLevel), new Vector2(520, 160), new Vector2(0, -270), 36, TextDim);

        var countdown = Label(pullPanel, "", new Vector2(760, 44), new Vector2(0, -585), 28, TextDim);
        Btn(pullPanel, rerollLabel, new Vector2(400, 130), new Vector2(-200, -480), BPurple, () => { if(!done) onReroll?.Invoke(); }, 34);
        Btn(pullPanel, "받기", new Vector2(320, 130), new Vector2(210, -480), BGreen, accept, 48);

        bool skipped = false;
        skip.onClick.AddListener(() => skipped = true);
        pullRoutine = StartCoroutine(PullReveal(def.grade, frame, gradeLabel, cg, countdown, () => skipped, accept));
    }

    IEnumerator PullReveal(AugGrade grade, Image frame, TextMeshProUGUI gradeLabel, CanvasGroup content, TextMeshProUGUI countdown, Func<bool> skipped, Action accept)
    {
        // 실버 → (골드) → (프리즘) 승급 연출
        float wait = 0.35f;
        for(int g = 1; g <= (int)grade; g++) {
            float t = 0;
            while(t < wait && !skipped()) { t += Time.unscaledDeltaTime; yield return null; }
            var col = AugmentDefs.GradeColor((AugGrade)g);
            frame.color = Color.white;
            gradeLabel.text = AugmentDefs.GradeName((AugGrade)g) + (g == 2 ? "!!" : "!");
            gradeLabel.color = col;
            float f = 0;
            while(f < 0.15f) { f += Time.unscaledDeltaTime; frame.color = Color.Lerp(Color.white, col, f / 0.15f); yield return null; }
            frame.color = col;
            frame.rectTransform.localScale = Vector3.one * 1.04f;
            yield return null;
            frame.rectTransform.localScale = Vector3.one;
        }
        float a = 0;
        while(a < 1f) { a += Time.unscaledDeltaTime * 5f; content.alpha = a; yield return null; }
        content.alpha = 1;

        // 자동으로 받기 (방치 중에도 진행되도록)
        float left = 8f;
        while(left > 0) {
            if(grade == AugGrade.Prism) frame.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.35f, 1f), 0.45f, 1f);
            countdown.text = $"{Mathf.CeilToInt(left)}초 뒤 자동으로 받아요";
            left -= Time.unscaledDeltaTime;
            yield return null;
        }
        accept();
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
        Label(p, "출격 무기와 축복을 고르고 출발하세요", new Vector2(860, 48), new Vector2(0, 610), 32, TextDim);

        // ---- 출격 무기
        var weaponBox = Row(p, new Vector2(900, 420), new Vector2(0, 370), "WeaponBox");
        Label(weaponBox.transform, "출격 무기", new Vector2(840, 56), new Vector2(0, 160), 40, Color.white, FontStyles.Bold);
        var owned = mgr.OwnedWeapons();
        Image leftCard = null, rightCard = null;
        TextMeshProUGUI leftText = null, rightText = null;
        Action refreshSlots = null;
        Func<SideWeapon, SideWeapon, SideWeapon> next = (cur, other) => {
            var opts = new List<SideWeapon> { SideWeapon.None };
            foreach(var w in owned) if(w != other) opts.Add(w);
            int idx = opts.IndexOf(cur);
            return opts[(idx + 1) % opts.Count];
        };
        leftCard = ColorCard(weaponBox.transform, new Vector2(260, 210), new Vector2(-280, 20), LockedTint, "Left");
        leftText = Label(leftCard.transform, "", new Vector2(220, 170), new Vector2(0, 2), 34, Color.white, FontStyles.Bold);
        leftCard.gameObject.AddComponent<Button>().onClick.AddListener(() => { left = next(left, right); refreshSlots(); });
        var center = ColorCard(weaponBox.transform, new Vector2(240, 210), new Vector2(0, 20), new Color(0.55f, 0.6f, 0.75f), "MainGun");
        Label(center.transform, "기본총\n(고정)", new Vector2(200, 170), new Vector2(0, 2), 36, Color.white, FontStyles.Bold);
        rightCard = ColorCard(weaponBox.transform, new Vector2(260, 210), new Vector2(280, 20), LockedTint, "Right");
        rightText = Label(rightCard.transform, "", new Vector2(220, 170), new Vector2(0, 2), 34, Color.white, FontStyles.Bold);
        rightCard.gameObject.AddComponent<Button>().onClick.AddListener(() => { right = next(right, left); refreshSlots(); });
        Label(weaponBox.transform, owned.Count == 0 ? "무기 상점에서 무기를 사면 여기에 장착할 수 있어요" : "칸을 누를 때마다 다른 무기로 바뀌어요", new Vector2(840, 44), new Vector2(0, -150), 28, TextDim);
        refreshSlots = () => {
            SetSlot(leftCard, leftText, "왼쪽", left);
            SetSlot(rightCard, rightText, "오른쪽", right);
        };
        refreshSlots();

        // ---- 축복
        var blessBox = Row(p, new Vector2(900, 620), new Vector2(0, -170), "BlessBox");
        Label(blessBox.transform, "축복", new Vector2(840, 56), new Vector2(0, 260), 40, Color.white, FontStyles.Bold);
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        string[] icons = { "bless_chain", "bless_curse", "bless_split" };
        var cards = new Image[3];
        Action refreshCards = null;
        bool anyUnlocked = false;
        for(int i = 0; i < 3; i++) {
            int idx = i;
            var tag = tags[i];
            bool unlocked = mgr.BlessingUnlocked(tag);
            anyUnlocked |= unlocked;
            var card = ColorCard(blessBox.transform, new Vector2(270, 430), new Vector2(-285 + i * 285, -10), unlocked ? AugmentDefs.TagColor(tag) : LockedTint, "Blessing");
            cards[i] = card;
            var face = Row(card.transform, new Vector2(240, 400), new Vector2(0, 3), "Face");
            Icon(face.transform, icons[i], new Vector2(130, 130), new Vector2(0, 110));
            Label(face.transform, AugmentDefs.GodName(tag), new Vector2(220, 56), new Vector2(0, 12), 34, unlocked ? AugmentDefs.TagColor(tag) : TextDim, FontStyles.Bold);
            if(unlocked) {
                Label(face.transform, $"{AugmentDefs.TagName(tag)} 증강이\n더 잘 나와요", new Vector2(220, 110), new Vector2(0, -95), 30, TextDim);
                var b = card.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { blessing = blessing == idx ? -1 : idx; refreshCards(); });
            } else {
                SkinIcon(face.transform, "Icon_Lock03", new Vector2(56, 56), new Vector2(0, -50));
                Label(face.transform, $"{AugmentManager.BlessingPlanetName(tag)}\n행성을 클리어하면 열려요", new Vector2(220, 100), new Vector2(0, -130), 24, TextDim);
            }
        }
        Label(blessBox.transform, anyUnlocked ? "다시 누르면 선택을 해제해요" : "4번째 행성(헤파이스토스)을 클리어하면 첫 축복이 열려요", new Vector2(840, 44), new Vector2(0, -262), 26, TextDim);
        refreshCards = () => {
            for(int i = 0; i < 3; i++) {
                if(!mgr.BlessingUnlocked(tags[i])) continue;
                bool on = blessing == i;
                // 고른 카드는 밝고 크게, 나머지는 살짝 어둡게
                cards[i].color = on ? AugmentDefs.TagColor(tags[i]) : AugmentDefs.TagColor(tags[i]) * new Color(0.55f, 0.55f, 0.55f, 1f);
                cards[i].rectTransform.localScale = on ? Vector3.one * 1.06f : Vector3.one;
            }
        };
        refreshCards();

        Btn(p, "출발!", new Vector2(440, 142), new Vector2(0, -620), BGreen, () => {
            mgr.SetLoadout(left, right);
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

    #region 증강 목록
    public void ShowAugmentList()
    {
        if(!EnsureBuilt() || mgr == null || !mgr.Loaded) return;
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(980, 1680), out Action close);
        Title(p, "이번 원정의 증강", 760, 54, 940);

        string bless = mgr.Blessing >= 0 ? AugmentDefs.GodName((AugTag)mgr.Blessing) : "없음";
        string weapons = $"{AugmentDefs.WeaponName(mgr.LoadoutLeft)} / 기본총 / {AugmentDefs.WeaponName(mgr.LoadoutRight)}";
        if(mgr.ExtraWeapon != SideWeapon.None) weapons += $" + {AugmentDefs.WeaponName(mgr.ExtraWeapon)}";
        if(mgr.SupplyWeapon != SideWeapon.None) weapons += $" + {AugmentDefs.WeaponName(mgr.SupplyWeapon)}(보급)";
        Label(p, $"축복: {bless}\n무기: {weapons}", new Vector2(880, 96), new Vector2(0, 670), 30, TextDim);

        // 시너지
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        string[] icons = { "bless_chain", "bless_curse", "bless_split" };
        for(int i = 0; i < 3; i++) {
            var tag = tags[i];
            int c = mgr.TagCount(tag);
            string on = "<color=#FFD54A>●</color> ", off = "<color=#6B6F8A>○</color> ";
            string s2 = (c >= 2 ? on : off) + "2: " + AugmentDefs.SynergyText(tag, 2);
            string s4 = (c >= 4 ? on : off) + "4: " + AugmentDefs.SynergyText(tag, 4);
            var row = Row(p, new Vector2(900, 104), new Vector2(0, 550 - i * 114), "Synergy");
            IconTile(row.transform, icons[i], 72, new Vector2(-394, 0), AugmentDefs.TagColor(tag));
            Label(row.transform, $"{AugmentDefs.TagName(tag)} {c}", new Vector2(150, 70), new Vector2(-260, 0), 34, AugmentDefs.TagColor(tag), FontStyles.Bold);
            Label(row.transform, s2 + "\n" + s4, new Vector2(580, 88), new Vector2(150, 0), 26, TextDim);
        }

        // 보유 증강 (아이콘 격자)
        var ids = mgr.OwnedOrder;
        var descBox = Row(p, new Vector2(900, 150), new Vector2(0, -540), "Desc");
        var desc = Label(descBox.transform, ids.Count == 0 ? "아직 증강이 없어요.\n5스테이지마다 하나씩 얻어요." : "아이콘을 누르면 설명이 보여요", new Vector2(850, 130), Vector2.zero, 30, TextDim);
        int cols = 5;
        for(int i = 0; i < ids.Count && i < 20; i++) {
            var def = AugmentDefs.Get(ids[i]);
            if(def == null) continue;
            int lv = mgr.Level(def.id);
            int r = i / cols, col = i % cols;
            // 칸 160x170, 줄 간격 178: 시너지 줄(아래 끝 270)과 설명 칸(위 끝 -465) 사이에 4줄이 들어가요
            var cell = ColorCard(p, new Vector2(160, 168), new Vector2(-344 + col * 172, 180 - r * 178), AugmentDefs.GradeColor(def.grade), "Aug");
            var face = Row(cell.transform, new Vector2(140, 148), new Vector2(0, 2), "Face");
            Icon(face.transform, def.icon, new Vector2(88, 88), new Vector2(0, 22));
            Label(face.transform, $"{def.name}\n<color=#FFD54A>★{lv}</color>", new Vector2(132, 54), new Vector2(0, -44), 20, Color.white, FontStyles.Bold);
            var b = cell.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => desc.text = $"<color=#FFFFFF><b>{def.name}</b></color> <color=#FFD54A>★{lv}</color>\n{def.describe(lv)}");
        }

        string stack = $"처치 {mgr.ExpeditionKills:N0}";
        if(mgr.CurseStackTotal > 0) stack += $"   ·   누적 피해 +{mgr.CurseStackTotal * 100:0.#}%";
        Label(p, stack, new Vector2(860, 48), new Vector2(0, -650), 30, AugmentDefs.TagColor(AugTag.Curse), FontStyles.Bold);
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
        string[] nodeIcons = { "bless_split", "bless_chain", "bless_curse", "bless_split" };
        int nodes = AugmentManager.TreeNames.Length;
        for(int n = 0; n < nodes; n++) {
            int node = n;
            // 4칸: 칸 높이 160, 간격 172 → 위 끝 230(제목 아래), 아래 끝 -446(버튼 위 -595)
            float y = 150 - n * 172;
            var row = Row(p, new Vector2(900, 160), new Vector2(0, y), "Node");
            int lv = mgr.TreeLevel(node);
            bool max = lv >= AugmentManager.TreeMax[node];
            bool star = node == AugmentManager.StarPowerNode;
            string lvText = star ? $"Lv.{lv}" : $"Lv.{lv}/{AugmentManager.TreeMax[node]}";
            IconTile(row.transform, nodeIcons[node % nodeIcons.Length], 104, new Vector2(-362, 0), max || star ? TextGold : new Color(0.55f, 0.6f, 0.9f));
            Label(row.transform, $"{AugmentManager.TreeNames[node]}  <color=#FFD54A>{lvText}</color>", new Vector2(440, 56), new Vector2(-55, 36), 32, Color.white, FontStyles.Bold);
            Label(row.transform, mgr.TreeDesc(node), new Vector2(440, 74), new Vector2(-55, -32), 24, TextDim);
            var b = Btn(row.transform, max ? "최대" : $"강화\n<size=80%>먼지 {mgr.TreeCost(node):N0}</size>", new Vector2(230, 130), new Vector2(315, 0), max ? BGray : BBlue, () => {
                if(mgr.UpgradeTree(node)) BuildRebirth(p, close);
                else Toast("별의 먼지가 부족해요", Color.white);
            }, 32);
            b.interactable = !max;
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
