using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 미리보기 화면을 그대로 세우기 (2026-11-26 사용자: "보여 준 화면 그대로, 버튼 하나하나 위치까지 전부 일치하게")
// - tools/ui_preview/export_layout.js 가 미리보기에서 잰 Resources/UIGen/<이름>.json 을 읽어요
//   box = 크롬으로 찍은 판 그림 (그림자 여백 pad 포함), icon = 원본 그림, text = 글자 (게임 글꼴, 위치·크기·색·테두리 그대로)
// - 좌표는 미리보기 폰(360×780 px) 왼쪽 위 기준, 1px = 3단위. 틀은 화면에 맞춰 줄여요 (MgFit)
// - data-k 이름으로 코드가 글자를 바꾸고 · 보이기/숨기기 · 버튼을 달아요 (GenText / GenShow / GenHit)
public partial class RemasterUI
{
    [System.Serializable] public class GenItem { public string t, sprite, f, k, text, c, oc, align, icon, cls; public float[] r, pr; public float w, h, pad, a, fs, ow, ls; public bool own, pix; public int lines; }
    [System.Serializable] public class GenLayout { public string name; public float w, h; public GenItem[] items; }

    public class GenView
    {
        public RectTransform layer, box;
        public bool full;                 // 전체화면 창 (창 판을 화면 끝까지)
        public float scale = 1f, topPx, hostH;   // 내용 크기 · 리본 위 끝(미리보기 px) · 화면 높이(단위)
        public readonly Dictionary<string, List<GameObject>> objs = new Dictionary<string, List<GameObject>>();
        public readonly Dictionary<string, List<TMP_Text>> texts = new Dictionary<string, List<TMP_Text>>();
        public readonly Dictionary<string, UnityEngine.Rect> rects = new Dictionary<string, UnityEngine.Rect>();   // 이름표 요소들을 모두 덮는 칸 (미리보기 px)
    }

    readonly Dictionary<TMP_Text, float> genFs0 = new Dictionary<TMP_Text, float>();   // 글자 원래 크기 (줄였다가 다시 짧아지면 돌아가게)
    readonly Dictionary<TMP_Text, float> genW0 = new Dictionary<TMP_Text, float>();    // 글 원래 폭 (미리보기 px)
    readonly Dictionary<Graphic, float> genA0 = new Dictionary<Graphic, float>();      // 원래 투명도 (흐리게 했다가 돌아가게)
    readonly Dictionary<GameObject, string> genOwnKey = new Dictionary<GameObject, string>();   // 요소마다 자기 이름표
    readonly Dictionary<GameObject, float> genPad = new Dictionary<GameObject, float>();   // 판 그림의 그림자 여백 (px)
    readonly Dictionary<string, GenLayout> genCache = new Dictionary<string, GenLayout>();
    GenLayout GenLoad(string name)
    {
        if(genCache.TryGetValue(name, out var l)) return l;
        var ta = Resources.Load<TextAsset>("UIGen/" + name);
        l = ta != null ? JsonUtility.FromJson<GenLayout>(ta.text) : null;
        genCache[name] = l;
        return l;
    }

