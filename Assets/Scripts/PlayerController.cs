using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("Ball Interaction")]
    [SerializeField] private Transform handTransform;   // "Hand" for holding the ball
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private KeyCode throwKey = KeyCode.Space;

    private Rigidbody _rb;
    private BallController _ballInRange;   
    private BallController _heldBall;      

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Movement
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0; // Ignore vertical tilt of the camera
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 movement = (forward * moveZ + right * moveX) * moveSpeed;
        _rb.velocity = new Vector3(movement.x, _rb.velocity.y, movement.z);


        // Attempt to pick up ball (if in range)
        if (Input.GetKeyDown(pickupKey))
        {
            TryPickUpBall();
        }

        // Throw the ball if we have it
        if (_heldBall != null && Input.GetKeyDown(throwKey))
        {
            // Example: throw forward relative to player's facing direction
            Vector3 throwDirection = transform.forward;

            _heldBall.ThrowBall(throwDirection, true); 
            _heldBall = null;
        }
    }

    private void TryPickUpBall()
    {
        if (_ballInRange != null && _heldBall == null)
        {
            _heldBall = _ballInRange;
            _heldBall.PickUpByPlayer(handTransform);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            _ballInRange = other.GetComponent<BallController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            if (_ballInRange == other.GetComponent<BallController>())
            {
                _ballInRange = null;
            }
        }
    }

    /// <summary>
    /// Called by GameManager at the start or after AI's unsuccessful throw
    /// </summary>
    public void PickUpBallAtStart(BallController ball)
    {
        _heldBall = ball;
        _heldBall.PickUpByPlayer(handTransform);
        Debug.Log("Player picked up the ball (via GameManager).");
    }
}
