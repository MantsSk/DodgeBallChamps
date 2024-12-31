using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AIController : MonoBehaviour
{
    private enum AIState { SeekBall, Attack, Dodge }

    [Header("AI Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float dodgeSpeed = 6f;

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
        }
    }

    private void SeekBallBehavior()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        // For simplicity, the AI won't pick up the ball unless it physically collides.
        // The OnTriggerEnter logic would be needed to auto-pickup. 
        // You can add that or do a "find ball" approach.
        _rb.velocity = Vector3.zero;
    }

    private void AttackBehavior()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.SeekBall;
            return;
        }

        // Face the player, move a bit
        Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
        _rb.velocity = new Vector3(toPlayer.x * moveSpeed, _rb.velocity.y, toPlayer.z * moveSpeed);

        // Throw at intervals
        if (Time.time >= _nextThrowTime)
        {
            ThrowBallAtPlayer();
            _nextThrowTime = Time.time + throwCooldown;
        }
    }

    private void DodgeBehavior()
    {
        // Simple side-step
        Vector3 dodgeDirection = Vector3.Cross(Vector3.up, (playerTransform.position - transform.position)).normalized;
        _rb.velocity = dodgeDirection * dodgeSpeed;

        // Revert after a short time
        Invoke(nameof(ReturnToPriorState), 0.5f);
    }

    private bool ShouldDodge()
    {
        // Very simplified logic: Not holding the ball, the player's ball is in flight, etc.
        // Implementation depends on your preference.
        return false;  // Stub for demonstration
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
            // If it's free (not in player's hands), pick it up
            if (!ballRbIsKinematic(ball))
            {
                _heldBall = ball;
                _heldBall.PickUpByAI(handTransform);
                _currentState = AIState.Attack;
            }
        }
    }

    /// <summary>
    /// Called by GameManager at the start or after Player's missed throw
    /// </summary>
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
