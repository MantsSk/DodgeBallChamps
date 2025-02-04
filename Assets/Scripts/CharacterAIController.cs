using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterAIController : BaseCharacterController
{
    private enum AIState { Positioning, SeekBall, Attack, Avoid, InboundDodge }
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float ballSearchRadius = 15f;
    [SerializeField] private float positioningRange = 8f;
    [SerializeField] private float repositionInterval = 3f;

    [Header("Idle Sway")]
    [SerializeField] private bool enableIdleSway = true;
    [SerializeField] private float swayAmplitude = 0.5f;
    [SerializeField] private float swaySpeed = 1.0f;

    [Header("Throw Settings")]
    [SerializeField] private float throwCooldown = 3f;

    [Header("Avoid / Dodge Settings")]
    [SerializeField] private float threatDistance = 10f;
    [SerializeField] private float avoidDistance = 3f;
    [SerializeField] private float inboundCatchDistance = 2.0f;
    [SerializeField] private float inboundAngleThreshold = 15f;

    [Header("AI Hoarding Settings")]
    [SerializeField] private int maxHoardedBalls = 3;  // AI can hold up to this many extra balls

    private NavMeshAgent _navAgent;
    private AIState _currentState;
    private float _nextRepositionTime = 0f;
    private Vector3 _currentPosTarget;

    // For Avoid state
    private float _avoidEndTime = 0f;
    private float _avoidDuration = 2.0f;
    private Vector3 _avoidTargetPosition;
    private bool _avoidTargetSet = false;

    // For InboundDodge state
    private float _inboundDodgeEndTime = 0f;
    private float _inboundDodgeDuration = 1.0f;

    // List of extra (hoarded) balls
    private List<BallController> _hoardedBalls = new List<BallController>();

    // We override OnCollisionEnter so that the AI does not automatically pick up a ball via collision.
    protected override void OnCollisionEnter(Collision collision)
    {
        // Do nothing here. AI will use LookForAndHoardBalls() (called in Update)
    }

    private void Start()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _navAgent.speed = moveSpeed;
        _navAgent.updateRotation = false;

        // Restrict movement using NavMesh area masks.
        int team0Area = NavMesh.GetAreaFromName("Team0Area");
        int team1Area = NavMesh.GetAreaFromName("Team1Area");

        // (For debugging, you might log teamID and area indices)
        Debug.Log($"{name} teamID: {teamID}");
        Debug.Log($"Team0Area index: {team0Area}, Team1Area index: {team1Area}");
        
        _navAgent.areaMask = (teamID == 0) ? (1 << team0Area) : (1 << team1Area);

        _currentState = (_heldBall != null) ? AIState.Attack : AIState.Positioning;
    }

    private void Update()
    {
        // Look for free balls (on the ground) to pick up
        LookForAndHoardBalls();

        // Check for inbound (thrown) balls if not already busy
        if (_currentState != AIState.Attack && _currentState != AIState.Avoid && _currentState != AIState.InboundDodge)
        {
            if (CheckInboundBall(out BallController inboundBall))
            {
                float dist = Vector3.Distance(transform.position, inboundBall.transform.position);
                if (dist <= inboundCatchDistance && AttemptCatch(inboundBall))
                {
                    CatchBall(inboundBall);
                    _currentState = AIState.Attack;
                    return;
                }
                else
                {
                    _currentState = AIState.InboundDodge;
                    PrepareInboundDodge(inboundBall);
                }
            }
        }

        // Check for opponents who are threatening (holding a ball)
        if (_currentState != AIState.Attack && _currentState != AIState.Avoid && _currentState != AIState.InboundDodge)
        {
            if (IsOpponentThreatening(out BaseCharacterController threateningOpponent))
            {
                _currentState = AIState.Avoid;
                PrepareAvoid(threateningOpponent);
            }
        }

        // Execute behavior based on current state
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

        if (enableIdleSway && _currentState == AIState.Positioning)
        {
            ApplyIdleSway();
        }
    }

    /// <summary>
    /// Look around for free balls on the ground.
    /// If found and not already holding a ball, pick it up automatically.
    /// If already holding one and below the hoard limit, add it as an extra.
    /// </summary>
    private void LookForAndHoardBalls()
    {
        float localCatchRadius = 1.5f;
        Collider[] hits = Physics.OverlapSphere(transform.position, localCatchRadius);
        foreach (var c in hits)
        {
            if (c.CompareTag("Ball"))
            {
                BallController ball = c.GetComponent<BallController>();
                // Only take the ball if it is on the ground (not in motion)
                if (ball != null && !ball.IsInMotion())
                {
                    // For AI, if the ball is on the ground, we automatically succeed.
                    if (AttemptCatch(ball))
                    {
                        if (_heldBall == null)
                        {
                            CatchBall(ball); // primary ball
                            Debug.Log($"AI (Team {teamID}) picked up a ball as primary.");
                        }
                        else if (_hoardedBalls.Count < maxHoardedBalls)
                        {
                            _hoardedBalls.Add(ball);
                            ball.PickUpByPlayerOrAI(transform, this);
                            Debug.Log($"AI (Team {teamID}) hoarded an extra ball. Total extra: {_hoardedBalls.Count}");
                        }
                        _currentState = AIState.Attack;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks for an inbound ball (one in motion toward this AI).
    /// </summary>
    private bool CheckInboundBall(out BallController inboundBall)
    {
        inboundBall = null;
        BallController[] allBalls = FindObjectsOfType<BallController>();
        float closestDist = Mathf.Infinity;
        foreach (var ball in allBalls)
        {
            if (ball == null || !ball.IsInMotion())
                continue;
            Vector3 toAI = transform.position - ball.transform.position;
            float dist = toAI.magnitude;
            if (dist > 20f)
                continue;
            Rigidbody ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb == null || ballRb.velocity.sqrMagnitude < 0.01f)
                continue;
            float angle = Vector3.Angle(ballRb.velocity, toAI);
            if (angle <= inboundAngleThreshold && dist < closestDist)
            {
                closestDist = dist;
                inboundBall = ball;
            }
        }
        return (inboundBall != null);
    }

    /// <summary>
    /// Checks if any opponent holding a ball is within threat distance.
    /// </summary>
    private bool IsOpponentThreatening(out BaseCharacterController threatOpponent)
    {
        threatOpponent = null;
        BaseCharacterController[] allChars = FindObjectsOfType<BaseCharacterController>();
        float closestDist = float.MaxValue;
        foreach (var c in allChars)
        {
            if (c.teamID == teamID || c == this)
                continue;
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

    private void UpdatePositioning()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }
        BallController nearestBall = FindNearestFreeBall();
        if (nearestBall != null)
        {
            _currentState = AIState.SeekBall;
            return;
        }
        if (Time.time >= _nextRepositionTime)
        {
            _nextRepositionTime = Time.time + repositionInterval;
            _currentPosTarget = GetRandomPositionInOwnSide();
            SetDestination(_currentPosTarget);
        }
    }

    private void ApplyIdleSway()
    {
        if (_navAgent.remainingDistance <= 0.5f)
        {
            float swayOffsetX = Mathf.Sin(Time.time * swaySpeed) * swayAmplitude;
            float rotationY = swayOffsetX * 2f;
            transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }
    }

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
        SetDestination(opponent.transform.position);
        Vector3 targetPos = opponent.transform.position + Vector3.up * 1.5f;
        Vector3 dir = targetPos - transform.position;
        if (dir.sqrMagnitude > 0.1f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }
        if (Time.time >= _nextThrowTime)
        {
            Vector3 throwDir = dir.normalized;
            _heldBall.ThrowBall(throwDir);
            Debug.Log($"AI (Team {teamID}) threw its primary ball at {opponent.name}");
            if (_hoardedBalls.Count > 0)
            {
                foreach (var extraBall in _hoardedBalls)
                {
                    extraBall.ThrowBall(throwDir);
                    Debug.Log($"AI (Team {teamID}) also threw an extra ball at {opponent.name}");
                }
                _hoardedBalls.Clear();
            }
            _nextThrowTime = Time.time + throwCooldown;
            _currentState = AIState.Positioning;
        }
    }

    private void UpdateAvoid()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }
        if (Time.time >= _avoidEndTime)
        {
            _avoidTargetSet = false;
            _currentState = AIState.Positioning;
        }
    }

    private void PrepareAvoid(BaseCharacterController threateningOpponent)
    {
        _avoidEndTime = Time.time + _avoidDuration;
        _avoidTargetSet = false;
        Vector3 toOpp = (threateningOpponent.transform.position - transform.position).normalized;
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
            SetDestination(transform.position);
        }
    }

    private void UpdateInboundDodge()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack;
            return;
        }
        if (Time.time >= _inboundDodgeEndTime)
        {
            _currentState = AIState.Positioning;
        }
    }

    private void PrepareInboundDodge(BallController inboundBall)
    {
        _inboundDodgeEndTime = Time.time + _inboundDodgeDuration;
        Vector3 toMe = (transform.position - inboundBall.transform.position).normalized;
        Vector3 side = Vector3.Cross(toMe, Vector3.up).normalized;
        float leftOrRight = (Random.value < 0.5f) ? 1f : -1f;
        Vector3 dodgeOffset = side * (avoidDistance * leftOrRight) + (-toMe * avoidDistance * 0.5f);
        Vector3 dodgeTarget = transform.position + dodgeOffset;
        if (NavMesh.SamplePosition(dodgeTarget, out NavMeshHit hit, avoidDistance * 2f, _navAgent.areaMask))
        {
            SetDestination(hit.position);
        }
    }

    private BallController FindNearestFreeBall()
    {
        BallController[] allBalls = FindObjectsOfType<BallController>();
        BallController nearest = null;
        float closestDist = Mathf.Infinity;
        foreach (var b in allBalls)
        {
            // Only consider balls that are on the ground (not in motion and not held)
            if (b == null || b.IsInMotion() || b.transform.parent != null || b.isHeld)
                continue;
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
        BaseCharacterController[] allChars = FindObjectsOfType<BaseCharacterController>();
        BaseCharacterController nearest = null;
        float closestDist = Mathf.Infinity;
        foreach (var c in allChars)
        {
            if (c == this || c.teamID == teamID || !c.gameObject.activeInHierarchy)
                continue;
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
            return hit.position;
        return transform.position;
    }

    private void SetDestination(Vector3 target)
    {
        if (_navAgent.isActiveAndEnabled)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, _navAgent.areaMask))
                _navAgent.SetDestination(hit.position);
        }
    }

    /// <summary>
    /// For AI, override the catch logic:
    /// - If the ball is on the ground (not in motion), always succeed.
    /// - Otherwise, use the probability-based logic.
    /// </summary>
    public override bool AttemptCatch(BallController ball)
    {
        if (ball == null || IsHoldingBall()) return false;
        if (!ball.IsInMotion())
        {
            // When the ball is on the ground, simply take it.
            return true;
        }
        else
        {
            // Use the base logic for thrown (in-motion) balls.
            return base.AttemptCatch(ball);
        }
    }
}
