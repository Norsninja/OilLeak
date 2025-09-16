using UnityEngine;

/// <summary>
/// Interface for different game modes (Round-based, Endless, etc.)
/// Allows us to swap between modes without breaking existing code
/// </summary>
public interface IGameMode
{
    /// <summary>
    /// Initialize and start this game mode
    /// </summary>
    void StartMode();

    /// <summary>
    /// Update tick for this mode (called from GameController.Update)
    /// </summary>
    /// <param name="deltaTime">Time since last update</param>
    void Tick(float deltaTime);

    /// <summary>
    /// Pause this game mode
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume this game mode from pause
    /// </summary>
    void Resume();

    /// <summary>
    /// End this game mode
    /// </summary>
    /// <param name="reason">Why the mode is ending (game over, player quit, etc.)</param>
    void EndMode(string reason);

    /// <summary>
    /// Get current stats for this mode
    /// </summary>
    /// <returns>Stats object with mode-specific data</returns>
    GameModeStats GetStats();

    /// <summary>
    /// Check if the game mode is currently active
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Check if the game mode is paused
    /// </summary>
    bool IsPaused { get; }
}

/// <summary>
/// Stats container for game modes
/// </summary>
[System.Serializable]
public class GameModeStats
{
    public float timeElapsed;
    public int score;
    public int particlesBlocked;
    public int particlesEscaped;
    public float currentDifficulty;

    // Endless mode specific
    public int currentMilestone;
    public int gallonsDelayed;
    public int activeLeaks; // Number of active leak points
    public float pressureLevel; // Current pressure percentage (0-1)

    // Round mode specific (for backwards compatibility)
    public int roundNumber;
    public float roundTimeRemaining;
}