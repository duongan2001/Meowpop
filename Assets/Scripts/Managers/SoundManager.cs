using System;
using UnityEngine;

/// <summary>
/// Nhóm âm thanh hiệu ứng (SFX), mỗi nhóm là 1 mảng riêng trong SoundManager.
/// </summary>
public enum SoundGroup
{
    UI,         // bấm nút, mở / đóng panel...
    Cat,        // mèo kêu
    Bubble,     // bóng vỡ, băng vỡ...
    PowerUp,    // dùng / nhận power-up
    Gameplay,   // thắng, thua, ổ hoàn thành, khay đầy...
}

/// <summary>
/// Tên các âm thanh code đang gọi. Điền ĐÚNG tên này vào trường name của Sound trong Inspector.
/// </summary>
public static class SoundNames
{
    // UI
    public const string TAP = "Tap";
    public const string CLAIM_COIN = "ClaimCoin";

    // Power-up
    public const string USE_POWER_UP = "UsePowerUp";
    public const string NEW_POWER_UP = "NewPowerUp";

    // Gameplay
    public const string WIN = "Win";
    public const string LOSE = "Lose";
    public const string NEW_CHALLENGE = "NewChallenge";
    public const string SPAWN_NEW_CATBED = "SpawnNewCatbed";
}

/// <summary>
/// Quản lý âm thanh: 1 nhạc nền (BGM) + các nhóm SFX riêng (UI, mèo, bóng, power-up, gameplay).
/// Phát theo tên: SoundManager.Instance.PlayUISound("Click"), PlayGameplaySound("Win")...
/// Nút trong Editor gán OnClick → PlayUISound và điền tên âm thanh.
/// </summary>
public class SoundManager : MonoBehaviour
{
    #region VARIABLES

    [Header("--- BGM ---")]
    [Tooltip("Nhạc nền (nên bật Loop)")]
    [SerializeField] Sound bgm;
    [Tooltip("Tự phát nhạc nền khi vào game")]
    [SerializeField] bool playBGMOnStart = true;

    [Header("--- SOUND EFFECTS ---")]
    [Tooltip("Âm thanh UI: bấm nút, mở / đóng panel, nhận xu...")]
    [SerializeField] Sound[] uiSounds;
    [Tooltip("Tiếng mèo kêu (PlayRandomCatMeowSoundEffect lấy ngẫu nhiên trong mảng này)")]
    [SerializeField] Sound[] catSounds;
    [Tooltip("Âm thanh bóng: bóng vỡ, băng vỡ...")]
    [SerializeField] Sound[] bubbleSounds;
    [Tooltip("Âm thanh power-up: dùng / nhận power-up...")]
    [SerializeField] Sound[] powerUpSounds;
    [Tooltip("Âm thanh gameplay: thắng, thua, ổ hoàn thành, khay đầy...")]
    [SerializeField] Sound[] gameplaySounds;

    [Header("--- BUTTON TAP ---")]
    [Tooltip("Tự thêm tiếng Tap cho mọi Button trong scene (trừ mèo), không cần gán OnClick từng nút")]
    [SerializeField] bool autoTapSoundOnButtons = true;

    bool isMusicOn = true;
    bool isSoundEffectOn = true;
    int lastCatSoundIndex = -1;     // tránh phát lại đúng tiếng mèo vừa phát

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

        if (playBGMOnStart)
            PlayBGM();

