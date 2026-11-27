using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel chọn ngôn ngữ: mỗi nút ứng với 1 ngôn ngữ (đúng thứ tự enum Language), nút đang chọn đổi sprite.
/// Bấm nút là đổi ngôn ngữ ngay, toàn bộ text tự cập nhật. Gán OnClick trong Editor.
/// </summary>
public class LanguagePanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- LANGUAGE BUTTONS ---")]
    [Tooltip("Button - EN, VN, ID, TH, KO, JA, ZH, FR, PT, ES, RU (đúng thứ tự này)")]
    [SerializeField] Button[] languageButtons;

    [Tooltip("Sprite nút ngôn ngữ đang chọn (màu cam)")]
    [SerializeField] Sprite selectedSprite;
    [Tooltip("Sprite nút ngôn ngữ thường (màu xanh)")]
    [SerializeField] Sprite normalSprite;

    [Header("--- SCROLL ---")]
    [Tooltip("Language Buttons Scroll View - mở panel thì cuộn tới ngôn ngữ đang chọn")]
    [SerializeField] ScrollRect scrollRect;

    Action onClosed;

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();

        // Tự gán OnClick cho từng nút theo thứ tự, không cần kéo 11 lần trong Editor
        for (int i = 0; i < languageButtons.Length; i++)
        {
            if (languageButtons[i] == null)
                continue;

            int languageIndex = i;
            languageButtons[i].onClick.AddListener(() => OnLanguageButtonClick(languageIndex));
        }

        if (languageButtons.Length != MultiLanguageManager.LanguageCount)
            Debug.LogWarning($"[LanguagePanel] Có {languageButtons.Length} nút nhưng game có {MultiLanguageManager.LanguageCount} ngôn ngữ");
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// Mở panel. onClosed: gọi khi bấm Close (VD quay lại Panel - Settings).
    /// </summary>
    public void ShowLanguage(Action onClosed)
    {
        this.onClosed = onClosed;

        RefreshSelected();
        ShowDelay();
        ScrollToSelected();
    }

    /// <summary>
    /// Gán trực tiếp vào OnClick của nút khác nếu cần mở panel mà không quay lại đâu.
    /// </summary>
    public void ShowLanguage()
    {
        ShowLanguage(null);
    }

    #endregion


    #region BUTTON METHODS

    void OnLanguageButtonClick(int languageIndex)
    {
        if (MultiLanguageManager.Instance == null)
            return;

        MultiLanguageManager.Instance.SetLanguage(languageIndex);
        RefreshSelected();
    }

    /// <summary>
    /// Gán vào OnClick của Button - Close.
    /// </summary>
    public void OnCloseButtonClick()
    {
        Hide();

        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    #endregion


    #region HELPER METHODS

    // Nút ngôn ngữ đang dùng đổi sang sprite "đang chọn", các nút khác sprite thường
    void RefreshSelected()
    {
        int selectedIndex = GetSelectedIndex();

        for (int i = 0; i < languageButtons.Length; i++)
        {
            Button button = languageButtons[i];
            if (button == null || button.image == null)
                continue;

            Sprite sprite = i == selectedIndex ? selectedSprite : normalSprite;
            if (sprite != null)
                button.image.sprite = sprite;
        }
    }

    void ScrollToSelected()
    {
        if (scrollRect == null || languageButtons.Length <= 1)
            return;

        // Cuộn dọc: 1 = trên cùng, 0 = dưới cùng
        float t = (float)GetSelectedIndex() / (languageButtons.Length - 1);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - t);
    }

    int GetSelectedIndex()
    {
        return MultiLanguageManager.Instance != null ? (int)MultiLanguageManager.Instance.CurrentLanguage : 0;
    }

    #endregion
}