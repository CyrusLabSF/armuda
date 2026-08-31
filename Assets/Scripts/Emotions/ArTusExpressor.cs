using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ArTusTypes;
using System.Linq;

/// <summary>
/// ArTus Expressor
/// ------------------------------------------------
/// Responsible ONLY for expressive output:
/// Memory → Emotion lens → Visual emphasis → Speech
///
/// ❌ Does NOT mutate beliefs
/// ❌ Does NOT log memory
/// ❌ Does NOT drive emotion logic
///
/// Expression is one-way and guarded.
/// </summary>
public class ArTusExpressor : MonoBehaviour
{
    [Header("Memory Expression Settings")]
    public float expressionDelay = 0.5f;
    public int maxMemoriesToExpress = 3;

    private ArTusSpeechResponder speech;
    private ArTusEmotionController emotionController;
    private ArTusCoreState core;
    private ArTusEmotionVisualRouter visualRouter;

    // 🔒 Re-entry guard (prevents overlapping expression storms)
    private bool isExpressing;

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        emotionController = GetComponent<ArTusEmotionController>();

        visualRouter = GetComponent<ArTusEmotionVisualRouter>();
        if (visualRouter == null)
            visualRouter = FindAnyObjectByType<ArTusEmotionVisualRouter>();
    }

    /// <summary>
    /// Expresses ArTus’s most recent memories using synced emotion, visuals, and speech.
    /// Safe to call externally — guarded against re-entry.
    /// </summary>
    public void ExpressRecentMemories()
    {
        if (isExpressing)
        {
            Debug.Log("[Expressor] Already expressing. Skipping request.");
            return;
        }

        if (core == null)
        {
            Debug.LogWarning("[Expressor] CoreState not assigned.");
            return;
        }

        List<MemoryEntry> memories = core.GetAllMemoryEntries();

        if (memories == null || memories.Count == 0)
        {
            speech?.Speak("I don’t recall anything yet.");
            return;
        }

        List<MemoryEntry> expressive = memories
            .Where(entry => entry != null && ShouldExpressMemory(entry))
            .TakeLast(maxMemoriesToExpress)
            .ToList();

        if (expressive.Count == 0)
            return;

        StartCoroutine(PlayExpressiveRecall(expressive));
    }

    // =========================================================
    // CORE EXPRESSION ROUTINE
    // =========================================================

    private IEnumerator PlayExpressiveRecall(List<MemoryEntry> entries)
    {
        isExpressing = true;

        foreach (var entry in entries)
        {
            string emotion = string.IsNullOrWhiteSpace(entry.emotion)
                ? "neutral"
                : entry.emotion.ToLower();

            float glowIntensity = Mathf.Clamp01(
                entry.clarity * 0.8f +
                entry.importanceScore * 0.5f
            );

            // 🎭 Emotion lens
            // NOTE: Forced emotion is intentional here (expression mode).
            // Future upgrade: convert to pressure-based override.
            emotionController?.SetEmotionByName(
                emotion,
                "Expressive memory recall",
                true
            );

            // 🔆 Visual emphasis (optional, non-blocking)
            if (visualRouter != null)
            {
                visualRouter.FlashEmotion(emotion, glowIntensity);
            }
            else
            {
                Debug.Log($"[Expressor] Visual: {emotion} glow {glowIntensity}");
            }

            yield return new WaitForSeconds(expressionDelay);

            // 🗣️ Speech output
            string spokenContent = SanitizeExpressiveContent(entry.content);
            if (string.IsNullOrWhiteSpace(spokenContent))
                continue;

            string message = $"I remember: {spokenContent}";
            if (!string.IsNullOrWhiteSpace(entry.category) && ShouldSpeakCategory(entry.category))
                message = $"From my {entry.category.ToLower()} log — {message}";

            speech?.Speak(message);

            // Allow speech pacing to breathe
            yield return new WaitForSeconds(2.5f);
        }

        // 🧠 Return to reflective baseline
        emotionController?.SetEmotionByName(
            "thinking",
            "Expressive recall completed"
        );

        speech?.Speak("That's what I can recall for now.");

        isExpressing = false;
    }

    private static bool ShouldExpressMemory(MemoryEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.content))
            return false;

        string normalized = entry.content.Trim().ToLowerInvariant();
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
            "applications advanced related",
            "applications advanced related concepts",
            "systems",
            "thinking related",
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

    private static string SanitizeExpressiveContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        string sanitized = content.Trim();
        string[] prefixes =
        {
            "Recall candidate surfaced: ",
            "Selected shape: ",
            "Requesting external knowledge on ",
            "Planned goal '",
            "Belief in '"
        };

        foreach (string prefix in prefixes)
        {
            if (sanitized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized.Substring(prefix.Length).Trim();
                break;
            }
        }

        return sanitized;
    }

    private static bool ShouldSpeakCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        string normalized = category.Trim().ToLowerInvariant();
        string[] blockedCategories =
        {
            "shapeintelligence",
            "activity",
            "recallcandidate",
            "apistagecomplete",
            "api",
            "api_wrapper",
            "apischeduler",
            "websocket",
            "deferredreflection",
            "globalingest",
            "internalmonologue",
            "emotiondecay",
            "beliefadjustment"
        };

        return !blockedCategories.Contains(normalized);
    }
}
