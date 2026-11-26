using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Các ngôn ngữ trong game. Mã (code) trùng với tên cột trong file CSV.
/// Lưu ý: giá trị int được lưu PlayerPrefs, thêm ngôn ngữ mới thì thêm vào CUỐI.
/// </summary>
public enum Language
{
    English,        // en
    Vietnamese,     // vi
    Indonesian,     // id
    Thai,           // th
    Korean,         // ko
    Japanese,       // ja
    Chinese,        // zh (giản thể)
    French,         // fr
    Portuguese,     // pt
    Spanish,        // es
    Russian,        // ru
}

/// <summary>
/// Quản lý đa ngôn ngữ: đọc bảng dịch từ file CSV (cột key, en, vi, id, ...), lưu ngôn ngữ đã chọn,
/// báo cho các text cập nhật khi đổi ngôn ngữ.
/// Text cố định: gắn LocalizedText. Text đổi lúc show panel: gọi MultiLanguageManager.Translate(key, fallback).
/// </summary>
public class MultiLanguageManager : MonoBehaviour
{
    #region VARIABLES

    const string LANGUAGE_KEY = "language";
    const string KEY_COLUMN = "key";

    [Tooltip("File CSV bảng dịch (UTF-8): dòng đầu là key, en, vi, id, th, ko, ja, zh, fr, pt, es, ru")]
    [SerializeField] TextAsset localizationCsv;

    [Tooltip("Ngôn ngữ khi mở game lần đầu mà máy dùng ngôn ngữ không có trong game")]
    [SerializeField] Language defaultLanguage = Language.English;

    [Tooltip("Lần đầu mở game thì tự chọn theo ngôn ngữ của máy")]
    [SerializeField] bool useSystemLanguageOnFirstLaunch = true;

    [Header("--- FONTS ---")]
    [Tooltip("Font riêng cho ngôn ngữ cần bộ chữ khác (Thái, Hàn, Nhật, Trung, Nga...). Không có thì giữ font gốc của text")]
    [SerializeField] LanguageFont[] languageFonts;

    /// <summary>
    /// Bắn ra khi đổi ngôn ngữ (và 1 lần lúc load xong bảng dịch). Static để text đăng ký được cả khi manager chưa Awake.
    /// </summary>
    public static event Action OnLanguageChanged;

    // Mã ngôn ngữ dùng làm tên cột trong CSV (cùng thứ tự với enum Language)
    static readonly string[] LANGUAGE_CODES = { "en", "vi", "id", "th", "ko", "ja", "zh", "fr", "pt", "es", "ru" };

    // Tên ngôn ngữ viết bằng chính ngôn ngữ đó, dùng cho nút / dropdown chọn ngôn ngữ
    static readonly string[] LANGUAGE_NATIVE_NAMES =
    {
        "English", "Tiếng Việt", "Bahasa Indonesia", "ไทย", "한국어", "日本語",
        "简体中文", "Français", "Português", "Español", "Русский",
    };

    // key -> bản dịch theo từng ngôn ngữ (index = (int)Language)
    readonly Dictionary<string, string[]> translations = new Dictionary<string, string[]>();
    readonly HashSet<string> warnedMissingKeys = new HashSet<string>();

    Language currentLanguage;

    #endregion


    #region PROPERTIES

    public static MultiLanguageManager Instance { get; private set; }

    public Language CurrentLanguage => currentLanguage;
    public static int LanguageCount => LANGUAGE_CODES.Length;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[MultiLanguageManager] Có nhiều hơn 1 MultiLanguageManager trong scene, xóa bản thừa");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadTranslations();
        currentLanguage = LoadSavedLanguage();

        // Text nào đã bật trước khi manager Awake thì cập nhật lại
        OnLanguageChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion


    #region LANGUAGE METHODS

