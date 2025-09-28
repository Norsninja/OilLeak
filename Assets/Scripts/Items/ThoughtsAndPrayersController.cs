using UnityEngine;
using System.Collections;
using OilLeak.Toast.Services;
using Core; // For ResetRegistry and IResettable

public class ThoughtsAndPrayersController : MonoBehaviour, IResettable
{
    // Cooldown management
    private static float lastActivationTime = -60f; // Static to persist across instances
    private const float COOLDOWN_DURATION = 60f; // One minute cooldown

    // Configuration
    [Header("Timing")]
    [SerializeField] private float invocationDuration = 2f;
    [SerializeField] private float overlayDuration = 2f;
    [SerializeField] private float cascadeDuration = 3f;
    [SerializeField] private float anticlimaxDuration = 3f;
    [SerializeField] private float messageInterval = 0.3f; // Time between toast messages

    [Header("Visuals")]
    [SerializeField] private AnimationCurve glowIntensity = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.7f, 0.8f);
    [SerializeField] private float riseHeight = 2f;

    // State
    private ActivationPhase currentPhase = ActivationPhase.Idle;
    private Vector3 originalPosition;
    private MaterialPropertyBlock propertyBlock;
    private Renderer objectRenderer;
    private bool isActivated = false;
    private int prayerCount = 0;
    private IToastService toastService;
    private Coroutine activationCoroutine;

    // UI Controller reference
    private UIController uiController;

    // Physics control references
    private ItemController itemController;
    private Rigidbody rb;
    private float hoverHeight;
    private bool isHovering = false;

    private enum ActivationPhase
    {
        Idle,
        Invocation,
        Overlay,
        Cascade,
        Anticlimax,
        Complete
    }

    private void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            objectRenderer = GetComponentInChildren<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
        originalPosition = transform.position;

        // Get required components for physics control
        itemController = GetComponent<ItemController>();
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[ThoughtsAndPrayers] Rigidbody required!");
        }

        // Get toast service from GameCore
        if (GameCore.Toasts != null)
        {
            toastService = GameCore.Toasts;
        }

        // Find the UIController
        uiController = FindObjectOfType<UIController>();
        if (uiController == null)
        {
            Debug.LogWarning("[ThoughtsAndPrayers] UIController not found! UI effects will not display.");
        }

        // Start on PorousDebris layer (11) so oil passes through
        gameObject.layer = 11;

        // Ensure we have no collider with oil particles
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true; // Make it a trigger so it doesn't physically block
        }

        // Register for global resets to clear static cooldown/state
        ResetRegistry.Register(this);
    }

    private void OnEnable()
    {
        // Start activation when item is thrown/placed
        StartCoroutine(WaitAndActivate());
    }

    private IEnumerator WaitAndActivate()
    {
        // Wait a moment for item to settle
        yield return new WaitForSeconds(0.5f);

        // Let physics stabilize for one more frame
        yield return new WaitForFixedUpdate();

        if (!isActivated)
        {
            Activate();
        }
    }

    public void Activate()
    {
        if (isActivated) return;

        // Check cooldown
        float timeSinceLastUse = Time.time - lastActivationTime;
        if (timeSinceLastUse < COOLDOWN_DURATION)
        {
            float remaining = COOLDOWN_DURATION - timeSinceLastUse;
            Debug.Log($"[ThoughtsAndPrayers] Cooldown active. {remaining:F1} seconds remaining.");
            return; // Fail immediately as per Senior Dev's requirement
        }

        isActivated = true;
        lastActivationTime = Time.time; // Record activation time
        Debug.Log("[ThoughtsAndPrayers] Activating divine intervention...");

        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
        }

        activationCoroutine = StartCoroutine(ActivationSequence());
    }

    private IEnumerator ActivationSequence()
    {
        // Phase 1: Invocation
        currentPhase = ActivationPhase.Invocation;
        Debug.Log("[ThoughtsAndPrayers] Phase 1: Invocation");
        yield return StartCoroutine(InvocationPhase());

        // Start hovering coroutine that runs in parallel
        Coroutine hoverCoroutine = StartCoroutine(MaintainHover(
            overlayDuration + cascadeDuration + anticlimaxDuration
        ));

        // Phase 2: Overlay
        currentPhase = ActivationPhase.Overlay;
        Debug.Log("[ThoughtsAndPrayers] Phase 2: Divine Overlay");
        yield return StartCoroutine(OverlayPhase());

        // Phase 3: Cascade
        currentPhase = ActivationPhase.Cascade;
        Debug.Log("[ThoughtsAndPrayers] Phase 3: Prayer Cascade");
        yield return StartCoroutine(CascadePhase());

        // Phase 4: Anticlimax
        currentPhase = ActivationPhase.Anticlimax;
        Debug.Log("[ThoughtsAndPrayers] Phase 4: Anticlimax");
        yield return StartCoroutine(AnticlimaxPhase());

        // Stop hovering and release physics
        isHovering = false;
        if (hoverCoroutine != null)
        {
            StopCoroutine(hoverCoroutine);
        }
        ReleasePhysics();

        // Complete
        currentPhase = ActivationPhase.Complete;
        Debug.Log("[ThoughtsAndPrayers] Complete - Released to fall");

        // Wait for fall then fade out
        yield return new WaitForSeconds(2f); // Let it fall
        yield return StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator InvocationPhase()
    {
        Debug.Log($"[ThoughtsAndPrayers] Starting invocation at position {transform.position}, velocity: {rb?.velocity}");

        // Disable ItemController to prevent physics conflicts
        if (itemController != null)
        {
            itemController.enabled = false;
        }

        // Clear velocities BEFORE switching to kinematic mode
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Wait one physics frame for velocity clear to take effect
            yield return new WaitForFixedUpdate();
            rb.isKinematic = true;
        }

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        hoverHeight = startPos.y + riseHeight;
        Vector3 targetPos = new Vector3(startPos.x, hoverHeight, startPos.z);

        // Send initial "false hope" messages
        SendToastMessage("tp_hope_1");
        yield return new WaitForSeconds(messageInterval);
        SendToastMessage("tp_hope_2");
        yield return new WaitForSeconds(messageInterval);
        SendToastMessage("tp_hope_3");
        yield return new WaitForSeconds(messageInterval);

        // Smooth rise using MovePosition
        while (elapsed < invocationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / invocationDuration;

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            if (rb != null)
            {
                rb.MovePosition(newPos);
            }

            // Apply glow effect
            if (objectRenderer != null)
            {
                float intensity = glowIntensity.Evaluate(t);
                Color emissionColor = glowColor * intensity;
                propertyBlock.SetColor("_EmissionColor", emissionColor);
                objectRenderer.SetPropertyBlock(propertyBlock);
            }

            yield return new WaitForFixedUpdate();
        }

        isHovering = true;
    }

    private IEnumerator MaintainHover(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && isHovering)
        {
            // Optional: subtle bobbing motion
            float bobOffset = Mathf.Sin(Time.time * 2f) * 0.05f;
            Vector3 hoverPos = new Vector3(
                transform.position.x,
                hoverHeight + bobOffset,
                transform.position.z
            );

            if (rb != null)
            {
                rb.MovePosition(hoverPos);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    private void ReleasePhysics()
    {
        Debug.Log("[ThoughtsAndPrayers] Releasing physics control");

        // Return to physics mode
        if (rb != null)
        {
            rb.isKinematic = false;
            // Small downward impulse to start the fall
            rb.velocity = Vector3.down * 2f;
        }

        // Re-enable ItemController for buoyancy
        if (itemController != null)
        {
            itemController.enabled = true;
        }
    }

    private IEnumerator OverlayPhase()
    {
        // Show overlay using UIController
        if (uiController != null)
        {
            uiController.ShowThoughtsAndPrayersUI();
            uiController.AnimateThoughtsAndPrayersText();
        }

        yield return new WaitForSeconds(overlayDuration);
    }

    private IEnumerator CascadePhase()
    {
        // Send escalating messages
        for (int i = 1; i <= 6; i++)
        {
            SendToastMessage($"tp_cascade_{i}");

            // Update prayer counter
            prayerCount += Random.Range(10, 100);
            UpdatePrayerCounter();

            yield return new WaitForSeconds(messageInterval);
        }

        // Final burst of prayers
        prayerCount += 1000;
        UpdatePrayerCounter();
    }

    private IEnumerator AnticlimaxPhase()
    {
        // Big flash moment (rays are already rotating)
        // Could add a scale pulse or brightness boost here if needed

        yield return new WaitForSeconds(0.5f);

        // Send anticlimax message
        SendToastMessage("tp_anticlimax");

        // Show final prayer count with oil blocked: 0
        if (uiController != null)
        {
            uiController.UpdatePrayerCounter(prayerCount, 0);
        }

        yield return new WaitForSeconds(anticlimaxDuration);

        // Hide UI after anticlimax
        if (uiController != null)
        {
            uiController.HideThoughtsAndPrayersUI();
        }
    }

    // Removed AnimateTextDrop and FadeOutOverlay - UIController handles UI now

    private IEnumerator FadeOutAndDestroy()
    {
        float fadeTime = 2f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            // Fade out glow
            if (objectRenderer != null)
            {
                Color emissionColor = glowColor * (1 - t);
                propertyBlock.SetColor("_EmissionColor", emissionColor);
                propertyBlock.SetFloat("_Alpha", 1 - t);
                objectRenderer.SetPropertyBlock(propertyBlock);
            }

            yield return null;
        }

        // Return to pool or destroy
        if (GameCore.Items != null)
        {
            GameCore.Items.ReturnToPool(gameObject);
        }
        else
        {
            // Fallback to direct pooler access
            var pooler = FindObjectOfType<ItemPooler>();
            if (pooler != null)
            {
                pooler.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void SendToastMessage(string triggerId)
    {
        Debug.Log($"[ThoughtsAndPrayers] Triggering toast: {triggerId}");

        // Trigger the specific toast - ToastManager will select act-appropriate content
        if (toastService != null && toastService.IsReady)
        {
            // ForceToast will automatically detect current act and select appropriate message
            toastService.ForceToast(triggerId, null, null);
        }
    }

    private void UpdatePrayerCounter()
    {
        if (uiController != null)
        {
            uiController.UpdatePrayerCounter(prayerCount, 0);
        }
    }

    private void OnDisable()
    {
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }

        // Ensure physics is released if interrupted
        if (rb != null && rb.isKinematic)
        {
            ReleasePhysics();
        }

        // Ensure overlay UI is hidden if we were interrupted mid-sequence
        if (uiController != null)
        {
            uiController.HideThoughtsAndPrayersUI();
        }

        // Make sure hover loop exits
        isHovering = false;
    }

    // NO OnParticleCollision - oil passes through completely
    // This is the key to the satire - it does nothing
    // IResettable implementation to ensure clean cooldown/state between runs
    public void Reset()
    {
        // Clear global cooldown so new runs can use T&P immediately
        lastActivationTime = -COOLDOWN_DURATION;

        // Stop any local activity
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }

        // Stop hover and release physics control
        isHovering = false;
        if (rb != null)
        {
            // Return to physics mode safely
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Re-enable ItemController if it was disabled
        if (itemController != null)
        {
            itemController.enabled = true;
        }

        // Hide any lingering UI
        if (uiController != null)
        {
            uiController.HideThoughtsAndPrayersUI();
        }

        // Reset local state
        isActivated = false;
        currentPhase = ActivationPhase.Idle;
        prayerCount = 0;
    }

    public bool IsClean
    {
        get
        {
            bool physicsReleased = rb == null || !rb.isKinematic;
            return !isActivated && !isHovering && physicsReleased;
        }
    }
}
