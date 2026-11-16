using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;

// 닉네임 설정.
// - "닉네임이 있는지"는 기기(PlayerPrefs)가 아니라 Unity 계정 이름으로 판단해요. 재설치나 새 기기에서도 다시 묻지 않아요.
// - 규칙: 2~8자, 한글/영어/숫자만, 공백 X, 비속어 X, 랭킹에 있는 다른 사람과 같은 이름 X.
// - Unity 는 이름 뒤에 #1234 를 붙여 저장해요. 화면에는 Display() 로 # 앞부분만 보여 줘요.
public class UserNameManager : MonoBehaviour
{
    public static UserNameManager Instance;

    public string UserName;
    public GameObject UserNameSetTab;

    public bool DuplicateChecked;

    public TMP_InputField userNameInputField;
    public TextMeshProUGUI duplicateConditionText;

    public GameObject ErrorTab;
    public TextMeshProUGUI ErrorName;

    public string[] lines;

    // ---- 서버 이름 상태
    public static string ServerName { get; private set; }     // "닉네임#1234" 또는 null
    static bool nameFetched;                                   // 서버에서 확인했는지 (실패하면 PlayerPrefs 로 대신 판단)
    public static bool HasName => !string.IsNullOrEmpty(ServerName);

    public const int MinLen = 2, MaxLen = 8;
    static readonly Regex Allowed = new Regex("^[가-힣a-zA-Z0-9]+$");
    static readonly Color Bad = new Color(0.9f, 0.1f, 0.1f);
    static readonly Color Good = new Color(0.1f, 0.6f, 0.2f);
    static readonly Color Wait = new Color(0.45f, 0.45f, 0.5f);

    int checkVersion;
    bool submitting;

    void Awake()
    {
        Instance = this;
        var asset = Resources.Load<TextAsset>("BadWord");
        if(asset != null) {
            var list = new List<string>();
            foreach(var raw in Regex.Split(asset.text, @"\r\n|\n|\r")) {
                var w = raw.Trim().ToLowerInvariant();
                if(w.Length > 0) list.Add(w);
            }
            lines = list.ToArray();
        } else {
            Debug.LogWarning("[UserName] Resources/BadWord.txt 를 찾지 못했어요.");
            lines = new string[0];
        }
        if(userNameInputField != null) userNameInputField.characterLimit = MaxLen;
    }

    // ---- 이름 유틸
    public static string Display(string full)
    {
        if(string.IsNullOrEmpty(full)) return "";
        int i = full.IndexOf('#');
        return i >= 0 ? full.Substring(0, i) : full;
    }

    // 로그인 직후 호출: 계정에 저장된 이름을 가져와요 (없으면 새로 만들지 않음)
    public static async Task FetchServerName()
    {
        try {
            ServerName = await AuthenticationService.Instance.GetPlayerNameAsync(false);
            nameFetched = true;
        } catch(Exception e) {
            Debug.LogWarning("[UserName] 이름 확인 실패: " + e.Message);
            ServerName = AuthenticationService.Instance.PlayerName;
            nameFetched = HasName;
        }
        if(HasName) {
            PlayerPrefs.SetInt("username", 1);
            if(Player.instance != null) Player.instance.PlayerName = ServerName;
        } else if(nameFetched) {
            PlayerPrefs.SetInt("username", 0);
        }
    }

    // 닉네임 입력이 필요한지
    public static bool NeedsName()
    {
        if(AuthManager.SkipLoginForTest) return false;   // 테스트 중에는 닉네임 창을 띄우지 않아요
        if(nameFetched) return !HasName;
        return PlayerPrefs.GetInt("username", 0) == 0;   // 서버 확인 실패 시 기기 기록으로
    }

