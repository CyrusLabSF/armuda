using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ArTusTypes;

public class ArTusMemoryClarityEngine : MonoBehaviour
{
    private ArTusCoreState core;

    [Header("Clarity Settings")]
    public float decayRatePerSecond = 0.01f;
    public float sadnessPenalty = 0.02f;
    public float joyBoost = 0.01f;

    private string clarityLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/ClarityDecayLog.csv";
    private HashSet<string> previouslyFuzzy = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        if (!File.Exists(clarityLogPath))
            File.WriteAllText(clarityLogPath, "Timestamp,Category,Emotion,Clarity,Label,TrailID\n");
    }

    void Update()
    {
        foreach (var entry in core.GetAllMemoryEntries())
        {
            if (entry == null) continue;

            float before = entry.clarity;

            // 📉 Decay
            entry.clarity -= decayRatePerSecond * Time.deltaTime;

            // 😢 Penalty
            if (entry.emotion == "sad")
                entry.clarity -= sadnessPenalty * Time.deltaTime;

            // 😄 Bonus
            if (entry.emotion == "joy" || entry.emotion == "growing")
                entry.clarity += joyBoost * Time.deltaTime;

            // 🔒 Clamp
            entry.clarity = Mathf.Clamp01(entry.clarity);

            string label = GetClarityLabel(entry.clarity);

            // 🧠 Log clarity change if threshold crossed
            if (label == "hazy" && !previouslyFuzzy.Contains(entry.content))
            {
                previouslyFuzzy.Add(entry.content);
                core.LogMemory($"🕳️ Memory '{entry.content}' has faded. Clarity = {entry.clarity:F2}", "MemoryFading", 2, "forgetting", entry.trailID);
                core.ScheduleReflection(entry.category, "uncertain");
            }

            // 📤 CSV export
            File.AppendAllText(clarityLogPath,
                $"{DateTime.Now},{entry.category},{entry.emotion},{entry.clarity:F2},{label},{entry.trailID}\n");
        }
    }

    public string GetClarityLabel(float clarity)
    {
        if (clarity > 0.75f) return "clear";
        if (clarity > 0.35f) return "fuzzy";
        return "hazy";
    }

    public string DescribeMemoryClarity(MemoryEntry entry)
    {
        string label = GetClarityLabel(entry.clarity);
        return $"This memory feels {label}.";
    }
}
