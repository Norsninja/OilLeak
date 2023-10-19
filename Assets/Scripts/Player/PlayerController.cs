using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed;
    public InventoryController inventoryController;
    public GameState gameState;
    private Rigidbody boatRb;
    public GameController gameController;


    void Start()
    {
        gameController = FindObjectOfType<GameController>();
        boatRb = GetComponent<Rigidbody>();
        boatRb.interpolation = RigidbodyInterpolation.Interpolate;
        boatRb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        // Restrict movement only if the round was started and is now over
        if (gameState.roundState == RoundState.Over)
        {
            return;
        }

        // Capture horizontal input for boat movement
        float horizontalInput = Input.GetAxis("Horizontal");
        Vector3 movement = new Vector3(horizontalInput * speed * Time.fixedDeltaTime, 0, 0);
        boatRb.MovePosition(boatRb.position + movement);

        // Rotate the boat based on direction
        if (horizontalInput > 0)
        {
            boatRb.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (horizontalInput < 0)
        {
            boatRb.rotation = Quaternion.Euler(0, 180, 0);
        }
    }


    void Update()
    {
        // Drop item from inventory
        if (Input.GetKeyDown(KeyCode.Space))
        {
            inventoryController.DropItem();
        }

        // Update GameState
        // gameState.score = CalculateScore();
        // ... other game state updates
    }

    int CalculateScore()
    {
        // Implement your scoring logic here
        return 0;
    }
}



