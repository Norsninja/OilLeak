using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages all resupply events (air-drops, barges, etc)
/// Driven by LeakManager/EndlessMode lifecycle - not self-starting
/// Implements IResettable for deterministic cleanup during state transitions
/// </summary>
public class ResupplyManager : MonoBehaviour, IResettable
{
    [Header("Configuration")]
    [SerializeField] private ResupplyEventConfig airDropConfig;
    [SerializeField] private ResupplyEventConfig bargeConfig;
    [SerializeField] private LootTable grassrootsLoot;
    [SerializeField] private LootTable corporateLoot;
    [SerializeField] private LootTable desperationLoot;
    [SerializeField] private LootTable absurdistLoot;

    [Header("Water Settings")]
    [SerializeField] private float waterSurfaceY = 0f; // Adjust if water isn't at Y=0

    [Header("Rubber Band Settings")]
    [SerializeField] private bool enableRubberBandHelp = true;
    [SerializeField] private float rubberBandCooldown = 60f;
    [SerializeField] private float lowPerformanceThreshold = 0.5f;
    [SerializeField] private float lowPerformanceDuration = 10f;

    // State tracking
    private bool isActive = false;
    private float nextAirDropTime;
    private float nextBargeTime;
    private float lastRubberBandTime;
    private float struggleStartTime = -1f;

    // Active events - track separately for proper pooling
    private GameObject activeAircraft;
    private GameObject activeBarge;
    private List<GameObject> activePackages = new List<GameObject>();
    private List<GameObject> activeCrates = new List<GameObject>();

    // Object pools - keep separate for packages vs crates
    private Queue<GameObject> packagePool = new Queue<GameObject>();
    private Queue<GameObject> cratePool = new Queue<GameObject>();

    // Coroutine tracking to prevent double-despawn
    private Dictionary<GameObject, Coroutine> activeFloaters = new Dictionary<GameObject, Coroutine>();

    // References
    private GameController gameController;
    private InventoryController inventoryController;

    // Events
    public static event System.Action<string> OnResupplySpawned;
    public static event System.Action<string> OnResupplyPicked;

    void Awake()
    {
        // Find references
        gameController = GameController.Instance ?? FindObjectOfType<GameController>();
        inventoryController = FindObjectOfType<InventoryController>();

        // Initialize pools
        InitializePools();
    }

    void Update()
    {
        if (!isActive) return;

        float currentTime = GetGameTime();

        // Check for scheduled air-drop
        if (airDropConfig != null && currentTime >= nextAirDropTime && activeAircraft == null)
        {
            SpawnAirDrop();
            ScheduleNextAirDrop();
        }

        // Check for scheduled barge
        if (bargeConfig != null && currentTime >= nextBargeTime && activeBarge == null)
        {
            SpawnBarge();
            ScheduleNextBarge();
        }

        // Check rubber-band assistance
        if (enableRubberBandHelp && airDropConfig != null && airDropConfig.allowRubberBandTrigger)
        {
            CheckRubberBandTrigger();
        }
    }

