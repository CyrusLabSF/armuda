using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public class ArTusDomainReflector : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    private string domainRoot;
    private string exportPath;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior")]
    public bool enableSpeech = false;
    public bool enableReflectionSchedule = false;

    [Header("Limits")]
    public float reflectionCooldown = 10f;
    public int maxReflectionsPerRun = 10;

    private float lastReflectionTime;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        domainRoot = ArTusPathUtility.GetPersistent("UNIVERcity/Domains");
        exportPath = ArTusPathUtility.GetPersistent("UNIVERcity/Logs/DomainSummaries");

        Directory.CreateDirectory(domainRoot);
        Directory.CreateDirectory(exportPath);
    }

    public void ReflectOnDomain(string domainName)
    {
        if (Time.time - lastReflectionTime < reflectionCooldown)
            return;

        lastReflectionTime = Time.time;

        if (string.IsNullOrWhiteSpace(domainName) || core == null)
            return;

        var entries = core.memoryLog
            .Where(m => !string.IsNullOrEmpty(m.category) &&
                        m.category.IndexOf(domainName, StringComparison.OrdinalIgnoreCase) >= 0)
            .TakeLast(maxReflectionsPerRun)
            .ToList();

        if (entries.Count == 0)
        {
            if (!betaMode && enableSpeech)
                speech?.Speak($"I don’t yet have enough information about {domainName}.");
            return;
        }

        // -----------------------------
        // CONTROLLED REFLECTION
        // -----------------------------
        if (!betaMode)
        {
            foreach (var entry in entries)
                core.ReflectOnMemory(entry);
        }

        string dominantEmotion = entries
            .GroupBy(e => e.emotion)
            .OrderByDescending(g => g.Count())
            .First().Key;

        float avgConfidence = (float)entries.Average(e => e.score);
        string strongestMemory = entries.OrderByDescending(e => e.score).First().content;

        string belief =
            $"From my studies in {domainName}, I observed dominant emotion {dominantEmotion} " +
            $"with clarity {avgConfidence:F2}. A key memory: '{strongestMemory}'. " +
            $"This suggests {GenerateConclusion(domainName, dominantEmotion)}.";

        // -----------------------------
        // SAFE MEMORY LOGGING
        // -----------------------------
        if (!betaMode)
        {
            string trailID = $"Trail_{domainName}_Reflection";

            core.LogMemory(
                belief,
                "DomainSummary",
                3,
                dominantEmotion,
                trailID
            );
        }

        // -----------------------------
        // REFLECTION SCHEDULING (LIMITED)
        // -----------------------------
        if (!betaMode && avgConfidence < 1.5f && enableReflectionSchedule)
        {
            core.ScheduleReflection($"Review_{domainName}", "uncertain");
        }

        // -----------------------------
        // FILE OUTPUT (SAFE)
        // -----------------------------
        try
        {
            string domainDir = Path.Combine(domainRoot, domainName);
            Directory.CreateDirectory(domainDir);

            File.WriteAllText(Path.Combine(domainDir, "Summary.txt"), belief);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DomainReflector] Save failed: {ex.Message}");
        }

        // -----------------------------
        // JSON EXPORT
        // -----------------------------
        try
        {
            string jsonPath = Path.Combine(
                exportPath,
                $"{domainName}_Summary_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );

            var payload = new DomainSummaryPayload
            {
                domain = domainName,
                emotion = dominantEmotion,
                score = avgConfidence,
                belief = belief
            };

            File.WriteAllText(jsonPath, JsonUtility.ToJson(payload, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DomainReflector] JSON export failed: {ex.Message}");
        }

        // -----------------------------
        // SPEECH (DISABLED IN BETA)
        // -----------------------------
        if (!betaMode && enableSpeech)
        {
            speech?.Speak($"Here is what I believe about {domainName}.");
            speech?.Speak(belief);
        }

        Debug.Log($"[DomainReflector] {domainName} reflection complete.");
    }

    private string GenerateConclusion(string domain, string emotion)
    {
        domain = domain.ToLowerInvariant();
        emotion = emotion.ToLowerInvariant();

        if (domain == "biology")
            return "life adapts through variation and survival pressures.";

        if (domain == "memory")
            return "memory shapes identity more than it preserves exact events.";

        if (domain == "emotion")
            return "emotion influences belief weighting and decision-making.";

        if (domain == "technology")
            return "technology reflects the intent and ethics of its creators.";

        return "this domain is still forming and requires further observation.";
    }

    [Serializable]
    private class DomainSummaryPayload
    {
        public string domain;
        public string emotion;
        public float score;
        public string belief;
    }
}