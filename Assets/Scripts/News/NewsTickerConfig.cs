using UnityEngine;

namespace OilLeak.News
{
    /// <summary>
    /// Configuration for news ticker behavior.
    /// Allows designer tuning without code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "OilLeak/NewsTickerConfig", fileName = "NewsTickerConfig")]
    public class NewsTickerConfig : ScriptableObject
    {
        [Header("Scrolling")]
        [Tooltip("Pixels per second at 1080p (Canvas Scaler will adjust)")]
        public float scrollSpeed = 120f;

        [Header("Breaking News")]
        [Tooltip("Minimum seconds between any breaking news")]
        public float globalBreakingMinInterval = 10f;

        [Tooltip("Maximum breaking news items in queue")]
        public int maxBreakingQueue = 5;

        [Header("Deduplication")]
        [Tooltip("Number of recent headlines to avoid repeating")]
        public int recentIdsBufferSize = 10;

        [Header("Formatting")]
        [Tooltip("How to display time - 'days' or 'minutes'")]
        public TimeDisplayFormat timeFormat = TimeDisplayFormat.Days;

        [Tooltip("Seconds per day for time conversion")]
        public float secondsPerDay = 60f;

        [Header("Debug")]
        [Tooltip("Log headline selections and state changes")]
        public bool debugLogging = false;
    }

    public enum TimeDisplayFormat
    {
        Days,
        Minutes,
        Hours
    }
}