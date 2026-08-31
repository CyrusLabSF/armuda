using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class ArTusResourceLoader : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior")]
    public bool enableSpeech = false;
    public int batchSize = 25;
    public float batchDelaySeconds = 0.01f;

    [Header("Limits")]
    public int maxEntriesPerIngest = 200;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    // ==================================================
    // WEBGL / GENERIC INGESTION
    // ==================================================
    public void IngestTextPayload(
        string rawText,
        string topic,
        string sourceLabel = "InjectedText"
    )
    {
        if (string.IsNullOrWhiteSpace(rawText) || core == null)
            return;

        string[] lines = rawText.Split('\n');
        StartCoroutine(
            IngestLinesCoroutine(lines, topic, sourceLabel)
        );
    }

    // --------------------------------------------------
    // LEGACY COMPATIBILITY (IMPORTANT)
    // --------------------------------------------------
    public void IngestTextResource(string resourcePath, string topic)
    {
    #if UNITY_WEBGL
    Debug.LogWarning("[ResourceLoader] File ingestion not supported in WebGL.");
    return;
    #else
        if (string.IsNullOrEmpty(resourcePath) || !File.Exists(resourcePath))
        {
            Debug.LogWarning($"[ResourceLoader] File not found: {resourcePath}");
            return;
        }

        string rawText;

        try
        {
            rawText = File.ReadAllText(resourcePath);
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ResourceLoader] Failed to read file: {ex.Message}");
            return;
        }

        // Route into new system
        IngestTextPayload(rawText, topic, Path.GetFileName(resourcePath));
    #endif
    }

    // ==================================================
    // CORE INGESTION LOOP (SAFE)
    // ==================================================
    private IEnumerator IngestLinesCoroutine(
        string[] lines,
        string topic,
        string sourceLabel
    )
    {
        if (core == null || lines == null)
            yield break;

        topic = Normalize(topic);

        int entriesLogged = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (entriesLogged >= maxEntriesPerIngest)
                break;

            string content = lines[i]?.Trim();
            if (string.IsNullOrWhiteSpace(content))
                continue;

            string emotion = DetectEmotion(content);
            int score = EstimateConceptWeight(content);

            // 🔒 SAFE MEMORY LOGGING
            if (!betaMode)
            {
                core.LogMemory(
                    $"📘 {content}",
                    topic,
                    score,
                    emotion,
                    sourceLabel
                );
            }

            entriesLogged++;

            if (entriesLogged % batchSize == 0)
                yield return new WaitForSeconds(batchDelaySeconds);
        }

        // Final summary log
        if (!betaMode)
        {
            core.LogMemory(
                $"📚 Completed ingestion of {entriesLogged} entries from {sourceLabel}.",
                "ResourceIngestion",
                2,
                "organized",
                sourceLabel
            );
        }

        // 🔇 Speech disabled in beta
        if (!betaMode && enableSpeech)
        {
            speech?.Speak(
                $"I’ve processed {entriesLogged} concepts from {sourceLabel}."
            );
        }

        Debug.Log($"[ResourceLoader] Ingested {entriesLogged} entries.");
    }

    // ==================================================
    // UTILITIES
    // ==================================================
    private string Normalize(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? "general"
            : text.Trim().ToLowerInvariant();
    }

    private string DetectEmotion(string text)
    {
        text = text.ToLowerInvariant();

        if (text.Contains("miracle") || text.Contains("origin") || text.Contains("emerged"))
            return "awe";
        if (text.Contains("death") || text.Contains("extinction"))
            return "somber";
        if (text.Contains("replication") || text.Contains("efficiency"))
            return "curious";
        if (text.Contains("change") || text.Contains("variation"))
            return "adaptable";

        return "neutral";
    }

    private int EstimateConceptWeight(string text)
    {
        int baseScore = 2;

        if (text.Length > 150) baseScore += 1;
        if (text.Contains(":")) baseScore += 1;
        if (text.Contains("->")) baseScore += 1;

        return Mathf.Clamp(baseScore, 1, 5);
    }
}