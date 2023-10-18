using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderTest : MonoBehaviour
{
    public RagdollController ragdollController;  // Set this in the inspector

    void OnParticleCollision(GameObject other)
    {
        Debug.Log("OnParticleCollision triggered with: " + other.name);
        ragdollController.HandleParticleCollision(other);
    }

}
