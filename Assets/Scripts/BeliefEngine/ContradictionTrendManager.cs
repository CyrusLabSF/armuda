using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ArTusTypes;

public class ContradictionTrendManager : MonoBehaviour
{
    private ArTusCoreState core;
    private string contradictionLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/ContradictionTrend.csv";
    public List<ContradictionTrendEntry> trackedTrends = new();

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(contradictionLogPath));
            if (!File.Exists(contradictionLogPath))
            {
                // Use FileIOManager instead of direct File.WriteAllText
                FileIOManager.QueueWrite(
                    contradictionLogPath,
                    "Timestamp,Topic,Domain,ConflictCount,Severity,Certainty\n",
                    "ContradictionTrendHeader"
                );
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ContradictionTrendManager] ❌ Failed to init file: {ex.Message}");
        }

        ScanAllBeliefs();
    }

    public void ScanAllBeliefs()
    {
        if (core == null || core.beliefs == null || core.beliefs.Count == 0)
        {
            Debug.LogWarning("[ContradictionTrendManager] No beliefs to scan.");
            return;
        }

        int loggedCount = 0;
        DateTime startTime = DateTime.Now;

        foreach (var kvp in core.beliefs)
        {
            string topic = kvp.Key;
            var belief = kvp.Value;
            if (belief == null) continue;

            int contradictionCount = belief.contradictionCount;
            string domain = string.IsNullOrEmpty(belief.domain) ? "general" : belief.domain;

            if (contradictionCount > 0)
            {
                var entry = new ContradictionTrendEntry
                {
                    topic = topic,
                    domain = domain,
                    conflictCount = contradictionCount,
                    severity = GetSeverityLabel(contradictionCount),
                    certaintyAtDetection = belief.confidenceScore,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                trackedTrends.Add(entry);

                try
                {
                    string line =
                        $"{entry.timestamp},{entry.topic},{entry.domain},{entry.conflictCount},{entry.severity},{entry.certaintyAtDetection:F2}\n";

                    // ✅ Async safe write
                    FileIOManager.QueueWrite(contradictionLogPath, line, "ContradictionTrend", append: true);
                    loggedCount++;
                }
                catch (IOException ex)
                {
                    Debug.LogError($"[ContradictionTrendManager] ❌ Failed to log entry: {ex.Message}");
                }
            }
        }

        Debug.Log($"[ContradictionTrendManager] ✅ Logged {loggedCount} contradictions in {DateTime.Now - startTime}.");
    }

    public string GetSeverityLabel(int count)
    {
        return count switch
        {
            >= 5 => "high",
            >= 3 => "moderate",
            _ => "low"
        };
    }
}
