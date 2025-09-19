using System.Collections;
using UnityEngine;

public class ItemController : MonoBehaviour
{
    public Item item;
    public float buoyancy;
    public Vector3 rotationSpeed;
    private Rigidbody rb;
    public OilLeakData oilLeakData;
    private bool hasHitGround = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        buoyancy = item.buoyancy;
        rotationSpeed = new Vector3(0, 30, 0);
    }

    void FixedUpdate()
    {
        if (transform.position.y < 0 && !hasHitGround)
        {
            rb.useGravity = false;
            float adjustedGravity = 9.81f * buoyancy;
            rb.AddForce(Vector3.down * adjustedGravity, ForceMode.Acceleration);

            // Check if the vertical velocity is close to zero
            if (Mathf.Abs(rb.velocity.y) > 0.01f)
            {
                // If not, continue rotating
                transform.Rotate(rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            rb.useGravity = true;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object is on the "Terrain" layer and if the item has not hit the ground yet
        if (collision.gameObject.layer == LayerMask.NameToLayer("Terrain") && !hasHitGround)
        {
            // Start the StopMovement coroutine with a duration of 2 seconds
            StartCoroutine(StopMovement(2f));

            // Set hasHitGround to true
            hasHitGround = true;
        }
    }
    IEnumerator StopMovement(float duration)
    {
        // Store the initial velocity and angular velocity
        Vector3 initialVelocity = rb.velocity;
        Vector3 initialAngularVelocity = rb.angularVelocity;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Calculate the fraction of the duration that has passed
            float fraction = elapsed / duration;

            // Interpolate the velocity and angular velocity to zero
            rb.velocity = Vector3.Lerp(initialVelocity, Vector3.zero, fraction);
            rb.angularVelocity = Vector3.Lerp(initialAngularVelocity, Vector3.zero, fraction);

            yield return null;
        }

        // Ensure the velocity and angular velocity are exactly zero at the end
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Turn on gravity
        rb.useGravity = true;

        // Make the object kinematic
        rb.isKinematic = false;

        // Set hasHitGround to true
        hasHitGround = true;
    }

    void OnParticleCollision(GameObject other)
    {
        if (other.layer == LayerMask.NameToLayer("OilSpill"))
        {
            // Update GameSession instead of ScriptableObject
            if (GameCore.Session != null)
            {
                GameCore.Session.RecordParticleBlocked();
                // Debug.Log($"[ItemController] Particle blocked! Total: {GameCore.Session.ParticlesBlocked}");  // Commented out - too spammy
            }

            // Remove ScriptableObject write - GameSession is the sole authority now
            // oilLeakData.particlesBlocked++; // REMOVED - use GameCore.Session

            // Award points for blocking particles (continuous scoring)
            GameController gameController = GameController.Instance;
            if (gameController != null && gameController.gameState != null)
            {
                int pointsPerParticle = 10; // Base points for each particle blocked
                gameController.gameState.score += pointsPerParticle;
            }

            // Notify DifficultyService through GameCore (not singleton)
            if (GameCore.Difficulty != null)
            {
                GameCore.Difficulty.OnParticleBlocked(1);
            }

            // Notify ItemDegradation about oil exposure
            ItemDegradation degradation = GetComponent<ItemDegradation>();
            if (degradation != null)
            {
                degradation.RegisterOilExposure();
            }
        }
    }
}






