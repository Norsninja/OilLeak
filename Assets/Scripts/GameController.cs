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
        // Reset game state (or set it to a state that allows the player to continue)
        gameState.roundState = RoundState.NotStarted;
        uiController.UpdateUI();
        // Additional logic to reset or prepare the game for continuation could go here
    }



    // Calculate the maximum possible score, then uses that to calculate an adjusted grade score as a percentage. 
    // Finally, it uses this percentage to determine the grade
    // public void CalculateGrade()
    // {
    //     // Step 1: Calculate Maximum Possible Score
    //     int totalParticles = oilLeakData.particlesBlocked + oilLeakData.particlesEscaped;
    //     int maximumPossibleScore = totalParticles * 1000;
    //     Debug.Log($"Maximum Possible Score: {maximumPossibleScore}");

    //     // Step 2: Use the existing gameState.score as the base score
    //     int baseScore = gameState.score;
    //     Debug.Log($"Base Score: {baseScore}");

    //     // Step 3: Penalize for Escaped Particles
    //     float escapePenalty = (float)oilLeakData.particlesEscaped / (totalParticles); 
    //     Debug.Log($"Escape Penalty: {escapePenalty}");

    //     // Step 4: Calculate the Grade using penalized score and other efficiency metrics
    //     int totalItemInstances = inventoryController.TotalAvailableItems();  // Dynamic total items
    //     Debug.Log($"Total Available Items: {totalItemInstances}"); // Debugging line
    //     float efficiencyScore = 1 - ((float)inventoryController.itemsUsedThisRound / totalItemInstances);
        
    //     float throwEfficiencyScore = (inventoryController.itemsUsedThisRound == 0) ? 0 : Mathf.Min(1, (float)oilLeakData.particlesBlocked / inventoryController.itemsUsedThisRound);

    //     float efficiencyWeight1 = 0.2f;  // Weight for efficiencyScore
    //     float efficiencyWeight2 = 0.2f;  // Weight for throwEfficiencyScore
    //     float escapePenaltyWeight = 0.6f;  // Weight for escape penalty

    //     // Calculate the weighted sum of the penalized base score and efficiency scores
    //     float adjustedGradeScore = baseScore * (1 - escapePenalty * escapePenaltyWeight) * 
    //                                 (1 - efficiencyWeight1 - efficiencyWeight2) + 
    //                                 (efficiencyScore * baseScore * efficiencyWeight1) + 
    //                                 (throwEfficiencyScore * baseScore * efficiencyWeight2);

    //     float adjustedGradeScoreAsPercent = (float)adjustedGradeScore / maximumPossibleScore * 100;
    //     Debug.Log($"Adjusted Grade Score as Percent: {adjustedGradeScoreAsPercent}");

    //     // Step 5: Determine Bonus based on the Grade
    //     char grade = 'F';
    //     float bonus = 0f;
    //     if (oilLeakData.particlesEscaped == 0 && adjustedGradeScoreAsPercent >= 90) { grade = 'A'; bonus = 0.1f; }
    //     else if (adjustedGradeScoreAsPercent >= 80) { grade = 'B'; bonus = 0.05f; }
    //     else if (adjustedGradeScoreAsPercent >= 70) { grade = 'C'; }
    //     else if (adjustedGradeScoreAsPercent >= 60) { grade = 'D'; }
    //     Debug.Log($"Determined Grade: {grade}");

    //     // Step 6: Adjust the Base Score with the Grade
    //     int finalScore = (int)(baseScore * (1 + bonus));
    //     Debug.Log($"Final Score: {finalScore}");

    //     // Step 7: Calculate Currency Awarded based on the final score
    //     int currencyAwarded = finalScore / 1000;
    //     Debug.Log($"Currency Awarded: {currencyAwarded}");

    //     // Step 8: Update Player Profile
    //     gameState.score = finalScore;
    //     gameState.grade = grade;
    //     gameState.currency += currencyAwarded;
    //     if (finalScore > gameState.highScore)
    //     {
    //         gameState.highScore = finalScore;
    //     }

    //     // Step 9: Log the Results
    //     Debug.Log($"Final Grade: {grade}, Final Score: {finalScore}, Currency Awarded: {currencyAwarded}");
    //     Debug.Log($"Items Used This Round: {inventoryController.itemsUsedThisRound}");
    //     Debug.Log($"Total Items Used This Round (From Method): {inventoryController.TotalItemsUsedThisRound()}");
    //     Debug.Log($"Efficiency Score: {efficiencyScore}");
    //     Debug.Log($"Throw Efficiency Score: {throwEfficiencyScore}");
    // }


    //THis version does not have an escape penalty
    // public void CalculateGrade()
    // {
    //     // 1. Base Score Calculation
    //     float baseScore = (float)oilLeakData.particlesBlocked / (oilLeakData.particlesBlocked + oilLeakData.particlesEscaped) * 100;
    //     Debug.Log($"Base Score: {baseScore}");
    //     Debug.Log($"Particles Blocked: {oilLeakData.particlesBlocked}, Particles Escaped: {oilLeakData.particlesEscaped}");

    //     // 2. Grade (Adjusted Score) Calculation
    //     float efficiencyScore = 1 - ((float)inventoryController.itemsUsedThisRound / inventoryController.TotalItemsUsedThisRound());
    //     Debug.Log($"Original Efficiency Score: {efficiencyScore}");

    //     float throwEfficiencyScore = (inventoryController.itemsUsedThisRound == 0) ? 0 : Mathf.Min(1, (float)oilLeakData.particlesBlocked / inventoryController.itemsUsedThisRound);
    //     Debug.Log($"Throw Efficiency Score: {throwEfficiencyScore}");

    //     float efficiencyWeight1 = 0.2f;
    //     float efficiencyWeight2 = 0.2f;

    //     float adjustedScore = baseScore * (1 - efficiencyWeight1 - efficiencyWeight2) + 
    //                             (efficiencyScore * 100 * efficiencyWeight1) + 
    //                             (throwEfficiencyScore * 100 * efficiencyWeight2);
    //     adjustedScore = Mathf.Max(0, Mathf.Min(adjustedScore, 100));
    //     Debug.Log($"Adjusted Score (Grade): {adjustedScore}");

    //     // 3. Determine Bonus
    //     char grade = 'F';
    //     float bonus = 0f;
    //     if (adjustedScore >= 90) { grade = 'A'; bonus = 0.1f; }
    //     else if (adjustedScore >= 80) { grade = 'B'; bonus = 0.05f; }
    //     else if (adjustedScore >= 70) { grade = 'C'; }
    //     else if (adjustedScore >= 60) { grade = 'D'; }
    //     Debug.Log($"Determined Grade: {grade}");

    //     // 4. Final Score Calculation
    //     int finalScore = gameState.score; // Using real-time score as the base score

    //     // 5. Currency Award Calculation
    //     int currencyAwarded = (int)(finalScore / 100 * (1 + bonus)); // Real-time score influenced by grade bonus

    //     // 6. Update Player Profile
    //     gameState.score = finalScore;
    //     gameState.grade = grade;
    //     gameState.currency += currencyAwarded;
    //     if (finalScore > gameState.highScore)
    //     {
    //         gameState.highScore = finalScore;
    //     }

    //     // 7. Log the Results
    //     Debug.Log($"Final Grade: {grade}, Final Score: {finalScore}, Currency Awarded: {currencyAwarded}");
    // }

}
