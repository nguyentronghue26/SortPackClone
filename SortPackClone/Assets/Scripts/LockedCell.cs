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
    [SerializeField] private float unlockAnimDuration = 0.4f;

    [Header("Layer System")]
    [SerializeField] private int maxLockLayers = 3;
    private int currentLockLayer = 0;

    [Header("Item Positioning")]
    [SerializeField] private float itemPadding = 0.25f;    // Padding từ mép cell
    [SerializeField] private float itemYOffset = 0.15f;    // Độ cao items
    [SerializeField] private float itemScale = 0.8f;       // Scale items cho vừa cell

    // State
    private bool isLocked = true;
    private bool isAnimating = false;
    private Collider cellCollider;

    // Calculated spot positions
    private Vector3[] spotPositions;
    private int maxItems = 3;
    private float calculatedSpacing;

    // Events
    public System.Action<LockedCell> OnUnlocked;
    public System.Action<LockedCell> OnRelocked;
    public System.Action<LockedCell> OnAllLayersComplete;

    void Awake()
    {
        if (cell == null)
            cell = GetComponent<Cell>();

        cellCollider = GetComponent<Collider>();

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
            Collider col = lockIcon.GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider boxCol = lockIcon.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(1f, 1f, 0.5f);
            }
        }

        CalculateSpotPositions();
    }

    private void CalculateSpotPositions()
    {
        if (cell != null)
            maxItems = cell.GetMaxItems();

        spotPositions = new Vector3[maxItems];

        float cellWidth = 1.6f;
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            cellWidth = boxCol.size.x;
        }

        float availableWidth = cellWidth - (itemPadding * 2f);

        if (maxItems > 1)
        {
            calculatedSpacing = availableWidth / (maxItems - 1);
        }
        else
        {
            calculatedSpacing = 0f;
        }

        float startX = -availableWidth / 2f;

        for (int i = 0; i < maxItems; i++)
        {
            spotPositions[i] = new Vector3(
                startX + (i * calculatedSpacing),
                itemYOffset,
                -0.05f
            );
        }

        Debug.Log($"LockedCell: cellWidth={cellWidth}, spacing={calculatedSpacing:F2}");
    }

    void Start()
    {
        if (startLocked)
        {
            Lock();
        }
        else
        {
            Unlock();
        }

        if (cell != null)
        {
            cell.OnCellSorted += HandleCellSorted;
            cell.OnItemAdded += HandleItemAdded;
        }
    }

    void OnDestroy()
    {
        if (cell != null)
        {
            cell.OnCellSorted -= HandleCellSorted;
            cell.OnItemAdded -= HandleItemAdded;
        }

        DOTween.Kill(lockContainer);
        DOTween.Kill(lockIcon);
        DOTween.Kill(transform);
    }

    private void HandleItemAdded(Cell c, Item item)
    {
        if (c != cell) return;

        // ❌ Không gọi RepositionItems nữa, để Cell tự xếp item như cell thường
        // RepositionItems();

        if (!isLocked && item != null)
        {
            var col = item.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            item.enabled = true;
        }
    }

    private void RepositionItems()
    {
        if (cell == null) return;

        List<Item> items = cell.GetItems();

        Item[] orderedItems = new Item[maxItems];
        foreach (var item in items)
        {
            int spotIndex = item.GetSpotIndex();
            if (spotIndex >= 0 && spotIndex < maxItems)
            {
                orderedItems[spotIndex] = item;
            }
        }

        for (int i = 0; i < maxItems; i++)
        {
            Item item = orderedItems[i];
            if (item == null) continue;

            Vector3 localPos = spotPositions[i];

            SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float spriteHeight = sr.bounds.size.y * itemScale;
                localPos.y = itemYOffset + spriteHeight * 0.3f;
            }

            item.transform.localPosition = localPos;
            item.transform.localScale = Vector3.one * itemScale;
            item.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        }
    }

    // ========== INPUT HANDLING ==========

    void Update()
    {
        if (isLocked && !isAnimating && Input.GetMouseButtonDown(0))
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
        if (!isLocked || isAnimating) return;
        PlayUnlockAnimation();
    }

    // ========== LOCK/UNLOCK ==========

    public void Lock()
    {
        isLocked = true;

        if (lockContainer != null)
        {
            lockContainer.SetActive(true);
            lockContainer.transform.localScale = Vector3.one;
        }

        if (lockIcon != null)
        {
            lockIcon.SetActive(true);
            lockIcon.transform.localScale = Vector3.one;
        }

        if (cellCollider != null)
            cellCollider.enabled = false;

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

        if (cellCollider != null)
            cellCollider.enabled = true;

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
        Debug.Log($"{name} UNLOCKED");
    }

    // ========== DOTWEEN ANIMATIONS ==========

    private void PlayUnlockAnimation()
    {
        isAnimating = true;

        Sequence seq = DOTween.Sequence();

        // Lock icon: shake → bay lên + scale down
        if (lockIcon != null)
        {
            Vector3 originalPos = lockIcon.transform.localPosition;

            seq.Append(lockIcon.transform.DOShakeRotation(0.15f, new Vector3(0, 0, 20), 15, 90));
            seq.Append(lockIcon.transform.DOLocalMoveY(originalPos.y + 0.8f, 0.25f).SetEase(Ease.OutQuad));
            seq.Join(lockIcon.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack));
            seq.Join(lockIcon.transform.DOLocalRotate(new Vector3(0, 0, 180), 0.25f, RotateMode.LocalAxisAdd));
        }

        // Lock container (dây): rung nhẹ → scale down
        if (lockContainer != null)
        {
            seq.Insert(0.1f, lockContainer.transform.DOShakeScale(0.15f, 0.1f, 10, 90));
            seq.Insert(0.25f, lockContainer.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
        }

        seq.OnComplete(() =>
        {
            ResetLockVisuals();
            Unlock();
            isAnimating = false;
        });
    }

    private void PlayLockAnimation()
    {
        if (lockContainer != null)
        {
            lockContainer.SetActive(true);
            lockContainer.transform.localScale = Vector3.zero;
            lockContainer.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        }

        if (lockIcon != null)
        {
            lockIcon.SetActive(true);
            lockIcon.transform.localScale = Vector3.zero;
            lockIcon.transform.localRotation = Quaternion.identity;
            lockIcon.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(0.15f);
        }
    }

    private void ResetLockVisuals()
    {
        if (lockIcon != null)
        {
            lockIcon.transform.localPosition = Vector3.zero;
            lockIcon.transform.localScale = Vector3.one;
            lockIcon.transform.localRotation = Quaternion.identity;
        }

        if (lockContainer != null)
        {
            lockContainer.transform.localScale = Vector3.one;
        }
    }

    // ========== CELL SORTED HANDLING ==========

    private void HandleCellSorted(Cell sortedCell)
    {
        if (sortedCell != cell) return;

        currentLockLayer++;
        Debug.Log($"LockedCell {name}: Layer {currentLockLayer}/{maxLockLayers}");

        if (currentLockLayer >= maxLockLayers)
        {
            OnAllLayersComplete?.Invoke(this);
            PlayDestroyCellAnimation();
        }
        else
        {
            StartCoroutine(RelockAfterDelay());
        }
    }

    private IEnumerator RelockAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SpawnItemsInCell(cell);
        }

        yield return new WaitForSeconds(0.3f);

        // ❌ Không reposition nữa, giữ layout Cell
        // RepositionItems();

        isLocked = true;
        if (cellCollider != null)
            cellCollider.enabled = false;

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

        PlayLockAnimation();
        OnRelocked?.Invoke(this);
    }

    private void PlayDestroyCellAnimation()
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(transform.DOShakeScale(0.2f, 0.2f, 10, 90));
        seq.Append(transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
        seq.Join(transform.DOLocalMoveY(transform.localPosition.y + 0.5f, 0.3f).SetEase(Ease.InQuad));

        seq.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }

    // ========== PUBLIC METHODS ==========

    public bool IsLocked() => isLocked;
    public int GetRemainingLayers() => maxLockLayers - currentLockLayer;
    public Cell GetCell() => cell;

    public bool CanAcceptItem()
    {
        return !isLocked && cell != null && cell.CanAcceptItem(null);
    }
}
