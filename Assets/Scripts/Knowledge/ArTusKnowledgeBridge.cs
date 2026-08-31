using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;
using ArTusTypes;

public class ArTusKnowledgeBridge : MonoBehaviour
{
    [Header("Core References")]
    public ArTusCoreState core;
    public ArTusShapeKnowledgeBridge shapeKnowledgeBridge;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Connection")]
    [SerializeField]
    private string baseURL = "http://127.0.0.1:8000";

    [Header("Behavior")]
    public float requestTimeout = 5f;
    public bool autoScheduleReflection = true;
    public float retryCooldownSeconds = 45f;
    public bool suppressRepeatedConnectionWarnings = true;

    [Header("Storage (WebGL Safe)")]
    [SerializeField]
    private string knowledgeRootRelative =
        "UNIVERcity/Knowledge/External";

    private string KnowledgeRootPath =>
        ArTusPathUtility.GetPersistent(knowledgeRootRelative);

    private string KnowledgeIndexPath =>
        Path.Combine(KnowledgeRootPath, "knowledge_records.json");

    private float nextAllowedRequestTime;
    private bool loggedCooldownWarning;

    private void Awake()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
        if (shapeKnowledgeBridge == null)
            shapeKnowledgeBridge = GetComponent<ArTusShapeKnowledgeBridge>() ?? FindAnyObjectByType<ArTusShapeKnowledgeBridge>();

