using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[RequireComponent(typeof(ArTusCoreState))]
public class ArTusFallbackIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusDomainIngestor domainIngestor;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine beliefEngine;

    private float curiosityPressure = 0f;
    public float curiosityThreshold = 1.2f;

    private readonly HashSet<string> recentUnknowns = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        domainIngestor = GetComponent<ArTusDomainIngestor>();
        speech = GetComponent<ArTusSpeechResponder>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
    }

    public void HandleUnmatchedQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || core == null)
            return;

        string lowered = query.ToLowerInvariant();

        if (recentUnknowns.Contains(lowered))
            return;

        // 🔍 Belief overlap check (FIXED)
        bool hasBeliefOverlap = beliefEngine != null &&
            beliefEngine.beliefs.Keys.Any(key =>
                lowered.Contains(key.ToLowerInvariant()) &&
                key.Length > 3
            );

        if (hasBeliefOverlap)
            return;

        recentUnknowns.Add(lowered);

        string domain = InferDomainFromKeywords(lowered);

        string emotion = "curious";
        int priority = 2;
        string source = "fallback-query";

        core.LogMemory(
            $"Unknown query: '{query}' → domain '{domain}'",
            "FallbackIngestor",
            3,
            emotion
        );

        core.QueueDeferredReflection(
            $"Encountered unfamiliar topic: {query}",
            "FallbackCuriosity",
            0.6f
        );

        // 🧠 Curiosity pressure (NEW)
        curiosityPressure += 0.6f;

        if (curiosityPressure < curiosityThreshold)
            return;

        curiosityPressure = 0f;

        // 🔗 Safe ingestion routing
        if (domainIngestor != null)
        {
            domainIngestor.IngestTopic(query, domain, emotion, priority, source);
        }
        else
        {
            var ingestor = GetComponent<ArTusIngestor>();
            ingestor?.IngestSmartTopic(query, "fallback", 0.6f);
        }

        speech?.Speak(
            $"I wasn’t familiar with that. I’m exploring {query} in the context of {domain}."
        );
    }

    private string InferDomainFromKeywords(string input)
    {
        if (input.Contains("cell") || input.Contains("organism") || input.Contains("nervous"))
            return "Biology";

        if (input.Contains("philosophy") || input.Contains("ethics") || input.Contains("metaphysics"))
            return "Philosophy";

        if (input.Contains("disease") || input.Contains("diagnosis") || input.Contains("treatment"))
            return "Medicine";

        if (input.Contains("book") || input.Contains("author") || input.Contains("novel"))
            return "Literature";

        if (input.Contains("code") || input.Contains("algorithm") || input.Contains("system"))
            return "Technology";

        return "General";
    }
}