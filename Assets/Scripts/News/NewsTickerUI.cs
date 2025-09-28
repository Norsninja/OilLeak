using System.Text;
using UnityEngine;
using TMPro;

namespace OilLeak.News
{
    /// <summary>
    /// UI component that handles news ticker scrolling display.
    /// Requests headlines from manager and scrolls them across screen.
    /// </summary>
    public class NewsTickerUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform tickerPanel; // The masked panel container
        [SerializeField] private TextMeshProUGUI tickerText; // The scrolling text
        [SerializeField] private RectTransform textTransform; // RectTransform of the text

        [Header("Scrolling Configuration")]
        [SerializeField] private float scrollSpeed = 120f; // Pixels per second
        [SerializeField] private float rightPadding = 50f; // Space before text enters from right
        [SerializeField] private float leftPadding = 50f; // Space after text exits on left

        [Header("Display")]
        [SerializeField] private bool showBreakingPrefix = true;

        // Runtime state
        private INewsTickerService tickerService;
        private string currentHeadline = "";
        private float currentPosition = 0f;
        private float textWidth = 0f;
        private bool isScrolling = false;
        private bool isActive = false;
        private StringBuilder headlineBuilder;

        // Cached values
        private float panelWidth;
        private float startPosition;
        private float endPosition;

        void Awake()
        {
            // Cache panel dimensions
            if (tickerPanel != null)
            {
                panelWidth = tickerPanel.rect.width;
            }

            // Initialize string builder for efficiency
            headlineBuilder = new StringBuilder(256);

            // Ensure text is set up correctly
            if (tickerText != null && textTransform == null)
            {
                textTransform = tickerText.rectTransform;
            }

            // Hide initially
            HideTicker();
        }

        void Start()
        {
            // Service will be injected or found
            // For now, it will be set by GameCore when initializing
        }

        void Update()
        {
            if (!isActive || !isScrolling) return;

            // Move text left
            currentPosition -= scrollSpeed * Time.deltaTime;

            // Update text position
            if (textTransform != null)
            {
                textTransform.anchoredPosition = new Vector2(currentPosition, textTransform.anchoredPosition.y);
            }

            // Check if text has scrolled off screen
            if (currentPosition < endPosition)
            {
                // Request next headline
                LoadNextHeadline();
            }
        }

        #region Public API

        /// <summary>
        /// Check if the ticker is currently active
        /// Used by adapter for IsClean state validation
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Initialize the ticker with a news service
        /// </summary>
        public void Initialize(INewsTickerService service)
        {
            tickerService = service;
            Debug.Log("[NewsTickerUI] Initialized with service");
        }

        /// <summary>
        /// Start displaying the ticker
        /// </summary>
        public void StartTicker()
        {
            if (tickerService == null)
            {
                Debug.LogError("[NewsTickerUI] Cannot start - no service configured");
                return;
            }

            isActive = true;
            ShowTicker();
            LoadNextHeadline();
            Debug.Log("[NewsTickerUI] Started");
        }

        /// <summary>
        /// Pause the ticker scrolling
        /// </summary>
        public void PauseTicker()
        {
            isScrolling = false;
            Debug.Log("[NewsTickerUI] Paused");
        }

        /// <summary>
        /// Resume ticker scrolling
        /// </summary>
        public void ResumeTicker()
        {
            if (isActive)
            {
                isScrolling = true;
                Debug.Log("[NewsTickerUI] Resumed");
            }
        }

        /// <summary>
        /// Stop and hide the ticker
        /// </summary>
        public void StopTicker()
        {
            isActive = false;
            isScrolling = false;
            HideTicker();
            Debug.Log("[NewsTickerUI] Stopped");
        }

        /// <summary>
        /// Update scroll speed at runtime
        /// </summary>
        public void SetScrollSpeed(float pixelsPerSecond)
        {
            scrollSpeed = pixelsPerSecond;
        }

        #endregion

        #region Display Management

        private void ShowTicker()
        {
            if (tickerPanel != null)
            {
                tickerPanel.gameObject.SetActive(true);
            }
        }

        private void HideTicker()
        {
            if (tickerPanel != null)
            {
                tickerPanel.gameObject.SetActive(false);
            }
        }

        private void LoadNextHeadline()
        {
            if (tickerService == null || !tickerService.IsReady)
            {
                // Use fallback if service not ready
                SetHeadlineText("News service initializing...");
                return;
            }

            // Get next headline from service
            string headline = tickerService.GetNextHeadline();

            if (string.IsNullOrEmpty(headline))
            {
                headline = "No news available";
            }

            // Check if this is a breaking news item (simple heuristic)
            if (showBreakingPrefix && headline.Contains("BREAKING:") == false && headline.Contains("Day ") == false)
            {
                // Could enhance this with metadata from service later
            }

            SetHeadlineText(headline);
        }

        private void SetHeadlineText(string headline)
        {
            currentHeadline = headline;

            // Update text component
            if (tickerText != null)
            {
                tickerText.text = currentHeadline;

                // Force update to get accurate width
                tickerText.ForceMeshUpdate();

                // Get text dimensions
                Vector2 textSize = tickerText.GetRenderedValues(false);
                textWidth = textSize.x;

                // Calculate positions
                CalculateScrollPositions();

                // Start scrolling
                StartScrolling();
            }
        }

        private void CalculateScrollPositions()
        {
            // Recache panel width in case it changed
            if (tickerPanel != null)
            {
                panelWidth = tickerPanel.rect.width;
            }

            // Start position: text starts just off the right edge
            startPosition = panelWidth / 2f + rightPadding;

            // End position: text has completely scrolled off the left edge
            endPosition = -(textWidth + panelWidth / 2f + leftPadding);
        }

        private void StartScrolling()
        {
            // Reset to start position
            currentPosition = startPosition;

            // Update text position
            if (textTransform != null)
            {
                textTransform.anchoredPosition = new Vector2(currentPosition, textTransform.anchoredPosition.y);
            }

            // Begin scrolling
            isScrolling = true;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NewsTickerUI] Scrolling: '{currentHeadline}' (width: {textWidth:F0}px)");
            #endif
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Load configuration from NewsTickerConfig if available
        /// </summary>
        public void ApplyConfiguration(NewsTickerConfig config)
        {
            if (config != null)
            {
                scrollSpeed = config.scrollSpeed;

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (config.debugLogging)
                {
                    Debug.Log($"[NewsTickerUI] Applied config - Speed: {scrollSpeed}px/s");
                }
                #endif
            }
        }

        #endregion

        #region Editor Helpers

        #if UNITY_EDITOR
        [ContextMenu("Test Headline")]
        private void TestHeadline()
        {
            SetHeadlineText("TEST: Day 47 - Congress 'deeply concerned' after 97,262 gallons escape containment");
        }

        [ContextMenu("Test Breaking")]
        private void TestBreaking()
        {
            SetHeadlineText("BREAKING: Ocean integrity drops below 50% - Federal response deemed 'inadequate'");
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (tickerPanel == null) return;

            // Draw scroll bounds
            Vector3[] corners = new Vector3[4];
            tickerPanel.GetWorldCorners(corners);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(corners[0], corners[1]); // Left edge

            Gizmos.color = Color.red;
            Gizmos.DrawLine(corners[2], corners[3]); // Right edge
        }
        #endif

        #endregion
    }
}