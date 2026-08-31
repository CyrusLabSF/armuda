using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hi-Class Speech Manager for ArTus
/// • Rate-limited
/// • Deduplicated
/// • Priority-aware
/// • Loop-safe (no belief/emotion feedback storms)
/// </summary>
public class ArTusSpeechResponder : MonoBehaviour
{
    [Header("Speech Settings")]
    [SerializeField] private float baseCooldown = 1.5f;
    [SerializeField] private int maxQueueSize = 12;
    [SerializeField] private float globalRateLimit = 0.4f; // seconds between accepts

    private bool isProcessing;
    private bool isSpeaking;
    private float lastAcceptTime;

    private Coroutine speechRoutine;

    // Deduplication cache
    private readonly HashSet<string> recentLines = new();
    private readonly Queue<string> recentOrder = new();
    private const int MAX_RECENT_CACHE = 16;

    private readonly List<SpeechEntry> speechQueue = new();

    public enum SpeechCategory
    {
        Debug = 0,
        Reflection = 1,
        Reinforcement = 1,
        System = 2,
        UserFacing = 3,
        Diplomacy = 3,
        Alert = 4,
        Critical = 5
    }

    private class SpeechEntry
    {
        public string line;
        public SpeechCategory category;
        public int priority;
    }

    // ===========================
    // PUBLIC API (CANONICAL)
    // ===========================

    public bool IsSpeaking => isSpeaking;

    /// <summary>
    /// Canonical speech entry point
    /// </summary>
    public void RequestSpeak(string line, SpeechCategory category)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        // ⛔ Hard rate limit
        if (Time.time - lastAcceptTime < globalRateLimit)
            return;

        // ⛔ Deduplicate
        if (recentLines.Contains(line))
            return;

        lastAcceptTime = Time.time;
        CacheRecent(line);

        var entry = new SpeechEntry
        {
            line = line,
            category = category,
            priority = (int)category
        };

        // 🚨 Interrupt rules
        if (category == SpeechCategory.Critical)
        {
            speechQueue.Clear();
            speechQueue.Insert(0, entry);
        }
        else if (category == SpeechCategory.Alert)
        {
            speechQueue.Insert(0, entry);
        }
        else
        {
            if (speechQueue.Count >= maxQueueSize)
                return;

            speechQueue.Add(entry);
        }

        // Priority sort
        speechQueue.Sort((a, b) => b.priority.CompareTo(a.priority));

        if (!isProcessing)
            speechRoutine = StartCoroutine(ProcessQueue());
    }

    // ==========================================================
    // LEGACY / BACKWARD-COMPAT API
    // ==========================================================

    public void Speak(string line)
        => RequestSpeak(line, SpeechCategory.UserFacing);

    public void TriggerVoice(string line)
        => RequestSpeak(line, SpeechCategory.UserFacing);

    public void RequestSpeak(string line)
        => RequestSpeak(line, SpeechCategory.UserFacing);

    // ===========================
    // QUEUE PROCESSOR
    // ===========================

    private IEnumerator ProcessQueue()
    {
        isProcessing = true;

        while (speechQueue.Count > 0)
        {
            var next = speechQueue[0];
            speechQueue.RemoveAt(0);

            yield return PerformSpeech(next);
            yield return new WaitForSeconds(GetCooldown(next.category));
        }

        isProcessing = false;
        speechRoutine = null;
    }

    // ===========================
    // SPEECH EXECUTION
    // ===========================

    private IEnumerator PerformSpeech(SpeechEntry entry)
    {
        isSpeaking = true;

        float pacing = 1f;

        switch (entry.category)
        {
            case SpeechCategory.Reflection:
                pacing = 0.85f;
                break;
            case SpeechCategory.Alert:
                pacing = 1.25f;
                break;
            case SpeechCategory.Critical:
                pacing = 0.9f;
                break;
            case SpeechCategory.Diplomacy:
                pacing = 1.05f;
                break;
        }

        Debug.Log($"[ArTusSpeech] ({entry.category}) {entry.line}");
        ApplyVisualSync(entry.category);

        // Simulated TTS duration
        float duration = Mathf.Clamp(entry.line.Length * 0.045f / pacing, 0.8f, 6f);
        yield return new WaitForSeconds(duration);

        isSpeaking = false;
    }

    private float GetCooldown(SpeechCategory category)
    {
        return category switch
        {
            SpeechCategory.Alert => baseCooldown * 0.6f,
            SpeechCategory.Critical => baseCooldown * 0.25f,
            _ => baseCooldown
        };
    }

    // ===========================
    // VISUAL SYNC (SAFE)
    // ===========================

    private void ApplyVisualSync(SpeechCategory category)
    {
        // Visual-only signals — NO belief or emotion mutation
        switch (category)
        {
            case SpeechCategory.Alert:
                Debug.Log("[Aura] Alert pulse");
                break;
            case SpeechCategory.Critical:
                Debug.Log("[Aura] Critical lock");
                break;
            case SpeechCategory.Reflection:
                Debug.Log("[Aura] Calm reflection");
                break;
        }
    }

    // ===========================
    // DEDUP CACHE
    // ===========================

    private void CacheRecent(string line)
    {
        recentLines.Add(line);
        recentOrder.Enqueue(line);

        if (recentOrder.Count > MAX_RECENT_CACHE)
        {
            var old = recentOrder.Dequeue();
            recentLines.Remove(old);
        }
    }
}
