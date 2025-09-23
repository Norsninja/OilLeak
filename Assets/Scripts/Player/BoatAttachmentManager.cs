using UnityEngine;
using Core;

namespace Player
{
    /// <summary>
    /// Manages boom attachment to the boat. Handles joint creation, detachment, and cleanup.
    /// Only one boom can be attached at a time.
    /// </summary>
    public class BoatAttachmentManager : MonoBehaviour, IResettable
    {
        [Header("Configuration")]
        [SerializeField] private Transform boomAnchor; // Rear attachment point on boat
        [SerializeField] private Transform waterSurfaceRef; // Reference to water plane object
        [SerializeField] private float fallbackWaterlineY = 0f; // Fallback if no water surface ref

        [Header("Joint Settings")]
        [SerializeField] private float trailingDistance = 4f; // How far boom trails behind
        [SerializeField] private float springStrength = 50f;
        [SerializeField] private float springDamper = 8f;
        [SerializeField] private float ySpringStrength = 100f; // Stiffer vertical
        [SerializeField] private float ySpringDamper = 10f;

        // State
        private GameObject currentBoom;
        private ConfigurableJoint joint;
        private Rigidbody boatRb;
        private float waterlineY;

        void Awake()
        {
            boatRb = GetComponent<Rigidbody>();
            if (boatRb == null)
            {
                Debug.LogError("[BoatAttachmentManager] No Rigidbody found on boat!");
            }

            // Determine water surface Y
            if (waterSurfaceRef != null)
            {
                waterlineY = waterSurfaceRef.position.y;
                Debug.Log($"[BoatAttachmentManager] Using water surface Y: {waterlineY}");
            }
            else
            {
                waterlineY = fallbackWaterlineY;
                Debug.LogWarning($"[BoatAttachmentManager] No water surface ref, using fallback Y: {waterlineY}");
            }

            // Validate anchor point
            if (boomAnchor == null)
            {
                Debug.LogWarning("[BoatAttachmentManager] No boom anchor set, creating default");
                GameObject anchorObj = new GameObject("BoomAnchor");
                anchorObj.transform.parent = transform;
                anchorObj.transform.localPosition = new Vector3(0, 0, -3f); // Behind boat
                boomAnchor = anchorObj.transform;
            }

            // Register for cleanup
            ResetRegistry.Register(this);
        }

        void OnDestroy()
        {
            ResetRegistry.Unregister(this);
        }

        /// <summary>
        /// Check if a boom is currently attached
        /// </summary>
        public bool HasAttachedBoom()
        {
            return currentBoom != null && joint != null;
        }

        /// <summary>
        /// Get the waterline Y position for boom positioning
        /// </summary>
        public float GetWaterlineY()
        {
            return waterlineY;
        }

        /// <summary>
        /// Attach a boom GameObject to the boat
        /// </summary>
        public void AttachBoom(GameObject boom)
        {
            if (boom == null)
            {
                Debug.LogError("[BoatAttachmentManager] Cannot attach null boom!");
                return;
            }

            if (HasAttachedBoom())
            {
                Debug.LogWarning("[BoatAttachmentManager] Boom already attached!");
                return;
            }

            currentBoom = boom;

            // Position boom at anchor point and waterline
            boom.transform.position = new Vector3(
                boomAnchor.position.x,
                waterlineY,
                boomAnchor.position.z
            );

            // Create ConfigurableJoint on boom
            joint = boom.AddComponent<ConfigurableJoint>();
            joint.connectedBody = boatRb;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = transform.InverseTransformPoint(boomAnchor.position);

            // Configure motion constraints
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited; // Small vertical play
            joint.zMotion = ConfigurableJointMotion.Limited;

            // Lock rotations to keep boom horizontal
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            // Set linear limit (trailing distance)
            SoftJointLimit linearLimit = new SoftJointLimit();
            linearLimit.limit = trailingDistance;
            joint.linearLimit = linearLimit;

            // Configure spring drives for smooth trailing
            JointDrive xDrive = new JointDrive();
            xDrive.positionSpring = springStrength;
            xDrive.positionDamper = springDamper;
            xDrive.maximumForce = Mathf.Infinity;
            joint.xDrive = xDrive;

            JointDrive yDrive = new JointDrive();
            yDrive.positionSpring = ySpringStrength;
            yDrive.positionDamper = ySpringDamper;
            yDrive.maximumForce = Mathf.Infinity;
            joint.yDrive = yDrive;

            JointDrive zDrive = new JointDrive();
            zDrive.positionSpring = springStrength;
            zDrive.positionDamper = springDamper;
            zDrive.maximumForce = Mathf.Infinity;
            joint.zDrive = zDrive;

            // Enable projection to prevent joint explosions
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.2f;
            joint.projectionAngle = 10f;

            // Notify boom it's attached
            var boomController = boom.GetComponent<Items.BoomController>();
            if (boomController != null)
            {
                boomController.AttachTo(this, waterlineY);
            }

            Debug.Log("[BoatAttachmentManager] Boom attached successfully");
        }

        /// <summary>
        /// Detach the current boom
        /// </summary>
        public void DetachBoom()
        {
            if (!HasAttachedBoom())
            {
                return;
            }

            // Destroy joint
            if (joint != null)
            {
                Destroy(joint);
                joint = null;
            }

            // Clear reference
            currentBoom = null;

            Debug.Log("[BoatAttachmentManager] Boom detached");
        }

        /// <summary>
        /// Force detach and cleanup (for restart/cleanup)
        /// </summary>
        public void Reset()
        {
            if (HasAttachedBoom())
            {
                // Notify boom it's being forcibly detached
                var boomController = currentBoom.GetComponent<Items.BoomController>();
                if (boomController != null)
                {
                    boomController.ForceDetach();
                }

                DetachBoom();
            }
        }

        /// <summary>
        /// IResettable implementation
        /// </summary>
        public bool IsClean => !HasAttachedBoom();

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // Draw boom anchor point
            if (boomAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(boomAnchor.position, 0.5f);
                Gizmos.DrawLine(transform.position, boomAnchor.position);
            }

            // Draw waterline
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Vector3 waterStart = transform.position + Vector3.left * 5f;
                waterStart.y = waterlineY;
                Vector3 waterEnd = transform.position + Vector3.right * 5f;
                waterEnd.y = waterlineY;
                Gizmos.DrawLine(waterStart, waterEnd);
            }
        }
#endif
    }
}