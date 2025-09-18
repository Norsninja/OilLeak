using UnityEngine;

/// <summary>
/// Handles exposure-based item degradation - items become porous when touched by oil
/// Items persist as debris but oil can seep through porous items
/// </summary>
public class ItemDegradation : MonoBehaviour
{
    // Degradation states
    public enum DegradationState
    {
        Dry,          // Untouched by oil - solid
        Saturating,   // Starting to absorb oil - becoming porous
        Saturated,    // Fully saturated - oil passes through
        Sludge        // Final state - visual only
    }

    [Header("Current State")]
    [SerializeField] private DegradationState currentState = DegradationState.Dry;
    [SerializeField] private float exposureSeconds = 0f;
    [SerializeField] private int particlesBlockedCount = 0;

    [Header("Configuration")]
    [SerializeField] private Item itemData; // Reference to Item ScriptableObject

    // Components
    private Renderer itemRenderer;
    private MaterialPropertyBlock propertyBlock;
    private ItemController itemController;
    private RagdollController ragdollController;
    private ItemPooler itemPooler;

    // State tracking
    private float lastOilContactTime = 0f;
    private bool hasChangedLayer = false;
    private Vector3 originalPosition;

    // Layer references - using names to avoid index drift
    private int LAYER_ITEMS_SOLID;
    private int LAYER_POROUS_DEBRIS;

    // Shader property IDs for performance
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int TintProperty = Shader.PropertyToID("_TintColor");

    void Awake()
    {
        // Get layer indices by name to avoid hardcoding
        LAYER_ITEMS_SOLID = LayerMask.NameToLayer("Items");
        LAYER_POROUS_DEBRIS = LayerMask.NameToLayer("PorousDebris");

        // Validate layers exist
        if (LAYER_ITEMS_SOLID == -1)
        {
            Debug.LogError("ItemDegradation: 'Items' layer not found! Please add it to the Layer Manager.");
        }
        if (LAYER_POROUS_DEBRIS == -1)
        {
            Debug.LogWarning("ItemDegradation: 'PorousDebris' layer not found! Please add it to Layer 11 in the Layer Manager.");
            LAYER_POROUS_DEBRIS = 11; // Fallback to expected index
        }

        // Cache components once
        itemRenderer = GetComponentInChildren<Renderer>();
        itemController = GetComponent<ItemController>();
        ragdollController = GetComponent<RagdollController>();

        // Find ItemPooler
        itemPooler = FindObjectOfType<ItemPooler>();

        // Create MaterialPropertyBlock for efficient color changes
        if (itemRenderer != null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        // Try to get Item data from controllers if not set
        if (itemData == null)
        {
            if (itemController != null)
                itemData = itemController.item;
            else if (ragdollController != null)
                itemData = ragdollController.item;
        }
    }

    void OnEnable()
    {
        Debug.Log($"[DEGRADE] OnEnable at position {transform.position}");

        // Reset state when reused from pool
        ResetState();

        // Store position now that item is properly positioned before activation
        originalPosition = transform.position;
    }

    void Update()
    {
        // Only accumulate exposure if we've had recent oil contact
        float gracePeriod = itemData != null ? itemData.exposureGracePeriod : 0.4f;
        if (Time.time - lastOilContactTime < gracePeriod)
        {
            exposureSeconds += Time.deltaTime;
            UpdateDegradationState();
        }

        // Update visual feedback based on current state
        UpdateVisuals();
    }

    /// <summary>
    /// Called when oil particles hit this item
    /// </summary>
    public void RegisterOilExposure(int hitCount = 1)
    {
        if (itemData == null) return;

        lastOilContactTime = Time.time;
        particlesBlockedCount += hitCount;

        // Add exposure per particle hit
        float exposureGain = itemData.exposurePerParticle * hitCount;

        // Accelerate if over capacity
        if (particlesBlockedCount > itemData.blockCapacity)
        {
            exposureGain *= 1.5f; // 50% faster when over capacity
        }

        exposureSeconds += exposureGain;

        // Immediately check state in case particle count threshold is met
        UpdateDegradationState();
    }

    private void UpdateDegradationState()
    {
        if (itemData == null) return;

        DegradationState previousState = currentState;

        // HYBRID APPROACH: Check both particle count AND exposure time

        // Check for Sludge (exposure only for now)
        if (exposureSeconds >= itemData.exposureToSludge && itemData.exposureToSludge > 0)
        {
            currentState = DegradationState.Sludge;
        }
        // Check for Saturated (whichever comes first)
        else if (particlesBlockedCount >= itemData.particlesToSaturated ||
                 exposureSeconds >= itemData.exposureToSaturated)
        {
            currentState = DegradationState.Saturated;
        }
        // Check for Saturating (whichever comes first)
        else if (particlesBlockedCount >= itemData.particlesToSaturating ||
                 exposureSeconds >= itemData.exposureToSaturating)
        {
            currentState = DegradationState.Saturating;
        }
        else
        {
            currentState = DegradationState.Dry;
        }

        // Handle state transitions
        if (previousState != currentState)
        {
            OnStateChanged(previousState, currentState);
        }
    }

    private void OnStateChanged(DegradationState from, DegradationState to)
    {
        // Show which threshold triggered the change
        string trigger = "";
        if (to == DegradationState.Saturating)
        {
            bool hitParticleThreshold = particlesBlockedCount >= itemData.particlesToSaturating;
            bool hitTimeThreshold = exposureSeconds >= itemData.exposureToSaturating;
            trigger = hitParticleThreshold ? " (PARTICLE threshold)" : " (TIME threshold)";
        }
        else if (to == DegradationState.Saturated)
        {
            bool hitParticleThreshold = particlesBlockedCount >= itemData.particlesToSaturated;
            bool hitTimeThreshold = exposureSeconds >= itemData.exposureToSaturated;
            trigger = hitParticleThreshold ? " (PARTICLE threshold)" : " (TIME threshold)";
        }

        Debug.Log($"[DEGRADE] {to}{trigger}: hits={particlesBlockedCount}, exposure={exposureSeconds:F1}s");

        // Change layer when becoming porous
        if (to >= DegradationState.Saturating && !hasChangedLayer)
        {
            SetLayerRecursive(gameObject, LAYER_POROUS_DEBRIS);
            DisableForceFields();
            hasChangedLayer = true;
            Debug.Log($"[DEGRADE] Layer changed to PorousDebris recursively - oil will pass through, force fields disabled");
        }

        // Apply sinking if configured
        if (itemData != null && itemData.sinksOnExposure && to > from)
        {
            ApplySinking();
        }
    }

    private void UpdateVisuals()
    {
        if (itemRenderer == null || propertyBlock == null || itemData == null) return;

        Color targetColor = Color.white;

        switch (currentState)
        {
            case DegradationState.Dry:
                targetColor = Color.white;
                break;
            case DegradationState.Saturating:
                targetColor = itemData.tintSaturating;
                break;
            case DegradationState.Saturated:
                targetColor = itemData.tintSaturated;
                break;
            case DegradationState.Sludge:
                targetColor = itemData.tintSludge;
                break;
        }

        // Apply porosity-based alpha if in transitional state
        if (currentState == DegradationState.Saturating)
        {
            float normalizedExposure = Mathf.InverseLerp(
                itemData.exposureToSaturating,
                itemData.exposureToSaturated,
                exposureSeconds
            );
            float porosity = itemData.porosityCurve.Evaluate(normalizedExposure);
            targetColor.a = Mathf.Lerp(1f, 0.7f, porosity);
        }

        // Apply color via MaterialPropertyBlock (avoids material instancing)
        itemRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorProperty, targetColor);
        itemRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplySinking()
    {
        if (itemData == null) return;

        // Apply downward movement per state change
        transform.position += Vector3.down * itemData.sinkStepPerStage;
    }

    private void ResetState()
    {
        currentState = DegradationState.Dry;
        exposureSeconds = 0f;
        particlesBlockedCount = 0;
        lastOilContactTime = 0f;
        hasChangedLayer = false;

        // Reset to solid layer (recursively)
        SetLayerRecursive(gameObject, LAYER_ITEMS_SOLID);

        // Re-enable force fields for fresh items
        EnableForceFields();

        // Reset visuals
        if (itemRenderer != null && propertyBlock != null)
        {
            itemRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorProperty, Color.white);
            itemRenderer.SetPropertyBlock(propertyBlock);
        }

        // Don't store position here - it's done after positioning in StorePositionNextFrame()
    }

