using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn trên từng nút "Power-up N". Chỉ lo hiển thị + hiệu ứng, logic nằm ở PowerUpManager.
/// Gán OnClick của Button trong Editor vào OnPowerUpButtonClick().
/// </summary>
public class PowerUpButton : MonoBehaviour
{
    #region VARIABLES

    [SerializeField] PowerUpType type;

    [Header("--- UI ---")]
    [SerializeField] Button button;
    [Tooltip("Power-up Locked")]
    [SerializeField] GameObject lockedObject;
    [Tooltip("Target Level Text (VD: Lv3)")]
    [SerializeField] Text targetLevelText;
    [Tooltip("Plus Icon - hiện khi hết lượt")]
    [SerializeField] GameObject plusIcon;
    [Tooltip("Remain Count - badge số lượt")]
    [SerializeField] GameObject remainCountObject;
    [SerializeField] Text remainText;

    [Header("--- GAIN ANIMATION ---")]
    [Tooltip("Nơi chứa icon rơi (nằm trên cùng), để trống thì dùng GameManager.catFlyContainer")]
    [SerializeField] RectTransform effectContainer;
    [Tooltip("Icon xuất hiện cao hơn nút bao nhiêu rồi rơi xuống")]
    [SerializeField] float dropHeight = 400f;
    [SerializeField] float appearDuration = 0.25f;
    [SerializeField] float holdDuration = 0.15f;
    [SerializeField] float dropDuration = 0.4f;
    [SerializeField] float dropEndScale = 0.6f;

    Image dropIcon;
    Coroutine gainCoroutine;
    Vector3 originalScale;

    #endregion


    #region PROPERTIES

    public PowerUpType Type => type;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        originalScale = transform.localScale;

        if (button == null)
            button = GetComponent<Button>();
    }

    private void OnDisable()
    {
        StopGainAnimation();
    }

    #endregion


    #region DISPLAY METHODS

    /// <summary>
    /// Cập nhật giao diện nút: khóa (Lv) / hết lượt (dấu +) / còn lượt (badge số).
    /// </summary>
    public void Refresh(bool isUnlocked, int count, int unlockLevel)
    {
        SetActive(lockedObject, !isUnlocked);
        SetActive(targetLevelText != null ? targetLevelText.gameObject : null, !isUnlocked);

        if (targetLevelText != null)
            targetLevelText.text = $"Lv{unlockLevel}";

        SetActive(plusIcon, isUnlocked && count <= 0);
        SetActive(remainCountObject, isUnlocked && count > 0);

        if (remainText != null)
            remainText.text = count.ToString();
    }

    #endregion


    #region INTERACT

    /// <summary>
    /// Gán vào OnClick của Button trong Editor.
    /// </summary>
    public void OnPowerUpButtonClick()
    {
        if (PowerUpManager.Instance != null)
            PowerUpManager.Instance.OnPowerUpButtonClicked(type);
    }

    #endregion


    #region GAIN ANIMATION METHODS

    /// <summary>
    /// Icon power-up hiện phía trên nút, rơi vào nút, nút nảy nhẹ, tới nơi gọi onArrived (cập nhật số).
    /// </summary>
    public void PlayGainAnimation(Sprite icon, Action onArrived)
    {
        StopGainAnimation();

        if (icon == null || !gameObject.activeInHierarchy)
        {
            onArrived?.Invoke();
            return;
        }

        gainCoroutine = StartCoroutine(GainAnimationRoutine(icon, onArrived));
    }

    IEnumerator GainAnimationRoutine(Sprite icon, Action onArrived)
    {
        RectTransform container = effectContainer != null ? effectContainer : GameManager.Instance.catFlyContainer;
        RectTransform buttonRect = (RectTransform)transform;

        Image iconImage = GetDropIcon(container, buttonRect);
        iconImage.sprite = icon;

        RectTransform iconRect = iconImage.rectTransform;
        Vector3 targetPos = container.InverseTransformPoint(buttonRect.TransformPoint(buttonRect.rect.center));
        Vector3 startPos = targetPos + Vector3.up * dropHeight;

        iconRect.DOKill();
        iconRect.localPosition = startPos;
        iconRect.localScale = Vector3.zero;
        iconRect.SetAsLastSibling();
        iconImage.gameObject.SetActive(true);

        // Chờ 1 frame cho qua frame khựng (mở khóa / mua), tránh tween bị "nuốt"
        yield return null;

        Sequence dropSequence = DOTween.Sequence().SetTarget(iconRect);
        dropSequence.Append(iconRect.DOScale(Vector3.one, appearDuration).SetEase(Ease.OutBack));
        dropSequence.AppendInterval(holdDuration);
        dropSequence.Append(iconRect.DOLocalMove(targetPos, dropDuration).SetEase(Ease.InQuad));
        dropSequence.Join(iconRect.DOScale(Vector3.one * dropEndScale, dropDuration).SetEase(Ease.InQuad));
        dropSequence.OnComplete(() =>
        {
            iconImage.gameObject.SetActive(false);
            gainCoroutine = null;

            // Nút nảy nhẹ khi icon rơi vào
            transform.DOKill();
            transform.localScale = originalScale;
            transform.DOPunchScale(originalScale * 0.2f, 0.25f, 1);

            onArrived?.Invoke();
        });
    }

    /// <summary>
    /// Tạo 1 lần ảnh icon "sạch" (chỉ RectTransform + Image), kích thước bằng nút.
    /// </summary>
    Image GetDropIcon(RectTransform container, RectTransform buttonRect)
    {
        if (dropIcon != null)
            return dropIcon;

        GameObject iconObject = new GameObject($"{type} Drop Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = container.gameObject.layer;

        RectTransform rect = (RectTransform)iconObject.transform;
        rect.SetParent(container, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = buttonRect.rect.size;

        dropIcon = iconObject.GetComponent<Image>();
        dropIcon.preserveAspect = true;
        dropIcon.raycastTarget = false;
        iconObject.SetActive(false);

        return dropIcon;
    }

    void StopGainAnimation()
    {
        if (gainCoroutine != null)
        {
            StopCoroutine(gainCoroutine);
            gainCoroutine = null;
        }

        if (dropIcon != null)
        {
            dropIcon.rectTransform.DOKill();
            dropIcon.gameObject.SetActive(false);
        }

        transform.DOKill();
        if (originalScale != Vector3.zero)
            transform.localScale = originalScale;
    }

    #endregion


    #region HELPER METHODS

    void SetActive(GameObject target, bool isActive)
    {
        if (target != null)
            target.SetActive(isActive);
    }

    #endregion
}