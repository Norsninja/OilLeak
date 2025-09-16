using UnityEngine;

/// <summary>
/// Migration adapter to bridge old GameState ScriptableObject to new GameSession
/// This allows existing code to work while we refactor
/// DELETE THIS after full migration
/// </summary>
public class GameStateAdapter : MonoBehaviour
{
    // Reference to the old ScriptableObject (set in Inspector)
    [Header("Legacy References")]
    [SerializeField] private GameState legacyGameState;

    // Bridge to new system
    private GameSession session => GameCore.Session;

    // Update frequency
    private float updateInterval = 0.1f; // 10Hz updates
    private float lastUpdate = 0f;

    void Awake()
    {
        if (legacyGameState == null)
        {
            Debug.LogError("GameStateAdapter: No legacy GameState assigned!");
            return;
        }

        Debug.LogWarning("GameStateAdapter: TEMPORARY migration adapter active. Remove after refactor!");
    }

    void Update()
    {
        // Throttle updates for performance
        if (Time.time - lastUpdate < updateInterval)
            return;

        lastUpdate = Time.time;

        // Only sync if we have both systems
        if (legacyGameState == null || session == null)
            return;

        // Sync data from new to old (one-way for now)
        SyncToLegacy();
    }

    /// <summary>
    /// Sync new GameSession data to legacy GameState
    /// This keeps old UI and systems working
    /// </summary>
    private void SyncToLegacy()
    {
        // Map session data to legacy fields
        legacyGameState.timer = session.TimeElapsed;
        legacyGameState.score = session.CombinedScore;

        // Map state
        if (GameCore.Flow != null)
        {
            switch (GameCore.Flow.CurrentState)
            {
                case GameFlowState.Menu:
                case GameFlowState.Starting:
                    legacyGameState.roundState = RoundState.NotStarted;
                    break;

                case GameFlowState.Running:
                case GameFlowState.Paused:
                    legacyGameState.roundState = RoundState.Active;
                    break;

                case GameFlowState.Ending:
                case GameFlowState.Cleaning:
                case GameFlowState.ShowingResults:
                    legacyGameState.roundState = RoundState.Over;
                    break;
            }
        }

        // Grade is deprecated but UI might still read it
        legacyGameState.grade = 'F'; // Always failing in futility simulator

        // Currency is deprecated
        legacyGameState.currency = 0;
    }

    /// <summary>
    /// Handle particle events from old system
    /// Routes them to new GameSession
    /// </summary>
    public void OnParticleBlocked()
    {
        session?.RecordParticleBlocked();
    }

    public void OnParticleEscaped()
    {
        session?.RecordParticleEscaped();
    }

    public void OnItemThrown()
    {
        session?.RecordItemThrown();
    }
}