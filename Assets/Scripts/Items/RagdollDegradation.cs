using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Specialized degradation handler for ragdolls with multiple renderers across bones.
/// Inherits from ItemDegradation to reuse core degradation logic while handling multi-renderer visuals
/// and smart layer management to preserve ColliderTest functionality.
/// </summary>
public class RagdollDegradation : ItemDegradation
{
    // Multi-renderer support
    private Renderer[] allBoneRenderers;
    private MaterialPropertyBlock[] bonePropertyBlocks;
    private Color[] originalBoneColors;  // Cache original colors from materials

    // Layer management
    private Dictionary<GameObject, int> originalBoneLayers;

    // Prevent multiple initializations
    private bool isInitialized = false;

    protected override void Awake()
    {
        // Initialize our data structures FIRST
        originalBoneLayers = new Dictionary<GameObject, int>();

        // Cache original layers BEFORE base.Awake (which might trigger state changes)
        CacheOriginalLayers();

        // Now call base to do standard initialization
        base.Awake();
    }

    protected override void InitializeComponents()
    {
        // Call base to get controllers and itemData
        base.InitializeComponents();

        // Override renderer discovery for multi-renderer support
        DiscoverAllRenderers();

        isInitialized = true;
    }

    protected override void OnEnable()
    {
        // Refresh cached data BEFORE base.OnEnable (which calls ResetState)
        if (isInitialized)
        {
            RefreshCachedData();
        }

        // Call base (which will set colors to white temporarily)
        base.OnEnable();

        // CRITICAL: Restore original colors AFTER base.OnEnable
        // This fixes the white override issue from base.ResetState()
        if (isInitialized)
        {
            RestoreDefaultColors();
        }
    }

    protected override void OnDisable()
    {
        // Call base first
        base.OnDisable();

        // Any ragdoll-specific cleanup can go here
    }

    /// <summary>
    /// Discover all renderers across ragdoll bones and cache their original colors
    /// </summary>
    private void DiscoverAllRenderers()
    {
        allBoneRenderers = GetComponentsInChildren<Renderer>();
        bonePropertyBlocks = new MaterialPropertyBlock[allBoneRenderers.Length];
        originalBoneColors = new Color[allBoneRenderers.Length];

        // Initialize property blocks and cache original colors (no LINQ to avoid allocations)
        for (int i = 0; i < bonePropertyBlocks.Length; i++)
        {
            bonePropertyBlocks[i] = new MaterialPropertyBlock();

            // Cache the original color from the material
            if (allBoneRenderers[i] != null && allBoneRenderers[i].sharedMaterial != null)
            {
                // First check if renderer already has a property block with color
                allBoneRenderers[i].GetPropertyBlock(bonePropertyBlocks[i]);
                if (bonePropertyBlocks[i].HasProperty(ColorProperty))
                {
                    // Use existing property block color
                    originalBoneColors[i] = bonePropertyBlocks[i].GetColor(ColorProperty);
                }
                else if (allBoneRenderers[i].sharedMaterial.HasProperty(ColorProperty))
                {
                    // Fall back to material color
                    originalBoneColors[i] = allBoneRenderers[i].sharedMaterial.GetColor(ColorProperty);
                }
                else
                {
                    // Default to white if no color property exists
                    originalBoneColors[i] = Color.white;
                }
            }
            else
            {
                originalBoneColors[i] = Color.white;
            }
        }

        // Set base class fields for compatibility
        if (allBoneRenderers.Length > 0)
        {
            itemRenderer = allBoneRenderers[0];
            propertyBlock = bonePropertyBlocks[0];
        }

        Debug.Log($"[RagdollDegradation] Found {allBoneRenderers.Length} renderers across bones, cached original colors");
    }

    /// <summary>
    /// Cache original layer configuration for all bones
    /// </summary>
    private void CacheOriginalLayers()
    {
        originalBoneLayers.Clear();

        Transform[] allTransforms = GetComponentsInChildren<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            GameObject go = allTransforms[i].gameObject;
            originalBoneLayers[go] = go.layer;
        }

