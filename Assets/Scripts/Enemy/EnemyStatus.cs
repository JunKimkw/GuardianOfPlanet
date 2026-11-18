using UnityEngine;

// 적 상태이상 (증강: 독·둔화·빙결·기절). Enemy.cs 와 같은 클래스예요.
public partial class Enemy
{
    [System.NonSerialized] public int PoisonStacks;
    [System.NonSerialized] public float PoisonLeft;
    [System.NonSerialized] public float SlowAmount, SlowLeft;
    [System.NonSerialized] public bool Frozen;
    float poisonTimer;

    const int MaxPoisonStacks = 30;
    static readonly Color IceTint = new Color(0.6f, 0.9f, 1f);

    public bool IsBoss => _typeofEnemy == typeofEnemy.InfiniteStageBoss || _typeofEnemy == typeofEnemy.boss || _typeofEnemy == typeofEnemy.DungeonBoss;
    public bool IsPoisoned => PoisonStacks > 0 && PoisonLeft > 0;

    void ClearStatus()
    {
        PoisonStacks = 0;
        PoisonLeft = SlowLeft = SlowAmount = poisonTimer = 0;
        Frozen = false;
    }

    public void AddPoison(int stacks, float seconds)
    {
        if(_state == EnemyState.dead) return;
        PoisonStacks = Mathf.Min(MaxPoisonStacks, PoisonStacks + stacks);
        PoisonLeft = Mathf.Max(PoisonLeft, seconds);
    }

    public void AddSlow(float amount, float seconds)
    {
        if(_state == EnemyState.dead) return;
        SlowAmount = Mathf.Clamp(Mathf.Max(SlowLeft > 0 ? SlowAmount : 0f, amount), 0f, 0.8f);
        SlowLeft = Mathf.Max(SlowLeft, seconds);
    }

    // 기절: 움직이지 않아요 (더 긴 쪽이 남아요)
    public void Stun(float seconds)
    {
        if(_state == EnemyState.dead || _state == EnemyState.reached) return;
        _state = EnemyState.stunned;
        stunLeft = Mathf.Max(stunLeft, seconds);
    }

    public void Freeze(float seconds)
    {
        if(_state == EnemyState.dead || _state == EnemyState.reached) return;
        Stun(seconds);
        Frozen = true;
        SetTint(IceTint);
    }

    // 증강 효과로 바로 처치 (처형, 표식, 역병, 방벽)
    public void ForceKill()
    {
        if(_state == EnemyState.dead || !gameObject.activeInHierarchy) return;
        if(AugmentManager.DebugTestMode) { DamagePopup.Create(transform.position, CurrentHealth, true); return; }   // 테스트 모드: 즉사 대신 숫자만
        double left = CurrentHealth;
        CurrentHealth = 0;
        _state = EnemyState.dead;
        DamagePopup.Create(transform.position, left, true);
        EnemyManager.EnemyDead(this);
    }

    // Update 에서 불러요: 독 틱, 둔화 시간
    void TickStatus(float dt)
    {
        if(SlowLeft > 0) SlowLeft -= dt;
        if(PoisonLeft > 0) {
            PoisonLeft -= dt;
            poisonTimer += dt;
            if(poisonTimer >= 0.5f) {
                poisonTimer -= 0.5f;
                if(AugmentManager.Instance != null) AugmentManager.Instance.PoisonTick(this);
            }
            if(PoisonLeft <= 0) PoisonStacks = 0;
        }
    }

    // 기절이 풀릴 때 얼어 있었으면 녹으면서 (빙하 폭발)
    void OnStunEnded()
    {
        if(!Frozen) return;
        Frozen = false;
        SetTint(baseTint);
        if(AugmentManager.Instance != null) AugmentManager.Instance.OnThaw(this);
    }

    float MoveFactor => AugmentManager.Instance != null ? AugmentManager.Instance.MoveFactor(this) : 1f;
}