        try
        {
            Directory.CreateDirectory(KnowledgeRootPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KnowledgeBridge] Init failed: {ex.Message}");
        }
    }

    // =====================================================
    // PUBLIC API
    // =====================================================

    public void QueryAndIngest(
        string topic,
        string route,
        string domain
    )
    {
        if (string.IsNullOrWhiteSpace(topic) ||
            string.IsNullOrWhiteSpace(route))
            return;

        if (Time.unscaledTime < nextAllowedRequestTime)
        {
            if (!loggedCooldownWarning)
            {
                Debug.LogWarning(
                    $"[KnowledgeBridge] Request cooldown active until {nextAllowedRequestTime:F1}s after repeated connection failures."
                );
                loggedCooldownWarning = true;
            }

            if (!betaMode)
            {
                core?.LogMemory(
                    $"Knowledge bridge cooldown skipped '{topic}'.",
                    "KnowledgeBridge",
                    1,
                    "alert"
                );
            }
            return;
        }

        loggedCooldownWarning = false;

        StartCoroutine(QueryRoutine(topic, route, domain));
    }

    // =====================================================
    // CORE ROUTINE
    // =====================================================

    private IEnumerator QueryRoutine(
        string topic,
        string route,
        string domain
    )
    {
        string trailID = BuildTrailID(route, topic);

        string url =
            $"{baseURL.TrimEnd('/')}/{route}?q={UnityWebRequest.EscapeURL(topic)}";

        using UnityWebRequest req = UnityWebRequest.Get(url);

        // 🔒 CRITICAL FIX: prevent hanging
        req.timeout = Mathf.RoundToInt(requestTimeout);

        DateTime start = DateTime.UtcNow;

        yield return req.SendWebRequest();

        double latency =
            (DateTime.UtcNow - start).TotalMilliseconds;

        if (req.result != UnityWebRequest.Result.Success)
        {
            HandleRequestFailure(topic, req.error);

            if (!betaMode)
            {
                core?.LogMemory(
                    $"❌ Bridge failed for '{topic}' ({req.error})",
                    "KnowledgeBridge",
                    1,
                    "alert"
                );
            }

            yield break;
        }

        string payload = req.downloadHandler.text;

        if (string.IsNullOrWhiteSpace(payload))
        {
            Debug.LogWarning("[KnowledgeBridge] Empty payload.");
            yield break;
        }

        var record = BuildKnowledgeRecord(
            topic,
            route,
            domain,
            url,
            trailID,
            payload
        );

        PersistKnowledgeRecord(record);
        AttachKnowledgeToMemory(record);
        shapeKnowledgeBridge?.NotifyKnowledgeUpdated(record);
        nextAllowedRequestTime = 0f;
        loggedCooldownWarning = false;

        Debug.Log(
            $"[KnowledgeBridge] Success: {topic} ({latency:F0}ms)"
        );

        // -----------------------------
        // SAFE MEMORY LOGGING
        // -----------------------------
        if (!betaMode)
        {
            core?.LogMemory(
                $"🌐 External knowledge ingested for '{topic}' via '{route}'",
                "KnowledgeBridge",
                1,
                "thinking",
                trailID
            );
        }

        // -----------------------------
        // SAFE REFLECTION TRIGGER
        // -----------------------------
        if (autoScheduleReflection)
        {
            core?.ScheduleReflection(
                $"External knowledge received for {domain}",
                "ingestion"
            );
        }

        // -----------------------------
        // 🔥 OPTIONAL FUTURE HOOK
        // -----------------------------
        // Example:
        // resourceLoader?.IngestTextPayload(payload, domain, route);
    }

    // =====================================================
    // STORAGE
    // =====================================================

    private void PersistKnowledgeRecord(KnowledgeRecord record)
    {
        try
        {
            string domainDir = Path.Combine(
                KnowledgeRootPath,
                Sanitize(record.domain)
            );

            Directory.CreateDirectory(domainDir);

            string filePath =
                Path.Combine(domainDir, $"{record.trailID}.json");

            File.WriteAllText(
                filePath,
                JsonUtility.ToJson(record, true),
                Encoding.UTF8
            );

            var wrapper = LoadKnowledgeIndex();
            wrapper.entries.Add(record);

            File.WriteAllText(
                KnowledgeIndexPath,
                JsonUtility.ToJson(wrapper, true),
                Encoding.UTF8
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[KnowledgeBridge] Persist failed: {ex.Message}"
            );
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private string BuildTrailID(string route, string topic)
    {
        string safeTopic = Sanitize(topic);

        return
            $"Trail_{route}_{safeTopic}_{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        foreach (char c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');

        return input.Replace(" ", "_").ToLowerInvariant();
    }

    private void AttachKnowledgeToMemory(KnowledgeRecord record)
    {
        if (core == null || record == null)
            return;

        string summary = string.IsNullOrWhiteSpace(record.summary)
            ? record.topic
            : record.summary;

        MemoryEntry entry = core.LogMemory(
            $"topic: {record.topic} | evidence: {summary}",
            "ExternalKnowledge",
            Mathf.Clamp(record.confidence, 0.35f, 1f),
            "thinking",
            "ArTus",
            record.trailID,
            record.id
        );

        if (entry == null)
            return;

        entry.sourceType = record.sourceType;
        entry.sourceURL = record.sourceUrl;
        entry.trailID = record.trailID;
        entry.originTrailID = record.id;
        entry.clarity = Mathf.Clamp01(record.confidence);
        entry.confidence = Mathf.Clamp01(record.confidence);
        entry.tags = new List<string>(record.tags ?? new List<string>());
        entry.tags.Add($"topic:{record.topic}");
        entry.tags.Add($"domain:{record.domain}");
        entry.tags.Add($"route:{record.route}");
        entry.relatedBeliefs = new List<string> { record.topic };

        core.PromoteBelief(
            new BeliefMemoryEntry(
                record.topic,
                Mathf.Clamp01(record.confidence),
                record.route,
                "thinking",
                record.trailID,
                record.domain
            )
            {
                description = summary
            }
        );
    }

    private void HandleRequestFailure(string topic, string error)
    {
        bool connectionFailure =
            !string.IsNullOrWhiteSpace(error) &&
            (
                error.IndexOf("Cannot connect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("destination host", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0
            );

        if (connectionFailure && retryCooldownSeconds > 0f)
            nextAllowedRequestTime = Time.unscaledTime + retryCooldownSeconds;

        if (!suppressRepeatedConnectionWarnings || !connectionFailure || !loggedCooldownWarning)
        {
            string suffix = connectionFailure && retryCooldownSeconds > 0f
                ? $" Backing off for {retryCooldownSeconds:F0}s."
                : string.Empty;

            Debug.LogWarning($"[KnowledgeBridge] Failed: {error}.{suffix}");
        }

        loggedCooldownWarning = connectionFailure;

        if (!betaMode)
        {
            core?.LogMemory(
                $"Knowledge bridge request failed for '{topic}' ({error}).",
                "KnowledgeBridge",
                1,
                "alert"
            );
        }
    }

    private KnowledgeRecord BuildKnowledgeRecord(
        string topic,
        string route,
        string domain,
        string sourceUrl,
        string trailID,
        string payload
    )
    {
        string normalized = NormalizePayload(payload);
        List<string> evidence = ExtractEvidence(normalized);

        return new KnowledgeRecord
        {
            topic = topic,
            domain = string.IsNullOrWhiteSpace(domain) ? "general" : domain,
            route = route,
            query = topic,
            sourceUrl = sourceUrl,
            summary = BuildSummary(topic, evidence, normalized),
            rawPayload = payload,
            evidence = evidence,
            confidence = EstimateConfidence(normalized, evidence),
            trailID = trailID,
            tags = BuildTags(topic, domain, route, evidence)
        };
    }

    private KnowledgeRecordWrapper LoadKnowledgeIndex()
    {
        try
        {
            if (!File.Exists(KnowledgeIndexPath))
                return new KnowledgeRecordWrapper();

            string json = File.ReadAllText(KnowledgeIndexPath, Encoding.UTF8);
            return JsonUtility.FromJson<KnowledgeRecordWrapper>(json)
                ?? new KnowledgeRecordWrapper();
        }
        catch
        {
            return new KnowledgeRecordWrapper();
        }
    }

    private static string NormalizePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;

        return payload
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\\\"", "\"")
            .Trim();
    }

    private static List<string> ExtractEvidence(string payload)
    {
        var evidence = payload
            .Split(new[] { '.', '!', '?', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 20)
            .Take(3)
            .ToList();

        if (evidence.Count == 0 && !string.IsNullOrWhiteSpace(payload))
            evidence.Add(payload.Length > 240 ? payload.Substring(0, 240) : payload);

        return evidence;
    }

    private static string BuildSummary(string topic, List<string> evidence, string payload)
    {
        if (evidence != null && evidence.Count > 0)
            return evidence[0];

        if (string.IsNullOrWhiteSpace(payload))
            return $"Knowledge retrieved for {topic}.";

        return payload.Length > 180 ? payload.Substring(0, 180) : payload;
    }

    private static float EstimateConfidence(string payload, List<string> evidence)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return 0.2f;

        float confidence = 0.45f;

        if (payload.TrimStart().StartsWith("{") || payload.TrimStart().StartsWith("["))
            confidence += 0.15f;

        confidence += Mathf.Min(0.25f, (evidence?.Count ?? 0) * 0.08f);
        confidence += Mathf.Min(0.15f, payload.Length / 2000f);

        return Mathf.Clamp01(confidence);
    }

    private static List<string> BuildTags(
        string topic,
        string domain,
        string route,
        List<string> evidence
    )
    {
        var tags = new List<string>
        {
            topic?.ToLowerInvariant() ?? "unknown",
            domain?.ToLowerInvariant() ?? "general",
            route?.ToLowerInvariant() ?? "web"
        };

        if (evidence != null && evidence.Count > 1)
            tags.Add("multi_evidence");

        return tags;
    }
}
