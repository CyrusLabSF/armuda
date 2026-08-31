using UnityEngine;
using System.Collections.Generic;

public class ArTusCognitiveLoopGuard : MonoBehaviour
{
    [Header("Cooldown Settings")]
    public float topicCooldownSeconds = 180f;

    [Header("Reflection Depth Control")]
    public int maxReflectionDepth = 2;

    [Header("Duplicate Prevention")]
    public bool blockDuplicateTopics = true;

    private Dictionary<string, float> topicCooldowns = new Dictionary<string, float>();
    private Dictionary<string, int> reflectionDepth = new Dictionary<string, int>();

    // =========================================
    // MAIN CHECK
    // =========================================
    public bool CanProcess(string topic, bool isReflection)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string key = Normalize(topic);

        // -------------------------
        // COOLDOWN BLOCK
        // -------------------------
        if (topicCooldowns.TryGetValue(key, out float nextAllowed))
        {
            if (Time.time < nextAllowed)
                return false;
        }

        // -------------------------
        // DEPTH BLOCK
        // -------------------------
        if (isReflection)
        {
            int depth = reflectionDepth.ContainsKey(key) ? reflectionDepth[key] : 0;

            if (depth >= maxReflectionDepth)
                return false;
        }

        return true;
    }

    // =========================================
    // MARK PROCESSED
    // =========================================
    public void MarkProcessed(string topic, bool isReflection)
    {
        string key = Normalize(topic);

        topicCooldowns[key] = Time.time + topicCooldownSeconds;

        if (isReflection)
        {
            if (!reflectionDepth.ContainsKey(key))
                reflectionDepth[key] = 0;

            reflectionDepth[key]++;
        }
    }

    // =========================================
    // RESET (OPTIONAL)
    // =========================================
    public void DecayMemory()
    {
        List<string> keys = new List<string>(reflectionDepth.Keys);

        foreach (var key in keys)
        {
            reflectionDepth[key] = Mathf.Max(0, reflectionDepth[key] - 1);
        }
    }

    // =========================================
    // NORMALIZATION (CRITICAL)
    // =========================================
    private string Normalize(string topic)
    {
        topic = topic.ToLower().Trim();

        topic = topic.Replace("reflect on", "");
        topic = topic.Replace("reflection on", "");
        topic = topic.Replace("learn about", "");
        topic = topic.Replace("meta reflection", "");
        topic = topic.Replace("meta-reflection", "");

        return topic.Trim();
    }
}