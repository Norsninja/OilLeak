using UnityEngine;

/// <summary>
/// Configuration for a resupply event (air-drop, barge, etc)
/// </summary>
[CreateAssetMenu(fileName = "ResupplyEvent", menuName = "OilLeak/ResupplyEventConfig")]
public class ResupplyEventConfig : ScriptableObject
{
    [Header("Event Type")]
    public string eventName = "Air Drop";
    public ResupplyType type = ResupplyType.AirDrop;

    [Header("Timing")]
    [Tooltip("Minimum time between spawns (seconds)")]
    public float minInterval = 75f;

    [Tooltip("Maximum time between spawns (seconds)")]
    public float maxInterval = 120f;

    [Tooltip("Cooldown after event (seconds)")]
    public float cooldown = 90f;

    [Tooltip("Time window for spawn variation")]
    public float spawnWindow = 30f;

    [Header("Movement")]
    [Tooltip("Height above water for air events")]
    public float spawnHeight = 15f;

    [Tooltip("Speed of vehicle crossing screen")]
    public float vehicleSpeed = 8f;

    [Tooltip("Parachute descent speed (units/sec)")]
    public float parachuteDescentSpeed = 2f;

    [Tooltip("Horizontal drift while descending")]
    public float horizontalDrift = 0.3f;

    [Header("Drops")]
    [Tooltip("Minimum packages to drop")]
    public int minDropCount = 1;

    [Tooltip("Maximum packages to drop")]
    public int maxDropCount = 3;

    [Tooltip("Time packages float before despawning")]
    public float despawnTime = 30f;

    [Tooltip("Radius for boat pickup")]
    public float pickupRadius = 1.5f;

    [Header("Rubber Band")]
    [Tooltip("Can be triggered by rubber-band assistance")]
    public bool allowRubberBandTrigger = true;

    [Tooltip("Priority when multiple events could trigger")]
    public int rubberBandPriority = 1;

    [Header("Visual")]
    [Tooltip("Prefab for the vehicle (plane/barge)")]
    public GameObject vehiclePrefab;

    [Tooltip("Prefab for the package/crate")]
    public GameObject packagePrefab;

    [Tooltip("Show on-screen indicator")]
    public bool showIndicator = true;

    [Header("Audio")]
    [Tooltip("Sound when vehicle appears")]
    public AudioClip spawnSound;

    [Tooltip("Sound when package is picked up")]
    public AudioClip pickupSound;
}

public enum ResupplyType
{
    AirDrop,
    Barge,
    Emergency,
    Milestone
}