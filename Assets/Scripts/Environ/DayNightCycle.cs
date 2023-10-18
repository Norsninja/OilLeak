using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public DayNightData dayNightData;
    public GameTimerData gameTimerData;  // Reference to GameTimerData

    public Vector3 centerPoint = new Vector3(10, 0, 10);  // Center of the map.
    public float revolutionRadius = 50;  // Distance from the center of the map at which the light will revolve.

    private float rotationSpeed;

    private void Start()
    {
        dayNightData.dayLength = gameTimerData.roundDuration;  // Set dayLength based on roundDuration
        rotationSpeed = 360 / dayNightData.dayLength;  // Update rotation speed
        dayNightData.currentTime = 0;  // Reset currentTime
    }

    private void Update()
    {
        dayNightData.currentTime += Time.deltaTime;

        // Reset the cycle if it exceeds the day length
        if (dayNightData.currentTime > dayNightData.dayLength)
        {
            dayNightData.currentTime -= dayNightData.dayLength;
        }

        // Calculate whether it's currently day or night based on the angle
        float angle = (dayNightData.currentTime / dayNightData.dayLength) * 360;
        dayNightData.isNight = angle > 180;

        // Convert the angle to radians for trigonometric calculations.
        float radian = angle * Mathf.Deg2Rad;
        
        // Calculate the y and z positions using trigonometry.
        float y = centerPoint.y + revolutionRadius * Mathf.Sin(radian);
        float z = centerPoint.z + revolutionRadius * Mathf.Cos(radian);

        // Set the light's position.
        transform.position = new Vector3(centerPoint.x, y, z);
        
        // Point the light towards the center of the map.
        transform.LookAt(centerPoint);
    }
}

