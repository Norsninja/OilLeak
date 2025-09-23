using UnityEngine;
using Player;

namespace Items
{
    /// <summary>
    /// Controls boom behavior when attached to boat and after detachment.
    /// Maintains waterline position while attached, handles sinking when detached.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BoomController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float buoyancyStrength = 50f; // Optional buoyancy force
        [SerializeField] private bool useYConstraint = true; // Use constraint vs buoyancy
        [SerializeField] private float sinkDelay = 5f; // Time before returning to pool after detach

        // State
        private Rigidbody rb;
        private BoatAttachmentManager attachedTo;
        private bool isDetached = false;
        private float waterlineY = 0f;
        private Vector3 initialConstraints;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("[BoomController] No Rigidbody found on boom!");
                rb = gameObject.AddComponent<Rigidbody>();
            }

            // Store initial constraints for restoration
            initialConstraints = new Vector3(
                (rb.constraints & RigidbodyConstraints.FreezePositionX) != 0 ? 1 : 0,
                (rb.constraints & RigidbodyConstraints.FreezePositionY) != 0 ? 1 : 0,
                (rb.constraints & RigidbodyConstraints.FreezePositionZ) != 0 ? 1 : 0
            );
        }

        void FixedUpdate()
        {
            // Apply buoyancy if not using Y constraint and not detached
            if (!isDetached && !useYConstraint && waterlineY > -100f)
            {
                ApplyBuoyancy();
            }
        }

        /// <summary>
        /// Attach this boom to a boat
        /// </summary>
        public void AttachTo(BoatAttachmentManager manager, float waterY)
        {
            if (manager == null)
            {
                Debug.LogError("[BoomController] Cannot attach to null manager!");
                return;
            }

            attachedTo = manager;
            waterlineY = waterY;
            isDetached = false;

            // Cancel any pending return to pool
            CancelInvoke("ReturnToPool");

            // Configure physics for attached state
            rb.useGravity = false; // No gravity while attached

            if (useYConstraint)
            {
                // Constrain to waterline using freeze constraint
                rb.constraints = RigidbodyConstraints.FreezePositionY |
                                RigidbodyConstraints.FreezeRotationX |
                                RigidbodyConstraints.FreezeRotationZ;

                // Position at waterline
                Vector3 pos = transform.position;
                pos.y = waterlineY;
                transform.position = pos;
            }
            else
            {
                // Use buoyancy instead of constraint for more natural movement
                rb.constraints = RigidbodyConstraints.FreezeRotationX |
                                RigidbodyConstraints.FreezeRotationZ;
            }

            Debug.Log($"[BoomController] Attached to boat at waterline Y: {waterlineY}");
        }

        /// <summary>
        /// Detach from boat and begin sinking
        /// </summary>
        public void DetachFromBoat()
        {
            if (isDetached) return;

            Debug.Log("[BoomController] Detaching from boat");

            // Mark as detached
            isDetached = true;

            // Notify manager to destroy joint
            if (attachedTo != null)
            {
                attachedTo.DetachBoom();
                attachedTo = null;
            }

            // Enable sinking physics
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None; // Remove all constraints

            // Add some downward velocity to start sinking
            rb.velocity = new Vector3(rb.velocity.x, -1f, rb.velocity.z);

            // Schedule return to pool
            Invoke("ReturnToPool", sinkDelay);
        }

        /// <summary>
        /// Force detach during cleanup/restart
        /// </summary>
        public void ForceDetach()
        {
            if (isDetached) return;

            Debug.Log("[BoomController] Force detaching");

            isDetached = true;
            attachedTo = null;

            // Immediately return to pool
            CancelInvoke("ReturnToPool");
            ReturnToPool();
        }

        /// <summary>
        /// Apply buoyancy force to maintain waterline position
        /// </summary>
        private void ApplyBuoyancy()
        {
            float depth = waterlineY - transform.position.y;

            // Apply upward force proportional to depth below waterline
            if (depth > 0)
            {
                Vector3 buoyancyForce = Vector3.up * depth * buoyancyStrength;
                rb.AddForce(buoyancyForce, ForceMode.Force);

                // Add damping to prevent oscillation
                rb.velocity = new Vector3(
                    rb.velocity.x,
                    rb.velocity.y * 0.95f, // Damping
                    rb.velocity.z
                );
            }
        }

        /// <summary>
        /// Return boom to pool for reuse
        /// </summary>
        private void ReturnToPool()
        {
            Debug.Log("[BoomController] Returning to pool");

            // Reset state
            isDetached = false;
            attachedTo = null;
            waterlineY = 0f;

            // Reset physics
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;

            // Return to ItemPooler
            var pooler = ItemPooler.Instance;
            if (pooler != null)
            {
                pooler.ReturnItem(gameObject);
            }
            else
            {
                // Fallback: just deactivate
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Reset boom when returned to pool
        /// </summary>
        void OnDisable()
        {
            // Ensure clean state when disabled
            CancelInvoke("ReturnToPool");
            isDetached = false;
            attachedTo = null;

            // Reset physics
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Check if boom is currently attached
        /// </summary>
        public bool IsAttached => !isDetached && attachedTo != null;

        /// <summary>
        /// Get current attachment manager
        /// </summary>
        public BoatAttachmentManager GetAttachment => attachedTo;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (Application.isPlaying && !isDetached)
            {
                // Draw waterline indicator
                Gizmos.color = Color.cyan;
                Vector3 waterLeft = transform.position + Vector3.left * 2f;
                waterLeft.y = waterlineY;
                Vector3 waterRight = transform.position + Vector3.right * 2f;
                waterRight.y = waterlineY;
                Gizmos.DrawLine(waterLeft, waterRight);

                // Draw attachment status
                if (attachedTo != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, attachedTo.transform.position);
                }
            }
        }
#endif
    }
}