using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using ArTusTypes;

public class ArTusWebIngestor : MonoBehaviour
{
    [Header("Core References")]
    public ArTusCoreState core;
    public ArTusCuriosityEngine curiosity;
    public ArTusKnowledgeConfidence knowledgeConfidence;

    [Header("Interest / Desire Tuning")]
    [Range(0f, 1f)] public float baseInterest = 0.4f;
    [Range(0f, 1f)] public float noveltyBoost = 0.3f;
    [Range(0f, 1f)] public float confidenceGapWeight = 0.5f;
    [Range(0f, 1f)] public float repetitionPenalty = 0.25f;

    public int maxTrackedTopics = 50;

    private readonly Dictionary<string, int> recentTopicHits = new();
    private readonly HashSet<string> recentCuriosityPush = new();

    private void Awake()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>();

        if (curiosity == null)
            curiosity = GetComponent<ArTusCuriosityEngine>();

        if (knowledgeConfidence == null)
            knowledgeConfidence = GetComponent<ArTusKnowledgeConfidence>();

        InvokeRepeating(nameof(DecayInterestMemory), 60f, 60f);
    }

    public void IngestWebKnowledge(
        string topic,
        string domain,
        string summary,
        string source,
        string trailID
    )
    {
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(summary))
            return;

        topic = topic.Trim().ToLower();
        domain = string.IsNullOrWhiteSpace(domain) ? "general" : domain.Trim().ToLower();

        if (summary.Length > 500)
            summary = summary.Substring(0, 500);

        float interest = EvaluateInterest(topic);
        float desire = EvaluateDesire(topic);

        core?.LogMemory(
            $"🌐 Web knowledge received on '{topic}' from {source}.",
            "WebIngestion",
            Mathf.Clamp(1f + interest + desire, 1f, 3f),
            "thinking",
            trailID
        );

        if (curiosity != null && interest > 0.5f && !recentCuriosityPush.Contains(topic))
        {
            curiosity.AddTopic(topic, domain);
            recentCuriosityPush.Add(topic);

            if (recentCuriosityPush.Count > 30)
                recentCuriosityPush.Remove(recentCuriosityPush.First());
        }

        core?.RegisterGrowthSignal(domain, interest, desire);

        if (recentTopicHits.Count >= maxTrackedTopics)
        {
            var oldest = recentTopicHits.Keys.First();
            recentTopicHits.Remove(oldest);
        }

        if (!recentTopicHits.ContainsKey(topic))
            recentTopicHits[topic] = 0;

        recentTopicHits[topic]++;

        core?.QueueDeferredReflection(topic, domain, interest + desire);
    }

    private float EvaluateInterest(string topic)
    {
        float interest = baseInterest;

        if (!recentTopicHits.ContainsKey(topic))
            interest += noveltyBoost;

        if (recentTopicHits.ContainsKey(topic))
        {
            float penalty = recentTopicHits[topic] * repetitionPenalty * 0.1f;
            interest -= Mathf.Min(penalty, 0.5f);
        }

        return Mathf.Clamp01(interest);
    }

    private float EvaluateDesire(string topic)
    {
        if (knowledgeConfidence == null)
            return 0.3f;

        float confidence = knowledgeConfidence.GetConfidenceForTopic(topic);

        float desire = (1f - confidence) * confidenceGapWeight;

        return Mathf.Clamp01(desire);
    }

    public void DecayInterestMemory()
    {
        var keys = new List<string>(recentTopicHits.Keys);

        foreach (var key in keys)
        {
            recentTopicHits[key]--;

            if (recentTopicHits[key] <= 0)
                recentTopicHits.Remove(key);
        }
    }
}