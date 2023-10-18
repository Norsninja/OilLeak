using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoundLocationData", menuName = "Game/RoundLocationData")]
public class RoundLocationData : ScriptableObject
{
    public GameTimerData gameTimerData; // Round-specific timer data
    public OilLeakData oilLeakData; // Round-specific oil leak data
    // ... (add any other round-specific data you want)
}

