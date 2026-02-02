using UnityEngine;
using DG.Tweening;

/// <summary>
/// Gắn vào Item để có viền sáng khi drag
/// Cách 1: Dùng Shader (cần SpriteOutline.shader – cho SpriteRenderer)
/// Cách 2: Dùng thêm SpriteRenderer / Mesh làm viền (ExtraSprite – Sprite + Mesh)
/// </summary>
public class ItemOutline : MonoBehaviour
{
    public enum OutlineMethod
    {
        Shader,         // Dùng custom shader (SpriteRenderer)
        ExtraSprite     // Dùng thêm sprite / mesh làm viền (Sprite + Mesh)
    }

    [Header("Method")]
    [SerializeField] private OutlineMethod method = OutlineMethod.ExtraSprite;

    [Header("Outline Settings")]
    [SerializeField] private Color outlineColor = Color.white;  // Mặc định: TRẮNG
    [SerializeField] private float outlineWidth = 3f;
    [SerializeField] private float glowIntensity = 1.5f;        // chỉ dùng cho SpriteOutline shader (2D)

    [Header("Extra Sprite / Mesh")]
    [SerializeField] private Material outlineMaterial;  // Material cho viền (2D: glow, 3D: Custom/Outline3D)

    // References
    private SpriteRenderer spriteRenderer;      // 2D
    private MeshRenderer meshRenderer;          // 3D
    private Material originalMaterial;
    private Material outlineMaterialInstance;

    // Extra sprite / mesh
    private GameObject outlineObj;             // Sprite hoặc Mesh copy
    private SpriteRenderer outlineSpriteRenderer;  // chỉ cho Sprite
    private bool Is3D => meshRenderer != null && spriteRenderer == null;

    // State
    private bool isOutlineActive = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (spriteRenderer != null)
            originalMaterial = spriteRenderer.material;
        else if (meshRenderer != null)
            originalMaterial = meshRenderer.material;

