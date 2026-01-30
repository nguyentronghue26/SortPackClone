using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SortPackLevelList",
    menuName = "SortPack/Level List"
)]
public class SortPackLevelList : ScriptableObject
{
    [Tooltip("Danh sách tất cả level của game SortPack")]
    public List<SortPackLevelData> levels = new List<SortPackLevelData>();

    /// <summary>
    /// Lấy level theo index trong list (0-based).
    /// </summary>
    public SortPackLevelData GetLevelByIndex(int index)
    {
        if (levels == null || levels.Count == 0) return null;
        if (index < 0 || index >= levels.Count) return null;
        return levels[index];
    }

    /// <summary>
    /// Lấy level theo levelIndex (1,2,3,...).
    /// </summary>
    public SortPackLevelData GetLevelByLevelNumber(int levelNumber)
    {
        if (levels == null) return null;

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].levelIndex == levelNumber)
                return levels[i];
        }
        return null;
    }

    /// <summary>
    /// Tổng số level.
    /// </summary>
    public int GetTotalLevels()
    {
        return levels != null ? levels.Count : 0;
    }
}
