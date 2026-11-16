using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PoolObjectTypeStone
{
    Stone0,
    Stone1,
    Stone2,
    Stone3,
    Stone4,
    Stone5,
    Stone6,
    Stone7,
    Stone8,
    Stone9,
    Stone10,
    Stone11,
    Stone12,
    Stone13,
    Stone14,
}

[Serializable]
public class PoolInfo : PoolEntry<PoolObjectTypeStone> { }

public class ObjectPoolStone : TypedObjectPool<ObjectPoolStone, PoolObjectTypeStone, PoolInfo>
{
}
