using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ArTusTypes;

/// <summary>
/// Semantic question answering via vector memory recall.
/// Upgraded to prefer evidence-backed knowledge and flag conflicts.
/// </summary>
public class ArTusSemanticSearch : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusVectorMemory vectorMemory;
    public ArTusCoreState coreState;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Confidence Control")]
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.4f;

    [Header("Behavior Settings")]
    public bool enableSpeech = false;

    public class SemanticResult
    {
        public bool success;
        public string answer;
        public float confidence;
        public string topic;
        public string sourceUrl;
        public string evidenceSummary;
        public bool isEvidenceBacked;
        public string domain;
        public string verificationState;
        public int supportingEvidenceCount;
        public List<string> citations;
    }

    public SemanticResult Query(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Fail("empty", 0f);

        if (vectorMemory == null)
        {
            Debug.LogWarning("[SemanticSearch] Vector memory not assigned.");
            return Fail("no_memory", 0f);
        }

        var matches = vectorMemory.GetTopMatches(question, 3);
        var result = (matches != null && matches.Count > 0) ? matches[0] : null;

        if (result == null || string.IsNullOrWhiteSpace(result.summary))
        {
            SafeLog($"No semantic match for: '{question}'", "unsure");
            return Fail("no_match", 0f);
        }

        var verification = VerifyEvidence(question, matches);
        float confidence = verification.adjustedConfidence;

        if (confidence < confidenceThreshold)
        {
            SafeLog($"Low-confidence match: '{question}' -> {confidence:F2}", "unsure");
            return Fail("low_confidence", confidence);
        }

        return new SemanticResult
        {
            success = true,
            answer = BuildAnswer(result, verification),
            confidence = confidence,
            topic = NormalizeTopic(result.topic),
            sourceUrl = result.sourceUrl,
            evidenceSummary = result.evidenceSummary,
            isEvidenceBacked = result.isEvidenceBacked,
            domain = result.domain,
            verificationState = verification.state,
            supportingEvidenceCount = verification.supportingEvidenceCount,
            citations = verification.citations
        };
    }

    public void AnswerQuestion(string question)
    {
        var result = Query(question);

        if (!result.success)
        {
            if (!betaMode && enableSpeech)
                coreState?.TriggerVoice("I'm still learning. I don't have a confident answer.");

            return;
        }

        if (!betaMode && enableSpeech)
            coreState?.TriggerVoice(result.answer);

        SafeLog(
            $"Semantic recall: '{result.topic}' ({result.verificationState}, conf: {result.confidence:F2})",
            "reflective"
        );
    }

    private VerificationResult VerifyEvidence(
        string question,
        List<ArTusVectorMemory.MatchResult> matches
    )
    {
        var evidenceBacked = (matches ?? new List<ArTusVectorMemory.MatchResult>())
            .Where(m => m != null && m.isEvidenceBacked)
            .Take(3)
            .ToList();

        if (evidenceBacked.Count < 2)
        {
            return new VerificationResult
            {
                state = evidenceBacked.Count == 1 ? "single_source" : "unverified",
                adjustedConfidence = matches != null && matches.Count > 0
                    ? Mathf.Clamp01(matches[0].confidence)
                    : 0f,
                supportingEvidenceCount = evidenceBacked.Count,
                citations = evidenceBacked
                    .Where(m => !string.IsNullOrWhiteSpace(m.sourceUrl))
                    .Select(m => m.sourceUrl)
                    .Distinct()
                    .ToList()
            };
        }

        var normalized = evidenceBacked
            .Select(m => NormalizeEvidence(m.evidenceSummary ?? m.summary))
            .ToList();

        bool conflict = false;
        for (int i = 0; i < normalized.Count; i++)
        {
            for (int j = i + 1; j < normalized.Count; j++)
            {
                if (ComputeTokenOverlap(normalized[i], normalized[j]) < 0.2f)
                {
                    conflict = true;
                    break;
                }
            }

            if (conflict)
                break;
        }

        if (conflict)
        {
            LogContradiction(question, evidenceBacked);

            return new VerificationResult
            {
                state = "conflicted",
                adjustedConfidence = Mathf.Clamp01((evidenceBacked[0].confidence + evidenceBacked[1].confidence) * 0.25f),
                supportingEvidenceCount = evidenceBacked.Count,
                citations = evidenceBacked
                    .Where(m => !string.IsNullOrWhiteSpace(m.sourceUrl))
                    .Select(m => m.sourceUrl)
                    .Distinct()
                    .ToList()
            };
        }

        float avgConfidence = evidenceBacked.Average(m => m.confidence);
        return new VerificationResult
        {
            state = "verified",
            adjustedConfidence = Mathf.Clamp01(avgConfidence + 0.08f),
            supportingEvidenceCount = evidenceBacked.Count,
            citations = evidenceBacked
                .Where(m => !string.IsNullOrWhiteSpace(m.sourceUrl))
                .Select(m => m.sourceUrl)
                .Distinct()
                .ToList()
        };
    }

    private void LogContradiction(string question, List<ArTusVectorMemory.MatchResult> evidenceBacked)
    {
        var contradictionLog = GetComponent<ContradictionLogManager>()
            ?? gameObject.AddComponent<ContradictionLogManager>();

        if (evidenceBacked == null || evidenceBacked.Count < 2)
            return;

        var entry = new ContradictionEntry(
            topic: question,
            domain: evidenceBacked[0].domain ?? "general",
            description: "Evidence-backed sources disagree on this query.",
            contentA: evidenceBacked[0].evidenceSummary ?? evidenceBacked[0].summary,
            contentB: evidenceBacked[1].evidenceSummary ?? evidenceBacked[1].summary,
            severity: 0.75f,
            confidenceScore: Mathf.Clamp01((evidenceBacked[0].confidence + evidenceBacked[1].confidence) * 0.5f),
            certaintyA: evidenceBacked[0].confidence,
            certaintyB: evidenceBacked[1].confidence,
            origin: "SemanticSearch",
            emotion: "conflicted"
        );

        contradictionLog.LogContradiction(entry);

        if (!betaMode)
        {
            coreState?.LogMemory(
                $"Evidence conflict detected for '{question}'.",
                "Contradiction",
                2,
                "conflicted"
            );
        }
    }

    private SemanticResult Fail(string reason, float confidence)
    {
        return new SemanticResult
        {
            success = false,
            answer = "",
            confidence = confidence,
            topic = reason,
            verificationState = "failed",
            supportingEvidenceCount = 0,
            citations = new List<string>()
        };
    }

    private void SafeLog(string message, string emotion)
    {
        if (!betaMode)
        {
            coreState?.LogMemory(
                message,
                "SemanticQA",
                1,
                emotion
            );
        }
    }

    private static string BuildAnswer(
        ArTusVectorMemory.MatchResult result,
        VerificationResult verification
    )
    {
        if (result == null)
            return string.Empty;

        string topic = NormalizeTopic(result.topic);
        string evidence = string.IsNullOrWhiteSpace(result.evidenceSummary)
            ? result.summary
            : result.evidenceSummary;

        if (verification.state == "conflicted")
            return $"I found conflicting evidence about {topic}. The sources do not fully agree yet.";

        if (result.isEvidenceBacked && !string.IsNullOrWhiteSpace(result.sourceUrl))
            return $"{topic}: {evidence} (source: {result.sourceUrl})";

        return string.IsNullOrWhiteSpace(evidence) ? topic : evidence;
    }

    private static string NormalizeEvidence(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return new string(
            input.ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .ToArray()
        );
    }

    private static float ComputeTokenOverlap(string a, string b)
    {
        var setA = a.Split(' ').Where(t => t.Length > 3).ToHashSet();
        var setB = b.Split(' ').Where(t => t.Length > 3).ToHashSet();

        if (setA.Count == 0 || setB.Count == 0)
            return 0f;

        int overlap = setA.Intersect(setB).Count();
        int baseline = Mathf.Max(setA.Count, setB.Count);
        return baseline == 0 ? 0f : (float)overlap / baseline;
    }

    private static string NormalizeTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "unknown";

        if (!topic.StartsWith("knowledge::", System.StringComparison.OrdinalIgnoreCase))
            return topic;

        string[] parts = topic.Split(new[] { "::" }, System.StringSplitOptions.None);
        return parts.Length >= 2 ? parts[1] : topic;
    }

    private class VerificationResult
    {
        public string state;
        public float adjustedConfidence;
        public int supportingEvidenceCount;
        public List<string> citations;
    }
}
