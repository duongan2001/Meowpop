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

    public RectTransform testPos;

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

    readonly List<CatBubble> spawnedBubbles = new List<CatBubble>();
    readonly List<Vector2> placedPositions = new List<Vector2>();
    readonly List<float> placedRadiuses = new List<float>();

    #endregion


    #region PROPERTIES

    public IReadOnlyList<CatBubble> SpawnedBubbles => spawnedBubbles;

    // Đang sinh bóng dở, dùng khi check thắng (chưa sinh hết bóng thì chưa thắng)
    public bool IsSpawningBubbles => spawnCoroutine != null;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        Instance = this;
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
        LevelManager.Instance.LoadLevelData();

        if (LevelManager.Instance.CurrentLevel == null)
            return;

        // TODO: reset catBeds và catTray khi có code CatBed / CatTray

        SpawnBubbles();
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
}