using UnityEngine;

public abstract class BaseCharacterController : MonoBehaviour
{
    public int teamID;
    [SerializeField] protected Transform handTransform; // assign in Inspector

    [HideInInspector] public Vector3 initialSpawnPosition;

    protected BallController _heldBall;

    public virtual void PickUpBallAtStart(BallController ball)
    {
        _heldBall = ball;
        _heldBall.PickUpByPlayerOrAI(handTransform, this);
    }


    public void OnBallLost()
    {
        _heldBall = null;
    }

    [SerializeField] private float catchRadius = 2f; // Adjustable catch radius

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Ball"))
        {
            BallController ball = other.GetComponent<BallController>();
            if (ball != null && !_heldBall && !ball.IsInMotion())
            {
                CatchBall(ball);
            }
        }
    }

    public bool AttemptCatch(BallController ball)
    {
        if (ball == null || _heldBall != null) return false;

        float centerAlignment = 1f - Mathf.Clamp01(Vector3.Distance(ball.transform.position, transform.position));
        float speedFactor = Mathf.Clamp01(1f - (ball.GetSpeed() / ball.MaxCatchableSpeed)); // Scale speed to catchability
        float randomnessFactor = Random.Range(0f, 1f);

        float catchProbability = centerAlignment * 0.5f + speedFactor * 0.3f + randomnessFactor * 0.2f;

        if (catchProbability > 0.7f) // Threshold for successful catch
        {
            CatchBall(ball);
            return true;
        }

        return false;
    }
    public void CatchBall(BallController ball)
    {
        if (ball == null || _heldBall != null) return;

        _heldBall = ball;
        ball.PickUpByPlayerOrAI(handTransform, this);
    }

}
