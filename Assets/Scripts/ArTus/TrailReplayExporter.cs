using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class TrailReplayExporter : MonoBehaviour
{
    private string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/TrailReplayLog.csv";

    public void ExportReplay(string belief, string emotion, float confidence, List<string> trails)
    {
        bool fileExists = File.Exists(exportPath);
        using (StreamWriter writer = new StreamWriter(exportPath, true))
        {
            if (!fileExists)
                writer.WriteLine("Belief,Emotion,Confidence,TrailCount,TrailList,Timestamp");

            string flatTrail = string.Join(" | ", trails);
            string line = $"{belief},{emotion},{confidence:F2},{trails.Count},{flatTrail},{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            writer.WriteLine(line);
        }

        Debug.Log($"[TrailReplayExporter] Logged replay of belief: {belief}");
    }
}
