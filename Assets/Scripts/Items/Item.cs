using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Item", order = 2)]
public class Item : ScriptableObject
{
    [Header("Basic Properties")]
    public string itemName;
    public float buoyancy;
    public float weight;  // Add this new weight variable
    public int cost;
    public GameObject itemPrefab;  // Reference to the item's prefab
    public bool isRagdoll;         // Flag to identify if the item is a ragdoll

    [Header("Degradation - Exposure Based")]
    [Tooltip("Seconds of oil exposure before item starts becoming porous")]
    public float exposureToSaturating = 20f;  // Increased for better blocking

    [Tooltip("Seconds of oil exposure before item is fully saturated")]
    public float exposureToSaturated = 40f;  // Increased for longer effectiveness

    [Tooltip("Seconds of oil exposure before item becomes sludge (optional)")]
    public float exposureToSludge = 180f;

    [Tooltip("How porous the item is over exposure time (0=solid, 1=fully porous)")]
    public AnimationCurve porosityCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("Maximum particles this item can block before saturating faster")]
    public float blockCapacity = 50f;  // Increased for more blocking capacity

    [Header("Degradation - Particle Count Based")]
    [Tooltip("Number of particles to hit before entering Saturating state")]
    public int particlesToSaturating = 30;  // Increased from 12

    [Tooltip("Number of particles to hit before fully Saturated")]
    public int particlesToSaturated = 80;  // Increased from 36

    [Tooltip("Number of particles to hit before Sludge state")]
    public int particlesToSludge = 120;  // New field for sludge transition

    [Tooltip("Exposure seconds added per particle hit")]
    public float exposurePerParticle = 0.3f;

    [Tooltip("Grace period after particle hit for continuous exposure")]
    public float exposureGracePeriod = 0.4f;

    [Header("Physics Behavior")]
    [Tooltip("Should item sink as it becomes saturated?")]
    public bool sinksOnExposure = false;

    [Tooltip("How much to sink per degradation stage")]
    public float sinkStepPerStage = 0.5f;

    [Header("Visual Feedback")]
    [Tooltip("Tint color when saturating")]
    public Color tintSaturating = new Color(0.8f, 0.7f, 0.5f, 1f);

    [Tooltip("Tint color when saturated")]
    public Color tintSaturated = new Color(0.5f, 0.4f, 0.3f, 1f);

    [Tooltip("Tint color when sludge")]
    public Color tintSludge = new Color(0.3f, 0.25f, 0.2f, 1f);
}

