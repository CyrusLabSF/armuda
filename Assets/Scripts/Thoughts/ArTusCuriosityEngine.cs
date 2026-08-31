using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using ArTusTypes;
using ArTus.Data;

public class ArTusCuriosityEngine : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusKnowledgeConfidence knowledgeConfidence;
    private ArTusEmotionController emotionController;
    private ArTusSpeechResponder speech;

    private readonly Dictionary<string, float> curiosityCooldown = new();
    private readonly Dictionary<string, float> ingestionCooldown = new();
    private readonly List<string> curiosityTopics = new();
    private readonly HashSet<string> topicSet = new();

    [Header("Curiosity Settings")]
    public float curiosityThreshold = 0.4f;
    public int topCuriousTopics = 3;
    public float cooldownDecayTime = 180f;
    public float curiosityTickInterval = 45f;
    public bool allowSpokenCuriosity = true;

    [Header("Limits")]
    public int maxCuriosityTopics = 25;

    [Header("Autonomous Learning")]
    public bool enableAutoIngestion = true;
    public float ingestionChance = 0.6f;
    public float ingestionCooldownTime = 120f;

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        knowledgeConfidence = GetComponent<ArTusKnowledgeConfidence>();
        emotionController = GetComponent<ArTusEmotionController>();
        speech = GetComponent<ArTusSpeechResponder>();

        InvokeRepeating(nameof(CuriosityTick), curiosityTickInterval, curiosityTickInterval);
    }

    // =====================================================
    // 🔁 COMPATIBILITY (RESTORED)
    // =====================================================

    public void SpeakCuriosityFocus()
    {
        var topics = GetMostCuriousTopics();
        if (topics.Count == 0) return;

        string joined = string.Join(", ", topics);

        speech?.RequestSpeak(
            $"I feel most curious about: {joined}.",
            ArTusSpeechResponder.SpeechCategory.Reflection
        );

        core?.LogMemory(
            $"Curiosity focus spoken: {joined}",
            "CuriosityFocus",
            1,
            "curious"
        );
    }

    public List<IngestedTopic> ExtractCuriosityTopics(string summary, string domain)
    {
        var newTopics = new List<IngestedTopic>();
        var seen = new HashSet<string>();

        string[] tokens = summary.Split(
            new[] { ' ', ',', '.', ';', ':', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (string token in tokens)
        {
            string word = token.Trim().ToLower();

            if (word.Length <= 5 || !char.IsLetter(word[0]) || seen.Contains(word))
                continue;

            if (curiosityTopics.Count >= maxCuriosityTopics)
                break;

            string topic = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(word);

            if (curiosityCooldown.ContainsKey(topic))
                continue;

            seen.Add(word);

            AddTopic(topic, domain);

            newTopics.Add(new IngestedTopic
            {
                topic = topic,
                domain = domain,
                tags = new() { "emergent" },
                curiosityScore = 0.8f
            });

            if (newTopics.Count >= 3)
                break;
        }

        foreach (var topic in newTopics)
        {
            core?.LogMemory(
                $"Curiosity topic surfaced: {topic.topic}",
                "CuriosityDiscovery",
                2,
                "curious"
            );
        }

        return newTopics;
    }

    // =====================================================
    // CORE SYSTEM
    // =====================================================

    public void AddTopic(string topic, string domain)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        topic = topic.Trim().ToLower();

        if (!topicSet.Contains(topic))
        {
            if (curiosityTopics.Count >= maxCuriosityTopics)
            {
                topicSet.Remove(curiosityTopics[0]);
                curiosityTopics.RemoveAt(0);
            }

            curiosityTopics.Add(topic);
            topicSet.Add(topic);
        }

        curiosityCooldown[topic] = Time.time;

        core?.LogMemory(
            $"Curiosity topic injected: {topic} (Domain: {domain})",
            "CuriosityInjection",
            2,
            "curious"
        );

        emotionController?.AddPressure(
            ArTusEmotionController.EmotionState.curious,
            0.15f
        );
    }

    private void CuriosityTick()
    {
        DecayCooldowns();

        var topics = GetMostCuriousTopics();
        if (topics.Count == 0) return;

        GenerateCuriosityPrompts(topics);

        if (enableAutoIngestion && UnityEngine.Random.value < ingestionChance)
        {
            string selected = topics[UnityEngine.Random.Range(0, topics.Count)];

            if (ingestionCooldown.TryGetValue(selected, out float last))
            {
                if (Time.time - last < ingestionCooldownTime)
                    return;
            }

            ingestionCooldown[selected] = Time.time;

            core?.FetchExternalKnowledge("web", selected, "general");

            core?.LogMemory(
                $"Curiosity-driven learning triggered: {selected}",
                "CuriosityIngestion",
                2,
                "curious"
            );
        }

        if (allowSpokenCuriosity && UnityEngine.Random.value > 0.7f)
        {
            SpeakCuriosityFocus();
        }
    }

    public List<string> GetMostCuriousTopics()
    {
        if (beliefEngine == null || knowledgeConfidence == null)
            return new();

        var scored = new List<(string topic, float score)>();

        foreach (var topic in beliefEngine.beliefs.Keys.Take(100))
        {
            float belief = beliefEngine.GetBeliefConfidence(topic);
            float knowledge = knowledgeConfidence.GetConfidenceForTopic(topic);

            float cooldownPenalty = curiosityCooldown.ContainsKey(topic) ? 0.25f : 0f;

            float curiosityScore =
                (1f - knowledge) * 0.5f +
                (1f - Mathf.Clamp01(belief)) * 0.3f -
                cooldownPenalty;

            if (curiosityScore >= curiosityThreshold)
                scored.Add((topic, curiosityScore));
        }

        return scored
            .OrderByDescending(s => s.score)
            .Take(topCuriousTopics)
            .Select(s => s.topic)
            .ToList();
    }

    private void GenerateCuriosityPrompts(List<string> topics)
    {
        foreach (string topic in topics)
        {
            curiosityCooldown[topic] = Time.time;

            AddTopic(topic, "general");

            core?.LogMemory(
                $"What am I missing about {topic}?",
                "CuriosityPrompt",
                1,
                "curious"
            );
        }
    }

    public List<string> GetCuriosityTopics()
    {
        return new List<string>(curiosityTopics);
    }

    private void DecayCooldowns()
    {
        var expired = curiosityCooldown
            .Where(kv => Time.time - kv.Value > cooldownDecayTime)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
            curiosityCooldown.Remove(key);
    }
}