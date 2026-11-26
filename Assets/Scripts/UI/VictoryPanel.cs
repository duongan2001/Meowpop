using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel thắng: hiện lần lượt các thành phần (OutBack), chạy thanh tiến trình tới cơ chế mới,
/// nhận thưởng xu (x2 có hiệu ứng xu bung ra và bay lên ô xu).
/// </summary>
public class VictoryPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- COIN ---")]
    [Tooltip("Coin Icon ở góc trên - đích bay tới của xu")]
    [SerializeField] RectTransform coinIcon;
    [SerializeField] Text coinAmountText;

    [Header("--- TITLE ---")]
    [SerializeField] RectTransform title;

    [Header("--- NEW MECHANIC ---")]
    [SerializeField] RectTransform newMechanicBackground;
    [SerializeField] RectTransform newMechanicProgressBackground;
    [Tooltip("Image Type = Filled")]
    [SerializeField] Image newMechanicProgressValue;
    [SerializeField] Image newMechanicIcon;

    [Header("--- REWARD ---")]
    [SerializeField] RectTransform reward;
    [SerializeField] Text rewardAmountText;
    [SerializeField] int rewardCoin = 50;
    [SerializeField] int rewardMultiplier = 2;
    [Tooltip("Thời gian text phần thưởng tăng từ x50 lên x100 khi bấm X2")]
    [SerializeField] float rewardCountUpDuration = 0.5f;

    [Header("--- BUTTONS ---")]
    [SerializeField] Button x2RewardButton;
    [SerializeField] Button nextLevelButton;
    [SerializeField] Text nextLevelText;

    [Header("--- SHOW ANIMATION ---")]
    [SerializeField] float scaleDuration = 0.3f;
    [SerializeField] float progressFillDuration = 0.5f;

    [Header("--- COIN EFFECT ---")]
    [Tooltip("Ảnh xu dùng để bay, để trống thì nhân bản Coin Icon")]
    [SerializeField] Image coinEffectPrefab;
    [Tooltip("Nơi chứa xu bay (nằm trên cùng panel), để trống thì dùng chính panel")]
    [SerializeField] RectTransform coinEffectContainer;
    [Tooltip("Vị trí xu bung ra, để trống thì dùng Reward")]
    [SerializeField] RectTransform coinBurstOrigin;
    [SerializeField] int coinEffectCount = 10;
    [SerializeField] float coinBurstRadius = 150f;
    [SerializeField] float coinBurstDuration = 0.35f;
    [Tooltip("Thời gian xu nằm yên sau khi nổ xong rồi mới bắt đầu bay lên")]
    [SerializeField] float coinHoldAfterBurst = 0.25f;
    [SerializeField] float coinFlyDuration = 0.6f;
    [SerializeField] float coinFlyStagger = 0.05f;
    [SerializeField] float coinFlyEndScale = 0.6f;

    int wonLevel;
    int displayedCoin;
    int currentReward;          // số xu sẽ nhận khi bấm Next Level (x2 thì nhân lên)
    bool isRewardClaimed;
    bool isRewardDoubled;

    Sequence showSequence;
    Coroutine coinEffectCoroutine;
    Tween rewardTextTween;

    readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
    readonly List<Image> coinPool = new List<Image>();

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();

        CacheOriginalScale(coinIcon);
        foreach (Transform element in GetAnimatedElements())
            CacheOriginalScale(element);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllEffects();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// GameManager gọi khi thắng, truyền level vừa thắng.
    /// </summary>
    public void ShowVictory(int level)
    {
        wonLevel = level;
        isRewardClaimed = false;
        isRewardDoubled = false;
        currentReward = rewardCoin;

        ShowDelay();
    }

    public override void Show()
    {
        base.Show();
        PlayShowSequence();
    }

    public override void Hide()
    {
        StopAllEffects();
        base.Hide();
    }

    #endregion


    #region BUTTON METHODS

    /// <summary>
    /// Gán vào OnClick của Button - X2 Reward: nhân đôi phần thưởng, text tăng dần x50 -> x100.
    /// Xu chưa được cộng, bấm Next Level mới nhận.
    /// </summary>
    public void OnX2RewardButtonClick()
    {
        if (isRewardClaimed || isRewardDoubled)
            return;

        // TODO: xem quảng cáo, xem xong mới nhân đôi
        isRewardDoubled = true;

        int fromReward = currentReward;
        currentReward = rewardCoin * rewardMultiplier;

        SetButtonInteractable(x2RewardButton, false);
        SetButtonInteractable(nextLevelButton, false);

        PlayRewardCountUp(fromReward, currentReward, () => SetButtonInteractable(nextLevelButton, true));
    }

    /// <summary>
    /// Gán vào OnClick của Button - Next Level: nhận xu (thường hoặc x2), chạy hiệu ứng xu bay xong mới qua level.
    /// </summary>
    public void OnNextLevelButtonClick()
    {
        if (isRewardClaimed)
            return;

        SetButtonInteractable(x2RewardButton, false);
        SetButtonInteractable(nextLevelButton, false);

        int fromCoin = displayedCoin;
        int toCoin = ClaimReward(currentReward);

        PlayCoinEffect(fromCoin, toCoin, GoToNextLevel);
    }

    /// <summary>
    /// Cộng và lưu xu ngay (thoát game giữa hiệu ứng cũng không mất). Trả về số xu mới.
    /// </summary>
    int ClaimReward(int amount)
    {
        isRewardClaimed = true;

        int newCoin = DataManager.Instance.LoadCoinData() + amount;
        DataManager.Instance.SaveCoinData(newCoin);

        return newCoin;
    }

    void GoToNextLevel()
    {
        Hide();
        GameManager.Instance.NextLevel();
    }

    #endregion


    #region SHOW ANIMATION METHODS

    void PlayShowSequence()
    {
        StopAllEffects();
        ResetScales();
        SetButtonInteractable(x2RewardButton, false);
        SetButtonInteractable(nextLevelButton, false);

        // Load xu hiện tại
        displayedCoin = DataManager.Instance.LoadCoinData();
        UpdateCoinText(displayedCoin);

        // Panel được mở không qua ShowVictory (VD bật tay để test) thì vẫn có phần thưởng mặc định
        if (currentReward <= 0)
            currentReward = rewardCoin;

        if (rewardAmountText != null)
            SetRewardText(currentReward);

        if (nextLevelText != null)
            nextLevelText.text = MultiLanguageManager.TranslateFormat("common.level", "Level {0}", wonLevel + 1);

        bool hasNextMechanic = SetupMechanicProgress(out float targetFill);

        showSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);

        AppendScale(title);

        if (hasNextMechanic)
        {
            AppendScale(newMechanicBackground);
            AppendScale(newMechanicProgressBackground);

            if (newMechanicProgressValue != null)
            {
                showSequence.Append(DOTween.To(
                        () => newMechanicProgressValue.fillAmount,
                        value => newMechanicProgressValue.fillAmount = value,
                        targetFill,
                        progressFillDuration)
                    .SetEase(Ease.OutQuad));
            }
        }

        AppendScale(reward);

        // Mở nút ngay lúc nút đó bắt đầu scale, không chờ hết hiệu ứng
        // Kiểm tra lại trạng thái vì người chơi có thể đã bấm nút trước đó trong lúc hiệu ứng còn chạy
        AppendButtonScale(x2RewardButton, () => !isRewardDoubled && !isRewardClaimed);
        AppendButtonScale(nextLevelButton, () => !isRewardClaimed && !IsRewardCountingUp());
    }

    void AppendButtonScale(Button button, System.Func<bool> canEnable)
    {
        if (button == null)
            return;

        showSequence.AppendCallback(() =>
        {
            if (canEnable())
                SetButtonInteractable(button, true);
        });

        AppendScale(button.transform);
    }

    bool IsRewardCountingUp()
    {
        return rewardTextTween != null && rewardTextTween.IsActive() && rewardTextTween.IsPlaying();
    }

    void AppendScale(Transform element)
    {
        if (element == null || !element.gameObject.activeSelf)
            return;

        showSequence.Append(element.DOScale(GetOriginalScale(element), scaleDuration).SetEase(Ease.OutBack));
    }

    /// <summary>
    /// Scale các thành phần về 0 trước khi chạy hiệu ứng (trừ coin).
    /// </summary>
    void ResetScales()
    {
        foreach (Transform element in GetAnimatedElements())
        {
            element.DOKill();
            element.localScale = Vector3.zero;
        }
    }

    IEnumerable<Transform> GetAnimatedElements()
    {
        Transform[] elements =
        {
            title,
            newMechanicBackground,
            newMechanicProgressBackground,
            reward,
            x2RewardButton != null ? x2RewardButton.transform : null,
            nextLevelButton != null ? nextLevelButton.transform : null,
        };

        foreach (Transform element in elements)
        {
            if (element != null)
                yield return element;
        }
    }

    #endregion


    #region PROGRESS METHODS

    /// <summary>
    /// Tiến trình tới cơ chế mới kế tiếp: thanh chạy từ mốc level vừa thắng tới level tiếp theo.
    /// Không còn cơ chế mới thì ẩn phần New Mechanic, trả về false.
    /// </summary>
    bool SetupMechanicProgress(out float targetFill)
    {
        targetFill = 1f;

        ChallengeManager challengeManager = ChallengeManager.Instance;
        ChallengeData nextChallenge = challengeManager != null ? challengeManager.GetNextChallenge(wonLevel) : null;
        bool hasNextMechanic = nextChallenge != null;

        SetActive(newMechanicBackground, hasNextMechanic);
        SetActive(newMechanicProgressBackground, hasNextMechanic);

        if (!hasNextMechanic)
            return false;

        int startLevel = challengeManager.GetPreviousUnlockLevel(wonLevel);
        float totalLevels = Mathf.Max(1, nextChallenge.unlockLevel - startLevel);

        float startFill = Mathf.Clamp01((wonLevel - startLevel) / totalLevels);
        targetFill = Mathf.Clamp01((wonLevel + 1 - startLevel) / totalLevels);

        if (newMechanicProgressValue != null)
            newMechanicProgressValue.fillAmount = startFill;

        if (newMechanicIcon != null && nextChallenge.icon != null)
            newMechanicIcon.sprite = nextChallenge.icon;

        return true;
    }

    #endregion


    #region COIN EFFECT METHODS

    /// <summary>
    /// Xu bung ra quanh vị trí phần thưởng rồi lần lượt bay lên Coin Icon, số xu tăng dần theo từng đồng.
    /// </summary>
    void PlayCoinEffect(int fromCoin, int toCoin, System.Action onComplete = null)
    {
        KillCoinEffect();
        coinEffectCoroutine = StartCoroutine(CoinEffectRoutine(fromCoin, toCoin, onComplete));
    }

    IEnumerator CoinEffectRoutine(int fromCoin, int toCoin, System.Action onComplete)
    {
        RectTransform container = coinEffectContainer != null ? coinEffectContainer : (RectTransform)transform;
        RectTransform origin = coinBurstOrigin != null ? coinBurstOrigin : reward;

        Vector3 startPos = container.InverseTransformPoint(GetWorldCenter(origin));
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

        // Frame bấm nút thường bị khựng (tạo object, lưu PlayerPrefs, click từ Editor...).
        // Tween dùng thời gian thực (unscaled) KHÔNG bị giới hạn delta, nên nếu chạy ngay thì frame khựng
        // "nuốt" hết 0.35s bung ra -> xu hiện luôn scale 1. Chờ 1 frame cho qua frame khựng rồi mới chạy.
        yield return null;

        int arrivedCount = 0;

        for (int i = 0; i < coinCount; i++)
        {
            Image coin = coins[i];
            RectTransform coinRect = coin.rectTransform;
            Vector3 burstPos = startPos + (Vector3)(Random.insideUnitCircle * coinBurstRadius);

            // Mỗi xu 1 Sequence riêng (không lồng vào Sequence lớn) để giá trị bắt đầu luôn đúng
            Sequence coinFly = DOTween.Sequence().SetUpdate(true).SetTarget(coinRect);

            // Bung ra
            coinFly.Append(coinRect.DOScale(Vector3.one, coinBurstDuration).SetEase(Ease.OutBack));
            coinFly.Join(coinRect.DOLocalMove(burstPos, coinBurstDuration).SetEase(Ease.OutQuad));
            // Tất cả xu nổ xong cùng lúc, nằm yên coinHoldAfterBurst giây rồi mới lần lượt bay lên
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

    /// <summary>
    /// Đổi text phần thưởng rồi build lại layout của Reward ngay trong frame,
    /// để layout group / content size fitter cập nhật kích thước khi chữ dài ra (x50 -> x100).
    /// </summary>
    void SetRewardText(int rewardValue)
    {
        rewardAmountText.text = $"x{rewardValue}";

        if (reward != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(reward);
    }

    /// <summary>
    /// Text phần thưởng tăng dần (VD x50 -> x100) trong rewardCountUpDuration giây.
    /// </summary>
    void PlayRewardCountUp(int fromReward, int toReward, System.Action onComplete)
    {
        rewardTextTween?.Kill();

        if (rewardAmountText == null || rewardCountUpDuration <= 0f)
        {
            if (rewardAmountText != null)
                SetRewardText(toReward);

            onComplete?.Invoke();
            return;
        }

        int shownReward = fromReward;

        rewardTextTween = DOTween.To(() => shownReward, value =>
        {
            shownReward = value;
            SetRewardText(shownReward);
        }, toReward, rewardCountUpDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => onComplete?.Invoke());
    }

    void OnCoinArrived(int fromCoin, int toCoin, int arrivedIndex, int coinCount)
    {
        displayedCoin = fromCoin + Mathf.RoundToInt((toCoin - fromCoin) * (float)arrivedIndex / coinCount);
        UpdateCoinText(displayedCoin);

        // Coin Icon nảy nhẹ mỗi lần có xu bay tới
        if (coinIcon != null)
        {
            coinIcon.DOKill();
            coinIcon.localScale = GetOriginalScale(coinIcon);
            coinIcon.DOPunchScale(Vector3.one * 0.2f, 0.15f, 1).SetUpdate(true);
        }
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
    /// Tạo 1 object xu "sạch" (chỉ có RectTransform + Image), chép hình từ template.
    /// Không Instantiate template vì Coin Icon có thể mang theo Animator / DOTweenAnimation / script khác
    /// tự set scale = 1 khi bật lên, làm mất hiệu ứng bung từ 0.
    /// </summary>
    Image CreateCoinImage(Image template, RectTransform container)
    {
        GameObject coinObject = new GameObject("Coin Effect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coinObject.layer = container.gameObject.layer;

        RectTransform rect = (RectTransform)coinObject.transform;
        rect.SetParent(container, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = template.rectTransform.rect.size;
        rect.localRotation = Quaternion.identity;
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
            coinIcon.localScale = GetOriginalScale(coinIcon);
        }
    }

    #endregion


    #region TEST METHODS

    /// <summary>
    /// Test hiệu ứng xu bay, KHÔNG cộng / lưu xu thật.
    /// Gọi khi đang Play và panel đang hiện: chuột phải component → Test Coin Effect, hoặc gán vào 1 nút test.
    /// </summary>
    [ContextMenu("Test Coin Effect")]
    public void TestCoinEffect()
    {
        if (!Application.isPlaying || !gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[VictoryPanel] Chỉ test được khi đang Play và panel đang hiện");
            return;
        }

        // Lấy số xu đang hiện làm mốc, chỉ tăng số trên UI.
        // Dùng phần thưởng hiện tại: chưa bấm X2 = 50, đã bấm X2 = 100
        int testReward = currentReward > 0 ? currentReward : rewardCoin;
        PlayCoinEffect(displayedCoin, displayedCoin + testReward);
    }

    #endregion


    #region HELPER METHODS

    void StopAllEffects()
    {
        showSequence?.Kill();
        showSequence = null;

        rewardTextTween?.Kill();
        rewardTextTween = null;

        KillCoinEffect();
    }

    void UpdateCoinText(int coin)
    {
        if (coinAmountText != null)
            coinAmountText.text = coin.ToString();
    }

    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    void SetActive(Component component, bool isActive)
    {
        if (component != null)
            component.gameObject.SetActive(isActive);
    }

    void CacheOriginalScale(Transform element)
    {
        if (element != null && !originalScales.ContainsKey(element))
            originalScales[element] = element.localScale;
    }

    Vector3 GetOriginalScale(Transform element)
    {
        return originalScales.TryGetValue(element, out Vector3 scale) ? scale : Vector3.one;
    }

    Vector3 GetWorldCenter(RectTransform rect)
    {
        return rect.TransformPoint(rect.rect.center);
    }

    #endregion
}