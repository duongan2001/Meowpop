using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel mua thêm ổ mèo: mở khi bấm vào ổ đang khóa (dấu +).
/// Mua bằng xu: xu bay từ ô xu góc trên xuống icon xu trên nút Buy, xong thì đóng panel và ổ chạy hiệu ứng mở.
/// Claim (quảng cáo): đóng panel và mở ổ luôn. Gán OnClick các nút trong Editor.
/// </summary>
public class BuyCatbedPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- COIN ---")]
    [Tooltip("Coin Icon ở góc trên - nơi xu bay ra")]
    [SerializeField] RectTransform coinIcon;
    [Tooltip("Amount Text - số xu hiện có")]
    [SerializeField] Text coinAmountText;

    [Header("--- PRICE ---")]
    [SerializeField] int catbedPrice = 1000;
    [Tooltip("Text giá trên Button - Buy Catbed")]
    [SerializeField] Text priceText;
    [Tooltip("Icon xu trên Button - Buy Catbed - nơi xu bay tới")]
    [SerializeField] RectTransform buyCoinIcon;

    [Header("--- BUTTONS ---")]
    [Tooltip("Button - Buy Catbed")]
    [SerializeField] Button buyCatbedButton;
    [Tooltip("Button - Claim Catbed Ad")]
    [SerializeField] Button claimCatbedAdButton;
    [Tooltip("Button - Close")]
    [SerializeField] Button closeButton;

    [Header("--- VFX ---")]
    [SerializeField] ParticleSystem sparkleVfx;

    [Header("--- COIN EFFECT ---")]
    [Tooltip("Ảnh xu dùng để bay, để trống thì chép hình Coin Icon")]
    [SerializeField] Image coinEffectPrefab;
    [Tooltip("Nơi chứa xu bay (nằm trên cùng panel), để trống thì dùng chính panel")]
    [SerializeField] RectTransform coinEffectContainer;
    [SerializeField] int coinEffectCount = 8;
    [SerializeField] float coinAppearDuration = 0.15f;
    [SerializeField] float coinFlyDuration = 0.45f;
    [SerializeField] float coinFlyStagger = 0.06f;
    [SerializeField] float coinFlyEndScale = 0.8f;

    [Tooltip("Chờ panel mờ đi rồi mới chạy hiệu ứng mở ổ (giây)")]
    [SerializeField] float unlockDelay = 0.25f;

    CatBed targetBed;
    bool isBuying;              // đã trừ xu, đang chạy hiệu ứng xu bay
    int displayedCoin;

    Coroutine coinEffectCoroutine;
    Vector3 buyCoinIconScale = Vector3.one;
    readonly List<Image> coinPool = new List<Image>();

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();

        if (buyCoinIcon != null)
            buyCoinIconScale = buyCoinIcon.localScale;
    }

    void OnEnable()
    {
        BuyCoinPanel.OnCoinClaimed += OnCoinClaimed;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        BuyCoinPanel.OnCoinClaimed -= OnCoinClaimed;

        // Panel bị tắt giữa lúc xu đang bay (VD load level): xu đã trừ nên vẫn phải mở ổ
        if (isBuying)
            FinishPurchase(false);

        StopVfx();
        KillCoinEffect();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// GameManager gọi khi bấm vào ổ đang khóa: ghi nhớ ổ cần mở, load xu rồi hiện panel.
    /// </summary>
    public void ShowBuyCatbed(CatBed bed)
    {
        if (bed == null || bed.IsUnlocked)
            return;

        targetBed = bed;
        isBuying = false;

        displayedCoin = DataManager.Instance.LoadCoinData();
        UpdateCoinText(displayedCoin);

        if (priceText != null)
            priceText.text = catbedPrice.ToString();

        SetButtonsInteractable(true);
        ShowDelay();
    }

    public override void Show()
    {
        base.Show();
        PlayVfx();
    }

    public override void Hide()
    {
        SetButtonsInteractable(false);
        StopVfx();
        base.Hide();
    }

    #endregion


    #region BUTTON METHODS

    /// <summary>
    /// Gán vào OnClick của Button - Buy Catbed: đủ xu thì trừ xu, xu bay xuống nút Buy, xong mở ổ.
    /// </summary>
    public void OnBuyCatbedButtonClick()
    {
        if (targetBed == null || isBuying)
            return;

        int coin = DataManager.Instance.LoadCoinData();
        if (coin < catbedPrice)
        {
            GameManager.Instance.OpenBuyCoinPanel();
            return;
        }

        // Trừ và lưu xu ngay, hiệu ứng chỉ để hiển thị
        int newCoin = coin - catbedPrice;
        DataManager.Instance.SaveCoinData(newCoin);

        isBuying = true;
        SetButtonsInteractable(false);

        PlayCoinEffect(displayedCoin, newCoin, () => FinishPurchase(true));
    }

    /// <summary>
    /// Gán vào OnClick của Button - Claim Catbed Ad: xem quảng cáo xong mở ổ.
    /// </summary>
    public void OnClaimCatbedAdButtonClick()
    {
        if (targetBed == null || isBuying)
            return;

        // TODO: hiện rewarded ad, xem xong mới mở ổ
        FinishPurchase(true);
    }

    /// <summary>
    /// Gán vào OnClick của Button - Close. Đang mua (xu đang bay) thì không cho đóng.
    /// </summary>
    public void OnCloseButtonClick()
    {
        if (isBuying)
            return;

        targetBed = null;
        Hide();
    }

    /// <summary>
    /// Đóng panel, ổ chạy hiệu ứng mở (thu nhỏ → mở khóa, nhận loại mèo → phóng to).
    /// </summary>
    void FinishPurchase(bool hidePanel)
    {
        CatBed bed = targetBed;
        targetBed = null;
        isBuying = false;

        if (hidePanel)
            Hide();

        if (bed != null)
            bed.UnlockBedWithAnimation(hidePanel ? unlockDelay : 0f);
    }

    #endregion


    #region COIN EFFECT METHODS

    /// <summary>
    /// Xu lần lượt hiện ở ô xu góc trên rồi bay xuống icon xu trên nút Buy, số xu giảm dần.
    /// </summary>
    void PlayCoinEffect(int fromCoin, int toCoin, Action onComplete)
    {
        KillCoinEffect();
        coinEffectCoroutine = StartCoroutine(CoinEffectRoutine(fromCoin, toCoin, onComplete));
    }

    IEnumerator CoinEffectRoutine(int fromCoin, int toCoin, Action onComplete)
    {
        if (coinIcon == null || buyCoinIcon == null)
        {
            displayedCoin = toCoin;
            UpdateCoinText(displayedCoin);
            onComplete?.Invoke();
            yield break;
        }

        RectTransform container = coinEffectContainer != null ? coinEffectContainer : (RectTransform)transform;

        Vector3 startPos = container.InverseTransformPoint(GetWorldCenter(coinIcon));
        Vector3 targetPos = container.InverseTransformPoint(GetWorldCenter(buyCoinIcon));

        int coinCount = Mathf.Max(1, coinEffectCount);
        List<Image> coins = new List<Image>(coinCount);

        for (int i = 0; i < coinCount; i++)
        {
            Image coin = GetCoinFromPool(container);
            coin.rectTransform.DOKill();
            coin.rectTransform.localPosition = startPos;
            coin.rectTransform.localScale = Vector3.zero;
            coins.Add(coin);
        }

        // Chờ 1 frame cho qua frame khựng (lưu PlayerPrefs, tạo xu), tránh tween bị "nuốt"
        yield return null;

        int arrivedCount = 0;

        for (int i = 0; i < coinCount; i++)
        {
            Image coin = coins[i];
            RectTransform coinRect = coin.rectTransform;
            int leftIndex = i + 1;

            Sequence coinFly = DOTween.Sequence().SetUpdate(true).SetTarget(coinRect);
            coinFly.AppendInterval(i * coinFlyStagger);

            // Xu rời ô xu -> số xu giảm dần
            coinFly.AppendCallback(() =>
            {
                displayedCoin = fromCoin - Mathf.RoundToInt((fromCoin - toCoin) * (float)leftIndex / coinCount);
                UpdateCoinText(displayedCoin);
            });

            coinFly.Append(coinRect.DOScale(Vector3.one, coinAppearDuration).SetEase(Ease.OutBack));
            coinFly.Append(coinRect.DOLocalMove(targetPos, coinFlyDuration).SetEase(Ease.InQuad));
            coinFly.Join(coinRect.DOScale(Vector3.one * coinFlyEndScale, coinFlyDuration).SetEase(Ease.InQuad));
            coinFly.OnComplete(() =>
            {
                coin.gameObject.SetActive(false);
                PunchBuyCoinIcon();

                arrivedCount++;
                if (arrivedCount < coinCount)
                    return;

                coinEffectCoroutine = null;
                displayedCoin = toCoin;
                UpdateCoinText(displayedCoin);
                onComplete?.Invoke();
            });
        }
    }

    void PunchBuyCoinIcon()
    {
        buyCoinIcon.DOKill();
        buyCoinIcon.localScale = buyCoinIconScale;
        buyCoinIcon.DOPunchScale(buyCoinIconScale * 0.2f, 0.15f, 1).SetUpdate(true);
    }

    Image GetCoinFromPool(RectTransform container)
    {
        foreach (Image pooledCoin in coinPool)
        {
            if (!pooledCoin.gameObject.activeSelf)
            {
                pooledCoin.gameObject.SetActive(true);
                pooledCoin.rectTransform.SetAsLastSibling();
                return pooledCoin;
            }
        }

        Image template = coinEffectPrefab != null ? coinEffectPrefab : coinIcon.GetComponent<Image>();
        Image coin = CreateCoinImage(template, container);

        coin.gameObject.SetActive(true);
        coinPool.Add(coin);
        return coin;
    }

    /// <summary>
    /// Tạo xu "sạch" (chỉ RectTransform + Image), chép hình từ template để không mang theo Animator / script khác.
    /// </summary>
    Image CreateCoinImage(Image template, RectTransform container)
    {
        GameObject coinObject = new GameObject("Coin Effect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coinObject.layer = container.gameObject.layer;

        RectTransform rect = (RectTransform)coinObject.transform;
        rect.SetParent(container, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = template.rectTransform.rect.size;
        rect.localScale = Vector3.zero;
        rect.SetAsLastSibling();

        Image coin = coinObject.GetComponent<Image>();
        coin.sprite = template.sprite;
        coin.color = template.color;
        coin.material = template.material;
        coin.preserveAspect = template.preserveAspect;
        coin.raycastTarget = false;

        return coin;
    }

    void KillCoinEffect()
    {
        if (coinEffectCoroutine != null)
        {
            StopCoroutine(coinEffectCoroutine);
            coinEffectCoroutine = null;
        }

        foreach (Image coin in coinPool)
        {
            coin.rectTransform.DOKill();
            coin.gameObject.SetActive(false);
        }

        if (buyCoinIcon != null)
        {
            buyCoinIcon.DOKill();
            buyCoinIcon.localScale = buyCoinIconScale;
        }
    }

    #endregion


    #region VFX METHODS

    void PlayVfx()
    {
        if (sparkleVfx == null)
            return;

        sparkleVfx.gameObject.SetActive(true);
        sparkleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparkleVfx.Play(true);
    }

    void StopVfx()
    {
        if (sparkleVfx == null)
            return;

        sparkleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion


    #region HELPER METHODS

    // Khóa nút khi đang mua / đóng panel, tránh bấm 2 lần
    void SetButtonsInteractable(bool interactable)
    {
        if (buyCatbedButton != null)
            buyCatbedButton.interactable = interactable;

        if (claimCatbedAdButton != null)
            claimCatbedAdButton.interactable = interactable;

        if (closeButton != null)
            closeButton.interactable = interactable;
    }

    // Nhận xu ở Panel - Buy Coin xong thì cập nhật lại số xu (không đụng lúc xu đang bay)
    void OnCoinClaimed(int newCoin)
    {
        if (isBuying)
            return;

        displayedCoin = newCoin;
        UpdateCoinText(displayedCoin);
    }

    void UpdateCoinText(int coin)
    {
        if (coinAmountText != null)
            coinAmountText.text = coin.ToString();
    }

    Vector3 GetWorldCenter(RectTransform rect)
    {
        return rect.TransformPoint(rect.rect.center);
    }

    #endregion


    #region TEST METHODS

    /// <summary>
    /// Test: mở panel với số xu hiện tại, không gắn ổ nào (bấm Buy / Claim không làm gì, Close vẫn đóng được).
    /// </summary>
    [ContextMenu("Test Show Panel")]
    public void TestShowPanel()
    {
        if (!Application.isPlaying)
            return;

        targetBed = null;
        isBuying = false;
        KillCoinEffect();

        displayedCoin = DataManager.Instance.LoadCoinData();
        UpdateCoinText(displayedCoin);

        if (priceText != null)
            priceText.text = catbedPrice.ToString();

        SetButtonsInteractable(true);
        Show();
    }

    /// <summary>
    /// Test: chạy hiệu ứng xu bay + đóng panel như khi mua thật,
    /// nhưng KHÔNG trừ / lưu xu và KHÔNG mở ổ nào.
    /// </summary>
    [ContextMenu("Test Buy Effect")]
    public void TestBuyEffect()
    {
        if (!Application.isPlaying || isBuying)
            return;

        targetBed = null;

        if (!gameObject.activeSelf)
            TestShowPanel();

        int realCoin = DataManager.Instance.LoadCoinData();
        displayedCoin = realCoin;
        UpdateCoinText(displayedCoin);

        SetButtonsInteractable(false);

        // Chỉ giảm số hiển thị, xong thì trả lại số xu thật và đóng panel
        PlayCoinEffect(realCoin, Mathf.Max(0, realCoin - catbedPrice), () =>
        {
            displayedCoin = realCoin;
            UpdateCoinText(displayedCoin);
            Hide();
        });
    }

    #endregion
}