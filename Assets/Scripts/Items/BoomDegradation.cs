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

        private BoomController boomController;
        private bool hasDetached = false;
        private Color originalColor = Color.white; // Store boom's original color
        private bool hasStoredOriginalColor = false;

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

                // Perform detachment
                if (shouldDetach)
                {
                    hasDetached = true;
                    boomController.DetachFromBoat();
                }
            }
        }

        /// <summary>
        /// Reset detachment flag when boom is reset
        /// </summary>
        protected override void OnEnable()
        {
            // Store original color before base reset
            if (!hasStoredOriginalColor && itemRenderer != null)
            {
                if (itemRenderer.sharedMaterial != null && itemRenderer.sharedMaterial.HasProperty(ColorProperty))
                {
                    originalColor = itemRenderer.sharedMaterial.GetColor(ColorProperty);
                    hasStoredOriginalColor = true;
                    Debug.Log($"[BoomDegradation] Stored original color: {originalColor}");
                }
            }

            // Call base (will set to white temporarily)
            base.OnEnable();

            // Restore original color after base reset
            if (hasStoredOriginalColor && itemRenderer != null && propertyBlock != null)
            {
                itemRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorProperty, originalColor);
                itemRenderer.SetPropertyBlock(propertyBlock);
                Debug.Log("[BoomDegradation] Restored original boom color");
            }

            hasDetached = false;
        }

        /// <summary>
        /// Override visual application to preserve boom's original color
        /// </summary>
        protected override void ApplyDegradationVisuals(Color targetColor)
        {
            if (itemRenderer == null || propertyBlock == null) return;

            Color finalColor;

            // Preserve original color for dry state
            if (currentState == DegradationState.Dry)
            {
                finalColor = originalColor;
            }
            else
            {
                // Blend degradation tint with original color
                // This preserves the yellow while adding oil darkness
                finalColor = originalColor * targetColor;
            }

            itemRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorProperty, finalColor);
            itemRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Get current degradation percentage for UI
        /// </summary>
        public float GetDegradationPercent()
        {
            // Use the base class GetPorosity() which returns 0-1 for degradation
            return GetPorosity();
        }

        /// <summary>
        /// Check if boom is still effective (not porous)
        /// </summary>
        public bool IsEffective()
        {
            return gameObject.layer != LAYER_POROUS_DEBRIS;
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