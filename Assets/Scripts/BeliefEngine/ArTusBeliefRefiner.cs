using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArTusTypes;

public class ArTusBeliefRefiner : MonoBehaviour
{
    [Header("Paths & Files")]
    [SerializeField] private string beliefRootFolder = "D:/ArTusCloud-Deployment/UNIVERcity/Beliefs";
    [SerializeField] private string beliefFileName = "BeliefTrees.json";
    [SerializeField] private bool enableBackups = true;
    [SerializeField] private int maxBackupFiles = 5;

    [Header("Storage & Autosave")]
    [SerializeField] private bool enableAutosave = true;
    [SerializeField] private float autosaveSeconds = 8f;
    [SerializeField] private bool verboseLogs = false;

    [Header("Merge Settings")]
    [Range(0f, 1f)][SerializeField] private float mergeSimilarityThreshold = 0.6f;

    private string BeliefFilePath => Path.Combine(beliefRootFolder, beliefFileName);

    // cached store + index
    private BeliefNodeListWrapper _store = new BeliefNodeListWrapper();
    private Dictionary<string, BeliefNode> _index =
        new Dictionary<string, BeliefNode>(StringComparer.OrdinalIgnoreCase);

    private bool _loaded;
    private bool _dirty;
    private Coroutine _autosaveCo;

    private ArTusCoreState coreState;
    private ArTusSpeechResponder speech;

    void Awake()
    {
        coreState = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        Directory.CreateDirectory(beliefRootFolder);
    }

    void OnEnable()
    {
        EnsureLoaded();
        if (enableAutosave && _autosaveCo == null)
            _autosaveCo = StartCoroutine(AutosaveLoop());
    }

    void OnDisable()
    {
        if (_autosaveCo != null) { StopCoroutine(_autosaveCo); _autosaveCo = null; }
        if (_dirty) SaveNow();
    }

    public string GetBeliefJustification(string topic)
    {
        var belief = GetBelief(topic);
        if (belief == null)
            return "No justification available.";

        // ✅ Use description if it exists
        if (!string.IsNullOrEmpty(belief.description))
            return belief.description;

        // ✅ Otherwise build a summary using confidence & emotion
        return $"Belief in '{belief.belief}' is supported with confidence {belief.confidenceScore:F2} and emotion {belief.dominantEmotion}.";
    }

    // ---------------------- Load / Save ----------------------

    private void EnsureLoaded()
    {
        if (_loaded) return;

        _store = LoadBeliefsFromDisk();
        RebuildIndex();
        _loaded = true;

        if (verboseLogs) Debug.Log($"[BeliefRefiner] Loaded {_store.beliefs.Count} belief(s).");
    }

