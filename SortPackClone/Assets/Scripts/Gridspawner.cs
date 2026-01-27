using System.Collections.Generic;
using UnityEngine;

public class GridSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject cellPrefab;

    [Header("Grid Settings")]
    [Range(1, 3)]
    [SerializeField] private int columns = 3;
    [Range(1, 7)]
    [SerializeField] private int initialRows = 3;  // Số hàng ban đầu
    [SerializeField] private int maxRows = 7;      // Số hàng tối đa

    [Header("Spacing Settings")]
    [SerializeField] private bool autoDetectSize = false;
    [SerializeField] private float cellWidth = 2.1f;
    [SerializeField] private float cellHeight = 1.5f;
    [SerializeField] private float overlapAmount = 0.15f;

    [Header("Alignment")]
    [SerializeField] private bool centerGrid = true;

    [Header("Screen Fit")]
    [SerializeField] private bool autoFitToScreen = true;
    [SerializeField] private float screenPadding = 0.1f;
    [SerializeField] private Camera mainCamera;

    [Header("Expand Animation")]
    [SerializeField] private float expandAnimDuration = 0.3f;

    // Dynamic grid storage
    private List<List<GameObject>> cellRows = new List<List<GameObject>>();
    private int currentRows = 0;
    private float actualCellWidth;
    private float actualCellHeight;

    void Start()
    {
        SpawnGrid();
    }

    public void SpawnGrid()
    {
        ClearGrid();
        CalculateCellSize();

        cellRows.Clear();
        currentRows = 0;

        // Spawn hàng ban đầu (từ dưới lên)
        for (int row = 0; row < initialRows; row++)
        {
            AddRowInternal();
        }

        // Recenter sau khi spawn xong
        RecenterGrid();

        Debug.Log($"Grid spawned: {columns}x{currentRows} | Cell size: {actualCellWidth:F2} x {actualCellHeight:F2}");

        if (autoFitToScreen)
        {
            FitGridToScreen();
        }
    }

    // Thêm 1 hàng mới ở DƯỚI cùng
    public List<GameObject> AddRow()
    {
        if (currentRows >= maxRows)
        {
            Debug.Log("Đã đạt số hàng tối đa!");
            return null;
        }

        List<GameObject> newCells = AddRowAtBottom();

        // Re-fit grid
        if (autoFitToScreen)
        {
            FitGridToScreen();
        }

        return newCells;
    }

    // Thêm hàng mới ở dưới cùng
    private List<GameObject> AddRowAtBottom()
    {
        List<GameObject> newRowCells = new List<GameObject>();

        float spacingX = actualCellWidth - overlapAmount;
        float spacingY = actualCellHeight - overlapAmount;

        float offsetX = centerGrid ? (columns - 1) * spacingX / 2f : 0f;

        int newRowIndex = currentRows;  // Hàng mới ở dưới cùng

        for (int col = 0; col < columns; col++)
        {
            float posX = col * spacingX - offsetX;

            // Tính Y position - hàng dưới cùng
            float offsetY = centerGrid ? (currentRows) * spacingY / 2f : 0f;
            float posY = -newRowIndex * spacingY + offsetY;

            Vector3 targetPos = new Vector3(posX, posY, 0f);

            // Spawn từ dưới và animate lên
            Vector3 startPos = targetPos + Vector3.down * 3f;
            GameObject cell = Instantiate(cellPrefab, transform.position + startPos, Quaternion.identity, transform);
            StartCoroutine(AnimateCellMove(cell, targetPos));

            cell.name = $"Cell_{newRowIndex}_{col}";

            Cell cellScript = cell.GetComponent<Cell>();
            if (cellScript != null)
            {
                cellScript.Row = newRowIndex;
                cellScript.Column = col;
            }

            newRowCells.Add(cell);
        }

        // Thêm hàng mới vào cuối list
        cellRows.Add(newRowCells);
        currentRows++;

        // Recenter grid sau animation
        StartCoroutine(DelayedRecenter());

        return newRowCells;
    }

    private System.Collections.IEnumerator DelayedRecenter()
    {
        yield return new WaitForSeconds(expandAnimDuration + 0.1f);
        RecenterGrid();

        if (autoFitToScreen)
        {
            FitGridToScreen();
        }
    }

    private System.Collections.IEnumerator AnimateCellMove(GameObject cell, Vector3 targetLocalPos)
    {
        Vector3 startPos = cell.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < expandAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / expandAnimDuration);
            cell.transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, t);
            yield return null;
        }

        cell.transform.localPosition = targetLocalPos;
    }

    // Dùng cho spawn ban đầu (không animate)
    private void AddRowInternal()
    {
        float spacingX = actualCellWidth - overlapAmount;
        float spacingY = actualCellHeight - overlapAmount;

        float offsetX = centerGrid ? (columns - 1) * spacingX / 2f : 0f;

        int rowIndex = currentRows;
        List<GameObject> newRowCells = new List<GameObject>();

        for (int col = 0; col < columns; col++)
        {
            float posX = col * spacingX - offsetX;
            float posY = -rowIndex * spacingY;

            Vector3 position = transform.position + new Vector3(posX, posY, 0f);
            GameObject cell = Instantiate(cellPrefab, position, Quaternion.identity, transform);

            cell.name = $"Cell_{rowIndex}_{col}";

            Cell cellScript = cell.GetComponent<Cell>();
            if (cellScript != null)
            {
                cellScript.Row = rowIndex;
                cellScript.Column = col;
            }

            newRowCells.Add(cell);
        }

        cellRows.Add(newRowCells);
        currentRows++;
    }

    private void RecenterGrid()
    {
        if (!centerGrid) return;

        float spacingY = actualCellHeight - overlapAmount;
        float totalHeight = (currentRows - 1) * spacingY;
        float offsetY = totalHeight / 2f;

        // Di chuyển tất cả cells để căn giữa theo Y
        for (int row = 0; row < cellRows.Count; row++)
        {
            for (int col = 0; col < cellRows[row].Count; col++)
            {
                GameObject cell = cellRows[row][col];
                if (cell != null)
                {
                    Vector3 pos = cell.transform.localPosition;
                    float spacingX = actualCellWidth - overlapAmount;
                    float offsetX = (columns - 1) * spacingX / 2f;

                    pos.x = col * spacingX - offsetX;
                    pos.y = -row * spacingY + offsetY;

                    cell.transform.localPosition = pos;
                }
            }
        }
    }

    private void FitGridToScreen()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("No camera found for auto-fit!");
            return;
        }

        float spacingX = actualCellWidth - overlapAmount;
        float spacingY = actualCellHeight - overlapAmount;

        float gridWidth = columns * spacingX;
        float gridHeight = currentRows * spacingY;

        float screenHeight = mainCamera.orthographicSize * 2f;
        float screenWidth = screenHeight * mainCamera.aspect;

        float availableWidth = screenWidth * (1f - screenPadding * 2f);
        float availableHeight = screenHeight * (1f - screenPadding * 2f);

        float scaleX = availableWidth / gridWidth;
        float scaleY = availableHeight / gridHeight;

        float finalScale = Mathf.Min(scaleX, scaleY);

        transform.localScale = Vector3.one * finalScale;
        transform.position = mainCamera.transform.position + new Vector3(0, 0, 10f);

        Debug.Log($"Grid scaled to {finalScale:F2} to fit screen");
    }

    private void CalculateCellSize()
    {
        if (!autoDetectSize || cellPrefab == null)
        {
            actualCellWidth = cellWidth;
            actualCellHeight = cellHeight;
            return;
        }

        Renderer renderer = cellPrefab.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            actualCellWidth = renderer.bounds.size.x;
            actualCellHeight = renderer.bounds.size.y;
            return;
        }

        Collider collider = cellPrefab.GetComponentInChildren<Collider>();
        if (collider != null)
        {
            actualCellWidth = collider.bounds.size.x;
            actualCellHeight = collider.bounds.size.y;
            return;
        }

        SpriteRenderer spriteRenderer = cellPrefab.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            actualCellWidth = spriteRenderer.bounds.size.x;
            actualCellHeight = spriteRenderer.bounds.size.y;
            return;
        }

        actualCellWidth = cellWidth;
        actualCellHeight = cellHeight;
    }

    public void ClearGrid()
    {
        foreach (Transform child in transform)
        {
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
        cellRows.Clear();
        currentRows = 0;
    }

    public GameObject GetCell(int col, int row)
    {
        if (row < 0 || row >= cellRows.Count || col < 0 || col >= columns)
            return null;
        return cellRows[row][col];
    }

    // Trả về tất cả cells dưới dạng 2D array để tương thích với code cũ
    public GameObject[,] GetAllCells()
    {
        if (cellRows.Count == 0) return null;

        GameObject[,] cells = new GameObject[columns, currentRows];

        for (int row = 0; row < cellRows.Count; row++)
        {
            for (int col = 0; col < cellRows[row].Count; col++)
            {
                cells[col, row] = cellRows[row][col];
            }
        }

        return cells;
    }

    public int GetCurrentRows() => currentRows;
    public int GetMaxRows() => maxRows;
    public int GetColumns() => columns;

    // Tracking cells đang được replace để tránh gọi 2 lần
    private HashSet<Cell> cellsBeingReplaced = new HashSet<Cell>();

    // Cell cũ bay lên biến mất, cell mới từ dưới đẩy lên
    public void ReplaceCellWithNewFromBelow(Cell oldCell, System.Action<Cell> onComplete)
    {
        // Tránh gọi 2 lần cho cùng 1 cell
        if (oldCell == null || cellsBeingReplaced.Contains(oldCell))
        {
            onComplete?.Invoke(null);
            return;
        }

        cellsBeingReplaced.Add(oldCell);
        StartCoroutine(ReplaceCellCoroutine(oldCell, onComplete));
    }

    private System.Collections.IEnumerator ReplaceCellCoroutine(Cell cell, System.Action<Cell> onComplete)
    {
        if (cell == null)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        // Nếu đã xử lý cell này rồi thì thôi
        if (cellsBeingReplaced.Contains(cell))
        {
            onComplete?.Invoke(cell);
            yield break;
        }

        cellsBeingReplaced.Add(cell);

        Transform tr = cell.transform;

        // Lưu trạng thái ban đầu
        Vector3 originalWorldPos = tr.position;
        Vector3 originalLocalScale = tr.localScale;

        // 1. Cell cũ bay lên và nhỏ dần
        float flyUpDuration = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = originalWorldPos;
        Vector3 endPos = originalWorldPos + Vector3.up * 2f;

        while (elapsed < flyUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyUpDuration;

            tr.position = Vector3.Lerp(startPos, endPos, t);
            tr.localScale = Vector3.Lerp(originalLocalScale, Vector3.zero, t);

            yield return null;
        }

        // 2. Đưa cell về đúng vị trí X,Y nhưng "tụt vào trong" Z + 0.2
        float zOffset = 0.2f; // muốn sâu hơn thì tăng số này
        Vector3 insidePos = new Vector3(
            originalWorldPos.x,
            originalWorldPos.y,
            originalWorldPos.z + zOffset
        );

        tr.position = insidePos;
        tr.localScale = Vector3.zero;   // chuẩn bị pop ra từ 0

        // 3. Pop cell mới từ 0 lên full scale (không đổi vị trí nữa)
        float popDuration = 0.4f;
        elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / popDuration);
            tr.localScale = Vector3.Lerp(Vector3.zero, originalLocalScale, t);
            yield return null;
        }

        tr.localScale = originalLocalScale;

        // Kết thúc
        cellsBeingReplaced.Remove(cell);
        onComplete?.Invoke(cell);
    }


    [ContextMenu("Spawn Grid")]
    private void SpawnGridEditor() => SpawnGrid();

    [ContextMenu("Clear Grid")]
    private void ClearGridEditor() => ClearGrid();

    [ContextMenu("Add Row")]
    private void AddRowEditor() => AddRow();

    private void OnDrawGizmosSelected()
    {
        if (cellPrefab == null) return;

        float previewWidth = cellWidth - overlapAmount;
        float previewHeight = cellHeight - overlapAmount;

        float offsetX = centerGrid ? (columns - 1) * previewWidth / 2f : 0f;
        float offsetY = centerGrid ? (initialRows - 1) * previewHeight / 2f : 0f;

        Gizmos.color = Color.cyan;

        for (int row = 0; row < initialRows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float posX = col * previewWidth - offsetX;
                float posY = -row * previewHeight + offsetY;

                Vector3 position = transform.position + new Vector3(posX, posY, 0f);
                Gizmos.DrawWireCube(position, new Vector3(cellWidth * 0.95f, cellHeight * 0.95f, 0.1f));
            }
        }
    }
}