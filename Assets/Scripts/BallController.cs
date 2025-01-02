using UnityEngine;

public class BallController : MonoBehaviour
{
    [SerializeField] private float throwForce = 10f;
    [SerializeField] private float spinForce = 5f; // Amount of spin applied during throw
    [SerializeField] private float minThrowForceMultiplier = 0.8f;
    [SerializeField] private float maxThrowForceMultiplier = 1.2f;
    [SerializeField] private float minHitVelocity = 3f;
    [SerializeField] private float throwTimeout = 2f;

    private Rigidbody _rb;

    private bool _isHeldByPlayer;
    private bool _isHeldByAI;
    private Transform _holderTransform;

    private bool _wasPlayerLastThrow;
    private bool _hasHitOnThisThrow;

    private float _throwTimer;
    private bool _throwTimerActive;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (_isHeldByPlayer || _isHeldByAI)
        {
            transform.position = _holderTransform.position;
        }

        if (_throwTimerActive)
        {
            _throwTimer -= Time.deltaTime;
            if (_throwTimer <= 0f && !_hasHitOnThisThrow)
            {
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
        _wasPlayerLastThrow = thrownByPlayer;
        _hasHitOnThisThrow = false;

        _isHeldByPlayer = false;
        _isHeldByAI = false;
        _rb.isKinematic = false;
        _holderTransform = null;

        _rb.velocity = Vector3.zero;

        float randomMultiplier = Random.Range(minThrowForceMultiplier, maxThrowForceMultiplier);
        Vector3 force = direction * throwForce * randomMultiplier;

        // Add spin (Magnus effect simulation)
        Vector3 spin = Vector3.Cross(force.normalized, Vector3.up) * spinForce;

        _rb.AddForce(force, ForceMode.Impulse);
        _rb.AddTorque(spin, ForceMode.Impulse);

        _throwTimer = throwTimeout;
        _throwTimerActive = true;
    }

     public void ResetBallState()
    {
        // Reset Rigidbody properties
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;

        // Reset holding states
        _isHeldByPlayer = false;
        _isHeldByAI = false;
        _holderTransform = null;

        // Reset throw-related variables
        _wasPlayerLastThrow = false;
        _hasHitOnThisThrow = false;
        _throwTimerActive = false;
        _throwTimer = 0f;

        // Position ball in a neutral or initial state (optional)
        transform.position = Vector3.zero; // Adjust as needed
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_rb.velocity.magnitude < minHitVelocity) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            _hasHitOnThisThrow = true;
            GameManager.Instance.PlayerHit();
            _throwTimerActive = false;
        }
        else if (collision.gameObject.CompareTag("AI"))
        {
            _hasHitOnThisThrow = true;
            GameManager.Instance.AIHit();
            _throwTimerActive = false;
        }
    }

    private void ResetThrowState()
    {
        _throwTimerActive = false;
        _throwTimer = 0f;
        _hasHitOnThisThrow = false;
    }
}
