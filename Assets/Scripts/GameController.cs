using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
    {// Singleton instance made read-only
    public static GameController Instance { get; private set; }
    public GameState gameState; // Reference to GameState Scriptable Object
    public PlayerProfile playerProfile; // Reference to PlayerProfile Scriptable Object
    public OilLeakData oilLeakData; // Reference to OilLeakData Scriptable Object
    public GameRulesConfig gameRulesConfig; // Reference to GameRulesConfig Scriptable Object
    public InventoryController inventoryController;
    public UIController uiController;
    private RoundState previousRoundState;
    private RoundLocationData currentRoundLocationData; // The current round's data
    public RoundLocationData testRoundData; // TEMPORARY: For testing auto-start
    public ScoringManager scoringManager;
    public bool roundStarted = false;

    // NEW: Game Mode Support
    private IGameMode currentMode;
    public bool useEndlessMode = true; // Toggle in Inspector for testing
    // Activate Singleton 
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }    
    // Initialize the game state
    void Start()
    {
        // TEMPORARY: Initialize GameCore if not already present
        // This will be moved to a persistent scene later
        if (!GameCore.IsInitialized)
        {
            Debug.Log("GameController: Creating GameCore...");
            GameObject coreObject = new GameObject("GameCore");
            coreObject.AddComponent<GameCore>();

            // Add migration adapters
            GameObject adapterObject = new GameObject("MigrationAdapters");
            var stateAdapter = adapterObject.AddComponent<GameStateAdapter>();
            var oilAdapter = adapterObject.AddComponent<OilLeakDataAdapter>();

            // Wire up legacy references (these would be set in Inspector normally)
            // For now, using the existing references
            Debug.LogWarning("MIGRATION: Temporary adapters active. Remove after full refactor!");
        }

        InitializeGameState();
        uiController.UpdatePlayerProfileUI();

        // NEW: Initialize game mode
        if (useEndlessMode)
        {
            Debug.Log("Initializing Endless Mode");
            // Create endless mode with proper dependency injection
            currentMode = new EndlessMode(this);
            // Don't auto-start - wait for player to press E
        }
        else if (testRoundData != null)
        {
            // Keep old round system for backwards compatibility
            Debug.Log("Auto-starting round with test data");
            StartNewRound(testRoundData);
        }
    }
    void InitializeGameState()
    {
        Debug.Log("GameState instance ID in GameController: " + gameState.GetInstanceID());
        gameState.roundState = RoundState.NotStarted;
        gameState.score = 0;
        gameState.highScore = 0;
        // gameState.currency = 0; // Removed - futility simulator uses only score
    }
    // Input handling for restart
    private float restartHoldTime = 0f;
    private const float restartHoldDuration = 1f; // Hold R for 1 second
    private bool restartConfirmed = false;
    private bool showRestartProgress = false;

    // Update and check round-over conditions
    void Update()
    {
        // NEW: Handle input for pause and restart
        HandleGameInput();

        // NEW: Route through game mode if active
        if (currentMode != null && currentMode.IsActive)
        {
            currentMode.Tick(Time.deltaTime);
            return; // Skip all round-based logic
        }

        // OLD: Keep round system for backwards compatibility
        if (!useEndlessMode)
        {
            RoundState currentRoundState = GetCurrentRoundState();
            HandleRoundStateLogic(currentRoundState);
            CheckForStateChanges(currentRoundState);
        }

        // NEW: Check for endless mode start (Press E)
        if (useEndlessMode && currentMode != null && !currentMode.IsActive)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("Starting Endless Mode!");
                currentMode.StartMode();
            }
        }
    }

    void HandleGameInput()
    {
        // Pause/Resume with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentMode != null && currentMode.IsActive)
            {
                if (currentMode.IsPaused)
                {
                    Debug.Log("Resuming game...");
                    currentMode.Resume();
                }
                else
                {
                    Debug.Log("Pausing game...");
                    currentMode.Pause();
                }
            }
        }

        // Pause with Tab or Right Click (for radial menu in future)
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(1))
        {
            if (currentMode != null && currentMode.IsActive && !currentMode.IsPaused)
            {
                Debug.Log("Pausing for radial menu...");
                currentMode.Pause();
                // TODO: Open radial menu UI
            }
        }

        // Hold R to restart
        if (currentMode != null && currentMode.IsActive)
        {
            if (Input.GetKey(KeyCode.R))
            {
                restartHoldTime += Time.unscaledDeltaTime;
                showRestartProgress = true;

                if (restartHoldTime >= restartHoldDuration && !restartConfirmed)
                {
                    restartConfirmed = true;
                    showRestartProgress = false;
                    Debug.Log("Restart confirmed! Restarting game...");

                    // End current mode and restart
                    currentMode.EndMode("Player Restart");

                    // Small delay then start new game
                    Invoke(nameof(RestartGame), 0.5f);
                }
            }
            else if (Input.GetKeyUp(KeyCode.R))
            {
                if (restartHoldTime < restartHoldDuration && restartHoldTime > 0)
                {
                    Debug.Log($"Hold R for {restartHoldDuration} seconds to restart (held for {restartHoldTime:F1}s)");
                }
                restartHoldTime = 0f;
                restartConfirmed = false;
                showRestartProgress = false;
            }
        }
    }

    void RestartGame()
    {
        if (currentMode != null)
        {
            Debug.Log("Starting new game...");
            currentMode.StartMode();
        }
    }

    void OnGUI()
    {
        // Show restart progress bar
        if (showRestartProgress && !restartConfirmed)
        {
            float progress = restartHoldTime / restartHoldDuration;

            // Center of screen
            float barWidth = 300f;
            float barHeight = 30f;
            float x = (Screen.width - barWidth) / 2;
            float y = Screen.height / 2 - 100;

            // Background
            GUI.Box(new Rect(x - 5, y - 25, barWidth + 10, barHeight + 30), "");

            // Text
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 16;
            GUI.Label(new Rect(x, y - 20, barWidth, 20), "Hold R to Restart", labelStyle);

            // Progress bar background
            GUI.Box(new Rect(x, y, barWidth, barHeight), "");

            // Progress bar fill
            GUI.color = Color.red;
            GUI.Box(new Rect(x + 2, y + 2, (barWidth - 4) * progress, barHeight - 4), "");
            GUI.color = Color.white;

            // Progress text
            labelStyle.fontSize = 12;
            GUI.Label(new Rect(x, y + 5, barWidth, 20), $"{(progress * 100):F0}%", labelStyle);
        }

        // Show paused indicator
        if (currentMode != null && currentMode.IsPaused)
        {
            GUIStyle pauseStyle = new GUIStyle(GUI.skin.label);
            pauseStyle.alignment = TextAnchor.MiddleCenter;
            pauseStyle.fontSize = 32;
            pauseStyle.fontStyle = FontStyle.Bold;

            float width = 200;
            float height = 50;
            float x = (Screen.width - width) / 2;
            float y = 50;

            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.Box(new Rect(x - 5, y - 5, width + 10, height + 10), "");

            GUI.color = Color.yellow;
            GUI.Label(new Rect(x, y, width, height), "PAUSED", pauseStyle);

            pauseStyle.fontSize = 14;
            pauseStyle.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(x, y + 40, width, 20), "Press ESC to Resume", pauseStyle);
            GUI.color = Color.white;
        }
    }

    RoundState GetCurrentRoundState()
    {
        return gameState.roundState;
    }

    void HandleRoundStateLogic(RoundState currentRoundState)
    {
        switch (currentRoundState)
        {
            case RoundState.Active:
                HandleActiveState();
                break;
            
            case RoundState.Over:
                HandleOverState();
                break;
        }
    }

    void HandleActiveState()
    {
        // Increment timer
        gameState.timer += Time.deltaTime;

        UpdateRealTimeScore(); // Update the score in real-time
        CheckRoundOverConditions();
    }

    void HandleOverState()
    {
        uiController.ShowRoundOverUI();
        gameState.roundState = RoundState.NotStarted; // Reset the round state to NotStarted after showing UI
    }

    void CheckForStateChanges(RoundState currentRoundState)
    {
        if (currentRoundState != previousRoundState)
        {
            // Update previousRoundState
            previousRoundState = currentRoundState;

            // Call the UpdatePlayerProfileUI method in UIController
            uiController.UpdatePlayerProfileUI();
        }
    }


    // Check if the round is over based on the timer
    void CheckRoundOverConditions()
    {
        if (gameState.timer >= currentRoundLocationData.gameTimerData.roundDuration) 
        {
            gameState.roundState = RoundState.Over;
            scoringManager.CalculateGrade();
            UpdatePlayerProfile(); // Save scores to PlayerProfile
            uiController.UpdateUI();
        }
    }

    void UpdateRealTimeScore()
    {
        gameState.score = currentRoundLocationData.oilLeakData.particlesBlocked * 1000;
        uiController.UpdateUI();
    }

    void UpdatePlayerProfile()
    {
        playerProfile.totalScore += gameState.score;
        // playerProfile.totalCurrency += gameState.currency; // Removed - futility simulator uses only score

        // Update scoresByLocation dictionary
        if (playerProfile.scoresByLocation.ContainsKey(currentRoundLocationData.name))
        {
            // Update the score for this location if it's higher than the previous
            if (gameState.score > playerProfile.scoresByLocation[currentRoundLocationData.name])
            {
                playerProfile.scoresByLocation[currentRoundLocationData.name] = gameState.score;
            }
        }
        else
        {
            playerProfile.scoresByLocation.Add(currentRoundLocationData.name, gameState.score);
        }
    }
    public float GetCurrentRoundDuration()
    {
        if (currentRoundLocationData != null && currentRoundLocationData.gameTimerData != null)
        {
            return currentRoundLocationData.gameTimerData.roundDuration;
        }
        else
        {
            return -1; // Return -1 to indicate no active round
        }
    }
    // Common method to reset game state
    public void ResetGameState(RoundState newRoundState)
    {
        gameState.score = 0;
        gameState.timer = 0;
        gameState.roundState = newRoundState;
        uiController.UpdateUI();
    }

    public void StartNewRound(RoundLocationData roundLocationData)
    {
        // Set the current round's data
        currentRoundLocationData = roundLocationData;
        oilLeakData = roundLocationData.oilLeakData;

        // Reset game state parameters
        ResetGameState(RoundState.Active);
        inventoryController.itemsUsedThisRound = 0;
        // Capture the initial state of the inventory
        int initialTotalItems = inventoryController.TotalAvailableItems();
        gameState.initialTotalItems = initialTotalItems;
        Debug.Log($"Initial total items available: {initialTotalItems}");

        // Reactivate game-related components
        OilController oilController = FindObjectOfType<OilController>();
        if (oilController)
        {
            oilController.ResetOilSystem();
        }

        GameTimer.Instance.ResetTimer();  // Using Singleton instance
    }
    public void ContinueGame()
    {
        uiController.HideRoundOverUI();
        ResetGameState(RoundState.NotStarted);
        scoringManager.scoreSummary = null;
    }

}
