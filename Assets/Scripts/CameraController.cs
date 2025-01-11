using UnityEngine;

public class CameraController : MonoBehaviour{

    [Header("Camera Positioning")]
    [SerializeField] private Vector3 offset = new Vector3(0, 10f, -5f);
    // Adjust this to move the camera higher or further behind the player
    // Example: (0, 10, -5) means the camera is 10 units above the player and 5 units behind on the Z-axis.

    [Header("Smooth Follow")]
    [SerializeField] private float followSpeed = 0.15f;
    // Lower values -> more "lag", higher values -> snappier movement.

    [Header("Look Settings")]
    [SerializeField] private bool lookAtPlayer = true;
    // If true, the camera will tilt/rotate to face the player.

    public void SetCameraTarget(GameObject playerTransform)
    {
        if (playerTransform == null) return;

        // Desired camera position = player's position + offset
        Vector3 desiredPosition = playerTransform.transform.position + offset;

        // Smoothly interpolate from current position to desired position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, followSpeed);
        transform.position = smoothedPosition;

        // Optionally rotate to look at the player
        if (lookAtPlayer)
        {
            transform.LookAt(playerTransform.transform.position);
            Debug.Log("Camera is looking at player");
        }
    }
}
