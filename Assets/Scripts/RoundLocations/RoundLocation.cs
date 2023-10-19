using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundLocation : MonoBehaviour
{
    public RoundLocationData roundLocationData; // Reference to the RoundLocation ScriptableObject
    private bool playerInside = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Assuming the player has a "Player" tag
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

    private void Update()
    {
        if (playerInside && Input.GetKeyDown(KeyCode.E))
        {
            GameController gameController = FindObjectOfType<GameController>();
            gameController.StartNewRound(roundLocationData);
        }
    }

}

