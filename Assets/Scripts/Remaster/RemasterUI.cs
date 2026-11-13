using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 리마스터 UI: 씬을 건드리지 않고 코드로 만드는 화면들 (기준 해상도 1080x2048, 가로폭 맞춤 = 기존 UI와 동일)
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

    static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.16f, 0.97f);
    static readonly Color PanelInner = new Color(0.15f, 0.17f, 0.25f, 1f);
    static readonly Color ButtonBlue = new Color(0.22f, 0.5f, 0.95f);
    static readonly Color ButtonGreen = new Color(0.2f, 0.72f, 0.42f);
    static readonly Color ButtonGray = new Color(0.35f, 0.37f, 0.45f);
    static readonly Color TextDim = new Color(0.75f, 0.78f, 0.86f);

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
                        if(mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0.18f);
                        if(mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.9f));
                    }
                    // ◆ 같은 기호가 미원에 없을 때를 대비
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

    Image Box(Transform parent, Vector2 size, Vector2 pos, Color color, string name = "Box")
    {
        var rt = Rect(name, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

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

    Button Btn(Transform parent, string text, Vector2 size, Vector2 pos, Color color, Action onClick, float fontSize = 44f)
    {
        var img = Box(parent, size, pos, color, "Button");
        var b = img.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        b.colors = cb;
        b.onClick.AddListener(() => onClick?.Invoke());
        // 버튼 안쪽 여백을 두고 가운데 정렬
        Label(img.transform, text, new Vector2(size.x - 28f, size.y - 16f), Vector2.zero, fontSize, Color.white, FontStyles.Bold);
        return b;
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

    // 모달: 화면 어둡게 + 가운데 패널
    RectTransform OpenModal(Vector2 size, out Action close)
    {
        EnsureBuilt();
        var dim = Stretch("Modal", modalRoot);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0, 0, 0, 0.62f);
        var panel = Box(dim, size, Vector2.zero, PanelColor, "Panel");
        openModals++;
        bool closed = false;
        close = () => {
            if(closed) return;
            closed = true;
            openModals = Mathf.Max(0, openModals - 1);
            Destroy(dim.gameObject);
            RefreshHUD();
        };
        StartCoroutine(PopIn(panel.rectTransform));
        return panel.rectTransform;
    }

    IEnumerator PopIn(RectTransform rt)
    {
        float t = 0;
        while(t < 0.18f && rt != null) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.18f);
            float s = Mathf.Lerp(0.85f, 1f, 1f - (1f - k) * (1f - k));
            rt.localScale = new Vector3(s, s, 1);
            yield return null;
        }
        if(rt != null) rt.localScale = Vector3.one;
    }
    #endregion

    #region HUD
    RectTransform hud;
    TextMeshProUGUI hudStardust, hudStack;
    readonly TextMeshProUGUI[] hudTag = new TextMeshProUGUI[3];
    Button hudRebirthBtn;
    Image hudRebirthImg;

    void BuildHud()
    {
        // 화면 왼쪽 가운데보다 조금 위 (적이 오는 길을 덜 가리도록 작고 반투명하게)
        hud = Rect("ExpeditionHud", hudRoot, new Vector2(176, 470), Vector2.zero);
        hud.anchorMin = hud.anchorMax = new Vector2(0f, 0.6f);
        hud.pivot = new Vector2(0f, 0.5f);
        hud.anchoredPosition = new Vector2(12, 0);
        var bg = hud.gameObject.AddComponent<Image>();
        bg.sprite = rounded; bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.07f, 0.12f, 0.6f);

        Label(hud, "원정", new Vector2(160, 40), new Vector2(0, 205), 30, Color.white, FontStyles.Bold);
        hudStardust = Label(hud, "", new Vector2(160, 36), new Vector2(0, 168), 26, AugmentDefs.TagColor(AugTag.Treasure));
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        string[] icons = { "bless_chain", "bless_curse", "bless_split" };
        for(int i = 0; i < 3; i++) {
            float y = 112 - i * 58;
            Icon(hud, icons[i], new Vector2(46, 46), new Vector2(-48, y));
            hudTag[i] = Label(hud, "", new Vector2(100, 46), new Vector2(26, y), 30, AugmentDefs.TagColor(tags[i]), FontStyles.Bold);
        }
        hudStack = Label(hud, "", new Vector2(164, 56), new Vector2(0, -60), 22, TextDim);
        Btn(hud, "증강", new Vector2(150, 64), new Vector2(0, -126), ButtonBlue, ShowAugmentList, 30);
        hudRebirthBtn = Btn(hud, "환생", new Vector2(150, 64), new Vector2(0, -198), ButtonGray, ShowRebirth, 30);
        hudRebirthImg = hudRebirthBtn.GetComponent<Image>();
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
        hudStardust.text = $"먼지 {mgr.Stardust:N0}";
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        for(int i = 0; i < 3; i++) hudTag[i].text = mgr.TagCount(tags[i]).ToString();
        float stack = mgr.CurseStackTotal;
        hudStack.text = stack > 0 ? $"누적 피해\n+{stack * 100:0.#}%" : $"처치 {mgr.ExpeditionKills:N0}";
        bool can = mgr.CanRebirth;
        hudRebirthImg.color = can ? new Color(0.95f, 0.65f, 0.2f) : ButtonGray;
    }
    #endregion

    #region 토스트
    readonly List<RectTransform> toasts = new List<RectTransform>();

    public void Toast(string text, Color color, bool big = false)
    {
        if(!EnsureBuilt()) return;
        var box = Box(toastRoot, new Vector2(900, big ? 120 : 92), Vector2.zero, new Color(0.05f, 0.06f, 0.1f, 0.9f), "Toast");
        var rt = box.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.78f);
        Label(rt, text, new Vector2(860, big ? 104 : 80), Vector2.zero, big ? 42 : 34, color, big ? FontStyles.Bold : FontStyles.Normal);
        toasts.Insert(0, rt);
        for(int i = 0; i < toasts.Count; i++)
            if(toasts[i] != null) toasts[i].anchoredPosition = new Vector2(0, -i * 108);
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
        if(pullPanel == null) pullPanel = OpenModal(new Vector2(860, 1260), out pullClose);
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

        Label(pullPanel, "증강 획득!", new Vector2(760, 80), new Vector2(0, 560), 60, Color.white, FontStyles.Bold);
        Label(pullPanel, $"{AugmentManager.PullInterval}스테이지마다 증강을 하나씩 얻어요", new Vector2(760, 48), new Vector2(0, 500), 30, TextDim);

        // 카드
        var frame = Box(pullPanel, new Vector2(600, 800), new Vector2(0, 50), AugmentDefs.GradeColor(AugGrade.Silver), "CardFrame");
        var inner = Box(frame.transform, new Vector2(572, 772), Vector2.zero, PanelInner, "CardInner");
        var skip = frame.gameObject.AddComponent<Button>();
        var gradeLabel = Label(inner.transform, AugmentDefs.GradeName(AugGrade.Silver), new Vector2(520, 56), new Vector2(0, 340), 40, AugmentDefs.GradeColor(AugGrade.Silver), FontStyles.Bold);

        var content = Rect("Content", inner.transform, new Vector2(572, 700), new Vector2(0, -30));
        var cg = content.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        var tagChip = Box(content, new Vector2(340, 56), new Vector2(0, 260), AugmentDefs.TagColor(def.tag) * new Color(1, 1, 1, 0.35f), "TagChip");
        Label(tagChip.transform, $"{AugmentDefs.TagName(def.tag)} · {AugmentDefs.GodName(def.tag)}", new Vector2(320, 48), Vector2.zero, 30, Color.white, FontStyles.Bold);
        Icon(content, def.icon, new Vector2(230, 230), new Vector2(0, 80));
        Label(content, def.name, new Vector2(520, 76), new Vector2(0, -90), 56, Color.white, FontStyles.Bold);
        Label(content, newLevel <= 1 ? "NEW" : $"★{newLevel}  (최대 ★{def.maxLevel})", new Vector2(520, 48), new Vector2(0, -150), 34, newLevel <= 1 ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.85f, 0.4f), FontStyles.Bold);
        Label(content, def.describe(newLevel), new Vector2(520, 170), new Vector2(0, -265), 36, TextDim);

        var countdown = Label(pullPanel, "", new Vector2(760, 44), new Vector2(0, -570), 28, TextDim);
        Btn(pullPanel, rerollLabel, new Vector2(400, 120), new Vector2(-195, -470), ButtonGray, () => { if(!done) onReroll?.Invoke(); }, 34);
        Btn(pullPanel, "받기", new Vector2(300, 120), new Vector2(215, -470), ButtonGreen, accept, 46);

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
        var p = OpenModal(new Vector2(880, 900), out Action close);
        Label(p, title, new Vector2(800, 120), new Vector2(0, 360), 50, Color.white, FontStyles.Bold);
        Label(p, body, new Vector2(800, 520), new Vector2(0, 30), 36, TextDim);
        Btn(p, "확인", new Vector2(360, 120), new Vector2(0, -350), ButtonGreen, () => { close(); onClose?.Invoke(); }, 46);
    }

    public void ShowWeaponChoice(string title, List<SideWeapon> options, Action<SideWeapon> onPick)
    {
        if(!EnsureBuilt() || options.Count == 0) { onPick?.Invoke(options.Count > 0 ? options[0] : SideWeapon.None); return; }
        float h = 300 + options.Count * 130;
        var p = OpenModal(new Vector2(800, h), out Action close);
        Label(p, title, new Vector2(720, 90), new Vector2(0, h / 2 - 80), 46, Color.white, FontStyles.Bold);
        for(int i = 0; i < options.Count; i++) {
            var w = options[i];
            var tag = AugmentDefs.WeaponTag(w);
            Color c = tag.HasValue ? AugmentDefs.TagColor(tag.Value) * new Color(0.75f, 0.75f, 0.75f, 1f) : ButtonBlue;
            Btn(p, $"{AugmentDefs.WeaponName(w)}  Lv.{mgr.WeaponLevel(w)}", new Vector2(620, 110), new Vector2(0, h / 2 - 200 - i * 130), c, () => { close(); onPick?.Invoke(w); }, 40);
        }
    }
    #endregion

    #region 원정 준비 (출격 무기 + 축복)
    public void ShowSetup(Action onDone)
    {
        if(!EnsureBuilt()) { onDone?.Invoke(); return; }
        var p = OpenModal(new Vector2(960, 1500), out Action close);
        SideWeapon left = mgr.LoadoutLeft, right = mgr.LoadoutRight;
        int blessing = mgr.Blessing;
        if(blessing >= 0 && !mgr.BlessingUnlocked((AugTag)blessing)) blessing = -1;

        Label(p, "원정 준비", new Vector2(880, 100), new Vector2(0, 660), 64, Color.white, FontStyles.Bold);
        Label(p, "출격 무기와 축복을 고르고 출발하세요", new Vector2(880, 50), new Vector2(0, 595), 32, TextDim);

        // ---- 출격 무기
        Label(p, "출격 무기", new Vector2(880, 60), new Vector2(0, 510), 42, Color.white, FontStyles.Bold);
        var owned = mgr.OwnedWeapons();
        Button leftBtn = null, rightBtn = null;
        Action refreshSlots = null;
        Func<SideWeapon, SideWeapon, SideWeapon> next = (cur, other) => {
            var opts = new List<SideWeapon> { SideWeapon.None };
            foreach(var w in owned) if(w != other) opts.Add(w);
            int idx = opts.IndexOf(cur);
            return opts[(idx + 1) % opts.Count];
        };
        leftBtn = Btn(p, "", new Vector2(270, 200), new Vector2(-300, 370), ButtonGray, () => { left = next(left, right); refreshSlots(); }, 34);
        var center = Box(p, new Vector2(230, 200), new Vector2(0, 370), new Color(0.3f, 0.32f, 0.4f), "MainGun");
        Label(center.transform, "기본총\n(고정)", new Vector2(200, 170), Vector2.zero, 36, Color.white, FontStyles.Bold);
        rightBtn = Btn(p, "", new Vector2(270, 200), new Vector2(300, 370), ButtonGray, () => { right = next(right, left); refreshSlots(); }, 34);
        Label(p, owned.Count == 0 ? "무기 상점에서 무기를 사면 여기에 장착할 수 있어요" : "칸을 누를 때마다 다른 무기로 바뀌어요", new Vector2(880, 46), new Vector2(0, 240), 28, TextDim);
        refreshSlots = () => {
            SetSlot(leftBtn, "왼쪽", left);
            SetSlot(rightBtn, "오른쪽", right);
        };
        refreshSlots();

        // ---- 축복
        Label(p, "축복", new Vector2(880, 60), new Vector2(0, 150), 42, Color.white, FontStyles.Bold);
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
            var card = Box(p, new Vector2(280, 420), new Vector2(-300 + i * 300, -110), PanelInner, "Blessing");
            cards[i] = card;
            Icon(card.transform, icons[i], new Vector2(130, 130), new Vector2(0, 110));
            Label(card.transform, AugmentDefs.GodName(tag), new Vector2(250, 56), new Vector2(0, 10), 36, AugmentDefs.TagColor(tag), FontStyles.Bold);
            if(unlocked) {
                Label(card.transform, $"{AugmentDefs.TagName(tag)} 증강이\n더 잘 나와요", new Vector2(250, 110), new Vector2(0, -90), 30, TextDim);
                var b = card.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => { blessing = blessing == idx ? -1 : idx; refreshCards(); });
            } else {
                card.color = new Color(0.12f, 0.12f, 0.16f);
                Label(card.transform, $"잠김\n{AugmentManager.BlessingPlanetName(tag)}\n행성을 클리어하면 열려요", new Vector2(250, 170), new Vector2(0, -110), 26, new Color(0.6f, 0.6f, 0.65f));
            }
        }
        Label(p, anyUnlocked ? "다시 누르면 선택을 해제해요" : "4번째 행성(헤파이스토스)을 클리어하면 첫 축복이 열려요", new Vector2(880, 46), new Vector2(0, -360), 28, TextDim);
        refreshCards = () => {
            for(int i = 0; i < 3; i++) {
                if(!mgr.BlessingUnlocked(tags[i])) continue;
                cards[i].color = blessing == i ? AugmentDefs.TagColor(tags[i]) * new Color(0.55f, 0.55f, 0.55f, 1f) : PanelInner;
                cards[i].rectTransform.localScale = blessing == i ? Vector3.one * 1.05f : Vector3.one;
            }
        };
        refreshCards();

        Btn(p, "출발!", new Vector2(420, 140), new Vector2(0, -580), ButtonGreen, () => {
            mgr.SetLoadout(left, right);
            mgr.SetBlessing(blessing);
            close();
            onDone?.Invoke();
        }, 56);
    }

    void SetSlot(Button btn, string side, SideWeapon w)
    {
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        var img = btn.GetComponent<Image>();
        if(w == SideWeapon.None) {
            label.text = $"{side}\n비어 있음";
            img.color = ButtonGray;
        } else {
            label.text = $"{side}\n{AugmentDefs.WeaponName(w)}\nLv.{mgr.WeaponLevel(w)}";
            var tag = AugmentDefs.WeaponTag(w);
            img.color = tag.HasValue ? AugmentDefs.TagColor(tag.Value) * new Color(0.7f, 0.7f, 0.7f, 1f) : ButtonBlue;
        }
    }
    #endregion

    #region 증강 목록
    public void ShowAugmentList()
    {
        if(!EnsureBuilt() || mgr == null || !mgr.Loaded) return;
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(960, 1640), out Action close);
        Label(p, "이번 원정의 증강", new Vector2(880, 90), new Vector2(0, 750), 56, Color.white, FontStyles.Bold);

        string bless = mgr.Blessing >= 0 ? AugmentDefs.GodName((AugTag)mgr.Blessing) : "없음";
        string weapons = $"{AugmentDefs.WeaponName(mgr.LoadoutLeft)} / 기본총 / {AugmentDefs.WeaponName(mgr.LoadoutRight)}";
        if(mgr.ExtraWeapon != SideWeapon.None) weapons += $" + {AugmentDefs.WeaponName(mgr.ExtraWeapon)}";
        if(mgr.SupplyWeapon != SideWeapon.None) weapons += $" + {AugmentDefs.WeaponName(mgr.SupplyWeapon)}(보급)";
        Label(p, $"축복: {bless}\n무기: {weapons}", new Vector2(880, 100), new Vector2(0, 660), 30, TextDim);

        // 시너지
        AugTag[] tags = { AugTag.Chain, AugTag.Curse, AugTag.Split };
        for(int i = 0; i < 3; i++) {
            var tag = tags[i];
            int c = mgr.TagCount(tag);
            string s2 = (c >= 2 ? "● " : "○ ") + "2: " + AugmentDefs.SynergyText(tag, 2);
            string s4 = (c >= 4 ? "● " : "○ ") + "4: " + AugmentDefs.SynergyText(tag, 4);
            var row = Box(p, new Vector2(880, 96), new Vector2(0, 540 - i * 108), PanelInner, "Synergy");
            Label(row.transform, $"{AugmentDefs.TagName(tag)} {c}개", new Vector2(200, 80), new Vector2(-330, 0), 34, AugmentDefs.TagColor(tag), FontStyles.Bold);
            Label(row.transform, s2 + "\n" + s4, new Vector2(620, 84), new Vector2(110, 0), 26, TextDim);
        }

        // 보유 증강 (아이콘 격자)
        var ids = mgr.OwnedOrder;
        var desc = Label(p, ids.Count == 0 ? "아직 증강이 없어요.\n5스테이지마다 하나씩 얻어요." : "아이콘을 누르면 설명이 보여요", new Vector2(860, 140), new Vector2(0, -530), 32, TextDim);
        int cols = 5;
        for(int i = 0; i < ids.Count && i < 20; i++) {
            var def = AugmentDefs.Get(ids[i]);
            if(def == null) continue;
            int lv = mgr.Level(def.id);
            int r = i / cols, col = i % cols;
            // 칸 160x170, 줄 간격 180: 시너지 줄(아래 끝 276)과 설명 칸(위 끝 -460) 사이에 4줄이 들어가요
            var cell = Box(p, new Vector2(160, 170), new Vector2(-340 + col * 170, 180 - r * 180), AugmentDefs.GradeColor(def.grade) * new Color(0.45f, 0.45f, 0.45f, 1f), "Aug");
            Icon(cell.transform, def.icon, new Vector2(100, 100), new Vector2(0, 28));
            Label(cell.transform, $"{def.name}\n★{lv}", new Vector2(150, 60), new Vector2(0, -52), 22, Color.white, FontStyles.Bold);
            var b = cell.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => desc.text = $"<b>{def.name}</b> ★{lv}\n{def.describe(lv)}");
        }

        string stack = $"처치 {mgr.ExpeditionKills:N0}";
        if(mgr.CurseStackTotal > 0) stack += $"   ·   누적 피해 +{mgr.CurseStackTotal * 100:0.#}%";
        Label(p, stack, new Vector2(860, 50), new Vector2(0, -630), 30, AugmentDefs.TagColor(AugTag.Curse));
        Btn(p, "닫기", new Vector2(320, 110), new Vector2(0, -735), ButtonGray, () => { close(); mgr.ResumeFromUI(); }, 42);
    }
    #endregion

    #region 환생
    public void ShowRebirth()
    {
        if(!EnsureBuilt() || mgr == null || !mgr.Loaded) return;
        mgr.PauseForUI();
        var p = OpenModal(new Vector2(960, 1560), out Action close);
        BuildRebirth(p, close);
    }

    void BuildRebirth(RectTransform p, Action close)
    {
        for(int i = p.childCount - 1; i >= 0; i--) Destroy(p.GetChild(i).gameObject);
        int stage = InfiniteStage.Instance != null ? InfiniteStage.Instance.CurrentStage : 1;
        Label(p, "환생", new Vector2(880, 100), new Vector2(0, 710), 64, Color.white, FontStyles.Bold);
        Label(p, $"현재 {stage:N0}스테이지   ·   최고 {Mathf.Max(mgr.BestStage, stage):N0}스테이지", new Vector2(880, 50), new Vector2(0, 640), 32, TextDim);
        Label(p, $"보유한 별의 먼지 {mgr.Stardust:N0}개", new Vector2(880, 60), new Vector2(0, 575), 40, AugmentDefs.TagColor(AugTag.Treasure), FontStyles.Bold);

        var info = Box(p, new Vector2(880, 200), new Vector2(0, 420), PanelInner, "Info");
        if(mgr.CanRebirth)
            Label(info.transform, $"지금 환생하면 별의 먼지 {mgr.RebirthGain:N0}개를 받아요.\n증강과 스테이지는 초기화되고,\n무기·장비·골드는 그대로 남아요.", new Vector2(840, 180), Vector2.zero, 32, Color.white);
        else
            Label(info.transform, $"{mgr.RebirthThreshold:N0}스테이지부터 환생할 수 있어요\n(지금 {stage:N0}스테이지)\n환생하면 별의 먼지로 영구 강화를 해요.", new Vector2(840, 180), Vector2.zero, 32, TextDim);

        Label(p, "환생 트리 (영구 강화)", new Vector2(880, 60), new Vector2(0, 270), 40, Color.white, FontStyles.Bold);
        for(int n = 0; n < 3; n++) {
            int node = n;
            float y = 140 - n * 210;
            var row = Box(p, new Vector2(880, 190), new Vector2(0, y), PanelInner, "Node");
            int lv = mgr.TreeLevel(node);
            bool max = lv >= AugmentManager.TreeMax[node];
            Label(row.transform, $"{AugmentManager.TreeNames[node]}  Lv.{lv}/{AugmentManager.TreeMax[node]}", new Vector2(540, 60), new Vector2(-150, 45), 36, Color.white, FontStyles.Bold);
            Label(row.transform, mgr.TreeDesc(node), new Vector2(560, 90), new Vector2(-150, -35), 26, TextDim);
            var b = Btn(row.transform, max ? "최대" : $"강화\n먼지 {mgr.TreeCost(node):N0}", new Vector2(250, 140), new Vector2(300, 0), max ? ButtonGray : ButtonBlue, () => {
                if(mgr.UpgradeTree(node)) BuildRebirth(p, close);
                else Toast("별의 먼지가 부족해요", Color.white);
            }, 32);
            b.interactable = !max;
        }

        var rb = Btn(p, "환생하기", new Vector2(420, 130), new Vector2(-220, -640), mgr.CanRebirth ? new Color(0.95f, 0.6f, 0.2f) : ButtonGray, () => {
            var c = OpenModal(new Vector2(800, 560), out Action closeConfirm);
            Label(c, "정말 환생할까요?", new Vector2(720, 90), new Vector2(0, 170), 50, Color.white, FontStyles.Bold);
            Label(c, $"별의 먼지 {mgr.RebirthGain:N0}개를 받고\n1스테이지부터 다시 출발해요.", new Vector2(720, 140), new Vector2(0, 30), 34, TextDim);
            Btn(c, "취소", new Vector2(280, 120), new Vector2(-170, -170), ButtonGray, () => closeConfirm(), 42);
            Btn(c, "환생", new Vector2(280, 120), new Vector2(170, -170), new Color(0.95f, 0.6f, 0.2f), () => {
                closeConfirm();
                close();
                mgr.ResumeFromUI();
                mgr.DoRebirth();
            }, 42);
        }, 46);
        rb.interactable = mgr.CanRebirth;
        Btn(p, "닫기", new Vector2(320, 130), new Vector2(240, -640), ButtonGray, () => { close(); mgr.ResumeFromUI(); }, 42);
    }
    #endregion
}
