using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class BeliefData
{
    // ==========================================================
    // 🔹 CORE IDENTIFIERS
    // ==========================================================
    public string belief;                       // Primary belief text
    public string domain = "general";
    public string theme = "general";
    public string origin = "unspecified";
    public string belief_source = "unknown";
    public string action_origin = "inferred";
    public string executed_by = "ArTus";

    // ==========================================================
    // 🔹 TIME
    // ==========================================================
    public string lastUpdated;                  // STRING (kept for compatibility)

    // ==========================================================
    // 🔹 EMOTION
    // ==========================================================
    public string dominantEmotion = "neutral";
    public string supportingTrail = "none";
    public string trailID = "";

    public Dictionary<string, int> emotionCounts = new();
    public List<string> associatedEmotions = new();

    // ==========================================================
    // 🔹 CONFIDENCE / REINFORCEMENT
    // ==========================================================
    public float confidenceScore = 1f;          // Primary internal score

    // 🔁 ENGINE COMPATIBILITY ALIAS
    public float confidence
    {
        get => confidenceScore;
        set => confidenceScore = value;
    }

    public int reinforcementCount = 0;
    public float cumulativeReinforcement = 0f;
    public List<string> reinforcementSources = new();

    // ==========================================================
    // 🔹 CONTRADICTION TRACKING
    // ==========================================================
    public bool emotionMismatchFlag = false;
    public bool isFlaggedContradiction = false;
    public bool hasContradiction = false;       // ✅ REQUIRED by BeliefEngine
    public float contradictionSeverity = 0f;
    public int conflictCount = 0;

    // ==========================================================
    // 🔹 RELATIONSHIPS
    // ==========================================================
    public List<string> relatedTrails = new();

    // ==========================================================
    // 🔹 COMPATIBILITY ALIASES
    // ==========================================================
    public string topic
    {
        get => belief;
        set => belief = value;
    }

    public string title
    {
        get => belief;
        set => belief = value;
    }

    // Optional description
    public string description;

    // ==========================================================
    // 🔹 CONSTRUCTOR
    // ==========================================================
    public BeliefData(string belief = "")
    {
        this.belief = belief;
        Touch();
    }

    // ==========================================================
    // 🔹 CORE HELPERS
    // ==========================================================
    public void Touch()
    {
        lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void UpdateEmotion(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion)) return;

        emotion = emotion.ToLower();
        dominantEmotion = emotion;

        if (!emotionCounts.ContainsKey(emotion))
            emotionCounts[emotion] = 1;
        else
            emotionCounts[emotion]++;

        associatedEmotions.Add(emotion);
        Touch();
    }

    public void AdjustConfidence(float delta)
    {
        confidenceScore = Mathf.Clamp(confidenceScore + delta, -10f, 10f);
        reinforcementCount++;
        cumulativeReinforcement += delta;
        Touch();
    }

    public void AddTrail(string trailId)
    {
        if (!relatedTrails.Contains(trailId))
            relatedTrails.Add(trailId);
    }

    // ==========================================================
    // 🔹 ANALYTICS
    // ==========================================================
    public float GetDecayFactor()
    {
        if (!DateTime.TryParse(lastUpdated, out var dt))
            dt = DateTime.UtcNow;

        float ageDays = (float)(DateTime.UtcNow - dt).TotalDays;
        float resilience = Mathf.Clamp01(confidenceScore / 10f);
        return ageDays * (1f - resilience);
    }

    public string GetDominantEmotion()
    {
        if (associatedEmotions == null || associatedEmotions.Count == 0)
            return "neutral";

        return associatedEmotions
            .GroupBy(e => e)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    public string GetJustification()
    {
        return $"I believe this because it has been reinforced {reinforcementCount} time(s), most often while feeling {GetDominantEmotion()}.";
    }

    // ==========================================================
    // 🔹 CLASSIFICATION
    // ==========================================================
    public string ClassifyBeliefDepartment(string beliefText)
    {
        beliefText = beliefText.ToLower();

        if (beliefText.Contains("emotion") || beliefText.Contains("feeling")) return "psychology";
        if (beliefText.Contains("language") || beliefText.Contains("syntax")) return "linguistics";
        if (beliefText.Contains("memory") || beliefText.Contains("logic")) return "cognition";
        if (beliefText.Contains("cell") || beliefText.Contains("gene")) return "biology";
        if (beliefText.Contains("atom") || beliefText.Contains("energy")) return "physics";
        if (beliefText.Contains("culture") || beliefText.Contains("belief")) return "philosophy";
        if (beliefText.Contains("space") || beliefText.Contains("planet")) return "astronomy";
        if (beliefText.Contains("unity") || beliefText.Contains("component")) return "application_learning";
        if (beliefText.Contains("code") || beliefText.Contains("loop")) return "computer_science";
        if (beliefText.Contains("ethics") || beliefText.Contains("justice")) return "law";
        if (beliefText.Contains("history") || beliefText.Contains("era")) return "history";
        if (beliefText.Contains("beauty") || beliefText.Contains("form")) return "aesthetics";

        return "general";
    }
}
