using System;
using UnityEngine;

/// <summary>
/// Các loại power-up.
/// Lưu ý: tên enum được dùng làm key PlayerPrefs (số lượng, đã tặng free), đổi tên sẽ mất dữ liệu đã lưu.
/// </summary>
public enum PowerUpType
{
    FillBed,        // tự làm đầy 1 ổ
    ClearTray,      // dọn khay: chuyển mèo trong khay vào ô số 6
    ShuffleCats,    // trộn mèo trong các bóng đang hiển thị
}

[Serializable]
public class PowerUpData
{
    #region VARIABLES

    public PowerUpType type;

    [Tooltip("Tới level này thì mở khóa power-up")]
    [Min(1)] public int unlockLevel = 3;

    [Tooltip("Số lượt được tặng khi mở khóa")]
    [Min(0)] public int freeAmountOnUnlock = 3;

    [Header("Mua bằng xu")]
    [Min(0)] public int coinPrice = 100;
    [Tooltip("Số lượt nhận được mỗi lần mua")]
    [Min(1)] public int buyAmount = 1;

    [Header("Hiển thị")]
    public string title;
    [TextArea(2, 4)] public string description;
    [Tooltip("Icon power-up, dùng cho hiệu ứng rơi vào nút")]
    public Sprite icon;

    #endregion
}