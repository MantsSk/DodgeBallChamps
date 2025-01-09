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
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        _movement = new Vector3(moveX, 0, moveZ) * moveSpeed;

        if (_heldBall != null)
        {
            if (Input.GetKeyDown(throwKey))
            {
                // Throw forward
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
