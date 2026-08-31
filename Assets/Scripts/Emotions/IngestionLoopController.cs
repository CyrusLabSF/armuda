using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

[Serializable]
public class IngestedTopic
{
    public string topic;
    public string domain;
    public List<string> tags;
    public float curiosityScore;
}

[Serializable]
public class TopicWrapper
{
    public List<IngestedTopic> topics;
}

[Serializable]
public class LoopClosureLog
{
    public string topic;
    public string domain;
    public List<string> triggeredTags;
    public string simulationSummary;
    public bool contradictionResolved;
    public float beliefChangeDelta;
    public string timestamp;
}

public class IngestionLoopController : MonoBehaviour
{
    public string ingestionPath = "D:/ArTusCloud-Deployment/UNIVERcity/Ingested/IngestedTopics.json";
    public string loopClosureLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/LoopClosures/";

    private ArTusCoreState core;
    private ArTusCuriosityEngine curiosityEngine;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        curiosityEngine = GetComponent<ArTusCuriosityEngine>();

        InvokeRepeating(nameof(ProcessTopics), 5f, 1800f); // every 30 minutes
    }

    private void ProcessTopics()
    {
        if (!File.Exists(ingestionPath))
        {
            Debug.Log("[IngestionLoop] No ingestion file found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(ingestionPath);
            var wrapper = JsonUtility.FromJson<TopicWrapper>(json);

            if (wrapper?.topics == null || wrapper.topics.Count == 0)
            {
                Debug.Log("[IngestionLoop] No topics to process.");
                return;
            }

            foreach (var topic in wrapper.topics)
            {
                if (topic == null || topic.tags == null) continue;

                bool shouldIngest = topic.tags.Contains("contradiction")
                                 || topic.tags.Contains("emotionally_resonant")
                                 || topic.tags.Contains("multi_domain")
                                 || topic.curiosityScore > 0.7f;

                if (shouldIngest)
                {
                    LogTopicEntry(topic);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IngestionLoop] ❌ Failed to process topics: {ex.Message}");
        }
    }

    private void LogTopicEntry(IngestedTopic topic)
    {
        string summary = $"🧠 Ingested topic '{topic.topic}' from domain '{topic.domain}'. Curiosity score: {topic.curiosityScore:F2}. Tags: {string.Join(", ", topic.tags)}";
        core?.LogMemory(summary, "IngestedTopic", 2, "curious");

        try
        {
            Directory.CreateDirectory(loopClosureLogPath);
            string safeFilename = $"{topic.domain}_{topic.topic}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string logPath = Path.Combine(loopClosureLogPath, safeFilename);

            var log = new LoopClosureLog
            {
                topic = topic.topic,
                domain = topic.domain,
                triggeredTags = topic.tags,
                simulationSummary = "🌀 Simulation logic disabled. Symbolic ingestion only.",
                contradictionResolved = false,
                beliefChangeDelta = 0f,
                timestamp = DateTime.Now.ToString("s")
            };

            File.WriteAllText(logPath, JsonUtility.ToJson(log, true));
        }
        catch (IOException ex)
        {
            Debug.LogError($"[IngestionLoop] ❌ Failed to write loop log: {ex.Message}");
        }

        // 🔁 Optional curiosity expansion
        List<IngestedTopic> curiousTopics = curiosityEngine?.ExtractCuriosityTopics(topic.topic, topic.domain);
        if (curiousTopics != null && curiousTopics.Count > 0)
        {
            try
            {
                string json = JsonUtility.ToJson(new TopicWrapper { topics = curiousTopics }, true);
                File.WriteAllText(ingestionPath, json);
                Debug.Log($"[CuriosityTrigger] ArTus generated {curiousTopics.Count} new symbolic topics.");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[IngestionLoop] ❌ Failed to write new topics: {ex.Message}");
            }
        }
    }
}
