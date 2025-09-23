using UnityEngine;

namespace Items
{
    /// <summary>
    /// Specialized degradation for boom items that detaches from boat when becoming porous.
    /// Extends ItemDegradation to hook into layer change events.
    /// </summary>
    public class BoomDegradation : ItemDegradation
    {
        [Header("Boom Settings")]
        [SerializeField] private bool detachOnPorous = true; // Detach when oil can pass through
        [SerializeField] private bool detachOnSludge = false; // Alternative: detach only when fully degraded

        private BoomController boomController;
        private bool hasDetached = false;

        protected override void Awake()
        {
            base.Awake();

            // Get boom controller component
            boomController = GetComponent<BoomController>();
            if (boomController == null)
            {
                Debug.LogError("[BoomDegradation] No BoomController found on boom!");
            }
        }

        /// <summary>
        /// Override layer change to trigger detachment
        /// </summary>
        protected override void OnLayerChange(int newLayer)
        {
            base.OnLayerChange(newLayer);

            // Check if we should detach based on the new layer
            if (!hasDetached && boomController != null)
            {
                bool shouldDetach = false;

                // Check detachment conditions
                if (detachOnPorous && newLayer == LAYER_POROUS_DEBRIS)
                {
                    shouldDetach = true;
                    Debug.Log("[BoomDegradation] Boom became porous - detaching");
                }
                else if (detachOnSludge && newLayer == LAYER_SLUDGE)
                {
                    shouldDetach = true;
                    Debug.Log("[BoomDegradation] Boom became sludge - detaching");
                }

                // Perform detachment
                if (shouldDetach)
                {
                    hasDetached = true;
                    boomController.DetachFromBoat();
                }
            }
        }

        /// <summary>
        /// Override state transition for additional detachment trigger
        /// </summary>
        protected override void TransitionToState(ItemState newState)
        {
            base.TransitionToState(newState);

            // Additional check for sludge state if using that trigger
            if (!hasDetached && detachOnSludge && newState == ItemState.Sludge)
            {
                if (boomController != null && boomController.IsAttached)
                {
                    hasDetached = true;
                    boomController.DetachFromBoat();
                    Debug.Log("[BoomDegradation] Boom reached sludge state - detaching");
                }
            }
        }

        /// <summary>
        /// Reset detachment flag when boom is reset
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            hasDetached = false;
        }

        /// <summary>
        /// Get current degradation percentage for UI
        /// </summary>
        public float GetDegradationPercent()
        {
            if (itemData == null) return 0f;

            // Calculate based on particle exposure
            float maxExposure = itemData.particlesToSludge;
            if (maxExposure <= 0) return 0f;

            return Mathf.Clamp01(oilExposure / maxExposure);
        }

        /// <summary>
        /// Check if boom is still effective (not porous)
        /// </summary>
        public bool IsEffective()
        {
            return gameObject.layer != LAYER_POROUS_DEBRIS &&
                   gameObject.layer != LAYER_SLUDGE;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Debug visualization
        /// </summary>
        void OnGUI()
        {
            if (Application.isEditor && boomController != null && boomController.IsAttached)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
                if (screenPos.z > 0)
                {
                    float percent = GetDegradationPercent() * 100f;
                    GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 20, 100, 20),
                        $"Boom: {percent:F0}%");
                }
            }
        }
#endif
    }
}