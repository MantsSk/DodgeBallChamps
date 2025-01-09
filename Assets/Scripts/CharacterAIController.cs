using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterAIController : BaseCharacterController
{
    private enum AIState { SeekBall, Wander, Attack, Pass }

    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float throwCooldown = 3f;

    private NavMeshAgent _navAgent;
    private AIState _currentState;
    private float _nextThrowTime;

    // For wandering
    private Vector3 _wanderTarget;
    private float _wanderRadius = 5f;
    private float _wanderCooldown;
    private float _wanderInterval = 2f;

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _navAgent.speed = moveSpeed;

        // Dynamically set area costs based on the team
        if (teamID == 0)
        {
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team0Area"), 1);   // Walkable for Team 0
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team1Area"), 1000); // Impassable for Team 0
        }
        else if (teamID == 1)
        {
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team0Area"), 1000); // Impassable for Team 1
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team1Area"), 1);    // Walkable for Team 1
        }
    }

    private void Start()
    {
        _currentState = AIState.Wander;
        SetRandomWanderTarget();
    }

    private void FixedUpdate()
    {
        switch (_currentState)
        {
            case AIState.SeekBall:
                SeekBallBehavior();
                break;
            case AIState.Wander:
                WanderBehavior();
                break;
            case AIState.Attack:
                AttackBehavior();
                break;
            case AIState.Pass:
                PassBehavior();
                break;
        }
    }

    private void SetDestination(Vector3 targetPosition)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 1.0f, NavMesh.AllAreas))
        {
            int targetArea = hit.mask;

            int team0AreaIndex = NavMesh.GetAreaFromName("Team0Area");
            int team1AreaIndex = NavMesh.GetAreaFromName("Team1Area");

            // Use bitwise check for area match
            if ((teamID == 0 && (targetArea & (1 << team0AreaIndex)) != 0) ||
                (teamID == 1 && (targetArea & (1 << team1AreaIndex)) != 0))
            {
                _navAgent.SetDestination(targetPosition);
            }
            else
            {
                Debug.Log($"Invalid destination for Team {teamID}: {targetPosition} (Dynamic Area: {targetArea})");
            }
        }
    }

    private void SeekBallBehavior()
    {
        if (_heldBall != null)
        {
            ChooseAttackOrPass();
            return;
        }

        BallController freeBall = FindFreeBall();
        if (freeBall != null)
        {
            SetDestination(freeBall.transform.position);
        }
        else
        {
            _currentState = AIState.Wander;
        }
    }

    private void WanderBehavior()
    {
        if (_heldBall != null)
        {
            ChooseAttackOrPass();
            return;
        }

        if (!_navAgent.pathPending && _navAgent.remainingDistance < 1f)
        {
            SetRandomWanderTarget();
        }
    }

    private void AttackBehavior()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.SeekBall;
            return;
        }

        BaseCharacterController nearestOpponent = FindNearestOpponent();
        if (nearestOpponent == null)
        {
            _currentState = AIState.Wander;
            return;
        }

        Vector3 targetPosition = nearestOpponent.transform.position;
        SetDestination(targetPosition);

        if (Time.time >= _nextThrowTime)
        {
            _heldBall.ThrowBall((targetPosition - transform.position).normalized);
            _nextThrowTime = Time.time + throwCooldown;
        }
    }

    private void PassBehavior()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.SeekBall;
            return;
        }

        BaseCharacterController mate = FindNearestTeammate();
        if (mate != null)
        {
            Vector3 targetPosition = mate.transform.position;
            SetDestination(targetPosition);

            if (Time.time >= _nextThrowTime)
            {
                _heldBall.ThrowBall((targetPosition - transform.position).normalized);
                _nextThrowTime = Time.time + throwCooldown;
            }
        }
        else
        {
            _currentState = AIState.Attack;
        }
    }

    private void SetRandomWanderTarget()
    {
        float courtLineZ = GameManager.Instance.CourtLine.position.z;

        Vector3 randomOffset = new Vector3(
            Random.Range(-_wanderRadius, _wanderRadius),
            0f,
            Random.Range(-_wanderRadius, _wanderRadius)
        );

        _wanderTarget = transform.position + randomOffset;

        // Adjust target to stay within valid team zones
        if (teamID == 0)
        {
            _wanderTarget.z = Mathf.Clamp(_wanderTarget.z, float.MinValue, courtLineZ - 1f);
        }
        else if (teamID == 1)
        {
            _wanderTarget.z = Mathf.Clamp(_wanderTarget.z, courtLineZ + 1f, float.MaxValue);
        }

        SetDestination(_wanderTarget);
    }

    private void ChooseAttackOrPass()
    {
        if (Random.value < 0.5f)
            _currentState = AIState.Attack;
        else
            _currentState = AIState.Pass;

        _nextThrowTime = Time.time + 1f;
    }

    private BallController FindFreeBall()
    {
        BallController[] allBalls = FindObjectsOfType<BallController>();
        foreach (var ball in allBalls)
        {
            if (!ball.GetComponent<Rigidbody>().isKinematic)
            {
                return ball;
            }
        }
        return null;
    }

    private BaseCharacterController FindNearestOpponent()
    {
        float closestDist = Mathf.Infinity;
        BaseCharacterController closest = null;

        BaseCharacterController[] allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c.teamID == this.teamID || !c.gameObject.activeSelf) continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = c;
            }
        }
        return closest;
    }

    private BaseCharacterController FindNearestTeammate()
    {
        float closestDist = Mathf.Infinity;
        BaseCharacterController closest = null;

        BaseCharacterController[] allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c.teamID != this.teamID || !c.gameObject.activeSelf || c == this) continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = c;
            }
        }
        return closest;
    }
}
