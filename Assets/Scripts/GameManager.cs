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

        for (int i = 0; i < count; i++)
        {
            bool isHuman = (teamID == 0 && i == 0); // Team A's first player is human
            GameObject prefab = isHuman ? playerPrefab : aiPrefab;

            Transform spawnPoint = spawnPoints[i];
            GameObject characterObj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            // Rotate friendly AI by -180 degrees for Team 1
            if (teamID == 1)
            {
                characterObj.transform.rotation = Quaternion.Euler(0, 180, 0);
            }

            BaseCharacterController controller = characterObj.GetComponent<BaseCharacterController>();
            controller.teamID = teamID;
            teamList.Add(controller);

            Debug.Log($"Spawned {(isHuman ? "Player" : "AI")} at {spawnPoint.position} for Team {teamID}, rotation: {characterObj.transform.rotation.eulerAngles}");
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
