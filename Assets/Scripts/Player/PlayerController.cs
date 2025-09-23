using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    [SerializeField] private float rotationSpeed = 720f; // Degrees per second (2 full rotations/sec = fast but smooth)

    [Header("Boost Settings")]
    [SerializeField] private float boostSpeed = 10f;       // 2x normal speed
    [SerializeField] private float boostDuration = 2f;     // How long boost lasts
    [SerializeField] private float boostCooldown = 3f;     // Cooldown between boosts

    [Header("References")]
    public InventoryController inventoryController;

    private Rigidbody boatRb;
    private bool canMove = false;
    private Vector3 startPosition;

    // Boost state
    private bool isBoosting = false;
    private float boostTimeRemaining = 0f;
    private float cooldownTimeRemaining = 0f;


    void Start()
    {
        boatRb = GetComponent<Rigidbody>();
        boatRb.interpolation = RigidbodyInterpolation.Interpolate;
        boatRb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        startPosition = transform.position;

        // Initialize boost with full tank
        boostTimeRemaining = boostDuration;
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

        // Use boost speed if boosting, normal speed otherwise
        float currentSpeed = isBoosting ? boostSpeed : speed;

        Vector3 movement = new Vector3(horizontalInput * currentSpeed * Time.fixedDeltaTime, 0, 0);
        boatRb.MovePosition(boatRb.position + movement);

        // Smoothly rotate the boat based on direction
        if (horizontalInput > 0)
        {
            // Facing right
            Quaternion targetRotation = Quaternion.Euler(0, 0, 0);
            boatRb.rotation = Quaternion.RotateTowards(boatRb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        else if (horizontalInput < 0)
        {
            // Facing left
            Quaternion targetRotation = Quaternion.Euler(0, 180, 0);
            boatRb.rotation = Quaternion.RotateTowards(boatRb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }


    void Update()
    {
        // Handle boost input and timing
        UpdateBoost();

        // Drop item from inventory
        if (Input.GetKeyDown(KeyCode.Space))
        {
            inventoryController.DropItem();
        }

        // Update GameState
        // gameState.score = CalculateScore();
        // ... other game state updates
    }

    private void UpdateBoost()
    {
        // Update cooldown
        if (cooldownTimeRemaining > 0)
        {
            cooldownTimeRemaining -= Time.deltaTime;
        }

        // Check for boost input (only while held)
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            // Can only boost if not on cooldown and has boost time remaining
            if (cooldownTimeRemaining <= 0 && boostTimeRemaining > 0)
            {
                isBoosting = true;
                boostTimeRemaining -= Time.deltaTime;

                // If boost depleted, start cooldown
                if (boostTimeRemaining <= 0)
                {
                    boostTimeRemaining = 0;
                    cooldownTimeRemaining = boostCooldown;
                    isBoosting = false;
                }
            }
            else
            {
                isBoosting = false;
            }
        }
        else
        {
            // Not holding shift - stop boosting
            isBoosting = false;

            // Regenerate boost when not in use and not on cooldown
            if (cooldownTimeRemaining <= 0 && boostTimeRemaining < boostDuration)
            {
                boostTimeRemaining = Mathf.Min(boostTimeRemaining + Time.deltaTime * 2f, boostDuration);
            }
        }
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

        // Reset boost state
        isBoosting = false;
        boostTimeRemaining = boostDuration;  // Reset to full boost
        cooldownTimeRemaining = 0f;
    }

    /// <summary>
    /// Get current boost status for UI/DevHUD
    /// </summary>
    public bool IsBoosting => isBoosting;

    /// <summary>
    /// Get boost progress (0-1) for UI
    /// </summary>
    public float GetBoostProgress()
    {
        if (isBoosting)
        {
            return boostTimeRemaining / boostDuration;
        }
        return 0f;
    }

    /// <summary>
    /// Get cooldown progress (0-1) for UI
    /// </summary>
    public float GetCooldownProgress()
    {
        if (cooldownTimeRemaining > 0)
        {
            return cooldownTimeRemaining / boostCooldown;
        }
        return 0f;
    }
}