        if (method == OutlineMethod.ExtraSprite)
            SetupExtraSprite();
        else
            SetupShaderMethod();
    }

    void OnDestroy()
    {
        if (outlineObj != null)
            Destroy(outlineObj);

        if (outlineMaterialInstance != null)
            Destroy(outlineMaterialInstance);
    }

    // ==================== SETUP ====================

    private void SetupShaderMethod()
    {
        if (spriteRenderer == null)
        {
            // Shader chỉ dùng cho SpriteRenderer
            Debug.LogWarning("[ItemOutline] Shader mode chỉ dùng cho SpriteRenderer. Chuyển sang ExtraSprite.");
            method = OutlineMethod.ExtraSprite;
            SetupExtraSprite();
            return;
        }

        Shader outlineShader = Shader.Find("Custom/SpriteOutline");
        if (outlineShader != null)
        {
            outlineMaterialInstance = new Material(outlineShader);
            outlineMaterialInstance.SetColor("_OutlineColor", outlineColor);
            outlineMaterialInstance.SetFloat("_OutlineWidth", outlineWidth);
            outlineMaterialInstance.SetFloat("_GlowIntensity", glowIntensity);
            outlineMaterialInstance.SetFloat("_OutlineEnabled", 0);
        }
        else
        {
            Debug.LogWarning("[ItemOutline] Shader 'Custom/SpriteOutline' not found! Chuyển sang ExtraSprite.");
            method = OutlineMethod.ExtraSprite;
            SetupExtraSprite();
        }
    }

    private void SetupExtraSprite()
    {
        // ========= CASE 2D: SPRITERENDERER =========
        if (spriteRenderer != null)
        {
            outlineObj = new GameObject("OutlineSprite");
            outlineObj.transform.SetParent(transform);
            outlineObj.transform.localPosition = Vector3.zero;
            outlineObj.transform.localRotation = Quaternion.identity;
            outlineObj.transform.localScale = Vector3.one * (1f + outlineWidth * 0.05f);

            outlineSpriteRenderer = outlineObj.AddComponent<SpriteRenderer>();
            outlineSpriteRenderer.sprite = spriteRenderer.sprite;
            outlineSpriteRenderer.color = outlineColor;

            outlineSpriteRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
            outlineSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;

            if (outlineMaterial != null)
                outlineSpriteRenderer.material = outlineMaterial;
            else
                outlineSpriteRenderer.material = new Material(Shader.Find("Sprites/Default"));

            outlineObj.SetActive(false);
        }
        // ========= CASE 3D: MESHRENDERER =========
        else if (meshRenderer != null)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning("[ItemOutline] MeshRenderer không có MeshFilter/mesh trên " + name);
                return;
            }

            outlineObj = new GameObject("OutlineMesh");
            outlineObj.transform.SetParent(transform);
            outlineObj.transform.localRotation = Quaternion.identity;

            // scale base > 1 tí để vỏ to hơn chai → tạo mép viền
            float baseScale = 1f + outlineWidth * 0.05f;
            outlineObj.transform.localScale = Vector3.one * baseScale;

            // pivot chai ở đáy → khi scale từ pivot, vỏ sẽ nở lên trên nhiều hơn
            // => bù Y xuống để viền đều trên–dưới
            var lb = meshRenderer.localBounds;
            float halfHeight = lb.extents.y;
            float extra = baseScale - 1f;
            float offsetY = -halfHeight * extra;      // dịch xuống theo phần nở thêm
            outlineObj.transform.localPosition = new Vector3(0f, offsetY, 0f);

            MeshFilter outlineMF = outlineObj.AddComponent<MeshFilter>();
            outlineMF.sharedMesh = mf.sharedMesh;

            MeshRenderer outlineMR = outlineObj.AddComponent<MeshRenderer>();

            if (outlineMaterial != null)
                outlineMR.material = outlineMaterial;  // ở đây Huệ gán material dùng shader Custom/Outline3D
            else
            {
                // Material đơn giản (Unlit/Color) – nếu không gán outlineMaterial.
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                outlineMR.material = new Material(shader);
            }

            if (outlineMR.material.HasProperty("_Color"))
                outlineMR.material.color = outlineColor;

            outlineObj.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[ItemOutline] Không có SpriteRenderer hoặc MeshRenderer trên " + name);
        }
    }

    // ==================== PUBLIC API ====================

    public void ShowOutline()
    {
        if (isOutlineActive) return;
        isOutlineActive = true;

        if (method == OutlineMethod.Shader)
            ShowOutlineShader();
        else
            ShowOutlineExtraSprite();
    }

    public void HideOutline()
    {
        if (!isOutlineActive) return;
        isOutlineActive = false;

        if (method == OutlineMethod.Shader)
            HideOutlineShader();
        else
            HideOutlineExtraSprite();
    }

    public void SetOutlineColor(Color color)
    {
        outlineColor = color;

        if (method == OutlineMethod.Shader && outlineMaterialInstance != null)
        {
            outlineMaterialInstance.SetColor("_OutlineColor", color);
        }
        else if (outlineSpriteRenderer != null)
        {
            outlineSpriteRenderer.color = color;
        }
        else if (Is3D && outlineObj != null)
        {
            var mr = outlineObj.GetComponent<MeshRenderer>();
            if (mr != null && mr.material.HasProperty("_Color"))
                mr.material.color = color;
        }
    }

    // ==================== SHADER (2D) ====================

    private void ShowOutlineShader()
    {
        if (spriteRenderer == null || outlineMaterialInstance == null) return;

        outlineMaterialInstance.mainTexture = spriteRenderer.sprite.texture;
        spriteRenderer.material = outlineMaterialInstance;
        outlineMaterialInstance.SetFloat("_OutlineEnabled", 1);
    }

    private void HideOutlineShader()
    {
        if (spriteRenderer == null) return;

        if (outlineMaterialInstance != null)
            outlineMaterialInstance.SetFloat("_OutlineEnabled", 0);

        spriteRenderer.material = originalMaterial;
    }

    // ==================== EXTRA SPRITE / MESH ====================

    private void ShowOutlineExtraSprite()
    {
        if (outlineObj == null) return;

        float baseScale = 1f + outlineWidth * 0.05f;

        if (!Is3D && outlineSpriteRenderer != null && spriteRenderer != null)
        {
            // 2D: sync sprite & vị trí
            outlineSpriteRenderer.sprite = spriteRenderer.sprite;
            outlineObj.transform.localScale = Vector3.one * baseScale;
            outlineObj.transform.localPosition = Vector3.zero;
        }
        else if (Is3D && meshRenderer != null)
        {
            // 3D: update lại offset Y theo baseScale (phòng khi Huệ đổi outlineWidth lúc runtime)
            var lb = meshRenderer.localBounds;
            float halfH = lb.extents.y;
            float extra = baseScale - 1f;
            float offsetY = -halfH * extra;
            outlineObj.transform.localPosition = new Vector3(0f, offsetY, 0f);
            outlineObj.transform.localScale = Vector3.one * baseScale;
        }

        outlineObj.SetActive(true);

        // Animation nảy nhẹ khi hiện (giữ lại cho đẹp)
        outlineObj.transform.localScale = Vector3.zero;
        outlineObj.transform
            .DOScale(Vector3.one * baseScale, 0.15f)
            .SetEase(Ease.OutBack);
    }

    private void HideOutlineExtraSprite()
    {
        if (outlineObj == null) return;

        // Animation thu nhỏ rồi tắt
        outlineObj.transform
            .DOScale(Vector3.zero, 0.1f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (outlineObj != null)
                    outlineObj.SetActive(false);
            });
    }

    // ==================== AUTO SYNC SPRITE (2D) ====================

    void LateUpdate()
    {
        // 2D: nếu đang bật outline thì sync sprite khi đổi sprite
        if (method == OutlineMethod.ExtraSprite && isOutlineActive && !Is3D)
        {
            if (outlineSpriteRenderer != null && spriteRenderer != null)
            {
                if (outlineSpriteRenderer.sprite != spriteRenderer.sprite)
                    outlineSpriteRenderer.sprite = spriteRenderer.sprite;
            }
        }
    }
}
