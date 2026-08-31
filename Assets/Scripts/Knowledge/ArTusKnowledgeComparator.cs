using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using ArTusTypes;

public class ArTusKnowledgeComparator : MonoBehaviour
{
    private ArTusCoreState core;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Comparison Behavior")]
    public bool enableSimulationRequest = false;

    [Header("Performance Controls")]
    public int maxComparisonsPerRun = 25; // reduced for beta
    public int maxMemoryLogsPerRun = 5;

    private float lastRunTime;
    public float comparisonCooldown = 60f;

    private string csvOutputPath;

    private readonly List<string> trustedSources = new()
    {
        "OpenLibrary",
        "PubMed",
        "StanfordSEP",
        "WolframAlpha"
    };

    private static readonly HashSet<string> stopWords = new()
    {
        "about","there","which","their","between","system","method","using"
    };

    private readonly HashSet<string> recentComparisons = new();

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        csvOutputPath =
            ArTusPathUtility.GetPersistent(
                "UNIVERcity/System/KnowledgeComparison.csv"
            );

        EnsureCsv(
            csvOutputPath,
            "Timestamp,Topic,SourceA,SourceB,ScoreA,ScoreB,EmotionA,EmotionB,Contradiction,Summary,TrailID\n"
        );
    }

    private void Start()
    {
        InvokeRepeating(nameof(RunComparisonAnalysis), 60f, 86400f);
    }

    public void RunComparisonAnalysis()
    {
        if (Time.time - lastRunTime < comparisonCooldown)
            return;

        lastRunTime = Time.time;

        if (core == null || core.memoryLog == null || core.memoryLog.Count < 2)
            return;

        EnsureDir(csvOutputPath);

        var memories = core.memoryLog
            .Where(m => m != null && trustedSources.Contains(m.category))
            .TakeLast(50) // reduced dataset
            .ToList();

        if (memories.Count < 2)
            return;

        List<string> csvLines = new();

        int comparisonCount = 0;
        int memoryLogCount = 0;

        for (int i = 0; i < memories.Count; i++)
        {
            for (int j = i + 1; j < memories.Count; j++)
            {
                if (comparisonCount >= maxComparisonsPerRun)
                    goto WRITE_RESULTS;

                var a = memories[i];
                var b = memories[j];

                if (!SharedKeywords(a.content, b.content))
                    continue;

                string topic = GetSharedTopic(a.content, b.content);

                string pairKey = $"{a.category}-{b.category}-{topic}";
                if (recentComparisons.Contains(pairKey))
                    continue;

                recentComparisons.Add(pairKey);
                if (recentComparisons.Count > 100)
                    recentComparisons.Remove(recentComparisons.First());

                comparisonCount++;

                float delta = Mathf.Abs(a.score - b.score);
                float weighted = delta * ((a.score + b.score) / 2f);

                bool contradicts =
                    a.emotion != b.emotion ||
                    weighted >= 0.5f;

                string summary = contradicts
                    ? $"⚠️ Conflict on '{topic}' between {a.category} and {b.category}"
                    : $"✅ Alignment on '{topic}' between {a.category} and {b.category}";

                string line = string.Join(",",
                    Quote(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    Quote(topic),
                    Quote(a.category),
                    Quote(b.category),
                    Mathf.RoundToInt(a.score * 10f).ToString(),
                    Mathf.RoundToInt(b.score * 10f).ToString(),
                    Quote(a.emotion),
                    Quote(b.emotion),
                    contradicts.ToString(),
                    Quote(summary),
                    Quote($"Trail_Compare_{topic}")
                );

                csvLines.Add(line);

                // -----------------------------
                // SAFE MEMORY LOGGING
                // -----------------------------
                if (!betaMode && memoryLogCount < maxMemoryLogsPerRun)
                {
                    core.LogMemory(
                        summary,
                        contradicts ? "CrossSourceConflict" : "CrossSourceAlignment",
                        1,
                        contradicts ? "conflicted" : "reassured"
                    );

                    memoryLogCount++;
                }
            }
        }

    WRITE_RESULTS:

        if (csvLines.Count > 0)
        {
            try
            {
                File.AppendAllLines(csvOutputPath, csvLines);
            }
            catch (IOException ex)
            {
                Debug.LogError($"[Comparator] CSV write failed: {ex.Message}");
            }
        }

        Debug.Log($"[Comparator] Completed {comparisonCount} comparisons.");
    }

    private bool SharedKeywords(string a, string b)
    {
        var wordsA = a.ToLower().Split(' ');
        var wordsB = b.ToLower().Split(' ');

        int matches = 0;

        for (int i = 0; i < wordsA.Length; i++)
        {
            for (int j = 0; j < wordsB.Length; j++)
            {
                if (wordsA[i] == wordsB[j])
                {
                    matches++;
                    if (matches >= 3)
                        return true;
                }
            }
        }

        return false;
    }

    private string GetSharedTopic(string a, string b)
    {
        var shared = a.ToLower().Split(' ')
            .Intersect(b.ToLower().Split(' '))
            .Where(w => w.Length > 4 && !stopWords.Contains(w))
            .ToList();

        return shared.OrderByDescending(w => w.Length).FirstOrDefault() ?? "unknown";
    }

    private static void EnsureDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static void EnsureCsv(string filePath, string header)
    {
        EnsureDir(filePath);
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, header);
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        if (value.Contains(",") || value.Contains("\""))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}