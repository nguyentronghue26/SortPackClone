using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SortPackLevelData",
    menuName = "SortPack/Level Data"
)]
public class SortPackLevelData : ScriptableObject
{
    [Header("Level Info")]
    [Tooltip("Số màn (1,2,3,...)")]
    public int levelIndex = 1;

    [TextArea]
    [Tooltip("Ghi chú thêm cho level này")]
    public string description;

    [Header("Board Size")]
    [Tooltip("X = số hàng dọc (trên xuống dưới)")]
    public int sizeX = 4;

    [Tooltip("Y = số cột ngang (trái sang phải)")]
    public int sizeY = 4;

    [Tooltip("Z = số tầng (layer). Tầng 1 = z=0 = dưới cùng")]
    public int sizeZ = 1;

    [Header("Slots per Cell")]
    [Tooltip("Số slot trên mỗi ô (mặc định 3)")]
    public int slotsPerCell = 3;

    [Header("Match Settings")]
    [Tooltip("Số items cùng loại để match (mặc định 3)")]
    public int itemsPerMatch = 3;

    [Header("Item Statistic (Auto-calculated)")]
    [Tooltip("Tổng số lượng mỗi item ID. Bấm Recalculate trong Editor để cập nhật.")]
    public List<ItemCountInfo> itemCounts = new List<ItemCountInfo>();

    [Header("Item Placement")]
    [Tooltip("Danh sách item: mỗi dòng là 1 item đặt vào 1 ô + slot")]
    public List<CellSlotItemData> placements = new List<CellSlotItemData>();

    /// <summary>
    /// Đếm số lượng item theo ID
    /// </summary>
    public Dictionary<int, int> GetItemCountsByID()
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();

        if (placements == null) return counts;

        foreach (var p in placements)
        {
            if (p == null) continue;

            if (!counts.ContainsKey(p.itemID))
                counts[p.itemID] = 0;

            counts[p.itemID]++;
        }

        return counts;
    }

    /// <summary>
    /// Kiểm tra xem có item nào vượt quá itemsPerMatch không
    /// </summary>
    public List<int> GetInvalidItemIDs()
    {
        List<int> invalidIDs = new List<int>();
        var counts = GetItemCountsByID();

        foreach (var kvp in counts)
        {
            // Item phải là bội số của itemsPerMatch (3, 6, 9, ...)
            if (kvp.Value % itemsPerMatch != 0)
            {
                invalidIDs.Add(kvp.Key);
            }
        }

        return invalidIDs;
    }

    /// <summary>
    /// Kiểm tra level có hợp lệ không
    /// </summary>
    public bool IsValid()
    {
        return GetInvalidItemIDs().Count == 0;
    }
}

[System.Serializable]
public class ItemCountInfo
{
    [Tooltip("ID của item")]
    public int itemID;

    [Tooltip("Tổng số lượng item này trong level")]
    public int totalQuantity;

    [Tooltip("Có hợp lệ không (phải là bội số của 3)")]
    public bool isValid;
}

[System.Serializable]
public class CellSlotItemData
{
    [Header("Grid Position (X,Y,Z)")]
    [Tooltip("X = hàng dọc (0..sizeX-1)")]
    public int x;

    [Tooltip("Y = cột ngang (0..sizeY-1)")]
    public int y;

    [Tooltip("Z = tầng (0..sizeZ-1). 0 = tầng 1 (dưới cùng)")]
    public int z;

    [Header("Slot in this Cell")]
    [Tooltip("Slot index trong ô (0,1,2). 0 = trái, 1 = giữa, 2 = phải")]
    [Range(0, 2)]
    public int slotIndex;

    [Header("Item Data")]
    [Tooltip("Item ID đặt ở slot này (-1 = trống)")]
    public int itemID = -1;
}