using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public static DamagePopup Create(Vector3 position, double damageAmount, bool isCriticalHit)
    {
        if(!RemasterUI.ShowDamageText) return null;   // 설정 "데미지 숫자 보기" 끔 (2026-11-26)
        GameObject damagepopupTransform = ObjectPoolPopUp.Instance.GetPoolObject(PoolObjectTypePopUp.Damage);
        damagepopupTransform.transform.position = Spread(position);
        damagepopupTransform.SetActive(true);
        DamagePopup damagepopup = damagepopupTransform.GetComponent<DamagePopup>();
        damagepopup.SetUp(damageAmount, isCriticalHit);

        return damagepopup;
    }
    public static DamagePopup Create(Vector3 position, double damageAmount , bool isCriticalHit, Color c)
    {
        if(!RemasterUI.ShowDamageText) return null;   // 설정 "데미지 숫자 보기" 끔 (2026-11-26)
        GameObject damagepopupTransform = ObjectPoolPopUp.Instance.GetPoolObject(PoolObjectTypePopUp.Damage);
        damagepopupTransform.transform.position = Spread(position);
        damagepopupTransform.SetActive(true);
        damagepopupTransform.GetComponent<TextMeshPro>().color = c;
        DamagePopup damagepopup = damagepopupTransform.GetComponent<DamagePopup>();
        damagepopup.SetUp(damageAmount, isCriticalHit);

        return damagepopup;
    }

    // 2026-11-22 사용자: "데미지가 중첩되면 너무 겹쳐 있어, 약간 띄움이 필요해"
    //  같은 자리(popup_stack_radius 칸 안)에 popup_stack_time 초 안에 연달아 뜨면 위로 한 칸(popup_stack_step)씩 쌓아요 (popup_stack_max 칸에서 다시 아래부터).
    //  좌우로도 살짝(popup_jitter) 흩고, 날아가는 방향도 좌우로 섞어요. 예전엔 모두 적 중심 한 점에서 같은 방향으로 움직여 완전히 포개졌어요
    static readonly List<(Vector3 p, float t)> recent = new List<(Vector3, float)>();

    static Vector3 Spread(Vector3 p)
    {
        float now = Time.time, win = Tuning.Get("popup_stack_time", 0.45f), r = Tuning.Get("popup_stack_radius", 0.4f);
        recent.RemoveAll(x => now - x.t > win);
        int n = 0;
        foreach(var x in recent) if(((Vector2)(x.p - p)).sqrMagnitude <= r * r) n++;
        recent.Add((p, now));   // 원래 자리로 기억해요 (같은 적끼리 묶이게)
        if(recent.Count > 300) recent.RemoveAt(0);
        int max = Mathf.Max(1, Tuning.GetInt("popup_stack_max", 4));
        float step = Tuning.Get("popup_stack_step", 0.12f), jit = Tuning.Get("popup_jitter", 0.1f);
        return p + new Vector3(Random.Range(-jit, jit), (n % max) * step + Random.Range(0f, jit * 0.5f), 0f);
    }

    private static int sortingOrder;

    private const float DISAPPEAR_TIMER_MAX = 1f;

    private TextMeshPro textmesh;
    private float disappearTimer;
    private Color textColor;
    private Vector3 moveVector;

    private void Awake()
    {
        textmesh = transform.GetComponent<TextMeshPro>();
    }

    public void SetUp(double damageAmount, bool isCritical)
    {
        // 우주 단위 표시 (10만부터 Sk, Cm, Mn …) + 단위가 오를수록 색이 바뀌어요
        textmesh.SetText(BigNumber.Format(damageAmount));
        Color tierColor = BigNumber.TierColor(damageAmount);
        transform.localScale = Vector3.one * ScreenSize.GetScreenToWorldWidth / 6f;
        if(!isCritical) {
            //NormalHit
            textmesh.fontSize = 2.1f;
            textColor = tierColor;
        } else {
            //CritHit
            textmesh.fontSize = 3f;
            textColor = Color.Lerp(tierColor, new Color(1, 0.1686275f, 0), 0.6f);
            
        }
        textmesh.color = textColor;
        disappearTimer = DISAPPEAR_TIMER_MAX;

        sortingOrder++;
        textmesh.sortingOrder = sortingOrder;
        moveVector = new Vector3(Random.Range(-0.7f, 0.7f), 1) * 0.6f;   // 좌우로 섞어요 (예전엔 모두 오른쪽 위)
    }

    private void Update()
    {
        transform.position += moveVector * Time.deltaTime;
        moveVector -= moveVector * 2.2f * Time.deltaTime;


        if(disappearTimer > DISAPPEAR_TIMER_MAX * .5f) {
            //FirstHalf of the popup lifetime
            float increaseScaleAmount = 0.2f;
            transform.localScale += Vector3.one * increaseScaleAmount * Time.deltaTime;
        } else {
            //secondHalf of the popup lifetime
            float decreaseScaleAmount = 1f;
            transform.localScale -= Vector3.one * decreaseScaleAmount * Time.deltaTime;
        }
        disappearTimer -= Time.deltaTime;
        if(disappearTimer < 0) {
            float disappearSpeed = 3f;
            textColor.a -= disappearSpeed * Time.deltaTime;
            textmesh.color = textColor;

            if(textColor.a < 0) {
                ObjectPoolPopUp.Instance.CoolObject(this.gameObject, PoolObjectTypePopUp.Damage);
            }
        }
    }
}
