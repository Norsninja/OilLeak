using UnityEngine;

public class GameTimer : MonoBehaviour
{
    public GameState gameState;  // Reference to GameState
    public GameTimerData gameTimerData;  // Reference to GameTimerData

    void Start()
    {
        Debug.Log("GameState instance ID in GameTimer: " + gameState.GetInstanceID());

        gameState.timer = 0;  // Reset timer
        gameState.roundDuration = gameTimerData.roundDuration;  // Set round duration
    }

    void Update()
    {
        if (!gameState.isRoundOver)
        {
            UpdateTimer();
        }
    }

    // Update the game timer
    void UpdateTimer()
    {
        gameState.timer += Time.deltaTime;
    }
}
