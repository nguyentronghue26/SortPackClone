using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    [Header("Cell Settings")]
    [SerializeField] private int maxItems = 3;
    [SerializeField] private Transform itemContainer;

    [Header("Layer System")]
    [SerializeField] private int maxLayers = 3;
    private int currentLayer;

    [Header("Spot Settings")]
    [SerializeField] private Transform startSpot;
    [SerializeField] private float spotSpacing = 0.6f;
    [SerializeField] private bool arrangeHorizontal = true;

    [Header("Item Rotation")]
    [SerializeField] private bool tiltItems = true;
    [SerializeField] private float tiltAngleX = 15f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private Color validDropColor = Color.green;
    [SerializeField] private Color invalidDropColor = Color.red;

    // Spot system
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
    public System.Action<Cell> OnLayerUsed;
    public System.Action<Cell> OnLayerDepleted;

    void Awake()
    {
        currentLayer = maxLayers;

        if (itemContainer == null)
        {
            itemContainer = transform.Find("ItemContainer");
            if (itemContainer == null)
                itemContainer = FindChildRecursive(transform, "ItemContainer");
            if (itemContainer == null)
                itemContainer = transform;
        }

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
            spotPositions[i].z = -1f;
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

    public int GetNearestSpotIndex(Vector3 worldPosition)
    {
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < maxItems; i++)
        {
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

    public Vector3 GetSpotWorldPosition(int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return itemContainer.position;

        return itemContainer.TransformPoint(spotPositions[spotIndex]);
    }

    public bool IsSpotEmpty(int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return false;

        return spots[spotIndex] == null;
    }

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
        return GetEmptySpotCount() > 0;
    }

    public bool AddItem(Item item)
    {
        return AddItemAtPosition(item, item.transform.position);
    }

    public bool AddItemAtPosition(Item item, Vector3 dropPosition)
    {
        int spotIndex = GetNearestSpotIndex(dropPosition);

        if (spotIndex < 0)
            return false;

        return AddItemToSpot(item, spotIndex);
    }

    public bool AddItemToSpot(Item item, int spotIndex)
    {
        if (spotIndex < 0 || spotIndex >= maxItems)
            return false;

        if (spots[spotIndex] != null)
            return false;

        spots[spotIndex] = item;
        item.SetCell(this);
        item.SetSpotIndex(spotIndex);

        if (itemContainer != null)
            item.transform.SetParent(itemContainer);
        else
            item.transform.SetParent(transform);

        PositionItemAtSpot(item, spotIndex);

        OnItemAdded?.Invoke(this, item);

        if (GetEmptySpotCount() == 0)
        {
            OnCellFull?.Invoke(this);
        }

        return true;
    }

    private void PositionItemAtSpot(Item item, int spotIndex)
    {
        Vector3 localPos = spotPositions[spotIndex];

        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float spriteHeight = sr.bounds.size.y;
            float pivotOffsetY = sr.bounds.center.y - item.transform.position.y;
            localPos.y += (spriteHeight / 2f) - pivotOffsetY;
        }

        item.transform.localPosition = localPos;

        if (tiltItems)
        {
            item.transform.localRotation = Quaternion.Euler(tiltAngleX, 0f, 0f);
        }
    }

    /// <summary>
    /// Remove item khi DRAG (nhấc lên) - KHÔNG gọi OnCellEmpty
    /// </summary>
    public bool RemoveItem(Item item)
    {
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] == item)
            {
                spots[i] = null;
                item.transform.SetParent(null);
                item.SetSpotIndex(-1);

                // KHÔNG gọi OnCellEmpty ở đây!
                // Sẽ gọi sau khi item được DROP thành công vào cell khác

                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gọi sau khi item đã DROP THÀNH CÔNG vào cell khác
    /// Kiểm tra nếu cell trống thì mới trigger OnCellEmpty
    /// </summary>
    public void NotifyItemMovedToOtherCell()
    {
        if (GetItemCount() == 0)
        {
            Debug.Log($"[Cell] {name} is now empty after item moved to other cell");
            OnCellEmpty?.Invoke(this);
        }
    }

    /// <summary>
    /// Gọi function này khi cần check và trigger event (dùng cho các trường hợp khác)
    /// </summary>
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

    public void CheckSorted()
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

    // ========== LAYER SYSTEM ==========

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

    public bool HasLayersRemaining()
    {
        return currentLayer > 0;
    }

    public int GetRemainingLayers()
    {
        return currentLayer;
    }

    public int GetMaxLayers()
    {
        return maxLayers;
    }

    public void ResetLayers()
    {
        currentLayer = maxLayers;
    }

    // ========== PUBLIC GETTERS/SETTERS ==========

    public int GetMaxItems() => maxItems;

    public void SetSpotSpacing(float spacing)
    {
        spotSpacing = spacing;
        InitializeSpots();
    }

    public float GetSpotSpacing() => spotSpacing;

    // ========== DEBUG ==========

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
        if (spotPositions == null || spotPositions.Length == 0)
        {
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
            for (int i = 0; i < maxItems; i++)
            {
                Vector3 pos = GetSpotWorldPosition(i);

                Gizmos.color = spots[i] != null ? Color.red : Color.green;
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
    }

    /// <summary>
    /// Clear items reference nhưng KHÔNG destroy (để bay theo cell)
    /// </summary>
    public void ClearItemsWithoutDestroy()
    {
        for (int i = 0; i < maxItems; i++)
        {
            if (spots[i] != null)
            {
                spots[i].SetCell(null);
                spots[i].SetSpotIndex(-1);
                spots[i] = null;
            }
        }
    }
}