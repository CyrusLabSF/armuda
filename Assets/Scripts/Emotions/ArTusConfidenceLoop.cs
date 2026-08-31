using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ArTusTypes;

public class ArTusConfidenceLoop : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    private Dictionary<string, float> behaviorConfidence = new();

    [Header("Confidence Settings")]
    public float positiveBoost = 0.2f;
    public float negativeDecay = 0.3f;
    public float minConfidence = 0.2f;
    public float maxConfidence = 1.0f;

    private string[] behaviors = new[] { "thinking", "growing", "curious", "bored", "joy", "sad" };

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    void Update()
    {
        var recent = core.GetAllMemoryEntries().TakeLast(10).ToList();
        if (recent.Count == 0) return;

        foreach (var behavior in behaviors)
        {
            float delta = CalculateEmotionEffect(recent, behavior);
            AdjustConfidence(behavior, delta);
        }

        ExportConfidenceScores();
    }

    float CalculateEmotionEffect(List<MemoryEntry> entries, string target)
    {
        float score = 0f;

        foreach (var entry in entries)
        {
            if (entry.emotion == target)
                score += 1f;
            else if (entry.content.ToLower().Contains(target))
                score += 0.5f;
            else if (entry.emotion == "sad" && target != "sad")
                score -= 0.75f;
        }

        return Mathf.Clamp(score / entries.Count, -1f, 1f);
    }

    void AdjustConfidence(string behavior, float delta)
    {
        if (!behaviorConfidence.ContainsKey(behavior))
            behaviorConfidence[behavior] = 0.5f;

        float prev = behaviorConfidence[behavior];

        behaviorConfidence[behavior] += delta > 0 ? positiveBoost : -negativeDecay;
        behaviorConfidence[behavior] = Mathf.Clamp(behaviorConfidence[behavior], minConfidence, maxConfidence);

        float updated = behaviorConfidence[behavior];
        Debug.Log($"[ConfidenceLoop] {behavior} confidence adjusted: {prev:F2} → {updated:F2}");

        if (updated <= minConfidence)
        {
            speech?.Speak($"I'm losing confidence in the behavior '{behavior}' — I may reconsider how I use it.");
            core?.LogMemory($"⚠️ Confidence in behavior '{behavior}' dropped to {updated:F2}.", "BehaviorConfidence", 2, "uncertain");
        }

        if (updated >= maxConfidence && delta > 0.5f)
        {
            speech?.Speak($"The behavior '{behavior}' continues to succeed — I’m reinforcing it.");
            core?.LogMemory($"✅ Confidence in behavior '{behavior}' strengthened to {updated:F2}.", "BehaviorConfidence", 3, "confident");
        }
    }

    void ExportConfidenceScores()
    {
        string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/BehaviorConfidence.csv";
        List<string> lines = new() { "Timestamp,Behavior,Confidence" };

        foreach (var pair in behaviorConfidence)
        {
            lines.Add($"{System.DateTime.Now},{pair.Key},{pair.Value:F2}");
        }

        File.AppendAllLines(path, lines);
    }

    public float GetConfidence(string behavior)
    {
        return behaviorConfidence.ContainsKey(behavior) ? behaviorConfidence[behavior] : 0.5f;
    }

    public Dictionary<string, float> GetAllConfidenceScores()
    {
        return new Dictionary<string, float>(behaviorConfidence);
    }
}
