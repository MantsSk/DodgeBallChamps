using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Court Settings")]
    [SerializeField] private Transform courtLine; // Assign this in the editor
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
        // Spawn Teams
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

        for (int i = 0; i < count; i++)
        {
            // Select prefab based on team and whether it's human or AI
            bool isHuman = (teamID == 0 && i == 0); // Team A's first player is human
            GameObject prefab = isHuman ? playerPrefab : aiPrefab;

            // Instantiate character at the specific spawn point
            Transform spawnPoint = spawnPoints[i];
            GameObject characterObj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            BaseCharacterController controller = characterObj.GetComponent<BaseCharacterController>();
            controller.teamID = teamID;
            teamList.Add(controller);

            Debug.Log($"Spawned {(isHuman ? "Player" : "AI")} at {spawnPoint.position} for Team {teamID}");
        }
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

        // Ball is reassigned only after a delay
        StartCoroutine(DelayReassignBall());
    }

    private IEnumerator DelayReassignBall()
    {
        yield return new WaitForSeconds(1f); // Add delay to let the throw process
        ReassignBallToOppositeTeam();
    }


    public void OnBallThrown(BaseCharacterController lastHolder)
    {
        List<BaseCharacterController> oppositeTeam = lastHolder.teamID == 0 ? teamB : teamA;

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

        List<BaseCharacterController> oppositeTeam = lastHolder.teamID == 0 ? teamB : teamA;

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
}