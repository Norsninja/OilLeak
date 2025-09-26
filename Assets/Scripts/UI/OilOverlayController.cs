using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Core;

namespace OilLeak.UI
{
    /// <summary>
    /// Manages oil splat overlay that progressively covers the screen as particles escape.
    /// Uses pooled Image sprites with a 4-tier placement system for visual contamination.
    /// </summary>
    public class OilOverlayController : MonoBehaviour, IResettable
    {
        [Header("Canvas References")]
        [SerializeField] private Canvas oilOverlayCanvas;
        [SerializeField] private Transform splatContainer;
        [SerializeField] private Sprite[] splatSprites; // 2 splat variants from Assets/Images/splats

        [Header("Pool Configuration")]
        [SerializeField] private int maxSplats = 50;
        [SerializeField] private GameObject splatPrefab; // Image prefab for splats

        [Header("Tier Configuration")]
        [SerializeField] private float centerClearRadius = 200f; // Boat visibility zone
        [SerializeField] private float splatHalfSize = 250f; // Half of 500x500 splat
        [SerializeField] private int[] tierCapacities = new int[] { 20, 20, 30, 30 }; // Matches integrity acts: 80/60/30

        [Header("Splat Variation")]
        [SerializeField] private float scaleMin = 0.8f;
        [SerializeField] private float scaleMax = 1.2f;
        [SerializeField] private float angleJitter = 10f; // Degrees of randomness in placement angle

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Pool management
        private Queue<Image> splatPool = new Queue<Image>();
        private List<ActiveSplat> activeSplats = new List<ActiveSplat>();

        // Tier tracking
        private int placedSplatCount = 0;

        // Debug values exposed to Inspector
        [Header("Debug Values (Read Only)")]
        [SerializeField] private int lastEscapedCount;
        [SerializeField] private float currentCoverage;
        [SerializeField] private int targetSplatCount;

        // Current state
        private bool isActive;
        private bool subscribed;

        // Simplified struct - just track what's placed
        private struct ActiveSplat
        {
            public Image image;
            public Vector2 position;
            public int ringIndex;
        }

        #region Unity Lifecycle

        void Awake()
        {
            Debug.Log($"[OilOverlay] Awake (active:{gameObject.activeInHierarchy}, enabled:{enabled})");

            // Ensure canvas sorting order
            if (oilOverlayCanvas != null)
            {
                oilOverlayCanvas.sortingOrder = 15; // Above all game UI
            }

            InitializePool();
        }

        void Start()
        {
            Debug.Log($"[OilOverlay] Start (active:{gameObject.activeInHierarchy}, enabled:{enabled})");

            // Register for reset
            ResetRegistry.Register(this);

            if (debugLogging)
                Debug.Log("[OilOverlay] Initialized with pool size: " + maxSplats);

            // Start initialization coroutine
            StartCoroutine(InitializeWhenReady());
        }

        void OnEnable()
        {
            Debug.Log($"[OilOverlay] OnEnable (active:{gameObject.activeInHierarchy}, enabled:{enabled})");
        }

        void OnDisable()
        {
            Debug.Log($"[OilOverlay] OnDisable (active:{gameObject.activeInHierarchy}, enabled:{enabled})");
        }

        void OnDestroy()
        {
            Debug.Log("[OilOverlay] OnDestroy");

            // Unsubscribe from events
            if (subscribed && GameCore.Session != null)
            {
                GameCore.Session.OnGallonsEscapedChanged -= OnEscapedChanged;
                subscribed = false;
            }

            // Stop update loop
            StopAllCoroutines();
        }

