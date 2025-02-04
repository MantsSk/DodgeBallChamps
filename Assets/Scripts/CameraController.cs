using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Positioning")]
    [SerializeField] private Vector3 offset = new Vector3(0, 10f, -5f);
    [Header("Smooth Follow")]
    [SerializeField] private float followSpeed = 0.15f;
    [Header("Look Settings")]
    [SerializeField] private bool lookAtPlayer = true;

    public void SetCameraTarget(GameObject playerTransform)
    {
        if (playerTransform == null)
            return;
        Vector3 desiredPosition = playerTransform.transform.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, followSpeed);
        transform.position = smoothedPosition;
        if (lookAtPlayer)
        {
            transform.LookAt(playerTransform.transform.position);
            Debug.Log("Camera is looking at player");
        }
    }
}
