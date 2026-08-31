using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EmotionBiasedIngestor : MonoBehaviour
{
    private ArTusCategoryEmotionTagger tagger;
    private ArTusOpenSourceWideIngestor ingestor;
    private ArTusSpeechResponder speech;

    public List<string> targetEmotions = new() { "curious", "conflicted", "inspired" };

    void Start()
    {
        tagger = GetComponent<ArTusCategoryEmotionTagger>();
        ingestor = GetComponent<ArTusOpenSourceWideIngestor>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    [ContextMenu("Run Emotion-Biased Ingest")]
    public void RunBiasIngest()
    {
        tagger.ScanCategoryEmotions();

        var emotional = tagger.categoryProfiles
            .Where(p => p.emotionCounts.Any(e => targetEmotions.Contains(e.Key.ToLower())))
            .OrderByDescending(p => p.emotionCounts.Values.Sum())
            .Take(3);

        if (!emotional.Any())
        {
            speech?.TriggerVoice("I don't feel strongly about any category right now.");
            return;
        }

        foreach (var profile in emotional)
        {
            string cat = profile.category;
            string dom = profile.emotionCounts.OrderByDescending(e => e.Value).First().Key;

            ingestor?.IngestFromAllSources(cat);
            GetComponent<EmotionRingRenderer>()?.RenderEmotionRing(cat, dom);
        }
    }
}
