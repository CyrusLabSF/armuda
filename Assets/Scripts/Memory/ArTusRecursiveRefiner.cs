using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArTusTypes;

public class ArTusRecursiveRefiner : MonoBehaviour
{
    [Header("Mode")]
    public bool betaMode = true;

    [Header("Refinement Settings")]
    [SerializeField] private int maxDepth = 2; // reduced for beta
    [SerializeField] private float refinementCooldown = 10f;

    [Header("Advanced Behavior")]
    [SerializeField] private bool enableBranching = true;
    [SerializeField] private int maxBranches = 1; // reduced for beta

    [Header("Limits")]
    public int maxMemoryLogsPerRun = 3;

    private string refinementLogPath;

    private ArTusCoreState core;
    private float lastRefineTime;

    private int memoryLogCount;

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        refinementLogPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Refinement/RecursiveRefinerLog.jsonl"
        );

        EnsureDirectory(refinementLogPath);
    }

    // ------------------------------------------------
    // ENTRY
    // ------------------------------------------------
    public void TryRecursiveRefinement(string seedTopic)
    {
        if (Time.time - lastRefineTime < refinementCooldown)
            return;

        if (string.IsNullOrWhiteSpace(seedTopic))
            return;

        lastRefineTime = Time.time;
        memoryLogCount = 0;

        Refine(seedTopic, 0);
    }

    // ------------------------------------------------
    // CORE
    // ------------------------------------------------
    private void Refine(string topic, int depth)
    {
        if (depth >= maxDepth)
            return;

        if (string.IsNullOrWhiteSpace(topic))
            return;

        // -----------------------------
        // SAFE MEMORY LOGGING
        // -----------------------------
        if (!betaMode && memoryLogCount < maxMemoryLogsPerRun)
        {
            core?.LogMemory(
                $"🔁 Refining '{topic}' (depth {depth})",
                "RecursiveRefinement",
                1,
                "thinking"
            );

            memoryLogCount++;
        }

        AppendLog(topic, depth);

        // -----------------------------
        // SAFE CONTEXT FETCH
        // -----------------------------
        var memories = core?.GetAllMemoryEntries();

        if (memories == null || memories.Count == 0)
            return;

        var related = memories
            .Where(m =>
                m != null &&
                !string.IsNullOrEmpty(m.content) &&
                m.content.ToLower().Contains(topic.ToLower()))
            .Take(2) // reduced
            .ToList();

        // -----------------------------
        // GENERATE FOLLOWUPS
        // -----------------------------
        var nextTopics = GenerateFollowups(topic, related);

        int branchCount = 0;

        foreach (var next in nextTopics)
        {
            if (string.IsNullOrWhiteSpace(next))
                continue;

            Refine(next, depth + 1);

            branchCount++;

            if (!enableBranching || branchCount >= maxBranches)
                break;
        }
    }

    // ------------------------------------------------
    // FOLLOWUP GENERATION
    // ------------------------------------------------
    private List<string> GenerateFollowups(string topic, List<MemoryEntry> context)
    {
        var results = new List<string>();

        results.Add($"{topic} causes");
        results.Add($"{topic} risks");

        if (context != null && context.Count > 0)
        {
            var strongest = context.OrderByDescending(m => m.score).FirstOrDefault();

            if (strongest != null && !string.IsNullOrEmpty(strongest.content))
            {
                results.Add(
                    strongest.content.Substring(
                        0,
                        Mathf.Min(30, strongest.content.Length)
                    )
                );
            }
        }

        return results;
    }

    // ------------------------------------------------
    // LOGGING
    // ------------------------------------------------
    private void AppendLog(string topic, int depth)
    {
        try
        {
            string line = JsonUtility.ToJson(new RecursiveRefineLog
            {
                topic = topic,
                depth = depth,
                timestamp = DateTime.UtcNow.ToString("o")
            });

            File.AppendAllText(refinementLogPath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RecursiveRefiner] Log failed: {ex.Message}");
        }
    }

    private static void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    // ------------------------------------------------
    // DATA
    // ------------------------------------------------
    [Serializable]
    private class RecursiveRefineLog
    {
        public string topic;
        public int depth;
        public string timestamp;
    }
}