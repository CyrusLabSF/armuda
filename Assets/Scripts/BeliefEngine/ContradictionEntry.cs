using System;
using UnityEngine;

namespace ArTusTypes
{
    [System.Serializable]
    public class ContradictionEntry
    {
        // 🔹 General fields
        public string topic;
        public string domain;
        public string origin;
        public string description;
        public string contentA;
        public string contentB;

        // 🔹 Emotional & confidence tracking
        public string emotion;
        public float severity;          // general severity score
        public float confidenceScore;   // overall confidence at detection
        public float certaintyA;
        public float certaintyB;

        // 🔹 Encounter stats
        public int conflictCount;
        public int encounterCount;
        public DateTime lastDetected;

        // 🔹 Resolution metadata (needed by CoreState.ResolveContradiction)
        public string timestamp;
        public string threadA;
        public string threadB;
        public string dominant;     // stronger belief kept
        public string resolution;   // decision taken
        public float severityScore; // scaled severity
        public bool resolved;

        // ✅ Default constructor
        public ContradictionEntry()
        {
            topic = "unspecified";
            domain = "general";
            origin = "Unknown";
            description = "";
            contentA = "";
            contentB = "";

            emotion = "neutral";
            severity = 0f;
            severityScore = 0f;
            confidenceScore = 0.5f;
            certaintyA = 0.5f;
            certaintyB = 0.5f;

            conflictCount = 0;
            encounterCount = 0;
            resolved = false;

            lastDetected = DateTime.Now;
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // ✅ Full constructor
        public ContradictionEntry(string topic,
                                  string domain = "general",
                                  string description = "",
                                  string contentA = "",
                                  string contentB = "",
                                  float severity = 1f,
                                  float confidenceScore = 0.5f,
                                  float certaintyA = 0.5f,
                                  float certaintyB = 0.5f,
                                  string origin = "Unknown",
                                  string emotion = "neutral")
        {
            this.topic = topic;
            this.domain = domain;
            this.description = description;
            this.contentA = contentA;
            this.contentB = contentB;

            this.severity = severity;
            this.confidenceScore = confidenceScore;
            this.certaintyA = certaintyA;
            this.certaintyB = certaintyB;

            this.origin = origin;
            this.emotion = emotion;

            this.conflictCount = 1;
            this.encounterCount = 1;
            this.resolved = false;

            this.lastDetected = DateTime.Now;
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // ✅ Utility methods
        public void IncrementConflict()
        {
            conflictCount++;
            lastDetected = DateTime.Now;
        }

        public void IncrementEncounter()
        {
            encounterCount++;
            lastDetected = DateTime.Now;
        }

        public void MarkResolved(string resolutionText, string dominantBelief, float severity)
        {
            resolution = resolutionText;
            dominant = dominantBelief;
            severityScore = severity;
            resolved = true;
            lastDetected = DateTime.Now;
        }

        // ✅ Safe date assignment
        public void SetLastDetected(string dateString)
        {
            if (DateTime.TryParse(dateString, out var parsed))
                lastDetected = parsed;
            else
                lastDetected = DateTime.Now;
        }
    }
}
