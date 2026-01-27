using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    [Header("Cell Settings")]
    [SerializeField] private int maxItems = 3;
    [SerializeField] private Transform itemContainer;

    [Header("Spawn Settings")]
    [SerializeField] private Transform startSpot;  // Chỉ cần 1 spot làm điểm bắt đầu
    [SerializeField] private float itemSpacing = 1.0f;  // Khoảng cách giữa các item
    [SerializeField] private bool arrangeHorizontal = true;  // Xếp ngang hay dọc

    [Header("Item Rotation")]
    [SerializeField] private bool tiltItems = true;  // Nghiêng items
    [SerializeField] private float tiltAngleX = 15f;  // Góc nghiêng theo trục X (nghiêng vào trong)



    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlightObject;  // Object highlight khi hover
    [SerializeField] private Color validDropColor = Color.green;
    [SerializeField] private Color invalidDropColor = Color.red;

    // Data
    private List<Item> items = new List<Item>();
    public int Row { get; set; }
    public int Column { get; set; }

    // Events
    public System.Action<Cell> OnCellFull;
    public System.Action<Cell> OnCellEmpty;
    public System.Action<Cell> OnCellSorted;
    public System.Action<Cell, Item> OnItemAdded;  // Khi item được thêm vào cell

    void Awake()
    {
        // Tự động tìm ItemContainer
        if (itemContainer == null)
        {
            itemContainer = transform.Find("ItemContainer");
            if (itemContainer == null)
                itemContainer = FindChildRecursive(transform, "ItemContainer");
            if (itemContainer == null)
                itemContainer = transform;
        }

        // Tự động tìm StartSpot nếu chưa assign
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

    // ========== ITEM MANAGEMENT ==========

    public bool CanAcceptItem(Item item)
    {
        // Check còn chỗ không
        if (items.Count >= maxItems)
            return false;

        // Có thể thêm logic khác:
        // - Chỉ nhận item cùng loại
        // - Chỉ nhận khi cell trống
        // etc.

        return true;
    }

    public bool AddItem(Item item)
    {
        if (!CanAcceptItem(item))
            return false;

        items.Add(item);
        item.SetCell(this);

        // Set parent
        if (itemContainer != null)
            item.transform.SetParent(itemContainer);
        else
            item.transform.SetParent(transform);

        // Sắp xếp lại vị trí các items
        ArrangeItems();

        // Fire event - QUAN TRỌNG cho match check
        OnItemAdded?.Invoke(this, item);

        // Check events
        if (items.Count >= maxItems)
        {
            OnCellFull?.Invoke(this);
        }

        CheckSorted();

        return true;
    }

    public bool RemoveItem(Item item)
    {
        if (!items.Contains(item))
            return false;

        bool wasFull = items.Count >= maxItems;

        items.Remove(item);
        item.transform.SetParent(null);

        // Sắp xếp lại
        ArrangeItems();

        // Check events
        if (items.Count == 0)
        {
            OnCellEmpty?.Invoke(this);
        }

        return true;
    }

    public void ClearItems()
    {
        foreach (var item in items)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
     
        items.Clear();
        OnCellEmpty?.Invoke(this);

    }

    // ========== POSITIONING ==========

    [Header("Alignment")]
    [SerializeField] private bool alignToBottom = true;  // Căn theo đáy item

    private void ArrangeItems()
    {
        for (int i = 0; i < items.Count; i++)
        {
            Vector3 localPos = GetItemLocalPosition(i);

            if (alignToBottom)
            {
                SpriteRenderer sr = items[i].GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    float spriteHeight = sr.bounds.size.y;
                    float pivotOffsetY = sr.bounds.center.y - items[i].transform.position.y;
                    localPos.y += (spriteHeight / 2f) - pivotOffsetY;
                }
            }

            items[i].transform.localPosition = localPos;

            // Xoay item nghiêng tựa vào thành hộp
            if (tiltItems)
            {
                items[i].transform.localRotation = Quaternion.Euler(tiltAngleX, 0f, 0f);
            }
        }
    }

    private Vector3 GetItemLocalPosition(int index)
    {
        Vector3 startPos = Vector3.zero;

        // Lấy vị trí từ startSpot nếu có
        if (startSpot != null)
        {
            startPos = startSpot.localPosition;
        }

        // Tính offset dựa vào index
        Vector3 offset = Vector3.zero;
        if (arrangeHorizontal)
        {
            offset.x = index * itemSpacing;
        }
        else
        {
            offset.y = -index * itemSpacing;
        }

        // Đảm bảo Z ở phía trước cell (âm hơn để không bị cell che)
        Vector3 result = startPos + offset;
        result.z = -1f;  // Đưa items ra phía trước cell

        return result;
    }

    public Vector3 GetNextItemPosition()
    {
        // Trả về world position cho item tiếp theo
        // Bắt đầu từ vị trí của ItemContainer + offset
        Vector3 localPos = GetItemLocalPosition(items.Count);

        // Debug
        Vector3 worldPos = itemContainer.TransformPoint(localPos);
        Debug.Log($"Cell {name}: NextItemPos = {worldPos}, ItemContainer pos = {itemContainer.position}");

        return worldPos;
    }

    // Gọi function này từ Inspector để test vị trí
    [ContextMenu("Debug Item Positions")]
    private void DebugItemPositions()
    {
        Debug.Log($"=== Cell: {name} ===");
        Debug.Log($"ItemContainer: {(itemContainer != null ? itemContainer.name : "NULL")}");
        Debug.Log($"ItemContainer Position: {(itemContainer != null ? itemContainer.position.ToString() : "N/A")}");
        Debug.Log($"Start Spot: {(startSpot != null ? startSpot.position.ToString() : "NULL")}");
        Debug.Log($"Item Spacing: {itemSpacing}");

        for (int i = 0; i < maxItems; i++)
        {
            Vector3 pos = itemContainer.TransformPoint(GetItemLocalPosition(i));
            Debug.Log($"  Item {i} position: {pos}");
        }
    }

    // ========== VISUAL FEEDBACK ==========

    public void SetHighlight(bool active, bool isValid = true)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(active);

            // Đổi màu theo valid/invalid
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
        return items.Count >= maxItems;
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
        return new List<Item>(items);
    }

    public int GetItemCount()
    {
        return items.Count;
    }

    public string GetDominantItemType()
    {
        if (items.Count == 0)
            return null;

        // Đếm số lượng mỗi loại
        Dictionary<string, int> typeCounts = new Dictionary<string, int>();

        foreach (var item in items)
        {
            if (!typeCounts.ContainsKey(item.itemType))
                typeCounts[item.itemType] = 0;
            typeCounts[item.itemType]++;
        }

        // Tìm loại nhiều nhất
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
}