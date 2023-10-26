using UnityEngine;

public class RoundLocation : MonoBehaviour
{
    public RoundLocationData roundLocationData; // Reference to the RoundLocation ScriptableObject
    public GameState gameState; // Reference to the GameState ScriptableObject, set this in the Inspector
    public GameController gameController; // Reference to GameController, set this in the Inspector

    private bool playerInside = false;
    public GameObject sphere; // Reference to the child sphere GameObject, set this in the Inspector

    private void Update()
    {
        // Show or hide the sphere based on the game state
        if (gameState.roundState == RoundState.Active)
        {
            sphere.SetActive(false); // Hide sphere
        }
        else
        {
            sphere.SetActive(true); // Show sphere
        }

        // Check for player input to start a new round
        if (playerInside && Input.GetKeyDown(KeyCode.E))
        {
            gameController.StartNewRound(roundLocationData);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
        }
    }
}


