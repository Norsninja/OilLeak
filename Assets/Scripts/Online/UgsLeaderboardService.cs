using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using Core.Services;

namespace OilLeak.Online
{
    /// <summary>
    /// Unity Gaming Services implementation of ILeaderboardService
    /// Handles authentication and leaderboard operations
    /// </summary>
    public class UgsLeaderboardService : MonoBehaviour, ILeaderboardService, IResettable
    {
        [Header("Configuration")]
        [SerializeField] private bool debugLogging = true; // Force recompile
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelay = 1f;

        // Service state
        private bool isInitialized = false;
        private bool isOnline = false;
        private bool initializationInProgress = false;

        // Cached data
        private Dictionary<string, List<Core.Services.LeaderboardEntry>> cachedLeaderboards = new();
        private Dictionary<string, DateTime> cacheTimestamps = new();
        private readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(5);

        // Player name management
        private string cachedPlayerName = null;

        #region ILeaderboardService Implementation

        public bool IsReady => isInitialized && AuthenticationService.Instance != null &&
                               AuthenticationService.Instance.IsSignedIn;

        public bool IsOnline => isOnline && Application.internetReachability != NetworkReachability.NotReachable;

        public async Task<bool> InitializeAsync()
        {
            if (isInitialized || initializationInProgress)
            {
                if (debugLogging) Debug.Log("[UgsLeaderboard] Already initialized or initializing");
                return isInitialized;
            }

            initializationInProgress = true;

            try
            {
                // Initialize Unity Services
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                    if (debugLogging) Debug.Log("[UgsLeaderboard] Unity Services initialized");
                }

                // Sign in anonymously if not already signed in
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    if (debugLogging) Debug.Log($"[UgsLeaderboard] Signed in as: {AuthenticationService.Instance.PlayerId}");
                }

                isInitialized = true;
                isOnline = true;