    // 문제가 없으면 null, 있으면 안내 문구
    public string Validate(string name)
    {
        if(string.IsNullOrEmpty(name)) return "닉네임을 입력해 주세요";
        if(Regex.IsMatch(name, @"\s")) return "닉네임에 공백은 쓸 수 없어요";
        if(name.Length < MinLen || name.Length > MaxLen) return $"{MinLen}~{MaxLen}자로 입력해 주세요";
        if(!Allowed.IsMatch(name)) return "한글(완성된 글자), 영어, 숫자만 쓸 수 있어요";
        string low = name.ToLowerInvariant();
        if(lines != null)
            foreach(var w in lines)
                if(low.Contains(w)) return "쓸 수 없는 단어가 들어 있어요";
        return null;
    }

    // ---- 화면 흐름
    public void checkIfUserNameSet()
    {
        if(NeedsName()) openUserNameTab();
    }

    public void openUserNameTab()
    {
        UserNameSetTab.SetActive(true);
        if(userNameInputField != null) {
            userNameInputField.SetTextWithoutNotify("");
            userNameInputField.characterLimit = MaxLen;
        }
        DuplicateChecked = false;
        SetCondition($"{MinLen}~{MaxLen}자, 한글/영어/숫자", Wait);
    }

    // 입력창 이벤트에 연결돼 있어요 (글자를 바꿀 때마다). 바로 규칙 검사하고, 잠깐 멈추면 중복 검사.
    public void checkDuplication()
    {
        DuplicateChecked = false;
        string name = userNameInputField.text;
        int ver = ++checkVersion;

        string err = Validate(name);
        if(err != null) {
            SetCondition(err, Bad);
            return;
        }
        SetCondition("확인 중...", Wait);
        _ = CheckServerLater(name, ver);
    }

    async Task CheckServerLater(string name, int ver)
    {
        await Task.Delay(400);
        if(ver != checkVersion || this == null) return;
        bool taken = await LeaderBoardManager.IsNameTaken(name);
        if(ver != checkVersion || this == null) return;
        setConditionText(!taken);
    }

    // 예전 호출 호환: a = 중복 아님
    public void setConditionText(bool a)
    {
        string err = Validate(userNameInputField.text);
        if(err != null) {
            SetCondition(err, Bad);
            DuplicateChecked = false;
            return;
        }
        if(a) {
            SetCondition("사용할 수 있는 닉네임이에요", Good);
            DuplicateChecked = true;
        } else {
            SetCondition("이미 쓰고 있는 닉네임이에요", Bad);
            DuplicateChecked = false;
        }
    }

    // "확인" 버튼 (씬에서 연결됨)
    public async void setUserName()
    {
        if(submitting) return;
        string name = userNameInputField.text;

        string err = Validate(name);
        if(err != null) { ShowError(err); return; }

        submitting = true;
        try {
            SetCondition("확인 중...", Wait);
            if(await LeaderBoardManager.IsNameTaken(name)) {
                setConditionText(false);
                ShowError("이미 쓰고 있는 닉네임이에요");
                return;
            }

            string full = await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
            ServerName = string.IsNullOrEmpty(full) ? name : full;
            nameFetched = true;
            UserName = name;
            if(Player.instance != null) Player.instance.PlayerName = ServerName;
            PlayerPrefs.SetInt("username", 1);
            Debug.Log("[UserName] 닉네임 설정: " + ServerName);

            UserNameSetTab.SetActive(false);
            if(ErrorTab != null) ErrorTab.SetActive(false);
            DataPersistenceManager.instance.SaveGameNow();
            StartScreenScript.Instance.checkIfSignedIn();
            LeaderBoardManager.Instance.AddInfiniteStageScore();
        } catch(Exception e) {
            Debug.LogException(e);
            ShowError("닉네임을 저장하지 못했어요. 인터넷 연결을 확인하고 다시 시도해 주세요.");
        } finally {
            submitting = false;
        }
    }

    void SetCondition(string text, Color color)
    {
        if(duplicateConditionText == null) return;
        duplicateConditionText.color = color;
        duplicateConditionText.text = text;
    }

    void ShowError(string text)
    {
        if(ErrorName != null) ErrorName.text = text;
        if(ErrorTab != null) ErrorTab.SetActive(true);
        else AuthManager.ShowError(text);
    }
}
