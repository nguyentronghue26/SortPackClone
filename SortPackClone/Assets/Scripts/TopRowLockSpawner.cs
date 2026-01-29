using System.Collections.Generic;
using UnityEngine;

public class TopRowLockSpawner : MonoBehaviour
{
    [Header("Lock Cell Settings")]
    [SerializeField] private GameObject lockCellPrefab;   // Prefab Box_Two
    [SerializeField] private int lockCount = 3;           // số ô lock

    [Header("Screen Fit")]
    [SerializeField] private bool autoFitToScreen = true;
    [SerializeField] private Camera mainCamera;

    [Header("Position Settings")]
    [SerializeField, Range(0f, 1f)]
    private float verticalPosition = 0.75f;               // 0 = bottom, 1 = top (0.75 = 75% từ dưới lên)
    [SerializeField] private float cellSpacing = 0.1f;    // khoảng cách giữa các cells (sau khi scale)

    [Header("Scale Settings")]
    [SerializeField, Range(0.1f, 1f)]
    private float maxScreenWidthRatio = 0.9f;             // Max chiếm 90% chiều rộng màn hình
    [SerializeField] private float manualScale = 0.5f;    // Scale thủ công nếu không dùng auto fit

    [SerializeField] private bool autoSpawnOnStart = true;

    // lưu các lock đã spawn
    private readonly List<GameObject> spawnedLocks = new List<GameObject>();

    private void Reset()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (autoSpawnOnStart)
        {
            SpawnLocks();
        }
    }

    public void SpawnLocks()
    {
        if (lockCellPrefab == null)
        {
            Debug.LogWarning("[TopRowLockSpawner] Chưa gán lockCellPrefab!");
            return;
        }

        ClearLocks();

        if (autoFitToScreen)
        {
            SpawnWithScreenFit();
        }
        else
        {
            SpawnWithManualLayout();
        }
    }

    private void SpawnWithScreenFit()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Lấy kích thước cell từ prefab
        float cellWidth = GetCellWidth();
        float cellHeight = GetCellHeight();

        // Tính screen size
        float screenHeight = mainCamera.orthographicSize * 2f;
        float screenWidth = screenHeight * mainCamera.aspect;

        // Tính tổng width cần cho tất cả cells + spacing
        float totalCellWidth = cellWidth * lockCount;
        float totalSpacing = cellSpacing * (lockCount - 1);
        float totalWidth = totalCellWidth + totalSpacing;

        // Tính scale để fit màn hình
        float availableWidth = screenWidth * maxScreenWidthRatio;
        float scale = availableWidth / totalWidth;
        scale = Mathf.Min(scale, 1f); // Không scale lớn hơn 1

        // Apply scale cho spawner
        transform.localScale = Vector3.one * scale;

        // Tính vị trí Y
        float camY = mainCamera.transform.position.y;
        float yOffset = (verticalPosition - 0.5f) * screenHeight;
        float targetY = camY + yOffset;

        // Tính vị trí X (căn giữa)
        float targetX = mainCamera.transform.position.x;
        float targetZ = transform.position.z;

        // Đặt vị trí spawner
        transform.position = new Vector3(targetX, targetY, targetZ);

        // Spawn cells
        float scaledCellWidth = cellWidth; // local space, chưa scale
        float scaledSpacing = cellSpacing / scale; // điều chỉnh spacing theo scale
        float totalLocalWidth = scaledCellWidth * lockCount + scaledSpacing * (lockCount - 1);
        float startX = -totalLocalWidth / 2f + scaledCellWidth / 2f;

        for (int i = 0; i < lockCount; i++)
        {
            float x = startX + i * (scaledCellWidth + scaledSpacing);
            Vector3 localPos = new Vector3(x, 0f, 0f);

            GameObject lockObj = Instantiate(lockCellPrefab, transform);
            lockObj.transform.localPosition = localPos;
            lockObj.transform.localRotation = Quaternion.identity;
            lockObj.transform.localScale = Vector3.one;

            spawnedLocks.Add(lockObj);
        }

        Debug.Log($"[TopRowLockSpawner] Spawned {lockCount} locks. Scale: {scale:F2}, Y: {targetY:F2}");
    }

    private void SpawnWithManualLayout()
    {
        transform.localScale = Vector3.one * manualScale;

        float cellWidth = GetCellWidth();
        float totalWidth = cellWidth * lockCount + cellSpacing * (lockCount - 1);
        float startX = -totalWidth / 2f + cellWidth / 2f;

        for (int i = 0; i < lockCount; i++)
        {
            float x = startX + i * (cellWidth + cellSpacing);
            Vector3 localPos = new Vector3(x, 0f, 0f);

            GameObject lockObj = Instantiate(lockCellPrefab, transform);
            lockObj.transform.localPosition = localPos;
            lockObj.transform.localRotation = Quaternion.identity;
            lockObj.transform.localScale = Vector3.one;

            spawnedLocks.Add(lockObj);
        }

        Debug.Log($"[TopRowLockSpawner] Spawned {lockCount} locks (manual). Scale: {manualScale}");
    }

    private float GetCellWidth()
    {
        if (lockCellPrefab == null) return 1.6f;

        var boxCol = lockCellPrefab.GetComponent<BoxCollider>();
        if (boxCol != null)
            return boxCol.size.x;

        var sr = lockCellPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.bounds.size.x;

        return 1.6f; // default
    }

    private float GetCellHeight()
    {
        if (lockCellPrefab == null) return 1.5f;

        var boxCol = lockCellPrefab.GetComponent<BoxCollider>();
        if (boxCol != null)
            return boxCol.size.y;

        var sr = lockCellPrefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.bounds.size.y;

        return 1.5f; // default
    }

    public void ClearLocks()
    {
        foreach (var obj in spawnedLocks)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedLocks.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    public List<GameObject> GetSpawnedLocks() => spawnedLocks;
}