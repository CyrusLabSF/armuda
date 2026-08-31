using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ArTusTypes;
using System;

public class ArTusTrailBuilder : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine beliefEngine;
    private ArTusIngestor ingestor;

    public List<LearningTrailEntry> trails = new();
    public LearningTrailEntry currentGoalTrail;

    private Dictionary<string, TrailScore> trailScores = new();

    // ------------------------------------------------
    // UNITY LIFECYCLE
    // ------------------------------------------------
    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();

        ingestor = FindAnyObjectByType<ArTusIngestor>(); // ✅ FIXED

        // 🔥 Optional safety logs (recommended)
        if (core == null) Debug.LogWarning("[TrailBuilder] CoreState missing.");
        if (speech == null) Debug.LogWarning("[TrailBuilder] SpeechResponder missing.");
        if (beliefEngine == null) Debug.LogWarning("[TrailBuilder] BeliefEngine missing.");
        if (ingestor == null) Debug.LogWarning("[TrailBuilder] Ingestor missing.");
    }

    // ------------------------------------------------
    // TRAIL BUILDING
    // ------------------------------------------------

    public void BuildRecentTrail(string trailName = "Recent Trail")
    {
        var recent = core?.GetAllMemoryEntries()?.TakeLast(5).ToList();
        if (recent == null || recent.Count == 0) return;

        var trail = new LearningTrailEntry(trailName);

        foreach (var entry in recent)
        {
            string[] parts = entry.content.Split(':');
            if (parts.Length >= 2)
                trail.AddMemory(parts[1].Trim());
        }

        trail.dominantEmotion = recent
            .GroupBy(m => m.emotion)
            .OrderByDescending(g => g.Count())
            .First().Key;

        trail.RecalculateStrength();
        trails.Add(trail);

        speech?.RequestSpeak(
            $"I’ve created a new trail: {trail.trailName}. I felt mostly {trail.dominantEmotion} while learning."
        );

        core?.LogMemory(
            $"Trail built: {trail.trailName} ({trail.relatedMemoryContents.Count} items, emotion {trail.dominantEmotion})",
            "TrailBuild",
            2,
            trail.dominantEmotion
        );
    }

    // ------------------------------------------------
    // GOALS & CONTINUATION
    // ------------------------------------------------

    public void DeclareTrailGoal()
    {
        if (trails.Count == 0)
        {
            speech?.RequestSpeak("I don’t have any learning trails to commit to yet.");
            return;
        }

        var best = trails.OrderByDescending(t => t.relatedMemoryContents.Count).First();
        currentGoalTrail = best;

        speech?.RequestSpeak($"I have selected the trail '{best.trailName}' as my current learning goal.");
        core?.LogMemory($"Goalstacked Trail: {best.trailName}", "Goal", 3, "growing");
    }

    public void ContinueTrail()
    {
        var toContinue = GetTrailToContinue();
        if (toContinue == null)
        {
            speech?.RequestSpeak("I don’t have any trails to continue.");
            return;
        }

        speech?.RequestSpeak(
            $"I want to continue my trail on {toContinue.trailName}. It felt mostly {toContinue.dominantEmotion}."
        );

        string nextTopic = toContinue.relatedMemoryContents.LastOrDefault();
        if (string.IsNullOrWhiteSpace(nextTopic))
            return;

        // ✅ WebGL-safe ingestion
        ingestor?.IngestSmartTopic(nextTopic, "trail-continue", 0.6f);
    }

    public LearningTrailEntry GetTrailToContinue()
    {
        if (trails.Count == 0) return null;

        return trails
            .OrderByDescending(t =>
            {
                float emotionalWeight = t.dominantEmotion switch
                {
                    "curious" => 1.5f,
                    "growing" => 1.3f,
                    "joy" => 1.0f,
                    "sad" => 0.5f,
                    _ => 0.8f
                };
                return (5 - t.relatedMemoryContents.Count) * emotionalWeight;
            })
            .FirstOrDefault();
    }

    // ------------------------------------------------
    // REINFORCEMENT
    // ------------------------------------------------

    public void ReinforceTrail(
        string trailName,
        float beliefImpact,
        float emotionalWeight,
        string emotion = "neutral"
    )
    {
        if (!trailScores.ContainsKey(trailName))
        {
            trailScores[trailName] = new TrailScore
            {
                trailName = trailName,
                beliefImpact = beliefImpact,
                emotionalWeight = emotionalWeight,
                hitCount = 1,
                dominantEmotion = emotion,
                lastReinforced = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        else
        {
            var trail = trailScores[trailName];
            trail.beliefImpact += beliefImpact;
            trail.emotionalWeight += emotionalWeight;
            trail.hitCount++;
            trail.dominantEmotion = emotion;
            trail.lastReinforced = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        float score = trailScores[trailName].GetReinforcementScore();
        core?.LogMemory(
            $"🔁 Reinforced trail '{trailName}' | Score: {score:F2} | Emotion: {emotion}",
            "TrailReinforcement",
            2,
            emotion
        );
    }

    // ------------------------------------------------
    // EXPORTS
    // ------------------------------------------------

    public void ExportTrailScores()
    {
        var wrapper = new TrailScoreExport
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            trails = trailScores.Values.ToList()
        };

        string json = JsonUtility.ToJson(wrapper, true);

        string path = ArTusPathUtility.GetPersistent(
            $"UNIVERcity/Exports/TrailScores_{DateTime.Now:yyyyMMdd_HHmm}.json"
        );

        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, json);
            Debug.Log($"[TrailBuilder] Exported trail scores → {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TrailBuilder] Failed to export trail scores: {ex.Message}");
        }
    }

    // ------------------------------------------------
    // DATA STRUCTS
    // ------------------------------------------------

    [Serializable]
    public class TrailScore
    {
        public string trailName;
        public float beliefImpact;
        public float emotionalWeight;
        public int hitCount;
        public string lastReinforced;
        public string dominantEmotion;

        public float GetReinforcementScore()
        {
            float hitWeight = hitCount <= 5
                ? hitCount
                : 5 + (hitCount - 5) * 0.5f;

            return beliefImpact + emotionalWeight + hitWeight;
        }
    }

    [Serializable]
    public class TrailScoreExport
    {
        public string timestamp;
        public List<TrailScore> trails;
    }
}
