using System.Collections.Generic;
using UnityEngine;

// 신의 관문 보스 패턴 (설계서 v3 2-2): 돌진, 소환, 보호막.
// 구간마다 2개를 조합하고, 차원 각성(★)이 있으면 3개 모두 + 더 자주 써요.
// 관문 보스로 나올 때만 켜지고, 풀로 돌아가면 꺼져요(Enemy.SetEnemyToDefault).
[RequireComponent(typeof(Enemy))]
public class GateBossAbilities : MonoBehaviour
{
    public enum Pattern { Dash, Summon, Shield }

    static readonly Pattern[][] ZoneSets = {
        new[] { Pattern.Dash, Pattern.Summon },
        new[] { Pattern.Shield, Pattern.Dash },
        new[] { Pattern.Summon, Pattern.Shield },
    };

    Enemy enemy;
    readonly List<Pattern> patterns = new List<Pattern>();
    int next;
    float cooldown;
    float timer;
    float effectLeft;
    Pattern active;
    float baseSpeed;

    public void Setup(int zone, int awakening)
    {
        enemy = GetComponent<Enemy>();
        patterns.Clear();
        patterns.AddRange(ZoneSets[zone % ZoneSets.Length]);
        if(awakening > 0) {
            foreach(Pattern p in System.Enum.GetValues(typeof(Pattern)))
                if(!patterns.Contains(p)) patterns.Add(p);
        }
        cooldown = Mathf.Max(3f, 7f - awakening);   // 각성할수록 자주
        timer = cooldown * 0.6f;                     // 등장하고 조금 뒤에 첫 패턴
        next = 0;
        effectLeft = 0;
        enabled = true;
    }

    void OnDisable()
    {
        EndEffect();
    }

    void Update()
    {
        if(enemy == null || enemy._state == Enemy.EnemyState.dead) return;

        if(effectLeft > 0) {
            effectLeft -= Time.deltaTime;
            if(effectLeft <= 0) EndEffect();
            return;
        }

        timer -= Time.deltaTime;
        if(timer > 0) return;
        timer = cooldown;
        Use(patterns[next]);
        next = (next + 1) % patterns.Count;
    }

    void Use(Pattern p)
    {
        active = p;
        switch(p) {
            case Pattern.Dash:
                // 1초 동안 3배 속도로 돌진
                baseSpeed = enemy.enemyMovingSpeed;
                enemy.enemyMovingSpeed = baseSpeed * 3f;
                effectLeft = 1f;
                Notify("돌진!");
                break;
            case Pattern.Summon:
                // 그 구간의 졸개 3마리 소환
                var stage = InfiniteStage.Instance;
                if(stage != null && stage.enemyPrefab.Length > 0) {
                    var prefab = stage.enemyPrefab[Mathf.Clamp(stage.CurrentEnemyIndex[0], 0, stage.enemyPrefab.Length - 1)];
                    for(int i = 0; i < 3; i++) EnemyManager.Instance.createInfiniteEnemy(prefab, stage.EnemyScale);
                }
                Notify("소환!");
                break;
            case Pattern.Shield:
                // 3초 동안 받는 피해 80% 감소
                enemy.DamageTakenMul = 0.2f;
                effectLeft = 3f;
                Notify("보호막!");
                break;
        }
    }

    void EndEffect()
    {
        if(enemy == null) return;
        if(active == Pattern.Dash && baseSpeed > 0) {
            enemy.enemyMovingSpeed = baseSpeed;
            baseSpeed = 0;
        }
        enemy.DamageTakenMul = 1f;
        effectLeft = 0;
    }

    static void Notify(string text)
    {
        var ui = AugmentManager.Instance != null ? AugmentManager.Instance.UI : null;
        if(ui != null) ui.Toast(text, new Color(1f, 0.5f, 0.45f));
    }
}
