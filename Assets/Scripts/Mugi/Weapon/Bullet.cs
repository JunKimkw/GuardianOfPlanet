using UnityEngine;

// 모든 탄의 공통 부모: 이동, 화면 밖 정리, 풀 반납, 적/더미 타격.
public abstract class Bullet : MonoBehaviour
{
    [HideInInspector] public bool isCritical;
    [HideInInspector] public bool isStunning;
    [HideInInspector] public int edgeBounces;   // 증강 "반사 파편"으로 튕긴 횟수 (풀에서 꺼낼 때 0)
    public float BulletSpeed;

    protected abstract PoolObjectTypeBullet PoolType { get; }
    protected abstract TraitsData.WeaponType TraitType { get; }

    // 화면 밖으로 이만큼 더 나가면 반납
    protected virtual float OffScreenMargin => 0f;

    protected float SpeedMultiplier =>
        (1 + Player.instance.FinalAttack_Bullet_SpeedPercent) *
        (1 + TraitsManager.instance.GetStats(TraitType, TraitsData.TraitsType.BulletSpeed));

    Rigidbody2D body;
    bool bodyChecked;

    protected void MoveForward(float extraScale = 0f)
    {
        // 코드로 움직이는 탄은 물리엔진이 밀지 못하게 Kinematic 으로 바꿔요 (충돌 알림은 그대로 받아요).
        // 예전: 질량 0.0001 의 Dynamic 이라 적 충돌체와 조금만 겹쳐도 엄청난 속도로 튕겨 나갔어요
        // (갈라진 탄·관통탄이 순간적으로 엄청 빨라지거나 휘던 문제)
        if(!bodyChecked) {
            bodyChecked = true;
            body = GetComponent<Rigidbody2D>();
            if(body != null && body.bodyType == RigidbodyType2D.Dynamic) {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.useFullKinematicContacts = true;   // 적(움직이지 않는 충돌체)과 닿아도 OnCollisionEnter2D 가 오게
            }
        }
        if(body != null) {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        transform.Translate(Vector3.up * BulletSpeed * SpeedMultiplier * (1 + extraScale) * Time.deltaTime);
    }

    protected bool IsOffScreen()
    {
        Camera cam = GameManager.instance.mainCamera;
        float h = cam.orthographicSize + OffScreenMargin;
        float w = cam.orthographicSize * cam.aspect + OffScreenMargin;
        Vector3 p = transform.position;
        bool outX = p.x > w || p.x < -w, outY = p.y > h || p.y < -h;
        if(!outX && !outY) return false;
        // 증강 "반사 파편": 화면 끝에서 튕겨 돌아와요
        if(edgeBounces < AugmentManager.RicochetCount) {
            edgeBounces++;
            Vector3 dir = transform.up;
            if(outX) dir.x = -dir.x;
            if(outY) dir.y = -dir.y;
            transform.position = new Vector3(Mathf.Clamp(p.x, -w, w), Mathf.Clamp(p.y, -h, h), p.z);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            return false;
        }
        return true;
    }

    public virtual void DestroyBullet() => ObjectPoolBullet.Instance.CoolObject(gameObject, PoolType);

    // 적이나 더미를 맞혔으면 true
    protected bool Hit(GameObject target, long damage, float critDamage)
    {
        if(target.TryGetComponent(out Enemy enemy)) {
            enemy.OnHit(damage, isStunning, isCritical, critDamage, Armor.Pen(TraitType));
            return true;
        }
        if(target.TryGetComponent(out Dummy dummy)) {
            dummy.addDamage(damage, isCritical, critDamage);
            return true;
        }
        return false;
    }

    protected static float PlayerCritDamage => Player.instance.FinalCriticalDamage;
}