        if (autoTapSoundOnButtons)
            AddTapSoundToButtons();
    }

    #endregion


    #region SETUP METHODS

    void CreateAudioSources()
    {
        CreateAudioSource(bgm);

        foreach (SoundGroup group in Enum.GetValues(typeof(SoundGroup)))
        {
            Sound[] sounds = GetSounds(group);
            if (sounds == null)
                continue;

            foreach (Sound sound in sounds)
                CreateAudioSource(sound);
        }
    }

    void CreateAudioSource(Sound sound)
    {
        if (sound == null || sound.clip == null)
            return;

        sound.source = gameObject.AddComponent<AudioSource>();

        sound.source.playOnAwake = false;
        sound.source.clip = sound.clip;
        sound.source.volume = sound.volume;
        sound.source.pitch = sound.pitch;
        sound.source.loop = sound.loop;
    }

    #endregion


    #region BGM METHODS

    public void PlayBGM()
    {
        if (bgm == null || bgm.source == null)
            return;

        // Đang phát thì không phát lại từ đầu
        if (!bgm.source.isPlaying)
            bgm.source.Play();
    }

    public void StopBGM()
    {
        if (bgm != null && bgm.source != null)
            bgm.source.Stop();
    }

    #endregion


    #region SFX METHODS

    /// <summary>
    /// Phát SFX từ script khác, không cần kiểm tra SoundManager có trong scene hay không.
    /// </summary>
    public static void Play(SoundGroup group, string soundName)
    {
        if (Instance != null)
            Instance.PlaySFX(group, soundName);
    }

    public static void PlayCatMeow()
    {
        if (Instance != null)
            Instance.PlayRandomCatMeowSoundEffect();
    }

    // Phát theo tên trong từng nhóm (public để gán được vào OnClick trong Editor)

    public void PlayUISound(string soundName) => PlaySFX(SoundGroup.UI, soundName);
    public void PlayCatSound(string soundName) => PlaySFX(SoundGroup.Cat, soundName);
    public void PlayBubbleSound(string soundName) => PlaySFX(SoundGroup.Bubble, soundName);
    public void PlayPowerUpSound(string soundName) => PlaySFX(SoundGroup.PowerUp, soundName);
    public void PlayGameplaySound(string soundName) => PlaySFX(SoundGroup.Gameplay, soundName);

    public void PlaySFX(SoundGroup group, string soundName)
    {
        PlaySFX(group, GetSoundIndex(group, soundName));
    }

    public void PlaySFX(SoundGroup group, int index)
    {
        if (!isSoundEffectOn)
            return;

        Sound sound = GetSound(group, index);
        if (sound == null || sound.source == null)
            return;

        // Âm thanh lặp (VD tiếng nền của power-up) thì Play, còn lại PlayOneShot để chồng được nhiều lần
        if (sound.loop)
        {
            if (!sound.source.isPlaying)
                sound.source.Play();
        }
        else
            sound.source.PlayOneShot(sound.source.clip);
    }

    /// <summary>
    /// Phát ngẫu nhiên 1 âm thanh trong nhóm.
    /// </summary>
    public void PlayRandomSFX(SoundGroup group)
    {
        Sound[] sounds = GetSounds(group);
        if (sounds == null || sounds.Length == 0)
            return;

        PlaySFX(group, UnityEngine.Random.Range(0, sounds.Length));
    }

    /// <summary>
    /// Phát ngẫu nhiên 1 tiếng mèo kêu trong catSounds, không lặp lại tiếng vừa phát.
    /// </summary>
    public void PlayRandomCatMeowSoundEffect()
    {
        if (catSounds == null || catSounds.Length == 0)
            return;

        int index = UnityEngine.Random.Range(0, catSounds.Length);

        if (catSounds.Length > 1 && index == lastCatSoundIndex)
            index = (index + 1) % catSounds.Length;

        lastCatSoundIndex = index;
        PlaySFX(SoundGroup.Cat, index);
    }

    /// <summary>
    /// Dừng 1 âm thanh đang lặp (loop).
    /// </summary>
    public void StopSFX(SoundGroup group, string soundName)
    {
        Sound sound = GetSound(group, GetSoundIndex(group, soundName));
        if (sound != null && sound.source != null)
            sound.source.Stop();
    }

    #endregion


    #region SETTING METHODS

    /// <summary>
    /// Đọc trạng thái SFX đã lưu và áp dụng.
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
    /// Bật / tắt SFX, lưu lại (Panel - Settings gọi).
    /// </summary>
    public void SetSoundEffectOn(bool isOn)
    {
        DataManager.Instance.SaveSoundEffectStateData(isOn ? 1 : 0);
        SetOnOffSoundState();
    }

    /// <summary>
    /// Bật / tắt nhạc nền, lưu lại (Panel - Settings gọi).
    /// </summary>
    public void SetMusicOn(bool isOn)
    {
        DataManager.Instance.SaveMusicStateData(isOn ? 1 : 0);
        SetOnOffMusicState();
    }

    /// <summary>
    /// Nhạc nền theo trạng thái music, các nhóm SFX theo trạng thái sound (tắt = volume 0).
    /// </summary>
    void ApplyVolumes()
    {
        ApplyVolume(bgm, isMusicOn);

        foreach (SoundGroup group in Enum.GetValues(typeof(SoundGroup)))
        {
            Sound[] sounds = GetSounds(group);
            if (sounds == null)
                continue;

            foreach (Sound sound in sounds)
                ApplyVolume(sound, isSoundEffectOn);
        }
    }

    void ApplyVolume(Sound sound, bool isOn)
    {
        if (sound != null && sound.source != null)
            sound.source.volume = isOn ? sound.volume : 0f;
    }

    #endregion


    #region HELPER METHODS

    /// <summary>
    /// Thêm tiếng Tap vào OnClick của mọi Button có sẵn trong scene (kể cả panel đang tắt).
    /// Bỏ qua mèo (mèo có tiếng kêu riêng). Nút sinh ra sau lúc Start thì gán tay: OnClick → PlayUISound("Tap").
    /// </summary>
    void AddTapSoundToButtons()
    {
        UnityEngine.UI.Button[] buttons = FindObjectsOfType<UnityEngine.UI.Button>(true);

        foreach (UnityEngine.UI.Button button in buttons)
        {
            if (button.GetComponent<Cat>() != null)
                continue;

            button.onClick.AddListener(() => PlayUISound(SoundNames.TAP));
        }
    }

    Sound[] GetSounds(SoundGroup group)
    {
        switch (group)
        {
            case SoundGroup.UI: return uiSounds;
            case SoundGroup.Cat: return catSounds;
            case SoundGroup.Bubble: return bubbleSounds;
            case SoundGroup.PowerUp: return powerUpSounds;
            case SoundGroup.Gameplay: return gameplaySounds;
            default: return null;
        }
    }

    Sound GetSound(SoundGroup group, int index)
    {
        Sound[] sounds = GetSounds(group);

        if (sounds != null && index >= 0 && index < sounds.Length)
            return sounds[index];

        if (index >= 0)
            Debug.LogWarning($"[SoundManager] Nhóm {group} không có âm thanh ở vị trí {index}");

        return null;
    }

    int GetSoundIndex(SoundGroup group, string soundName)
    {
        Sound[] sounds = GetSounds(group);

        if (sounds != null)
        {
            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i] != null && sounds[i].name == soundName)
                    return i;
            }
        }

        Debug.LogWarning($"[SoundManager] Nhóm {group} không có âm thanh tên \"{soundName}\"");
        return -1;
    }

    #endregion
}