using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

public class ArTusThoughtDiverter : MonoBehaviour
{
    private ArTusCoreState core;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Emotion Regulation")]
    public int sadnessThreshold = 4; // slightly higher for beta
    public float scanIntervalSeconds = 20f;
    public float suppressionCooldownHours = 2f;

    private readonly Dictionary<string, int> sadnessCounts = new();
    private readonly Dictionary<string, DateTime> suppressedUntil = new();

    private float nextScanTime;

    private string suppressionLog =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/SadnessSuppressionLog.csv"
        );

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        PrepareLog();
        nextScanTime = Time.time + scanIntervalSeconds;
    }

    void Update()
    {
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + scanIntervalSeconds;
        ScanForSadnessPatterns();
    }

    // ==================================================
    // CORE LOGIC
    // ==================================================
    private void ScanForSadnessPatterns()
    {
        if (core == null)
            return;

        var memories = core.GetAllMemoryEntries();
        if (memories == null || memories.Count < 10)
            return;

        sadnessCounts.Clear();

        foreach (var m in memories)
        {
            if (m.emotion != "sad")
                continue;

            string topic = Normalize(m.content);

            if (!sadnessCounts.ContainsKey(topic))
                sadnessCounts[topic] = 0;

            sadnessCounts[topic]++;
        }

        foreach (var kvp in sadnessCounts)
        {
            if (kvp.Value < sadnessThreshold)
                continue;

            if (IsCurrentlySuppressed(kvp.Key))
                continue;

            SuppressTopic(kvp.Key, kvp.Value);
        }
    }

    private void SuppressTopic(string topic, int count)
    {
        DateTime until =
            DateTime.UtcNow.AddHours(suppressionCooldownHours);

        suppressedUntil[topic] = until;

        // 🔇 NO speech in beta

        if (!betaMode)
        {
            core?.LogMemory(
                $"🧠 Suppressed topic '{topic}' after {count} sad occurrences.",
                "ThoughtDiverter",
                1,
                "sad",
                topic
            );
        }

        AppendLog(topic, count, until);

        Debug.Log($"[ThoughtDiverter] Suppressed: {topic}");
    }

    // ==================================================
    // PUBLIC API (CRITICAL FOR THOUGHT SYSTEM)
    // ==================================================
    public bool IsTopicIgnored(string topic)
    {
        topic = Normalize(topic);

        if (!suppressedUntil.TryGetValue(topic, out var until))
            return false;

        if (DateTime.UtcNow > until)
        {
            suppressedUntil.Remove(topic);
            return false;
        }

        return true;
    }

    // ==================================================
    // UTILITIES
    // ==================================================
    private string Normalize(string text)
    {
        return text?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private void PrepareLog()
    {
        try
        {
            string dir = Path.GetDirectoryName(suppressionLog);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(suppressionLog))
            {
                File.WriteAllText(
                    suppressionLog,
                    "Timestamp,Topic,Occurrences,SuppressedUntil\n"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThoughtDiverter] Init failed: {ex.Message}");
        }
    }

    private void AppendLog(string topic, int count, DateTime until)
    {
        try
        {
            File.AppendAllText(
                suppressionLog,
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}," +
                $"{topic}," +
                $"{count}," +
                $"{until:yyyy-MM-dd HH:mm:ss}\n"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThoughtDiverter] Log failed: {ex.Message}");
        }
    }

    private bool IsCurrentlySuppressed(string topic)
    {
        if (!suppressedUntil.TryGetValue(topic, out var until))
            return false;

        if (DateTime.UtcNow > until)
        {
            suppressedUntil.Remove(topic);
            return false;
        }

        return true;
    }
}