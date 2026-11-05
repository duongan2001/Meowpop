using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class Cat : MonoBehaviour
{
    #region VARIABLES

    // Tên state trong Animator Controller
    const string IDLE_STATE = "CatIdleAnim";
    const string WINK_STATE = "CatWinkAnim";

    int catID;
    bool isFlying;
    Coroutine winkCoroutine;

    RectTransform rectTransform;
    Transform originalParent;
    Vector3 originalScale;
    Vector2 originalAnchoredPosition;
    Quaternion originalLocalRotation;
    CatBubble catBubble;

    // Bắn ra khi người chơi bấm vào mèo, logic game lắng nghe để quyết định mèo bay đi đâu
    public event Action<Cat> OnClicked;

    [SerializeField] Image image;
    [SerializeField] Button button;
    [SerializeField] Animator animator;

    [Header("Wink")]
    [SerializeField] float minWinkDelay = 6f;
    [SerializeField] float maxWinkDelay = 10f;

    [Header("Fly To Target")]
    [SerializeField] float flyDuration = 0.45f;
    [SerializeField] float flyEndScale = 0.6f;
    [SerializeField] Ease flyEase = Ease.InOutSine;

    #endregion


    #region PROPERTIES

    public int CatID => catID;
    public bool IsFlying => isFlying;
    public Image Image => image;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalParent = rectTransform.parent;
        originalScale = rectTransform.localScale;
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalLocalRotation = rectTransform.localRotation;
        catBubble = GetComponentInParent<CatBubble>();

        if (image == null)
            image = GetComponent<Image>();

        if (button == null)
            button = GetComponent<Button>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnEnable()
    {
        // Reset trạng thái khi mèo được lấy lại từ pool
        isFlying = false;
        transform.localScale = originalScale;

        if (button != null)
            button.interactable = true;

        winkCoroutine = StartCoroutine(PlayWinkAnimation());
    }

    private void OnDisable()
    {
        if (winkCoroutine != null)
        {
            StopCoroutine(winkCoroutine);
            winkCoroutine = null;
        }

        transform.DOKill();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClick);
    }

    #endregion


    #region SETUP METHODS

    public void SetUpCat(int targetCatID)
    {
        catID = targetCatID;

        SetUpCatSprite();
        SetUpCatAnimator();
    }

    /// <summary>
    /// Gán sprite theo loại mèo (chạy được cả khi chưa có animation).
    /// </summary>
    void SetUpCatSprite()
    {
        Sprite[] sprites = GameManager.Instance.catSprites;

        if (image == null || sprites == null || catID < 0 || catID >= sprites.Length)
            return;

        image.sprite = sprites[catID];
    }

    /// <summary>
    /// Gán animator controller theo loại mèo nếu có, chưa có thì bỏ qua.
    /// </summary>
    void SetUpCatAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            return;

        RuntimeAnimatorController[] controllers = GameManager.Instance.catAnimatorControllers;
        bool hasController = controllers != null && catID >= 0 && catID < controllers.Length && controllers[catID] != null;

        // Chưa có controller cho loại mèo này thì tắt Animator để không đè lên sprite tĩnh
        animator.runtimeAnimatorController = hasController ? controllers[catID] : null;
        animator.enabled = hasController;

        PlayAnimation(IDLE_STATE);
    }

    /// <summary>
    /// Đưa mèo về trạng thái ban đầu trong bóng, dừng bay nếu đang bay dở (dùng khi load level mới).
    /// </summary>
    public void ResetCat()
    {
        // Mèo chưa từng được bật thì chưa chạy Awake, không có gì để reset
        if (rectTransform == null)
            return;

        // DOKill không gọi OnComplete nên sẽ không trừ mèo của bóng / không báo tới ổ
        transform.DOKill();
        ReturnToOriginalParent();

        isFlying = false;
        transform.localScale = originalScale;
    }

    void ReturnToOriginalParent()
    {
        if (transform.parent != originalParent)
            transform.SetParent(originalParent, false);

        rectTransform.anchoredPosition = originalAnchoredPosition;

        // Trả lại góc xoay ban đầu trong bóng (khi bay đã bị xoay về 0)
        rectTransform.localRotation = originalLocalRotation;
    }

    #endregion


    #region INTERACT

    void OnButtonClick()
    {
        if (isFlying)
            return;

        // GameManager lắng nghe để chọn đích (ổ / khay) rồi gọi OnCatClick
        OnClicked?.Invoke(this);
    }

    public void OnCatClick(RectTransform target, Action onArrived = null)
    {
        if (isFlying || target == null)
            return;

        isFlying = true;

        if (winkCoroutine != null)
        {
            StopCoroutine(winkCoroutine);
            winkCoroutine = null;
        }
        PlayAnimation(IDLE_STATE);

        transform.SetParent(GameManager.Instance.catFlyContainer);

        // Trừ mèo ngay khi bấm (mèo đã rời khỏi bóng nên bóng vỡ / tắt cũng không ảnh hưởng mèo đang bay)
        if (catBubble != null)
            catBubble.RemoveCat();

        Vector3 targetPos = GetTargetPosition(target);

        transform.DOKill();
        Sequence flySeq = DOTween.Sequence();
        flySeq.Append(transform.DOMove(targetPos, flyDuration).SetEase(flyEase));
        flySeq.Join(transform.DOScale(originalScale * flyEndScale, flyDuration).SetEase(Ease.InQuad));
        // Xoay dần về thẳng (0, 0, 0) trong lúc bay
        flySeq.Join(transform.DORotate(Vector3.zero, flyDuration).SetEase(flyEase));
        flySeq.SetTarget(transform);
        flySeq.OnComplete(() =>
        {
            onArrived?.Invoke();

            // Trả mèo về parent + vị trí cũ trước khi tắt (không được SetParent trong OnDisable)
            ReturnToOriginalParent();
            gameObject.SetActive(false);
        });
    }

    #endregion


    #region ANIMATION METHODS

    bool CanPlayAnimation()
    {
        return animator != null && animator.enabled && animator.runtimeAnimatorController != null;
    }

    void PlayAnimation(string animState)
    {
        if (!CanPlayAnimation())
            return;

        int stateHash = Animator.StringToHash(animState);
        if (!animator.HasState(0, stateHash))
            return;

        animator.Play(stateHash, 0, 0f);
    }

    /// <summary>
    /// Lấy vị trí world của target theo hệ tọa độ canvas của mèo.
    /// Dùng được cả khi target nằm ở canvas khác (Overlay / Camera).
    /// </summary>
    Vector3 GetTargetPosition(RectTransform target)
    {
        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera targetCam = GetCanvasCamera(targetCanvas);
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(targetCam, target.position);

        Canvas myCanvas = GetComponentInParent<Canvas>();
        Camera myCam = GetCanvasCamera(myCanvas);
        RectTransform parentRect = (RectTransform)rectTransform.parent;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPos, myCam, out Vector3 worldPos))
            return worldPos;

        return target.position;
    }

    Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null)
            return null;

        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    IEnumerator PlayWinkAnimation()
    {
        while (true)
        {
            // Random 1 khoảng thời gian rồi chuyển idle -> nháy mắt
            yield return new WaitForSeconds(UnityEngine.Random.Range(minWinkDelay, maxWinkDelay));

            if (isFlying)
                yield break;

            // Chưa có animation thì bỏ qua lượt này, chờ lượt sau
            if (!CanPlayAnimation())
                continue;

            PlayAnimation(WINK_STATE);

            // Đợi 1 frame để Animator cập nhật sang state wink rồi mới lấy độ dài clip
            yield return null;
            float winkLength = CanPlayAnimation() ? animator.GetCurrentAnimatorStateInfo(0).length : 0f;
            yield return new WaitForSeconds(winkLength);

            if (isFlying)
                yield break;

            PlayAnimation(IDLE_STATE);
        }
    }

    #endregion
}