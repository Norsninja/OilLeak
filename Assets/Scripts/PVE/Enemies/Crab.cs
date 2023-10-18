using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Crab : EnemyBase
{
    public LayerMask itemLayer;
    public float eatDistance = 1.0f;
    public float detectionDistance = 10.0f;

    private NavMeshAgent agent;
    private List<Collider> detectedObjects = new List<Collider>();
    private bool isEating = false;
    public static List<Crab> AllCrabs = new List<Crab>();
    public float messageCooldown = 10f;
    private float lastMessageTime;
    public float messageDistance = 30f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        AllCrabs.Add(this);
    }
    void OnDestroy()
    {
        AllCrabs.Remove(this);
    }
    void Update()
    {
        // Update detected objects
        UpdateDetectedObjects("Item");

        // Move to the closest item if detected and not currently eating
        if (!isEating && MoveToTarget("Item"))
        {
            // Check if the crab is close enough to eat the item
            Collider closestItem = GetClosestItem("Item");
            if (closestItem != null && Vector3.Distance(transform.position, closestItem.transform.position) <= eatDistance)
            {
                StartCoroutine(EatItem(closestItem.gameObject));
            }
        }
        else
        {
            // If no items are detected, wander
            Wander();
        }
    }
    private Collider GetClosestItem(string targetTag)
    {
        float closestDistance = float.MaxValue;
        Collider closestItem = null;

        foreach (Collider hit in detectedObjects)
        {
            if (hit.CompareTag(targetTag))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestItem = hit;
                }
            }
        }

        return closestItem;
    }

    private void UpdateDetectedObjects(string targetTag)
    {
        detectedObjects.Clear();
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionDistance, itemLayer);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(targetTag))
            {
                detectedObjects.Add(hit);  // Add the Collider directly to the list
            }
        }
    }


    private bool MoveToTarget(string targetTag)
    {
        float closestDistance = float.MaxValue;
        Vector3? closestTargetPosition = null;

        foreach (Collider hit in detectedObjects)  // Note: We're iterating through Colliders now
        {
            if (hit.CompareTag(targetTag))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTargetPosition = hit.transform.position;
                }
            }
        }

        if (closestTargetPosition.HasValue)
        {
            agent.SetDestination(closestTargetPosition.Value);
            return true;
        }

        return false;
    }
    public void BroadcastFoodFound(Vector3 foodPosition)
    {
        foreach (Crab crab in AllCrabs)
        {
            if (Vector3.Distance(transform.position, crab.transform.position) <= messageDistance)
            {
                crab.ReceiveFoodFoundMessage(foodPosition);
            }
        }
    }
    void OnDrawGizmosSelected()
    {
        // Draw a wire sphere to represent the message distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, messageDistance);
    }
    public void ReceiveFoodFoundMessage(Vector3 foodPosition)
    {
        if (Time.time - lastMessageTime < messageCooldown)
        {
            return;
        }

        lastMessageTime = Time.time;

        // Check if the food is still available
        RaycastHit hit;
        if (Physics.Raycast(transform.position, (foodPosition - transform.position).normalized, out hit, detectionDistance, itemLayer))
        {
            if (hit.collider.CompareTag("Item"))
            {
                // Only change target if the new food source is closer
                if (Vector3.Distance(transform.position, foodPosition) < Vector3.Distance(transform.position, agent.destination))
                {
                    // Update the crab's target to the food position
                    agent.SetDestination(foodPosition);
                }
            }
        }
    }
    private IEnumerator EatItem(GameObject item)
    {
        if (item != null)
        {
            // Broadcast the food found message
            BroadcastFoodFound(item.transform.position);

            GameObject root = item.transform.root.gameObject;  // Get the root GameObject
            Destroy(root);  // Destroy the root, effectively destroying the whole prefab

            isEating = true;
            yield return new WaitForSeconds(10f);
            isEating = false;
        }
    }



    private void Wander()
    {
        if (agent.pathPending || agent.remainingDistance > 0.1f)
            return;

        Vector3 randomDestination = RandomNavSphere(transform.position, 20, -1);
        agent.SetDestination(randomDestination);
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float distance, int layermask)
    {
        Vector3 randomDirection = Random.insideUnitSphere * distance;
        randomDirection += origin;
        UnityEngine.AI.NavMeshHit navHit;
        UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out navHit, distance, layermask);
        return navHit.position;
    }
}
