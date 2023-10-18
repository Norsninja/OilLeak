using UnityEngine;

[CreateAssetMenu(fileName = "GameState", menuName = "Game/GameState")]
public class GameState : ScriptableObject
{
    public bool isRoundOver;
    public float timer;
    public float roundDuration;  // Added roundDuration
    public int score;
    public int highScore;
    public int currency;
    public char grade;
}
