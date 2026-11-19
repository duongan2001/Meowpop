using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ChallengeManager : MonoBehaviour
{
    #region VARIABLES

    const string INTRO_SHOWN_KEY_PREFIX = "ChallengeIntroShown_";

    [Tooltip("Danh sách thử thách và level mở khóa")]
    [SerializeField] ChallengeData[] challenges;

    [Tooltip("Panel giới thiệu thử thách mới")]
    [FormerlySerializedAs("newChallengePopup")]
    [SerializeField] NewChallengePanel newChallengePanel;

    // Hàng chờ popup (1 level có thể mở nhiều thử thách cùng lúc)
    readonly Queue<ChallengeData> pendingIntros = new Queue<ChallengeData>();
    bool isShowingIntro;

    #endregion


    #region PROPERTIES

    public static ChallengeManager Instance { get; private set; }

    public IReadOnlyList<ChallengeData> Challenges => challenges;

    // Đang hiện / còn popup thử thách chờ hiện
    public bool HasPendingIntro => isShowingIntro || pendingIntros.Count > 0;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[ChallengeManager] Có nhiều hơn 1 ChallengeManager trong scene, xóa bản thừa");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion


    #region UNLOCK METHODS

    /// <summary>
    /// Thử thách đã mở ở level này chưa. Thử thách chưa cấu hình thì coi như đã mở (không giới hạn).
    /// Dùng khi sinh bóng đóng băng / bóng khóa.
    /// </summary>
    public bool IsChallengeUnlocked(ChallengeType type, int level)
    {
        ChallengeData challenge = GetChallenge(type);
        return challenge == null || level >= challenge.unlockLevel;
    }

    public ChallengeData GetChallenge(ChallengeType type)
    {
        if (challenges == null)
            return null;

        foreach (ChallengeData challenge in challenges)
        {
            if (challenge.type == type)
                return challenge;
        }

        return null;
    }

    /// <summary>
    /// Các thử thách đã mở ở level này.
    /// </summary>
    public List<ChallengeData> GetUnlockedChallenges(int level)
    {
        List<ChallengeData> result = new List<ChallengeData>();

        if (challenges == null)
            return result;

        foreach (ChallengeData challenge in challenges)
        {
            if (level >= challenge.unlockLevel)
                result.Add(challenge);
        }

        return result;
    }

    /// <summary>
    /// Thử thách gần nhất CHƯA mở ở level này (unlockLevel > level). Hết thử thách thì trả về null.
    /// </summary>
    public ChallengeData GetNextChallenge(int level)
    {
        ChallengeData nextChallenge = null;

        if (challenges == null)
            return null;

        foreach (ChallengeData challenge in challenges)
        {
            if (challenge.unlockLevel <= level)
                continue;

            if (nextChallenge == null || challenge.unlockLevel < nextChallenge.unlockLevel)
                nextChallenge = challenge;
        }

        return nextChallenge;
    }

    /// <summary>
    /// Level mở thử thách gần nhất đã qua (unlockLevel <= level), chưa có thì trả về 1.
    /// Dùng làm mốc bắt đầu cho thanh tiến trình tới thử thách kế tiếp.
    /// </summary>
    public int GetPreviousUnlockLevel(int level)
    {
        int previousLevel = 1;

        if (challenges == null)
            return previousLevel;

        foreach (ChallengeData challenge in challenges)
        {
            if (challenge.unlockLevel <= level && challenge.unlockLevel > previousLevel)
                previousLevel = challenge.unlockLevel;
        }

        return previousLevel;
    }

    #endregion


    #region INTRO METHODS

    /// <summary>
    /// Gọi khi vào level: thử thách nào đã mở mà người chơi chưa xem giới thiệu thì hiện popup.
    /// </summary>
    public void CheckNewChallenges(int level)
    {
        if (challenges == null)
            return;

        // Level thấp mở trước thì giới thiệu trước
        List<ChallengeData> sortedChallenges = new List<ChallengeData>(challenges);
        sortedChallenges.Sort((a, b) => a.unlockLevel.CompareTo(b.unlockLevel));

        foreach (ChallengeData challenge in sortedChallenges)
        {
            if (level < challenge.unlockLevel || IsIntroShown(challenge.type) || pendingIntros.Contains(challenge))
                continue;

            pendingIntros.Enqueue(challenge);
        }

        ShowNextIntro();
    }

    /// <summary>
    /// Panel gọi khi người chơi đóng: lưu đã xem, hiện tiếp thử thách kế (nếu có).
    /// </summary>
    public void OnChallengeIntroClosed(ChallengeData challenge)
    {
        if (challenge != null)
            MarkIntroShown(challenge.type);

        isShowingIntro = false;
        ShowNextIntro();
    }

    void ShowNextIntro()
    {
        if (isShowingIntro || pendingIntros.Count == 0)
            return;

        if (newChallengePanel == null)
        {
            Debug.LogWarning("[ChallengeManager] Chưa gán NewChallengePanel, không hiện được panel thử thách mới");
            return;
        }

        isShowingIntro = true;
        newChallengePanel.ShowChallenge(pendingIntros.Dequeue());
    }

    #endregion


    #region SAVE METHODS

    bool IsIntroShown(ChallengeType type)
    {
        return PlayerPrefs.GetInt(GetIntroKey(type), 0) == 1;
    }

    void MarkIntroShown(ChallengeType type)
    {
        PlayerPrefs.SetInt(GetIntroKey(type), 1);
        PlayerPrefs.Save();
    }

    string GetIntroKey(ChallengeType type)
    {
        return INTRO_SHOWN_KEY_PREFIX + type;
    }

    /// <summary>
    /// Xóa trạng thái đã xem để test lại popup (chuột phải component → Reset Challenge Intros).
    /// </summary>
    [ContextMenu("Reset Challenge Intros")]
    public void ResetAllIntros()
    {
        foreach (ChallengeType type in System.Enum.GetValues(typeof(ChallengeType)))
            PlayerPrefs.DeleteKey(GetIntroKey(type));

        PlayerPrefs.Save();
        Debug.Log("[ChallengeManager] Đã reset trạng thái xem popup thử thách");
    }

    #endregion
}