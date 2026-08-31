using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArTusTypes
{
    [Serializable]
    public class MemoryEntry
    {
        // 📝 Core content
        public string content;
        public string category;
        public string emotion;

        // 📊 Scoring
        public float importance = 0.5f;   // primary weight (0..1 recommended)
        public float clarity = 1.0f;      // (0..1)
        public float score = 1.0f;        // general score (free-form)
        public float confidence = 1.0f;   // (0..1 or 0..10, your choice)

        // 🔁 Reinforcement
        public int reinforcementCount = 1;
        public float reinforcementStrength = 0.1f;
        public float decayRate = 0.01f;

        // 🧵 Linking & provenance
        public string threadID;
        public string trailID;
        public string conversationID;
        public string sourceType;
        public string originTrailID;
        public string sourceURL;

        // 🏷 Metadata
        public List<string> relatedBeliefs = new();
        public List<string> tags = new();

        // 🤖 Execution context
        public string action_origin;
        public string executed_by;
        public bool is_autonomous;
        public string reflection_entry_id;
        public string physical_result;

        // 🗣 Speaker (needed by other scripts)
        public string speaker = "system";

        // ⏱ Time
        public DateTime timestamp = DateTime.UtcNow;

        // --------------------
        // Backwards-compat aliases
        // --------------------

        // Some scripts expect "importanceScore"
        public float importanceScore
        {
            get => importance;
            set => importance = value;
        }

        // Some scripts expect "age"
        // (seconds since timestamp; change to TotalMinutes/Hours if you prefer)
        public float age
        {
            get
            {
                var dt = DateTime.UtcNow - timestamp;
                return (float)dt.TotalSeconds;
            }
        }

        // --------------------
        // Constructors
        // --------------------
        public MemoryEntry() { }

        public MemoryEntry(string content, float score, string emotion)
        {
            this.content = content;
            this.score = score;
            this.emotion = emotion;

            timestamp = DateTime.UtcNow;
            reinforcementCount = 1;
            reinforcementStrength = 0.1f;
            decayRate = 0.01f;
            clarity = 1.0f;
            importance = 0.5f;
        }
    }

    [Serializable]
    public class BeliefMemoryEntry
    {
        public string belief;
        public string topic;
        public float confidence;
        public string description;
        public string domain;
        public string origin;
        public string dominantEmotion;
        public string supportingTrail;
        public string timestamp;

        public BeliefMemoryEntry() { }

        public BeliefMemoryEntry(string belief, float confidence, string origin, string emotion, string trail, string domain)
        {
            this.belief = belief;
            this.topic = belief;
            this.confidence = confidence;
            this.origin = origin;
            this.dominantEmotion = emotion;
            this.supportingTrail = trail;
            this.domain = domain;
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            this.description = $"Belief derived from {origin} with {emotion} context.";
        }
    }

    [Serializable] public class BeliefMemoryWrapper { public List<BeliefMemoryEntry> entries = new(); }
    [Serializable] public class MemoryWrapper { public List<MemoryEntry> log = new(); }

    [Serializable]
    public class KnowledgeRecord
    {
        public string id;
        public string topic;
        public string domain;
        public string route;
        public string query;
        public string sourceUrl;
        public string sourceType;
        public string summary;
        public string rawPayload;
        public List<string> evidence = new();
        public List<string> tags = new();
        public float confidence;
        public string createdAt;
        public string trailID;

        public KnowledgeRecord()
        {
            id = Guid.NewGuid().ToString();
            createdAt = DateTime.UtcNow.ToString("o");
            sourceType = "external_knowledge";
        }
    }

    [Serializable]
    public class KnowledgeRecordWrapper
    {
        public List<KnowledgeRecord> entries = new();
    }

    [Serializable]
    public class DiscoveredConceptRecord
    {
        public string id;
        public string concept;
        public string domain;
        public string seedTopic;
        public List<string> supportingTopics = new();
        public List<string> evidence = new();
        public float noveltyScore;
        public int supportCount;
        public int promotedCount;
        public string status;
        public string lastGoalId;
        public string createdAt;
        public string updatedAt;
        public string lastPromotedAt;

        public DiscoveredConceptRecord()
        {
            id = Guid.NewGuid().ToString();
            status = "candidate";
            createdAt = DateTime.UtcNow.ToString("o");
            updatedAt = createdAt;
        }
    }

    [Serializable]
    public class DiscoveredConceptWrapper
    {
        public List<DiscoveredConceptRecord> entries = new();
    }

    [Serializable]
    public class VerificationAuditEntry
    {
        public string goalId;
        public string topic;
        public string query;
        public string domain;
        public string requestedState;
        public string finalState;
        public float confidence;
        public int supportingEvidenceCount;
        public List<string> citations = new();
        public string summary;
        public string completedAt;

        public VerificationAuditEntry()
        {
            completedAt = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class VerificationAuditWrapper
    {
        public List<VerificationAuditEntry> entries = new();
    }

    [Serializable]
    public class ShapeKnowledgeRecord
    {
        public string id;
        public string topic;
        public string domain;
        public string knowledgeRecordId;
        public string descriptorId;
        public string verificationState;
        public string sourceUrl;
        public string summary;
        public List<string> evidence = new();
        public List<string> tags = new();
        public float confidence;
        public string createdAt;
        public ArTusShapeProfile shapeProfile = new();

        public ShapeKnowledgeRecord()
        {
            id = Guid.NewGuid().ToString();
            createdAt = DateTime.UtcNow.ToString("o");
            verificationState = "unverified";
        }
    }

    [Serializable]
    public class ShapeKnowledgeWrapper
    {
        public List<ShapeKnowledgeRecord> entries = new();
    }

    [Serializable]
    public class ShapeFormDescriptor
    {
        public string descriptorId;
        public string topic;
        public string domain;
        public string archetype;
        public string symbolicMeaning;
        public string sourceKind;
        public string sourcePath;
        public string sourceSite;
        public string sourceLicense;
        public string attribution;
        public int importPriority = 50;
        public string notes;
        public Vector3 axisWeights = new Vector3(0.33f, 0.34f, 0.33f);
        public Vector3 baseScale = Vector3.one;
        public float stability = 0.5f;
        public float complexity = 0.5f;
        public float confidence = 0.5f;
        public float pulseStrength = 0.2f;
        public float rippleStrength = 0.2f;
        public float orbitStrength = 0.2f;
        public float twistStrength = 0.2f;
        public float taperStrength = 0.2f;
        public float emotionalAffinityCuriosity = 0.5f;
        public float emotionalAffinityThinking = 0.5f;
        public float emotionalAffinityConflict = 0.5f;
        public float emotionalAffinityCalm = 0.5f;
        public float emotionalAffinityJoy = 0.5f;
        public List<string> tags = new();
        public int refinementCount = 0;
        public float targetReconstructionScore = 0.75f;
        public float lastObservedScore = 0f;
        public string lastRefinedAt;

        public ShapeFormDescriptor()
        {
            descriptorId = Guid.NewGuid().ToString();
            sourceKind = "database";
            lastRefinedAt = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class ShapeFormDescriptorWrapper
    {
        public List<ShapeFormDescriptor> entries = new();
    }

    [Serializable]
    public class ShapeDatabaseRecord
    {
        public string topic;
        public string domain;
        public string archetype;
        public string symbolicMeaning;
        public string sourceKind;
        public string sourcePath;
        public string sourceSite;
        public string sourceLicense;
        public string attribution;
        public int importPriority = 50;
        public string notes;
        public string tags;
        public float axisX = 0.33f;
        public float axisY = 0.34f;
        public float axisZ = 0.33f;
        public float baseScaleX = 1f;
        public float baseScaleY = 1f;
        public float baseScaleZ = 1f;
        public float stability = 0.5f;
        public float complexity = 0.5f;
        public float confidence = 0.5f;
        public float pulseStrength = 0.2f;
        public float rippleStrength = 0.2f;
        public float orbitStrength = 0.2f;
        public float twistStrength = 0.2f;
        public float taperStrength = 0.2f;
        public float emotionalAffinityCuriosity = 0.5f;
        public float emotionalAffinityThinking = 0.5f;
        public float emotionalAffinityConflict = 0.5f;
        public float emotionalAffinityCalm = 0.5f;
        public float emotionalAffinityJoy = 0.5f;
    }

    [Serializable]
    public class ShapeDatabaseRecordWrapper
    {
        public List<ShapeDatabaseRecord> entries = new();
    }

    [Serializable]
    public class ShapeIngestionManifestEntry
    {
        public string fileName;
        public string relativePath;
        public string topic;
        public string domain;
        public string archetype;
        public string symbolicMeaning;
        public string sourceSite;
        public string sourceLicense;
        public string attribution;
        public int importPriority = 50;
        public string tags;
        public string notes;
        public bool enabled = true;
    }

    [Serializable]
    public class ShapeIngestionManifest
    {
        public List<ShapeIngestionManifestEntry> entries = new();
    }

    [Serializable]
    public class ShapeIngestionAuditEntry
    {
        public string topic;
        public string domain;
        public string sourceSite;
        public string sourceLicense;
        public string attribution;
        public string relativePath;
        public string status;
        public string summary;
        public int importPriority;
        public float reconstructionScore;
        public string goalId;
        public string recordedAt;

        public ShapeIngestionAuditEntry()
        {
            recordedAt = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class ShapeIngestionAuditWrapper
    {
        public List<ShapeIngestionAuditEntry> entries = new();
    }

    [Serializable]
    public class SimulationResult
    {
        public string topic;
        public string domain;
        public string hypothesis;
        public string outcome;
        public string emotion;
        public string timestamp;
        public List<string> supportingEvidence = new();
        public float adjustedConfidence;
        public float confidenceChange;
        public float confidence;
        public float clarityDelta;
        public float insightStrength;
        public bool contradictionResolved;
        public string sourceURL;
        public string originType;
        public List<string> insightTags = new();
        public string originBelief;
        public bool alteredBelief;
        public string resultSummary;
        public string result;
        public string simulationID;
        public string category;
        public string generatedAt;

        public SimulationResult()
        {
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            simulationID = Guid.NewGuid().ToString();
            generatedAt = Application.productName;
        }
    }

    [Serializable] public class SimulationResultWrapper { public List<SimulationResult> results = new(); }

    [Serializable]
    public class BeliefNode
    {
        public string topic;
        public string belief;
        public string description;
        public float confidence;
        public string origin;
        public string source;
        public string domain;
        public string emotion;
        public string dominantEmotion;
        public string inferredEmotion;
        public string trail;
        public List<string> relatedTrails = new();
        public string lastUpdated;
        public int contradictionCount;
        public string lastContradictionAt;
        public int reinforcementCount;
        public List<float> confidenceTrend = new();
        public List<string> emotionSpread = new();
        public List<string> associatedEmotions = new();
        public List<string> tags = new();

        public float confidenceScore
        {
            get => confidence;
            set => confidence = value;
        }

        public bool IsWeak => confidence < 0.3f || contradictionCount >= 5;

        public BeliefNode() { }

        public BeliefNode(string topic, float confidence = 0f, string emotion = "neutral")
        {
            this.topic = topic;
            this.belief = topic;
            this.confidence = confidence;
            this.dominantEmotion = emotion;
            this.emotion = emotion;
            this.description = "";
            this.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public void AdjustConfidence(float delta)
        {
            confidenceScore = Mathf.Clamp(confidenceScore + delta, 0f, 10f);
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            reinforcementCount++;
        }
    }

    [Serializable] public class BeliefNodeListWrapper { public List<BeliefNode> beliefs = new(); }

    // NOTE: These types are referenced but not defined in your snippet.
    // Keep them if they exist elsewhere in your project.
    [Serializable] public class ContradictionTrendWrapper { public List<ContradictionTrendEntry> trends = new(); }

    [Serializable]
    public class DriverEntry
    {
        public string source;
        public string signalType;
        public string value;
        public string timestamp;
        public string name;
        public string deviceName;
        public string version;
        public string date;
        public string weight;
        public bool IsOutdated => signalType == "driver" && value != null && value.ToLower().Contains("outdated");
    }

    [Serializable] public class InternalDriverListWrapper { public List<DriverEntry> drivers = new(); }

    // NOTE: referenced but not shown in your snippet
    [Serializable] public class KnowledgeNode { }
    [Serializable] public class ExternalKnowledgeWrapper { public List<KnowledgeNode> entries; }

    [Serializable]
    public class IoTReading
    {
        public string sensorID;
        public string dataType;
        public float value;
        public string unit;
    }

    [Serializable] public class IoTDataWrapper { public List<IoTReading> readings; }
}
