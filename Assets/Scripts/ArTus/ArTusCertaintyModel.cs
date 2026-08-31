using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using ArTusTypes;

/// <summary>
/// Hi-Class Certainty Model
/// --------------------------------------------------
/// Pure estimator: computes certainty score for a topic.
/// No speech, no logging, no exports, no visuals, no mutation.
/// Safe for use by reflection, simulation, routing, and decision layers.
/// </summary>
public class ArTusCertaintyModel : MonoBehaviour
{
    private ArTusBeliefEngine beliefEngine;
    private ArTusKnowledgeConfidence knowledgeConfidence;
    private ArTusMemoryClarityEngine clarityEngine;
    private ArTusCoreState core;

    [Header("Weighting")]
    [Range(0f, 1f)] public float beliefWeight = 0.4f;
    [Range(0f, 1f)] public float knowledgeWeight = 0.4f;
    [Range(0f, 1f)] public float clarityWeight = 0.2f;

    [Header("Fallbacks")]
    [Range(0f, 1f)] public float defaultClarityWhenNoMemory = 0.3f;
    [Range(0f, 1f)] public float minimumKnowledgeScore = 0f;
    [Range(0f, 1f)] public float minimumBeliefScore = 0f;

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        knowledgeConfidence = GetComponent<ArTusKnowledgeConfidence>();
        clarityEngine = GetComponent<ArTusMemoryClarityEngine>();
    }

    // --------------------------------------------------
    // PRIMARY API
    // --------------------------------------------------
    public CertaintyEstimate Estimate(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return CertaintyEstimate.Empty;

        string normalizedTopic = NormalizeTopic(topic);

        float normalizedBeliefWeight;
        float normalizedKnowledgeWeight;
        float normalizedClarityWeight;
        NormalizeWeights(
            out normalizedBeliefWeight,
            out normalizedKnowledgeWeight,
            out normalizedClarityWeight
        );

        float beliefScore = GetBeliefScore(normalizedTopic);
        float knowledgeScore = GetKnowledgeScore(normalizedTopic);
        float clarityScore = GetClarityScore(normalizedTopic);

        float weighted =
            (beliefScore * normalizedBeliefWeight) +
            (knowledgeScore * normalizedKnowledgeWeight) +
            (clarityScore * normalizedClarityWeight);

        float certainty = Mathf.Clamp01(weighted);

        return new CertaintyEstimate
        {
            topic = normalizedTopic,
            value = certainty,
            descriptor = Describe(certainty),
            beliefComponent = beliefScore,
            knowledgeComponent = knowledgeScore,
            clarityComponent = clarityScore
        };
    }

    // --------------------------------------------------
    // COMPONENT SCORING
    // --------------------------------------------------
    private float GetBeliefScore(string normalizedTopic)
    {
        if (beliefEngine == null)
            return minimumBeliefScore;

        float raw = beliefEngine.GetBeliefConfidence(normalizedTopic);

        // Assumes belief confidence is on a 0–10 scale in your current system.
        float normalized = raw / 10f;
        return Mathf.Clamp01(normalized);
    }

    private float GetKnowledgeScore(string normalizedTopic)
    {
        if (knowledgeConfidence == null)
            return minimumKnowledgeScore;

        float score = knowledgeConfidence.GetConfidenceForTopic(normalizedTopic);
        return Mathf.Clamp01(score);
    }

    private float GetClarityScore(string normalizedTopic)
    {
        if (core == null)
            return defaultClarityWhenNoMemory;

        var memories = core.GetAllMemoryEntries();
        if (memories == null || memories.Count == 0)
            return defaultClarityWhenNoMemory;

        var relevant = memories
            .Where(m =>
                m != null &&
                !string.IsNullOrWhiteSpace(m.content) &&
                NormalizeTopic(m.content).Contains(normalizedTopic))
            .ToList();

        if (relevant.Count == 0)
            return defaultClarityWhenNoMemory;

        float avgClarity = relevant.Average(m => Mathf.Clamp01(m.clarity));

        // Optional future hook:
        // if (clarityEngine != null) { ... }
        // Left unused intentionally to keep this version pure and schema-safe.

        return Mathf.Clamp01(avgClarity);
    }

    // --------------------------------------------------
    // DESCRIPTION ONLY (PURE)
    // --------------------------------------------------
    public string Describe(float value)
    {
        value = Mathf.Clamp01(value);

        if (value >= 0.85f) return "very certain";
        if (value >= 0.60f) return "mostly confident";
        if (value >= 0.30f) return "uncertain";
        return "not confident";
    }

    // --------------------------------------------------
    // HELPERS
    // --------------------------------------------------
    private string NormalizeTopic(string input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : input.Trim().ToLowerInvariant();
    }

    private void NormalizeWeights(
        out float normalizedBeliefWeight,
        out float normalizedKnowledgeWeight,
        out float normalizedClarityWeight
    )
    {
        float total = beliefWeight + knowledgeWeight + clarityWeight;

        if (total <= 0.0001f)
        {
            normalizedBeliefWeight = 0.4f;
            normalizedKnowledgeWeight = 0.4f;
            normalizedClarityWeight = 0.2f;
            return;
        }

        normalizedBeliefWeight = beliefWeight / total;
        normalizedKnowledgeWeight = knowledgeWeight / total;
        normalizedClarityWeight = clarityWeight / total;
    }

    // --------------------------------------------------
    // DATA MODEL
    // --------------------------------------------------
    public struct CertaintyEstimate
    {
        public string topic;
        public float value;
        public string descriptor;

        // Useful for routing, dashboards, and debugging
        // without introducing side effects into the model.
        public float beliefComponent;
        public float knowledgeComponent;
        public float clarityComponent;

        public static CertaintyEstimate Empty => new CertaintyEstimate
        {
            topic = "",
            value = 0f,
            descriptor = "unknown",
            beliefComponent = 0f,
            knowledgeComponent = 0f,
            clarityComponent = 0f
        };
    }
}