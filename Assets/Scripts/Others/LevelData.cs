using System;
using UnityEngine;

[Serializable]
public class LevelData
{
    #region VARIABLES

    // Hằng số theo luật game
    public const int CATS_PER_BED = 3;
    public const int MIN_CAT_IN_BUBBLE = 1;
    public const int MAX_CAT_IN_BUBBLE = 5;
    public const int FROZEN_BUBBLE_CAT_COUNT = 5;   // bóng đóng băng luôn chứa đủ 5 mèo

    [Tooltip("Số thứ tự level")]
    [Min(1)] public int level = 1;

    [Header("Cat")]
    [Tooltip("Tổng số lượng mèo trong level (phải chia hết cho 3)")]
    [Min(CATS_PER_BED)] public int totalCatCount = 9;

    [Tooltip("Tổng số lượng loại mèo trong level")]
    [Min(1)] public int catTypeCount = 3;

    [Header("Bubble")]
    [Tooltip("Số mèo tối thiểu trong 1 bóng")]
    [Range(MIN_CAT_IN_BUBBLE, MAX_CAT_IN_BUBBLE)] public int minCatInBubble = 1;

    [Tooltip("Số mèo tối đa trong 1 bóng")]
    [Range(MIN_CAT_IN_BUBBLE, MAX_CAT_IN_BUBBLE)] public int maxCatInBubble = 5;

    [Header("Obstacle")]
    [Tooltip("Số lượng bóng bị đóng băng (mỗi bóng luôn 5 mèo), vị trí ngẫu nhiên. Bỏ qua nếu đã điền frozenBubbleOrders")]
    [Min(0)] public int frozenBubbleCount;

    [Tooltip("Tự đặt thứ tự sinh của bóng đóng băng (bắt đầu từ 1, VD: 3, 10 = bóng thứ 3 và thứ 10). Để trống thì dùng frozenBubbleCount + vị trí ngẫu nhiên")]
    public int[] frozenBubbleOrders;

    [Tooltip("Số lượng bóng bị khóa (mỗi bóng khóa đi kèm 1 chìa khóa)")]
    [Min(0)] public int lockedBubbleCount;

    #endregion


    #region PROPERTIES

    // Có điền thứ tự sinh thì số bóng đóng băng = số phần tử, không thì dùng frozenBubbleCount
    public bool HasCustomFrozenOrders => frozenBubbleOrders != null && frozenBubbleOrders.Length > 0;
    public int FrozenBubbleTotal => HasCustomFrozenOrders ? frozenBubbleOrders.Length : frozenBubbleCount;

    // Mỗi bóng khóa có 1 chìa khóa tương ứng
    public int KeyCount => lockedBubbleCount;

    // Số bóng ít nhất / nhiều nhất có thể tạo ra từ tổng số mèo
    public int MinBubbleCount => Mathf.CeilToInt((float)totalCatCount / maxCatInBubble);
    public int MaxBubbleCount => totalCatCount / minCatInBubble;

    #endregion


    #region VALIDATE METHODS

    /// <summary>
    /// Kiểm tra dữ liệu level có hợp lệ với luật game không.
    /// </summary>
    public bool IsValid(out string error)
    {
        // Phần tử mới thêm vào mảng trong Inspector có thể bị toàn số 0
        if (totalCatCount <= 0 || catTypeCount <= 0)
        {
            error = $"Level {level}: tổng số mèo ({totalCatCount}) và số loại mèo ({catTypeCount}) phải lớn hơn 0";
            return false;
        }

        if (minCatInBubble < MIN_CAT_IN_BUBBLE || maxCatInBubble > MAX_CAT_IN_BUBBLE)
        {
            error = $"Level {level}: số mèo trong bóng phải trong khoảng {MIN_CAT_IN_BUBBLE}-{MAX_CAT_IN_BUBBLE} (đang là {minCatInBubble}-{maxCatInBubble})";
            return false;
        }

        if (totalCatCount % CATS_PER_BED != 0)
        {
            error = $"Level {level}: tổng số mèo ({totalCatCount}) phải chia hết cho {CATS_PER_BED}";
            return false;
        }

        if (catTypeCount * CATS_PER_BED > totalCatCount)
        {
            error = $"Level {level}: {catTypeCount} loại mèo cần ít nhất {catTypeCount * CATS_PER_BED} con, đang có {totalCatCount}";
            return false;
        }

        if (minCatInBubble > maxCatInBubble)
        {
            error = $"Level {level}: min mèo trong bóng ({minCatInBubble}) lớn hơn max ({maxCatInBubble})";
            return false;
        }

        if (FrozenBubbleTotal + lockedBubbleCount > MinBubbleCount)
        {
            error = $"Level {level}: số bóng đóng băng + bóng khóa ({FrozenBubbleTotal + lockedBubbleCount}) có thể vượt quá số bóng tạo ra (tối thiểu {MinBubbleCount})";
            return false;
        }

        if (HasCustomFrozenOrders)
        {
            foreach (int order in frozenBubbleOrders)
            {
                if (order < 1)
                {
                    error = $"Level {level}: thứ tự sinh bóng đóng băng phải từ 1 trở lên (đang có {order})";
                    return false;
                }
            }

            if (new System.Collections.Generic.HashSet<int>(frozenBubbleOrders).Count != frozenBubbleOrders.Length)
            {
                error = $"Level {level}: frozenBubbleOrders bị trùng thứ tự";
                return false;
            }
        }

        // Bóng đóng băng lấy trước 5 mèo / bóng, phần còn lại vẫn phải chia được theo min-max
        int restCatCount = totalCatCount - FrozenBubbleTotal * FROZEN_BUBBLE_CAT_COUNT;
        if (restCatCount < 0)
        {
            error = $"Level {level}: {FrozenBubbleTotal} bóng đóng băng cần {FrozenBubbleTotal * FROZEN_BUBBLE_CAT_COUNT} mèo, level chỉ có {totalCatCount}";
            return false;
        }

        if (restCatCount > 0 && Mathf.CeilToInt((float)restCatCount / maxCatInBubble) * minCatInBubble > restCatCount)
        {
            error = $"Level {level}: sau khi lấy mèo cho bóng đóng băng còn {restCatCount} mèo, không chia được thành bóng {minCatInBubble}-{maxCatInBubble} mèo";
            return false;
        }

        error = null;
        return true;
    }

    #endregion
}