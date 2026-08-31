using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ArTusShapeIntelligence : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusCoreState core;
    public ArTusEmotionController emotion;
    public ArTusMorphController morph;

    [Header("Shape Library")]
    public List<ArTusShapeProfile> knownShapes = new List<ArTusShapeProfile>();

    [Header("Autonomy")]
    public bool enableAutonomousShapeSelection = true;
    public float shapeDecisionInterval = 18f;
    public float minConfidenceToReplicate = 0.3f;
    public bool preferKnowledgeContext = true;
    public float shapeRetentionBias = 0.5f;

    private float nextDecisionTime = 0f;
    private ArTusShapeProfile currentTargetShape;
    private string lastAppliedShapeId;
    private string currentKnowledgeTopic;
    private string currentKnowledgeDomain;
    private string currentVerificationState;

    private void Awake()
    {
        if (core == null) core = FindAnyObjectByType<ArTusCoreState>();
        if (emotion == null) emotion = FindAnyObjectByType<ArTusEmotionController>();
        if (morph == null) morph = FindAnyObjectByType<ArTusMorphController>();

        if (knownShapes.Count == 0)
            SeedDefaultGeometryLibrary();
    }

    private void Update()
    {
        if (!enableAutonomousShapeSelection) return;
        if (Time.time < nextDecisionTime) return;

        nextDecisionTime = Time.time + shapeDecisionInterval;

        currentTargetShape = ChooseAutonomousShape();
        if (currentTargetShape != null)
        {
            if (string.Equals(lastAppliedShapeId, currentTargetShape.shapeId, StringComparison.OrdinalIgnoreCase))
                return;

            morph?.ApplyShapeProfile(currentTargetShape);
            lastAppliedShapeId = currentTargetShape.shapeId;
            core?.LogMemory(
                $"Selected shape: {currentTargetShape.displayName}",
                "ShapeIntelligence",
                0.9f,
                "curious"
            );
        }
    }

    public void LearnShape(ArTusShapeProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.shapeId))
            return;

        if (profile.isKnowledgeDerived && !IsValidKnowledgeTopic(profile.learnedTopic))
            return;

        var existing = knownShapes.FirstOrDefault(s => s.shapeId == profile.shapeId);
        if (existing == null)
        {
            profile.timesLearned = 1;
            knownShapes.Add(profile);
        }
        else
        {
            existing.timesLearned++;
            existing.confidence = Mathf.Clamp01(Mathf.Max(existing.confidence, profile.confidence) + 0.05f);
            existing.reconstructionScore = Mathf.Max(existing.reconstructionScore, profile.reconstructionScore);

            if (profile.isKnowledgeDerived)
            {
                existing.isKnowledgeDerived = true;
                existing.learnedTopic = profile.learnedTopic;
                existing.learnedDomain = profile.learnedDomain;
                existing.sourceKnowledgeId = profile.sourceKnowledgeId;
                existing.verificationState = profile.verificationState;
                existing.symbolicMeaning = string.IsNullOrWhiteSpace(profile.symbolicMeaning)
                    ? existing.symbolicMeaning
                    : profile.symbolicMeaning;
                existing.sourceTags = profile.sourceTags != null
                    ? new List<string>(profile.sourceTags)
                    : new List<string>();
            }
        }
    }

    public void SetKnowledgeContext(string topic, string domain = null, string verificationState = null)
    {
        string normalizedTopic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
        currentKnowledgeTopic = IsValidKnowledgeTopic(normalizedTopic) ? normalizedTopic : null;
        currentKnowledgeDomain = string.IsNullOrWhiteSpace(domain) ? null : domain.Trim();
        currentVerificationState = string.IsNullOrWhiteSpace(verificationState)
            ? null
            : verificationState.Trim().ToLowerInvariant();
    }

    public void ClearKnowledgeContext()
    {
        currentKnowledgeTopic = null;
        currentKnowledgeDomain = null;
        currentVerificationState = null;
        lastAppliedShapeId = null;
    }

    public ArTusShapeProfile ChooseShapeForTopic(string topic, string domain = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return null;

        string normalizedTopic = topic.Trim();
        if (!IsValidKnowledgeTopic(normalizedTopic))
            return null;

        string normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? null
            : domain.Trim();

        return knownShapes
            .Where(shape =>
                shape != null &&
                shape.isKnowledgeDerived &&
                IsValidKnowledgeTopic(shape.learnedTopic) &&
                !string.IsNullOrWhiteSpace(shape.learnedTopic) &&
                string.Equals(shape.learnedTopic, normalizedTopic, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(normalizedDomain) ||
                 string.Equals(shape.learnedDomain, normalizedDomain, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(shape => shape.confidence + shape.reconstructionScore)
            .FirstOrDefault();
    }

    public ArTusShapeProfile ChooseAutonomousShape()
    {
        if (knownShapes.Count == 0) return null;

        if (preferKnowledgeContext && !string.IsNullOrWhiteSpace(currentKnowledgeTopic))
        {
            var contextualShape = ChooseShapeForTopic(currentKnowledgeTopic, currentKnowledgeDomain);
            if (contextualShape != null)
                return contextualShape;
        }

        string mood = emotion != null ? emotion.GetCurrentEmotionName().ToLower() : "thinking";

        float bestScore = float.MinValue;
        ArTusShapeProfile best = null;
        float currentScore = float.MinValue;

        foreach (var shape in knownShapes)
        {
            if (shape == null)
                continue;

            if (shape.isKnowledgeDerived && !IsValidKnowledgeTopic(shape.learnedTopic))
                continue;

            float score = ScoreShape(shape, mood);

            if (score > bestScore)
            {
                bestScore = score;
                best = shape;
            }

            if (currentTargetShape != null &&
                string.Equals(shape.shapeId, currentTargetShape.shapeId, StringComparison.OrdinalIgnoreCase))
            {
                currentScore = score;
            }
        }

        if (currentTargetShape != null && currentScore > float.MinValue && best != null)
        {
            if (currentScore + shapeRetentionBias >= bestScore)
                return currentTargetShape;
        }

        return best;
    }

    private float ScoreShape(ArTusShapeProfile shape, string mood)
    {
        float score = shape.confidence + shape.reconstructionScore;

        if (shape.isKnowledgeDerived)
        {
            score += 0.12f;

            if (!string.IsNullOrWhiteSpace(currentKnowledgeDomain) &&
                string.Equals(shape.learnedDomain, currentKnowledgeDomain, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.08f;
            }

            if (!string.IsNullOrWhiteSpace(currentVerificationState))
            {
                if (string.Equals(shape.verificationState, "verified", StringComparison.OrdinalIgnoreCase))
                    score += 0.12f;
                else if (string.Equals(shape.verificationState, "conflicted", StringComparison.OrdinalIgnoreCase))
                    score -= 0.05f;
            }
        }

        switch (mood)
        {
            case "curious":
                score += shape.emotionalAffinityCuriosity;
                score += shape.complexity * 0.35f;
                break;
            case "thinking":
                score += shape.emotionalAffinityThinking;
                score += shape.stability * 0.25f;
                break;
            case "conflict":
                score += shape.emotionalAffinityConflict;
                score += shape.twistStrength * 0.25f;
                break;
            case "calm":
                score += shape.emotionalAffinityCalm;
                break;
            case "joy":
                score += shape.emotionalAffinityJoy;
                break;
        }

        return score;
    }

    private static bool IsValidKnowledgeTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalized = topic.Trim().ToLowerInvariant();

        if (normalized.Length < 3)
            return false;

        if (normalized == "form" ||
            normalized == "applications" ||
            normalized == "topic topic experienced" ||
            normalized == "topic experienced applications" ||
            normalized == "experienced related" ||
            normalized == "emotionally leaned toward" ||
            normalized == "leaned toward" ||
            normalized == "leaned toward thinking" ||
            normalized == "leaned toward basics" ||
            normalized == "leaned advanced" ||
            normalized == "leaned" ||
            normalized == "domain autonomy" ||
            normalized == "applications domain autonomy" ||
            normalized == "domain autonomy applications" ||
            normalized == "concepts domain autonomy" ||
            normalized == "examples domain autonomy" ||
            normalized == "cycle" ||
            normalized == "cycle experienced" ||
            normalized == "cycle experienced events" ||
            normalized == "cycle experienced basics" ||
            normalized == "cycle real" ||
            normalized == "experienced events" ||
            normalized == "experienced events top" ||
            normalized == "experienced events top basics" ||
            normalized == "events top categories" ||
            normalized == "categories concept" ||
            normalized == "categories concept basics" ||
            normalized == "categories concept discovery" ||
            normalized == "categories related" ||
            normalized == "categories" ||
            normalized == "exploratory" ||
            normalized == "concept discovery weight" ||
            normalized == "concept discovery" ||
            normalized == "concept" ||
            normalized == "discovery weight emotionally" ||
            normalized == "discovery weight" ||
            normalized == "discovery" ||
            normalized == "discovery weight advanced" ||
            normalized == "reflective synthesis" ||
            normalized == "reflective synthesis updated" ||
            normalized == "openuv api" ||
            normalized == "helioviewer api" ||
            normalized == "us congress")
        {
            return false;
        }

        if (normalized.EndsWith(" form", StringComparison.Ordinal) ||
            normalized.Contains("procedural geometry seed", StringComparison.Ordinal) ||
            normalized.Contains("generated procedural geometry", StringComparison.Ordinal) ||
            normalized.Contains("generated 9 procedural geometry", StringComparison.Ordinal) ||
            normalized.Contains("procedural shape seed", StringComparison.Ordinal) ||
            normalized.StartsWith("reflective synthesis", StringComparison.Ordinal) ||
            normalized.StartsWith("topic ", StringComparison.Ordinal) ||
            normalized.StartsWith("web:{", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized.Contains("via route web", StringComparison.Ordinal) ||
            normalized.Contains("bridge knowledge update", StringComparison.Ordinal) ||
            normalized.Contains("topic topic experienced", StringComparison.Ordinal) ||
            normalized.Contains("topic experienced applications", StringComparison.Ordinal) ||
            normalized.Contains("experienced related", StringComparison.Ordinal) ||
            normalized.Contains("emotionally leaned toward", StringComparison.Ordinal) ||
            normalized.Contains("leaned toward", StringComparison.Ordinal) ||
            normalized.Contains("leaned advanced", StringComparison.Ordinal) ||
            normalized.Contains("topic cycle", StringComparison.Ordinal) ||
            normalized.Contains("cycle experienced", StringComparison.Ordinal) ||
            normalized.Contains("top categories", StringComparison.Ordinal) ||
            normalized.Contains("experienced events top", StringComparison.Ordinal))
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

        return true;
    }

    public void RecordReproductionResult(string shapeId, float quality)
    {
        var shape = knownShapes.FirstOrDefault(s => s.shapeId == shapeId);
        if (shape == null) return;

        shape.successfulReproductions++;
        shape.reconstructionScore = Mathf.Lerp(shape.reconstructionScore, quality, 0.35f);
        shape.confidence = Mathf.Clamp01(shape.confidence + quality * 0.05f);
    }

    public List<ArTusShapeProfile> GetKnownShapes()
    {
        return knownShapes != null
            ? new List<ArTusShapeProfile>(knownShapes)
            : new List<ArTusShapeProfile>();
    }

    public string GetCurrentKnowledgeTopic() => currentKnowledgeTopic;

    public string GetCurrentKnowledgeDomain() => currentKnowledgeDomain;

    public string GetCurrentVerificationState() => currentVerificationState;

    private void SeedDefaultGeometryLibrary()
    {
        knownShapes.Add(new ArTusShapeProfile
        {
            shapeId = "sphere",
            displayName = "Sphere",
            category = "Primitive",
            archetype = "sphere",
            symbolicMeaning = "Unity, totality, calm center",
            stability = 0.9f,
            complexity = 0.2f,
            confidence = 0.7f,
            pulseStrength = 0.2f,
            rippleStrength = 0.1f,
            twistStrength = 0.05f,
            emotionalAffinityCalm = 0.9f,
            emotionalAffinityThinking = 0.6f
        });

        knownShapes.Add(new ArTusShapeProfile
        {
            shapeId = "torus",
            displayName = "Torus",
            category = "Primitive",
            archetype = "torus",
            symbolicMeaning = "Circulation, thought loop, continuity",
            stability = 0.8f,
            complexity = 0.5f,
            confidence = 0.9f,
            pulseStrength = 0.4f,
            rippleStrength = 0.3f,
            orbitStrength = 0.6f,
            twistStrength = 0.2f,
            emotionalAffinityThinking = 0.9f,
            emotionalAffinityCuriosity = 0.7f
        });

        knownShapes.Add(new ArTusShapeProfile
        {
            shapeId = "helix",
            displayName = "Helix",
            category = "Advanced",
            archetype = "helix",
            symbolicMeaning = "Growth, ascent, structured curiosity",
            stability = 0.5f,
            complexity = 0.9f,
            confidence = 0.45f,
            pulseStrength = 0.3f,
            rippleStrength = 0.35f,
            twistStrength = 0.8f,
            emotionalAffinityCuriosity = 0.95f,
            emotionalAffinityConflict = 0.45f
        });
    }
}
