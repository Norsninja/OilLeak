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
    [SerializeField] protected DegradationState currentState = DegradationState.Dry;
    [SerializeField] private float exposureSeconds = 0f;
    [SerializeField] private int particlesBlockedCount = 0;

    [Header("Configuration")]
    [SerializeField] protected Item itemData; // Reference to Item ScriptableObject - protected for subclass access

    // Components - protected for subclass access
    protected Renderer itemRenderer;
    protected MaterialPropertyBlock propertyBlock;
    protected ItemController itemController;
    protected RagdollController ragdollController;
    private ItemPooler itemPooler;

    // State tracking
    private float lastOilContactTime = 0f;
    private bool hasChangedLayer = false;
    protected Vector3 originalPosition;  // Protected for subclass access

    // Layer references - protected for subclass access
    protected int LAYER_ITEMS_SOLID;
    protected int LAYER_POROUS_DEBRIS;

    // Shader property IDs - protected for subclass access
    protected static readonly int ColorProperty = Shader.PropertyToID("_Color");
    protected static readonly int TintProperty = Shader.PropertyToID("_TintColor");

    protected virtual void Awake()
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

        // Delegate component initialization to virtual method
        InitializeComponents();
    }

    // Virtual method for component initialization - can be overridden by subclasses
    protected virtual void InitializeComponents()
    {
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

    protected virtual void OnEnable()
    {
        Debug.Log($"[DEGRADE] OnEnable at position {transform.position}");

        // Reset state when reused from pool
        ResetState();

        // Store position now that item is properly positioned before activation
        OnPositionSet();
    }

    // Virtual method called when position is set - can be overridden
    protected virtual void OnPositionSet()
    {
        originalPosition = transform.position;
    }

    // Virtual method for cleanup - can be overridden
    protected virtual void OnDisable()
    {
        // Subclasses can override this for cleanup
        // Base ItemDegradation doesn't subscribe to any events
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

        // Check for Sludge (particles OR exposure - whichever comes first)
        if ((particlesBlockedCount >= itemData.particlesToSludge && itemData.particlesToSludge > 0) ||
            (exposureSeconds >= itemData.exposureToSludge && itemData.exposureToSludge > 0))
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
        else if (to == DegradationState.Sludge)
        {
            bool hitParticleThreshold = particlesBlockedCount >= itemData.particlesToSludge;
            bool hitTimeThreshold = exposureSeconds >= itemData.exposureToSludge;
            trigger = hitParticleThreshold ? " (PARTICLE threshold)" : " (TIME threshold)";
        }

        Debug.Log($"[DEGRADE] {to}{trigger}: hits={particlesBlockedCount}, exposure={exposureSeconds:F1}s");

        // Change layer only when reaching Sludge state OR max capacity (fully degraded)
        // Items continue blocking particles through Saturating and Saturated states
        if ((to == DegradationState.Sludge || particlesBlockedCount >= itemData.blockCapacity) && !hasChangedLayer)
        {
            OnLayerChange(LAYER_POROUS_DEBRIS);  // Route through virtual method
            DisableForceFields();
            hasChangedLayer = true;
            Debug.Log($"[DEGRADE] Reached Sludge/Max capacity - now porous, oil passes through (particles: {particlesBlockedCount}, capacity: {itemData.blockCapacity})");
        }

        // Apply sinking if configured
        if (itemData != null && itemData.sinksOnExposure && to > from)
        {
            ApplySinking();
        }
    }

    private void UpdateVisuals()
    {
        if (itemData == null) return;

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

        // Delegate visual application to virtual method
        ApplyDegradationVisuals(targetColor);
    }

    // Virtual method for applying visuals - can be overridden for multi-renderer support
    protected virtual void ApplyDegradationVisuals(Color targetColor)
    {
        if (itemRenderer == null || propertyBlock == null) return;

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
        OnLayerChange(LAYER_ITEMS_SOLID);  // Route through virtual method

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
            OnLayerChange(LAYER_POROUS_DEBRIS);  // Route through virtual method
            DisableForceFields();
            hasChangedLayer = true;
        }
    }

    // Virtual method for layer changes - can be overridden for smart layer management
    protected virtual void OnLayerChange(int newLayer)
    {
        SetLayerRecursive(gameObject, newLayer);
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