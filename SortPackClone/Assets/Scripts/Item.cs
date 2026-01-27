using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("Item Settings")]
    public string itemType;  // Loại item: "carrot", "fries", "coke"...
    public int itemID;       // ID để phân biệt các item cùng loại

    [Header("Drag Settings")]
    [SerializeField] private float dragSpeed = 20f;
    [SerializeField] private float snapSpeed = 15f;
    [SerializeField] private float dragZOffset = -1f;  // Đưa item lên trước khi kéo


    private Vector3 dragVelocity = Vector3.zero;

    [SerializeField] private float smoothTime = 0.05f; //0.03-0,1
    // State
    private bool isDragging = false;
    private Vector3 originalPosition;
    private float originalZ;

    // References
    private Camera mainCamera;
    private Cell currentCell;      // Cell đang chứa item này
    private Cell hoveredCell;      // Cell đang hover khi kéo
    private Collider itemCollider; // 3D Collider

    // Events
    public System.Action<Item> OnItemPickedUp;
    public System.Action<Item, Cell> OnItemDropped;

    void Start()
    {
        mainCamera = Camera.main;
        itemCollider = GetComponent<Collider>();
        originalZ = transform.position.z;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse down somewhere");
        }
        // Check input
        if (Input.GetMouseButtonDown(0) && !isDragging)
        {
            TryStartDrag();
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }

        if (isDragging)
        {
            Vector3 mousePos = GetInputPosition();
            mousePos.z = originalZ + dragZOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                mousePos,
                ref dragVelocity,
                smoothTime
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
                Debug.Log($"Item {name} clicked via raycast!");
                StartDrag();
            }
        }
    }

    // ========== INPUT HANDLING ==========

    void OnMouseDown()
    {
        Debug.Log($"Item {name} clicked!");
        StartDrag();
    }

    void OnMouseUp()
    {
        Debug.Log($"Item {name} released!");
        EndDrag();
    }

    // Cho touch trên mobile
    public void StartDrag()
    {
        isDragging = true;
        originalPosition = transform.position;

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

        // Xử lý drop
        if (hoveredCell != null && hoveredCell.CanAcceptItem(this))
        {
            // Drop vào cell mới
            Cell oldCell = currentCell;

            // Remove from old cell
            if (currentCell != null)
            {
                currentCell.RemoveItem(this);
            }

            // Add to new cell
            hoveredCell.AddItem(this);
            currentCell = hoveredCell;

            // Snap vào vị trí trong cell
            //SnapToCell(hoveredCell);

            OnItemDropped?.Invoke(this, hoveredCell);
        }
        else
        {
            // Trả về vị trí cũ
            SnapToPosition(originalPosition);
        }

        // Reset sorting order
        SetSortingOrder(0);
        hoveredCell = null;
    }

    // ========== HELPER METHODS ==========

    private Vector3 GetInputPosition()
    {
        Vector3 inputPos = transform.position;

        // Tạo ray từ camera qua vị trí mouse
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Tạo plane ngang tại vị trí Z của item
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, originalZ + dragZOffset));

        float distance;
        if (plane.Raycast(ray, out distance))
        {
            inputPos = ray.GetPoint(distance);
        }

        return inputPos;
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
            if (cell != null && cell != currentCell)
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
            hoveredCell.SetHighlight(foundCell.CanAcceptItem(this));
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
}