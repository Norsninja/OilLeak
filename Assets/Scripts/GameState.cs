using UnityEngine;

[CreateAssetMenu(fileName = "GameState", menuName = "Game/GameState")]
public class GameState : ScriptableObject
{
    public RoundState roundState = RoundState.NotStarted;
    public float timer;
    public float roundDuration;  // Added roundDuration
    public int score;
    public int highScore;
    public int currency;
    public char grade;
    public int initialTotalItems;
}

public enum RoundState
{
    NotStarted,
    Active,
    Over
}

