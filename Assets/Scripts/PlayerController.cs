using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : BaseCharacterController
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private KeyCode throwKey = KeyCode.Space;
    [SerializeField] private KeyCode passKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode catchKey = KeyCode.F;  // used for catching thrown balls

    private Rigidbody _rb;
    private Vector3 _movement;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Optionally freeze rotation so the physics simulation doesn't rotate your player unexpectedly.
        _rb.freezeRotation = true;
    }

    /// <summary>
    /// On collision, if the ball is on the ground, automatically take it.
    /// (This represents "taking the ball" by touching it.)
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
        {
            BallController ball = collision.gameObject.GetComponent<BallController>();
            // Automatically take the ball if it is on the ground (not in motion) and not already held.
            if (ball != null && _heldBall == null && !ball.IsInMotion())
            {
                Debug.Log("Player takes the ball by touching it on the ground.");
                CatchBall(ball);
            }
        }
    }

    private void Update()
    {
        HandleMovementInput();
        HandleThrowInput();
        HandleCatchInput(); // For thrown ball catch
    }

    private void HandleMovementInput()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Align movement with the camera's forward direction.
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        _movement = (forward * moveZ + right * moveX) * moveSpeed;
    }

    private void HandleThrowInput()
    {
        if (_heldBall != null)
        {
            if (Input.GetKeyDown(throwKey))
            {
                Vector3 throwDir = transform.forward;
                _heldBall.ThrowBall(throwDir);
            }
            else if (Input.GetKeyDown(passKey))
            {
                BaseCharacterController teammate = FindNearestTeammate();
                if (teammate != null)
                {
                    Vector3 passDir = (teammate.transform.position - transform.position).normalized;
                    _heldBall.ThrowBall(passDir);
                }
            }
        }
    }

    /// <summary>
    /// When the catch key (F) is pressed, try to catch a thrown ball.
    /// (This is the "catch" action, which is only for thrown balls.)
    /// </summary>
    private void HandleCatchInput()
    {
        if (Input.GetKeyDown(catchKey))
        {
            TryCatchThrownBall();
        }
    }

    private void FixedUpdate()
    {
        // Apply horizontal movement while preserving vertical velocity (gravity).
        Vector3 currentVelocity = _rb.velocity;
        Vector3 desiredVelocity = new Vector3(_movement.x, currentVelocity.y, _movement.z);
        _rb.velocity = desiredVelocity;
    }

    /// <summary>
    /// Try to catch a ball that is in motion (thrown at you).
    /// </summary>
    private void TryCatchThrownBall()
    {
        // Only consider balls that are in motion (i.e. thrown).
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, catchRadius);
        bool caught = false;
        foreach (var collider in hitColliders)
        {
            if (collider.CompareTag("Ball"))
            {
                BallController ball = collider.GetComponent<BallController>();
                if (ball != null && _heldBall == null && ball.IsInMotion())
                {
                    if (AttemptCatch(ball))
                    {
                        Debug.Log("Player caught the thrown ball!");
                        CatchBall(ball);
                        caught = true;
                        break;
                    }
                }
            }
        }
        if (!caught)
            Debug.Log("No thrown ball caught!");
    }

    private BaseCharacterController FindNearestTeammate()
    {
        float closestDist = Mathf.Infinity;
        BaseCharacterController nearest = null;
        BaseCharacterController[] allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c == this || c.teamID != this.teamID)
                continue;
            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }

    /// <summary>
    /// For players, the catch action (when pressing F) is used to catch thrown balls.
    /// The probabilistic catch logic is applied here.
    /// </summary>
    public override bool AttemptCatch(BallController ball)
    {
        if (ball == null || IsHoldingBall()) return false;
        
        // Compute catch probability based on distance, speed, and angle.
        float distance = Vector3.Distance(ball.transform.position, transform.position);
        float distanceFactor = Mathf.Clamp01(1f - (distance / catchRadius));
        float speed = ball.GetSpeed();
        float speedFactor = Mathf.Clamp01(1f - (speed / ball.MaxCatchableSpeed));
        Vector3 ballDirection = (ball.transform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, ballDirection);
        float angleFactor = Mathf.Clamp01(1f - (angle / 90f));
        float randomnessFactor = Random.Range(0.8f, 1.2f);
        float catchProbability = (distanceFactor * 0.4f) +
                                 (speedFactor * 0.3f) +
                                 (angleFactor * 0.3f);
        catchProbability *= randomnessFactor;
        Debug.Log($"Catch Probability: {catchProbability} (Dist: {distanceFactor}, Speed: {speedFactor}, Angle: {angleFactor})");
        return (catchProbability > 0.7f);
    }
}
