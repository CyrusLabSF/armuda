using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ArTusTypes;

public class ArTusVectorMemory : MonoBehaviour
{
    [Header("Ingest Settings")]
    public string ingestFolder = "IngestedTopics";
    public string matchLogPath = "VectorMatchLog.csv";
    public bool enableLogging = true;
    public string knowledgeIndexRelativePath = "UNIVERcity/Knowledge/External/knowledge_records.json";

    [Header("Tuning")]
    [Range(0f, 1f)] public float minConfidence = 0.15f;
    [Range(0f, 1f)] public float knowledgeRecordBoost = 0.12f;

    // Internal data
    private readonly Dictionary<string, string> summaries = new();
    private readonly Dictionary<string, Dictionary<string, float>> vectorIndex = new();
    private readonly HashSet<string> vocabulary = new();
    private readonly Dictionary<string, RecordMetadata> metadataIndex = new();

    private string KnowledgeIndexPath =>
        ArTusPathUtility.GetPersistent(knowledgeIndexRelativePath);

    private string IngestFolderPath =>
        Path.IsPathRooted(ingestFolder)
            ? ingestFolder
            : ArTusPathUtility.GetPersistent(ingestFolder);

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        ReloadMemory();
    }

    // --------------------------------------------------
    // 🔁 PUBLIC RELOAD (IMPORTANT FOR LIVE SYSTEM)
    // --------------------------------------------------
    public void ReloadMemory()
    {
        LoadAllSummaries();
        BuildVectorIndex();

        Debug.Log($"[VectorMemory] Reloaded {summaries.Count} topics.");
    }

    // --------------------------------------------------
    // 🔍 PUBLIC QUERY INTERFACE
    // --------------------------------------------------
    public List<MatchResult> GetTopMatches(string query, int topN = 3)
    {
        if (string.IsNullOrWhiteSpace(query) || vectorIndex.Count == 0)
            return null;

        var queryVector = ComputeTF(query);
        var results = new List<MatchResult>();

        foreach (var kvp in vectorIndex)
        {
            float score = CosineSimilarity(queryVector, kvp.Value);
            metadataIndex.TryGetValue(kvp.Key, out var metadata);

            if (metadata != null && metadata.retrievalSource == "KnowledgeRecord")
                score = Mathf.Clamp01(score + knowledgeRecordBoost);

            if (score < minConfidence) continue;

            results.Add(new MatchResult
            {
                topic = kvp.Key,
                summary = summaries[kvp.Key],
                confidence = score,
                sourceTrail = kvp.Key,
                retrievalTime = DateTime.UtcNow.ToString("o"),
                retrievalSource = metadata?.retrievalSource ?? "VectorMemory",
                sourceUrl = metadata?.sourceUrl,
                evidenceSummary = metadata?.evidenceSummary,
                domain = metadata?.domain,
                evidenceCount = metadata?.evidenceCount ?? 0,
                isEvidenceBacked = metadata?.retrievalSource == "KnowledgeRecord"
            });
        }

        if (results.Count == 0)
            return null;

        var ordered = results
            .OrderByDescending(r => r.confidence)
            .Take(topN)
            .ToList();

        if (enableLogging && ordered.Count > 0)
            LogMatch(query, ordered[0]);

        return ordered;
    }

    // --------------------------------------------------
    // 🔥 STRONG MATCH CHECK (VERY USEFUL)
    // --------------------------------------------------
    public bool HasStrongMatch(string query, float threshold = 0.35f)
    {
        var matches = GetTopMatches(query, 1);

        if (matches == null || matches.Count == 0)
            return false;

        return matches[0].confidence >= threshold;
    }

    // --------------------------------------------------
    // INGESTION
    // --------------------------------------------------
    private void LoadAllSummaries()
    {
        summaries.Clear();
        metadataIndex.Clear();

        if (!Directory.Exists(IngestFolderPath))
        {
            Directory.CreateDirectory(IngestFolderPath);
            Debug.Log($"[VectorMemory] Created ingest folder: {IngestFolderPath}");
        }

        if (Directory.Exists(IngestFolderPath))
        {
            foreach (var file in Directory.GetFiles(IngestFolderPath, "*.txt"))
            {
                string topic = Path.GetFileNameWithoutExtension(file);
                string content = File.ReadAllText(file);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    summaries[topic] = content;
                    metadataIndex[topic] = new RecordMetadata
                    {
                        retrievalSource = "VectorMemory",
                        evidenceSummary = content
                    };
                }
            }
        }

        LoadKnowledgeRecords();
    }

    private void BuildVectorIndex()
    {
        vectorIndex.Clear();
        vocabulary.Clear();

        foreach (var summary in summaries.Values)
        {
            foreach (var token in Tokenize(summary))
                vocabulary.Add(token);
        }

        foreach (var kvp in summaries)
        {
            vectorIndex[kvp.Key] = ComputeTF(kvp.Value);
        }
    }

    private void LoadKnowledgeRecords()
    {
        if (!File.Exists(KnowledgeIndexPath))
            return;

        try
        {
            string json = File.ReadAllText(KnowledgeIndexPath);
            var wrapper = JsonUtility.FromJson<KnowledgeRecordWrapper>(json);

            if (wrapper?.entries == null)
                return;

            foreach (var record in wrapper.entries)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.topic))
                    continue;

                string key = $"knowledge::{record.topic.ToLowerInvariant()}::{record.id}";
                string searchable = BuildSearchableText(record);

                if (string.IsNullOrWhiteSpace(searchable))
                    continue;

                summaries[key] = searchable;
                metadataIndex[key] = new RecordMetadata
                {
                    sourceUrl = record.sourceUrl,
                    evidenceSummary = record.summary,
                    retrievalSource = "KnowledgeRecord",
                    domain = record.domain,
                    evidenceCount = record.evidence?.Count ?? 0
                };
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VectorMemory] Failed to load knowledge records: {ex.Message}");
        }
    }

    private static string BuildSearchableText(KnowledgeRecord record)
    {
        var parts = new List<string>
        {
            record.topic,
            record.domain,
            record.summary
        };

        if (record.evidence != null && record.evidence.Count > 0)
            parts.AddRange(record.evidence);

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    // --------------------------------------------------
    // TEXT → VECTOR
    // --------------------------------------------------
    private Dictionary<string, float> ComputeTF(string text)
    {
        var tf = new Dictionary<string, float>();
        var tokens = Tokenize(text);

        if (tokens.Count == 0)
            return tf;

        foreach (var token in tokens)
        {
            if (!tf.ContainsKey(token))
                tf[token] = 0f;

            tf[token] += 1f;
        }

        float count = tokens.Count;

        foreach (var key in tf.Keys.ToList())
            tf[key] /= count;

        return tf;
    }

    private List<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', ';', ':', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();
    }

    // --------------------------------------------------
    // VECTOR MATH
    // --------------------------------------------------
    private float CosineSimilarity(
        Dictionary<string, float> a,
        Dictionary<string, float> b)
    {
        float dot = 0f;
        float magA = 0f;
        float magB = 0f;

        foreach (var token in vocabulary)
        {
            float va = a.ContainsKey(token) ? a[token] : 0f;
            float vb = b.ContainsKey(token) ? b[token] : 0f;

            dot += va * vb;
            magA += va * va;
            magB += vb * vb;
        }

        if (magA == 0f || magB == 0f)
            return 0f;

        return dot / (Mathf.Sqrt(magA) * Mathf.Sqrt(magB));
    }

    // --------------------------------------------------
    // LOGGING
    // --------------------------------------------------
    private void LogMatch(string query, MatchResult result)
    {
        try
        {
            bool writeHeader = !File.Exists(matchLogPath);

            using var sw = new StreamWriter(matchLogPath, true);

            if (writeHeader)
                sw.WriteLine("Timestamp,Query,MatchedTopic,Confidence,Source");

            sw.WriteLine(
                $"{DateTime.UtcNow:o}," +
                $"\"{query.Replace("\"", "")}\"," +
                $"{result.topic}," +
                $"{result.confidence:F4}," +
                $"{result.retrievalSource}"
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VectorMemory] Logging failed: {ex.Message}");
        }
    }

    // --------------------------------------------------
    // DATA STRUCT
    // --------------------------------------------------
    [Serializable]
    public class MatchResult
    {
        public string topic;
        public string summary;
        public float confidence;
        public string sourceTrail;
        public string retrievalTime;
        public string retrievalSource;
        public string sourceUrl;
        public string evidenceSummary;
        public string domain;
        public int evidenceCount;
        public bool isEvidenceBacked;
    }

    private class RecordMetadata
    {
        public string sourceUrl;
        public string evidenceSummary;
        public string retrievalSource;
        public string domain;
        public int evidenceCount;
    }
}
