using UnityEngine;

[CreateAssetMenu(fileName = "OilLeakData", menuName = "ScriptableObjects/OilLeakData", order = 1)]
public class OilLeakData : ScriptableObject
{
    public int emissionRate; // Rate of oil particle emission
    public int particlesEmitted; // Total particles emitted
    public int particlesEscaped; // Particles that reached the surface
    public int particlesBlocked; // Particles blocked by items

    public void ResetCounts()
    {
        particlesEmitted = 0;
        particlesEscaped = 0;
        particlesBlocked = 0;
    }
}
