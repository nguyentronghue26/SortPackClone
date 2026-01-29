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
    [Header("Level 1 Preset")]
    [SerializeField] private bool useLevel1Preset = false;
    [Tooltip("Mỗi phần tử là index của prefab trong itemPrefabs. Ví dụ: 0,1,2,...")]
    [SerializeField] private List<int> level1ItemIDs = new List<int>();

    [Header("Spawn Settings")]
    [SerializeField] private int itemsPerMatch = 3;  // Số items cần để match (3)
    [SerializeField] private int totalLayers = 3;    // Số tầng (layers) của cell
    [SerializeField] private int minEmptySlots = 1;  // Chỉ cần 1 slot trống để bắt đầu
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private float itemScale = 0.5f;
    [SerializeField] private string itemSortingLayer = "Default";
    [SerializeField] private int itemSortingOrder = 2000;

    [Header("Match Settings")]
    [SerializeField] private float matchDelay = 0.3f;

    [Header("Grid Expand Settings")]
    [SerializeField] private bool enableGridExpand = true;
    [SerializeField] private int maxRows = 7;
    [SerializeField] private float expandDelay = 0.5f;
    [Header("Initial Board Empty Slots")]
    [SerializeField] private int initialEmptySlots = 6;   // số slot trống muốn có ở tầng 1

    // State
    private List<Cell> allCells = new List<Cell>();
    private bool isGameWon = false;
    private int moveCount = 0;
    private int totalMatches = 0;

    // Item tracking
    private Dictionary<string, int> itemTypeCounts = new Dictionary<string, int>();
    // Những loại item đã hoàn toàn bị clear, không spawn nữa
    private HashSet<string> disabledItemTypes = new HashSet<string>();

    // Events
    public System.Action OnGameWin;
    public System.Action<int> OnMoveCompleted;
    public System.Action<Cell> OnMatchFound;
    public System.Action<int> OnGridExpanded;

    private int currentRows = 0;

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

        if (gridSpawner != null)
        {
            currentRows = gridSpawner.GetCurrentRows();
        }

        foreach (var cell in allCells)
        {
            cell.OnItemAdded += OnCellItemAdded;
            cell.OnCellEmpty += OnCellBecameEmpty;
        }

        if (autoSpawnOnStart && itemPrefabs.Count > 0)
        {
            SpawnItemsSmart();
        }
    }

    private void CollectCells()
    {
        allCells.Clear();

        if (gridSpawner == null)
            gridSpawner = FindObjectOfType<GridSpawner>();

        // 1) Lấy tất cả cell trong grid chính
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

        // 2) THÊM bước này: add TẤT CẢ Cell khác trong scene (lock row, ô đặc biệt,...)
        Cell[] allSceneCells = FindObjectsOfType<Cell>();
        foreach (var c in allSceneCells)
        {
            if (c != null && !allCells.Contains(c))
            {
                allCells.Add(c);
            }
        }

        Debug.Log($"GameManager found {allCells.Count} cells (including lock cells)");
    }

    // ========== SMART ITEM SPAWNING ==========

    // Queue items cho các tầng sau
    private Queue<GameObject> itemQueue = new Queue<GameObject>();

    public void SpawnItemsSmart()
    {
        if (itemPrefabs.Count == 0)
        {
            Debug.LogWarning("Chưa có item prefabs!");
            return;
        }

        if (allCells.Count == 0)
        {
            Debug.LogWarning("Không tìm thấy cells!");
            return;
        }

        // ========== TÍNH TOÁN SLOT CHO TOÀN BOARD (CHỈ CELL THƯỜNG) ==========

        // Đếm CHỈ những cell KHÔNG phải lock để spawn
        int totalCells = 0;
        foreach (var c in allCells)
        {
            if (c == null) continue;
            if (c.GetComponent<LockedCell>() != null) continue; // bỏ cell lock
            totalCells++;
        }

        if (totalCells <= 0)
        {
            Debug.LogWarning("SpawnItemsSmart: không có cell thường nào để spawn!");
            return;
        }

        int slotsPerCell = itemsPerMatch;              // 3 slots per cell
        int slotsPerLayer = totalCells * slotsPerCell; // VD: 12 cells × 3 = 36 slots/tầng
                                                       // VD: 9 cells × 3 = 27 slots/tầng

        // Ví dụ: totalLayers = 3
        int totalSlots = slotsPerLayer * totalLayers;      // VD: 27 × 3 = 81 slots tổng

       
        int layer1EmptySlots = Mathf.Clamp(initialEmptySlots, 1, slotsPerLayer - 1);
        int layer1Slots = Mathf.Max(0, slotsPerLayer - layer1EmptySlots);


        // Tầng 2,3 cũng để 2 slots trống mỗi tầng
        int otherLayersSlots = Mathf.Max(0, (slotsPerLayer - 2) * (totalLayers - 1));
        int totalAvailableSlots = layer1Slots + otherLayersSlots;

        int itemsPerType = 3;

        // List chứa toàn bộ item để dùng cho cả 3 tầng
        List<GameObject> allItems = new List<GameObject>();
        itemTypeCounts.Clear();

        // ================== MODE 1: PRESET LEVEL 1 ==================
        if (useLevel1Preset && level1ItemIDs != null && level1ItemIDs.Count > 0)
        {
            // Dùng đúng các ID ông cấu hình trong level1ItemIDs.
            // Mỗi ID → 3 item cùng loại.
            List<GameObject> selectedPrefabs = new List<GameObject>();

            foreach (int id in level1ItemIDs)
            {
                if (id < 0 || id >= itemPrefabs.Count)
                {
                    Debug.LogWarning($"Level1Preset: ID {id} nằm ngoài range itemPrefabs (0..{itemPrefabs.Count - 1})");
                    continue;
                }

                GameObject prefab = itemPrefabs[id];

                // Tránh add trùng prefab nếu ông lỡ nhập cùng ID nhiều lần
                if (!selectedPrefabs.Contains(prefab))
                    selectedPrefabs.Add(prefab);
            }

            if (selectedPrefabs.Count == 0)
            {
                Debug.LogWarning("Level1Preset bật nhưng không có ID hợp lệ. Dùng logic spawn bình thường.");
            }
            else
            {
                foreach (var prefab in selectedPrefabs)
                {
                    string typeName = prefab.name.ToLower();
                    itemTypeCounts[typeName] = itemsPerType;   // đúng 3 item

                    for (int i = 0; i < itemsPerType; i++)
                    {
                        allItems.Add(prefab);
                    }
                }

                int presetTotalItems = allItems.Count;
                Debug.Log($"[Level1Preset] Types: {selectedPrefabs.Count}, TotalItems: {presetTotalItems}");
            }
        }

        // ================== MODE 2: LOGIC CŨ (RANDOM) ==================
        if (allItems.Count == 0)
        {
            // Chỉ chạy nếu preset không bật hoặc không có ID hợp lệ
            int maxTypesByPrefabs = itemPrefabs.Count;
            int maxTypesBySlots = totalAvailableSlots / itemsPerType;

            int targetTypes = Mathf.Min(maxTypesByPrefabs, maxTypesBySlots);
            if (targetTypes <= 0)
            {
                Debug.LogWarning("Không thể spawn: không đủ loại item hoặc slot!");
                return;
            }

            int targetTotalItems = targetTypes * itemsPerType;

            Debug.Log($"=== SPAWN CALCULATION (ALL LAYERS) ===");
            Debug.Log($"Cells: {totalCells}, Layers: {totalLayers}");
            Debug.Log($"Slots: Layer1={layer1Slots}, Others={otherLayersSlots}, Total={totalAvailableSlots}");
            Debug.Log($"Items: {targetTotalItems}, Types: {targetTypes}");

            // Chọn ngẫu nhiên targetTypes loại (không lặp)
            List<GameObject> shuffledPrefabs = new List<GameObject>(itemPrefabs);
            ShuffleList(shuffledPrefabs);
            List<GameObject> selectedPrefabs = shuffledPrefabs.GetRange(0, targetTypes);

            foreach (var prefab in selectedPrefabs)
            {
                string typeName = prefab.name.ToLower();
                itemTypeCounts[typeName] = itemsPerType; // đúng 3 item cho mỗi loại

                for (int i = 0; i < itemsPerType; i++)
                {
                    allItems.Add(prefab);
                }
            }
        }

        // Nếu vẫn không có item nào thì thôi
        if (allItems.Count == 0)
        {
            Debug.LogWarning("SpawnItemsSmart: allItems rỗng, không spawn được gì.");
            return;
        }

        // ========== PHẦN CHUNG: TRỘN & CHIA CHO LAYER 1 / QUEUE ==========
        ShuffleList(allItems);

        // Items cho tầng 1 (fill gần đầy, chỉ để layer1EmptySlots trống)
        int itemsForLayer1 = Mathf.Min(layer1Slots, allItems.Count);
        List<GameObject> layer1Items = new List<GameObject>();
        for (int i = 0; i < itemsForLayer1; i++)
        {
            layer1Items.Add(allItems[i]);
        }

        // Phần còn lại đưa vào queue cho các tầng sau
        itemQueue.Clear();
        for (int i = itemsForLayer1; i < allItems.Count; i++)
        {
            itemQueue.Enqueue(allItems[i]);
        }

        Debug.Log($"Layer 1: {layer1Items.Count} items, Queue: {itemQueue.Count} items");

        // ========== SPAWN TẦNG 1 ==========
        SpawnLayer1Items(layer1Items);
        ValidateGlobalItemCounts();

    }


    private void SpawnLayer1Items(List<GameObject> items)
    {
        // Chỉ spawn lên cell thường, bỏ cell nào có LockedCell
        List<Cell> shuffledCells = new List<Cell>();

        foreach (var c in allCells)
        {
            if (c == null) continue;

            // Nếu trên GameObject này có component LockedCell => bỏ qua
            if (c.GetComponent<LockedCell>() != null)
                continue;

            shuffledCells.Add(c);
        }

        ShuffleList(shuffledCells);

        int totalItemsSpawned = 0;

        Dictionary<Cell, List<string>> cellItems = new Dictionary<Cell, List<string>>();
        foreach (var cell in shuffledCells)
        {
            cellItems[cell] = new List<string>();
        }


        // Shuffle items trước
        ShuffleList(items);

        // BƯỚC 1: Đảm bảo mỗi cell có ít nhất 1 item
        int itemIndex = 0;
        foreach (var cell in shuffledCells)
        {
            if (itemIndex >= items.Count) break;

            GameObject prefab = items[itemIndex];
            string itemType = prefab.name.ToLower();

            SpawnItem(prefab, cell, totalItemsSpawned);
            cellItems[cell].Add(itemType);
            totalItemsSpawned++;
            itemIndex++;
        }

        // BƯỚC 2: Spawn phần còn lại vào các cells (tránh 3 cùng loại)
        for (int i = itemIndex; i < items.Count; i++)
        {
            GameObject prefab = items[i];
            string itemType = prefab.name.ToLower();

            // Tìm cell phù hợp:
            // 1. Chưa đầy (< 3 items)
            // 2. Chưa có 2 items cùng loại
            Cell targetCell = null;

            // Shuffle lại để phân bổ đều
            ShuffleList(shuffledCells);

            foreach (var cell in shuffledCells)
            {
                var cellItemList = cellItems[cell];

                // Check chưa đầy
                if (cellItemList.Count >= itemsPerMatch) continue;

                // Check chưa có 2 items cùng loại
                int sameTypeCount = 0;
                foreach (var type in cellItemList)
                {
                    if (type == itemType) sameTypeCount++;
                }

                if (sameTypeCount < 2)
                {
                    targetCell = cell;
                    break;
                }
            }

            // Nếu không tìm được cell lý tưởng, tìm cell chưa đầy bất kỳ
            if (targetCell == null)
            {
                foreach (var cell in shuffledCells)
                {
                    if (cellItems[cell].Count < itemsPerMatch)
                    {
                        targetCell = cell;
                        break;
                    }
                }
            }

            // Spawn item
            if (targetCell != null)
            {
                SpawnItem(prefab, targetCell, totalItemsSpawned);
                cellItems[targetCell].Add(itemType);
                totalItemsSpawned++;
            }
        }

        Debug.Log($"Layer 1 spawned: {totalItemsSpawned} items, all cells have at least 1 item");
    }

    private Cell FindCellWithSpace()
    {
        List<Cell> availableCells = allCells.FindAll(c => c.GetEmptySpotCount() > 0);

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

    // Spawn items vào 1 cell (dùng khi respawn tầng 2, 3)
    public void SpawnItemsInCell(Cell cell)
    {
        if (cell == null) return;
        if (itemPrefabs.Count == 0) return;

        // Hết queue => không còn gì để spawn
        if (itemQueue.Count == 0)
        {
            Debug.Log("[SpawnItemsInCell] Queue empty - no items to spawn");
            return;
        }

        // ===== 1) RESET CELL ĐỂ CHẮC CHẮN NÓ RỖNG =====
        // Tránh trường hợp Cell giữ state sai (vẫn nghĩ còn item)
        cell.ClearItems();

        int capacity = itemsPerMatch;   // thường = 3

        // Không cho cell mới full 3, phải chừa lại 1 slot để kéo
        int maxSpawnInCell = capacity - 1; // 2
        if (maxSpawnInCell <= 0)
        {
            Debug.LogWarning("[SpawnItemsInCell] capacity quá bé, không thể respawn");
            return;
        }

        // Phụ thuộc vào số item còn trong queue
        int maxSpawn = Mathf.Min(maxSpawnInCell, itemQueue.Count);

        if (maxSpawn <= 0)
        {
            Debug.LogWarning($"[SpawnItemsInCell] maxSpawn<=0 (queue={itemQueue.Count})");
            return;
        }

        // Số item sẽ spawn cho cell này: 1..maxSpawn (=> 1 hoặc 2)
        int spawnCount = Random.Range(1, maxSpawn + 1);

        // ===== 2) ĐẾM SỐ LƯỢNG MỖI LOẠI TRÊN BOARD =====
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

        // Copy queue sang list để duyệt và chọn các item sẽ spawn
        List<GameObject> queueList = new List<GameObject>(itemQueue);
        List<GameObject> spawnList = new List<GameObject>();

        // Hàm local để lấy type name
        string GetTypeName(GameObject go) => go.name.ToLower();

        // ===== 3) ƯU TIÊN 1: TÌM ITEM HOÀN THÀNH BỘ 3 (nếu có) =====
        for (int i = 0; i < queueList.Count && spawnList.Count < spawnCount; i++)
        {
            GameObject prefab = queueList[i];
            if (prefab == null) continue;

            string typeName = GetTypeName(prefab);
            int before = boardCounts.ContainsKey(typeName) ? boardCounts[typeName] : 0;
            int after = before + 1;

            // Sau khi thêm 1 con này, nếu đủ 3 và là bội số 3 -> sẽ tạo ít nhất 1 bộ 3
            if (after >= itemsPerMatch && after % itemsPerMatch == 0)
            {
                spawnList.Add(prefab);
                boardCounts[typeName] = after;
                queueList.RemoveAt(i);
                i--;
                break; // chỉ cần ưu tiên 1 con immediate trước
            }
        }

        // ===== 4) ƯU TIÊN 2: TÌM ITEM ĐÃ CÓ TRÊN BOARD (build dần lên 3) =====
        for (int i = 0; i < queueList.Count && spawnList.Count < spawnCount; i++)
        {
            GameObject prefab = queueList[i];
            if (prefab == null) continue;

            string typeName = GetTypeName(prefab);
            int before = boardCounts.ContainsKey(typeName) ? boardCounts[typeName] : 0;

            if (before > 0) // trên board đã có sẵn loại này
            {
                spawnList.Add(prefab);
                boardCounts[typeName] = before + 1;
                queueList.RemoveAt(i);
                i--;
            }
        }

        // ===== 5) NẾU VẪN CHƯA ĐỦ spawnCount -> LẤY RANDOM TỪ PHẦN CÒN LẠI =====
        // (kể cả là loại mới, vì queue không còn loại trùng thì chịu)
        System.Random rng = new System.Random();
        while (spawnList.Count < spawnCount && queueList.Count > 0)
        {
            int idx = rng.Next(0, queueList.Count);
            GameObject prefab = queueList[idx];

            spawnList.Add(prefab);
            queueList.RemoveAt(idx);
        }

        // ===== 6) CẬP NHẬT LẠI QUEUE (BỎ NHỮNG ITEM ĐÃ DÙNG) =====
        itemQueue.Clear();
        foreach (var prefab in queueList)
        {
            itemQueue.Enqueue(prefab);
        }

        // ===== 7) SPAWN CÁC ITEM VÀO CELL =====
        int itemsSpawned = 0;
        foreach (var prefab in spawnList)
        {
            SpawnItem(prefab, cell, moveCount + itemsSpawned);
            itemsSpawned++;
        }

        Debug.Log($"[SpawnItemsInCell] Cell {cell.name} respawned {itemsSpawned} items. " +
                  $"Queue left={itemQueue.Count}");
        ValidateGlobalItemCounts();

    }



    /// <summary>
    /// Đảm bảo mỗi itemType chỉ xuất hiện tối đa itemsPerMatch lần
    /// (trên toàn bộ board + queue). Nếu queue có thừa thì cắt bớt.
    /// </summary>
    private void ValidateGlobalItemCounts()
    {
        // Đếm trên board
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (var cell in allCells)
        {
            foreach (var it in cell.GetItems())
            {
                if (it == null) continue;
                string type = it.itemType;
                if (string.IsNullOrEmpty(type)) continue;

                if (!counts.ContainsKey(type))
                    counts[type] = 0;
                counts[type]++;
            }
        }

        // Đếm thêm trong queue
        List<GameObject> queueList = new List<GameObject>(itemQueue);

        foreach (var prefab in queueList)
        {
            if (prefab == null) continue;
            string type = prefab.name.ToLower();

            if (!counts.ContainsKey(type))
                counts[type] = 0;
            counts[type]++;
        }

        // Nếu loại nào vượt quá itemsPerMatch (thường = 3) thì cắt bớt trong queue
        Queue<GameObject> newQueue = new Queue<GameObject>();

        // Copy lại counts để còn biết đã cho phép bao nhiêu
        Dictionary<string, int> allowed = new Dictionary<string, int>(counts);

        foreach (var kvp in counts)
        {
            // Giới hạn tối đa
            if (allowed[kvp.Key] > itemsPerMatch)
                allowed[kvp.Key] = itemsPerMatch;
        }

        // Bây giờ rebuild queue: chỉ giữ lại những prefab mà chưa vượt quota
        foreach (var prefab in queueList)
        {
            if (prefab == null) continue;
            string type = prefab.name.ToLower();

            // Nếu loại này không bị limit thì cứ giữ
            if (!allowed.ContainsKey(type))
            {
                newQueue.Enqueue(prefab);
                continue;
            }

            // Số lượng hiện tại trên board (không tính queue mới)
            int currentOnBoard = 0;
            foreach (var cell in allCells)
            {
                foreach (var it in cell.GetItems())
                {
                    if (it != null && it.itemType == type)
                        currentOnBoard++;
                }
            }

            // Nếu currentOnBoard đã >= quota thì bỏ prefab này, không enqueue nữa
            if (currentOnBoard >= allowed[type])
            {
                // bỏ qua prefab này
                continue;
            }

            // Ngược lại còn slot cho loại này
            newQueue.Enqueue(prefab);
            currentOnBoard++;

            // Cập nhật lại allowed[type] nếu muốn chặt chẽ hơn
            // (ở đây không bắt buộc vì ta luôn check lại currentOnBoard)
        }

        itemQueue = newQueue;

        // Debug xem còn bao nhiêu mỗi loại
        foreach (var kvp in allowed)
        {
            Debug.Log($"[ValidateGlobalItemCounts] Type={kvp.Key}, allowedMax={itemsPerMatch}");
        }
    }


    private GameObject FindImmediateMergeItemInQueue()
    {
        // Đếm số lượng từng loại item đang có trên board
        Dictionary<string, int> boardItemCounts = new Dictionary<string, int>();

        foreach (var cell in allCells)
        {
            foreach (var it in cell.GetItems())
            {
                if (!boardItemCounts.ContainsKey(it.itemType))
                    boardItemCounts[it.itemType] = 0;

                boardItemCounts[it.itemType]++;
            }
        }

        // Copy queue ra list để tiện duyệt và rebuild lại queue
        List<GameObject> queueList = new List<GameObject>(itemQueue);

        GameObject chosen = null;

        foreach (var prefab in queueList)
        {
            if (prefab == null) continue;

            string typeName = prefab.name.ToLower();

            if (!boardItemCounts.TryGetValue(typeName, out int countOnBoard))
                continue;

            int afterSpawn = countOnBoard + 1;

            // Sau khi spawn thêm 1 con này, nếu đủ 3 và là bội số 3 -> tạo được bộ 3 ngay
            if (afterSpawn >= itemsPerMatch && afterSpawn % itemsPerMatch == 0)
            {
                chosen = prefab;
                break;
            }
        }

        if (chosen == null)
            return null;

        // Rebuild lại queue nhưng bỏ đi 1 instance chosen
        itemQueue.Clear();
        bool removed = false;

        foreach (var p in queueList)
        {
            if (!removed && p == chosen)
            {
                removed = true;
                continue;
            }
            itemQueue.Enqueue(p);
        }

        Debug.Log($"[FindImmediateMergeItemInQueue] Found: {chosen.name}");

        return chosen;
    }

  
    private GameObject FindBoardMatchItemInQueue()
    {
        // Đếm số lượng mỗi loại item trên board
        Dictionary<string, int> boardItemCounts = new Dictionary<string, int>();

        foreach (var cell in allCells)
        {
            foreach (var it in cell.GetItems())
            {
                if (!boardItemCounts.ContainsKey(it.itemType))
                    boardItemCounts[it.itemType] = 0;

                boardItemCounts[it.itemType]++;
            }
        }

        // Copy queue ra list
        List<GameObject> queueList = new List<GameObject>(itemQueue);

        GameObject chosen = null;

        foreach (var prefab in queueList)
        {
            if (prefab == null) continue;

            string typeName = prefab.name.ToLower();

            if (boardItemCounts.TryGetValue(typeName, out int c) && c > 0)
            {
                chosen = prefab;
                break;
            }
        }

        if (chosen == null)
            return null;

        // rebuild queue, bỏ 1 chosen
        itemQueue.Clear();
        bool removed = false;
        foreach (var p in queueList)
        {
            if (!removed && p == chosen)
            {
                removed = true;
                continue;
            }
            itemQueue.Enqueue(p);
        }

        return chosen;
    }



    // ========== GAME LOGIC ==========

    private void OnItemDropped(Item item, Cell cell)
    {
        moveCount++;
        OnMoveCompleted?.Invoke(moveCount);

        // Chỉ check match khi PLAYER drop item
        CheckForMatch(cell);
    }

    private void OnCellItemAdded(Cell cell, Item item)
    {
        // KHÔNG check match ở đây - chỉ check khi player drop
        // Để tránh tự động merge khi spawn
    }

    private void OnCellBecameEmpty(Cell cell)
    {
        // Cell trống → trigger clear animation
        Debug.Log($"Cell {cell.name} became empty - triggering clear");
        // BoardController sẽ handle qua OnCellSorted hoặc manual check
    }

    private void CheckForMatch(Cell cell)
    {
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
                StartCoroutine(RemoveMatchedItems(cell, kvp.Value));
                break;
            }
        }
    }


    private IEnumerator RemoveMatchedItems(Cell cell, List<Item> matchedItems)
    {
        // Delay chút cho đẹp
        yield return new WaitForSeconds(matchDelay);

        if (matchedItems == null || matchedItems.Count == 0)
            yield break;

        // Loại item vừa được merge
        string itemType = matchedItems[0].itemType;

        // 1) TÌM TẤT CẢ Item cùng loại đó trong toàn scene
        Item[] allItems = FindObjectsOfType<Item>();
        List<Item> allItemsOfType = new List<Item>();
        HashSet<Cell> affectedCells = new HashSet<Cell>();   // các cell có item bị xóa

        foreach (var it in allItems)
        {
            if (it != null && it.itemType == itemType)
            {
                allItemsOfType.Add(it);

                Cell owner = it.GetCurrentCell();   // Item.GetCurrentCell của em
                if (owner != null)
                    affectedCells.Add(owner);
            }
        }

        // 2) Xoá toàn bộ item loại này
        foreach (var it in allItemsOfType)
        {
            Cell ownerCell = it.GetCurrentCell();

            if (ownerCell != null)
            {
                ownerCell.RemoveItem(it);  // xoá khỏi spots của cell (kể cả cell lock)
            }

            StartCoroutine(ItemDisappearAnimation(it.gameObject));
        }

        int removedCount = allItemsOfType.Count;

        // 3) Cell nào vì thế mà trống ⇒ bắn OnCellEmpty (BoardController / LockedCell xử lý)
        foreach (var c in affectedCells)
        {
            if (c != null && c.GetItemCount() == 0)
            {
                c.CheckEmpty();    // sẽ gọi OnCellEmpty
            }
            // nếu sau này muốn clear theo full+sorted loại khác thì có thể thêm CheckSorted() ở đây
        }

        // 4) Cập nhật đếm loại đó (nếu có dùng)
        if (itemTypeCounts.ContainsKey(itemType))
        {
            itemTypeCounts[itemType] -= removedCount;
            if (itemTypeCounts[itemType] < 0)
                itemTypeCounts[itemType] = 0;
        }

        totalMatches++;
        OnMatchFound?.Invoke(cell);

        Debug.Log($"Match! Type: {itemType}. Removed {removedCount} items. Total matches: {totalMatches}");

        // 5) Xoá loại này khỏi queue spawn luôn (để không spawn lại)
        DisableItemTypeCompletely(itemType);

        // 6) Kiểm tra thắng
        CheckWinCondition();
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

        // Đếm tổng items trên board
        int totalItems = 0;
        foreach (var cell in allCells)
        {
            totalItems += cell.GetItemCount();
        }

        // WIN khi: không còn items trên board VÀ queue đã hết
        if (totalItems == 0 && itemQueue.Count == 0)
        {
            isGameWon = true;
            OnGameWin?.Invoke();
            Debug.Log($"WIN! Moves: {moveCount}, Matches: {totalMatches}");
        }
    }

    private int GetTotalEmptySpotsOnBoard()
    {
        int total = 0;
        foreach (var c in allCells)
            total += c.GetEmptySpotCount();
        return total;
    }

    /// <summary>
    /// Số item tối đa được phép spawn vào cell này
    /// sao cho sau khi spawn, toàn board vẫn còn >= minEmptySlots chỗ trống.
    /// </summary>
    private int GetMaxSpawnAllowedForCell(Cell cell)
    {
        if (cell == null) return 0;

        int cellEmpty = cell.GetEmptySpotCount();
        if (cellEmpty <= 0) return 0;

        int boardEmpty = GetTotalEmptySpotsOnBoard();

        // Spawn k item sẽ làm boardEmpty giảm k.
        // Ta cần: boardEmpty - k >= minEmptySlots  =>  k <= boardEmpty - minEmptySlots
        int maxByKeepSpace = boardEmpty - minEmptySlots;

        if (maxByKeepSpace <= 0) return 0;

        return Mathf.Min(cellEmpty, itemQueue.Count, maxByKeepSpace);
    }


    // Check có còn items không (để UI hiển thị)
    public bool HasRemainingItems()
    {
        int totalItems = 0;
        foreach (var cell in allCells)
        {
            totalItems += cell.GetItemCount();
        }
        return totalItems > 0 || itemQueue.Count > 0;
    }

    public int GetQueueCount()
    {
        return itemQueue.Count;
    }



    // Gọi khi muốn đảm bảo loại item này không còn xuất hiện nữa
    private void DisableItemTypeCompletely(string itemType)
    {
        // 1) Kiểm tra xem trên board còn itemType này không
        int countOnBoard = 0;
        foreach (var cell in allCells)
        {
            foreach (var it in cell.GetItems())
            {
                if (it.itemType == itemType)
                    countOnBoard++;
            }
        }

        // Nếu vẫn còn trên board thì chưa được disable
        if (countOnBoard > 0)
            return;

        // 2) Đánh dấu loại này đã bị disable
        if (!disabledItemTypes.Contains(itemType))
            disabledItemTypes.Add(itemType);

        // 3) Xoá tất cả itemType này khỏi queue để nó không spawn nữa
        if (itemQueue.Count > 0)
        {
            Queue<GameObject> newQueue = new Queue<GameObject>();

            foreach (var obj in itemQueue)
            {
                if (obj == null) continue;

                string t = obj.name.ToLower();
                if (t != itemType)         // giữ loại khác
                    newQueue.Enqueue(obj);
            }

            itemQueue = newQueue;
        }

        Debug.Log($"[DisableItemType] '{itemType}' is fully cleared. Removed from queue. Queue now: {itemQueue.Count}");
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
            cell.ResetLayers();
        }

        isGameWon = false;
        moveCount = 0;
        totalMatches = 0;
        itemTypeCounts.Clear();

        SpawnItemsSmart();
    }

    public int GetMoveCount() => moveCount;
    public int GetTotalMatches() => totalMatches;
    public bool IsGameWon() => isGameWon;
    public int GetItemsPerMatch() => itemsPerMatch;
}