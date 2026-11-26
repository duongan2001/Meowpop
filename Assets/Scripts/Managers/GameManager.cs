using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    #region VARIABLES

    public static GameManager Instance;

    public RuntimeAnimatorController[] catAnimatorControllers;

    public CatBed[] catBeds;

    public CatTray catTray;

    public RectTransform catFlyContainer;

    public Sprite[] catSprites;

    [Header("Bubble")]
    [SerializeField] GameObject catBubblePrefab;
    [Tooltip("Vùng sàn chứa bóng, bóng được sinh ngẫu nhiên trong rect này")]
    [SerializeField] RectTransform bubbleContainer;
    [Tooltip("Khoảng cách tối thiểu giữa 2 bóng")]
    [SerializeField] float bubbleSpacing = 10f;
    [Tooltip("Số lần thử tìm vị trí không chồng lên bóng khác")]
    [SerializeField] int maxPlaceAttempts = 50;
    [Tooltip("Thời gian giữa 2 lần sinh bóng (giây)")]
    [SerializeField] float bubbleSpawnInterval = 0.5f;
    [Tooltip("Mèo trong bao nhiêu bóng đầu tiên (còn mèo) luôn đứng thẳng")]
    [SerializeField] int uprightBubbleCount = 20;
    [Tooltip("Số bóng (còn mèo) hiện trên màn hình tối đa. Vỡ 1 bóng thì bật thêm bóng tiếp theo")]
    [SerializeField] int maxActiveBubbles = 30;
    [Tooltip("Bóng đóng băng: khoảng hở tối đa giữa 2 viền bóng để tính là \"bên cạnh\" (bấm mèo ở bóng bên cạnh thì phá băng)")]
    [SerializeField] float frozenNeighborDistance = 60f;

    [Header("UI")]
    [Tooltip("Text hiện level đang chơi ở trên cùng màn hình (VD: Level 5)")]
    [SerializeField] Text levelText;

    [Header("Panels")]
    [SerializeField] VictoryPanel victoryPanel;
    [SerializeField] LosePanel losePanel;
    [SerializeField] BuyCatbedPanel buyCatbedPanel;
    [SerializeField] BuyCoinPanel buyCoinPanel;
    [SerializeField] FullTrayPanel fullTrayPanel;

    [Header("Full Tray")]
    [Tooltip("Số lần được cứu bằng Panel - Full Tray mỗi level, hết lượt thì khay đầy là thua luôn. < 0 = không giới hạn")]
    [SerializeField] int maxFullTrayContinues = 1;
    [Tooltip("Chờ panel mờ đi rồi mới dọn khay + mở ổ (giây)")]
    [SerializeField] float fullTrayContinueDelay = 0.25f;

    Coroutine spawnCoroutine;
    Coroutine fullTrayCoroutine;
    int fullTrayContinueCount;  // số lần đã cứu bằng Panel - Full Tray trong level này
    bool isGameOver;            // dừng chơi (thắng hoặc thua)
    int collectedCatCount;      // số mèo đã thu thập, cộng mỗi khi 1 ổ hoàn thành
    int playingLevelNumber;     // level đang chơi (OnLevelWin lưu trước level tiếp theo nên không lấy từ LevelManager được)

    readonly List<CatBubble> spawnedBubbles = new List<CatBubble>();
    readonly List<Vector2> placedPositions = new List<Vector2>();
    readonly List<float> placedRadiuses = new List<float>();

    int levelBubbleCount;       // số bóng của level hiện tại (bóng thừa của level trước nằm sau trong pool)
    int placedBubbleCount;      // số bóng (theo thứ tự sinh) đã được chọn vị trí, chờ hoặc đã hiện
    int shownBubbleCount;       // số bóng đã hiện lên màn hình

    #endregion


    #region PROPERTIES

    public IReadOnlyList<CatBubble> SpawnedBubbles => spawnedBubbles;

    // Đang sinh bóng dở, dùng khi check thắng (chưa sinh hết bóng thì chưa thắng)
    public bool IsSpawningBubbles => spawnCoroutine != null || shownBubbleCount < levelBubbleCount;
    public bool IsGameOver => isGameOver;
    public int CollectedCatCount => collectedCatCount;
    public int PlayingLevelNumber => playingLevelNumber;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (catTray != null)
            catTray.OnTrayFull += OnTrayFull;

        // Đổi ngôn ngữ thì cập nhật chữ "Level"
        MultiLanguageManager.OnLanguageChanged += UpdateLevelText;
    }

    private void OnDisable()
    {
        if (catTray != null)
            catTray.OnTrayFull -= OnTrayFull;

        MultiLanguageManager.OnLanguageChanged -= UpdateLevelText;
    }

    private void Start()
    {
        Application.targetFrameRate = 60;

        LoadLevel();
    }

    #endregion


    #region LEVEL METHODS

    public void LoadLevel()
    {
        playingLevelNumber = LevelManager.Instance.GetCurrentLevel();
        UpdateLevelText();
        LevelManager.Instance.LoadLevelData();

        if (LevelManager.Instance.CurrentLevel == null)
            return;

        StopFullTrayCoroutine();

        isGameOver = false;
        collectedCatCount = 0;
        fullTrayContinueCount = 0;
        UpdateCollectedCountUI();

        if (catTray != null)
            catTray.ResetTray();

        SpawnBubbles();

        // Bóng đã setup xong dữ liệu mèo (dù chưa hiện) nên ổ chọn được loại mèo ngay
        SetUpCatBeds();

        // Tới level mở thử thách mới thì hiện popup giới thiệu
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.CheckNewChallenges(playingLevelNumber);

        // Cập nhật nút power-up, tặng lượt free nếu vừa mở khóa
        if (PowerUpManager.Instance != null)
            PowerUpManager.Instance.OnLevelLoaded(playingLevelNumber);
    }

    /// <summary>
    /// Gọi từ CatBed khi ổ hoàn thành: cộng số mèo thu thập, đủ tổng số mèo của level thì thắng.
    /// </summary>
    public void OnCatBedCompleted(int catCount)
    {
        if (isGameOver)
            return;

        collectedCatCount += catCount;
        UpdateCollectedCountUI();

        if (collectedCatCount >= LevelManager.Instance.CurrentLevel.totalCatCount)
            OnLevelWin();
    }

    void UpdateLevelText()
    {
        if (levelText != null)
            levelText.text = MultiLanguageManager.TranslateFormat("common.level", "Level {0}", playingLevelNumber);
    }

    void UpdateCollectedCountUI()
    {
        LevelData level = LevelManager.Instance.CurrentLevel;

        if (catTray != null && level != null)
            catTray.UpdateCatCollectedCount(collectedCatCount, level.totalCatCount);
    }

    /// <summary>
    /// Chơi lại level đang chơi (dùng cho nút Replay ở popup thua / thắng).
    /// Không ghi đè tiến trình: thắng rồi chơi lại thì tiến trình đã lưu vẫn là level tiếp theo.
    /// </summary>
    public void ReplayLevel()
    {
        LevelManager.Instance.SetCurrentLevel(playingLevelNumber, saveProgress: false);
        LoadLevel();
    }

    /// <summary>
    /// Sang level tiếp theo (dùng cho nút Next ở popup thắng).
    /// </summary>
    public void NextLevel()
    {
        LevelManager.Instance.SetCurrentLevel(playingLevelNumber + 1);
        LoadLevel();
    }

    /// <summary>
    /// Load thẳng 1 level để test, không ghi đè tiến trình đã lưu.
    /// </summary>
    public void TestLevel(int level)
    {
        Debug.Log($"[GameManager] Test level {level}");

        LevelManager.Instance.SetCurrentLevel(level, saveProgress: false);
        LoadLevel();
    }

    void OnLevelWin()
    {
        isGameOver = true;
        Debug.Log("[GameManager] Thu thập đủ mèo - THẮNG");

        // Lưu luôn level tiếp theo để thoát game giữa chừng không mất tiến trình
        LevelManager.Instance.SetCurrentLevel(LevelManager.Instance.GetCurrentLevel() + 1);

        if (victoryPanel != null)
            victoryPanel.ShowVictory(playingLevelNumber);
    }

    void SetUpCatBeds()
    {
        // Dọn hết trước để các ổ không bị trùng loại với dữ liệu của level cũ
        foreach (CatBed bed in catBeds)
        {
            if (bed != null)
                bed.ClearBed();
        }

        foreach (CatBed bed in catBeds)
        {
            if (bed != null)
                bed.SetupBed();
        }
    }

    #endregion


    #region INTERACT

    /// <summary>
    /// Chọn đích cho con mèo vừa được bấm:
    /// 1. Ổ đang nhận đúng loại và còn chỗ
    /// 2. Ổ đã mở nhưng chưa nhận loại nào -> gán loại này cho ổ
    /// 3. Khay chờ
    /// </summary>
    void OnCatClicked(Cat cat)
    {
        if (isGameOver || cat.IsFlying)
            return;

        // Đang chạy power-up trộn mèo thì không cho bấm
        if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsBusy)
            return;

        // Bóng đang đóng băng thì không bấm được, lớp băng rung nhẹ
        CatBubble sourceBubble = cat.Bubble;
        if (sourceBubble != null && sourceBubble.IsFrozen)
        {
            sourceBubble.PlayFrozenShake();
            return;
        }

        SendCatToTarget(cat);

        // Mèo đã rời bóng (không bị kẹt vì khay đầy) thì trừ băng các bóng bên cạnh
        if (cat.IsFlying && sourceBubble != null)
            UnfreezeNeighborBubbles(sourceBubble);

        // Bấm mèo có thể làm bóng vỡ -> bóng sau lọt vào nhóm bóng đầu, mèo của nó xoay thẳng
        RefreshUprightCats(true);
    }

    void SendCatToTarget(Cat cat)
    {
        int catID = cat.CatID;

        CatBed bed = FindBedForCat(catID);
        if (bed != null)
        {
            int slotIndex = bed.ReserveSlot();
            cat.OnCatClick(bed.GetSlotRect(slotIndex), bed.AddCatToBed);
            return;
        }

        if (catTray == null)
            return;

        int cellIndex = catTray.AddCatToTray(catID);
        if (cellIndex < 0)
            return;

        cat.OnCatClick(catTray.GetCellRect(cellIndex), () =>
        {
            catTray.OnCatArrived(catID);

            // Trong lúc bay có thể ổ đã đổi sang nhận đúng loại này -> bay tiếp lên ổ
            SendTrayCatsToBeds();
        });
    }

    /// <summary>
    /// Ổ nào đang nhận loại có trong khay và còn chỗ thì cho mèo trong khay bay lên.
    /// </summary>
    void SendTrayCatsToBeds()
    {
        if (catTray == null)
            return;

        foreach (CatBed bed in catBeds)
        {
            if (bed != null && bed.CatID != CatBed.NO_CAT && !bed.IsFull)
                catTray.SendCatsToBed(bed);
        }
    }

    CatBed FindBedForCat(int catID)
    {
        // Ưu tiên ổ đang nhận đúng loại
        foreach (CatBed bed in catBeds)
        {
            if (bed != null && bed.CanAcceptCat(catID))
                return bed;
        }

        // Không có thì lấy ổ đang chờ (đã mở, chưa có loại) và gán loại này
        foreach (CatBed bed in catBeds)
        {
            if (bed != null && bed.IsWaiting)
            {
                bed.SetupBedForCat(catID);

                // Gán xong có thể ổ đã kéo đủ mèo từ khay, kiểm tra lại
                if (bed.CanAcceptCat(catID))
                    return bed;
            }
        }

        return null;
    }

    /// <summary>
    /// CatBed gọi khi bấm vào ổ đang khóa: mở panel mua ổ.
    /// </summary>
    public void OpenBuyCatbedPanel(CatBed bed)
    {
        if (isGameOver || buyCatbedPanel == null)
            return;

        buyCatbedPanel.ShowBuyCatbed(bed);
    }

    /// <summary>
    /// Mở panel nhận xu (gán vào nút + cạnh ô xu, hoặc gọi khi không đủ xu).
    /// </summary>
    public void OpenBuyCoinPanel()
    {
        OpenBuyCoinPanel(null);
    }

    /// <summary>
    /// Mở panel nhận xu, onClosed gọi khi bấm Close ở Buy Coin (VD quay lại Panel - Full Tray).
    /// </summary>
    public void OpenBuyCoinPanel(System.Action onClosed)
    {
        if (buyCoinPanel == null)
            return;

        buyCoinPanel.ShowBuyCoin(onClosed);
    }

    /// <summary>
    /// Khay đầy: dừng chơi, còn lượt cứu thì chờ mèo cuối bay tới khay rồi hiện Panel - Full Tray, hết lượt thì thua.
    /// </summary>
    void OnTrayFull()
    {
        if (isGameOver)
            return;

        isGameOver = true;

        bool canContinue = fullTrayPanel != null
            && (maxFullTrayContinues < 0 || fullTrayContinueCount < maxFullTrayContinues);

        if (!canContinue)
        {
            ShowLosePanel();
            return;
        }

        StopFullTrayCoroutine();
        fullTrayCoroutine = StartCoroutine(ShowFullTrayRoutine());
    }

    IEnumerator ShowFullTrayRoutine()
    {
        // Chờ mèo cuối bay tới khay (để lúc dọn khay hút được hết)
        while (catTray != null && !catTray.AreAllCatsArrived)
            yield return null;

        fullTrayCoroutine = null;
        fullTrayPanel.ShowFullTray();
    }

    /// <summary>
    /// Panel - Full Tray gọi khi người chơi chọn chơi tiếp: dọn khay vào ô 6 + mở 1 ổ khóa (nếu còn).
    /// </summary>
    public void ContinueAfterFullTray()
    {
        fullTrayContinueCount++;

        StopFullTrayCoroutine();
        fullTrayCoroutine = StartCoroutine(ContinueAfterFullTrayRoutine());
    }

    IEnumerator ContinueAfterFullTrayRoutine()
    {
        // Chờ panel mờ đi cho thấy rõ hiệu ứng
        if (fullTrayContinueDelay > 0f)
            yield return new WaitForSecondsRealtime(fullTrayContinueDelay);

        if (catTray != null)
            catTray.UseVacuumPowerup();

        CatBed lockedBed = GetFirstLockedBed();
        if (lockedBed != null)
            lockedBed.UnlockBedWithAnimation();

        fullTrayCoroutine = null;
        isGameOver = false;
    }

    /// <summary>
    /// Ổ khóa đầu tiên (theo thứ tự catBeds), null nếu đã mở hết.
    /// </summary>
    public CatBed GetFirstLockedBed()
    {
        foreach (CatBed bed in catBeds)
        {
            if (bed != null && !bed.IsUnlocked)
                return bed;
        }

        return null;
    }

    public void ShowLosePanel()
    {
        isGameOver = true;

        if (losePanel != null)
            losePanel.ShowLose();
    }

    void StopFullTrayCoroutine()
    {
        if (fullTrayCoroutine != null)
        {
            StopCoroutine(fullTrayCoroutine);
            fullTrayCoroutine = null;
        }
    }

    #endregion


    #region BUBBLE METHODS

    /// <summary>
    /// Setup dữ liệu cho TOÀN BỘ bóng của level, nhưng chỉ đặt vị trí + hiện tối đa maxActiveBubbles bóng.
    /// Bóng còn lại chờ, mỗi khi 1 bóng vỡ thì bật thêm (OnBubblePopped).
    /// </summary>
    void SpawnBubbles()
    {
        if (catBubblePrefab == null || bubbleContainer == null)
        {
            Debug.LogError("[GameManager] Chưa gán catBubblePrefab hoặc bubbleContainer");
            return;
        }

        // Load level mới khi level cũ còn đang sinh bóng dở thì dừng lại
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        IReadOnlyList<int[]> bubbleCatIDs = LevelManager.Instance.BubbleCatIDs;
        levelBubbleCount = bubbleCatIDs.Count;
        placedBubbleCount = 0;
        shownBubbleCount = 0;

        // Setup đủ dữ liệu mèo cho mọi bóng (ổ / power-up cần biết mèo của cả bóng chưa hiện), dùng lại bóng cũ nếu có
        for (int i = 0; i < levelBubbleCount; i++)
        {
            CatBubble bubble = GetBubble(i);
            bubble.SetUpCatBubble(bubbleCatIDs[i]);
            bubble.SetFrozen(LevelManager.Instance.IsBubbleFrozen(i));

            // Ẩn đi, chờ tới lượt mới hiện
            bubble.gameObject.SetActive(false);
        }

        // Tắt các bóng thừa của level trước (reset trước để mèo đang bay dở không bay tiếp)
        for (int i = levelBubbleCount; i < spawnedBubbles.Count; i++)
        {
            spawnedBubbles[i].ResetBubble();
            spawnedBubbles[i].gameObject.SetActive(false);
        }

        PlaceFirstBubbles();

        // Mèo trong các bóng đầu đứng thẳng ngay từ lúc sinh
        RefreshUprightCats(false);

        StartShowBubbles();
    }

    /// <summary>
    /// Đặt vị trí cho nhóm bóng đầu tiên (tối đa maxActiveBubbles), đặt bóng to trước để dễ xếp không bị chồng.
    /// </summary>
    void PlaceFirstBubbles()
    {
        int firstCount = Mathf.Min(levelBubbleCount, Mathf.Max(1, maxActiveBubbles));

        List<CatBubble> firstBubbles = new List<CatBubble>(firstCount);
        for (int i = 0; i < firstCount; i++)
            firstBubbles.Add(spawnedBubbles[i]);

        firstBubbles.Sort((a, b) => GetBubbleRadius(b).CompareTo(GetBubbleRadius(a)));

        placedPositions.Clear();
        placedRadiuses.Clear();

        foreach (CatBubble bubble in firstBubbles)
        {
            float radius = GetBubbleRadius(bubble);
            Vector2 pos = FindBubblePosition(radius);

            bubble.transform.localPosition = pos;
            placedPositions.Add(pos);
            placedRadiuses.Add(radius);
        }

        placedBubbleCount = firstCount;
    }

    /// <summary>
    /// CatBubble gọi khi vỡ: còn bóng chờ và chưa đủ maxActiveBubbles bóng trên màn hình thì bật thêm.
    /// </summary>
    void OnBubblePopped(CatBubble poppedBubble)
    {
        int index = spawnedBubbles.IndexOf(poppedBubble);
        if (index < 0 || index >= levelBubbleCount)
            return;

        bool hasNewBubble = false;

        while (placedBubbleCount < levelBubbleCount && GetAliveBubbleCount() < maxActiveBubbles)
        {
            PlaceNextBubble();
            hasNewBubble = true;
        }

        if (hasNewBubble)
        {
            // Bóng mới lọt vào nhóm bóng đầu thì mèo của nó xoay thẳng
            RefreshUprightCats(true);
            StartShowBubbles();
        }

        UnfreezeIfOnlyFrozenLeft();
    }

    /// <summary>
    /// Đặt bóng tiếp theo (theo thứ tự sinh) vào chỗ trống, tránh các bóng đang còn trên màn hình.
    /// </summary>
    void PlaceNextBubble()
    {
        CatBubble bubble = spawnedBubbles[placedBubbleCount];

        placedPositions.Clear();
        placedRadiuses.Clear();

        // Chỉ tránh bóng còn mèo (bóng đang vỡ coi như đã trống chỗ)
        for (int i = 0; i < placedBubbleCount; i++)
        {
            CatBubble placed = spawnedBubbles[i];
            if (placed.RemainingCat <= 0)
                continue;

            placedPositions.Add(placed.transform.localPosition);
            placedRadiuses.Add(GetBubbleRadius(placed));
        }

        bubble.transform.localPosition = FindBubblePosition(GetBubbleRadius(bubble));
        placedBubbleCount++;
    }

    // Số bóng đã đặt (đang hiện hoặc chờ hiện) và còn mèo
    int GetAliveBubbleCount()
    {
        int count = 0;

        for (int i = 0; i < placedBubbleCount; i++)
        {
            if (spawnedBubbles[i].RemainingCat > 0)
                count++;
        }

        return count;
    }

    void StartShowBubbles()
    {
        if (spawnCoroutine == null && shownBubbleCount < placedBubbleCount)
            spawnCoroutine = StartCoroutine(ShowPlacedBubbles());
    }

    /// <summary>
    /// Hiện lần lượt các bóng đã đặt vị trí, mỗi bóng cách nhau bubbleSpawnInterval giây.
    /// Trong lúc chạy mà có bóng mới được đặt thêm thì hiện luôn bóng đó.
    /// </summary>
    IEnumerator ShowPlacedBubbles()
    {
        WaitForSeconds wait = new WaitForSeconds(bubbleSpawnInterval);

        while (shownBubbleCount < placedBubbleCount)
        {
            CatBubble bubble = spawnedBubbles[shownBubbleCount];
            shownBubbleCount++;

            bubble.gameObject.SetActive(true);
            bubble.PlayAppear();

            // Bóng cuối không cần chờ
            if (shownBubbleCount < placedBubbleCount)
                yield return wait;
        }

        spawnCoroutine = null;
    }

    /// <summary>
    /// Mèo trong uprightBubbleCount bóng đầu (theo thứ tự sinh, bỏ bóng đã vỡ) đứng thẳng,
    /// các bóng sau giữ góc nghiêng ban đầu.
    /// </summary>
    public void RefreshUprightCats(bool animate)
    {
        int levelBubbleCount = Mathf.Min(LevelManager.Instance.BubbleCatIDs.Count, spawnedBubbles.Count);
        int uprightCount = 0;

        for (int i = 0; i < levelBubbleCount; i++)
        {
            CatBubble bubble = spawnedBubbles[i];

            if (bubble.RemainingCat <= 0)
                continue;

            bool upright = uprightCount < uprightBubbleCount;
            bubble.SetCatsUpright(upright, animate);
            uprightCount++;
        }
    }

    CatBubble GetBubble(int index)
    {
        if (index < spawnedBubbles.Count)
            return spawnedBubbles[index];

        CatBubble bubble = Instantiate(catBubblePrefab, bubbleContainer).GetComponent<CatBubble>();
        spawnedBubbles.Add(bubble);

        // Lắng nghe click của mọi con mèo trong bóng (chỉ đăng ký 1 lần lúc tạo, bóng được dùng lại qua các level)
        foreach (Cat cat in bubble.AllCats)
            cat.OnClicked += OnCatClicked;

        bubble.OnPopped += OnBubblePopped;

        return bubble;
    }

    float GetBubbleRadius(CatBubble bubble)
    {
        return bubble.Radius * bubble.transform.localScale.x;
    }

    /// <summary>
    /// Tìm vị trí ngẫu nhiên trong bubbleContainer không chồng lên các bóng đã đặt.
    /// Không tìm được thì lấy vị trí bị chồng ít nhất.
    /// </summary>
    Vector2 FindBubblePosition(float radius)
    {
        Rect area = bubbleContainer.rect;

        float minX = area.xMin + radius;
        float maxX = area.xMax - radius;
        float minY = area.yMin + radius;
        float maxY = area.yMax - radius;

        // Bóng to hơn vùng chứa thì đặt ở giữa
        if (minX > maxX) minX = maxX = area.center.x;
        if (minY > maxY) minY = maxY = area.center.y;

        Vector2 bestPos = area.center;
        float bestOverlap = float.MaxValue;

        for (int attempt = 0; attempt < maxPlaceAttempts; attempt++)
        {
            Vector2 pos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            float overlap = GetMaxOverlap(pos, radius);

            if (overlap <= 0f)
                return pos;

            if (overlap < bestOverlap)
            {
                bestOverlap = overlap;
                bestPos = pos;
            }
        }

        return bestPos;
    }

    float GetMaxOverlap(Vector2 pos, float radius)
    {
        float maxOverlap = 0f;

        for (int i = 0; i < placedPositions.Count; i++)
        {
            float minDistance = radius + placedRadiuses[i] + bubbleSpacing;
            float overlap = minDistance - Vector2.Distance(pos, placedPositions[i]);

            if (overlap > maxOverlap)
                maxOverlap = overlap;
        }

        return maxOverlap;
    }

    #endregion


    #region FROZEN BUBBLE METHODS

    /// <summary>
    /// Mọi bóng đóng băng nằm cạnh bóng vừa được bấm mèo bị trừ 1 lần, hết lần thì vỡ băng.
    /// </summary>
    void UnfreezeNeighborBubbles(CatBubble sourceBubble)
    {
        for (int i = 0; i < shownBubbleCount; i++)
        {
            CatBubble bubble = spawnedBubbles[i];

            if (bubble == sourceBubble || !bubble.IsFrozen || bubble.RemainingCat <= 0)
                continue;

            if (IsNeighborBubble(sourceBubble, bubble))
                bubble.HitFrozen();
        }
    }

    /// <summary>
    /// 2 bóng "bên cạnh" nhau: khoảng hở giữa 2 viền bóng không quá frozenNeighborDistance.
    /// </summary>
    bool IsNeighborBubble(CatBubble a, CatBubble b)
    {
        float distance = Vector2.Distance(a.transform.localPosition, b.transform.localPosition);
        float gap = distance - GetBubbleRadius(a) - GetBubbleRadius(b);

        return gap <= frozenNeighborDistance;
    }

    /// <summary>
    /// Chống kẹt: đã đặt hết bóng mà bóng còn mèo đều đóng băng (không còn bóng nào để bấm phá băng)
    /// thì phá băng tất cả.
    /// </summary>
    void UnfreezeIfOnlyFrozenLeft()
    {
        if (placedBubbleCount < levelBubbleCount)
            return;

        bool hasFrozenBubble = false;

        for (int i = 0; i < placedBubbleCount; i++)
        {
            CatBubble bubble = spawnedBubbles[i];
            if (bubble.RemainingCat <= 0)
                continue;

            if (!bubble.IsFrozen)
                return;

            hasFrozenBubble = true;
        }

        if (!hasFrozenBubble)
            return;

        for (int i = 0; i < placedBubbleCount; i++)
        {
            if (spawnedBubbles[i].RemainingCat > 0)
                spawnedBubbles[i].Unfreeze();
        }
    }

    #endregion


    #region CAT QUERY METHODS

    /// <summary>
    /// Lấy mèo đang hiển thị trong maxBubbleCount bóng đầu (thứ tự sinh, bỏ bóng đã vỡ / chưa sinh ra).
    /// catID = -1: mọi loại. maxCats: lấy tối đa bao nhiêu con. Không lấy mèo đang bay.
    /// Dùng cho power-up (làm đầy ổ, trộn mèo).
    /// </summary>
    public List<Cat> GetCatsInBubbles(int maxBubbleCount, int catID = -1, int maxCats = int.MaxValue)
    {
        List<Cat> result = new List<Cat>();

        int levelBubbleCount = Mathf.Min(LevelManager.Instance.BubbleCatIDs.Count, spawnedBubbles.Count);
        int checkedBubbles = 0;

        for (int i = 0; i < levelBubbleCount && checkedBubbles < maxBubbleCount && result.Count < maxCats; i++)
        {
            CatBubble bubble = spawnedBubbles[i];

            // Bóng đã vỡ, chưa hiện lên màn hình hoặc đang đóng băng thì bỏ qua
            if (bubble.RemainingCat <= 0 || bubble.Cats == null || !bubble.gameObject.activeInHierarchy || bubble.IsFrozen)
                continue;

            checkedBubbles++;

            foreach (Cat cat in bubble.Cats)
            {
                if (result.Count >= maxCats)
                    break;

                if (!cat.gameObject.activeInHierarchy || cat.IsFlying)
                    continue;

                if (catID >= 0 && cat.CatID != catID)
                    continue;

                result.Add(cat);
            }
        }

        return result;
    }

    /// <summary>
    /// Đếm số mèo còn lại của từng loại chưa được ổ nào nhận:
    /// mèo còn nằm trong bóng (cả bóng chưa sinh ra) + mèo trong khay (kể cả đang bay tới khay).
    /// Mèo đang bay tới ổ đã được tính vào ổ nên không đếm.
    /// </summary>
    public Dictionary<int, int> GetRemainingCatCounts()
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();

        int levelBubbleCount = Mathf.Min(LevelManager.Instance.BubbleCatIDs.Count, spawnedBubbles.Count);

        for (int i = 0; i < levelBubbleCount; i++)
        {
            CatBubble bubble = spawnedBubbles[i];

            if (bubble.RemainingCat <= 0 || bubble.Cats == null)
                continue;

            foreach (Cat cat in bubble.Cats)
            {
                if (!cat.gameObject.activeSelf || cat.IsFlying)
                    continue;

                counts.TryGetValue(cat.CatID, out int count);
                counts[cat.CatID] = count + 1;
            }
        }

        if (catTray != null)
        {
            foreach (int trayCatID in catTray.GetCatIDsInTray())
            {
                counts.TryGetValue(trayCatID, out int count);
                counts[trayCatID] = count + catTray.CountCatsByID(trayCatID);
            }

            // Mèo trong ô 6 (power-up Dọn khay) cũng còn chờ vào ổ
            foreach (int vacuumedCatID in catTray.GetVacuumedCatTypes())
            {
                counts.TryGetValue(vacuumedCatID, out int count);
                counts[vacuumedCatID] = count + catTray.CountVacuumedCatsByID(vacuumedCatID);
            }
        }

        return counts;
    }

    /// <summary>
    /// Kiểm tra bubbleCount bóng đầu tiên (theo thứ tự sinh) còn mèo, trả về danh sách ID mèo
    /// xếp từ nhiều nhất đến ít nhất. Bằng nhau thì loại xuất hiện trước đứng trước.
    /// Chỉ đếm mèo còn nằm trong bóng (không tính mèo đang bay / đã bay đi).
    /// </summary>
    public List<int> GetMostCommonCatIDs(int bubbleCount = 20)
    {
        Dictionary<int, int> catCounts = new Dictionary<int, int>();
        Dictionary<int, int> firstSeenOrder = new Dictionary<int, int>();

        // Chỉ xét bóng của level hiện tại (bóng thừa của level trước nằm sau trong pool)
        int levelBubbleCount = Mathf.Min(LevelManager.Instance.BubbleCatIDs.Count, spawnedBubbles.Count);
        int checkedBubbles = 0;

        for (int i = 0; i < levelBubbleCount && checkedBubbles < bubbleCount; i++)
        {
            CatBubble bubble = spawnedBubbles[i];

            // Bóng đã vỡ / đang đóng băng (chưa lấy được mèo) thì bỏ qua, không tính vào 20 bóng
            if (bubble.RemainingCat <= 0 || bubble.Cats == null || bubble.IsFrozen)
                continue;

            checkedBubbles++;

            foreach (Cat cat in bubble.Cats)
            {
                if (!cat.gameObject.activeSelf || cat.IsFlying)
                    continue;

                if (!catCounts.ContainsKey(cat.CatID))
                {
                    catCounts[cat.CatID] = 0;
                    firstSeenOrder[cat.CatID] = firstSeenOrder.Count;
                }

                catCounts[cat.CatID]++;
            }
        }

        List<int> result = new List<int>(catCounts.Keys);
        result.Sort((a, b) =>
        {
            int compare = catCounts[b].CompareTo(catCounts[a]);
            return compare != 0 ? compare : firstSeenOrder[a].CompareTo(firstSeenOrder[b]);
        });

        return result;
    }

    #endregion
}