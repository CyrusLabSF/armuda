using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class BeliefContradictionExporter : MonoBehaviour
{
    public ArTusBeliefEngine beliefEngine;
    private string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/BeliefContradictionExport.csv";

    [ContextMenu("Export Contradictions to CSV")]
    public void ExportToCSV()
    {
        if (beliefEngine == null || beliefEngine.beliefs == null)
        {
            Debug.LogWarning("[Exporter] Belief engine or data is null. Aborting export.");
            return;
        }

        List<string> lines = new()
        {
            "Belief,Domain,Confidence,Emotion,Contradiction,Severity,EmotionMismatch,TrailID,LastUpdated"
        };

        int count = 0;

        foreach (var kvp in beliefEngine.beliefs)
        {
            var b = kvp.Value;
            if (b == null || !b.isFlaggedContradiction) continue;

            string trailID = string.IsNullOrEmpty(b.supportingTrail) ? "none" : b.supportingTrail;
            string emotionMismatch = b.emotionMismatchFlag ? "Yes" : "No";
            string severity = b.contradictionSeverity > 0 ? b.contradictionSeverity.ToString("0.00") : "N/A";

            string line = $"{Escape(b.belief)},{b.domain},{b.confidenceScore:0.00},{b.dominantEmotion},Yes,{severity},{emotionMismatch},{trailID},{b.lastUpdated}";
            lines.Add(line);
            count++;
        }

        try
        {
            File.WriteAllLines(path, lines);
            Debug.Log($"✅ Exported {count} contradictions to CSV: {path} @ {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[Exporter] Failed to write contradiction CSV: {ex.Message}");
        }
    }

    // ✅ Utility to clean CSV entries
    private string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
