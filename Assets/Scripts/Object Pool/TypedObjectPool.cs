using System;
using System.Collections.Generic;
using UnityEngine;

// 종류(enum)별로 오브젝트를 미리 만들어 두고 꺼내 쓰는 풀의 공통 부모.
// (적·던전 적·탄·팝업·광석 풀이 같은 코드를 5번 복사해서 쓰던 것을 하나로)
[Serializable]
public class PoolEntry<TEnum>
{
    public TEnum type;
    public int amount = 0;
    public GameObject prefab;
    public GameObject Parent;

    [HideInInspector]
    public List<GameObject> pool = new List<GameObject>();
}

public abstract class TypedObjectPool<TSelf, TEnum, TInfo> : Singleton<TSelf>
    where TSelf : MonoBehaviour
    where TInfo : PoolEntry<TEnum>
{
    [SerializeField]
    protected List<TInfo> listofPool;

    static readonly Vector3 DefaultPos = new Vector3(-100, -100, -100);
    Dictionary<TEnum, TInfo> byType;

    protected virtual void Start()
    {
        foreach(var info in listofPool) FillPool(info);
    }

    void FillPool(TInfo info)
    {
        for(int i = 0; i < info.amount; i++) {
            GameObject ob = Instantiate(info.prefab, info.Parent.transform);
            ob.SetActive(false);
            ob.transform.position = DefaultPos;
            info.pool.Add(ob);
        }
    }

    // 그 종류의 원본 프리팹 (그림만 빌려 쓸 때, 2026-11-21 종결 미사일)
    public GameObject PrefabOf(TEnum type) => GetPoolByType(type)?.prefab;

    public GameObject GetPoolObject(TEnum type)
    {
        TInfo info = GetPoolByType(type);
        if(info == null) {
            Debug.LogError($"[{typeof(TSelf).Name}] 풀에 없는 종류: {type}");
            return null;
        }

        GameObject ob;
        var pool = info.pool;
        if(pool.Count > 0) {
            ob = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        } else {
            ob = Instantiate(info.prefab, info.Parent.transform);
        }
        OnTake(type, ob);
        return ob;
    }

    public void CoolObject(GameObject ob, TEnum type)
    {
        if(ob == null) return;
        ob.SetActive(false);
        ob.transform.position = DefaultPos;

        TInfo info = GetPoolByType(type);
        if(info != null && !info.pool.Contains(ob))
            info.pool.Add(ob);
    }

    // 꺼낼 때 추가로 할 일 (탄 크기 증강 등)
    protected virtual void OnTake(TEnum type, GameObject ob) { }

    TInfo GetPoolByType(TEnum type)
    {
        if(byType == null) {
            byType = new Dictionary<TEnum, TInfo>();
            foreach(var info in listofPool)
                if(!byType.ContainsKey(info.type)) byType[info.type] = info;
        }
        byType.TryGetValue(type, out TInfo found);
        return found;
    }
}
