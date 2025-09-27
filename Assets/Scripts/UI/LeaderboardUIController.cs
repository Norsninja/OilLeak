using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using Core.Services;

namespace OilLeak.UI
{
    /// <summary>
    /// Controls the leaderboard UI and player name entry
    /// </summary>
    public class LeaderboardUIController : MonoBehaviour
    {
        [Header("Canvas References")]
        [SerializeField] private GameObject leaderboardCanvas;
        [SerializeField] private GameObject namePromptPanel;
        [SerializeField] private GameObject leaderboardListPanel;

        [Header("Name Prompt UI")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TextMeshProUGUI namePromptText;
        [SerializeField] private TextMeshProUGUI nameErrorText;

        [Header("Leaderboard Display")]
        [SerializeField] private Transform leaderboardContent; // Parent for entry items
        [SerializeField] private GameObject leaderboardEntryPrefab; // Prefab for each row
        [SerializeField] private TextMeshProUGUI leaderboardStatusText;
        [SerializeField] private TextMeshProUGUI playerBestText; // "Your Best: #57 - 12,345"
        [SerializeField] private TextMeshProUGUI leaderboardTitleText;

        [Header("Configuration")]
        [SerializeField] private int maxNameLength = 20;
        [SerializeField] private bool debugLogging = true;

        // State
        private bool isOpen = false;
        private bool isNamePromptActive = false;
        private bool isFetchingData = false;

        // Cached entries
        private List<GameObject> displayedEntries = new();

        // Static flag for input blocking
        public static bool IsModalActive { get; private set; } = false;

        // Task completion source for awaitable name prompt
        private TaskCompletionSource<bool> namePromptTaskSource;

        void Awake()
        {
            // Hide everything on start
            if (leaderboardCanvas != null)
            {
                leaderboardCanvas.SetActive(false);

                // COMMENTED OUT: Respect sorting order set in Inspector instead of overriding at runtime
                // This allows designers to control layering through Unity's Inspector
                // var canvas = leaderboardCanvas.GetComponent<Canvas>();
                // if (canvas != null)
                // {
                //     canvas.sortingOrder = 10; // Higher than RoundOverCanvas
                // }
            }
            if (namePromptPanel != null)
                namePromptPanel.SetActive(false);
            if (leaderboardListPanel != null)
                leaderboardListPanel.SetActive(false);
        }

        /// <summary>
        /// Open the leaderboard (check for name first)
        /// </summary>
        public void Open()
        {
            if (isOpen) return;

            isOpen = true;
            leaderboardCanvas.SetActive(true);

            // Check if player has a name
            var leaderboardService = GameCore.Leaderboards as OilLeak.Online.UgsLeaderboardService;
            if (leaderboardService != null && !leaderboardService.HasCustomPlayerName())
            {
                // Show name prompt on first leaderboard access
                ShowNamePrompt();
            }
            else
            {
                // Player has name, fetch and show leaderboard
                FetchAndRenderTop();
            }
        }

        /// <summary>
        /// Close the leaderboard
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;

            isOpen = false;
            isNamePromptActive = false;
            IsModalActive = false;

            // Clear displayed entries
            foreach (var entry in displayedEntries)
            {
                Destroy(entry);
            }
            displayedEntries.Clear();

            // Hide everything
            if (leaderboardCanvas != null)
                leaderboardCanvas.SetActive(false);
            if (namePromptPanel != null)
                namePromptPanel.SetActive(false);
            if (leaderboardListPanel != null)
                leaderboardListPanel.SetActive(false);

            Debug.Log("[LeaderboardUI] Closed");
        }

        /// <summary>
        /// Show the name entry prompt
        /// </summary>
        private void ShowNamePrompt()
        {
            isNamePromptActive = true;
            IsModalActive = true;

            namePromptPanel.SetActive(true);
            leaderboardListPanel.SetActive(false);

            // Set prompt text
            if (namePromptText != null)
                namePromptText.text = "Enter your name for the leaderboard";

            // Clear error text
            if (nameErrorText != null)
                nameErrorText.text = "";

            // Focus input field
            if (playerNameInput != null)
            {
                playerNameInput.text = "";
                playerNameInput.ActivateInputField();
                playerNameInput.Select();
            }

            Debug.Log("[LeaderboardUI] Showing name prompt");
        }

