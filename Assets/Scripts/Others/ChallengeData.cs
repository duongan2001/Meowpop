using System;
using UnityEngine;

/// <summary>
/// Các loại vật cản / thử thách trong game.
/// Lưu ý: tên enum được dùng làm key PlayerPrefs, đổi tên sẽ làm popup giới thiệu hiện lại.
/// </summary>
public enum ChallengeType
{
    FrozenBubble,   // bóng đóng băng
    LockedBubble,   // bóng khóa + chìa khóa
}

[Serializable]
public class ChallengeData
{
    #region VARIABLES

    public ChallengeType type;

    [Tooltip("Tới level này thì mở khóa thử thách")]
    [Min(1)] public int unlockLevel = 1;

    [Header("Popup giới thiệu")]
    public string title;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    #endregion
}