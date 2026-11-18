using System;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    #region VARIABLES

    // Key PlayerPrefs (giữ nguyên key cũ để không mất dữ liệu đã lưu)
    const string MUSIC_STATE_KEY = "musicState";
    const string SOUND_EFFECT_STATE_KEY = "soundEffectState";
    const string VIBRATION_STATE_KEY = "vibration";
    const string COIN_KEY = "coin";

    // Giá trị mặc định khi chưa có dữ liệu (lần đầu mở game)
    const int DEFAULT_MUSIC_STATE = 1;
    const int DEFAULT_SOUND_EFFECT_STATE = 1;
    const int DEFAULT_VIBRATION_STATE = 0;
    const int DEFAULT_COIN = 0;

    #endregion


    #region PROPERTIES

    public static DataManager Instance { get; private set; }

    // Giữ tên cũ để các script đang gọi DataManager.dataManager không bị lỗi, code mới dùng Instance
    [Obsolete("Dùng DataManager.Instance")]
    public static DataManager dataManager => Instance;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[DataManager] Có nhiều hơn 1 DataManager trong scene, xóa bản thừa");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion


    #region SETTING METHODS

    // 1 = bật, 0 = tắt

    public void SaveMusicStateData(int state)
    {
        SaveInt(MUSIC_STATE_KEY, state);
    }

    public int LoadMusicStateData()
    {
        return PlayerPrefs.GetInt(MUSIC_STATE_KEY, DEFAULT_MUSIC_STATE);
    }

    public void SaveSoundEffectStateData(int state)
    {
        SaveInt(SOUND_EFFECT_STATE_KEY, state);
    }

    public int LoadSoundEffectStateData()
    {
        return PlayerPrefs.GetInt(SOUND_EFFECT_STATE_KEY, DEFAULT_SOUND_EFFECT_STATE);
    }

    public void SaveVibrationStateData(int state)
    {
        SaveInt(VIBRATION_STATE_KEY, state);
    }

    public int LoadVibrationStateData()
    {
        return PlayerPrefs.GetInt(VIBRATION_STATE_KEY, DEFAULT_VIBRATION_STATE);
    }

    #endregion


    #region COIN METHODS

    public void SaveCoinData(int amount)
    {
        SaveInt(COIN_KEY, Mathf.Max(0, amount));
    }

    public int LoadCoinData()
    {
        return PlayerPrefs.GetInt(COIN_KEY, DEFAULT_COIN);
    }

    #endregion


    #region SAVE METHODS

    void SaveInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);

        // Ghi xuống ổ đĩa ngay, tránh mất dữ liệu khi game bị tắt đột ngột trên mobile
        PlayerPrefs.Save();
    }

    #endregion
}