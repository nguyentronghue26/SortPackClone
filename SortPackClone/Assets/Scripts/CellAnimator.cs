using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class CellAnimator : MonoBehaviour
{
    [Header("Highlight Animation")]
    [SerializeField] private float highlightPulseScale = 1.05f;
    [SerializeField] private float highlightPulseDuration = 0.4f;
    [SerializeField] private Color highlightValidColor = new Color(0.5f, 1f, 0.5f, 0.5f);
    [SerializeField] private Color highlightInvalidColor = new Color(1f, 0.5f, 0.5f, 0.5f);

    [Header("Spawn Animation")]
    [SerializeField] private float spawnDuration = 0.5f;
    [SerializeField] private float spawnBounceStrength = 1.2f;
    [SerializeField] private Ease spawnEase = Ease.OutBack;

    [Header("Clear Animation")]
    [SerializeField] private float clearDuration = 0.4f;
    [SerializeField] private float clearFlyUpDistance = 2f;
    [SerializeField] private Ease clearEase = Ease.InBack;

    [Header("Respawn Animation")]
    [SerializeField] private float respawnDuration = 0.5f;
    [SerializeField] private float respawnOffsetZ = 0.5f;
    [SerializeField] private Ease respawnEase = Ease.OutBack;

    [Header("Shake Animation")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 0.15f;

    [Header("Success Animation")]
    [SerializeField] private float successScaleUp = 1.1f;
    [SerializeField] private float successDuration = 0.3f;

    // Cache
    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Tweener pulseTween;
    private Renderer cellRenderer;
    private Color originalColor;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.position;

        cellRenderer = GetComponentInChildren<Renderer>();
        if (cellRenderer != null)
        {
            originalColor = cellRenderer.material.color;
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
    }

    // ========== HIGHLIGHT (khi hover) ==========
    public void PlayHighlightEnter(bool isValid = true)
    {
        // Không làm gì
    }

    public void PlayHighlightExit()
    {
        // Không làm gì
    }

    // ========== SPAWN ==========
    public void PlaySpawn(System.Action onComplete = null)
    {
        transform.localScale = Vector3.zero;

        Sequence spawnSequence = DOTween.Sequence();

        // Scale up với overshoot
        spawnSequence.Append(
            transform.DOScale(originalScale * spawnBounceStrength, spawnDuration * 0.6f)
                .SetEase(Ease.OutQuad)
        );

        // Bounce back
        spawnSequence.Append(
            transform.DOScale(originalScale, spawnDuration * 0.4f)
                .SetEase(Ease.OutBounce)
        );

        spawnSequence.OnComplete(() => onComplete?.Invoke());
    }

    // ========== SPAWN FROM BEHIND (Z) ==========
    public void PlaySpawnFromBehind(System.Action onComplete = null)
    {
        Vector3 behindPos = new Vector3(
            originalPosition.x,
            originalPosition.y,
            originalPosition.z + respawnOffsetZ
        );

        transform.position = behindPos;
        transform.localScale = Vector3.zero;

        Sequence spawnSequence = DOTween.Sequence();

        // Di chuyển ra phía trước
        spawnSequence.Append(
            transform.DOMove(originalPosition, respawnDuration)
                .SetEase(respawnEase)
        );

        // Scale lên cùng lúc
        spawnSequence.Join(
            transform.DOScale(originalScale, respawnDuration)
                .SetEase(respawnEase)
        );

        spawnSequence.OnComplete(() => onComplete?.Invoke());
    }

    // ========== CLEAR (bay lên và biến mất) ==========
    public void PlayClear(System.Action onComplete = null)
    {
        Sequence clearSequence = DOTween.Sequence();

        // Bay lên
        clearSequence.Append(
            transform.DOMoveY(transform.position.y + clearFlyUpDistance, clearDuration)
                .SetEase(clearEase)
        );

        // Thu nhỏ cùng lúc
        clearSequence.Join(
            transform.DOScale(Vector3.zero, clearDuration)
                .SetEase(clearEase)
        );

        // Xoay nhẹ
        clearSequence.Join(
            transform.DORotate(new Vector3(0, 0, 15f), clearDuration)
                .SetEase(Ease.InQuad)
        );

        clearSequence.OnComplete(() => onComplete?.Invoke());
    }

    // ========== RESPAWN ==========
    public void PlayRespawn(Vector3 fromPosition, System.Action onComplete = null)
    {
        transform.position = fromPosition;
        transform.localScale = Vector3.zero;

        Sequence respawnSequence = DOTween.Sequence();

        // Di chuyển đến vị trí
        respawnSequence.Append(
            transform.DOMove(originalPosition, respawnDuration)
                .SetEase(respawnEase)
        );

        // Scale lên
        respawnSequence.Join(
            transform.DOScale(originalScale, respawnDuration)
                .SetEase(respawnEase)
        );

        respawnSequence.OnComplete(() => {
            // Bounce các items bên trong
            BounceAllItems();
            onComplete?.Invoke();
        });
    }

    // ========== SHAKE (invalid action) ==========
    public void PlayShake()
    {
        transform.DOShakePosition(shakeDuration, shakeStrength, 15, 90, false, true);
    }

    // ========== SUCCESS (sorted/matched) ==========
    public void PlaySuccess()
    {
        Sequence successSequence = DOTween.Sequence();

        // Scale up
        successSequence.Append(
            transform.DOScale(originalScale * successScaleUp, successDuration * 0.5f)
                .SetEase(Ease.OutQuad)
        );

        // Glow effect (nếu có renderer)
        if (cellRenderer != null)
        {
            successSequence.Join(
                cellRenderer.material.DOColor(Color.yellow, successDuration * 0.5f)
            );
        }

        // Scale back
        successSequence.Append(
            transform.DOScale(originalScale, successDuration * 0.5f)
                .SetEase(Ease.OutBounce)
        );

        // Color back
        if (cellRenderer != null)
        {
            successSequence.Join(
                cellRenderer.material.DOColor(originalColor, successDuration * 0.5f)
            );
        }
    }

    // ========== BOUNCE ALL ITEMS IN CELL ==========
    public void BounceAllItems()
    {
        // Không làm gì - bỏ bounce
    }

    // ========== WOBBLE ALL ITEMS ==========
    public void WobbleAllItems()
    {
        // Không làm gì - bỏ wobble
    }

    // ========== POP ITEMS (merge effect) ==========
    public void PopAllItemsToCenter(System.Action onComplete = null)
    {
        Cell cell = GetComponent<Cell>();
        if (cell == null)
        {
            onComplete?.Invoke();
            return;
        }

        List<Item> items = cell.GetItems();
        if (items.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        // Tính vị trí trung tâm
        Vector3 center = transform.position;
        int completed = 0;

        foreach (var item in items)
        {
            if (item == null) continue;

            ItemAnimator itemAnim = item.GetComponent<ItemAnimator>();
            if (itemAnim != null)
            {
                itemAnim.PlayMerge(center, () => {
                    completed++;
                    if (completed >= items.Count)
                    {
                        onComplete?.Invoke();
                    }
                });
            }
            else
            {
                // Fallback nếu không có animator
                item.transform.DOMove(center, 0.3f).SetEase(Ease.InQuad);
                item.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
                    .OnComplete(() => {
                        completed++;
                        if (completed >= items.Count)
                        {
                            onComplete?.Invoke();
                        }
                    });
            }
        }
    }

    // ========== UTILITIES ==========
    public void ResetToOriginal()
    {
        transform.DOKill();
        transform.localScale = originalScale;
        transform.position = originalPosition;
        transform.rotation = Quaternion.identity;

        if (cellRenderer != null)
        {
            cellRenderer.material.color = originalColor;
        }
    }

    public void SetOriginalScale(Vector3 scale)
    {
        originalScale = scale;
    }

    public void SetOriginalPosition(Vector3 position)
    {
        originalPosition = position;
    }
}