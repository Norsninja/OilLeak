using UnityEngine;

public class OilController : MonoBehaviour
{
    public OilLeakData oilLeakData;
    public GameState gameState; // Add a reference to GameState
    public int totalParticlesToEmit;
    public ParticleSystem oilParticles;

    void Start()
    {
        oilParticles = GetComponent<ParticleSystem>();
        var emission = oilParticles.emission;
        emission.rateOverTime = oilLeakData.emissionRate;
        oilLeakData.ResetCounts();
    }

    void Update()
    {
        if (gameState.isRoundOver)
        {
            var emission = oilParticles.emission;
            emission.enabled = false;
            oilParticles.Clear(); // Clears all particles from the system
            return;
        }
        if (oilLeakData.particlesEmitted >= totalParticlesToEmit &&
            (oilLeakData.particlesBlocked + oilLeakData.particlesEscaped) >= totalParticlesToEmit)
        {
            float score = (float) oilLeakData.particlesBlocked / totalParticlesToEmit * 100;
            // Trigger end-of-round logic
        }
    }
}
