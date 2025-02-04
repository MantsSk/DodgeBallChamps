using UnityEngine;

public abstract class BaseCharacterController : MonoBehaviour
{
    public int teamID;
    [SerializeField] protected Transform handTransform; // assign in Inspector
    [HideInInspector] public Vector3 initialSpawnPosition;
    protected BallController _heldBall;
    [SerializeField] protected float catchRadius = 2f;

    [Header("Throw Timing")]
    [SerializeField] private float throwDelayAfterPickup = 2f;  // how long to wait after picking up/catching a ball
    protected float _nextThrowTime;  // earliest time we can throw again

    public virtual void PickUpBallAtStart(BallController ball)
    {
        _heldBall = ball;
        _heldBall.PickUpByPlayerOrAI(handTransform, this);
        _nextThrowTime = Time.time + throwDelayAfterPickup;
    }

    /// <summary>
    /// Whether this character is currently holding a ball.
    /// </summary>
    public bool IsHoldingBall()
    {
        return _heldBall != null;
    }

    /// <summary>
    /// Called by the ball when the character loses possession.
    /// </summary>
    public void OnBallLost()
    {
        _heldBall = null;
    }

    /// <summary>
    /// Auto-catch on collision for non-player characters.
    /// (Players will override this to prevent automatic pickup.)
    /// </summary>
    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
        {
            BallController ball = collision.gameObject.GetComponent<BallController>();
            // Only auto-catch if the ball is on the ground (not in motion)
            if (ball != null && _heldBall == null && !ball.IsInMotion())
            {
                CatchBall(ball);
            }
        }
    }

    /// <summary>
    /// Attempt a probabilistic catch (used by AI).
    /// </summary>
    public virtual bool AttemptCatch(BallController ball)
    {
        if (ball == null || _heldBall != null) return false;

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
    
    /// <summary>
    /// Actually pick up the ball (assuming a successful catch).
    /// </summary>
    public void CatchBall(BallController ball)
    {
        if (ball == null || _heldBall != null) return;
        _heldBall = ball;
        ball.PickUpByPlayerOrAI(handTransform, this);
        _nextThrowTime = Time.time + throwDelayAfterPickup;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, catchRadius);
    }
}
