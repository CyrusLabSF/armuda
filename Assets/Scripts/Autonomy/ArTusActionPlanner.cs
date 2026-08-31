using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ArTusActionPlanStep
{
    public string stepId;
    public string kind;
    public string target;
    public string rationale;
    public bool requiresConfirmation;
}

[Serializable]
public class ArTusActionPlan
{
    public string planId;
    public string objective;
    public string activeContext;
    public string status;
    public float confidence;
    public string createdAt;
    public List<ArTusActionPlanStep> steps = new();
}

public class ArTusActionPlanner : MonoBehaviour
{
    [SerializeField] private ArTusActionPlan lastPlan;

    public ArTusActionPlan BuildLearningPlan(string objective, string activeContext, float confidence = 0.6f)
    {
        lastPlan = new ArTusActionPlan
        {
            planId = Guid.NewGuid().ToString("N"),
            objective = string.IsNullOrWhiteSpace(objective) ? "learn" : objective.Trim(),
            activeContext = activeContext?.Trim() ?? string.Empty,
            status = "planned",
            confidence = Mathf.Clamp01(confidence),
            createdAt = DateTime.UtcNow.ToString("o"),
            steps = new List<ArTusActionPlanStep>
            {
                CreateStep("observe", activeContext, "Gather knowledge and state before acting.", false),
                CreateStep("analyze", activeContext, "Build a working model and compare beliefs.", false),
                CreateStep("act", activeContext, "Select the best available tool or learning action.", true),
                CreateStep("verify", activeContext, "Measure outcome and update certainty.", false)
            }
        };

        return lastPlan;
    }

    public ArTusActionPlan GetMostRecentPlan()
    {
        return lastPlan;
    }

    private static ArTusActionPlanStep CreateStep(string kind, string target, string rationale, bool requiresConfirmation)
    {
        return new ArTusActionPlanStep
        {
            stepId = Guid.NewGuid().ToString("N"),
            kind = kind,
            target = target?.Trim() ?? string.Empty,
            rationale = rationale?.Trim() ?? string.Empty,
            requiresConfirmation = requiresConfirmation
        };
    }
}
