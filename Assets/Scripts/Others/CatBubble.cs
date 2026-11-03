using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CatBubble : MonoBehaviour
{
    #region VARIABLES

    // Phần tử thứ i dùng cho bóng có (i + 1) con mèo
    // VD: catContainerObjects[0] = container 1 mèo, bubbleScales[4] = scale bóng 5 mèo
    [SerializeField] GameObject[] catContainerObjects;
    [SerializeField] float[] bubbleScales;

    [SerializeField] Image catBubbleImage;
    [SerializeField] CircleCollider2D bubbleCollider;

    [Header("Appear")]
    [Tooltip("Thời gian hiệu ứng bóng phóng to khi xuất hiện, = 0 thì hiện ngay")]
    [SerializeField] float appearDuration = 0.25f;

    [Header("Pop")]
    [SerializeField] float popDuration = 0.2f;

    readonly Vector2 bubbleBaseSize = new Vector2(463, 463);

    Cat[] cats;
    Cat[] allCats;      // toàn bộ mèo của mọi container, kể cả mèo đang bay (đã rời khỏi bóng)
    int remainingCat;
    Vector3 originalScale;

    #endregion


    #region PROPERTIES

    public Cat[] Cats => cats;
    public int RemainingCat => remainingCat;
    public float Radius => bubbleCollider.radius;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        originalScale = transform.localScale;
        allCats = GetComponentsInChildren<Cat>(true);
    }

    private void OnDisable()
    {
        transform.DOKill();
    }

    #endregion


    #region SETUP METHODS

    /// <summary>
    /// Setup bóng theo danh sách ID mèo, số mèo = catIDs.Length.
    /// </summary>
    public void SetUpCatBubble(int[] catIDs)
    {
        SetUpCatBubble(catIDs.Length);

        if (cats.Length != catIDs.Length)
            Debug.LogError($"[CatBubble] Container {catIDs.Length} mèo đang có {cats.Length} object Cat, kiểm tra lại prefab bóng", this);

        for (int i = 0; i < cats.Length && i < catIDs.Length; i++)
            cats[i].SetUpCat(catIDs[i]);
    }

    public void SetUpCatBubble(int numberOfCat)
    {
        numberOfCat = Mathf.Clamp(numberOfCat, 1, catContainerObjects.Length);
        remainingCat = numberOfCat;

        ResetBubble();
        gameObject.SetActive(true);

        SetUpCatContainer(numberOfCat);
        SetUpBubbleImage(numberOfCat);
        SetUpBubbleCollider();
    }

    /// <summary>
    /// Dừng hiệu ứng vỡ bóng, kéo mọi con mèo (kể cả đang bay dở) về lại bóng.
    /// </summary>
    public void ResetBubble()
    {
        transform.DOKill();
        transform.localScale = originalScale;

        if (allCats == null)
            return;

        foreach (Cat cat in allCats)
            cat.ResetCat();
    }

    void SetUpCatContainer(int numberOfCat)
    {
        int activeIndex = numberOfCat - 1;

        for (int i = 0; i < catContainerObjects.Length; i++)
            catContainerObjects[i].SetActive(i == activeIndex);

        // Lấy mèo trong container đang bật, bật lại các con đã bay đi ở lần chơi trước
        cats = catContainerObjects[activeIndex].GetComponentsInChildren<Cat>(true);
        foreach (Cat cat in cats)
            cat.gameObject.SetActive(true);
    }

    void SetUpBubbleImage(int numberOfCat)
    {
        int index = Mathf.Clamp(numberOfCat - 1, 0, bubbleScales.Length - 1);
        catBubbleImage.rectTransform.sizeDelta = bubbleBaseSize * bubbleScales[index];
    }

    void SetUpBubbleCollider()
    {
        bubbleCollider.radius = catBubbleImage.rectTransform.rect.height / 2f;
    }

    #endregion


    #region CAT METHODS

    /// <summary>
    /// Gọi từ Cat ngay khi mèo được bấm và bắt đầu bay. Hết mèo thì bóng vỡ và tắt luôn.
    /// </summary>
    public void RemoveCat()
    {
        if (remainingCat <= 0)
            return;

        remainingCat--;

        if (remainingCat == 0)
            PopBubble();
    }

    #endregion


    #region ANIMATION METHODS

    /// <summary>
    /// Hiệu ứng bóng phóng to từ 0 khi được sinh ra.
    /// </summary>
    public void PlayAppear()
    {
        transform.DOKill();

        if (appearDuration <= 0f)
        {
            transform.localScale = originalScale;
            return;
        }

        transform.localScale = Vector3.zero;
        transform.DOScale(originalScale, appearDuration).SetEase(Ease.OutBack);
    }

    void PopBubble()
    {
        transform.DOKill();
        transform.DOScale(Vector3.zero, popDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }

    #endregion
}