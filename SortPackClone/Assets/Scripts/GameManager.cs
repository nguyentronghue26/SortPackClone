using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridSpawner gridSpawner;

    [Header("Item Prefabs - Kéo prefabs vào đây!")]
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();

    public List<GameObject> ItemPrefabs => itemPrefabs;

    [Header("Game Settings")]
    [SerializeField] private int itemsPerType = 3;  // Mỗi loại item xuất hiện 3 lần
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private float itemScale = 0.5f;
    [SerializeField] private string itemSortingLayer = "Default";
    [SerializeField] private int itemSortingOrder = 2000;

    public float ItemScale => itemScale;
    public string ItemSortingLayer => itemSortingLayer;
    public int ItemSortingOrder => itemSortingOrder;

    [Header("Match Settings")]
    [SerializeField] private int itemsToMatch = 3;  // Số items cùng loại để match và biến mất
    [SerializeField] private float matchDelay = 0.3f;  // Delay trước khi biến mất

    [Header("Grid Expand Settings")]
    [SerializeField] private bool enableGridExpand = true;  // Bật tính năng mở rộng grid
    [SerializeField] private int maxRows = 7;  // Số hàng tối đa
    [SerializeField] private float expandDelay = 0.5f;  // Delay trước khi expand

    // State
    private List<Cell> allCells = new List<Cell>();
    private bool isGameWon = false;
    private int moveCount = 0;
    private int totalMatches = 0;

    // Events
    public System.Action OnGameWin;
    public System.Action<int> OnMoveCompleted;
    public System.Action<Cell> OnMatchFound;  // Khi match 3 items
    public System.Action<int> OnGridExpanded;  // Khi grid mở rộng thêm hàng

    private int currentRows = 0;  // Số hàng hiện tại

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Invoke(nameof(SetupGame), 0.2f);
    }

    // ========== GAME SETUP ==========

    public void SetupGame()
    {
        CollectCells();

        // Lấy số hàng ban đầu từ GridSpawner
        if (gridSpawner != null)
        {
            currentRows = gridSpawner.GetCurrentRows();
        }

        // Subscribe to cell events
        foreach (var cell in allCells)
        {
            cell.OnItemAdded += OnCellItemAdded;
            cell.OnCellEmpty += OnCellBecameEmpty;  // Khi cell trống
        }

        if (autoSpawnOnStart && itemPrefabs.Count > 0)
        {
            SpawnItemsRandom();
        }
    }

    private void CollectCells()
    {
        allCells.Clear();

        if (gridSpawner == null)
            gridSpawner = FindObjectOfType<GridSpawner>();

        if (gridSpawner != null)
        {
            var cellArray = gridSpawner.GetAllCells();
            if (cellArray != null)
            {
                foreach (var cellObj in cellArray)
                {
                    if (cellObj != null)
                    {
                        Cell cell = cellObj.GetComponent<Cell>();
                        if (cell != null)
                        {
                            allCells.Add(cell);
                        }
                    }
                }
            }
        }

        if (allCells.Count == 0)
        {
            allCells.AddRange(FindObjectsOfType<Cell>());
        }

        Debug.Log($"GameManager found {allCells.Count} cells");
    }

    // ========== ITEM SPAWNING ==========

    public void SpawnItemsRandom()
    {
        if (itemPrefabs.Count == 0)
        {
            Debug.LogWarning("Chua co item prefabs! Keo prefabs vao GameManager.");
            return;
        }

        if (allCells.Count == 0)
        {
            Debug.LogWarning("Khong tim thay cells!");
            return;
        }

        // Tạo list items: mỗi loại xuất hiện itemsPerType lần (mặc định 3)
        List<GameObject> itemsToSpawn = new List<GameObject>();

        foreach (var prefab in itemPrefabs)
        {
            for (int i = 0; i < itemsPerType; i++)
            {
                itemsToSpawn.Add(prefab);
            }
        }

        // Shuffle để random vị trí
        ShuffleList(itemsToSpawn);

        // Spawn vào cells
        int totalItems = itemsToSpawn.Count;

        for (int i = 0; i < totalItems; i++)
        {
            // Tìm cell còn chỗ
            Cell targetCell = FindCellWithSpace();
            if (targetCell == null)
            {
                Debug.LogWarning("Khong con cell trong!");
                break;
            }

            SpawnItem(itemsToSpawn[i], targetCell, i);
        }

        Debug.Log($"Spawned {totalItems} items ({itemPrefabs.Count} types x {itemsPerType} each)");
    }

    private Cell FindCellWithSpace()
    {
        // Tìm random cell còn chỗ
        List<Cell> availableCells = allCells.FindAll(c => c.GetItemCount() < itemsToMatch);

        if (availableCells.Count == 0)
            return null;

        return availableCells[Random.Range(0, availableCells.Count)];
    }

    private void SpawnItem(GameObject prefab, Cell cell, int id)
    {
        if (prefab == null) return;

        Vector3 spawnPos = cell.GetNextItemPosition();

        GameObject itemObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        itemObj.transform.localScale = Vector3.one * itemScale;

        SpriteRenderer sr = itemObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = itemSortingLayer;
            sr.sortingOrder = itemSortingOrder;
        }

        Item item = itemObj.GetComponent<Item>();
        if (item == null)
        {
            item = itemObj.AddComponent<Item>();
        }

        if (string.IsNullOrEmpty(item.itemType))
        {
            item.itemType = prefab.name.ToLower();
        }

        item.itemID = id;
        item.OnItemDropped += OnItemDropped;

        cell.AddItem(item);
    }

    // ========== GAME LOGIC ==========

    private void OnItemDropped(Item item, Cell cell)
    {
        moveCount++;
        OnMoveCompleted?.Invoke(moveCount);
    }

    // Được gọi khi có item được thêm vào cell
    private void OnCellItemAdded(Cell cell, Item item)
    {
        // Check xem cell có đủ 3 items cùng loại không
        CheckForMatch(cell);
    }

    // Được gọi khi cell trở nên trống (player kéo hết items ra)
    private void OnCellBecameEmpty(Cell cell)
    {
        if (!enableGridExpand) return;

        // Cell trống → bay lên và thay thế
        StartCoroutine(HandleEmptyCell(cell));
    }

    private IEnumerator HandleEmptyCell(Cell cell)
    {
        yield return new WaitForSeconds(expandDelay);
        ReplaceCellFromBelow(cell);
    }

    private void CheckForMatch(Cell cell)
    {
        List<Item> items = cell.GetItems();

        if (items.Count < itemsToMatch)
            return;

        // Đếm số lượng mỗi loại item trong cell
        Dictionary<string, List<Item>> itemsByType = new Dictionary<string, List<Item>>();

        foreach (var item in items)
        {
            if (!itemsByType.ContainsKey(item.itemType))
            {
                itemsByType[item.itemType] = new List<Item>();
            }
            itemsByType[item.itemType].Add(item);
        }

        // Tìm loại có đủ itemsToMatch items
        foreach (var kvp in itemsByType)
        {
            if (kvp.Value.Count >= itemsToMatch)
            {
                // MATCH! Xóa 3 items này
                StartCoroutine(RemoveMatchedItems(cell, kvp.Value));
                break;
            }
        }
    }

    private IEnumerator RemoveMatchedItems(Cell cell, List<Item> matchedItems)
    {
        // Delay nhỏ để player thấy match
        yield return new WaitForSeconds(matchDelay);

        // Xóa items
        for (int i = 0; i < itemsToMatch && i < matchedItems.Count; i++)
        {
            Item item = matchedItems[i];
            cell.RemoveItem(item);

            // Animation biến mất
            StartCoroutine(ItemDisappearAnimation(item.gameObject));
        }

        totalMatches++;
        OnMatchFound?.Invoke(cell);

        Debug.Log($"Match found! Type: {matchedItems[0].itemType}. Total matches: {totalMatches}");

        // OnCellBecameEmpty sẽ tự động được gọi khi cell trống
        // Không cần gọi ReplaceCellFromBelow ở đây nữa

        // Check win
        CheckWinCondition();
    }

    // Cell trống → bay lên biến mất, cell mới từ dưới đẩy lên
    private void ReplaceCellFromBelow(Cell emptyCell)
    {
        if (gridSpawner == null || emptyCell == null) return;

        // GridSpawner sẽ tự handle tránh gọi 2 lần
        gridSpawner.ReplaceCellWithNewFromBelow(emptyCell, (newCell) => {
            if (newCell != null)
            {
                // Subscribe events cho cell mới
                newCell.OnItemAdded += OnCellItemAdded;
                newCell.OnCellEmpty += OnCellBecameEmpty;

                // Cập nhật list cells
                allCells.Remove(emptyCell);
                allCells.Add(newCell);

                // Spawn items vào cell mới
                SpawnItemsInCell(newCell);
            }
        });
    }

    // Spawn items ngẫu nhiên vào 1 cell
    private void SpawnItemsInCell(Cell cell)
    {
        if (itemPrefabs.Count == 0) return;

        // Random số items (1-3)
        int itemCount = Random.Range(1, itemsToMatch + 1);

        for (int i = 0; i < itemCount; i++)
        {
            GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
            SpawnItem(prefab, cell, moveCount + i);
        }
    }

    private IEnumerator ItemDisappearAnimation(GameObject itemObj)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 startScale = itemObj.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            itemObj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(itemObj);
    }

    private void CheckWinCondition()
    {
        if (isGameWon) return;

        // Win khi tất cả items đã được match (không còn items nào)
        int totalItems = 0;
        foreach (var cell in allCells)
        {
            totalItems += cell.GetItemCount();
        }

        if (totalItems == 0)
        {
            isGameWon = true;
            OnGameWin?.Invoke();
            Debug.Log($"YOU WIN! Completed in {moveCount} moves with {totalMatches} matches!");
        }
    }

    // ========== UTILITIES ==========

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // ========== PUBLIC METHODS ==========

    public void RestartGame()
    {
        foreach (var cell in allCells)
        {
            cell.ClearItems();
        }

        isGameWon = false;
        moveCount = 0;
        totalMatches = 0;

        SpawnItemsRandom();
    }

    public int GetMoveCount() => moveCount;
    public int GetTotalMatches() => totalMatches;
    public bool IsGameWon() => isGameWon;
}