using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LearningTrailEntry
{
    public string trailName;
    public List<string> relatedMemoryContents = new();
    public string dominantEmotion = "neutral";
    public int strengthScore = 0;

    public float confidenceEstimate = 0.5f;
    public string creationTime;
    public string lastUpdated;

    public LearningTrailEntry(string name)
    {
        trailName = name;
        creationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        lastUpdated = creationTime;
    }

    // ➕ Add memory if new and valid
    public void AddMemory(string memory)
    {
        if (!string.IsNullOrWhiteSpace(memory) && !relatedMemoryContents.Contains(memory))
        {
            relatedMemoryContents.Add(memory);
            RecalculateStrength();
        }
    }

    // ♻️ Recalculate strength score based on memory count
    public void RecalculateStrength()
    {
        strengthScore = relatedMemoryContents.Count * 2;
        lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // ⬆️ Reinforce trail confidence over time
    public void Reinforce(float amount = 0.05f)
    {
        confidenceEstimate = Mathf.Clamp01(confidenceEstimate + amount);
        strengthScore += 1;
        lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // 🔁 Visual trigger (no external system attached yet)
    public void VisualPulseTrigger()
    {
        Debug.Log($"[Trail] Visual pulse: {trailName} | Strength={strengthScore} | Emotion={dominantEmotion}");
        // ❌ Armuda visuals are disabled during this phase
    }

    // 📤 Export format for Power BI / trail logging
    public string ToCSV()
    {
        return $"{trailName},{dominantEmotion},{strengthScore},{confidenceEstimate:F2},{relatedMemoryContents.Count},{creationTime},{lastUpdated}";
    }
}
