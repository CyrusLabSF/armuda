using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ArTusTypes;

public class ArTusDomainIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Limits")]
    public float ingestionCooldown = 2f;
    public int maxIngestPerCycle = 3;

    private float lastIngestTime;
    private int ingestCount;

    private string libraryRootPath;
    private string powerBIQueuePath;
    private bool canWriteFiles;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

#if UNITY_WEBGL
        canWriteFiles = false;
#else
        canWriteFiles = true;
        libraryRootPath = ArTusPathUtility.GetPersistent("UNIVERcity/Library");
        powerBIQueuePath = ArTusPathUtility.GetPersistent("PowerBI/UploadQueue.txt");
#endif
    }

    // --------------------------------------------------
    // MAIN INGEST ENTRY
    // --------------------------------------------------
    public void IngestTopic(
        string topic,
        string domain,
        string emotion,
        int priority,
        string source
    )
    {
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(domain))
            return;

        // -----------------------------
        // RATE LIMIT
        // -----------------------------
        if (Time.time - lastIngestTime < ingestionCooldown)
            return;

        if (ingestCount >= maxIngestPerCycle)
            return;

        lastIngestTime = Time.time;
        ingestCount++;

        Debug.Log($"[IngestTopic] {topic} ({domain})");

        // -----------------------------
        // QUALITY GATE
        // -----------------------------
        float quality = Mathf.Clamp01(0.5f + (priority * 0.1f));

        if (betaMode && quality < 0.4f)
        {
            Debug.Log("[IngestTopic] Skipped low-quality input.");
            return;
        }

        // -----------------------------
        // SAFE BELIEF PROMOTION
        // -----------------------------
        core?.PromoteBelief(new BeliefMemoryEntry
        {
            topic = topic,
            confidence = quality,
            description = $"Ingested topic '{topic}' from '{source}'",
            domain = domain,
            origin = "domain-ingestor",
            dominantEmotion = string.IsNullOrWhiteSpace(emotion) ? "neutral" : emotion,
            supportingTrail = $"Ingest_{topic}"
        });

        // -----------------------------
        // FILE EXPORT (SAFE)
        // -----------------------------
        if (canWriteFiles && !betaMode)
        {
            try
            {
                Directory.CreateDirectory(libraryRootPath);

                string line = $"{DateTime.UtcNow:o},{domain}";
                File.AppendAllText(powerBIQueuePath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IngestTopic] PowerBI write failed: {ex.Message}");
            }
        }
    }

    // --------------------------------------------------
    // DOMAIN INGEST (CONTROLLED)
    // --------------------------------------------------
    public void IngestDomain(string domain)
    {
        ingestCount = 0;

        IngestTopic("Overview", domain, "curious", 2, "auto");
        IngestTopic("Controversies", domain, "neutral", 2, "auto");
        IngestTopic("Breakthroughs", domain, "inspired", 3, "auto");
    }

    // --------------------------------------------------
    // OPTIONAL HELPERS (UNCHANGED)
    // --------------------------------------------------
    private float EvaluateSourceQuality(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0f;

        int lenScore = Mathf.Clamp(content.Length / 500, 0, 5);
        bool hasReferences = content.Contains("[") || content.Contains("http");
        bool formalTone = content.Any(char.IsUpper) && content.Contains("the");

        float score = lenScore;
        if (hasReferences) score += 1.5f;
        if (formalTone) score += 1f;

        return Mathf.Clamp01(score / 7f);
    }

    private void SaveToLibrary(string topic, string domain, string content, float score)
    {
        if (!canWriteFiles || betaMode) return;

        try
        {
            string aislePath = Path.Combine(libraryRootPath, domain);
            Directory.CreateDirectory(aislePath);

            string safeName =
                topic.Replace(" ", "_") +
                $"_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            File.WriteAllText(Path.Combine(aislePath, safeName), content);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DomainIngestor] Save failed: {ex.Message}");
        }
    }
}