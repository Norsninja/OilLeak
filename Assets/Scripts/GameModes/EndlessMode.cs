using UnityEngine;

/// <summary>
/// Endless survival mode - the core of our futility simulator
/// Oil keeps coming, difficulty increases, you can't win
/// </summary>
public class EndlessMode : IGameMode
{
    // State
    private bool isActive = false;
    private bool isPaused = false;
    public float timeElapsed { get; private set; } = 0f;
    private bool debugStateChanges = true;

    // Milestones (in seconds)
    private readonly int[] milestones = { 60, 120, 300, 600, 900, 1500, 3000 }; // 1, 2, 5, 10, 15, 25, 50 minutes
    private readonly string[] milestoneNames = {
        "Local Hero",
        "Gulf Guardian",
        "BP's Nightmare",
        "Congressional Medal",
        "International Hero",
        "Literally Better Than BP",
        "YOU CAPPED THE WELL!"
    };
    private int currentMilestoneIndex = 0;

    // References (injected via constructor - no FindObjectOfType!)
    private GameController gameController;
    private UIController uiController;
    private GameState gameState;
    private OilLeakData oilLeakData; // Shared data for particle counts
    private DifficultyManager difficultyManager;
    private LeakManager leakManager;
    private GameRulesConfig gameRules; // Configuration for win/lose conditions

    // Warning state
    private bool hasShownNearFailWarning = false;

    // Constructor with dependency injection
    public EndlessMode(GameController controller)
    {
        gameController = controller;
        gameState = controller.gameState;
        uiController = controller.uiController;
        oilLeakData = controller.oilLeakData; // Get shared oil leak data

        // Get managers (these should ideally be injected too, but we'll find them for now)
        difficultyManager = GameObject.FindObjectOfType<DifficultyManager>();
        if (difficultyManager == null)
        {
            GameObject managerObj = new GameObject("DifficultyManager");
            difficultyManager = managerObj.AddComponent<DifficultyManager>();
        }

        leakManager = LeakManager.Instance; // Use singleton
        if (leakManager == null)
        {
            leakManager = GameObject.FindObjectOfType<LeakManager>();
            if (leakManager == null)
            {
                GameObject managerObj = new GameObject("LeakManager");
                leakManager = managerObj.AddComponent<LeakManager>();
            }
        }

        // Try to get game rules from GameController first (if assigned in Inspector)
        if (controller != null && controller.gameRulesConfig != null)
        {
            gameRules = controller.gameRulesConfig;
            Debug.Log("EndlessMode: Using GameRulesConfig from GameController");
        }
        else
        {
            // Fallback to Resources folder
            gameRules = Resources.Load<GameRulesConfig>("GameRulesConfig");
            if (gameRules != null)
            {
                Debug.Log("EndlessMode: Loaded GameRulesConfig from Resources");
            }
        }

        if (gameRules == null)
        {
            Debug.LogWarning("GameRulesConfig not found! Creating default rules. Please assign in GameController or create in Resources folder.");
            gameRules = ScriptableObject.CreateInstance<GameRulesConfig>();
        }

        // Initialize menu state on construction
        if (leakManager != null)
        {
            leakManager.InitializeMenuState();
        }

        // Log what we have
        if (debugStateChanges)
        {
            Debug.Log($"EndlessMode initialized - GameState: {gameState != null}, UI: {uiController != null}, " +
                     $"OilLeakData: {oilLeakData != null}, Difficulty: {difficultyManager != null}, " +
                     $"Leaks: {leakManager != null}, Rules: {gameRules != null}");
        }
    }

    // IGameMode implementation
    public bool IsActive => isActive;
    public bool IsPaused => isPaused;

