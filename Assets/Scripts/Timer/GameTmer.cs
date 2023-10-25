using UnityEngine;

public class GameTimer : MonoBehaviour
{   // Singleton instance made read-only
    public static GameTimer Instance { get; private set; }
    public GameState gameState;
    // Use a constant instead of float.MaxValue
    private const float DefaultRoundDuration = float.MaxValue;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ResetTimer();
        InitializeRoundDuration();
    }

    void Update()
    {
        if (gameState.roundState == RoundState.Active)
        {
            UpdateTimer();
        }
    }

    void UpdateTimer()
    {
        gameState.timer += Time.deltaTime;
    }

    public void ResetTimer()
    {
        gameState.timer = 0;
    }

    void InitializeRoundDuration()
    {
        float roundDuration = GameController.Instance.GetCurrentRoundDuration();
        gameState.roundDuration = (roundDuration >= 0) ? roundDuration : DefaultRoundDuration;
    }
}

