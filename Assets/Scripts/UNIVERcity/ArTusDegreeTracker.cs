using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public class ArTusDegreeTracker : MonoBehaviour
{
    private readonly string degreePath = "D:/ArTusCloud-Deployment/UNIVERcity/System/DegreeTrack_SCES.json";
    private readonly string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/DegreeProgress.csv";

    public DegreeProfile degree;

    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        LoadDegreeTrack();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
            if (!File.Exists(csvPath))
                File.WriteAllText(csvPath, "Timestamp,Domain,Completed,Progress%\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[DegreeTracker] Failed to initialize CSV: {ex.Message}");
        }
    }

    public void LoadDegreeTrack()
    {
        if (!File.Exists(degreePath))
        {
            Debug.LogWarning("[DegreeTracker] No degree profile found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(degreePath);
            degree = JsonUtility.FromJson<DegreeProfile>(json);
            Debug.Log("[DegreeTracker] Degree profile loaded.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DegreeTracker] Failed to load degree file: {ex.Message}");
        }
    }

    public void LogDomainCompletion(string domain)
    {
        if (degree == null || degree.completed.Contains(domain)) return;

        degree.completed.Add(domain);
        degree.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            File.WriteAllText(degreePath, JsonUtility.ToJson(degree, true));
        }
        catch (IOException ex)
        {
            Debug.LogError($"[DegreeTracker] Failed to save degree file: {ex.Message}");
        }

        float percent = GetProgressPercentage();
        core?.LogMemory($"🎓 Completed domain: {domain} — {percent:F1}% of degree completed.", "DegreeProgress", 2, "motivated", domain);
        speech?.Speak($"I’ve completed the domain {domain}. My progress is now {percent:F1}%.");

        try
        {
            File.AppendAllText(csvPath, $"{DateTime.Now},{domain},yes,{percent:F1}\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[DegreeTracker] Failed to append to progress log: {ex.Message}");
        }
    }

    public float GetProgressPercentage()
    {
        if (degree == null || degree.tracks.Count == 0) return 0f;
        return (float)degree.completed.Count / degree.tracks.Count * 100f;
    }

    public void ReportProgress()
    {
        float percent = GetProgressPercentage();
        string report = $"I have completed {degree.completed.Count} of {degree.tracks.Count} domains — {percent:F1}% of my degree is complete.";

        Debug.Log($"[DegreeTracker] {report}");
        speech?.Speak(report);
    }

    public List<string> GetRemainingDomains()
    {
        return degree.tracks.Where(t => !degree.completed.Contains(t)).ToList();
    }

    public string SuggestNextDomain()
    {
        var remaining = GetRemainingDomains();
        if (remaining.Count == 0) return "None — degree complete!";
        return remaining[0]; // Future: use confidence scores or trail density
    }

    [System.Serializable]
    public class DegreeProfile
    {
        public string degree;
        public List<string> tracks = new();
        public List<string> completed = new();
        public string version;
        public string lastUpdated;
    }
}
