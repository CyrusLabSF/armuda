using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ArTus.Data;

namespace ArTus.Data
{
    [Serializable]
    public class BeliefTrailEntry
    {
        public string topic;
        public string domain;
        public float confidence; // range: 0.0 to 1.0
        public string lastReinforced;
    }

    [Serializable]
    public class BeliefTrailWrapper
    {
        public List<BeliefTrailEntry> trails = new();
    }

    public class BeliefTrailManager
    {
        private string trailPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/BeliefTrail.json";
        private BeliefTrailWrapper wrapper = new();

        public BeliefTrailManager()
        {
            Load();
        }

        public void Reinforce(string topic, string domain, float delta = 0.1f)
        {
            var match = wrapper.trails.Find(t => t.topic == topic && t.domain == domain);
            if (match != null)
            {
                match.confidence = Mathf.Clamp(match.confidence + delta, 0f, 1f);
                match.lastReinforced = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                wrapper.trails.Add(new BeliefTrailEntry
                {
                    topic = topic,
                    domain = domain,
                    confidence = Mathf.Clamp(delta, 0f, 1f),
                    lastReinforced = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            Save();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(trailPath))
                {
                    string json = File.ReadAllText(trailPath);
                    wrapper = JsonUtility.FromJson<BeliefTrailWrapper>(json);
                    if (wrapper == null)
                        wrapper = new BeliefTrailWrapper();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BeliefTrailManager] ❌ Failed to load trail log: {ex.Message}");
                wrapper = new BeliefTrailWrapper();
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(trailPath));
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(trailPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BeliefTrailManager] ❌ Failed to save trail log: {ex.Message}");
            }
        }

        public void ExportToCSV(string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/BeliefTrail.csv")
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                List<string> lines = new()
                {
                    "Topic,Domain,Confidence,LastReinforced"
                };

                foreach (var b in wrapper.trails)
                {
                    lines.Add($"{b.topic},{b.domain},{b.confidence:F2},{b.lastReinforced}");
                }

                File.WriteAllLines(path, lines);
                Debug.Log($"[BeliefTrail Export] ✅ Exported to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BeliefTrail Export] ❌ Failed: {ex.Message}");
            }
        }
    }
}
