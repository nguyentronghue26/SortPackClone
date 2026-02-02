using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridSpawner gridSpawner;

    [Header("Level System")]
    [SerializeField] private SortPackLevelList levelList;
    [SerializeField] private ItemList itemList;
    [SerializeField] private int currentLevelIndex = 1;

    [Header("Item Prefabs - Fallback nếu không dùng ItemList")]
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [SerializeField] private int itemsPerMatch = 3;
    [SerializeField] private int totalLayers = 3;
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private float itemScale = 0.5f;
    [SerializeField] private string itemSortingLayer = "Default";
    [SerializeField] private int itemSortingOrder = 2000;

    [Header("Match Settings")]
    [SerializeField] private float matchDelay = 0.3f;
    [Header("Layer Visual")]
    [SerializeField] private float zScaleReducePerLayer = 0.25f;

    // State
    private List<Cell> allCells = new List<Cell>();
    private bool isGameWon = false;
    private int moveCount = 0;
    private int totalMatches = 0;

    // Item tracking
    private Dictionary<string, int> itemTypeCounts = new Dictionary<string, int>();
    private HashSet<string> disabledItemTypes = new HashSet<string>();

    // Queue items cho các tầng sau
    private Queue<GameObject> itemQueue = new Queue<GameObject>();

    // Level data cache
    private SortPackLevelData currentLevelData;

    // Lưu items theo layer để spawn dần
    private Dictionary<int, List<CellSlotItemData>> itemsByLayer = new Dictionary<int, List<CellSlotItemData>>();

    // Track layer hiện tại của mỗi Cell
    private Dictionary<Cell, int> cellCurrentLayer = new Dictionary<Cell, int>();

    // Track cells đang trong quá trình animation - KHÔNG check match
    private HashSet<Cell> cellsInAnimation = new HashSet<Cell>();
    private bool suppressCellEmptyEvents = false;
    // Events
    public System.Action OnGameWin;
    public System.Action<int> OnMoveCompleted;
    public System.Action<Cell> OnMatchFound;
    public System.Action<int> OnLevelLoaded;

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
        if (autoSpawnOnStart)
        {
            LoadLevel(currentLevelIndex);
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
                    if (cellObj == null) continue;
                    Cell cell = cellObj.GetComponent<Cell>();
                    if (cell != null && !allCells.Contains(cell))
                    {
                        allCells.Add(cell);
                    }
                }
            }
        }

        Cell[] allSceneCells = FindObjectsOfType<Cell>();
        foreach (var c in allSceneCells)
        {
            if (c != null && !allCells.Contains(c))
            {
                allCells.Add(c);
            }
        }

        Debug.Log($"GameManager found {allCells.Count} cells");

        foreach (var cell in allCells)
        {
            if (cell == null) continue;
            cell.OnItemAdded += OnCellItemAdded;
            cell.OnCellEmpty += OnCellBecameEmpty;
            //cell.OnCellEmpty += HandleCellEmptyForAnimation;    
        }

        cellCurrentLayer.Clear();
        foreach (var cell in allCells)
        {
            if (cell != null)
                cellCurrentLayer[cell] = 0;
        }
    }
    //private void HandleCellEmptyForAnimation(Cell cell)
    //{
    //    if (cell == null) return;

    //    Debug.Log($"[HandleCellEmptyForAnimation] {cell.name} became empty -> raise next layer");

    //    // Tránh conflict với các cell đang bay vì match
    //    if (cellsInAnimation.Contains(cell))
    //    {
    //        Debug.Log($"[HandleCellEmptyForAnimation] Skip {cell.name} because it's already in animation");
    //        return;
    //    }

    //    // Đánh dấu đang anim để tránh CheckForMatch chạm vào
    //    cellsInAnimation.Add(cell);

    //    // Dùng lại logic cũ: cell bay lên + hiện cell layer dưới / remove hẳn
    //    TryRaiseCellFromNextLayer(cell);

    //}


    // ========== DEBUG ITEM IDs ==========

    private void DebugItemIDs()
    {
        Debug.Log("========== DEBUG ITEM IDs ==========");

        Debug.Log("[ItemList] Các Item ID có sẵn:");
        if (itemList != null)
        {
            foreach (var prefab in itemList.itemPrefabs)
            {
                if (prefab == null) continue;
                Item item = prefab.GetComponent<Item>();
                if (item != null)
                {
                    Debug.Log($"  - Prefab: {prefab.name}, ItemID: {item.itemID}, ItemType: {item.itemType}");
                }
                else
                {
                    Debug.LogWarning($"  - Prefab: {prefab.name} KHÔNG CÓ Item component!");
                }
            }
        }
        else
        {
            Debug.LogWarning("[ItemList] ItemList chưa được gán!");
        }

        Debug.Log("[LevelData] Các Item ID được sử dụng trong level:");
        if (currentLevelData != null)
        {
            HashSet<int> usedIDs = new HashSet<int>();
            foreach (var placement in currentLevelData.placements)
            {
                if (placement != null && placement.itemID >= 0)
                {
                    usedIDs.Add(placement.itemID);
                }
            }

            foreach (int id in usedIDs)
            {
                bool exists = itemList != null && itemList.GetPrefab(id) != null;
                string status = exists ? "✓ CÓ" : "✗ KHÔNG TÌM THẤY";
                Debug.Log($"  - ItemID: {id} → {status}");
            }
        }

        Debug.Log("=====================================");
    }

    // ========== LEVEL LOADING ==========

    public void LoadLevel(int levelIndex)
    {
        if (levelList == null)
        {
            Debug.LogError("GameManager: LevelList chưa được gán!");
            SpawnItemsRandom();
            return;
        }

        if (itemList == null)
        {
            Debug.LogError("GameManager: ItemList chưa được gán!");
            SpawnItemsRandom();
            return;
        }

        currentLevelData = levelList.GetLevelByLevelNumber(levelIndex);

        if (currentLevelData == null)
        {
            Debug.LogError($"GameManager: Không tìm thấy level {levelIndex} trong LevelList!");
            SpawnItemsRandom();
            return;
        }

        currentLevelIndex = levelIndex;

        if (gridSpawner == null)
            gridSpawner = FindObjectOfType<GridSpawner>();

        if (gridSpawner != null)
        {
            gridSpawner.SpawnGridForLevel(currentLevelData);
        }
        else
        {
            Debug.LogError("GameManager: Không tìm thấy GridSpawner!");
            return;
        }

        CollectCells();
        ClearAllItems();
        ParseLevelData(currentLevelData);
        DebugItemIDs();
        SpawnLayer(0);

        OnLevelLoaded?.Invoke(levelIndex);
        Debug.Log($"Loaded Level {levelIndex}: {currentLevelData.description}");
    }

    private void ParseLevelData(SortPackLevelData levelData)
    {
        itemsByLayer.Clear();
        itemQueue.Clear();
        itemTypeCounts.Clear();
        disabledItemTypes.Clear();
        cellCurrentLayer.Clear();
        cellsInAnimation.Clear();

        if (levelData.itemsPerMatch > 0)
        {
            itemsPerMatch = levelData.itemsPerMatch;
        }

        foreach (var placement in levelData.placements)
        {
            if (placement == null) continue;
            if (placement.itemID < 0) continue;

            int layer = placement.z;

            if (!itemsByLayer.ContainsKey(layer))
            {
                itemsByLayer[layer] = new List<CellSlotItemData>();
            }

            itemsByLayer[layer].Add(placement);
        }

        for (int z = 1; z < levelData.sizeZ; z++)
        {
            if (itemsByLayer.ContainsKey(z))
            {
                foreach (var placement in itemsByLayer[z])
                {
                    GameObject prefab = GetPrefabByItemID(placement.itemID);
                    if (prefab != null)
                    {
                        itemQueue.Enqueue(prefab);
                    }
                }
            }
        }

        int layer0Count = itemsByLayer.ContainsKey(0) ? itemsByLayer[0].Count : 0;
        Debug.Log($"ParseLevelData: Layer0={layer0Count} items, Queue={itemQueue.Count} items");
    }

    private void SpawnLayer(int layerIndex)
    {
        if (!itemsByLayer.ContainsKey(layerIndex))
        {
            Debug.Log($"Layer {layerIndex} không có items");
            return;
        }

        var placements = itemsByLayer[layerIndex];
        int spawnedCount = 0;

        foreach (var placement in placements)
        {
            int row = placement.x;
            int col = placement.y;

            Cell targetCell = FindCellAtPosition(col, row);
            if (targetCell == null)
            {
                Debug.LogWarning($"[SpawnLayer] Không tìm thấy cell tại (col={col}, row={row})");
                continue;
            }

            GameObject prefab = GetPrefabByItemID(placement.itemID);
            if (prefab == null)
            {
                Debug.LogWarning($"[SpawnLayer] Không tìm thấy prefab với itemID {placement.itemID}");
                continue;
            }

            // Spawn item làm CHILD của cell
            SpawnItemAtSlot(prefab, targetCell, placement.slotIndex, spawnedCount);
            spawnedCount++;

            string itemType = prefab.name.ToLower();
            if (!itemTypeCounts.ContainsKey(itemType))
                itemTypeCounts[itemType] = 0;
            itemTypeCounts[itemType]++;
        }

        Debug.Log($"Layer {layerIndex}: Spawned {spawnedCount} items");
    }

    private Cell FindCellAtPosition(int col, int row)
    {
        foreach (var cell in allCells)
        {
            if (cell == null) continue;
            if (cell.GetComponent<LockedCell>() != null) continue;

            if (cell.Column == col && cell.Row == row)
            {
                return cell;
            }
        }

        return null;
    }

    private GameObject GetPrefabByItemID(int itemID)
    {
        if (itemList != null)
        {
            GameObject prefab = itemList.GetPrefab(itemID);
            if (prefab != null)
                return prefab;
        }

        if (itemID >= 0 && itemID < itemPrefabs.Count)
            return itemPrefabs[itemID];

        foreach (var prefab in itemPrefabs)
        {
            if (prefab == null) continue;
            Item item = prefab.GetComponent<Item>();
            if (item != null && item.itemID == itemID)
                return prefab;
        }

        return null;
    }

    /// <summary>
    /// Spawn item vào slot - LUÔN LÀ CHILD CỦA CELL
    /// </summary>
    private void SpawnItemAtSlot(GameObject prefab, Cell cell, int slotIndex, int id)
    {
        if (prefab == null || cell == null) return;

        Vector3 spawnPos = cell.GetSpotWorldPosition(slotIndex);

        // LUÔN spawn làm child của cell
        GameObject itemObj = Instantiate(prefab, spawnPos, Quaternion.identity, cell.transform);
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

        cell.AddItemToSpot(item, slotIndex);
    }

    private void ClearAllItems()
    {
        suppressCellEmptyEvents = true;
        foreach (var cell in allCells)
        {
            if (cell != null)
            {
                cell.ClearItems();
                cell.ResetLayers();
            }
        }


        suppressCellEmptyEvents = false;
        itemQueue.Clear();
        itemTypeCounts.Clear();
        disabledItemTypes.Clear();
        cellCurrentLayer.Clear();
        cellsInAnimation.Clear();

        moveCount = 0;
        totalMatches = 0;
        isGameWon = false;
    }

    // ========== RANDOM SPAWN (FALLBACK) ==========

    private void SpawnItemsRandom()
    {
        if (itemPrefabs.Count == 0)
        {
            Debug.LogWarning("Chưa có item prefabs!");
            return;
        }

        List<Cell> normalCells = new List<Cell>();
        foreach (var c in allCells)
        {
            if (c == null) continue;
            if (c.GetComponent<LockedCell>() != null) continue;
            normalCells.Add(c);
        }

        if (normalCells.Count == 0) return;

        int slotsPerCell = itemsPerMatch;
        int totalSlots = normalCells.Count * slotsPerCell;
        int targetTypes = Mathf.Min(itemPrefabs.Count, totalSlots / 3);

        List<GameObject> allItems = new List<GameObject>();
        List<GameObject> shuffledPrefabs = new List<GameObject>(itemPrefabs);
        ShuffleList(shuffledPrefabs);

        for (int i = 0; i < targetTypes; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                allItems.Add(shuffledPrefabs[i]);
            }
        }

        ShuffleList(allItems);
        ShuffleList(normalCells);

        int itemIndex = 0;
        foreach (var cell in normalCells)
        {
            for (int slot = 0; slot < slotsPerCell && itemIndex < allItems.Count; slot++)
            {
                SpawnItemAtSlot(allItems[itemIndex], cell, slot, itemIndex);
                itemIndex++;
            }
        }

        Debug.Log($"Random spawn: {itemIndex} items");
    }

    // ========== SPAWN ITEMS IN CELL (RESPAWN QUEUE) ==========

    public void SpawnItemsInCell(Cell cell)
    {
        if (cell == null) return;
        if (itemQueue.Count == 0)
        {
            Debug.Log("[SpawnItemsInCell] Queue empty");
            return;
        }

        cell.ClearItems();

        int capacity = itemsPerMatch;
        int maxSpawn = Mathf.Min(capacity - 1, itemQueue.Count);
        if (maxSpawn <= 0) return;

        int spawnCount = Random.Range(1, maxSpawn + 1);

        Dictionary<string, int> boardCounts = new Dictionary<string, int>();
        foreach (var c in allCells)
        {
            foreach (var it in c.GetItems())
            {
                if (!boardCounts.ContainsKey(it.itemType))
                    boardCounts[it.itemType] = 0;
                boardCounts[it.itemType]++;
            }
        }

        List<GameObject> queueList = new List<GameObject>(itemQueue);
        List<GameObject> spawnList = new List<GameObject>();

        // Ưu tiên 1: Tìm item hoàn thành bộ 3
        for (int i = 0; i < queueList.Count && spawnList.Count < spawnCount; i++)
        {
            GameObject prefab = queueList[i];
            if (prefab == null) continue;

            string typeName = prefab.name.ToLower();
            int before = 0;
            boardCounts.TryGetValue(typeName, out before);
            int after = before + 1;

            if (after >= itemsPerMatch && after % itemsPerMatch == 0)
            {
                spawnList.Add(prefab);
                boardCounts[typeName] = after;
                queueList.RemoveAt(i);
                i--;
                break;
            }
        }

        // Ưu tiên 2: Tìm item đã có trên board
        for (int i = 0; i < queueList.Count && spawnList.Count < spawnCount; i++)
        {
            GameObject prefab = queueList[i];
            if (prefab == null) continue;

            string typeName = prefab.name.ToLower();
            int before = 0;
            boardCounts.TryGetValue(typeName, out before);

            if (before > 0)
            {
                spawnList.Add(prefab);
                boardCounts[typeName] = before + 1;
                queueList.RemoveAt(i);
                i--;
            }
        }

        // Random nếu chưa đủ
        while (spawnList.Count < spawnCount && queueList.Count > 0)
        {
            int idx = Random.Range(0, queueList.Count);
            spawnList.Add(queueList[idx]);
            queueList.RemoveAt(idx);
        }

        itemQueue.Clear();
        foreach (var prefab in queueList)
        {
            itemQueue.Enqueue(prefab);
        }

        for (int i = 0; i < spawnList.Count; i++)
        {
            SpawnItemAtSlot(spawnList[i], cell, i, moveCount + i);
        }

        Debug.Log($"[SpawnItemsInCell] {cell.name}: {spawnList.Count} items, Queue: {itemQueue.Count}");
    }

    // ========== LAYER RAISE LOGIC ==========

    /// <summary>
    /// Khi cell trống (sau khi bay đi), spawn cell mới với items từ layer kế tiếp
    /// </summary>
    private void TryRaiseCellFromNextLayer(Cell cell)
    {
        if (cell == null || gridSpawner == null)
            return;

        // Tránh gọi 2 lần
        if (cellsInAnimation.Contains(cell))
            return;

        cellsInAnimation.Add(cell);

        int row = cell.Row;
        int col = cell.Column;

        if (!cellCurrentLayer.TryGetValue(cell, out int curLayer))
            curLayer = 0;

        int nextLayer = curLayer + 1;

        // Tìm items ở layer kế tiếp (có thể rỗng)
        List<CellSlotItemData> itemsForThisCell = new List<CellSlotItemData>();
        if (itemsByLayer.ContainsKey(nextLayer))
        {
            itemsForThisCell = itemsByLayer[nextLayer].FindAll(p => p.x == row && p.y == col);
        }

        // Check xem có layer tiếp theo không
        bool hasNextLayer = itemsForThisCell.Count > 0;

        if (hasNextLayer)
        {
            // CÓ layer tiếp → cell bay đi, spawn cell mới
            gridSpawner.ReplaceCellWithNewFromBelow(cell, (newCell) =>
            {
                if (newCell == null)
                {
                    cellsInAnimation.Remove(cell);
                    return;
                }

                // Update allCells
                int index = allCells.IndexOf(cell);
                if (index >= 0)
                    allCells[index] = newCell;
                else
                    allCells.Add(newCell);

                // Update layer tracking
                cellCurrentLayer.Remove(cell);
                cellCurrentLayer[newCell] = nextLayer;

                // Gắn events
                newCell.OnItemAdded += OnCellItemAdded;
                newCell.OnCellEmpty += OnCellBecameEmpty;

                // Spawn items
                int id = 0;
                foreach (var p in itemsForThisCell)
                {
                    GameObject prefab = GetPrefabByItemID(p.itemID);
                    if (prefab == null) continue;

                    SpawnItemAtSlot(prefab, newCell, p.slotIndex, id++);
                }

                Debug.Log($"[Raise] Cell ({row},{col}) → layer {nextLayer}, items: {itemsForThisCell.Count}");

                cellsInAnimation.Remove(cell);
                cellsInAnimation.Remove(newCell);

                CheckWinCondition();
            });
        }
        else
        {
            // KHÔNG còn layer → cell bay đi, KHÔNG spawn cell mới
            gridSpawner.RemoveCellWithAnimation(cell, () =>
            {
                // Xóa khỏi allCells
                allCells.Remove(cell);
                cellCurrentLayer.Remove(cell);
                cellsInAnimation.Remove(cell);

                Debug.Log($"[Remove] Cell ({row},{col}) removed - no more layers");

                CheckWinCondition();
            });
        }
    }

    // ========== GAME LOGIC ==========

    private void OnItemDropped(Item item, Cell cell)
    {
        moveCount++;
        OnMoveCompleted?.Invoke(moveCount);
        CheckForMatch(cell);
    }

    private void OnCellItemAdded(Cell cell, Item item)
    {
        // Không check match ở đây
    }

    private void OnCellBecameEmpty(Cell cell)
    {
        Debug.Log($"[OnCellBecameEmpty] {cell.name}");

        if (cell == null) return;

        // Nếu đang reset level thì bỏ qua
        if (suppressCellEmptyEvents)
        {
            Debug.Log($"[OnCellBecameEmpty] Suppressed for {cell.name} (clearing level)");
            return;
        }

        // ========== THÊM DÒNG NÀY ==========
        // Skip nếu cell thuộc LockedCell - để LockedCell tự xử lý
        LockedCell lockedCell = cell.GetComponent<LockedCell>();
        if (lockedCell == null)
        {
            lockedCell = cell.GetComponentInParent<LockedCell>();
        }

        if (lockedCell != null)
        {
            Debug.Log($"[OnCellBecameEmpty] Skip {cell.name} - belongs to LockedCell");
            return;  // LockedCell sẽ tự xử lý logic của nó
        }
        // ===================================

        // Bình thường: cell rỗng => bay lên & lôi layer dưới lên
        TryRaiseCellFromNextLayer(cell);
    }


    private void CheckForMatch(Cell cell)
    {
        // Skip nếu cell đang trong animation
        if (cellsInAnimation.Contains(cell))
        {
            Debug.Log($"[CheckForMatch] Skipping - cell {cell.name} is in animation");
            return;
        }

        // ========== THÊM DÒNG NÀY ==========
        // Skip nếu cell thuộc LockedCell - LockedCell có logic riêng
        LockedCell lockedCell = cell.GetComponent<LockedCell>();
        if (lockedCell == null)
        {
            lockedCell = cell.GetComponentInParent<LockedCell>();
        }

        if (lockedCell != null)
        {
            Debug.Log($"[CheckForMatch] Skip {cell.name} - belongs to LockedCell, let LockedCell handle it");
            // Gọi CheckSorted để trigger OnCellSorted cho LockedCell
            cell.CheckSorted();
            return;
        }
        // ===================================

        List<Item> items = cell.GetItems();

        if (items.Count < itemsPerMatch)
            return;

        Dictionary<string, List<Item>> itemsByType = new Dictionary<string, List<Item>>();

        foreach (var item in items)
        {
            if (!itemsByType.ContainsKey(item.itemType))
            {
                itemsByType[item.itemType] = new List<Item>();
            }
            itemsByType[item.itemType].Add(item);
        }

        foreach (var kvp in itemsByType)
        {
            if (kvp.Value.Count >= itemsPerMatch)
            {
                StartCoroutine(ClearCellWithAnimation(cell, kvp.Key));
                break;
            }
        }
    }




    private IEnumerator ClearCellWithAnimation(Cell cell, string matchedItemType)
    {
        yield return new WaitForSeconds(matchDelay);

        if (cell == null) yield break;

        cellsInAnimation.Add(cell);

        List<Cell> cellsToClear = new List<Cell>();
        int totalItemsRemoved = 0;

        foreach (var c in allCells)
        {
            if (c == null || cellsInAnimation.Contains(c)) continue;

            List<Item> items = c.GetItems();
            bool hasMatchedType = false;

            foreach (var it in items)
            {
                if (it.itemType == matchedItemType)
                {
                    hasMatchedType = true;
                    totalItemsRemoved++;
                }
            }

            if (hasMatchedType)
            {
                cellsToClear.Add(c);
                cellsInAnimation.Add(c);
            }
        }

        if (!cellsToClear.Contains(cell))
        {
            cellsToClear.Add(cell);
        }

        Debug.Log($"[ClearCellWithAnimation] Clearing {cellsToClear.Count} cells with type '{matchedItemType}'");

        foreach (var c in cellsToClear)
        {
            c.ClearItemsWithoutDestroy();
        }

        if (itemTypeCounts.ContainsKey(matchedItemType))
        {
            itemTypeCounts[matchedItemType] -= totalItemsRemoved;
            if (itemTypeCounts[matchedItemType] < 0)
                itemTypeCounts[matchedItemType] = 0;
        }

        totalMatches++;
        OnMatchFound?.Invoke(cell);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMerge();

        Debug.Log($"Match! Type: {matchedItemType}. Removed {totalItemsRemoved} items from {cellsToClear.Count} cells");

        // ============ SỬA PHẦN NÀY ============
        foreach (var c in cellsToClear)
        {
            int row = c.Row;
            int col = c.Column;

            // Lấy layer hiện tại của cell
            int curLayer = 0;
            if (cellCurrentLayer.TryGetValue(c, out int layer))
            {
                curLayer = layer;
            }

            int nextLayer = curLayer + 1;

            // Check xem có items ở layer tiếp theo không
            bool hasNextLayer = false;
            if (itemsByLayer.ContainsKey(nextLayer))
            {
                List<CellSlotItemData> itemsForThisCell = itemsByLayer[nextLayer].FindAll(
                    p => p.x == row && p.y == col
                );
                hasNextLayer = itemsForThisCell.Count > 0;
            }

            if (hasNextLayer)
            {
                // CÓ layer tiếp → Replace cell và spawn items mới
                gridSpawner.ReplaceCellWithNewFromBelow(c, (newCell) =>
                {
                    OnCellReplacedAfterMatch(c, newCell, matchedItemType);
                });
            }
            else
            {
                // KHÔNG còn layer → Cell bay đi và biến mất, KHÔNG spawn cell mới
                gridSpawner.RemoveCellWithAnimation(c, () =>
                {
                    // Xóa khỏi tracking
                    allCells.Remove(c);
                    cellCurrentLayer.Remove(c);
                    cellsInAnimation.Remove(c);

                    Debug.Log($"[ClearCellWithAnimation] Cell ({row},{col}) removed - no more layers after merge");
                });
            }
        }
        // =====================================

        DisableItemTypeCompletely(matchedItemType);

        yield return new WaitForSeconds(0.5f);
        CheckWinCondition();
    }

    /// <summary>
    /// Callback sau khi cell được thay thế (sau match)
    /// </summary>
    private void OnCellReplacedAfterMatch(Cell oldCell, Cell newCell, string matchedItemType)
    {
        if (newCell == null)
        {
            cellsInAnimation.Remove(oldCell);
            return;
        }

        // Update allCells
        int index = allCells.IndexOf(oldCell);
        if (index >= 0)
            allCells[index] = newCell;
        else
            allCells.Add(newCell);

        // Update layer tracking
        int curLayer = 0;
        if (cellCurrentLayer.TryGetValue(oldCell, out curLayer))
        {
            cellCurrentLayer.Remove(oldCell);
        }

        int nextLayer = curLayer + 1;
        cellCurrentLayer[newCell] = nextLayer;

        // Gắn events
        newCell.OnItemAdded += OnCellItemAdded;
        newCell.OnCellEmpty += OnCellBecameEmpty;

        // Tìm items từ layer kế tiếp cho cell này
        int row = newCell.Row;
        int col = newCell.Column;

        if (itemsByLayer.ContainsKey(nextLayer))
        {
            List<CellSlotItemData> itemsForThisCell = itemsByLayer[nextLayer].FindAll(
                p => p.x == row && p.y == col
            );

            // Spawn items cho cell mới
            int id = 0;
            foreach (var p in itemsForThisCell)
            {
                GameObject prefab = GetPrefabByItemID(p.itemID);
                if (prefab == null) continue;

                SpawnItemAtSlot(prefab, newCell, p.slotIndex, id++);
            }

            Debug.Log($"[OnCellReplaced] Cell ({row},{col}) now at layer {nextLayer} with {itemsForThisCell.Count} items");
        }

        // Xóa khỏi animation set
        cellsInAnimation.Remove(oldCell);
        cellsInAnimation.Remove(newCell);
    }

    private void CheckWinCondition()
    {
        if (isGameWon) return;

        int totalItems = 0;
        foreach (var cell in allCells)
        {
            if (cell != null)
                totalItems += cell.GetItemCount();
        }

        if (totalItems == 0 && itemQueue.Count == 0)
        {
            isGameWon = true;
            OnGameWin?.Invoke();
            Debug.Log($"WIN! Level {currentLevelIndex}, Moves: {moveCount}, Matches: {totalMatches}");
        }
    }

    private void DisableItemTypeCompletely(string itemType)
    {
        int countOnBoard = 0;
        foreach (var cell in allCells)
        {
            if (cell == null) continue;
            foreach (var it in cell.GetItems())
            {
                if (it.itemType == itemType)
                    countOnBoard++;
            }
        }

        if (countOnBoard > 0)
            return;

        if (!disabledItemTypes.Contains(itemType))
            disabledItemTypes.Add(itemType);

        if (itemQueue.Count > 0)
        {
            Queue<GameObject> newQueue = new Queue<GameObject>();

            foreach (var obj in itemQueue)
            {
                if (obj == null) continue;

                string t = obj.name.ToLower();
                if (t != itemType)
                    newQueue.Enqueue(obj);
            }

            itemQueue = newQueue;
        }

        Debug.Log($"[DisableItemType] '{itemType}' cleared. Queue: {itemQueue.Count}");
    }



    // ========== UTILITIES & PUBLIC API ==========

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

    public void LoadNextLevel() => LoadLevel(currentLevelIndex + 1);
    public void RestartLevel() => LoadLevel(currentLevelIndex);
    public void RestartGame() => RestartLevel();

    public int GetCurrentLevelIndex() => currentLevelIndex;
    public int GetMoveCount() => moveCount;
    public int GetTotalMatches() => totalMatches;
    public bool IsGameWon() => isGameWon;
    public int GetItemsPerMatch() => itemsPerMatch;
    public int GetQueueCount() => itemQueue.Count;

    public bool HasRemainingItems()
    {
        int totalItems = 0;
        foreach (var cell in allCells)
        {
            if (cell != null)
                totalItems += cell.GetItemCount();
        }
        return totalItems > 0 || itemQueue.Count > 0;
    }
}