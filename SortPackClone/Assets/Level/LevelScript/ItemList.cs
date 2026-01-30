using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemList",
    menuName = "SortPack/Item List"
)]
public class ItemList : ScriptableObject
{
    [Tooltip("Danh sách tất cả item prefab (mỗi prefab phải có component Item chứa itemID)")]
    public List<GameObject> itemPrefabs = new List<GameObject>();

    private Dictionary<int, GameObject> idToPrefab;

    private void OnEnable()
    {
        BuildDictionary();
    }

    /// <summary>
    /// Tạo dictionary map itemID -> prefab (để lookup nhanh).
    /// </summary>
    public void BuildDictionary()
    {
        idToPrefab = new Dictionary<int, GameObject>();

        foreach (var prefab in itemPrefabs)
        {
            if (prefab == null) continue;

            Item item = prefab.GetComponent<Item>();
            if (item == null)
            {
                Debug.LogWarning($"Prefab {prefab.name} không có component Item!");
                continue;
            }

            if (idToPrefab.ContainsKey(item.itemID))
            {
                Debug.LogWarning($"Trùng itemID {item.itemID} trong ItemList!");
                continue;
            }

            idToPrefab.Add(item.itemID, prefab);
        }
    }

    /// <summary>
    /// Lấy prefab item theo itemID.
    /// </summary>
    public GameObject GetPrefab(int itemID)
    {
        if (idToPrefab == null || idToPrefab.Count == 0)
            BuildDictionary();

        if (idToPrefab.TryGetValue(itemID, out GameObject prefab))
            return prefab;

        Debug.LogWarning($"Không tìm thấy prefab với itemID {itemID}");
        return null;
    }
}
