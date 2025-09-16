using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages multiple oil leak points to prevent perfect sealing
/// Single authority for all oil leaks - controls emission, collisions, and state
/// </summary>
public enum LeakManagerState
{
    Menu,       // Pre-game aesthetics (ambient oil, no gameplay)
    Running,    // Game active (managed leaks, full gameplay)
    Paused,     // Game paused (emission/collisions off)
    Stopped     // Game ended (particles cleared)
}

public class LeakManager : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private LeakManagerState currentState = LeakManagerState.Menu;
    public LeakManagerState CurrentState => currentState;
    [SerializeField] private bool debugStateChanges = true;

    // Singleton for easier access
    public static LeakManager Instance { get; private set; }

    [Header("Leak Configuration")]
    [SerializeField] private GameObject oilLeakPrefab; // REQUIRED: Prefab with ParticleSystem and OilController
    [SerializeField] private int maxLeaks = 3;
    [SerializeField] private float minLeakSpacing = 5f; // Minimum distance between leaks
    [SerializeField] private float leakSpawnAreaWidth = 20f; // Width of spawn area
    [SerializeField] private Vector3 baseLeakPosition = new Vector3(0, -27.9f, 0); // Ocean floor position
    [SerializeField] private Vector3 ambientOffset = Vector3.zero; // Optional offset for menu ambient leak
    [SerializeField] private bool useCustomRotation = false; // Override prefab rotation if needed
    [SerializeField] private Vector3 customLeakRotation = new Vector3(-90f, 0f, 0f); // Custom rotation if override enabled

    [Header("Timing")]
    [SerializeField] private float secondLeakAtSec = 120f; // 2 minutes for second leak
    [SerializeField] private float thirdLeakAtSec = 300f; // 5 minutes for third leak
    [SerializeField] private float ambientRate = 9f; // Low rate for menu aesthetics

    [Header("Pressure System")]
    [SerializeField] private bool enablePressureSystem = true;
    [SerializeField] private float pressureBuildupRate = 0.5f; // Increased - Pressure per blocked particle
    [SerializeField] private float pressureReleaseThreshold = 100f; // Now triggers after ~200 blocked particles
    [SerializeField] private float burstEmissionMultiplier = 3f; // Emission boost during burst
    [SerializeField] private float burstDuration = 5f; // How long burst lasts

    [Header("Burst Physics")]
    [SerializeField] private float burstExplosionRadius = 3f;
    [SerializeField] private float burstExplosionForce = 100f;
    [SerializeField] private float burstUpwardModifier = 0.7f;
    [SerializeField] private int maxAffectedBodies = 20;
    [SerializeField] private float burstCooldown = 12f; // Minimum time between bursts

    // State management
    private List<OilController> managedLeaks = new List<OilController>(); // Running state leaks
    private OilController ambientLeak; // Menu state leak
    private float runStartTime;
    private float nextLeakSpawnTime;

    // Pressure state
    private float currentPressure = 0f;
    private bool isBursting = false;
    private float burstEndTime = 0f;
    private float lastBurstTime = -999f;

    // Cached layer masks
    private int itemsLayer;
    private int porousLayer;
    private int surfaceLayer;
    private LayerMask itemsMask;
    private LayerMask porousMask;
    private LayerMask surfaceMask;
    private LayerMask collisionMask; // Combined mask for particle collisions

    // Throttling
    private float lastUpdateTime = 0f;
    private float updateInterval = 0.5f; // 2Hz update rate

    // Events
    public static event System.Action<int> OnNewLeakCreated;
    public static event System.Action<float> OnPressureBurst;

    void OnValidate()
    {
        #if UNITY_EDITOR
        if (oilLeakPrefab == null)
        {
            Debug.LogWarning("LeakManager: oilLeakPrefab is not assigned! This is required for spawning leaks.");
        }
        else
        {
            // Validate prefab has required components
            var ps = oilLeakPrefab.GetComponent<ParticleSystem>();
            var controller = oilLeakPrefab.GetComponent<OilController>();
            if (ps == null || controller == null)
            {
                Debug.LogWarning("LeakManager: oilLeakPrefab must have both ParticleSystem and OilController components!");
            }
        }
        #endif
    }

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Validate prefab at runtime
        if (oilLeakPrefab == null)
        {
            Debug.LogError("LeakManager: oilLeakPrefab is not assigned! Disabling LeakManager.");
            enabled = false;
            return;
        }

        // Cache layer masks
        itemsLayer = LayerMask.NameToLayer("Items");
        porousLayer = LayerMask.NameToLayer("PorousDebris");
        surfaceLayer = LayerMask.NameToLayer("Surface");
        itemsMask = 1 << itemsLayer;
        porousMask = 1 << porousLayer;
        surfaceMask = 1 << surfaceLayer;

        // Combined mask for particle collisions (Items + Surface, excluding PorousDebris)
        collisionMask = itemsMask | surfaceMask;
        Debug.Log($"LeakManager: Collision mask includes - Items: {itemsLayer}, Surface: {surfaceLayer}, Combined mask value: {collisionMask}");

        if (itemsLayer == -1)
        {
            Debug.LogWarning("LeakManager: 'Items' layer not found! Collision detection may not work.");
        }
        if (porousLayer == -1)
        {
            Debug.LogWarning("LeakManager: 'PorousDebris' layer not found! This is expected if not yet created.");
        }
        if (surfaceLayer == -1)
        {
            Debug.LogWarning("LeakManager: 'Surface' layer not found! Particles won't collide with water surface.");
        }
    }

    void Start()
    {
        // Start in Menu state with aesthetic oil
        InitializeMenuState();
    }

    void OnDisable()
    {
        // Clean up everything
        StopAndClear();
    }

    void Update()
    {
        // Only process game logic when running
        if (currentState != LeakManagerState.Running) return;

        float elapsed = Time.time - runStartTime;

        // Spawn additional leaks based on time
        if (managedLeaks.Count == 1 && elapsed >= secondLeakAtSec)
        {
            Debug.Log($"Creating second leak at {elapsed:F1} seconds");
            SpawnAdditionalManagedLeak();
        }
        else if (managedLeaks.Count == 2 && elapsed >= thirdLeakAtSec)
        {
            Debug.Log($"Creating third leak at {elapsed:F1} seconds");
            SpawnAdditionalManagedLeak();
        }

        // Throttle pressure updates to 2Hz
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            if (enablePressureSystem)
            {
                UpdatePressureSystem();
            }
            lastUpdateTime = Time.time;
        }
    }

    // === Lifecycle Methods ===

    /// <summary>
    /// Initialize menu state with ambient oil for aesthetics
    /// </summary>
    public void InitializeMenuState()
    {
        if (oilLeakPrefab == null)
        {
            Debug.LogError("LeakManager: Cannot initialize - oilLeakPrefab is null!");
            return;
        }

        ChangeState(LeakManagerState.Menu);

        // Clear any managed leaks
        DestroyManagedLeaks();

        // Ensure we have an ambient leak
        if (ambientLeak == null)
        {
            Vector3 position = baseLeakPosition + ambientOffset;
            Debug.Log($"LeakManager: Creating ambient leak at position {position} (base: {baseLeakPosition}, offset: {ambientOffset})");
            // Use the prefab's authored rotation (preserves Shape module orientation)
            GameObject leakObj = Instantiate(oilLeakPrefab, position, GetLeakRotation());
            leakObj.name = "AmbientOilLeak";
            ambientLeak = leakObj.GetComponent<OilController>();
            Debug.Log($"LeakManager: Ambient leak actual position: {leakObj.transform.position}");
        }

        // Configure ambient leak for menu aesthetics
        if (ambientLeak != null)
        {
            ambientLeak.EnableCollisions(false, 0); // No collisions in menu
            ambientLeak.ApplyEmission(ambientRate);
            ambientLeak.EnableEmission(true);
        }

        // Disable pressure/burst in menu
        currentPressure = 0f;
        isBursting = false;

        Debug.Log("LeakManager: Initialized menu state with ambient oil");
    }

    /// <summary>
    /// Start a new run - transitions from Menu to Running state
    /// </summary>
    public void StartRun()
    {
        if (oilLeakPrefab == null)
        {
            Debug.LogError("LeakManager: Cannot start run - oilLeakPrefab is null!");
            return;
        }

        ChangeState(LeakManagerState.Running);

        // Destroy ambient leak
        if (ambientLeak != null)
        {
            Destroy(ambientLeak.gameObject);
            ambientLeak = null;
        }

        // Clear any existing managed leaks (shouldn't be any, but be safe)
        DestroyManagedLeaks();

        // Spawn initial managed leak
        Debug.Log($"LeakManager: Creating managed leak at baseLeakPosition: {baseLeakPosition}");
        // Use the prefab's authored rotation (preserves Shape module orientation)
        GameObject firstLeak = Instantiate(oilLeakPrefab, baseLeakPosition, GetLeakRotation());
        firstLeak.name = "ManagedOilLeak_1";
        Debug.Log($"LeakManager: Managed leak actual position: {firstLeak.transform.position}, rotation: {firstLeak.transform.eulerAngles}");
        OilController controller = firstLeak.GetComponent<OilController>();

        if (controller != null)
        {
            managedLeaks.Add(controller);
            controller.EnableCollisions(true, collisionMask); // Enable collisions with Items and Surface layers
            controller.EnableEmission(true);
        }

        // Reset timers
        runStartTime = Time.time;
        nextLeakSpawnTime = runStartTime + secondLeakAtSec;

        // Reset pressure/burst state
        currentPressure = 0f;
        isBursting = false;
        lastBurstTime = -999f;

        // Apply initial emission budget
        if (DifficultyManager.Instance != null)
        {
            SetEmissionRate(DifficultyManager.Instance.GetCurrentEmissionRate());
        }

        OnNewLeakCreated?.Invoke(managedLeaks.Count);
        Debug.Log("LeakManager: Started run with initial managed leak");
    }

    /// <summary>
    /// Pause the current run
    /// </summary>
    public void PauseRun()
    {
        if (currentState != LeakManagerState.Running) return;

        ChangeState(LeakManagerState.Paused);

        // Disable emission and collisions for all managed leaks
        foreach (var leak in managedLeaks)
        {
            if (leak != null)
            {
                leak.EnableEmission(false);
                leak.EnableCollisions(false, 0);
                leak.PauseSimulation(); // Freeze particles
            }
        }

        Debug.Log("LeakManager: Paused run");
    }

    /// <summary>
    /// Resume from pause
    /// </summary>
    public void ResumeRun()
    {
        if (currentState != LeakManagerState.Paused) return;

        ChangeState(LeakManagerState.Running);

        // Re-enable emission and collisions for all managed leaks
        foreach (var leak in managedLeaks)
        {
            if (leak != null)
            {
                leak.EnableEmission(true);
                leak.EnableCollisions(true, collisionMask); // Items and Surface layers
                leak.ResumeSimulation(); // Unfreeze particles
            }
        }

        // Reapply emission budget
        if (DifficultyManager.Instance != null)
        {
            SetEmissionRate(DifficultyManager.Instance.GetCurrentEmissionRate());
        }

        Debug.Log("LeakManager: Resumed run");
    }

    /// <summary>
    /// End the current run and transition to stopped state
    /// </summary>
    public void EndRun()
    {
        ChangeState(LeakManagerState.Stopped);

        // Clear everything
        StopAndClear();

        Debug.Log("LeakManager: Ended run, transitioning to stopped state");

        // Optionally return to menu (can be called separately if needed)
        // InitializeMenuState();
    }

    /// <summary>
    /// Utility to stop and clear all leaks
    /// </summary>
    public void StopAndClear()
    {
        // Destroy ambient leak if exists
        if (ambientLeak != null)
        {
            ambientLeak.ResetOilSystem();
            ambientLeak.EnableEmission(false);
            Destroy(ambientLeak.gameObject);
            ambientLeak = null;
        }

        // Destroy all managed leaks
        DestroyManagedLeaks();

        // Reset pressure/burst state
        currentPressure = 0f;
        isBursting = false;
        lastBurstTime = -999f;
    }

    // === Emission Management ===

    /// <summary>
    /// Set emission rate for all leaks (called by DifficultyManager)
    /// </summary>
    public void SetEmissionRate(float totalRate)
    {
        // Only apply to managed leaks during Running state
        if (currentState != LeakManagerState.Running || managedLeaks.Count == 0) return;

        // Divide the total emission budget across all active leaks
        float perLeakRate = totalRate / managedLeaks.Count;

        // Apply burst multiplier if bursting
        float actualRate = isBursting ? perLeakRate * burstEmissionMultiplier : perLeakRate;

        foreach (var leak in managedLeaks)
        {
            if (leak != null)
            {
                leak.ApplyEmission(actualRate);
            }
        }
    }

    /// <summary>
    /// Called when particles are blocked to build pressure
    /// </summary>
    public void OnParticleBlocked(int count)
    {
        // Only track pressure during active gameplay
        if (currentState != LeakManagerState.Running) return;

        if (enablePressureSystem && !isBursting)
        {
            currentPressure += count * pressureBuildupRate;
        }
    }

    // === Private Helper Methods ===

    /// <summary>
    /// Get the rotation to use for spawning leaks
    /// </summary>
    private Quaternion GetLeakRotation()
    {
        if (useCustomRotation)
        {
            return Quaternion.Euler(customLeakRotation);
        }
        else if (oilLeakPrefab != null)
        {
            return oilLeakPrefab.transform.rotation;
        }
        else
        {
            return Quaternion.identity;
        }
    }

    private void ChangeState(LeakManagerState newState)
    {
        if (currentState != newState)
        {
            if (debugStateChanges)
            {
                Debug.Log($"LeakManager: State change {currentState} → {newState}");
            }
            currentState = newState;
        }
    }

    private void DestroyManagedLeaks()
    {
        foreach (var leak in managedLeaks)
        {
            if (leak != null)
            {
                leak.ResetOilSystem();
                leak.EnableEmission(false);
                Destroy(leak.gameObject);
            }
        }
        managedLeaks.Clear();
    }

    private void SpawnAdditionalManagedLeak()
    {
        if (managedLeaks.Count >= maxLeaks) return;

        Vector3 position = FindSpacedLeakPosition();
        // Use the prefab's authored rotation (preserves Shape module orientation)
        GameObject leak = Instantiate(oilLeakPrefab, position, GetLeakRotation());
        leak.name = $"ManagedOilLeak_{managedLeaks.Count + 1}";

        OilController controller = leak.GetComponent<OilController>();
        if (controller != null)
        {
            managedLeaks.Add(controller);
            controller.EnableCollisions(true, collisionMask); // Items and Surface layers
            controller.EnableEmission(true);
        }

        // Redistribute emission budget
        if (DifficultyManager.Instance != null)
        {
            SetEmissionRate(DifficultyManager.Instance.GetCurrentEmissionRate());
        }

        OnNewLeakCreated?.Invoke(managedLeaks.Count);
        Debug.Log($"Created managed leak {managedLeaks.Count} at position {position}");
    }

    private Vector3 FindSpacedLeakPosition()
    {
        int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            float xOffset = Random.Range(-leakSpawnAreaWidth / 2, leakSpawnAreaWidth / 2);
            Vector3 candidatePos = baseLeakPosition + new Vector3(xOffset, 0, 0);

            bool validPosition = true;
            foreach (var leak in managedLeaks)
            {
                if (Vector3.Distance(leak.transform.position, candidatePos) < minLeakSpacing)
                {
                    validPosition = false;
                    break;
                }
            }

            if (validPosition)
            {
                return candidatePos;
            }
        }

        // Fallback: offset from base position
        return baseLeakPosition + new Vector3(managedLeaks.Count * minLeakSpacing, 0, 0);
    }

    private void UpdatePressureSystem()
    {
        // Check if burst is ending
        if (isBursting && Time.time >= burstEndTime)
        {
            EndPressureBurst();
        }

        // Check if pressure threshold reached
        if (!isBursting && currentPressure >= pressureReleaseThreshold &&
            Time.time - lastBurstTime >= burstCooldown)
        {
            TriggerPressureBurst();
        }
    }

    private void TriggerPressureBurst()
    {
        isBursting = true;
        burstEndTime = Time.time + burstDuration;
        lastBurstTime = Time.time;

        // Apply explosion force to nearby items
        ApplyBurstExplosion();

        // Reapply emission with burst multiplier
        if (DifficultyManager.Instance != null)
        {
            SetEmissionRate(DifficultyManager.Instance.GetCurrentEmissionRate());
        }

        // Reset pressure
        currentPressure = 0f;

        OnPressureBurst?.Invoke(burstEmissionMultiplier);
        Debug.Log($"PRESSURE BURST! Emission multiplied by {burstEmissionMultiplier}x for {burstDuration} seconds");
    }

    private void ApplyBurstExplosion()
    {
        // Apply explosion to each managed leak
        foreach (var leak in managedLeaks)
        {
            if (leak == null) continue;

            Vector3 explosionPos = leak.transform.position;

            // Use cached layer masks
            LayerMask affectedMask = itemsMask | porousMask;

            // Find all colliders in burst radius
            Collider[] colliders = Physics.OverlapSphere(explosionPos, burstExplosionRadius, affectedMask);

            int bodiesAffected = 0;
            foreach (Collider col in colliders)
            {
                if (bodiesAffected >= maxAffectedBodies) break;

                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    float distance = Vector3.Distance(explosionPos, col.transform.position);
                    float falloff = 1f - (distance / burstExplosionRadius);
                    falloff = Mathf.Clamp01(falloff);

                    Vector3 explosionDir = (col.transform.position - explosionPos).normalized;
                    explosionDir.y += burstUpwardModifier;
                    explosionDir.Normalize();

                    rb.AddForce(explosionDir * burstExplosionForce * falloff, ForceMode.Impulse);
                    bodiesAffected++;
                }
            }

            Debug.Log($"Burst explosion at {explosionPos} affected {bodiesAffected} items");
        }
    }

    private void EndPressureBurst()
    {
        isBursting = false;

        // Reset emission rates without burst multiplier
        if (DifficultyManager.Instance != null)
        {
            SetEmissionRate(DifficultyManager.Instance.GetCurrentEmissionRate());
        }

        Debug.Log("Pressure burst ended, emission rates normalized");
    }

    // === Public Getters for DevHUD ===

    public int GetActiveLeakCount() => managedLeaks.Count;
    public bool HasAmbientLeak() => ambientLeak != null;
    public float GetCurrentPressure() => currentPressure;
    public float GetPressurePercentage() => pressureReleaseThreshold > 0 ? currentPressure / pressureReleaseThreshold : 0f;
    public bool IsBursting() => isBursting;

    public int GetTotalActiveParticles()
    {
        int total = 0;

        // Count ambient leak particles
        if (ambientLeak != null)
        {
            total += ambientLeak.GetActiveParticleCount();
        }

        // Count managed leak particles
        foreach (var leak in managedLeaks)
        {
            if (leak != null)
            {
                total += leak.GetActiveParticleCount();
            }
        }

        return total;
    }
}