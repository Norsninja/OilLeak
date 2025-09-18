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
    [SerializeField, HideInInspector] public TextMeshProUGUI gradeText; // Hidden - no grades in futility mode
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

    // New futility mode UI elements
    public TextMeshProUGUI futilityMessageText;  // "You are Sisyphus..."
    public TextMeshProUGUI restartInstructionText;  // "Press R to Try Again"
    public TextMeshProUGUI peakDifficultyText;  // Shows highest difficulty reached

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
        // Read from GameSession instead of PlayerProfile ScriptableObject
        if (GameCore.Session != null)
        {
            profileTotalScoreText.text = "Total Score: " + GameCore.Session.CombinedScore;
        }
        else
        {
            profileTotalScoreText.text = "Total Score: 0";
        }
        // currencyText.text = "Scrilla: " + playerProfile.totalCurrency; // Removed - futility simulator uses only score
    }

    /// <summary>
    /// New unified update method that takes SessionStats directly
    /// Replaces dependency on ScriptableObjects
    /// </summary>
    public void UpdateGameUI(SessionStats stats)
    {
        // Format time as MM:SS for endless mode
        int totalSeconds = Mathf.FloorToInt(stats.TimeElapsed);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"Time: {minutes:00}:{seconds:00}";

        // Show gallons delayed as main score
        scoreText.text = $"Gallons Delayed: {stats.GallonsDelayed:N0}";

        // Show current block value and multiplier
        if (particlesBlockedText != null)
        {
            particlesBlockedText.text = $"Next Block: {stats.CurrentBlockValue} pts ({stats.ScoreMultiplier}x)";
        }

        // Show particles escaped as secondary stat
        if (particlesEscapedText != null)
        {
            particlesEscapedText.text = $"Oil Escaped: {stats.GallonsEscaped:N0} gallons";
        }
    }

    /// <summary>
    /// Update player profile with SessionStats
    /// </summary>
    public void UpdatePlayerProfile(SessionStats stats)
    {
        profileTotalScoreText.text = "Total Score: " + stats.Score;
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
    // New method that uses SessionStats instead of ScriptableObjects
    public void ShowRoundOverUI(SessionStats stats, float peakDifficulty)
    {
        roundOverCanvas.SetActive(true);

        // Calculate display values from SessionStats
        int minutes = Mathf.FloorToInt(stats.TimeElapsed / 60f);
        int seconds = Mathf.FloorToInt(stats.TimeElapsed % 60f);

        // Set the futility message - the oil always wins
        if (roundOverTitleText != null)
        {
            roundOverTitleText.text = "The Oil Won";
        }

        // Show what they accomplished before the inevitable
        if (roundOverSubtitleText != null)
        {
            roundOverSubtitleText.text = $"You delayed {stats.GallonsDelayed:N0} gallons for {minutes:00}:{seconds:00}";
        }

        // Hide the grade system - no grades in futility mode
        if (gradeText != null && gradeText.gameObject != null)
        {
            gradeText.gameObject.SetActive(false);
        }

        // Main stats - what matters in the futility simulator
        if (roundTotalScoreText != null)
            roundTotalScoreText.text = $"Survived: {minutes:00}:{seconds:00}";

        // Show gallons instead of particles for impact
        if (particlesBlockedText != null)
            particlesBlockedText.text = $"Gallons Delayed: {stats.GallonsDelayed:N0}";

        if (particlesEscapedText != null)
            particlesEscapedText.text = $"Gallons Escaped: {stats.GallonsEscaped:N0}";

        // Show peak difficulty reached
        if (peakDifficultyText != null)
            peakDifficultyText.text = $"Peak Difficulty: {peakDifficulty:F1}x";

        // Show high score if it's a new record
        if (highScoreText != null)
        {
            if (stats.IsNewRecord)
                highScoreText.text = $"NEW HIGH SCORE: {stats.Score}";
            else
                highScoreText.text = $"High Score: {stats.PersonalBest}";
        }

        // Show futility message based on score
        if (futilityMessageText != null)
        {
            futilityMessageText.text = GetFutilityMessage(stats.Score);
        }

        // Show restart instruction
        if (restartInstructionText != null)
        {
            restartInstructionText.text = "Press R to Try Again";
        }

        // Hide old scoring UI elements
        HideOldScoringElements();
    }

    // Helper method to generate futility messages
    private string GetFutilityMessage(int score)
    {
        if (score < 500) return "You barely tried. The ocean weeps.";
        if (score < 1000) return "A valiant effort. Still futile.";
        if (score < 2000) return "You fought the tide. The tide won.";
        if (score < 5000) return "Impressive! But oil companies don't care.";
        return "You are Sisyphus. The oil is your boulder.";
    }

    // Helper to hide old UI elements that don't fit futility theme
    private void HideOldScoringElements()
    {
        // Hide detailed scoring breakdown - not relevant for futility
        if (maximumPossibleScoreText != null && maximumPossibleScoreText.gameObject != null)
            maximumPossibleScoreText.gameObject.SetActive(false);

        if (escapePenaltyText != null && escapePenaltyText.gameObject != null)
            escapePenaltyText.gameObject.SetActive(false);

        if (efficiencyScoreText != null && efficiencyScoreText.gameObject != null)
            efficiencyScoreText.gameObject.SetActive(false);

        if (throwEfficiencyScoreText != null && throwEfficiencyScoreText.gameObject != null)
            throwEfficiencyScoreText.gameObject.SetActive(false);

        if (adjustedGradeScoreText != null && adjustedGradeScoreText.gameObject != null)
            adjustedGradeScoreText.gameObject.SetActive(false);

        if (bonusText != null && bonusText.gameObject != null)
            bonusText.gameObject.SetActive(false);
    }

    public void HideRoundOverUI()
    {
        // Hide the "Game Over" UI here. This could be as simple as setting its GameObject to inactive.
        // For example:
        roundOverCanvas.SetActive(false);
    }
}

