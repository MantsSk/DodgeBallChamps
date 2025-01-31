using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterAIController : BaseCharacterController
{
    // AI states, including new "InboundDodge"
    private enum AIState { Positioning, SeekBall, Attack, Avoid, InboundDodge }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 4f;

    [Tooltip("Max distance the AI searches for a free ball on its own side.")]
    [SerializeField] private float ballSearchRadius = 15f;

    [Tooltip("Random area around the AI's initial spawn for idle movement.")]
    [SerializeField] private float positioningRange = 8f;

    [Tooltip("Time between picking new random positions while idle.")]
    [SerializeField] private float repositionInterval = 3f;

    [Header("Idle Sway (small movement while standing)")]
    [SerializeField] private bool enableIdleSway = true;
    [SerializeField] private float swayAmplitude = 0.5f; // how far to sway
    [SerializeField] private float swaySpeed = 1.0f;     // how fast the sway oscillates

    [Header("Throw Settings")]
    [SerializeField] private float throwCooldown = 3f;

    [Header("Avoid / Dodge Settings")]
    [Tooltip("If an opponent with the ball is within this distance, AI tries to avoid.")]
    [SerializeField] private float threatDistance = 10f;

    [Tooltip("How far the AI moves sideways or backwards to avoid.")]
    [SerializeField] private float avoidDistance = 3f;

    [Tooltip("Minimum distance to attempt a catch if the ball is coming straight at AI.")]
    [SerializeField] private float inboundCatchDistance = 2.0f;

    [Tooltip("Angle threshold (degrees) for deciding if the ball is heading at the AI.")]
    [SerializeField] private float inboundAngleThreshold = 15f;
    
    private NavMeshAgent _navAgent;
    private AIState _currentState;

    // For Positioning movement
    private float _nextRepositionTime = 0f;
    private Vector3 _currentPosTarget;

    // For Avoid state
    private float _avoidEndTime = 0f;       // how long to remain in avoid
    private float _avoidDuration = 2.0f;    // time spent sidestepping/backing away
    private Vector3 _avoidTargetPosition;   // random sidestep target
    private bool _avoidTargetSet = false;

    // For InboundDodge
    private float _inboundDodgeEndTime = 0f;
    private float _inboundDodgeDuration = 1.0f;  // short, sharper dodge

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _navAgent.speed = moveSpeed;
        _navAgent.updateRotation = false;  // We'll handle rotation manually

        // Restrict movement to own side using areaMask
        int team0Area = NavMesh.GetAreaFromName("Team0Area");
        int team1Area = NavMesh.GetAreaFromName("Team1Area");

        if (teamID == 0)
        {
            _navAgent.areaMask = 1 << team0Area;
        }
        else
        {
            _navAgent.areaMask = 1 << team1Area;
        }
    }

    private void Start()
    {
        // Start the AI's state machine
        _currentState = (_heldBall != null) ? AIState.Attack : AIState.Positioning;
    }

    private void Update()
    {
        // Always check for free balls to catch within a small overlap
        LookForAndCatchBall();

        // Check for inbound (thrown) balls specifically traveling toward us
        if (_currentState != AIState.Attack && _currentState != AIState.Avoid && _currentState != AIState.InboundDodge)
        {
            // We only do inbound detection if not currently in a forced dodge or attacking
            if (CheckInboundBall(out BallController inboundBall))
            {
                // Decide: do we attempt a quick catch or do an inbound dodge?
                float dist = Vector3.Distance(transform.position, inboundBall.transform.position);
                if (dist <= inboundCatchDistance && AttemptCatch(inboundBall))
                {
                    // If close enough & we succeed, pick up the ball. Then Attack
                    CatchBall(inboundBall);
                    _currentState = AIState.Attack;
                    return;
                }
                else
                {
                    // Perform a quick side-step or dodge
                    _currentState = AIState.InboundDodge;
                    PrepareInboundDodge(inboundBall);
                }
            }
        }

        // Evaluate if we should switch to "Avoid" because an opponent has the ball
        if (_currentState != AIState.Attack && _currentState != AIState.Avoid && _currentState != AIState.InboundDodge)
        {
            if (IsOpponentThreatening(out BaseCharacterController threateningOpponent))
            {
                _currentState = AIState.Avoid;
                PrepareAvoid(threateningOpponent);
            }
        }

        // Run the behavior for the current state
        switch (_currentState)
        {
            case AIState.Positioning:
                UpdatePositioning();
                break;
            case AIState.SeekBall:
                UpdateSeekBall();
                break;
            case AIState.Attack:
                UpdateAttack();
                break;
            case AIState.Avoid:
                UpdateAvoid();
                break;
            case AIState.InboundDodge:
                UpdateInboundDodge();
                break;
        }

        // If desired, do small local swaying around the target (purely cosmetic)
        if (enableIdleSway && _currentState == AIState.Positioning)
        {
            ApplyIdleSway();
        }
    }

    // ----------------------------------------------------------------
    // 1) DETECT / CATCH BALL
    // ----------------------------------------------------------------
    private void LookForAndCatchBall()
    {
        // Basic overlap check in a small radius to pick up a stationary ball
        float localCatchRadius = 1.5f;
        Collider[] hits = Physics.OverlapSphere(transform.position, localCatchRadius);
        foreach (var c in hits)
        {
            if (c.CompareTag("Ball"))
            {
                BallController ball = c.GetComponent<BallController>();
                if (ball != null && !ball.IsInMotion() && _heldBall == null)
                {
                    bool didCatch = AttemptCatch(ball);
                    if (didCatch)
                    {
                        Debug.Log($"AI (Team {teamID}) caught a free ball!");
                        _currentState = AIState.Attack;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check for a thrown (in-motion) ball that is heading toward this AI.
    /// Returns true if found, and sets the inboundBall param to that ball.
    /// </summary>
    private bool CheckInboundBall(out BallController inboundBall)
    {
        inboundBall = null;

        // Find all moving balls
        BallController[] allBalls = FindObjectsOfType<BallController>();
        float closestDist = Mathf.Infinity;

        foreach (var ball in allBalls)
        {
            if (ball == null) continue;
            if (!ball.IsInMotion()) continue; // skip non-moving or held balls

            // We only consider it "inbound" if the ball's velocity is roughly pointing at us
            Vector3 toAI = (transform.position - ball.transform.position);
            float dist = toAI.magnitude;
            if (dist > 20f) continue; // arbitrary cutoff to ignore very distant balls

            // If the ball is heading our way:
            Vector3 ballVel = ball.GetComponent<Rigidbody>().velocity;
            if (ballVel.sqrMagnitude < 0.01f) continue;

            // Angle between the ball's direction and the line from the ball to the AI
            float angle = Vector3.Angle(ballVel, toAI);
            if (angle <= inboundAngleThreshold) 
            {
                // It's fairly close to heading at us
                if (dist < closestDist)
                {
                    closestDist = dist;
                    inboundBall = ball;
                }
            }
        }

        return (inboundBall != null);
    }

    // ----------------------------------------------------------------
    // 2) DETECT OPPONENT THREAT
    // ----------------------------------------------------------------
    private bool IsOpponentThreatening(out BaseCharacterController threatOpponent)
    {
        threatOpponent = null;

        var allChars = FindObjectsOfType<BaseCharacterController>();
        float closestDist = float.MaxValue;

        foreach (var c in allChars)
        {
            if (c.teamID == teamID || c == this) 
                continue; // skip teammates and self

            // Opponent is a threat if holding a ball
            if (!c.IsHoldingBall()) 
                continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < threatDistance && dist < closestDist)
            {
                closestDist = dist;
                threatOpponent = c;
            }
        }

        return (threatOpponent != null);
    }

    // ----------------------------------------------------------------
    // STATE: POSITIONING (IDLE MOVEMENT)
    // ----------------------------------------------------------------
    private void UpdatePositioning()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        // If there's a free ball in range, go Seek it
        BallController nearestBall = FindNearestFreeBall();
        if (nearestBall != null)
        {
            _currentState = AIState.SeekBall;
            return;
        }

        // Otherwise, move around randomly
        if (Time.time >= _nextRepositionTime)
        {
            _nextRepositionTime = Time.time + repositionInterval;
            _currentPosTarget = GetRandomPositionInOwnSide();
            SetDestination(_currentPosTarget);
        }
    }

    // Small side sway while the AI is essentially "idle" at a position
    private void ApplyIdleSway()
    {
        if (_navAgent.remainingDistance <= 0.5f) // close to target => sway in place
        {
            // We'll offset the local position slightly
            float swayOffsetX = Mathf.Sin(Time.time * swaySpeed) * swayAmplitude;
            float swayOffsetZ = Mathf.Cos(Time.time * swaySpeed) * swayAmplitude * 0.5f; // can differ on each axis

            // The easiest approach is to just offset transform localPosition visually,
            // but if the AI uses a NavMeshAgent, that might fight with the agent's position.
            // So we only do a small rotation or slight shift that won't break pathing:

            // For demonstration: slightly "bob" left and right by rotating the transform around Y
            float rotationY = swayOffsetX * 2f; // rotate around Y
            transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }
    }

    // ----------------------------------------------------------------
    // STATE: SEEK BALL
    // ----------------------------------------------------------------
    private void UpdateSeekBall()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        BallController nearestBall = FindNearestFreeBall();
        if (nearestBall == null)
        {
            _currentState = AIState.Positioning;
            return;
        }

        SetDestination(nearestBall.transform.position);
    }

    // ----------------------------------------------------------------
    // STATE: ATTACK
    // ----------------------------------------------------------------
    private void UpdateAttack()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.Positioning;
            return;
        }

        BaseCharacterController opponent = FindNearestOpponent();
        if (opponent == null)
        {
            _currentState = AIState.Positioning;
            return;
        }

        // Move closer
        SetDestination(opponent.transform.position);

        // Face them
        Vector3 targetPos = opponent.transform.position + Vector3.up * 1.5f; // Aim slightly above ground
        Vector3 dir = (targetPos - transform.position);
        dir.y = Mathf.Max(dir.y, 0.1f); // ensure some upward angle, optional

        if (dir.sqrMagnitude > 0.1f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }

        // Now rely on the parent's _nextThrowTime for the post-pickup delay
        // and for any additional throw cooldown we want to apply.
        if (Time.time >= _nextThrowTime)  // from BaseCharacterController
        {
            // Actually throw
            Vector3 throwDir = dir.normalized;
            _heldBall.ThrowBall(throwDir);

            // Set a new cooldown so AI won't throw again for, say, 3 seconds
            _nextThrowTime = Time.time + throwCooldown;

            Debug.Log($"AI (Team {teamID}) THREW the ball at {opponent.name}");
            _currentState = AIState.Positioning;
        }
    }



    // ----------------------------------------------------------------
    // STATE: AVOID
    // (Opponents on your side with ball, not necessarily inbound throw)
    // ----------------------------------------------------------------
    private void UpdateAvoid()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        // If the avoid timer is done, revert to normal positioning
        if (Time.time >= _avoidEndTime)
        {
            _avoidTargetSet = false;
            _currentState = AIState.Positioning;
            return;
        }
    }

    private void PrepareAvoid(BaseCharacterController threateningOpponent)
    {
        _avoidEndTime = Time.time + _avoidDuration;
        _avoidTargetSet = false;

        // Move away from the threatening opponent
        Vector3 toOpp = (threateningOpponent.transform.position - transform.position).normalized;

        // CROSS product with up => left/right direction
        Vector3 side = Vector3.Cross(toOpp, Vector3.up).normalized;
        float leftOrRight = (Random.value < 0.5f) ? 1f : -1f;

        Vector3 backward = -toOpp * (avoidDistance * 0.5f);
        Vector3 sideways = side * (avoidDistance * 0.5f * leftOrRight);
        Vector3 targetPos = transform.position + backward + sideways;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, avoidDistance * 2f, _navAgent.areaMask))
        {
            _avoidTargetPosition = hit.position;
            _avoidTargetSet = true;
            SetDestination(_avoidTargetPosition);
        }
        else
        {
            _avoidTargetPosition = transform.position;
        }
    }

    // ----------------------------------------------------------------
    // STATE: INBOUND DODGE
    // (Ball is literally flying at us)
    // ----------------------------------------------------------------
    private void UpdateInboundDodge()
    {
        // If we got a ball in the meantime => Attack
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }

        // Timer-based, short dodge
        if (Time.time >= _inboundDodgeEndTime)
        {
            _currentState = AIState.Positioning;
        }
    }

    private void PrepareInboundDodge(BallController inboundBall)
    {
        _inboundDodgeEndTime = Time.time + _inboundDodgeDuration;

        // Direction from the ball to us
        Vector3 toMe = (transform.position - inboundBall.transform.position).normalized;

        // Decide random left or right offset, plus partial backward
        Vector3 side = Vector3.Cross(toMe, Vector3.up).normalized;
        float leftOrRight = (Random.value < 0.5f) ? 1f : -1f;
        Vector3 dodgeOffset = side * (avoidDistance * leftOrRight) + (-toMe * avoidDistance * 0.5f);

        Vector3 dodgeTarget = transform.position + dodgeOffset;

        if (NavMesh.SamplePosition(dodgeTarget, out NavMeshHit hit, avoidDistance * 2f, _navAgent.areaMask))
        {
            SetDestination(hit.position);
        }
    }

    // ----------------------------------------------------------------
    // HELPERS: BALL / OPPONENT / RANDOM MOVES
    // ----------------------------------------------------------------
    private BallController FindNearestFreeBall()
    {
        BallController[] allBalls = FindObjectsOfType<BallController>();
        BallController nearest = null;
        float closestDist = Mathf.Infinity;

        foreach (var b in allBalls)
        {
            if (b == null) continue;
            if (b.IsInMotion()) continue;
            if (b.transform.parent != null) continue;

            float dist = Vector3.Distance(transform.position, b.transform.position);
            if (dist < closestDist && dist < ballSearchRadius)
            {
                closestDist = dist;
                nearest = b;
            }
        }
        return nearest;
    }

    private BaseCharacterController FindNearestOpponent()
    {
        float closestDist = Mathf.Infinity;
        BaseCharacterController nearest = null;

        var allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c == this || c.teamID == teamID) continue;
            if (!c.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }

    private Vector3 GetRandomPositionInOwnSide()
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-positioningRange, positioningRange),
            0f,
            Random.Range(-positioningRange, positioningRange)
        );
        Vector3 candidatePos = initialSpawnPosition + randomOffset;

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, positioningRange, _navAgent.areaMask))
        {
            return hit.position;
        }
        return transform.position;
    }

    private void SetDestination(Vector3 target)
    {
        if (_navAgent.isActiveAndEnabled)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, _navAgent.areaMask))
            {
                _navAgent.SetDestination(hit.position);
            }
        }
    }
}

/* 
 * Disclaimer: This is still a simplified approach. 
 * In a real dodgeball game, you might add line-of-sight checks, 
 * advanced dodging (anticipating throws), or formation logic with teammates. 
 * But this example should get you closer to a more dynamic, 
 * “avoid/dodge inbound throws or catch if in range” behavior.
 */
