using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OilLeak.Toast.Services;

namespace OilLeak.Toast.UI
{
    /// <summary>
    /// Individual toast item UI component
    /// Handles display and animation of a single toast
    /// </summary>
    public class ToastItemUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image borderImage;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI handleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private GameObject duplicateBadge;
        [SerializeField] private TextMeshProUGUI duplicateCountText;

        [Header("Animation")]
        [SerializeField] private bool useTypewriterEffect = true;
        [SerializeField] private float typewriterSpeed = 0.05f;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Styling")]
        [SerializeField] private float borderWidth = 3f;
        [SerializeField] private Color defaultBorderColor = Color.white;

        // References
        private ToastUIController controller;
        private ToastPayload currentPayload;
        private Coroutine typewriterCoroutine;

        // State
        private bool isAnimating = false;

        public void Initialize(ToastUIController controller)
        {
            this.controller = controller;

            // Ensure we have a canvas group for fading
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Set up border if needed
            if (borderImage != null)
            {
                var outline = borderImage.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = borderImage.gameObject.AddComponent<Outline>();
                    outline.effectDistance = new Vector2(borderWidth, borderWidth);
                }
            }
        }

        public void SetPayload(ToastPayload payload)
        {
            if (payload == null) return;

            currentPayload = payload;

            // Set handle
            if (handleText != null)
            {
                handleText.text = payload.handle;
            }

            // Set message
            if (messageText != null)
            {
                if (useTypewriterEffect)
                {
                    messageText.text = "";
                    if (typewriterCoroutine != null)
                    {
                        StopCoroutine(typewriterCoroutine);
                    }
                    typewriterCoroutine = StartCoroutine(TypewriterEffect(payload.interpolatedText));
                }
                else
                {
                    messageText.text = payload.interpolatedText;
                }
            }

            // Set avatar
            if (avatarImage != null && payload.avatar != null)
            {
                avatarImage.sprite = payload.avatar;
                avatarImage.gameObject.SetActive(true);
            }
            else if (avatarImage != null)
            {
                // Use default avatar or hide
                avatarImage.gameObject.SetActive(false);
            }

            // Set border color
            if (borderImage != null)
            {
                borderImage.color = payload.borderColor;

                // Also apply to outline if present
                var outline = borderImage.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = payload.borderColor;
                }
            }

            // Handle duplicate badge
            if (duplicateBadge != null)
            {
                if (payload.duplicateCount > 1)
                {
                    duplicateBadge.SetActive(true);
                    if (duplicateCountText != null)
                    {
                        duplicateCountText.text = $"x{payload.duplicateCount}";
                    }
                }
                else
                {
                    duplicateBadge.SetActive(false);
                }
            }

            // Add timestamp if we have a component for it
            var timestampText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (timestampText != null && timestampText.name.Contains("Timestamp"))
            {
                timestampText.text = FormatTimestamp(payload.timestamp);
            }
        }

        private IEnumerator TypewriterEffect(string text)
        {
            isAnimating = true;
            int charIndex = 0;

            while (charIndex < text.Length)
            {
                messageText.text = text.Substring(0, charIndex + 1);
                charIndex++;

                yield return new WaitForSeconds(typewriterSpeed);
            }

            isAnimating = false;
        }

        private string FormatTimestamp(float timestamp)
        {
            // Format as relative time
            if (timestamp < 60)
            {
                return "now";
            }
            else if (timestamp < 3600)
            {
                int minutes = Mathf.FloorToInt(timestamp / 60);
                return $"{minutes}m ago";
            }
            else
            {
                int hours = Mathf.FloorToInt(timestamp / 3600);
                return $"{hours}h ago";
            }
        }

        public void ResetToast()
        {
            // Stop any ongoing animations
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }

            // Clear text
            if (handleText != null) handleText.text = "";
            if (messageText != null) messageText.text = "";
            if (duplicateCountText != null) duplicateCountText.text = "";

            // Reset visuals
            if (avatarImage != null) avatarImage.sprite = null;
            if (borderImage != null) borderImage.color = defaultBorderColor;
            if (duplicateBadge != null) duplicateBadge.SetActive(false);

            // Reset state
            currentPayload = null;
            isAnimating = false;

            // Reset alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        // Animation helpers
        public void FadeIn(float duration)
        {
            if (canvasGroup != null)
            {
                StartCoroutine(Fade(0f, 1f, duration));
            }
        }

        public void FadeOut(float duration)
        {
            if (canvasGroup != null)
            {
                StartCoroutine(Fade(1f, 0f, duration));
            }
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        // Click handling (optional)
        public void OnToastClicked()
        {
            if (currentPayload != null)
            {
                Debug.Log($"[ToastItemUI] Toast clicked: {currentPayload.id}");
                // Could open a detail view or dismiss early
            }
        }

        // Hover effects (optional)
        public void OnPointerEnter()
        {
            // Could scale up slightly or brighten
            transform.localScale = Vector3.one * 1.02f;
        }

        public void OnPointerExit()
        {
            transform.localScale = Vector3.one;
        }
    }
}