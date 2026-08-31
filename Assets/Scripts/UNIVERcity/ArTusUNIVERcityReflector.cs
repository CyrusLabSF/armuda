using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class ArTusUNIVERcityReflector : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusSpeechResponder speech;

    [Header("Reflection Settings")]
    public int topTopicsToSpeak = 3;
    private string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/KnowledgeReflection.csv";

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        speech = GetComponent<ArTusSpeechResponder>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath));
            if (!File.Exists(exportPath))
                File.WriteAllText(exportPath, "Timestamp,Category,MemoryCount,DominantEmotion\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[UNIVERcityReflector] Failed to initialize CSV: {ex.Message}");
        }
    }

    public void ReflectOnLearnedTopics()
    {
        var allMemories = core.GetAllMemoryEntries();
        if (allMemories.Count == 0)
        {
            speech?.Speak("I have not explored any knowledge yet.");
            return;
        }

        var categories = allMemories
            .Where(m => m.content.Contains(":"))
            .GroupBy(m => m.content.Split(':')[0].Trim().ToLower())
            .ToList();

        foreach (var categoryGroup in categories)
        {
            string category = categoryGroup.Key;
            int count = categoryGroup.Count();

            string dominantEmotion = categoryGroup
                .GroupBy(m => m.emotion)
                .OrderByDescending(e => e.Count())
                .FirstOrDefault()?.Key ?? "neutral";

            float avgClarity = categoryGroup.Average(m => m.clarity);

            speech?.Speak($"In {category}, I reflected {count} times — mostly feeling {dominantEmotion}.");

            try
            {
                File.AppendAllText(exportPath,
                    $"{DateTime.Now},{category},{count},{dominantEmotion}\n");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[UNIVERcityReflector] Failed to write reflection CSV: {ex.Message}");
            }
        }

        var topBeliefs = beliefEngine.GetTopBeliefs(topTopicsToSpeak);
        foreach (var belief in topBeliefs)
        {
            speech?.Speak($"I currently believe: {belief}");
        }

        core?.LogMemory($"🧠 Completed knowledge reflection across {categories.Count} categories.",
            "KnowledgeReflection", 2, "reflective");
    }

    public void ReflectOnWeakTopics()
    {
        var weak = core.beliefs
            .Where(kv => kv.Value.confidenceScore < 0.45f)
            .Select(kv => kv.Key)
            .Take(3)
            .ToList();

        if (weak.Count == 0)
        {
            speech?.Speak("None of my current beliefs appear unstable.");
            return;
        }

        foreach (var topic in weak)
        {
            speech?.Speak($"I am uncertain about {topic}. I may need to revisit it.");
            core?.ScheduleReflection(topic, "uncertain_belief");
        }
    }
}
