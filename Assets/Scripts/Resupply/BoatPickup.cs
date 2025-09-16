using UnityEngine;

/// <summary>
/// Handles boat collision with resupply crates using 3D physics
/// Add as child GameObject to boat with a trigger collider
/// </summary>
public class BoatPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private LayerMask pickupLayer;

    [Header("Effects")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private GameObject pickupEffectPrefab;

    private ResupplyManager resupplyManager;
    private AudioSource audioSource;
    private GameObject pickupTrigger;

    void Start()
    {
        // Find ResupplyManager
        resupplyManager = FindObjectOfType<ResupplyManager>();

        // Get or add AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Create child GameObject with trigger collider for pickups
        // This preserves the boat's main collider for physics
        SetupPickupTrigger();

        // Set pickupLayer to Pickups layer
        pickupLayer = LayerMask.GetMask("Pickups");
    }

    private void SetupPickupTrigger()
    {
        // Create a child object for the trigger zone
        pickupTrigger = new GameObject("PickupTrigger");
        pickupTrigger.transform.SetParent(transform);
        pickupTrigger.transform.localPosition = Vector3.zero;

        // Add sphere collider as trigger
        SphereCollider triggerCollider = pickupTrigger.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = pickupRadius;

        // Add this script to the trigger object to receive collision events
        PickupTriggerProxy proxy = pickupTrigger.AddComponent<PickupTriggerProxy>();
        proxy.Initialize(this);

        // Set layer to same as boat (not Surface, keep boat's layer)
        pickupTrigger.layer = gameObject.layer;
    }

    // Called by trigger proxy when entering a trigger
    public void OnPickupTriggerEnter(Collider other)
    {
        // Check if it's a pickup item on the Pickups layer
        if (((1 << other.gameObject.layer) & pickupLayer) != 0)
        {
            CollectCrate(other.gameObject, other);
        }
    }

    private void CollectCrate(GameObject crate, Collider collider)
    {
        // Notify ResupplyManager
        if (resupplyManager != null)
        {
            resupplyManager.OnCratePickup(crate, collider);
        }

        // Play pickup sound
        if (pickupSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }

        // Spawn pickup effect
        if (pickupEffectPrefab != null)
        {
            GameObject effect = Instantiate(pickupEffectPrefab, crate.transform.position, Quaternion.identity);
            Destroy(effect, 2f); // Clean up after 2 seconds
        }

        Debug.Log($"Collected resupply crate at {crate.transform.position}");
    }

    // Visual helper in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    // Inner class to handle trigger events on the child object
    private class PickupTriggerProxy : MonoBehaviour
    {
        private BoatPickup parent;

        public void Initialize(BoatPickup parent)
        {
            this.parent = parent;
        }

        void OnTriggerEnter(Collider other)
        {
            parent?.OnPickupTriggerEnter(other);
        }
    }
}