using UnityEngine;

public class BallController : MonoBehaviour
{
    [SerializeField] private float throwForce = 10f;
    [SerializeField] private float minHitVelocity = 3f;   // Minimum speed to count as a hit
    [SerializeField] private float throwTimeout = 2f;     // Time to consider a throw "missed" if no hit

    private Rigidbody _rb;

    private bool _isHeldByPlayer;
    private bool _isHeldByAI;
    private Transform _holderTransform;

    // Track who threw the ball last so we know who missed
    private bool _wasPlayerLastThrow;
    private bool _hasHitOnThisThrow;  // Did we get a successful hit during the current throw?

    private float _throwTimer;
    private bool _throwTimerActive;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // If the ball is held, keep it on the holder's "hand"
        if (_isHeldByPlayer || _isHeldByAI)
        {
            transform.position = _holderTransform.position;
        }

        // If we're mid-throw, check timer
        if (_throwTimerActive)
        {
            _throwTimer -= Time.deltaTime;
            if (_throwTimer <= 0f && !_hasHitOnThisThrow)
            {
                // The throw time expired and no hit occurred => unsuccessful throw
                _throwTimerActive = false;
                GameManager.Instance.UnsuccessfulThrow(_wasPlayerLastThrow);
            }
        }
    }

    public void PickUpByPlayer(Transform holder)
    {
        ResetThrowState();
        _isHeldByPlayer = true;
        _isHeldByAI = false;
        _rb.isKinematic = true;
        _holderTransform = holder;
    }

    public void PickUpByAI(Transform holder)
    {
        ResetThrowState();
        _isHeldByAI = true;
        _isHeldByPlayer = false;
        _rb.isKinematic = true;
        _holderTransform = holder;
    }

    public void ThrowBall(Vector3 direction, bool thrownByPlayer)
    {
        // Mark who threw
        _wasPlayerLastThrow = thrownByPlayer;
        _hasHitOnThisThrow = false;

        _isHeldByPlayer = false;
        _isHeldByAI = false;
        _rb.isKinematic = false;
        _holderTransform = null;

        // Stop any previous velocity
        _rb.velocity = Vector3.zero;
        _rb.AddForce(direction * throwForce, ForceMode.Impulse);

        // Start the "miss" timer
        _throwTimer = throwTimeout;
        _throwTimerActive = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // If ball is barely moving, ignore
        if (_rb.velocity.magnitude < minHitVelocity) return;

        // If it hits Player or AI => successful throw
        if (collision.gameObject.CompareTag("Player"))
        {
            _hasHitOnThisThrow = true;
            GameManager.Instance.PlayerHit();
            _throwTimerActive = false; // Stop the timer
        }
        else if (collision.gameObject.CompareTag("AI"))
        {
            _hasHitOnThisThrow = true;
            GameManager.Instance.AIHit();
            _throwTimerActive = false; // Stop the timer
        }
    }

    /// <summary>
    /// Resets throw-related variables when someone picks the ball up.
    /// </summary>
    private void ResetThrowState()
    {
        _throwTimerActive = false;
        _throwTimer = 0f;
        _hasHitOnThisThrow = false;
    }
}