        Debug.Log($"[RagdollDegradation] Cached {originalBoneLayers.Count} bone layers");
    }

    /// <summary>
    /// Refresh cached data when re-enabled from pool
    /// </summary>
    private void RefreshCachedData()
    {
        // Guard against uninitialized state
        if (allBoneRenderers == null || bonePropertyBlocks == null || originalBoneColors == null)
        {
            DiscoverAllRenderers();
        }

        // Re-check renderers in case hierarchy changed
        Renderer[] currentRenderers = GetComponentsInChildren<Renderer>();

        // Only reallocate if count changed
        if (currentRenderers.Length != allBoneRenderers.Length)
        {
            Debug.Log($"[RagdollDegradation] Renderer count changed from {allBoneRenderers.Length} to {currentRenderers.Length}");
            DiscoverAllRenderers();
        }
        else
        {
            allBoneRenderers = currentRenderers;
        }

        // Always refresh layer cache
        CacheOriginalLayers();
    }

    /// <summary>
    /// Apply degradation visuals to ALL bone renderers using cached original colors
    /// </summary>
    protected override void ApplyDegradationVisuals(Color targetColor)
    {
        // Guard against null
        if (allBoneRenderers == null || bonePropertyBlocks == null || originalBoneColors == null) return;

        // Apply to all renderers (no LINQ, iterate by index to avoid GC)
        for (int i = 0; i < allBoneRenderers.Length; i++)
        {
            if (allBoneRenderers[i] == null) continue;

            Color finalColor;

            // For Dry state, use original color
            // For degraded states, blend with original color to preserve character while showing oil
            if (currentState == DegradationState.Dry)
            {
                finalColor = originalBoneColors[i];
            }
            else
            {
                // Multiply degradation tint with original color to preserve character palette
                // This keeps the original variations while adding oil saturation
                finalColor = originalBoneColors[i] * targetColor;
            }

            allBoneRenderers[i].GetPropertyBlock(bonePropertyBlocks[i]);
            bonePropertyBlocks[i].SetColor(ColorProperty, finalColor);
            allBoneRenderers[i].SetPropertyBlock(bonePropertyBlocks[i]);
        }
    }

    /// <summary>
    /// Smart layer management - only change root to preserve ColliderTest on bones
    /// </summary>
    protected override void OnLayerChange(int newLayer)
    {
        // Guard against null dictionary
        if (originalBoneLayers == null)
        {
            CacheOriginalLayers();
        }

        if (newLayer == LAYER_POROUS_DEBRIS)
        {
            // CRITICAL: Only change root layer to porous
            // This marks the ragdoll as porous for game logic
            // But keeps bones on Items layer so ColliderTest continues working
            gameObject.layer = newLayer;

            Debug.Log($"[RagdollDegradation] Root set to porous layer {newLayer}, bones remain on Items layer for ColliderTest");
        }
        else if (newLayer == LAYER_ITEMS_SOLID)
        {
            // Restore all original layers when resetting to solid
            foreach (var kvp in originalBoneLayers)
            {
                if (kvp.Key != null)  // Guard against destroyed objects
                {
                    kvp.Key.layer = kvp.Value;
                }
            }

            Debug.Log($"[RagdollDegradation] Restored all original layers");
        }
    }

    /// <summary>
    /// Restore original colors to all bone renderers
    /// </summary>
    private void RestoreDefaultColors()
    {
        if (allBoneRenderers == null || bonePropertyBlocks == null || originalBoneColors == null) return;

        for (int i = 0; i < allBoneRenderers.Length; i++)
        {
            if (allBoneRenderers[i] == null) continue;

            allBoneRenderers[i].GetPropertyBlock(bonePropertyBlocks[i]);
            bonePropertyBlocks[i].SetColor(ColorProperty, originalBoneColors[i]);
            allBoneRenderers[i].SetPropertyBlock(bonePropertyBlocks[i]);
        }
    }

    /// <summary>
    /// Override position storage if needed for ragdoll-specific behavior
    /// </summary>
    protected override void OnPositionSet()
    {
        // Call base to store position
        base.OnPositionSet();

        // Could add ragdoll-specific position logic here if needed
        // For example, storing center of mass position instead of root position
    }
}

