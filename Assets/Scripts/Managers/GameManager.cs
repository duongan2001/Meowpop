using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    Coroutine spawnCoroutine;
    bool isGameOver;            // dừng chơi (thắng hoặc thua)
    int collectedCatCount;      // số mèo đã thu thập, cộng mỗi khi 1 ổ hoàn thành
    int playingLevelNumber;     // level đang chơi (OnLevelWin lưu trước level tiếp theo nên không lấy từ LevelManager được)

    readonly List<CatBubble> spawnedBubbles = new List<CatBubble>();
    readonly List<Vector2> placedPositions = new List<Vector2>();
    readonly List<float> placedRadiuses = new List<float>();

    #endregion


    #region PROPERTIES

    public IReadOnlyList<CatBubble> SpawnedBubbles => spawnedBubbles;

    // Đang sinh bóng dở, dùng khi check thắng (chưa sinh hết bóng thì chưa thắng)
    public bool IsSpawningBubbles => spawnCoroutine != null;
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
    }

    private void OnDisable()
    {
        if (catTray != null)
            catTray.OnTrayFull -= OnTrayFull;
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
        Debug.Log($"[Game Manager] Load Level: {playingLevelNumber}");

        LevelManager.Instance.LoadLevelData();

        if (LevelManager.Instance.CurrentLevel == null)
            return;

        isGameOver = false;
        collectedCatCount = 0;
        UpdateCollectedCountUI();

        if (catTray != null)
            catTray.ResetTray();

        SpawnBubbles();

        // Bóng đã setup xong dữ liệu mèo (dù chưa hiện) nên ổ chọn được loại mèo ngay
        SetUpCatBeds();
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

        // TODO: mở popup thắng, bấm tiếp tục thì gọi LoadLevel()
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

    void OnTrayFull()
    {
        isGameOver = true;
        // TODO: mở popup thua
    }

    #endregion


    #region BUBBLE METHODS

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
        List<CatBubble> activeBubbles = new List<CatBubble>(bubbleCatIDs.Count);

        // Setup bóng, dùng lại bóng cũ nếu đã sinh ở level trước
        for (int i = 0; i < bubbleCatIDs.Count; i++)
        {
            CatBubble bubble = GetBubble(i);
            bubble.SetUpCatBubble(bubbleCatIDs[i]);
            activeBubbles.Add(bubble);
        }

        // Tắt các bóng thừa của level trước (reset trước để mèo đang bay dở không bay tiếp)
        for (int i = bubbleCatIDs.Count; i < spawnedBubbles.Count; i++)
        {
            spawnedBubbles[i].ResetBubble();
            spawnedBubbles[i].gameObject.SetActive(false);
        }

        // Giữ thứ tự sinh theo dữ liệu level (đã random sẵn)
        List<CatBubble> spawnOrder = new List<CatBubble>(activeBubbles);

        // Tính trước vị trí cho tất cả bóng, đặt bóng to trước để dễ xếp không bị chồng
        activeBubbles.Sort((a, b) => GetBubbleRadius(b).CompareTo(GetBubbleRadius(a)));

        placedPositions.Clear();
        placedRadiuses.Clear();

        foreach (CatBubble bubble in activeBubbles)
        {
            float radius = GetBubbleRadius(bubble);
            Vector2 pos = FindBubblePosition(radius);

            bubble.transform.localPosition = pos;
            placedPositions.Add(pos);
            placedRadiuses.Add(radius);

            // Ẩn đi, chờ tới lượt mới hiện
            bubble.gameObject.SetActive(false);
        }

        spawnCoroutine = StartCoroutine(SpawnBubblesSequentially(spawnOrder));
    }

    /// <summary>
    /// Hiện lần lượt từng bóng, mỗi bóng cách nhau bubbleSpawnInterval giây.
    /// </summary>
    IEnumerator SpawnBubblesSequentially(List<CatBubble> bubbles)
    {
        WaitForSeconds wait = new WaitForSeconds(bubbleSpawnInterval);

        for (int i = 0; i < bubbles.Count; i++)
        {
            bubbles[i].gameObject.SetActive(true);
            bubbles[i].PlayAppear();

            // Bóng cuối không cần chờ
            if (i < bubbles.Count - 1)
                yield return wait;
        }

        spawnCoroutine = null;
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


    #region CAT QUERY METHODS

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

            // Bóng đã vỡ thì bỏ qua, không tính vào 20 bóng
            if (bubble.RemainingCat <= 0 || bubble.Cats == null)
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