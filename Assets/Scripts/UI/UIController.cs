using UnityEngine;
using TMPro; // for TextMeshProUGUI

public class UIController : MonoBehaviour
{
    public GameObject roundOverCanvas;  // Reference to the RoundOver Canvas GameObject
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI currencyText;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI particlesBlockedText;
    public TextMeshProUGUI particlesEscapedText;
    public TextMeshProUGUI roundTotalScoreText;  // For round-over UI 
    public TextMeshProUGUI profileTotalScoreText;
    public TextMeshProUGUI highScoreText; 
    public TextMeshProUGUI currencyRewardedText; 

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
        scoreText.text = "Round Score: " + gameState.score.ToString();
        timerText.text = "Round Time: " + Mathf.RoundToInt(gameState.timer).ToString() + "/" + gameTimerData.roundDuration.ToString();
        // currencyText.text = "Currency: " + gameState.currency.ToString();
    }
    public void UpdatePlayerProfileUI()
    {
        profileTotalScoreText.text = "Total Score: " + playerProfile.totalScore;
        currencyText.text = "Scrilla: " + playerProfile.totalCurrency;
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
        // Update round-over UI components
        gradeText.text = "Grade: " + gameState.grade;  
        SetGradeTextColor(gameState.grade);  // Call the new function here

        particlesBlockedText.text = "Particles Blocked: " + oilLeakData.particlesBlocked;
        particlesEscapedText.text = "Particles Escaped: " + oilLeakData.particlesEscaped;
        roundTotalScoreText.text = "Total Score: " + gameState.score;
        highScoreText.text = "High Score: " + gameState.highScore;
        currencyRewardedText.text = "Currency Rewarded: " + gameState.currency;
        maximumPossibleScoreText.text = "Max Possible Score: " + gameController.scoringManager.scoreSummary.maximumPossibleScore;
        escapePenaltyText.text = "Escape Penalty: " + gameController.scoringManager.scoreSummary.escapePenalty;
        efficiencyScoreText.text = "Efficiency Score: " + gameController.scoringManager.scoreSummary.efficiencyScore;
        throwEfficiencyScoreText.text = "Throw Efficiency Score: " + gameController.scoringManager.scoreSummary.throwEfficiencyScore;
        adjustedGradeScoreText.text = "Adjusted Grade Score: " + gameController.scoringManager.scoreSummary.adjustedGradeScore;
        bonusText.text = "Bonus: " + gameController.scoringManager.scoreSummary.bonus;
    }
    public void HideRoundOverUI()
    {
        // Hide the "Game Over" UI here. This could be as simple as setting its GameObject to inactive.
        // For example:
        roundOverCanvas.SetActive(false);
    }
}

