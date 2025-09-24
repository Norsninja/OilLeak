using UnityEngine;

/// <summary>
/// Simple rotating rays effect for Thoughts & Prayers
/// Cartoon-style god rays that rotate behind text
/// Based on Terry Gilliam's paper cutout animation style
/// </summary>
public class RotatingRays : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 30f; // Degrees per second

    [Header("Optional Pulsing")]
    [SerializeField] private bool enablePulsing = false;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0, 1, 1, 1);
    [SerializeField] private float pulseSpeed = 1f; // Cycles per second

    private RectTransform rectTransform;
    private float time;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogWarning("[RotatingRays] No RectTransform found - using regular transform");
        }
    }

    void Update()
    {
        // Rotate continuously
        if (rectTransform != null)
        {
            rectTransform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }

        // Optional divine pulsing effect
        if (enablePulsing)
        {
            time += Time.deltaTime * pulseSpeed;
            float scale = scaleCurve.Evaluate(time % 1f);

            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * scale;
            }
            else
            {
                transform.localScale = Vector3.one * scale;
            }
        }
    }
}