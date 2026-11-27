using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel cài đặt: bật / tắt nhạc nền, âm thanh, rung; mở Panel - Language; Rate us.
/// Gán OnClick các nút trong Editor (nút bánh răng → ShowSettings).
/// </summary>
public class SettingsPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- TOGGLES ---")]
    [Tooltip("Music Toggle State")]
    [SerializeField] Image musicToggleState;
    [Tooltip("Sound Toggle State")]
    [SerializeField] Image soundToggleState;
    [Tooltip("Vibration Toggle State")]
    [SerializeField] Image vibrationToggleState;

    [Tooltip("Sprite công tắc đang bật")]
    [SerializeField] Sprite toggleOnSprite;
    [Tooltip("Sprite công tắc đang tắt")]
    [SerializeField] Sprite toggleOffSprite;

    [Header("--- LANGUAGE ---")]
    [Tooltip("Panel - Language")]
    [SerializeField] LanguagePanel languagePanel;

    [Header("--- RATE US ---")]
    [Tooltip("Link trang game trên App Store (iOS). Android tự dùng link Google Play theo package name")]
    [SerializeField] string iosStoreUrl;

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// Gán vào OnClick của nút bánh răng: load trạng thái đã lưu rồi hiện panel.
    /// </summary>
    public void ShowSettings()
    {
        RefreshToggles();
        ShowDelay();
    }

    #endregion


    #region BUTTON METHODS

    /// <summary>
    /// Gán vào OnClick của Music Toggle.
    /// </summary>
    public void OnMusicToggleClick()
    {
        bool isOn = DataManager.Instance.LoadMusicStateData() != 1;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMusicOn(isOn);
        else
            DataManager.Instance.SaveMusicStateData(isOn ? 1 : 0);

        RefreshToggles();
    }

    /// <summary>
    /// Gán vào OnClick của Sound Toggle.
    /// </summary>
    public void OnSoundToggleClick()
    {
        bool isOn = DataManager.Instance.LoadSoundEffectStateData() != 1;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSoundEffectOn(isOn);
        else
            DataManager.Instance.SaveSoundEffectStateData(isOn ? 1 : 0);

        RefreshToggles();
    }

    /// <summary>
    /// Gán vào OnClick của Vibration Toggle. Bật lên thì rung 1 cái cho người chơi biết.
    /// </summary>
    public void OnVibrationToggleClick()
    {
        bool isOn = DataManager.Instance.LoadVibrationStateData() != 1;
        DataManager.Instance.SaveVibrationStateData(isOn ? 1 : 0);

        if (isOn)
            Vibrate();

        RefreshToggles();
    }

    /// <summary>
    /// Gán vào OnClick của Button - Language: mở Panel - Language (panel này tự tắt), đóng Language thì quay lại đây.
    /// </summary>
    public void OnLanguageButtonClick()
    {
        if (languagePanel == null)
            return;

        languagePanel.ShowLanguage(ShowSettings);
    }

    /// <summary>
    /// Gán vào OnClick của Button - Rate us: mở trang game trên store.
    /// </summary>
    public void OnRateUsButtonClick()
    {
#if UNITY_ANDROID
        Application.OpenURL($"market://details?id={Application.identifier}");
#elif UNITY_IOS
        if (!string.IsNullOrEmpty(iosStoreUrl))
            Application.OpenURL(iosStoreUrl);
#else
        Application.OpenURL($"https://play.google.com/store/apps/details?id={Application.identifier}");
#endif
    }

    /// <summary>
    /// Gán vào OnClick của Button - Close.
    /// </summary>
    public void OnCloseButtonClick()
    {
        Hide();
    }

    #endregion


    #region TOGGLE METHODS

    void RefreshToggles()
    {
        SetToggleState(musicToggleState, DataManager.Instance.LoadMusicStateData() == 1);
        SetToggleState(soundToggleState, DataManager.Instance.LoadSoundEffectStateData() == 1);
        SetToggleState(vibrationToggleState, DataManager.Instance.LoadVibrationStateData() == 1);
    }

    void SetToggleState(Image toggleState, bool isOn)
    {
        if (toggleState == null)
            return;

        Sprite sprite = isOn ? toggleOnSprite : toggleOffSprite;
        if (sprite != null)
            toggleState.sprite = sprite;
    }

    #endregion


    #region HELPER METHODS

    /// <summary>
    /// Rung máy nếu đang bật rung (chỉ chạy trên điện thoại). Script khác cũng gọi được: SettingsPanel.TryVibrate().
    /// </summary>
    public static void TryVibrate()
    {
        if (DataManager.Instance != null && DataManager.Instance.LoadVibrationStateData() == 1)
            Vibrate();
    }

    static void Vibrate()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    #endregion
}