using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel nhận thêm xu bằng rewarded ad (giới hạn số lần mỗi ngày).
/// Nhận xu: xu bung ra quanh Coin Image rồi lần lượt bay lên ô xu góc trên, số xu tăng dần (giống Victory).
/// Gán OnClick các nút trong Editor.
/// </summary>
public class BuyCoinPanel : PanelBaseUIController
{
    #region VARIABLES

    const string DAILY_DATE_KEY = "buyCoinAdDate";
    const string DAILY_COUNT_KEY = "buyCoinAdCount";

    [Header("--- COIN ---")]
    [Tooltip("Coin Icon ở góc trên - đích bay tới của xu")]
    [SerializeField] RectTransform coinIcon;
    [Tooltip("Amount Text - số xu hiện có")]
    [SerializeField] Text coinAmountText;

    [Header("--- REWARD ---")]
    [SerializeField] int rewardCoin = 300;
    [Tooltip("Text +300 cạnh Coin Image")]
    [SerializeField] Text rewardAmountText;
    [Tooltip("Coin Image - nơi xu bung ra")]
    [SerializeField] RectTransform coinImage;

    [Header("--- DAILY LIMIT ---")]
    [SerializeField] int dailyLimit = 5;
    [Tooltip("Daily Limit Count Text - hiện dạng 0/5")]
    [SerializeField] Text dailyLimitCountText;

    [Header("--- BUTTONS ---")]
    [Tooltip("Button - Claim Coin Ad")]
    [SerializeField] Button claimCoinAdButton;
    [Tooltip("Button - Close")]
    [SerializeField] Button closeButton;
    [Tooltip("Nhận xu xong thì tự đóng panel")]
    [SerializeField] bool hideAfterClaim = false;

    [Header("--- VFX ---")]
    [SerializeField] ParticleSystem sparkleVfx;

    [Header("--- COIN EFFECT ---")]
    [Tooltip("Ảnh xu dùng để bay, để trống thì chép hình Coin Icon")]
    [SerializeField] Image coinEffectPrefab;
    [Tooltip("Nơi chứa xu bay (nằm trên cùng panel), để trống thì dùng chính panel")]
    [SerializeField] RectTransform coinEffectContainer;
    [SerializeField] int coinEffectCount = 10;
    [SerializeField] float coinBurstRadius = 150f;
    [SerializeField] float coinBurstDuration = 0.35f;
    [Tooltip("Thời gian xu nằm yên sau khi nổ xong rồi mới bắt đầu bay lên")]
    [SerializeField] float coinHoldAfterBurst = 0.25f;
    [SerializeField] float coinFlyDuration = 0.6f;
    [SerializeField] float coinFlyStagger = 0.05f;
    [SerializeField] float coinFlyEndScale = 0.6f;

    /// <summary>
    /// Gọi khi nhận xu xong (đã lưu), truyền số xu mới. Các panel / HUD khác đăng ký để cập nhật text.
    /// </summary>
    public static event Action<int> OnCoinClaimed;

    bool isClaiming;            // đang chạy hiệu ứng xu bay
    Action onClosed;            // gọi khi bấm Close (VD quay lại panel trước)
    int displayedCoin;

    Coroutine coinEffectCoroutine;
    Vector3 coinIconScale = Vector3.one;
    readonly List<Image> coinPool = new List<Image>();

    #endregion


    #region PROPERTIES

    public int TodayClaimCount
    {
        get
        {
            // Sang ngày mới thì số lần xem reset về 0
            if (PlayerPrefs.GetString(DAILY_DATE_KEY, "") != GetTodayString())
                return 0;

            return PlayerPrefs.GetInt(DAILY_COUNT_KEY, 0);
        }
    }

    public bool CanClaim => TodayClaimCount < dailyLimit;

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();

