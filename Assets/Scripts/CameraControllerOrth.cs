using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject boat; // Reference to the boat GameObject
    public Vector3 offset; // Offset distance between the camera and the boat
    public float verticalSpeed = 5.0f; // Speed for vertical camera movement

    [Header("Camera Return Settings")]
    public float returnDuration = 1.5f; // Time in seconds to complete return
    public AnimationCurve returnCurve; // Easing curve for return animation

    [Header("Zoom Settings")]
    public float zoomSpeed = 2.0f; // Speed for zooming the camera
    public float minZoom = 5.0f; // Minimum zoom level
    public float maxZoom = 20.0f; // Maximum zoom level

    // State tracking
    private Vector3 originalOffset; // To store the original offset
    private float returnElapsed = 0f; // Time elapsed during return
    private float returnStartY; // Y position when return started
    private float returnTargetY; // Target Y position for return
    private bool isReturning = false; // Are we currently returning to center?

    // Start is called before the first frame update
    void Start()
    {
        // Calculate the initial offset between the camera and the boat
        originalOffset = offset = transform.position - boat.transform.position;
        Camera.main.orthographicSize = 10; // Set the initial zoom level

        // Set default ease-in-out curve if none specified
        if (returnCurve == null || returnCurve.keys.Length == 0)
        {
            returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            Debug.Log("[CameraController] Using default ease-in-out curve for camera return");
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // ALWAYS lock camera X and Z to boat position
        // This ensures camera follows boat left/right instantly
        Vector3 currentPos = transform.position;
        currentPos.x = boat.transform.position.x + originalOffset.x;
        currentPos.z = boat.transform.position.z + originalOffset.z;

        // Handle vertical camera movement separately
        float verticalInput = Input.GetAxis("Vertical");

        // If vertical input is detected, move the camera up or down
        if (Mathf.Abs(verticalInput) > 0)
        {
            // Cancel any ongoing return
            if (isReturning)
            {
                isReturning = false;
            }

            // Only adjust Y position
            currentPos.y += verticalInput * verticalSpeed * Time.deltaTime;
        }
        else
        {
            // Start return if not already returning
            if (!isReturning)
            {
                StartVerticalReturn();
            }

            // Process vertical return animation
            if (isReturning)
            {
                currentPos.y = UpdateVerticalReturn();
            }
        }

        // Apply the final position
        transform.position = currentPos;

        // Handle zoom separately
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            Camera.main.orthographicSize -= scrollInput * zoomSpeed;
            Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);
        }
    }

    /// <summary>
    /// Start the vertical return animation (Y axis only)
    /// </summary>
    private void StartVerticalReturn()
    {
        returnStartY = transform.position.y;
        returnTargetY = boat.transform.position.y + originalOffset.y;
        returnElapsed = 0f;
        isReturning = true;
    }

    /// <summary>
    /// Update the vertical return animation (Y axis only)
    /// Returns the new Y position
    /// </summary>
    private float UpdateVerticalReturn()
    {
        // Update elapsed time
        returnElapsed += Time.deltaTime;

        // Calculate normalized progress (0 to 1)
        float t = Mathf.Clamp01(returnElapsed / returnDuration);

        // Apply easing curve
        float easedT = returnCurve.Evaluate(t);

        // Interpolate Y position only
        float newY = Mathf.LerpUnclamped(returnStartY, returnTargetY, easedT);

        // Check if return is complete
        if (t >= 1f)
        {
            // Snap to exact target Y
            newY = returnTargetY;
            isReturning = false;
        }

        return newY;
    }

}



