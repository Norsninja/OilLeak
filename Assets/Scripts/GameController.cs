using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
    {// Singleton instance made read-only
    public static GameController Instance { get; private set; }
    public GameState gameState; // Reference to GameState Scriptable Object
    public PlayerProfile playerProfile; // Reference to PlayerProfile Scriptable Object
    public OilLeakData oilLeakData; // Reference to OilLeakData Scriptable Object
    public InventoryController inventoryController; 
    public UIController uiController;
    private RoundState previousRoundState;
    private RoundLocationData currentRoundLocationData; // The current round's data
    public ScoringManager scoringManager;
    public bool roundStarted = false;
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
        InitializeGameState();
        uiController.UpdatePlayerProfileUI();
    }
    void InitializeGameState()
    {
        Debug.Log("GameState instance ID in GameController: " + gameState.GetInstanceID());
        gameState.roundState = RoundState.NotStarted;
        gameState.score = 0;
        gameState.highScore = 0;
        gameState.currency = 0;
    }
    // Update and check round-over conditions
    void Update()
    {
        RoundState currentRoundState = GetCurrentRoundState();
        
        HandleRoundStateLogic(currentRoundState);
        
        CheckForStateChanges(currentRoundState);
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
            UpdatePlayerProfile(); // Save scores and currency to PlayerProfile
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
        playerProfile.totalCurrency += gameState.currency; // Assuming currency is calculated somewhere

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
