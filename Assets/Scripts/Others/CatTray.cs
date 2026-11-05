using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CatTray : MonoBehaviour
{
    #region VARIABLES

    const int EMPTY = -1;
    const int MAIN_CELL_COUNT = 5;      // 5 ô đầu: ô chờ bình thường, đầy là thua
    const int EXTRA_CELL_INDEX = 5;     // ô thứ 6: chỉ bật khi dùng powerup hút

    // Bắn ra khi 5 ô chờ đều có mèo (thua)
    public event Action OnTrayFull;

    //Cat collected
    [SerializeField] Text catCollectedCountText;

    //Cat tray
    [SerializeField] Image[] trayCellImages;
    [SerializeField] Sprite trayFirstCellSprite;
    [SerializeField] Sprite trayMiddleCellSprite;
    [SerializeField] Sprite trayLastCellSprite;

    [SerializeField] Image[] catInTrayImages;
    [Tooltip("Text số mèo đã bị hút vào ô extra (ô thứ 6)")]
    [FormerlySerializedAs("powerupUsedCatNumberText")]   // giữ nguyên reference đã kéo trong Inspector
    [SerializeField] Text suckedCatCountText;

    // Dữ liệu từng ô chờ: catID hoặc EMPTY
    int[] trayCatIDs = new int[MAIN_CELL_COUNT];
    // Mèo đã bay tới ô chưa (đã giữ chỗ nhưng còn đang bay thì chưa hiện hình)
    bool[] isCatArrived = new bool[MAIN_CELL_COUNT];

    // Danh sách ID mèo đã bị hút vào ô extra
    readonly List<int> suckedCatIDs = new List<int>();

    [Header("Fly To Bed")]
    [SerializeField] float flyToBedDuration = 0.35f;
    [Tooltip("Mỗi con bay sau con trước 1 khoảng (giây)")]
    [SerializeField] float flyToBedStagger = 0.08f;
    [SerializeField] Ease flyToBedEase = Ease.InOutSine;

    // Pool ảnh mèo dùng để bay từ khay lên ổ
    readonly List<Image> flyingImagePool = new List<Image>();

    #endregion


    #region PROPERTIES

    public IReadOnlyList<int> SuckedCatIDs => suckedCatIDs;
    public int CatCountInTray => CountCatsInTray();
    public bool IsFull => CountCatsInTray() >= MAIN_CELL_COUNT;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        ResetTray();
    }

    #endregion


    #region SETUP METHODS

    /// <summary>
    /// Đặt sprite cho các ô đang bật: ô đầu - ô giữa - ô cuối.
    /// </summary>
    public void SetupTrayCellImages()
    {
        List<Image> activeCells = new List<Image>(trayCellImages.Length);

        foreach (Image cell in trayCellImages)
        {
            if (cell != null && cell.gameObject.activeSelf)
                activeCells.Add(cell);
        }

        for (int i = 0; i < activeCells.Count; i++)
        {
            if (i == 0)
                activeCells[i].sprite = trayFirstCellSprite;
            else if (i == activeCells.Count - 1)
                activeCells[i].sprite = trayLastCellSprite;
            else
                activeCells[i].sprite = trayMiddleCellSprite;
        }
    }

    /// <summary>
    /// Dọn sạch khay (gọi khi load level mới).
    /// </summary>
    public void ResetTray()
    {
        ReturnAllFlyingImages();

        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            trayCatIDs[i] = EMPTY;
            isCatArrived[i] = false;
        }

        suckedCatIDs.Clear();
        SetExtraCellActive(false);

        RefreshCatImages();
        SetupTrayCellImages();
    }

    #endregion


    #region COLLECTION METHODS

    /// <summary>
    /// Cập nhật text số mèo đã thu thập, VD: 5/111.
    /// </summary>
    public void UpdateCatCollectedCount(int collectedCount, int totalCount)
    {
        if (catCollectedCountText != null)
            catCollectedCountText.text = $"{collectedCount}/{totalCount}";
    }

    #endregion


    #region TRAY METHODS

    /// <summary>
    /// Giữ chỗ 1 ô trống cho mèo (gọi ngay khi bấm mèo để mèo bay tới).
    /// Trả về index ô, -1 nếu khay đã đầy. Khay đầy sau khi thêm thì thua.
    /// </summary>
    public int AddCatToTray(int catID)
    {
        int cellIndex = GetFirstEmptyCell();
        if (cellIndex == EMPTY)
            return EMPTY;

        trayCatIDs[cellIndex] = catID;
        isCatArrived[cellIndex] = false;

        if (IsFull)
        {
            Debug.Log("[CatTray] Khay chờ đầy 5/5 - THUA");
            OnTrayFull?.Invoke();
        }

        return cellIndex;
    }

    /// <summary>
    /// Gọi khi mèo bay tới khay xong: hiện hình mèo ở ô đã giữ chỗ.
    /// </summary>
    public void OnCatArrived(int catID)
    {
        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] == catID && !isCatArrived[i])
            {
                isCatArrived[i] = true;
                RefreshCatImage(i);
                return;
            }
        }
    }

    /// <summary>
    /// Vị trí ô để mèo bay tới.
    /// </summary>
    public RectTransform GetCellRect(int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= trayCellImages.Length)
            return null;

        return trayCellImages[cellIndex].rectTransform;
    }

    /// <summary>
    /// Danh sách loại mèo đang có trong khay (không trùng, theo thứ tự ô).
    /// </summary>
    public List<int> GetCatIDsInTray()
    {
        List<int> catIDs = new List<int>(MAIN_CELL_COUNT);

        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] != EMPTY && !catIDs.Contains(trayCatIDs[i]))
                catIDs.Add(trayCatIDs[i]);
        }

        return catIDs;
    }

    /// <summary>
    /// Số mèo của 1 loại đang có trong khay (gồm cả mèo đang bay tới khay).
    /// </summary>
    public int CountCatsByID(int catID)
    {
        int count = 0;

        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] == catID)
                count++;
        }

        return count;
    }

    int GetFirstEmptyCell()
    {
        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] == EMPTY)
                return i;
        }

        return EMPTY;
    }

    int CountCatsInTray()
    {
        int count = 0;

        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] != EMPTY)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Dồn mèo về bên trái, không để ô trống ở giữa.
    /// </summary>
    void CompactTray()
    {
        int writeIndex = 0;

        for (int readIndex = 0; readIndex < MAIN_CELL_COUNT; readIndex++)
        {
            if (trayCatIDs[readIndex] == EMPTY)
                continue;

            trayCatIDs[writeIndex] = trayCatIDs[readIndex];
            isCatArrived[writeIndex] = isCatArrived[readIndex];
            writeIndex++;
        }

        for (int i = writeIndex; i < MAIN_CELL_COUNT; i++)
        {
            trayCatIDs[i] = EMPTY;
            isCatArrived[i] = false;
        }

        RefreshCatImages();
    }

    void RefreshCatImages()
    {
        for (int i = 0; i < MAIN_CELL_COUNT; i++)
            RefreshCatImage(i);
    }

    void RefreshCatImage(int cellIndex)
    {
        if (cellIndex >= catInTrayImages.Length || catInTrayImages[cellIndex] == null)
            return;

        Image catImage = catInTrayImages[cellIndex];
        bool showCat = trayCatIDs[cellIndex] != EMPTY && isCatArrived[cellIndex];

        // Gán sprite trước rồi mới bật object để không bị nháy hình cũ
        if (showCat)
            catImage.sprite = GetCatSprite(trayCatIDs[cellIndex]);

        catImage.gameObject.SetActive(showCat);
    }

    Sprite GetCatSprite(int catID)
    {
        Sprite[] sprites = GameManager.Instance.catSprites;

        if (sprites == null || catID < 0 || catID >= sprites.Length)
            return null;

        return sprites[catID];
    }

    #endregion


    #region FLY TO BED METHODS

    /// <summary>
    /// Cho các mèo cùng loại với ổ (đã nằm trong khay) bay từ khay lên ổ.
    /// Mèo còn đang bay tới khay thì chưa gửi, tới nơi GameManager sẽ gọi lại hàm này.
    /// Trả về số mèo đã gửi.
    /// </summary>
    public int SendCatsToBed(CatBed bed)
    {
        if (bed == null || bed.CatID == EMPTY)
            return 0;

        int sentCount = 0;

        for (int i = 0; i < MAIN_CELL_COUNT && !bed.IsFull; i++)
        {
            if (trayCatIDs[i] != bed.CatID || !isCatArrived[i])
                continue;

            int slotIndex = bed.ReserveSlot();
            FlyCatImage(catInTrayImages[i], bed.GetSlotRect(slotIndex), sentCount * flyToBedStagger, bed.AddCatToBed);

            trayCatIDs[i] = EMPTY;
            isCatArrived[i] = false;
            sentCount++;
        }

        // Ẩn mèo ở ô cũ và dồn khay ngay, ảnh bay là bản sao nên không bị ảnh hưởng
        if (sentCount > 0)
            CompactTray();

        return sentCount;
    }

    /// <summary>
    /// Tạo 1 ảnh mèo bay từ vị trí ô khay tới chỗ trong ổ, tới nơi gọi onArrived.
    /// </summary>
    void FlyCatImage(Image fromImage, RectTransform toRect, float delay, Action onArrived)
    {
        RectTransform flyContainer = GameManager.Instance.catFlyContainer;
        Image flyImage = GetFlyingImage(flyContainer);
        RectTransform flyRect = flyImage.rectTransform;

        flyImage.sprite = fromImage.sprite;
        flyRect.SetAsLastSibling();

        // Đặt đúng vị trí + kích thước của ảnh mèo trong ô khay
        RectTransform fromRect = fromImage.rectTransform;
        flyRect.position = GetWorldCenter(fromRect);
        flyRect.rotation = Quaternion.identity;
        flyRect.localScale = Vector3.one;
        flyRect.sizeDelta = GetSizeInContainer(fromRect, flyContainer);

        Vector3 targetPos = GetWorldCenter(toRect);
        Vector2 targetSize = GetSizeInContainer(toRect, flyContainer);

        flyRect.DOKill();
        Sequence flySeq = DOTween.Sequence();
        flySeq.SetDelay(delay);
        flySeq.Append(flyRect.DOMove(targetPos, flyToBedDuration).SetEase(flyToBedEase));
        flySeq.Join(flyRect.DOSizeDelta(targetSize, flyToBedDuration).SetEase(flyToBedEase));
        flySeq.SetTarget(flyRect);
        flySeq.OnComplete(() =>
        {
            flyImage.gameObject.SetActive(false);
            onArrived?.Invoke();
        });
    }

    Image GetFlyingImage(RectTransform flyContainer)
    {
        foreach (Image image in flyingImagePool)
        {
            if (!image.gameObject.activeSelf)
            {
                image.gameObject.SetActive(true);
                return image;
            }
        }

        // Nhân bản từ ảnh mèo trong khay để giữ đúng style (preserveAspect, material...)
        Image newImage = Instantiate(catInTrayImages[0], flyContainer);
        newImage.name = "Flying Cat Image";
        newImage.raycastTarget = false;

        RectTransform rect = newImage.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

        newImage.gameObject.SetActive(true);
        flyingImagePool.Add(newImage);
        return newImage;
    }

    void ReturnAllFlyingImages()
    {
        // DOKill không gọi OnComplete nên mèo đang bay dở sẽ không được thêm vào ổ
        foreach (Image image in flyingImagePool)
        {
            image.rectTransform.DOKill();
            image.gameObject.SetActive(false);
        }
    }

    Vector3 GetWorldCenter(RectTransform rect)
    {
        return rect.TransformPoint(rect.rect.center);
    }

    /// <summary>
    /// Đổi kích thước của rect sang đơn vị local của flyContainer (2 bên có thể khác scale).
    /// </summary>
    Vector2 GetSizeInContainer(RectTransform rect, RectTransform container)
    {
        float scaleRatio = rect.lossyScale.x / container.lossyScale.x;
        return rect.rect.size * scaleRatio;
    }

    #endregion


    #region POWERUP METHODS

    /// <summary>
    /// Powerup hút: chuyển toàn bộ mèo đã tới khay vào ô extra (ô thứ 6).
    /// Mèo đang bay tới khay thì giữ lại.
    /// </summary>
    public void UseSuckPowerup()
    {
        int lastSuckedCatID = EMPTY;

        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] == EMPTY || !isCatArrived[i])
                continue;

            suckedCatIDs.Add(trayCatIDs[i]);
            lastSuckedCatID = trayCatIDs[i];

            trayCatIDs[i] = EMPTY;
            isCatArrived[i] = false;
        }

        if (lastSuckedCatID == EMPTY)
            return;

        CompactTray();

        SetExtraCellActive(true);
        SetupTrayCellImages();

        // Ô extra hiện hình con mèo hút vào cuối cùng
        if (EXTRA_CELL_INDEX < catInTrayImages.Length && catInTrayImages[EXTRA_CELL_INDEX] != null)
        {
            catInTrayImages[EXTRA_CELL_INDEX].sprite = GetCatSprite(lastSuckedCatID);
            catInTrayImages[EXTRA_CELL_INDEX].gameObject.SetActive(true);
        }

        if (suckedCatCountText != null)
            suckedCatCountText.text = suckedCatIDs.Count.ToString();
    }

    void SetExtraCellActive(bool isActive)
    {
        if (EXTRA_CELL_INDEX < trayCellImages.Length && trayCellImages[EXTRA_CELL_INDEX] != null)
            trayCellImages[EXTRA_CELL_INDEX].gameObject.SetActive(isActive);

        if (EXTRA_CELL_INDEX < catInTrayImages.Length && catInTrayImages[EXTRA_CELL_INDEX] != null)
            catInTrayImages[EXTRA_CELL_INDEX].gameObject.SetActive(isActive);

        if (suckedCatCountText != null)
            suckedCatCountText.gameObject.SetActive(isActive);
    }

    #endregion
}