using System;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    #region VARIABLES

    const int NO_SOUND = -1;
    const int DEFAULT_BGM_INDEX = 0;
    const int MEOW_SOUND_COUNT = 4;     // số tiếng mèo kêu ở cuối danh sách sounds

    [Tooltip("Danh sách âm thanh. Phần tử 0 mặc định là nhạc nền (BGM), các phần tử còn lại là SFX")]
    public Sound[] sounds;

    [Tooltip("Tự phát nhạc nền ở phần tử này khi vào game, -1 = không tự phát")]
    [SerializeField] int playBGMOnStart = DEFAULT_BGM_INDEX;

    bool isMusicOn = true;
    bool isSoundEffectOn = true;
    int currentBGMIndex = DEFAULT_BGM_INDEX;    // phần tử đang dùng làm nhạc nền

    #endregion


    #region PROPERTIES

    public static SoundManager Instance { get; private set; }

    // Giữ tên cũ để các script đang gọi SoundManager.soundManager không bị lỗi, code mới dùng Instance
    [Obsolete("Dùng SoundManager.Instance")]
    public static SoundManager soundManager => Instance;

    public bool IsMusicOn => isMusicOn;
    public bool IsSoundEffectOn => isSoundEffectOn;

    #endregion


    #region UNITY METHODS

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[SoundManager] Có nhiều hơn 1 SoundManager trong scene, xóa bản thừa");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CreateAudioSources();
    }

    private void Start()
    {
        SetOnOffSoundState();
        SetOnOffMusicState();

        if (playBGMOnStart != NO_SOUND)
            PlayBGM(playBGMOnStart);
    }

    #endregion


    #region SETUP METHODS

    void CreateAudioSources()
    {
        foreach (Sound sound in sounds)
        {
            sound.source = gameObject.AddComponent<AudioSource>();

            sound.source.playOnAwake = false;
            sound.source.clip = sound.clip;
            sound.source.volume = sound.volume;
            sound.source.pitch = sound.pitch;
            sound.source.loop = sound.loop;
        }
    }

    #endregion


    #region PLAY METHODS

    public void PlayBGM(int index)
    {
        if (!IsValidIndex(index))
            return;

        // Đổi bài thì dừng bài cũ, đang phát đúng bài này thì không phát lại từ đầu
        if (index != currentBGMIndex)
        {
            StopBGM();
            currentBGMIndex = index;
            ApplyVolumes();
        }

        AudioSource source = sounds[currentBGMIndex].source;
        if (!source.isPlaying)
            source.Play();
    }

    public void PlayBGM(string soundName)
    {
        PlayBGM(GetSoundIndex(soundName));
    }

    public void StopBGM()
    {
        if (IsValidIndex(currentBGMIndex))
            sounds[currentBGMIndex].source.Stop();
    }

    public void PlaySFX(int index)
    {
        if (!isSoundEffectOn || !IsValidIndex(index))
            return;

        AudioSource source = sounds[index].source;
        source.PlayOneShot(source.clip);
    }

    public void PlaySFX(string soundName)
    {
        PlaySFX(GetSoundIndex(soundName));
    }

    /// <summary>
    /// Phát ngẫu nhiên 1 trong các tiếng mèo kêu (MEOW_SOUND_COUNT phần tử cuối danh sách sounds).
    /// </summary>
    public void PlayRandomCatMeowSoundEffect()
    {
        if (sounds == null || sounds.Length == 0)
            return;

        // Danh sách ít hơn 4 phần tử thì lấy hết các phần tử có (trừ nhạc nền)
        int firstMeowIndex = Mathf.Max(0, sounds.Length - MEOW_SOUND_COUNT);
        if (firstMeowIndex == currentBGMIndex && sounds.Length > 1)
            firstMeowIndex++;

        // Ghi rõ UnityEngine.Random vì file có using System (trùng tên System.Random)
        PlaySFX(UnityEngine.Random.Range(firstMeowIndex, sounds.Length));
    }

    #endregion


    #region SETTING METHODS

    /// <summary>
    /// Đọc trạng thái SFX đã lưu và áp dụng (tắt = volume 0 cho mọi âm thanh trừ nhạc nền).
    /// </summary>
    public void SetOnOffSoundState()
    {
        isSoundEffectOn = DataManager.Instance.LoadSoundEffectStateData() == 1;
        ApplyVolumes();
    }

    /// <summary>
    /// Đọc trạng thái nhạc nền đã lưu và áp dụng.
    /// </summary>
    public void SetOnOffMusicState()
    {
        isMusicOn = DataManager.Instance.LoadMusicStateData() == 1;
        ApplyVolumes();
    }

    /// <summary>
    /// Bật / tắt SFX, lưu lại (dùng cho nút trong màn Setting).
    /// </summary>
    public void SetSoundEffectOn(bool isOn)
    {
        DataManager.Instance.SaveSoundEffectStateData(isOn ? 1 : 0);
        SetOnOffSoundState();
    }

    /// <summary>
    /// Bật / tắt nhạc nền, lưu lại (dùng cho nút trong màn Setting).
    /// </summary>
    public void SetMusicOn(bool isOn)
    {
        DataManager.Instance.SaveMusicStateData(isOn ? 1 : 0);
        SetOnOffMusicState();
    }

    /// <summary>
    /// Nhạc nền theo trạng thái music, các âm thanh còn lại theo trạng thái SFX.
    /// </summary>
    void ApplyVolumes()
    {
        for (int i = 0; i < sounds.Length; i++)
        {
            if (sounds[i].source == null)
                continue;

            bool isOn = i == currentBGMIndex ? isMusicOn : isSoundEffectOn;
            sounds[i].source.volume = isOn ? sounds[i].volume : 0f;
        }
    }

    #endregion


    #region HELPER METHODS

    bool IsValidIndex(int index)
    {
        if (sounds != null && index >= 0 && index < sounds.Length)
            return true;

        Debug.LogWarning($"[SoundManager] Không có âm thanh ở vị trí {index}");
        return false;
    }

    int GetSoundIndex(string soundName)
    {
        for (int i = 0; i < sounds.Length; i++)
        {
            if (sounds[i].name == soundName)
                return i;
        }

        Debug.LogWarning($"[SoundManager] Không tìm thấy âm thanh tên \"{soundName}\"");
        return NO_SOUND;
    }

    #endregion
}