using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ScoreSummary
{
    public int maximumPossibleScore;
    public float escapePenalty;
    public float efficiencyScore;
    public float throwEfficiencyScore;
    public float adjustedGradeScore;
    public float bonus;

    // Constructor to initialize all fields
    public ScoreSummary(int maximumPossibleScore, float escapePenalty, float efficiencyScore, float throwEfficiencyScore, float adjustedGradeScore, float bonus)
    {
        this.maximumPossibleScore = maximumPossibleScore;
        this.escapePenalty = escapePenalty;
        this.efficiencyScore = efficiencyScore;
        this.throwEfficiencyScore = throwEfficiencyScore;
        this.adjustedGradeScore = adjustedGradeScore;
        this.bonus = bonus;
    }

    // You can also add methods here to update or manipulate these variables if needed
}


