using UnityEngine;
using System.Collections;
using Core; // For GameFlowState

namespace OilLeak.UI
{
    /// <summary>
    /// Controls the start menu modal that displays game instructions.
    /// Shows automatically in Menu state, hides when player starts the game.
    /// </summary>
    public class StartModalController : MonoBehaviour, IResettable
    {
        [Header("UI References")]
        [SerializeField] private GameObject modalCanvas;
        [SerializeField] private GameObject modalPanel;

        [Header("Settings")]
        [SerializeField] private bool debugLogs = false;

        private bool isShowing = false;
        private bool isSubscribed = false; // Track subscription state to avoid double-subscribe

        #region Unity Lifecycle

        void Awake()
        {
            // Ensure modal starts hidden
            HideModal();

            // Don't subscribe here - Flow might not exist yet
            // Subscription moved to Start() for guaranteed initialization order
        }

        void Start()
        {
            // Try to subscribe immediately
            TrySubscribeToFlow();

            // If subscription failed, start coroutine to wait for Flow
            if (!isSubscribed)
            {
                StartCoroutine(SubscribeLateToFlow());
            }
        }

        void OnEnable()
        {
            // Re-subscribe if we were previously subscribed but got disabled
            if (!isSubscribed && GameCore.Flow != null)
            {
                TrySubscribeToFlow();
            }
        }

        void OnDisable()
        {
            // Unsubscribe when disabled
            if (isSubscribed && GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged -= OnStateChanged;
                isSubscribed = false;

                if (debugLogs)
                {
                    Debug.Log("[StartModalController] Unsubscribed from Flow (OnDisable)");
                }
            }
        }

        void OnDestroy()
        {
            // Clean up event subscription (belt-and-suspenders)
            if (isSubscribed && GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged -= OnStateChanged;
                isSubscribed = false;
            }
        }

        /// <summary>
        /// Try to subscribe to Flow state changes
        /// </summary>
        private void TrySubscribeToFlow()
        {
            if (isSubscribed) return; // Already subscribed

            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged += OnStateChanged;
                isSubscribed = true;

                if (debugLogs)
                {
                    Debug.Log($"[StartModalController] Subscribed to Flow. Current state: {GameCore.Flow.CurrentState}");
                }

                // Immediately check current state - if not Menu, hide
                if (GameCore.Flow.CurrentState != GameFlowState.Menu)
                {
                    if (debugLogs)
                    {
                        Debug.Log($"[StartModalController] Flow is in {GameCore.Flow.CurrentState}, hiding modal");
                    }
                    HideModal();
                }
            }
        }

        /// <summary>
        /// Coroutine to wait for Flow to be available and subscribe
        /// </summary>
        private IEnumerator SubscribeLateToFlow()
        {
            if (debugLogs)
            {
                Debug.Log("[StartModalController] Starting subscribe-late coroutine");
            }

            // Wait until Flow exists
            while (GameCore.Flow == null)
            {
                yield return null; // Wait one frame
            }

            // Now subscribe
            TrySubscribeToFlow();
        }

        /// <summary>
        /// Safety net - automatically hide modal when leaving Menu state
        /// </summary>
        private void OnStateChanged(GameFlowState oldState, GameFlowState newState)
        {
            // Hide modal whenever we're not in Menu state
            if (newState != GameFlowState.Menu && isShowing)
            {
                if (debugLogs)
                {
                    Debug.Log($"[StartModalController] Auto-hiding modal on state change from {oldState} to {newState}");
                }
                HideModal();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Show the start modal (called when entering Menu state)
        /// </summary>
        public void ShowModal()
        {
            if (isShowing) return;

            if (modalCanvas != null)
            {
                modalCanvas.SetActive(true);
            }

            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
            }

            isShowing = true;

            if (debugLogs)
            {
                Debug.Log("[StartModalController] Modal shown");
            }
        }

        /// <summary>
        /// Hide the start modal (called when player presses E to start)
        /// </summary>
        public void HideModal()
        {
            if (!isShowing && modalCanvas != null && !modalCanvas.activeSelf) return;

            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }

            if (modalCanvas != null)
            {
                modalCanvas.SetActive(false);
            }

            isShowing = false;

            if (debugLogs)
            {
                Debug.Log("[StartModalController] Modal hidden");
            }
        }

        /// <summary>
        /// Check if the modal is currently showing
        /// </summary>
        public bool IsShowing => isShowing;

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            // Hide modal on reset
            HideModal();

            if (debugLogs)
            {
                Debug.Log("[StartModalController] Reset - modal hidden");
            }
        }

        public bool IsClean => !isShowing &&
                               (modalCanvas == null || !modalCanvas.activeSelf) &&
                               (modalPanel == null || !modalPanel.activeSelf);

        #endregion
    }
}