        if (coinIcon != null)
            coinIconScale = coinIcon.localScale;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        isClaiming = false;
        StopVfx();
        KillCoinEffect();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// Mở panel: load số xu, số lần nhận hôm nay rồi hiện panel.
    /// </summary>
    public void ShowBuyCoin(Action onClosed = null)
    {
        this.onClosed = onClosed;
        isClaiming = false;
        RefreshUI();
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
    /// Gán vào OnClick của Button - Claim Coin Ad: xem quảng cáo xong nhận xu.
    /// </summary>
    public void OnClaimCoinAdButtonClick()
    {
        if (isClaiming || !CanClaim)
            return;

        // TODO: hiện rewarded ad, xem xong mới gọi ClaimReward()
        ClaimReward();
    }

    /// <summary>
    /// Gán vào OnClick của Button - Close. Đang nhận xu (xu đang bay) thì không cho đóng.
    /// </summary>
    public void OnCloseButtonClick()
    {
        if (isClaiming)
            return;

        Hide();
        InvokeOnClosed();
    }

    void InvokeOnClosed()
    {
        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    /// <summary>
    /// Cộng + lưu xu và số lần nhận ngay, sau đó chạy hiệu ứng xu bay.
    /// Sau này gắn ad thì gọi hàm này trong callback xem xong quảng cáo.
    /// </summary>
    public void ClaimReward()
    {
        if (isClaiming || !CanClaim)
            return;

        SaveTodayClaimCount(TodayClaimCount + 1);

        int fromCoin = displayedCoin;
        int newCoin = DataManager.Instance.LoadCoinData() + rewardCoin;
        DataManager.Instance.SaveCoinData(newCoin);

        isClaiming = true;
        SetButtonsInteractable(false);
        UpdateDailyLimitText();

        PlayCoinEffect(fromCoin, newCoin, () =>
        {
            isClaiming = false;
            OnCoinClaimed?.Invoke(newCoin);

            if (hideAfterClaim)
            {
                Hide();
                InvokeOnClosed();
                return;
            }

            RefreshUI();
        });
    }

    #endregion


    #region DAILY LIMIT METHODS

    void SaveTodayClaimCount(int count)
    {
        PlayerPrefs.SetString(DAILY_DATE_KEY, GetTodayString());
        PlayerPrefs.SetInt(DAILY_COUNT_KEY, count);
        PlayerPrefs.Save();
    }

    string GetTodayString()
    {
        return DateTime.Now.ToString("yyyyMMdd");
    }

    void UpdateDailyLimitText()
    {
        if (dailyLimitCountText != null)
            dailyLimitCountText.text = $"{Mathf.Min(TodayClaimCount, dailyLimit)}/{dailyLimit}";
    }

    #endregion


    #region COIN EFFECT METHODS

    /// <summary>
    /// Xu bung ra quanh Coin Image rồi lần lượt bay lên Coin Icon, số xu tăng dần theo từng đồng.
    /// </summary>
    void PlayCoinEffect(int fromCoin, int toCoin, Action onComplete)
    {
        KillCoinEffect();
        coinEffectCoroutine = StartCoroutine(CoinEffectRoutine(fromCoin, toCoin, onComplete));
    }

    IEnumerator CoinEffectRoutine(int fromCoin, int toCoin, Action onComplete)
    {
        if (coinIcon == null || coinImage == null)
        {
            displayedCoin = toCoin;
            UpdateCoinText(displayedCoin);
            onComplete?.Invoke();
            yield break;
        }

        RectTransform container = coinEffectContainer != null ? coinEffectContainer : (RectTransform)transform;

        Vector3 startPos = container.InverseTransformPoint(GetWorldCenter(coinImage));
        Vector3 targetPos = container.InverseTransformPoint(GetWorldCenter(coinIcon));

        int coinCount = Mathf.Max(1, coinEffectCount);
        List<Image> coins = new List<Image>(coinCount);

        // Đặt sẵn xu ở vị trí bung, scale 0
        for (int i = 0; i < coinCount; i++)
        {
            Image coin = GetCoinFromPool(container);
            coin.rectTransform.DOKill();
            coin.rectTransform.localPosition = startPos;
            coin.rectTransform.localScale = Vector3.zero;
            coins.Add(coin);
        }

        // Chờ 1 frame cho qua frame khựng (lưu PlayerPrefs, tạo xu), tránh tween bung ra bị "nuốt"
        yield return null;

        int arrivedCount = 0;

        for (int i = 0; i < coinCount; i++)
        {
            Image coin = coins[i];
            RectTransform coinRect = coin.rectTransform;
            Vector3 burstPos = startPos + (Vector3)(UnityEngine.Random.insideUnitCircle * coinBurstRadius);

            Sequence coinFly = DOTween.Sequence().SetUpdate(true).SetTarget(coinRect);

            // Bung ra
            coinFly.Append(coinRect.DOScale(Vector3.one, coinBurstDuration).SetEase(Ease.OutBack));
            coinFly.Join(coinRect.DOLocalMove(burstPos, coinBurstDuration).SetEase(Ease.OutQuad));
            // Nằm yên rồi lần lượt bay lên
            coinFly.AppendInterval(coinHoldAfterBurst + i * coinFlyStagger);
            coinFly.Append(coinRect.DOLocalMove(targetPos, coinFlyDuration).SetEase(Ease.InQuad));
            coinFly.Join(coinRect.DOScale(Vector3.one * coinFlyEndScale, coinFlyDuration).SetEase(Ease.InQuad));
            coinFly.OnComplete(() =>
            {
                coin.gameObject.SetActive(false);

                arrivedCount++;
                OnCoinArrived(fromCoin, toCoin, arrivedCount, coinCount);

                if (arrivedCount < coinCount)
                    return;

                coinEffectCoroutine = null;
                displayedCoin = toCoin;
                UpdateCoinText(displayedCoin);
                onComplete?.Invoke();
            });
        }
    }

    void OnCoinArrived(int fromCoin, int toCoin, int arrivedIndex, int coinCount)
    {
        displayedCoin = fromCoin + Mathf.RoundToInt((toCoin - fromCoin) * (float)arrivedIndex / coinCount);
        UpdateCoinText(displayedCoin);

        // Coin Icon nảy nhẹ mỗi lần có xu bay tới
        coinIcon.DOKill();
        coinIcon.localScale = coinIconScale;
        coinIcon.DOPunchScale(coinIconScale * 0.2f, 0.15f, 1).SetUpdate(true);
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

        if (coinIcon != null)
        {
            coinIcon.DOKill();
            coinIcon.localScale = coinIconScale;
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
    /// Test: chạy hiệu ứng xu bay như khi nhận thật nhưng KHÔNG cộng / lưu xu và KHÔNG tính lượt.
    /// </summary>
    [ContextMenu("Test Coin Effect")]
    public void TestCoinEffect()
    {
        if (!Application.isPlaying || isClaiming)
            return;

        if (!gameObject.activeSelf)
            ShowBuyCoin();

        int realCoin = DataManager.Instance.LoadCoinData();
        displayedCoin = realCoin;
        UpdateCoinText(displayedCoin);

        isClaiming = true;
        SetButtonsInteractable(false);

        PlayCoinEffect(realCoin, realCoin + rewardCoin, () =>
        {
            isClaiming = false;
            RefreshUI();    // trả lại số xu thật
        });
    }

    /// <summary>
    /// Test: reset số lần nhận hôm nay về 0.
    /// </summary>
    [ContextMenu("Reset Daily Limit")]
    public void ResetDailyLimit()
    {
        PlayerPrefs.DeleteKey(DAILY_DATE_KEY);
        PlayerPrefs.DeleteKey(DAILY_COUNT_KEY);
        PlayerPrefs.Save();

        if (Application.isPlaying && gameObject.activeInHierarchy && !isClaiming)
            RefreshUI();
    }

    #endregion


    #region HELPER METHODS

    void RefreshUI()
    {
        displayedCoin = DataManager.Instance.LoadCoinData();
        UpdateCoinText(displayedCoin);

        if (rewardAmountText != null)
            rewardAmountText.text = $"+{rewardCoin}";

        UpdateDailyLimitText();
        SetButtonsInteractable(true);
    }

    // Hết lượt hôm nay thì khóa nút Claim, Close vẫn bấm được
    void SetButtonsInteractable(bool interactable)
    {
        if (claimCoinAdButton != null)
            claimCoinAdButton.interactable = interactable && CanClaim;

        if (closeButton != null)
            closeButton.interactable = interactable;
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