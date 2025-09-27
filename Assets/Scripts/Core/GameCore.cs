using UnityEngine;
using System;
using System.Threading.Tasks;
using Core;
using Core.Services;
using Core.Systems;
using OilLeak.Toast.Services;

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
    public static IItemLookupService ItemLookup { get; private set; }
    public static IResupplyService Resupply { get; private set; }
    public static IAudioService Audio { get; private set; }
    public static IDifficultyService Difficulty { get; private set; }
    public static IHUDService HUD { get; private set; }
    public static IPlayerMovementService Player { get; private set; }
    public static IDevHudService DevHud { get; private set; }
    public static ILeaderboardService Leaderboards { get; private set; }

    // Toast system services
    public static IGameStateProvider ToastState { get; private set; }
    public static IToastService Toasts { get; private set; }

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
    [SerializeField] private OilLeak.Inventory.ItemCatalog itemCatalog; // Central item catalog

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

    void Start()
    {
        // Apply initial state after all Awake() calls complete
        if (Flow == null) return;

        Debug.Log($"[GameCore] Start - Applying initial state: {Flow.CurrentState}");

        // Initialize leaderboard service asynchronously (fire-and-forget)
        InitializeLeaderboardsAsync();

        // Handle whatever state we're starting in
        switch (Flow.CurrentState)
        {
            case GameFlowState.Menu:
                HandleMenuState();
                break;
            case GameFlowState.Starting:
                HandleStartingState();
                break;
            case GameFlowState.Running:
                HandleRunningState(GameFlowState.Menu); // Assume from Menu if starting in Running
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
        }
    }

    /// <summary>
    /// Initialize leaderboard service asynchronously
    /// </summary>
    private async void InitializeLeaderboardsAsync()
    {
        if (Leaderboards == null)
        {
            Debug.LogWarning("[GameCore] No leaderboard service to initialize");
            return;
        }

        bool success = await Leaderboards.InitializeAsync();
        if (success)
        {
            Debug.Log("[GameCore] Leaderboard service initialized successfully");
        }
        else
        {
            Debug.LogWarning("[GameCore] Leaderboard service initialization failed - continuing offline");
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

        // ItemCatalog service
        if (itemCatalog != null)
        {
            ItemLookup = new ItemCatalogAdapter(itemCatalog);
            Debug.Log("[GameCore] ItemCatalog registered as ItemLookupService");
        }
        else
        {
            Debug.LogWarning("[GameCore] ItemCatalog not assigned - ItemLookupService will be null");
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

        // Register SoundtrackManager as audio service
        var soundtrackManager = FindObjectOfType<OilLeak.Audio.SoundtrackManager>();
        if (soundtrackManager != null)
        {
            Audio = soundtrackManager;
            ResetRegistry.Register(soundtrackManager);
            Debug.Log("[GameCore] SoundtrackManager registered as Audio service");
        }
        else
        {
            Audio = null;
            Debug.LogWarning("[GameCore] SoundtrackManager not found - Audio service will be null");
        }

        // Register UgsLeaderboardService as leaderboard service
        var leaderboardService = FindObjectOfType<OilLeak.Online.UgsLeaderboardService>();
        if (leaderboardService != null)
        {
            Leaderboards = leaderboardService;
            ResetRegistry.Register(leaderboardService);
            Debug.Log("[GameCore] UgsLeaderboardService registered as Leaderboard service");
        }
        else
        {
            Leaderboards = null;
            Debug.LogWarning("[GameCore] UgsLeaderboardService not found - Leaderboard service will be null");
        }

        // Register Toast system services
        if (Session != null)
        {
            // Create the state provider that connects game systems to toasts
            ToastState = new ToastGameStateProvider(Session);
            ResetRegistry.Register((IResettable)ToastState);
            Debug.Log("[GameCore] ToastGameStateProvider created and registered");

            // Create the toast manager with the provider
            var toastManager = new ToastManager(ToastState);

            // Initialize with voice profiles (will log errors if loading fails)
            toastManager.Initialize();

            // Only set as active service if initialization succeeded
            if (toastManager.IsReady)
            {
                Toasts = toastManager;
                Debug.Log("[GameCore] ToastManager initialized and ready");
            }
            else
            {
                Debug.LogWarning("[GameCore] ToastManager failed to initialize - toasts disabled");
                Toasts = null;
            }
        }
        else
        {
            Debug.LogWarning("[GameCore] Session not available - Toast services will be null");
            ToastState = null;
            Toasts = null;
        }

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
                HandleRunningState(oldState);
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
                // Reset when restarting from Round Over (bypasses Cleaning state)
                // This handles the ShowingResults → Menu transition that skips normal cleanup
                if (oldState == GameFlowState.ShowingResults)
                {
                    Debug.Log("[GameCore] Direct ShowingResults → Menu transition - calling ResetRegistry.ResetAll()");
                    ResetRegistry.ResetAll();
                }
                HandleMenuState();
                break;
        }
    }

    private void HandleStartingState()
    {
        // Reset session
        Session.Reset();

        // Stop toasts for fresh run
        Toasts?.StopToasting();

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

    private void HandleRunningState(GameFlowState fromState)
    {
        // Start session
        Session.StartSession();

        // Handle services based on where we're coming from
        if (fromState == GameFlowState.Starting)
        {
            // Fresh run - start all services
            Leaks?.StartLeaks();
            Resupply?.StartResupply();
            Toasts?.StartToasting();
            Player?.EnableMovement(true);
            Debug.Log("[GameCore] Starting fresh run - all services started");
        }
        else if (fromState == GameFlowState.Paused)
        {
            // Resuming from pause - resume services (don't restart)
            Leaks?.ResumeLeaks();
            Resupply?.ResumeResupply();
            Toasts?.ResumeToasting();
            Player?.EnableMovement(true);

            // Hide pause UI
            var uiController = FindObjectOfType<UIController>();
            uiController?.HidePauseUI();

            Debug.Log("[GameCore] Resuming from pause - services resumed");
        }
        else
        {
            Debug.LogWarning($"[GameCore] HandleRunningState called from unexpected state: {fromState}");
        }

        // FutilitySystem responds to state changes automatically
        // No need to explicitly start it
    }

    private void HandlePausedState()
    {
        // Pause services (when they exist)
        Leaks?.PauseLeaks();
        Resupply?.PauseResupply();
        Audio?.PauseAll();
        Toasts?.PauseToasting();
        Player?.EnableMovement(false);

        // Show pause UI
        var uiController = FindObjectOfType<UIController>();
        uiController?.ShowPauseUI();

        Debug.Log("[GameCore] Paused - all services paused, player movement disabled");
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
        Toasts?.StopToasting();

        // Transition to cleaning
        Flow.TransitionTo(GameFlowState.Cleaning);
    }

    private void HandleCleaningState()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        float cleanStart = Time.realtimeSinceStartup;
        #endif

        // Stop toasts and reset state provider
        Toasts?.StopToasting();
        (ToastState as IResettable)?.Reset();

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

    // Track if scores have been submitted this session
    private bool scoresSubmitted = false;

    private async void HandleShowingResultsState()
    {
        // Display results (UI will handle this)
        var stats = Session.GetStats();
        Debug.Log($"Game Over - Time: {stats.TimeElapsed:F1}s, Gallons: {stats.GallonsDelayed}");

        // Check for player name BEFORE submitting scores
        if (!scoresSubmitted && Leaderboards != null && Leaderboards.IsReady)
        {
            scoresSubmitted = true;

            // Check if player has a custom name
            var ugsLeaderboard = Leaderboards as OilLeak.Online.UgsLeaderboardService;
            if (ugsLeaderboard != null && !ugsLeaderboard.HasCustomPlayerName())
            {
                Debug.Log("[GameCore] No player name found - prompting before score submission");

                // Show name prompt and wait for completion
                var leaderboardUI = UnityEngine.Object.FindObjectOfType<OilLeak.UI.LeaderboardUIController>();
                if (leaderboardUI != null)
                {
                    bool nameEntered = await leaderboardUI.PromptForPlayerNameAsync();
                    if (!nameEntered)
                    {
                        Debug.Log("[GameCore] Player cancelled name entry - skipping score submission");
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning("[GameCore] LeaderboardUIController not found - submitting with anonymous name");
                }
            }

            // NOW submit scores with the correct name
            await SubmitScoresAsync(stats);
        }
    }

    /// <summary>
    /// Submit scores to all leaderboards asynchronously
    /// </summary>
    private async Task SubmitScoresAsync(SessionStats stats)
    {
        // Use SessionStats as the single source of truth for final score
        int finalScore = stats.Score;

        Debug.Log($"[GameCore] Submitting score - Total: {finalScore} (Gallons: {stats.GallonsDelayed}, Time: {stats.TimeElapsed:F0}s)");

        // Submit to the single leaderboard (per-run final score)
        var totalTask = Leaderboards.SubmitScoreAsync(LeaderboardIds.EndlessTotalScore, finalScore);
        bool ok = await totalTask;
        if (ok)
            Debug.Log("[GameCore] Score submitted successfully");
        else
            Debug.LogWarning("[GameCore] Score submission failed");
    }

    private void HandleMenuState()
    {
        // Hide any lingering results UI first
        HUD?.HideResults();

        // Reset submission flag for next run
        scoresSubmitted = false;

        // Play menu music
        if (Audio != null)
        {
            Audio.PlayMusic(MusicType.Menu);
            Debug.Log("[GameCore] Starting menu music");
        }

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
        if (DevHud != null) { DevHud = null; hadState = true; }

        // Clear toast services
        if (ToastState != null)
        {
            if (ToastState is IDisposable disposableState)
            {
                disposableState.Dispose();
            }
            ToastState = null;
            hadState = true;
        }
        if (Toasts != null) { Toasts = null; hadState = true; }

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
