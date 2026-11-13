using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 증강 효과 연출 (폭발 원, 충격파, 파편 선). 그림 파일 없이 코드로 만든 원 텍스처를 써요.
public class RemasterFx : MonoBehaviour
{
    static RemasterFx inst;
    static Sprite circle, ring;
    static Material lineMat;
    readonly Stack<SpriteRenderer> spritePool = new Stack<SpriteRenderer>();
    readonly Stack<LineRenderer> linePool = new Stack<LineRenderer>();
    int activeBursts;

    static RemasterFx Get()
    {
        if(inst == null) {
            var go = new GameObject("RemasterFx");
            inst = go.AddComponent<RemasterFx>();
        }
        return inst;
    }

    public static Sprite CircleSprite {
        get {
            if(circle == null) circle = MakeCircle(128, false);
            return circle;
        }
    }

    static Sprite RingSprite {
        get {
            if(ring == null) ring = MakeCircle(128, true);
            return ring;
        }
    }

    static Sprite MakeCircle(int size, bool ringOnly)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size / 2f;
        var px = new Color32[size * size];
        for(int y = 0; y < size; y++) {
            for(int x = 0; x < size; x++) {
                float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                float a;
                if(ringOnly) a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.12f);
                else a = Mathf.Clamp01((1f - d) * 6f) * Mathf.Lerp(0.35f, 1f, d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    SpriteRenderer TakeSprite()
    {
        SpriteRenderer sr = spritePool.Count > 0 ? spritePool.Pop() : null;
        if(sr == null) {
            var go = new GameObject("fx");
            go.transform.SetParent(transform);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 500;
        }
        sr.gameObject.SetActive(true);
        return sr;
    }

    // 폭발: 반경 radius(월드 단위)의 원이 퍼지며 사라짐
    public static void Burst(Vector3 pos, float radius, Color color)
    {
        var fx = Get();
        if(fx.activeBursts > 60) return; // 너무 많으면 연출만 생략 (피해는 그대로)
        fx.StartCoroutine(fx.BurstRoutine(pos, radius, color, CircleSprite, 0.28f));
    }

    public static void Shockwave(Vector3 pos)
    {
        var fx = Get();
        fx.StartCoroutine(fx.BurstRoutine(pos, 12f, new Color(1f, 0.9f, 0.6f), RingSprite, 0.6f));
        fx.StartCoroutine(fx.BurstRoutine(pos, 8f, new Color(1f, 0.6f, 0.3f), CircleSprite, 0.45f));
    }

    IEnumerator BurstRoutine(Vector3 pos, float radius, Color color, Sprite sprite, float duration)
    {
        activeBursts++;
        var sr = TakeSprite();
        sr.sprite = sprite;
        sr.transform.position = new Vector3(pos.x, pos.y, 0f);
        float t = 0;
        while(t < duration) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(0.3f, 1f, 1f - (1f - k) * (1f - k)) * radius * 2f;
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.color = new Color(color.r, color.g, color.b, (1f - k) * 0.85f);
            yield return null;
        }
        sr.gameObject.SetActive(false);
        spritePool.Push(sr);
        activeBursts--;
    }

    public static void Line(Vector3 from, Vector3 to, Color color)
    {
        var fx = Get();
        fx.StartCoroutine(fx.LineRoutine(from, to, color));
    }

    IEnumerator LineRoutine(Vector3 from, Vector3 to, Color color)
    {
        LineRenderer lr = linePool.Count > 0 ? linePool.Pop() : null;
        if(lr == null) {
            var go = new GameObject("fxLine");
            go.transform.SetParent(transform);
            lr = go.AddComponent<LineRenderer>();
            if(lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
            lr.material = lineMat;
            lr.positionCount = 2;
            lr.sortingOrder = 501;
            lr.useWorldSpace = true;
        }
        lr.gameObject.SetActive(true);
        lr.SetPosition(0, new Vector3(from.x, from.y, 0));
        lr.SetPosition(1, new Vector3(to.x, to.y, 0));
        float t = 0, dur = 0.18f;
        while(t < dur) {
            t += Time.deltaTime;
            float a = 1f - t / dur;
            lr.startColor = new Color(1f, 1f, 1f, a);
            lr.endColor = new Color(color.r, color.g, color.b, a);
            lr.widthMultiplier = 0.08f * a + 0.02f;
            yield return null;
        }
        lr.gameObject.SetActive(false);
        linePool.Push(lr);
    }
}
