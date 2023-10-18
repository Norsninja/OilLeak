using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerProfile", menuName = "Game/PlayerProfile")]
public class PlayerProfile : ScriptableObject
{
    public int totalCurrency;
    public Dictionary<string, int> scoresByLocation = new Dictionary<string, int>(); // Save scores by location name
    public int totalScore; // Total score accumulated by the player
}

