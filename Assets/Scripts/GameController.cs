using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    public GameState gameState; // Reference to GameState Scriptable Object
    public PlayerProfile playerProfile; // Reference to PlayerProfile Scriptable Object
    public OilLeakData oilLeakData; // Reference to OilLeakData Scriptable Object
    public InventoryController inventoryController; 
    public UIController uiController;
    private RoundState previousRoundState;
    private RoundLocationData currentRoundLocationData; // The current round's data
    public ScoringManager scoringManager;
    public bool roundStarted = false;

    // Initialize the game state
    void Start()
    {
        Debug.Log("GameState instance ID in GameController: " + gameState.GetInstanceID());

        // Set the initial round state
        gameState.roundState = RoundState.NotStarted;

        gameState.score = 0;
        gameState.highScore = 0;
        gameState.currency = 0;
        uiController.UpdatePlayerProfileUI();
    }
    // Update and check round-over conditions
    void Update()
    {
        // Store the current round state at the beginning of the frame
        RoundState currentRoundState = gameState.roundState;

        switch (currentRoundState)
        {
            case RoundState.Active:
                UpdateRealTimeScore(); // Update the score in real-time
                CheckRoundOverConditions();
                break;
            
            case RoundState.Over:
                uiController.ShowRoundOverUI();
                gameState.roundState = RoundState.NotStarted; // Reset the round state to NotStarted after showing UI
                break;
        }
        // Check for state changes
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


    public void SetCurrentRoundLocation(RoundLocationData roundLocationData)
    {
        currentRoundLocationData = roundLocationData;
        oilLeakData = roundLocationData.oilLeakData; // Update oilLeakData with the current round's data

        // Reset round-specific data
        gameState.roundState = RoundState.Over;
        gameState.score = 0;
        gameState.timer = 0;
        
        OilController oilController = FindObjectOfType<OilController>();
        oilController.SetOilLeakData(oilLeakData); // Set the oil data in the OilController
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

    public void StartNewRound(RoundLocationData roundLocationData)
    {
        // Set the current round's data
        currentRoundLocationData = roundLocationData;
        oilLeakData = roundLocationData.oilLeakData;

        // Reset game state parameters
        gameState.roundState = RoundState.Active; // Set the round state to Active
        gameState.score = 0;
        gameState.timer = 0;
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

        GameTimer gameTimer = FindObjectOfType<GameTimer>();
        if (gameTimer)
        {
            gameTimer.ResetTimer();
        }
    }
    public void ContinueGame()
    {
        // Hide the "Game Over" UI
        uiController.HideRoundOverUI();
        gameState.score = 0;
        gameState.timer = 0;
        scoringManager.scoreSummary = null;
        // Reset game state (or set it to a state that allows the player to continue)
        gameState.roundState = RoundState.NotStarted;
        uiController.UpdateUI();
        // Additional logic to reset or prepare the game for continuation could go here
    }

}
