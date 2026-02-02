using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class LockedCell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject lockContainer;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Cell cell;

    [Header("Settings")]
    [SerializeField] private bool startLocked = true;

    [Header("Clear Animation (giống GridSpawner)")]
    [SerializeField] private float clearFlyZOffset = -1.0f;
    [SerializeField] private float clearFlyDuration = 0.25f;
    [SerializeField] private float clearSlideDistance = 8f;
    [SerializeField] private float clearSlideDuration = 0.35f;
    [SerializeField] private Ease clearFlyEase = Ease.OutQuad;
    [SerializeField] private Ease clearSlideEase = Ease.InQuad;

    [Header("Respawn Animation")]
    [SerializeField] private float respawnDelay = 0.2f;
    [SerializeField] private float respawnDuration = 0.4f;
    [SerializeField] private Ease respawnEase = Ease.OutBack;

    // State
    private bool isLocked = true;
    private bool isAnimating = false;
    private bool isMerging = false;
    private Collider cellCollider;

    // Cache original values
    private Vector3 originalLocalPosition;
    private Vector3 originalWorldPosition;  // NEW: cache world position
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Transform originalParent;  // NEW: cache parent

    // Cache lock visuals original position
    private Vector3 lockIconOriginalLocalPos;
    private Vector3 lockContainerOriginalLocalPos;

    // Events
    public System.Action<LockedCell> OnUnlocked;
    public System.Action<LockedCell> OnRelocked;
    public System.Action<LockedCell> OnMergeComplete;

    // Property để GameManager biết đây là LockedCell
    public bool IsLockedCell => true;

    void Awake()
    {
        if (cell == null)
            cell = GetComponent<Cell>();

        cellCollider = GetComponent<Collider>();

        // KHÔNG cache position ở đây vì spawner chưa đặt vị trí
        // Sẽ cache trong Start()

        if (lockContainer == null)
        {
            Transform lock3 = transform.Find("Lock_3");
            if (lock3 != null)
                lockContainer = lock3.gameObject;
        }

        if (lockIcon == null)
        {
            Transform lockTrans = transform.Find("Lock_3/Lock");
            if (lockTrans == null)
                lockTrans = transform.Find("Lock");
            if (lockTrans != null)
                lockIcon = lockTrans.gameObject;
        }

        if (lockIcon != null)
        {
            // Cache vị trí gốc của lockIcon từ prefab
            lockIconOriginalLocalPos = lockIcon.transform.localPosition;

            Collider col = lockIcon.GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider boxCol = lockIcon.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(1f, 1f, 0.5f);
            }
        }

        // Cache vị trí gốc của lockContainer
        if (lockContainer != null)
        {
            lockContainerOriginalLocalPos = lockContainer.transform.localPosition;
        }
    }

    void Start()
    {
        // Cache position SAU KHI spawner đã đặt vị trí
        CacheOriginalTransform();

        if (startLocked)
        {
            Lock();
        }
        else
        {
            Unlock();
        }

        // Subscribe to cell sorted event
        if (cell != null)
        {
            cell.OnCellSorted += HandleCellSorted;
        }
    }

    /// <summary>
    /// Cache vị trí gốc - gọi sau khi spawner đã đặt vị trí
    /// </summary>
    private void CacheOriginalTransform()
    {
        originalLocalPosition = transform.localPosition;
        originalWorldPosition = transform.position;
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
        originalParent = transform.parent;

        Debug.Log($"[LockedCell] {name} cached position: local={originalLocalPosition}, world={originalWorldPosition}");
    }

    void OnDestroy()
    {
        if (cell != null)
        {
            cell.OnCellSorted -= HandleCellSorted;
        }

        DOTween.Kill(transform);
        DOTween.Kill(lockContainer);
        DOTween.Kill(lockIcon);
    }

    // ========== INPUT HANDLING ==========

    void Update()
    {
        if (isLocked && !isAnimating && !isMerging && Input.GetMouseButtonDown(0))
        {
            CheckLockClick();
        }
    }

    private void CheckLockClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.transform.gameObject == lockIcon ||
                hit.transform.IsChildOf(lockContainer?.transform) ||
                hit.transform.gameObject == lockContainer)
            {
                TryUnlock();
            }
        }
    }

    public void TryUnlock()
    {
        if (!isLocked || isAnimating || isMerging) return;
        PlayUnlockAnimation();
    }

    // ========== LOCK/UNLOCK ==========

    public void Lock()
    {
        isLocked = true;

        if (lockContainer != null)
        {
            lockContainer.SetActive(true);
            lockContainer.transform.localPosition = lockContainerOriginalLocalPos;
            lockContainer.transform.localScale = Vector3.one;
        }

        if (lockIcon != null)
        {
            lockIcon.SetActive(true);
            lockIcon.transform.localPosition = lockIconOriginalLocalPos;
            lockIcon.transform.localScale = Vector3.one;
            lockIcon.transform.localRotation = Quaternion.identity;
        }

        // Disable cell collider - không cho kéo items vào
        if (cellCollider != null)
            cellCollider.enabled = false;

        // Disable tất cả items trong cell (nếu có)
        if (cell != null)
        {
            foreach (var item in cell.GetItems())
            {
                if (item == null) continue;
                var col = item.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                item.enabled = false;
            }
        }

        Debug.Log($"{name} LOCKED");
    }

    public void Unlock()
    {
        isLocked = false;

        if (lockContainer != null)
            lockContainer.SetActive(false);

        if (lockIcon != null)
            lockIcon.SetActive(false);

        // Enable cell collider - cho phép kéo items vào
        if (cellCollider != null)
            cellCollider.enabled = true;

        // Enable tất cả items trong cell (nếu có)
        if (cell != null)
        {
            foreach (var item in cell.GetItems())
            {
                if (item == null) continue;
                var col = item.GetComponent<Collider>();
                if (col != null) col.enabled = true;
                item.enabled = true;
            }
        }

        OnUnlocked?.Invoke(this);
        Debug.Log($"{name} UNLOCKED - hoạt động như cell thường");
    }

    // ========== CELL SORTED (MERGE) HANDLING ==========

    private void HandleCellSorted(Cell sortedCell)
    {
        if (sortedCell != cell) return;
        if (isMerging) return;

        Debug.Log($"LockedCell {name}: Cell sorted! Starting merge animation...");
        StartCoroutine(PlayMergeAndRespawn());
    }

    /// <summary>
    /// Animation: Cell + Items bay đi CÙNG NHAU → Respawn locked cell
    /// Items là child của cell nên sẽ tự động bay theo!
    /// </summary>
    private IEnumerator PlayMergeAndRespawn()
    {
        isMerging = true;

        // Disable input cho cell và items
        if (cellCollider != null)
            cellCollider.enabled = false;

        // Disable tất cả items (không cho drag trong khi animation)
        List<Item> items = cell.GetItems();
        foreach (var item in items)
        {
            if (item == null) continue;
            item.enabled = false;
            var col = item.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // ========== CELL + ITEMS BAY ĐI CÙNG NHAU ==========
        // Items là child của cell → khi cell bay thì items tự động bay theo!

        Debug.Log($"LockedCell {name}: Cell + Items flying out together...");

        Vector3 originalWorldPos = transform.position;

        // Xác định hướng slide (trái/phải dựa vào vị trí)
        int dirSign = (transform.localPosition.x <= 0) ? -1 : 1;
        Vector3 sideDir = Vector3.right * dirSign;

        // Vị trí bay theo Z
        Vector3 flyPos = originalWorldPos + new Vector3(0f, 0f, clearFlyZOffset);
        Vector3 slidePos = flyPos + sideDir * clearSlideDistance;

        // Tạo sequence animation cho CELL (items sẽ bay theo vì là child)
        Sequence cellSeq = DOTween.Sequence();

        // Bước 1: Bay theo trục Z (về phía camera)
        cellSeq.Append(
            transform.DOMove(flyPos, clearFlyDuration)
                .SetEase(clearFlyEase)
        );

        // Bước 2: Trượt qua trái/phải + scale nhỏ lại
        cellSeq.Append(
            transform.DOMove(slidePos, clearSlideDuration)
                .SetEase(clearSlideEase)
        );
        cellSeq.Join(
            transform.DOScale(Vector3.zero, clearSlideDuration)
                .SetEase(clearSlideEase)
        );

        yield return cellSeq.WaitForCompletion();

        // ========== DESTROY ITEMS SAU KHI ANIMATION XONG ==========
        foreach (var item in items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        // Clear cell references
        cell.ClearItemsWithoutDestroy();

        OnMergeComplete?.Invoke(this);

        // ========== RESPAWN CELL (LOCKED, NO ITEMS) ==========
        yield return new WaitForSeconds(respawnDelay);

        Debug.Log($"LockedCell {name}: Respawning as locked cell at original position...");

        // Đảm bảo parent vẫn đúng
        if (originalParent != null && transform.parent != originalParent)
        {
            transform.SetParent(originalParent);
        }

        // Reset về vị trí gốc - dùng WORLD position để chắc chắn
        transform.position = originalWorldPosition;
        transform.localRotation = originalRotation;
        transform.localScale = Vector3.zero;

        // Reset lock visuals
        ResetLockVisuals();

        // Animation xuất hiện
        Sequence respawnSeq = DOTween.Sequence();

        // Scale cell lên
        respawnSeq.Append(
            transform.DOScale(originalScale, respawnDuration)
                .SetEase(respawnEase)
        );

        // Lock animation
        if (lockContainer != null)
        {
            lockContainer.SetActive(true);
            lockContainer.transform.localScale = Vector3.zero;
            respawnSeq.Join(
                lockContainer.transform.DOScale(1f, respawnDuration)
                    .SetEase(respawnEase)
                    .SetDelay(0.1f)
            );
        }

        if (lockIcon != null)
        {
            lockIcon.SetActive(true);
            lockIcon.transform.localScale = Vector3.zero;
            respawnSeq.Join(
                lockIcon.transform.DOScale(1f, respawnDuration)
                    .SetEase(respawnEase)
                    .SetDelay(0.15f)
            );
        }

        yield return respawnSeq.WaitForCompletion();

        // Set state to locked
        isLocked = true;
        isMerging = false;

        if (cellCollider != null)
            cellCollider.enabled = false;

        OnRelocked?.Invoke(this);

        Debug.Log($"LockedCell {name}: Respawned as LOCKED (empty, no items)");
    }

    private void ResetLockVisuals()
    {
        if (lockIcon != null)
        {
            // Dùng vị trí gốc từ prefab thay vì Vector3.zero
            lockIcon.transform.localPosition = lockIconOriginalLocalPos;
            lockIcon.transform.localScale = Vector3.one;
            lockIcon.transform.localRotation = Quaternion.identity;
        }

        if (lockContainer != null)
        {
            lockContainer.transform.localPosition = lockContainerOriginalLocalPos;
            lockContainer.transform.localScale = Vector3.one;
        }
    }

    // ========== UNLOCK ANIMATION ==========

    private void PlayUnlockAnimation()
    {
        isAnimating = true;

        // 🔊 PHÁT SOUND UNLOCK
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUnlock();

        Sequence seq = DOTween.Sequence();

        if (lockIcon != null)
        {
            Vector3 originalPos = lockIcon.transform.localPosition;

            seq.Append(lockIcon.transform.DOShakeRotation(0.15f, new Vector3(0, 0, 20), 15, 90));
            seq.Append(lockIcon.transform.DOLocalMoveY(originalPos.y + 0.8f, 0.25f).SetEase(Ease.OutQuad));
            seq.Join(lockIcon.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack));
            seq.Join(lockIcon.transform.DOLocalRotate(new Vector3(0, 0, 180), 0.25f, RotateMode.LocalAxisAdd));
        }

        if (lockContainer != null)
        {
            seq.Insert(0.1f, lockContainer.transform.DOShakeScale(0.15f, 0.1f, 10, 90));
            seq.Insert(0.25f, lockContainer.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
        }

        seq.OnComplete(() =>
        {
            Unlock();
            isAnimating = false;
        });
    }

    // ========== PUBLIC METHODS ==========

    public bool IsLocked() => isLocked;
    public bool IsMerging() => isMerging;
    public Cell GetCell() => cell;

    public bool CanAcceptItem()
    {
        return !isLocked && !isAnimating && !isMerging && cell != null && cell.CanAcceptItem(null);
    }

    public void ForceLock()
    {
        StopAllCoroutines();
        DOTween.Kill(transform);
        DOTween.Kill(lockContainer);
        DOTween.Kill(lockIcon);
        isAnimating = false;
        isMerging = false;

        // Reset về vị trí gốc - dùng world position
        if (originalParent != null && transform.parent != originalParent)
        {
            transform.SetParent(originalParent);
        }
        transform.position = originalWorldPosition;
        transform.localScale = originalScale;
        transform.localRotation = originalRotation;

        // Destroy any remaining items
        if (cell != null)
        {
            foreach (var item in cell.GetItems())
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            cell.ClearItemsWithoutDestroy();
        }

        Lock();
        ResetLockVisuals();
    }

    public void ForceUnlock()
    {
        StopAllCoroutines();
        DOTween.Kill(transform);
        isAnimating = false;
        isMerging = false;
        Unlock();
    }
}