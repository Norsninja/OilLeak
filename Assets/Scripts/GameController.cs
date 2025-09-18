using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple input façade that translates user input to GameCore commands
/// NO game logic, NO mode management, NO direct service access
/// </summary>
public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    // Essential ScriptableObject references (kept for legacy compatibility)
    public GameState gameState;
    public PlayerProfile playerProfile;
    public OilLeakData oilLeakData;
    public GameRulesConfig gameRulesConfig;
    public InventoryController inventoryController;
    public UIController uiController;

    // Legacy properties (kept for compatibility, but unused)
    public bool useEndlessMode = true;
    public ScoringManager scoringManager;

    // State tracking for input
    private bool gameStarted = false;
    private bool restartPending = false;
    private bool initialized = false; // Track if we've subscribed to GameCore

    void Awake()
    {
        Debug.Log($"[GameController] Awake START - GameObject: {gameObject.name}, InstanceID: {GetInstanceID()}");
        Debug.Log($"[GameController] Static Instance before assignment: {(Instance != null ? $"{Instance.gameObject.name} (ID: {Instance.GetInstanceID()})" : "null")}");

        // Diagnostic: Check if Instance is stale from previous play session
        if (Instance != null)
        {
            Debug.LogWarning($"[GameController] DUPLICATE DETECTED!");
            Debug.LogWarning($"  - Original Instance: '{Instance.gameObject.name}' (InstanceID: {Instance.GetInstanceID()})");
            Debug.LogWarning($"  - This duplicate: '{gameObject.name}' (InstanceID: {GetInstanceID()})");
            Debug.LogWarning($"  - Are same object: {Instance == this}");
            Debug.LogWarning($"  - Original still valid: {Instance != null && Instance.gameObject != null}");

            if (Instance != this)
            {
                Debug.LogError($"[GameController] DESTROYING DUPLICATE '{gameObject.name}'! Original '{Instance.gameObject.name}' will remain.");
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        Debug.Log($"[GameController] Awake COMPLETE - Set as Instance: {gameObject.name} (ID: {GetInstanceID()})");
    }

    void Start()
    {
        Debug.Log($"[GameController] Start() called on {gameObject.name} - Active: {gameObject.activeInHierarchy}, Enabled: {enabled}");

        // Debug break removed - was causing compilation issues
        // Ready to test Enter Play Mode without domain reload

        // Don't disable ourselves - we'll wait in Update for GameCore to be ready
    }

    void Update()
    {
        // First, ensure we're initialized
        if (!initialized)
        {
            if (!GameCore.IsInitialized)
            {
                // Still waiting for GameCore
                return;
            }

            // GameCore is ready! Subscribe to state changes
            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged += OnGameStateChanged;
                initialized = true;
                Debug.Log($"[GameController] Input façade ready - GameCore is the authority. Initial state: {GameCore.Flow.CurrentState}");
            }
            else
            {
                Debug.LogError("[GameController] GameCore.IsInitialized but Flow is null!");
                return;
            }
        }

        // Now we can handle input
        if (!GameCore.IsInitialized)
        {
            // This shouldn't happen after initialization
            Debug.LogError("[GameController] Lost GameCore after initialization!");
            return;
        }

        // Handle E to start (only from Menu state)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (GameCore.Flow.CurrentState == GameFlowState.Menu)
            {
                Debug.Log("[GameController] E pressed - Starting game via GameCore");
                GameCore.StartGame();
                gameStarted = true;
            }
            else
            {
                Debug.Log($"[GameController] E pressed but ignored - Current state: {GameCore.Flow.CurrentState}");
            }
        }

        // Handle R to restart (only during Running or ShowingResults)
        if (Input.GetKeyDown(KeyCode.R))
        {
            var currentState = GameCore.Flow.CurrentState;

            if (currentState == GameFlowState.Running || currentState == GameFlowState.ShowingResults)
            {
                if (!restartPending) // Prevent multiple restart calls
                {
                    Debug.Log($"[GameController] R pressed - Restarting from {currentState}");
                    restartPending = true;

                    // End current game first
                    if (currentState == GameFlowState.Running)
                    {
                        GameCore.EndGame(); // Will transition through Ending -> Cleaning -> ShowingResults
                    }

                    // Return to menu (from ShowingResults or after EndGame completes)
                    Invoke(nameof(ReturnToMenu), 2f); // Give time for transitions
                }
            }
            else
            {
                Debug.Log($"[GameController] R pressed but ignored - Current state: {currentState}");
            }
        }

        // Handle P to pause/unpause (only during Running)
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (GameCore.Flow.CurrentState == GameFlowState.Running)
            {
                Debug.Log("[GameController] P pressed - Pausing game");
                GameCore.PauseGame();
            }
            else if (GameCore.Flow.CurrentState == GameFlowState.Paused)
            {
                Debug.Log("[GameController] P pressed - Resuming game");
                GameCore.ResumeGame();
            }
        }
    }

    /// <summary>
    /// Return to menu state and prepare for new game
    /// </summary>
    void ReturnToMenu()
    {
        Debug.Log("[GameController] Returning to menu");
        GameCore.RestartGame(); // This transitions to Menu state
        restartPending = false;
        gameStarted = false;
    }

    /// <summary>
    /// Track state changes for debugging
    /// </summary>
    void OnGameStateChanged(GameFlowState oldState, GameFlowState newState)
    {
        Debug.Log($"[GameController] Observed state change: {oldState} → {newState}");

        // Clear restart flag when we reach menu
        if (newState == GameFlowState.Menu)
        {
            restartPending = false;
        }
    }

    // Legacy methods kept for compatibility
    public void StartNewRound(RoundLocationData roundData)
    {
        Debug.LogWarning("[GameController] StartNewRound called - legacy method, use GameCore instead");
    }

    public float GetCurrentRoundDuration()
    {
        return 60f; // Default duration
    }

    void OnDestroy()
    {
        Debug.LogWarning($"[GameController] OnDestroy called for {gameObject.name}! Instance={Instance?.name}");

        // CRITICAL: Log stack trace to find what's destroying us
        string stackTrace = UnityEngine.StackTraceUtility.ExtractStackTrace();
        Debug.LogError($"[GameController] DESTRUCTION STACK TRACE:\n{stackTrace}");

        // Unsubscribe from events if we subscribed
        if (initialized && GameCore.Flow != null)
        {
            GameCore.Flow.OnStateChanged -= OnGameStateChanged;
        }

        // Clear instance if it's us (fixes Enter Play Mode issues)
        if (Instance == this)
        {
            Debug.Log("[GameController] Clearing static Instance reference");
            Instance = null;
        }
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Editor-only helper to reset singleton for Enter Play Mode without domain reload
    /// </summary>
    public static bool EditorResetSingleton()
    {
        if (Instance != null)
        {
            Instance = null;
            return true;
        }
        return false;
    }
    #endif
}