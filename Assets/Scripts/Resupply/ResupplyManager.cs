using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages all resupply events (air-drops, barges, etc)
/// Driven by LeakManager/EndlessMode lifecycle - not self-starting
/// </summary>
public class ResupplyManager : MonoBehaviour
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

        package.transform.position = dropPosition;
        package.SetActive(true);

        // Set layer for pickup detection
        package.layer = LayerMask.NameToLayer("Pickups");

        // Add to active list
        activePackages.Add(package);

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

        // Start floating behavior
        StartCoroutine(FloatAndDespawn(package, true)); // true = is package
    }

    private IEnumerator FloatAndDespawn(GameObject floatingObject, bool isPackage)
    {
        float floatDuration = isPackage && airDropConfig != null ? airDropConfig.despawnTime :
                              bargeConfig != null ? bargeConfig.despawnTime : 30f;

        float elapsed = 0f;
        float bobSpeed = 2f;
        float bobHeight = 0.2f;
        Vector3 basePosition = floatingObject.transform.position;

        while (elapsed < floatDuration && floatingObject.activeSelf && isActive)
        {
            elapsed += Time.deltaTime;

            // Bob up and down
            float yOffset = Mathf.Sin(elapsed * bobSpeed) * bobHeight;
            floatingObject.transform.position = basePosition + Vector3.up * yOffset;

            // Slight drift
            floatingObject.transform.position += Vector3.right * 0.1f * Time.deltaTime;

            yield return null;
        }

        // Despawn after timeout
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

        crate.transform.position = position + Vector3.down * 0.5f;
        crate.SetActive(true);

        // Set layer for pickup
        crate.layer = LayerMask.NameToLayer("Pickups");

        // Add to active crates list
        activeCrates.Add(crate);

        // Start floating
        StartCoroutine(FloatAndDespawn(crate, false)); // false = is crate
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
        if (activePackages.Contains(crate))
        {
            ReturnPackageToPool(crate);
        }
        else if (activeCrates.Contains(crate))
        {
            ReturnCrateToPool(crate);
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
        // Use GameState timer (updated by EndlessMode)
        if (gameController?.gameState != null)
        {
            return gameController.gameState.timer;
        }

        return Time.time; // Fallback
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
        package.SetActive(false);
        activePackages.Remove(package);
        packagePool.Enqueue(package);
    }

    private void ReturnCrateToPool(GameObject crate)
    {
        crate.SetActive(false);
        activeCrates.Remove(crate);
        cratePool.Enqueue(crate);
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
        if (activeAircraft != null) Destroy(activeAircraft);
        if (activeBarge != null) Destroy(activeBarge);

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
}