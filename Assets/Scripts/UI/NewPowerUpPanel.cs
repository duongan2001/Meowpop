using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel giới thiệu power-up vừa mở khóa (Lv3 / Lv5 / Lv7).
/// Hiện lần lượt Title → New Power-up Icon → Power-up Des → Button - OK (OutBack), icon "thở" nhẹ khi panel mở.
/// Bấm OK (Claim): đóng panel, tặng lượt free (icon rơi vào nút power-up). Gán OnClick trong Editor.
/// </summary>
public class NewPowerUpPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- UI ---")]
    [Tooltip("Title Area / Title")]
    [SerializeField] RectTransform title;
    [Tooltip("New Power-up Icon")]
    [SerializeField] Image powerUpIcon;
    [Tooltip("Power-up Des")]
    [SerializeField] Text powerUpDescriptionText;
    [Tooltip("Button - OK")]
    [SerializeField] Button okButton;

    [Header("--- SHOW ANIMATION ---")]
    [SerializeField] float scaleDuration = 0.3f;

    [Header("--- ICON IDLE ---")]
    [Tooltip("Icon phồng / xẹp nhẹ lặp lại sau khi hiện, = 0 thì tắt")]
    [SerializeField] float iconIdleScaleAmount = 0.06f;
    [SerializeField] float iconIdleDuration = 1.2f;

    [Header("--- VFX ---")]
    [SerializeField] ParticleSystem sparkleVfx;

    [Header("--- TEST ---")]
    [SerializeField] PowerUpType testType = PowerUpType.FillBed;

    PowerUpType currentType;
    bool isWaitingClaim;
    bool isTesting;             // panel mở từ hàm test: bấm OK chỉ đóng, không tặng lượt

    Sequence showSequence;
    Tween iconIdleTween;

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

        // Bị panel khác đè tắt trước khi bấm OK: báo PowerUpManager, lần load level sau sẽ hiện lại
        if (isWaitingClaim && !isTesting && PowerUpManager.Instance != null)
            PowerUpManager.Instance.OnNewPowerUpInterrupted();

        isWaitingClaim = false;
        isTesting = false;

        StopAllEffects();
        StopVfx();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// PowerUpManager gọi: load thông tin power-up vừa mở rồi hiện panel.
    /// </summary>
    public void ShowNewPowerUp(PowerUpType type)
    {
        PowerUpData data = PowerUpManager.Instance != null ? PowerUpManager.Instance.GetPowerUpData(type) : null;

        if (data == null)
        {
            Debug.LogWarning($"[NewPowerUpPanel] Chưa cấu hình PowerUpData cho {type}");
            return;
        }

        currentType = type;
        isWaitingClaim = true;

        if (powerUpDescriptionText != null)
            powerUpDescriptionText.text = MultiLanguageManager.Translate($"powerup.{type}.description", data.description);

        // Không có icon thì giữ ảnh mặc định đang đặt sẵn trong panel
        if (powerUpIcon != null && data.icon != null)
            powerUpIcon.sprite = data.icon;

        ShowDelay();
    }

    public override void Show()
    {
        base.Show();
        PlayShowSequence();
        PlayVfx();
    }

    public override void Hide()
    {
        SetButtonInteractable(okButton, false);
        StopAllEffects();
        StopVfx();
        base.Hide();
    }

    #endregion


    #region BUTTON METHODS

    /// <summary>
    /// Gán vào OnClick của Button - OK: đóng panel, tặng lượt free (icon rơi vào nút power-up).
    /// </summary>
    public void OnOKButtonClick()
    {
        // Chặn bấm nhiều lần
        if (!isWaitingClaim)
            return;

        isWaitingClaim = false;
        Hide();

        if (isTesting)
        {
            isTesting = false;
            return;
        }

        if (PowerUpManager.Instance != null)
            PowerUpManager.Instance.OnNewPowerUpClosed(currentType);
    }

    #endregion


    #region SHOW ANIMATION METHODS

    void PlayShowSequence()
    {
        StopAllEffects();
        ResetScales();
        SetButtonInteractable(okButton, false);

        showSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);

        AppendScale(title);
        AppendScale(powerUpIcon != null ? powerUpIcon.transform : null);
        showSequence.AppendCallback(PlayIconIdle);

        AppendScale(powerUpDescriptionText != null ? powerUpDescriptionText.transform : null);

        // Mở nút ngay lúc nút bắt đầu scale
        if (okButton != null)
        {
            showSequence.AppendCallback(() =>
            {
                if (isWaitingClaim)
                    SetButtonInteractable(okButton, true);
            });

            AppendScale(okButton.transform);
        }
    }

    void AppendScale(Transform element)
    {
        if (element == null || !element.gameObject.activeSelf)
            return;

        showSequence.Append(element.DOScale(GetOriginalScale(element), scaleDuration).SetEase(Ease.OutBack));
    }

    /// <summary>
    /// Icon phồng / xẹp nhẹ lặp lại.
    /// </summary>
    void PlayIconIdle()
    {
        if (powerUpIcon == null || iconIdleScaleAmount <= 0f)
            return;

        Transform icon = powerUpIcon.transform;
        Vector3 baseScale = GetOriginalScale(icon);

        iconIdleTween?.Kill();
        iconIdleTween = icon.DOScale(baseScale * (1f + iconIdleScaleAmount), iconIdleDuration * 0.5f)
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
        if (title != null)
            yield return title;

        if (powerUpIcon != null)
            yield return powerUpIcon.transform;

        if (powerUpDescriptionText != null)
            yield return powerUpDescriptionText.transform;

        if (okButton != null)
            yield return okButton.transform;
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
    /// Test: hiện panel với power-up testType, bấm OK chỉ đóng (không tặng lượt, không lưu gì).
    /// </summary>
    [ContextMenu("Test Show Panel")]
    public void TestShowPanel()
    {
        if (!Application.isPlaying)
            return;

        isTesting = true;
        ShowNewPowerUp(testType);
    }

    #endregion


    #region HELPER METHODS

    void StopAllEffects()
    {
        showSequence?.Kill();
        showSequence = null;

        iconIdleTween?.Kill();
        iconIdleTween = null;

        // Trả icon về kích thước gốc (tránh kẹt ở giữa nhịp phồng / xẹp)
        if (powerUpIcon != null)
            powerUpIcon.transform.localScale = GetOriginalScale(powerUpIcon.transform);
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