    /// <summary>
    /// Đổi ngôn ngữ, lưu lại và cập nhật toàn bộ text.
    /// </summary>
    public void SetLanguage(Language language)
    {
        if (!Enum.IsDefined(typeof(Language), language))
            return;

        currentLanguage = language;

        PlayerPrefs.SetInt(LANGUAGE_KEY, (int)language);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Dùng cho Dropdown OnValueChanged / nút trong Editor (truyền index theo enum Language).
    /// </summary>
    public void SetLanguage(int languageIndex)
    {
        SetLanguage((Language)languageIndex);
    }

    /// <summary>
    /// Chuyển sang ngôn ngữ kế tiếp (gán cho nút đổi ngôn ngữ dạng bấm vòng).
    /// </summary>
    public void NextLanguage()
    {
        SetLanguage((Language)(((int)currentLanguage + 1) % LanguageCount));
    }

    public void PreviousLanguage()
    {
        SetLanguage((Language)(((int)currentLanguage - 1 + LanguageCount) % LanguageCount));
    }

    public static string GetNativeName(Language language)
    {
        int index = (int)language;
        return index >= 0 && index < LANGUAGE_NATIVE_NAMES.Length ? LANGUAGE_NATIVE_NAMES[index] : language.ToString();
    }

    Language LoadSavedLanguage()
    {
        if (PlayerPrefs.HasKey(LANGUAGE_KEY))
        {
            int saved = PlayerPrefs.GetInt(LANGUAGE_KEY);
            if (Enum.IsDefined(typeof(Language), saved))
                return (Language)saved;
        }

        return useSystemLanguageOnFirstLaunch ? FromSystemLanguage(Application.systemLanguage) : defaultLanguage;
    }

    Language FromSystemLanguage(SystemLanguage systemLanguage)
    {
        switch (systemLanguage)
        {
            case SystemLanguage.English: return Language.English;
            case SystemLanguage.Vietnamese: return Language.Vietnamese;
            case SystemLanguage.Indonesian: return Language.Indonesian;
            case SystemLanguage.Thai: return Language.Thai;
            case SystemLanguage.Korean: return Language.Korean;
            case SystemLanguage.Japanese: return Language.Japanese;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return Language.Chinese;
            case SystemLanguage.French: return Language.French;
            case SystemLanguage.Portuguese: return Language.Portuguese;
            case SystemLanguage.Spanish: return Language.Spanish;
            case SystemLanguage.Russian: return Language.Russian;
            default: return defaultLanguage;
        }
    }

    #endregion


    #region TRANSLATE METHODS

    /// <summary>
    /// Lấy bản dịch theo ngôn ngữ hiện tại. Thiếu bản dịch thì lấy tiếng Anh, không có key thì trả về fallback (hoặc chính key).
    /// Gọi được cả khi chưa có manager trong scene (trả về fallback).
    /// </summary>
    public static string Translate(string key, string fallback = null)
    {
        if (string.IsNullOrEmpty(key))
            return fallback ?? string.Empty;

        if (Instance == null)
            return fallback ?? key;

        return Instance.Get(key, fallback);
    }

    /// <summary>
    /// Bản dịch có tham số, VD key "common.level" = "Level {0}" -> TranslateFormat("common.level", 5) = "Level 5".
    /// </summary>
    public static string TranslateFormat(string key, string fallbackFormat, params object[] args)
    {
        string format = Translate(key, fallbackFormat);

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[MultiLanguageManager] Bản dịch của key \"{key}\" sai định dạng {{0}}: {format}");
            return fallbackFormat != null ? string.Format(fallbackFormat, args) : format;
        }
    }

    public string Get(string key, string fallback = null)
    {
        if (!translations.TryGetValue(key, out string[] values))
        {
            // Chỉ cảnh báo 1 lần mỗi key cho đỡ rối Console
            if (warnedMissingKeys.Add(key))
                Debug.LogWarning($"[MultiLanguageManager] Chưa có key \"{key}\" trong file CSV");

            return fallback ?? key;
        }

        string value = values[(int)currentLanguage];
        if (!string.IsNullOrEmpty(value))
            return value;

        // Ngôn ngữ này chưa dịch thì dùng tiếng Anh
        string english = values[(int)Language.English];
        return !string.IsNullOrEmpty(english) ? english : fallback ?? key;
    }

    public bool HasKey(string key)
    {
        return !string.IsNullOrEmpty(key) && translations.ContainsKey(key);
    }

    #endregion


    #region FONT METHODS

    /// <summary>
    /// Font riêng của ngôn ngữ hiện tại, null nếu không cấu hình (giữ font gốc).
    /// </summary>
    public Font GetCurrentFont()
    {
        if (languageFonts == null)
            return null;

        foreach (LanguageFont languageFont in languageFonts)
        {
            if (languageFont.language == currentLanguage && languageFont.font != null)
                return languageFont.font;
        }

        return null;
    }

    #endregion


    #region CSV METHODS

    void LoadTranslations()
    {
        translations.Clear();

        if (localizationCsv == null)
        {
            Debug.LogError("[MultiLanguageManager] Chưa gán file CSV bảng dịch");
            return;
        }

        List<List<string>> rows = ParseCsv(localizationCsv.text);
        if (rows.Count == 0)
            return;

        // Dòng đầu: xác định cột nào là key, cột nào là ngôn ngữ nào
        List<string> header = rows[0];
        int keyColumn = -1;
        int[] languageColumns = new int[LanguageCount];

        for (int i = 0; i < languageColumns.Length; i++)
            languageColumns[i] = -1;

        for (int col = 0; col < header.Count; col++)
        {
            string name = header[col].Trim().ToLowerInvariant();

            if (name == KEY_COLUMN)
            {
                keyColumn = col;
                continue;
            }

            int languageIndex = Array.IndexOf(LANGUAGE_CODES, name);
            if (languageIndex >= 0)
                languageColumns[languageIndex] = col;
        }

        if (keyColumn < 0)
        {
            Debug.LogError($"[MultiLanguageManager] File CSV thiếu cột \"{KEY_COLUMN}\"");
            return;
        }

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (keyColumn >= row.Count)
                continue;

            string key = row[keyColumn].Trim();

            // Bỏ dòng trống và dòng ghi chú (bắt đầu bằng #)
            if (string.IsNullOrEmpty(key) || key.StartsWith("#"))
                continue;

            string[] values = new string[LanguageCount];
            for (int lang = 0; lang < LanguageCount; lang++)
            {
                int col = languageColumns[lang];
                values[lang] = col >= 0 && col < row.Count ? row[col].Replace("\\n", "\n") : string.Empty;
            }

            if (translations.ContainsKey(key))
                Debug.LogWarning($"[MultiLanguageManager] Key \"{key}\" bị trùng trong file CSV, dùng dòng sau cùng");

            translations[key] = values;
        }
    }

    /// <summary>
    /// Tách CSV chuẩn: hỗ trợ ô có dấu phẩy / xuống dòng / dấu ngoặc kép khi bọc trong "...".
    /// </summary>
    static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool inQuotes = false;

        // Bỏ BOM nếu file lưu UTF-8 có BOM
        if (text.Length > 0 && text[0] == '﻿')
            text = text.Substring(1);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" trong ô bọc ngoặc là 1 dấu "
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                        inQuotes = false;
                }
                else
                    cell.Append(c);

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;

                case '\r':
                    break;

                case '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;

                default:
                    cell.Append(c);
                    break;
            }
        }

        // Dòng cuối không có xuống dòng
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    #endregion


    #region TEST METHODS

    [ContextMenu("Test/Next Language")]
    public void TestNextLanguage()
    {
        if (!Application.isPlaying)
            return;

        NextLanguage();
        Debug.Log($"[MultiLanguageManager] Ngôn ngữ hiện tại: {currentLanguage} ({GetNativeName(currentLanguage)})");
    }

    [ContextMenu("Test/Reset Saved Language")]
    public void ResetSavedLanguage()
    {
        PlayerPrefs.DeleteKey(LANGUAGE_KEY);
        PlayerPrefs.Save();
        Debug.Log("[MultiLanguageManager] Đã xóa ngôn ngữ đã lưu, lần mở sau sẽ chọn theo máy");
    }

    #endregion
}


/// <summary>
/// Font dùng cho 1 ngôn ngữ (VD Noto Sans Thai cho tiếng Thái).
/// </summary>
[Serializable]
public class LanguageFont
{
    public Language language;
    public Font font;
}