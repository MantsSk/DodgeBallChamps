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
}