        /// <summary>
        /// Prompt for player name asynchronously (for score submission flow)
        /// </summary>
        public async Task<bool> PromptForPlayerNameAsync()
        {
            // Create task completion source
            namePromptTaskSource = new TaskCompletionSource<bool>();

            // Show the leaderboard canvas but only the name prompt
            if (leaderboardCanvas != null)
            {
                leaderboardCanvas.SetActive(true);

                // COMMENTED OUT: Respect sorting order set in Inspector instead of overriding at runtime
                // This allows designers to control layering through Unity's Inspector
                // var canvas = leaderboardCanvas.GetComponent<Canvas>();
                // if (canvas != null)
                // {
                //     canvas.sortingOrder = 10; // Higher than RoundOverCanvas
                // }
            }

            // Show name prompt
            ShowNamePrompt();

            // Wait for user to confirm or cancel
            bool result = await namePromptTaskSource.Task;

            // If cancelled, hide everything
            if (!result)
            {
                isNamePromptActive = false;
                IsModalActive = false;
                if (leaderboardCanvas != null)
                    leaderboardCanvas.SetActive(false);
                if (namePromptPanel != null)
                    namePromptPanel.SetActive(false);
            }
            else
            {
                // Name confirmed - hide the prompt but keep canvas active
                // (in case they want to view leaderboard after)
                HideNamePrompt();
                if (leaderboardCanvas != null)
                    leaderboardCanvas.SetActive(false);
            }

            return result;
        }

        /// <summary>
        /// Hide the name prompt
        /// </summary>
        private void HideNamePrompt()
        {
            isNamePromptActive = false;
            IsModalActive = false;

            if (namePromptPanel != null)
                namePromptPanel.SetActive(false);
        }

        /// <summary>
        /// Confirm the entered name
        /// </summary>
        public async void ConfirmName()
        {
            if (!isNamePromptActive || playerNameInput == null) return;

            string enteredName = playerNameInput.text.Trim();

            // Validate name
            if (string.IsNullOrEmpty(enteredName))
            {
                if (nameErrorText != null)
                    nameErrorText.text = "Name cannot be empty";
                return;
            }

            if (enteredName.Length > maxNameLength)
            {
                if (nameErrorText != null)
                    nameErrorText.text = $"Name too long (max {maxNameLength} chars)";
                return;
            }

            // Sanitize name (alphanumeric, spaces, underscores only)
            string sanitized = SanitizeName(enteredName);
            if (sanitized != enteredName)
            {
                if (nameErrorText != null)
                    nameErrorText.text = "Invalid characters removed";
                enteredName = sanitized;
            }

            // Save name (this now updates Unity Auth too)
            var leaderboardService = GameCore.Leaderboards as OilLeak.Online.UgsLeaderboardService;
            if (leaderboardService != null)
            {
                await leaderboardService.SetLocalPlayerName(enteredName);
                Debug.Log($"[LeaderboardUI] Name saved and Unity Auth updated: {enteredName}");
            }

            // Complete the task if we're in async mode
            if (namePromptTaskSource != null && !namePromptTaskSource.Task.IsCompleted)
            {
                namePromptTaskSource.SetResult(true);
                // Don't show leaderboard here - just complete the prompt
            }
            else
            {
                // Normal flow from Open() - hide prompt and show leaderboard
                HideNamePrompt();
                FetchAndRenderTop();
            }
        }

        /// <summary>
        /// Cancel name entry
        /// </summary>
        public void CancelNamePrompt()
        {
            // Complete the task if we're in async mode
            if (namePromptTaskSource != null && !namePromptTaskSource.Task.IsCompleted)
            {
                namePromptTaskSource.SetResult(false);
                // Don't close here - let the async method handle it
            }
            else
            {
                // Normal flow - close everything
                HideNamePrompt();
                Close();
            }
        }

        /// <summary>
        /// Sanitize player name
        /// </summary>
        private string SanitizeName(string name)
        {
            // Allow alphanumeric, spaces, and underscores only
            return Regex.Replace(name, @"[^a-zA-Z0-9_ ]", "").Trim();
        }

