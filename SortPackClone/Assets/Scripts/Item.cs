using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("Item Settings")]
    public string itemType;
    public int itemID;
    private int spotIndex = -1;

    [Header("Drag Settings")]
    [SerializeField] private float dragSpeed = 50f;
    [SerializeField] private float snapSpeed = 20f;
    [SerializeField] private float dragZOffset = -1f;

    // State
    private bool isDragging = false;
    private Vector3 originalPosition;
    private float originalZ;

    // References
    private Camera mainCamera;
    private Cell currentCell;
    private Cell hoveredCell;
    private Collider itemCollider;
    private ItemAnimator itemAnimator;
    private ItemOutline itemOutline;  // NEW: Viền sáng

    // Cache cell gốc khi bắt đầu drag
    private Cell dragStartCell;

    // Events
    public System.Action<Item> OnItemPickedUp;
    public System.Action<Item, Cell> OnItemDropped;

    void Start()
    {
        mainCamera = Camera.main;
        itemCollider = GetComponent<Collider>();
        itemAnimator = GetComponent<ItemAnimator>();
        itemOutline = GetComponent<ItemOutline>();  // NEW
        originalZ = transform.position.z;
    }

    void Update()
    {
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
        if (isDragging)
        {
            Vector3 targetPos = GetInputPosition();
            targetPos.z = originalZ + dragZOffset;

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
            if (hit.collider.gameObject == gameObject)
            {
                StartDrag();
            }
        }
    }

    void OnMouseDown()
    {
        StartDrag();
    }

    void OnMouseUp()
    {
        EndDrag();
    }

    public void StartDrag()
    {
        if (isDragging) return;

        isDragging = true;
        originalPosition = transform.position;

        // Cache cell gốc trước khi drag
        dragStartCell = currentCell;

        if (itemAnimator != null)
        {
            itemAnimator.StopIdleAnimation();
            itemAnimator.PlayPickUp();
        }

        // NEW: Bật viền sáng
        if (itemOutline != null)
        {
            itemOutline.ShowOutline();
        }

        SetSortingOrder(100);

        if (itemCollider != null)
            itemCollider.enabled = false;

        // 🔊 PHÁT SOUND PICK UP
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPickUp();

        OnItemPickedUp?.Invoke(this);
    }

    public void EndDrag()
    {
        if (!isDragging) return;

        isDragging = false;

        if (itemCollider != null)
            itemCollider.enabled = true;

        bool dropSuccess = false;
        Cell oldCell = dragStartCell;  // Dùng cell gốc đã cache

        if (hoveredCell != null)
        {
            if (hoveredCell == currentCell)
            {
                // Cùng cell - đổi spot
                int newSpotIndex = hoveredCell.GetNearestSpotIndex(transform.position);
                if (newSpotIndex >= 0 && newSpotIndex != spotIndex)
                {
                    currentCell.RemoveItem(this);
                    currentCell.AddItemToSpot(this, newSpotIndex);
                    dropSuccess = true;
                }
            }
            else if (hoveredCell.CanAcceptItem(this))
            {
                // Khác cell - di chuyển item sang cell mới
                if (currentCell != null)
                {
                    currentCell.RemoveItem(this);
                }

                hoveredCell.AddItemAtPosition(this, transform.position);
                currentCell = hoveredCell;
                dropSuccess = true;

                // ========== QUAN TRỌNG ==========
                // Chỉ notify cell cũ SAU KHI item đã được drop thành công vào cell mới
                if (oldCell != null && oldCell != hoveredCell)
                {
                    oldCell.NotifyItemMovedToOtherCell();
                }
                // =================================

                OnItemDropped?.Invoke(this, hoveredCell);
            }
        }

        if (dropSuccess)
        {
            if (itemAnimator != null)
            {
                itemAnimator.PlayDropBounce();
            }

            // 🔊 PHÁT SOUND DROP
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDrop();
        }
        else
        {
            // Trả về vị trí cũ
            SnapToPosition(originalPosition);

            // 🔊 PHÁT SOUND INVALID DROP
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayInvalidDrop();
        }

        // NEW: Tắt viền sáng
        if (itemOutline != null)
        {
            itemOutline.HideOutline();
        }

        SetSortingOrder(0);

        if (hoveredCell != null)
        {
            hoveredCell.SetHighlight(false);
            hoveredCell = null;
        }
    }

    // ========== HELPER METHODS ==========

    private Vector3 GetInputPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
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

            bool canAccept = false;
            if (foundCell == currentCell)
            {
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