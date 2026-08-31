using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using ArTusTypes;

[Serializable]
public enum ArTusGoalStatus
{
    Queued,
    Planning,
    Running,
    Blocked,
    Completed,
    Failed
}

public class ArTusConceptDiscovery : MonoBehaviour
{
    [Header("Discovery Settings")]
    public int maxRecentMemories = 140;
    public int maxKnowledgeRecords = 80;
    public int maxDiscoveries = 120;
    public float minimumDiscoveryScore = 0.52f;
    public float refreshIntervalSeconds = 45f;
    public bool enableDebug = false;

    private readonly HashSet<string> stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "been", "being", "by", "can",
        "do", "for", "from", "how", "if", "in", "into", "is", "it", "its", "of",
        "on", "or", "that", "the", "their", "this", "to", "via", "was", "what",
        "when", "where", "which", "who", "why", "with", "within", "without",
        "your", "my", "our", "his", "her", "they", "them", "you", "we", "i"
    };

    private readonly string[] blockedFragments =
    {
        "activity score",
        "api stage started",
        "api stage completed",
        "api scheduler triggered",
        "candidate surfaced",
        "artus-local-bridge",
        "belief in",
        "curiosity focus",
        "deferred reflection",
        "emotion idle",
        "executor ingested",
        "ingested",
        "topic ingested",
        "topic ingested pubmed",
        "ingested pubmed",
        "ingested pubmed data",
        "ingested pubmed data real world examples",
        "ingested pubmed data real world examples related concepts",
        "ingested wikipedia",
        "ingested openlibrary",
        "openlibrary data",
        "externalknowledge",
        "goal created",
        "i have formed a new belief",
        "i remember",
        "internally, i still feel",
        "local bridge synthesis",
        "planned goal",
        "promoted belief",
        "was received",
        "advanced was received",
        "topic topic experienced",
        "topic experienced applications",
        "experienced related",
        "emotionally leaned toward",
        "leaned toward",
        "leaned toward thinking",
        "leaned toward basics",
        "leaned advanced",
        "leaned",
        "purpose:",
        "rate limited",
        "synthesis for topic",
        "recall candidate",
        "requesting external knowledge",
        "selected shape",
        "service:",
        "summary local",
        "shape profile",
        "shapeintelligence",
        "topic systems thinking",
        "systems thinking form",
        "systems thinking related",
        "systems thinking real",
        "data for topic",
        "systems applications",
        "examples advanced",
        "topic thinking",
        "thinking related",
        "thinking real",
        "theory applications",
        "through the web",
        "world examples applications",
        "world examples applications basics",
        "real world examples applications",
        "real world examples applications basics",
        "examples applications basics",
        "thinking basics related",
        "applications advanced related",
        "applications advanced related concepts",
        "knowledge source",
        "local development",
        "operating local",
        "reflective synthesis",
        "reflective synthesis updated",
        "procedural geometry seed",
        "generated procedural geometry",
        "generated 9 procedural geometry",
        "procedural shape seed",
        "highlights include",
        "toward thinking",
        "belief fading",
        "shape systems thinking",
        "fading thinking dropped",
        "received through",
        "bridge operating",
        "bridge synthesis",
        "via route web",
        "cycle experienced",
        "cycle experienced events",
        "cycle experienced basics",
        "cycle real",
        "topic cycle",
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
        "recall tracker",
        "coingecko nft",
        "usda topics",
        "ai nutritional",
        "stackoverflow q&a",
        "spotify lyrics",
        "preparing daily reflection",
        "belief systems thinking",
        "form",
        "type:",
        "web:{"
    };

    private string discoveryPath;
    private string knowledgeIndexPath;
    private float lastRefreshTime = -999f;

    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private DiscoveredConceptWrapper discoveryIndex = new();

    private sealed class DiscoveryAccumulator
    {
        public string concept;
        public int occurrences;
        public readonly HashSet<string> supportingTopics = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> evidence = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> domains = new(StringComparer.OrdinalIgnoreCase);
    }

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>() ?? FindAnyObjectByType<ArTusBeliefEngine>();
        discoveryPath = ArTusPathUtility.GetPersistent("UNIVERcity/Knowledge/ConceptDiscovery/discovered_concepts.json");
        knowledgeIndexPath = ArTusPathUtility.GetPersistent("UNIVERcity/Knowledge/External/knowledge_records.json");
        discoveryIndex = LoadDiscoveryIndex();
    }

    public List<DiscoveredConceptRecord> GetHighPriorityDiscoveredConcepts(int maxCount = 5)
    {
        RefreshDiscoveriesIfNeeded();

        return (discoveryIndex?.entries ?? new List<DiscoveredConceptRecord>())
            .Where(entry =>
                entry != null &&
                !string.IsNullOrWhiteSpace(entry.concept) &&
                IsDiscoveryCandidate(entry.concept) &&
                entry.noveltyScore >= minimumDiscoveryScore &&
                entry.supportCount >= 2 &&
                (entry.evidence?.Count ?? 0) >= 1 &&
                (entry.supportingTopics?.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0) >= 2 &&
                !IsRecentlyPromoted(entry))
            .OrderByDescending(GetDiscoveryPriorityScore)
            .ThenByDescending(entry => entry.noveltyScore)
            .ThenByDescending(entry => entry.evidence?.Count ?? 0)
            .ThenByDescending(entry => entry.supportCount)
            .ThenByDescending(entry => entry.updatedAt)
            .Take(Mathf.Max(1, maxCount))
            .ToList();
    }

    public List<DiscoveredConceptRecord> GetBootstrapDiscoveredConcepts(int maxCount = 8, string preferredRoot = "")
    {
        RefreshDiscoveriesIfNeeded();

        string normalizedPreferredRoot = NormalizeConcept(preferredRoot);

        return (discoveryIndex?.entries ?? new List<DiscoveredConceptRecord>())
            .Where(entry =>
                entry != null &&
                !string.IsNullOrWhiteSpace(entry.concept) &&
                IsDiscoveryCandidate(entry.concept) &&
                entry.noveltyScore >= minimumDiscoveryScore * 0.75f &&
                (
                    (entry.evidence?.Count ?? 0) >= 1 ||
                    entry.supportCount >= 1 ||
                    (entry.supportingTopics?.Count ?? 0) >= 1 ||
                    entry.promotedCount >= 1))
            .OrderByDescending(entry =>
                !string.IsNullOrWhiteSpace(normalizedPreferredRoot) &&
                string.Equals(
                    ExtractDiscoveryRootTopic(entry.concept),
                    normalizedPreferredRoot,
                    StringComparison.OrdinalIgnoreCase))
            .ThenBy(entry => entry.promotedCount)
            .ThenByDescending(GetPromotionAgeMinutes)
            .ThenByDescending(GetDiscoveryPriorityScore)
            .ThenByDescending(entry => entry.supportCount)
            .Take(Mathf.Max(1, maxCount))
            .ToList();
    }

    public void MarkConceptPromoted(string concept, string goalId)
    {
        if (string.IsNullOrWhiteSpace(concept))
            return;

        RefreshDiscoveriesIfNeeded();

        var entry = (discoveryIndex?.entries ?? new List<DiscoveredConceptRecord>())
            .FirstOrDefault(existing => string.Equals(existing.concept, concept.Trim(), StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            return;

        entry.promotedCount += 1;
        entry.lastGoalId = goalId;
        entry.lastPromotedAt = DateTime.UtcNow.ToString("o");
        entry.updatedAt = entry.lastPromotedAt;
        entry.status = "promoted";
        PersistDiscoveryIndex();
    }

    public void RecordConceptOutcome(string concept, string outcome)
    {
        if (string.IsNullOrWhiteSpace(concept))
            return;

        RefreshDiscoveriesIfNeeded();

        var entry = (discoveryIndex?.entries ?? new List<DiscoveredConceptRecord>())
            .FirstOrDefault(existing => string.Equals(existing.concept, concept.Trim(), StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            return;

        entry.status = string.IsNullOrWhiteSpace(outcome) ? "candidate" : outcome.Trim().ToLowerInvariant();
        entry.updatedAt = DateTime.UtcNow.ToString("o");
        PersistDiscoveryIndex();
    }

    public void RefreshDiscoveriesIfNeeded(bool force = false)
    {
        if (!force && Time.time - lastRefreshTime < refreshIntervalSeconds)
            return;

        discoveryIndex = LoadDiscoveryIndex();
        BuildDiscoveries();
        lastRefreshTime = Time.time;
    }

    private void BuildDiscoveries()
    {
        var accumulators = new Dictionary<string, DiscoveryAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var memory in GetRecentMemories())
        {
            if (memory == null || string.IsNullOrWhiteSpace(memory.content))
                continue;

            if (ShouldSkipMemoryForDiscovery(memory))
                continue;

            string seedTopic = NormalizeConcept(memory.category == "KnowledgeRequest"
                ? core?.lastIngestedTopic
                : ExtractSeedTopic(memory.content, core?.lastIngestedTopic));

            AddCandidatesFromText(accumulators, memory.content, seedTopic, "general");
        }

        foreach (var record in GetRecentKnowledgeRecords())
        {
            if (record == null)
                continue;

            string seedTopic = NormalizeConcept(record.topic);
            string domain = string.IsNullOrWhiteSpace(record.domain) ? "general" : record.domain.Trim().ToLowerInvariant();

            AddCandidatesFromText(accumulators, record.summary, seedTopic, domain);

            foreach (string evidence in record.evidence ?? new List<string>())
                AddCandidatesFromText(accumulators, evidence, seedTopic, domain);
        }

        UpsertDiscoveries(accumulators);
    }

    private IEnumerable<MemoryEntry> GetRecentMemories()
    {
        return (core?.memoryLog ?? new List<MemoryEntry>())
            .Where(memory => memory != null && !string.IsNullOrWhiteSpace(memory.content))
            .TakeLast(Mathf.Max(10, maxRecentMemories));
    }

    private IEnumerable<KnowledgeRecord> GetRecentKnowledgeRecords()
    {
        if (!File.Exists(knowledgeIndexPath))
            return Enumerable.Empty<KnowledgeRecord>();

        try
        {
            string json = File.ReadAllText(knowledgeIndexPath);
            var wrapper = JsonUtility.FromJson<KnowledgeRecordWrapper>(json) ?? new KnowledgeRecordWrapper();
            return (wrapper.entries ?? new List<KnowledgeRecord>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.topic))
                .TakeLast(Mathf.Max(10, maxKnowledgeRecords))
                .ToList();
        }
        catch
        {
            return Enumerable.Empty<KnowledgeRecord>();
        }
    }

    private void AddCandidatesFromText(
        IDictionary<string, DiscoveryAccumulator> accumulators,
        string text,
        string seedTopic,
        string domain)
    {
        if (IsBridgeSummaryText(text))
            return;

        foreach (string candidate in ExtractCandidatePhrases(text))
        {
            if (!accumulators.TryGetValue(candidate, out DiscoveryAccumulator accumulator))
            {
                accumulator = new DiscoveryAccumulator { concept = candidate };
                accumulators[candidate] = accumulator;
            }

            accumulator.occurrences += 1;

            if (!string.IsNullOrWhiteSpace(seedTopic))
                accumulator.supportingTopics.Add(seedTopic);

            if (!string.IsNullOrWhiteSpace(text))
                accumulator.evidence.Add(CompactEvidence(text));

            string normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "general" : domain.Trim().ToLowerInvariant();
            accumulator.domains[normalizedDomain] = accumulator.domains.TryGetValue(normalizedDomain, out int count)
                ? count + 1
                : 1;
        }
    }

    private IEnumerable<string> ExtractCandidatePhrases(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        string normalized = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s\-]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            yield break;

        string[] tokens = normalized
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 3 && !token.All(char.IsDigit))
            .ToArray();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int size = 2; size <= 4; size++)
        {
            for (int i = 0; i <= tokens.Length - size; i++)
            {
                string[] slice = tokens.Skip(i).Take(size).ToArray();
                if (slice.Length != size)
                    continue;

                if (stopWords.Contains(slice[0]) || stopWords.Contains(slice[^1]))
                    continue;

                string phrase = string.Join(" ", slice).Trim();
                phrase = NormalizeConcept(phrase);

                if (!IsDiscoveryCandidate(phrase) || !seen.Add(phrase))
                    continue;

                yield return phrase;
            }
        }
    }

    private void UpsertDiscoveries(IDictionary<string, DiscoveryAccumulator> accumulators)
    {
        if (discoveryIndex == null)
            discoveryIndex = new DiscoveredConceptWrapper();

        foreach (var pair in accumulators.Values)
        {
            if (pair.occurrences < 2 && pair.supportingTopics.Count < 2)
                continue;

            if (pair.supportingTopics.Count + pair.evidence.Count < 3)
                continue;

            float beliefConfidence = beliefEngine != null
                ? Mathf.Clamp01(beliefEngine.GetBeliefConfidence(pair.concept) / 1.2f)
                : 0f;

            float supportScore = Mathf.Clamp01(pair.supportingTopics.Count / 4f);
            float recurrenceScore = Mathf.Clamp01(pair.occurrences / 6f);
            float evidenceScore = Mathf.Clamp01(pair.evidence.Count / 4f);
            float domainDiversityScore = Mathf.Clamp01(pair.domains.Count / 3f);
            float qualityScore = Mathf.Clamp01(
                supportScore * 0.35f +
                evidenceScore * 0.25f +
                recurrenceScore * 0.20f +
                domainDiversityScore * 0.20f);
            float novelty = Mathf.Clamp01(
                (1f - beliefConfidence) * 0.35f +
                qualityScore * 0.65f);

            if (novelty < minimumDiscoveryScore)
                continue;

            string dominantDomain = pair.domains.Count == 0
                ? "general"
                : pair.domains.OrderByDescending(entry => entry.Value).First().Key;

            var existing = discoveryIndex.entries.FirstOrDefault(entry =>
                string.Equals(entry.concept, pair.concept, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new DiscoveredConceptRecord
                {
                    concept = pair.concept,
                    domain = dominantDomain,
                    seedTopic = NormalizeDiscoverySupportTopic(pair.supportingTopics.FirstOrDefault(), pair.concept)
                };

                discoveryIndex.entries.Add(existing);
            }

            existing.domain = dominantDomain;
            existing.seedTopic = NormalizeDiscoverySupportTopic(
                pair.supportingTopics.FirstOrDefault() ?? existing.seedTopic ?? pair.concept,
                pair.concept);
            existing.supportingTopics = pair.supportingTopics
                .Select(topic => NormalizeDiscoverySupportTopic(topic, pair.concept))
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
            existing.evidence = pair.evidence
                .Select(evidence => NormalizeDiscoveryEvidenceText(evidence, pair.concept))
                .Where(evidence => !string.IsNullOrWhiteSpace(evidence))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
            existing.supportCount = pair.supportingTopics.Count;
            existing.noveltyScore = novelty;
            existing.updatedAt = DateTime.UtcNow.ToString("o");

            if (string.IsNullOrWhiteSpace(existing.status) || existing.status == "promoted")
                existing.status = "candidate";
        }

        discoveryIndex.entries = discoveryIndex.entries
            .Where(entry => entry != null && IsDiscoveryCandidate(entry.concept))
            .OrderByDescending(GetDiscoveryPriorityScore)
            .ThenByDescending(entry => entry.noveltyScore)
            .ThenByDescending(entry => entry.evidence?.Count ?? 0)
            .ThenByDescending(entry => entry.supportCount)
            .Take(maxDiscoveries)
            .ToList();

        PersistDiscoveryIndex();

        if (enableDebug)
            Debug.Log($"[ConceptDiscovery] Refreshed {discoveryIndex.entries.Count} discovered concepts.");
    }

    private static float GetDiscoveryPriorityScore(DiscoveredConceptRecord entry)
    {
        if (entry == null)
            return 0f;

        float supportScore = Mathf.Clamp01(entry.supportCount / 4f);
        float evidenceScore = Mathf.Clamp01((entry.evidence?.Count ?? 0) / 4f);
        float topicDiversityScore = Mathf.Clamp01((entry.supportingTopics?.Count ?? 0) / 5f);
        float domainScore = string.Equals(entry.domain, "general", StringComparison.OrdinalIgnoreCase) ? 0.25f : 1f;

        return Mathf.Clamp01(
            entry.noveltyScore * 0.45f +
            supportScore * 0.20f +
            evidenceScore * 0.20f +
            topicDiversityScore * 0.10f +
            domainScore * 0.05f);
    }

    private static double GetPromotionAgeMinutes(DiscoveredConceptRecord entry)
    {
        if (entry == null || !DateTime.TryParse(entry.lastPromotedAt, out DateTime promotedAt))
            return double.PositiveInfinity;

        return (DateTime.UtcNow - promotedAt).TotalMinutes;
    }

    private bool IsRecentlyPromoted(DiscoveredConceptRecord entry)
    {
        if (entry == null)
            return true;

        if (!DateTime.TryParse(entry.lastPromotedAt, out DateTime promotedAt))
            return false;

        return (DateTime.UtcNow - promotedAt).TotalMinutes < 30d;
    }

    private static string ExtractDiscoveryRootTopic(string topic)
    {
        string normalized = NormalizeConcept(topic);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        string[] siblingSuffixes =
        {
            "real world examples",
            "advanced concepts",
            "applications",
            "basics",
            "theory",
            "feedback loops",
            "leverage points",
            "system dynamics",
            "causal loop diagrams",
            "emergence"
        };

        foreach (string suffix in siblingSuffixes)
        {
            string marker = " " + suffix;
            if (normalized.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(0, normalized.Length - marker.Length).Trim();
        }

        return normalized;
    }

    private static string ExtractSeedTopic(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback ?? string.Empty;

        Match topicMatch = Regex.Match(text, @"\bfor topic ([a-z0-9\s\-]+)\b", RegexOptions.IgnoreCase);
        if (topicMatch.Success)
            return topicMatch.Groups[1].Value.Trim();

        Match aboutMatch = Regex.Match(text, @"\babout ([a-z0-9\s\-]+)\b", RegexOptions.IgnoreCase);
        if (aboutMatch.Success)
            return aboutMatch.Groups[1].Value.Trim();

        return fallback ?? string.Empty;
    }

    private static string CompactEvidence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string compact = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length > 180 ? compact.Substring(0, 180) : compact;
    }

    private bool IsDiscoveryCandidate(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return false;

        string normalized = phrase.Trim().ToLowerInvariant();

        if (normalized.Length < 6 || normalized.Length > 72)
            return false;

        if (normalized.Count(c => c == ' ') < 1)
            return false;

        string[] tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= 2)
            return false;

        if (normalized.Contains("2026-04-20t", StringComparison.Ordinal))
            return false;

        if (Regex.IsMatch(normalized, @"\b20\d{2}\-\d{2}\-\d{2}t\d{2}\b"))
            return false;

        if (float.TryParse(normalized, out _))
            return false;

        if (blockedFragments.Any(fragment => normalized.Contains(fragment)))
            return false;

        if (CountMetadataTokens(normalized) >= 2)
            return false;

        if (IsExpansionFragmentDominated(tokens))
            return false;

        if (stopWords.Contains(normalized))
            return false;

        if (normalized.StartsWith("belief ", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("systems thinking", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.StartsWith("this thread ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("connect ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("useful for ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("thread useful ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("my belief in ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("fading ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("may need ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("need revisit ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.StartsWith("preparing ", StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains("reflection", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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

        string[] blockedExact =
        {
            "real world examples",
            "advanced concepts",
            "related concepts",
            "external knowledge",
            "learning queue",
            "domain autonomy",
            "domain autonomy applications",
            "domain autonomy real",
            "concepts domain autonomy",
            "concepts applications",
            "applications domain autonomy",
            "route web summary",
            "operating local",
            "operating local development",
            "local development knowledge",
            "local development knowledge source",
            "development knowledge",
            "topic systems thinking",
            "topic applications",
            "topic topic applications",
            "topic topic topic",
            "topic topic topic topic",
            "topic topic cycle",
            "topic topic topic applications",
            "applications",
            "cycle experienced",
            "cycle experienced events",
            "cycle experienced basics",
            "cycle real",
            "topic cycle real",
            "experienced events",
            "experienced events top",
            "experienced events top basics",
            "events top categories",
            "top categories",
            "recall tracker",
            "coingecko nft",
            "usda topics",
            "systems thinking related",
            "systems thinking real",
            "systems applications",
            "examples advanced",
            "examples domain autonomy",
            "applications domain autonomy",
            "topic ingested pubmed",
            "ingested",
            "topic ingested",
            "ingested pubmed",
            "ingested pubmed data",
            "advanced",
            "advanced into 5 steps",
            "advanced concept discovery",
            "synthesis for topic advanced",
            "systems thinking advanced theory",
            "systems thinking advanced theory basics",
            "systems thinking basics advanced",
            "systems thinking basics advanced advanced theory",
            "advanced was received",
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
            "synthesis for topic",
            "bridge synthesis for topic",
            "bridge synthesis",
            "bridge operating",
            "candidate surfaced ingested",
            "prioritizing belief in systems",
            "ingested wikipedia",
            "ingested openlibrary",
            "ingested openlibrary data",
            "openlibrary data",
            "theory applications",
            "knowledge source",
            "knowledge source for artus",
            "source for artus",
            "received through",
            "through the web",
            "web route",
            "development knowledge source",
            "summary local",
            "purpose:",
            "systems thinking advanced",
            "domain autonomy related",
            "basics domain autonomy",
            "theory domain autonomy",
            "via route web",
            "form"
        };

        return !blockedExact.Contains(normalized);
    }

    private static string NormalizeConcept(string concept)
    {
        if (string.IsNullOrWhiteSpace(concept))
            return string.Empty;

        string normalized = concept.Trim().ToLowerInvariant();
        normalized = normalized.Replace("\"", string.Empty).Replace("'", string.Empty);
        normalized = normalized.Replace("concept_discovery", "concept discovery");
        normalized = normalized.Replace(" in domain autonomy", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        normalized = normalized.Trim(' ', '.', ',', ':', ';');

        if (normalized.StartsWith("systems thinking causal loop", StringComparison.OrdinalIgnoreCase))
            return "systems thinking causal loop diagrams";

        if (normalized.StartsWith("systems thinking causal ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("systems thinking causal", StringComparison.OrdinalIgnoreCase))
            return "systems thinking causal loop diagrams";

        if (normalized.StartsWith("systems thinking system dynamics", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("systems thinking system", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("thinking system dynamics", StringComparison.OrdinalIgnoreCase))
        {
            return "systems thinking system dynamics";
        }

        if (normalized.StartsWith("systems thinking feedback ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("systems thinking feedback", StringComparison.OrdinalIgnoreCase))
            return "systems thinking feedback loops";

        if (normalized.StartsWith("systems thinking leverage", StringComparison.OrdinalIgnoreCase))
            return "systems thinking leverage points";

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

    private static string NormalizeDiscoverySupportTopic(string topic, string concept)
    {
        string normalizedTopic = NormalizeConcept(topic);
        string normalizedConcept = NormalizeConcept(concept);
        if (string.IsNullOrWhiteSpace(normalizedTopic))
            return normalizedConcept;

        if (string.IsNullOrWhiteSpace(normalizedConcept))
            return normalizedTopic;

        if (string.Equals(normalizedTopic, normalizedConcept, StringComparison.OrdinalIgnoreCase))
            return normalizedConcept;

        if (normalizedTopic.StartsWith(normalizedConcept + " ", StringComparison.OrdinalIgnoreCase))
            return normalizedConcept;

        string topicRoot = ExtractDiscoveryRootTopic(normalizedTopic);
        string conceptRoot = ExtractDiscoveryRootTopic(normalizedConcept);
        if (string.Equals(topicRoot, conceptRoot, StringComparison.OrdinalIgnoreCase) &&
            normalizedTopic.Contains(normalizedConcept, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedConcept;
        }

        return normalizedTopic;
    }

    private static string NormalizeDiscoveryEvidenceText(string evidence, string concept)
    {
        string compact = CompactEvidence(evidence);
        if (string.IsNullOrWhiteSpace(compact))
            return string.Empty;

        string normalizedConcept = NormalizeConcept(concept);
        string normalizedEvidence = NormalizeConcept(compact);
        if (string.IsNullOrWhiteSpace(normalizedEvidence))
            return string.Empty;

        if (IsBridgeSummaryText(compact))
            return string.Empty;

        if (normalizedEvidence.Contains("was received through", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("the topic ", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("route at 2026-", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("this cycle, i deepened work on", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("preparing daily reflection", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("promoted belief", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("evidence:[", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("\"evidence\":[", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("belief fading", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("dropped below confidence", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("highlights include", StringComparison.OrdinalIgnoreCase) ||
            normalizedEvidence.Contains("toward thinking", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (normalizedEvidence.StartsWith("my belief in ", StringComparison.OrdinalIgnoreCase) &&
            normalizedEvidence.Contains(" is fading", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (normalizedEvidence.StartsWith("this thread is useful for understanding ", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (normalizedEvidence.StartsWith("a useful next step is to connect ", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(normalizedConcept) &&
            (compact.IndexOf("[concept_discovery]", StringComparison.OrdinalIgnoreCase) >= 0 ||
             compact.IndexOf("(exploratory)", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return $"Knowledge ingested for {normalizedConcept}.";
        }

        if (!string.IsNullOrWhiteSpace(normalizedConcept) &&
            normalizedEvidence.StartsWith("ingested ", StringComparison.OrdinalIgnoreCase) &&
            normalizedEvidence.Contains(" data for topic ", StringComparison.OrdinalIgnoreCase))
        {
            return $"Knowledge ingested for {normalizedConcept}.";
        }

        if (!string.IsNullOrWhiteSpace(normalizedConcept) &&
            (normalizedEvidence.StartsWith("ingested topic:", StringComparison.OrdinalIgnoreCase) ||
             normalizedEvidence.StartsWith("knowledge ingested for ", StringComparison.OrdinalIgnoreCase)))
        {
            return $"Knowledge ingested for {normalizedConcept}.";
        }

        return compact;
    }

    private static bool IsBridgeSummaryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string normalized = text.Trim().ToLowerInvariant();
        return normalized.Contains("local bridge synthesis for topic") ||
               normalized.Contains("\"route\":\"web\"") ||
               normalized.Contains("route web") ||
               normalized.Contains("\"summary\":\"local bridge synthesis");
    }

    private static bool ShouldSkipMemoryForDiscovery(MemoryEntry memory)
    {
        if (memory == null)
            return true;

        string category = (memory.category ?? string.Empty).Trim().ToLowerInvariant();
        string[] blockedCategories =
        {
            "activity",
            "api",
            "api_wrapper",
            "apischeduler",
            "apistagecomplete",
            "beliefadjustment",
            "deferredreflection",
            "emotiondecay",
            "externalknowledge",
            "goalplanning",
            "goalexecution",
            "internalmonologue",
            "knowledgerequest",
            "recallcandidate",
            "shapeintelligence",
            "websocket"
        };

        if (blockedCategories.Contains(category))
            return true;

        return IsBridgeSummaryText(memory.content);
    }

    private static int CountMetadataTokens(string normalized)
    {
        string[] metadataTokens =
        {
            "bridge",
            "local",
            "route",
            "summary",
            "topic",
            "web",
            "service",
            "timestamp",
            "evidence",
            "domain"
        };

        int count = 0;
        foreach (string token in metadataTokens)
        {
            if (normalized.Contains(token))
                count += 1;
        }

        return count;
    }

    private static bool IsExpansionFragmentDominated(string[] tokens)
    {
        if (tokens == null || tokens.Length < 3)
            return false;

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
        return genericCount >= tokens.Length - 1 ||
               (tokens.Length >= 4 && genericCount >= 2);
    }

    private DiscoveredConceptWrapper LoadDiscoveryIndex()
    {
        try
        {
            if (!File.Exists(discoveryPath))
                return new DiscoveredConceptWrapper();

            string json = File.ReadAllText(discoveryPath);
            var wrapper = JsonUtility.FromJson<DiscoveredConceptWrapper>(json) ?? new DiscoveredConceptWrapper();
            if (PruneDiscoveryIndex(wrapper) > 0)
                PersistLoadedDiscoveryIndex(wrapper);
            return wrapper;
        }
        catch
        {
            return new DiscoveredConceptWrapper();
        }
    }

    private void PersistDiscoveryIndex()
    {
        try
        {
            if (discoveryIndex != null)
                PruneDiscoveryIndex(discoveryIndex);

            ArTusPathUtility.EnsureParentDirectory(discoveryPath);
            string json = JsonUtility.ToJson(discoveryIndex, true);
            File.WriteAllText(discoveryPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ConceptDiscovery] Failed to persist concept discoveries: {ex.Message}");
        }
    }

    private int PruneDiscoveryIndex(DiscoveredConceptWrapper wrapper)
    {
        if (wrapper == null)
            return 0;

        int beforeCount = wrapper.entries?.Count ?? 0;
        if (beforeCount == 0)
            return 0;

        foreach (var entry in wrapper.entries ?? new List<DiscoveredConceptRecord>())
        {
            if (entry == null)
                continue;

            entry.concept = NormalizeConcept(entry.concept);
            entry.seedTopic = NormalizeDiscoverySupportTopic(entry.seedTopic, entry.concept);
            entry.supportingTopics = (entry.supportingTopics ?? new List<string>())
                .Select(topic => NormalizeDiscoverySupportTopic(topic, entry.concept))
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
            entry.evidence = (entry.evidence ?? new List<string>())
                .Select(evidence => NormalizeDiscoveryEvidenceText(evidence, entry.concept))
                .Where(evidence => !string.IsNullOrWhiteSpace(evidence))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
        }

        wrapper.entries = (wrapper.entries ?? new List<DiscoveredConceptRecord>())
            .Where(entry =>
                entry != null &&
                IsDiscoveryCandidate(entry.concept) &&
                IsDiscoveryCandidate(entry.seedTopic) &&
                (entry.supportingTopics?.Any(topic => IsDiscoveryCandidate(topic)) ?? false))
            .GroupBy(entry => entry.concept, StringComparer.OrdinalIgnoreCase)
            .Select(group => MergeDiscoveryEntries(group))
            .Where(entry => entry != null)
            .ToList();

        return beforeCount - wrapper.entries.Count;
    }

    private static DiscoveredConceptRecord MergeDiscoveryEntries(IEnumerable<DiscoveredConceptRecord> group)
    {
        var entries = group?.Where(entry => entry != null).ToList();
        if (entries == null || entries.Count == 0)
            return null;

        var primary = entries
            .OrderByDescending(entry => entry.promotedCount)
            .ThenByDescending(entry => entry.supportCount)
            .ThenByDescending(entry => entry.updatedAt)
            .First();

        primary.supportingTopics = entries
            .SelectMany(entry => entry.supportingTopics ?? new List<string>())
            .Select(topic => NormalizeDiscoverySupportTopic(topic, primary.concept))
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        primary.evidence = entries
            .SelectMany(entry => entry.evidence ?? new List<string>())
            .Select(evidence => NormalizeDiscoveryEvidenceText(evidence, primary.concept))
            .Where(evidence => !string.IsNullOrWhiteSpace(evidence))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        primary.supportCount = Mathf.Max(primary.supportCount, primary.supportingTopics.Count);
        primary.promotedCount = entries.Max(entry => entry.promotedCount);
        primary.status = entries.Any(entry => string.Equals(entry.status, "promoted", StringComparison.OrdinalIgnoreCase))
            ? "promoted"
            : primary.status;

        return primary;
    }

    private void PersistLoadedDiscoveryIndex(DiscoveredConceptWrapper wrapper)
    {
        try
        {
            ArTusPathUtility.EnsureParentDirectory(discoveryPath);
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(discoveryPath, json);
        }
        catch
        {
        }
    }
}

[Serializable]
public enum ArTusExecutionStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

[Serializable]
public enum ArTusExecutionStepType
{
    IngestTopic,
    FetchKnowledge,
    QueueReflection,
    ScheduleDomainExpansion,
    RunSimulation,
    ReinforceBelief,
    ApplyShapeKnowledge,
    EvaluateShapeReconstruction,
    RefineShapeDescriptor,
    RefreshConceptDiscovery
}

[Serializable]
public class ArTusExecutionStep
{
    public string id;
    public string title;
    public ArTusExecutionStepType type;
    public string target;
    public string route;
    public string domain;
    public float weight;
    public ArTusExecutionStepStatus status;
    public string lastResult;
    public string updatedAt;
}

[Serializable]
public class ArTusExecutionPlan
{
    public string summary;
    public string extractedTopic;
    public List<ArTusExecutionStep> steps = new();
}

public class ArTusGoalExecutor : MonoBehaviour
{
    [Header("Execution")]
    public float minStepInterval = 2f;
    public bool enableDebug = true;

    private ArTusCoreState core;
    private ArTusIngestor ingestor;
    private ArTusArmudaSimulator simulator;
    private ArTusBeliefEngine beliefEngine;
    private ArTusShapeKnowledgeBridge shapeKnowledgeBridge;
    private ArTusShapeReconstruction shapeReconstruction;
    private ArTusConceptDiscovery conceptDiscovery;
    private float lastStepTime;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void ResolveDependencies()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
        if (ingestor == null)
            ingestor = GetComponent<ArTusIngestor>() ?? FindAnyObjectByType<ArTusIngestor>();
        if (simulator == null)
            simulator = GetComponent<ArTusArmudaSimulator>() ?? FindAnyObjectByType<ArTusArmudaSimulator>();
        if (beliefEngine == null)
            beliefEngine = GetComponent<ArTusBeliefEngine>() ?? FindAnyObjectByType<ArTusBeliefEngine>();
        if (conceptDiscovery == null)
            conceptDiscovery = GetComponent<ArTusConceptDiscovery>() ?? FindAnyObjectByType<ArTusConceptDiscovery>();
        if (shapeKnowledgeBridge == null)
            shapeKnowledgeBridge = GetComponent<ArTusShapeKnowledgeBridge>() ?? FindAnyObjectByType<ArTusShapeKnowledgeBridge>();
        if (shapeReconstruction == null)
            shapeReconstruction = GetComponent<ArTusShapeReconstruction>() ?? FindAnyObjectByType<ArTusShapeReconstruction>();
    }

    public bool TryExecuteGoal(ArTusGoal goal)
    {
        if (goal == null || IsTerminal(goal.status))
            return false;

        if (Time.time - lastStepTime < minStepInterval)
            return false;

        EnsurePlan(goal);

        var nextStep = goal.executionPlan.steps.FirstOrDefault(
            s => s.status == ArTusExecutionStepStatus.Pending
        );

        if (nextStep == null)
        {
            goal.status = ArTusGoalStatus.Completed;
            goal.completed = true;
            goal.executionSummary = "All execution steps completed.";
            goal.lastUpdatedAt = StampNow();
            return true;
        }

        goal.status = ArTusGoalStatus.Running;
        nextStep.status = ArTusExecutionStepStatus.Running;
        nextStep.updatedAt = StampNow();

        bool success = ExecuteStep(goal, nextStep, out string result);

        nextStep.lastResult = result;
        nextStep.updatedAt = StampNow();
        nextStep.status = success
            ? ArTusExecutionStepStatus.Completed
            : ArTusExecutionStepStatus.Failed;

        goal.executionSummary = result;
        goal.lastUpdatedAt = StampNow();
        goal.status = success ? ArTusGoalStatus.Running : ArTusGoalStatus.Blocked;

        lastStepTime = Time.time;

        if (success && goal.executionPlan.steps.All(
            s => s.status == ArTusExecutionStepStatus.Completed ||
                 s.status == ArTusExecutionStepStatus.Skipped
        ))
        {
            goal.status = ArTusGoalStatus.Completed;
            goal.completed = true;
            goal.executionSummary = "Goal completed through executor plan.";
            goal.lastUpdatedAt = StampNow();
        }

        if (enableDebug)
            Debug.Log($"[GoalExecutor] {goal.description} -> {nextStep.type} | {result}");

        return true;
    }

    private void EnsurePlan(ArTusGoal goal)
    {
        if (goal.executionPlan != null && goal.executionPlan.steps.Count > 0)
            return;

        string topic = ExtractTopic(goal.description);

        goal.executionPlan = BuildPlan(goal, topic);
        goal.status = ArTusGoalStatus.Planning;
        goal.lastUpdatedAt = StampNow();

        core?.LogMemory(
            $"Planned goal '{goal.description}' into {goal.executionPlan.steps.Count} steps.",
            "GoalPlanning",
            2,
            goal.emotionTag
        );
    }

    private ArTusExecutionPlan BuildPlan(ArTusGoal goal, string topic)
    {
        var plan = new ArTusExecutionPlan
        {
            extractedTopic = topic,
            summary = $"Execution plan for {goal.description}"
        };

        string domain = string.IsNullOrWhiteSpace(goal.domain) ? "general" : goal.domain;
        string route = "web";
        float weight = Mathf.Clamp01(goal.confidence);

        if (string.Equals(goal.domain, "verification", StringComparison.OrdinalIgnoreCase))
        {
            plan.steps.Add(CreateStep("Seed verification topic", ArTusExecutionStepType.IngestTopic, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Fetch comparison evidence", ArTusExecutionStepType.FetchKnowledge, topic, route, domain, weight));

            string followupTarget = string.Equals(goal.evidenceState, "conflicted", StringComparison.OrdinalIgnoreCase)
                ? $"{topic} independent source comparison"
                : $"{topic} corroborating evidence";

            plan.steps.Add(CreateStep("Fetch corroborating evidence", ArTusExecutionStepType.FetchKnowledge, followupTarget, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.65f))));
            plan.steps.Add(CreateStep("Schedule domain expansion", ArTusExecutionStepType.ScheduleDomainExpansion, domain, route, domain, weight));
            plan.steps.Add(CreateStep("Queue verification reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.5f))));
        }
        else if (goal.domain == "curiosity")
        {
            plan.steps.Add(CreateStep("Seed topic ingestion", ArTusExecutionStepType.IngestTopic, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Fetch external knowledge", ArTusExecutionStepType.FetchKnowledge, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Queue reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Reinforce belief", ArTusExecutionStepType.ReinforceBelief, topic, route, domain, 0.05f));
        }
        else if (goal.domain == "reflection")
        {
            plan.steps.Add(CreateStep("Queue reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Reinforce belief", ArTusExecutionStepType.ReinforceBelief, topic, route, domain, 0.03f));
        }
        else if (goal.domain == "concept_discovery")
        {
            plan.steps.Add(CreateStep("Seed discovered concept", ArTusExecutionStepType.IngestTopic, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Fetch concept evidence", ArTusExecutionStepType.FetchKnowledge, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.65f))));
            plan.steps.Add(CreateStep("Queue concept reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Refresh concept discovery", ArTusExecutionStepType.RefreshConceptDiscovery, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.5f))));
            plan.steps.Add(CreateStep("Reinforce belief", ArTusExecutionStepType.ReinforceBelief, topic, route, domain, 0.05f));
        }
        else if (goal.domain == "shape_curation")
        {
            plan.steps.Add(CreateStep("Seed curation topic", ArTusExecutionStepType.IngestTopic, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Fetch source metadata", ArTusExecutionStepType.FetchKnowledge, $"{topic} model license attribution source", route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.65f))));
            plan.steps.Add(CreateStep("Queue curation reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.5f))));
            plan.steps.Add(CreateStep("Schedule domain expansion", ArTusExecutionStepType.ScheduleDomainExpansion, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.45f))));
        }
        else if (goal.domain == "shape_refinement")
        {
            plan.steps.Add(CreateStep("Apply shape knowledge", ArTusExecutionStepType.ApplyShapeKnowledge, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.6f))));
            plan.steps.Add(CreateStep("Evaluate reconstruction", ArTusExecutionStepType.EvaluateShapeReconstruction, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Refine descriptor target", ArTusExecutionStepType.RefineShapeDescriptor, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.5f))));
            plan.steps.Add(CreateStep("Queue reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Reinforce belief", ArTusExecutionStepType.ReinforceBelief, topic, route, domain, 0.04f));
        }
        else if (goal.domain == "shape_ingestion_resilience")
        {
            plan.steps.Add(CreateStep("Seed ingestion resilience topic", ArTusExecutionStepType.IngestTopic, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Fetch ingestion recovery context", ArTusExecutionStepType.FetchKnowledge, $"{topic} geometry source recovery import diagnostics", route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.65f))));
            plan.steps.Add(CreateStep("Apply shape knowledge", ArTusExecutionStepType.ApplyShapeKnowledge, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Evaluate reconstruction", ArTusExecutionStepType.EvaluateShapeReconstruction, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.55f))));
            plan.steps.Add(CreateStep("Queue resilience reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.5f))));
            plan.steps.Add(CreateStep("Schedule domain expansion", ArTusExecutionStepType.ScheduleDomainExpansion, topic, route, domain, Mathf.Clamp01(Mathf.Max(weight, 0.45f))));
        }
        else
        {
            plan.steps.Add(CreateStep("Schedule domain expansion", ArTusExecutionStepType.ScheduleDomainExpansion, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Fetch external knowledge", ArTusExecutionStepType.FetchKnowledge, topic, route, domain, weight));
            plan.steps.Add(CreateStep("Queue reflection", ArTusExecutionStepType.QueueReflection, topic, route, domain, weight));
        }

        string lowered = goal.description.ToLowerInvariant();
        if (lowered.Contains("conflict") || lowered.Contains("ethic") || lowered.Contains("simulate"))
        {
            plan.steps.Insert(
                Math.Min(1, plan.steps.Count),
                CreateStep("Run simulation", ArTusExecutionStepType.RunSimulation, topic, route, domain, weight)
            );
        }

        return plan;
    }

    private static ArTusExecutionStep CreateStep(
        string title,
        ArTusExecutionStepType type,
        string target,
        string route,
        string domain,
        float weight
    )
    {
        return new ArTusExecutionStep
        {
            id = Guid.NewGuid().ToString(),
            title = title,
            type = type,
            target = target,
            route = route,
            domain = domain,
            weight = weight,
            status = ArTusExecutionStepStatus.Pending,
            updatedAt = StampNow()
        };
    }

    private bool ExecuteStep(ArTusGoal goal, ArTusExecutionStep step, out string result)
    {
        ResolveDependencies();
        result = step.title;

        switch (step.type)
        {
            case ArTusExecutionStepType.IngestTopic:
                if (ingestor == null)
                {
                    result = "Ingestor missing.";
                    return false;
                }

                ingestor.IngestSmartTopic(step.target, step.domain, Mathf.Clamp01(Mathf.Max(step.weight, 0.6f)));
                if (core != null && !string.IsNullOrWhiteSpace(step.target))
                    core.lastIngestedTopic = step.target.Trim();
                core?.LogMemory($"Executor ingested '{step.target}'.", "GoalExecution", 2, goal.emotionTag);
                result = $"Ingested topic '{step.target}'.";
                return true;

            case ArTusExecutionStepType.FetchKnowledge:
                if (core == null)
                {
                    result = "CoreState missing.";
                    return false;
                }

                core.FetchExternalKnowledge(step.route, step.target, step.domain);
                result = $"Requested external knowledge for '{step.target}'.";
                return true;

            case ArTusExecutionStepType.QueueReflection:
                if (core == null)
                {
                    result = "CoreState missing.";
                    return false;
                }

                core.QueueDeferredReflection(step.target, step.domain, step.weight);
                result = $"Queued reflection for '{step.target}'.";
                return true;

            case ArTusExecutionStepType.ScheduleDomainExpansion:
                if (core == null)
                {
                    result = "CoreState missing.";
                    return false;
                }

                core.ScheduleDomainExpansion(step.target);
                result = $"Scheduled expansion for '{step.target}'.";
                return true;

            case ArTusExecutionStepType.RunSimulation:
                if (simulator == null)
                {
                    result = "Simulator missing.";
                    return false;
                }

                simulator.RunSimulation(step.target, $"goal:{goal.id}");
                result = $"Simulation started for '{step.target}'.";
                return true;

            case ArTusExecutionStepType.ReinforceBelief:
                if (beliefEngine == null && core == null)
                {
                    result = "Belief engine missing.";
                    return false;
                }

                if (beliefEngine != null)
                    beliefEngine.ReinforceBelief(step.target, Mathf.Max(step.weight, 0.01f), "goal-executor");
                else
                    core.ReinforceBelief(step.target, Mathf.Max(step.weight, 0.01f));

                result = $"Reinforced belief '{step.target}'.";
                return true;

            case ArTusExecutionStepType.ApplyShapeKnowledge:
                if (shapeKnowledgeBridge == null)
                {
                    result = "Shape knowledge bridge missing.";
                    return false;
                }

                if (!shapeKnowledgeBridge.ApplyShapeForTopic(step.target, goal.category))
                {
                    result = $"No shape knowledge available for '{step.target}'.";
                    return false;
                }

                result = $"Applied shape knowledge for '{step.target}'.";
                return true;

            case ArTusExecutionStepType.EvaluateShapeReconstruction:
                if (shapeReconstruction == null)
                {
                    result = "Shape reconstruction missing.";
                    return false;
                }

                shapeReconstruction.EvaluateCurrentShape();
                result = $"Evaluated reconstruction for '{step.target}' with score {shapeReconstruction.GetLastFinalScore():F2}.";
                return true;

            case ArTusExecutionStepType.RefineShapeDescriptor:
                if (shapeKnowledgeBridge == null)
                {
                    result = "Shape knowledge bridge missing.";
                    return false;
                }

                float observedScore = shapeReconstruction != null
                    ? shapeReconstruction.GetLastFinalScore()
                    : -1f;

                if (!shapeKnowledgeBridge.RefineShapeDescriptorForTopic(step.target, goal.category, observedScore))
                {
                    result = $"No descriptor refinement available for '{step.target}'.";
                    return false;
                }

                result = $"Refined descriptor target for '{step.target}' after score {Mathf.Clamp01(observedScore):F2}.";
                return true;

            case ArTusExecutionStepType.RefreshConceptDiscovery:
                if (conceptDiscovery == null)
                {
                    result = "Concept discovery missing.";
                    return false;
                }

                conceptDiscovery.RefreshDiscoveriesIfNeeded(true);
                result = $"Refreshed concept discovery after exploring '{step.target}'.";
                return true;
        }

        result = $"Unsupported step type '{step.type}'.";
        return false;
    }

    private static string ExtractTopic(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "systems thinking";

        string cleaned = description.Trim();
        string[] prefixes =
        {
            "Learn about ",
            "Reflect on ",
            "Explore ",
            "Investigate ",
            "Study ",
            "Analyze ",
            "Refine visual embodiment of "
            ,"Discover emerging concept around "
        };

        foreach (string prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return cleaned.Substring(prefix.Length).Trim();
        }

        return cleaned;
    }

    private static bool IsTerminal(ArTusGoalStatus status)
    {
        return status == ArTusGoalStatus.Completed || status == ArTusGoalStatus.Failed;
    }

    private static string StampNow()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[Serializable]
public class ArTusGoal
{
    public string id;

    public string goalName;
    public int priority;
    public string category;
    public string createdAt;

    public string description;
    public string domain;
    public string source;
    public string emotionTag;
    public float confidence;
    public string timestamp;
    public bool completed;
    public ArTusGoalStatus status;
    public string executionSummary;
    public string lastUpdatedAt;
    public string focusTopic;
    public string triggerQuery;
    public string evidenceState;
    public List<string> citations = new();
    public ArTusExecutionPlan executionPlan = new();
}

public class ArTusGoalController : MonoBehaviour
{
    public List<ArTusGoal> activeGoals = new();
    public List<ArTusGoal> completedGoals = new();

    [Header("Autonomy Settings")]
    public float autoGoalInterval = 10f;
    public float executionInterval = 4f;

    private float lastGoalTime;
    private float lastExecutionTime;
    private string verificationAuditPath;
    private VerificationAuditWrapper verificationAudit = new();

    private ArTusCoreState core;
    private ArTusGoalExecutor executor;
    private ArTusSemanticSearch semanticSearch;
    private ArTusShapeKnowledgeBridge shapeKnowledgeBridge;
    private ArTusConceptDiscovery conceptDiscovery;
    private ArTusShapeIntelligence shapeIntelligence;
    private ArTusMorphController morphController;
    private string lastSpecificFamilyTopic = string.Empty;
    private float lastSpecificFamilyTopicTime = -9999f;
    private string lastCompletedSpecificFamilyTopic = string.Empty;
    private float lastCompletedSpecificFamilyTopicTime = -9999f;
    private string lastCompletedContinuationTopic = string.Empty;
    private float lastCompletedContinuationTopicTime = -9999f;
    private string lastFamilyRestartTopic = string.Empty;
    private float lastFamilyRestartTime = -9999f;
    private string lastSpecificContinuationSourceTopic = string.Empty;
    private string lastSpecificContinuationTargetTopic = string.Empty;
    private float lastSpecificContinuationTime = -9999f;
    private readonly Dictionary<string, float> recentSpecificFamilyTopicVisits = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private string lastAutonomyFamilyRoot = string.Empty;
    private string lastAutonomyFamilyTopic = string.Empty;
    private float lastAutonomyFamilyAdvanceTime = -9999f;
    private int consecutiveAutonomyFamilyQueues = 0;
    private const int MaxConsecutiveAutonomyFamilyQueues = 8;
    private const float AutonomyFamilyCooldownSeconds = 120f;
    private const float AutonomyFamilyResetSeconds = 300f;
    private string lastAutonomyFamilyCooldownLogRoot = string.Empty;
    private float lastAutonomyFamilyCooldownLogTime = -9999f;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        executor = GetComponent<ArTusGoalExecutor>();
        semanticSearch = GetComponent<ArTusSemanticSearch>();
        shapeKnowledgeBridge = GetComponent<ArTusShapeKnowledgeBridge>();
        conceptDiscovery = GetComponent<ArTusConceptDiscovery>() ?? GetComponentInChildren<ArTusConceptDiscovery>();
        shapeIntelligence = GetComponent<ArTusShapeIntelligence>() ?? FindAnyObjectByType<ArTusShapeIntelligence>();
        morphController = GetComponent<ArTusMorphController>() ?? FindAnyObjectByType<ArTusMorphController>();
        verificationAuditPath = ArTusPathUtility.GetPersistent("UNIVERcity/Verification/verification_audit.json");
        LoadVerificationAudit();

        if (executor == null)
            executor = gameObject.AddComponent<ArTusGoalExecutor>();
        if (conceptDiscovery == null)
            conceptDiscovery = gameObject.AddComponent<ArTusConceptDiscovery>();
    }

    void Update()
    {
        RunAutonomy();
        RunExecution();
        DecayGoals(0.002f);
    }

    // =========================================================
    // 🧠 AUTONOMY LOOP
    // =========================================================
    void RunAutonomy()
    {
        if (Time.time - lastGoalTime > autoGoalInterval)
        {
            if (activeGoals.Count == 0)
            {
                GenerateAutonomousGoal();
                lastGoalTime = Time.time;
            }
        }
    }

    // =========================================================
    // 🚀 EXECUTION LOOP
    // =========================================================
    void RunExecution()
    {
        if (Time.time - lastExecutionTime < executionInterval) return;
        if (!HasActiveGoals()) return;

        var goal = GetTopGoal();
        if (goal == null) return;

        // 🚫 HARD BLOCK: recursive reflection loops
        string lowered = goal.description.ToLower();
        if (lowered.Contains("self reflection") ||
            lowered.Contains("reflect on learn"))
        {
            Debug.Log("[GoalExec] 🚫 Skipping recursive reflection goal");
            CompleteGoal(goal.id);
            return;
        }

        Debug.Log($"[GoalExec] 🚀 Advancing → {goal.description} ({goal.status})");

        if (executor != null)
            executor.TryExecuteGoal(goal);

        if (goal.status == ArTusGoalStatus.Completed)
            CompleteGoal(goal.id);

        lastExecutionTime = Time.time;
    }

    // =========================================================
    // 🔥 BEHAVIOR HANDLERS
    // =========================================================
    void TriggerCuriosity(ArTusGoal goal)
    {
        core?.LogMemory($"🧠 Exploring: {goal.description}", "Curiosity", 2, goal.emotionTag);

        Debug.Log($"[Curiosity] 🌊 Diving into → {goal.description}");
    }

    void TriggerReflection(ArTusGoal goal)
    {
        // 🚫 Prevent reflection recursion
        if (goal.description.ToLower().Contains("self reflection"))
            return;

        core?.LogMemory($"🪞 Reflecting: {goal.description}", "Reflection", 3, "reflective");

        Debug.Log($"[Reflection] 🔁 Processing → {goal.description}");
    }

    void TriggerGeneric(ArTusGoal goal)
    {
        core?.LogMemory($"⚙ Executing: {goal.description}", "Action", 2, "neutral");
    }

    // =========================================================
    // 🧠 GOAL GENERATION
    // =========================================================
    void GenerateAutonomousGoal()
    {
        string discoveredConcept = GetPriorityDiscoveredConcept();
        if (!string.IsNullOrWhiteSpace(discoveredConcept))
        {
            bool queuedDiscovery = TryQueueConceptDiscoveryGoal(discoveredConcept, "general", 0.83f);
            if (queuedDiscovery)
            {
                Debug.Log($"[Autonomy] 🔬 Prioritized concept discovery → {discoveredConcept}");
                return;
            }
        }

        string ingestionRiskTopic = GetPriorityShapeIngestionRiskTopic();
        if (!string.IsNullOrWhiteSpace(ingestionRiskTopic))
        {
            bool queuedIngestionRisk = TryQueueShapeIngestionResilienceGoal(ingestionRiskTopic, "shape_ingestion_resilience", 0.84f);
            if (queuedIngestionRisk)
            {
                Debug.Log($"[Autonomy] 📡 Prioritized ingestion resilience → {ingestionRiskTopic}");
                return;
            }
        }

        string curationTopic = GetPriorityShapeCurationTopic();
        if (!string.IsNullOrWhiteSpace(curationTopic))
        {
            bool queuedCuration = TryQueueShapeCurationGoal(curationTopic, "shape_curation", 0.82f);
            if (queuedCuration)
            {
                Debug.Log($"[Autonomy] 🧾 Prioritized shape curation → {curationTopic}");
                return;
            }
        }

        string refinementTopic = GetPriorityShapeRefinementTopic();
        if (!string.IsNullOrWhiteSpace(refinementTopic))
        {
            bool queuedRefinement = TryQueueShapeRefinementGoal(refinementTopic, "general", 0.8f);
            if (queuedRefinement)
            {
                Debug.Log($"[Autonomy] 🌀 Prioritized shape refinement → {refinementTopic}");
                return;
            }
        }

        string threadContinuationTopic = GetAutonomyThreadContinuationTopic();
        if (!string.IsNullOrWhiteSpace(threadContinuationTopic))
        {
            bool queuedContinuation = TryQueueCuriosityGoal(
                threadContinuationTopic,
                "thread-continuation",
                "curious",
                0.82f,
                true,
                true);

            if (queuedContinuation)
            {
                string queuedTopic = activeGoals.LastOrDefault()?.focusTopic ?? threadContinuationTopic;
                Debug.Log($"[Autonomy] 🔁 Continued concept thread → {queuedTopic}");
                return;
            }
        }

        string familyRestartTopic = GetAutonomyFamilyRestartTopic();
        if (!string.IsNullOrWhiteSpace(familyRestartTopic))
        {
            bool queuedRestart = TryQueueCuriosityGoal(
                familyRestartTopic,
                "thread-restart",
                "curious",
                0.8f,
                true,
                true);

            if (queuedRestart)
            {
                string queuedTopic = activeGoals.LastOrDefault()?.focusTopic ?? familyRestartTopic;
                Debug.Log($"[Autonomy] ♻ Restarted concept family → {queuedTopic}");
                return;
            }
        }

        string bootstrapSpecificTopic = GetBootstrapSpecificDiscoveredConceptTopic();
        if (!string.IsNullOrWhiteSpace(bootstrapSpecificTopic))
        {
            bool queuedBootstrap = TryQueueCuriosityGoal(
                bootstrapSpecificTopic,
                "bootstrap-family",
                "curious",
                0.83f,
                true,
                true);

            if (queuedBootstrap)
            {
                string queuedTopic = activeGoals.LastOrDefault()?.focusTopic ?? bootstrapSpecificTopic;
                Debug.Log($"[Autonomy] 🧭 Bootstrapped concept family → {queuedTopic}");
                return;
            }
        }

        string topic = GetPriorityVerificationTopic();
        bool fromVerificationAudit = !string.IsNullOrWhiteSpace(topic);

        if (!fromVerificationAudit && core != null)
        {
            string autonomySeedTopic = GetPreferredAutonomySeedTopic();
            if (!string.IsNullOrEmpty(autonomySeedTopic))
            {
                bool shouldExploreSpecificTopic =
                    CountExpansionFragments(autonomySeedTopic) >= 1 &&
                    !HasRecentCuriosityGoal(autonomySeedTopic, 35d) &&
                    !HasRecentConceptDiscoveryGoal(autonomySeedTopic, 40d);

                topic = shouldExploreSpecificTopic
                    ? autonomySeedTopic
                    : GetAutonomyContinuationTopic(autonomySeedTopic);
            }
            else if (core.memoryLog != null && core.memoryLog.Count > 0)
            {
                topic = core.memoryLog[
                    UnityEngine.Random.Range(0, core.memoryLog.Count)
                ].content;
            }
        }

        if (!fromVerificationAudit &&
            string.IsNullOrWhiteSpace(topic) &&
            CountExpansionFragments(GetEmbodiedSpecificTopic()) >= 1)
        {
            return;
        }

        // 🚫 FINAL SAFETY NET
        if (string.IsNullOrEmpty(topic) || topic.ToLower().Contains("self reflection"))
        {
            topic = "systems thinking";
        }

        if (fromVerificationAudit)
        {
            bool queued = TryQueueVerificationGoal(
                topic,
                "general",
                "unverified",
                topic,
                0.78f
            );

            if (queued)
            {
                Debug.Log($"[Autonomy] 🔎 Prioritized verification revisit → {topic}");
                return;
            }
        }

        topic = SanitizeAutonomyTopic(topic);
        topic = ResolveEmbodiedAutonomyTopic(topic);
        if (string.IsNullOrWhiteSpace(topic))
            return;

        string desc = $"Learn about {topic}";

        if (!TryQueueCuriosityGoal(topic, "autonomous", "curious", UnityEngine.Random.Range(0.6f, 1.0f)))
            return;

        Debug.Log($"[Autonomy] 🧠 Generated → {desc}");
    }

    string ExpandTopic(string baseTopic)
    {
        if (string.IsNullOrWhiteSpace(baseTopic))
            return "systems thinking";

        baseTopic = SanitizeAutonomyTopic(baseTopic);

        if (CountExpansionFragments(baseTopic) >= 2)
            return baseTopic;

        string[] expansions = GetAutonomySiblingSuffixes(baseTopic)
            .Select(suffix => $"{baseTopic} {suffix}".Trim())
            .Concat(new[] { baseTopic + " systems" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return expansions[UnityEngine.Random.Range(0, expansions.Length)];
    }

    private string GetAutonomyThreadContinuationTopic()
    {
        string preferredSeed = GetPreferredAutonomySeedTopic();
        string recentSpecificGoalTopic = GetRecentSpecificGoalTopic();

        var seeds = new[] { preferredSeed, recentSpecificGoalTopic }
            .Where(seed => !string.IsNullOrWhiteSpace(seed))
            .Select(SanitizeAutonomyTopic)
            .Where(seed => CountExpansionFragments(seed) >= 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(CountSignificantConceptTokens)
            .ThenByDescending(seed => seed.Length);

        foreach (string seed in seeds)
        {
            string sibling = GetAutonomySiblingContinuationTopic(seed, 20d, 24d, 2.5d);
            if (!string.IsNullOrWhiteSpace(sibling))
                return sibling;
        }

        return string.Empty;
    }

    private string GetRecentSpecificGoalTopic()
    {
        DateTime now = DateTime.Now;

        var recentSpecificGoal = completedGoals
            .Where(goal => goal != null && !string.IsNullOrWhiteSpace(goal.focusTopic))
            .Where(goal =>
                string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(goal.domain, "curiosity", StringComparison.OrdinalIgnoreCase))
            .Select(goal => new
            {
                Topic = SanitizeAutonomyTopic(goal.focusTopic),
                UpdatedAt = DateTime.TryParse(goal.lastUpdatedAt, out DateTime parsed)
                    ? parsed
                    : DateTime.MinValue
            })
            .Where(entry => CountExpansionFragments(entry.Topic) >= 1)
            .Where(entry => entry.UpdatedAt != DateTime.MinValue && (now - entry.UpdatedAt).TotalMinutes <= 45d)
            .OrderByDescending(entry => entry.UpdatedAt)
            .FirstOrDefault();

        return recentSpecificGoal?.Topic ?? string.Empty;
    }

    private string GetPreferredAutonomySeedTopic()
    {
        var candidates = new List<string>();

        void AddCandidate(string candidate)
        {
            string sanitized = SanitizeAutonomyTopic(candidate);
            if (!IsViableAutonomySeedTopic(sanitized))
                return;

            if (!candidates.Any(existing =>
                    string.Equals(existing, sanitized, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(sanitized);
            }
        }

        AddCandidate(shapeIntelligence?.GetCurrentKnowledgeTopic());

        var activeShapeProfile = morphController?.GetActiveShapeProfile();
        AddCandidate(activeShapeProfile?.learnedTopic);
        AddCandidate(ExtractTopicFromShapeDisplayName(activeShapeProfile?.displayName));

        AddCandidate(GetRecentSpecificFamilyAnchorTopic());
        AddCandidate(GetBootstrapSpecificDiscoveredConceptTopic());

        AddCandidate(core?.focusKeyword);
        AddCandidate(core?.lastIngestedTopic);

        return candidates
            .OrderByDescending(CountSignificantConceptTokens)
            .ThenByDescending(candidate => candidate.Length)
            .FirstOrDefault();
    }

    public string GetCurrentAutonomyContextTopic()
    {
        string activeGoalTopic = SanitizeAutonomyTopic(GetTopGoal()?.focusTopic);
        if (CountExpansionFragments(activeGoalTopic) >= 1)
            return activeGoalTopic;

        string embodiedSpecificTopic = GetEmbodiedSpecificTopic();
        if (!string.IsNullOrWhiteSpace(embodiedSpecificTopic))
            return embodiedSpecificTopic;

        string recentSpecificGoalTopic = GetRecentSpecificGoalTopic();
        if (!string.IsNullOrWhiteSpace(recentSpecificGoalTopic))
            return recentSpecificGoalTopic;

        string recentSpecificFamilyAnchor = GetRecentSpecificFamilyAnchorTopic();
        if (!string.IsNullOrWhiteSpace(recentSpecificFamilyAnchor))
            return recentSpecificFamilyAnchor;

        string bootstrapSpecificTopic = GetBootstrapSpecificDiscoveredConceptTopic();
        if (!string.IsNullOrWhiteSpace(bootstrapSpecificTopic))
            return bootstrapSpecificTopic;

        string preferredSeedTopic = GetPreferredAutonomySeedTopic();
        if (!string.IsNullOrWhiteSpace(preferredSeedTopic))
            return preferredSeedTopic;

        return string.Empty;
    }

    private string GetEmbodiedSpecificTopic()
    {
        var candidates = new List<string>();

        void AddCandidate(string candidate)
        {
            string sanitized = SanitizeAutonomyTopic(candidate);
            if (string.IsNullOrWhiteSpace(sanitized) || CountExpansionFragments(sanitized) < 1)
                return;

            if (!candidates.Any(existing => string.Equals(existing, sanitized, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(sanitized);
        }

        AddCandidate(shapeIntelligence?.GetCurrentKnowledgeTopic());

        var activeShapeProfile = morphController?.GetActiveShapeProfile();
        AddCandidate(activeShapeProfile?.learnedTopic);
        AddCandidate(ExtractTopicFromShapeDisplayName(activeShapeProfile?.displayName));

        return candidates
            .OrderByDescending(CountSignificantConceptTokens)
            .ThenByDescending(candidate => candidate.Length)
            .FirstOrDefault();
    }

    private string ResolveEmbodiedAutonomyTopic(string requestedTopic)
    {
        string sanitizedRequested = SanitizeAutonomyTopic(requestedTopic);
        string embodiedSpecificTopic = GetEmbodiedSpecificTopic();

        if (string.IsNullOrWhiteSpace(embodiedSpecificTopic))
            return sanitizedRequested;

        if (string.IsNullOrWhiteSpace(sanitizedRequested))
            return embodiedSpecificTopic;

        if (IsParentFallbackForEmbodiedThread(sanitizedRequested, embodiedSpecificTopic))
        {
            string siblingContinuation = GetAutonomySiblingContinuationTopic(embodiedSpecificTopic, 30d, 35d);
            if (!string.IsNullOrWhiteSpace(siblingContinuation))
                return siblingContinuation;

            return embodiedSpecificTopic;
        }

        return sanitizedRequested;
    }

    private string GetAutonomyFamilyRestartTopic()
    {
        string recentSpecificGoalTopic = GetRecentSpecificGoalTopic();
        string embodiedSpecificTopic = GetEmbodiedSpecificTopic();
        string recentSpecificFamilyAnchor = GetRecentSpecificFamilyAnchorTopic();
        string contextTopic =
            CountExpansionFragments(embodiedSpecificTopic) >= 1
                ? embodiedSpecificTopic
                : (CountExpansionFragments(recentSpecificGoalTopic) >= 1
                    ? recentSpecificGoalTopic
                    : (CountExpansionFragments(recentSpecificFamilyAnchor) >= 1
                        ? recentSpecificFamilyAnchor
                        : GetCurrentAutonomyContextTopic()));

        if (CountExpansionFragments(contextTopic) < 1)
            return string.Empty;

        string recentRestartTopic = GetRecentFamilyRestartTopic(45f);
        string recentCompletedTopic = GetRecentCompletedSpecificFamilyTopic(45f);
        string recentContinuationTopic = GetRecentCompletedContinuationTopic(30f);

        var restartExcludedTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddRestartExclusion(string topic)
        {
            string normalizedTopic = SanitizeAutonomyTopic(topic);
            if (!string.IsNullOrWhiteSpace(normalizedTopic) && CountExpansionFragments(normalizedTopic) >= 1)
                restartExcludedTopics.Add(normalizedTopic);
        }

        AddRestartExclusion(recentSpecificGoalTopic);
        AddRestartExclusion(embodiedSpecificTopic);
        AddRestartExclusion(recentSpecificFamilyAnchor);
        AddRestartExclusion(recentRestartTopic);
        AddRestartExclusion(recentCompletedTopic);
        AddRestartExclusion(recentContinuationTopic);

        string siblingContinuation = GetAutonomySiblingContinuationTopic(
            contextTopic,
            2d,
            3d,
            0d,
            restartExcludedTopics.ToArray());
        if (!string.IsNullOrWhiteSpace(siblingContinuation) &&
            !restartExcludedTopics.Contains(SanitizeAutonomyTopic(siblingContinuation)))
        {
            return siblingContinuation;
        }

        string excludedTopic = CountExpansionFragments(recentSpecificGoalTopic) >= 1
            ? recentSpecificGoalTopic
            : (CountExpansionFragments(recentSpecificFamilyAnchor) >= 1
                ? recentSpecificFamilyAnchor
                : contextTopic);

        var excludedTopics = new List<string>();
        if (!string.IsNullOrWhiteSpace(excludedTopic))
            excludedTopics.Add(excludedTopic);
        excludedTopics.AddRange(restartExcludedTopics);

        return GetAutonomyThreadRestartTopic(contextTopic, 0d, excludedTopics.ToArray());
    }

    private string GetBootstrapSpecificDiscoveredConceptTopic(
        string preferredRoot = "",
        string excludedTopic = "")
    {
        if (conceptDiscovery == null)
            return string.Empty;

        string normalizedPreferredRoot = SanitizeAutonomyTopic(preferredRoot);
        if (string.IsNullOrWhiteSpace(normalizedPreferredRoot))
            normalizedPreferredRoot = ExtractAutonomyRootTopic(GetEmbodiedSpecificTopic());
        if (string.IsNullOrWhiteSpace(normalizedPreferredRoot))
            normalizedPreferredRoot = ExtractAutonomyRootTopic(GetRecentSpecificFamilyAnchorTopic());
        if (string.IsNullOrWhiteSpace(normalizedPreferredRoot))
            normalizedPreferredRoot = "systems thinking";

        string normalizedExcludedTopic = SanitizeAutonomyTopic(excludedTopic);
        string recentRestartTopic = GetRecentFamilyRestartTopic(45f);
        string recentCompletedTopic = GetRecentCompletedSpecificFamilyTopic(45f);
        string recentContinuationTopic = GetRecentCompletedContinuationTopic(30f);
        string recentSpecificFamilyAnchor = GetRecentSpecificFamilyAnchorTopic();

        var candidates = conceptDiscovery.GetBootstrapDiscoveredConcepts(12, normalizedPreferredRoot);
        var rankedCandidates = new List<(DiscoveredConceptRecord Entry, string Topic, int VisitCount, double RecencyMinutes)>();

        foreach (var entry in candidates)
        {
            string normalizedTopic = SanitizeAutonomyTopic(entry?.concept);
            if (CountExpansionFragments(normalizedTopic) < 1)
                continue;

            if (!string.IsNullOrWhiteSpace(normalizedExcludedTopic) &&
                string.Equals(normalizedTopic, normalizedExcludedTopic, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(recentRestartTopic) &&
                string.Equals(normalizedTopic, recentRestartTopic, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(recentCompletedTopic) &&
                string.Equals(normalizedTopic, recentCompletedTopic, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(recentContinuationTopic) &&
                string.Equals(normalizedTopic, recentContinuationTopic, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(recentSpecificFamilyAnchor) &&
                string.Equals(normalizedTopic, recentSpecificFamilyAnchor, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(
                    ExtractAutonomyRootTopic(normalizedTopic),
                    normalizedPreferredRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasActiveExactThreadGoal(normalizedTopic))
                continue;

            double recencyMinutes = GetExactThreadRecencyMinutes(normalizedTopic);
            if (recencyMinutes <= 1d)
                continue;

            rankedCandidates.Add((
                entry,
                normalizedTopic,
                GetExactThreadGoalCount(normalizedTopic, "curiosity", "concept_discovery"),
                recencyMinutes));
        }

        if (rankedCandidates.Count == 0)
            return string.Empty;

        return rankedCandidates
            .OrderBy(candidate => candidate.VisitCount)
            .ThenBy(candidate => candidate.Entry.promotedCount)
            .ThenByDescending(candidate => candidate.RecencyMinutes)
            .ThenByDescending(candidate => candidate.Entry.supportCount)
            .Select(candidate => candidate.Topic)
            .FirstOrDefault() ?? string.Empty;
    }

    private string GetRecentSpecificFamilyAnchorTopic(double windowMinutes = 240d)
    {
        if (!string.IsNullOrWhiteSpace(lastSpecificFamilyTopic) &&
            CountExpansionFragments(lastSpecificFamilyTopic) >= 1 &&
            Time.time - lastSpecificFamilyTopicTime <= (float)(windowMinutes * 60d))
        {
            return lastSpecificFamilyTopic;
        }

        DateTime now = DateTime.Now;

        var recentSpecificGoal = completedGoals
            .Where(goal => goal != null && !string.IsNullOrWhiteSpace(goal.focusTopic))
            .Where(goal =>
                string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(goal.domain, "curiosity", StringComparison.OrdinalIgnoreCase))
            .Select(goal => new
            {
                Topic = SanitizeAutonomyTopic(goal.focusTopic),
                UpdatedAt = DateTime.TryParse(goal.lastUpdatedAt, out DateTime parsed)
                    ? parsed
                    : DateTime.MinValue
            })
            .Where(entry => CountExpansionFragments(entry.Topic) >= 1)
            .Where(entry => entry.UpdatedAt != DateTime.MinValue && (now - entry.UpdatedAt).TotalMinutes <= windowMinutes)
            .OrderByDescending(entry => entry.UpdatedAt)
            .FirstOrDefault();

        return recentSpecificGoal?.Topic ?? string.Empty;
    }

    private string GetRecentFamilyRestartTopic(float windowMinutes = 30f)
    {
        if (string.IsNullOrWhiteSpace(lastFamilyRestartTopic))
            return string.Empty;

        if (Time.time - lastFamilyRestartTime > windowMinutes * 60f)
            return string.Empty;

        return lastFamilyRestartTopic;
    }

    private string GetRecentCompletedSpecificFamilyTopic(float windowMinutes = 30f)
    {
        if (string.IsNullOrWhiteSpace(lastCompletedSpecificFamilyTopic))
            return string.Empty;

        if (Time.time - lastCompletedSpecificFamilyTopicTime > windowMinutes * 60f)
            return string.Empty;

        return lastCompletedSpecificFamilyTopic;
    }

    private string GetRecentCompletedContinuationTopic(float windowMinutes = 20f)
    {
        if (string.IsNullOrWhiteSpace(lastCompletedContinuationTopic))
            return string.Empty;

        if (Time.time - lastCompletedContinuationTopicTime > windowMinutes * 60f)
            return string.Empty;

        return lastCompletedContinuationTopic;
    }

    private static string ExtractTopicFromShapeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return string.Empty;

        string normalized = displayName.Trim();
        if (normalized.EndsWith(" Form", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(0, normalized.Length - " Form".Length).Trim();

        return normalized;
    }

    private bool IsViableAutonomySeedTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        if (string.Equals(topic, "systems thinking", StringComparison.OrdinalIgnoreCase))
        {
            bool hasMoreSpecificActiveShape =
                shapeIntelligence != null &&
                !string.IsNullOrWhiteSpace(shapeIntelligence.GetCurrentKnowledgeTopic()) &&
                !string.Equals(
                    SanitizeAutonomyTopic(shapeIntelligence.GetCurrentKnowledgeTopic()),
                    topic,
                    StringComparison.OrdinalIgnoreCase);

            bool hasRecentSpecificFamilyAnchor =
                CountExpansionFragments(GetRecentSpecificFamilyAnchorTopic()) >= 1 &&
                string.Equals(
                    ExtractAutonomyRootTopic(GetRecentSpecificFamilyAnchorTopic()),
                    topic,
                    StringComparison.OrdinalIgnoreCase);

            if (hasMoreSpecificActiveShape || hasRecentSpecificFamilyAnchor)
                return false;
        }

        return true;
    }

    private static int CountSignificantConceptTokens(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return 0;

        return topic
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Count(token => token.Length >= 4);
    }

    private static bool IsParentFallbackForEmbodiedThread(string candidateTopic, string embodiedTopic)
    {
        string sanitizedCandidate = SanitizeAutonomyTopic(candidateTopic);
        string sanitizedEmbodied = SanitizeAutonomyTopic(embodiedTopic);

        if (string.IsNullOrWhiteSpace(sanitizedCandidate) || string.IsNullOrWhiteSpace(sanitizedEmbodied))
            return false;

        if (string.Equals(sanitizedCandidate, sanitizedEmbodied, StringComparison.OrdinalIgnoreCase))
            return false;

        string candidateRoot = ExtractAutonomyRootTopic(sanitizedCandidate);
        string embodiedRoot = ExtractAutonomyRootTopic(sanitizedEmbodied);

        if (!string.Equals(candidateRoot, embodiedRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        return CountExpansionFragments(sanitizedCandidate) < CountExpansionFragments(sanitizedEmbodied);
    }

    private static string ExtractAutonomyRootTopic(string topic)
    {
        string sanitizedTopic = SanitizeAutonomyTopic(topic);
        if (string.IsNullOrWhiteSpace(sanitizedTopic))
            return string.Empty;

        string[] siblingSuffixes = GetKnownAutonomySiblingSuffixes();

        foreach (string suffix in siblingSuffixes.OrderByDescending(s => s.Length))
        {
            string marker = " " + suffix;
            if (sanitizedTopic.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
                return sanitizedTopic.Substring(0, sanitizedTopic.Length - marker.Length).Trim();
        }

        return sanitizedTopic;
    }

    private static string[] GetKnownAutonomySiblingSuffixes()
    {
        return new[]
        {
            "basics",
            "applications",
            "real world examples",
            "advanced concepts",
            "theory",
            "feedback loops",
            "leverage points",
            "system dynamics",
            "causal loop diagrams",
            "emergence"
        };
    }

    private static string[] GetAutonomySiblingSuffixes(string rootTopic = null)
    {
        string normalizedRoot = SanitizeAutonomyTopic(rootTopic);
        string[] coreSuffixes =
        {
            "basics",
            "applications",
            "real world examples",
            "advanced concepts",
            "theory"
        };

        if (string.IsNullOrWhiteSpace(normalizedRoot))
            return coreSuffixes;

        if (normalizedRoot.StartsWith("systems thinking", StringComparison.OrdinalIgnoreCase))
            return GetKnownAutonomySiblingSuffixes();

        return coreSuffixes;
    }

    private string GetAutonomyContinuationTopic(string baseTopic)
    {
        string sanitizedBase = SanitizeAutonomyTopic(baseTopic);
        if (string.IsNullOrWhiteSpace(sanitizedBase))
            return "systems thinking";

        string siblingContinuation = GetAutonomySiblingContinuationTopic(sanitizedBase, 18d, 22d);
        if (!string.IsNullOrWhiteSpace(siblingContinuation))
            return siblingContinuation;

        string[] siblingSuffixes = GetAutonomySiblingSuffixes(sanitizedBase);

        string rootTopic = sanitizedBase;
        string currentSuffix = string.Empty;

        foreach (string suffix in siblingSuffixes.OrderByDescending(s => s.Length))
        {
            string marker = " " + suffix;
            if (sanitizedBase.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                rootTopic = sanitizedBase.Substring(0, sanitizedBase.Length - marker.Length).Trim();
                currentSuffix = suffix;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(rootTopic))
            rootTopic = sanitizedBase;

        var candidates = new List<string>();
        foreach (string suffix in siblingSuffixes)
        {
            if (string.Equals(suffix, currentSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            candidates.Add($"{rootTopic} {suffix}".Trim());
        }

        foreach (string candidate in candidates)
        {
            string normalizedCandidate = SanitizeAutonomyTopic(candidate);
            if (string.Equals(normalizedCandidate, sanitizedBase, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!HasRecentCuriosityGoal(normalizedCandidate, 30d) &&
                !HasRecentConceptDiscoveryGoal(normalizedCandidate, 35d))
            {
                return normalizedCandidate;
            }
        }

        if (CountExpansionFragments(sanitizedBase) >= 1)
        {
            bool recentlyExploredSpecificTopic =
                HasRecentCuriosityGoal(sanitizedBase, 25d) ||
                HasRecentConceptDiscoveryGoal(sanitizedBase, 30d);

            if (recentlyExploredSpecificTopic)
                return string.Empty;

            return sanitizedBase;
        }

        return ExpandTopic(rootTopic);
    }

    private string GetAutonomySiblingContinuationTopic(
        string sanitizedBase,
        double curiosityWindowMinutes,
        double discoveryWindowMinutes,
        double exactRecencyFloorMinutes = 0d,
        params string[] excludedTopics)
    {
        if (string.IsNullOrWhiteSpace(sanitizedBase))
            return string.Empty;

        var normalizedExcludedTopics = new HashSet<string>(
            (excludedTopics ?? Array.Empty<string>())
                .Select(SanitizeAutonomyTopic)
                .Where(topic => !string.IsNullOrWhiteSpace(topic)),
            StringComparer.OrdinalIgnoreCase);

        string[] siblingSuffixes = GetAutonomySiblingSuffixes(sanitizedBase);

        string rootTopic = sanitizedBase;
        string currentSuffix = string.Empty;

        foreach (string suffix in siblingSuffixes.OrderByDescending(s => s.Length))
        {
            string marker = " " + suffix;
            if (sanitizedBase.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                rootTopic = sanitizedBase.Substring(0, sanitizedBase.Length - marker.Length).Trim();
                currentSuffix = suffix;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(rootTopic))
            rootTopic = sanitizedBase;

        var rankedCandidates = new List<(
            string Topic,
            bool IsFresh,
            bool RepeatsRecentTransition,
            bool WasRecentlyVisitedInFamily,
            int DiscoveryCount,
            int VisitCount,
            float FamilyVisitAgeMinutes,
            double RecencyMinutes)>();

        foreach (string suffix in siblingSuffixes)
        {
            if (string.Equals(suffix, currentSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            string normalizedCandidate = SanitizeAutonomyTopic($"{rootTopic} {suffix}".Trim());
            if (string.Equals(normalizedCandidate, sanitizedBase, StringComparison.OrdinalIgnoreCase))
                continue;

            if (normalizedExcludedTopics.Contains(normalizedCandidate))
                continue;

            if (HasActiveExactThreadGoal(normalizedCandidate))
                continue;

            bool recentCuriosity = HasRecentCuriosityGoal(normalizedCandidate, curiosityWindowMinutes);
            bool recentDiscovery = HasRecentConceptDiscoveryGoal(normalizedCandidate, discoveryWindowMinutes);
            double exactRecencyMinutes = GetExactThreadRecencyMinutes(normalizedCandidate);
            if (exactRecencyMinutes <= exactRecencyFloorMinutes)
                continue;

            rankedCandidates.Add((
                normalizedCandidate,
                !recentCuriosity && !recentDiscovery,
                IsRecentSpecificContinuationTransition(sanitizedBase, normalizedCandidate),
                WasSpecificFamilyTopicVisitedRecently(normalizedCandidate, 45f),
                GetExactThreadGoalCount(normalizedCandidate, "concept_discovery"),
                GetExactThreadGoalCount(normalizedCandidate, "curiosity", "concept_discovery"),
                GetSpecificFamilyTopicVisitAgeMinutes(normalizedCandidate),
                exactRecencyMinutes));
        }

        if (rankedCandidates.Count == 0)
            return string.Empty;

        var bestCandidate = rankedCandidates
            .OrderByDescending(candidate => candidate.IsFresh)
            .ThenBy(candidate => candidate.RepeatsRecentTransition)
            .ThenBy(candidate => candidate.WasRecentlyVisitedInFamily)
            .ThenBy(candidate => candidate.DiscoveryCount)
            .ThenBy(candidate => candidate.VisitCount)
            .ThenByDescending(candidate => candidate.FamilyVisitAgeMinutes)
            .ThenByDescending(candidate => candidate.RecencyMinutes)
            .First();

        var topTierCandidates = rankedCandidates
            .Where(candidate => candidate.IsFresh == bestCandidate.IsFresh)
            .Where(candidate => candidate.RepeatsRecentTransition == bestCandidate.RepeatsRecentTransition)
            .Where(candidate => candidate.WasRecentlyVisitedInFamily == bestCandidate.WasRecentlyVisitedInFamily)
            .Where(candidate => candidate.DiscoveryCount == bestCandidate.DiscoveryCount)
            .Where(candidate => candidate.VisitCount == bestCandidate.VisitCount)
            .ToList();

        if (topTierCandidates.Count == 1)
            return topTierCandidates[0].Topic;

        float topFamilyVisitAge = topTierCandidates.Max(candidate => candidate.FamilyVisitAgeMinutes);
        var familyVisitTier = topTierCandidates
            .Where(candidate =>
                Mathf.Abs(candidate.FamilyVisitAgeMinutes - topFamilyVisitAge) < 0.01f ||
                (float.IsPositiveInfinity(candidate.FamilyVisitAgeMinutes) &&
                 float.IsPositiveInfinity(topFamilyVisitAge)) ||
                candidate.FamilyVisitAgeMinutes >= topFamilyVisitAge - 30f)
            .OrderByDescending(candidate => candidate.FamilyVisitAgeMinutes)
            .Take(Mathf.Min(3, topTierCandidates.Count))
            .ToList();

        if (familyVisitTier.Count == 1)
            return familyVisitTier[0].Topic;

        double topRecency = familyVisitTier.Max(candidate => candidate.RecencyMinutes);
        var recencyTier = familyVisitTier
            .Where(candidate =>
                Math.Abs(candidate.RecencyMinutes - topRecency) < 0.01d ||
                (double.IsPositiveInfinity(candidate.RecencyMinutes) &&
                 double.IsPositiveInfinity(topRecency)) ||
                candidate.RecencyMinutes >= topRecency - 20d)
            .OrderByDescending(candidate => candidate.RecencyMinutes)
            .Take(Mathf.Min(3, familyVisitTier.Count))
            .ToList();

        if (recencyTier.Count == 1)
            return recencyTier[0].Topic;

        return recencyTier[UnityEngine.Random.Range(0, recencyTier.Count)].Topic;
    }

    private int GetExactThreadGoalCount(string topic, params string[] domains)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return 0;

        string normalizedTopic = SanitizeAutonomyTopic(topic);
        HashSet<string> normalizedDomains = domains == null || domains.Length == 0
            ? null
            : new HashSet<string>(
                domains
                    .Where(domain => !string.IsNullOrWhiteSpace(domain))
                    .Select(domain => domain.Trim()),
                StringComparer.OrdinalIgnoreCase);

        return activeGoals
            .Concat(completedGoals)
            .Count(goal =>
            {
                if (goal == null)
                    return false;

                if (normalizedDomains != null &&
                    (string.IsNullOrWhiteSpace(goal.domain) || !normalizedDomains.Contains(goal.domain.Trim())))
                {
                    return false;
                }

                string goalTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                    ? ExtractGoalTopic(goal.description)
                    : goal.focusTopic;

                return string.Equals(
                    SanitizeAutonomyTopic(goalTopic),
                    normalizedTopic,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    // =========================================================
    // 🎯 GOAL MANAGEMENT
    // =========================================================
    public void AddGoal(string description, string domain, string source, string emotion, float confidence = 0.8f)
    {
        string lowered = description.ToLower();

        // 🚫 BLOCK RECURSIVE / BAD GOALS
        if (lowered.Contains("self reflection") ||
            lowered.Contains("reflect on learn"))
        {
            Debug.Log("[Goal] 🚫 Blocked recursive reflection goal");
            return;
        }

        ArTusGoal g = new()
        {
            id = Guid.NewGuid().ToString(),

            goalName = description,
            priority = Mathf.RoundToInt(confidence * 10),
            category = domain,
            createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),

            description = description,
            domain = domain,
            source = source,
            emotionTag = emotion,
            confidence = confidence,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            completed = false,
            status = ArTusGoalStatus.Queued,
            lastUpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            executionSummary = "Awaiting execution plan.",
            focusTopic = ExtractGoalTopic(description),
            triggerQuery = description,
            evidenceState = "general"
        };

        activeGoals.Add(g);

        Debug.Log($"[Goal] 🎯 Added → {description}");

        core?.LogMemory($"🎯 Goal created: {description}", "GoalSystem", 3, emotion);
    }

    private bool TryQueueCuriosityGoal(
        string topic,
        string source,
        string emotion,
        float confidence = 0.8f,
        bool allowRecentThreadOverride = false,
        bool forceSpecificThreadContinuation = false)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = ResolveEmbodiedAutonomyTopic(topic);
        if (string.IsNullOrWhiteSpace(normalizedTopic))
            return false;

        if (IsAutonomyContinuationSource(source) && CountExpansionFragments(normalizedTopic) < 1)
        {
            string embodiedSpecificTopic = GetEmbodiedSpecificTopic();
            string recentCompletedTopic = GetRecentCompletedSpecificFamilyTopic(90f);
            string recentSpecificFamilyAnchor = GetRecentSpecificFamilyAnchorTopic();

            bool recentSpecificFamilyExists =
                (CountExpansionFragments(embodiedSpecificTopic) >= 1 &&
                 string.Equals(
                     ExtractAutonomyRootTopic(embodiedSpecificTopic),
                     normalizedTopic,
                     StringComparison.OrdinalIgnoreCase)) ||
                (CountExpansionFragments(recentCompletedTopic) >= 1 &&
                 string.Equals(
                     ExtractAutonomyRootTopic(recentCompletedTopic),
                     normalizedTopic,
                     StringComparison.OrdinalIgnoreCase)) ||
                (CountExpansionFragments(recentSpecificFamilyAnchor) >= 1 &&
                 string.Equals(
                     ExtractAutonomyRootTopic(recentSpecificFamilyAnchor),
                     normalizedTopic,
                     StringComparison.OrdinalIgnoreCase));

            if (recentSpecificFamilyExists)
                return false;
        }

        if (IsAutonomyContinuationSource(source) && CountExpansionFragments(normalizedTopic) >= 1)
        {
            string recentCompletedTopic = GetRecentCompletedSpecificFamilyTopic(45f);
            string recentContinuationTopic = GetRecentCompletedContinuationTopic(30f);

            if ((!string.IsNullOrWhiteSpace(recentCompletedTopic) &&
                 string.Equals(normalizedTopic, recentCompletedTopic, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(recentContinuationTopic) &&
                 string.Equals(normalizedTopic, recentContinuationTopic, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        double recentThreadFloor = 8d;
        if (string.Equals(source, "completed-thread-cycle", StringComparison.OrdinalIgnoreCase))
            recentThreadFloor = 1d;
        else if (string.Equals(source, "completed-thread-restart", StringComparison.OrdinalIgnoreCase))
            recentThreadFloor = 0.25d;
        else if (string.Equals(source, "completed-thread-last-chance", StringComparison.OrdinalIgnoreCase))
            recentThreadFloor = 0d;
        else if (string.Equals(source, "completed-thread-continuation", StringComparison.OrdinalIgnoreCase))
            recentThreadFloor = 1.5d;
        else if (string.Equals(source, "thread-continuation", StringComparison.OrdinalIgnoreCase))
            recentThreadFloor = 2.5d;
        else if (string.Equals(source, "thread-restart", StringComparison.OrdinalIgnoreCase))
            recentThreadFloor = 0d;

        if (forceSpecificThreadContinuation)
        {
            if (HasActiveExactThreadGoal(normalizedTopic) ||
                GetExactThreadRecencyMinutes(normalizedTopic) <= recentThreadFloor)
            {
                return false;
            }
        }
        else if (HasRecentCuriosityGoal(normalizedTopic))
        {
            if (!allowRecentThreadOverride ||
                HasActiveExactThreadGoal(normalizedTopic) ||
                GetExactThreadRecencyMinutes(normalizedTopic) <= recentThreadFloor)
            {
                return false;
            }
        }

        if (!CanQueueAutonomyFamilyTopic(normalizedTopic, source))
        {
            string rootTopic = ExtractAutonomyRootTopic(normalizedTopic);
            if (!string.IsNullOrWhiteSpace(rootTopic) && ShouldLogAutonomyFamilyCooldown(rootTopic))
                Debug.Log($"[Autonomy] ⏸ Cooling down concept family → {rootTopic}");

            return false;
        }

        AddGoal(
            $"Learn about {normalizedTopic}",
            "curiosity",
            string.IsNullOrWhiteSpace(source) ? "autonomous" : source,
            string.IsNullOrWhiteSpace(emotion) ? "curious" : emotion,
            Mathf.Clamp(confidence, 0.45f, 0.95f)
        );

        var goal = activeGoals.LastOrDefault();
        if (goal == null)
            return false;

        goal.focusTopic = normalizedTopic;
        goal.triggerQuery = normalizedTopic;
        goal.evidenceState = "exploratory";
        goal.executionSummary = "Queued for exploratory learning.";

        RememberSpecificFamilyTopic(normalizedTopic);
        RememberAutonomyFamilyQueue(normalizedTopic, source);
        if (string.Equals(source, "thread-restart", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "bootstrap-family", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "completed-thread-restart", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "completed-thread-last-chance", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(source, "completed-thread-bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            RememberFamilyRestartTopic(normalizedTopic);
        }

        return true;
    }

    private double GetSpecificConceptDiscoveryCooldownMinutes(string topic)
    {
        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (CountExpansionFragments(normalizedTopic) < 1)
            return 25d;

        int curiosityCount = GetExactThreadGoalCount(normalizedTopic, "curiosity");
        int discoveryCount = GetExactThreadGoalCount(normalizedTopic, "concept_discovery");

        if (curiosityCount >= Mathf.Max(6, discoveryCount * 3))
            return 8d;

        if (curiosityCount >= Mathf.Max(3, discoveryCount * 2))
            return 12d;

        return 20d;
    }

    public bool TryQueueConceptDiscoveryGoal(
        string topic,
        string domain,
        float confidence = 0.83f,
        double recentWindowMinutes = 25d
    )
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (HasRecentConceptDiscoveryGoal(normalizedTopic, recentWindowMinutes))
            return false;

        AddGoal(
            $"Discover emerging concept around {normalizedTopic}",
            "concept_discovery",
            "concept-discovery",
            "curious",
            Mathf.Clamp(confidence, 0.45f, 0.97f)
        );

        var goal = activeGoals.LastOrDefault();
        if (goal == null)
            return false;

        goal.focusTopic = normalizedTopic;
        goal.category = string.IsNullOrWhiteSpace(domain) ? "general" : domain.Trim();
        goal.triggerQuery = normalizedTopic;
        goal.evidenceState = "discovered_concept";
        goal.executionSummary = "Queued from concept discovery due to recurring novel support.";

        RememberSpecificFamilyTopic(normalizedTopic);

        core?.LogMemory(
            $"Concept discovery goal queued for '{normalizedTopic}'.",
            "GoalConceptDiscovery",
            2,
            "curious"
        );

        return true;
    }

    public bool TryQueueVerificationGoal(
        string topic,
        string domain,
        string verificationState,
        string triggerQuery,
        float confidence,
        List<string> citations = null
    )
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = topic.Trim();
        string normalizedState = string.IsNullOrWhiteSpace(verificationState)
            ? "unverified"
            : verificationState.Trim().ToLowerInvariant();
        string normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? "general"
            : domain.Trim();

        if (HasRecentVerificationGoal(normalizedTopic, normalizedState))
            return false;

        string description = normalizedState == "conflicted"
            ? $"Verify conflicting evidence about {normalizedTopic}"
            : $"Verify additional evidence for {normalizedTopic}";

        AddGoal(
            description,
            "verification",
            "semantic-search",
            normalizedState == "conflicted" ? "conflicted" : "curious",
            Mathf.Clamp(confidence + 0.1f, 0.45f, 0.95f)
        );

        var goal = activeGoals.LastOrDefault();
        if (goal == null)
            return false;

        goal.focusTopic = normalizedTopic;
        goal.triggerQuery = string.IsNullOrWhiteSpace(triggerQuery) ? normalizedTopic : triggerQuery;
        goal.evidenceState = normalizedState;
        goal.category = normalizedDomain;
        goal.citations = citations != null
            ? citations.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList()
            : new List<string>();
        goal.executionSummary = $"Queued for {normalizedState} evidence follow-up.";

        core?.LogMemory(
            $"Verification goal queued for '{normalizedTopic}' ({normalizedState}).",
            "GoalVerification",
            2,
            goal.emotionTag
        );

        return true;
    }

    public bool TryQueueShapeRefinementGoal(
        string topic,
        string domain,
        float confidence = 0.8f
    )
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = topic.Trim();
        if (HasRecentShapeRefinementGoal(normalizedTopic))
            return false;

        AddGoal(
            $"Refine visual embodiment of {normalizedTopic}",
            "shape_refinement",
            "shape-analytics",
            "focused",
            Mathf.Clamp(confidence, 0.45f, 0.95f)
        );

        var goal = activeGoals.LastOrDefault();
        if (goal == null)
            return false;

        goal.focusTopic = normalizedTopic;
        goal.category = string.IsNullOrWhiteSpace(domain) ? "general" : domain.Trim();
        goal.triggerQuery = normalizedTopic;
        goal.evidenceState = "shape_refinement";
        goal.executionSummary = "Queued from shape analytics due to weak reconstruction.";

        core?.LogMemory(
            $"Shape refinement goal queued for '{normalizedTopic}'.",
            "GoalShapeRefinement",
            2,
            "focused"
        );

        return true;
    }

    public bool TryQueueShapeCurationGoal(
        string topic,
        string domain,
        float confidence = 0.82f
    )
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = topic.Trim();
        if (HasRecentShapeCurationGoal(normalizedTopic))
            return false;

        AddGoal(
            $"Curate source metadata for {normalizedTopic}",
            "shape_curation",
            "shape-manifest",
            "focused",
            Mathf.Clamp(confidence, 0.45f, 0.95f)
        );

        var goal = activeGoals.LastOrDefault();
        if (goal == null)
            return false;

        goal.focusTopic = normalizedTopic;
        goal.category = string.IsNullOrWhiteSpace(domain) ? "shape_curation" : domain.Trim();
        goal.triggerQuery = $"{normalizedTopic} model license attribution";
        goal.evidenceState = "missing_license";
        goal.executionSummary = "Queued from manifest dashboard due to missing source metadata.";

        core?.LogMemory(
            $"Shape curation goal queued for '{normalizedTopic}'.",
            "GoalShapeCuration",
            2,
            "focused"
        );

        return true;
    }

    public bool TryQueueShapeIngestionResilienceGoal(
        string topic,
        string domain,
        float confidence = 0.84f
    )
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = topic.Trim();
        if (HasRecentShapeIngestionResilienceGoal(normalizedTopic))
            return false;

        AddGoal(
            $"Stabilize shape ingestion for {normalizedTopic}",
            "shape_ingestion_resilience",
            "shape-ingestion-audit",
            "focused",
            Mathf.Clamp(confidence, 0.45f, 0.97f)
        );

        var goal = activeGoals.LastOrDefault();
        if (goal == null)
            return false;

        goal.focusTopic = normalizedTopic;
        goal.category = string.IsNullOrWhiteSpace(domain) ? "shape_ingestion_resilience" : domain.Trim();
        goal.triggerQuery = $"{normalizedTopic} geometry import diagnostics recovery";
        goal.evidenceState = "ingestion_risk";
        goal.executionSummary = "Queued from ingestion audit due to unstable source availability or weak import outcomes.";

        core?.LogMemory(
            $"Shape ingestion resilience goal queued for '{normalizedTopic}'.",
            "GoalShapeIngestion",
            2,
            "focused"
        );

        return true;
    }

    public void CompleteGoal(string id)
    {
        var g = activeGoals.Find(x => x.id == id);
        if (g == null) return;

        if (string.Equals(g.domain, "verification", StringComparison.OrdinalIgnoreCase))
            FinalizeVerificationGoal(g);
        else if (string.Equals(g.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase))
            FinalizeConceptDiscoveryGoal(g);
        else if (string.Equals(g.domain, "shape_curation", StringComparison.OrdinalIgnoreCase))
            FinalizeShapeCurationGoal(g);
        else if (string.Equals(g.domain, "shape_ingestion_resilience", StringComparison.OrdinalIgnoreCase))
            FinalizeShapeIngestionResilienceGoal(g);

        g.completed = true;
        g.lastUpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        activeGoals.Remove(g);
        completedGoals.Add(g);

        string completedTopic = SanitizeAutonomyTopic(g.focusTopic);
        if (CountExpansionFragments(completedTopic) >= 1)
        {
            RememberSpecificFamilyTopic(completedTopic);
            RememberCompletedSpecificFamilyTopic(completedTopic);
        }

        Debug.Log($"[Goal] ✅ Completed → {g.description}");

        core?.LogMemory($"✅ Goal completed: {g.description}", "GoalSystem", 4, g.emotionTag);

        TryQueueCompletedGoalContinuation(g);

        // 🔁 SAFE CHAINING
        if (g.domain == "curiosity" && g.status == ArTusGoalStatus.Completed)
        {
            if (!g.description.ToLower().Contains("reflect"))
            {
                AddGoal($"Reflect on {g.description}", "reflection", "chain", "reflective", 0.8f);
            }
        }
    }

    private bool TryQueueCompletedGoalContinuation(ArTusGoal completedGoal)
    {
        if (completedGoal == null)
            return false;

        bool isCuriosityGoal = string.Equals(completedGoal.domain, "curiosity", StringComparison.OrdinalIgnoreCase);
        bool isConceptDiscoveryGoal = string.Equals(completedGoal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase);
        if (!isCuriosityGoal && !isConceptDiscoveryGoal)
            return false;

        if (activeGoals.Any(goal =>
                goal != null &&
                !goal.completed &&
                !string.Equals(goal.domain, "reflection", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string completedTopic = SanitizeAutonomyTopic(completedGoal.focusTopic);
        if (CountExpansionFragments(completedTopic) < 1)
        {
            string bootstrapTopic = GetBootstrapSpecificDiscoveredConceptTopic(completedTopic);
            if (!string.IsNullOrWhiteSpace(bootstrapTopic) &&
                TryQueueCuriosityGoal(
                    bootstrapTopic,
                    "completed-thread-bootstrap-root",
                    "curious",
                    Mathf.Clamp(completedGoal.confidence + 0.02f, 0.76f, 0.9f)))
            {
                Debug.Log($"[Autonomy] 🌱 Bootstrapped completed concept family → {bootstrapTopic}");
                return true;
            }

            return false;
        }

        double discoveryCooldownMinutes = GetSpecificConceptDiscoveryCooldownMinutes(completedTopic);
        if (isCuriosityGoal && !HasRecentConceptDiscoveryGoal(completedTopic, discoveryCooldownMinutes))
        {
            if (TryQueueConceptDiscoveryGoal(
                    completedTopic,
                    "general",
                    Mathf.Clamp(completedGoal.confidence + 0.04f, 0.78f, 0.92f),
                    discoveryCooldownMinutes))
            {
                Debug.Log($"[Autonomy] 🔬 Deepened active concept thread → {completedTopic}");
                return true;
            }
        }

        string recentCompletedTopic = GetRecentCompletedSpecificFamilyTopic(45f);
        string recentRestartTopic = GetRecentFamilyRestartTopic(45f);
        string recentContinuationTopic = GetRecentCompletedContinuationTopic(30f);
        string[] restartExcludedTopics = new[]
        {
            completedTopic,
            recentCompletedTopic,
            recentRestartTopic,
            recentContinuationTopic
        }
        .Where(topic => !string.IsNullOrWhiteSpace(topic))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var continuationCandidates = new List<(string Topic, string Source)>
        {
            (GetAutonomySiblingContinuationTopic(completedTopic, 6d, 8d, 1.5d, restartExcludedTopics), "completed-thread-continuation"),
            (GetAutonomySiblingContinuationTopic(completedTopic, 2d, 3d, 1d, restartExcludedTopics), "completed-thread-cycle"),
            (GetAutonomyThreadRestartTopic(completedTopic, 0.5d, restartExcludedTopics), "completed-thread-restart"),
            (GetAutonomyThreadRestartTopic(completedTopic, 0d, restartExcludedTopics), "completed-thread-last-chance")
        };

        var triedTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in continuationCandidates)
        {
            string normalizedCandidate = SanitizeAutonomyTopic(candidate.Topic);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
                continue;

            if (!triedTopics.Add(normalizedCandidate))
                continue;

            if (TryQueueCuriosityGoal(
                    normalizedCandidate,
                    candidate.Source,
                    "curious",
                    Mathf.Clamp(completedGoal.confidence + 0.02f, 0.76f, 0.9f),
                    true,
                    true))
            {
                string queuedTopic = activeGoals.LastOrDefault()?.focusTopic ?? normalizedCandidate;
                RememberCompletedContinuationTopic(queuedTopic);
                RememberSpecificContinuationTransition(completedTopic, queuedTopic);
                Debug.Log($"[Autonomy] 🔁 Continued completed concept thread → {queuedTopic}");
                return true;
            }
        }

        string bootstrapCandidate = GetBootstrapSpecificDiscoveredConceptTopic(
            ExtractAutonomyRootTopic(completedTopic),
            completedTopic);

        if (!string.IsNullOrWhiteSpace(bootstrapCandidate) &&
            triedTopics.Add(bootstrapCandidate) &&
            TryQueueCuriosityGoal(
                bootstrapCandidate,
                "completed-thread-bootstrap",
                "curious",
                Mathf.Clamp(completedGoal.confidence + 0.02f, 0.76f, 0.9f),
                true,
                true))
        {
            string queuedTopic = activeGoals.LastOrDefault()?.focusTopic ?? bootstrapCandidate;
            RememberCompletedContinuationTopic(queuedTopic);
            RememberSpecificContinuationTransition(completedTopic, queuedTopic);
            Debug.Log($"[Autonomy] 🧭 Bootstrapped completed concept thread → {queuedTopic}");
            return true;
        }

        return false;
    }

    private void RememberSpecificFamilyTopic(string topic)
    {
        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (CountExpansionFragments(normalizedTopic) < 1)
            return;

        lastSpecificFamilyTopic = normalizedTopic;
        lastSpecificFamilyTopicTime = Time.time;
        recentSpecificFamilyTopicVisits[normalizedTopic] = Time.time;
    }

    private void RememberCompletedSpecificFamilyTopic(string topic)
    {
        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (CountExpansionFragments(normalizedTopic) < 1)
            return;

        lastCompletedSpecificFamilyTopic = normalizedTopic;
        lastCompletedSpecificFamilyTopicTime = Time.time;
    }

    private void RememberCompletedContinuationTopic(string topic)
    {
        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (CountExpansionFragments(normalizedTopic) < 1)
            return;

        lastCompletedContinuationTopic = normalizedTopic;
        lastCompletedContinuationTopicTime = Time.time;
    }

    private void RememberSpecificContinuationTransition(string fromTopic, string toTopic)
    {
        string normalizedFrom = SanitizeAutonomyTopic(fromTopic);
        string normalizedTo = SanitizeAutonomyTopic(toTopic);
        if (CountExpansionFragments(normalizedFrom) < 1 || CountExpansionFragments(normalizedTo) < 1)
            return;

        if (!string.Equals(
                ExtractAutonomyRootTopic(normalizedFrom),
                ExtractAutonomyRootTopic(normalizedTo),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lastSpecificContinuationSourceTopic = normalizedFrom;
        lastSpecificContinuationTargetTopic = normalizedTo;
        lastSpecificContinuationTime = Time.time;
    }

    private bool IsRecentSpecificContinuationTransition(string fromTopic, string toTopic, float windowMinutes = 90f)
    {
        if (string.IsNullOrWhiteSpace(lastSpecificContinuationSourceTopic) ||
            string.IsNullOrWhiteSpace(lastSpecificContinuationTargetTopic))
        {
            return false;
        }

        if (Time.time - lastSpecificContinuationTime > windowMinutes * 60f)
            return false;

        return string.Equals(
                   SanitizeAutonomyTopic(fromTopic),
                   lastSpecificContinuationSourceTopic,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   SanitizeAutonomyTopic(toTopic),
                   lastSpecificContinuationTargetTopic,
                   StringComparison.OrdinalIgnoreCase);
    }

    private float GetSpecificFamilyTopicVisitAgeMinutes(string topic)
    {
        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (string.IsNullOrWhiteSpace(normalizedTopic) ||
            !recentSpecificFamilyTopicVisits.TryGetValue(normalizedTopic, out float lastVisitTime))
        {
            return float.PositiveInfinity;
        }

        return Mathf.Max(0f, (Time.time - lastVisitTime) / 60f);
    }

    private bool WasSpecificFamilyTopicVisitedRecently(string topic, float windowMinutes = 45f)
    {
        return GetSpecificFamilyTopicVisitAgeMinutes(topic) <= windowMinutes;
    }

    private void RememberFamilyRestartTopic(string topic)
    {
        string normalizedTopic = SanitizeAutonomyTopic(topic);
        if (CountExpansionFragments(normalizedTopic) < 1)
            return;

        lastFamilyRestartTopic = normalizedTopic;
        lastFamilyRestartTime = Time.time;
    }

    private static bool IsAutonomyContinuationSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        return string.Equals(source, "thread-continuation", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "thread-restart", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "bootstrap-family", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "completed-thread-continuation", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "completed-thread-cycle", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "completed-thread-restart", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "completed-thread-last-chance", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(source, "completed-thread-bootstrap", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanQueueAutonomyFamilyTopic(string topic, string source)
    {
        if (!IsAutonomyContinuationSource(source))
            return true;

        string normalizedTopic = SanitizeAutonomyTopic(topic);
        string rootTopic = ExtractAutonomyRootTopic(topic);
        if (string.IsNullOrWhiteSpace(rootTopic))
            return true;

        float now = Time.time;
        if (string.IsNullOrWhiteSpace(lastAutonomyFamilyRoot) ||
            !string.Equals(lastAutonomyFamilyRoot, rootTopic, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (now - lastAutonomyFamilyAdvanceTime > AutonomyFamilyResetSeconds)
            return true;

        bool isSpecificSiblingShift =
            CountExpansionFragments(normalizedTopic) >= 1 &&
            !string.IsNullOrWhiteSpace(lastAutonomyFamilyTopic) &&
            string.Equals(lastAutonomyFamilyRoot, rootTopic, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lastAutonomyFamilyTopic, normalizedTopic, StringComparison.OrdinalIgnoreCase);

        if (isSpecificSiblingShift)
            return true;

        if (consecutiveAutonomyFamilyQueues < MaxConsecutiveAutonomyFamilyQueues)
            return true;

        return now - lastAutonomyFamilyAdvanceTime > AutonomyFamilyCooldownSeconds;
    }

    private void RememberAutonomyFamilyQueue(string topic, string source)
    {
        if (!IsAutonomyContinuationSource(source))
            return;

        string normalizedTopic = SanitizeAutonomyTopic(topic);
        string rootTopic = ExtractAutonomyRootTopic(normalizedTopic);
        if (string.IsNullOrWhiteSpace(rootTopic))
            return;

        float now = Time.time;
        if (!string.Equals(lastAutonomyFamilyRoot, rootTopic, StringComparison.OrdinalIgnoreCase) ||
            now - lastAutonomyFamilyAdvanceTime > AutonomyFamilyResetSeconds)
        {
            consecutiveAutonomyFamilyQueues = 1;
        }
        else if (!string.Equals(lastAutonomyFamilyTopic, normalizedTopic, StringComparison.OrdinalIgnoreCase))
        {
            consecutiveAutonomyFamilyQueues = 1;
        }
        else
        {
            consecutiveAutonomyFamilyQueues++;
        }

        lastAutonomyFamilyRoot = rootTopic;
        lastAutonomyFamilyTopic = normalizedTopic;
        lastAutonomyFamilyAdvanceTime = now;
    }

    private bool ShouldLogAutonomyFamilyCooldown(string rootTopic, float cooldownSeconds = 12f)
    {
        if (string.IsNullOrWhiteSpace(rootTopic))
            return false;

        if (!string.Equals(lastAutonomyFamilyCooldownLogRoot, rootTopic, StringComparison.OrdinalIgnoreCase) ||
            Time.time - lastAutonomyFamilyCooldownLogTime > cooldownSeconds)
        {
            lastAutonomyFamilyCooldownLogRoot = rootTopic;
            lastAutonomyFamilyCooldownLogTime = Time.time;
            return true;
        }

        return false;
    }

    private string GetAutonomyThreadRestartTopic(
        string sanitizedBase,
        double exactRecencyFloorMinutes,
        params string[] excludedTopics)
    {
        if (string.IsNullOrWhiteSpace(sanitizedBase))
            return string.Empty;

        string rootTopic = ExtractAutonomyRootTopic(sanitizedBase);
        if (string.IsNullOrWhiteSpace(rootTopic))
            return string.Empty;

        var normalizedExcludedTopics = new HashSet<string>(
            (excludedTopics ?? Array.Empty<string>())
                .Select(SanitizeAutonomyTopic)
                .Where(topic => !string.IsNullOrWhiteSpace(topic)),
            StringComparer.OrdinalIgnoreCase);

        string[] siblingSuffixes = GetAutonomySiblingSuffixes(rootTopic);
        var rankedCandidates = new List<(
            string Topic,
            bool WasRecentlyVisitedInFamily,
            int DiscoveryCount,
            int VisitCount,
            float FamilyVisitAgeMinutes,
            double RecencyMinutes)>();

        foreach (string suffix in siblingSuffixes)
        {
            string normalizedCandidate = SanitizeAutonomyTopic($"{rootTopic} {suffix}".Trim());
            if (string.Equals(normalizedCandidate, sanitizedBase, StringComparison.OrdinalIgnoreCase))
                continue;

            if (normalizedExcludedTopics.Contains(normalizedCandidate))
            {
                continue;
            }

            if (HasActiveExactThreadGoal(normalizedCandidate))
                continue;

            double exactRecencyMinutes = GetExactThreadRecencyMinutes(normalizedCandidate);
            if (exactRecencyMinutes <= exactRecencyFloorMinutes)
                continue;

            rankedCandidates.Add((
                normalizedCandidate,
                WasSpecificFamilyTopicVisitedRecently(normalizedCandidate, 60f),
                GetExactThreadGoalCount(normalizedCandidate, "concept_discovery"),
                GetExactThreadGoalCount(normalizedCandidate, "curiosity", "concept_discovery"),
                GetSpecificFamilyTopicVisitAgeMinutes(normalizedCandidate),
                exactRecencyMinutes));
        }

        if (rankedCandidates.Count == 0)
            return string.Empty;

        var bestCandidate = rankedCandidates
            .OrderBy(candidate => candidate.WasRecentlyVisitedInFamily)
            .ThenBy(candidate => candidate.DiscoveryCount)
            .ThenBy(candidate => candidate.VisitCount)
            .ThenByDescending(candidate => candidate.FamilyVisitAgeMinutes)
            .ThenByDescending(candidate => candidate.RecencyMinutes)
            .First();

        var topTierCandidates = rankedCandidates
            .Where(candidate => candidate.WasRecentlyVisitedInFamily == bestCandidate.WasRecentlyVisitedInFamily)
            .Where(candidate => candidate.DiscoveryCount == bestCandidate.DiscoveryCount)
            .Where(candidate => candidate.VisitCount == bestCandidate.VisitCount)
            .ToList();

        if (topTierCandidates.Count == 1)
            return topTierCandidates[0].Topic;

        float topFamilyVisitAge = topTierCandidates.Max(candidate => candidate.FamilyVisitAgeMinutes);
        var familyVisitTier = topTierCandidates
            .Where(candidate =>
                Mathf.Abs(candidate.FamilyVisitAgeMinutes - topFamilyVisitAge) < 0.01f ||
                (float.IsPositiveInfinity(candidate.FamilyVisitAgeMinutes) &&
                 float.IsPositiveInfinity(topFamilyVisitAge)) ||
                candidate.FamilyVisitAgeMinutes >= topFamilyVisitAge - 30f)
            .OrderByDescending(candidate => candidate.FamilyVisitAgeMinutes)
            .Take(Mathf.Min(3, topTierCandidates.Count))
            .ToList();

        if (familyVisitTier.Count == 1)
            return familyVisitTier[0].Topic;

        double topRecency = familyVisitTier.Max(candidate => candidate.RecencyMinutes);
        var recencyTier = familyVisitTier
            .Where(candidate =>
                Math.Abs(candidate.RecencyMinutes - topRecency) < 0.01d ||
                (double.IsPositiveInfinity(candidate.RecencyMinutes) &&
                 double.IsPositiveInfinity(topRecency)) ||
                candidate.RecencyMinutes >= topRecency - 20d)
            .OrderByDescending(candidate => candidate.RecencyMinutes)
            .Take(Mathf.Min(3, familyVisitTier.Count))
            .ToList();

        if (recencyTier.Count == 1)
            return recencyTier[0].Topic;

        return recencyTier[UnityEngine.Random.Range(0, recencyTier.Count)].Topic;
    }

    public ArTusGoal GetTopGoal()
    {
        if (activeGoals.Count == 0) return null;

        return activeGoals
            .Where(g => !g.completed && g.status != ArTusGoalStatus.Failed)
            .OrderByDescending(g => g.status == ArTusGoalStatus.Running ? 1 : 0)
            .ThenByDescending(g => g.confidence)
            .FirstOrDefault();
    }

    public bool HasActiveGoals() => activeGoals.Count > 0;

    private void FinalizeVerificationGoal(ArTusGoal goal)
    {
        if (goal == null)
            return;

        string requestedState = goal.evidenceState;

        string query = string.IsNullOrWhiteSpace(goal.triggerQuery)
            ? goal.focusTopic
            : goal.triggerQuery;

        if (string.IsNullOrWhiteSpace(query) || semanticSearch == null)
        {
            goal.executionSummary = "Verification completed, but semantic re-check was unavailable.";
            goal.evidenceState = "recheck_unavailable";
            RecordVerificationAudit(goal, query, requestedState, goal.evidenceState, 0f, 0, goal.executionSummary, goal.citations);
            return;
        }

        var reevaluated = semanticSearch.Query(query);
        if (reevaluated == null || !reevaluated.success)
        {
            goal.executionSummary = $"Verification completed, but re-check on '{query}' did not return a confident answer yet.";
            goal.evidenceState = "recheck_failed";
            RecordVerificationAudit(goal, query, requestedState, goal.evidenceState, 0f, 0, goal.executionSummary, goal.citations);

            core?.LogMemory(
                $"Verification re-check stayed inconclusive for '{query}'.",
                "GoalVerification",
                2,
                "unsure"
            );
            return;
        }

        goal.evidenceState = reevaluated.verificationState;
        goal.citations = reevaluated.citations != null
            ? reevaluated.citations.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList()
            : new List<string>();
        goal.executionSummary =
            $"Verification re-check for '{query}' -> {reevaluated.verificationState} " +
            $"(conf {reevaluated.confidence:F2}, sources {reevaluated.supportingEvidenceCount}).";
        RecordVerificationAudit(
            goal,
            query,
            requestedState,
            reevaluated.verificationState,
            reevaluated.confidence,
            reevaluated.supportingEvidenceCount,
            goal.executionSummary,
            goal.citations
        );

        string emotion = "thinking";
        if (reevaluated.verificationState == "verified")
            emotion = "reassured";
        else if (reevaluated.verificationState == "conflicted")
            emotion = "conflicted";
        else if (reevaluated.verificationState == "single_source")
            emotion = "curious";

        core?.LogMemory(
            $"Verification re-check: '{query}' is now {reevaluated.verificationState} with confidence {reevaluated.confidence:F2}.",
            "GoalVerification",
            3,
            emotion
        );

        if (reevaluated.verificationState == "verified")
        {
            string reinforceTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                ? reevaluated.topic
                : goal.focusTopic;

            if (!string.IsNullOrWhiteSpace(reinforceTopic))
                core?.ReinforceBelief(reinforceTopic, Mathf.Clamp(reevaluated.confidence * 0.1f, 0.03f, 0.12f));
        }
    }

    private void FinalizeConceptDiscoveryGoal(ArTusGoal goal)
    {
        if (goal == null || conceptDiscovery == null)
            return;

        string topic = string.IsNullOrWhiteSpace(goal.focusTopic)
            ? ExtractGoalTopic(goal.description)
            : goal.focusTopic;

        conceptDiscovery.MarkConceptPromoted(topic, goal.id);
        conceptDiscovery.RecordConceptOutcome(topic, "promoted");

        core?.LogMemory(
            $"Discovered concept promoted for '{topic}'.",
            "GoalConceptDiscovery",
            3,
            "curious"
        );
    }

    private void FinalizeShapeCurationGoal(ArTusGoal goal)
    {
        if (goal == null || shapeKnowledgeBridge == null)
            return;

        string topic = string.IsNullOrWhiteSpace(goal.focusTopic)
            ? ExtractGoalTopic(goal.description)
            : goal.focusTopic;

        string summary = string.IsNullOrWhiteSpace(goal.executionSummary)
            ? $"Shape curation completed for '{topic}'."
            : goal.executionSummary;

        shapeKnowledgeBridge.RecordShapeIngestionAudit(
            topic,
            string.IsNullOrWhiteSpace(goal.category) ? "shape_curation" : goal.category,
            "manifest-dashboard",
            null,
            null,
            null,
            "curation_completed",
            summary,
            Mathf.RoundToInt(Mathf.Clamp01(goal.confidence) * 100f),
            0f,
            goal.id
        );

        core?.LogMemory(
            $"Shape curation completed for '{topic}'.",
            "GoalShapeCuration",
            3,
            "focused"
        );
    }

    private void FinalizeShapeIngestionResilienceGoal(ArTusGoal goal)
    {
        if (goal == null || shapeKnowledgeBridge == null)
            return;

        string topic = string.IsNullOrWhiteSpace(goal.focusTopic)
            ? ExtractGoalTopic(goal.description)
            : goal.focusTopic;

        string summary = string.IsNullOrWhiteSpace(goal.executionSummary)
            ? $"Shape ingestion resilience cycle completed for '{topic}'."
            : goal.executionSummary;

        shapeKnowledgeBridge.RecordShapeIngestionAudit(
            topic,
            string.IsNullOrWhiteSpace(goal.category) ? "shape_ingestion_resilience" : goal.category,
            "ingestion-audit",
            null,
            null,
            null,
            "resilience_completed",
            summary,
            Mathf.RoundToInt(Mathf.Clamp01(goal.confidence) * 100f),
            0f,
            goal.id
        );

        core?.LogMemory(
            $"Shape ingestion resilience completed for '{topic}'.",
            "GoalShapeIngestion",
            3,
            "focused"
        );
    }

    private static string ExtractGoalTopic(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "systems thinking";

        string cleaned = description.Trim();
        string[] prefixes =
        {
            "Learn about ",
            "Reflect on ",
            "Explore ",
            "Investigate ",
            "Study ",
            "Analyze ",
            "Verify conflicting evidence about ",
            "Verify additional evidence for ",
            "Refine visual embodiment of ",
            "Curate source metadata for ",
            "Stabilize shape ingestion for "
        };

        foreach (string prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return cleaned.Substring(prefix.Length).Trim();
        }

        return cleaned;
    }

    private bool HasRecentVerificationGoal(string topic, string verificationState, double windowMinutes = 10d)
    {
        DateTime now = DateTime.Now;

        bool IsDuplicate(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "verification", StringComparison.OrdinalIgnoreCase))
                return false;

            bool sameTopic =
                string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ExtractGoalTopic(goal.description), topic, StringComparison.OrdinalIgnoreCase);

            if (!sameTopic)
                return false;

            if (!string.Equals(goal.evidenceState, verificationState, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        return activeGoals.Any(IsDuplicate) || completedGoals.Any(IsDuplicate);
    }

    private bool HasRecentCuriosityGoal(string topic, double windowMinutes = 20d)
    {
        DateTime now = DateTime.Now;

        bool IsDuplicate(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "curiosity", StringComparison.OrdinalIgnoreCase))
                return false;

            string goalTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                ? ExtractGoalTopic(goal.description)
                : goal.focusTopic;

            string normalizedGoalTopic = SanitizeAutonomyTopic(goalTopic);
            if (!string.Equals(normalizedGoalTopic, topic, StringComparison.OrdinalIgnoreCase) &&
                !IsSameOrBroaderConceptThread(topic, normalizedGoalTopic))
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        bool recentCuriosity = activeGoals.Any(IsDuplicate) || completedGoals.Any(IsDuplicate);
        if (recentCuriosity)
            return true;

        bool IsRecentDiscoveryInSameThread(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase))
                return false;

            string goalTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                ? ExtractGoalTopic(goal.description)
                : goal.focusTopic;

            string normalizedGoalTopic = SanitizeAutonomyTopic(goalTopic);
            if (!IsSameOrBroaderConceptThread(topic, normalizedGoalTopic))
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        return activeGoals.Any(IsRecentDiscoveryInSameThread) || completedGoals.Any(IsRecentDiscoveryInSameThread);
    }

    private static string SanitizeAutonomyTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "systems thinking";

        string sanitized = topic.Trim();
        string[] clippedMarkers =
        {
            " advanced theory",
            " real world examples",
            " related concepts",
            " applications",
            " advanced concepts",
            " theory",
            " systems"
        };

        string[] noisyPrefixes =
        {
            "Learn about ",
            "Reflect on ",
            "Executor ingested '",
            "Verification goal queued for '",
            "Shape refinement goal queued for '",
            "Shape curation goal queued for '",
            "Shape ingestion resilience goal queued for '",
            "Belief in '",
            "Selected shape: ",
            "Goal created: ",
            "Planned goal '"
        };

        foreach (string prefix in noisyPrefixes)
        {
            if (sanitized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized.Substring(prefix.Length).Trim();
                break;
            }
        }

        sanitized = sanitized
            .Replace("'", "")
            .Replace("\"", "")
            .Replace(":", " ")
            .Replace("→", " ")
            .Replace("—", " ");

        string lowered = sanitized.ToLowerInvariant();
        string[] noisyFragments =
        {
            "strengthened by",
            "from memory clarity",
            "goal created",
            "queued topic",
            "executor ingested",
            "planned goal",
            "knowledgerequest",
            "shapeintelligence",
            "beliefadjustment",
            "purpose:",
            "summary local",
            "local bridge synthesis",
            "data for topic",
            "openlibrary data",
            "ingested openlibrary",
            "promoted belief"
        };

        foreach (string fragment in noisyFragments)
        {
            int index = lowered.IndexOf(fragment, StringComparison.Ordinal);
            if (index > 0)
            {
                sanitized = sanitized.Substring(0, index).Trim();
                lowered = sanitized.ToLowerInvariant();
            }
        }

        while (sanitized.Contains("  "))
            sanitized = sanitized.Replace("  ", " ");

        int suffixMatches = 0;
        string loweredClipped = sanitized.ToLowerInvariant();
        foreach (string marker in clippedMarkers)
        {
            int searchFrom = 0;
            while (true)
            {
                int index = loweredClipped.IndexOf(marker, searchFrom, StringComparison.Ordinal);
                if (index < 0)
                    break;

                suffixMatches += 1;
                if (suffixMatches >= 2)
                {
                    sanitized = sanitized.Substring(0, index).Trim();
                    loweredClipped = sanitized.ToLowerInvariant();
                    break;
                }

                searchFrom = index + marker.Length;
            }

            if (suffixMatches >= 3)
                break;
        }

        if (string.IsNullOrWhiteSpace(sanitized))
            return "systems thinking";

        return sanitized;
    }

    private static int CountExpansionFragments(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return 0;

        string normalized = topic.ToLowerInvariant();
        string[] fragments =
        {
            " basics",
            " applications",
            " real world examples",
            " advanced concepts",
            " feedback loops",
            " leverage points",
            " system dynamics",
            " causal loop diagrams",
            " emergence",
            " advanced theory",
            " related concepts",
            " theory",
            " systems"
        };

        int count = 0;
        foreach (string fragment in fragments)
        {
            if (normalized.Contains(fragment))
                count += 1;
        }

        return count;
    }

    private bool HasRecentShapeRefinementGoal(string topic, double windowMinutes = 15d)
    {
        DateTime now = DateTime.Now;

        bool IsDuplicate(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "shape_refinement", StringComparison.OrdinalIgnoreCase))
                return false;

            bool sameTopic =
                string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ExtractGoalTopic(goal.description), topic, StringComparison.OrdinalIgnoreCase);

            if (!sameTopic)
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        return activeGoals.Any(IsDuplicate) || completedGoals.Any(IsDuplicate);
    }

    private bool HasRecentConceptDiscoveryGoal(string topic, double windowMinutes = 25d)
    {
        DateTime now = DateTime.Now;

        bool IsDuplicate(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase))
                return false;

            string goalTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                ? ExtractGoalTopic(goal.description)
                : goal.focusTopic;

            string normalizedGoalTopic = SanitizeAutonomyTopic(goalTopic);
            if (!string.Equals(normalizedGoalTopic, topic, StringComparison.OrdinalIgnoreCase) &&
                !IsSameOrBroaderConceptThread(topic, normalizedGoalTopic))
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        return activeGoals.Any(IsDuplicate) || completedGoals.Any(IsDuplicate);
    }

    private bool HasActiveExactThreadGoal(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalizedTopic = SanitizeAutonomyTopic(topic);
        return activeGoals.Any(goal =>
        {
            if (goal == null || goal.completed)
                return false;

            if (!string.Equals(goal.domain, "curiosity", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string goalTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                ? ExtractGoalTopic(goal.description)
                : goal.focusTopic;

            return string.Equals(
                SanitizeAutonomyTopic(goalTopic),
                normalizedTopic,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private double GetExactThreadRecencyMinutes(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return double.PositiveInfinity;

        string normalizedTopic = SanitizeAutonomyTopic(topic);
        DateTime now = DateTime.Now;

        IEnumerable<ArTusGoal> relevantGoals = activeGoals
            .Concat(completedGoals)
            .Where(goal =>
            {
                if (goal == null)
                    return false;

                if (!string.Equals(goal.domain, "curiosity", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string goalTopic = string.IsNullOrWhiteSpace(goal.focusTopic)
                    ? ExtractGoalTopic(goal.description)
                    : goal.focusTopic;

                return string.Equals(
                    SanitizeAutonomyTopic(goalTopic),
                    normalizedTopic,
                    StringComparison.OrdinalIgnoreCase);
            });

        DateTime? mostRecent = relevantGoals
            .Select(goal => DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt) ? updatedAt : DateTime.MinValue)
            .Where(updatedAt => updatedAt != DateTime.MinValue)
            .DefaultIfEmpty()
            .Max();

        if (!mostRecent.HasValue || mostRecent.Value == DateTime.MinValue)
            return double.PositiveInfinity;

        return (now - mostRecent.Value).TotalMinutes;
    }

    private static bool IsSameConceptThread(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        string left = SanitizeAutonomyTopic(a).ToLowerInvariant();
        string right = SanitizeAutonomyTopic(b).ToLowerInvariant();

        if (string.Equals(left, right, StringComparison.Ordinal))
            return true;

        if (left.StartsWith(right + " ", StringComparison.Ordinal) ||
            right.StartsWith(left + " ", StringComparison.Ordinal))
            return true;

        var leftTokens = left
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 4)
            .Distinct()
            .ToList();

        var rightTokens = right
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 4)
            .Distinct()
            .ToList();

        if (leftTokens.Count == 0 || rightTokens.Count == 0)
            return false;

        int overlap = leftTokens.Intersect(rightTokens).Count();
        return overlap >= 2;
    }

    private static bool IsSameOrBroaderConceptThread(string candidateTopic, string referenceTopic)
    {
        if (string.IsNullOrWhiteSpace(candidateTopic) || string.IsNullOrWhiteSpace(referenceTopic))
            return false;

        string candidate = SanitizeAutonomyTopic(candidateTopic).ToLowerInvariant();
        string reference = SanitizeAutonomyTopic(referenceTopic).ToLowerInvariant();

        if (string.Equals(candidate, reference, StringComparison.Ordinal))
            return true;

        if (reference.StartsWith(candidate + " ", StringComparison.Ordinal))
            return true;

        var candidateTokens = candidate
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 4)
            .Distinct()
            .ToList();

        var referenceTokens = reference
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 4)
            .Distinct()
            .ToList();

        if (candidateTokens.Count == 0 || referenceTokens.Count == 0)
            return false;

        return candidateTokens.All(referenceTokens.Contains);
    }

    private bool HasRecentShapeCurationGoal(string topic, double windowMinutes = 20d)
    {
        DateTime now = DateTime.Now;

        bool IsDuplicate(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "shape_curation", StringComparison.OrdinalIgnoreCase))
                return false;

            bool sameTopic =
                string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ExtractGoalTopic(goal.description), topic, StringComparison.OrdinalIgnoreCase);

            if (!sameTopic)
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        return activeGoals.Any(IsDuplicate) || completedGoals.Any(IsDuplicate);
    }

    private bool HasRecentShapeIngestionResilienceGoal(string topic, double windowMinutes = 20d)
    {
        DateTime now = DateTime.Now;

        bool IsDuplicate(ArTusGoal goal)
        {
            if (goal == null)
                return false;

            if (!string.Equals(goal.domain, "shape_ingestion_resilience", StringComparison.OrdinalIgnoreCase))
                return false;

            bool sameTopic =
                string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ExtractGoalTopic(goal.description), topic, StringComparison.OrdinalIgnoreCase);

            if (!sameTopic)
                return false;

            if (!DateTime.TryParse(goal.lastUpdatedAt, out DateTime updatedAt))
                return !goal.completed;

            return (now - updatedAt).TotalMinutes <= windowMinutes;
        }

        return activeGoals.Any(IsDuplicate) || completedGoals.Any(IsDuplicate);
    }

    public void DecayGoals(float rate)
    {
        foreach (var g in activeGoals)
            g.confidence = Mathf.Max(0f, g.confidence - rate);
    }

    public List<VerificationAuditEntry> GetVerificationAuditEntries()
    {
        return verificationAudit?.entries != null
            ? new List<VerificationAuditEntry>(verificationAudit.entries)
            : new List<VerificationAuditEntry>();
    }

    public List<string> GetHighPriorityVerificationTopics(int minimumAttempts = 2)
    {
        return (verificationAudit?.entries ?? new List<VerificationAuditEntry>())
            .Where(entry =>
                entry != null &&
                !string.IsNullOrWhiteSpace(entry.topic) &&
                !string.Equals(entry.finalState, "verified", StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.topic.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= minimumAttempts)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(entry => entry.completedAt))
            .Select(group => group.Key)
            .ToList();
    }

    public bool QueuePriorityVerificationTopic(string topic, float confidence = 0.8f)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        return TryQueueVerificationGoal(
            topic,
            "general",
            "unverified",
            topic,
            confidence
        );
    }

    public int ClearResolvedVerificationAudit()
    {
        if (verificationAudit == null || verificationAudit.entries == null)
            return 0;

        int before = verificationAudit.entries.Count;
        verificationAudit.entries = verificationAudit.entries
            .Where(entry =>
                entry != null &&
                !string.Equals(entry.finalState, "verified", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int removed = before - verificationAudit.entries.Count;
        if (removed > 0)
            PersistVerificationAudit();

        return removed;
    }

    private string GetPriorityVerificationTopic()
    {
        return GetHighPriorityVerificationTopics(2).FirstOrDefault(topic =>
            !activeGoals.Any(goal =>
                goal != null &&
                string.Equals(goal.domain, "verification", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase)));
    }

    private string GetPriorityDiscoveredConcept()
    {
        if (conceptDiscovery == null)
            return null;

        return conceptDiscovery
            .GetHighPriorityDiscoveredConcepts(5)
            .Where(entry =>
                entry != null &&
                entry.supportCount >= 2 &&
                (entry.evidence?.Count ?? 0) >= 1 &&
                (entry.supportingTopics?.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0) >= 2)
            .Select(entry => entry.concept)
            .FirstOrDefault(topic =>
                !activeGoals.Any(goal =>
                    goal != null &&
                    string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase)));
    }

    private string GetPriorityShapeRefinementTopic()
    {
        if (shapeKnowledgeBridge == null)
            return null;

        return shapeKnowledgeBridge
            .GetHighPriorityShapeRefinementTopics()
            .FirstOrDefault(topic =>
                !activeGoals.Any(goal =>
                    goal != null &&
                    string.Equals(goal.domain, "shape_refinement", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase)));
    }

    private string GetPriorityShapeCurationTopic()
    {
        if (shapeKnowledgeBridge == null)
            return null;

        return shapeKnowledgeBridge
            .GetHighPriorityMissingLicenseTopics()
            .FirstOrDefault(topic =>
                !activeGoals.Any(goal =>
                    goal != null &&
                    string.Equals(goal.domain, "shape_curation", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase)));
    }

    private string GetPriorityShapeIngestionRiskTopic()
    {
        if (shapeKnowledgeBridge == null)
            return null;

        return shapeKnowledgeBridge
            .GetHighPriorityIngestionRiskTopics()
            .FirstOrDefault(topic =>
                !activeGoals.Any(goal =>
                    goal != null &&
                    string.Equals(goal.domain, "shape_ingestion_resilience", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(goal.focusTopic, topic, StringComparison.OrdinalIgnoreCase)));
    }

    private void RecordVerificationAudit(
        ArTusGoal goal,
        string query,
        string requestedState,
        string finalState,
        float confidence,
        int supportingEvidenceCount,
        string summary,
        List<string> citations
    )
    {
        if (goal == null)
            return;

        if (verificationAudit == null)
            verificationAudit = new VerificationAuditWrapper();

        verificationAudit.entries.Add(new VerificationAuditEntry
        {
            goalId = goal.id,
            topic = string.IsNullOrWhiteSpace(goal.focusTopic) ? ExtractGoalTopic(goal.description) : goal.focusTopic,
            query = query,
            domain = string.IsNullOrWhiteSpace(goal.category) ? goal.domain : goal.category,
            requestedState = requestedState,
            finalState = finalState,
            confidence = confidence,
            supportingEvidenceCount = supportingEvidenceCount,
            citations = citations != null
                ? citations.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList()
                : new List<string>(),
            summary = summary,
            completedAt = DateTime.UtcNow.ToString("o")
        });

        PersistVerificationAudit();
    }

    private void LoadVerificationAudit()
    {
        try
        {
            string dir = Path.GetDirectoryName(verificationAuditPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(verificationAuditPath))
            {
                verificationAudit = new VerificationAuditWrapper();
                return;
            }

            string json = File.ReadAllText(verificationAuditPath);
            verificationAudit = JsonUtility.FromJson<VerificationAuditWrapper>(json)
                ?? new VerificationAuditWrapper();
        }
        catch (Exception ex)
        {
            verificationAudit = new VerificationAuditWrapper();
            Debug.LogWarning($"[GoalVerification] Audit load failed: {ex.Message}");
        }
    }

    private void PersistVerificationAudit()
    {
        try
        {
            string dir = Path.GetDirectoryName(verificationAuditPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(
                verificationAuditPath,
                JsonUtility.ToJson(verificationAudit ?? new VerificationAuditWrapper(), true)
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GoalVerification] Audit persist failed: {ex.Message}");
        }
    }
}