        private IEnumerator InitializeWhenReady()
        {
            Debug.Log("[OilOverlay] Waiting for GameCore.Session...");

            // Wait for GameCore.Session to exist
            while (GameCore.Session == null)
            {
                yield return null;
            }

            Debug.Log("[OilOverlay] GameCore.Session found, subscribing to events");

            // Force canvas layout update to ensure dimensions are ready
            yield return null; // Wait one frame for UI to initialize
            Canvas.ForceUpdateCanvases();

            // Log canvas diagnostics (one-time during init)
            LogCanvasDiagnostics();

            // Ensure pool size matches max escaped
            int maxEscaped = GameCore.Session.GetMaxEscapedForDisplay();
            EnsurePoolSize(maxEscaped);

            // Subscribe to events
            GameCore.Session.OnGallonsEscapedChanged += OnEscapedChanged;
            subscribed = true;

            // MUST set isActive BEFORE starting coroutine that checks it!
            isActive = true;

            // Start update loop
            StartCoroutine(UpdateLoop());

            Debug.Log($"[OilOverlay] Initialized - Pool size: {splatPool.Count + activeSplats.Count}, Max escaped: {maxEscaped}");
        }

        private void LogCanvasDiagnostics()
        {
            // Log render mode
            string renderMode = oilOverlayCanvas != null ? oilOverlayCanvas.renderMode.ToString() : "null canvas";

            // Log pixel rect
            Rect pixelRect = oilOverlayCanvas != null ? oilOverlayCanvas.pixelRect : Rect.zero;

            // Log container rect
            RectTransform containerRT = splatContainer as RectTransform;
            Rect containerRect = containerRT != null ? containerRT.rect : Rect.zero;

            Debug.Log($"[OilOverlay] Canvas Diagnostics:");
            Debug.Log($"  - Render Mode: {renderMode}");
            Debug.Log($"  - Canvas Pixel Rect: {pixelRect} (w:{pixelRect.width}, h:{pixelRect.height})");
            Debug.Log($"  - Container Rect: {containerRect} (w:{containerRect.width}, h:{containerRect.height})");
            Debug.Log($"  - Container Anchors: min({containerRT?.anchorMin}), max({containerRT?.anchorMax})");
            Debug.Log($"  - Container Pivot: {containerRT?.pivot}");
        }

        #endregion

        #region Pool Management

        private void InitializePool()
        {
            // Pre-warm splat pool with initial size from inspector
            for (int i = 0; i < maxSplats; i++)
            {
                CreatePooledSplat();
            }

            if (debugLogging)
                Debug.Log($"[OilOverlay] Pool initialized with {splatPool.Count} splats");
        }

        private void EnsurePoolSize(int requiredSize)
        {
            int currentPoolSize = splatPool.Count + activeSplats.Count;

            if (currentPoolSize < requiredSize)
            {
                int toAdd = requiredSize - currentPoolSize + 10; // Add buffer

                for (int i = 0; i < toAdd; i++)
                {
                    CreatePooledSplat();
                }

                if (debugLogging)
                    Debug.Log($"[OilOverlay] Pool topped up: added {toAdd} splats (total: {splatPool.Count + activeSplats.Count})");
            }
        }

        private void CreatePooledSplat()
        {
            GameObject splatGO;

            if (splatPrefab != null)
            {
                splatGO = Instantiate(splatPrefab, splatContainer);
            }
            else
            {
                // Create default if no prefab assigned
                splatGO = new GameObject($"Splat_{splatPool.Count}");
                splatGO.transform.SetParent(splatContainer, false);
                var image = splatGO.AddComponent<Image>();

                // Assign random splat sprite if available
                if (splatSprites != null && splatSprites.Length > 0)
                {
                    image.sprite = splatSprites[Random.Range(0, splatSprites.Length)];
                }

                // Configure for UI overlay
                image.raycastTarget = false;
                image.color = new Color(0.1f, 0.05f, 0.02f, 0.7f); // Dark oil color
            }

            var splatImage = splatGO.GetComponent<Image>();
            splatGO.SetActive(false);
            splatPool.Enqueue(splatImage);
        }

        private Image GetPooledSplat()
        {
            if (splatPool.Count > 0)
            {
                var splat = splatPool.Dequeue();
                splat.gameObject.SetActive(true);
                return splat;
            }

            Debug.LogWarning("[OilOverlay] Pool exhausted!");
            return null;
        }

        private void ReturnToPool(Image splat)
        {
            if (splat != null)
            {
                splat.gameObject.SetActive(false);
                splatPool.Enqueue(splat);
            }
        }

        #endregion

        #region Update Logic

