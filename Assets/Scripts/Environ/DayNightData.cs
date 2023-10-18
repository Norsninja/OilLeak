using UnityEngine;

[CreateAssetMenu(fileName = "DayNightData", menuName = "Game/DayNightData")]
public class DayNightData : ScriptableObject
{
    public bool isNight;
    public float currentTime;  // Store the current time within the cycle here
    public float dayLength;
}