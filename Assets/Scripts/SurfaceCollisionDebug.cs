using UnityEngine;

public class SurfaceCollisionDebug : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision detected with " + collision.gameObject.name);
    }

    void OnParticleCollision(GameObject other)
    {
        Debug.Log("Particle collision detected with " + other.name);
    }
}