        private IEnumerator UpdateLoop()
        {
            // Simple polling loop - check for particles escaped changes
            while (isActive)
            {
                CheckAndPlaceSplats();
                yield return new WaitForSeconds(0.1f); // Check 10 times per second
            }
        }

        private void OnEscapedChanged(int gallonsValue)
        {
            // Event fired - immediately check for new splats to place
            CheckAndPlaceSplats();
        }

        private void CheckAndPlaceSplats()
        {
            if (GameCore.Session == null) return;

            // Get current particle count
            int escapedParticles = GameCore.Session.ParticlesEscaped;
            int maxEscaped = GameCore.Session.GetMaxEscapedForDisplay();
            escapedParticles = Mathf.Clamp(escapedParticles, 0, maxEscaped);

            // Place splats for any new particles
            int toPlace = escapedParticles - placedSplatCount;
            if (toPlace > 0)
            {
                if (debugLogging)
                    Debug.Log($"[OilOverlay] Placing {toPlace} new splats (total: {escapedParticles})");

                for (int i = 0; i < toPlace; i++)
                {
                    PlaceStaticSplat();
                }

                // Update coverage tracking
                currentCoverage = maxEscaped > 0 ? (float)escapedParticles / maxEscaped : 0f;
            }
        }

        #endregion

        #region Splat Management

        private void PlaceStaticSplat()
        {
            var image = GetPooledSplat();
            if (image == null) return;

            // Determine tier based on current count (deterministic)
            int tier = GetTierForIndex(placedSplatCount);
            Vector2 position;

            // Calculate position based on tier
            if (tier == 1)
            {
                // Tier 1: Rectangle perimeter placement
                position = GetRectangleEdge(placedSplatCount);
            }
            else
            {
                // Tiers 2-4: Elliptical band placement
                int tierStartIndex = tier == 2 ? 20 : (tier == 3 ? 40 : 70);
                int tierCapacity = tier == 2 ? 20 : (tier == 3 ? 30 : 30);
                int indexInTier = placedSplatCount - tierStartIndex;

                var (normalizedRadius, angleDeg) = GetTierBand(tier, indexInTier, tierCapacity);
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Get container dimensions for ellipse
                RectTransform containerRT = splatContainer as RectTransform;
                float rx = containerRT.rect.width * 0.5f;
                float ry = containerRT.rect.height * 0.5f;

                // Calculate actual position on ellipse
                position = new Vector2(
                    rx * normalizedRadius * Mathf.Cos(angleRad),
                    ry * normalizedRadius * Mathf.Sin(angleRad)
                );
            }

            // Set splat position and appearance
            image.rectTransform.anchoredPosition = position;

            // Reduced scale variation for 500px splats (was 0.8-1.2, now 0.95-1.05)
            float scale = Random.Range(0.95f, 1.05f);
            image.rectTransform.localScale = Vector3.one * scale;

            // Random static rotation for variety
            float rotation = Random.Range(0f, 360f);
            image.rectTransform.rotation = Quaternion.Euler(0, 0, rotation);

            // Random sprite if available
            if (splatSprites != null && splatSprites.Length > 0)
            {
                image.sprite = splatSprites[Random.Range(0, splatSprites.Length)];
            }

            // Track the placed splat (simplified - no ring tracking needed)
            ActiveSplat splat = new ActiveSplat
            {
                image = image,
                position = position,
                ringIndex = tier - 1  // Store tier as "ring" for compatibility
            };
            activeSplats.Add(splat);

            // Update tracking - only need count now
            placedSplatCount++;

            if (debugLogging)
                Debug.Log($"[OilOverlay] Placed splat {placedSplatCount} in tier {tier} at {position}");
        }

        private int GetRingForSplat(int splatIndex)
        {
            // Determine which ring a splat belongs to based on cumulative capacities
            int cumulative = 0;
            for (int i = 0; i < tierCapacities.Length; i++)
            {
                cumulative += tierCapacities[i];
                if (splatIndex < cumulative)
                    return i;
            }
            return tierCapacities.Length - 1; // Last ring if somehow over capacity
        }

