using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OilLeak.Toast.Services;

namespace OilLeak.Toast.UI
{
    /// <summary>
    /// Main controller for the Toast UI system
    /// Manages the social feed display of toast notifications
    /// </summary>
    public class ToastUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform toastContainer;
        [SerializeField] private GameObject toastItemPrefab;
        [SerializeField] private int maxVisibleToasts = 3;

        [Header("Animation Settings")]
        [SerializeField] private float slideInDuration = 0.3f;
        [SerializeField] private float slideOutDuration = 0.3f;
        [SerializeField] private float displayDuration = 2.5f;
        [SerializeField] private AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve slideOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Audio")]
        [SerializeField] private AudioSource notificationAudioSource;
        [SerializeField] private float notificationVolume = 0.5f;

        [Header("Layout")]
        [SerializeField] private float toastSpacing = 10f;
        [SerializeField] private bool stackFromBottom = true;

        // Service reference
        private IToastService toastService;

        // Toast management
        private Queue<ToastPayload> pendingToasts = new Queue<ToastPayload>();
        private List<ToastItemUI> activeToasts = new List<ToastItemUI>();
        private Queue<ToastItemUI> toastPool = new Queue<ToastItemUI>();

        // State
        private bool isProcessing = false;
        private bool isPaused = false;
        private Coroutine processCoroutine;

        void Start()
        {
            InitializePool();
            StartCoroutine(ConnectToServiceWithRetry());
        }

        void OnDestroy()
        {
            DisconnectFromService();
        }

        private void InitializePool()
        {
            // Pre-create toast items for pooling
            for (int i = 0; i < maxVisibleToasts + 2; i++)
            {
                var toastGO = Instantiate(toastItemPrefab, toastContainer);
                var toastItem = toastGO.GetComponent<ToastItemUI>();

                if (toastItem == null)
                {
                    toastItem = toastGO.AddComponent<ToastItemUI>();
                }

                toastItem.Initialize(this);
                toastGO.SetActive(false);
                toastPool.Enqueue(toastItem);
            }
        }

        private IEnumerator ConnectToServiceWithRetry()
        {
            int retryCount = 0;
            const int maxRetries = 5;
            const float retryDelay = 0.5f;

            while (retryCount < maxRetries)
            {
                if (ConnectToService())
                {
                    yield break; // Successfully connected
                }

                retryCount++;
                #if UNITY_EDITOR && TOAST_DEBUG
                Debug.Log($"[ToastUIController] Waiting for GameCore initialization (attempt {retryCount}/{maxRetries})");
                #endif
                yield return new WaitForSeconds(retryDelay);
            }

            // Final attempt with fallback
            #if UNITY_EDITOR
            if (!ConnectToService())
            {
                // In editor, create a mock for testing
                #if UNITY_EDITOR
                Debug.Log("[ToastUIController] Creating mock toast service for editor testing after retries");
                #endif
                var mockProvider = new MockGameStateProvider();
                toastService = new ToastManager(mockProvider);
                toastService.Initialize();
                SubscribeToEvents();
            }
            #else
            Debug.LogWarning("[ToastUIController] Failed to connect to toast service after retries - toasts disabled");
            enabled = false;
            #endif
        }

        private bool ConnectToService()
        {
            // Try to get the service from GameCore
            if (GameCore.Toasts != null)
            {
                #if UNITY_EDITOR && TOAST_DEBUG
                Debug.Log("[ToastUIController] Using toast service from GameCore");
                #endif
                toastService = GameCore.Toasts;
                SubscribeToEvents();
                return true;
            }

            return false;
        }

        private void SubscribeToEvents()
        {
            if (toastService != null)
            {
                toastService.OnToastQueued += HandleToastQueued;
                toastService.OnToastDisplayed += HandleToastDisplayed;
                toastService.OnToastDismissed += HandleToastDismissed;
                #if UNITY_EDITOR && TOAST_DEBUG
                Debug.Log("[ToastUIController] Subscribed to toast service events");
                #endif
            }
        }

