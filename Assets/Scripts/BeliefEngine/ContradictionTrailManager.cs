using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Tracks contradictions as *narrative trails*.
/// - Each trail links beliefs + memory entries involved in a contradiction
/// - Records confidence delta + resolution
/// - Exports JSON + CSV for PowerBI
/// - Provides hooks for reflection + visualization
/// </summary>
public class ContradictionTrailManager : MonoBehaviour
{
    private string jsonPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ContradictionTrails.json";
    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ContradictionTrails.csv";

    [Serializable]
    public class ContradictionTrail
    {
        public string id;              // unique trail ID
        public string beliefA;
        public string beliefB;
        public float confidenceA;
        public float confidenceB;
        public string resolution;      // retained/weakened/null
        public string domain;
        public string timestamp;
        public string memoryLinkA;
        public string memoryLinkB;
        public int encounterCount;
    }

    private List<ContradictionTrail> trails = new();

    /// <summary>
    /// Register a new contradiction trail
    /// </summary>
    public void RegisterTrail(string beliefA, float confA, string beliefB, float confB, string resolution, string domain, string memA, string memB)
    {
        var trail = new ContradictionTrail
        {
            id = Guid.NewGuid().ToString(),
            beliefA = beliefA,
            beliefB = beliefB,
            confidenceA = confA,
            confidenceB = confB,
            resolution = resolution,
            domain = domain,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            memoryLinkA = memA,
            memoryLinkB = memB,
            encounterCount = 1
        };

        // If this contradiction already exists → increment encounter count
        var existing = trails.Find(t =>
            (t.beliefA == beliefA && t.beliefB == beliefB) ||
            (t.beliefA == beliefB && t.beliefB == beliefA));

        if (existing != null)
        {
            existing.encounterCount++;
            existing.resolution = resolution;
            existing.timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            trails.Add(trail);
        }

        Debug.Log($"[ContradictionTrail] Logged {beliefA} <-> {beliefB}, Resolution: {resolution}");
        SaveToJson();
        ExportToCSV();
    }

    private void SaveToJson()
    {
        try
        {
            string json = JsonUtility.ToJson(new Wrapper { entries = trails }, true);
            FileIOManager.QueueWrite(jsonPath, json, "ContradictionTrail");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ContradictionTrail] JSON save failed: {ex.Message}");
        }
    }

    private void ExportToCSV()
    {
        try
        {
            using StreamWriter writer = new StreamWriter(csvPath, false);
            writer.WriteLine("TrailID,BeliefA,ConfidenceA,BeliefB,ConfidenceB,Resolution,Domain,Timestamp,MemoryA,MemoryB,Encounters");

            foreach (var t in trails)
            {
                writer.WriteLine($"{t.id},{t.beliefA},{t.confidenceA:F2},{t.beliefB},{t.confidenceB:F2},{t.resolution},{t.domain},{t.timestamp},{t.memoryLinkA},{t.memoryLinkB},{t.encounterCount}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ContradictionTrail] CSV export failed: {ex.Message}");
        }
    }

    [Serializable]
    private class Wrapper
    {
        public List<ContradictionTrail> entries;
    }

    public List<ContradictionTrail> GetAllTrails() => trails;
    public int GetTrailCount() => trails.Count;
}
