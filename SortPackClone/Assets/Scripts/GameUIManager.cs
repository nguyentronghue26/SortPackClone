using UnityEngine;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Transform canvasTransform; // Cha để spawn panel (thường là Canvas)

    [Header("Prefabs")]
    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private GameObject retryPanelPrefab;

    [Header("Options")]
    [SerializeField] private bool useTimeScalePause = true;   // true = mở panel thì dừng game

    // instance đã spawn trong scene
    private GameObject settingsPanelInstance;
    private GameObject retryPanelInstance;

    private void Awake()
    {
        // Singleton đơn giản
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Nếu muốn UIManager sống qua nhiều scene thì mở dòng dưới
        // DontDestroyOnLoad(gameObject);
    }

    // ==================== SETTINGS ====================

    // Gọi từ nút icon setting
    public void OnSettingsButtonClicked()
    {
        // Nếu chưa spawn thì Instantiate lần đầu
        if (settingsPanelInstance == null)
        {
            if (settingsPanelPrefab == null || canvasTransform == null)
            {
                Debug.LogWarning("[GameUIManager] Chưa gán SettingsPanelPrefab hoặc CanvasTransform");
                return;
            }

            settingsPanelInstance = Instantiate(settingsPanelPrefab, canvasTransform);
        }

        settingsPanelInstance.SetActive(true);
        SetPause(true);
        PlayClickSfx();
    }

    // Gọi từ nút Close / X trên setting panel
    public void OnSettingsCloseClicked()
    {
        if (settingsPanelInstance == null) return;

        settingsPanelInstance.SetActive(false);
        SetPause(false);
        PlayClickSfx();
    }

    // ==================== RETRY PANEL ====================

    // Gọi khi bấm nút Retry ở HUD, hoặc khi thua
    public void ShowRetryPanel()
    {
        if (retryPanelInstance == null)
        {
            if (retryPanelPrefab == null || canvasTransform == null)
            {
                Debug.LogWarning("[GameUIManager] Chưa gán RetryPanelPrefab hoặc CanvasTransform");
                return;
            }

            retryPanelInstance = Instantiate(retryPanelPrefab, canvasTransform);
        }

        retryPanelInstance.SetActive(true);
        SetPause(true);
        PlayClickSfx();
    }

    // Nút Retry trong retry panel
    public void OnRetryConfirmClicked()
    {
        SetPause(false);
        PlayClickSfx();

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    // Nút Home trong retry panel
    public void OnRetryHomeClicked()
    {
        SetPause(false);
        PlayClickSfx();

        // Đổi "MainMenu" thành tên scene menu của Huệ
        SceneManager.LoadScene("MainMenu");
    }

    // Nếu retry panel có nút Close (X)
    public void OnRetryCloseClicked()
    {
        if (retryPanelInstance != null)
            retryPanelInstance.SetActive(false);

        SetPause(false);
        PlayClickSfx();
    }

    // ==================== HELPER ====================

    private void SetPause(bool pause)
    {
        if (!useTimeScalePause) return;
        Time.timeScale = pause ? 0f : 1f;
    }

    private void PlayClickSfx()
    {
        // Nếu có AudioManager thì mở dòng dưới
        // if (AudioManager.Instance != null)
        //     AudioManager.Instance.PlayUI_Click();
    }
}
