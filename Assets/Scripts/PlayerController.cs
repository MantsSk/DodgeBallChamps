using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : BaseCharacterController
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private KeyCode throwKey = KeyCode.Space;
    [SerializeField] private KeyCode passKey = KeyCode.LeftShift;

    private Rigidbody _rb;
    private Vector3 _movement;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Get input
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Align movement with the camera's forward direction
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        // Flatten the vectors to ignore vertical camera tilt
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Combine the input direction with camera orientation
        _movement = (forward * moveZ + right * moveX) * moveSpeed;

        if (_heldBall != null)
        {
            if (Input.GetKeyDown(throwKey))
            {
                // Throw forward relative to the player
                Vector3 throwDir = transform.forward;
                _heldBall.ThrowBall(throwDir);
            }
            else if (Input.GetKeyDown(passKey))
            {
                // Pass to nearest teammate
                BaseCharacterController teammate = FindNearestTeammate();
                if (teammate != null)
                {
                    Vector3 passDir = (teammate.transform.position - transform.position).normalized;
                    _heldBall.ThrowBall(passDir);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        // Move in FixedUpdate
        _rb.velocity = new Vector3(_movement.x, _rb.velocity.y, _movement.z);
    }

    private BaseCharacterController FindNearestTeammate()
    {
        // Same logic as before
        float closestDist = Mathf.Infinity;
        BaseCharacterController nearest = null;
        var allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c == this) continue;
            if (c.teamID != this.teamID) continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }
}
