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
    [SerializeField] private float slideDistance = 12.0f;   // bay đủ xa để ra khỏi màn hình
    [SerializeField] private float forwardDistance = 1.0f;  // lao gần camera trước khi bay
    [SerializeField] private AnimationCurve clearCurve;

    [Header("Respawn Animation")]
    [SerializeField] private float respawnDuration = 0.5f;
    // khoảng cách spawn phía SAU kệ (trên trục Z, xa camera hơn)
    [SerializeField] private float respawnOffsetZ = 2f;


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
        if (cell == null) yield break;

        clearingCells.Add(cell);

        // --- 1. Chuẩn bị ---
        Vector3 basePos = cell.transform.position;

        // Tắt collider của item
        List<Item> items = cell.GetItems();
        foreach (var it in items)
        {
            if (it == null) continue;
            var col = it.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // Hướng sâu theo trục Z (lao ra gần camera)
        Camera cam = Camera.main;
        Vector3 depthDir;
        if (cam != null)
            depthDir = (cam.transform.position - basePos).normalized; // hướng từ kệ tới camera
        else
            depthDir = Vector3.back; // fallback

        // Hướng trượt ngang (trái/phải)
        Vector3 sideDir = GetFreeSideDirection(cell);   // Vector3.left hoặc Vector3.right

        // Đích kết thúc pha 1 (chỉ đi theo Z)
        Vector3 midPos = basePos + depthDir * forwardDistance;

        float t = 0f;

        while (t < clearDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / clearDuration);
            float curve = (clearCurve != null && clearCurve.keys.Length > 0)
                ? clearCurve.Evaluate(n)
                : n;

            if (n < 0.5f)
            {
                // ===== PHA 1: chỉ bay theo Z (không đổi X/Y) =====
                float p = curve / 0.5f;    // 0..1 trong n = 0..0.5
                cell.transform.position = Vector3.Lerp(basePos, midPos, Mathf.Clamp01(p));
            }
            else
            {
                // ===== PHA 2: giữ nguyên Z, chỉ trượt ngang =====
                float p = (curve - 0.5f) / 0.5f;  // 0..1 trong n = 0.5..1
                Vector3 endPos = midPos + sideDir * slideDistance;
                cell.transform.position = Vector3.Lerp(midPos, endPos, Mathf.Clamp01(p));
            }

            yield return null;
        }

        // --- 2. Clear items trong ô ---
        foreach (var it in items)
        {
            if (it != null)
                Destroy(it.gameObject);
        }
        cell.ClearItems();

        // Ẩn cell, trả lại vị trí logic
        cell.gameObject.SetActive(false);
        cell.transform.position = basePos;

        yield return new WaitForSeconds(0.1f);

        // --- 3. Respawn từ phía sau (theo Z) + spawn item mới ---
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

        // Bật lại ô
        cell.gameObject.SetActive(true);

        // Hướng từ camera → ra phía kệ
        Camera cam = Camera.main;
        Vector3 fromBehindDir;
        if (cam != null)
            fromBehindDir = (basePos - cam.transform.position).normalized;
        else
            fromBehindDir = Vector3.forward; // fallback

        // Spawn từ phía sau (xa camera hơn)
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

        // Sau khi ô đã “chui” từ phía sau ra → spawn item mới bằng rule GameManager
        if (gameManager != null)
        {
            gameManager.SpawnItemsInCell(cell);
        }
        else
        {
            Debug.LogWarning("[BoardController] gameManager null, không spawn item cho cell mới.");
        }
    }





}
