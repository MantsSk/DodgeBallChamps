using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : BaseCharacterController
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private KeyCode throwKey = KeyCode.Space;
    [SerializeField] private KeyCode passKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode catchKey = KeyCode.F;

    private CharacterController _charController;
    
    // We'll store our desired movement vector here each frame
    private Vector3 _movement;

    private void Awake()
    {
        // Get the CharacterController on this GameObject
        _charController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovementInput();
        HandleThrowInput();
        HandleCatchInput();
    }

    private void HandleMovementInput()
    {
        // Basic WASD/Arrow input
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Align movement with the camera's forward direction (if applicable)
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right   = Camera.main.transform.right;

        // Flatten the vectors so we ignore vertical camera tilt
        forward.y = 0f;
        right.y   = 0f;
        forward.Normalize();
        right.Normalize();

        // Combine the input with camera orientation
        _movement = (forward * moveZ + right * moveX) * moveSpeed;
    }

    private void HandleThrowInput()
    {
        // If we currently hold the ball, check for throw or pass
        if (_heldBall != null)
        {
            if (Input.GetKeyDown(throwKey))
            {
                // Throw forward relative to the player's transform
                Vector3 throwDir = transform.forward;
                _heldBall.ThrowBall(throwDir);
            }
            else if (Input.GetKeyDown(passKey))
            {
                // Pass to nearest teammate
                BaseCharacterController teammate = FindNearestTeammate();
                if (teammate != null)
                {
                    Vector3 passDir = (teammate.transform.position - transform.position).normalized;
                    _heldBall.ThrowBall(passDir);
                }
            }
        }
    }

    private void HandleCatchInput()
    {
        // Press a key to attempt a manual catch
        if (Input.GetKeyDown(catchKey))
        {
            TryCatchBall();
        }
    }

    private void FixedUpdate()
    {
        // CharacterControllers typically move in Update or LateUpdate,
        // but if your game logic depends on physics timing, you can do it here.
        // We'll do it in FixedUpdate as in your example.

        Vector3 displacement = _movement * Time.fixedDeltaTime;

        // If needed, apply simple gravity:
        // displacement.y += Physics.gravity.y * Time.fixedDeltaTime;

        _charController.Move(displacement);
    }

    /// <summary>
    /// Check for a stationary ball in range when the player presses F,
    /// and catch it if found.
    /// </summary>
    private void TryCatchBall()
    {
        // OverlapSphere to detect a ball within 'catchRadius'
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, catchRadius);
        bool caught = false;

        foreach (var collider in hitColliders)
        {
            if (collider.CompareTag("Ball"))
            {
                BallController ball = collider.GetComponent<BallController>();
                // If the ball is valid, not already held, and not in motion (your existing logic)
                if (ball != null && _heldBall == null && !ball.IsInMotion())
                {
                    // Call AttemptCatch (overridden version for the Player)
                    if (AttemptCatch(ball))
                    {
                        Debug.Log("Player caught the ball!");
                        CatchBall(ball);
                        caught = true;
                        break;
                    }
                }
            }
        }

        if (!caught)
        {
            Debug.Log("No ball caught!");
        }
    }

    /// <summary>
    /// Find the nearest teammate to pass the ball.
    /// </summary>
    private BaseCharacterController FindNearestTeammate()
    {
        float closestDist = Mathf.Infinity;
        BaseCharacterController nearest = null;
        var allChars = FindObjectsOfType<BaseCharacterController>();

        foreach (var c in allChars)
        {
            if (c == this) continue;
            if (c.teamID != this.teamID) continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }

    // ======================================================================
    // OVERRIDE THE BASE-CLASS CATCH BEHAVIOR
    // ======================================================================
    /// <summary>
    /// Override: For the player, we ignore the AI's probability-based catch
    /// and rely purely on whether the user pressed F and the ball is in range.
    /// By default, if we get here, it means we want to succeed automatically
    /// (i.e., no randomness).
    /// </summary>
    public override bool AttemptCatch(BallController ball)
    {
        // For the Player, let's always succeed if the ball is valid.
        // (You can add more timing constraints if you want.)
        if (ball == null || IsHoldingBall()) return false;
        
        // E.g., return true if we simply want "instant catch" on key press:
        return true;
    }
}