        /// <summary>
        /// Fetch and display top scores
        /// </summary>
        private async void FetchAndRenderTop()
        {
            if (isFetchingData) return;
            isFetchingData = true;

            // Show leaderboard panel
            leaderboardListPanel.SetActive(true);
            namePromptPanel.SetActive(false);

            // Update status
            if (leaderboardStatusText != null)
                leaderboardStatusText.text = "Fetching leaderboard...";

            // Clear old entries
            foreach (var entry in displayedEntries)
            {
                Destroy(entry);
            }
            displayedEntries.Clear();

            var leaderboardService = GameCore.Leaderboards;

            if (leaderboardService == null || !leaderboardService.IsReady)
            {
                // Offline mode
                ShowOfflineMessage();
                isFetchingData = false;
                return;
            }

            try
            {
                // Fetch top 10
                var topScores = await leaderboardService.GetTopAsync(LeaderboardIds.EndlessTotalScore, 10);

                // Fetch player's best score
                var playerScore = await leaderboardService.GetPlayerAsync(LeaderboardIds.EndlessTotalScore);

                // Check if player is in top 10
                bool isPlayerInTop10 = false;
                if (playerScore != null && !string.IsNullOrEmpty(playerScore.PlayerId))
                {
                    isPlayerInTop10 = topScores != null && topScores.Any(e => e.PlayerId == playerScore.PlayerId);
                }

                // Display entries
                if (topScores != null && topScores.Count > 0)
                {
                    DisplayLeaderboard(topScores, playerScore, isPlayerInTop10);
                }
                else
                {
                    if (leaderboardStatusText != null)
                        leaderboardStatusText.text = "No scores yet. Be the first!";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LeaderboardUI] Error fetching scores: {e.Message}");
                ShowOfflineMessage();
            }
            finally
            {
                isFetchingData = false;
            }
        }

