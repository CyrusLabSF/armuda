using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

/// <summary>
/// Tracks growth across beliefs and domains.
/// Logs confidence changes, growth deltas, and domain-level growth events.
/// </summary>
public class ArTusGrowthLogger : MonoBehaviour
{
    private ArTusBeliefEngine beliefEngine;
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Growth Settings")]
    public float growthThreshold = 1.5f;
    public bool enableReflection = false;
    public bool enableSpeech = true;

    private Dictionary<string, float> lastBeliefSnapshot = new();

    private string beliefCsvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/BeliefGrowthLog.csv";
    private string domainCsvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/DomainGrowthLog.csv";

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        speech = GetComponent<ArTusSpeechResponder>();

        try
        {
            if (!File.Exists(beliefCsvPath))
                File.WriteAllText(beliefCsvPath, "Timestamp,Topic,Delta,Type,Emotion\n");

            if (!File.Exists(domainCsvPath))
                File.WriteAllText(domainCsvPath, "Timestamp,Domain,Message\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[GrowthLogger] Could not create CSV log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Tracks belief-level confidence deltas between snapshots.
    /// </summary>
    public void CompareBeliefChange()
    {
        foreach (var kvp in beliefEngine.beliefs)
        {
            string topic = kvp.Key;
            float current = kvp.Value.confidenceScore;

            if (lastBeliefSnapshot.TryGetValue(topic, out float previous))
            {
                float delta = current - previous;
                string deltaType = ClassifyDelta(delta);
                string emotion = GetEmotion(deltaType);

                string log = $"{(delta >= 0 ? "+" : "")}{delta:F2} → {deltaType}";
                string trailID = $"Trail_Confidence_{topic.Replace(" ", "_")}";

                string memory = deltaType switch
                {
                    "surge" => $"⚡ Confidence surged in '{topic}' ({log})",
                    "drop" => $"📉 Confidence dropped in '{topic}' ({log})",
                    "stable" => $"🟢 Confidence stable in '{topic}' ({log})",
                    "fade" => $"🕳️ Confidence fading in '{topic}' ({log})",
                    _ => $"🧠 Belief shift in '{topic}' ({log})"
                };

                core.LogMemory(memory, "BeliefDelta", 3, emotion, trailID);

                try
                {
                    File.AppendAllText(beliefCsvPath, $"{DateTime.Now},{topic},{delta:F2},{deltaType},{emotion}\n");
                }
                catch (IOException ex)
                {
                    Debug.LogError($"[GrowthLogger] Could not write to CSV: {ex.Message}");
                }

                if (deltaType == "drop" && enableReflection)
                {
                    if (enableSpeech)
                        speech?.Speak($"I’m less certain about {topic} than before.");

                    core.ScheduleReflection(topic, "uncertain");
                }
            }

            lastBeliefSnapshot[topic] = current;
        }
    }

    /// <summary>
    /// Logs domain/adversary growth events.
    /// Called externally by DomainRotationScheduler, AdversaryTrailManager, etc.
    /// </summary>
    public void LogGrowth(string domainName, string message = "Domain rotation/growth detected")
    {
        string logLine = $"{DateTime.Now},{domainName},{message}";

        try
        {
            File.AppendAllText(domainCsvPath, logLine + Environment.NewLine);
            Debug.Log($"[GrowthLogger] 📈 Growth logged for {domainName}: {message}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[GrowthLogger] Failed to log domain growth: {ex.Message}");
        }

        core?.LogMemory($"Growth event in {domainName}: {message}", "DomainGrowth", 2, "curious");
    }

    private string ClassifyDelta(float delta)
    {
        if (delta >= growthThreshold) return "surge";
        if (delta <= -growthThreshold) return "drop";
        if (Mathf.Abs(delta) < 0.1f) return "stable";
        return delta > 0 ? "mild_gain" : "fade";
    }

    private string GetEmotion(string deltaType)
    {
        return deltaType switch
        {
            "surge" => "confident",
            "drop" => "uncertain",
            "fade" => "concerned",
            _ => "neutral"
        };
    }
}
