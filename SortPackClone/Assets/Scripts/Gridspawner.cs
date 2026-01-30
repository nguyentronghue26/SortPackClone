using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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
    [SerializeField] private float screenPadding = 0.25f;  // Tăng padding để grid nhỏ lại
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float verticalOffset = 0.5f;  // Đẩy grid lên cao hơn
    [Header("Auto Spawn")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [Header("Expand Animation")]
    [SerializeField] private float expandAnimDuration = 0.3f;

    [Header("Clear Animation")]
    [SerializeField] private float clearFlyZOffset = -1.0f;   // Bay về phía camera (Z nhỏ lại)
    [SerializeField] private float clearFlyDuration = 0.25f;
    [SerializeField] private float clearSlideDistance = 8f;   // Trượt ra viền màn hình
    [SerializeField] private float clearSlideDuration = 0.35f;
    [SerializeField] private Ease clearFlyEase = Ease.OutQuad;
    [SerializeField] private Ease clearSlideEase = Ease.InQuad;
    [Header("Layer Visual")]
    [SerializeField] private float zScaleReducePerLayer = 0.3f;
    
    // Dynamic grid storage
    private List<List<GameObject>> cellRows = new List<List<GameObject>>();
    private int currentRows = 0;
    private float actualCellWidth;
    private float actualCellHeight;

    void Start()
    {
        if (autoSpawnOnStart)
        {
            SpawnGrid();
        }
    }
    public void SpawnGridForLevel(SortPackLevelData levelData)
    {
        if (levelData == null)
        {
            Debug.LogError("GridSpawner: levelData null!");
            return;
        }

        // X = số hàng (Row), Y = số cột (Column)
        columns = levelData.sizeY;
        initialRows = levelData.sizeX;

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

        // Lấy scale nhỏ hơn để vừa khít
        float finalScale = Mathf.Min(scaleX, scaleY);

        transform.localScale = Vector3.one * finalScale;

        // Đặt vị trí - đẩy lên cao hơn
        Vector3 camPos = mainCamera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y + verticalOffset, camPos.z + 10f);

        Debug.Log($"Grid scaled to {finalScale:F2}, offset Y: {verticalOffset}");
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

        int row = oldCell.Row;
        int col = oldCell.Column;
        GameObject oldCellObj = oldCell.gameObject;

        // Lưu transform gốc
        Vector3 originalWorldPos = oldCellObj.transform.position;
        Vector3 originalScale = oldCellObj.transform.localScale;
        Quaternion originalRotation = oldCellObj.transform.rotation;

        // Xác định hướng trượt sang trái / phải:
        float centerCol = (columns - 1) / 2f;
        int dirSign = (col <= centerCol) ? -1 : 1;   // trái = -1, phải = +1
        Vector3 sideDir = Vector3.right * dirSign;

        // Vị trí bay theo Z
        Vector3 flyPos = originalWorldPos + new Vector3(0f, 0f, clearFlyZOffset);
        Vector3 slidePos = flyPos + sideDir * clearSlideDistance;

        // Tạo sequence DOTween
        Sequence seq = DOTween.Sequence();

        // Bước 1: Bay theo trục Z
        seq.Append(
            oldCellObj.transform.DOMove(flyPos, clearFlyDuration)
                .SetEase(clearFlyEase)
        );

        // Bước 2: Trượt qua trái/phải ra mép rồi biến mất
        seq.Append(
            oldCellObj.transform.DOMove(slidePos, clearSlideDuration)
                .SetEase(clearSlideEase)
        );

        // OnComplete: xoá cell cũ, spawn cell mới ở cùng vị trí nhưng MỎNG Z HƠN
        seq.OnComplete(() =>
        {
            // Xoá cell cũ khỏi list
            if (row < cellRows.Count && col < cellRows[row].Count)
            {
                cellRows[row][col] = null;
            }

            cellsBeingReplaced.Remove(oldCell);

            if (oldCellObj != null)
            {
                Destroy(oldCellObj);
            }

            // ❌ KHÔNG tụt Z nữa → dùng đúng vị trí cũ
            Vector3 spawnWorldPos = originalWorldPos;

            GameObject newCellObj = Instantiate(cellPrefab, spawnWorldPos, originalRotation, transform);

            // ✅ Giảm độ dày theo trục Z thay vì dời position
            Vector3 newScale = originalScale;
            newScale.z = Mathf.Max(originalScale.z - zScaleReducePerLayer, 0.05f); // tránh về 0
            newCellObj.transform.localScale = newScale;

            newCellObj.name = $"Cell_{row}_{col}";

            Cell newCell = newCellObj.GetComponent<Cell>();
            if (newCell != null)
            {
                newCell.Row = row;
                newCell.Column = col;
            }

            // Cập nhật list
            if (row < cellRows.Count && col < cellRows[row].Count)
            {
                cellRows[row][col] = newCellObj;
            }

            // Gọi callback cho GameManager spawn item layer dưới (giữ nguyên logic cũ)
            onComplete?.Invoke(newCell);
        });
    }


    //private System.Collections.IEnumerator ReplaceCellCoroutine(Cell oldCell, System.Action<Cell> onComplete)
    //{
    //    int row = oldCell.Row;
    //    int col = oldCell.Column;
    //    GameObject oldCellObj = oldCell.gameObject;

    //    // Lưu thông tin của cell cũ TRƯỚC khi xóa
    //    Vector3 originalWorldPos = oldCellObj.transform.position;
    //    Vector3 originalScale = oldCellObj.transform.localScale;
    //    Quaternion originalRotation = oldCellObj.transform.rotation;

    //    // 1. Animation: Cell cũ bay lên và biến mất
    //    float flyUpDuration = 0.3f;
    //    float elapsed = 0f;
    //    Vector3 startPos = originalWorldPos;
    //    Vector3 endPos = originalWorldPos + Vector3.up * 2f;

    //    while (elapsed < flyUpDuration)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = elapsed / flyUpDuration;

    //        if (oldCellObj != null)
    //        {
    //            oldCellObj.transform.position = Vector3.Lerp(startPos, endPos, t);
    //            oldCellObj.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
    //        }

    //        yield return null;
    //    }

    //    // Xóa cell cũ khỏi list
    //    if (row < cellRows.Count && col < cellRows[row].Count)
    //    {
    //        cellRows[row][col] = null;
    //    }

    //    // Xóa khỏi tracking set
    //    cellsBeingReplaced.Remove(oldCell);

    //    // DESTROY CELL CŨ
    //    if (oldCellObj != null)
    //    {
    //        Destroy(oldCellObj);
    //    }

    //    // Đợi end of frame để đảm bảo cell cũ đã bị xóa
    //    yield return new WaitForEndOfFrame();

    //    // 2. Spawn cell mới tại vị trí tụt vào trong (Z + 0.5 để ở hàng 2)
    //    Vector3 spawnWorldPos = new Vector3(
    //        originalWorldPos.x,
    //        originalWorldPos.y,
    //        originalWorldPos.z + 0.5f  // Tụt sâu để ở hàng 2
    //    );

    //    GameObject newCellObj = Instantiate(cellPrefab, spawnWorldPos, originalRotation, transform);
    //    newCellObj.transform.localScale = originalScale;
    //    newCellObj.name = $"Cell_{row}_{col}";

    //    Cell newCell = newCellObj.GetComponent<Cell>();
    //    if (newCell != null)
    //    {
    //        newCell.Row = row;
    //        newCell.Column = col;
    //    }

    //    // Cập nhật list
    //    if (row < cellRows.Count && col < cellRows[row].Count)
    //    {
    //        cellRows[row][col] = newCellObj;
    //    }

    //    // 3. Animation: Cell mới scale từ nhỏ lên to (không di chuyển Z)
    //    float pushDuration = 0.4f;
    //    elapsed = 0f;

    //    // Scale từ 0 lên full
    //    Vector3 startScale = Vector3.zero;

    //    newCellObj.transform.localScale = startScale;

    //    while (elapsed < pushDuration)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = Mathf.SmoothStep(0, 1, elapsed / pushDuration);
    //        newCellObj.transform.localScale = Vector3.Lerp(startScale, originalScale, t);
    //        yield return null;
    //    }

    //    newCellObj.transform.localScale = originalScale;
    //    // Giữ nguyên position tụt sâu (Z + 0.5)

    //    // Callback
    //    onComplete?.Invoke(newCell);
    //}

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
    /// <summary>
    /// Cell bay đi và biến mất, KHÔNG spawn cell mới
    /// </summary>
    public void RemoveCellWithAnimation(Cell cell, System.Action onComplete)
    {
        if (cell == null || cellsBeingReplaced.Contains(cell))
        {
            onComplete?.Invoke();
            return;
        }

        cellsBeingReplaced.Add(cell);

        int row = cell.Row;
        int col = cell.Column;
        GameObject cellObj = cell.gameObject;

        Vector3 originalWorldPos = cellObj.transform.position;
        Vector3 originalScale = cellObj.transform.localScale;

        // Animation: Cell bay lên và biến mất
        float flyUpDuration = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = originalWorldPos;
        Vector3 endPos = originalWorldPos + Vector3.up * 2f;

        StartCoroutine(RemoveCellCoroutine(cell, cellObj, row, col, startPos, endPos, originalScale, flyUpDuration, onComplete));
    }

    private System.Collections.IEnumerator RemoveCellCoroutine(Cell cell, GameObject cellObj, int row, int col,
        Vector3 startPos, Vector3 endPos, Vector3 originalScale, float duration, System.Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (cellObj != null)
            {
                cellObj.transform.position = Vector3.Lerp(startPos, endPos, t);
                cellObj.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            }

            yield return null;
        }

        // Xóa cell khỏi list
        if (row < cellRows.Count && col < cellRows[row].Count)
        {
            cellRows[row][col] = null;
        }

        cellsBeingReplaced.Remove(cell);

        // Destroy cell
        if (cellObj != null)
        {
            Destroy(cellObj);
        }

        // Callback
        onComplete?.Invoke();
    }
}