    private void InitializePools()
    {
        // Pre-warm package pool
        if (airDropConfig?.packagePrefab != null)
        {
            for (int i = 0; i < 5; i++)
            {
                GameObject pkg = Instantiate(airDropConfig.packagePrefab);
                pkg.SetActive(false);
                packagePool.Enqueue(pkg);
            }
        }

        // Pre-warm crate pool
        if (bargeConfig?.packagePrefab != null)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject crate = Instantiate(bargeConfig.packagePrefab);
                crate.SetActive(false);
                cratePool.Enqueue(crate);
            }
        }
    }

    // Called by EndlessMode/LeakManager when entering Running state
    public void StartResupply()
    {
        isActive = true;
        ScheduleInitialEvents();
        Debug.Log("[RESUPPLY] Started! First air-drop in " + (nextAirDropTime - GetGameTime()) + " seconds");
    }

    private void ScheduleInitialEvents()
    {
        float currentTime = GetGameTime();

        // Schedule air-drop using config
        if (airDropConfig != null)
        {
            nextAirDropTime = currentTime + Random.Range(airDropConfig.minInterval, airDropConfig.maxInterval);
        }

        // Schedule barge using config (or hardcoded milestone)
        if (bargeConfig != null)
        {
            // First barge at 2 minutes as per design
            nextBargeTime = currentTime + 120f;
        }
    }

    private void ScheduleNextAirDrop()
    {
        if (airDropConfig == null) return;
        nextAirDropTime = GetGameTime() + Random.Range(airDropConfig.cooldown, airDropConfig.cooldown + airDropConfig.spawnWindow);
    }

    private void ScheduleNextBarge()
    {
        if (bargeConfig == null) return;
        // Next barge at 5 minutes (3 more minutes after first)
        nextBargeTime = GetGameTime() + 180f;
    }

    private void SpawnAirDrop()
    {
        if (airDropConfig?.vehiclePrefab == null)
        {
            Debug.LogWarning("[RESUPPLY] Cannot spawn air-drop: vehiclePrefab is null in airDropConfig!");
            return;
        }

        // Determine direction
        bool leftToRight = Random.value > 0.5f;

        // Get spawn positions using viewport (works for both ortho and perspective)
        Vector3 leftEdge = GetWorldEdgePosition(0f, airDropConfig.spawnHeight);
        Vector3 rightEdge = GetWorldEdgePosition(1f, airDropConfig.spawnHeight);

        Vector3 spawnPos = leftToRight ? leftEdge : rightEdge;
        float targetX = leftToRight ? rightEdge.x : leftEdge.x;

        // Spawn aircraft
        activeAircraft = Instantiate(airDropConfig.vehiclePrefab, spawnPos, Quaternion.identity);

        // Start aircraft movement
        StartCoroutine(MoveAircraft(activeAircraft, targetX, leftToRight));

        // Fire event
        OnResupplySpawned?.Invoke("Air-Drop inbound!");
    }

    private IEnumerator MoveAircraft(GameObject aircraft, float targetX, bool leftToRight)
    {
        if (aircraft == null || airDropConfig == null) yield break;

        float startX = aircraft.transform.position.x;
        float duration = airDropConfig.vehicleSpeed;
        float elapsed = 0f;

        // Schedule package drops based on config
        int dropCount = Random.Range(airDropConfig.minDropCount, airDropConfig.maxDropCount + 1);
        float[] dropTimes = new float[dropCount];
        for (int i = 0; i < dropCount; i++)
        {
            dropTimes[i] = Random.Range(0.3f, 0.7f) * duration;
        }

        int nextDropIndex = 0;

        while (elapsed < duration && isActive)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Move aircraft
            if (aircraft != null)
            {
                float currentX = Mathf.Lerp(startX, targetX, t);
                aircraft.transform.position = new Vector3(currentX, airDropConfig.spawnHeight, 0);

                // Check for package drops
                if (nextDropIndex < dropCount && elapsed >= dropTimes[nextDropIndex])
                {
                    DropPackage(aircraft.transform.position);
                    nextDropIndex++;
                }
            }

            yield return null;
        }

        // Clean up aircraft
        if (aircraft != null)
        {
            Destroy(aircraft);
        }
        activeAircraft = null;
    }

    private void DropPackage(Vector3 dropPosition)
    {
        if (airDropConfig == null) return;

        GameObject package = GetPackageFromPool();
        if (package == null) return;

        // CRITICAL: Prepare pickup BEFORE activation to prevent OnEnable overrides
        PreparePickup(package, dropPosition);

        // Add to active list
        activePackages.Add(package);

        // NOW it's safe to activate
        package.SetActive(true);

        // Debug: Check spawn state (uncomment if pickup issues return)
        // Rigidbody packageRb = package.GetComponent<Rigidbody>();
        // if (packageRb != null)
        // {
        //     Debug.Log($"[ResupplyManager] Package spawned - Sleeping: {packageRb.IsSleeping()}, Kinematic: {packageRb.isKinematic}");
        // }

        // Start parachute descent
        StartCoroutine(ParachuteDescend(package));
    }

    private IEnumerator ParachuteDescend(GameObject package)
    {
        if (package == null || airDropConfig == null) yield break;

        float descentSpeed = airDropConfig.parachuteDescentSpeed;
        float horizontalDrift = Random.Range(-airDropConfig.horizontalDrift, airDropConfig.horizontalDrift);

        // Descend with parachute
        while (package.transform.position.y > waterSurfaceY && package.activeSelf && isActive)
        {
            Vector3 movement = new Vector3(
                horizontalDrift * Time.deltaTime,
                -descentSpeed * Time.deltaTime,
                0
            );
            package.transform.position += movement;
            yield return null;
        }

        // Convert to floating crate at water surface
        if (package.activeSelf)
        {
            ConvertToFloatingCrate(package);
        }
    }

    private void ConvertToFloatingCrate(GameObject package)
    {
        // Position at water surface
        package.transform.position = new Vector3(package.transform.position.x, waterSurfaceY, package.transform.position.z);

        // Start floating behavior and track coroutine
        Coroutine floater = StartCoroutine(FloatAndDespawn(package, true)); // true = is package
        activeFloaters[package] = floater;
    }

    private IEnumerator FloatAndDespawn(GameObject floatingObject, bool isPackage)
    {
        float floatDuration = isPackage && airDropConfig != null ? airDropConfig.despawnTime :
                              bargeConfig != null ? bargeConfig.despawnTime : 30f;

        float elapsed = 0f;
        float bobSpeed = 2f;
        float bobHeight = 0.2f;
        Vector3 basePosition = floatingObject.transform.position;

        // Get Rigidbody for physics-aware movement
        Rigidbody rb = floatingObject.GetComponent<Rigidbody>();
        bool usePhysicsMovement = rb != null;

        while (elapsed < floatDuration && floatingObject.activeSelf && isActive)
        {
            elapsed += Time.deltaTime;

            // Calculate new position
            float yOffset = Mathf.Sin(elapsed * bobSpeed) * bobHeight;
            Vector3 newPosition = basePosition + Vector3.up * yOffset;

            // Slight drift
            newPosition += Vector3.right * 0.1f * elapsed;

            // Move using physics-aware method if Rigidbody exists
            if (usePhysicsMovement)
            {
                // Use MovePosition for kinematic bodies - ensures physics detection
                rb.MovePosition(newPosition);
            }
            else
            {
                // Fallback for objects without Rigidbody
                floatingObject.transform.position = newPosition;
            }

            yield return new WaitForFixedUpdate(); // Use FixedUpdate for physics
        }

        // Despawn after timeout
        // Remove from coroutine tracking since we're completing naturally
        if (activeFloaters.ContainsKey(floatingObject))
        {
            activeFloaters.Remove(floatingObject);
        }

        if (isPackage)
        {
            ReturnPackageToPool(floatingObject);
        }
        else
        {
            ReturnCrateToPool(floatingObject);
        }
    }

    private void SpawnBarge()
    {
        if (bargeConfig?.vehiclePrefab == null) return;

        bool leftToRight = Random.value > 0.5f;

        // Get spawn positions at water level
        Vector3 leftEdge = GetWorldEdgePosition(0f, waterSurfaceY);
        Vector3 rightEdge = GetWorldEdgePosition(1f, waterSurfaceY);

        Vector3 spawnPos = leftToRight ? leftEdge : rightEdge;
        float targetX = leftToRight ? rightEdge.x : leftEdge.x;

        // Spawn barge
        activeBarge = Instantiate(bargeConfig.vehiclePrefab, spawnPos, Quaternion.identity);

        // Start barge movement
        StartCoroutine(MoveBarge(activeBarge, targetX));

        // Fire event
        OnResupplySpawned?.Invoke("Corporate barge is 'helping'...");
    }

    private IEnumerator MoveBarge(GameObject barge, float targetX)
    {
        if (barge == null || bargeConfig == null) yield break;

        float startX = barge.transform.position.x;
        float duration = bargeConfig.vehicleSpeed;
        float elapsed = 0f;
        bool hasDropped = false;

        while (elapsed < duration && isActive)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Move barge
            if (barge != null)
            {
                float currentX = Mathf.Lerp(startX, targetX, t);
                barge.transform.position = new Vector3(currentX, waterSurfaceY, barge.transform.position.z);

                // Drop crate at midpoint
                if (!hasDropped && t >= 0.5f)
                {
                    DropCorporateCrate(barge.transform.position);
                    hasDropped = true;
                }
            }

            yield return null;
        }

        // Clean up barge
        if (barge != null)
        {
            Destroy(barge);
        }
        activeBarge = null;
    }

    private void DropCorporateCrate(Vector3 position)
    {
        GameObject crate = GetCrateFromPool();
        if (crate == null) return;

        // CRITICAL: Prepare pickup BEFORE activation to prevent OnEnable overrides
        PreparePickup(crate, position + Vector3.down * 0.5f);

        // Add to active crates list
        activeCrates.Add(crate);

        // NOW it's safe to activate
        crate.SetActive(true);

        // Debug: Check spawn state (uncomment if pickup issues return)
        // Rigidbody crateRb = crate.GetComponent<Rigidbody>();
        // if (crateRb != null)
        // {
        //     Debug.Log($"[ResupplyManager] Crate spawned - Layer: {crate.layer}, Sleeping: {crateRb.IsSleeping()}, Kinematic: {crateRb.isKinematic}");
        // }

        // Start floating and track coroutine
        Coroutine floater = StartCoroutine(FloatAndDespawn(crate, false)); // false = is crate
        activeFloaters[crate] = floater;
    }

    private void CheckRubberBandTrigger()
    {
        float currentTime = GetGameTime();
        if (currentTime - lastRubberBandTime < rubberBandCooldown) return;

        float blockPercentage = GetCurrentBlockPercentage();

        if (blockPercentage < lowPerformanceThreshold)
        {
            if (struggleStartTime < 0)
            {
                struggleStartTime = currentTime;
            }
            else if (currentTime - struggleStartTime >= lowPerformanceDuration)
            {
                // Trigger help
                SpawnAirDrop();
                lastRubberBandTime = currentTime;
                struggleStartTime = -1f;
                Debug.Log("Rubber-band assistance triggered!");
            }
        }
        else
        {
            struggleStartTime = -1f;
        }
    }

    private float GetCurrentBlockPercentage()
    {
        if (gameController?.oilLeakData != null)
        {
            int total = gameController.oilLeakData.particlesBlocked + gameController.oilLeakData.particlesEscaped;
            if (total > 0)
            {
                return (float)gameController.oilLeakData.particlesBlocked / total;
            }
        }
        return 0.5f;
    }

    // Pickup handling - now with 3D Collider
    public void OnCratePickup(GameObject crate, Collider boatCollider)
    {
        // Resolve to root GameObject in case we got a child collider
        GameObject rootCrate = crate.transform.root.gameObject;

        // Stop any active coroutine for this pickup
        if (activeFloaters.ContainsKey(rootCrate))
        {
            StopCoroutine(activeFloaters[rootCrate]);
            activeFloaters.Remove(rootCrate);
        }

        // Award loot based on current tier
        LootTable currentLoot = GetCurrentLootTable();
        if (currentLoot != null && inventoryController != null)
        {
            var lootItems = currentLoot.GenerateLoot();

            string summary = "";
            foreach (var (item, count) in lootItems)
            {
                inventoryController.AddItem(item, count);
                summary += $"+{count} {item.itemName}, ";
            }

            if (!string.IsNullOrEmpty(summary))
            {
                summary = summary.TrimEnd(',', ' ');
                OnResupplyPicked?.Invoke(summary);
            }
        }

        // Return to appropriate pool
        if (activePackages.Contains(rootCrate))
        {
            ReturnPackageToPool(rootCrate);
        }
        else if (activeCrates.Contains(rootCrate))
        {
            ReturnCrateToPool(rootCrate);
        }
        else
        {
            // Failsafe: Item not in any list (orphaned), still clean it up
            Debug.LogWarning($"[ResupplyManager] Orphaned pickup '{rootCrate.name}' - cleaning up anyway");
            rootCrate.SetActive(false);
            // Try to return to appropriate pool based on name
            if (rootCrate.name.Contains("Package"))
            {
                ReturnPackageToPool(rootCrate);
            }
            else if (rootCrate.name.Contains("Crate"))
            {
                ReturnCrateToPool(rootCrate);
            }
        }
    }

    private LootTable GetCurrentLootTable()
    {
        // Use game timer, not Time.time
        float timeElapsed = GetGameTime();

        if (timeElapsed > 300 && absurdistLoot != null) return absurdistLoot; // 5+ minutes
        if (timeElapsed > 180 && desperationLoot != null) return desperationLoot; // 3+ minutes
        if (timeElapsed > 120 && corporateLoot != null) return corporateLoot; // 2+ minutes
        return grassrootsLoot; // Default
    }

    // Helper to get game time (not Time.time which ignores pause)
    private float GetGameTime()
    {
        // Use GameSession's elapsed time (properly tracked by GameCore)
        if (GameCore.Session != null && GameCore.Session.IsActive)
        {
            return GameCore.Session.TimeElapsed;
        }

        // Fallback to Time.time if session not available
        return Time.time;
    }

    // Helper to get world edge positions (works for ortho and perspective)
    private Vector3 GetWorldEdgePosition(float viewportX, float height)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        // Get world position at viewport edge
        Vector3 viewportPos = new Vector3(viewportX, 0.5f, cam.nearClipPlane + 10f);
        Vector3 worldPos = cam.ViewportToWorldPoint(viewportPos);
        worldPos.y = height;
        worldPos.z = 0;

        // Add buffer
        float buffer = 2f;
        worldPos.x += viewportX < 0.5f ? -buffer : buffer;

        return worldPos;
    }

    // Pool management - separate for packages and crates
    private GameObject GetPackageFromPool()
    {
        if (packagePool.Count > 0)
        {
            return packagePool.Dequeue();
        }
        else if (airDropConfig?.packagePrefab != null)
        {
            return Instantiate(airDropConfig.packagePrefab);
        }
        return null;
    }

    private GameObject GetCrateFromPool()
    {
        if (cratePool.Count > 0)
        {
            return cratePool.Dequeue();
        }
        else if (bargeConfig?.packagePrefab != null)
        {
            return Instantiate(bargeConfig.packagePrefab);
        }
        return null;
    }

    private void ReturnPackageToPool(GameObject package)
    {
        // Prevent double-enqueue
        if (!package.activeSelf && packagePool.Contains(package))
        {
            Debug.LogWarning($"[ResupplyManager] Attempted to double-enqueue package '{package.name}'" );
            return;
        }

        package.SetActive(false);
        activePackages.Remove(package);

        // Clean up coroutine tracking
        if (activeFloaters.ContainsKey(package))
        {
            activeFloaters.Remove(package);
        }

        // Reset state for clean reuse
        // Layer will be set again by PreparePickup on next use
        package.transform.rotation = Quaternion.identity;

        packagePool.Enqueue(package);
    }

    private void ReturnCrateToPool(GameObject crate)
    {
        // Prevent double-enqueue
        if (!crate.activeSelf && cratePool.Contains(crate))
        {
            Debug.LogWarning($"[ResupplyManager] Attempted to double-enqueue crate '{crate.name}'" );
            return;
        }

        crate.SetActive(false);
        activeCrates.Remove(crate);

        // Clean up coroutine tracking
        if (activeFloaters.ContainsKey(crate))
        {
            activeFloaters.Remove(crate);
        }

        // Reset state for clean reuse
        // Layer will be set again by PreparePickup on next use
        crate.transform.rotation = Quaternion.identity;

        cratePool.Enqueue(crate);
    }

    /// <summary>
    /// Prepares a pickup object with consistent state BEFORE activation
    /// Must be called while object is inactive to prevent OnEnable conflicts
    /// </summary>
    private void PreparePickup(GameObject pickup, Vector3 position)
    {
        if (pickup == null) return;

        // CRITICAL: Set all state while object is INACTIVE
        // This prevents any OnEnable/Start methods from overriding our settings

        // Set position
        pickup.transform.position = position;

        // Reset rotation (prevent weird tilted pickups)
        pickup.transform.rotation = Quaternion.identity;

        // Set layer recursively for all children
        int pickupLayer = LayerMask.NameToLayer("Pickups");
        SetLayerRecursive(pickup, pickupLayer);

        // Ensure collider is a trigger
        Collider col = pickup.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Reset rigidbody if present
        Rigidbody rb = pickup.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // CRITICAL: Wake up the rigidbody so it re-enters physics solver
            // This ensures trigger detection works immediately
            rb.WakeUp();

            // For dynamic bodies, set sleep threshold to 0 to prevent sleeping
            // This keeps them active for trigger detection
            if (!rb.isKinematic)
            {
                rb.sleepThreshold = 0f;
            }
        }
    }

    /// <summary>
    /// Recursively set layer for GameObject and all children
    /// Fixes issue where child colliders don't get detected
    /// </summary>
    private void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj == null) return;

        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    // Lifecycle management - called by EndlessMode/LeakManager
    public void PauseResupply()
    {
        isActive = false;
    }

    public void ResumeResupply()
    {
        isActive = true;
    }

    public void EndResupply()
    {
        isActive = false;

        // Clean up all active objects
        if (activeAircraft != null)
        {
            Destroy(activeAircraft);
            activeAircraft = null; // Clear reference immediately for IsClean check
        }

        if (activeBarge != null)
        {
            Destroy(activeBarge);
            activeBarge = null; // Clear reference immediately for IsClean check
        }

        // Return all packages to pool
        foreach (var package in activePackages.ToArray())
        {
            if (package != null) ReturnPackageToPool(package);
        }

        // Return all crates to pool
        foreach (var crate in activeCrates.ToArray())
        {
            if (crate != null) ReturnCrateToPool(crate);
        }

        activePackages.Clear();
        activeCrates.Clear();
    }

    // === IResettable Implementation ===

    /// <summary>
    /// Reset to initial state for game restart
    /// Reuses existing EndResupply logic
    /// </summary>
    public void Reset()
    {
        // Stop all coroutines first
        StopAllCoroutines();

        // Use existing cleanup logic
        EndResupply();

        // Reset timing state
        nextAirDropTime = 0f;
        nextBargeTime = 0f;
        lastRubberBandTime = 0f;
        struggleStartTime = -1f;

        // Ensure state is clean
        isActive = false;
    }

    /// <summary>
    /// Verify the manager is properly cleaned
    /// </summary>
    public bool IsClean
    {
        get
        {
            // Check no active vehicles
            bool noVehicles = activeAircraft == null && activeBarge == null;

            // Check no active packages or crates
            bool noActiveItems = (activePackages == null || activePackages.Count == 0) &&
                                  (activeCrates == null || activeCrates.Count == 0);

            // Check not active
            bool notActive = !isActive;

            // Check pools are consistent (all pooled items should be inactive)
            bool poolsClean = true;
            foreach (var package in packagePool)
            {
                if (package != null && package.activeSelf)
                {
                    poolsClean = false;
                    Debug.LogError($"[ResupplyManager] Active package found in pool during IsClean check");
                    break;
                }
            }

            if (poolsClean)
            {
                foreach (var crate in cratePool)
                {
                    if (crate != null && crate.activeSelf)
                    {
                        poolsClean = false;
                        Debug.LogError($"[ResupplyManager] Active crate found in pool during IsClean check");
                        break;
                    }
                }
            }

            return noVehicles && noActiveItems && notActive && poolsClean;
        }
    }

    /// <summary>
    /// Check if any major event is active (for telemetry)
    /// </summary>
    public bool IsMajorEventActive => activeAircraft != null || activeBarge != null;

    /// <summary>
    /// Get total count of active packages and crates (for service layer)
    /// </summary>
    public int ActivePackageCount => activePackages.Count + activeCrates.Count;

    /// <summary>
    /// Check if resupply system is active
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Get time until next air drop (for DevHUD)
    /// </summary>
    public float GetTimeToNextAirDrop()
    {
        if (!isActive) return -1;
        float timeLeft = nextAirDropTime - GetGameTime();
        return timeLeft > 0 ? timeLeft : -1;
    }

    /// <summary>
    /// Get time until next barge (for DevHUD)
    /// </summary>
    public float GetTimeToNextBarge()
    {
        if (!isActive) return -1;
        float timeLeft = nextBargeTime - GetGameTime();
        return timeLeft > 0 ? timeLeft : -1;
    }
}