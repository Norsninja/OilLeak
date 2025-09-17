using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public InventoryController inventoryController;
    private Rigidbody boatRb;
    private bool canMove = false;
    private Vector3 startPosition;


    void Start()
    {
        boatRb = GetComponent<Rigidbody>();
        boatRb.interpolation = RigidbodyInterpolation.Interpolate;
        boatRb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        startPosition = transform.position;
    }

    void FixedUpdate()
    {
        // Early out if movement is disabled
        if (!canMove)
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

    /// <summary>
    /// Enable or disable player movement
    /// </summary>
    public void EnableMovement(bool enable)
    {
        canMove = enable;
    }

    /// <summary>
    /// Check if movement is enabled
    /// </summary>
    public bool IsMovementEnabled => canMove;

    /// <summary>
    /// Reset boat to starting position
    /// </summary>
    public void ResetPosition()
    {
        if (boatRb != null)
        {
            boatRb.position = startPosition;
            boatRb.rotation = Quaternion.identity;
        }
    }
}



