using System.Collections.Generic;
using UnityEngine;

// 필드 위 적 목록 관리: 생성(풀), 가장 가까운 적 찾기, 처치·도착 처리, 처치 기록 저장.
public class EnemyManager : MonoBehaviour, IDataPersistence
{
    public static EnemyManager Instance;

    [Header("적")]
    [SerializeField] public List<Enemy> enemyData;
    public Enemy[] BossEnemies;
    [SerializeField] public List<Enemy> bossData;
    public int enemyKilled_Number;
    public List<int> bossKilled_number;
    public int totalBossKilled;

    public long totalKilledEnemy;

    public Transform TopEnemySpawnLoc;

    void Awake()
    {
        // 예전엔 Start 에서 등록해서, 다른 스크립트의 Start 가 먼저 돌면 null 이었어요.
        Instance = this;
        // 보스별 처치 수 (예전엔 0이 아니라 0,1,2… 로 채워졌어요)
        bossKilled_number = new List<int>(new int[BossEnemies.Length]);
    }

    // ---- 찾기
    static bool IsBossType(Enemy e) =>
        e._typeofEnemy == Enemy.typeofEnemy.boss || e._typeofEnemy == Enemy.typeofEnemy.DungeonBoss || e._typeofEnemy == Enemy.typeofEnemy.InfiniteStageBoss;

    // 가장 가까운 적 (화면 안). 보스를 노리는 중이어도 거리 3 안에 들어온 일반 적을 먼저 노려요.
    Enemy FindClose(System.Predicate<Enemy> allow)
    {
        Enemy close = null;
        float distance = 100000;
        foreach(var b in bossData) {
            if(b.IsInRange && distance >= b.distance && allow(b)) {
                distance = b.distance;
                close = b;
            }
        }
        foreach(var e in enemyData) {
            if(!e.IsInRange || distance < e.distance || !allow(e)) continue;
            if(close == null || !IsBossType(close) || e.distance <= 3) {
                close = e;
                distance = e.distance;
            }
        }
        return close;
    }

    public Enemy FindCloseEnemy() => FindClose(_ => true);

    // 튕김탄용: 이미 맞힌 적은 빼고 찾아서 목록에 넣어요
    public Enemy FindCloseEnemy(BounceEnemy bounceEnemy)
    {
        Enemy close = FindClose(e => !bounceEnemy.checkBouncedEnemyisInList(e));
        if(close != null) bounceEnemy.BouncedEnemies.Add(close);
        return close;
    }

    // 화면 밖 포함, 보스 우선
    public Enemy FindCloseEnemyWithoutRange()
    {
        Enemy close = null;
        float distance = 100000;
        foreach(var b in bossData)
            if(distance >= b.distance) { distance = b.distance; close = b; }
        if(close != null) return close;
        foreach(var e in enemyData)
            if(distance >= e.distance) { distance = e.distance; close = e; }
        return close;
    }

    // 반경 1 안에 적이 가장 많이 모인 적 (같으면 더 가까운 쪽)
    public Enemy FindCrowdedEnemy()
    {
        Enemy best = null;
        int max = 0;
        for(int i = 0; i < enemyData.Count; i++) {
            Vector2 p = enemyData[i].transform.position;
            int count = 0;
            for(int j = 0; j < enemyData.Count; j++)
                if(((Vector2)enemyData[j].transform.position - p).sqrMagnitude <= 1f) count++;

            if(count > max || (count == max && best != null && best.distance >= enemyData[i].distance)) {
                max = count;
                best = enemyData[i];
            }
        }
        return best;
    }

    // ---- 생성
    // 풀에서 꺼내 화면 가장자리에 배치. side: 1 왼쪽, 2 위, 3 오른쪽, 4 아래
    Enemy Spawn(Enemy data, Enemy.typeofEnemy type, bool dungeonPool, int side)
    {
        GameObject go = dungeonPool
            ? ObjectPoolDungeon.Instance.GetPoolObject(data.typeDungeonPool)
            : ObjectPoolEnemy.Instance.GetPoolObject(data.typePool);
        Enemy enemy = go.GetComponent<Enemy>();
        enemy._typeofEnemy = type;
        enemy.transform.position = RandomLocation(side);
        enemy.gameObject.SetActive(true);
        return enemy;
    }

