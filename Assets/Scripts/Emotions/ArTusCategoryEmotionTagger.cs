using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class ArTusCategoryEmotionTagger : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [System.Serializable]
    public class CategoryEmotionProfile
    {
        public string category;
        public Dictionary<string, int> emotionCounts = new();
    }

    public List<CategoryEmotionProfile> categoryProfiles = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    // 🧠 Build category → emotion profiles from memory
    public void ScanCategoryEmotions()
    {
        var memories = core.GetAllMemoryEntries();
        categoryProfiles.Clear();

        var grouped = memories
            .Where(m => !string.IsNullOrWhiteSpace(m.content))
            .GroupBy(m => m.content.ToLower().Split(':')[0].Trim());

        foreach (var group in grouped)
        {
            var profile = new CategoryEmotionProfile { category = group.Key };

            foreach (var entry in group)
            {
                if (string.IsNullOrWhiteSpace(entry.emotion)) continue;

                if (!profile.emotionCounts.ContainsKey(entry.emotion))
                    profile.emotionCounts[entry.emotion] = 0;

                profile.emotionCounts[entry.emotion]++;
            }

            categoryProfiles.Add(profile);
        }

        Debug.Log($"[EmotionTagger] ✅ Scanned {categoryProfiles.Count} categories.");
        core?.LogMemory($"\ud83d\udcca Emotion map built with {categoryProfiles.Count} profiles.", "EmotionTrail", 1, "system");
    }

    // 🌟 Return the most emotionally active category overall
    public string GetFavoriteCategory()
    {
        var ranked = categoryProfiles
            .OrderByDescending(p => p.emotionCounts.Values.Sum())
            .FirstOrDefault();

        return ranked?.category ?? "unknown";
    }

    // 🏆 Return top category associated with a specific emotion
    public string GetTopCategoryForEmotion(string emotion)
    {
        return categoryProfiles
            .Where(p => p.emotionCounts.ContainsKey(emotion))
            .OrderByDescending(p => p.emotionCounts[emotion])
            .Select(p => p.category)
            .FirstOrDefault() ?? "none";
    }

    // 📄 Optional Power BI export with timestamped file
    public void ExportCategoryEmotionMap(string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/CategoryEmotionMap.csv")
    {
        List<string> lines = new() { "Category,Emotion,Count" };

        foreach (var profile in categoryProfiles)
        {
            foreach (var pair in profile.emotionCounts)
            {
                lines.Add($"{profile.category},{pair.Key},{pair.Value}");
            }
        }

        string datedPath = path.Replace(".csv", $"_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllLines(datedPath, lines);
        Debug.Log($"[EmotionTagger] ✅ Exported emotional map to: {datedPath}");
    }
}
