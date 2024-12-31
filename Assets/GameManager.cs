using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // References to the player, AI, and ball.
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
        EndGame();
    }

    public void AIHit()
    {
        Debug.Log("AI was hit! Player wins!");
        EndGame();
    }

    public void UnsuccessfulThrow(bool wasPlayerThrow)
    {
        // If player threw and missed, AI gets the ball
        // If AI threw and missed, Player gets the ball
        if (wasPlayerThrow)
        {
            ai.PickUpBallAtStart(ball);
            Debug.Log("Player missed -> AI gets the ball.");
        }
        else
        {
            player.PickUpBallAtStart(ball);
            Debug.Log("AI missed -> Player gets the ball.");
        }
    }

    private void EndGame()
    {
        // Freeze the game for a simple prototype
        Time.timeScale = 0f;
    }
}
