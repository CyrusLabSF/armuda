using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class IngestSweepScheduler : MonoBehaviour
{
    public float sweepIntervalHours = 24f;
    private float lastSweepTime = -999f;

    private ArTusBeliefEngine beliefEngine;
    private ArTusOpenSourceWideIngestor ingestor;
    private ArTusSpeechResponder speech;

    void Start()
    {
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        ingestor = GetComponent<ArTusOpenSourceWideIngestor>();
        speech = GetComponent<ArTusSpeechResponder>();

        lastSweepTime = Time.realtimeSinceStartup / 3600f;
    }

    void Update()
    {
        float currentHours = Time.realtimeSinceStartup / 3600f;
        if (currentHours - lastSweepTime >= sweepIntervalHours)
        {
            lastSweepTime = currentHours;
            RunDailySweep();
        }
    }

    public void RunDailySweep()
    {
        List<string> topics = new();

        // 🧠 Weak/conflicted beliefs
        topics.AddRange(beliefEngine.beliefs
            .Where(kv => kv.Value.confidenceScore < 2f || kv.Value.conflictCount > 0)
            .Select(kv => kv.Key)
            .Take(3));

        // ➕ Topics from queue
        string queuePath = "D:/ArTusCloud-Deployment/UNIVERcity/QueuedTopics.json";
        if (File.Exists(queuePath))
        {
            try
            {
                string json = File.ReadAllText(queuePath);
                var queued = JsonUtility.FromJson<TopicQueueWrapper>(json);
                if (queued?.topics != null)
                    topics.AddRange(queued.topics.Take(3));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SweepScheduler] ❌ Failed to read queue: {ex.Message}");
            }
        }

        if (topics.Count == 0)
        {
            speech?.TriggerVoice("No new topics to sweep right now. All domains appear stable.");
            return;
        }

        speech?.TriggerVoice("Beginning my daily knowledge sweep.");

        foreach (string topic in topics.Distinct())
        {
            ingestor?.IngestFromAllSources(topic);
        }

        GetComponent<ArTusCoreState>()?.LogMemory(
            $"🌀 Daily ingest sweep completed for topics: {string.Join(", ", topics)}",
            "Sweep", 2, "inspired"
        );
    }

    [Serializable]
    public class TopicQueueWrapper
    {
        public List<string> topics = new();
    }
}
