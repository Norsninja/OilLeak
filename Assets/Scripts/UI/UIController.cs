using UnityEngine;
using TMPro; // for TextMeshProUGUI

public class UIController : MonoBehaviour
{
    public GameObject roundOverCanvas;  // Reference to the RoundOver Canvas GameObject
    public TextMeshProUGUI roundOverTitleText;  // Title text for game over message
    public TextMeshProUGUI roundOverSubtitleText;  // Subtitle showing survival stats
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    [SerializeField, HideInInspector] private TextMeshProUGUI currencyText; // Hidden for serialization compatibility
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI particlesBlockedText;
    public TextMeshProUGUI particlesEscapedText;
    public TextMeshProUGUI roundTotalScoreText;  // For round-over UI
    public TextMeshProUGUI profileTotalScoreText;
    public TextMeshProUGUI highScoreText;
    [SerializeField, HideInInspector] private TextMeshProUGUI currencyRewardedText; // Hidden for serialization compatibility 

    public TextMeshProUGUI maximumPossibleScoreText;
    public TextMeshProUGUI escapePenaltyText;
    public TextMeshProUGUI efficiencyScoreText;
    public TextMeshProUGUI throwEfficiencyScoreText;
    public TextMeshProUGUI adjustedGradeScoreText;
    public TextMeshProUGUI bonusText;

    public ScoringManager scoringManager;
    public GameState gameState; // Reference to GameState ScriptableObject
    public GameTimerData gameTimerData; // Reference to GameTimerData ScriptableObject
    public OilLeakData oilLeakData;
    public GameController gameController;
    public PlayerProfile playerProfile;  // Add this line
    void Start()
    {
        gameController = FindObjectOfType<GameController>();
        scoringManager = FindObjectOfType<ScoringManager>();
        // ... any other initialization logic you might have
    }
    // Update UI elements based on game state
    public void UpdateUI()
    {
        // Check if we're in endless mode
        if (gameController != null && gameController.useEndlessMode)
        {
            // Format time as MM:SS for endless mode
            int totalSeconds = Mathf.FloorToInt(gameState.timer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"Time: {minutes:00}:{seconds:00}";

            // Calculate gallons delayed (main metric)
            int gallonsDelayed = oilLeakData.particlesBlocked * 100; // Each particle = 100 gallons
            scoreText.text = $"Gallons Delayed: {gallonsDelayed:N0}";

            // Show difficulty multiplier if we have DifficultyManager
            var difficultyManager = DifficultyManager.Instance;
            if (difficultyManager != null && particlesBlockedText != null)
            {
                float multiplier = difficultyManager.GetCurrentMultiplier();
                particlesBlockedText.text = $"Difficulty: {multiplier:F1}x";
            }

            // Show particles escaped as secondary stat
            if (particlesEscapedText != null)
            {
                particlesEscapedText.text = $"Oil Escaped: {oilLeakData.particlesEscaped}";
            }
        }
        else
        {
            // Original round-based display
            scoreText.text = "Round Score: " + gameState.score.ToString();
            timerText.text = "Round Time: " + Mathf.RoundToInt(gameState.timer).ToString() + "/" + gameTimerData.roundDuration.ToString();
        }
    }
    public void UpdatePlayerProfileUI()
    {
        profileTotalScoreText.text = "Total Score: " + playerProfile.totalScore;
        // currencyText.text = "Scrilla: " + playerProfile.totalCurrency; // Removed - futility simulator uses only score
    }

    public void SetGradeTextColor(char grade)
    {
        switch (grade)
        {
            case 'A':
                gradeText.color = Color.green;
                break;
            case 'B':
                gradeText.color = new Color(0.5f, 1f, 0); // Light green
                break;
            case 'C':
                gradeText.color = Color.yellow;
                break;
            case 'D':
                gradeText.color = new Color(1f, 0.5f, 0); // Orange
                break;
            case 'F':
                gradeText.color = Color.red;
                break;
            default:
                gradeText.color = Color.white; // Default to white if grade is not A, B, C, D, or F
                break;
        }
    }


    // Show the round-over UI
    public void ShowRoundOverUI()
    {
        roundOverCanvas.SetActive(true);

        // Calculate survival time and gallons for futility display
        int minutes = Mathf.FloorToInt(gameState.timer / 60f);
        int seconds = Mathf.FloorToInt(gameState.timer % 60f);
        int gallonsDelayed = oilLeakData.particlesBlocked * 100;
        int gallonsEscaped = oilLeakData.particlesEscaped * 100;

        // Set the futility message - the oil always wins
        if (roundOverTitleText != null)
        {
            roundOverTitleText.text = "The Oil Won";
        }

        // Show what they accomplished before the inevitable
        if (roundOverSubtitleText != null)
        {
            roundOverSubtitleText.text = $"You delayed {minutes:00}:{seconds:00} and blocked {gallonsDelayed:N0} gallons";
        }

        // Update round-over UI components
        gradeText.text = "Grade: " + gameState.grade;
        SetGradeTextColor(gameState.grade);

        // Main stats - what matters in the futility simulator
        if (roundTotalScoreText != null)
            roundTotalScoreText.text = $"Survived: {minutes:00}:{seconds:00}";

        // Show gallons instead of particles for impact
        particlesBlockedText.text = $"Gallons Delayed: {gallonsDelayed:N0}";
        particlesEscapedText.text = $"Gallons Escaped: {gallonsEscaped:N0}";

        highScoreText.text = "High Score: " + gameState.highScore;

        // Show detailed scoring if available
        if (gameController.scoringManager.scoreSummary != null)
        {
            maximumPossibleScoreText.text = "Max Possible Score: " + gameController.scoringManager.scoreSummary.maximumPossibleScore;
            escapePenaltyText.text = "Escape Penalty: " + gameController.scoringManager.scoreSummary.escapePenalty;
            efficiencyScoreText.text = "Efficiency Score: " + gameController.scoringManager.scoreSummary.efficiencyScore;
            throwEfficiencyScoreText.text = "Throw Efficiency Score: " + gameController.scoringManager.scoreSummary.throwEfficiencyScore;
            adjustedGradeScoreText.text = "Adjusted Grade Score: " + gameController.scoringManager.scoreSummary.adjustedGradeScore;
            bonusText.text = "Bonus: " + gameController.scoringManager.scoreSummary.bonus;
        }
    }
    public void HideRoundOverUI()
    {
        // Hide the "Game Over" UI here. This could be as simple as setting its GameObject to inactive.
        // For example:
        roundOverCanvas.SetActive(false);
    }
}

