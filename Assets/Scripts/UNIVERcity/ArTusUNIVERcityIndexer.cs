using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArTusUNIVERcityIndexer : MonoBehaviour
{
    private ArTusCoreState core;

    [System.Serializable]
    public class IndexedTopic
    {
        public string topic;
        public string category;
        public string emotion;
        public float score;
        public float clarity = 1.0f; // Optional for future clarity layering
        public float confidence = 0.5f;
        public string sourceURL;
        public string evidenceSummary;
        public string sourceType;
    }

    public List<IndexedTopic> index = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
    }

    public void BuildIndex()
    {
        if (core == null) return;

        var entries = core.GetAllMemoryEntries();
        index.Clear();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.content)) continue;

            string category = entry.category?.Trim().ToLower() ?? "general";
            string topic = ExtractTopic(entry);

            if (string.IsNullOrEmpty(topic)) continue;

            bool exists = index.Any(i => i.topic == topic && i.category == category);
            if (!exists)
            {
                index.Add(new IndexedTopic
                {
                    topic = topic,
                    category = category,
                    emotion = entry.emotion?.ToLower() ?? "neutral",
                    score = entry.score,
                    clarity = entry.clarity,
                    confidence = entry.confidence > 0f ? entry.confidence : entry.score,
                    sourceURL = entry.sourceURL,
                    evidenceSummary = BuildEvidenceSummary(entry),
                    sourceType = entry.sourceType
                });
            }
        }

        Debug.Log($"[UNIVERcity Indexer] ✅ Indexed {index.Count} topics.");
    }

    public List<IndexedTopic> GetByCategory(string category)
    {
        return index.Where(i => i.category == category.ToLower()).ToList();
    }

    public List<IndexedTopic> GetByEmotion(string emotion)
    {
        return index.Where(i => i.emotion == emotion.ToLower()).ToList();
    }

    public void SpeakSummaryByCategory(string category)
    {
        var results = GetByCategory(category);
        if (results.Count == 0)
        {
            core?.TriggerVoice($"I have not explored much in {category}.");
        }
        else
        {
            core?.TriggerVoice($"In {category}, I’ve explored {results.Count} topics.");
        }
    }

    private static string ExtractTopic(ArTusTypes.MemoryEntry entry)
    {
        if (entry.tags != null)
        {
            string taggedTopic = entry.tags
                .FirstOrDefault(t => t.StartsWith("topic:", System.StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(taggedTopic))
                return taggedTopic.Substring("topic:".Length).Trim();
        }

        string content = entry.content ?? "";

        int topicIndex = content.IndexOf("topic:", System.StringComparison.OrdinalIgnoreCase);
        if (topicIndex >= 0)
        {
            string candidate = content.Substring(topicIndex + "topic:".Length).Trim();
            int separator = candidate.IndexOf('|');
            return separator >= 0 ? candidate.Substring(0, separator).Trim() : candidate;
        }

        string[] parts = content.Split(':');
        if (parts.Length >= 2)
            return parts[1].Trim();

        return content.Trim();
    }

    private static string BuildEvidenceSummary(ArTusTypes.MemoryEntry entry)
    {
        string content = entry.content ?? "";
        int evidenceIndex = content.IndexOf("evidence:", System.StringComparison.OrdinalIgnoreCase);
        if (evidenceIndex >= 0)
            return content.Substring(evidenceIndex + "evidence:".Length).Trim();

        return content;
    }
}
