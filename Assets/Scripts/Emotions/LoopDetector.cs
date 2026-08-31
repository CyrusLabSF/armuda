using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LoopDetector : MonoBehaviour
{
    public ArTusCoreState core;
    public int checkIntervalHours = 12;
    public int loopThreshold = 3; // How many times the same topic appears in recent memory
    public int memoryWindowHours = 48;

    private float timer = 0f;
    private float checkFrequencySeconds = 300f; // Check every 5 minutes

    void Start()
    {
        if (core == null) core = GetComponent<ArTusCoreState>();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= checkFrequencySeconds)
        {
            timer = 0f;
            DetectAndRespondToLoops();
        }
    }

    public void DetectAndRespondToLoops()
    {
        // ✅ timestamp is DateTime now, no TryParse required
        var recentMemories = core.memoryLog
            .Where(m => (DateTime.UtcNow - m.timestamp).TotalHours <= memoryWindowHours)
            .ToList();

        Dictionary<string, int> topicFrequency = new();

        foreach (var m in recentMemories)
        {
            string topic = ExtractTopicKey(m.content);
            if (!topicFrequency.ContainsKey(topic))
                topicFrequency[topic] = 1;
            else
                topicFrequency[topic]++;
        }

        // ✅ moved inside same method
        foreach (var pair in topicFrequency)
        {
            if (pair.Value >= loopThreshold)
            {
                Debug.LogWarning($"[LoopDetector] ⚠️ Potential cognitive loop detected on topic: '{pair.Key}' (Count: {pair.Value})");

                string loopMessage =
                    $"⚠️ Loop detected: '{pair.Key}' has appeared {pair.Value} times in the last {memoryWindowHours} hours. [origin=internal, speaker=ArTus, category=loop]";

                core.LogMemory(loopMessage, "LoopWarning", 2, "alert");

                // Request knowledge reinforcement/expansion
                core.RequestKnowledge(pair.Key);
            }
        }
    }

    private string ExtractTopicKey(string raw)
    {
        return raw.ToLower().Split(' ').FirstOrDefault(w => w.Length > 3) ?? "general";
    }
}
