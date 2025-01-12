using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class CharacterAIController : BaseCharacterController
{
    // We only keep these three states for now:
    private enum AIState { SideSteps, Attack }

    [Header("Movement/Throw Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float throwCooldown = 3f;

    [Header("Side-Step Settings")]
    [SerializeField] private float sideDistance = 2f;   // How far each sidestep goes
    [SerializeField] private float sideInterval = 2f;  // How often to switch side
    private float _sideTimer = 0f;
    private float _sideDirection = 1f;                 // +1 or -1

    private NavMeshAgent _navAgent;
    private AIState _currentState;
    private float _nextThrowTime;

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _navAgent.speed = moveSpeed;

        // Prevent the NavMeshAgent from rotating this character automatically
        _navAgent.updateRotation = false;

        // If you want to *completely* block the other team's area, you could use area masks
        // or set the cost of the other area to something huge or infinity.
        if (teamID == 0)
        {
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team0Area"), 1);
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team1Area"), 1000);
        }
        else if (teamID == 1)
        {
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team0Area"), 1000);
            _navAgent.SetAreaCost(NavMesh.GetAreaFromName("Team1Area"), 1);
        }
    }

    private Quaternion _baseRotation; // AI's initial spawn rotation

    private void Start()
    {
        _baseRotation = transform.rotation; // Save the AI's initial rotation
        // Optional: Give this AI the ball at the start if you want to see them shoot right away.
        // (e.g. only team 0 gets the ball at start)
        if (teamID == 0)
        {
            BallController ball = FindObjectOfType<BallController>();
            if (ball != null)
            {
                PickUpBallAtStart(ball);
            }
        }

        // Start in side-steps
        _currentState = AIState.SideSteps;
    }

    private void Update() 
    {
        LookForAndCatchBall();
    }

    private void FixedUpdate()
    {
        switch (_currentState)
        {
            case AIState.SideSteps:
                SideStepsBehavior();
                break;
            case AIState.Attack:
                AttackBehavior();
                break;
            // case AIState.Pass:
            //     PassBehavior();
            //     break;
        }
    }


    private void LookForAndCatchBall()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1.5f); // Catch radius
        foreach (var collider in hitColliders)
        {
            if (collider.CompareTag("Ball"))
            {
                BallController ball = collider.GetComponent<BallController>();
                if (ball != null && !ball.IsInMotion())
                {
                    bool didCatch = AttemptCatch(ball);
                    Debug.Log(didCatch ? "AI caught the ball!" : "AI failed to catch the ball.");
                }
            }
        }
    }


    // ----------------------------------------
    //  SideSteps Behavior
    // ----------------------------------------
    private void SideStepsBehavior()
    {
        if (_heldBall != null)
        {
            _currentState = AIState.Attack; // Switch to attack if holding the ball
            return;
        }

        // Time to flip the sidestep direction?
        if (Time.time >= _sideTimer)
        {
            _sideTimer = Time.time + sideInterval;
            _sideDirection *= -1f;

            // Calculate sidestep target based on base rotation and side direction
            Vector3 sidestepOffset = _baseRotation * Vector3.right * _sideDirection * sideDistance;
            Vector3 sidestepTarget = transform.position + sidestepOffset;

            // Ensure the sidestep target is valid on the NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(sidestepTarget, out hit, sideDistance, NavMesh.AllAreas))
            {
                _navAgent.SetDestination(hit.position);
            }
        }

        // Rotate the AI to match sidestep direction only when moving
        if (_navAgent.velocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _baseRotation, Time.deltaTime * 10f);
        }
        else
        {
            // Keep the AI facing forward relative to the base rotation
            transform.rotation = _baseRotation;
        }
    }



    // ----------------------------------------
    //  Attack Behavior
    // ----------------------------------------
    private void AttackBehavior()
    {
        if (_heldBall == null)
        {
            _currentState = AIState.SideSteps;
            return;
        }

        // Find nearest opponent
        BaseCharacterController nearestOpponent = FindNearestOpponent();
        if (nearestOpponent == null)
        {
            _currentState = AIState.SideSteps; // No valid target
            return;
        }

        // Move toward opponent
        Vector3 targetPosition = nearestOpponent.transform.position;
        SetDestination(targetPosition);

        // Throw if cooldown is ready
        if (Time.time >= _nextThrowTime)
        {
            Vector3 throwDirection = (targetPosition - transform.position).normalized;
            _heldBall.ThrowBall(throwDirection);

            // Log throw action
            Debug.Log($"AI on Team {teamID} threw the ball at {nearestOpponent.name}");

            // Reset cooldown and reassign ball
            _nextThrowTime = Time.time + throwCooldown;
            ReassignBall(false); // Assume missed shot for simplicity
        }
    }


    // ----------------------------------------
    //  Pass Behavior
    // ----------------------------------------
    // private void PassBehavior()
    // {
    //     if (_heldBall == null)
    //     {
    //         _currentState = AIState.SideSteps;
    //         return;
    //     }

    //     // Find nearest teammate
    //     BaseCharacterController mate = FindNearestTeammate();
    //     if (mate != null)
    //     {
    //         Vector3 targetPosition = mate.transform.position;
    //         SetDestination(targetPosition);

    //         if (Time.time >= _nextThrowTime)
    //         {
    //             _heldBall.ThrowBall((targetPosition - transform.position).normalized);

    //             // Passing is effectively a "hit" => same team
    //             ReassignBall(true);

    //             _nextThrowTime = Time.time + throwCooldown;
    //         }
    //     }
    //     else
    //     {
    //         // No teammate => fallback to Attack
    //         _currentState = AIState.Attack;
    //     }

    //     // If we lost the ball after reassign, go back to side-step
    //     if (_heldBall == null)
    //     {
    //         _currentState = AIState.SideSteps;
    //     }
    // }

    // ----------------------------------------
    //  Movement Helpers
    // ----------------------------------------
    private void SetDestination(Vector3 targetPosition)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 1.0f, NavMesh.AllAreas))
        {
            int targetArea = hit.mask;
            int team0AreaIndex = NavMesh.GetAreaFromName("Team0Area");
            int team1AreaIndex = NavMesh.GetAreaFromName("Team1Area");

            // Check if valid area
            if ((teamID == 0 && (targetArea & (1 << team0AreaIndex)) != 0) ||
                (teamID == 1 && (targetArea & (1 << team1AreaIndex)) != 0))
            {
                _navAgent.SetDestination(hit.position);
            }
        }
    }

    // ----------------------------------------
    //  Ball Reassignment
    // ----------------------------------------
    private void ReassignBall(bool didHit)
    {
        if (_heldBall == null) return;

        // same team on "hit", other team on "miss"
        int newTeam = didHit ? teamID : 1 - teamID;

        BaseCharacterController newOwner = FindRandomPlayer(newTeam);
        if (newOwner != null)
        {
            newOwner.PickUpBallAtStart(_heldBall);
            _heldBall = null; // Current AI no longer has it
        }
    }

    private BaseCharacterController FindRandomPlayer(int desiredTeamID)
    {
        var candidates = new List<BaseCharacterController>();
        var allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c.teamID == desiredTeamID && c.gameObject.activeSelf)
            {
                candidates.Add(c);
            }
        }
        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    // ----------------------------------------
    //  Opponent / Teammate Helpers
    // ----------------------------------------
    private BaseCharacterController FindNearestOpponent()
    {
        float closestDist = Mathf.Infinity;
        BaseCharacterController closest = null;

        var allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c.teamID == teamID || !c.gameObject.activeSelf) 
                continue;

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

        var allChars = FindObjectsOfType<BaseCharacterController>();
        foreach (var c in allChars)
        {
            if (c.teamID != teamID || !c.gameObject.activeSelf || c == this) 
                continue;

            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = c;
            }
        }
        return closest;
    }

    // ----------------------------------------
    //  Example: If BaseCharacterController
    //  doesn't define PickUpBall, add it here
    // ----------------------------------------
    // public override void PickUpBall(BallController ball)
    // {
    //     _heldBall = ball;
    //     var rb = ball.GetComponent<Rigidbody>();
    //     if (rb) rb.isKinematic = true;
    //     ball.transform.SetParent(transform);
    //     ball.transform.localPosition = Vector3.zero;
    // }
}
