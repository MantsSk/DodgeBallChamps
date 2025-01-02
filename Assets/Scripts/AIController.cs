using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AIController : MonoBehaviour
{
    private enum AIState { SeekBall, Attack, Dodge }

    [Header("AI Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float dodgeSpeed = 6f;
    [SerializeField] private float constantMoveChangeInterval = 2f; // Interval to change movement direction

    [Header("References")]
    [SerializeField] private Transform playerTransform;   // The player's transform
    [SerializeField] private Transform handTransform;     // AI's "hand"

    private Rigidbody _rb;
    private AIState _currentState;
    private BallController _heldBall;

    private Vector3 _constantMoveDirection; // Direction for constant movement
    private float _nextMoveChangeTime;

    private float _nextThrowTime;

    [SerializeField] private float moveRange = 0.5f; // Range of movement around its starting position
    [SerializeField] private bool pingPongMovement = true; // Toggle for ping pong movement
    private Vector3 _startPosition; // Store the AI's starting position
    private bool _movingRight = true; // Direction toggle for ping pong movement
    [SerializeField] private float throwCooldown = 2f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _currentState = AIState.SeekBall;
        ChangeConstantMoveDirection(); // Initialize the movement direction
        _startPosition = transform.position; // Store starting position
    }

    private void FixedUpdate()
    {
        switch (_currentState)
        {
            case AIState.SeekBall:
                SeekBallBehavior();
                break;
            case AIState.Attack:
                AttackBehavior();
                break;
        //     case AIState.Dodge:
        //         DodgeBehavior();
        //         break;
        }

        // Add constant movement regardless of state
        ApplyConstantMovement();

        if (ShouldDodge())
        {
            _currentState = AIState.Dodge;
        }
    }

    private void SeekBallBehavior()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        // AI will move randomly while seeking the ball
        Debug.Log("AI is seeking the ball.");
    }

    private void AttackBehavior()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.SeekBall;
            return;
        }

        // AI moves toward the player while preparing to throw
        Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
        _rb.velocity = toPlayer * moveSpeed;

        // Throw at intervals
        if (Time.time >= _nextThrowTime)
        {
            ThrowBallAtPlayer();
            _nextThrowTime = Time.time + throwCooldown;
        }
    }

    // private void DodgeBehavior()
    // {
    //     Vector3 dodgeDirection = Vector3.Cross(Vector3.up, (playerTransform.position - transform.position)).normalized;
    //     _rb.velocity = dodgeDirection * dodgeSpeed;

    //     Invoke(nameof(ReturnToPriorState), 0.5f);
    // }

    private void ApplyConstantMovement()
    {
        if (Time.time >= _nextMoveChangeTime)
        {
            ChangeConstantMoveDirection();
        }

        // Apply movement based on the constant move direction
        transform.Translate(_constantMoveDirection * moveSpeed * Time.fixedDeltaTime);

        Debug.Log($"AI Position: {transform.position}");
    }

    private void ChangeConstantMoveDirection()
    {
        if (pingPongMovement)
        {
            // Calculate the distance from the starting position
            float distanceFromStart = Vector3.Distance(transform.position, _startPosition);

            // If AI reaches the move range, toggle direction and reset start position
            if (distanceFromStart >= moveRange)
            {
                _movingRight = !_movingRight; // Toggle direction
                _startPosition = transform.position; // Reset start position
            }

            // Ping pong movement direction
            _constantMoveDirection = _movingRight 
                ? new Vector3(1f, 0f, 0f) // Move right
                : new Vector3(-1f, 0f, 0f); // Move left
        }
        else
        {
            // Default random movement
            _constantMoveDirection = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized;
        }

        _nextMoveChangeTime = Time.time + constantMoveChangeInterval;
        Debug.Log($"AI changed direction: {_constantMoveDirection}, Start Pos: {_startPosition}");
    }

    private bool ShouldDodge()
    {
        // Check if the player is holding the ball
        BallController playerHeldBall = playerTransform.GetComponentInChildren<BallController>();
        if (playerHeldBall == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        Vector3 toAI = (transform.position - playerTransform.position).normalized;
        float facingDot = Vector3.Dot(playerTransform.forward, toAI);

        if (distanceToPlayer < 5f && facingDot > 0.7f)
        {
            return Random.value < 0.8f;
        }

        return false;
    }

    private void ReturnToPriorState()
    {
        _currentState = _heldBall != null ? AIState.Attack : AIState.SeekBall;
    }

    private void ThrowBallAtPlayer()
    {
        if (_heldBall == null) return;

        Vector3 direction = (playerTransform.position - transform.position).normalized;
        _heldBall.ThrowBall(direction, false);
        _heldBall = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball") && _heldBall == null)
        {
            BallController ball = other.GetComponent<BallController>();
            if (!ballRbIsKinematic(ball))
            {
                _heldBall = ball;
                _heldBall.PickUpByAI(handTransform);
                _currentState = AIState.Attack;
            }
        }
    }

    private bool ballRbIsKinematic(BallController ball)
    {
        return ball.GetComponent<Rigidbody>().isKinematic;
    }

    public void PickUpBallAtStart(BallController ball)
    {
        _heldBall = ball;
        _heldBall.PickUpByAI(handTransform);
        _currentState = AIState.Attack;
    }
}
