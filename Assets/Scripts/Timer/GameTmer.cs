using UnityEngine;

public class GameTimer : MonoBehaviour
{
    public GameState gameState;  // Reference to GameState
    private GameController gameController; // Reference to GameController to access RoundLocationData

    void Start()
    {
        Debug.Log("GameState instance ID in GameTimer: " + gameState.GetInstanceID());
        gameController = FindObjectOfType<GameController>(); 
        gameState.timer = 0;  // Reset timer

        float roundDuration = gameController.GetCurrentRoundDuration();
        if (roundDuration >= 0)
        {
            gameState.roundDuration = roundDuration; // Set round duration only if a round is active
        }
        else
        {
            gameState.roundDuration = float.MaxValue; // Set to a large value to indicate no active timer
        }
    }


    void Update()
    {
        if (gameState.roundState == RoundState.Active)
        {
            UpdateTimer();
        }
    }


    // Update the game timer
    void UpdateTimer()
    {
        gameState.timer += Time.deltaTime;
    }
    public void ResetTimer()
    {
        gameState.timer = 0;
    }

}
