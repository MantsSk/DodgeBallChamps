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
    protected float _nextThrowTime;                             // earliest time we can throw again

    public virtual void PickUpBallAtStart(BallController ball)
    {
        _heldBall = ball;
        _heldBall.PickUpByPlayerOrAI(handTransform, this);

        // Force a delay before the character can throw
        _nextThrowTime = Time.time + throwDelayAfterPickup;
    }

    /// <summary>
    /// Whether this character currently holds the ball
    /// </summary>
    public bool IsHoldingBall()
    {
        return _heldBall != null;
    }

    /// <summary>
    /// Called by the ball when the character loses possession
    /// </summary>
    public void OnBallLost()
    {
        _heldBall = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
        {
            BallController ball = collision.gameObject.GetComponent<BallController>();
            if (ball != null && _heldBall == null && !ball.IsInMotion())
            {
                CatchBall(ball);
            }
        }
    }

    /// <summary>
    /// Attempt a probabilistic catch, if we're not already holding the ball
    /// </summary>
    public virtual bool AttemptCatch(BallController ball)
    {
        if (ball == null || _heldBall != null) return false;

        // Calculate factors for catch probability
        float distance = Vector3.Distance(ball.transform.position, transform.position);
        float distanceFactor = Mathf.Clamp01(1f - (distance / catchRadius)); // Closer is better

        float speed = ball.GetSpeed();
        float speedFactor = Mathf.Clamp01(1f - (speed / ball.MaxCatchableSpeed)); // Slower is better

        Vector3 ballDirection = (ball.transform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, ballDirection);
        float angleFactor = Mathf.Clamp01(1f - (angle / 90f)); // Directly in front is better

        // Add some randomness to make catching less predictable
        float randomnessFactor = Random.Range(0.8f, 1.2f);

        // Combine factors to calculate catch probability
        float catchProbability = distanceFactor * 0.4f 
                               + speedFactor * 0.3f 
                               + angleFactor * 0.3f;
        catchProbability *= randomnessFactor;

        // Debugging output to see catch probability
        Debug.Log($"Catch Probability: {catchProbability} " +
                  $"(Distance: {distanceFactor}, Speed: {speedFactor}, Angle: {angleFactor})");

        return (catchProbability > 0.7f); // success threshold
    }
    
    /// <summary>
    /// Actually pick up the ball (assumes we already passed AttemptCatch)
    /// </summary>
    public void CatchBall(BallController ball)
    {
        if (ball == null || _heldBall != null) return;

        _heldBall = ball;
        ball.PickUpByPlayerOrAI(handTransform, this);

        // Also apply the throw delay after catching
        _nextThrowTime = Time.time + throwDelayAfterPickup;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, catchRadius);
    }
}
