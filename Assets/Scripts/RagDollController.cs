using System.Collections;
using UnityEngine;

public class RagdollController : MonoBehaviour
{
    public Item item; // Reference to the item class
    public float buoyancy;
    public Vector3 rotationSpeed;
    private Rigidbody[] ragdollRigidbodies;
    public OilLeakData oilLeakData; // Reference to oil leak data
    public Rigidbody spineRigidBody; // Reference to the specific rigid body for "Spine.002"
    private bool hasHitGround = false;

    // Bind pose storage for proper pooling reset
    private System.Collections.Generic.Dictionary<Transform, Vector3> bindPoseLocalPositions;
    private System.Collections.Generic.Dictionary<Transform, Quaternion> bindPoseLocalRotations;
    private bool bindPoseStored = false;
    void Start()
    {
        // Fetch all rigid bodies of the ragdoll
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();

        if (spineRigidBody == null)
        {
            Debug.LogError("Spine Rigidbody not set in the inspector");
            return;
        }

        // Store bind pose for all child transforms
        StoreBindPose();

        buoyancy = item.buoyancy;
        rotationSpeed = new Vector3(0, 30, 0);
    }

    private void StoreBindPose()
    {
        if (bindPoseStored) return;

        bindPoseLocalPositions = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        bindPoseLocalRotations = new System.Collections.Generic.Dictionary<Transform, Quaternion>();

        Transform[] allBones = GetComponentsInChildren<Transform>();
        foreach (Transform bone in allBones)
        {
            // Skip the root transform itself
            if (bone == transform) continue;

            bindPoseLocalPositions[bone] = bone.localPosition;
            bindPoseLocalRotations[bone] = bone.localRotation;
        }

        bindPoseStored = true;
        Debug.Log($"[RagdollController] Stored bind pose for {allBones.Length - 1} bones");
    }

    void OnEnable()
    {
        hasHitGround = false;

        // Ensure rigidbodies are cached
        if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0)
        {
            ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        }

        // Ensure bind pose is stored (for runtime-spawned ragdolls)
        if (!bindPoseStored)
        {
            StoreBindPose();
        }

        // Step 1: Zero velocities BEFORE setting kinematic to avoid warnings
        foreach (Rigidbody rbPart in ragdollRigidbodies)
        {
            if (rbPart != null)
            {
                // Only zero velocities if not already kinematic
                if (!rbPart.isKinematic)
                {
                    rbPart.velocity = Vector3.zero;
                    rbPart.angularVelocity = Vector3.zero;
                }
                rbPart.isKinematic = true;
            }
        }

        // Step 2: Restore all bones to bind pose
        if (bindPoseLocalPositions != null)
        {
            foreach (var kvp in bindPoseLocalPositions)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localPosition = kvp.Value;
                }
            }
        }

        if (bindPoseLocalRotations != null)
        {
            foreach (var kvp in bindPoseLocalRotations)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localRotation = kvp.Value;
                }
            }
        }

        // Step 3: Sync transforms with physics engine
        Physics.SyncTransforms();

        // Step 4: Re-enable physics simulation
        foreach (Rigidbody rbPart in ragdollRigidbodies)
        {
            if (rbPart != null)
            {
                rbPart.isKinematic = false;
                rbPart.WakeUp();
            }
        }

        Debug.Log($"[RagdollController] Reset complete - restored {bindPoseLocalPositions?.Count ?? 0} bones to bind pose");
    }

    void FixedUpdate()
    {
        foreach (Rigidbody rbPart in ragdollRigidbodies)
        {
            if (rbPart == null)
            {
                Debug.LogWarning("Rigidbody part is null");
                continue;
            }

            // Clamp velocities to prevent glitching
            if (rbPart.velocity.magnitude > 50f) // Max 50 units/second
            {
                rbPart.velocity = rbPart.velocity.normalized * 50f;
            }
            if (rbPart.angularVelocity.magnitude > 30f) // Max 30 rad/second
            {
                rbPart.angularVelocity = rbPart.angularVelocity.normalized * 30f;
            }

            if (rbPart.transform.position.y < 0 && !hasHitGround)
            {
                rbPart.useGravity = false;
                float adjustedGravity = 9.81f * buoyancy;
                rbPart.AddForce(Vector3.down * adjustedGravity, ForceMode.Acceleration);
            }
            else
            {
                rbPart.useGravity = true;
            }
        }
    }

    // OnParticleCollision handles particles hitting the ragdoll root
    // ColliderTest components on bones are optional for additional collision detection
    void OnParticleCollision(GameObject other)
    {
        if (other.layer == LayerMask.NameToLayer("OilSpill"))
        {
            HandleParticleCollision(other);
        }
    }

    public void HandleParticleCollision(GameObject other)
    {
        if (other.layer == LayerMask.NameToLayer("OilSpill"))
        {
            // Update GameSession instead of ScriptableObject
            if (GameCore.Session != null)
            {
                GameCore.Session.RecordParticleBlocked();
                // Debug.Log($"[RagdollController] Particle blocked! Total: {GameCore.Session.ParticlesBlocked}"); // Commented - too spammy
            }

            // Keep updating ScriptableObject for backward compatibility (temporary)
            oilLeakData.particlesBlocked++;

            // Award points for blocking particles (matching ItemController)
            GameController gameController = GameController.Instance;
            if (gameController != null && gameController.gameState != null)
            {
                int pointsPerParticle = 10; // Base points for each particle blocked
                gameController.gameState.score += pointsPerParticle;
            }

            // Notify DifficultyService through GameCore (matching ItemController)
            if (GameCore.Difficulty != null)
            {
                GameCore.Difficulty.OnParticleBlocked(1);
            }

            // CRITICAL: Notify ItemDegradation about oil exposure for ragdoll degradation
            ItemDegradation degradation = GetComponent<ItemDegradation>();
            if (degradation != null)
            {
                degradation.RegisterOilExposure();
            }
        }
    }
    // Function to apply throwing force to "Spine.002"
    public void Throw(Vector3 direction, float force)
    {
        spineRigidBody.AddForce(direction * force, ForceMode.Impulse);
    }

    /// <summary>
    /// Clean up when returning to pool
    /// </summary>
    void OnDisable()
    {
        // Zero velocities first while non-kinematic
        foreach (Rigidbody rbPart in ragdollRigidbodies)
        {
            if (rbPart != null && !rbPart.isKinematic)
            {
                rbPart.velocity = Vector3.zero;
                rbPart.angularVelocity = Vector3.zero;
            }
        }

        // Then set to kinematic for pool storage
        foreach (Rigidbody rbPart in ragdollRigidbodies)
        {
            if (rbPart != null)
            {
                rbPart.isKinematic = true;
            }
        }
    }
    void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object is on the "Terrain" layer and if the ragdoll has not hit the ground yet
        if (collision.gameObject.layer == LayerMask.NameToLayer("Terrain") && !hasHitGround)
        {
            hasHitGround = true;
        }
    }


}
