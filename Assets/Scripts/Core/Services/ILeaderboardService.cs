using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Services
{
    /// <summary>
    /// Service interface for leaderboard management
    /// Handles score submission and retrieval
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// Initialize the leaderboard service
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Submit a score to a leaderboard
        /// </summary>
        Task<bool> SubmitScoreAsync(string boardId, int score);

        /// <summary>
        /// Get top scores from a leaderboard
        /// </summary>
        Task<List<LeaderboardEntry>> GetTopAsync(string boardId, int limit = 20);

        /// <summary>
        /// Get player's entry from a leaderboard
        /// </summary>
        Task<LeaderboardEntry> GetPlayerAsync(string boardId);

        /// <summary>
        /// Get scores around the player
        /// </summary>
        Task<List<LeaderboardEntry>> GetAroundPlayerAsync(string boardId, int range = 5);

        /// <summary>
        /// Check if service is initialized and ready
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Check if network is available
        /// </summary>
        bool IsOnline { get; }
    }

    /// <summary>
    /// Represents a single leaderboard entry
    /// </summary>
    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public int Score { get; set; }
        public System.DateTime SubmittedAt { get; set; }

        public LeaderboardEntry() { }

        public LeaderboardEntry(int rank, string playerId, string playerName, int score)
        {
            Rank = rank;
            PlayerId = playerId;
            PlayerName = playerName;
            Score = score;
            SubmittedAt = System.DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Known leaderboard IDs for the game
    /// </summary>
    public static class LeaderboardIds
    {
        public const string EndlessTotalScore = "endless_total_score";
        public const string EndlessGallonsDelayed = "endless_gallons_delayed";
        public const string EndlessTimeSurvived = "endless_time_survived";
    }
}