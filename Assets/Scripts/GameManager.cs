using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MatchMode
{
    Small, // 3×3 with 1 ball spawned
    Large  // 6×6 with 6 balls spawned
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Camera Reference")]
    [SerializeField] private Camera mainCamera;
    private CameraController cameraController;

    [Header("Court Settings")]
    [SerializeField] private Transform courtLine;
    public Transform CourtLine => courtLine;

    [Header("Team Setup")]
    [SerializeField] private Transform[] teamASpawnPoints;
    [SerializeField] private Transform[] teamBSpawnPoints;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject aiPrefab;
    [SerializeField] private BallController ballPrefab;

    [Header("Match Configuration")]
    [SerializeField] private MatchMode matchMode = MatchMode.Small;
    private int playersPerTeam;

    [Header("Ball Spawn Points")]
    [SerializeField] private Transform[] ballSpawnPoints; // Set these in the Inspector

    private List<BaseCharacterController> teamA = new List<BaseCharacterController>();
    private List<BaseCharacterController> teamB = new List<BaseCharacterController>();
    private List<BallController> balls = new List<BallController>();

    private int teamAAliveCount;
    private int teamBAliveCount;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        cameraController = mainCamera.GetComponent<CameraController>();

        // Set players per team based on match mode.
        if (matchMode == MatchMode.Small)
            playersPerTeam = 3;
        else
            playersPerTeam = 6;

        SpawnTeam(teamA, 0, teamASpawnPoints);
        SpawnTeam(teamB, 1, teamBSpawnPoints);

        teamAAliveCount = teamA.Count;
        teamBAliveCount = teamB.Count;

        // Instead of assigning balls to players, simply spawn balls at designated spawn points.
        if (matchMode == MatchMode.Small)
        {
            // Spawn one ball from the first ball spawn point (if available).
            if (ballSpawnPoints.Length > 0)
            {
                BallController newBall = Instantiate(ballPrefab, ballSpawnPoints[0].position, ballSpawnPoints[0].rotation);
                balls.Add(newBall);
            }
        }
        else // Large mode
        {
            // Spawn up to six balls (use the first six spawn points if available).
            int ballCount = Mathf.Min(6, ballSpawnPoints.Length);
            for (int i = 0; i < ballCount; i++)
            {
                BallController newBall = Instantiate(ballPrefab, ballSpawnPoints[i].position, ballSpawnPoints[i].rotation);
                balls.Add(newBall);
            }
        }
    }

    private void SpawnTeam(List<BaseCharacterController> teamList, int teamID, Transform[] spawnPoints)
    {
        int count = Mathf.Min(playersPerTeam, spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            // For simplicity, let Team A’s first player be human; all others are AI.
            bool isHuman = (teamID == 0 && i == 0);
            GameObject prefab = isHuman ? playerPrefab : aiPrefab;
            Transform spawnPoint = spawnPoints[i];
            GameObject characterObj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            BaseCharacterController controller = characterObj.GetComponent<BaseCharacterController>();
            controller.teamID = teamID;
            controller.initialSpawnPosition = spawnPoint.position;
            teamList.Add(controller);

            // (Optional) Set team color here if desired.
            Renderer rend = characterObj.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = (teamID == 0) ? Color.blue : Color.red;
            else
            {
                foreach (Renderer childRend in characterObj.GetComponentsInChildren<Renderer>())
                {
                    childRend.material.color = (teamID == 0) ? Color.blue : Color.red;
                }
            }
        }
    }

    // In this design, once a ball is spawned it stays on the ground until a player or AI picks it up.
    // You can add additional logic here if you wish to reposition or reset balls later.
    public void ReassignBall(BallController ball)
    {
        // For now, do nothing.
    }

    public void OnPlayerHit(BaseCharacterController hitCharacter)
    {
        Debug.Log($"Player on Team {hitCharacter.teamID} was hit and is out!");
        hitCharacter.gameObject.SetActive(false);

        // Recalculate alive counts.
        teamAAliveCount = 0;
        foreach (var p in teamA)
        {
            if (p.gameObject.activeSelf)
                teamAAliveCount++;
        }
        teamBAliveCount = 0;
        foreach (var p in teamB)
        {
            if (p.gameObject.activeSelf)
                teamBAliveCount++;
        }
        Debug.Log($"After elimination: Team 0 alive: {teamAAliveCount}, Team 1 alive: {teamBAliveCount}");

        if (teamAAliveCount <= 0)
            EndGame(0);
        if (teamBAliveCount <= 0)
            EndGame(1);

        StartCoroutine(DelayReassignBall());
    }

    private IEnumerator DelayReassignBall()
    {
        yield return new WaitForSeconds(1f);
        // Optionally, reposition the ball if needed.
    }

    private void EndGame(int losingTeamID)
    {
        int winningTeamID = (losingTeamID == 0) ? 1 : 0;
        Debug.Log($"Team {winningTeamID} wins! (Team {losingTeamID} lost all players).");
        Time.timeScale = 0f;
    }

    private void Update()
    {
        // For camera follow: if a human player exists (tagged "Player"), follow that; otherwise, follow an active Team A member.
        GameObject characterObject = GameObject.FindWithTag("Player");
        if (characterObject == null)
        {
            foreach (var p in teamA)
            {
                if (p.gameObject.activeSelf)
                {
                    characterObject = p.gameObject;
                    break;
                }
            }
        }
        if (characterObject != null)
            cameraController.SetCameraTarget(characterObject);
    }
}
