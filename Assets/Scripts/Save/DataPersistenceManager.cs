using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class DataPersistenceManager : MonoBehaviour
{
    [Header("File Storage Config")]
    [SerializeField] private string fileName;
    [SerializeField] private bool useEncryption;

    public GameData gameData;
    private List<IDataPersistence> dataPersistenceObjects;

    public static DataPersistenceManager instance { get; private set; }

    private bool isReset = false;
    private bool isDataLoaded = false;
    private bool isLoadFailed = false;
    private bool isSaving = false;
    private bool isLoading = false;

    private float autoSaveTimer = 0f;
    private const float AUTO_SAVE_INTERVAL = 5f;

    private void Awake()
    {
        if(instance != null && instance != this) {
            Debug.LogError("Found more than one Data Persistence Manager in the scene.");
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    // 런타임에 만들어지는 매니저(리마스터 시스템)는 여기로 등록해요. 항상 목록 맨 뒤에서 불러오고 저장해요.
    private static readonly List<IDataPersistence> lateRegistered = new List<IDataPersistence>();

    public static void Register(IDataPersistence obj)
    {
        if(obj == null || lateRegistered.Contains(obj))
            return;
        lateRegistered.Add(obj);
        if(instance != null && instance.dataPersistenceObjects != null) {
            instance.dataPersistenceObjects.Remove(obj);
            instance.dataPersistenceObjects.Add(obj);
            if(instance.isDataLoaded && instance.gameData != null)
                obj.LoadData(instance.gameData);
        }
    }

    public bool IsDataLoaded => isDataLoaded && !isLoadFailed;

    private void Start()
    {
        dataPersistenceObjects = FindAllDataPersistenceObjects();
        ReorderPersistenceObjects();
        foreach(IDataPersistence obj in lateRegistered) {
            dataPersistenceObjects.Remove(obj);
            dataPersistenceObjects.Add(obj);
        }
    }

    public async void LoadGame()
    {
        if(isLoading) {
            Debug.LogWarning("[DataPersistence] Load skipped: already loading.");
            return;
        }

        isLoading = true;
        isDataLoaded = false;
        isLoadFailed = false;

        try {
            // 네트워크가 잠깐 끊겨도 바로 포기하지 않게 3번까지 시도 (실패하면 이번 실행 내내 저장이 막혀요)
            var loadResult = await CloudSaveManager.LoadDataOnCloud("GameData");
            for(int attempt = 1; attempt < 3 && !loadResult.Success; attempt++) {
                Debug.LogWarning($"[DataPersistence] Cloud load retry {attempt}");
                await System.Threading.Tasks.Task.Delay(1500 * attempt);
                loadResult = await CloudSaveManager.LoadDataOnCloud("GameData");
            }

            if(!loadResult.Success) {
                Debug.LogError("[DataPersistence] Cloud load failed. Save will be blocked to prevent overwrite.");
                isLoadFailed = true;
                return;
            }

            if(!loadResult.HasData) {
                Debug.Log("[DataPersistence] No cloud save found. Creating new game data.");
                NewGame();
            } else {
                gameData = loadResult.Data;
                EnsureGameDataValid();
            }

            foreach(IDataPersistence dataPersistenceObj in dataPersistenceObjects) {
                dataPersistenceObj.LoadData(gameData);
            }

            isDataLoaded = true;
            Debug.Log("[DataPersistence] Load complete.");
        } finally {
            isLoading = false;
        }
    }

    public void NewGame()
    {
        gameData = new GameData();
        EnsureGameDataValid();
    }

    public async Task<bool> SaveGame()
    {
        if(!isDataLoaded) {
            Debug.LogWarning("[DataPersistence] Save blocked: data not loaded yet.");
            return false;
        }

        if(isLoadFailed) {
            Debug.LogWarning("[DataPersistence] Save blocked: previous load failed.");
            return false;
        }

        if(gameData == null) {
            Debug.LogWarning("[DataPersistence] Save blocked: gameData is null.");
            return false;
        }

        if(isSaving) {
            Debug.LogWarning("[DataPersistence] Save skipped: already saving.");
            return false;
        }

        isSaving = true;

        try {
            foreach(IDataPersistence dataPersistenceObj in dataPersistenceObjects) {
                dataPersistenceObj.SaveData(gameData);
            }

            EnsureGameDataValid();

            bool success = await CloudSaveManager.SaveDataOnCloud("GameData", gameData);

            if(!success) {
                Debug.LogError("[DataPersistence] Cloud save failed.");
                return false;
            }

            return true;
        } finally {
            isSaving = false;
        }
    }

    public async void SaveGameNow()
    {
        await SaveGame();
    }

    public async void DeleteGame()
    {
        bool deleted = await CloudSaveManager.DeleteSomeData("GameData");

        if(!deleted) {
            Debug.LogError("[DataPersistence] Delete failed.");
            return;
        }

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        isReset = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private async void Update()
    {
        if(!isDataLoaded || isLoadFailed)
            return;

        autoSaveTimer += Time.deltaTime;

        if(autoSaveTimer >= AUTO_SAVE_INTERVAL) {
            autoSaveTimer = 0f;
            await SaveGame();
        }
    }

    private async void OnApplicationPause(bool pauseStatus)
    {
        if(pauseStatus && !isReset) {
            await SaveGame();
        }
    }

    private async void OnApplicationQuit()
    {
        if(!isReset) {
            await SaveGame();
        }
    }

    private List<IDataPersistence> FindAllDataPersistenceObjects()
    {
        IEnumerable<IDataPersistence> persistenceObjects =
            FindObjectsOfType<MonoBehaviour>(true).OfType<IDataPersistence>();

        return new List<IDataPersistence>(persistenceObjects);
    }

    private void ReorderPersistenceObjects()
    {
        if(dataPersistenceObjects == null || dataPersistenceObjects.Count == 0)
            return;

        IDataPersistence weapons = dataPersistenceObjects
            .FirstOrDefault(x => x != null && x.ToString() == "Weapons (WeaponManager)");

        IDataPersistence player = dataPersistenceObjects
            .FirstOrDefault(x => x != null && x.ToString() == "Player (Player)");

        if(weapons != null) {
            dataPersistenceObjects.Remove(weapons);
            dataPersistenceObjects.Insert(0, weapons);
        }

        if(player != null) {
            dataPersistenceObjects.Remove(player);

            int insertIndex = Mathf.Min(1, dataPersistenceObjects.Count);
            dataPersistenceObjects.Insert(insertIndex, player);
        }
    }

    private void EnsureGameDataValid()
    {
        if(gameData == null) {
            gameData = new GameData();
        }

        gameData.UpgradeLevel_Weapon ??= new SerializableDictionary<string, int>();
        gameData.Stats_Level ??= new SerializableDictionary<string, int>();
        gameData.bossKilled_Number ??= new SerializableDictionary<string, int>();
        gameData.Stage_Cleared ??= new SerializableDictionary<string, bool>();
        gameData.Planet_Cleared ??= new SerializableDictionary<string, bool>();
        gameData.Equip_Amount ??= new SerializableDictionary<string, int>();
        gameData.Equip_Level ??= new SerializableDictionary<string, int>();
        gameData.Equip_IsEquiped ??= new SerializableDictionary<string, bool>();
        gameData.Equip_SetData ??= new SerializableDictionary<string, string>();
        gameData.Equip_UpgradeIndex ??= new SerializableDictionary<string, string>();
        gameData.UpgradeStone_Amount ??= new SerializableDictionary<string, int>();
        gameData.Loot_Amount ??= new SerializableDictionary<string, int>();
        gameData.Collection_KilledAmount ??= new SerializableDictionary<string, int>();
        gameData.Collection_Grade ??= new SerializableDictionary<string, int>();
        gameData.Collection_EarnDiamond ??= new SerializableDictionary<string, int>();
        gameData.currentQuest ??= new SerializableDictionary<string, int>();
        gameData.AchieveCondition ??= new SerializableDictionary<string, bool>();
        gameData.CompletedTab ??= new SerializableDictionary<string, bool>();
        gameData.DrillLevel ??= new SerializableDictionary<string, int>();
        gameData.DrillDamage ??= new SerializableDictionary<string, int>();
        gameData.TraitLevel ??= new SerializableDictionary<string, int>();
        gameData.MinerLevel ??= new SerializableDictionary<string, int>();
        gameData.railShopLevel ??= new SerializableDictionary<string, int>();
        gameData.minedStoneAmount ??= new SerializableDictionary<string, long>();
        gameData.possessedOre ??= new SerializableDictionary<string, long>();
        gameData.unminedOre ??= new SerializableDictionary<string, long>();
        gameData.railDrill_DamageLevel ??= new SerializableDictionary<string, int>();
        gameData.railDrill_AttackSpeedLevel ??= new SerializableDictionary<string, int>();
        gameData.railDrill_luckLevel ??= new SerializableDictionary<string, int>();
        gameData.buffLevel ??= new SerializableDictionary<string, int>();
        gameData.isUnlocked_Icon ??= new SerializableDictionary<string, bool>();
        gameData.isSelected_Icon ??= new SerializableDictionary<string, bool>();
        gameData.Shop_isBought ??= new SerializableDictionary<string, bool>();
        gameData.Shop_isActivate ??= new SerializableDictionary<string, bool>();
        gameData.Shop_leftTime ??= new SerializableDictionary<string, string>();
        gameData.notification_Bool ??= new SerializableDictionary<string, bool>();
        gameData.autoSelectedRailBool ??= new SerializableDictionary<string, bool>();
        gameData.augIds ??= new List<string>();
        gameData.augLevels ??= new List<int>();
        gameData.rebirthTree ??= new List<int>();
        gameData.freeRerollDate ??= "";

        if(gameData.lvl <= 0) gameData.lvl = 1;
        if(gameData.maxMoney <= 0) gameData.maxMoney = 500;
        if(gameData.Clicklevel <= 0) gameData.Clicklevel = 1;
        if(gameData.StageIndex <= 0) gameData.StageIndex = 1;
        if(gameData.DungeonTicket < 0) gameData.DungeonTicket = 0;
    }
}