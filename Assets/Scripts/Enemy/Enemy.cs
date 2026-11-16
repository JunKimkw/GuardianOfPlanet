using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [SerializeField] public string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    [Header("적 스탯")]
    public float EnemyHealth;
    public int Enemy_EXP;
    public long enemy_Money;
    public float StunResistence;

    public enum typeofEnemy { normal, boss, Dungeon, InfiniteStage, InfiniteStageBoss, DungeonBoss, LabMonster }
    [Header("적 정보")]
    [Space(5)]
    public PoolObjectTypeEnemy typePool;
    public PoolObjectTypeDungeon typeDungeonPool;
    public typeofEnemy _typeofEnemy;
    public Vector3 pos => transform.position;
    public float enemyMovingSpeed;
    public EnemyState _state;

    [Header("추가 요소")]
    public Sprite RawImage;

    [Header("초기 스탯")]
    public InitialValues initialValue = new InitialValues();

    [Header("Slider")]
    public HealthbarBehaviour Healthbar;

    [Header("ISAttackble")]
    public bool IsInRange;

    [Header("#----X Offset")]
    public float XOffset;

    public enum EnemyState
    {
        moving,
        attacked,
        stunned,
        dead,
        reached
    }

    [Header("스폰지역")]
    public Vector3 startLocation;
    public float distance;

    [Header("채력바")]
    public float CurrentHealth;

    //Stunned: 남은 스턴 시간(초). 스턴 중에 다시 맞으면 1초로 갱신
    float stunLeft;

    // 매 프레임 GetComponents 를 부르지 않게 한 번만 찾아 둬요
    Collider2D[] colliders;
    SpriteRenderer[] sprites;

    //Range Related
    Vector3 screenPos;

    private void Awake()
    {
        colliders = GetComponents<Collider2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        initialValue.SetInitialValues(EnemyHealth, Enemy_EXP, enemy_Money, StunResistence, enemyMovingSpeed);
    }

    private void Start()
    {
        //Speed
        enemyMovingSpeed = initialValue.initialSpeed;
        //Collider and Sprites Default
        SetLocationandRoatation();
        CurrentHealth = EnemyHealth;
    }

    private void OnEnable()
    {
        ResetVisuals();
        stunLeft = 0;
        //Speed
        enemyMovingSpeed = initialValue.initialSpeed;

        //Collider and Sprites Default
        SetLocationandRoatation();
        //Default
        _state = EnemyState.moving;

        //Healthbar Loc
        if(Healthbar != null) {
            Healthbar.slider.gameObject.SetActive(false);


            Healthbar.slider.transform.position = Camera.main.WorldToScreenPoint(transform.position + Healthbar.Offset);
        }
    }

    void Update()
    {
        if(_state == EnemyState.moving) {
            if(_typeofEnemy == typeofEnemy.normal || _typeofEnemy == typeofEnemy.Dungeon || _typeofEnemy == typeofEnemy.InfiniteStage)
                transform.position = Vector3.MoveTowards(transform.position, WeaponManager.instance.mainWeapon.transform.position, Time.deltaTime * enemyMovingSpeed);
            else {
                if(Vector3.Distance(WeaponManager.instance.mainWeapon.transform.position, transform.position) > Camera.main.orthographicSize / 1.2f)
                    transform.position = Vector3.MoveTowards(transform.position, WeaponManager.instance.mainWeapon.transform.position, Time.deltaTime * enemyMovingSpeed * 10);
                else
                    transform.position = Vector3.MoveTowards(transform.position, WeaponManager.instance.mainWeapon.transform.position, Time.deltaTime * enemyMovingSpeed);

            }
        } else if(_state == EnemyState.stunned) {
            stunLeft -= Time.deltaTime;
            if(stunLeft <= 0) _state = EnemyState.moving;
        }
        distanceBetween();

        screenPos = GameManager.instance.mainCamera.WorldToScreenPoint(transform.position);

        if(_typeofEnemy == typeofEnemy.boss || _typeofEnemy == typeofEnemy.InfiniteStageBoss || _typeofEnemy == typeofEnemy.DungeonBoss) {
            IsInRange = true;
        } else {
            IsInRange = screenPos.x > 0f && screenPos.x < Screen.width && screenPos.y > 0f && screenPos.y < Screen.height;
        }
        if(Healthbar != null) {
            Healthbar.SetHealth(CurrentHealth, EnemyHealth);
            Healthbar.slider.transform.position = Camera.main.WorldToScreenPoint(transform.position + Healthbar.Offset);
        }

    }

    public void SetLocationandRoatation()
    {
        startLocation = new Vector3(this.transform.position.x, this.transform.position.y, this.transform.position.z);
        _state = EnemyState.moving;

        if(startLocation.x < 0) {
            this.gameObject.transform.rotation = Quaternion.Euler(0.0f, 180, 0.0f);
            if(Healthbar != null)
                Healthbar.Offset = new Vector3(-XOffset, Healthbar.Offset.y, Healthbar.Offset.z);
        } else {
            this.gameObject.transform.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
            if(Healthbar != null)
                Healthbar.Offset = new Vector3(0, Healthbar.Offset.y, Healthbar.Offset.z);
        }
    }

    public void SetEnemyToDefault()
    {
        EnemyHealth = initialValue.EnemyHealth;
        Enemy_EXP = initialValue.Enemy_EXP;
        enemy_Money = initialValue.enemy_Money;
        StunResistence = initialValue.StunResistence;
        enemyMovingSpeed = initialValue.initialSpeed;
        distance = 0;
        ResetVisuals();
    }

    // 충돌 켜기 + 색 원래대로 (그림자는 반투명 검정)
    void ResetVisuals()
    {
        foreach(var c in colliders) c.enabled = true;
        foreach(var r in sprites)
            r.color = r.name != "Shadow" ? Color.white : new Color(0, 0, 0, 0.62f);
        StopAllCoroutines();
    }

    public void distanceBetween()
    {
        var ship = WeaponManager.instance != null ? WeaponManager.instance.mainWeapon : null;
        if(ship != null)
            distance = Vector3.Distance(ship.transform.position, pos);
        if(distance < 0.5f && _state != EnemyState.dead) {
            shelterReached();
        }
    }

    public void shelterReached()
    {
        EnemyManager.EnemyReached(this);
    }
    public void OnHit(long DealtDamage, bool Stunned, bool Critical, float CritDamage)
    {
        // 이미 죽었거나(사라지는 중) 비활성인 적은 무시 (비활성 오브젝트에서 코루틴 시작 오류 방지)
        if(_state == EnemyState.dead || !gameObject.activeInHierarchy)
            return;

        long totalDamage = 0;
        if(Critical) 
            totalDamage = (long)(DealtDamage * (2 + CritDamage));
        else 
            totalDamage = DealtDamage;
        

        if(Stunned)
            Stunned = Random.Range(0, 101) >= StunResistence;

        if(Healthbar != null)
            Healthbar.slider.gameObject.SetActive(true);

        DamagePopup.Create(this.transform.position, totalDamage, Critical);
        SoundManager.Instance.playEnemyHit();

        //리마스터: 적중 증강 효과(분열 등)
        if(AugmentManager.Instance != null)
            AugmentManager.Instance.OnEnemyHit(this, totalDamage);

        // 오프라인 진행 계산용 초당 피해 측정 (원정 적만, 넘친 피해 제외)
        if(_typeofEnemy == typeofEnemy.InfiniteStage || _typeofEnemy == typeofEnemy.InfiniteStageBoss)
            OfflineProgress.RecordDamage(System.Math.Min(totalDamage, (double)CurrentHealth));

        //Dead
        if(CurrentHealth - totalDamage > 0) {
            CurrentHealth -= totalDamage;
            //Stun
            if(Stunned) {
                _state = EnemyState.stunned;
                stunLeft = 1f;
            }
            StartCoroutine(DamagedEffect());
        } else if(CurrentHealth - totalDamage <= 0 && _state != EnemyState.dead) {
            _state = EnemyState.dead;
            EnemyManager.EnemyDead(this);
        }
    }

    IEnumerator DamagedEffect()
    {
        SetTint(Color.red);
        yield return new WaitForSeconds(0.25f);
        SetTint(Color.white);
    }

    // 그림자(Circle)는 빼고 색 바꾸기
    void SetTint(Color c)
    {
        foreach(var r in sprites)
            if(r != null && (r.sprite == null || r.sprite.name != "Circle")) r.color = c;
    }

    public void EnemyDeath()
    {
        if(Healthbar != null)
            Healthbar.slider.gameObject.SetActive(false);

        enemyMovingSpeed = 0;
        foreach(var c in colliders) c.enabled = false;
        for(int i = 0; i < sprites.Length; i++)
            EnemyDeathEffect(sprites[i], i == sprites.Length - 1);

        SoundManager.Instance.Invoke("playEnemyDead", SoundManager.Instance.EnemyDamagedSFX.length);
    }

    public async void EnemyDeathEffect(SpriteRenderer sprite, bool isLast)
    {
        sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, 1f);
        for(float f = 1f; f >= -0.1f; f -= 0.075f) {
            if(this == null || sprite == null) return;   // 플레이 종료·파괴 후 접근 오류 방지
            sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, f);
            await Task.Delay(50);
        }
        if(this == null) return;
        if(isLast) {
            if(_typeofEnemy == typeofEnemy.Dungeon || _typeofEnemy == typeofEnemy.DungeonBoss) {
                ObjectPoolDungeon.Instance.CoolObject(gameObject, typeDungeonPool);
            } else {
                ObjectPoolEnemy.Instance.CoolObject(gameObject, typePool);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.collider.tag == "MainWeapon")
            shelterReached();
    }
}
public class InitialValues
{
    public float EnemyHealth;

    public int Enemy_EXP;

    public long enemy_Money;

    public float StunResistence;

    public float initialSpeed;

    public void SetInitialValues(float health, int exp, long money, float stun, float speed)
    {
        EnemyHealth = health;
        Enemy_EXP = exp;
        enemy_Money = money;
        StunResistence = stun;
        initialSpeed = speed;
    }
}
