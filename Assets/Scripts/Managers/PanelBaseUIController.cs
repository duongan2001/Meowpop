using System.Collections;
using UnityEngine;

public class PanelBaseUIController : MonoBehaviour
{
    #region VARIABLES
    
    [Header("--- PANEL SETTINGS ---")]
    [SerializeField] CanvasGroup panelCanvasGroup;
    [SerializeField] float fadeDuration = 0.25f;
    [SerializeField] float showDelayTime = 0.25f;

    [Header("--- INTERSTITIAL AD SETTINGS ---")]
    [SerializeField] private bool showInterstitialOnOpen = false;
    [SerializeField] private float interstitialCooldown = 0f;

    private Coroutine fadeCoroutine;
    private Coroutine showDelayCoroutine;

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
    }

    #endregion


    #region PANEL METHODS

    public virtual void Show()
    {
        CancelShowDelay();

        gameObject.SetActive(true);

        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        StartFade(1f);
    }

    public virtual void ShowDelay()
    {
        CancelShowDelay();

        gameObject.SetActive(true);

        // Keep panel hidden during delay.
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        showDelayCoroutine = StartCoroutine(ShowDelayCoroutine());
    }

    public virtual void Hide()
    {
        CancelShowDelay();

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        StartFade(0f);
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

    private void StartFade(float targetAlpha)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        fadeCoroutine = StartCoroutine(FadeCanvasGroup(targetAlpha));
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha)
    {
        float startAlpha = panelCanvasGroup.alpha;
        float duration = Mathf.Max(0f, fadeDuration);

        if (duration <= 0f)
        {
            panelCanvasGroup.alpha = targetAlpha;
            fadeCoroutine = null;
            yield break;
        }

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

        panelCanvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
    }

    #endregion
}
