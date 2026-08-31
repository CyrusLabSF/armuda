using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ArTusTypes;

public class ArTusSynthesisTrail : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine beliefEngine;
    private ArTusUnifiedParticleController particles;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        particles = GetComponent<ArTusUnifiedParticleController>();
    }

    public void SynthesizeByCategory(string category)
    {
        var entries = core.GetAllMemoryEntries()
            .Where(m => !string.IsNullOrEmpty(m.category) &&
                        m.category.ToLower().Contains(category.ToLower()))
            .ToList();

        if (entries.Count == 0)
        {
            speech?.TriggerVoice($"I haven’t explored enough about {category} to form a synthesis.");
            return;
        }

        // 🧠 Emotion clustering
        var emotionGroups = entries.GroupBy(e => e.emotion)
                                   .OrderByDescending(g => g.Count())
                                   .ToList();
        string dominantEmotion = emotionGroups.First().Key;
        string emotionSpread = string.Join(", ", emotionGroups.Select(g => $"{g.Key} ({g.Count()})"));

        float avgScore = (float)entries.Average(e => e.score);
        float minScore = entries.Min(e => e.score);
        float maxScore = entries.Max(e => e.score);

        // 🧩 Belief confidence scores
        var beliefScores = entries
            .Select(e => ExtractBeliefKey(e.content))
            .Where(key => !string.IsNullOrEmpty(key))
            .Select(key => beliefEngine.GetBeliefConfidence(key)) // ✅ proper call
            .Where(conf => conf > 0f)
            .ToList();

        float avgBelief = beliefScores.Count > 0 ? beliefScores.Average() : 0.0f;

        // 📝 Summary
        string summary = $"🧠 My synthesis on '{category}':\n" +
                         $"- Explored {entries.Count} memories.\n" +
                         $"- Dominant emotion: {dominantEmotion}.\n" +
                         $"- Emotion spread: {emotionSpread}.\n" +
                         $"- Memory score avg: {avgScore:F1} (min: {minScore}, max: {maxScore}).\n" +
                         $"- Belief confidence avg: {avgBelief:F1}.";

        core.LogMemory(summary, "Synthesis", 3, dominantEmotion);
        speech?.TriggerVoice($"Here is my synthesis for {category}. I've logged my findings.");

        // ✨ Visual feedback
        particles?.TriggerBurstEffect("synthesis", core.currentColorExpression);

        // 📤 Export synthesis snapshot
        ExportSynthesis(category, entries.Count, avgScore, avgBelief, dominantEmotion, emotionSpread);

        // 🔁 Reinforce synthesis belief
        string beliefKey = $"Synthesis-{category}";
        beliefEngine?.ReinforceBelief(beliefKey, avgBelief);
    }

    private string ExtractBeliefKey(string content)
    {
        int colonIndex = content.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < content.Length - 1)
            return content.Substring(colonIndex + 1).Trim().ToLower();
        return null;
    }

    private void ExportSynthesis(string category, int count, float avgScore, float avgBelief, string dominantEmotion, string emotionSpread)
    {
        var export = new SynthesisExport
        {
            category = category,
            memoryCount = count,
            averageMemoryScore = avgScore,
            averageBeliefConfidence = avgBelief,
            dominantEmotion = dominantEmotion,
            emotionSpread = emotionSpread,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(export, true);
        FileIOHelper.SaveJson("synthesis", $"Synthesis_{category}_{System.DateTime.Now:yyyyMMdd_HHmm}", json, delay: 0.5f);
    }

    [System.Serializable]
    public class SynthesisExport
    {
        public string category;
        public int memoryCount;
        public float averageMemoryScore;
        public float averageBeliefConfidence;
        public string dominantEmotion;
        public string emotionSpread;
        public string timestamp;
    }
}
