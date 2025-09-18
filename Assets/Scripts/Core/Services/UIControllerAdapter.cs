using UnityEngine;
using Core;

namespace Core.Services
{
    /// <summary>
    /// Adapter that bridges UIController to IHUDService
    /// Implements IResettable for clean restart support
    /// </summary>
    public class UIControllerAdapter : IHUDService, IResettable
    {
        private readonly UIController controller;
        private string lastMessage;
        private float messageTimer;

        public UIControllerAdapter(UIController controller)
        {
            this.controller = controller;
            if (controller == null)
            {
                Debug.LogError("[UIAdapter] Controller is null!");
            }
        }

        #region IHUDService Implementation

        public void UpdateGameUI()
        {
            if (controller != null)
            {
                controller.UpdateUI();
            }
        }

        public void UpdatePlayerProfile()
        {
            if (controller != null)
            {
                controller.UpdatePlayerProfileUI();
            }
        }

        public void ShowResults()
        {
            if (controller != null)
            {
                controller.ShowRoundOverUI();
            }
        }

        public void ShowResults(SessionStats stats, float peakDifficulty)
        {
            if (controller != null)
            {
                controller.ShowRoundOverUI(stats, peakDifficulty);
            }
        }

        public void HideResults()
        {
            if (controller != null)
            {
                controller.HideRoundOverUI();
            }
        }

        public void UpdateScore(int score)
        {
            // UIController doesn't have direct score update
            // It reads from GameState, so trigger general update
            if (controller != null && controller.gameState != null)
            {
                controller.gameState.score = score;
                controller.UpdateUI();
            }
        }

        public void UpdateTimer(float timeRemaining)
        {
            // UIController reads from GameState
            if (controller != null && controller.gameState != null)
            {
                // GameState uses 'timer' not 'timeRemaining'
                controller.gameState.timer = timeRemaining;
                controller.UpdateUI();
            }
        }

        public void UpdateOilLeaked(int gallonsLeaked)
        {
            // GameState doesn't have gallonsLeaked directly
            // Just trigger a general UI update
            if (controller != null)
            {
                controller.UpdateUI();
            }
        }

        public void ShowMessage(string message, float duration = 2f)
        {
            // Store message for potential display
            // UIController doesn't have direct message display yet
            lastMessage = message;
            messageTimer = duration;
            Debug.Log($"[HUD] {message}");
        }

        public void UpdateDifficultyDisplay(float difficulty)
        {
            // UIController doesn't have difficulty display yet
            // Log for now
            if (difficulty > 1.5f)
            {
                Debug.Log($"[HUD] Difficulty escalating: {difficulty:F1}x");
            }
        }

        public void SetGrade(char grade)
        {
            if (controller != null)
            {
                controller.SetGradeTextColor(grade);
            }
        }

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            lastMessage = null;
            messageTimer = 0f;

            if (controller != null)
            {
                // CRITICAL: Hide the round over screen when resetting
                controller.HideRoundOverUI();
                controller.UpdateUI();
            }

            Debug.Log("[UIAdapter] State reset");
        }

        public bool IsClean => string.IsNullOrEmpty(lastMessage) &&
            messageTimer == 0f;

        #endregion
    }
}