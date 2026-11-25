using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel khay đầy: hiện khi 5 ô khay đã đầy (mèo cuối đã bay tới).
/// Continue (xu / quảng cáo): dọn khay (hút mèo vào ô 6) + mở thêm 1 ổ, chơi tiếp.
/// Trả bằng xu: xu bay từ ô xu góc trên xuống icon xu trên nút Continue Play rồi mới đóng panel.
/// Close: hiện panel thua. Gán OnClick các nút trong Editor.
/// </summary>
public class FullTrayPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- COIN ---")]
    [Tooltip("Coin Icon ở góc trên - nơi xu bay ra")]
    [SerializeField] RectTransform coinIcon;
    [Tooltip("Amount Text - số xu hiện có")]
    [SerializeField] Text coinAmountText;

    [Header("--- PRICE ---")]
    [SerializeField] int continuePrice = 1000;
    [Tooltip("Text giá trên Button - Continue Play")]
    [SerializeField] Text priceText;
    [Tooltip("Icon xu (Coin) trên Button - Continue Play - nơi xu bay tới")]
    [SerializeField] RectTransform buyCoinIcon;

    [Header("--- CATBED REWARD ---")]
    [Tooltip("Catbed Image (+1) - ẩn đi khi không còn ổ khóa nào để mở")]
    [SerializeField] GameObject catbedImage;
    [Tooltip("Unlock Catbed Text")]
    [SerializeField] Text unlockCatbedText;
    [SerializeField] string unlockCatbedMessage = "Clean tray and claim a catbed";
    [Tooltip("Text khi đã mở hết ổ, chỉ dọn khay")]
    [SerializeField] string cleanTrayOnlyMessage = "Clean tray and continue";

    [Header("--- BUTTONS ---")]
    [Tooltip("Button - Continue Play (xu)")]
    [SerializeField] Button continuePlayButton;
    [Tooltip("Button - Continue Play Ad")]
    [SerializeField] Button continuePlayAdButton;
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

    bool isWaitingChoice;       // panel đang chờ người chơi chọn (tránh bấm 2 lần)
    bool isPaying;              // đã trừ xu, đang chạy hiệu ứng xu bay
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

        // Panel bị tắt giữa lúc xu đang bay: xu đã trừ nên vẫn phải cho chơi tiếp
        if (isPaying)
        {
            isPaying = false;
            GameManager.Instance.ContinueAfterFullTray();
        }

        StopVfx();
        KillCoinEffect();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// GameManager gọi khi khay đầy: cập nhật giá, phần thưởng ổ rồi hiện panel.
    /// </summary>
    public void ShowFullTray()
    {
        isWaitingChoice = true;
        isPaying = false;

        displayedCoin = DataManager.Instance.LoadCoinData();
        UpdateCoinText(displayedCoin);

        if (priceText != null)
            priceText.text = continuePrice.ToString();

        // Còn ổ khóa thì hiện +1 ổ, hết rồi thì chỉ dọn khay
        bool hasLockedBed = GameManager.Instance.GetFirstLockedBed() != null;

        if (catbedImage != null)
            catbedImage.SetActive(hasLockedBed);

        if (unlockCatbedText != null)
            unlockCatbedText.text = hasLockedBed ? unlockCatbedMessage : cleanTrayOnlyMessage;

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
    /// Gán vào OnClick của Button - Continue Play: đủ xu thì trừ xu, chơi tiếp.
    /// Không đủ xu thì mở Panel - Buy Coin, đóng Buy Coin xong quay lại panel này.
    /// </summary>
    public void OnContinuePlayButtonClick()
    {
        if (!isWaitingChoice)
            return;

        int coin = DataManager.Instance.LoadCoinData();
        if (coin < continuePrice)
        {
            GameManager.Instance.OpenBuyCoinPanel(ShowFullTray);
            return;
        }

        // Trừ và lưu xu ngay, hiệu ứng chỉ để hiển thị
        int newCoin = coin - continuePrice;
        DataManager.Instance.SaveCoinData(newCoin);

        isWaitingChoice = false;
        isPaying = true;
        SetButtonsInteractable(false);

        PlayCoinEffect(displayedCoin, newCoin, () =>
        {
            isPaying = false;
            ContinuePlay();
        });
    }

    /// <summary>
    /// Gán vào OnClick của Button - Continue Play Ad: xem quảng cáo xong chơi tiếp.
    /// </summary>
    public void OnContinuePlayAdButtonClick()
    {
        if (!isWaitingChoice)
            return;

        // TODO: hiện rewarded ad, xem xong mới gọi ContinuePlay()
        ContinuePlay();
    }

    /// <summary>
    /// Gán vào OnClick của Button - Close: bỏ cuộc -> hiện panel thua.
    /// </summary>
    public void OnCloseButtonClick()
    {
        if (!isWaitingChoice)
            return;

        isWaitingChoice = false;

        // Panel thua hiện lên sẽ tự tắt panel này
        GameManager.Instance.ShowLosePanel();
    }

    void ContinuePlay()
    {
        isWaitingChoice = false;

        Hide();
        GameManager.Instance.ContinueAfterFullTray();
    }

    #endregion


    #region COIN EFFECT METHODS

    /// <summary>
    /// Xu lần lượt hiện ở ô xu góc trên rồi bay xuống icon xu trên nút Continue Play, số xu giảm dần.
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


    #region TEST METHODS

    /// <summary>
    /// Test: chỉ hiện panel để xem giao diện (bấm nút vẫn chạy logic thật).
    /// </summary>
    [ContextMenu("Test Show Panel")]
    public void TestShowPanel()
    {
        if (Application.isPlaying)
            ShowFullTray();
    }

    #endregion


    #region HELPER METHODS

    void SetButtonsInteractable(bool interactable)
    {
        if (continuePlayButton != null)
            continuePlayButton.interactable = interactable;

        if (continuePlayAdButton != null)
            continuePlayAdButton.interactable = interactable;

        if (closeButton != null)
            closeButton.interactable = interactable;
    }

    // Nhận xu ở Panel - Buy Coin xong thì cập nhật lại số xu (không đụng lúc xu đang bay)
    void OnCoinClaimed(int newCoin)
    {
        if (isPaying)
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
}