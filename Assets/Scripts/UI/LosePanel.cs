using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel thua: hiện lần lượt Title → Cat Icon → Button - Retry (OutBack), mèo "thở" nhẹ khi panel đang mở.
/// Gán OnClick của Button - Retry trong Editor vào OnRetryButtonClick().
/// </summary>
public class LosePanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- UI ---")]
    [Tooltip("Title (YOU LOSE)")]
    [SerializeField] RectTransform title;
    [Tooltip("Cat Icon")]
    [SerializeField] RectTransform catIcon;
    [Tooltip("Button - Retry")]
    [SerializeField] Button retryButton;

    [Header("--- SHOW ANIMATION ---")]
    [SerializeField] float scaleDuration = 0.3f;

    [Header("--- CAT IDLE ---")]
    [Tooltip("Mèo phồng / xẹp nhẹ lặp lại sau khi hiện, = 0 thì tắt")]
    [SerializeField] float catIdleScaleAmount = 0.04f;
    [SerializeField] float catIdleDuration = 1.2f;

    Sequence showSequence;
    Tween catIdleTween;
    bool isRetrying;

    readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();

        foreach (Transform element in GetAnimatedElements())
            originalScales[element] = element.localScale;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllEffects();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// GameManager gọi khi thua (khay đầy).
    /// </summary>
    public void ShowLose()
    {
        isRetrying = false;
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
    /// Gán vào OnClick của Button - Retry: đóng panel, chơi lại level hiện tại.
    /// </summary>
    public void OnRetryButtonClick()
    {
        // Chặn bấm nhiều lần
        if (isRetrying)
            return;

        isRetrying = true;
        SetButtonInteractable(retryButton, false);

        Hide();
        GameManager.Instance.ReplayLevel();
    }

    #endregion


    #region SHOW ANIMATION METHODS

    void PlayShowSequence()
    {
        StopAllEffects();
        ResetScales();
        SetButtonInteractable(retryButton, false);

        showSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);

        AppendScale(title);
        AppendScale(catIcon);
        showSequence.AppendCallback(PlayCatIdle);

        // Mở nút ngay lúc nút bắt đầu scale
        if (retryButton != null)
        {
            showSequence.AppendCallback(() =>
            {
                if (!isRetrying)
                    SetButtonInteractable(retryButton, true);
            });

            AppendScale(retryButton.transform);
        }
    }

    void AppendScale(Transform element)
    {
        if (element == null || !element.gameObject.activeSelf)
            return;

        showSequence.Append(element.DOScale(GetOriginalScale(element), scaleDuration).SetEase(Ease.OutBack));
    }

    /// <summary>
    /// Mèo phồng / xẹp nhẹ lặp lại, như đang thút thít.
    /// </summary>
    void PlayCatIdle()
    {
        if (catIcon == null || catIdleScaleAmount <= 0f)
            return;

        Vector3 baseScale = GetOriginalScale(catIcon);

        catIdleTween?.Kill();
        catIdleTween = catIcon.DOScale(baseScale * (1f + catIdleScaleAmount), catIdleDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    /// <summary>
    /// Scale các thành phần về 0 trước khi chạy hiệu ứng.
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
            catIcon,
            retryButton != null ? retryButton.transform : null,
        };

        foreach (Transform element in elements)
        {
            if (element != null)
                yield return element;
        }
    }

    #endregion


    #region HELPER METHODS

    void StopAllEffects()
    {
        showSequence?.Kill();
        showSequence = null;

        catIdleTween?.Kill();
        catIdleTween = null;

        // Trả mèo về kích thước gốc (tránh kẹt ở giữa nhịp phồng / xẹp)
        if (catIcon != null)
            catIcon.localScale = GetOriginalScale(catIcon);
    }

    void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    Vector3 GetOriginalScale(Transform element)
    {
        return originalScales.TryGetValue(element, out Vector3 scale) ? scale : Vector3.one;
    }

    #endregion
}