using System.Collections;
using UnityEngine;

public class BallController : MonoBehaviour
{
    [SerializeField] private float throwForce = 15f;
    [SerializeField] private float minHitVelocity = 3f;
    [SerializeField] private float throwTimeout = 2f;
    [SerializeField] private float maxCatchableSpeed = 10f;
    public float MaxCatchableSpeed => maxCatchableSpeed;

    private Rigidbody _rb;
    private bool _isHeld;
    private BaseCharacterController _currentHolder;

    private float _throwTimer;
    private bool _throwTimerActive;
    private bool _hasHitOnThisThrow;
    private bool _isInMotion = false;
    
    // Who last threw or held the ball
    private BaseCharacterController _lastHolder; 

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (_throwTimerActive)
        {
            _throwTimer -= Time.deltaTime;
            if (_throwTimer <= 0f && !_hasHitOnThisThrow)
            {
                _throwTimerActive = false;
                // Handle a missed throw
                StartCoroutine(DelayBallAssignment());
            }
        }
    }

    public void PickUpByPlayerOrAI(Transform holderTransform, BaseCharacterController holder)
    {
        _isHeld = true;
        _currentHolder = holder;
        _lastHolder = holder; // update last holder

        _rb.isKinematic = true;
        _rb.velocity = Vector3.zero;

        transform.SetParent(holderTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        _hasHitOnThisThrow = false;
        _throwTimerActive = false;
        _isInMotion = false;
    }

    public void ThrowBall(Vector3 direction)
    {
        if (!_isHeld || _currentHolder == null) return;

        _isHeld = false;
        transform.SetParent(null);

        _rb.isKinematic = false;
        _rb.velocity = Vector3.zero;
        _rb.AddForce(direction * throwForce, ForceMode.Impulse);

        // Start "throw timer" so if it doesn't hit an opponent within throwTimeout, it's considered a miss
        _throwTimer = throwTimeout;
        _throwTimerActive = true;
        _isInMotion = true;

        _currentHolder.OnBallLost();
        _currentHolder = null;
    }

    /// <summary> Returns the last holder who threw or held the ball. </summary>
    public BaseCharacterController GetLastHolder()
    {
        return _lastHolder;
    }

    /// <summary> Handle collisions to see if we hit an opponent or if there's a catch attempt. </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // If velocity is too low, we ignore it (e.g. ball is basically rolling on ground).
        if (_rb.velocity.magnitude < minHitVelocity) return;

        // Check if we collided with a character
        BaseCharacterController hitCharacter = collision.gameObject.GetComponent<BaseCharacterController>();
        if (hitCharacter != null)
        {
            // If the ball was thrown by someone (lastHolder) and the collision is with a different team
            if (_lastHolder != null && _lastHolder.teamID != hitCharacter.teamID)
            {
                // Attempt a catch:
                bool caught = hitCharacter.AttemptCatch(this);

                if (caught)
                {
                    Debug.Log($"{hitCharacter.name} caught the ball thrown by team {_lastHolder.teamID}!");
                    // Let the character actually pick it up
                    hitCharacter.CatchBall(this);
                }
                else
                {
                    // They failed to catch => get eliminated
                    Debug.Log($"{hitCharacter.name} got hit by the ball from team {_lastHolder.teamID}!");
                    _hasHitOnThisThrow = true;
                    _throwTimerActive = false;

                    GameManager.Instance.OnPlayerHit(hitCharacter);

                    // Possibly reassign the ball after a short delay
                    StartCoroutine(DelayBallAssignment());
                }
            }
            else
            {
                // The ball is from the same team => no elimination
                // Could consider this a "pass" if you want, or just ignore it
                Debug.Log("Same team collision or no last holder => ignoring.");
            }
        }

        // After a collision with a potential target, the ball is no longer in motion (or at least the throw is concluded)
        _isInMotion = false;
    }

    /// <summary> Speed of the ball (used for catch probability checks). </summary>
    public float GetSpeed()
    {
        return _rb.velocity.magnitude;
    }

    public bool IsInMotion()
    {
        return _isInMotion;
    }

    private IEnumerator DelayBallAssignment()
    {
        yield return new WaitForSeconds(1f);
        GameManager.Instance.ReassignBallToOppositeTeam();
    }
}
