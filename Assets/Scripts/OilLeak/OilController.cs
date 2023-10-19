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
        if (gameState.roundState == RoundState.Over)
        {
            var emission = oilParticles.emission;
            emission.enabled = false;
            oilParticles.Clear(); // Clears all particles from the system
            return;
        }

        // If all particles have been emitted and all have either been blocked or escaped
        if (oilLeakData.particlesEmitted >= totalParticlesToEmit &&
            (oilLeakData.particlesBlocked + oilLeakData.particlesEscaped) >= totalParticlesToEmit)
        {
            // You can trigger any end-of-round logic specific to the OilController here
            // Note: The score calculation and other end-of-round updates are now in the GameController

            // If there are additional end-of-round actions specific to the OilController, add them here
        }
    }

    public void SetOilLeakData(OilLeakData data)
    {
        oilLeakData = data;
    }
    public void ResetOilSystem()
    {
        // Clear existing particles
        oilParticles.Clear();

        // Enable emission
        var emission = oilParticles.emission;
        emission.enabled = true;

        // Reset counts in OilLeakData
        oilLeakData.ResetCounts();
    }

}
