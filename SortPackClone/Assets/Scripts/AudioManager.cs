using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý tất cả sound effects trong game
/// Gắn vào 1 GameObject trong scene, gọi AudioManager.Instance.PlayXxx()
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;         // Sound effects
    [SerializeField] private AudioSource musicSource;       // Background music

    [Header("Background Music")]
    [SerializeField] private AudioClip backgroundMusic;     // Nhạc nền chính
    [SerializeField] private AudioClip menuMusic;           // Nhạc menu (optional)
    [SerializeField] private AudioClip winMusic;            // Nhạc thắng (optional)
    [SerializeField] private AudioClip loseMusic;           // Nhạc thua (optional)
    [SerializeField] private bool playMusicOnStart = true;  // Tự động phát khi start
    [SerializeField] private float musicFadeDuration = 1f;  // Thời gian fade in/out

    [Header("Item Sounds")]
    [SerializeField] private AudioClip pickUpSound;         // Nhấc item lên
    [SerializeField] private AudioClip dropSound;           // Thả item xuống
    [SerializeField] private AudioClip invalidDropSound;    // Thả sai chỗ

    [Header("Merge Sounds")]
    [SerializeField] private AudioClip mergeSound;          // Merge thành công
    [SerializeField] private AudioClip[] comboSounds;       // Combo x2, x3, x4... (optional)

    [Header("Cell Sounds")]
    [SerializeField] private AudioClip cellSpawnSound;      // Cell xuất hiện
    [SerializeField] private AudioClip cellFlyAwaySound;    // Cell bay đi

    [Header("Lock Sounds")]
    [SerializeField] private AudioClip unlockSound;         // Mở khóa

    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClickSound;    // Click button
    [SerializeField] private AudioClip winSound;            // Thắng game
    [SerializeField] private AudioClip loseSound;           // Thua game
    [SerializeField] private AudioClip warningSound;        // Sắp hết giờ

    [Header("Settings")]
    [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private bool enableSFX = true;
    [SerializeField] private bool enableMusic = true;

    [Header("Haptic/Vibration (Mobile)")]
    [SerializeField] private bool enableHaptic = true;
    [SerializeField] private long lightVibrationMs = 20;    // Rung nhẹ (ms)
    [SerializeField] private long mediumVibrationMs = 40;   // Rung vừa (ms)
    [SerializeField] private long heavyVibrationMs = 80;    // Rung mạnh (ms)

    [Header("Pitch Variation")]
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;
    [SerializeField] private bool randomizePitch = true;

    void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto create AudioSource nếu chưa có
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        // Load settings
        LoadSettings();
    }

    void Start()
    {
        // Tự động phát nhạc nền khi game start
        if (playMusicOnStart && backgroundMusic != null)
        {
            PlayBackgroundMusic();
        }
    }

    // ==================== ITEM SOUNDS ====================

    /// <summary>
    /// Phát sound khi nhấc item lên + rung nhẹ
    /// </summary>
    public void PlayPickUp()
    {
        PlaySFX(pickUpSound);
        VibrateLight();  // Rung nhẹ khi pick up
    }

    /// <summary>
    /// Phát sound khi thả item xuống + rung nhẹ
    /// </summary>
    public void PlayDrop()
    {
        PlaySFX(dropSound);
        VibrateLight();  // Rung nhẹ khi drop
    }

    /// <summary>
    /// Phát sound khi thả sai chỗ + rung mạnh
    /// </summary>
    public void PlayInvalidDrop()
    {
        PlaySFX(invalidDropSound);
        VibrateHeavy();  // Rung mạnh khi drop sai
    }

    // ==================== MERGE SOUNDS ====================

    /// <summary>
    /// Phát sound khi merge thành công + rung vừa
    /// </summary>
    public void PlayMerge(int comboCount = 1)
    {
        Debug.Log($"[AudioManager] PlayMerge called! comboCount={comboCount}");

        if (sfxSource == null)
        {
            Debug.LogError("[AudioManager] sfxSource is NULL!");
            return;
        }

        if (mergeSound == null)
        {
            Debug.LogError("[AudioManager] mergeSound is NULL! Hãy kéo AudioClip vào Inspector.");
            return;
        }

        if (comboSounds != null && comboSounds.Length > 0 && comboCount > 1)
        {
            int index = Mathf.Min(comboCount - 2, comboSounds.Length - 1);
            PlaySFX(comboSounds[index], 1f + (comboCount - 1) * 0.1f);
        }
        else
        {
            Debug.Log($"[AudioManager] Playing mergeSound: {mergeSound.name}");
            PlaySFX(mergeSound);
        }

        // Rung vừa khi merge, rung mạnh hơn nếu combo cao
        if (comboCount >= 3)
            VibrateHeavy();
        else
            VibrateMedium();
    }

    // ==================== CELL SOUNDS ====================

    /// <summary>
    /// Phát sound khi cell xuất hiện
    /// </summary>
    public void PlayCellSpawn()
    {
        PlaySFX(cellSpawnSound);
    }

    /// <summary>
    /// Phát sound khi cell bay đi
    /// </summary>
    public void PlayCellFlyAway()
    {
        PlaySFX(cellFlyAwaySound);
    }

    // ==================== LOCK SOUNDS ====================

    /// <summary>
    /// Phát sound khi mở khóa + rung vừa
    /// </summary>
    public void PlayUnlock()
    {
        PlaySFX(unlockSound);
        VibrateMedium();  // Rung vừa khi unlock
    }

    // ==================== UI SOUNDS ====================

    /// <summary>
    /// Phát sound click button
    /// </summary>
    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound, 1f, false); // Không random pitch
    }

    /// <summary>
    /// Phát sound thắng game
    /// </summary>
    public void PlayWin()
    {
        PlaySFX(winSound, 1f, false);
    }

    /// <summary>
    /// Phát sound thua game
    /// </summary>
    public void PlayLose()
    {
        PlaySFX(loseSound, 1f, false);
    }

    /// <summary>
    /// Phát sound warning (sắp hết giờ)
    /// </summary>
    public void PlayWarning()
    {
        PlaySFX(warningSound);
    }

    // ==================== CORE METHODS ====================

    /// <summary>
    /// Phát sound effect
    /// </summary>
    public void PlaySFX(AudioClip clip, float pitchMultiplier = 1f, bool randomPitch = true)
    {
        if (!enableSFX)
        {
            Debug.Log("[AudioManager] SFX is disabled!");
            return;
        }

        if (clip == null)
        {
            Debug.LogError("[AudioManager] AudioClip is NULL!");
            return;
        }

        if (sfxSource == null)
        {
            Debug.LogError("[AudioManager] sfxSource is NULL!");
            return;
        }

        // Random pitch để sound không bị nhàm chán
        float pitch = pitchMultiplier;
        if (randomPitch && randomizePitch)
        {
            pitch *= Random.Range(minPitch, maxPitch);
        }

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, sfxVolume);

        Debug.Log($"[AudioManager] Playing: {clip.name}, volume={sfxVolume}, pitch={pitch}");
    }

    /// <summary>
    /// Phát sound tại vị trí (3D sound)
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (!enableSFX || clip == null) return;

        AudioSource.PlayClipAtPoint(clip, position, volume * sfxVolume);
    }

    // ==================== BACKGROUND MUSIC ====================

    /// <summary>
    /// Phát nhạc nền chính (loop)
    /// </summary>
    public void PlayBackgroundMusic()
    {
        if (backgroundMusic == null)
        {
            Debug.LogWarning("[AudioManager] backgroundMusic is NULL!");
            return;
        }

        PlayMusic(backgroundMusic, true);
        Debug.Log($"[AudioManager] Playing background music: {backgroundMusic.name}");
    }

    /// <summary>
    /// Phát nhạc menu
    /// </summary>
    public void PlayMenuMusic()
    {
        if (menuMusic != null)
            PlayMusic(menuMusic, true);
        else
            PlayBackgroundMusic();
    }

    /// <summary>
    /// Phát nhạc thắng (không loop)
    /// </summary>
    public void PlayWinMusic()
    {
        if (winMusic != null)
            PlayMusic(winMusic, false);
    }

    /// <summary>
    /// Phát nhạc thua (không loop)
    /// </summary>
    public void PlayLoseMusic()
    {
        if (loseMusic != null)
            PlayMusic(loseMusic, false);
    }

    /// <summary>
    /// Phát nhạc với fade in
    /// </summary>
    public void PlayMusic(AudioClip music, bool loop = true)
    {
        if (musicSource == null || music == null) return;

        StartCoroutine(FadeAndPlayMusic(music, loop));
    }

    private System.Collections.IEnumerator FadeAndPlayMusic(AudioClip newMusic, bool loop)
    {
        // Fade out nhạc cũ (nếu đang phát)
        if (musicSource.isPlaying)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration / 2f));
                yield return null;
            }

            musicSource.Stop();
        }

        // Đổi nhạc mới
        musicSource.clip = newMusic;
        musicSource.loop = loop;
        musicSource.volume = 0f;

        if (enableMusic)
        {
            musicSource.Play();

            // Fade in nhạc mới
            float elapsed = 0f;
            while (elapsed < musicFadeDuration / 2f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsed / (musicFadeDuration / 2f));
                yield return null;
            }

            musicSource.volume = musicVolume;
        }
    }

    /// <summary>
    /// Dừng nhạc nền với fade out
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            StartCoroutine(FadeOutMusic());
        }
    }

    private System.Collections.IEnumerator FadeOutMusic()
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume;
    }

    /// <summary>
    /// Pause/Resume nhạc nền
    /// </summary>
    public void PauseMusic(bool pause)
    {
        if (musicSource == null) return;

        if (pause)
            musicSource.Pause();
        else
            musicSource.UnPause();
    }

    /// <summary>
    /// Giảm volume nhạc tạm thời (khi có dialog, cutscene...)
    /// </summary>
    public void DuckMusic(bool duck, float duckVolume = 0.3f)
    {
        if (musicSource == null) return;

        float targetVolume = duck ? musicVolume * duckVolume : musicVolume;
        StartCoroutine(FadeMusicVolume(targetVolume, 0.5f));
    }

    private System.Collections.IEnumerator FadeMusicVolume(float targetVolume, float duration)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        musicSource.volume = targetVolume;
    }

    // ==================== HAPTIC FEEDBACK (VIBRATION) ====================

    public enum HapticType { Light, Medium, Heavy }

    /// <summary>
    /// Rung điện thoại
    /// </summary>
    public void TriggerHaptic(HapticType type)
    {
        if (!enableHaptic) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidVibrate(type);
#elif UNITY_IOS && !UNITY_EDITOR
        IOSVibrate(type);
#else
        // Editor - chỉ log
        Debug.Log($"[AudioManager] Haptic: {type}");
#endif
    }

    /// <summary>
    /// Rung nhẹ - dùng khi pick up, drop thành công
    /// </summary>
    public void VibrateLight()
    {
        TriggerHaptic(HapticType.Light);
    }

    /// <summary>
    /// Rung vừa - dùng khi merge, unlock
    /// </summary>
    public void VibrateMedium()
    {
        TriggerHaptic(HapticType.Medium);
    }

    /// <summary>
    /// Rung mạnh - dùng khi invalid drop, error, game over
    /// </summary>
    public void VibrateHeavy()
    {
        TriggerHaptic(HapticType.Heavy);
    }

