using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PoolObjectTypeBullet
{
    Main,
    Double,    
    Tracking,
    Mining,
    Sniper,
    Bomber,
    Poison,
    PoisonArea,
    BounceBullet
}


[Serializable]
public class PoolInfoBullet : PoolEntry<PoolObjectTypeBullet> { }

public class ObjectPoolBullet : TypedObjectPool<ObjectPoolBullet, PoolObjectTypeBullet, PoolInfoBullet>
{
    readonly Dictionary<GameObject, Vector3> baseScales = new Dictionary<GameObject, Vector3>();

    //리마스터: 탄 크기 증강 (원래 크기를 기억해 두고 배율 적용)
    protected override void OnTake(PoolObjectTypeBullet type, GameObject ob)
    {
        if(type == PoolObjectTypeBullet.Mining || type == PoolObjectTypeBullet.PoisonArea || type == PoolObjectTypeBullet.Poison) return;
        if(!baseScales.TryGetValue(ob, out Vector3 baseScale)) {
            baseScale = ob.transform.localScale;
            baseScales[ob] = baseScale;
        }
        float mult = AugmentManager.Instance != null ? AugmentManager.Instance.BulletScaleMultiplier : 1f;
        ob.transform.localScale = baseScale * mult;
    }
}
