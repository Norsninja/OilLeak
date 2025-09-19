using UnityEngine;
using System;
using Core;
using Core.Services;
using Core.Systems;

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
    public static IDifficultyService Difficulty { get; private set; }
    public static IHUDService HUD { get; private set; }
    public static IPlayerMovementService Player { get; private set; }
    public static IDevHudService DevHud { get; private set; }

    // Core game systems
    public static GameFlowStateMachine Flow { get; private set; }
    public static GameSession Session { get; private set; }

    // Gameplay system - orchestrates the futility loop
    private static FutilityGameplaySystem futilitySystem;

    // UI update coordinator - manages UI refresh rate
    private static HudUpdateCoordinator hudCoordinator;

    // Deferred state transition flag
    private static bool pendingStartRun = false;

    // Configuration references (will be set in Inspector)
    [Header("Service Configurations")]
    [SerializeField] private GameObject leakManagerPrefab; // For future spawning
    [SerializeField] private ItemPoolConfig itemPoolConfig;
    [SerializeField] private ScoringConfig scoringConfig; // Dynamic scoring configuration

    // Debug settings
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    /// <summary>
    /// Check if GameCore is initialized
    /// </summary>
    public static bool IsInitialized => instance != null;

    void Awake()
    {
        Debug.Log($"[GameCore] Awake START - GameObject: {gameObject.name}, InstanceID: {GetInstanceID()}");
        Debug.Log($"[GameCore] Static instance before assignment: {(instance != null ? $"{instance.gameObject.name} (ID: {instance.GetInstanceID()})" : "null")}");

        // Singleton pattern
        if (instance != null && instance != this)
        {
            Debug.LogError($"[GameCore] DUPLICATE DETECTED! DESTROYING GameObject '{gameObject.name}'");
            Debug.LogError($"  - Original instance: '{instance.gameObject.name}' (InstanceID: {instance.GetInstanceID()})");
            Debug.LogError($"  - This duplicate: '{gameObject.name}' (InstanceID: {GetInstanceID()})");
            Debug.LogError($"  - This will also destroy GameController on same GameObject!");
            Destroy(gameObject);
            return;
        }

        instance = this;
        Debug.Log($"[GameCore] Awake COMPLETE - Set as instance: {gameObject.name} (ID: {GetInstanceID()})");
        // DontDestroyOnLoad removed - single scene game doesn't need persistence
        // This was causing duplicate GameController issues with Enter Play Mode settings

        // Initialize core systems
        InitializeCoreSystemsPhase1();

        // Register services explicitly - NO REFLECTION
        RegisterServices();

        if (debugMode)
        {
            Debug.Log("GameCore initialized with explicit service registration");
        }
    }

    /// <summary>
    /// Phase 1 initialization - Core systems only
    /// </summary>
    private void InitializeCoreSystemsPhase1()
    {
        // Get config from GameController
        GameRulesConfig rulesConfig = null;
        var gameController = GetComponent<GameController>();
        if (gameController != null)
        {
            rulesConfig = gameController.gameRulesConfig;
        }

        // Create game session with config
        Session = new GameSession();
        Session.Initialize(rulesConfig, scoringConfig);

        // Create state machine
        Flow = new GameFlowStateMachine();

        // Subscribe to state changes
        Flow.OnStateChanged += HandleStateChange;
    }

    /// <summary>
    /// Register services explicitly - NO FindObjectOfType after Awake
    /// </summary>
    private void RegisterServices()
    {
        // Find existing managers (only in Awake, not after)
        var leakManager = FindObjectOfType<LeakManager>();
        var itemPooler = FindObjectOfType<ItemPooler>();
        var resupplyManager = FindObjectOfType<ResupplyManager>();
        var difficultyManager = FindObjectOfType<DifficultyManager>();
        var uiController = FindObjectOfType<UIController>();
        var playerController = FindObjectOfType<PlayerController>();
        var inventoryController = FindObjectOfType<InventoryController>();

        // Wrap in adapters
        if (leakManager != null)
        {
            Leaks = new LeakManagerAdapter(leakManager);
            ResetRegistry.Register(leakManager);
            Debug.Log("[GameCore] LeakManager registered");
        }
        else
        {
            Debug.LogError("[GameCore] LeakManager not found - LeakService will be null!");
        }

        if (itemPooler != null)
        {
            Items = new ItemPoolerAdapter(itemPooler);
            ResetRegistry.Register(itemPooler);
            Debug.Log("[GameCore] ItemPooler registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] ItemPooler not found - ItemService will be null");
        }

        if (resupplyManager != null)
        {
            Resupply = new ResupplyManagerAdapter(resupplyManager);
            ResetRegistry.Register((IResettable)Resupply);
            Debug.Log("[GameCore] ResupplyManagerAdapter registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] ResupplyManager not found - ResupplyService will be null");
        }

        if (difficultyManager != null)
        {
            var adapter = new DifficultyManagerAdapter(difficultyManager);
            Difficulty = adapter;
            ResetRegistry.Register(adapter);
            Debug.Log("[GameCore] DifficultyManager registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] DifficultyManager not found - DifficultyService will be null");
        }

        if (uiController != null)
        {
            var adapter = new UIControllerAdapter(uiController);
            HUD = adapter;
            ResetRegistry.Register(adapter);
            Debug.Log("[GameCore] UIController registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] UIController not found - HUDService will be null");
        }

        if (playerController != null)
        {
            var adapter = new PlayerControllerAdapter(playerController);
            Player = adapter;
            ResetRegistry.Register(adapter);
            Debug.Log("[GameCore] PlayerController registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] PlayerController not found - PlayerMovementService will be null");
        }

        // DevHUD service
        var devHud = FindObjectOfType<DevHUD>();
        if (devHud != null)
        {
            var adapter = new DevHudAdapter(devHud);
            DevHud = adapter;
            ResetRegistry.Register(adapter);
            Debug.Log("[GameCore] DevHUD registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] DevHUD not found - DevHudService will be null");
        }

        // Inventory controller registration
        if (inventoryController != null)
        {
            ResetRegistry.Register(inventoryController);
            Debug.Log("[GameCore] InventoryController registered");
        }
        else
        {
            Debug.LogWarning("[GameCore] InventoryController not found - inventory won't reset properly");
        }

        // Audio service remains null for now
        Audio = null;

        // Verify critical services
        Debug.Assert(Leaks != null, "LeakService is required!");
        Debug.Assert(Items != null, "ItemService is required!");

        // Create HUD update coordinator (after services are registered)
        if (hudCoordinator == null)
        {
            hudCoordinator = new HudUpdateCoordinator();
            ResetRegistry.Register(hudCoordinator);
            Debug.Log("[GameCore] HudUpdateCoordinator created and registered");
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

        // Create FutilityGameplaySystem
        if (futilitySystem == null)
        {
            futilitySystem = new FutilityGameplaySystem();
            ResetRegistry.Register(futilitySystem);
            Debug.Log("[GameCore] FutilityGameplaySystem created and registered");
        }

        // Queue transition to Running (deferred to Update to avoid re-entrancy)
        pendingStartRun = true;
        Debug.Log("[GameCore] Starting state complete - queued transition to Running");
    }

    private void HandleRunningState()
    {
        // Start session
        Session.StartSession();

        // Start services (when they exist)
        Leaks?.StartLeaks();
        Resupply?.StartResupply();

        // FutilitySystem responds to state changes automatically
        // No need to explicitly start it
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
        // FutilitySystem responds to state changes automatically
        // It will handle ending logic without calling EndGame()

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
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        float cleanStart = Time.realtimeSinceStartup;
        #endif

        // FutilitySystem handles its own cleanup via IResettable

        // Order matters for cleanup
        Leaks?.Clear();           // Stop particles first
        Items?.ClearAll();        // Return all items
        Resupply?.CancelAll();    // Cancel coroutines
        Audio?.StopAll();

        // Reset all registered objects using ResetRegistry
        ResetRegistry.ResetAll(); // Replaces ResettableExtensions.ResetAll()

        // Force garbage collection (WebGL consideration)
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        float cleanTime = (Time.realtimeSinceStartup - cleanStart) * 1000f;

        // Raised threshold to 8ms for realistic cleanup with full service suite
        // Use Warning for moderate overruns (8-15ms), Error only for severe (>15ms)
        if (cleanTime > 15f)
        {
            Debug.LogError($"[GameCore] SEVERE PERFORMANCE: Cleaning took {cleanTime:F2}ms (critical: >15ms)");
        }
        else if (cleanTime > 8f)
        {
            Debug.LogWarning($"[GameCore] PERFORMANCE: Cleaning took {cleanTime:F2}ms (target: 8ms)");
        }
        else
        {
            Debug.Log($"[GameCore] Cleaning completed in {cleanTime:F2}ms");
        }
        #endif

        // Auto-transition to results
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
        // Hide any lingering results UI first
        HUD?.HideResults();

        // Initialize menu state
        Leaks?.InitializeMenuState();
    }

    /// <summary>
    /// Update game session (called from game loop)
    /// </summary>
    void Update()
    {
        // Handle deferred Starting -> Running transition (avoids re-entrancy)
        if (pendingStartRun && Flow != null && Flow.CurrentState == GameFlowState.Starting)
        {
            pendingStartRun = false;
            Debug.Log("[GameCore] Executing deferred transition: Starting → Running");
            Flow.TransitionTo(GameFlowState.Running);
        }

        // Update session timer if running
        if (Flow != null && Flow.IsActive())
        {
            Session?.Tick(Time.deltaTime);

            // Push session data to DevHUD
            if (DevHud != null && Session != null)
            {
                // Get comprehensive stats and pass them to DevHUD
                SessionStats stats = Session.GetStats();
                DevHud.UpdateSessionStats(stats);

                DevHud.UpdateGameState(Flow.CurrentState, true);
            }

            // Push resupply status to DevHUD
            if (DevHud != null && Resupply != null)
            {
                DevHud.UpdateResupplyStatus(
                    Resupply.IsActive,
                    Resupply.GetTimeToNextAirDrop(),
                    Resupply.GetTimeToNextBarge(),
                    Resupply.ActivePackageCount
                );
            }

            // Push leak status to DevHUD
            if (DevHud != null && Leaks != null)
            {
                DevHud.UpdateLeakStatus(
                    Leaks.TotalParticleCount,
                    Leaks.PressurePercentage,
                    Leaks.ActiveLeakCount
                );
            }

            // Update FutilityGameplaySystem
            if (futilitySystem != null)
            {
                futilitySystem.Update();
            }

            // Update HUD coordinator (manages UI refresh rate)
            if (hudCoordinator != null)
            {
                hudCoordinator.Update();
            }
        }

        // Performance monitoring (every 5 seconds)
        if (Time.frameCount % 300 == 0 && debugMode)
        {
            string leakStatus = Leaks != null ? $"Clean:{Leaks.IsClean}" : "null";
            string itemStatus = Items != null ? $"Active:{Items.ActiveItemCount}" : "null";
            string resupplyStatus = Resupply != null ? $"Active:{Resupply.IsMajorEventActive}" : "null";

            Debug.Log($"[GameCore Monitor] Leaks:{leakStatus}, Items:{itemStatus}, Resupply:{resupplyStatus}");
        }
    }

    // ============================================================
    // PUBLIC API - External systems use these to control game flow
    // ============================================================

    /// <summary>
    /// Start a new game session
    /// Transitions: Menu → Starting → Running (automatic chain)
    /// </summary>
    public static void StartGame()
    {
        if (!IsInitialized || Flow == null)
        {
            Debug.LogError("[GameCore] Cannot start - not initialized");
            return;
        }

        Debug.Log("[GameCore] StartGame called - transitioning to Starting state");
        Flow.TransitionTo(GameFlowState.Starting);
    }

    /// <summary>
    /// End the current game
    /// Transitions: Running → Ending → Cleaning → ShowingResults (automatic chain)
    /// </summary>
    public static void EndGame()
    {
        if (!IsInitialized || Flow == null) return;

        Debug.Log("[GameCore] EndGame called - transitioning to Ending state");
        Flow.TransitionTo(GameFlowState.Ending);
    }

    /// <summary>
    /// Restart the game (return to menu, ready for new start)
    /// </summary>
    public static void RestartGame()
    {
        if (!IsInitialized || Flow == null) return;

        Debug.Log("[GameCore] RestartGame called - returning to Menu state");
        Flow.TransitionTo(GameFlowState.Menu);
    }

    /// <summary>
    /// Pause the game
    /// </summary>
    public static void PauseGame()
    {
        if (!IsInitialized || Flow == null) return;
        
        if (Flow.CurrentState == GameFlowState.Running)
        {
            Debug.Log("[GameCore] PauseGame called - transitioning to Paused state");
            Flow.TransitionTo(GameFlowState.Paused);
        }
    }

    /// <summary>
    /// Resume from pause
    /// </summary>
    public static void ResumeGame()
    {
        if (!IsInitialized || Flow == null) return;
        
        if (Flow.CurrentState == GameFlowState.Paused)
        {
            Debug.Log("[GameCore] ResumeGame called - returning to Running state");
            Flow.TransitionTo(GameFlowState.Running);
        }
    }


    /// <summary>
    /// Safe accessor for services with null checks
    /// </summary>
    public static ILeakService GetLeakService()
    {
        if (instance == null)
        {
            Debug.LogError("GameCore not initialized!");
            return null;
        }
        return Leaks;
    }

    public static IItemService GetItemService()
    {
        if (instance == null)
        {
            Debug.LogError("GameCore not initialized!");
            return null;
        }
        return Items;
    }

    public static IResupplyService GetResupplyService()
    {
        if (instance == null)
        {
            Debug.LogError("GameCore not initialized!");
            return null;
        }
        return Resupply;
    }

    public static GameSession GetSession()
    {
        if (instance == null)
        {
            Debug.LogError("GameCore not initialized!");
            return null;
        }
        return Session;
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

    /// <summary>
    /// Get FutilityGameplaySystem for integrity queries
    /// </summary>
    public static FutilityGameplaySystem FutilitySystem => futilitySystem;

    #if UNITY_EDITOR
    /// <summary>
    /// Editor-only helper to reset all static state for Enter Play Mode without domain reload
    /// </summary>
    public static bool EditorResetStatics()
    {
        bool hadState = false;

        // Clear singleton instance
        if (instance != null)
        {
            instance = null;
            hadState = true;
        }

        // Clear services
        if (Leaks != null) { Leaks = null; hadState = true; }
        if (Items != null) { Items = null; hadState = true; }
        if (Resupply != null) { Resupply = null; hadState = true; }
        if (Audio != null) { Audio = null; hadState = true; }
        if (Difficulty != null) { Difficulty = null; hadState = true; }
        if (HUD != null) { HUD = null; hadState = true; }
        if (Player != null) { Player = null; hadState = true; }

        // Clear core systems
        if (Flow != null)
        {
            // Unsubscribe any lingering event handlers
            // Event cleanup handled by setting Flow to null
            Flow = null;
            hadState = true;
        }

        if (Session != null)
        {
            Session.Dispose();
            Session = null;
            hadState = true;
        }

        // Clear FutilityGameplaySystem
        if (futilitySystem != null)
        {
            futilitySystem = null;
            hadState = true;
        }

        return hadState;
    }
    #endif
}