        /// <summary>
        /// Display the leaderboard entries
        /// </summary>
        private void DisplayLeaderboard(List<LeaderboardEntry> entries,
            LeaderboardEntry playerEntry = null, bool isPlayerInTop10 = false)
        {
            Debug.Log($"[LeaderboardUI] Displaying {entries.Count} leaderboard entries");

            if (leaderboardStatusText != null)
                leaderboardStatusText.text = "";

            if (leaderboardTitleText != null)
                leaderboardTitleText.text = "TOP 10 FUTILITY CHAMPIONS";

            // Get player ID to highlight their entry
            string currentPlayerId = "";
            var authService = Unity.Services.Authentication.AuthenticationService.Instance;
            if (authService != null && authService.IsSignedIn)
            {
                currentPlayerId = authService.PlayerId;
            }

            foreach (var entry in entries)
            {
                GameObject entryGO;

                if (leaderboardEntryPrefab != null)
                {
                    // Use prefab if available
                    entryGO = Instantiate(leaderboardEntryPrefab, leaderboardContent);

                    // Find and update the text components
                    var texts = entryGO.GetComponentsInChildren<TextMeshProUGUI>();

                    // Removed excessive layout debug logging

                    if (texts.Length >= 3)
                    {
                        // Assuming order: Rank, PlayerName, Score
                        // Convert 0-based rank to 1-based for display
                        int displayRank = entry.Rank == 0 ? 1 : entry.Rank;
                        texts[0].text = $"{displayRank}.";
                        texts[1].text = entry.PlayerName;
                        texts[2].text = $"{entry.Score:N0}";

                        // Removed excessive PlayerName debug code

                        // Removed TEST_VISIBLE debug code

                        // Highlight if this is the current player
                        if (!string.IsNullOrEmpty(currentPlayerId) && entry.PlayerId == currentPlayerId)
                        {
                            Color goldColor = new Color(1.0f, 0.843f, 0.0f); // Gold
                            texts[0].color = goldColor; // Rank
                            texts[1].color = goldColor; // PlayerName
                            texts[2].color = goldColor; // Score
                        }
                    }
                    else if (texts.Length == 1)
                    {
                        // Fallback if prefab has single text
                        int displayRank = entry.Rank == 0 ? 1 : entry.Rank;
                        texts[0].text = $"{displayRank}. {entry.PlayerName} - {entry.Score:N0}";

                        // Highlight if this is the current player
                        if (!string.IsNullOrEmpty(currentPlayerId) && entry.PlayerId == currentPlayerId)
                        {
                            texts[0].color = new Color(1.0f, 0.843f, 0.0f); // Gold
                        }
                    }
                }
                else
                {
                    // Fallback: create simple text entry
                    int displayRank = entry.Rank == 0 ? 1 : entry.Rank;
                    entryGO = new GameObject($"Entry_{displayRank}");
                    entryGO.transform.SetParent(leaderboardContent, false);

                    var text = entryGO.AddComponent<TextMeshProUGUI>();
                    text.text = $"{displayRank}. {entry.PlayerName} - {entry.Score:N0}";
                    text.fontSize = 24;
                    text.alignment = TextAlignmentOptions.Left;

                    // Highlight if this is the current player
                    if (!string.IsNullOrEmpty(currentPlayerId) && entry.PlayerId == currentPlayerId)
                    {
                        text.color = new Color(1.0f, 0.843f, 0.0f); // Gold
                    }
                }

                displayedEntries.Add(entryGO);
            }

            // Add player entry if not in top 10
            if (playerEntry != null && !isPlayerInTop10 && playerEntry.Score > 0)
            {
                // Create separator (simple ellipsis text)
                GameObject separator = new GameObject("Separator");
                separator.transform.SetParent(leaderboardContent, false);
                var separatorText = separator.AddComponent<TextMeshProUGUI>();
                separatorText.text = "...";
                separatorText.fontSize = 24;
                separatorText.alignment = TextAlignmentOptions.Center;
                displayedEntries.Add(separator);

                // Create player entry at position 11
                GameObject playerEntryGO;
                if (leaderboardEntryPrefab != null)
                {
                    playerEntryGO = Instantiate(leaderboardEntryPrefab, leaderboardContent);
                    var texts = playerEntryGO.GetComponentsInChildren<TextMeshProUGUI>();

                    if (texts.Length >= 3)
                    {
                        int displayRank = playerEntry.Rank == 0 ? 1 : playerEntry.Rank;
                        texts[0].text = $"{displayRank}.";
                        texts[1].text = playerEntry.PlayerName;
                        texts[2].text = $"{playerEntry.Score:N0}";

                        // Apply gold color to ALL components
                        Color goldColor = new Color(1.0f, 0.843f, 0.0f); // RGB for gold
                        texts[0].color = goldColor; // Rank
                        texts[1].color = goldColor; // PlayerName
                        texts[2].color = goldColor; // Score
                    }
                    else if (texts.Length == 1)
                    {
                        // Fallback for single text prefab
                        int displayRank = playerEntry.Rank == 0 ? 1 : playerEntry.Rank;
                        texts[0].text = $"{displayRank}. {playerEntry.PlayerName} - {playerEntry.Score:N0}";
                        texts[0].color = new Color(1.0f, 0.843f, 0.0f); // Gold
                    }
                }
                else
                {
                    // Fallback creation if no prefab
                    playerEntryGO = new GameObject("PlayerEntry");
                    playerEntryGO.transform.SetParent(leaderboardContent, false);
                    var text = playerEntryGO.AddComponent<TextMeshProUGUI>();
                    int displayRank = playerEntry.Rank == 0 ? 1 : playerEntry.Rank;
                    text.text = $"{displayRank}. {playerEntry.PlayerName} - {playerEntry.Score:N0}";
                    text.fontSize = 24;
                    text.alignment = TextAlignmentOptions.Left;
                    text.color = new Color(1.0f, 0.843f, 0.0f); // Gold
                }

                displayedEntries.Add(playerEntryGO);
            }
        }

        // Removed DisplayPlayerBest method - redundant with leaderboard display

        /// <summary>
        /// Show offline message
        /// </summary>
        private void ShowOfflineMessage()
        {
            if (leaderboardStatusText != null)
                leaderboardStatusText.text = "Leaderboard unavailable (offline mode)";

            if (leaderboardTitleText != null)
                leaderboardTitleText.text = "OFFLINE";
        }

        /// <summary>
        /// Handle input while modal is active
        /// </summary>
        void Update()
        {
            if (!isOpen) return;

            if (isNamePromptActive)
            {
                // Handle name prompt input
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ConfirmName();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelNamePrompt();
                }
            }
            else
            {
                // Handle leaderboard navigation
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Close();
                }
            }
        }
    }
}