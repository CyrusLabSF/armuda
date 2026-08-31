using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class ArTusKnowledgeConfidence : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;

    [Header("Weights")]
    public float clarityWeight = 0.4f;
    public float beliefWeight = 0.5f;
    public float emotionBonus = 0.1f;

    [Header("Behavior Settings")]
    public bool enableReflection = false;

    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/ConfidenceReport.csv";

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();

        try
        {
            if (!File.Exists(csvPath))
                File.WriteAllText(csvPath, "Timestamp,Topic,Clarity,Belief,EmotionBoost,TotalScore,Description,TrailID\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ConfidenceLogger] Could not create confidence CSV: {ex.Message}");
        }
    }

    public float GetConfidenceForTopic(string topic)
    {
        topic = topic.ToLower();
        var memoryEntries = core.GetAllMemoryEntries()
            .Where(m => m.content.ToLower().Contains(topic))
            .ToList();

        if (memoryEntries.Count == 0)
            return 0f;

        float clarityAvg = memoryEntries.Average(m => m.clarity);
        float beliefScore = beliefEngine.GetBeliefConfidence(topic);
        float emotionBoost = memoryEntries.Any(m => m.emotion == "joy" || m.emotion == "growing") ? emotionBonus : 0f;

        float total = (clarityAvg * clarityWeight) + (beliefScore * beliefWeight) + emotionBoost;
        float score = Mathf.Clamp01(total / (clarityWeight + beliefWeight + emotionBonus));

        LogConfidence(topic, clarityAvg, beliefScore, emotionBoost, score);

        if (score < 0.3f && enableReflection)
        {
            string trailID = $"Trail_Confidence_{topic.Replace(" ", "_")}";
            core.LogMemory($"🟡 Confidence in '{topic}' is low ({score:F2}). Consider reviewing this topic.", "LowConfidence", 2, "uncertain", trailID);
            core.ScheduleReflection(topic, "uncertain");
        }

        return score;
    }

    private void LogConfidence(string topic, float clarity, float belief, float emotion, float score)
    {
        string desc = DescribeConfidence(score);
        string trailID = $"Trail_Confidence_{topic.Replace(" ", "_")}";

        try
        {
            File.AppendAllText(csvPath,
                $"{DateTime.Now},{topic},{clarity:F2},{belief:F2},{emotion:F2},{score:F2},{desc},{trailID}\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ConfidenceLogger] Failed to log confidence: {ex.Message}");
        }
    }

    public string DescribeConfidence(float score)
    {
        if (score > 0.8f) return "high confidence";
        if (score > 0.5f) return "moderate confidence";
        if (score > 0.25f) return "low confidence";
        return "no confidence";
    }
}
