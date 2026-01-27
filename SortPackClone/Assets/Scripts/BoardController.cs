using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GridSpawner gridSpawner;

    private List<Cell> cells = new List<Cell>();

    [Header("Clear Animation")]
    [SerializeField] private float clearDuration = 0.6f;
    [SerializeField] private float slideDistance = 2.0f;
    [SerializeField] private AnimationCurve clearCurve;

    [Header("Respawn Animation")]
    [SerializeField] private float respawnDuration = 0.5f;
    [SerializeField] private float respawnOffsetY = -3f;

    [Header("Spawn Item")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private int itemsPerNewCell = 3;

    private HashSet<Cell> clearingCells = new HashSet<Cell>();

    void Start()
    {
        if (gridSpawner == null)
            gridSpawner = FindObjectOfType<GridSpawner>();

        InitCellsFromGrid();

        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (clearCurve == null || clearCurve.keys.Length == 0)
            clearCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    private void InitCellsFromGrid()
    {
        cells.Clear();

        GameObject[,] grid = gridSpawner.GetAllCells();
        if (grid == null)
        {
            Debug.LogWarning("BoardController: grid is null, chắc Start chạy trước khi GridSpawner SpawnGrid");
            return;
        }

        int cols = grid.GetLength(0);
        int rows = grid.GetLength(1);

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                GameObject cellGO = grid[c, r];
                if (cellGO == null) continue;

                Cell cell = cellGO.GetComponent<Cell>();
                if (cell != null)
                {
                    cells.Add(cell);
                    cell.OnCellSorted += HandleCellSorted;
                    cell.OnCellEmpty += HandleCellEmpty;
                }
            }
        }

        Debug.Log($"BoardController: Registered {cells.Count} cells from GridSpawner");
    }

    void HandleCellSorted(Cell cell)
    {
        if (!clearingCells.Contains(cell))
            StartCoroutine(ClearAndRespawnCell(cell));
    }

    void HandleCellEmpty(Cell cell)
    {
        if (!clearingCells.Contains(cell))
            StartCoroutine(ClearAndRespawnCell(cell));
    }

    // ==== Clear & Respawn như mình gửi hôm trước ====
    private IEnumerator ClearAndRespawnCell(Cell cell)
    {
        clearingCells.Add(cell);

        List<Item> items = cell.GetItems();

        if (items.Count > 0)
        {
            foreach (var it in items)
            {
                if (it == null) continue;
                var col = it.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            Vector3 slideDir = GetFreeSideDirection(cell);

            Vector3[] startPos = new Vector3[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    startPos[i] = items[i].transform.position;
            }

            float t = 0f;
            while (t < clearDuration)
            {
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / clearDuration);
                float upAmount = clearCurve.Evaluate(n);

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;

                    Vector3 basePos = startPos[i];
                    Vector3 pos =
                        basePos +
                        Vector3.up * upAmount * 1.5f +
                        slideDir * slideDistance * n;

                    items[i].transform.position = pos;
                }

                yield return null;
            }

            foreach (var it in items)
            {
                if (it != null)
                    Destroy(it.gameObject);
            }

            cell.ClearItems();
        }

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(RespawnCellWithItems(cell));

        clearingCells.Remove(cell);
    }

    private Vector3 GetFreeSideDirection(Cell cell)
    {
        float checkDistance = 3f;
        Vector3 origin = cell.transform.position;

        bool leftBlocked = Physics.Raycast(origin, Vector3.left, checkDistance);
        bool rightBlocked = Physics.Raycast(origin, Vector3.right, checkDistance);

        if (leftBlocked && !rightBlocked) return Vector3.right;
        if (rightBlocked && !leftBlocked) return Vector3.left;

        return Vector3.right;
    }

    private IEnumerator RespawnCellWithItems(Cell cell)
    {
        if (cell == null)
            yield break;

        // 1) Animate cell đi từ dưới lên (giữ nguyên như cũ)
        Vector3 basePos = cell.transform.position;
        float offset = Mathf.Abs(respawnOffsetY);
        Vector3 startPos = basePos + Vector3.down * offset;

        cell.transform.position = startPos;

        float t = 0f;
        while (t < respawnDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / respawnDuration);
            cell.transform.position = Vector3.Lerp(startPos, basePos, n);
            yield return null;
        }
        cell.transform.position = basePos;

        // 2) Spawn item mới với scale & layer giống GameManager

        if (gameManager == null || gameManager.ItemPrefabs == null || gameManager.ItemPrefabs.Count == 0)
        {
            Debug.LogError("[BoardController] GameManager hoặc ItemPrefabs bị null, kiểm tra lại.");
            yield break;
        }

        var prefabList = gameManager.ItemPrefabs;
        int spawnCount = Mathf.Max(1, itemsPerNewCell);

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefabGO = prefabList[Random.Range(0, prefabList.Count)];
            if (prefabGO == null)
                continue;

            Vector3 spawnPos = cell.GetNextItemPosition();

            // 👉 instantiate
            GameObject itemObj = Instantiate(prefabGO, spawnPos, Quaternion.identity);

            // 👉 dùng cùng itemScale với GameManager
            float scale = gameManager != null ? gameManager.ItemScale : 1f;
            itemObj.transform.localScale = Vector3.one * scale;

            // 👉 dùng cùng sorting layer với GameManager
            SpriteRenderer sr = itemObj.GetComponent<SpriteRenderer>();
            if (sr != null && gameManager != null)
            {
                sr.sortingLayerName = gameManager.ItemSortingLayer;
                sr.sortingOrder = gameManager.ItemSortingOrder;
            }

            // Lấy/ thêm component Item
            Item newItem = itemObj.GetComponent<Item>();
            if (newItem == null)
                newItem = itemObj.AddComponent<Item>();

            if (string.IsNullOrEmpty(newItem.itemType))
                newItem.itemType = prefabGO.name.ToLower();

            // Cho cell quản lý & snap lại
            cell.AddItem(newItem);
        }
    }

}
