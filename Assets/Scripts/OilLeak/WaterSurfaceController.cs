using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSurfaceController : MonoBehaviour
{
    public OilLeakData oilLeakData;  // Reference to the OilLeakData ScriptableObject
    public AudioClip bubbleSound; // The sound of a bubble
    public GameObject bubblePopPrefab; // The prefab of the bubble pop Particle System
    private AudioSource audioSource; // Source of the sound
    private List<ParticleCollisionEvent> collisionEvents; // List to hold the collision events

    void Start()
    {
        // Initialize if needed
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) // if AudioSource is not attached
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 1f; // Set the audio source to fully 3D
        audioSource.rolloffMode = AudioRolloffMode.Custom; // Set the rolloff mode to logarithmic
        audioSource.maxDistance = 8f; // Set the max distance to 8

        collisionEvents = new List<ParticleCollisionEvent>(); // Initialize the collision events list
    }

    void OnParticleCollision(GameObject other)
    {
        // Check if the collided object is on the "OilSpill" layer
        if (other.layer == LayerMask.NameToLayer("OilSpill"))
        {
            // Increase the count of escaped particles
            oilLeakData.particlesEscaped++;
            // Debug.Log("Escaped to the WaterSurface: " + oilLeakData.particlesEscaped);

            // Notify DifficultyManager for rubber band system
            if (DifficultyManager.Instance != null)
            {
                DifficultyManager.Instance.OnParticleEscaped(1);
            }

            // Play the bubble sound with a varied pitch
            audioSource.pitch = Random.Range(0.8f, 1.2f); // Change the pitch randomly between 0.8 and 1.2

            // Get the collision events
            ParticleSystem particleSystem = other.GetComponent<ParticleSystem>();
            ParticlePhysicsExtensions.GetCollisionEvents(particleSystem, gameObject, collisionEvents);

            // For each collision event, instantiate the bubble pop effect at the collision point
            foreach (ParticleCollisionEvent collisionEvent in collisionEvents)
            {
                // Instantiate the bubble pop effect at the location of the particle collision
                GameObject popEffect = Instantiate(bubblePopPrefab, collisionEvent.intersection, Quaternion.Euler(0, 0, 0));
                
                // Start a coroutine to destroy the effect after it finishes playing
                StartCoroutine(DestroyAfterDelay(popEffect, popEffect.GetComponent<ParticleSystem>().main.duration + 0.5f));
                AudioSource.PlayClipAtPoint(bubbleSound, collisionEvent.intersection, Random.Range(0.8f, 1.2f));
            }
        }
    }
    // Water surface has arigidbody excluding Items layer so items can not be detected, but this was the idea:
    // void OnCollisionEnter(Collision collision)
    // {
    //     Debug.Log("Item crossed surface plane... ");
    //     // Check if the collided object is on the "Items" layer
    //     if (collision.gameObject.layer == LayerMask.NameToLayer("Items"))
    //     {
    //         // Play the bubble sound with a varied pitch
    //         audioSource.pitch = Random.Range(0.8f, 1.2f); // Change the pitch randomly between 0.8 and 1.2
    //         AudioSource.PlayClipAtPoint(bubbleSound, collision.transform.position, audioSource.pitch);

    //         // Instantiate the bubble pop effect at the location of the collision
    //         GameObject popEffect = Instantiate(bubblePopPrefab, collision.transform.position, Quaternion.Euler(-90, 0, 0));

    //         // Start a coroutine to destroy the effect after it finishes playing
    //         StartCoroutine(DestroyAfterDelay(popEffect, popEffect.GetComponent<ParticleSystem>().main.duration));
    //     }
    // }
    IEnumerator DestroyAfterDelay(GameObject objectToDestroy, float delay)
    {
        // Wait for the delay
        yield return new WaitForSeconds(delay);

        // Then destroy the object
        Destroy(objectToDestroy);
    }
}
