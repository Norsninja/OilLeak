using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject boat; // Reference to the boat GameObject
    public Vector3 offset; // Offset distance between the camera and the boat
    public float verticalSpeed = 5.0f; // Speed for vertical camera movement
    public float returnSpeed = 2.0f; // Speed for returning to the boat
    public float zoomSpeed = 2.0f; // Speed for zooming the camera
    public float minZoom = 5.0f; // Minimum zoom level
    public float maxZoom = 20.0f; // Maximum zoom level
    private Vector3 originalOffset; // To store the original offset

    // Start is called before the first frame update
    void Start()
    {
        // Calculate the initial offset between the camera and the boat
        originalOffset = offset = transform.position - boat.transform.position;
        Camera.main.orthographicSize = 10; // Set the initial zoom level
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Capture vertical input ("W" and "S" keys)
        float verticalInput = Input.GetAxis("Vertical");

        // Capture scroll wheel input
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        // Zoom the camera based on scroll wheel input
        Camera.main.orthographicSize -= scrollInput * zoomSpeed;
        Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);

        // If vertical input is detected, move the camera up or down
        if (Mathf.Abs(verticalInput) > 0)
        {
            Vector3 verticalMovement = new Vector3(0, verticalInput * verticalSpeed * Time.deltaTime, 0);
            transform.position += verticalMovement;
        }
        else
        {
            // Instead of returning to the original vertical position,
            // maintain the current vertical position while following the boat
            Vector3 targetPosition = boat.transform.position + new Vector3(originalOffset.x, transform.position.y - boat.transform.position.y, originalOffset.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * returnSpeed);
        }
    }

}



