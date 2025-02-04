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
    public bool isHeld;
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
        isHeld = true;
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
        if (!isHeld || _currentHolder == null) return;

        isHeld = false;
        transform.SetParent(null);
        _rb.isKinematic = false;
        _rb.velocity = Vector3.zero;
        _rb.AddForce(direction * throwForce, ForceMode.Impulse);

        // Start throw timer (if the ball doesn't hit within timeout, it is reassigned)
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

    /// <summary> Handles collisions to check for a hit or a catch. </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // Ignore very slow collisions (e.g. rolling ball)
        if (_rb.velocity.magnitude < minHitVelocity)
            return;

        // Check if we collided with a character
        BaseCharacterController hitCharacter = collision.gameObject.GetComponent<BaseCharacterController>();
        if (hitCharacter != null)
        {
            // Only count as a hit if the ball was thrown by someone from the opposite team
            if (_lastHolder != null && _lastHolder.teamID != hitCharacter.teamID)
            {
                bool caught = hitCharacter.AttemptCatch(this);
                if (caught)
                {
                    Debug.Log($"{hitCharacter.name} caught the ball thrown by team {_lastHolder.teamID}!");
                    hitCharacter.CatchBall(this);
                }
                else
                {
                    Debug.Log($"{hitCharacter.name} got hit by the ball from team {_lastHolder.teamID}!");
                    _hasHitOnThisThrow = true;
                    _throwTimerActive = false;
                    GameManager.Instance.OnPlayerHit(hitCharacter);
                    StartCoroutine(DelayBallAssignment());
                }
            }
            else
            {
                Debug.Log("Same team collision or no valid last holder => ignoring.");
            }
        }
        _isInMotion = false;
    }

    /// <summary> Returns the current speed of the ball. </summary>
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
        GameManager.Instance.ReassignBall(this);
    }
}
