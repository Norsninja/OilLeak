using UnityEngine;
using System;

/// <summary>
/// Central service locator for all game services
/// Single point of access, eliminates FindObjectOfType
/// </summary>
public class GameCore : MonoBehaviour
{
    // Singleton instance
    private static GameCore instance;

    // Services (null until properly initialized)
    public static ILeakService Leaks { get; private set; }
    public static IItemService Items { get; private set; }
    public static IResupplyService Resupply { get; private set; }
    public static IAudioService Audio { get; private set; }

    // Core game systems
    public static GameFlowStateMachine Flow { get; private set; }
    public static GameSession Session { get; private set; }

    // Configuration references (will be set in Inspector)
    [Header("Service Configurations")]
    [SerializeField] private GameObject leakManagerPrefab; // For future spawning
    [SerializeField] private ItemPoolConfig itemPoolConfig;

    // Debug settings
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool useNullServices = true; // For testing without implementations

    /// <summary>
    /// Check if GameCore is initialized
    /// </summary>
    public static bool IsInitialized => instance != null;

    void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize core systems
        InitializeCoreSystemsPhase1();

        if (debugMode)
        {
            Debug.Log("GameCore initialized - Phase 1 (Core systems only)");
        }
    }

    /// <summary>
    /// Phase 1 initialization - Core systems only
    /// Services will be added in Phase 2
    /// </summary>
    private void InitializeCoreSystemsPhase1()
    {
        // Create game session
        Session = new GameSession();
        Session.Initialize();

        // Create state machine
        Flow = new GameFlowStateMachine();

        // Services will be null for now (Phase 2)
        if (useNullServices)
        {
            Debug.LogWarning("GameCore: Using NULL services for Phase 1 testing");
            Leaks = null;
            Items = null;
            Resupply = null;
            Audio = null;
        }

        // Subscribe to state changes
        Flow.OnStateChanged += HandleStateChange;
    }

    /// <summary>
    /// Phase 2 initialization - Wire up services
    /// This will be called after service implementations exist
    /// </summary>
    public void InitializeServicesPhase2()
    {
        if (!useNullServices)
        {
            Debug.Log("GameCore: Initializing services - Phase 2");

            // TODO: Find existing managers and wrap them
            // For now, services remain null

            // Example of what Phase 2 will do:
            // var leakManager = FindObjectOfType<LeakManager>();
            // Leaks = new LeakServiceAdapter(leakManager);
        }
    }

    /// <summary>
    /// Handle state machine transitions
    /// </summary>
    private void HandleStateChange(GameFlowState oldState, GameFlowState newState)
    {
        if (debugMode)
        {
            Debug.Log($"GameCore: State changed from {oldState} to {newState}");
        }

        // State-specific logic
        switch (newState)
        {
            case GameFlowState.Starting:
                HandleStartingState();
                break;

            case GameFlowState.Running:
                HandleRunningState();
                break;

            case GameFlowState.Paused:
                HandlePausedState();
                break;

            case GameFlowState.Ending:
                HandleEndingState();
                break;

            case GameFlowState.Cleaning:
                HandleCleaningState();
                break;

            case GameFlowState.ShowingResults:
                HandleShowingResultsState();
                break;

            case GameFlowState.Menu:
                HandleMenuState();
                break;
        }
    }

    private void HandleStartingState()
    {
        // Reset session
        Session.Reset();

        // Reset services (when they exist)
        Leaks?.Reset();
        Items?.Reset();
        Resupply?.Reset();
        Audio?.Reset();

        // Auto-transition to Running
        Flow.TransitionTo(GameFlowState.Running);
    }

    private void HandleRunningState()
    {
        // Start session
        Session.StartSession();

        // Start services (when they exist)
        Leaks?.StartLeaks();
        Resupply?.StartResupply();
    }

    private void HandlePausedState()
    {
        // Pause services (when they exist)
        Leaks?.PauseLeaks();
        Resupply?.PauseResupply();
        Audio?.PauseAll();
    }

    private void HandleEndingState()
    {
        // End session
        Session.EndSession();

        // Stop services (when they exist)
        Leaks?.EndLeaks();
        Resupply?.EndResupply();

        // Transition to cleaning
        Flow.TransitionTo(GameFlowState.Cleaning);
    }

    private void HandleCleaningState()
    {
        // Clear everything
        Leaks?.Clear();
        Items?.ClearAll();
        Resupply?.CancelAll();
        Audio?.StopAll();

        // Reset all IResettable objects
        ResettableExtensions.ResetAll();

        // Force garbage collection (WebGL consideration)
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Verify cleanup
        if (debugMode && !ResettableExtensions.VerifyAllClean())
        {
            Debug.LogError("GameCore: Cleanup verification failed!");
        }

        // Transition to showing results
        Flow.TransitionTo(GameFlowState.ShowingResults);
    }

    private void HandleShowingResultsState()
    {
        // Display results (UI will handle this)
        var stats = Session.GetStats();
        Debug.Log($"Game Over - Time: {stats.TimeElapsed:F1}s, Gallons: {stats.GallonsDelayed}");
    }

    private void HandleMenuState()
    {
        // Initialize menu state
        Leaks?.InitializeMenuState();
    }

    /// <summary>
    /// Update game session (called from game loop)
    /// </summary>
    void Update()
    {
        // Update session timer if running
        if (Flow != null && Flow.IsActive())
        {
            Session?.Tick(Time.deltaTime);
        }
    }

    /// <summary>
    /// Cleanup on destroy
    /// </summary>
    void OnDestroy()
    {
        if (instance == this)
        {
            Flow.OnStateChanged -= HandleStateChange;
            Session?.Dispose();
            instance = null;
        }
    }

    /// <summary>
    /// Get service availability (for debug)
    /// </summary>
    public static string GetServiceStatus()
    {
        return $"Services - Leaks: {Leaks != null}, Items: {Items != null}, " +
               $"Resupply: {Resupply != null}, Audio: {Audio != null}";
    }
}