    /// <summary>
    /// Get current porosity value (0 = solid, 1 = fully porous)
    /// </summary>
    public float GetPorosity()
    {
        if (itemData == null) return 0f;

        if (currentState == DegradationState.Dry) return 0f;
        if (currentState >= DegradationState.Saturated) return 1f;

        // Calculate porosity for transitional state
        float normalizedExposure = Mathf.InverseLerp(
            itemData.exposureToSaturating,
            itemData.exposureToSaturated,
            exposureSeconds
        );

        return itemData.porosityCurve.Evaluate(normalizedExposure);
    }

    /// <summary>
    /// Force item to specific degradation state (for testing)
    /// </summary>
    public void ForceState(DegradationState state, float exposure)
    {
        currentState = state;
        exposureSeconds = exposure;
        UpdateVisuals();

        if (state >= DegradationState.Saturating)
        {
            SetLayerRecursive(gameObject, LAYER_POROUS_DEBRIS);
            DisableForceFields();
            hasChangedLayer = true;
        }
    }

    /// <summary>
    /// Recursively set layer for GameObject and all children
    /// </summary>
    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Disable all ParticleSystemForceField components in hierarchy
    /// </summary>
    private void DisableForceFields()
    {
        ParticleSystemForceField[] forceFields = GetComponentsInChildren<ParticleSystemForceField>();
        foreach (var field in forceFields)
        {
            field.enabled = false;
            Debug.Log($"[DEGRADE] Disabled force field on {field.gameObject.name}");
        }
    }

    /// <summary>
    /// Re-enable all ParticleSystemForceField components in hierarchy (for reset)
    /// </summary>
    private void EnableForceFields()
    {
        ParticleSystemForceField[] forceFields = GetComponentsInChildren<ParticleSystemForceField>();
        foreach (var field in forceFields)
        {
            field.enabled = true;
            Debug.Log($"[DEGRADE] Re-enabled force field on {field.gameObject.name}");
        }
    }

    // Called from particle collision (integrate with existing OnParticleCollision)
    void OnParticleCollision(GameObject other)
    {
        if (other.layer == LayerMask.NameToLayer("OilSpill"))
        {
            RegisterOilExposure();
        }
    }
}