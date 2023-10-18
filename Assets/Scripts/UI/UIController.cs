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
    public TextMeshProUGUI totalScoreText; 
    public TextMeshProUGUI highScoreText; 
    public TextMeshProUGUI currencyRewardedText; 



    public GameState gameState; // Reference to GameState ScriptableObject
    public GameTimerData gameTimerData; // Reference to GameTimerData ScriptableObject
    public OilLeakData oilLeakData;
 
    // Update UI elements based on game state
    public void UpdateUI()
    {
        scoreText.text = "Score: " + gameState.score.ToString();
        timerText.text = "Time: " + Mathf.RoundToInt(gameState.timer).ToString() + "/" + gameTimerData.roundDuration.ToString();
        currencyText.text = "Currency: " + gameState.currency.ToString();
    }


    // Show the round-over UI
    public void ShowRoundOverUI()
    {
        roundOverCanvas.SetActive(true);
        // Update round-over UI components
        gradeText.text = "Grade: " + gameState.grade;  // assuming gameState.grade exists
        particlesBlockedText.text = "Particles Blocked: " + oilLeakData.particlesBlocked;
        particlesEscapedText.text = "Particles Escaped: " + oilLeakData.particlesEscaped;
        totalScoreText.text = "Total Score: " + gameState.score;
        highScoreText.text = "High Score: " + gameState.highScore;
        currencyRewardedText.text = "Currency Rewarded: " + gameState.currency;

        // Implement the logic to show the round-over UI
    }
}

