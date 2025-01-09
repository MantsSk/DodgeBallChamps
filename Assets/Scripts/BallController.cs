using System.Collections;
using UnityEngine;

public class BallController : MonoBehaviour
{
    [SerializeField] private float throwForce = 15f;
    [SerializeField] private float minHitVelocity = 3f;
    [SerializeField] private float throwTimeout = 2f;

    private Rigidbody _rb;
    private bool _isHeld;
    private BaseCharacterController _currentHolder;

    private float _throwTimer;
    private bool _throwTimerActive;
    private bool _hasHitOnThisThrow;
    private bool _isInMotion = false; // Track if ball is in motion
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
                // Handle missed throw: reassign ball to the other team
                StartCoroutine(DelayBallAssignment());
            }
        }
    }

    public void PickUpByPlayerOrAI(Transform holderTransform, BaseCharacterController holder)
    {
        _isHeld = true;
        _currentHolder = holder;
        _lastHolder = holder; // Update last holder

        _rb.isKinematic = true;
        _rb.velocity = Vector3.zero;

        transform.SetParent(holderTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        _hasHitOnThisThrow = false;
        _throwTimerActive = false;
    }

    public void ThrowBall(Vector3 direction)
    {
        if (!_isHeld || _currentHolder == null) return;

        _isHeld = false;
        transform.SetParent(null);

        _rb.isKinematic = false;
        _rb.velocity = Vector3.zero;
        _rb.AddForce(direction * throwForce, ForceMode.Impulse);

        _throwTimer = throwTimeout;
        _throwTimerActive = true;
        _isInMotion = true; // Ball is now in motion

        // Let the holder know they lost the ball
        _currentHolder.OnBallLost();
        _currentHolder = null;
    }

    public BaseCharacterController GetLastHolder()
    {
        return _lastHolder;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_rb.velocity.magnitude < minHitVelocity) return;

        BaseCharacterController hitCharacter = collision.gameObject.GetComponent<BaseCharacterController>();
        if (hitCharacter != null)
        {
            _hasHitOnThisThrow = true;
            _throwTimerActive = false;

            GameManager.Instance.OnPlayerHit(hitCharacter);

            // Reassign the ball after a delay to prevent instant reassignment
            StartCoroutine(DelayBallAssignment());
        }

        _isInMotion = false; // Ball is no longer in motion
    }

    // Wait before assigning the ball to the next team
    private IEnumerator DelayBallAssignment()
    {
        yield return new WaitForSeconds(1f); // Add a delay
        GameManager.Instance.ReassignBallToOppositeTeam();
    }
}
