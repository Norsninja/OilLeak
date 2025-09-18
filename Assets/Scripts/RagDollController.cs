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
    void Start()
    {
        // Fetch all rigid bodies of the ragdoll
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();

        if (spineRigidBody == null)
        {
            Debug.LogError("Spine Rigidbody not set in the inspector");
            return;
        }


        buoyancy = item.buoyancy;
        rotationSpeed = new Vector3(0, 30, 0);
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

    public void HandleParticleCollision(GameObject other)
    {
        if (other.layer == LayerMask.NameToLayer("OilSpill"))
        {
            // Update GameSession instead of ScriptableObject
            if (GameCore.Session != null)
            {
                GameCore.Session.RecordParticleBlocked();
                Debug.Log($"[RagdollController] Particle blocked! Total: {GameCore.Session.ParticlesBlocked}");
            }

            // Keep updating ScriptableObject for backward compatibility (temporary)
            oilLeakData.particlesBlocked++;
        }
    }
    // Function to apply throwing force to "Spine.002"
    public void Throw(Vector3 direction, float force)
    {
        spineRigidBody.AddForce(direction * force, ForceMode.Impulse);
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