    private BeliefNodeListWrapper LoadBeliefsFromDisk()
    {
        try
        {
            if (!File.Exists(BeliefFilePath))
                return new BeliefNodeListWrapper();

            string json = File.ReadAllText(BeliefFilePath);
            return JsonUtility.FromJson<BeliefNodeListWrapper>(json) ?? new BeliefNodeListWrapper();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BeliefRefiner] Error loading beliefs: {ex.Message}");
            return new BeliefNodeListWrapper();
        }
    }

    private void SaveNow()
    {
        try
        {
            Directory.CreateDirectory(beliefRootFolder);
            if (enableBackups && File.Exists(BeliefFilePath)) CreateBackup();

            string prettyJson = JsonUtility.ToJson(_store, true);
            File.WriteAllText(BeliefFilePath, prettyJson);
            _dirty = false;

            if (verboseLogs) Debug.Log("[BeliefRefiner] Beliefs saved.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BeliefRefiner] Failed to save beliefs: {ex.Message}");
        }
    }

    private IEnumerator AutosaveLoop()
    {
        var wait = new WaitForSeconds(autosaveSeconds);
        while (true)
        {
            yield return wait;
            if (_dirty) SaveNow();
        }
    }

    private void CreateBackup()
    {
        try
        {
            var backups = Directory.GetFiles(beliefRootFolder, "BeliefTrees_*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            while (backups.Count >= maxBackupFiles)
            {
                File.Delete(backups.Last());
                backups.RemoveAt(backups.Count - 1);
            }

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupName = $"BeliefTrees_{ts}.json";
            File.Copy(BeliefFilePath, Path.Combine(beliefRootFolder, backupName));
            if (verboseLogs) Debug.Log($"[BeliefRefiner] Backup: {backupName}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BeliefRefiner] Backup failed: {ex.Message}");
        }
    }

    private void MarkDirty() => _dirty = true;

    private void RebuildIndex()
    {
        _index.Clear();
        foreach (var b in _store.beliefs)
        {
            if (b == null || string.IsNullOrWhiteSpace(b.topic)) continue;
            _index[NormalizeTopic(b.topic)] = b;
        }
    }

    private static string NormalizeTopic(string s)
    {
        string normalized = (s ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        normalized = normalized.Replace("\"", string.Empty).Replace("'", string.Empty).Trim();

        string[] prefixes =
        {
            "my belief in ",
            "belief in ",
            "i have reinforced my belief in ",
            "i have formed a new belief: ",
            "reinforced belief in "
        };

        foreach (string prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(prefix.Length).Trim();
                break;
            }
        }

        string[] suffixMarkers =
        {
            " is fading",
            " strengthened by",
            " from memory clarity",
            " with confidence ",
            " due to ",
            " based on ",
            " via ",
            "| evidence:"
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

        return normalized;
    }

    private static bool ShouldSpeakBeliefTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalized = topic.Trim().ToLowerInvariant();
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
            "generated 9 procedural geometry seed descriptors",
            "generated 9 procedural geometry seed descriptors.",
            "procedural shape seed",
            "deferred",
            "reflection",
            "in this cycle,",
            "i experienced ",
            "planned goal",
            "externalknowledge",
            "requesting external knowledge",
            "selected shape",
            " artus-local-bridge",
            "type: connected",
            "service: artus-local-bridge",
            "web:{",
            "activity score",
            "api stage completed",
            "rate limited:",
            "ingested topic:",
            "api failed:",
            "promoted belief:",
            "api stage started",
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
            "thinking real",
            "topic thinking",
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
            "request timeout",
            "spacex rockets",
            "causal loop diagrams is",
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
            "theory applications",
            "knowledge source",
            "source for artus",
            "bridge operating",
            "received through",
            "local development knowledge",
            "through the web",
            "via route web",
            "candidate surfaced",
            "2026-04-20t"
        };

        if (normalized.EndsWith(" form", StringComparison.Ordinal))
            return false;

        return !blockedFragments.Any(fragment => normalized.Contains(fragment));
    }

    // ---------------------- Public API ----------------------

    public void AddOrReinforceBelief(string beliefTopic, float confidence)
    {
        EnsureLoaded();

        string key = NormalizeTopic(beliefTopic);
        if (!ShouldSpeakBeliefTopic(key))
            return;

        if (!_index.TryGetValue(key, out var existing))
        {
            var node = new BeliefNode(key, confidence)
            {
                confidenceScore = Mathf.Clamp01(confidence),
                lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            _store.beliefs.Add(node);
            _index[key] = node;
            MarkDirty();

            if (verboseLogs) Debug.Log($"[BeliefRefiner] + Added: '{key}' @ {confidence:F2}");
            if (ShouldSpeakBeliefTopic(key))
                speech?.Speak($"I have formed a new belief: {key}.");
            return;
        }

        // only reinforce upward
        if (confidence > existing.confidenceScore)
        {
            existing.confidenceScore = Mathf.Clamp01(confidence);
            existing.reinforcementCount++;
            existing.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            MarkDirty();

            if (verboseLogs) Debug.Log($"[BeliefRefiner] ↗ Reinforced: '{key}' → {existing.confidenceScore:F2}");
            if (ShouldSpeakBeliefTopic(key))
                speech?.Speak($"I have reinforced my belief in {key}.");
        }
    }

    public void AddBelief(string beliefTopic, float confidence) =>
        AddOrReinforceBelief(beliefTopic, confidence);

    /// <summary>
    /// Update belief by delta. If autoCreate = true, creates with a seed confidence when missing.
    /// </summary>
    public void UpdateBeliefConfidence(string beliefName, float delta, bool autoCreate = false, float seedConfidence = 0.05f)
    {
        EnsureLoaded();

        string normalized = NormalizeTopic(beliefName);
        if (!ShouldSpeakBeliefTopic(normalized))
            return;

        var target = GetBelief(normalized);
        if (target == null)
        {
            if (!autoCreate)
            {
                if (verboseLogs) Debug.LogWarning($"[BeliefRefiner] Could not find belief: {normalized}");
                return;
            }

            // create on first touch
            target = EnsureBelief(normalized, seedConfidence);
            if (verboseLogs) Debug.Log($"[BeliefRefiner] Seeded missing belief '{normalized}' @ {seedConfidence:F2}");
        }

        target.AdjustConfidence(delta);
        target.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        MarkDirty();

        if (verboseLogs) Debug.Log($"[BeliefRefiner] Δ '{normalized}' {delta:+0.00;-0.00}, now {target.confidenceScore:F2}");
    }

    /// <summary> Create belief if missing and return it. </summary>
    public BeliefNode EnsureBelief(string beliefTopic, float seedConfidence = 0.05f)
    {
        EnsureLoaded();

        string key = NormalizeTopic(beliefTopic);
        if (!ShouldSpeakBeliefTopic(key))
            return null;

        if (_index.TryGetValue(key, out var b)) return b;

        var node = new BeliefNode(key, seedConfidence)
        {
            confidenceScore = Mathf.Clamp01(seedConfidence),
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        _store.beliefs.Add(node);
        _index[key] = node;
        MarkDirty();
        return node;
    }

    public List<BeliefNode> GetMostConfidentBeliefs(int count) =>
        _store.beliefs.Where(b => b != null && ShouldSpeakBeliefTopic(b.topic))
                      .OrderByDescending(b => b.confidenceScore)
                      .Take(Mathf.Max(0, count)).ToList();

    public List<BeliefNode> GetAllBeliefs() =>
        _store.beliefs;

    public int PruneInvalidBeliefs()
    {
        EnsureLoaded();

        int removed = _store.beliefs.RemoveAll(b => b == null || !ShouldSpeakBeliefTopic(b.topic));

        var deduped = new Dictionary<string, BeliefNode>(StringComparer.OrdinalIgnoreCase);
        foreach (BeliefNode belief in _store.beliefs.Where(b => b != null))
        {
            string key = NormalizeTopic(belief.topic);
            if (!ShouldSpeakBeliefTopic(key))
            {
                removed++;
                continue;
            }

            if (!deduped.TryGetValue(key, out BeliefNode existing))
            {
                belief.topic = key;
                deduped[key] = belief;
                continue;
            }

            existing.confidenceScore = Mathf.Max(existing.confidenceScore, belief.confidenceScore);
            existing.reinforcementCount = Mathf.Max(existing.reinforcementCount, belief.reinforcementCount);
            if (string.IsNullOrWhiteSpace(existing.lastUpdated) ||
                string.CompareOrdinal(belief.lastUpdated, existing.lastUpdated) > 0)
            {
                existing.lastUpdated = belief.lastUpdated;
            }

            foreach (string trail in belief.relatedTrails)
            {
                if (!existing.relatedTrails.Contains(trail))
                    existing.relatedTrails.Add(trail);
            }

            removed++;
        }

        _store.beliefs = deduped.Values.ToList();
        _index = new Dictionary<string, BeliefNode>(deduped, StringComparer.OrdinalIgnoreCase);

        if (removed <= 0)
            return 0;

        MarkDirty();
        return removed;
    }

    public List<BeliefNode> GetWeakBeliefs(int count) =>
        _store.beliefs.Where(b => b != null && ShouldSpeakBeliefTopic(b.topic))
                      .OrderBy(b => b.confidenceScore)
                      .Take(Mathf.Max(0, count)).ToList();

    public List<BeliefNode> GetCoreBeliefAnchors(int count) =>
        GetMostConfidentBeliefs(count);

    public void MergeSimilarBeliefs()
    {
        EnsureLoaded();

        var original = _store.beliefs.ToList();
        var merged = new List<BeliefNode>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool anyMerged = false;

        for (int i = 0; i < original.Count; i++)
        {
            var a = original[i];
            if (a == null || used.Contains(a.topic)) continue;

            var groupTrails = new List<string>(a.relatedTrails);
            float confSum = a.confidenceScore;
            int count = 1;

            for (int j = i + 1; j < original.Count; j++)
            {
                var b = original[j];
                if (b == null || used.Contains(b.topic)) continue;

                float sim = GetTrailSimilarity(a.relatedTrails, b.relatedTrails);
                if (sim >= mergeSimilarityThreshold)
                {
                    groupTrails = groupTrails.Union(b.relatedTrails).ToList();
                    confSum += b.confidenceScore;
                    count++;
                    used.Add(b.topic);
                    anyMerged = true;
                }
            }

            merged.Add(new BeliefNode(a.topic, confSum / count)
            {
                relatedTrails = groupTrails,
                confidenceScore = Mathf.Clamp01(confSum / count),
                reinforcementCount = groupTrails.Count,
                lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });

            used.Add(a.topic);
        }

        if (anyMerged)
        {
            _store.beliefs = merged;
            RebuildIndex();
            MarkDirty();

            speech?.Speak("I have merged similar beliefs based on trail similarity.");
            coreState?.LogMemory("🔗 Merged beliefs for consolidation", "BeliefMerge", 3, "neutral");
        }
        else if (verboseLogs)
        {
            Debug.Log("[BeliefRefiner] No belief pairs met the similarity threshold for merging.");
        }
    }

    public void ResolveContradiction(string beliefName)
    {
        EnsureLoaded();

        var target = GetBelief(beliefName);
        if (target == null)
        {
            if (verboseLogs) Debug.LogWarning($"[BeliefRefiner] No belief found to resolve: {beliefName}");
            return;
        }

        UpdateBeliefConfidence(beliefName, -0.1f);
        speech?.Speak($"I have resolved contradiction for {beliefName} by adjusting confidence.");
        coreState?.LogMemory($"🔧 Resolved contradiction for {beliefName}", "BeliefResolution", 2, "neutral");
    }

    private float GetTrailSimilarity(List<string> a, List<string> b)
    {
        if (a == null || b == null || a.Count == 0 || b.Count == 0) return 0f;
        int shared = a.Intersect(b).Count();
        int total = a.Union(b).Count();
        return total > 0 ? (float)shared / total : 0f;
    }

    public List<BeliefNode> QueryBeliefs(Func<BeliefNode, bool> predicate)
    {
        EnsureLoaded();
        return _store.beliefs.Where(predicate).ToList();
    }

    public BeliefNode GetBelief(string topic)
    {
        EnsureLoaded();
        string key = NormalizeTopic(topic);
        if (_index.TryGetValue(key, out var match)) return match;

        // Fallback: partial match
        return _store.beliefs.FirstOrDefault(b =>
            !string.IsNullOrEmpty(b?.topic) &&
            b.topic.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
