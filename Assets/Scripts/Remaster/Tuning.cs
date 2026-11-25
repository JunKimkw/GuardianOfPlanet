using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 실행 중 숫자 조정 (에디터 전용)
// 프로젝트 폴더의 Tuning/tuning.txt 를 1초마다 확인해서, 바뀌었으면 다시 읽어요. 플레이를 멈추지 않아도 바로 반영돼요.
//   형식: 이름 = 숫자   # 설명      (# 뒤는 무시, 잘못 적은 줄은 무시하고 기본값을 써요)
// 빌드(안드로이드·iOS)는 파일을 읽지 않고 코드의 기본값을 써요. 정한 숫자는 출시 전에 코드 기본값으로 옮겨요.
public static class Tuning
{
#if UNITY_EDITOR
    static readonly Dictionary<string, float> values = new Dictionary<string, float>();
    static float nextCheck = -1f;
    static System.DateTime lastWrite;
    static string FilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tuning", "tuning.txt"));

    static void Refresh()
    {
        // 에디터 시계 (2026-11-26): 플레이 시작 때 코드를 다시 불러오지 않는 설정(Reload Domain 꺼짐)이라 nextCheck 가 지난 플레이 값으로 남아요.
        //  Time.realtimeSinceStartup 은 플레이마다 0부터라 한참 동안 다시 안 읽었어요 → 에디터가 켜진 뒤로 계속 가는 시계로
        float now = (float)UnityEditor.EditorApplication.timeSinceStartup;
        if(now < nextCheck) return;
        nextCheck = now + 1f;
        try {
            string path = FilePath;
            if(!File.Exists(path)) { values.Clear(); return; }
            var t = File.GetLastWriteTimeUtc(path);
            if(t == lastWrite) return;
            lastWrite = t;
            values.Clear();
            foreach(var raw in File.ReadAllLines(path)) {
                string line = raw;
                int hash = line.IndexOf('#');
                if(hash >= 0) line = line.Substring(0, hash);
                int eq = line.IndexOf('=');
                if(eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                if(float.TryParse(line.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    values[key] = v;
                else
                    Debug.LogWarning($"[Tuning] 숫자가 아니라서 무시했어요: {raw}");
            }
            Debug.Log($"[Tuning] tuning.txt 다시 읽음 ({values.Count}개)");
        } catch(System.Exception e) {
            Debug.LogWarning("[Tuning] 읽기 실패: " + e.Message);
        }
    }
#endif

    public static float Get(string key, float defaultValue)
    {
#if UNITY_EDITOR
        Refresh();
        return values.TryGetValue(key, out float v) ? v : defaultValue;
#else
        return defaultValue;
#endif
    }

    public static int GetInt(string key, int defaultValue) => Mathf.RoundToInt(Get(key, defaultValue));
}
