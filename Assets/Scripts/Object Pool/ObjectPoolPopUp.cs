using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PoolObjectTypePopUp
{
    Damage,
    Material,
    Money
}


[Serializable]
public class PoolInfoPopUp : PoolEntry<PoolObjectTypePopUp> { }

public class ObjectPoolPopUp : TypedObjectPool<ObjectPoolPopUp, PoolObjectTypePopUp, PoolInfoPopUp>
{
}
