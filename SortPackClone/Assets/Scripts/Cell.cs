using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    [Header("Cell Settings")]
    [SerializeField] private int maxItems = 3;
    [SerializeField] private Transform itemContainer;

    [Header("Layer System")]
    [SerializeField] private int maxLayers = 3;  // Số tầng (mạng) của cell
    private int currentLayer;  // Tầng hiện tại (còn lại)

    [Header("Spot Settings")]
    [SerializeField] private Transform startSpot;  // Spot đầu tiên (trái)
    [SerializeField] private float spotSpacing = 0.6f;  // Khoảng cách giữa các spots
    [SerializeField] private bool arrangeHorizontal = true;  // Xếp ngang hay dọc

    [Header("Item Rotation")]
    [SerializeField] private bool tiltItems = true;
    [SerializeField] private float tiltAngleX = 15f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private Color validDropColor = Color.green;
    [SerializeField] private Color invalidDropColor = Color.red;

    // Spot system - mỗi spot có thể chứa 1 item hoặc null
    private Item[] spots;
    private Vector3[] spotPositions;

    // Data
    public int Row { get; set; }
    public int Column { get; set; }

    // Events
    public System.Action<Cell> OnCellFull;
    public System.Action<Cell> OnCellEmpty;
    public System.Action<Cell> OnCellSorted;
    public System.Action<Cell, Item> OnItemAdded;
    public System.Action<Cell> OnLayerUsed;      // Khi dùng hết 1 layer
    public System.Action<Cell> OnLayerDepleted;  // Khi hết tất cả layers

    void Awake()
    {
        // Khởi tạo layers
        currentLayer = maxLayers;
        // Tự động tìm ItemContainer
        if (itemContainer == null)
        {
            itemContainer = transform.Find("ItemContainer");
            if (itemContainer == null)
                itemContainer = FindChildRecursive(transform, "ItemContainer");
            if (itemContainer == null)
                itemContainer = transform;
        }

        // Tự động tìm StartSpot
        if (startSpot == null)
        {
            startSpot = FindChildRecursive(transform, "Spot");
            if (startSpot == null)
                startSpot = FindChildRecursive(transform, "StartSpot");
            if (startSpot == null)
                startSpot = FindChildRecursive(transform, "SpawnPoint");
        }

        if (highlightObject != null)
            highlightObject.SetActive(false);

        // Khởi tạo spots
        InitializeSpots();
    }

    private void InitializeSpots()
    {
        spots = new Item[maxItems];
        spotPositions = new Vector3[maxItems];

        Vector3 startPos = Vector3.zero;
        if (startSpot != null)
        {
            startPos = startSpot.localPosition;
        }

        // Tạo vị trí cho từng spot
        for (int i = 0; i < maxItems; i++)
        {
            Vector3 offset = Vector3.zero;
            if (arrangeHorizontal)
            {
                offset.x = i * spotSpacing;
            }
            else
            {
                offset.y = -i * spotSpacing;
            }

            spotPositions[i] = startPos + offset;
            spotPositions[i].z = -1f;  // Đưa ra phía trước cell
        }
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }

    // ========== SPOT SYSTEM ==========

    // Tìm spot gần nhất với vị trí drop
    public int GetNearestSpotIndex(Vector3 worldPosition)
    {
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < maxItems; i++)
        {
            // Chỉ xét spot trống
            if (spots[i] != null) continue;

            Vector3 spotWorldPos = itemContainer.TransformPoint(spotPositions[i]);
            float distance = Vector3.Distance(worldPosition, spotWorldPos);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    // Lấy vị trí world của spot
    public Vector3 GetSpotWorldPosition(int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return itemContainer.position;

        return itemContainer.TransformPoint(spotPositions[spotIndex]);
    }

    // Check spot có trống không
    public bool IsSpotEmpty(int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return false;

        return spots[spotIndex] == null;
    }

    // Đếm số spots trống
    public int GetEmptySpotCount()
    {
        int count = 0;
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] == null) count++;
        }
        return count;
    }

    // ========== ITEM MANAGEMENT ==========

    public bool CanAcceptItem(Item item)
    {
        // Check còn spot trống không
        return GetEmptySpotCount() > 0;
    }

    // Thêm item vào spot gần nhất
    public bool AddItem(Item item)
    {
        return AddItemAtPosition(item, item.transform.position);
    }

    // Thêm item vào spot gần vị trí drop nhất
    public bool AddItemAtPosition(Item item, Vector3 dropPosition)
    {
        int spotIndex = GetNearestSpotIndex(dropPosition);

        if (spotIndex < 0)
            return false;

        return AddItemToSpot(item, spotIndex);
    }

    // Thêm item vào spot cụ thể
    public bool AddItemToSpot(Item item, int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return false;

        if (spots[spotIndex] != null)
            return false;

        spots[spotIndex] = item;
        item.SetCell(this);
        item.SetSpotIndex(spotIndex);

        // Set parent
        if (itemContainer != null)
            item.transform.SetParent(itemContainer);
        else
            item.transform.SetParent(transform);

        // Đặt item vào vị trí spot
        PositionItemAtSpot(item, spotIndex);

        // Fire event
        OnItemAdded?.Invoke(this, item);

        // Check events
        if (GetEmptySpotCount() == 0)
        {
            OnCellFull?.Invoke(this);
        }

        CheckSorted();

        return true;
    }

    private void PositionItemAtSpot(Item item, int spotIndex)
    {
        Vector3 localPos = spotPositions[spotIndex];

        // Căn theo đáy nếu cần
        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float spriteHeight = sr.bounds.size.y;
            float pivotOffsetY = sr.bounds.center.y - item.transform.position.y;
            localPos.y += (spriteHeight / 2f) - pivotOffsetY;
        }

        item.transform.localPosition = localPos;

        // Xoay item
        if (tiltItems)
        {
            item.transform.localRotation = Quaternion.Euler(tiltAngleX, 0f, 0f);
        }
    }

    public bool RemoveItem(Item item)
    {
        // Tìm và xóa item khỏi spots
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] == item)
            {
                spots[i] = null;
                item.transform.SetParent(null);
                item.SetSpotIndex(-1);

                // KHÔNG trigger OnCellEmpty ở đây
                // Để GameManager/BoardController tự check sau

                return true;
            }
        }

        return false;
    }

    // Gọi function này khi cần check và trigger event
    public void CheckEmpty()
    {
        if (GetItemCount() == 0)
        {
            OnCellEmpty?.Invoke(this);
        }
    }

    public void ClearItems()
    {
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] != null)
            {
                Destroy(spots[i].gameObject);
                spots[i] = null;
            }
        }

        OnCellEmpty?.Invoke(this);
    }

    // ========== VISUAL FEEDBACK ==========

    public void SetHighlight(bool active, bool isValid = true)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(active);

            SpriteRenderer sr = highlightObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = isValid ? validDropColor : invalidDropColor;
            }
        }
    }

    // ========== SORTING CHECK ==========

    public bool IsSorted()
    {
        List<Item> items = GetItems();
        if (items.Count < 2)
            return true;

        string firstType = items[0].itemType;

        foreach (var item in items)
        {
            if (item.itemType != firstType)
                return false;
        }

        return true;
    }

    public bool IsFull()
    {
        return GetEmptySpotCount() == 0;
    }

    public bool IsFullAndSorted()
    {
        return IsFull() && IsSorted();
    }

    private void CheckSorted()
    {
        if (IsFullAndSorted())
        {
            OnCellSorted?.Invoke(this);
        }
    }

    // ========== GETTERS ==========

    public List<Item> GetItems()
    {
        List<Item> items = new List<Item>();
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] != null)
            {
                items.Add(spots[i]);
            }
        }
        return items;
    }

    public int GetItemCount()
    {
        int count = 0;
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] != null) count++;
        }
        return count;
    }

    public Item GetItemAtSpot(int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return null;
        return spots[spotIndex];
    }

    public Vector3 GetNextItemPosition()
    {
        // Tìm spot trống đầu tiên
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] == null)
            {
                return GetSpotWorldPosition(i);
            }
        }
        return itemContainer.position;
    }

    public string GetDominantItemType()
    {
        List<Item> items = GetItems();
        if (items.Count == 0)
            return null;

        Dictionary<string, int> typeCounts = new Dictionary<string, int>();

        foreach (var item in items)
        {
            if (!typeCounts.ContainsKey(item.itemType))
                typeCounts[item.itemType] = 0;
            typeCounts[item.itemType]++;
        }

        string dominant = null;
        int maxCount = 0;

        foreach (var kvp in typeCounts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                dominant = kvp.Key;
            }
        }

        return dominant;
    }

    // ========== DEBUG ==========

    // ========== LAYER SYSTEM ==========

    // Gọi khi merge thành công - giảm 1 layer
    public void UseLayer()
    {
        if (currentLayer <= 0) return;

        currentLayer--;
        OnLayerUsed?.Invoke(this);

        Debug.Log($"Cell {name}: Layer used. Remaining: {currentLayer}/{maxLayers}");

        if (currentLayer <= 0)
        {
            OnLayerDepleted?.Invoke(this);
            Debug.Log($"Cell {name}: All layers depleted!");
        }
    }

    // Check còn layer không
    public bool HasLayersRemaining()
    {
        return currentLayer > 0;
    }

    // Lấy số layer còn lại
    public int GetRemainingLayers()
    {
        return currentLayer;
    }

    // Lấy tổng số layers
    public int GetMaxLayers()
    {
        return maxLayers;
    }

    // Reset layers (nếu cần)
    public void ResetLayers()
    {
        currentLayer = maxLayers;
    }

    // ========== DEBUG SPOTS ==========

    [ContextMenu("Debug Spot Positions")]
    private void DebugSpotPositions()
    {
        Debug.Log($"=== Cell: {name} ===");
        for (int i = 0; i < maxItems; i++)
        {
            Vector3 worldPos = GetSpotWorldPosition(i);
            string itemName = spots[i] != null ? spots[i].name : "EMPTY";
            Debug.Log($"  Spot {i}: {worldPos} - {itemName}");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Vẽ spots trong Editor
        if (spotPositions == null || spotPositions.Length == 0)
        {
            // Preview khi chưa play
            Vector3 startPos = startSpot != null ? startSpot.localPosition : Vector3.zero;

            for (int i = 0; i < maxItems; i++)
            {
                Vector3 offset = arrangeHorizontal ? new Vector3(i * spotSpacing, 0, 0) : new Vector3(0, -i * spotSpacing, 0);
                Vector3 pos = transform.TransformPoint(startPos + offset);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pos, 0.1f);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(pos + Vector3.left * 0.15f, pos + Vector3.right * 0.15f);
                Gizmos.DrawLine(pos + Vector3.up * 0.15f, pos + Vector3.down * 0.15f);
            }
        }
        else
        {
            // Vẽ khi đang play
            for (int i = 0; i < maxItems; i++)
            {
                Vector3 pos = GetSpotWorldPosition(i);

                Gizmos.color = spots[i] != null ? Color.red : Color.green;
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
    }
}