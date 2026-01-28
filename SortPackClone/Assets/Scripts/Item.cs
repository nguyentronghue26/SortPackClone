using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("Item Settings")]
    public string itemType;  // Loại item: "carrot", "fries", "coke"...
    public int itemID;       // ID để phân biệt các item cùng loại
    private int spotIndex = -1;  // Vị trí spot trong cell (-1 = không có)

    [Header("Drag Settings")]
    [SerializeField] private float dragSpeed = 50f;  // Tăng mạnh để mượt hơn
    [SerializeField] private float snapSpeed = 20f;
    [SerializeField] private float dragZOffset = -1f;  // Đưa item lên trước khi kéo

    // State
    private bool isDragging = false;
    private Vector3 originalPosition;
    private float originalZ;

    // References
    private Camera mainCamera;
    private Cell currentCell;      // Cell đang chứa item này
    private Cell hoveredCell;      // Cell đang hover khi kéo
    private Collider itemCollider; // 3D Collider
    private ItemAnimator itemAnimator;

    // Events
    public System.Action<Item> OnItemPickedUp;
    public System.Action<Item, Cell> OnItemDropped;

    void Start()
    {
        mainCamera = Camera.main;
        itemCollider = GetComponent<Collider>();
        itemAnimator = GetComponent<ItemAnimator>();
        originalZ = transform.position.z;
    }

    void Update()
    {
        // Check input
        if (Input.GetMouseButtonDown(0) && !isDragging)
        {
            TryStartDrag();
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }
    }

    void LateUpdate()
    {
        // Drag trong LateUpdate để mượt hơn
        if (isDragging)
        {
            Vector3 targetPos = GetInputPosition();
            targetPos.z = originalZ + dragZOffset;

            // Lerp với tốc độ cao để mượt và responsive
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                dragSpeed * Time.deltaTime
            );

            CheckHoveredCell();
        }
    }

    private void TryStartDrag()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            // Check xem có click vào chính item này không
            if (hit.collider.gameObject == gameObject)
            {
                StartDrag();
            }
        }
    }

    // ========== INPUT HANDLING ==========

    void OnMouseDown()
    {
        StartDrag();
    }

    void OnMouseUp()
    {
        EndDrag();
    }

    // Cho touch trên mobile
    public void StartDrag()
    {
        if (isDragging) return;  // Tránh gọi 2 lần

        isDragging = true;
        originalPosition = transform.position;

        // Animation pick up
        if (itemAnimator != null)
        {
            itemAnimator.StopIdleAnimation();
            itemAnimator.PlayPickUp();
        }

        // Đưa lên layer trên cùng
        SetSortingOrder(100);

        // Tắt collider để không block raycast
        if (itemCollider != null)
            itemCollider.enabled = false;

        OnItemPickedUp?.Invoke(this);
    }

    public void EndDrag()
    {
        if (!isDragging) return;

        isDragging = false;

        // Bật lại collider
        if (itemCollider != null)
            itemCollider.enabled = true;

        bool dropSuccess = false;
        Cell oldCell = currentCell;

        // Xử lý drop
        if (hoveredCell != null)
        {
            if (hoveredCell == currentCell)
            {
                // Cùng cell - đổi spot
                int newSpotIndex = hoveredCell.GetNearestSpotIndex(transform.position);
                if (newSpotIndex >= 0 && newSpotIndex != spotIndex)
                {
                    // Remove khỏi spot cũ, add vào spot mới
                    currentCell.RemoveItem(this);
                    currentCell.AddItemToSpot(this, newSpotIndex);
                    dropSuccess = true;
                }
            }
            else if (hoveredCell.CanAcceptItem(this))
            {
                // Khác cell
                if (currentCell != null)
                {
                    currentCell.RemoveItem(this);
                }

                hoveredCell.AddItemAtPosition(this, transform.position);
                currentCell = hoveredCell;
                dropSuccess = true;

                // Check cell cũ có trống không
                if (oldCell != null && oldCell != hoveredCell)
                {
                    oldCell.CheckEmpty();
                }

                OnItemDropped?.Invoke(this, hoveredCell);
            }
        }

        if (dropSuccess)
        {
            // Animation drop bounce
            if (itemAnimator != null)
            {
                itemAnimator.PlayDropBounce();
            }
        }
        else
        {
            // Trả về vị trí cũ
            SnapToPosition(originalPosition);
        }

        // Reset sorting order
        SetSortingOrder(0);

        // Clear highlight
        if (hoveredCell != null)
        {
            hoveredCell.SetHighlight(false);
            hoveredCell = null;
        }
    }

    // ========== HELPER METHODS ==========

    private Vector3 GetInputPosition()
    {
        // Tạo ray từ camera qua vị trí mouse
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Tạo plane ngang tại vị trí Z của item
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, originalZ + dragZOffset));

        float distance;
        if (plane.Raycast(ray, out distance))
        {
            return ray.GetPoint(distance);
        }

        return transform.position;
    }

    private void CheckHoveredCell()
    {
        // 3D Raycast từ camera qua mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        Cell foundCell = null;

        foreach (var hit in hits)
        {
            Cell cell = hit.collider.GetComponent<Cell>();
            if (cell != null)
            {
                foundCell = cell;
                break;
            }
        }

        if (foundCell != null)
        {
            if (hoveredCell != null && hoveredCell != foundCell)
            {
                hoveredCell.SetHighlight(false);
            }

            hoveredCell = foundCell;

            // Check có thể drop không (kể cả cùng cell nhưng khác spot)
            bool canAccept = false;
            if (foundCell == currentCell)
            {
                // Cùng cell - check có spot trống khác không
                canAccept = foundCell.GetEmptySpotCount() > 0;
            }
            else
            {
                canAccept = foundCell.CanAcceptItem(this);
            }

            hoveredCell.SetHighlight(canAccept);
        }
        else
        {
            if (hoveredCell != null)
            {
                hoveredCell.SetHighlight(false);
                hoveredCell = null;
            }
        }
    }

    private void SnapToCell(Cell cell)
    {
        Vector3 targetPos = cell.GetNextItemPosition();
        targetPos.z = originalZ;
        SnapToPosition(targetPos);
    }

    private void SnapToPosition(Vector3 position)
    {
        StartCoroutine(SmoothSnapCoroutine(position));
    }

    private System.Collections.IEnumerator SmoothSnapCoroutine(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, target, snapSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    private void SetSortingOrder(int order)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = order;
        }
    }

    // ========== PUBLIC METHODS ==========

    public void SetCell(Cell cell)
    {
        currentCell = cell;
    }

    public Cell GetCurrentCell()
    {
        return currentCell;
    }

    public void Initialize(string type, int id)
    {
        itemType = type;
        itemID = id;
    }

    public ItemAnimator GetAnimator()
    {
        return itemAnimator;
    }

    public void SetSpotIndex(int index)
    {
        spotIndex = index;
    }

    public int GetSpotIndex()
    {
        return spotIndex;
    }
}