                // Load saved player name and update Unity Auth if we have one
                string savedName = PlayerPrefs.GetString("PlayerName", "");
                if (!string.IsNullOrEmpty(savedName))
                {
                    if (debugLogging) Debug.Log($"[UgsLeaderboard] Found saved name: '{savedName}', updating Unity Auth...");

                    try
                    {
                        // Update Unity's display name to match our saved name
                        if (debugLogging) Debug.Log($"[UgsLeaderboard] Calling UpdatePlayerNameAsync('{savedName}')...");
                        await AuthenticationService.Instance.UpdatePlayerNameAsync(savedName);
                        if (debugLogging) Debug.Log($"[UgsLeaderboard] UpdatePlayerNameAsync completed");

                        // Check what Unity returned
                        string unityName = AuthenticationService.Instance.PlayerName;
                        if (debugLogging) Debug.Log($"[UgsLeaderboard] Unity returned PlayerName: '{unityName ?? "<null>"}'");

                        // Cache the result (with suffix)
                        if (!string.IsNullOrEmpty(unityName))
                        {
                            cachedPlayerName = unityName;
                            if (debugLogging) Debug.Log($"[UgsLeaderboard] Unity Auth name successfully set to: {cachedPlayerName}");
                        }
                        else
                        {
                            Debug.LogWarning($"[UgsLeaderboard] Unity returned empty PlayerName after update - using local name");
                            cachedPlayerName = savedName;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[UgsLeaderboard] Failed to set Unity Auth name on init: {e.Message}");
                        Debug.LogWarning($"[UgsLeaderboard] Exception type: {e.GetType().Name}");
                        if (e.InnerException != null)
                        {
                            Debug.LogWarning($"[UgsLeaderboard] Inner exception: {e.InnerException.Message}");
                        }
                        // Fall back to local name
                        cachedPlayerName = savedName;
                    }
                }
                else
                {
                    // No saved name - will be prompted later
                    if (debugLogging) Debug.Log("[UgsLeaderboard] No saved name found - will prompt on first leaderboard access");
                }

                return true;
            }
            catch (ServicesInitializationException e)
            {
                Debug.LogError($"[UgsLeaderboard] Services initialization failed: {e.Message}");
                isOnline = false;
                return false;
            }
            catch (AuthenticationException e)
            {
                Debug.LogError($"[UgsLeaderboard] Authentication failed: {e.Message}");
                isOnline = false;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UgsLeaderboard] Unexpected error during initialization: {e.Message}");
                isOnline = false;
                return false;
            }
            finally
            {
                initializationInProgress = false;
            }
        }

        public async Task<bool> SubmitScoreAsync(string boardId, int score)
        {
            if (!IsReady)
            {
                if (debugLogging) Debug.LogWarning("[UgsLeaderboard] Not ready to submit score");
                return false;
            }

            int retries = 0;
            while (retries < maxRetries)
            {
                try
                {
                    // Log current auth state before submission
                    if (debugLogging)
                    {
                        string currentAuthName = AuthenticationService.Instance.PlayerName ?? "<null>";
                        string currentPlayerId = AuthenticationService.Instance.PlayerId ?? "<null>";
                        Debug.Log($"[UgsLeaderboard] Pre-submission - AuthName: {currentAuthName}, PlayerId: {currentPlayerId}");
                    }

                    // Submit score to leaderboard (v1.0.0 API uses double)
                    var scoreResponse = await LeaderboardsService.Instance.AddPlayerScoreAsync(
                        boardId,
                        (double)score
                    );

                    if (debugLogging)
                    {
                        Debug.Log($"[UgsLeaderboard] Score submitted - Board: {boardId}, Score: {score}");
                        if (scoreResponse != null)
                        {
                            Debug.Log($"[UgsLeaderboard] Response - PlayerId: {scoreResponse.PlayerId}, PlayerName: {scoreResponse.PlayerName}");
                        }
                    }

                    // Clear cache for this leaderboard
                    if (cachedLeaderboards.ContainsKey(boardId))
                    {
                        cachedLeaderboards.Remove(boardId);
                        cacheTimestamps.Remove(boardId);
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UgsLeaderboard] Submit failed (attempt {retries + 1}): {e.Message}");
                    retries++;

                    if (retries < maxRetries)
                    {
                        await Task.Delay((int)(retryDelay * 1000));
                    }
                }
            }

            return false;
        }

        public async Task<List<Core.Services.LeaderboardEntry>> GetTopAsync(string boardId, int limit = 20)
        {
            // Check cache first
            if (IsCacheValid(boardId))
            {
                if (debugLogging) Debug.Log("[UgsLeaderboard] Returning cached leaderboard");
                return cachedLeaderboards[boardId];
            }

            if (!IsReady)
            {
                if (debugLogging) Debug.LogWarning("[UgsLeaderboard] Not ready to fetch leaderboard");
                return new List<Core.Services.LeaderboardEntry>();
            }

            try
            {
                // Fetch top scores (v1.0.0 API with pagination)
                var scoresResponse = await LeaderboardsService.Instance.GetScoresAsync(
                    boardId,
                    new GetScoresOptions { Offset = 0, Limit = limit }
                );

                var entries = new List<Core.Services.LeaderboardEntry>();

                if (scoresResponse?.Results != null)
                {
                    int count = 0;
                    foreach (var result in scoresResponse.Results)
                    {
                        if (count >= limit) break;

                        // Debug log what we're getting from Unity
                        if (debugLogging)
                        {
                            Debug.Log($"[UgsLeaderboard] Entry {count}: PlayerId={result.PlayerId}, PlayerName={result.PlayerName ?? "<null>"}, Score={result.Score}");
                        }

                        entries.Add(new Core.Services.LeaderboardEntry
                        {
                            Rank = result.Rank,
                            PlayerId = result.PlayerId,
                            PlayerName = result.PlayerName ?? $"Player_{result.PlayerId.Substring(0, Math.Min(6, result.PlayerId.Length))}",
                            Score = (int)result.Score,
                            SubmittedAt = DateTime.UtcNow
                        });
                        count++;
                    }
                }

                // Update cache
                cachedLeaderboards[boardId] = entries;
                cacheTimestamps[boardId] = DateTime.UtcNow;

                if (debugLogging)
                {
                    Debug.Log($"[UgsLeaderboard] Fetched {entries.Count} top scores for {boardId}");
                }

                return entries;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UgsLeaderboard] Unexpected error fetching leaderboard: {e.Message}");
                return new List<Core.Services.LeaderboardEntry>();
            }
        }

        public async Task<Core.Services.LeaderboardEntry> GetPlayerAsync(string boardId)
        {
            if (!IsReady)
            {
                if (debugLogging) Debug.LogWarning("[UgsLeaderboard] Not ready to fetch player score");
                return null;
            }

            try
            {
                // Get player's own score (v1.0.0 API)
                var playerScore = await LeaderboardsService.Instance.GetPlayerScoreAsync(boardId);

                if (playerScore != null)
                {
                    return new Core.Services.LeaderboardEntry
                    {
                        Rank = playerScore.Rank,
                        PlayerId = playerScore.PlayerId,
                        PlayerName = playerScore.PlayerName ?? $"Player_{playerScore.PlayerId.Substring(0, Math.Min(6, playerScore.PlayerId.Length))}",
                        Score = (int)playerScore.Score,
                        SubmittedAt = DateTime.UtcNow
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UgsLeaderboard] Unexpected error fetching player score: {e.Message}");
            }

            return null;
        }

        public async Task<List<Core.Services.LeaderboardEntry>> GetAroundPlayerAsync(string boardId, int range = 5)
        {
            if (!IsReady)
            {
                if (debugLogging) Debug.LogWarning("[UgsLeaderboard] Not ready to fetch scores around player");
                return new List<Core.Services.LeaderboardEntry>();
            }

            try
            {
                // Get scores around player using the v1.0.0 GetPlayerRangeAsync API
                var scoresResponse = await LeaderboardsService.Instance.GetPlayerRangeAsync(
                    boardId,
                    new GetPlayerRangeOptions { RangeLimit = range }
                );

                var entries = new List<Core.Services.LeaderboardEntry>();

                if (scoresResponse?.Results != null)
                {
                    foreach (var result in scoresResponse.Results)
                    {
                        entries.Add(new Core.Services.LeaderboardEntry
                        {
                            Rank = result.Rank,
                            PlayerId = result.PlayerId,
                            PlayerName = result.PlayerName ?? $"Player_{result.PlayerId.Substring(0, Math.Min(6, result.PlayerId.Length))}",
                            Score = (int)result.Score,
                            SubmittedAt = DateTime.UtcNow
                        });
                    }
                }

                if (debugLogging)
                {
                    Debug.Log($"[UgsLeaderboard] Fetched {entries.Count} scores around player for {boardId}");
                }

                return entries;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UgsLeaderboard] Unexpected error: {e.Message}");
                return new List<Core.Services.LeaderboardEntry>();
            }
        }

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            // Clear cached data
            cachedLeaderboards.Clear();
            cacheTimestamps.Clear();

            if (debugLogging) Debug.Log("[UgsLeaderboard] Cache cleared");
        }

        public bool IsClean => cachedLeaderboards.Count == 0;

        #endregion

        #region Helper Methods

        private bool IsCacheValid(string boardId)
        {
            if (!cachedLeaderboards.ContainsKey(boardId))
                return false;

            if (!cacheTimestamps.ContainsKey(boardId))
                return false;

            var age = DateTime.UtcNow - cacheTimestamps[boardId];
            return age < cacheExpiration;
        }

        private string GeneratePlayerName()
        {
            // Generate a fun anonymous name
            string[] adjectives = { "Swift", "Brave", "Noble", "Clever", "Bold" };
            string[] nouns = { "Pelican", "Dolphin", "Seagull", "Captain", "Sailor" };

            var adj = adjectives[UnityEngine.Random.Range(0, adjectives.Length)];
            var noun = nouns[UnityEngine.Random.Range(0, nouns.Length)];
            var number = UnityEngine.Random.Range(100, 999);

            return $"{adj}{noun}{number}";
        }

        #endregion

        #region Player Name Management

        /// <summary>
        /// Get the local player name (from PlayerPrefs or generate anonymous)
        /// </summary>
        public string GetLocalPlayerName()
        {
            // Use cached name if available
            if (!string.IsNullOrEmpty(cachedPlayerName))
                return cachedPlayerName;

            // Try to load from PlayerPrefs
            string savedName = PlayerPrefs.GetString("PlayerName", "");

            if (!string.IsNullOrEmpty(savedName))
            {
                cachedPlayerName = savedName;
                return cachedPlayerName;
            }

            // Generate anonymous name as fallback
            cachedPlayerName = GeneratePlayerName();
            return cachedPlayerName;
        }

        /// <summary>
        /// Set and save the local player name, and attempt to update UGS display name
        /// </summary>
        public async Task SetLocalPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (debugLogging) Debug.LogWarning("[UgsLeaderboard] Attempted to set empty player name");
                return;
            }

            cachedPlayerName = name;
            PlayerPrefs.SetString("PlayerName", name);
            PlayerPrefs.Save(); // Critical for WebGL

            if (debugLogging) Debug.Log($"[UgsLeaderboard] Player name set to: {name}");

            // Best-effort: update Unity Authentication display name
            try
            {
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(name);

                    // Cache the resulting display name (with suffix) if available
                    if (!string.IsNullOrEmpty(AuthenticationService.Instance.PlayerName))
                    {
                        cachedPlayerName = AuthenticationService.Instance.PlayerName;
                        if (debugLogging) Debug.Log($"[UgsLeaderboard] UGS display name updated to: {cachedPlayerName}");
                    }
                }
                else if (debugLogging)
                {
                    Debug.LogWarning("[UgsLeaderboard] Not signed in - cannot update UGS display name now");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UgsLeaderboard] Failed to update UGS display name: {e.Message}");
                // Continue anyway; local name is saved
            }
        }

        /// <summary>
        /// Check if player has set a custom name
        /// </summary>
        public bool HasCustomPlayerName()
        {
            bool hasKey = PlayerPrefs.HasKey("PlayerName");
            string savedName = PlayerPrefs.GetString("PlayerName", "");
            bool hasName = hasKey && !string.IsNullOrEmpty(savedName);

            if (debugLogging)
            {
                Debug.Log($"[UgsLeaderboard] HasCustomPlayerName check - HasKey: {hasKey}, SavedName: '{savedName}', Result: {hasName}");
            }

            return hasName;
        }

        /// <summary>
        /// Clear the saved player name (for testing)
        /// </summary>
        [ContextMenu("Clear Player Name")]
        public void ClearPlayerName()
        {
            cachedPlayerName = null;
            PlayerPrefs.DeleteKey("PlayerName");
            PlayerPrefs.Save();
            Debug.Log("[UgsLeaderboard] Player name cleared");
        }

        #endregion

        #region MonoBehaviour

        void Awake()
        {
            // Register with ResetRegistry
            Core.ResetRegistry.Register(this);
        }

        void OnDestroy()
        {
            // Unregister from ResetRegistry
            Core.ResetRegistry.Unregister(this);
        }

        #endregion
    }
}
