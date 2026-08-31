using UnityEngine;
using System;
using System.Collections.Generic;

public class ArTusInhibitionEngine : MonoBehaviour
{
    [Header("🧠 Inhibition Settings")]
    [Tooltip("Time (in seconds) to wait before re-allowing the same topic.")]
    public float cooldownSeconds = 60f;

    private Dictionary<string, float> inhibitionTimestamps = new();
    private ArTusSpeechResponder speech;
    private ArTusCoreState core;

    void Start()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        core = GetComponent<ArTusCoreState>();
    }

    /// <summary>
    /// Checks whether the topic is recently reflected upon and inhibits if within cooldown.
    /// </summary>
    public bool IsTopicInhibited(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return false;

        string key = topic.Trim().ToLowerInvariant();

        if (inhibitionTimestamps.TryGetValue(key, out float lastTime))
        {
            if (Time.time - lastTime < cooldownSeconds)
            {
                speech?.Speak($"I’ve recently reflected on '{topic}'. I’ll give it more time.");
                core?.LogMemory($"⛔ Inhibited reflection on '{topic}' (still cooling down)", "Inhibition", 1, "neutral");
                return true;
            }
        }

        // ✅ Allow and update timestamp
        inhibitionTimestamps[key] = Time.time;
        return false;
    }

    /// <summary>
    /// Manually clear a topic from inhibition cache.
    /// </summary>
    public void ClearInhibition(string topic)
    {
        string key = topic.Trim().ToLowerInvariant();
        inhibitionTimestamps.Remove(key);
        Debug.Log($"[Inhibition] Cleared: {key}");
    }

    /// <summary>
    /// Flush the entire inhibition system.
    /// </summary>
    public void ResetAllInhibitions()
    {
        inhibitionTimestamps.Clear();
        Debug.Log("[Inhibition] All topic inhibitions have been cleared.");
    }
}
