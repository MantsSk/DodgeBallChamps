using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private PlayerController player;
    [SerializeField] private AIController ai;
    [SerializeField] private BallController ball;

    private void Awake()
    {
        // Simple Singleton
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Randomly assign ball to either Player or AI at the start
        bool giveToPlayer = (Random.value > 0.5f);
        if (giveToPlayer)
        {
            player.PickUpBallAtStart(ball);
            Debug.Log("Ball assigned to PLAYER at start");
        }
        else
        {
            ai.PickUpBallAtStart(ball);
            Debug.Log("Ball assigned to AI at start");
        }
    }

    public void PlayerHit()
    {
        Debug.Log("Player was hit! AI wins!");
        player.gameObject.SetActive(false);
        EndGame();
    }

    public void AIHit()
    {
        Debug.Log("AI was hit! Player wins!");
        ai.gameObject.SetActive(false);
        EndGame();
    }

    public void UnsuccessfulThrow(bool wasPlayerThrow)
    {
        if (wasPlayerThrow)
        {
            ball.ResetBallState();
            ai.PickUpBallAtStart(ball);
            Debug.Log("Player missed -> AI gets the ball.");
        }
        else
        {
            ball.ResetBallState();
            player.PickUpBallAtStart(ball);
            Debug.Log("AI missed -> Player gets the ball.");
        }
    }

    private void EndGame()
    {
        Debug.Log("Game End!");
    }
}
