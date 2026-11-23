using System.Collections;
using UnityEngine;

public class PanelBaseUIController : MonoBehaviour
{
    #region VARIABLES

    [Header("--- PANEL SETTINGS ---")]
    [SerializeField] CanvasGroup panelCanvasGroup;
    [SerializeField] float fadeDuration = 0.25f;
    [SerializeField] float showDelayTime = 0.25f;
    [Tooltip("Bật panel này thì tự tắt panel đang mở trước đó (VD: không đủ xu -> tắt Buy Catbed rồi mới hiện Buy Coin)")]
    [SerializeField] bool closeOtherPanelOnShow = true;

    [Header("--- INTERSTITIAL AD SETTINGS ---")]
    [SerializeField] private bool showInterstitialOnOpen = false;
    [SerializeField] private float interstitialCooldown = 0f;

    private Coroutine fadeCoroutine;
    private Coroutine showDelayCoroutine;

    // Panel đang mở hiện tại (chỉ tính panel bật closeOtherPanelOnShow)
    static PanelBaseUIController currentPanel;

    #endregion


    #region UNITY METHODS

    protected virtual void Awake()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    protected virtual void OnDisable()
    {
        fadeCoroutine = null;
        showDelayCoroutine = null;

        if (currentPanel == this)
            currentPanel = null;
    }

    #endregion


    #region PANEL METHODS

    public virtual void Show()
    {
        CancelShowDelay();
        CloseCurrentPanel();

        // Panel đang tắt thì bắt đầu từ alpha 0 để luôn có hiệu ứng fade in
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            panelCanvasGroup.alpha = 0f;
        }

        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        StartFade(1f, false);
    }

    public virtual void ShowDelay()
    {
        CancelShowDelay();

        // Tắt panel cũ ngay, panel mới hiện sau showDelayTime
        CloseCurrentPanel();

        gameObject.SetActive(true);

        // Keep panel hidden during delay.
        StopFade();

        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        // Object cha đang tắt thì không chạy được coroutine, hiện luôn
        if (!gameObject.activeInHierarchy)
        {
            Show();
            return;
        }

        showDelayCoroutine = StartCoroutine(ShowDelayCoroutine());
    }

    /// <summary>
    /// Fade alpha về 0 rồi tắt hẳn panel (SetActive false).
    /// </summary>
    public virtual void Hide()
    {
        CancelShowDelay();

        if (currentPanel == this)
            currentPanel = null;

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        StartFade(0f, true);
    }

    /// <summary>
    /// Tắt panel đang mở (nếu khác panel này) rồi đánh dấu panel này là panel hiện tại.
    /// </summary>
    void CloseCurrentPanel()
    {
        if (!closeOtherPanelOnShow)
            return;

        PanelBaseUIController previousPanel = currentPanel;
        currentPanel = this;

        if (previousPanel != null && previousPanel != this && previousPanel.gameObject.activeSelf)
            previousPanel.Hide();
    }

    #endregion


    #region DELAY METHODS

    private IEnumerator ShowDelayCoroutine()
    {
        if (showDelayTime > 0f)
            yield return new WaitForSecondsRealtime(showDelayTime);

        showDelayCoroutine = null;

        Show();
    }

    private void CancelShowDelay()
    {
        if (showDelayCoroutine != null)
        {
            StopCoroutine(showDelayCoroutine);
            showDelayCoroutine = null;
        }
    }

    #endregion


    #region FADE METHODS

    private void StartFade(float targetAlpha, bool deactivateOnComplete)
    {
        StopFade();

        // Panel (hoặc object cha) đang tắt thì không chạy được coroutine, đặt kết quả luôn
        if (!gameObject.activeInHierarchy)
        {
            FinishFade(targetAlpha, deactivateOnComplete);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeCanvasGroup(targetAlpha, deactivateOnComplete));
    }

    private void StopFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha, bool deactivateOnComplete)
    {
        float startAlpha = panelCanvasGroup.alpha;
        float duration = Mathf.Max(0f, fadeDuration);

        if (duration > 0f)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsedTime / duration);

                // SmoothStep.
                t = t * t * (3f - 2f * t);

                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }
        }

        fadeCoroutine = null;
        FinishFade(targetAlpha, deactivateOnComplete);
    }

    private void FinishFade(float targetAlpha, bool deactivateOnComplete)
    {
        panelCanvasGroup.alpha = targetAlpha;

        if (deactivateOnComplete)
            gameObject.SetActive(false);
    }

    #endregion
}