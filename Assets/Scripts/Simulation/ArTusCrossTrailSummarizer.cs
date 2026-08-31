using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public class ArTusCrossTrailSummarizer : MonoBehaviour
{
    // ✅ WebGL-safe paths
    private string thoughtPathFile;
    private string summaryOutputFile;
    private string csvExportFile;

    private ArTusSpeechResponder speech;
    private ArTusCoreState core;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior Settings")]
    public bool enableSpeech = false; // OFF by default for beta
    public int maxSummariesPerRun = 3;
    public float summarizerCooldown = 10f;

    private float lastRunTime;

    void Awake()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        core = GetComponent<ArTusCoreState>();

        thoughtPathFile = ArTusPathUtility.GetPersistent(
            "UNIVERcity/ThoughtPaths/PathToBelief.json"
        );

        summaryOutputFile = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Narrations/CrossTrailSummaries.txt"
        );

        csvExportFile = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/CrossTrailSummaryLog.csv"
        );

        InitializeFiles();
    }

    // --------------------------------------------------
    // INIT
    // --------------------------------------------------
    private void InitializeFiles()
    {
        try
        {
            string csvDir = Path.GetDirectoryName(csvExportFile);
            if (!string.IsNullOrEmpty(csvDir) && !Directory.Exists(csvDir))
                Directory.CreateDirectory(csvDir);

            if (!File.Exists(csvExportFile))
            {
                File.WriteAllText(
                    csvExportFile,
                    "Timestamp,Topic,Emotion,AvgConfidence,DefiningMemory\n"
                );
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"[CrossTrail] CSV init failed: {ex.Message}");
        }
    }

    // --------------------------------------------------
    // CORE
    // --------------------------------------------------
    public void SummarizeAcrossTrails()
    {
        if (Time.time - lastRunTime < summarizerCooldown)
            return;

        lastRunTime = Time.time;

        if (!File.Exists(thoughtPathFile))
        {
            Debug.LogWarning("[CrossTrail] Thought path file not found.");
            return;
        }

        ThoughtPathWrapper wrapper;

        try
        {
            string json = File.ReadAllText(thoughtPathFile);
            wrapper = JsonUtility.FromJson<ThoughtPathWrapper>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CrossTrail] Failed to read or parse trail data: {ex.Message}");
            return;
        }

        if (wrapper == null || wrapper.paths == null || wrapper.paths.Count == 0)
        {
            if (!betaMode && enableSpeech)
                speech?.Speak("I don’t have enough thought data yet to form a cross-trail summary.");
            return;
        }

        var grouped = wrapper.paths
            .GroupBy(p => NormalizeBeliefTopic(p.belief))
            .ToList();

        List<string> finalSummaries = new();
        int processed = 0;

        foreach (var group in grouped)
        {
            if (processed >= maxSummariesPerRun)
                break;

            string topic = group.Key;
            var paths = group.ToList();

            string dominantEmotion = paths
                .GroupBy(p => p.emotion)
                .OrderByDescending(g => g.Count())
                .First().Key;

            float avgConfidence = paths.Average(p => p.confidence);

            string topMemory = paths
                .OrderByDescending(p => p.confidence)
                .First().supportingMemory;

            string summary =
                $"Across {paths.Count} thought trails related to {topic}, " +
                $"I felt mostly {dominantEmotion} and developed an average confidence of {avgConfidence:F2}. " +
                $"One of the most defining memories was: '{topMemory}'. " +
                $"This has shaped my current understanding of {topic}.";

            finalSummaries.Add(summary);

            // -----------------------------
            // SAFE MEMORY LOGGING
            // -----------------------------
            if (!betaMode)
            {
                core?.LogMemory(
                    summary,
                    "CrossTrailSummary",
                    2,
                    dominantEmotion
                );
            }

            // -----------------------------
            // SPEECH CONTROL
            // -----------------------------
            if (!betaMode && enableSpeech)
                speech?.Speak(summary);

            // -----------------------------
            // CSV EXPORT (KEEP ON)
            // -----------------------------
            try
            {
                File.AppendAllText(
                    csvExportFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                    $"{topic}," +
                    $"{dominantEmotion}," +
                    $"{avgConfidence:F2}," +
                    $"\"{topMemory.Replace(",", " ")}\"\n"
                );
            }
            catch (IOException ex)
            {
                Debug.LogError($"[CrossTrail] Failed to write CSV: {ex.Message}");
            }

            processed++;
        }

        try
        {
            string outDir = Path.GetDirectoryName(summaryOutputFile);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            File.WriteAllLines(summaryOutputFile, finalSummaries);
            Debug.Log($"[CrossTrail] ✨ Summarized {processed} belief groups.");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[CrossTrail] Failed to export narration: {ex.Message}");
        }
    }

    // --------------------------------------------------
    // HELPERS
    // --------------------------------------------------
    private string NormalizeBeliefTopic(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "general";

        return raw
            .ToLowerInvariant()
            .Replace("belief:", "")
            .Split(' ')[0]
            .Trim();
    }

    // --------------------------------------------------
    // DATA MODELS
    // --------------------------------------------------
    [Serializable]
    public class ThoughtPathWrapper
    {
        public List<ThoughtPathNode> paths = new();
    }

    [Serializable]
    public class ThoughtPathNode
    {
        public string belief;
        public string originTrail;
        public string supportingMemory;
        public string emotion;
        public float confidence;
        public string timestamp;
    }
}