    static int RandomSide() => Random.Range(1, 5);
    static int RandomBossSide() => Random.Range(1, 3) * 2;   // 위 또는 아래

    // 예전 방식 스케일 (일반 스테이지·던전)
    static void ApplyScale(Enemy e, float scale, bool speed)
    {
        e.EnemyHealth *= (1 + scale);
        e.enemy_Money = (long)(e.enemy_Money * (1 + scale / 5));
        e.Enemy_EXP = (int)(e.Enemy_EXP * (1 + scale / 5));
        if(speed) {
            e.enemyMovingSpeed *= (1 + scale / 50);
            e.StunResistence *= (1 + scale);
        }
    }

    void AddEnemy(Enemy e)
    {
        e.CurrentHealth = e.EnemyHealth;
        enemyData.Add(e);
    }

    void AddBoss(Enemy e)
    {
        e.CurrentHealth = e.EnemyHealth;
        bossData.Add(e);
    }

    public void createEnemy(Enemy _e) => AddEnemy(Spawn(_e, Enemy.typeofEnemy.normal, false, RandomSide()));

    public void createEnemy(Enemy _e, float scale)
    {
        Enemy e = Spawn(_e, Enemy.typeofEnemy.normal, false, RandomSide());
        ApplyScale(e, scale, true);
        AddEnemy(e);
    }

    public void CreateBoss(Enemy _e, float scale)
    {
        Enemy e = Spawn(_e, Enemy.typeofEnemy.boss, false, RandomBossSide());
        ApplyScale(e, scale, false);
        AddBoss(e);
    }

    public void createInfiniteEnemy(Enemy _e, float scale)
    {
        Enemy e = Spawn(_e, Enemy.typeofEnemy.InfiniteStage, false, RandomSide());
        if(InfiniteStage.Instance != null) {
            // 리마스터 원정: 체력·골드·경험치는 RemasterBalance 곡선 (적 종류마다 ±10% 차이)
            int stage = InfiniteStage.Instance.CurrentStage;
            float variety = 0.9f + 0.2f * (((int)_e.typePool & 0xff) / 255f);
            e.NumberScale = RemasterBalance.NumberScale(stage);
            e.EnemyHealth = RemasterBalance.EnemyHp(stage) * variety * ExpeditionZones.HpMul(stage) * e.NumberScale;
            e.enemy_Money = (long)System.Math.Max(1, RemasterBalance.EnemyGold(stage) * ExpeditionZones.GoldMul(stage));
            e.Enemy_EXP = (int)System.Math.Min(int.MaxValue, System.Math.Max(1, RemasterBalance.EnemyExp(stage)));
        } else {
            ApplyScale(e, scale, false);
        }
        e.enemyMovingSpeed *= (1 + scale / 50);
        if(AugmentManager.Instance != null)
            e.enemyMovingSpeed *= AugmentManager.Instance.EnemySpeedMultiplier;
        if(InfiniteStage.Instance != null) {
            int stage = InfiniteStage.Instance.CurrentStage;
            e.enemyMovingSpeed *= ExpeditionZones.SpeedMul(stage);
            e.ApplyLook(ExpeditionZones.StageTint(stage), 1f);
        }
        e.StunResistence *= (1 + scale);
        AddEnemy(e);
    }

    public void CreateInfiniteBoss(Enemy _e, float scale)
    {
        Enemy e = Spawn(_e, Enemy.typeofEnemy.InfiniteStageBoss, false, RandomBossSide());
        if(InfiniteStage.Instance != null) {
            // 리마스터 원정: 10스테이지 보스 ×8, 50스테이지 신의 관문 ×15
            int stage = InfiniteStage.Instance.CurrentStage;
            e.NumberScale = RemasterBalance.NumberScale(stage);
            e.EnemyHealth = RemasterBalance.BossHp(stage) * ExpeditionZones.HpMul(stage) * e.NumberScale;
            e.enemy_Money = (long)System.Math.Max(1, RemasterBalance.EnemyGold(stage) * 20 * ExpeditionZones.GoldMul(stage));
            // 차원 색조 + 관문 보스 각성(차원 2 = ★1, 15%씩 커져요)
            float awaken = ExpeditionZones.IsGate(stage) ? 1f + 0.15f * ExpeditionZones.Awakening(stage) : 1f;
            e.ApplyLook(ExpeditionZones.StageTint(stage), awaken);
            // 신의 관문 보스: 돌진·소환·보호막 패턴
            if(ExpeditionZones.IsGate(stage)) {
                if(!e.TryGetComponent(out GateBossAbilities gate)) gate = e.gameObject.AddComponent<GateBossAbilities>();
                gate.Setup(ExpeditionZones.Zone(stage), ExpeditionZones.Awakening(stage));
            }
            e.Enemy_EXP = (int)System.Math.Min(int.MaxValue, System.Math.Max(1, RemasterBalance.EnemyExp(stage) * 20));
        } else {
            ApplyScale(e, scale, false);
        }
        AddBoss(e);
    }

