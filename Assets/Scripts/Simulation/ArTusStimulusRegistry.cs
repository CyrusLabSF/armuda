using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class ArTusStimulusRegistry : MonoBehaviour
{
    [Serializable]
    public class StimulusEntry
    {
        public string type;
        public string path;
        public string topic;
        public string category;
        public string source;
        public string timestamp;
        public string notes;
    }

    // -------------------- Mode --------------------
    [Header("Mode")]
    public bool betaMode = true;

    [Header("Limits")]
    public int maxStimuli = 500;
    public float ingestionCooldown = 1.0f;

    private float lastIngestTime;

    // -------------------- Storage --------------------
    private readonly List<StimulusEntry> stimuli = new();

    private string exportDir;
    private string csvLogPath;

    private ArTusCoreState core;

    // -------------------- Init --------------------
    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        exportDir = ArTusPathUtility.GetPersistent("UNIVERcity/Stimuli");
        csvLogPath = ArTusPathUtility.GetPersistent("UNIVERcity/Logs/StimulusRegistryLog.csv");

        try
        {
            Directory.CreateDirectory(exportDir);
            Directory.CreateDirectory(Path.GetDirectoryName(csvLogPath));

            if (!File.Exists(csvLogPath))
            {
                File.WriteAllText(
                    csvLogPath,
                    "Timestamp,Type,Topic,Category,Source,Path,Notes\n"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StimulusRegistry] Init failed: {ex.Message}");
        }
    }

    // -------------------- Public API --------------------
    public void RegisterStimulus(
        string type,
        string path,
        string topic,
        string category,
        string source,
        string notes = ""
    )
    {
        // -----------------------------
        // RATE LIMIT
        // -----------------------------
        if (Time.time - lastIngestTime < ingestionCooldown)
            return;

        lastIngestTime = Time.time;

        // -----------------------------
        // CAP SIZE
        // -----------------------------
        if (stimuli.Count >= maxStimuli)
        {
            stimuli.RemoveAt(0); // drop oldest
        }

        StimulusEntry entry = new StimulusEntry
        {
            type = type,
            path = path,
            topic = topic,
            category = category,
            source = source,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            notes = notes
        };

        stimuli.Add(entry);

        SaveToDisk(entry);
        AppendToCSV(entry);

        // -----------------------------
        // SAFE MEMORY LOGGING
        // -----------------------------
        if (!betaMode)
        {
            core?.LogMemory(
                $"📥 Received {type} stimulus for '{topic}'",
                "Stimulus",
                2,
                "curious",
                topic
            );
        }

        Debug.Log($"[StimulusRegistry] Registered {type} → {topic}");
    }

    // -------------------- Persistence --------------------
    private void SaveToDisk(StimulusEntry entry)
    {
        try
        {
            string safeTopic = entry.topic.Replace(" ", "_");
            string fileName = $"Stimulus_{safeTopic}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string fullPath = Path.Combine(exportDir, fileName);

            File.WriteAllText(fullPath, JsonUtility.ToJson(entry, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StimulusRegistry] JSON save failed: {ex.Message}");
        }
    }

    private void AppendToCSV(StimulusEntry entry)
    {
        try
        {
            string line =
                $"{entry.timestamp}," +
                $"{Sanitize(entry.type)}," +
                $"{Sanitize(entry.topic)}," +
                $"{Sanitize(entry.category)}," +
                $"{Sanitize(entry.source)}," +
                $"{Sanitize(entry.path)}," +
                $"{Sanitize(entry.notes)}\n";

            File.AppendAllText(csvLogPath, line);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StimulusRegistry] CSV append failed: {ex.Message}");
        }
    }

    private static string Sanitize(string input)
    {
        return string.IsNullOrEmpty(input)
            ? ""
            : input.Replace(",", " ").Replace("\n", " ");
    }

    // -------------------- Queries --------------------
    public List<StimulusEntry> GetAllStimuli() => stimuli;

    public List<StimulusEntry> GetStimuliByCategory(string category)
    {
        return stimuli.FindAll(s =>
            s.category.Equals(category, StringComparison.OrdinalIgnoreCase)
        );
    }

    // -------------------- Export --------------------
    public void ExportSummaryByCategory()
    {
        string summaryPath =
            ArTusPathUtility.GetPersistent("UNIVERcity/Logs/StimulusCategorySummary.csv");

        Dictionary<string, int> counts = new();

        foreach (var s in stimuli)
        {
            if (!counts.ContainsKey(s.category))
                counts[s.category] = 0;
            counts[s.category]++;
        }

        try
        {
            File.WriteAllText(summaryPath, "Category,Count\n");

            foreach (var kv in counts)
                File.AppendAllText(summaryPath, $"{kv.Key},{kv.Value}\n");

            Debug.Log("[StimulusRegistry] Category summary exported.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StimulusRegistry] Summary export failed: {ex.Message}");
        }
    }
}