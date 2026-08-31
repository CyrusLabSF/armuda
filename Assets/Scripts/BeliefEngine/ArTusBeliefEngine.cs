using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ArTusBeliefEngine : MonoBehaviour
{
    // ==========================================================
    // CONFIG
    // ==========================================================

    [SerializeField] private ArTusBeliefRefiner beliefRefiner;

    private const string PYTHON_EXE = "python";

    // ==========================================================
    // STATE (PUBLIC BY DESIGN — LEGACY DEPENDENCY)
    // ==========================================================
    public Dictionary<string, BeliefData> beliefs = new Dictionary<string, BeliefData>();

    private readonly ConcurrentQueue<Action> mainThreadActions = new();
    private readonly ConcurrentQueue<(string belief, float delta, string source)> reinforcementQueue = new();
    // Throttle Python so we never spawn storms
    private readonly ConcurrentQueue<BeliefData> pendingOntology = new();
    private readonly ConcurrentQueue<BeliefData> pendingBeliefLog = new();
    private readonly HashSet<string> queuedBeliefs = new HashSet<string>();

    private const float PYTHON_FLUSH_INTERVAL = 1.0f;  // run at most once per second
    private const int PYTHON_MAX_PER_FLUSH = 2;         // limit how many python calls per tick

    private bool initialized;
    private float invalidBeliefPurgeTimer;
    private const float INVALID_BELIEF_PURGE_INTERVAL = 10f;

    // ==========================================================
    // UNITY LIFECYCLE
    // ==========================================================
    public static ArTusBeliefEngine Instance { get; private set; }

    private void Awake()
    {
        // 🔒 Singleton enforcement (component-safe)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("🧠 Duplicate ArTusBeliefEngine detected — disabling duplicate instance.");
            enabled = false;
            return;
        }

        Instance = this;

        // 🔗 Auto-bind BeliefRefiner if missing
        if (!beliefRefiner)
        {
            beliefRefiner = GetComponent<ArTusBeliefRefiner>();
            if (!beliefRefiner)
                Debug.LogWarning("⚠️ ArTusBeliefEngine has no BeliefRefiner attached — beliefs will not persist.");
        }
    }

    private void Start()
    {
        if (initialized) return;
        initialized = true;
        PurgeInvalidBeliefs();
        Debug.Log("🧠 ArTusBeliefEngine initialized");
    }

    private float beliefDrainTimer = 0f;
    private const float BELIEF_DRAIN_INTERVAL = 0.25f; // seconds
    private const int MAX_DRAIN_PER_TICK = 2;

    private void Update()
    {
        // 🔁 Main-thread safe actions
        while (mainThreadActions.TryDequeue(out var a))
            a?.Invoke();

        // 🔁 Reinforcement adjustments
        while (reinforcementQueue.TryDequeue(out var job))
            AdjustBeliefConfidence(job.belief, job.delta, job.source);

        invalidBeliefPurgeTimer += Time.deltaTime;
        if (invalidBeliefPurgeTimer >= INVALID_BELIEF_PURGE_INTERVAL)
        {
            invalidBeliefPurgeTimer = 0f;
            PurgeInvalidBeliefs();
        }

        // ⏱ Throttle belief side-effects
        beliefDrainTimer += Time.deltaTime;
        if (beliefDrainTimer < BELIEF_DRAIN_INTERVAL)
            return;

        beliefDrainTimer = 0f;

        // 🧠 Drain ontology queue (structure first)
        int processed = 0;
        while (processed < MAX_DRAIN_PER_TICK &&
               pendingOntology.TryDequeue(out var belief))
        {
            ProcessOntologyUpdate(belief);
            processed++;
        }

        // 📜 Drain belief logging queue
        processed = 0;
        while (processed < MAX_DRAIN_PER_TICK &&
               pendingBeliefLog.TryDequeue(out var belief))
        {
            ProcessBeliefLog(belief);
            processed++;
        }
    }


    // ==========================================================
    // CORE REGISTER (CANONICAL)
    // ==========================================================
    public void RegisterBelief(
    string topic,
    float confidence,
    string origin,
    string emotion,
    string trail)
    {
        topic = NormalizeConceptTopic(topic);
        if (!IsConceptTopicCandidate(topic))
            return;

        // 🔒 Ensure single belief instance per topic
        if (!beliefs.TryGetValue(topic, out var belief))
        {
            belief = new BeliefData(topic);
            beliefs.Add(topic, belief);
        }

        // 🧠 Update belief metadata
        belief.origin = string.IsNullOrWhiteSpace(origin) ? belief.origin : origin;
        belief.supportingTrail = string.IsNullOrWhiteSpace(trail) ? belief.supportingTrail : trail;
        belief.dominantEmotion = string.IsNullOrWhiteSpace(emotion)
            ? belief.dominantEmotion
            : emotion.ToLowerInvariant();

        belief.confidenceScore = Mathf.Clamp01(confidence);
        belief.Touch();

        // 🔥 Deduplicated queueing (FIXED)
        if (!queuedBeliefs.Contains(belief.topic))
        {
            pendingOntology.Enqueue(belief);
            pendingBeliefLog.Enqueue(belief);
            queuedBeliefs.Add(belief.topic);
        }

        beliefRefiner?.AddOrReinforceBelief(belief.topic, belief.confidenceScore);
    }

    private void ProcessOntologyUpdate(BeliefData belief)
    {
        queuedBeliefs.Remove(belief.topic);
    }

    private void ProcessBeliefLog(BeliefData belief)
    {
        queuedBeliefs.Remove(belief.topic);
    }

    // ==========================================================
    // 🔒 STRING-FIRST OVERLOADS (CRITICAL FIX)
    // ==========================================================
    public void RegisterBelief(string topic, string source)
        => RegisterBelief(topic, 1f, source, "neutral", null);

    public void RegisterBelief(string topic, string source, float confidence)
        => RegisterBelief(topic, confidence, source, "neutral", null);

    public void RegisterBelief(string topic, string source, float confidence, string emotion)
        => RegisterBelief(topic, confidence, source, emotion, null);

    public void RegisterBelief(string topic, string source, float confidence, string emotion, string trail)
        => RegisterBelief(topic, confidence, source, emotion, trail);

    private void PurgeInvalidBeliefs()
    {
        if (beliefs.Count > 0)
        {
            var invalidKeys = beliefs.Keys
                .Where(key => !IsConceptTopicCandidate(key))
                .ToList();

            foreach (string key in invalidKeys)
                beliefs.Remove(key);
        }

        queuedBeliefs.RemoveWhere(key => !IsConceptTopicCandidate(key));
        beliefRefiner?.PruneInvalidBeliefs();
    }

    // ==========================================================
    // FLOAT-FIRST OVERLOADS (OLDER CALLERS)
    // ==========================================================
    public void RegisterBelief(string topic, float confidence, string origin)
        => RegisterBelief(topic, confidence, origin, "neutral", null);

    public void RegisterBelief(string topic, float confidence, string origin, string emotion)
        => RegisterBelief(topic, confidence, origin, emotion, null);

    // ==========================================================
    // FEED / LOGGING API
    // ==========================================================
    public void RegisterFeedBelief(string topic)
        => RegisterBelief(topic, 1f, "feed", "neutral", null);

    public void RegisterFeedBelief(string topic, float confidence)
        => RegisterBelief(topic, confidence, "feed", "neutral", null);

    public void RegisterFeedBelief(string topic, string source)
        => RegisterBelief(topic, 1f, source, "neutral", null);

    public void RegisterFeedBelief(string topic, string source, float confidence)
        => RegisterBelief(topic, confidence, source, "neutral", null);

    public void RegisterFeedBelief(string topic, float confidence, string source, string emotion, string trail)
        => RegisterBelief(topic, confidence, source, emotion, trail);

    public void LogTopicBelief(string topic)
        => RegisterBelief(topic, 1f, "log", "neutral", null);

    public void LogTopicBelief(string topic, float confidence)
        => RegisterBelief(topic, confidence, "log", "neutral", null);

    public void LogTopicBelief(string topic, string source)
        => RegisterBelief(topic, 1f, source, "neutral", null);

    public void LogTopicBelief(string topic, string source, float confidence)
        => RegisterBelief(topic, confidence, source, "neutral", null);

    // ==========================================================
    // TOP BELIEFS / SUMMARY (LEGACY API)
    // ==========================================================

    public List<BeliefData> GetTopBeliefs(int count = 10)
    {
        return beliefs.Values
            .Where(b => b != null && IsConceptTopicCandidate(b.belief))
            .OrderByDescending(b => b.confidenceScore)
            .Take(Mathf.Max(0, count))
            .ToList();
    }

    public string GetBeliefSummary(int count = 8)
    {
        var top = GetTopBeliefs(count);

        if (top.Count == 0)
            return string.Empty;

        return string.Join("\n", top.Select(b =>
            $"- {b.belief} (conf: {b.confidenceScore:0.00}, emo: {b.dominantEmotion})"
        ));
    }

    // ==========================================================
    // API WRAPPER COMPAT (FINAL STRING/FLOAT FIX)
    // ==========================================================

    public void RegisterBelief(
        string topic,
        string source,
        string emotion,
        float confidence)
    {
        RegisterBelief(topic, confidence, source, emotion, null);
    }

    public void RegisterBelief(
        string topic,
        string source,
        string emotion,
        float confidence,
        string trail)
    {
        RegisterBelief(topic, confidence, source, emotion, trail);
    }


    // ==========================================================
    // CONFIDENCE / REINFORCEMENT
    // ==========================================================
    public float GetBeliefConfidence(string topic)
    {
        topic = NormalizeConceptTopic(topic);
        return beliefs.TryGetValue(topic, out var b) ? b.confidenceScore : 0f;
    }

    public void AdjustBeliefConfidence(string topic, float delta, string source = "adjust")
    {
        topic = NormalizeConceptTopic(topic);
        if (!IsConceptTopicCandidate(topic))
            return;

        if (!beliefs.TryGetValue(topic, out var b))
        {
            RegisterBelief(topic, delta, source, "neutral", null);
            return;
        }

        b.AdjustConfidence(delta);
        if (!string.IsNullOrWhiteSpace(source))
            b.reinforcementSources.Add(source);
    }

    public void ReinforceBelief(string topic, float delta = 0.25f, string source = "reinforce")
        => AdjustBeliefConfidence(topic, delta, source);

    public void QueueBeliefForReinforcement(string topic, float delta = 0.25f, string source = "queued")
    {
        topic = NormalizeConceptTopic(topic);
        if (!IsConceptTopicCandidate(topic))
            return;

        reinforcementQueue.Enqueue((topic, delta, source));
    }

    // ==========================================================
    // DECAY / COUNTS
    // ==========================================================
    public int DecayBeliefs(float decayRate = 0.01f)
    {
        int count = 0;
        foreach (var b in beliefs.Values)
        {
            float before = b.confidenceScore;
            b.confidenceScore = Mathf.Clamp(b.confidenceScore - decayRate, -10f, 10f);
            if (!Mathf.Approximately(before, b.confidenceScore))
                count++;
        }
        return count;
    }

    public int GetBeliefCount() => beliefs.Count;

    // ==========================================================
    // CONTRADICTIONS (MINIMAL, COMPAT)
    // ==========================================================
    public bool HasContradiction()
        => beliefs.Values.Any(b => b.hasContradiction);

    public bool HasContradiction(string topic)
        => beliefs.TryGetValue(topic, out var b) && b.hasContradiction;

    public int GetConflictBeliefCount()
        => beliefs.Values.Count(b => b.hasContradiction);

    public void FlagContradictingBelief(string topic, float severity = 1f)
    {
        if (!beliefs.TryGetValue(topic, out var b)) return;
        b.hasContradiction = true;
        b.contradictionSeverity = Mathf.Max(b.contradictionSeverity, severity);
    }

    public List<BeliefData> FindContradictions(int max = 25)
    {
        // Minimal, stable contradiction scan:
        // return beliefs already flagged as contradictory, ordered by severity + confidence.
        return beliefs.Values
            .Where(b => b != null && b.hasContradiction)
            .OrderByDescending(b => b.contradictionSeverity)
            .ThenByDescending(b => b.confidenceScore)
            .Take(Mathf.Max(1, max))
            .ToList();
    }


    public void UpdateContradictionHeatmap(string topic, float severity = 1f, string reason = "update")
        => FlagContradictingBelief(topic, severity);

    public void CoolContradictionHeatmap(float amount = 0.05f)
    {
        foreach (var b in beliefs.Values)
        {
            b.contradictionSeverity = Mathf.Max(0f, b.contradictionSeverity - amount);
            if (b.contradictionSeverity <= 0.01f)
                b.hasContradiction = false;
        }
    }

    // ==========================================================
    // SNAPSHOTS (REFLECTION)
    // ==========================================================
    public Dictionary<string, float> CaptureBeliefSnapshot()
        => beliefs.ToDictionary(kv => kv.Key, kv => kv.Value.confidenceScore);

    public Dictionary<string, float> CompareBeliefSnapshotToCurrent()
        => CaptureBeliefSnapshot();

    public Dictionary<string, float> CompareBeliefSnapshotToCurrent(Dictionary<string, float> snapshot)
    {
        var delta = new Dictionary<string, float>();
        foreach (var kv in beliefs)
        {
            float before = snapshot.TryGetValue(kv.Key, out var v) ? v : 0f;
            delta[kv.Key] = kv.Value.confidenceScore - before;
        }
        return delta;
    }

    private void RunPython(string script, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = PYTHON_EXE,
                Arguments = $"\"{script}\" {args}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = Process.Start(psi);
            p.WaitForExit();
        }
        catch (Exception ex)
        {
            mainThreadActions.Enqueue(() =>
                Debug.LogError($"🐍 Python error: {ex.Message}")
            );
        }
    }

    private static string NormalizeConceptTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return string.Empty;

        string normalized = topic.Trim();
        normalized = normalized.Replace("\"", "").Replace("'", "").Trim();

        string[] prefixes =
        {
            "i have formed a new belief: ",
            "i have reinforced my belief in ",
            "my belief in ",
            "in this cycle, ",
            "📥 ingested topic: ",
            "belief in ",
            "selected shape:",
            "requesting external knowledge on ",
            "recall candidate surfaced:",
            "from my recallcandidate log — i remember: ",
            "from my recallcandidate log - i remember: ",
            "from my globalingest log — i remember: ",
            "from my globalingest log - i remember: ",
            "from my api_wrapper log — i remember: ",
            "from my api_wrapper log - i remember: ",
            "from my apistagecomplete log — i remember: ",
            "from my apistagecomplete log - i remember: ",
            "promoted belief:",
            "reinforced belief in ",
            "prioritizing belief in ",
            "priority focus set: ",
            "i have reinforced my belief in "
        };

        foreach (string prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(prefix.Length).Trim();
                break;
            }
        }

        while (normalized.StartsWith("topic ", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring("topic ".Length).Trim();

        string[] suffixMarkers =
        {
            " strengthened by",
            " via ",
            " based on ",
            " from memory clarity",
            " with confidence ",
            " due to ",
            " has grown",
            " has weakened",
            " is fading",
            "(purpose:",
            "| evidence:",
            " local bridge synthesis"
        };

        foreach (string marker in suffixMarkers)
        {
            int index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                normalized = normalized.Substring(0, index).Trim();
                break;
            }
        }

        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");

        if (normalized.EndsWith(" is", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - 3).Trim();

        if (normalized.StartsWith("systems thinking causal loop", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("causal loop diagrams", StringComparison.OrdinalIgnoreCase))
        {
            return "systems thinking causal loop diagrams";
        }

        if (normalized.StartsWith("systems thinking system dynamics", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("systems thinking system", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("thinking system dynamics", StringComparison.OrdinalIgnoreCase))
        {
            return "systems thinking system dynamics";
        }

        if (normalized.StartsWith("thinking leverage points", StringComparison.OrdinalIgnoreCase))
            return "systems thinking" + normalized.Substring("thinking".Length);

        if (normalized.StartsWith("leverage points", StringComparison.OrdinalIgnoreCase))
            return "systems thinking " + normalized;

        if (normalized.StartsWith("thinking feedback loops", StringComparison.OrdinalIgnoreCase))
            return "systems thinking" + normalized.Substring("thinking".Length);

        if (normalized.StartsWith("feedback loops", StringComparison.OrdinalIgnoreCase))
            return "systems thinking " + normalized;

        if (normalized.StartsWith("thinking emergence", StringComparison.OrdinalIgnoreCase))
            return "systems thinking" + normalized.Substring("thinking".Length);

        if (normalized.StartsWith("thinking causal loop", StringComparison.OrdinalIgnoreCase))
            return "systems thinking causal loop diagrams";

        return normalized.Trim();
    }

    private static bool IsConceptTopicCandidate(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalized = topic.Trim().ToLowerInvariant();

        if (float.TryParse(normalized, out _))
            return false;

        if (normalized.Length < 3)
            return false;

        if (normalized.EndsWith(" form", StringComparison.Ordinal) ||
            normalized.StartsWith("web:{", StringComparison.Ordinal) ||
            normalized.StartsWith("belief web:{", StringComparison.Ordinal) ||
            normalized.StartsWith("topic ", StringComparison.Ordinal))
        {
            return false;
        }

        string[] blockedExact =
        {
            "belief in",
            "selected shape:",
            "selected shape",
             "form",
             "priority focus refreshed.",
             "priority focus refreshed",
             "web route",
             "development knowledge",
              "operating local",
              "reflective synthesis",
              "reflective synthesis updated",
              "procedural geometry seed",
              "generated procedural geometry",
              "generated 9 procedural geometry",
              "generated 9 procedural geometry seed descriptors",
              "generated 9 procedural geometry seed descriptors.",
              "procedural shape seed",
             "reflection",
             "deferred",
             "requesting external",
            "requesting external knowledge",
            "recall candidate",
            "belief reinforcement",
            "planned goal",
            "externalknowledge",
            "externalknowledge",
            "goal created",
            "in this cycle, i",
            "rate limited: semantic scholar",
            "1.00",
            "api",
            "topic",
            "topic topic",
            "topic topic topic",
            "topic topic topic topic",
            "topic topic experienced",
            "topic experienced applications",
            "topic applications",
            "topic topic applications",
            "topic topic topic applications",
            "applications",
            "cycle",
            "cycle experienced",
            "cycle experienced events",
            "cycle experienced basics",
            "cycle real",
            "topic cycle real",
            "topic topic cycle",
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
            "plant hardiness",
            "i want",
            "github repo",
            "urban dictionary",
            "tmdb random",
            "earthquakes api",
            "globalingest",
            "shapeintelligence",
            "beliefadjustment",
            "internalmonologue",
            "torus",
            "crossref works",
            "wikipedia summary",
            "semantic scholar search",
            "openlibrary search",
            "pubmed search",
            "summary",
            "thinking",
            "high-value",
            "bridge synthesis",
            "local development",
            "scheduled reflection on external",
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
            "reflection: dominant emotion is",
            "ingestion pipeline started.",
            "belief fading",
            "knowledge event received",
            "route web",
            "local bridge",
            "evidence the topic",
            "api stage completed",
            "deferred",
            "general",
            "purpose:",
            "summary local",
            "promoted belief: data",
            "promoted belief: theory",
            "data for topic",
            "was received",
            "topic was received",
            "systems",
            "observer activity score: 0.00",
            "emotion idle decayed to",
            "emotion idle",
            "topic systems thinking",
            "prioritizing belief in systems",
            "synthesis for topic",
            "concepts applications",
            "concepts domain autonomy",
            "examples domain autonomy",
            "applications domain autonomy",
            "observer typed",
            "domain autonomy real",
            "ingested",
            "topic ingested",
            "ingested wikipedia",
            "ingested pubmed",
            "ingested pubmed data",
            "ingested wikipedia data for",
            "ingested openlibrary data for",
            "ingested pubmed data for",
            "ingested openlibrary",
            "ingested openlibrary data",
            "openlibrary data",
            "concepts domain autonomy",
            "web knowledge topic",
            "bridge knowledge update",
            "via route web",
            "systems thinking related",
            "systems thinking real",
            "systems applications",
            "examples advanced",
            "topic ingested pubmed",
            "advanced",
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
            "thinking related",
            "thinking related concepts",
            "thinking real",
            "topic thinking",
            "theory applications",
            "operating local development",
            "local development knowledge",
            "development knowledge source",
            "local development knowledge source",
            "knowledge source",
            "knowledge source for artus",
            "source for artus",
            "bridge operating",
            "received through",
            "through the web",
            "candidate surfaced ingested",
            "systems thinking advanced",
            "domain autonomy related",
            "basics domain autonomy",
            "theory domain autonomy",
            "observer",
            "observer trend",
            "i experienced 1 events.",
            "reflected on high-confidence belief:",
            "oxford dictionary api response",
            "openlibrary api response",
            "google books api response",
            "urban dictionary api response",
            "health conditions api response",
            "weather api api response",
            "alphavantage api response",
            "yahoo finance insider trades api response",
            "missing rapidapi key for",
            "request timeout",
            "spacex rockets",
            "causal loop diagrams is",
            "systems thinking causal loop",
            "emotion alert",
            "route web summary",
            "reflected on",
            "📄 api",
            "passive_observation activity",
            "advanced theory",
            "internally, i",
            "observer activity",
            "hourly observer",
            "inactivity loop",
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
            "🌐 autonomous"
        };

        if (blockedExact.Contains(normalized))
            return false;

        string[] blockedContains =
        {
            "memory clarity",
            "emotion decay",
            "high-value is fading",
            "scheduled reflection on external",
            "recall candidate surfaced",
            "api scheduler triggered",
            "api stage started",
            "added to learning queue",
            "i remember:",
            "internally, i still feel",
            "i have formed a new belief",
            "in this cycle,",
            "i experienced ",
            "my belief in ",
            "belief fading",
            "relevant",
            "relevant is fading",
            "flagged for reinforcement",
            "belief reinforcement review",
            "core anchor review",
            "belief weakness review triggered",
            "weak belief audit",
            "knowledge event received",
            "cycle progress summary updated",
            "reflection: dominant emotion is",
            "ingestion pipeline started",
            "planned goal",
            "rate limited:",
            "ingested topic:",
            "purpose:",
            "summary local",
            "local bridge synthesis",
              "via route web",
              "priority focus refreshed",
              "web route",
              "development knowledge",
              "operating local",
             "deferred",
             "| evidence:",
            "data for topic",
            "was received",
            "topic was received",
            "artus-local-bridge",
            "type: connected",
            "service: artus-local-bridge",
            "externalknowledge",
            "externalknowledge",
            "generated 9 procedural geometry",
            "procedural geometry seed descriptors",
            "reflective synthesis updated",
            "promoted belief",
            "deferred reflection queued",
            "observer activity score",
            "api stage completed",
            "crossref works api ingested",
            "ingested wikipedia data for",
            "ingested openlibrary data for",
            "ingested pubmed data for",
            "api failed:",
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
            "stage 'foundations'",
            "crossref works:",
            "semantic scholar search:",
            "route web summary",
            "reflected on",
            "passive_observation",
            "📄 api",
            "curiosity, weight=",
            "internally, i",
            "observer activity",
            "hourly observer",
            "inactivity loop",
            "preparing",
            "promoted belief:",
            "emotion idle decayed to",
            "emotion idle",
            "topic systems thinking",
            "prioritizing belief in systems",
            "priority focus set:",
            "synthesis for topic",
            "concepts applications",
            "concepts domain autonomy",
            "applications domain autonomy",
            "topic applications",
            "topic topic applications",
              "topic topic topic",
              "topic topic topic topic",
              "topic topic topic applications",
            "applications",
            "examples domain autonomy",
            "observer typed",
            "ingested",
            "topic ingested",
            "ingested wikipedia",
            "ingested pubmed",
            "ingested openlibrary",
            "openlibrary data",
            "concepts domain autonomy",
            "web knowledge topic",
            "bridge knowledge update",
            "web summary",
            "map tiles",
            "ny times",
            "iss location",
            "binance 24hr",
            "belief 'yahoo'",
            "yahoo",
            "systems thinking related",
            "systems thinking real",
            "systems applications",
            "examples advanced",
            "topic ingested pubmed",
            "advanced into 5 steps",
            "advanced concept discovery",
            "synthesis for topic advanced",
            "systems thinking advanced theory",
            "systems thinking basics advanced",
            "world examples applications",
            "world examples applications basics",
            "real world examples applications",
            "real world examples applications basics",
            "examples applications basics",
            "thinking basics related",
            "applications advanced related",
            "applications advanced related concepts",
            "thinking related",
            "thinking real",
            "topic thinking",
            "theory applications",
            "knowledge source for",
            "source for artus",
            "bridge operating",
            "received through",
            "through the web",
            "candidate surfaced",
            "2026-04-20t",
            "stage foundations",
            "chars",
            "score:",
            "domain autonomy",
            "autonomy related",
            "autonomy basics",
            "autonomy theory",
            "observer trend",
            "sceneswitches=",
            "avgactivity=",
            "clarity=",
            "concept_discovery, weight=",
            "api scheduler triggered",
            "selected shape: advanced was received form",
            "events.",
            "reflected on high-confidence belief:",
            "oxford dictionary api response",
            "preparing",
            "and emotion (thinking).",
            "externalknowledge"
        };

        if (blockedContains.Any(fragment => normalized.Contains(fragment)))
            return false;

        if (normalized.Contains("domain autonomy", StringComparison.Ordinal) &&
            (normalized.Contains(" real", StringComparison.Ordinal) ||
             normalized.Contains(" concepts", StringComparison.Ordinal) ||
             normalized.Contains(" examples", StringComparison.Ordinal) ||
             normalized.Contains(" basics", StringComparison.Ordinal) ||
             normalized.Contains(" related", StringComparison.Ordinal) ||
             normalized.Contains(" theory", StringComparison.Ordinal) ||
             normalized.Contains(" applications", StringComparison.Ordinal)))
        {
            return false;
        }

        string[] tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string[] genericTokens =
        {
            "advanced",
            "applications",
            "basics",
            "concepts",
            "examples",
            "real",
            "related",
            "theory",
            "world"
        };

        int genericCount = tokens.Count(token => genericTokens.Contains(token));
        if (tokens.Contains("autonomy") && tokens.Contains("domain") && genericCount >= 1)
            return false;

        if (tokens.Length >= 3 && tokens.Contains("systems") && tokens.Contains("thinking") && genericCount >= 1)
            return false;

        if (tokens.Length >= 4 && genericCount >= 2)
            return false;

        return tokens.Length < 3 || genericCount < tokens.Length - 1;
    }
}
