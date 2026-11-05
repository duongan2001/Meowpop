using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CatBed : MonoBehaviour
{
    #region VARIABLES

    public const int NO_CAT = -1;
    const string UNLOCK_KEY_PREFIX = "CatBedUnlocked_";

    int catID = NO_CAT;
    int reservedCount;      // số mèo đã nhận (kể cả đang bay tới)
    int arrivedCount;       // số mèo đã bay tới nơi
    Coroutine completeCoroutine;
    Vector3 zoomTargetScale;

    [SerializeField] bool isDefaultCatBed;

    [SerializeField] Image[] catImages;
    [SerializeField] GameObject catBedLockState;
    [SerializeField] Image targetCatBackgroundImage;
    [SerializeField] Image targetCatImage;
    [SerializeField] Text targetCatCurrentCollectedCount;

    [Header("Complete")]
    [Tooltip("Chờ sau khi con thứ 3 vào ổ rồi mới reset ổ")]
    [SerializeField] float completeDelay = 0.3f;
    [Tooltip("Object được zoom khi đổi ổ, để trống thì zoom cả ổ")]
    [SerializeField] Transform zoomTarget;
    [SerializeField] float zoomOutDuration = 0.2f;
    [SerializeField] float zoomInDuration = 0.25f;

    #endregion


    #region PROPERTIES

    public int CatID => catID;
    public int Capacity => catImages.Length;
    public bool IsUnlocked => isDefaultCatBed || PlayerPrefs.GetInt(GetUnlockKey(), 0) == 1;
    public bool IsFull => reservedCount >= Capacity;
    // Số chỗ còn trống (chưa có mèo nào nhận, kể cả mèo đang bay tới)
    public int FreeSlotCount => Capacity - reservedCount;
    // Ổ đã mở nhưng chưa nhận loại mèo nào
    public bool IsWaiting => IsUnlocked && catID == NO_CAT;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (zoomTarget == null)
            zoomTarget = transform;

        zoomTargetScale = zoomTarget.localScale;
    }

    private void OnDisable()
    {
        StopCompleteCoroutine();
        ResetZoom();
    }

    #endregion


    #region LOCK METHODS

    /// <summary>
    /// Kiểm tra ổ đã mở chưa, mở rồi thì tắt lockState. Trả về true nếu ổ đang mở.
    /// </summary>
    public bool CheckLockState()
    {
        bool isUnlocked = IsUnlocked;

        if (catBedLockState != null)
            catBedLockState.SetActive(!isUnlocked);

        return isUnlocked;
    }

    /// <summary>
    /// Mở khóa ổ (lưu lại), sau đó setup ổ để nhận mèo.
    /// </summary>
    public void UnlockBed()
    {
        if (IsUnlocked)
            return;

        PlayerPrefs.SetInt(GetUnlockKey(), 1);
        PlayerPrefs.Save();

        CheckLockState();
        SetupBed();
    }

    string GetUnlockKey()
    {
        int bedIndex = Array.IndexOf(GameManager.Instance.catBeds, this);
        return UNLOCK_KEY_PREFIX + bedIndex;
    }

    #endregion


    #region SETUP METHODS

    /// <summary>
    /// Chọn loại mèo cho ổ, hiện icon + bộ đếm, rồi kéo mèo cùng loại trong khay vào.
    /// </summary>
    public void SetupBed()
    {
        ClearBed();

        if (!CheckLockState())
            return;

        ApplyTargetCat(GetTargetCatID());
    }

    /// <summary>
    /// Gán thẳng 1 loại mèo cho ổ đang trống (dùng khi bấm mèo mà chưa ổ nào nhận loại đó).
    /// </summary>
    public void SetupBedForCat(int targetCatID)
    {
        ClearBed();

        if (!CheckLockState())
            return;

        ApplyTargetCat(targetCatID);
    }

    void ApplyTargetCat(int targetCatID)
    {
        catID = targetCatID;

        // Không còn loại mèo nào cho ổ này thì tắt icon, ổ đứng chờ
        bool hasTarget = catID != NO_CAT;
        if (targetCatBackgroundImage != null)
            targetCatBackgroundImage.gameObject.SetActive(hasTarget);

        if (!hasTarget)
            return;

        targetCatImage.sprite = GetCatSprite(catID);
        UpdateCollectedCountText();

        PullCatsFromTray();
    }

    /// <summary>
    /// Lấy loại mèo cho ổ. Thứ tự ưu tiên ứng viên: loại nhiều nhất trong 20 bóng đầu,
    /// loại đang nằm trong khay, rồi tới mọi loại còn lại trên sàn.
    /// Lượt 1: chọn loại chưa ổ nào nhận.
    /// Lượt 2: cho phép trùng loại với ổ khác nếu số mèo loại đó còn lại nhiều hơn số chỗ trống các ổ kia.
    /// Hết loại thì trả về NO_CAT.
    /// </summary>
    public int GetTargetCatID()
    {
        Dictionary<int, int> remainingCounts = GameManager.Instance.GetRemainingCatCounts();
        List<int> candidates = BuildCandidateCatIDs(remainingCounts);

        // Lượt 1: loại chưa có ổ nào nhận
        foreach (int candidateID in candidates)
        {
            GetFreeSlotsOfOtherBeds(candidateID, out bool isTaken);

            if (!isTaken)
                return candidateID;
        }

        // Lượt 2: loại đã có ổ nhận nhưng mèo còn lại không đủ chỗ trong các ổ đó
        foreach (int candidateID in candidates)
        {
            int freeSlots = GetFreeSlotsOfOtherBeds(candidateID, out _);
            remainingCounts.TryGetValue(candidateID, out int remaining);

            if (remaining > freeSlots)
                return candidateID;
        }

        return NO_CAT;
    }

    List<int> BuildCandidateCatIDs(Dictionary<int, int> remainingCounts)
    {
        List<int> candidates = GameManager.Instance.GetMostCommonCatIDs();

        // Thêm loại đang nằm trong khay (tránh kẹt khay khi loại đó không còn trong bóng)
        CatTray catTray = GameManager.Instance.catTray;
        if (catTray != null)
        {
            foreach (int trayCatID in catTray.GetCatIDsInTray())
            {
                if (!candidates.Contains(trayCatID))
                    candidates.Add(trayCatID);
            }
        }

        // Thêm mọi loại còn lại (nằm ngoài 20 bóng đầu), nhiều trước ít sau
        List<int> otherIDs = new List<int>();
        foreach (KeyValuePair<int, int> pair in remainingCounts)
        {
            if (pair.Value > 0 && !candidates.Contains(pair.Key))
                otherIDs.Add(pair.Key);
        }
        otherIDs.Sort((a, b) => remainingCounts[b].CompareTo(remainingCounts[a]));
        candidates.AddRange(otherIDs);

        return candidates;
    }

    /// <summary>
    /// Tổng số chỗ trống của các ổ khác đang nhận loại này. isTaken = có ổ khác đang nhận loại này không.
    /// </summary>
    int GetFreeSlotsOfOtherBeds(int targetCatID, out bool isTaken)
    {
        int freeSlots = 0;
        isTaken = false;

        foreach (CatBed bed in GameManager.Instance.catBeds)
        {
            if (bed == null || bed == this || bed.CatID != targetCatID)
                continue;

            isTaken = true;
            freeSlots += bed.FreeSlotCount;
        }

        return freeSlots;
    }

    /// <summary>
    /// Dọn ổ về trống, chưa gán loại mèo (dùng khi load level mới, trước khi setup lại các ổ).
    /// </summary>
    public void ClearBed()
    {
        StopCompleteCoroutine();
        ResetZoom();

        catID = NO_CAT;
        reservedCount = 0;
        arrivedCount = 0;

        foreach (Image catImage in catImages)
            catImage.gameObject.SetActive(false);

        if (targetCatBackgroundImage != null)
            targetCatBackgroundImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Reset ổ sau khi đủ 3 mèo: nhận loại mới, rồi gọi các ổ đang đứng chờ chọn lại loại.
    /// </summary>
    public void ResetBed()
    {
        SetupBed();

        foreach (CatBed bed in GameManager.Instance.catBeds)
        {
            if (bed != null && bed != this && bed.IsUnlocked && bed.CatID == NO_CAT)
                bed.SetupBed();
        }
    }

    #endregion


    #region CAT METHODS

    /// <summary>
    /// Ổ có nhận được con mèo loại này không.
    /// </summary>
    public bool CanAcceptCat(int targetCatID)
    {
        return IsUnlocked && catID != NO_CAT && catID == targetCatID && !IsFull;
    }

    /// <summary>
    /// Giữ 1 chỗ trong ổ ngay khi bấm mèo. Trả về index chỗ để mèo bay tới, -1 nếu ổ đầy.
    /// </summary>
    public int ReserveSlot()
    {
        if (IsFull)
            return NO_CAT;

        return reservedCount++;
    }

    /// <summary>
    /// Vị trí chỗ trong ổ để mèo bay tới.
    /// </summary>
    public RectTransform GetSlotRect(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= catImages.Length)
            return null;

        return catImages[slotIndex].rectTransform;
    }

    /// <summary>
    /// Gọi khi mèo bay tới ổ xong: bật ảnh mèo ở chỗ trống đầu tiên.
    /// Đủ 3 con thì reset ổ.
    /// </summary>
    public void AddCatToBed()
    {
        for (int i = 0; i < catImages.Length; i++)
        {
            // Ảnh mèo đang tắt = chỗ trống
            if (catImages[i].gameObject.activeSelf)
                continue;

            catImages[i].sprite = GetCatSprite(catID);
            catImages[i].gameObject.SetActive(true);

            arrivedCount++;
            UpdateCollectedCountText();

            if (arrivedCount >= Capacity)
                completeCoroutine = StartCoroutine(CompleteBedRoutine());

            return;
        }
    }

    /// <summary>
    /// Cho mèo cùng loại đang nằm trong khay bay lên ổ.
    /// </summary>
    public void PullCatsFromTray()
    {
        CatTray catTray = GameManager.Instance.catTray;

        if (catTray != null)
            catTray.SendCatsToBed(this);
    }

    void UpdateCollectedCountText()
    {
        if (targetCatCurrentCollectedCount != null)
            targetCatCurrentCollectedCount.text = $"{arrivedCount}/{Capacity}";
    }

    Sprite GetCatSprite(int targetCatID)
    {
        Sprite[] sprites = GameManager.Instance.catSprites;

        if (sprites == null || targetCatID < 0 || targetCatID >= sprites.Length)
            return null;

        return sprites[targetCatID];
    }

    #endregion


    #region COMPLETE METHODS

    void StopCompleteCoroutine()
    {
        if (completeCoroutine == null)
            return;

        StopCoroutine(completeCoroutine);
        completeCoroutine = null;
    }

    IEnumerator CompleteBedRoutine()
    {
        // Chờ 1 chút để người chơi thấy đủ 3 con rồi mới dọn ổ
        yield return new WaitForSeconds(completeDelay);

        // Ổ hoàn thành: cộng số mèo thu thập (đủ thì thắng)
        GameManager.Instance.OnCatBedCompleted(Capacity);

        // Zoom ổ về 0
        zoomTarget.DOKill();
        yield return zoomTarget.DOScale(Vector3.zero, zoomOutDuration).SetEase(Ease.InBack).WaitForCompletion();

        // Gán null trước khi ResetBed để ClearBed bên trong không dừng chính coroutine này
        completeCoroutine = null;
        ResetBed();

        // Zoom ổ (đã nhận loại mới) về kích thước ban đầu
        zoomTarget.localScale = Vector3.zero;
        zoomTarget.DOScale(zoomTargetScale, zoomInDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Dừng zoom, trả ổ về kích thước ban đầu (khi load level mới / tắt ổ).
    /// </summary>
    void ResetZoom()
    {
        if (zoomTarget == null)
            return;

        zoomTarget.DOKill();
        zoomTarget.localScale = zoomTargetScale;
    }

    #endregion
}