    public void StartMode()
    {
        Debug.Log("Starting Endless Mode - How long can you delay the inevitable?");

        isActive = true;
        isPaused = false;
        timeElapsed = 0f;
        currentMilestoneIndex = 0;
        hasShownNearFailWarning = false;

        // Reset game state for endless
        if (gameState != null)
        {
            gameState.roundState = RoundState.Active;
            gameState.timer = 0f;
            gameState.score = 0;
            // gameState.currency = 0; // Removed - futility simulator uses only score
        }

        // Reset shared oil leak data
        if (oilLeakData != null)
        {
            oilLeakData.ResetCounts();
        }

        // Start the run via LeakManager (handles all leak spawning and management)
        if (leakManager != null)
        {
            leakManager.StartRun();
        }
        else
        {
            Debug.LogError("EndlessMode: LeakManager is null! Cannot start oil leaks.");
        }

        // Reset difficulty system
        if (difficultyManager != null)
        {
            difficultyManager.ResetDifficulty();
        }

        // Start resupply system
        ResupplyManager resupplyManager = Object.FindObjectOfType<ResupplyManager>();
        if (resupplyManager != null)
        {
            resupplyManager.StartResupply();
        }

        // UI updates handled by HudUpdateCoordinator
    }

    public void Tick(float deltaTime)
    {
        if (!isActive || isPaused) return;

        // Update time
        timeElapsed += deltaTime;
        if (gameState != null)
        {
            gameState.timer = timeElapsed;
        }

        // Check for milestones
        CheckMilestones();

        // DifficultyManager handles difficulty updates automatically
        // LeakManager handles leak spawning automatically

        // UI updates handled by HudUpdateCoordinator

        // Check game over conditions
        CheckGameOver();
    }

    public void Pause()
    {
        if (!isActive) return;

        isPaused = true;

        // Option A: Keep Time.timeScale = 1, just pause emissions/collisions
        // This allows physics to continue but oil stops flowing
        if (leakManager != null)
        {
            leakManager.PauseRun();
        }

        // Pause resupply
        ResupplyManager resupplyManager = Object.FindObjectOfType<ResupplyManager>();
        if (resupplyManager != null)
        {
            resupplyManager.PauseResupply();
        }

        // Option B: If you want to freeze time globally (uncomment):
        // Time.timeScale = 0f;

        Debug.Log("Endless Mode Paused");
    }

    public void Resume()
    {
        if (!isActive) return;

        isPaused = false;

        // Resume emissions/collisions
        if (leakManager != null)
        {
            leakManager.ResumeRun();
        }

        // Resume resupply
        ResupplyManager resupplyManager = Object.FindObjectOfType<ResupplyManager>();
        if (resupplyManager != null)
        {
            resupplyManager.ResumeResupply();
        }

        // Restore time scale if it was paused (Option B):
        // Time.timeScale = 1f;

        Debug.Log("Endless Mode Resumed");
    }

    public void EndMode(string reason)
    {
        if (!isActive) return;

        isActive = false;

        Debug.Log($"Endless Mode Ended: {reason}");
        Debug.Log($"Final Stats - Time: {timeElapsed:F1}s, Score: {gameState?.score}, " +
                 $"Blocked: {oilLeakData?.particlesBlocked}, Escaped: {oilLeakData?.particlesEscaped}");

        // End the run via LeakManager (clears particles, stops emissions)
        if (leakManager != null)
        {
            leakManager.EndRun();
            // Return to menu state after ending
            leakManager.InitializeMenuState();
        }

        // End resupply
        ResupplyManager resupplyManager = Object.FindObjectOfType<ResupplyManager>();
        if (resupplyManager != null)
        {
            resupplyManager.EndResupply();
        }

        // Ensure time scale is normal
        Time.timeScale = 1f;

        // Update game state
        if (gameState != null)
        {
            gameState.roundState = RoundState.Over;
        }

        // Calculate final grade before showing UI
        GameController gameController = GameController.Instance;
        if (gameController != null && gameController.scoringManager != null)
        {
            gameController.scoringManager.CalculateGrade();
        }

        // Transition GameCore state machine to ending
        if (GameCore.IsInitialized)
        {
            Debug.Log("[EndlessMode] Signaling GameCore to end game...");
            GameCore.EndGame(); // Running → Ending → Cleaning → ShowingResults
        }

        // Show game over UI
        if (uiController != null)
        {
            uiController.ShowRoundOverUI();
        }
    }

