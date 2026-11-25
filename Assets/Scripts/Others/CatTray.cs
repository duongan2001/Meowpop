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

    [Header("Vacuum (ô số 6)")]
    [Tooltip("Vacuum Cat Count Text - số mèo đã bị hút vào ô số 6")]
    [FormerlySerializedAs("suckedCatCountText")]
    [FormerlySerializedAs("powerupUsedCatNumberText")]   // giữ nguyên reference đã kéo trong Inspector
    [SerializeField] Text vacuumCatCountText;
    [Tooltip("Cats - Power-up: bảng hiện các loại mèo đã hút, bật/tắt khi bấm ô số 6")]
    [SerializeField] GameObject vacuumCatsPopup;
    [Tooltip("Cat Count 1..5 trong Cats - Power-up")]
    [SerializeField] VacuumCatItem[] vacuumCatItems;
    [SerializeField] float vacuumPopupDuration = 0.2f;

    // Dữ liệu từng ô chờ: catID hoặc EMPTY
    int[] trayCatIDs = new int[MAIN_CELL_COUNT];
    // Mèo đã bay tới ô chưa (đã giữ chỗ nhưng còn đang bay thì chưa hiện hình)
    bool[] isCatArrived = new bool[MAIN_CELL_COUNT];

    // Danh sách ID mèo đã bị hút vào ô số 6
    readonly List<int> vacuumedCatIDs = new List<int>();
    int displayedVacuumCount;   // số đang hiện trên ô 6 (tăng dần khi từng con bay tới)
    Vector3 vacuumPopupScale = Vector3.one;

    [Header("Fly To Bed")]
    [SerializeField] float flyToBedDuration = 0.35f;
    [Tooltip("Mỗi con bay sau con trước 1 khoảng (giây)")]
    [SerializeField] float flyToBedStagger = 0.08f;
    [SerializeField] Ease flyToBedEase = Ease.InOutSine;

    // Pool ảnh mèo dùng để bay từ khay lên ổ
    readonly List<Image> flyingImagePool = new List<Image>();

    #endregion


    #region PROPERTIES

    public IReadOnlyList<int> VacuumedCatIDs => vacuumedCatIDs;
    public int CatCountInTray => CountCatsInTray();
    public bool IsFull => CountCatsInTray() >= MAIN_CELL_COUNT;
    public bool AreAllCatsArrived => CheckAllCatsArrived();

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (vacuumCatsPopup != null)
            vacuumPopupScale = vacuumCatsPopup.transform.localScale;

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

        vacuumedCatIDs.Clear();
        displayedVacuumCount = 0;
        HideVacuumPopup(false);
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
            Debug.Log("[CatTray] Khay chờ đầy 5/5");
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

    // Mọi mèo đã giữ chỗ trong khay đều đã bay tới nơi (không còn con nào đang bay tới khay)
    bool CheckAllCatsArrived()
    {
        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] != EMPTY && !isCatArrived[i])
                return false;
        }

        return true;
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
    void FlyCatImage(Image fromImage, RectTransform toRect, float delay, Action onArrived, Sprite overrideSprite = null)
    {
        RectTransform flyContainer = GameManager.Instance.catFlyContainer;
        Image flyImage = GetFlyingImage(flyContainer);
        RectTransform flyRect = flyImage.rectTransform;

        // Ô 6 chỉ có 1 ảnh nhưng chứa nhiều loại mèo -> truyền sprite đúng loại
        flyImage.sprite = overrideSprite != null ? overrideSprite : fromImage.sprite;
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


    #region VACUUM METHODS

    /// <summary>
    /// Power-up Dọn khay: hút toàn bộ mèo đã tới khay vào ô số 6 (có hiệu ứng bay vào).
    /// Mèo đang bay tới khay thì giữ lại. Trả về true nếu có mèo được hút (để PowerUpManager trừ lượt).
    /// </summary>
    public bool UseVacuumPowerup()
    {
        List<int> cellIndexes = new List<int>();

        for (int i = 0; i < MAIN_CELL_COUNT; i++)
        {
            if (trayCatIDs[i] != EMPTY && isCatArrived[i])
                cellIndexes.Add(i);
        }

        if (cellIndexes.Count == 0)
            return false;

        // Bật ô 6 và build lại layout ngay để lấy đúng vị trí đích
        SetExtraCellActive(true);
        SetupTrayCellImages();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)trayCellImages[EXTRA_CELL_INDEX].transform.parent);

        RectTransform vacuumTarget = GetVacuumTargetRect();

        for (int n = 0; n < cellIndexes.Count; n++)
        {
            int cellIndex = cellIndexes[n];
            int catID = trayCatIDs[cellIndex];

            vacuumedCatIDs.Add(catID);

            // Ảnh bay là bản sao, phải tạo trước khi xóa ô
            FlyCatImage(catInTrayImages[cellIndex], vacuumTarget, n * flyToBedStagger, () => OnCatVacuumed(catID));

            trayCatIDs[cellIndex] = EMPTY;
            isCatArrived[cellIndex] = false;
        }

        CompactTray();

        // Bảng loại mèo đang mở thì cập nhật luôn
        if (IsVacuumPopupOpen())
            RefreshVacuumCatItems();

        return true;
    }

    /// <summary>
    /// Gán vào OnClick của Button trên Cell 6 trong Editor: bật / tắt bảng các loại mèo đã hút.
    /// </summary>
    public void OnVacuumCellClick()
    {
        if (IsVacuumPopupOpen())
            HideVacuumPopup(true);
        else
            ShowVacuumPopup();
    }

    void OnCatVacuumed(int catID)
    {
        // Không vượt quá số mèo thật trong ô 6 (ổ có thể đã lấy mèo ra trong lúc mèo còn đang bay vào)
        displayedVacuumCount = Mathf.Min(displayedVacuumCount + 1, vacuumedCatIDs.Count);

        if (vacuumCatCountText != null)
            vacuumCatCountText.text = displayedVacuumCount.ToString();

        // Ô 6 hiện hình con mèo vừa hút vào (nếu ô có ảnh mèo)
        if (EXTRA_CELL_INDEX < catInTrayImages.Length && catInTrayImages[EXTRA_CELL_INDEX] != null)
        {
            catInTrayImages[EXTRA_CELL_INDEX].sprite = GetCatSprite(catID);
            catInTrayImages[EXTRA_CELL_INDEX].gameObject.SetActive(true);
        }

        // Ô 6 nảy nhẹ mỗi lần có mèo bay vào
        Transform cell = trayCellImages[EXTRA_CELL_INDEX].transform;
        cell.DOKill(true);
        cell.DOPunchScale(Vector3.one * 0.15f, 0.2f, 1);
    }

    /// <summary>
    /// Ổ mới nhận loại mèo: lấy mèo cùng loại trong ô 6 bay lên ổ (tối đa số chỗ trống của ổ).
    /// Ô 6 hết mèo thì tự ẩn. Trả về số mèo đã gửi.
    /// </summary>
    public int SendVacuumCatsToBed(CatBed bed)
    {
        if (bed == null || bed.CatID == EMPTY || vacuumedCatIDs.Count == 0)
            return 0;

        RectTransform fromRect = GetVacuumTargetRect();
        Image fromImage = fromRect.GetComponent<Image>();
        Sprite catSprite = GetCatSprite(bed.CatID);
        int sentCount = 0;

        for (int i = vacuumedCatIDs.Count - 1; i >= 0 && !bed.IsFull; i--)
        {
            if (vacuumedCatIDs[i] != bed.CatID)
                continue;

            int slotIndex = bed.ReserveSlot();
            FlyCatImage(fromImage, bed.GetSlotRect(slotIndex), sentCount * flyToBedStagger, bed.AddCatToBed, catSprite);

            vacuumedCatIDs.RemoveAt(i);
            sentCount++;
        }

        if (sentCount > 0)
            RefreshVacuumCell();

        return sentCount;
    }

    /// <summary>
    /// Danh sách loại mèo đang nằm trong ô 6 (không trùng).
    /// </summary>
    public List<int> GetVacuumedCatTypes()
    {
        List<int> catTypes = new List<int>();

        foreach (int catID in vacuumedCatIDs)
        {
            if (!catTypes.Contains(catID))
                catTypes.Add(catID);
        }

        return catTypes;
    }

    /// <summary>
    /// Số mèo của 1 loại đang nằm trong ô 6.
    /// </summary>
    public int CountVacuumedCatsByID(int catID)
    {
        int count = 0;

        foreach (int vacuumedCatID in vacuumedCatIDs)
        {
            if (vacuumedCatID == catID)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Cập nhật ô 6 sau khi mèo được lấy ra: số, hình con mèo cuối, bảng loại mèo; hết mèo thì ẩn ô.
    /// </summary>
    void RefreshVacuumCell()
    {
        displayedVacuumCount = Mathf.Min(displayedVacuumCount, vacuumedCatIDs.Count);

        if (vacuumedCatIDs.Count == 0)
        {
            HideVacuumPopup(true);
            SetExtraCellActive(false);
            SetupTrayCellImages();
            return;
        }

        if (vacuumCatCountText != null)
            vacuumCatCountText.text = displayedVacuumCount.ToString();

        if (EXTRA_CELL_INDEX < catInTrayImages.Length && catInTrayImages[EXTRA_CELL_INDEX] != null)
            catInTrayImages[EXTRA_CELL_INDEX].sprite = GetCatSprite(vacuumedCatIDs[vacuumedCatIDs.Count - 1]);

        if (IsVacuumPopupOpen())
            RefreshVacuumCatItems();
    }

    RectTransform GetVacuumTargetRect()
    {
        if (EXTRA_CELL_INDEX < catInTrayImages.Length && catInTrayImages[EXTRA_CELL_INDEX] != null)
            return catInTrayImages[EXTRA_CELL_INDEX].rectTransform;

        return trayCellImages[EXTRA_CELL_INDEX].rectTransform;
    }

    void ShowVacuumPopup()
    {
        if (vacuumCatsPopup == null || vacuumedCatIDs.Count == 0)
            return;

        RefreshVacuumCatItems();

        Transform popup = vacuumCatsPopup.transform;
        popup.DOKill();
        vacuumCatsPopup.SetActive(true);
        popup.localScale = Vector3.zero;
        popup.DOScale(vacuumPopupScale, vacuumPopupDuration).SetEase(Ease.OutBack);
    }

    void HideVacuumPopup(bool animate)
    {
        if (vacuumCatsPopup == null)
            return;

        Transform popup = vacuumCatsPopup.transform;
        popup.DOKill();

        if (!animate || !vacuumCatsPopup.activeInHierarchy)
        {
            popup.localScale = vacuumPopupScale;
            vacuumCatsPopup.SetActive(false);
            return;
        }

        popup.DOScale(Vector3.zero, vacuumPopupDuration).SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                vacuumCatsPopup.SetActive(false);
                popup.localScale = vacuumPopupScale;
            });
    }

    bool IsVacuumPopupOpen()
    {
        return vacuumCatsPopup != null && vacuumCatsPopup.activeSelf;
    }

    /// <summary>
    /// Gom mèo đã hút theo loại (giữ thứ tự hút vào), điền vào Cat Count 1..5, ô thừa thì ẩn.
    /// </summary>
    void RefreshVacuumCatItems()
    {
        if (vacuumCatItems == null)
            return;

        List<int> catTypes = new List<int>();
        Dictionary<int, int> catCounts = new Dictionary<int, int>();

        foreach (int catID in vacuumedCatIDs)
        {
            if (!catCounts.ContainsKey(catID))
            {
                catTypes.Add(catID);
                catCounts[catID] = 0;
            }

            catCounts[catID]++;
        }

        if (catTypes.Count > vacuumCatItems.Length)
            Debug.LogWarning($"[CatTray] Có {catTypes.Count} loại mèo trong ô 6 nhưng chỉ có {vacuumCatItems.Length} ô hiển thị");

        for (int i = 0; i < vacuumCatItems.Length; i++)
        {
            VacuumCatItem item = vacuumCatItems[i];
            bool hasCat = i < catTypes.Count;

            if (item.root != null)
                item.root.SetActive(hasCat);

            if (!hasCat)
                continue;

            int catID = catTypes[i];

            if (item.countText != null)
                item.countText.text = $"x{catCounts[catID]}";

            if (item.catIcon != null)
                item.catIcon.sprite = GetCatSprite(catID);
        }
    }

    void SetExtraCellActive(bool isActive)
    {
        if (EXTRA_CELL_INDEX < trayCellImages.Length && trayCellImages[EXTRA_CELL_INDEX] != null)
            trayCellImages[EXTRA_CELL_INDEX].gameObject.SetActive(isActive);

        if (EXTRA_CELL_INDEX < catInTrayImages.Length && catInTrayImages[EXTRA_CELL_INDEX] != null)
            catInTrayImages[EXTRA_CELL_INDEX].gameObject.SetActive(false);

        if (vacuumCatCountText != null)
        {
            vacuumCatCountText.gameObject.SetActive(isActive);
            vacuumCatCountText.text = displayedVacuumCount.ToString();
        }
    }

    #endregion
}


/// <summary>
/// 1 dòng trong bảng Cats - Power-up: "x5 [hình mèo]".
/// </summary>
[Serializable]
public class VacuumCatItem
{
    [Tooltip("Cat Count N")]
    public GameObject root;
    [Tooltip("Count Text")]
    public Text countText;
    [Tooltip("Cat Icon")]
    public Image catIcon;
}