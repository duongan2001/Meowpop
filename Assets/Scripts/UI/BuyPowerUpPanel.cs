using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel mua power-up: mở khi bấm nút power-up đã hết lượt.
/// Mua bằng xu hoặc nhận qua quảng cáo (Claim). Gán OnClick các nút trong Editor.
/// </summary>
public class BuyPowerUpPanel : PanelBaseUIController
{
    #region VARIABLES

    [Header("--- POWER-UP UI ---")]
    [Tooltip("Power-up Name")]
    [SerializeField] Text powerUpNameText;
    [Tooltip("Power-up Icon")]
    [SerializeField] Image powerUpIcon;
    [Tooltip("Text \"+1\" trên icon - số lượt nhận được")]
    [SerializeField] Text amountText;
    [Tooltip("Power-up Des")]
    [SerializeField] Text powerUpDescriptionText;

    [Header("--- BUTTONS ---")]
    [Tooltip("Nút mua bằng xu")]
    [SerializeField] Button coinButton;
    [Tooltip("Text giá trên nút xu (VD: 1000)")]
    [SerializeField] Text priceText;
    [Tooltip("Nút Claim (xem quảng cáo)")]
    [SerializeField] Button claimButton;

    [Header("--- VFX ---")]
    [SerializeField] ParticleSystem sparkleVfx;

    PowerUpType currentType;
    bool hasPowerUp;

    #endregion


    #region PROPERTIES

    public PowerUpType CurrentType => currentType;

    #endregion


    #region UNITY METHODS

    protected override void OnDisable()
    {
        base.OnDisable();
        StopVfx();
    }

    #endregion


    #region PANEL METHODS

    /// <summary>
    /// PowerUpManager gọi: load thông tin power-up cần mua rồi hiện panel.
    /// </summary>
    public void ShowBuyPowerUp(PowerUpType type)
    {
        PowerUpData data = PowerUpManager.Instance != null ? PowerUpManager.Instance.GetPowerUpData(type) : null;

        if (data == null)
        {
            Debug.LogWarning($"[BuyPowerUpPanel] Chưa cấu hình PowerUpData cho {type}");
            return;
        }

        currentType = type;
        hasPowerUp = true;

        if (powerUpNameText != null)
            powerUpNameText.text = MultiLanguageManager.Translate($"powerup.{type}.title", data.title);

        // Không có icon thì giữ ảnh mặc định đang đặt sẵn trong panel
        if (powerUpIcon != null && data.icon != null)
            powerUpIcon.sprite = data.icon;

        if (amountText != null)
            amountText.text = $"+{data.buyAmount}";

        if (powerUpDescriptionText != null)
            powerUpDescriptionText.text = MultiLanguageManager.Translate($"powerup.{type}.description", data.description);

        if (priceText != null)
            priceText.text = data.coinPrice.ToString();

        SetButtonsInteractable(true);
        ShowDelay();
    }

    public override void Show()
    {
        base.Show();
        PlayVfx();
    }

    public override void Hide()
    {
        hasPowerUp = false;
        SetButtonsInteractable(false);
        StopVfx();
        base.Hide();
    }

    #endregion


    #region BUTTON METHODS

    /// <summary>
    /// Gán vào OnClick của nút xu: đủ xu thì mua, đóng panel rồi icon rơi vào nút power-up.
    /// </summary>
    public void OnBuyWithCoinButtonClick()
    {
        if (!hasPowerUp || PowerUpManager.Instance == null)
            return;

        if (!PowerUpManager.Instance.CanAfford(currentType))
        {
            GameManager.Instance.OpenBuyCoinPanel();
            return;
        }

        PowerUpType type = currentType;

        // Đóng panel trước để thấy hiệu ứng icon rơi vào nút
        Hide();
        PowerUpManager.Instance.TryBuyPowerUp(type);
    }

    /// <summary>
    /// Gán vào OnClick của nút Claim: xem quảng cáo xong nhận lượt power-up.
    /// </summary>
    public void OnClaimButtonClick()
    {
        if (!hasPowerUp || PowerUpManager.Instance == null)
            return;

        PowerUpType type = currentType;
        PowerUpData data = PowerUpManager.Instance.GetPowerUpData(type);

        // TODO: hiện rewarded ad, xem xong mới chạy đoạn dưới
        Hide();
        PowerUpManager.Instance.AddPowerUp(type, data != null ? data.buyAmount : 1, true);
    }

    /// <summary>
    /// Gán vào OnClick của nút X (đóng).
    /// </summary>
    public void OnCloseButtonClick()
    {
        Hide();
    }

    #endregion


    #region VFX METHODS

    void PlayVfx()
    {
        if (sparkleVfx == null)
            return;

        sparkleVfx.gameObject.SetActive(true);
        sparkleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparkleVfx.Play(true);
    }

    void StopVfx()
    {
        if (sparkleVfx == null)
            return;

        sparkleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion


    #region HELPER METHODS

    // Khóa nút khi đang đóng panel, tránh bấm mua 2 lần
    void SetButtonsInteractable(bool interactable)
    {
        if (coinButton != null)
            coinButton.interactable = interactable;

        if (claimButton != null)
            claimButton.interactable = interactable;
    }

    #endregion
}