        // New helper methods for 4-tier system
        private int GetTierForIndex(int index)
        {
            // Deterministic tier assignment based on particle index
            // Tier 1: 1-20, Tier 2: 21-40, Tier 3: 41-70, Tier 4: 71-100
            if (index < 20) return 1;
            if (index < 40) return 2;
            if (index < 70) return 3;
            return 4;
        }

        private Vector2 GetRectangleEdge(int indexInTier)
        {
            // Place splats along rectangle perimeter (for Tier 1)
            // 5 per edge: top, right, bottom, left
            RectTransform containerRT = splatContainer as RectTransform;
            // Position splat centers exactly on screen edges (half on, half off)
            float halfWidth = containerRT.rect.width * 0.5f;   // Exact edge
            float halfHeight = containerRT.rect.height * 0.5f; // Exact edge

            int edge = indexInTier / 5;  // 0=top, 1=right, 2=bottom, 3=left
            int posOnEdge = indexInTier % 5;
            float t = (posOnEdge + 0.5f) / 5f;  // Position along edge (0.1, 0.3, 0.5, 0.7, 0.9)

            // Add jitter for variety
            float jitter = Random.Range(-50f, 50f);

            switch (edge % 4)  // Cycle through edges
            {
                case 0: // Top edge
                    return new Vector2(Mathf.Lerp(-halfWidth, halfWidth, t) + jitter, halfHeight);
                case 1: // Right edge
                    return new Vector2(halfWidth, Mathf.Lerp(halfHeight, -halfHeight, t) + jitter);
                case 2: // Bottom edge
                    return new Vector2(Mathf.Lerp(halfWidth, -halfWidth, t) + jitter, -halfHeight);
                case 3: // Left edge
                    return new Vector2(-halfWidth, Mathf.Lerp(-halfHeight, halfHeight, t) + jitter);
                default:
                    return Vector2.zero;
            }
        }

