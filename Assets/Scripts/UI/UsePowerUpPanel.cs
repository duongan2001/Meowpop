using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel hiện khi dùng power-up: nền đen dần (alpha 0 → 240), icon power-up bay từ nút power-up lên giữa Icon Area,
/// tới nơi bật VFX, chờ 1 giây rồi tắt panel và báo PowerUpManager dùng power-up.
/// </summary>
public class UsePowerUpPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- BACKGROUND ---")]
    [Tooltip("Image nền đen của panel (để trống thì lấy Image trên chính panel)")]
    [SerializeField] Image backgroundImage;
    [Tooltip("Độ đen tối đa của nền (0 - 255)")]
    [Range(0, 255)][SerializeField] int backgroundAlpha = 240;
    [SerializeField] float darkenDuration = 0.3f;

    [Header("--- ICON ---")]
    [Tooltip("Icon Area - icon bay tới giữa vùng này")]
    [SerializeField] RectTransform iconArea;
    [Tooltip("Kích thước icon khi tới giữa Icon Area")]
    [SerializeField] Vector2 iconSize = new Vector2(300f, 300f);
    [Tooltip("Scale lúc icon bắt đầu bay ra từ nút")]
    [SerializeField] float iconStartScale = 0.4f;
    [SerializeField] float iconFlyDuration = 0.5f;
    [SerializeField] Ease iconFlyEase = Ease.OutQuad;
    [Tooltip("Sorting Layer của icon bay (icon có Canvas riêng để vẽ theo layer này)")]
    [SerializeField] string iconSortingLayer = "UI Items";
    [SerializeField] int iconSortingOrder = 0;

    [Header("--- VFX ---")]
    [Tooltip("ItemSparkleBurstBlue - bật khi icon bay tới nơi")]
    [SerializeField] ParticleSystem arriveVfx;
    [Tooltip("Thời gian chờ sau khi icon tới nơi rồi mới tắt panel và dùng power-up")]
    [SerializeField] float holdDuration = 1f;

    [Header("--- TEST ---")]
    [SerializeField] PowerUpType testType = PowerUpType.FillBed;

    Image flyIcon;
    Canvas flyIconCanvas;
    Coroutine playCoroutine;
    Action<bool> onFinished;    // true = chạy xong (dùng power-up), false = bị hủy giữa chừng

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        StopEffects();

        // Bị tắt giữa chừng (panel khác đè, load level...) thì báo hủy để PowerUpManager không bị kẹt
        Finish(false);
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// PowerUpManager gọi khi bấm dùng power-up. fromButton: nút power-up (icon bay ra từ đây).
    /// onFinished(true) khi chạy xong → dùng power-up; onFinished(false) khi bị hủy.
    /// </summary>
    public void PlayUsePowerUp(Sprite icon, RectTransform fromButton, Action<bool> onFinished)
    {
        // Đang chạy dở thì hủy lượt cũ
        Finish(false);
        StopEffects();

        this.onFinished = onFinished;

        // Hiện ngay, không delay
        Show();
        playCoroutine = StartCoroutine(PlayRoutine(icon, fromButton));
    }

    /// <summary>
    /// Hủy ngay, không dùng power-up (VD load level mới).
    /// </summary>
    public void Cancel()
    {
        Finish(false);
        StopEffects();

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    #endregion


    #region ANIMATION METHODS

    IEnumerator PlayRoutine(Sprite icon, RectTransform fromButton)
    {
        Image iconImage = GetFlyIcon();
        RectTransform iconRect = iconImage.rectTransform;
        RectTransform container = (RectTransform)iconImage.transform.parent;

        iconImage.sprite = icon;
        iconImage.gameObject.SetActive(icon != null);
        ApplyIconSorting();
        iconRect.sizeDelta = iconSize;
        iconRect.localScale = Vector3.one * iconStartScale;
        iconRect.localPosition = GetLocalPosition(fromButton, container);

        SetBackgroundAlpha(0f);
        StopVfx();

        // Chờ 1 frame cho qua frame khựng lúc bấm (tween unscaled không bị "nuốt" mất đoạn đầu)
        yield return null;

        // Nền đen dần
        if (backgroundImage != null)
            backgroundImage.DOFade(backgroundAlpha / 255f, darkenDuration).SetUpdate(true).SetTarget(this);

        // Icon bay từ nút lên giữa Icon Area, phóng to dần
        Vector3 targetPos = iconArea != null ? GetLocalPosition(iconArea, container) : Vector3.zero;

        Sequence fly = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        fly.Append(iconRect.DOLocalMove(targetPos, iconFlyDuration).SetEase(iconFlyEase));
        fly.Join(iconRect.DOScale(Vector3.one, iconFlyDuration).SetEase(Ease.OutBack));

        yield return fly.WaitForCompletion();

        // Tới nơi: bật VFX, icon nảy nhẹ
        PlayVfx();
        iconRect.DOPunchScale(Vector3.one * 0.15f, 0.25f, 1).SetUpdate(true).SetTarget(this);

        yield return new WaitForSecondsRealtime(holdDuration);

        playCoroutine = null;

        Hide();
        Finish(true);
    }

    /// <summary>
    /// Icon bay: tạo 1 lần (chỉ RectTransform + Image), nằm trên cùng panel.
    /// </summary>
    Image GetFlyIcon()
    {
        if (flyIcon != null)
        {
            flyIcon.rectTransform.SetAsLastSibling();
            return flyIcon;
        }

        GameObject iconObject = new GameObject("Power-up Fly Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = gameObject.layer;

        RectTransform rect = (RectTransform)iconObject.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.SetAsLastSibling();

        flyIcon = iconObject.GetComponent<Image>();
        flyIcon.preserveAspect = true;
        flyIcon.raycastTarget = false;

        // Canvas riêng để đặt Sorting Layer cho icon
        flyIconCanvas = iconObject.AddComponent<Canvas>();

        return flyIcon;
    }

    /// <summary>
    /// Đặt Sorting Layer cho icon. Canvas con chỉ giữ override khi đang bật nên gọi lại mỗi lần hiện icon.
    /// </summary>
    void ApplyIconSorting()
    {
        if (flyIconCanvas == null || !flyIconCanvas.gameObject.activeInHierarchy)
            return;

        flyIconCanvas.overrideSorting = true;
        flyIconCanvas.sortingLayerName = iconSortingLayer;
        flyIconCanvas.sortingOrder = iconSortingOrder;
    }

    void StopEffects()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        DOTween.Kill(this);

        if (backgroundImage != null)
            backgroundImage.DOKill();

        if (flyIcon != null)
            flyIcon.rectTransform.DOKill();

        StopVfx();
    }

    void Finish(bool isCompleted)
    {
        Action<bool> callback = onFinished;
        onFinished = null;
        callback?.Invoke(isCompleted);
    }

    #endregion


    #region VFX METHODS

    void PlayVfx()
    {
        if (arriveVfx == null)
            return;

        arriveVfx.gameObject.SetActive(true);
        arriveVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        arriveVfx.Play(true);
    }

    void StopVfx()
    {
        if (arriveVfx == null)
            return;

        arriveVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion


    #region TEST METHODS

    /// <summary>
    /// Test: chạy hiệu ứng với power-up testType, KHÔNG dùng power-up, KHÔNG trừ lượt.
    /// Đang Play: chuột phải component → Test Play Panel.
    /// </summary>
    [ContextMenu("Test Play Panel")]
    public void TestPlayPanel()
    {
        if (!Application.isPlaying || PowerUpManager.Instance == null)
        {
            Debug.LogWarning("[UsePowerUpPanel] Chỉ test được khi đang Play");
            return;
        }

        PowerUpData data = PowerUpManager.Instance.GetPowerUpData(testType);

        PlayUsePowerUp(
            data != null ? data.icon : null,
            PowerUpManager.Instance.GetButtonRect(testType),
            isCompleted => Debug.Log($"[UsePowerUpPanel] Test {testType}: {(isCompleted ? "chạy xong" : "bị hủy")}"));
    }

    #endregion


    #region HELPER METHODS

    void SetBackgroundAlpha(float alpha)
    {
        if (backgroundImage == null)
            return;

        Color color = backgroundImage.color;
        color.a = alpha;
        backgroundImage.color = color;
    }

    /// <summary>
    /// Đổi vị trí giữa của 1 rect (có thể nằm ở canvas khác, VD nút power-up ở UI Canvas - Gameplay)
    /// sang tọa độ local của container trong panel này, qua tọa độ màn hình.
    /// </summary>
    Vector3 GetLocalPosition(RectTransform target, RectTransform container)
    {
        if (target == null)
            return Vector3.zero;

        Vector3 worldCenter = target.TransformPoint(target.rect.center);

        Camera targetCam = GetCanvasCamera(target);
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(targetCam, worldCenter);

        Camera containerCam = GetCanvasCamera(container);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPos, containerCam, out Vector2 localPos);

        return localPos;
    }

    Camera GetCanvasCamera(Component component)
    {
        Canvas canvas = component.GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    #endregion
}