namespace Core.Services
{
    /// <summary>
    /// Service interface for HUD/UI updates
    /// Provides decoupled access to UI operations
    /// </summary>
    public interface IHUDService
    {
        /// <summary>
        /// Update main game UI with current state
        /// </summary>
        void UpdateGameUI();

        /// <summary>
        /// Update player profile display
        /// </summary>
        void UpdatePlayerProfile();

        /// <summary>
        /// Show round over/results UI (legacy)
        /// </summary>
        void ShowResults();

        /// <summary>
        /// Show round over/results UI with session data
        /// </summary>
        /// <param name="stats">Session statistics including time, particles, score</param>
        /// <param name="peakDifficulty">Highest difficulty multiplier reached</param>
        void ShowResults(SessionStats stats, float peakDifficulty);

        /// <summary>
        /// Hide round over/results UI
        /// </summary>
        void HideResults();

        /// <summary>
        /// Update score display
        /// </summary>
        void UpdateScore(int score);

        /// <summary>
        /// Update timer display
        /// </summary>
        void UpdateTimer(float timeRemaining);

        /// <summary>
        /// Update oil leaked display
        /// </summary>
        void UpdateOilLeaked(int gallonsLeaked);

        /// <summary>
        /// Update UI with complete SessionStats data
        /// </summary>
        void UpdateWithStats(SessionStats stats);

        /// <summary>
        /// Show temporary message/notification
        /// </summary>
        void ShowMessage(string message, float duration = 2f);

        /// <summary>
        /// Update difficulty indicator
        /// </summary>
        void UpdateDifficultyDisplay(float difficulty);

        /// <summary>
        /// Set performance grade display
        /// </summary>
        void SetGrade(char grade);
    }
}