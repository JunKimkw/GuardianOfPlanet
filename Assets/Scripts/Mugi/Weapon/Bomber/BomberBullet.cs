using System.Collections;
using UnityEngine;

// 폭탄: 처음 닿은 대상에서 멈추고 1초 동안 폭발(범위 안 적은 95% 피해)
public class BomberBullet : Bullet
{
    [HideInInspector] public bool hasScaled;
    [HideInInspector] public int Bullet_Damage;

    public SpriteRenderer sprite;
    public Sprite DefaultSprite;
    public Animator ExplosionEffect;
    public CircleCollider2D circleCollider;

    bool isFirst;
    bool changeRadius;
    Rigidbody2D rb;

    protected override PoolObjectTypeBullet PoolType => PoolObjectTypeBullet.Bomber;
    protected override TraitsData.WeaponType TraitType => TraitsData.WeaponType.Bomber;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        changeRadius = false;
        rb.constraints = RigidbodyConstraints2D.None;
        ExplosionEffect.SetBool("isMoving", true);
        isFirst = false;
        circleCollider.radius = 1.1f;
    }

    void OnDisable()
    {
        // 폭발 도중 풀로 돌아가도 크기가 커진 채로 남지 않게
        if(hasScaled) {
            transform.localScale /= 1.5f;
            hasScaled = false;
        }
    }

    void Update()
    {
        if(!isFirst) MoveForward();
        if(IsOffScreen()) DestroyBullet();
    }

    void LateUpdate()
    {
        if(changeRadius) circleCollider.radius = 10f;
    }

    public void setBulletStats(int a) => Bullet_Damage = a;

    public IEnumerator PlayExplosionEffect()
    {
        SoundManager.Instance.playWeaponSFX(SoundManager.WeaponSFXType.BomberExplosion);

        // 패시브: 폭발 범위 1.5배
        if(Bomber.instance.ExploseRangeIncrease && !hasScaled) {
            transform.localScale *= 1.5f;
            hasScaled = true;
        }

        changeRadius = true;
        ExplosionEffect.SetBool("isMoving", false);
        yield return new WaitForSeconds(1);
        DestroyBullet();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if(!isFirst) {
            if(Hit(collision.gameObject, Bullet_Damage, PlayerCritDamage)) {
                isFirst = true;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
                StartCoroutine(PlayExplosionEffect());
            }
        } else {
            Hit(collision.gameObject, (int)(Bullet_Damage * 0.95f), PlayerCritDamage);
        }
    }
}
