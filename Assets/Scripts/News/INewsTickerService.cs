namespace OilLeak.News
{
    /// <summary>
    /// Service interface for news ticker functionality.
    /// Simpler than Toast service - single feed, no voices.
    /// </summary>
    public interface INewsTickerService
    {
        /// <summary>
        /// Whether the service is ready to provide headlines
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Initialize the service, loading content and configuration
        /// </summary>
        void Initialize();

        /// <summary>
        /// Start providing headlines
        /// </summary>
        void Start();

        /// <summary>
        /// Pause headline updates (for game pause)
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume headline updates
        /// </summary>
        void Resume();

        /// <summary>
        /// Stop the service completely
        /// </summary>
        void Stop();

        /// <summary>
        /// Get the next headline to display. Called by UI when ready.
        /// </summary>
        /// <returns>Fully interpolated headline text</returns>
        string GetNextHeadline();

        /// <summary>
        /// Notify of a breaking news event
        /// </summary>
        void NotifyBreaking(BreakingEventType type, object context);

        /// <summary>
        /// Update current game state for interpolation
        /// </summary>
        void UpdateState(SessionStats stats, int tier);
    }
}