        private void DisconnectFromService()
        {
            if (toastService != null)
            {
                toastService.OnToastQueued -= HandleToastQueued;
                toastService.OnToastDisplayed -= HandleToastDisplayed;
                toastService.OnToastDismissed -= HandleToastDismissed;
            }
        }

        private void HandleToastQueued(ToastPayload payload)
        {
            if (payload == null) return;

            #if UNITY_EDITOR && TOAST_DEBUG
            UnityEngine.Debug.Log($"[ToastUIController] Toast queued! ID: {payload.id}, Text: {payload.interpolatedText}");
            #endif
            pendingToasts.Enqueue(payload);

            if (!isProcessing && !isPaused)
            {
                processCoroutine = StartCoroutine(ProcessToastQueue());
            }
        }

        private void HandleToastDisplayed(ToastPayload payload)
        {
            // Currently handled internally, but available for future use
        }

        private void HandleToastDismissed(ToastPayload payload)
        {
            // Currently handled internally, but available for future use
        }

        private IEnumerator ProcessToastQueue()
        {
            isProcessing = true;

            while (pendingToasts.Count > 0 && !isPaused)
            {
                // Check if we have room for more toasts
                if (activeToasts.Count >= maxVisibleToasts)
                {
                    // Wait for a toast to finish
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                // Get next toast
                var payload = pendingToasts.Dequeue();

                // Get a toast item from pool
                var toastItem = GetToastFromPool();
                if (toastItem == null)
                {
                    Debug.LogWarning("[ToastUIController] No toast items available in pool");
                    continue;
                }

                // Activate the toast item FIRST (before SetPayload)
                toastItem.gameObject.SetActive(true);

                // Then set up the toast (so coroutines can start)
                toastItem.SetPayload(payload);

                // Add to active list
                activeToasts.Add(toastItem);

                // Play notification sound
                PlayNotificationSound(payload.notificationSound);

                // Animate in
                yield return StartCoroutine(AnimateToastIn(toastItem));

                // Start display timer
                StartCoroutine(ToastDisplayTimer(toastItem));

                // Small delay between toasts
                yield return new WaitForSeconds(0.1f);
            }

            isProcessing = false;
        }

        private ToastItemUI GetToastFromPool()
        {
            if (toastPool.Count > 0)
            {
                return toastPool.Dequeue();
            }

            // Emergency creation if pool is exhausted
            Debug.LogWarning("[ToastUIController] Toast pool exhausted, creating emergency toast");
            var toastGO = Instantiate(toastItemPrefab, toastContainer);
            var toastItem = toastGO.GetComponent<ToastItemUI>();

            if (toastItem == null)
            {
                toastItem = toastGO.AddComponent<ToastItemUI>();
            }

            toastItem.Initialize(this);
            return toastItem;
        }

        public void ReturnToastToPool(ToastItemUI toast)
        {
            if (toast == null) return;

            activeToasts.Remove(toast);
            toast.gameObject.SetActive(false);
            toast.ResetToast();
            toastPool.Enqueue(toast);

            // Update layout
            UpdateToastPositions();

            // Resume processing if needed
            if (!isProcessing && pendingToasts.Count > 0 && !isPaused)
            {
                processCoroutine = StartCoroutine(ProcessToastQueue());
            }
        }

        private IEnumerator ToastDisplayTimer(ToastItemUI toast)
        {
            yield return new WaitForSeconds(displayDuration);

            if (!isPaused && activeToasts.Contains(toast))
            {
                yield return StartCoroutine(AnimateToastOut(toast));
                ReturnToastToPool(toast);
            }
        }

        private IEnumerator AnimateToastIn(ToastItemUI toast)
        {
            var rectTransform = toast.GetComponent<RectTransform>();
            var containerRect = toastContainer.GetComponent<RectTransform>();

            // Set consistent starting position
            var startPos = new Vector2(containerRect.rect.width + 100, rectTransform.anchoredPosition.y);
            rectTransform.anchoredPosition = startPos;

            // Set consistent ending position (always the same X)
            var endPos = new Vector2(0, startPos.y); // X=0 to align with container edge

            float elapsed = 0;
            while (elapsed < slideInDuration)
            {
                if (isPaused) yield return null;

                elapsed += Time.deltaTime;
                float t = elapsed / slideInDuration;
                float curveValue = slideInCurve.Evaluate(t);

                var newPos = Vector2.Lerp(startPos, endPos, curveValue);
                rectTransform.anchoredPosition = newPos;

                yield return null;
            }

            rectTransform.anchoredPosition = endPos;
            UpdateToastPositions();
        }

        private IEnumerator AnimateToastOut(ToastItemUI toast)
        {
            var rectTransform = toast.GetComponent<RectTransform>();
            var containerRect = toastContainer.GetComponent<RectTransform>();
            var startPos = rectTransform.anchoredPosition;
            var endPos = startPos;
            endPos.x = containerRect.rect.width + 100; // Exit off-screen to the right

            float elapsed = 0;
            while (elapsed < slideOutDuration)
            {
                if (isPaused) yield return null;

                elapsed += Time.deltaTime;
                float t = elapsed / slideOutDuration;
                float curveValue = slideOutCurve.Evaluate(t);

                var newPos = Vector2.Lerp(startPos, endPos, curveValue);
                rectTransform.anchoredPosition = newPos;

                yield return null;
            }
        }

        private void UpdateToastPositions()
        {
            float currentY = 0;

            if (stackFromBottom)
            {
                // Stack from bottom up
                for (int i = activeToasts.Count - 1; i >= 0; i--)
                {
                    var rectTransform = activeToasts[i].GetComponent<RectTransform>();
                    // Keep X at 0, only update Y
                    var targetPos = new Vector2(0, currentY);

                    // Animate to new position
                    StartCoroutine(AnimateToPosition(rectTransform, targetPos, 0.2f));

                    currentY += rectTransform.sizeDelta.y + toastSpacing;
                }
            }
            else
            {
                // Stack from top down
                foreach (var toast in activeToasts)
                {
                    var rectTransform = toast.GetComponent<RectTransform>();
                    // Keep X at 0, only update Y
                    var targetPos = new Vector2(0, -currentY);

                    // Animate to new position
                    StartCoroutine(AnimateToPosition(rectTransform, targetPos, 0.2f));

                    currentY += rectTransform.sizeDelta.y + toastSpacing;
                }
            }
        }

        private IEnumerator AnimateToPosition(RectTransform rectTransform, Vector2 targetPos, float duration)
        {
            var startPos = rectTransform.anchoredPosition;
            float elapsed = 0;

            while (elapsed < duration)
            {
                if (isPaused) yield return null;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            rectTransform.anchoredPosition = targetPos;
        }

        private void PlayNotificationSound(AudioClip clip)
        {
            if (clip != null && notificationAudioSource != null)
            {
                notificationAudioSource.PlayOneShot(clip, notificationVolume);
            }
        }

        public void PauseToasts()
        {
            isPaused = true;

            if (processCoroutine != null)
            {
                StopCoroutine(processCoroutine);
                processCoroutine = null;
            }
        }

        public void ResumeToasts()
        {
            isPaused = false;

            if (!isProcessing && pendingToasts.Count > 0)
            {
                processCoroutine = StartCoroutine(ProcessToastQueue());
            }
        }

        public void ClearAllToasts()
        {
            // Stop processing
            if (processCoroutine != null)
            {
                StopCoroutine(processCoroutine);
                processCoroutine = null;
            }

            // Clear pending
            pendingToasts.Clear();

            // Return all active to pool
            while (activeToasts.Count > 0)
            {
                ReturnToastToPool(activeToasts[0]);
            }

            isProcessing = false;
        }

        // Debug/Testing
        [ContextMenu("Test Toast")]
        private void TestToast()
        {
            var testPayload = new ToastPayload
            {
                id = "test_001",
                triggerId = "test_trigger",
                voiceId = "corporate",
                handle = "@BPSocialTeam",
                interpolatedText = "Environmental impact remains very, very modest after 5 minutes",
                borderColor = Color.yellow,
                timestamp = Time.time,
                actNumber = 1
            };

            HandleToastQueued(testPayload);
        }
    }
}