    public void CreateDungeonEnemy(Enemy _e, Enemy.typeofEnemy type)
    {
        Enemy e = Spawn(_e, type, true, RandomSide());
        enemyData.Add(e);   // 던전 적 체력은 DungeonManager 가 정해요
    }

    public void CreateLabMonster(Enemy data)
    {
        bool dungeon = data._typeofEnemy == Enemy.typeofEnemy.Dungeon || data._typeofEnemy == Enemy.typeofEnemy.DungeonBoss;
        AddEnemy(Spawn(data, Enemy.typeofEnemy.LabMonster, dungeon, RandomSide()));
    }

    public Vector3 RandomLocation(int side)
    {
        Camera cam = Camera.main;
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        switch(side) {
            case 1: return new Vector3(-(w + 0.5f), Random.Range(-h, h), 0);                                           // 왼쪽
            case 2: return new Vector3(Random.Range(-w, w), cam.ScreenToWorldPoint(TopEnemySpawnLoc.position).y + 1f, 0); // 위
            case 3: return new Vector3(w + 0.5f, Random.Range(-h, h), 0);                                              // 오른쪽
            default: return new Vector3(Random.Range(-w, w), -(h + 1f), 0);                                           // 아래
        }
    }

    // ---- 처치
    public static void EnemyDead(Enemy enemy)
    {
        //리마스터: 증강 처치 효과(폭발, 누적 스택)
        if(AugmentManager.Instance != null)
            AugmentManager.Instance.OnEnemyKilled(enemy);

        var type = enemy._typeofEnemy;
        bool rewards = type == Enemy.typeofEnemy.normal || type == Enemy.typeofEnemy.boss
                    || type == Enemy.typeofEnemy.InfiniteStage || type == Enemy.typeofEnemy.InfiniteStageBoss;
        long gold = rewards ? GameManager.GetTotalGoldOfEnemy(enemy) : 0;
        int exp = rewards ? GameManager.GetTotalExpOfEnemy(enemy) : 0;
        if(rewards) {
            MoneyPopup.Create(enemy.transform.position, gold, 2.5f);
            CollectionManager.Instance.addEnemyKilled(enemy);
        }

        switch(type) {
            case Enemy.typeofEnemy.normal:
                StageManager.EnemyDeadNormal(exp, gold);
                Instance.CountNormalKill();
                break;
            case Enemy.typeofEnemy.boss:
                StageManager.EnemyDeadBoss(exp, gold);
                Instance.AddBossKilled(enemy);
                Instance.CountBossKill();
                break;
            case Enemy.typeofEnemy.InfiniteStage:
                InfiniteStage.EnemyDeadNormal(exp, gold);
                Instance.CountNormalKill();
                break;
            case Enemy.typeofEnemy.InfiniteStageBoss:
                InfiniteStage.EnemyDeadBoss(exp, gold);
                Instance.CountBossKill();
                break;
            case Enemy.typeofEnemy.Dungeon:
                DungeonManager.EnemyDeadNormal();
                break;
            case Enemy.typeofEnemy.DungeonBoss:
                DungeonManager.EnemyDeadBoss();
                break;
        }

        if(type != Enemy.typeofEnemy.LabMonster)
            Instance.totalKilledEnemy++;

        enemy.SetEnemyToDefault();
        enemy.EnemyDeath();
        Instance.enemyData.Remove(enemy);
        Instance.bossData.Remove(enemy);
    }

