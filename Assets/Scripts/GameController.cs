using UnityEngine;

public class GameController : MonoBehaviour
{
    public GameState gameState; // Reference to GameState Scriptable Object
    public OilLeakData oilLeakData; // Reference to OilLeakData Scriptable Object
    public GameTimerData gameTimerData; // Reference to GameTimerData Scriptable Object
    public InventoryController inventoryController; 
    public UIController uiController;
    // Initialize the game state
    void Start()
    {
        Debug.Log("GameState instance ID in GameController: " + gameState.GetInstanceID());
        gameState.isRoundOver = false;
        gameState.score = 0;
        gameState.highScore = 0;
        gameState.currency = 0;
    }

    // Update and check round-over conditions
    void Update()
    {
        if (!gameState.isRoundOver)
        {
            UpdateRealTimeScore();  // Update the score in real-time
            CheckRoundOverConditions();
        }
        else
        {
            // UIController is your reference to the UIController script
            // This assumes you have a way to access UIController from GameController
            uiController.ShowRoundOverUI();
        }
    }

    // Check if the round is over based on the timer
    void CheckRoundOverConditions()
    {
        if (gameState.timer >= gameTimerData.roundDuration) // Use GameTimerData for round duration
        {
            gameState.isRoundOver = true;
            CalculateGrade();
            uiController.UpdateUI();
            // Implement any other end-of-round logic here
        }
    }
    void UpdateRealTimeScore()
    {
        gameState.score = oilLeakData.particlesBlocked * 1000;
        uiController.UpdateUI();
    }


    // Calculate the grade based on particles that reached the surface
    public void CalculateGrade()
    {
        // Calculate the base score based on particles blocked vs escaped
        float score = (float)oilLeakData.particlesBlocked / 
                    (oilLeakData.particlesBlocked + oilLeakData.particlesEscaped) * 100;
        Debug.Log($"Base Score: {score}");
        Debug.Log($"Particles Blocked: {oilLeakData.particlesBlocked}, Particles Escaped: {oilLeakData.particlesEscaped}");

        // Calculate the original efficiency score based on items used
        float totalItemInstances = 50 + 10;  // default items + other items
        float efficiencyScore = 1 - ((float)inventoryController.itemsUsedThisRound / totalItemInstances);
        Debug.Log($"Original Efficiency Score: {efficiencyScore}");
        Debug.Log($"Items Used: {inventoryController.itemsUsedThisRound}, Total Possible Items: {inventoryController.allPossibleItems.Count}");

        // Calculate the throw efficiency score based on items thrown vs particles blocked
        float throwEfficiencyScore = (inventoryController.itemsUsedThisRound == 0) ? 0 :
                                    Mathf.Min(1, (float)oilLeakData.particlesBlocked / inventoryController.itemsUsedThisRound);
        Debug.Log($"Throw Efficiency Score: {throwEfficiencyScore}");
        Debug.Log($"Items Thrown: {inventoryController.itemsUsedThisRound}, Particles Blocked: {oilLeakData.particlesBlocked}");

        // Integrate both efficiency scores into the overall score
        float efficiencyWeight1 = 0.2f;  // Weight for the original efficiency score
        float efficiencyWeight2 = 0.2f;  // Weight for the throw efficiency score

        // Integrate both efficiency scores into the overall score
        score = score * (1 - efficiencyWeight1 - efficiencyWeight2) + 
                (efficiencyScore * 100 * efficiencyWeight1) + 
                (throwEfficiencyScore * 100 * efficiencyWeight2);

        // Ensure the score doesn't exceed 100
        score = Mathf.Min(score, 100);
        Debug.Log($"Adjusted Score (with both efficiency scores): {score}");

        // Determine the grade and apply a bonus
        char grade = 'F';
        float bonus = 0f;
        if (score >= 90)
        {
            grade = 'A';
            bonus = 0.1f;
        }
        else if (score >= 80)
        {
            grade = 'B';
            bonus = 0.05f;
        }
        else if (score >= 70)
        {
            grade = 'C';
            bonus = 0;
        }
        else if (score >= 60)
        {
            grade = 'D';
            bonus = 0;
        }
        Debug.Log($"Determined Grade: {grade}");

        gameState.grade = grade;
        int pointsAwarded = Mathf.RoundToInt(score + (score * bonus));
        gameState.currency += pointsAwarded;

        if (gameState.score > gameState.highScore)
        {
            gameState.highScore = gameState.score;
        }

        // Optional: Log grade and score
        Debug.Log("Final Grade: " + grade + ", Final Score: " + gameState.score);
    }



}
