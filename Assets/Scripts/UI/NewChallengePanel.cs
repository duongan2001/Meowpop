using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel giới thiệu thử thách mới, kế thừa fade / delay của PanelBaseUIController.
/// </summary>
public class NewChallengePanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- CHALLENGE UI ---")]
    [SerializeField] Text challengeNameText;
    [SerializeField] Image challengeImage;
    [SerializeField] Text challengeDescriptionText;

    [Header("--- VFX ---")]
    [SerializeField] ParticleSystem sparkleVfx;

    ChallengeData currentChallenge;

    #endregion


    #region UNITY METHODS

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopVfx();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// ChallengeManager gọi: đổ dữ liệu thử thách vào UI rồi hiện panel.
    /// </summary>
    public void ShowChallenge(ChallengeData challenge)
    {
        currentChallenge = challenge;

        if (challengeNameText != null)
            challengeNameText.text = MultiLanguageManager.Translate($"challenge.{challenge.type}.title", challenge.title);

        // Không có icon thì giữ ảnh mặc định đang đặt sẵn trong panel
        if (challengeImage != null && challenge.icon != null)
            challengeImage.sprite = challenge.icon;

        if (challengeDescriptionText != null)
            challengeDescriptionText.text = MultiLanguageManager.Translate($"challenge.{challenge.type}.description", challenge.description);

        ShowDelay();
    }

    /// <summary>
    /// Bật VFX khi panel thật sự hiện ra (sau thời gian delay của ShowDelay).
    /// </summary>
    public override void Show()
    {
        base.Show();
        PlayVfx();
    }

    /// <summary>
    /// Tắt VFX ngay khi bắt đầu ẩn panel.
    /// </summary>
    public override void Hide()
    {
        StopVfx();
        base.Hide();
    }

    /// <summary>
    /// Nút Continue: đóng panel và báo ChallengeManager.
    /// Gán vào OnClick của nút trong Editor (phải để public mới chọn được).
    /// </summary>
    public void OnContinueButtonClick()
    {
        // Chặn bấm liên tiếp làm báo đóng 2 lần
        if (currentChallenge == null)
            return;

        ChallengeData closedChallenge = currentChallenge;
        currentChallenge = null;

        Hide();

        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.OnChallengeIntroClosed(closedChallenge);
    }

    #endregion


    #region VFX METHODS

    void PlayVfx()
    {
        if (sparkleVfx == null)
            return;

        // Chạy lại từ đầu mỗi lần panel hiện
        sparkleVfx.gameObject.SetActive(true);
        sparkleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparkleVfx.Play(true);
    }

    void StopVfx()
    {
        if (sparkleVfx == null)
            return;

        // Xóa hạt ngay: particle không mờ dần theo CanvasGroup nên để lại sẽ còn lơ lửng khi panel đã ẩn
        sparkleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion
}