    void CountNormalKill()
    {
        enemyKilled_Number++;
        AchievementManager.instance.killedEnemyDaily++;
        AchievementManager.instance.killedEnemyWeekly++;
    }

    void CountBossKill()
    {
        totalBossKilled++;
        AchievementManager.instance.killedBossDaily++;
        AchievementManager.instance.killedBossWeekly++;
    }

    // ---- 우주선 도착
    public static void EnemyReached(Enemy enemy)
    {
        switch(enemy._typeofEnemy) {
            case Enemy.typeofEnemy.normal:
            case Enemy.typeofEnemy.boss:
                MoneyPopup.Create(enemy.transform.position, (-enemy.enemy_Money / 5) + 1, 2.5f);
                StageManager.instance.Died();
                break;
            case Enemy.typeofEnemy.InfiniteStage:
            case Enemy.typeofEnemy.InfiniteStageBoss:
                // 예전엔 원정 보스가 도착하면 아무 처리도 안 돼서, 우주선 위에 멈춘 채 매 프레임 이 함수를 불렀어요
                InfiniteStage.Instance.enemyHit(enemy);
                break;
            case Enemy.typeofEnemy.Dungeon:
            case Enemy.typeofEnemy.DungeonBoss:
                DungeonManager.EnemyHit();
                break;
        }

        enemy.SetEnemyToDefault();
        Instance.ReturnToPool(enemy);
        Instance.enemyData.Remove(enemy);
        Instance.bossData.Remove(enemy);
    }

    void ReturnToPool(Enemy e)
    {
        bool dungeon = e._typeofEnemy == Enemy.typeofEnemy.Dungeon || e._typeofEnemy == Enemy.typeofEnemy.DungeonBoss
                    || (e._typeofEnemy == Enemy.typeofEnemy.LabMonster && e.typePool == PoolObjectTypeEnemy.NONE);
        if(dungeon) ObjectPoolDungeon.Instance.CoolObject(e.gameObject, e.typeDungeonPool);
        else ObjectPoolEnemy.Instance.CoolObject(e.gameObject, e.typePool);
    }

    public void removeAllEnemies()
    {
        foreach(var e in enemyData) { e.SetEnemyToDefault(); ObjectPoolEnemy.Instance.CoolObject(e.gameObject, e.typePool); }
        foreach(var e in bossData) { e.SetEnemyToDefault(); ObjectPoolEnemy.Instance.CoolObject(e.gameObject, e.typePool); }
        enemyData.Clear();
        bossData.Clear();
    }

    public void removeAllDungeonEnemies()
    {
        foreach(var e in enemyData) { e.SetEnemyToDefault(); ObjectPoolDungeon.Instance.CoolObject(e.gameObject, e.typeDungeonPool); }
        foreach(var e in bossData) { e.SetEnemyToDefault(); ObjectPoolDungeon.Instance.CoolObject(e.gameObject, e.typeDungeonPool); }
        enemyData.Clear();
        bossData.Clear();
    }

    // 보스 종류별 처치 수 (예전엔 "필드 위 보스 목록"의 번호로 세서 항상 첫 보스만 올라갔어요)
    public void AddBossKilled(Enemy enemy)
    {
        for(int i = 0; i < BossEnemies.Length; i++) {
            if(BossEnemies[i].id == enemy.id) {
                bossKilled_number[i]++;
                return;
            }
        }
    }

    public bool getEnemyOnField() => false;

    public void LoadData(GameData data)
    {
        enemyKilled_Number = data.enemyKilled_Number;
        for(int i = 0; i < BossEnemies.Length; i++) {
            data.bossKilled_Number.TryGetValue(BossEnemies[i].id, out int amount);
            bossKilled_number[i] = amount;
        }
        totalBossKilled = data.totalBossKill;
        totalKilledEnemy = data.totalKilledEnemy;
    }

    public void SaveData(GameData data)
    {
        data.enemyKilled_Number = enemyKilled_Number;
        for(int i = 0; i < BossEnemies.Length; i++)
            data.bossKilled_Number[BossEnemies[i].id] = bossKilled_number[i];
        data.totalBossKill = totalBossKilled;
        data.totalKilledEnemy = totalKilledEnemy;
    }
}
