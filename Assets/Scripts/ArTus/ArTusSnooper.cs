using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ArTusSnooper : MonoBehaviour
{
    [Header("Snoop Settings")]
    public float snoopInterval = 90f;

    public List<string> interestingKeywords = new()
    {
        "ai","neural","training","model","belief","reflection","project","report"
    };

    [Header("Control Limits")]
    public int maxFilesPerScan = 10;
    public float keywordCooldown = 300f; // seconds

    private string snoopPath;
    private string csvPath;

    private ArTusCoreState core;
    private ArTusEmotionController emotion;
    private ArTusSpeechResponder speech;

    private readonly HashSet<string> seenFiles = new();
    private readonly Dictionary<string, int> keywordFrequency = new();
    private readonly Dictionary<string, float> keywordLastTriggered = new();
    private readonly Dictionary<string, float> topicPressure = new();

    private readonly List<string> recentHits = new();

    public enum SnoopMode { Learn, Understand, Reflect }
    public SnoopMode currentMode = SnoopMode.Learn;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        emotion = GetComponent<ArTusEmotionController>();
        speech = GetComponent<ArTusSpeechResponder>();

        snoopPath = ArTusPathUtility.GetSafePath("UserWorkspace");
        csvPath = ArTusPathUtility.GetSafePath("UNIVERcity/Exports/SnoopSummary.csv");

#if UNITY_ANDROID && !UNITY_EDITOR
        enabled = false;
        return;
#endif

        StartCoroutine(SnoopLoop());
    }

    private IEnumerator SnoopLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(snoopInterval);
            RunScan();
        }
    }

    public void RunScanNow()
    {
        RunScan();
    }

    void RunScan()
    {
        if (!Directory.Exists(snoopPath))
            return;

        string[] files = Directory.GetFiles(snoopPath, "*", SearchOption.AllDirectories);

        int processed = 0;
        int hits = 0;

        recentHits.Clear();

        foreach (string file in files)
        {
            if (processed >= maxFilesPerScan)
                break;

            if (seenFiles.Contains(file))
                continue;

            string name = Path.GetFileName(file).ToLower();

            // lightweight content preview
            string contentPreview = "";
            try
            {
                contentPreview = File.ReadLines(file).FirstOrDefault()?.ToLower() ?? "";
            }
            catch { }

            foreach (string keyword in interestingKeywords)
            {
                if (!name.Contains(keyword) && !contentPreview.Contains(keyword))
                    continue;

                // cooldown check
                if (keywordLastTriggered.ContainsKey(keyword))
                {
                    if (Time.time - keywordLastTriggered[keyword] < keywordCooldown)
                        continue;
                }

                keywordLastTriggered[keyword] = Time.time;

                seenFiles.Add(file);
                hits++;
                processed++;

                // frequency tracking
                if (!keywordFrequency.ContainsKey(keyword))
                    keywordFrequency[keyword] = 0;

                keywordFrequency[keyword]++;

                int ageMinutes = (int)(DateTime.Now - File.GetLastWriteTime(file)).TotalMinutes;
                float weight = ComputeWeight(keyword, ageMinutes);

                recentHits.Add(name);

                // topic pressure system
                if (!topicPressure.ContainsKey(keyword))
                    topicPressure[keyword] = 0f;

                topicPressure[keyword] += weight;

                // only log memory lightly (reduced spam)
                core?.LogMemory(
                    $"🧿 Snooper detected '{keyword}' in '{name}'",
                    "SnooperHit",
                    Mathf.CeilToInt(weight * 2f),
                    ageMinutes < 5 ? "alert" : "curious"
                );

                // trigger reflection ONLY when pressure builds
                if (topicPressure[keyword] > 1.5f)
                {
                    core?.QueueDeferredReflection(
                        keyword,
                        "SnooperPressure",
                        topicPressure[keyword]
                    );

                    HandleMode(keyword, topicPressure[keyword]);

                    topicPressure[keyword] = 0f;
                }

                ExportCsv(name, keyword, weight);

                break; // prevent multi-keyword spam per file
            }
        }

        EvaluateEmotion(hits);
    }

    void HandleMode(string keyword, float weight)
    {
        if (currentMode == SnoopMode.Learn && weight > 0.6f)
        {
            string goalType =
                keywordFrequency[keyword] > 5 ? "pattern" :
                weight > 0.7f ? "urgent" : "curiosity";

            core?.GoalController?.AddGoal(
                $"Investigate topic cluster: {keyword}",
                goalType,
                "snooper",
                "curious",
                weight
            );
        }
    }

    float ComputeWeight(string keyword, int ageMinutes)
    {
        float recency =
            ageMinutes < 5 ? 0.5f :
            ageMinutes < 30 ? 0.3f : 0.1f;

        float frequency =
            Mathf.Clamp01(Mathf.Log(keywordFrequency[keyword] + 1) / 2f);

        return Mathf.Clamp01(recency + frequency);
    }

    void EvaluateEmotion(int hits)
    {
        if (hits == 0)
        {
            emotion?.SetEmotionByName("idle");
            return;
        }

        int totalFrequency = keywordFrequency.Values.Sum();

        if (hits > 5)
            emotion?.SetEmotionByName("overstimulated");
        else if (totalFrequency > 10)
            emotion?.SetEmotionByName("focused");
        else if (hits > 2)
            emotion?.SetEmotionByName("curious");
        else
            emotion?.SetEmotionByName("thinking");
    }

    void ExportCsv(string file, string keyword, float weight)
    {
        try
        {
            bool exists = File.Exists(csvPath);
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));

            using StreamWriter writer = new(csvPath, true);

            if (!exists)
                writer.WriteLine("Timestamp,Filename,Keyword,Weight,Mode");

            writer.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{file},{keyword},{weight:F2},{currentMode}"
            );
        }
        catch { }
    }
}