using System;
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
    [Tooltip("Prefab VFX chạy khi bóng vỡ (để trống thì không chạy)")]
    [SerializeField] ParticleSystem popVfxPrefab;

    [Header("Frozen")]
    [Tooltip("Frozen Bubble Image - lớp băng phủ bóng, bóng thường thì tắt")]
    [SerializeField] Image frozenBubbleImage;
    [Tooltip("Frozen Break Count Text - số lần còn phải bấm mèo bên cạnh để phá băng")]
    [SerializeField] Text frozenBreakCountText;
    [Tooltip("Số lần bấm mèo ở bóng bên cạnh để phá băng")]
    [Min(1)][SerializeField] int frozenBreakCount = 3;
    [Tooltip("Prefab VFX băng vỡ (để trống thì không chạy)")]
    [SerializeField] ParticleSystem iceBreakVfxPrefab;
    [SerializeField] float frozenFadeDuration = 0.3f;
    [Tooltip("Lớp băng rung khi bấm vào mèo trong bóng đang đóng băng, = 0 thì tắt")]
    [SerializeField] float frozenShakeStrength = 12f;

    readonly Vector2 bubbleBaseSize = new Vector2(463, 463);

    Cat[] cats;
    Cat[] allCats;      // toàn bộ mèo của mọi container, kể cả mèo đang bay (đã rời khỏi bóng)
    int remainingCat;
    Vector3 originalScale;
    ParticleSystem popVfx;      // bản VFX của riêng bóng này, tạo lần đầu vỡ rồi dùng lại
    ParticleSystem iceBreakVfx;

    bool isFrozen;
    int frozenHitLeft;              // số lần còn phải bấm để phá băng
    CanvasGroup frozenCanvasGroup;  // fade cả lớp băng lẫn text số
    Vector2 frozenImagePosition;
    Vector3 frozenTextScale = Vector3.one;

    #endregion


    #region PROPERTIES

    public Cat[] Cats => cats;
    public Cat[] AllCats => allCats;
    public int RemainingCat => remainingCat;
    public float Radius => bubbleCollider.radius;
    public bool IsFrozen => isFrozen;

    /// <summary>
    /// Gọi ngay khi bóng hết mèo và bắt đầu vỡ. GameManager nghe để bật thêm bóng mới.
    /// </summary>
    public event Action<CatBubble> OnPopped;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        originalScale = transform.localScale;
        allCats = GetComponentsInChildren<Cat>(true);

        if (frozenBubbleImage != null)
        {
            frozenImagePosition = frozenBubbleImage.rectTransform.anchoredPosition;

            frozenCanvasGroup = frozenBubbleImage.GetComponent<CanvasGroup>();
            if (frozenCanvasGroup == null)
                frozenCanvasGroup = frozenBubbleImage.gameObject.AddComponent<CanvasGroup>();
        }

        if (frozenBreakCountText != null)
            frozenTextScale = frozenBreakCountText.transform.localScale;
    }

    private void LateUpdate()
    {
        // Bóng có thể bị xoay (lăn / va chạm), giữ số trên lớp băng luôn thẳng đứng
        if (!isFrozen || frozenBreakCountText == null)
            return;

        Transform text = frozenBreakCountText.transform;
        if (text.rotation != Quaternion.identity)
            text.rotation = Quaternion.identity;
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

        // Mặc định là bóng thường, GameManager gọi SetFrozen(true) sau nếu là bóng đóng băng
        SetFrozen(false);

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
        {
            PopBubble();
            OnPopped?.Invoke(this);
        }
    }

    /// <summary>
    /// Xoay mọi mèo còn trong bóng về thẳng đứng hoặc về góc nghiêng ban đầu.
    /// </summary>
    public void SetCatsUpright(bool upright, bool animate)
    {
        if (cats == null)
            return;

        foreach (Cat cat in cats)
        {
            if (cat.gameObject.activeSelf)
                cat.SetUpright(upright, animate);
        }
    }

    #endregion


    #region FROZEN METHODS

    /// <summary>
    /// Bật / tắt trạng thái đóng băng ngay (không hiệu ứng). Bóng đóng băng: hiện lớp băng + số lần cần bấm, không bấm được mèo.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        frozenHitLeft = frozen ? frozenBreakCount : 0;

        if (frozenBubbleImage == null)
            return;

        ResetFrozenImage();
        UpdateFrozenCountText();
        frozenBubbleImage.gameObject.SetActive(frozen);
    }

    /// <summary>
    /// GameManager gọi khi bấm mèo ở bóng bên cạnh: trừ 1 lần, hết thì phá băng.
    /// </summary>
    public void HitFrozen()
    {
        if (!isFrozen)
            return;

        frozenHitLeft--;

        if (frozenHitLeft <= 0)
        {
            Unfreeze();
            return;
        }

        UpdateFrozenCountText();
        PlayFrozenShake();

        // Số nảy lên mỗi lần bị trừ
        if (frozenBreakCountText != null)
        {
            Transform text = frozenBreakCountText.transform;
            text.DOKill();
            text.localScale = frozenTextScale;
            text.DOPunchScale(frozenTextScale * 0.3f, 0.25f, 1);
        }
    }

    /// <summary>
    /// Phá băng ngay: lớp băng mờ dần rồi tắt, chạy VFX băng vỡ, bóng trở về bình thường.
    /// </summary>
    public void Unfreeze()
    {
        if (!isFrozen)
            return;

        isFrozen = false;
        frozenHitLeft = 0;
        PlayVfx(iceBreakVfxPrefab, ref iceBreakVfx, "Ice Break VFX");
        SoundManager.Play(SoundGroup.Bubble, "IceBreak");

        if (frozenBubbleImage == null)
            return;

        ResetFrozenImage();

        if (frozenFadeDuration <= 0f || !frozenBubbleImage.gameObject.activeInHierarchy)
        {
            frozenBubbleImage.gameObject.SetActive(false);
            return;
        }

        frozenCanvasGroup.DOFade(0f, frozenFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetTarget(frozenBubbleImage)
            .OnComplete(() =>
            {
                frozenBubbleImage.gameObject.SetActive(false);
                ResetFrozenImage();
            });
    }

    /// <summary>
    /// Lớp băng rung nhẹ (bấm vào bóng đang đóng băng / bị trừ 1 lần).
    /// </summary>
    public void PlayFrozenShake()
    {
        if (!isFrozen || frozenBubbleImage == null || frozenShakeStrength <= 0f)
            return;

        RectTransform rect = frozenBubbleImage.rectTransform;
        rect.DOKill();
        rect.anchoredPosition = frozenImagePosition;
        rect.DOShakeAnchorPos(0.25f, frozenShakeStrength, 20)
            .OnComplete(() => rect.anchoredPosition = frozenImagePosition);
    }

    void UpdateFrozenCountText()
    {
        if (frozenBreakCountText != null)
            frozenBreakCountText.text = frozenHitLeft.ToString();
    }

    // Dừng tween, trả lớp băng về alpha / vị trí / scale gốc
    void ResetFrozenImage()
    {
        DOTween.Kill(frozenBubbleImage);
        frozenBubbleImage.rectTransform.DOKill();
        frozenBubbleImage.rectTransform.anchoredPosition = frozenImagePosition;

        if (frozenCanvasGroup != null)
            frozenCanvasGroup.alpha = 1f;

        if (frozenBreakCountText != null)
        {
            frozenBreakCountText.transform.DOKill();
            frozenBreakCountText.transform.localScale = frozenTextScale;
        }
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
        PlayVfx(popVfxPrefab, ref popVfx, "Pop VFX");
        SoundManager.Play(SoundGroup.Bubble, "BubblePop");

        transform.DOKill();
        transform.DOScale(Vector3.zero, popDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>
    /// Chạy VFX tại vị trí bóng (vỡ bóng / băng vỡ). VFX nằm ngoài bóng (cùng cha với bóng)
    /// để không bị thu nhỏ theo bóng và không bị tắt khi bóng SetActive(false). Mỗi bóng giữ 1 bản, dùng lại.
    /// </summary>
    void PlayVfx(ParticleSystem prefab, ref ParticleSystem instance, string vfxName)
    {
        if (prefab == null)
            return;

        if (instance == null)
        {
            instance = Instantiate(prefab, transform.parent);
            instance.name = $"{name} - {vfxName}";
        }

        Transform vfxTransform = instance.transform;
        vfxTransform.position = transform.position;
        vfxTransform.SetAsLastSibling();    // vẽ đè lên các bóng khác

        instance.gameObject.SetActive(true);
        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.Play(true);
    }

    #endregion
}