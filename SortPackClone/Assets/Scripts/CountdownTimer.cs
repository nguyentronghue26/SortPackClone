using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CountdownTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float startTimeInSeconds = 115f;  // 1:55 = 115 giây
    [SerializeField] private bool startOnAwake = true;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timerText;        // Dùng TextMeshPro
    [SerializeField] private Text timerTextLegacy;             // Hoặc UI Text thường
    [SerializeField] private RectTransform timerContainer;     // Container để scale (heartbeat)

    [Header("Warning Settings")]
    [SerializeField] private float warningTime = 30f;          // Đổi màu khi còn 30s
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color warningColor = Color.red;

    [Header("Heartbeat Effect")]
    [SerializeField] private bool enableHeartbeat = true;
    [SerializeField] private float heartbeatScale = 1.15f;     // Scale to lên 1.15
    [SerializeField] private float heartbeatDuration = 0.5f;   // Mỗi nhịp 0.5s (1 giây = 2 nhịp)
    [SerializeField] private Ease heartbeatEaseIn = Ease.OutQuad;
    [SerializeField] private Ease heartbeatEaseOut = Ease.InQuad;

    // State
    private float currentTime;
    private bool isRunning = false;
    private bool isPaused = false;
    private bool isHeartbeating = false;
    private Tweener heartbeatTween;

    // Events
    public System.Action OnTimerStart;
    public System.Action OnTimerEnd;
    public System.Action<float> OnTimerTick;

    void Awake()
    {
        currentTime = startTimeInSeconds;

        // Auto find container nếu chưa gán
        if (timerContainer == null)
        {
            if (timerText != null)
                timerContainer = timerText.rectTransform;
            else if (timerTextLegacy != null)
                timerContainer = timerTextLegacy.rectTransform;
        }

        UpdateTimerDisplay();

        if (startOnAwake)
        {
            StartTimer();
        }
    }

    void OnDestroy()
    {
        StopHeartbeat();
    }

    void Update()
    {
        if (!isRunning || isPaused) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            isRunning = false;
            StopHeartbeat();
            UpdateTimerDisplay();
            OnTimerEnd?.Invoke();
            Debug.Log("[CountdownTimer] Time's up!");
            return;
        }

        UpdateTimerDisplay();

        // Bắt đầu heartbeat khi vào warning time
        if (currentTime <= warningTime && enableHeartbeat && !isHeartbeating)
        {
            StartHeartbeat();
        }
    }

    private void UpdateTimerDisplay()
    {
        string timeString = FormatTime(currentTime);

        if (timerText != null)
        {
            timerText.text = timeString;
            timerText.color = currentTime <= warningTime ? warningColor : normalColor;
        }

        if (timerTextLegacy != null)
        {
            timerTextLegacy.text = timeString;
            timerTextLegacy.color = currentTime <= warningTime ? warningColor : normalColor;
        }

        OnTimerTick?.Invoke(currentTime);
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes}:{seconds:00}";
    }

    // ========== HEARTBEAT EFFECT ==========

    private void StartHeartbeat()
    {
        if (timerContainer == null || isHeartbeating) return;

        isHeartbeating = true;

        // Reset scale
        timerContainer.localScale = Vector3.one;

        // Tạo sequence lặp vô hạn
        Sequence heartbeatSeq = DOTween.Sequence();

        // To lên
        heartbeatSeq.Append(
            timerContainer.DOScale(heartbeatScale, heartbeatDuration)
                .SetEase(heartbeatEaseIn)
        );

        // Về scale cũ
        heartbeatSeq.Append(
            timerContainer.DOScale(1f, heartbeatDuration)
                .SetEase(heartbeatEaseOut)
        );

        // Lặp vô hạn
        heartbeatSeq.SetLoops(-1);

        heartbeatTween = null; // Dùng sequence thay vì tweener

        Debug.Log("[CountdownTimer] Heartbeat started!");
    }

    private void StopHeartbeat()
    {
        if (!isHeartbeating) return;

        isHeartbeating = false;

        // Kill all tweens on container
        if (timerContainer != null)
        {
            DOTween.Kill(timerContainer);
            timerContainer.localScale = Vector3.one;
        }

        Debug.Log("[CountdownTimer] Heartbeat stopped");
    }

    // ========== PUBLIC METHODS ==========

    public void StartTimer()
    {
        isRunning = true;
        isPaused = false;
        OnTimerStart?.Invoke();
    }

    public void StartTimer(float seconds)
    {
        currentTime = seconds;
        startTimeInSeconds = seconds;

        // Stop heartbeat nếu thời gian mới > warning time
        if (currentTime > warningTime)
        {
            StopHeartbeat();
        }

        StartTimer();
    }

    public void PauseTimer()
    {
        isPaused = true;

        // Pause heartbeat
        if (timerContainer != null)
        {
            DOTween.Pause(timerContainer);
        }
    }

    public void ResumeTimer()
    {
        isPaused = false;

        // Resume heartbeat
        if (timerContainer != null)
        {
            DOTween.Play(timerContainer);
        }
    }

    public void StopTimer()
    {
        isRunning = false;
        isPaused = false;
        StopHeartbeat();
    }

    public void ResetTimer()
    {
        currentTime = startTimeInSeconds;
        isRunning = false;
        isPaused = false;
        StopHeartbeat();
        UpdateTimerDisplay();
    }

    public void AddTime(float seconds)
    {
        currentTime += seconds;

        // Stop heartbeat nếu ra khỏi warning zone
        if (currentTime > warningTime)
        {
            StopHeartbeat();
        }

        UpdateTimerDisplay();
    }

    public float GetRemainingTime() => currentTime;

    public bool IsRunning() => isRunning && !isPaused;

    public void SetStartTime(float seconds)
    {
        startTimeInSeconds = seconds;
        currentTime = seconds;

        if (currentTime > warningTime)
        {
            StopHeartbeat();
        }

        UpdateTimerDisplay();
    }
}