        private (float radius, float angle) GetTierBand(int tier, int indexInTier, int totalInTier)
        {
            // Get radius and angle for elliptical placement (Tiers 2-4)
            RectTransform containerRT = splatContainer as RectTransform;
            // Use full container dimensions for ellipse (splats can extend beyond edges)
            float rx = containerRT.rect.width * 0.5f;   // Full width
            float ry = containerRT.rect.height * 0.5f;  // Full height

            // Radius bands for each tier (normalized 0-1)
            float rMin = 0f, rMax = 0f;
            float angleOffset = 0f;

            switch (tier)
            {
                case 2: // Outer band
                    rMin = 0.75f; rMax = 0.90f;
                    angleOffset = 0f;
                    break;
                case 3: // Middle band
                    rMin = 0.60f; rMax = 0.74f;
                    angleOffset = 22.5f;  // Offset to avoid radial lines
                    break;
                case 4: // Inner band
                    rMin = 0.45f; rMax = 0.59f;
                    angleOffset = 45f;    // Further offset
                    break;
            }

            // Use golden angle for even distribution within tier
            float goldenAngle = 137.5f;
            float baseAngle = (indexInTier * goldenAngle) + angleOffset;
            float angle = (baseAngle + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;

            // Random radius within band
            float normalizedRadius = Random.Range(rMin, rMax);

            // Return normalized radius and angle (position calculated in caller)
            return (normalizedRadius, angle * Mathf.Rad2Deg);
        }

        #endregion

        #region IResettable

        public void Reset()
        {
            if (debugLogging)
                Debug.Log("[OilOverlay] Resetting");

            // Return all active splats to pool
            foreach (var splat in activeSplats)
            {
                ReturnToPool(splat.image);
            }
            activeSplats.Clear();

            // Reset state
            currentCoverage = 0;
            targetSplatCount = 0;
            lastEscapedCount = 0;
            placedSplatCount = 0;
        }

        public bool IsClean =>
            activeSplats.Count == 0 &&
            currentCoverage == 0 &&
            placedSplatCount == 0;

        public int GetResetPriority() => 10; // UI resets after gameplay systems

        #endregion

        #region Debug

        // Diagnostic method to check actual screen vs container setup
        [ContextMenu("Log Screen vs Container Info")]
        private void LogScreenVsContainer()
        {
            RectTransform containerRT = splatContainer as RectTransform;

            Debug.Log("[OilOverlay] === Screen vs Container Diagnostics ===");
            Debug.Log($"  Unity Screen.width x height: {Screen.width} x {Screen.height}");
            Debug.Log($"  Canvas pixelRect: {oilOverlayCanvas.pixelRect}");
            Debug.Log($"  Container rect size: {containerRT?.rect.width} x {containerRT?.rect.height}");
            Debug.Log($"  Container anchors: Min({containerRT?.anchorMin}) Max({containerRT?.anchorMax})");
            Debug.Log($"  Container pivot: {containerRT?.pivot}");
            Debug.Log($"  Container anchoredPosition: {containerRT?.anchoredPosition}");
            Debug.Log($"  Canvas render mode: {oilOverlayCanvas.renderMode}");

            // Check if container is properly stretched
            bool isStretched = containerRT != null &&
                               containerRT.anchorMin == Vector2.zero &&
                               containerRT.anchorMax == Vector2.one;
            Debug.Log($"  Container stretched to full canvas: {isStretched}");
        }

        // Test method to spawn splats at known positions for verification
        [ContextMenu("Test Debug Markers")]
        private void TestDebugMarkers()
        {
            if (!isActive)
            {
                Debug.LogWarning("[OilOverlay] Cannot test - controller not active");
                return;
            }

            RectTransform containerRT = splatContainer as RectTransform;
            float w = containerRT.rect.width;
            float h = containerRT.rect.height;

            Debug.Log($"[OilOverlay] Spawning debug markers in {w}x{h} container");

            // Clear existing splats first
            foreach (var splat in activeSplats)
            {
                ReturnToPool(splat.image);
            }
            activeSplats.Clear();

            // Test positions: center, near edges
            Vector2[] testPositions = new Vector2[]
            {
                new Vector2(0, 0),                    // Center
                new Vector2(w * 0.5f - 60, 0),       // Near right
                new Vector2(-w * 0.5f + 60, 0),      // Near left
                new Vector2(0, h * 0.5f - 60),       // Near top
                new Vector2(0, -h * 0.5f + 60)       // Near bottom
            };

            foreach (var pos in testPositions)
            {
                var image = GetPooledSplat();
                if (image != null)
                {
                    image.rectTransform.anchoredPosition = pos;
                    image.rectTransform.localScale = Vector3.one;

                    // Create static splat
                    ActiveSplat splat = new ActiveSplat
                    {
                        image = image,
                        position = pos,
                        ringIndex = -1 // Debug marker, not part of ring system
                    };

                    activeSplats.Add(splat);
                    Debug.Log($"  Marker at: {pos}");
                }
            }

            Debug.Log($"[OilOverlay] Spawned {activeSplats.Count} debug markers");
        }

        // Test method to spawn ring-based splats
        [ContextMenu("Test Ring Placement (10 splats)")]
        private void TestRingPlacement()
        {
            if (!isActive)
            {
                Debug.LogWarning("[OilOverlay] Cannot test spawn - controller not active");
                return;
            }

            RectTransform containerRT = splatContainer as RectTransform;
            Debug.Log($"[OilOverlay] Testing ring placement in {containerRT.rect.width}x{containerRT.rect.height} container");

            // Temporarily set debug logging on
            bool originalDebug = debugLogging;
            debugLogging = true;

            // Place 10 splats using ring system
            for (int i = 0; i < 10; i++)
            {
                PlaceStaticSplat();
            }

            debugLogging = originalDebug;

            // Calculate which tier we ended in
            int finalTier = GetTierForIndex(placedSplatCount - 1);
            Debug.Log($"[OilOverlay] Test complete - placed {placedSplatCount} splats, ending in tier {finalTier}");
        }

        void OnGUI()
        {
            if (debugLogging && Application.isEditor)
            {
                GUI.Label(new Rect(10, 200, 300, 20), $"Oil Coverage: {currentCoverage:F2}");
                GUI.Label(new Rect(10, 220, 300, 20), $"Active Splats: {activeSplats.Count}/{targetSplatCount}");
                GUI.Label(new Rect(10, 240, 300, 20), $"Pool Available: {splatPool.Count}");
            }
        }

        #endregion
    }
}