using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PoolObjectTypeDungeon
{
    NONE,
    Dungeon1_1,
    Dungeon1_2,
    Dungeon1_3,
    Dungeon1_4,
    Dungeon1_Boss,
    Dungeon2_1,
    Dungeon2_2,
    Dungeon2_3,
    Dungeon2_4,
    Dungeon2_Boss,
    Dungeon3_1,
    Dungeon3_2,
    Dungeon3_3,
    Dungeon3_4,
    Dungeon3_Boss,
    Dungeon4_1,
    Dungeon4_2,
    Dungeon4_3,
    Dungeon4_4,
    Dungeon4_Boss,
    Dungeon5_1,
    Dungeon5_2,
    Dungeon5_3,
    Dungeon5_4,
    Dungeon5_Boss,
}


[Serializable]
public class PoolInfoDungeon : PoolEntry<PoolObjectTypeDungeon> { }

public class ObjectPoolDungeon : TypedObjectPool<ObjectPoolDungeon, PoolObjectTypeDungeon, PoolInfoDungeon>
{
}
