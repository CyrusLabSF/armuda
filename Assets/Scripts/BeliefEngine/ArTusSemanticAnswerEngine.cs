using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class ArTusSemanticAnswerEngine : MonoBehaviour
{
    private ArTusUNIVERcityIndexer indexer;
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusIngestor ingestor;

    void Awake()
    {
        indexer = GetComponent<ArTusUNIVERcityIndexer>();
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        ingestor = GetComponent<ArTusIngestor>();
    }

    // 🧠 Process a semantic query and respond from indexed memory
    public void Answer(string question)
    {
        if (indexer == null || core == null || string.IsNullOrWhiteSpace(question))
        {
            Debug.LogWarning("[SemanticAnswer] Missing components or question.");
            return;
        }

        string[] keywords = question.ToLower()
            .Split(new[] { ' ', ',', '?', '.', ':', ';' }, System.StringSplitOptions.RemoveEmptyEntries);

        var all = indexer.index;

        var matches = all
            .Where(i => keywords.Any(k =>
                i.topic.ToLower().Contains(k) || i.category.ToLower().Contains(k)))
            .OrderByDescending(i => i.score)
            .Take(3)
            .ToList();

        if (matches.Count == 0)
        {
            speech?.Speak("I don't yet have enough clarity to answer that. Let me learn more.");
            core?.LogMemory($"❌ Unable to answer question: '{question}' — insufficient index match.", "SemanticAnswer", 1, "uncertain");
            ingestor?.IngestTopic(question);
            return;
        }

        string response = "Based on what I’ve learned: ";
        List<string> fragments = new();

        foreach (var match in matches)
        {
            string evidence = string.IsNullOrWhiteSpace(match.evidenceSummary)
                ? match.topic
                : match.evidenceSummary;

            if (!string.IsNullOrWhiteSpace(match.sourceURL))
                fragments.Add($"‘{match.topic}’ suggests {evidence} (source: {match.sourceURL})");
            else
                fragments.Add($"‘{match.topic}’ suggests {evidence}");
        }

        response += string.Join("; ", fragments) + ".";
        string dominantEmotion = matches[0].emotion.ToLower();

        // 🎤 Speak and log
        GetComponent<ArTusEmotionController>()?.SetEmotionByName("joy");
        speech?.Speak(response);
        core?.LogMemory($"🔎 Answered semantic query:\nQ: {question}\nA: {response}", "SemanticAnswer", 2, dominantEmotion);

        // Belief + Reflection Hooks
        core?.AddOrUpdateBelief($"semantic_answer:{question}", response, "semantic", dominantEmotion);
        core?.QueueReflection($"semantic_query:{question}");

        // Power BI log
        string trailLog = $"{System.DateTime.Now},{question},{string.Join("|", matches.Select(m => m.topic))}\n";
        File.AppendAllText("D:/ArTusCloud-Deployment/UNIVERcity/SemanticTrail.csv", trailLog);
    }
}
