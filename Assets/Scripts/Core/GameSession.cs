using UnityEngine;
using System;

/// <summary>
/// Runtime game session data - replaces ScriptableObject misuse
/// Single source of truth for all game state
/// This is NOT a ScriptableObject - it's runtime state only
/// </summary>
public class GameSession : IDisposable
{
    // Core survival metrics
    private float timeElapsed = 0f;
    private int particlesBlocked = 0;
    private int particlesEscaped = 0;
    private int itemsThrown = 0;

    // Properties for read access
    public float TimeElapsed => timeElapsed;
    public int ParticlesBlocked => particlesBlocked;
    public int ParticlesEscaped => particlesEscaped;
    public int ItemsThrown => itemsThrown;

    // Derived values
    public int GallonsDelayed => particlesBlocked * 100; // 100 gallons per particle
    public int GallonsEscaped => particlesEscaped * 100;
    public int CombinedScore => CalculateCombinedScore();

    // Session state
    public bool IsActive { get; private set; }
    public bool IsFailing => particlesEscaped >= GetMaxEscaped();
    public float FailPercentage => GetMaxEscaped() > 0 ? (float)particlesEscaped / GetMaxEscaped() : 0f;

    // Personal best tracking
    private int personalBestScore = 0;
    private float personalBestTime = 0f;
    private int personalBestGallons = 0;

    // Configuration (will come from GameConfig later)
    private const int MAX_ESCAPED_PARTICLES = 1000;
    private const float NEAR_FAIL_PERCENT = 0.8f;

    // Events will be added in Phase 2
    // For now, direct data access only

    /// <summary>
    /// Initialize session with saved personal best
    /// </summary>
    public void Initialize()
    {
        // Load personal best from PlayerPrefs
        personalBestScore = PlayerPrefs.GetInt("PersonalBestScore", 0);
        personalBestTime = PlayerPrefs.GetFloat("PersonalBestTime", 0f);
        personalBestGallons = PlayerPrefs.GetInt("PersonalBestGallons", 0);

        // WebGL: Register for page unload to save data
        #if UNITY_WEBGL && !UNITY_EDITOR
        Application.focusChanged += OnApplicationFocusChanged;
        Application.wantsToQuit += OnApplicationWantsToQuit;
        #endif

        Debug.Log($"GameSession initialized. Personal best: {personalBestScore} (Time: {personalBestTime:F1}s, Gallons: {personalBestGallons})");
    }

    /// <summary>
    /// Handle WebGL focus loss (tab switch, minimize)
    /// </summary>
    private void OnApplicationFocusChanged(bool hasFocus)
    {
        if (!hasFocus && IsActive)
        {
            // Save when losing focus
            PlayerPrefs.Save();
            Debug.Log("GameSession: Saved due to focus loss");
        }
    }

    /// <summary>
    /// Handle WebGL page closing
    /// </summary>
    private bool OnApplicationWantsToQuit()
    {
        // Final save before quit
        if (IsActive)
        {
            EndSession();
        }
        PlayerPrefs.Save();
        Debug.Log("GameSession: Final save on quit");
        return true; // Allow quit
    }

    /// <summary>
    /// Start a new game session
    /// </summary>
    public void StartSession()
    {
        Reset();
        IsActive = true;
        Debug.Log("GameSession started - Timer running");
    }

    /// <summary>
    /// Update session timer (called from game loop)
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!IsActive) return;

        timeElapsed += deltaTime;
    }

    /// <summary>
    /// Record a particle being blocked
    /// </summary>
    public void RecordParticleBlocked()
    {
        if (!IsActive) return;
        particlesBlocked++;
    }

    /// <summary>
    /// Record a particle escaping
    /// </summary>
    public void RecordParticleEscaped()
    {
        if (!IsActive) return;
        particlesEscaped++;
    }

    /// <summary>
    /// Record an item being thrown
    /// </summary>
    public void RecordItemThrown()
    {
        if (!IsActive) return;
        itemsThrown++;
    }

    /// <summary>
    /// End the current session
    /// </summary>
    public void EndSession()
    {
        if (!IsActive) return;

        IsActive = false;

        // Check for new personal best
        bool isNewRecord = false;
        if (CombinedScore > personalBestScore)
        {
            personalBestScore = CombinedScore;
            personalBestTime = timeElapsed;
            personalBestGallons = GallonsDelayed;
            isNewRecord = true;

            // Save to PlayerPrefs
            PlayerPrefs.SetInt("PersonalBestScore", personalBestScore);
            PlayerPrefs.SetFloat("PersonalBestTime", personalBestTime);
            PlayerPrefs.SetInt("PersonalBestGallons", personalBestGallons);
            PlayerPrefs.Save(); // Explicit save for WebGL

            Debug.Log($"NEW PERSONAL BEST! Score: {personalBestScore}");
        }

        Debug.Log($"Session ended. Time: {timeElapsed:F1}s, Gallons: {GallonsDelayed}, Score: {CombinedScore}, New Record: {isNewRecord}");
    }

    /// <summary>
    /// Reset session to initial state
    /// </summary>
    public void Reset()
    {
        timeElapsed = 0f;
        particlesBlocked = 0;
        particlesEscaped = 0;
        itemsThrown = 0;
        IsActive = false;

        Debug.Log("GameSession reset to initial state");
    }

    /// <summary>
    /// Calculate combined score for leaderboard
    /// </summary>
    private int CalculateCombinedScore()
    {
        // Time (seconds) * 10 + Gallons / 10
        return (int)(timeElapsed * 10f) + (GallonsDelayed / 10);
    }

    /// <summary>
    /// Get maximum allowed escaped particles
    /// </summary>
    private int GetMaxEscaped()
    {
        // Could scale with time in future, for now constant
        return MAX_ESCAPED_PARTICLES;
    }

    /// <summary>
    /// Check if near failure threshold
    /// </summary>
    public bool IsNearFailure()
    {
        return FailPercentage >= NEAR_FAIL_PERCENT;
    }

    /// <summary>
    /// Get session statistics for UI
    /// </summary>
    public SessionStats GetStats()
    {
        return new SessionStats
        {
            TimeElapsed = timeElapsed,
            ParticlesBlocked = particlesBlocked,
            ParticlesEscaped = particlesEscaped,
            GallonsDelayed = GallonsDelayed,
            GallonsEscaped = GallonsEscaped,
            ItemsThrown = itemsThrown,
            Score = CombinedScore,
            IsNewRecord = CombinedScore > personalBestScore,
            PersonalBest = personalBestScore,
            PersonalBestTime = personalBestTime,
            PersonalBestGallons = personalBestGallons
        };
    }

    /// <summary>
    /// Dispose pattern for cleanup (events will be cleared here later)
    /// </summary>
    public void Dispose()
    {
        Reset();

        // Unregister WebGL handlers
        #if UNITY_WEBGL && !UNITY_EDITOR
        Application.focusChanged -= OnApplicationFocusChanged;
        Application.wantsToQuit -= OnApplicationWantsToQuit;
        #endif

        // Events will be nulled here in Phase 2
        Debug.Log("GameSession disposed");
    }
}

/// <summary>
/// Session statistics for UI display
/// </summary>
public struct SessionStats
{
    public float TimeElapsed;
    public int ParticlesBlocked;
    public int ParticlesEscaped;
    public int GallonsDelayed;
    public int GallonsEscaped;
    public int ItemsThrown;
    public int Score;
    public bool IsNewRecord;
    public int PersonalBest;
    public float PersonalBestTime;
    public int PersonalBestGallons;
}