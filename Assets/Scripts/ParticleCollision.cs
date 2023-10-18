using UnityEngine;

public class ParticleCollision : MonoBehaviour
{
    new private ParticleSystem particleSystem;
    private ParticleSystem.Particle[] particles;

    void Start()
    {
        particleSystem = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];
    }

    void OnParticleCollision(GameObject other)
    {
        if (other.layer == LayerMask.NameToLayer("Surface"))
        {
            int numParticles = particleSystem.GetParticles(particles);

            for (int i = 0; i < numParticles; i++)
            {
                particles[i].remainingLifetime = 0;
            }

            particleSystem.SetParticles(particles, numParticles);
        }
    }
}

