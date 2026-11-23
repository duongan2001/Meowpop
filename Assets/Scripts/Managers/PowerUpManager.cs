using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Quản lý power-up: mở khóa theo level, số lượt (lưu PlayerPrefs), mua bằng xu và hiệu ứng dùng power-up.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    #region VARIABLES

    const string COUNT_KEY_PREFIX = "PowerUpCount_";
    const string FREE_GRANTED_KEY_PREFIX = "PowerUpFreeGranted_";

    [Tooltip("Cấu hình 3 power-up: level mở khóa, số lượt tặng, giá, icon")]
    [SerializeField] PowerUpData[] powerUps;

    [Tooltip("3 nút Power-up trong màn chơi")]
    [SerializeField] PowerUpButton[] powerUpButtons;

    [Tooltip("Panel - Buy Power-up, mở khi bấm nút đã hết lượt")]
    [SerializeField] BuyPowerUpPanel buyPowerUpPanel;

    [Header("--- SHUFFLE ---")]
    [Tooltip("Số bóng đầu (đang hiển thị) được trộn mèo")]
    [SerializeField] int shuffleBubbleCount = 30;
    [Tooltip("Tâm vòng xoáy, để trống thì dùng giữa catFlyContainer")]
    [SerializeField] RectTransform shuffleCenter;
    [Tooltip("Số độ xoay của vòng xoáy mỗi chiều (nên là bội số 360)")]
    [SerializeField] float shuffleSpinDegrees = 360f;
    [SerializeField] float shuffleSuckDuration = 0.6f;
    [SerializeField] float shuffleReturnDuration = 0.6f;
    [Tooltip("Mèo bị thu nhỏ còn bao nhiêu khi vào tâm xoáy")]
    [SerializeField] float shuffleCenterScale = 0.3f;
    [Tooltip("Độ lệch thời gian ngẫu nhiên giữa các con mèo")]
    [SerializeField] float shuffleRandomDelay = 0.15f;

    bool isBusy;                // đang trộn mèo -> chặn bấm mèo / power-up
    int shufflingCatCount;
    PowerUpType pendingBuyType;

    readonly List<Transform> shufflingCats = new List<Transform>();

    #endregion


    #region PROPERTIES

    public static PowerUpManager Instance { get; private set; }

    public bool IsBusy => isBusy;

    // Power-up người chơi đang muốn mua (panel mua đọc giá trị này)
    public PowerUpType PendingBuyType => pendingBuyType;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[PowerUpManager] Có nhiều hơn 1 PowerUpManager trong scene, xóa bản thừa");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion


    #region LEVEL METHODS

    /// <summary>
    /// GameManager gọi cuối LoadLevel: dừng hiệu ứng cũ, cập nhật nút, tặng lượt free cho power-up vừa mở.
    /// </summary>
    public void OnLevelLoaded(int level)
    {
        StopShuffle();
        RefreshAllButtons();

        if (powerUps == null)
            return;

        foreach (PowerUpData powerUp in powerUps)
        {
            if (level < powerUp.unlockLevel || IsFreeGranted(powerUp.type))
                continue;

            MarkFreeGranted(powerUp.type);

            if (powerUp.freeAmountOnUnlock > 0)
                AddPowerUp(powerUp.type, powerUp.freeAmountOnUnlock, true);

            // TODO: mở Panel - New Power-up giới thiệu power-up vừa mở
        }
    }

    #endregion


    #region COUNT METHODS

    public bool IsUnlocked(PowerUpType type)
    {
        PowerUpData data = GetPowerUpData(type);
        int level = GameManager.Instance != null ? GameManager.Instance.PlayingLevelNumber : 1;

        return data == null || level >= data.unlockLevel;
    }

    public int GetCount(PowerUpType type)
    {
        return PlayerPrefs.GetInt(COUNT_KEY_PREFIX + type, 0);
    }

    /// <summary>
    /// Cộng lượt power-up (tặng / mua). playAnimation = true: icon rơi vào nút rồi số mới tăng.
    /// </summary>
    public void AddPowerUp(PowerUpType type, int amount, bool playAnimation)
    {
        if (amount <= 0)
            return;

        SaveCount(type, GetCount(type) + amount);

        PowerUpButton button = GetButton(type);
        PowerUpData data = GetPowerUpData(type);

        if (playAnimation && button != null && data != null)
            button.PlayGainAnimation(data.icon, () => RefreshButton(type));
        else
            RefreshButton(type);
    }

    /// <summary>
    /// Đủ xu để mua power-up này không.
    /// </summary>
    public bool CanAfford(PowerUpType type)
    {
        PowerUpData data = GetPowerUpData(type);
        return data != null && DataManager.Instance.LoadCoinData() >= data.coinPrice;
    }

    /// <summary>
    /// Mua power-up bằng xu (panel mua gọi). Không đủ xu trả về false.
    /// </summary>
    public bool TryBuyPowerUp(PowerUpType type)
    {
        PowerUpData data = GetPowerUpData(type);
        if (data == null)
            return false;

        int coin = DataManager.Instance.LoadCoinData();
        if (coin < data.coinPrice)
            return false;

        DataManager.Instance.SaveCoinData(coin - data.coinPrice);
        AddPowerUp(type, data.buyAmount, true);
        return true;
    }

    void SaveCount(PowerUpType type, int count)
    {
        PlayerPrefs.SetInt(COUNT_KEY_PREFIX + type, Mathf.Max(0, count));
        PlayerPrefs.Save();
    }

    bool IsFreeGranted(PowerUpType type)
    {
        return PlayerPrefs.GetInt(FREE_GRANTED_KEY_PREFIX + type, 0) == 1;
    }

    void MarkFreeGranted(PowerUpType type)
    {
        PlayerPrefs.SetInt(FREE_GRANTED_KEY_PREFIX + type, 1);
        PlayerPrefs.Save();
    }

    #endregion


    #region BUTTON METHODS

    /// <summary>
    /// PowerUpButton gọi khi bấm: khóa thì bỏ qua, hết lượt thì mở panel mua, còn lượt thì dùng.
    /// </summary>
    public void OnPowerUpButtonClicked(PowerUpType type)
    {
        if (isBusy || !IsUnlocked(type))
            return;

        if (GetCount(type) <= 0)
        {
            OpenBuyPanel(type);
            return;
        }

        if (GameManager.Instance.IsGameOver)
            return;

        // Chỉ trừ lượt khi power-up thật sự làm được gì đó
        if (UsePowerUp(type))
        {
            SaveCount(type, GetCount(type) - 1);
            RefreshButton(type);
        }
    }

    void OpenBuyPanel(PowerUpType type)
    {
        pendingBuyType = type;

        if (buyPowerUpPanel != null)
            buyPowerUpPanel.ShowBuyPowerUp(type);
    }

    public void RefreshAllButtons()
    {
        if (powerUpButtons == null)
            return;

        foreach (PowerUpButton button in powerUpButtons)
        {
            if (button != null)
                RefreshButton(button.Type);
        }
    }

    void RefreshButton(PowerUpType type)
    {
        PowerUpButton button = GetButton(type);
        PowerUpData data = GetPowerUpData(type);

        if (button != null)
            button.Refresh(IsUnlocked(type), GetCount(type), data != null ? data.unlockLevel : 1);
    }

    #endregion


    #region USE METHODS

    bool UsePowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.FillBed:
                return UseFillBed();

            case PowerUpType.ClearTray:
                return UseClearTray();

            case PowerUpType.ShuffleCats:
                return UseShuffleCats();
        }

        return false;
    }

    /// <summary>
    /// Ổ đang mở có ít mèo nhất -> lấy mèo cùng loại (trong khay trước, rồi trong bóng) bay vào cho đủ.
    /// </summary>
    bool UseFillBed()
    {
        CatBed targetBed = null;

        foreach (CatBed bed in GameManager.Instance.catBeds)
        {
            if (bed == null || !bed.IsUnlocked || bed.CatID == CatBed.NO_CAT || bed.IsFull)
                continue;

            if (targetBed == null || bed.FreeSlotCount > targetBed.FreeSlotCount)
                targetBed = bed;
        }

        if (targetBed == null)
            return false;

        int sentCount = 0;

        // 1. Mèo cùng loại đang nằm trong khay
        CatTray catTray = GameManager.Instance.catTray;
        if (catTray != null)
        {
            sentCount += catTray.SendCatsToBed(targetBed);

            // Mèo cùng loại trong ô 6
            if (!targetBed.IsFull)
                sentCount += catTray.SendVacuumCatsToBed(targetBed);
        }

        // 2. Mèo cùng loại trong các bóng đang hiển thị
        List<Cat> cats = GameManager.Instance.GetCatsInBubbles(int.MaxValue, targetBed.CatID, targetBed.FreeSlotCount);
        foreach (Cat cat in cats)
        {
            int slotIndex = targetBed.ReserveSlot();
            if (slotIndex < 0)
                break;

            cat.OnCatClick(targetBed.GetSlotRect(slotIndex), targetBed.AddCatToBed);
            sentCount++;
        }

        GameManager.Instance.RefreshUprightCats(true);

        return sentCount > 0;
    }

    /// <summary>
    /// Chuyển mèo đang có trong khay vào ô số 6.
    /// </summary>
    bool UseClearTray()
    {
        CatTray catTray = GameManager.Instance.catTray;
        return catTray != null && catTray.UseVacuumPowerup();
    }

    #endregion


    #region SHUFFLE METHODS

    /// <summary>
    /// Trộn loại mèo trong các bóng đang hiển thị: mèo bị hút xoáy vào tâm, đổi loại, rồi xoáy về chỗ cũ.
    /// Số lượng mỗi loại giữ nguyên (chỉ đổi chỗ cho nhau).
    /// </summary>
    bool UseShuffleCats()
    {
        List<Cat> cats = GameManager.Instance.GetCatsInBubbles(shuffleBubbleCount);
        if (cats.Count < 2)
            return false;

        List<int> catIDs = new List<int>(cats.Count);
        foreach (Cat cat in cats)
            catIDs.Add(cat.CatID);

        Shuffle(catIDs);

        Vector3 centerWorld = GetShuffleCenterWorld();

        isBusy = true;
        shufflingCatCount = cats.Count;
        shufflingCats.Clear();

        for (int i = 0; i < cats.Count; i++)
            PlayCatVortex(cats[i], catIDs[i], centerWorld);

        return true;
    }

    void PlayCatVortex(Cat cat, int newCatID, Vector3 centerWorld)
    {
        Transform catTransform = cat.transform;
        Transform parent = catTransform.parent;
        Vector3 originLocal = catTransform.localPosition;
        Vector3 originScale = catTransform.localScale;

        shufflingCats.Add(catTransform);

        Sequence vortex = DOTween.Sequence().SetTarget(catTransform);
        vortex.AppendInterval(Random.Range(0f, shuffleRandomDelay));

        // Hút vào tâm: bán kính giảm dần về 0, xoay quanh tâm
        vortex.Append(DOTween.To(() => 0f, t =>
        {
            SetVortexPosition(catTransform, parent, originLocal, centerWorld, t, t * shuffleSpinDegrees);
            catTransform.localScale = originScale * Mathf.Lerp(1f, shuffleCenterScale, t);
        }, 1f, shuffleSuckDuration).SetEase(Ease.InQuad));

        // Ở tâm thì đổi loại mèo
        vortex.AppendCallback(() => cat.SetUpCat(newCatID));

        // Trả về chỗ cũ: bán kính tăng dần, xoay tiếp cùng chiều (tổng 2 vòng nên dừng đúng vị trí cũ)
        vortex.Append(DOTween.To(() => 0f, t =>
        {
            SetVortexPosition(catTransform, parent, originLocal, centerWorld, 1f - t, shuffleSpinDegrees * (1f + t));
            catTransform.localScale = originScale * Mathf.Lerp(shuffleCenterScale, 1f, t);
        }, 1f, shuffleReturnDuration).SetEase(Ease.OutQuad));

        vortex.OnComplete(() =>
        {
            catTransform.localPosition = originLocal;
            catTransform.localScale = originScale;
            OnCatShuffleDone();
        });
    }

    /// <summary>
    /// Vị trí trên vòng xoáy: progress = 0 ở chỗ cũ, = 1 ở tâm; angle = góc xoay quanh tâm.
    /// Tính lại vị trí gốc từ parent mỗi frame để bóng có di chuyển vẫn về đúng chỗ.
    /// </summary>
    void SetVortexPosition(Transform catTransform, Transform parent, Vector3 originLocal, Vector3 centerWorld, float progress, float angle)
    {
        Vector3 originWorld = parent != null ? parent.TransformPoint(originLocal) : originLocal;
        Vector3 offset = originWorld - centerWorld;
        Vector3 rotatedOffset = Quaternion.Euler(0f, 0f, angle) * offset * (1f - progress);

        catTransform.position = centerWorld + rotatedOffset;
    }

    void OnCatShuffleDone()
    {
        shufflingCatCount--;

        if (shufflingCatCount > 0)
            return;

        isBusy = false;
        shufflingCats.Clear();
    }

    /// <summary>
    /// Dừng trộn giữa chừng (load level mới). Vị trí mèo do CatBubble.ResetBubble trả về.
    /// </summary>
    void StopShuffle()
    {
        foreach (Transform catTransform in shufflingCats)
        {
            if (catTransform != null)
                catTransform.DOKill();
        }

        shufflingCats.Clear();
        shufflingCatCount = 0;
        isBusy = false;
    }

    Vector3 GetShuffleCenterWorld()
    {
        RectTransform center = shuffleCenter != null ? shuffleCenter : GameManager.Instance.catFlyContainer;
        return center.TransformPoint(center.rect.center);
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion


    #region TEST METHODS

    // Gọi khi đang Play: chuột phải component PowerUpManager → chọn hàm, hoặc gán vào nút test (đều public).
    // Các hàm Test dùng power-up KHÔNG kiểm tra khóa và KHÔNG trừ lượt.

    [ContextMenu("Test/Fill Bed")]
    public void TestFillBed()
    {
        TestUsePowerUp(PowerUpType.FillBed);
    }

    [ContextMenu("Test/Clear Tray")]
    public void TestClearTray()
    {
        TestUsePowerUp(PowerUpType.ClearTray);
    }

    [ContextMenu("Test/Shuffle Cats")]
    public void TestShuffleCats()
    {
        TestUsePowerUp(PowerUpType.ShuffleCats);
    }

    /// <summary>
    /// Cộng 1 lượt mỗi power-up, có hiệu ứng icon rơi vào nút (lượt được lưu thật).
    /// </summary>
    [ContextMenu("Test/Add 1 Each Power-up")]
    public void TestAddOneEach()
    {
        if (!CanRunTest())
            return;

        foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
            AddPowerUp(type, 1, true);
    }

    /// <summary>
    /// Thêm xu để test mua power-up khi hết lượt.
    /// </summary>
    [ContextMenu("Test/Add 1000 Coin")]
    public void TestAddCoin()
    {
        if (!CanRunTest())
            return;

        DataManager.Instance.SaveCoinData(DataManager.Instance.LoadCoinData() + 1000);
        Debug.Log($"[PowerUpManager] Test: xu hiện tại {DataManager.Instance.LoadCoinData()}");
    }

    void TestUsePowerUp(PowerUpType type)
    {
        if (!CanRunTest())
            return;

        if (isBusy)
        {
            Debug.LogWarning("[PowerUpManager] Test: đang chạy power-up khác, đợi xong rồi thử lại");
            return;
        }

        bool isUsed = UsePowerUp(type);
        Debug.Log($"[PowerUpManager] Test {type}: {(isUsed ? "đã dùng" : "không dùng được (không có gì để xử lý)")}");
    }

    bool CanRunTest()
    {
        if (Application.isPlaying && GameManager.Instance != null)
            return true;

        Debug.LogWarning("[PowerUpManager] Chỉ test được khi đang Play");
        return false;
    }

    #endregion


    #region HELPER METHODS

    public PowerUpData GetPowerUpData(PowerUpType type)
    {
        if (powerUps == null)
            return null;

        foreach (PowerUpData data in powerUps)
        {
            if (data.type == type)
                return data;
        }

        return null;
    }

    PowerUpButton GetButton(PowerUpType type)
    {
        if (powerUpButtons == null)
            return null;

        foreach (PowerUpButton button in powerUpButtons)
        {
            if (button != null && button.Type == type)
                return button;
        }

        return null;
    }

    /// <summary>
    /// Xóa toàn bộ dữ liệu power-up để test (chuột phải component → Reset Power-up Data).
    /// </summary>
    [ContextMenu("Reset Power-up Data")]
    public void ResetPowerUpData()
    {
        foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
        {
            PlayerPrefs.DeleteKey(COUNT_KEY_PREFIX + type);
            PlayerPrefs.DeleteKey(FREE_GRANTED_KEY_PREFIX + type);
        }

        PlayerPrefs.Save();

        if (Application.isPlaying)
            RefreshAllButtons();

        Debug.Log("[PowerUpManager] Đã reset dữ liệu power-up");
    }

    #endregion
}