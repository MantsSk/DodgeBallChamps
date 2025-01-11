using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Camera Reference")]

    [Header("Camera Reference")]
    [SerializeField] private Camera mainCamera; 

    private CameraController cameraController; 

    [Header("Court Settings")]
    [SerializeField] private Transform courtLine; 
    public Transform CourtLine => courtLine;

    [Header("Team Setup")]
    [SerializeField] private int playersPerTeam = 3; 
    [SerializeField] private Transform[] teamASpawnPoints;
    [SerializeField] private Transform[] teamBSpawnPoints;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;     
    [SerializeField] private GameObject aiPrefab;         

    [Header("Scene References")]
    [SerializeField] private BallController ball;

    private List<BaseCharacterController> teamA = new List<BaseCharacterController>();
    private List<BaseCharacterController> teamB = new List<BaseCharacterController>();

    private int teamAAliveCount;
    private int teamBAliveCount;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        cameraController = mainCamera.GetComponent<CameraController>();
        // Spawn both teams
        SpawnTeam(teamA, 0, teamASpawnPoints);
        SpawnTeam(teamB, 1, teamBSpawnPoints);

        // Initialize alive counts
        teamAAliveCount = teamA.Count;
        teamBAliveCount = teamB.Count;

        // Randomly assign ball
        bool giveToTeamA = (Random.value > 0.5f);
        if (giveToTeamA && teamA.Count > 0)
        {
            teamA[0].PickUpBallAtStart(ball);
            Debug.Log("Ball assigned to Team A");
        }
        else if (teamB.Count > 0)
        {
            teamB[0].PickUpBallAtStart(ball);
            Debug.Log("Ball assigned to Team B");
        }
    }
    private void SpawnTeam(List<BaseCharacterController> teamList, int teamID, Transform[] spawnPoints)
    {
        int count = Mathf.Min(playersPerTeam, spawnPoints.Length);

        // Determine the opposing team's spawn center
        Vector3 opposingTeamCenter = (teamID == 0) ? GetSpawnCenter(teamBSpawnPoints) : GetSpawnCenter(teamASpawnPoints);

        for (int i = 0; i < count; i++)
        {
            // Decide if this spawn is human or AI.
            // For example, Team A's first slot might be the Player, rest are AI.
            bool isHuman = (teamID == 0 && i == 0);
            GameObject prefab = isHuman ? playerPrefab : aiPrefab;

            // Instantiate at the spawn point
            Transform spawnPoint = spawnPoints[i];
            GameObject characterObj = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

            // Rotate to face the opposing team's center
            Vector3 directionToFace = (opposingTeamCenter - spawnPoint.position).normalized;
            if (directionToFace != Vector3.zero)
            {
                characterObj.transform.rotation = Quaternion.LookRotation(directionToFace);
            }

            // Get the controller, assign team ID
            BaseCharacterController controller = characterObj.GetComponent<BaseCharacterController>();
            controller.teamID = teamID;
            teamList.Add(controller);

            Debug.Log($"Spawned {(isHuman ? "Player" : "AI")} at {spawnPoint.position} for Team {teamID}");
        }
    }

    // Helper method to calculate the center of spawn points
    private Vector3 GetSpawnCenter(Transform[] spawnPoints)
    {
        Vector3 center = Vector3.zero;
        foreach (var point in spawnPoints)
        {
            center += point.position;
        }
        return center / spawnPoints.Length;
    }


    public void OnPlayerHit(BaseCharacterController hitCharacter)
    {
        Debug.Log($"Player on Team {hitCharacter.teamID} was hit and is out!");
        hitCharacter.gameObject.SetActive(false);

        if (hitCharacter.teamID == 0)
        {
            teamAAliveCount--;
            if (teamAAliveCount <= 0) EndGame(0);
        }
        else
        {
            teamBAliveCount--;
            if (teamBAliveCount <= 0) EndGame(1);
        }

        // Ball is reassigned only after a small delay
        StartCoroutine(DelayReassignBall());
    }

    private IEnumerator DelayReassignBall()
    {
        yield return new WaitForSeconds(1f); // short delay
        ReassignBallToOppositeTeam();
    }

    public void OnBallThrown(BaseCharacterController lastHolder)
    {
        List<BaseCharacterController> oppositeTeam = (lastHolder.teamID == 0) ? teamB : teamA;
        foreach (var member in oppositeTeam)
        {
            if (member.gameObject.activeSelf)
            {
                member.PickUpBallAtStart(ball);
                Debug.Log($"Ball assigned to Team {(lastHolder.teamID == 0 ? "B" : "A")}");
                return;
            }
        }
    }

    public void ReassignBallToOppositeTeam()
    {
        BaseCharacterController lastHolder = ball.GetLastHolder();
        if (lastHolder == null) return;

        List<BaseCharacterController> oppositeTeam = (lastHolder.teamID == 0) ? teamB : teamA;
        foreach (var member in oppositeTeam)
        {
            if (member.gameObject.activeSelf)
            {
                member.PickUpBallAtStart(ball);
                Debug.Log($"Ball assigned to Team {(lastHolder.teamID == 0 ? "B" : "A")}");
                return;
            }
        }
    }

    private void EndGame(int losingTeamID)
    {
        int winningTeamID = (losingTeamID == 0) ? 1 : 0;
        Debug.Log($"Team {winningTeamID} wins! (Team {losingTeamID} lost all players).");
        Time.timeScale = 0f;
    }

    void Update()
    {
        GameObject characterObject = GameObject.FindWithTag("Player");

        if (characterObject == null) return; 


        Debug.Log(cameraController);
        Debug.Log(characterObject.name);
    
        cameraController.SetCameraTarget(characterObject);
    }
}
