using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ArTusIngestor : MonoBehaviour
{
    [Header("Ingestion Settings")]
    public bool removeAfterIngest = true;
    public bool enableSpeech = false;
    public float ingestCooldown = 2f;

    [Header("Intelligence Control")]
    public float ingestThreshold = 1.5f;

    private float ingestPressure = 0f;

    private string topicQueuePath;

    private bool isProcessing = false;
    private float lastIngestTime = 0f;

    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusSpeechResponder speech;
    private ArTusEmotionController emotion;

    private readonly HashSet<string> ingestedThisSession = new();

    private string lastIngestedTopic = "unknown-topic";

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        topicQueuePath = ArTusPathUtility.GetSafePath("Ingestion/topics.txt");
        ArTusPathUtility.EnsureParentDirectory(topicQueuePath);

        if (!File.Exists(topicQueuePath))
            File.WriteAllText(topicQueuePath, string.Empty);
    }

    void Start()
    {
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        speech = GetComponent<ArTusSpeechResponder>();
        emotion = GetComponent<ArTusEmotionController>();
    }

    public bool IsIngesting() => isProcessing;

    // --------------------------------------------------
    // ENTRYPOINTS
    // --------------------------------------------------

    public void StartIngestion()
    {
        if (isProcessing) return;

        core?.LogMemory("Ingestion pipeline started.", "Ingestion", 2, "curious");
        StartCoroutine(IngestFromQueue());
    }

    public void IngestSmartTopic(string topic, string category = "general", float priority = 0.5f)
    {
        if (string.IsNullOrWhiteSpace(topic)) return;
        if (IsSimilarTopic(topic)) return;

        // SAFE Emotion Handling (no GetCurrentEmotion dependency)
        string currentEmotion = "idle";

        if (emotion != null)
        {
            currentEmotion = emotion.CurrentEmotion.ToString().ToLower();
        }

        // Light gating instead of hard blocking
        if (currentEmotion == "overloaded" || currentEmotion == "fatigued")
            return;

        // Pressure-based ingestion
        ingestPressure += priority;

        if (ingestPressure < ingestThreshold)
            return;

        ingestPressure = 0f;

        ingestedThisSession.Add(topic);
        lastIngestedTopic = topic;

        float adjustedPriority = priority;

        // Boost if curious
        if (currentEmotion == "curious")
            adjustedPriority += 0.2f;

        adjustedPriority = Mathf.Clamp01(adjustedPriority);

        string summary = $"Ingested topic '{topic}' ({category})";

        beliefEngine?.RegisterBelief(
            topic,
            summary,
            adjustedPriority,
            "curious"
        );

        // Smart reflection
        if (adjustedPriority > 0.5f)
        {
            core?.QueueDeferredReflection(
                topic,
                $"Ingestor-{category}",
                adjustedPriority
            );
        }

        string reason =
            adjustedPriority > 0.8f ? "high-value" :
            adjustedPriority > 0.6f ? "relevant" : "exploratory";

        core?.LogMemory(
            $"📥 Ingested topic: {topic} [{category}] ({reason})",
            "Ingestion",
            Mathf.CeilToInt(adjustedPriority * 3f),
            "curious"
        );

        if (enableSpeech)
            speech?.TriggerVoice($"I’ve started learning about {topic}.");

        lastIngestTime = Time.time;
    }

    // --------------------------------------------------
    // SNOOPER / AWARENESS HOOKS
    // --------------------------------------------------

    public void IngestFromSnooper(string topic, float weight)
    {
        float priority = Mathf.Clamp01(weight + 0.3f);
        IngestSmartTopic(topic, "snooper", priority);
    }

    public void IngestFromAwareness(string topic, float intensity)
    {
        float priority = Mathf.Clamp01(intensity);
        IngestSmartTopic(topic, "awareness", priority);
    }

    // --------------------------------------------------
    // QUEUE INGESTION
    // --------------------------------------------------

    private IEnumerator IngestFromQueue()
    {
        if (!File.Exists(topicQueuePath))
            yield break;

        isProcessing = true;

        string[] topics = File.ReadAllLines(topicQueuePath);

        if (topics.Length == 0)
        {
            isProcessing = false;
            yield break;
        }

        foreach (string line in topics)
        {
            if (Time.time - lastIngestTime < ingestCooldown)
                yield return new WaitForSeconds(ingestCooldown);

            string topic = line.Trim();

            if (string.IsNullOrEmpty(topic))
                continue;

            IngestSmartTopic(topic, "queued", 0.6f);

            yield return new WaitForSeconds(0.5f);
        }

        if (removeAfterIngest)
            File.WriteAllText(topicQueuePath, string.Empty);

        isProcessing = false;
    }

    // --------------------------------------------------
    // FUZZY TOPIC MATCHING
    // --------------------------------------------------

    private bool IsSimilarTopic(string topic)
    {
        return ingestedThisSession.Any(t =>
            t.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
            topic.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    // --------------------------------------------------
    // LEGACY COMPATIBILITY
    // --------------------------------------------------

    public void IngestTopic(string topic)
    {
        IngestSmartTopic(topic, "legacy", 0.4f);
    }

    public void IngestSpecificTopic(string topic)
    {
        IngestSmartTopic(topic, "specific", 0.6f);
    }

    public void IngestFromVettedHub(string topic)
    {
        IngestSmartTopic(topic, "vetted", 0.8f);
    }

    public void IngestRandomTopic()
    {
        string[] pool =
        {
            "philosophy",
            "neuroscience",
            "systems thinking",
            "cognitive science",
            "cybersecurity"
        };

        string pick = pool[UnityEngine.Random.Range(0, pool.Length)];
        IngestSmartTopic(pick, "random", 0.5f);
    }

    public void IngestJsonTopic(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string topic = Path.GetFileNameWithoutExtension(path);
        lastIngestedTopic = topic;

        IngestSmartTopic(topic, "json", 0.6f);
    }

    public void IngestNextTopicFromQueue()
    {
        if (IsIngesting())
            return;

        StartIngestion();
    }

    public string GetLastTopicName()
    {
        return lastIngestedTopic;
    }
}
