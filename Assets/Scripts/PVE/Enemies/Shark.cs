using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shark : MonoBehaviour
{
    public GameObject waterSurface; // the water surface
    public GameObject seaFloor; // the sea floor

    public Vector3 pos; 
    public Vector3 currentPos; 
    public Vector3 center; 
        public Vector3 rotationOffset = Vector3.zero;

    [SerializeField] [Range(0, 100)] private float swimRange; 

    [SerializeField] private bool isSwimming; 
    private float t; 

    // Add these variables to set the range for the X and Z axes
    public float minX;
    public float maxX;
    public float minZ;
    public float maxZ;

    void Start()
    {
        center = transform.position;
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + rotationOffset);
    }

    void Update()
    {
        if(isSwimming == false) 
        {
            t = 0;
            currentPos = transform.position;
            float minY = Mathf.Max(center.y - swimRange, seaFloor.transform.position.y);
            float maxY = Mathf.Min(center.y + swimRange, waterSurface.transform.position.y);
            pos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), Random.Range(minZ, maxZ));
            isSwimming = true;
            StartCoroutine(SwimToTarget()); 
        }
    }

    IEnumerator SwimToTarget()
    {
        float swimSpeed = 1f; // Adjust this value to change the shark's maximum speed
        float turnSpeed = 2f; // Adjust this value to change the shark's turning speed

        while (Vector3.Distance(transform.position, pos) > 0.1f)
        {
            // Gradually rotate the shark towards its target
            Vector3 targetDirection = pos - transform.position;
            float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0) * Quaternion.Euler(rotationOffset);
            while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                yield return null;
            }

            t += Time.deltaTime / 6 * swimSpeed; // Multiply by swimSpeed to control the shark's speed
            transform.position = Vector3.Lerp(currentPos, pos, t); 
            if(transform.position.y > waterSurface.transform.position.y || transform.position.y < seaFloor.transform.position.y) 
            {
                break;
            }
            yield return null;
        }

        isSwimming = false;
    }
    void OnDrawGizmosSelected()
    {
        // Draw a wire sphere to represent the swim range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, swimRange);
    }
}