    public GameModeStats GetStats()
    {
        var stats = new GameModeStats
        {
            timeElapsed = timeElapsed,
            score = gameState?.score ?? 0,
            particlesBlocked = oilLeakData?.particlesBlocked ?? 0,
            particlesEscaped = oilLeakData?.particlesEscaped ?? 0,
            currentDifficulty = GetCurrentDifficultyMultiplier(),
            currentMilestone = currentMilestoneIndex,
            gallonsDelayed = CalculateGallonsDelayed(),
            activeLeaks = leakManager?.GetActiveLeakCount() ?? 0,
            pressureLevel = leakManager?.GetPressurePercentage() ?? 0f
        };
        return stats;
    }

    // Private methods
    private void CheckMilestones()
    {
        if (currentMilestoneIndex >= milestones.Length) return;

        if (timeElapsed >= milestones[currentMilestoneIndex])
        {
            string milestoneName = milestoneNames[currentMilestoneIndex];
            Debug.Log($"MILESTONE REACHED: {milestoneName} at {timeElapsed:F1} seconds!");

            // TODO: Trigger milestone UI notification
            // TODO: Trigger milestone audio
            // TODO: Update news ticker

            currentMilestoneIndex++;

            // No victory - just continue with harder difficulty
            // 50 minutes is an achievement, not a win
            // The oil always wins eventually
        }
    }

    private float GetCurrentDifficultyMultiplier()
    {
        // Get from DifficultyManager if available
        if (difficultyManager != null)
        {
            return difficultyManager.GetCurrentMultiplier();
        }

        // Fallback calculation
        float minutes = timeElapsed / 60f;
        return Mathf.Pow(1.15f, minutes);
    }

    private int CalculateGallonsDelayed()
    {
        // Each particle = 100 gallons (for readable numbers)
        int blocked = oilLeakData?.particlesBlocked ?? 0;
        return blocked * 100;
    }

    private void CheckGameOver()
    {
        if (gameRules == null || oilLeakData == null) return;

        // NO VICTORY CONDITION - The oil always wins
        // Game only ends when too much oil escapes

        // Get current escape threshold (may scale with time)
        int maxEscaped = gameRules.GetScaledMaxEscaped(timeElapsed);
        int currentEscaped = oilLeakData.particlesEscaped;

        // Check failure condition (too much oil escaped)
        if (currentEscaped >= maxEscaped)
        {
            // Format survival time
            int minutes = Mathf.FloorToInt(timeElapsed / 60f);
            int seconds = Mathf.FloorToInt(timeElapsed % 60f);
            int gallonsDelayed = oilLeakData.particlesBlocked * 100;

            Debug.Log($"The oil won after {minutes}:{seconds:00}. Blocked {gallonsDelayed:N0} gallons.");
            EndMode($"The oil won. You delayed {minutes}:{seconds:00} and blocked {gallonsDelayed:N0} gallons.");
            return;
        }

        // Check for near-fail warning
        if (!hasShownNearFailWarning)
        {
            int warningThreshold = gameRules.GetWarningThreshold(timeElapsed);
            if (currentEscaped >= warningThreshold)
            {
                hasShownNearFailWarning = true;
                float percent = gameRules.GetEscapedPercentage(currentEscaped, timeElapsed) * 100f;
                Debug.LogWarning($"WARNING: {percent:F0}% of oil escape limit reached!");

                // TODO: Trigger warning toast/UI feedback
                // if (ToastManager.Instance != null)
                // {
                //     ToastManager.Instance.ShowToast($"WARNING: {percent:F0}% oil escaped!", ToastType.Warning);
                // }
            }
        }
    }
}