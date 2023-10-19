using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoringManager : MonoBehaviour
{
    public GameState gameState; // Reference to GameState Scriptable Object
    public OilLeakData oilLeakData; // Reference to OilLeakData Scriptable Object
    public InventoryController inventoryController;
    private float efficiencyScore;
    private float throwEfficiencyScore;
    public void CalculateGrade()
    {
        // Step 1: Calculate Maximum Possible Score
        int maximumPossibleScore = CalculateMaximumPossibleScore();
        
        // Step 2: Get Base Score
        int baseScore = GetBaseScore();
        
        // Step 3: Calculate Escape Penalty
        float escapePenalty = CalculateEscapePenalty();
        
        // Step 4: Calculate Efficiency Metrics
        efficiencyScore = CalculateEfficiencyScore();
        throwEfficiencyScore = CalculateThrowEfficiencyScore();
        
        // Step 5: Calculate Adjusted Grade Score
        float adjustedGradeScore = CalculateAdjustedGradeScore(baseScore, escapePenalty, efficiencyScore, throwEfficiencyScore);
        
        // Step 6: Determine Grade and Bonus
        (char grade, float bonus) = DetermineGradeAndBonus(adjustedGradeScore, maximumPossibleScore);
        
        // Step 7: Calculate Final Score and Currency
        int finalScore = CalculateFinalScore(baseScore, bonus);
        int currencyAwarded = CalculateCurrencyAwarded(finalScore);
        
        // Step 8: Update Player Profile
        UpdatePlayerProfile(finalScore, grade, currencyAwarded);
        
        // Step 9: Log Results
        LogResults(grade, finalScore, currencyAwarded);
    }
    private int CalculateMaximumPossibleScore()
    {
        int totalParticles = oilLeakData.particlesBlocked + oilLeakData.particlesEscaped;
        return totalParticles * 1000;
    }

    private int GetBaseScore()
    {
        return gameState.score;
    }

    private float CalculateEscapePenalty()
    {
        int totalParticles = oilLeakData.particlesBlocked + oilLeakData.particlesEscaped;
        return (float)oilLeakData.particlesEscaped / totalParticles;
    }

    private float CalculateEfficiencyScore()
    {
        int totalItemInstances = inventoryController.TotalAvailableItems();  // Dynamic total items
        return 1 - ((float)inventoryController.itemsUsedThisRound / totalItemInstances);
    }

    private float CalculateThrowEfficiencyScore()
    {
        return (inventoryController.itemsUsedThisRound == 0) ? 0 : Mathf.Min(1, (float)oilLeakData.particlesBlocked / inventoryController.itemsUsedThisRound);
    }

    private float CalculateAdjustedGradeScore(int baseScore, float escapePenalty, float efficiencyScore, float throwEfficiencyScore)
    {
        float efficiencyWeight1 = 0.2f;
        float efficiencyWeight2 = 0.2f;
        float escapePenaltyWeight = 0.6f;

        return baseScore * (1 - escapePenalty * escapePenaltyWeight) * 
                            (1 - efficiencyWeight1 - efficiencyWeight2) + 
                            (efficiencyScore * baseScore * efficiencyWeight1) + 
                            (throwEfficiencyScore * baseScore * efficiencyWeight2);
    }

    private (char, float) DetermineGradeAndBonus(float adjustedGradeScore, int maximumPossibleScore)
    {
        float adjustedGradeScoreAsPercent = (float)adjustedGradeScore / maximumPossibleScore * 100;
        char grade = 'F';
        float bonus = 0f;
        if (oilLeakData.particlesEscaped == 0 && adjustedGradeScoreAsPercent >= 90) 
        { 
            grade = 'A'; 
            bonus = 0.1f; 
        }
        else if (adjustedGradeScoreAsPercent >= 80) 
        { 
            grade = 'B'; 
            bonus = 0.05f; 
        }
        else if (adjustedGradeScoreAsPercent >= 70) 
        { 
            grade = 'C'; 
            bonus = 0f; 
        }
        else if (adjustedGradeScoreAsPercent >= 60) 
        { 
            grade = 'D'; 
            bonus = -0.2f; 
        }
        // ... and so on for other grades
        
        return (grade, bonus);
    }

    private int CalculateFinalScore(int baseScore, float bonus)
    {
        return (int)(baseScore * (1 + bonus));
    }

    private int CalculateCurrencyAwarded(int finalScore)
    {
        return finalScore / 1000;
    }

    private void UpdatePlayerProfile(int finalScore, char grade, int currencyAwarded)
    {
        gameState.score = finalScore;
        gameState.grade = grade;
        gameState.currency += currencyAwarded;
        if (finalScore > gameState.highScore)
        {
            gameState.highScore = finalScore;
        }
    }

    private void LogResults(char grade, int finalScore, int currencyAwarded)
    {
        Debug.Log($"Final Grade: {grade}, Final Score: {finalScore}, Currency Awarded: {currencyAwarded}");
        Debug.Log($"Items Used This Round: {inventoryController.itemsUsedThisRound}");
        Debug.Log($"Total Items Used This Round (From Method): {inventoryController.TotalItemsUsedThisRound()}");
        Debug.Log($"Efficiency Score: {efficiencyScore}");
        Debug.Log($"Throw Efficiency Score: {throwEfficiencyScore}");
    }

}
