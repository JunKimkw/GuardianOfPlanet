using UnityEngine;

// 모든 탄의 공통 부모: 이동, 화면 밖 정리, 풀 반납, 적/더미 타격.
public abstract class Bullet : MonoBehaviour
{
    [HideInInspector] public bool isCritical;
    [HideInInspector] public bool isStunning;
    public float BulletSpeed;

    protected abstract PoolObjectTypeBullet PoolType { get; }
    protected abstract TraitsData.WeaponType TraitType { get; }

    // 화면 밖으로 이만큼 더 나가면 반납
    protected virtual float OffScreenMargin => 0f;

    protected float SpeedMultiplier =>
        (1 + Player.instance.FinalAttack_Bullet_SpeedPercent) *
        (1 + TraitsManager.instance.GetStats(TraitType, TraitsData.TraitsType.BulletSpeed));

    protected void MoveForward(float extraScale = 0f)
    {
        transform.Translate(Vector3.up * BulletSpeed * SpeedMultiplier * (1 + extraScale) * Time.deltaTime);
    }

    protected bool IsOffScreen()
    {
        Camera cam = GameManager.instance.mainCamera;
        float h = cam.orthographicSize + OffScreenMargin;
        float w = cam.orthographicSize * cam.aspect + OffScreenMargin;
        Vector3 p = transform.position;
        return p.y > h || p.y < -h || p.x > w || p.x < -w;
    }

    public virtual void DestroyBullet() => ObjectPoolBullet.Instance.CoolObject(gameObject, PoolType);

    // 적이나 더미를 맞혔으면 true
    protected bool Hit(GameObject target, long damage, float critDamage)
    {
        if(target.TryGetComponent(out Enemy enemy)) {
            enemy.OnHit(damage, isStunning, isCritical, critDamage);
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