#if UNITY_ANDROID
    private void AndroidVibrate(HapticType type)
    {
        long duration = type switch
        {
            HapticType.Light => lightVibrationMs,
            HapticType.Medium => mediumVibrationMs,
            HapticType.Heavy => heavyVibrationMs,
            _ => mediumVibrationMs
        };

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                // Android 26+ (Oreo) - dùng VibrationEffect
                if (GetAndroidSDKLevel() >= 26)
                {
                    using (AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", duration, GetVibrationAmplitude(type));
                        vibrator.Call("vibrate", vibrationEffect);
                    }
                }
                else
                {
                    // Android cũ
                    vibrator.Call("vibrate", duration);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AudioManager] Android vibration failed: {e.Message}");
            Handheld.Vibrate();
        }
    }

    private int GetAndroidSDKLevel()
    {
        using (AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            return buildVersion.GetStatic<int>("SDK_INT");
        }
    }

    private int GetVibrationAmplitude(HapticType type)
    {
        return type switch
        {
            HapticType.Light => 50,
            HapticType.Medium => 128,
            HapticType.Heavy => 255,
            _ => 128
        };
    }
#endif

#if UNITY_IOS
    private void IOSVibrate(HapticType type)
    {
        // iOS dùng Handheld.Vibrate() hoặc native plugin
        // Để có haptic feedback tốt hơn trên iOS, cần dùng native plugin
        Handheld.Vibrate();
    }
#endif

    public void SetHapticEnabled(bool enabled)
    {
        enableHaptic = enabled;
        PlayerPrefs.SetInt("HapticEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool IsHapticEnabled() => enableHaptic;

    // ==================== SETTINGS ====================

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveSettings();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
            musicSource.volume = musicVolume;
        SaveSettings();
    }

    public void SetSFXEnabled(bool enabled)
    {
        enableSFX = enabled;
        SaveSettings();
    }

    public void SetMusicEnabled(bool enabled)
    {
        enableMusic = enabled;
        if (musicSource != null)
        {
            if (enabled)
                musicSource.UnPause();
            else
                musicSource.Pause();
        }
        SaveSettings();
    }

    public float GetSFXVolume() => sfxVolume;
    public float GetMusicVolume() => musicVolume;
    public bool IsSFXEnabled() => enableSFX;
    public bool IsMusicEnabled() => enableMusic;

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetInt("SFXEnabled", enableSFX ? 1 : 0);
        PlayerPrefs.SetInt("MusicEnabled", enableMusic ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        enableSFX = PlayerPrefs.GetInt("SFXEnabled", 1) == 1;
        enableMusic = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;

        if (musicSource != null)
            musicSource.volume = musicVolume;
    }
}