using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    // Public variables to hold data for day-night cycle and game timer
    public DayNightData dayNightData;
    public GameTimerData gameTimerData;
    public Material _material;
    // Reference to the surface material
    public Material _surfaceMaterial;

    // Colors for the _DeepColor property during day and night
    public Color dayDeepColor = new Color(0, 0.5f, 1);
    public Color nightDeepColor = new Color(0, 0, 0.5f);
    public Color dayShallowColor = new Color(0.2f, 0.4f, 0.6f);
    public Color nightShallowColor = new Color(0.1f, 0.2f, 0.3f);
    public Color dayFarColor = new Color(0.1f, 0.2f, 0.4f);
    public Color nightFarColor = new Color(0.05f, 0.1f, 0.2f);
    // Center point and radius for light revolution
    public Vector3 centerPoint = new Vector3(10, 0, 10);
    public float revolutionRadius = 50;

    // References to Sun and Moon light objects
    public Light sunLight;
    public Light moonLight;

    // Constants for full and half circle in degrees
    private const float FULL_CIRCLE = 360f;
    private const float HALF_CIRCLE = 180f;

    // Colors for ambient light during day and night
    private Color dayColor = Color.white;
    private Color nightColor = new Color(0, 0, 0.5f);
    //colors for material set in in inspector
    public Color dayTopColor = new Color(0.5f, 0.75f, 1, 0.9f); // 90% transparency
    public Color dayBottomColor = new Color(1, 1, 0.5f, 1); // No transparency

    public Color nightTopColor = new Color(0, 0, 0.5f);
    public Color nightBottomColor = new Color(0, 0, 0.25f);
    // Speed factor for ambient light transition
    private float ambientSpeed = 0.1f;

    // Initialize variables at the start of the game
    private void Start()
    {
        dayNightData.dayLength = gameTimerData.roundDuration; // Set day length based on game round duration
        dayNightData.currentTime = 0; // Reset current time to zero
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateTime();
        UpdateDayNightCycle();
    }

    // Update the current time in the day-night cycle
    private void UpdateTime()
    {
        dayNightData.currentTime += Time.deltaTime;
        if (dayNightData.currentTime > dayNightData.dayLength)
        {
            dayNightData.currentTime -= dayNightData.dayLength;
        }
    }

    // Update the entire day-night cycle
    private void UpdateDayNightCycle()
    {
        float angle = CalculateAngle();
        
        // Update angle for the moon
        float moonAngle = angle > HALF_CIRCLE ? angle - HALF_CIRCLE : angle + HALF_CIRCLE;

        // Update positions
        UpdateLightPosition(sunLight, angle);
        UpdateLightPosition(moonLight, moonAngle);

        // Update intensity and colors
        UpdateLightIntensity();

        // Update ambient light
        UpdateAmbientLight();
        //
        UpdateShaderProperties();
    }


    // Update the intensity and color of the lights
    private void UpdateLightIntensity()
    {
        // Get the y-coordinates of the sun and moon
        float sunY = sunLight.transform.position.y;
        float moonY = moonLight.transform.position.y;

        // Calculate normalized intensity based on y-coordinate and revolution radius
        float sunIntensity = Mathf.Clamp01((sunY - centerPoint.y) / revolutionRadius);
        float moonIntensity = Mathf.Clamp01((moonY - centerPoint.y) / revolutionRadius);

        // Set the intensity
        sunLight.intensity = sunIntensity;
        moonLight.intensity = moonIntensity;
        // Update isNight status based on the y-coordinate of the sun
        dayNightData.isNight = sunY < centerPoint.y;
        // Optional: Color transitions based on intensity
        Color sunriseColor = new Color(1, 0.5f, 0);  // Orange
        Color middayColor = Color.white;  // White
        sunLight.color = Color.Lerp(sunriseColor, middayColor, sunIntensity);

        Color moonsetColor = new Color(0.5f, 0.5f, 1);  // Light Blue
        Color midnightColor = new Color(0, 0, 0.5f);  // Dark Blue
        moonLight.color = Color.Lerp(moonsetColor, midnightColor, moonIntensity);
    }


    // Calculate the current angle of the sun/moon based on the elapsed time
    private float CalculateAngle()
    {
        return (dayNightData.currentTime / dayNightData.dayLength) * FULL_CIRCLE;
    }

    // Update the position of a light based on a given angle
    private void UpdateLightPosition(Light light, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float y = centerPoint.y + revolutionRadius * Mathf.Sin(radian);
        float z = centerPoint.z + revolutionRadius * Mathf.Cos(radian);
        light.transform.position = new Vector3(centerPoint.x, y, z);
        light.transform.LookAt(centerPoint);
    }

    // Update the ambient light color based on the time of day
    private void UpdateAmbientLight()
    {
        Color targetColor = dayNightData.isNight ? nightColor : dayColor;
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetColor, Time.deltaTime * ambientSpeed);
    }
    private void UpdateShaderProperties()
    {
        // Common calculations and updates
        UpdateCommonShaderProperties();

        // Update gradient shader
        if (_material != null)
        {
            UpdateGradientShader(_material, "_TopColor", dayTopColor, nightTopColor);
            UpdateGradientShader(_material, "_BottomColor", dayBottomColor, nightBottomColor);
        }

        // Update surface material
        if (_surfaceMaterial != null)
        {
            UpdateGradientShader(_surfaceMaterial, "_DeepColor", dayDeepColor, nightDeepColor);
            UpdateGradientShader(_surfaceMaterial, "_ShallowColor", dayShallowColor, nightShallowColor);
            UpdateGradientShader(_surfaceMaterial, "_FarColor", dayFarColor, nightFarColor);
        }
    }


    private void UpdateCommonShaderProperties()
    {
        if (_material != null)
        {
            _material.SetFloat("_IsNight", dayNightData.isNight ? 1.0f : 0.0f);
            _material.SetFloat("_SunIntensity", sunLight.intensity);
            _material.SetFloat("_MoonIntensity", moonLight.intensity);
        }
    }

    private void UpdateGradientShader(Material material, string colorPropertyName, Color dayColor, Color nightColor)
    {
        float halfDay = dayNightData.dayLength / 2;
        bool isDayPhase = dayNightData.currentTime <= halfDay;
        float blendFactor = isDayPhase ?
            Mathf.InverseLerp(0, halfDay, dayNightData.currentTime) :
            Mathf.InverseLerp(halfDay, dayNightData.dayLength, dayNightData.currentTime);

        // Adjust the alpha based on the time of day
        Color currentColor = isDayPhase ?
            Color.Lerp(dayColor, nightColor, blendFactor) :
            Color.Lerp(nightColor, dayColor, blendFactor);

        // Set the color property
        material.SetColor(colorPropertyName, currentColor);
    }



}

