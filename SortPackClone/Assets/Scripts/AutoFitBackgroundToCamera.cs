using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AutoFitBackgroundToCamera : MonoBehaviour
{
    void Start()
    {
        FitToCamera();
    }

    void Update()
    {
#if UNITY_EDITOR
        FitToCamera();
#endif
    }

    void FitToCamera()
    {
        Camera cam = Camera.main;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        if (cam == null || sr == null) return;

        // Lấy kích thước sprite gốc
        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        // Lấy kích thước camera
        float worldHeight = cam.orthographicSize * 2f;
        float worldWidth = worldHeight * cam.aspect;

        // Tính scale để sprite khớp camera
        float scaleX = worldWidth / spriteWidth;
        float scaleY = worldHeight / spriteHeight;

        // Scale lớn nhất để bao hết
        float finalScale = Mathf.Max(scaleX, scaleY);

        transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }
}
