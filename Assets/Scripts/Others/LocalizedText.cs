using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn vào UI Text cố định (tiêu đề panel, chữ trên nút...): tự lấy bản dịch theo key, đổi ngôn ngữ thì tự cập nhật.
/// Để trống key: chỉ đổi font theo ngôn ngữ (dùng cho text do code gán lúc show panel, VD mô tả power-up).
/// </summary>
[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    #region VARIABLES

    [Tooltip("Key trong file CSV, VD: fulltray.title. Để trống thì chỉ đổi font, không đổi chữ")]
    [SerializeField] string key;

    [Tooltip("Đổi font theo ngôn ngữ (font cấu hình trong MultiLanguageManager)")]
    [SerializeField] bool applyLanguageFont = true;

    Text text;
    Font originalFont;

    #endregion


    #region PROPERTIES

    public string Key => key;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        CacheText();
    }

    private void OnEnable()
    {
        MultiLanguageManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        MultiLanguageManager.OnLanguageChanged -= Refresh;
    }

    #endregion


    #region LOCALIZE METHODS

    /// <summary>
    /// Đổi key lúc chạy (VD 1 text dùng nhiều câu khác nhau) rồi cập nhật luôn.
    /// </summary>
    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }

    /// <summary>
    /// Cập nhật chữ + font theo ngôn ngữ hiện tại.
    /// </summary>
    public void Refresh()
    {
        CacheText();

        if (text == null || MultiLanguageManager.Instance == null)
            return;

        if (!string.IsNullOrEmpty(key))
            text.text = MultiLanguageManager.Translate(key, text.text);

        if (applyLanguageFont)
        {
            Font languageFont = MultiLanguageManager.Instance.GetCurrentFont();
            text.font = languageFont != null ? languageFont : originalFont;
        }
    }

    void CacheText()
    {
        if (text != null)
            return;

        text = GetComponent<Text>();
        originalFont = text != null ? text.font : null;
    }

    #endregion
}