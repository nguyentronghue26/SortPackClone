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
    [SerializeField] private float slideDistance = 12.0f;
    [SerializeField] private float forwardDistance = 1.0f;
    [SerializeField] private AnimationCurve clearCurve;

    [Header("Respawn Animation")]
    [SerializeField] private float respawnDuration = 0.5f;
    [SerializeField] private float respawnOffsetZ = 0.5f;

    [Header("Spawn Item")]
    [SerializeField] private GameManager gameManager;

    private HashSet<Cell> clearingCells = new HashSet<Cell>();

    void Start()
    {
        if (gridSpawner == null)
            gridSpawner = FindObjectOfType<GridSpawner>();

        if (gameManager == null)
            gameManager = GameManager.Instance;

        InitCellsFromGrid();

        if (clearCurve == null || clearCurve.keys.Length == 0)
            clearCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    private void InitCellsFromGrid()
    {
        cells.Clear();

        GameObject[,] grid = gridSpawner.GetAllCells();
        if (grid == null)
        {
            Debug.LogWarning("BoardController: grid is null");
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
                    cell.OnCellEmpty += HandleCellEmpty;  // Khi cell trống
                    cell.OnLayerDepleted += HandleLayerDepleted;
                }
            }
        }

        Debug.Log($"BoardController: Registered {cells.Count} cells");
    }

    // Khi cell sorted (merge thành công)
    void HandleCellSorted(Cell cell)
    {
        if (clearingCells.Contains(cell)) return;

        // Giảm 1 layer
        cell.UseLayer();

        // Nếu còn layer thì respawn, không thì để HandleLayerDepleted xử lý
        if (cell.HasLayersRemaining())
        {
            StartCoroutine(ClearAndRespawnCell(cell));
        }
    }

    // Khi cell trống (player kéo hết items ra)
    void HandleCellEmpty(Cell cell)
    {
        if (clearingCells.Contains(cell)) return;

        Debug.Log($"Cell {cell.name} is empty - clearing");

        // Giảm 1 layer
        cell.UseLayer();

        // Nếu còn layer thì respawn
        if (cell.HasLayersRemaining())
        {
            StartCoroutine(ClearAndRespawnCell(cell));
        }
        else
        {
            // Hết layer - clear vĩnh viễn
            StartCoroutine(ClearCellPermanently(cell));
        }
    }

    // Khi cell hết tất cả layers
    void HandleLayerDepleted(Cell cell)
    {
        if (clearingCells.Contains(cell)) return;

        // Cell hết mạng - chỉ clear, không respawn
        StartCoroutine(ClearCellPermanently(cell));
    }

    // Clear cell và KHÔNG respawn (hết layers)
    private IEnumerator ClearCellPermanently(Cell cell)
    {
        // Nếu ngay từ đầu đã null thì thôi
        if (cell == null) yield break;

        clearingCells.Add(cell);

        // Cache vị trí & tên ngay đầu (phòng khi sau này cell bị Destroy ở chỗ khác)
        Vector3 basePos = cell.transform.position;
        string cellName = cell.name;

        // Tắt collider của items
        List<Item> items = cell.GetItems();
        foreach (var it in items)
        {
            if (it == null) continue;
            var col = it.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // Animation bay đi
        Camera cam = Camera.main;
        Vector3 depthDir = cam != null ? (cam.transform.position - basePos).normalized : Vector3.back;
        Vector3 sideDir = GetFreeSideDirection(cell);
        Vector3 midPos = basePos + depthDir * forwardDistance;

        float t = 0f;
        while (t < clearDuration)
        {
            // 🔹 Quan trọng: nếu cell đã bị Destroy ở nơi khác thì dừng coroutine
            if (cell == null)
                yield break;

            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / clearDuration);
            float curve = clearCurve != null ? clearCurve.Evaluate(n) : n;

            if (n < 0.5f)
            {
                float p = curve / 0.5f;
                cell.transform.position = Vector3.Lerp(basePos, midPos, Mathf.Clamp01(p));
            }
            else
            {
                float p = (curve - 0.5f) / 0.5f;
                Vector3 endPos = midPos + sideDir * slideDistance;
                cell.transform.position = Vector3.Lerp(midPos, endPos, Mathf.Clamp01(p));
            }

            yield return null;
        }

        // Nếu đến đây mà cell đã bị destroy ở đâu đó thì thôi, đừng đụng nữa
        if (cell == null) yield break;

        // Destroy items
        foreach (var it in items)
        {
            if (it != null)
                Destroy(it.gameObject);
        }
        cell.ClearItems();

        // Destroy cell vĩnh viễn
        cells.Remove(cell);          // nhớ check list này không chứa null ở chỗ khác nữa
        clearingCells.Remove(cell);
        Destroy(cell.gameObject);

        Debug.Log($"Cell {cellName} permanently removed (no layers remaining)");
    }

    // Clear cell và respawn (còn layers)
    private IEnumerator ClearAndRespawnCell(Cell cell)
    {
        if (cell == null) yield break;

        clearingCells.Add(cell);

        Vector3 basePos = cell.transform.position;

        // Tắt collider của items
        List<Item> items = cell.GetItems();
        foreach (var it in items)
        {
            if (it == null) continue;
            var col = it.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // Animation bay đi
        Camera cam = Camera.main;
        Vector3 depthDir = cam != null ? (cam.transform.position - basePos).normalized : Vector3.back;
        Vector3 sideDir = GetFreeSideDirection(cell);
        Vector3 midPos = basePos + depthDir * forwardDistance;

        float t = 0f;
        while (t < clearDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / clearDuration);
            float curve = clearCurve != null ? clearCurve.Evaluate(n) : n;

            if (n < 0.5f)
            {
                float p = curve / 0.5f;
                cell.transform.position = Vector3.Lerp(basePos, midPos, Mathf.Clamp01(p));
            }
            else
            {
                float p = (curve - 0.5f) / 0.5f;
                Vector3 endPos = midPos + sideDir * slideDistance;
                cell.transform.position = Vector3.Lerp(midPos, endPos, Mathf.Clamp01(p));
            }

            yield return null;
        }

        // Destroy items
        foreach (var it in items)
        {
            if (it != null) Destroy(it.gameObject);
        }
        cell.ClearItems();

        // Ẩn cell
        cell.gameObject.SetActive(false);
        cell.transform.position = basePos;

        yield return new WaitForSeconds(0.1f);

        // Respawn
        yield return StartCoroutine(RespawnCellWithItems(cell, basePos));

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

    private IEnumerator RespawnCellWithItems(Cell cell, Vector3 basePos)
    {
        if (cell == null) yield break;

        cell.gameObject.SetActive(true);

        // Spawn từ phía sau
        Camera cam = Camera.main;
        Vector3 fromBehindDir = cam != null
            ? (basePos - cam.transform.position).normalized
            : Vector3.forward;

        Vector3 startPos = basePos + fromBehindDir * respawnOffsetZ;
        cell.transform.position = startPos;

        float t = 0f;
        while (t < respawnDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / respawnDuration);
            float curve = Mathf.SmoothStep(0f, 1f, n);

            cell.transform.position = Vector3.Lerp(startPos, basePos, curve);
            yield return null;
        }

        cell.transform.position = basePos;

        // Spawn items mới
        if (gameManager != null)
        {
            gameManager.SpawnItemsInCell(cell);
        }

        Debug.Log($"Cell {cell.name} respawned. Layers remaining: {cell.GetRemainingLayers()}/{cell.GetMaxLayers()}");
    }
}