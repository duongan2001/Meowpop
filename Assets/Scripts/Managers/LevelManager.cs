using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    #region VARIABLES

    public static LevelManager Instance;

    const string CURRENT_LEVEL_KEY = "CurrentLevel";

    public LevelData[] levelDatas;

    LevelData currentLevel;
    int currentLevelNumber;

    // Kết quả random của level hiện tại
    List<int> levelCatIDs = new List<int>();           // các loại mèo xuất hiện trong level
    int[] catCountEachType;                              // số mèo của từng loại (cùng index với levelCatIDs)
    List<int> allCats = new List<int>();               // toàn bộ mèo của level (đã trộn)
    List<int[]> bubbleCatIDs = new List<int[]>();      // mèo trong từng bóng

    #endregion


    #region PROPERTIES

    public LevelData CurrentLevel => currentLevel;
    public IReadOnlyList<int> LevelCatIDs => levelCatIDs;
    public IReadOnlyList<int> CatCountEachType => catCountEachType;
    public IReadOnlyList<int[]> BubbleCatIDs => bubbleCatIDs;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentLevelNumber = PlayerPrefs.GetInt(CURRENT_LEVEL_KEY, 1);
    }

    #endregion


    #region LEVEL METHODS

    public int GetCurrentLevel()
    {
        return currentLevelNumber;
    }

    /// <summary>
    /// Đặt level hiện tại. saveProgress = false: chỉ đổi level đang chơi, không ghi đè tiến trình đã lưu
    /// (dùng khi chơi lại level đã thắng).
    /// </summary>
    public void SetCurrentLevel(int level, bool saveProgress = true)
    {
        currentLevelNumber = Mathf.Max(1, level);

        if (!saveProgress)
            return;

        PlayerPrefs.SetInt(CURRENT_LEVEL_KEY, currentLevelNumber);
        PlayerPrefs.Save();
    }

    public void LoadLevelData()
    {
        if (levelDatas == null || levelDatas.Length == 0)
        {
            Debug.LogError("[LevelManager] Chưa có dữ liệu level");
            return;
        }

        // Vượt quá số level đã thiết kế thì dùng lại level cuối
        int index = Mathf.Clamp(currentLevelNumber - 1, 0, levelDatas.Length - 1);
        LevelData levelData = levelDatas[index];

        // Dữ liệu sai thì dừng luôn, không random tiếp (tránh vòng lặp vô hạn / lỗi index)
        if (!levelData.IsValid(out string error))
        {
            Debug.LogError($"[LevelManager] {error}");
            currentLevel = null;
            return;
        }

        if (GameManager.Instance.catSprites == null || GameManager.Instance.catSprites.Length == 0)
        {
            Debug.LogError("[LevelManager] GameManager chưa gán catSprites");
            currentLevel = null;
            return;
        }

        currentLevel = levelData;

        RandomCatIDs();
        RandomCatNumberEachType();
        BuildAndShuffleAllCats();
        SplitCatsIntoBubbles();
    }

    #endregion


    #region RANDOM METHODS

    /// <summary>
    /// Chọn ngẫu nhiên catTypeCount loại mèo khác nhau từ toàn bộ loại mèo trong game.
    /// </summary>
    void RandomCatIDs()
    {
        int totalCatType = GameManager.Instance.catSprites.Length;
        int typeCount = Mathf.Min(currentLevel.catTypeCount, totalCatType);

        if (typeCount < currentLevel.catTypeCount)
            Debug.LogWarning($"[LevelManager] Level cần {currentLevel.catTypeCount} loại mèo nhưng game chỉ có {totalCatType}");

        List<int> allIDs = new List<int>(totalCatType);
        for (int i = 0; i < totalCatType; i++)
            allIDs.Add(i);

        Shuffle(allIDs);

        levelCatIDs.Clear();
        levelCatIDs.AddRange(allIDs.GetRange(0, typeCount));
    }

    /// <summary>
    /// Chia random tổng số mèo cho từng loại. Mỗi loại là bội số của 3 và có ít nhất 3 con.
    /// </summary>
    void RandomCatNumberEachType()
    {
        int typeCount = levelCatIDs.Count;
        int groupCount = currentLevel.totalCatCount / LevelData.CATS_PER_BED; // số nhóm 3 con

        catCountEachType = new int[typeCount];

        // Mỗi loại nhận trước 1 nhóm
        for (int i = 0; i < typeCount; i++)
            catCountEachType[i] = LevelData.CATS_PER_BED;

        // Các nhóm còn lại chia ngẫu nhiên
        for (int g = typeCount; g < groupCount; g++)
            catCountEachType[Random.Range(0, typeCount)] += LevelData.CATS_PER_BED;
    }

    /// <summary>
    /// Tạo danh sách toàn bộ mèo của level theo số lượng từng loại rồi trộn lên.
    /// </summary>
    void BuildAndShuffleAllCats()
    {
        allCats.Clear();

        for (int i = 0; i < levelCatIDs.Count; i++)
        {
            for (int j = 0; j < catCountEachType[i]; j++)
                allCats.Add(levelCatIDs[i]);
        }

        Shuffle(allCats);
    }

    /// <summary>
    /// Chia mèo vào các bóng, mỗi bóng có từ minCatInBubble đến maxCatInBubble con.
    /// </summary>
    void SplitCatsIntoBubbles()
    {
        bubbleCatIDs.Clear();

        // Min phải >= 1, nếu không size = 0 sẽ lặp vô hạn
        int min = Mathf.Max(1, currentLevel.minCatInBubble);
        int max = Mathf.Max(min, currentLevel.maxCatInBubble);
        int remaining = allCats.Count;
        int startIndex = 0;
        List<int> validSizes = new List<int>(max);

        while (remaining > 0)
        {
            // Chỉ chọn kích thước bóng mà phần mèo còn lại vẫn chia tiếp được
            validSizes.Clear();
            for (int s = min; s <= Mathf.Min(max, remaining); s++)
            {
                if (CanSplit(remaining - s, min, max))
                    validSizes.Add(s);
            }

            // Dữ liệu không thể chia đúng min-max thì dồn hết phần còn lại vào 1 bóng
            int size = validSizes.Count > 0
                ? validSizes[Random.Range(0, validSizes.Count)]
                : remaining;

            bubbleCatIDs.Add(allCats.GetRange(startIndex, size).ToArray());

            startIndex += size;
            remaining -= size;
        }
    }

    /// <summary>
    /// Kiểm tra số mèo có chia được thành các bóng, mỗi bóng từ min đến max con hay không.
    /// </summary>
    bool CanSplit(int catCount, int min, int max)
    {
        if (catCount == 0)
            return true;

        int leastBubbleCount = Mathf.CeilToInt((float)catCount / max);
        return leastBubbleCount * min <= catCount;
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
}