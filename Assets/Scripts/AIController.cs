using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AIController : MonoBehaviour
{
    private enum AIState { SeekBall, Attack, Dodge }

    [Header("AI Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float dodgeSpeed = 6f;
    [SerializeField] private float repositionRadius = 3f; // Radius for random repositioning

    [Header("References")]
    [SerializeField] private Transform playerTransform;   // The player's transform
    [SerializeField] private Transform handTransform;     // AI's "hand"

    private Rigidbody _rb;
    private AIState _currentState;
    private BallController _heldBall;

    private float _nextThrowTime;
    [SerializeField] private float throwCooldown = 2f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _currentState = AIState.SeekBall;
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
            case AIState.Dodge:
                DodgeBehavior();
                break;
        }

        if (ShouldDodge())
        {
            _currentState = AIState.Dodge;
            Debug.Log("Test");
        }
    }

    private void SeekBallBehavior()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        // Random movement
        Vector3 randomDirection = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ).normalized;

        Vector3 targetPosition = transform.position + randomDirection * 2f; // Move a small distance randomly
        MoveTowards(targetPosition, moveSpeed);
    }

    private void AttackBehavior()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.SeekBall;
            return;
        }

        // Face the player and move toward them slightly
        Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
        Vector3 repositionOffset = Vector3.Cross(toPlayer, Vector3.up).normalized * Random.Range(-1f, 1f);
        Vector3 targetPosition = transform.position + repositionOffset;

        MoveTowards(targetPosition, moveSpeed);

        // Throw at intervals
        if (Time.time >= _nextThrowTime)
        {
            ThrowBallAtPlayer();
            _nextThrowTime = Time.time + throwCooldown;
        }
    }

    private void MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        _rb.velocity = new Vector3(direction.x * speed, _rb.velocity.y, direction.z * speed);
    }

    private void DodgeBehavior()
    {
        // Side-step with more variability
        Vector3 dodgeDirection = Vector3.Cross(Vector3.up, (playerTransform.position - transform.position)).normalized;

        // Add randomness to dodge movement
        dodgeDirection += new Vector3(
            Random.Range(-0.5f, 0.5f),
            0f,
            Random.Range(-0.5f, 0.5f)
        ).normalized;

        _rb.velocity = dodgeDirection * dodgeSpeed;

        // Revert to prior state after a short time
        Invoke(nameof(ReturnToPriorState), 0.5f);
    }
    private bool ShouldDodge()
    {
        // Check if the player is holding the ball
        BallController playerHeldBall = playerTransform.GetComponentInChildren<BallController>();
        if (playerHeldBall == null)
        {
            Debug.Log("Player is not holding the ball, no dodge needed.");
            return false; // Player is not holding the ball
        }

        // Calculate the distance between the player and the AI
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        Debug.Log($"Distance to Player: {distanceToPlayer}");

        if (distanceToPlayer > 13f)
        {
            Debug.Log("Player is too far, no dodge needed.");
            return false; // Player is too far away
        }

        Debug.Log("ShouldDodge() returned false");
        return true;
    }

    private void ReturnToPriorState()
    {
        if (_heldBall != null) _currentState = AIState.Attack;
        else _currentState = AIState.SeekBall;
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

    public void PickUpBallAtStart(BallController ball)
    {
        _heldBall = ball;
        _heldBall.PickUpByAI(handTransform);
        _currentState = AIState.Attack;
        Debug.Log("AI picked up the ball (via GameManager).");
    }

    private bool ballRbIsKinematic(BallController ball)
    {
        return ball.GetComponent<Rigidbody>().isKinematic;
    }
}