    static Color CssColor(string s, float a = 1f)
    {
        if(string.IsNullOrEmpty(s)) return Color.white;
        var m = System.Text.RegularExpressions.Regex.Match(s, @"rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)");
        if(!m.Success) return Color.white;
        float al = m.Groups[4].Success ? float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture) : 1f;
        return new Color(float.Parse(m.Groups[1].Value) / 255f, float.Parse(m.Groups[2].Value) / 255f, float.Parse(m.Groups[3].Value) / 255f, al * a);
    }
    // CSS filter 중 밝기만 (못 만난 몬스터 그림자 = brightness(0)), 나머지는 무시
    static Color FilterTint(string f, float a)
    {
        float b = 1f;
        if(!string.IsNullOrEmpty(f)) foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(f, @"brightness\(([\d.]+)\)")) b *= float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        float op = 1f;
        if(!string.IsNullOrEmpty(f)) foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(f, @"opacity\(([\d.]+)\)")) op *= float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return new Color(b, b, b, a * op);
    }

    // 창 세우기: 어두운 바탕(화면 전체) + 틀(1080×2340, 화면에 맞춤) + 미리보기 요소들
    // dimColor 를 주면 = 반투명 바탕 위에 뜨는 창 (미리보기의 'pdim' 어둡게 칸은 빼요)
    // 안 주면 = 페이지 창: 미리보기 바탕(.bg) 그라데이션을 화면 전체에 깔아요 (틀 밖도 같은 바탕)
    GenView GenBuild(string name, string layerName, bool pop = true, RectTransform parent = null, Color? dimColor = null)
    {
        var host = parent != null ? parent : modalRoot;
        var lay = GenLoad(name);
        if(lay == null || !EnsureBuilt()) { Debug.LogWarning("[UIGen] 없음: " + name); return null; }
        MapFont();
        for(int i = host.childCount - 1; i >= 0; i--) {
            var c = host.GetChild(i);
            if(c.name == layerName) { c.name += " (closing)"; c.gameObject.SetActive(false); Destroy(c.gameObject); }
        }
        var v = new GenView();
        v.layer = Stretch(layerName, host);
        v.layer.SetAsLastSibling();
        var dim = Stretch("Dim", v.layer);
        var di = dim.gameObject.AddComponent<Image>();
        GenItem bg = null;
        foreach(var it in lay.items) if(it.t == "box" && it.cls == "bg") { bg = it; break; }
        if(dimColor.HasValue) di.color = dimColor.Value;
        else if(bg != null) { di.sprite = Resources.Load<Sprite>(bg.sprite); di.color = Color.white; }
        else di.color = new Color(0.063f, 0.102f, 0.271f, 1f);
        // 전체화면 (2026-11-27 사용자: "그 창들이 전체화면이 되게"): 미리보기 폰 틀째 줄이지 않고
        //  창 판(.win)은 화면 끝까지 늘리고(9-slice), 내용은 비율 그대로 화면에 맞는 가장 큰 크기로 · 가로 가운데 · 리본 위 끝 = 화면 위 끝
        GenItem win = null, rib = null;
        foreach(var it in lay.items) { if(it.t == "box" && it.cls == "win" && win == null) win = it; if(it.t == "box" && it.cls == "ribbon" && rib == null) rib = it; }
        float W = host.rect.width > 0 ? host.rect.width : 1080f, H = host.rect.height > 0 ? host.rect.height : 1920f;
        v.full = !dimColor.HasValue && win != null;
        float fit, safeTop = 0f;
        if(v.full) {
            // 안전 영역 (2026-11-27 점검): 폰의 카메라 구멍·노치·아래 막대 자리는 비우고 내용은 그 안에 (판·바탕은 화면 끝까지)
            //  안드로이드 설정이 노치 밑까지 그리게 되어 있어서(androidRenderOutsideSafeArea) 제목 리본이 카메라 구멍에 가릴 수 있었어요. 에디터는 0
            GenSafe(H, out safeTop, out float safeBot);
            float Hs = Mathf.Max(H * 0.5f, H - safeTop - safeBot);
            float top = rib != null ? rib.r[1] - rib.pad : win.r[1];
            float bottom = win.r[1] + win.r[3];
            fit = Mathf.Min(W / (win.r[2] * K), Hs / ((bottom - top) * K));
            v.scale = fit; v.topPx = top; v.hostH = Hs;
            // 창 판: 화면 끝까지 (위는 리본 절반만큼 비워요). 그림자 여백(pad)만큼 바깥으로 내서 테두리가 화면 끝에 오게
            var panel = Stretch("Panel", v.layer);
            var pimg = panel.gameObject.AddComponent<Image>();
            pimg.sprite = GenSliced(win.sprite, (win.pad + 25f) * K);
            pimg.type = Image.Type.Sliced;
            pimg.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, fit);   // 테두리 굵기도 내용과 같은 비율로
            pimg.color = new Color(1f, 1f, 1f, win.a);
            float padU = win.pad * K * fit, topInset = (win.r[1] - top) * K * fit;
            panel.offsetMin = new Vector2(-padU, -padU);
            panel.offsetMax = new Vector2(padU, -(safeTop + topInset - padU));
        } else fit = parent == null ? MgFit(1f) : Mathf.Min(1f, H / (lay.h * K), W / (lay.w * K));
        v.box = Rect("Box", v.layer, new Vector2(lay.w * K, lay.h * K), Vector2.zero);
        if(v.full) {
            v.box.anchorMin = v.box.anchorMax = new Vector2(0.5f, 1f);
            v.box.pivot = new Vector2((win.r[0] + win.r[2] / 2f) / lay.w, 1f);   // 창 가운데 = 화면 가운데
            v.box.anchoredPosition = new Vector2(0f, v.topPx * K * fit - safeTop);   // 리본 위 끝 = 화면 위 끝 (안전 영역 아래)
        }
        v.box.localScale = new Vector3(fit, fit, 1f);
        foreach(var it in lay.items) {
            if(it == bg && !dimColor.HasValue) continue;          // 바탕은 화면 전체에 깔았어요
            if(v.full && it == win) continue;                     // 창 판은 화면 끝까지 따로 깔았어요
            if(dimColor.HasValue && it.k == "pdim") continue;     // 반투명 바탕을 따로 깔았어요
            GenItemBuild(v, it);
        }
        // 전체화면: 닫기(X)를 창 오른쪽 끝(= 화면 오른쪽 끝)으로 (미리보기와 같은 안쪽 여백)
        if(v.full && v.objs.TryGetValue("close", out var cl)) {
            float dx = (W / 2f) / (K * fit) - win.r[2] / 2f;
            foreach(var g in cl) if(g != null) ((RectTransform)g.transform).anchoredPosition += new Vector2(dx * K, 0f);
            if(v.rects.TryGetValue("close", out var cr)) v.rects["close"] = new UnityEngine.Rect(cr.x + dx, cr.y, cr.width, cr.height);
        }
        // 장비 분해 창은 올라오지 않고 바로 (2026-11-28 사용자: "장비 분해 창은 아래에서 올라오는 애니메이션 없애 줘")
        if(pop && (name == "dismantle" || name == "dispick")) v.box.localScale = new Vector3(fit, fit, 1f);
        else if(pop) StartCoroutine(SlideIn(v.box, fit));   // 아래에서 올라와요 (2026-11-28 사용자: 모든 창)
        return v;
    }

    // 예전 페이지 찾기 (이름 + 부모 이름). 못 찾으면 1초 뒤에 다시 (매 프레임 장면 전체를 뒤지지 않게)
    readonly Dictionary<string, float> genFindNext = new Dictionary<string, float>();
    GameObject GenFindPage(string name, string parentName)
    {
        string key = parentName + "/" + name;
        if(genFindNext.TryGetValue(key, out float t) && Time.unscaledTime < t) return null;
        genFindNext[key] = Time.unscaledTime + 1f;
        foreach(var tr in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if(tr.name == name && tr.parent != null && tr.parent.name == parentName) return tr.gameObject;
        return null;
    }

    void GenItemBuild(GenView v, GenItem it)
    {
        float x = it.r[0], y = it.r[1], w = it.r[2], h = it.r[3];
        GameObject go = null;
        if(it.t == "box" || it.t == "icon") {
            float pad = it.t == "box" ? it.pad : 0f;
            var rt = PxBox(it.t == "box" ? "Box" : "Icon", v.box, x - pad, y - pad, w + pad * 2f, h + pad * 2f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Resources.Load<Sprite>(it.sprite);
            img.raycastTarget = false;
            if(it.t == "icon") { img.preserveAspect = true; img.color = FilterTint(it.f, it.a); }
            else img.color = new Color(1f, 1f, 1f, it.a);
            go = rt.gameObject;
            if(pad > 0f) genPad[go] = pad;
        } else if(it.t == "text") {
            bool multi = it.lines > 1;
            Color col = CssColor(it.c, it.a);
            TextMeshProUGUI tx;
            if(multi) {
                // 여러 줄: 미리보기 글 상자 폭으로 줄바꿈, 왼쪽 위부터
                var rt = PxBox("Text", v.box, x, y, w + 2f, h + 2f);
                tx = rt.gameObject.AddComponent<TextMeshProUGUI>();
                tx.font = MapFont(); tx.fontSize = it.fs * K; tx.text = it.text;
                tx.alignment = TextAlignmentOptions.TopLeft; tx.textWrappingMode = TextWrappingModes.Normal;
                tx.raycastTarget = false;
            } else {
                // 가운데 정렬(버튼 글자 등)은 가운데 기준, 나머지는 왼쪽 끝 기준 (글이 길어지면 오른쪽으로)
                if(it.align == "center") tx = PxText(v.box, it.text, x + w / 2f, y + h / 2f, it.fs, col);
                else if(it.align == "right") tx = PxText(v.box, it.text, x + w, y + h / 2f, it.fs, col, 0f, null, TextAlignmentOptions.Right);   // 오른쪽 끝 기준
                else tx = PxText(v.box, it.text, x, y + h / 2f, it.fs, col, 0f, null, TextAlignmentOptions.Left);
            }
            tx.color = col;
            if(it.ow > 0f) GameFont(tx, true, CssColor(string.IsNullOrEmpty(it.oc) ? "rgb(16,20,42)" : it.oc)); else GameFont(tx, false);
            tx.color = col;
            if(it.ls != 0f) tx.characterSpacing = it.ls;
            genFs0[tx] = tx.fontSize; genW0[tx] = w;
            go = tx.gameObject;
            if(!string.IsNullOrEmpty(it.k)) {
                if(!v.texts.TryGetValue(it.k, out var tl)) v.texts[it.k] = tl = new List<TMP_Text>();
                tl.Add(tx);
            }
        }
        if(go == null || string.IsNullOrEmpty(it.k)) return;
        genOwnKey[go] = it.k;
        // 이름표: 자기와 위쪽 이름표 모두에 (예: "g0.name" 요소는 "g0" 에도 들어가요)
        foreach(var key in GenKeys(it.k)) {
            if(!v.objs.TryGetValue(key, out var ol)) v.objs[key] = ol = new List<GameObject>();
            ol.Add(go);
            var rr = new UnityEngine.Rect(x, y, w, h);
            v.rects[key] = v.rects.TryGetValue(key, out var old) ? UnityEngine.Rect.MinMaxRect(Mathf.Min(old.xMin, rr.xMin), Mathf.Min(old.yMin, rr.yMin), Mathf.Max(old.xMax, rr.xMax), Mathf.Max(old.yMax, rr.yMax)) : rr;
        }
    }
    static IEnumerable<string> GenKeys(string k)
    {
        yield return k;
        int i = k.LastIndexOf('.');
        while(i > 0) { yield return k.Substring(0, i); i = k.LastIndexOf('.', i - 1); }
    }

    // ---- 이름표로 다루기
    void GenText(GenView v, string key, string s, int idx = 0)
    {
        if(v == null || !v.texts.TryGetValue(key, out var l) || idx >= l.Count || l[idx] == null) return;
        var t = l[idx];
        if(t.text == s) return;
        t.text = s;
        // 넓어진 글: 글 상자를 늘리고, 이름표 맨 위 칸(버튼·줄)보다 넓으면 글자를 줄여요. 크기는 늘 원래 크기에서 다시 계산
        if(genFs0.TryGetValue(t, out float fs0)) t.fontSize = fs0;
        float pw = t.GetPreferredValues(s, 99999f, 99999f).x;
        var rt = t.rectTransform;
        if(pw + 8f > rt.sizeDelta.x) {
            float grow = pw + 8f - rt.sizeDelta.x;
            rt.sizeDelta = new Vector2(pw + 8f, rt.sizeDelta.y);
            // 가운데 기준 상자라 늘리면 양쪽으로 커져요 → 왼쪽 정렬은 오른쪽으로, 오른쪽 정렬은 왼쪽으로만 늘게 옮겨요
            if(t.alignment == TextAlignmentOptions.MidlineLeft) rt.anchoredPosition += new Vector2(grow / 2f, 0f);
            else if(t.alignment == TextAlignmentOptions.MidlineRight) rt.anchoredPosition -= new Vector2(grow / 2f, 0f);
        }
        string top = key.Contains(".") ? key.Substring(0, key.IndexOf('.')) : key;
        // 감싸는 칸(버튼 등)이 글보다 넓을 때만 그 칸에 맞춰 줄여요 (글 하나뿐인 이름표는 자유롭게 늘어나요)
        genW0.TryGetValue(t, out float w0);
        if(v.rects.TryGetValue(top, out var r) && r.width > w0 + 6f && t.alignment != TextAlignmentOptions.MidlineLeft) {
            float maxW = (r.width - 8f) * K;
            if(pw > maxW && pw > 0f) t.fontSize = t.fontSize * maxW / pw;
        }
    }
    // 글을 바꾸고 폭이 maxPx(미리보기 px)를 넘으면 글자를 줄여요 (이름처럼 길이를 모르는 글)
    void GenTextFit(GenView v, string key, string s, float maxPx, int idx = 0)
    {
        GenText(v, key, s, idx);
        if(v == null || !v.texts.TryGetValue(key, out var l) || idx >= l.Count || l[idx] == null) return;
        var t = l[idx];
        float pw = t.GetPreferredValues(s, 99999f, 99999f).x, max = maxPx * K;
        if(pw > max && pw > 0f) t.fontSize = t.fontSize * max / pw;
    }
    void GenShow(GenView v, string key, bool on)
    {
        if(v != null && v.objs.TryGetValue(key, out var l)) foreach(var g in l) if(g != null && g.activeSelf != on) g.SetActive(on);
    }
    void GenAlpha(GenView v, string key, float a)
    {
        if(v == null || !v.objs.TryGetValue(key, out var l)) return;
        foreach(var g in l) {
            if(g == null) continue;
            var gr = g.GetComponent<Graphic>();
            if(gr == null) continue;
            if(!genA0.TryGetValue(gr, out float a0)) genA0[gr] = a0 = gr.color.a;
            var c = gr.color; c.a = a0 * a; gr.color = c;
        }
    }
    // 그 이름표 요소들을 덮는 투명 버튼 (pad = 바깥으로 넓히기, 미리보기 px)
    void GenHit(GenView v, string key, UnityEngine.Events.UnityAction act, float pad = 0f)
    {
        if(v == null || !v.rects.TryGetValue(key, out var r)) { Debug.LogWarning("[UIGen] 버튼 자리 없음: " + key); return; }
        JobHit(v.box, "Hit_" + key, r.x - pad, r.y - pad, r.width + pad * 2f, r.height + pad * 2f, () => {
            if(SoundManager.Instance != null) SoundManager.Instance.playClickSFX();
            act();
        });
    }
    // 닫기(X)를 원래 게임에서 가장 많이 쓰는 X 로 (2026-11-28 사용자: "X 도 다른 창에서 많이 쓰이는 X 로 바꿔 줘")
    //  = 장면의 원래 닫기 버튼(Button_Round06_Red 판 + Icon_Close01, 장면에서 45번)을 복사해서 미리보기 X 자리 가운데에. 누르기는 원래 'close' 자리(GenHit) 그대로
    GameObject genOrigClose;
    void GenUseOrigClose(GenView v)
    {
        if(v == null || !v.rects.TryGetValue("close", out var r)) return;
        if(genOrigClose == null)
            foreach(var tr in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if(tr.name == "Button_Close" && tr.parent != null && tr.parent.name == "Popup08_Topbar_Divided") { genOrigClose = tr.gameObject; break; }
        if(genOrigClose == null) return;
        GenShow(v, "close", false);
        var c = Instantiate(genOrigClose, v.box, false);
        c.name = "OrigClose";
        c.SetActive(true);
        foreach(var b in c.GetComponentsInChildren<Selectable>(true)) Destroy(b);   // 원래 버튼의 연결(설정 닫기)은 지워요
        foreach(var g in c.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        var rt = (RectTransform)c.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one * Tuning.Get("gen_close_scale", 0.9f);
        rt.anchoredPosition = new Vector2((r.x + r.width / 2f) * K, -(r.y + r.height / 2f) * K);
    }

    void GenClose(GenView v)
    {
        if(v != null && v.layer != null) Destroy(v.layer.gameObject);
    }

    // ---- 되풀이 칸 (카드 목록 등): 미리보기의 카드 몇 장을 틀로, 개수만큼 복제해서 스크롤 칸에 넣어요
    public class GenRow
    {
        public readonly Dictionary<string, List<GameObject>> objs = new Dictionary<string, List<GameObject>>();   // 접미사("" · ".img" …) → 요소
        public readonly Dictionary<string, TMP_Text> texts = new Dictionary<string, TMP_Text>();
        public void Show(string sub, bool on) { if(objs.TryGetValue(sub, out var l)) foreach(var g in l) if(g != null && g.activeSelf != on) g.SetActive(on); }
        public void Text(string sub, string s) { if(texts.TryGetValue(sub, out var t) && t != null) t.text = s; }
        public Image Img(string sub) { if(objs.TryGetValue(sub, out var l)) foreach(var g in l) { var i = g != null ? g.GetComponent<Image>() : null; if(i != null) return i; } return null; }
    }
    // templates = 틀 이름표들 (예: "c0" 일반 카드, "c8" 보스 카드), templateOf(i) = i 번째에 쓸 틀
    // dx·dy = 칸 사이 (미리보기 px), view = 스크롤 칸 (미리보기 px), removePrefix = 지울 원래 카드 이름표 앞부분 (예: "c")
    List<GenRow> GenRepeat(GenView v, string[] templates, System.Func<int, int> templateOf, int count, int cols, float dx, float dy, UnityEngine.Rect view, string removePrefix)
    {
        var rows = new List<GenRow>();
        if(v == null || !v.rects.TryGetValue(templates[0], out var r0)) return rows;
        // 스크롤 칸
        var vp = PxBox("RepeatView", v.box, view.x, view.y, view.width, view.height);
        vp.gameObject.AddComponent<RectMask2D>();
        vp.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        int nrows = Mathf.CeilToInt(count / (float)Mathf.Max(1, cols));
        float contentH = Mathf.Max(view.height, (r0.y - view.y) + nrows * dy + 12f);
        var content = PxBox("Content", vp, 0f, 0f, view.width, contentH);
        var sr = vp.gameObject.AddComponent<ScrollRect>();
        sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f;
        // 틀 요소 모으기 (이름표 → 접미사)
        var tpl = new List<List<(GameObject go, string sub)>>();
        var org = new List<Vector2>();
        foreach(var t in templates) {
            var list = new List<(GameObject, string)>();
            if(v.objs.TryGetValue(t, out var ol))
                foreach(var g in ol) { if(g == null) continue; genOwnKey.TryGetValue(g, out var own); list.Add((g, own != null && own.StartsWith(t) ? own.Substring(t.Length) : "")); }
            tpl.Add(list);
            org.Add(v.rects.TryGetValue(t, out var rr) ? new Vector2(rr.x, rr.y) : new Vector2(r0.x, r0.y));
        }
        for(int i = 0; i < count; i++) {
            int ti = Mathf.Clamp(templateOf(i), 0, templates.Length - 1);
            int col = i % cols, row = i / cols;
            var shift = new Vector2(-view.x - (org[ti].x - r0.x) + col * dx, -view.y - (org[ti].y - r0.y) + row * dy);   // 미리보기 px (아래가 +)
            var gr = new GenRow();
            foreach(var (g, sub) in tpl[ti]) {
                var c = Instantiate(g, content);
                c.SetActive(true);
                var rt = (RectTransform)c.transform;
                rt.anchoredPosition += new Vector2(shift.x * K, -shift.y * K);
                if(!gr.objs.TryGetValue(sub, out var l2)) gr.objs[sub] = l2 = new List<GameObject>();
                l2.Add(c);
                var tx = c.GetComponent<TMP_Text>();
                if(tx != null && !gr.texts.ContainsKey(sub)) gr.texts[sub] = tx;
            }
            rows.Add(gr);
        }
        // 원래 카드는 지워요 (틀로만 썼어요)
        var kill = new HashSet<GameObject>();
        foreach(var kv in v.objs) if(System.Text.RegularExpressions.Regex.IsMatch(kv.Key, "^" + removePrefix + @"\d+(\.|$)")) foreach(var g in kv.Value) if(g != null) kill.Add(g);
        foreach(var g in kill) Destroy(g);
        return rows;
    }

    // 9-slice 판 그림 (모서리 = border 텍스처 px). 크롬으로 찍은 판을 늘려도 모서리·테두리가 그대로
    readonly Dictionary<string, Sprite> genSliced = new Dictionary<string, Sprite>();
    Sprite GenSliced(string path, float border)
    {
        string key = path + "|" + border;
        if(genSliced.TryGetValue(key, out var sp) && sp != null) return sp;
        var src = Resources.Load<Sprite>(path);
        if(src == null) return null;
        var r = src.rect;
        float b = Mathf.Min(border, r.width / 2f - 1f, r.height / 2f - 1f);
        sp = Sprite.Create(src.texture, r, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        genSliced[key] = sp;
        return sp;
    }
    // 전체화면 창에서 화면 아래 끝 (미리보기 px, 안전 영역 위). 스크롤 목록을 여기까지 늘려요
    float GenBottomPx(GenView v, float fallback = 748f) => v != null && v.full ? v.topPx + v.hostH / (K * v.scale) : fallback;

    // 화면 안전 영역(카메라 구멍·노치·아래 막대)의 위·아래 두께 (화면 전체 높이 H 인 칸의 단위). 에디터·노치 없는 폰은 0
    static void GenSafe(float H, out float top, out float bottom)
    {
        top = bottom = 0f;
        if(Screen.height <= 0) return;
        var sa = Screen.safeArea;
        top = Mathf.Clamp((Screen.height - sa.yMax) / Screen.height * H, 0f, H * 0.2f);
        bottom = Mathf.Clamp(sa.yMin / Screen.height * H, 0f, H * 0.2f);
    }
}
