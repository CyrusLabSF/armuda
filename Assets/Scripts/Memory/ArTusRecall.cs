using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ArTusTypes;

public class ArTusRecall : MonoBehaviour
{
    [Header("Recall Settings")]
    public int maxRecallCandidates = 3;
    public float minClarityThreshold = 0.3f;
    public bool allowExpressiveRecall = true;

    private ArTusCoreState core;
    private ArTusInhibitionEngine inhibitor;
    private ArTusEmotionController emotionController;
    private ArTusExpressor expressor;
    private ArTusCuriosityEngine curiosity;

    private readonly HashSet<string> recentRecall = new();
    private readonly Queue<string> recallOrder = new();
    public int maxRecentRecall = 20;

    private readonly HashSet<string> recentCuriosityPush = new();

    private float lastExpressionTime = 0f;
    public float expressionCooldown = 10f;

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        inhibitor = GetComponent<ArTusInhibitionEngine>();
        emotionController = GetComponent<ArTusEmotionController>();
        expressor = GetComponent<ArTusExpressor>();
        curiosity = GetComponent<ArTusCuriosityEngine>();
    }

    public void PerformRecall()
    {
        if (core == null) return;

        var memories = core.GetAllMemoryEntries()
                          .TakeLast(100)
                          .ToList();

        if (memories.Count == 0) return;

        string currentEmotion =
            emotionController?.CurrentEmotion.ToString().ToLower() ?? "neutral";

        var candidates = memories
            .Where(m =>
                !string.IsNullOrWhiteSpace(m.content) &&
                IsRecallCandidateContent(m.content) &&
                m.clarity >= minClarityThreshold &&
                (
                    string.IsNullOrWhiteSpace(m.emotion) ||
                    (m.emotion?.ToLower() ?? "") == currentEmotion ||
                    Random.value < 0.1f
                ) &&
                !recentRecall.Contains(m.content) &&
                (inhibitor == null || !inhibitor.IsTopicInhibited(m.category))
            )
            .OrderByDescending(m =>
                m.clarity * 0.6f + m.importance * 0.4f
            )
            .Take(maxRecallCandidates)
            .ToList();

        if (candidates.Count == 0)
            return;

        foreach (var entry in candidates)
        {
            recentRecall.Add(entry.content);
            recallOrder.Enqueue(entry.content);

            if (recallOrder.Count > maxRecentRecall)
            {
                var oldest = recallOrder.Dequeue();
                recentRecall.Remove(oldest);
            }
        }

        foreach (var entry in candidates)
        {
            core.LogMemory(
                $"Recall candidate surfaced: {entry.content}",
                "RecallCandidate",
                1,
                "reflective"
            );

            if (entry.clarity < 0.4f &&
                !recentCuriosityPush.Contains(entry.category))
            {
                curiosity?.AddTopic(entry.category, "recall-gap");
                recentCuriosityPush.Add(entry.category);
            }
        }

        if (allowExpressiveRecall && expressor != null)
        {
            if (Time.time - lastExpressionTime > expressionCooldown)
            {
                expressor.ExpressRecentMemories();
                lastExpressionTime = Time.time;
            }
        }
    }

    public void ChainReflectByEmotion(string emotion)
    {
        PerformRecall();
    }

    public void ReflectOnGrowthTopics()
    {
        PerformRecall();
    }

    private static bool IsRecallCandidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        string normalized = content.Trim().ToLowerInvariant();
        string[] blockedFragments =
        {
            "form",
            "priority focus refreshed",
            "web route",
            "development knowledge",
            "operating local",
            "reflective synthesis",
            "reflective synthesis updated",
            "procedural geometry seed",
            "generated procedural geometry",
            "generated 9 procedural geometry",
            "procedural shape seed",
            "deferred",
            "reflection",
            "selected shape:",
            "shape profile:",
            "systems thinking form",
            "activity score:",
            "knowledge event received:",
            "requesting external knowledge on",
            "api stage started",
            "api stage completed",
            "api scheduler triggered",
            "i experienced ",
            "api failed:",
            "emotion idle decayed to",
            "emotion idle",
            "was received",
            "topic was received",
            "topic topic",
            "topic topic topic",
            "topic topic topic topic",
            "topic topic experienced",
            "topic experienced applications",
            "topic topic cycle",
            "topic cycle",
            "cycle experienced",
            "cycle experienced events",
            "cycle experienced basics",
            "cycle real",
            "experienced events",
            "experienced events top",
            "experienced events top basics",
            "events top categories",
            "top categories",
            "categories concept",
            "categories concept basics",
            "categories concept discovery",
            "categories related",
            "categories",
            "exploratory",
            "concept discovery weight",
            "concept discovery",
            "concept",
            "discovery weight emotionally",
            "discovery weight",
            "discovery",
            "discovery weight advanced",
            "openuv api",
            "helioviewer api",
            "us congress",
            "experienced",
            "experienced related",
            "emotionally leaned toward",
            "leaned toward",
            "leaned toward thinking",
            "leaned toward basics",
            "leaned advanced",
            "leaned",
            "recall tracker",
            "coingecko nft",
            "usda topics",
            "ai nutritional",
            "stackoverflow q&a",
            "spotify lyrics",
            "urban dictionary",
            "tmdb random",
            "earthquakes api",
            "plant hardiness",
            "i want",
            "github repo",
            "web knowledge topic",
            "bridge knowledge update",
            "via route web",
            "request timeout",
            "spacex rockets",
            "causal loop diagrams is",
            "concepts domain autonomy",
            "examples domain autonomy",
            "applications domain autonomy",
            "domain autonomy real",
            "prioritizing belief in systems",
            "priority focus set:",
            "synthesis for topic",
            "concepts applications",
            "topic applications",
            "topic topic applications",
            "topic topic topic applications",
            "applications",
            "observer typed",
            "topic systems thinking",
            "ingested",
            "topic ingested",
            "topic ingested pubmed",
            "ingested wikipedia",
            "ingested pubmed",
            "ingested pubmed data",
            "deferred reflection queued",
            "ingested crossref works data",
            "ingested semantic scholar",
            "semantic scholar search data",
            "ingested wikipedia data",
            "ingested openlibrary data",
            "ingested pubmed data",
            "ingested openlibrary",
            "openlibrary data",
            "systems thinking related",
            "systems thinking real",
            "systems applications",
            "examples advanced",
            "advanced into 5 steps",
            "advanced concept discovery",
            "synthesis for topic advanced",
            "systems thinking advanced theory",
            "systems thinking advanced theory basics",
            "systems thinking basics advanced",
            "systems thinking basics advanced advanced theory",
            "world examples applications",
            "world examples applications basics",
            "real world examples applications",
            "real world examples applications basics",
            "examples applications basics",
            "thinking basics related",
            "applications advanced related",
            "applications advanced related concepts",
            "systems",
            "thinking related",
            "systems thinking advanced",
            "domain autonomy related",
            "basics domain autonomy",
            "theory domain autonomy",
            "domain autonomy",
            "observer trend",
            "sceneswitches=",
            "concept_discovery, weight=",
            "api scheduler triggered",
            "events.",
            "reflected on high-confidence belief:",
            "oxford dictionary api response",
            "api response",
            "weather api",
            "alphavantage",
            "health conditions",
            "openlibrary api",
            "google books api",
            "urban dictionary api",
            "yahoo finance insider trades",
            "missing rapidapi",
            "rapidapi key",
            "emotion alert",
            "stage 'foundations'",
            "crossref works:",
            "semantic scholar search:",
            "route web summary",
            "reflected on",
            "passive_observation",
            "📄 api",
            "curiosity, weight=",
            "advanced theory",
            "internally, i",
            "observer activity",
            "hourly observer",
            "inactivity loop",
            "preparing",
            "and emotion (thinking).",
            "externalknowledge",
            "generated 9 procedural geometry seed descriptors",
            "generated 9 procedural geometry seed descriptors.",
            "generated procedural geometry seed descriptors",
            "reflective synthesis updated.",
            "web summary",
            "map tiles",
            "ny times",
            "iss location",
            "binance 24hr",
            "belief 'yahoo'",
            "yahoo",
            "hourly passive",
            "belief 'openweather'",
            "openweather",
            "national weather",
            "semantic scholar",
            "emotion joy",
            "🌐 api",
            "🌐 autonomous",
            "high-value",
            "high-value is fading",
            "scheduled reflection on external",
            "thinking",
            "bridge synthesis",
            "local development",
            "belief weakness review triggered",
            "belief weakness review triggered.",
            "weak belief audit",
            "weak belief audit.",
            "systems thinking is fading",
            "systems thinking is fading.",
            "relevant",
            "relevant is fading",
            "belief reinforcement review",
            "belief reinforcement review.",
            "core anchor review",
            "core anchor review.",
            "core anchor review. is",
            "summary",
            "reflection: dominant emotion is",
            "ingestion pipeline started",
            "belief fading",
            "flagged for reinforcement",
            "belief reinforcement review",
            "core anchor review",
            "knowledge event received",
            "cycle progress summary updated",
            "relevant is fading",
            "route web",
            "local bridge",
            "evidence the topic",
            "my belief in ",
            "thinking real",
            "topic thinking",
            "internally, i still feel",
            "recall candidate surfaced:",
            "planned goal '",
            "planned goal ",
            "externalknowledge",
            "purpose:",
            "summary local",
            "promoted belief:",
            "theory applications",
            "local bridge synthesis",
            "knowledge source",
            "source for artus",
            "bridge operating",
            "received through",
            "through the web",
            "candidate surfaced",
            "2026-04-20t",
            "web:{",
            "artus-local-bridge"
        };

        return !blockedFragments.Any(fragment => normalized.Contains(fragment));
    }
}
