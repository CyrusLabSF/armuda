using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ArTusTypes;

[Serializable]
public class ArTusCapabilityCategoryRecord
{
    public string key;
    public string label;
    public string baseline;
    public int maturityScore;
    public string rationale;
    public string evidence;
    public string nextUpgrade;
}

[Serializable]
public class ArTusCapabilityBaselineSnapshot
{
    public string generatedAt;
    public string activeContext;
    public int toolCount;
    public int connectedToolCount;
    public int deviceCount;
    public int writableDeviceCount;
    public int codeArtifactCount;
    public List<ArTusCapabilityCategoryRecord> categories = new();
    public List<string> growthPriorities = new();
}

public static class ArTusCapabilityBaseline
{
    public static ArTusCapabilityBaselineSnapshot BuildSnapshot(
        ArTusCoreState core,
        ArTusGoalController goalController,
        ArTusBeliefEngine beliefEngine,
        ArTusShapeKnowledgeBridge shapeKnowledgeBridge,
        ArTusActionPrioritizer actionPrioritizer,
        ArTusRecursiveModeler recursiveModeler,
        ArTusCertaintyModel certaintyModel,
        ArTusSelfCorrectionEngine selfCorrectionEngine,
        ArTusActionPlanner actionPlanner,
        ArTusToolRegistry toolRegistry,
        ArTusCodeIntelligence codeIntelligence,
        ArTusDeviceBridge deviceBridge,
        ArTusSelfModel selfModel,
        List<MemoryEntry> memories,
        List<ArTusGoal> goals,
        List<KnowledgeRecord> knowledgeRecords,
        List<DiscoveredConceptRecord> discoveredConcepts,
        List<ShapeKnowledgeRecord> shapeKnowledge,
        List<ArTusToolDefinition> toolDefinitions = null,
        List<ArTusDeviceDefinition> deviceDefinitions = null,
        List<ArTusCodeArtifact> codeKnowledgeArtifacts = null,
        bool hasIdentitySnapshot = false)
    {
        memories ??= new List<MemoryEntry>();
        goals ??= new List<ArTusGoal>();
        knowledgeRecords ??= new List<KnowledgeRecord>();
        discoveredConcepts ??= new List<DiscoveredConceptRecord>();
        shapeKnowledge ??= new List<ShapeKnowledgeRecord>();

        List<ArTusToolDefinition> tools = toolDefinitions ?? toolRegistry?.GetRegisteredTools() ?? new List<ArTusToolDefinition>();
        List<ArTusDeviceDefinition> devices = deviceDefinitions ?? deviceBridge?.GetDevices() ?? new List<ArTusDeviceDefinition>();
        List<ArTusCodeArtifact> codeArtifacts = codeKnowledgeArtifacts ?? codeIntelligence?.GetArtifacts() ?? new List<ArTusCodeArtifact>();

        int completedGoals = goals.Count(goal => goal != null && (goal.completed || goal.status == ArTusGoalStatus.Completed));
        int conceptGoals = goals.Count(goal =>
            goal != null &&
            (string.Equals(goal.domain, "concept_discovery", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(goal.domain, "curiosity", StringComparison.OrdinalIgnoreCase)));
        int externalKnowledgeEvents = memories.Count(memory =>
            memory != null &&
            (string.Equals(memory.category, "ExternalKnowledge", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(memory.category, "WebSocket", StringComparison.OrdinalIgnoreCase)));
        int reflectionEvents = memories.Count(memory =>
            memory != null &&
            (memory.category?.IndexOf("Reflection", StringComparison.OrdinalIgnoreCase) >= 0 ||
             memory.category?.IndexOf("Belief", StringComparison.OrdinalIgnoreCase) >= 0));
        int shapeEvents = memories.Count(memory =>
            memory != null &&
            string.Equals(memory.category, "ShapeIntelligence", StringComparison.OrdinalIgnoreCase));

        var snapshot = new ArTusCapabilityBaselineSnapshot
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            activeContext = goalController != null ? goalController.GetCurrentAutonomyContextTopic() : core?.lastIngestedTopic ?? string.Empty,
            toolCount = tools.Count,
            connectedToolCount = tools.Count(tool => tool.isConnected),
            deviceCount = devices.Count,
            writableDeviceCount = devices.Count(device => device.canWrite),
            codeArtifactCount = codeArtifacts.Count
        };

        AddCategory(snapshot, "perception_intake", "Perception & Intake",
            Score(externalKnowledgeEvents > 50, externalKnowledgeEvents > 10, externalKnowledgeEvents > 0),
            $"ArTus is consuming knowledge through bridge/websocket intake with {externalKnowledgeEvents} recent knowledge events.",
            $"Knowledge records: {knowledgeRecords.Count}",
            "Add source ranking, freshness scoring, and uncertainty-aware intake routing.");

        AddCategory(snapshot, "memory_persistence", "Memory & Persistence",
            Score(memories.Count > 250 && knowledgeRecords.Count > 1000, memories.Count > 100, memories.Count > 0),
            "ArTus persists memory, knowledge records, and discovered concepts across runs.",
            $"Memory: {memories.Count} | Knowledge: {knowledgeRecords.Count} | Concepts: {discoveredConcepts.Count}",
            "Introduce memory abstraction, summarization tiers, and retrieval policies.");

        AddCategory(snapshot, "belief_confidence", "Belief & Confidence",
            Score(beliefEngine != null && certaintyModel != null, beliefEngine != null, false),
            "Beliefs, confidence, reinforcement, and certainty estimation are present and active.",
            $"Beliefs: {beliefEngine?.GetBeliefCount() ?? 0}",
            "Add evidence-weighted contradiction handling and explicit trust decay.");

        AddCategory(snapshot, "goal_autonomy", "Goal & Autonomy",
            Score(goalController != null && completedGoals > 250, goalController != null && completedGoals > 50, goalController != null),
            "ArTus can generate, continue, deepen, and complete autonomous concept-thread goals.",
            $"Completed goals: {completedGoals} | Learning goals: {conceptGoals}",
            "Add multi-step planning, validation gates, and cross-domain switching policy.");

        AddCategory(snapshot, "learning_discovery", "Learning & Concept Discovery",
            Score(discoveredConcepts.Count >= 6, discoveredConcepts.Count >= 3, discoveredConcepts.Count > 0),
            "Concept discovery and promotion are functioning with persistent family learning.",
            $"Discovered concepts: {discoveredConcepts.Count}",
            "Improve novelty ranking, uncertainty reduction, and conclusive-result evaluation.");

        AddCategory(snapshot, "embodiment_expression", "Embodiment & Expression",
            Score(shapeKnowledgeBridge != null && shapeKnowledge.Count > 50 && shapeEvents > 25, shapeKnowledgeBridge != null, false),
            "ArTus expresses active concepts through morphing, speech, and emotional routing.",
            $"Shape knowledge: {shapeKnowledge.Count} | Shape events: {shapeEvents}",
            "Expand beyond face morphing into multi-modal embodied state shifts.");

        AddCategory(snapshot, "reflection_self_correction", "Reflection & Self-Correction",
            Score(reflectionEvents > 25 && selfCorrectionEngine != null, reflectionEvents > 10 || recursiveModeler != null, reflectionEvents > 0),
            "Reflection and self-correction loops exist, but remain lighter than autonomy and belief traversal.",
            $"Reflection events: {reflectionEvents}",
            "Add explicit error analysis, outcome review, and learning-policy updates.");

        AddCategory(snapshot, "planning_executive", "Planning & Executive Control",
            Score(actionPlanner != null && actionPrioritizer != null, actionPlanner != null || actionPrioritizer != null, false),
            "Planning scaffolds exist, but executive action selection is still early-stage.",
            $"Planner present: {BoolLabel(actionPlanner != null)} | Prioritizer present: {BoolLabel(actionPrioritizer != null)}",
            "Promote plans from advisory artifacts into validated execution policies.");

        AddCategory(snapshot, "tool_action_use", "Tool & Action Use",
            Score(tools.Count > 0 && tools.Any(tool => tool.canWrite), tools.Count > 0, false),
            "Tool registry support is now scaffolded, but operational tool use still needs live adapters and policy.",
            $"Tools: {tools.Count} | Connected: {tools.Count(tool => tool.isConnected)}",
            "Attach runtime adapters, permissions, and verification loops for real action execution.");

        AddCategory(snapshot, "code_intelligence", "Code Intelligence",
            Score(codeArtifacts.Count > 10, codeArtifacts.Count > 0 || codeIntelligence != null, false),
            "Code understanding is now scaffolded as a first-class category, but not yet populated as a live knowledge domain.",
            $"Code artifacts: {codeArtifacts.Count}",
            "Ingest source files, symbols, tests, and build outcomes as code-learning artifacts.");

        AddCategory(snapshot, "device_iot_control", "Device & IoT Control",
            Score(devices.Count > 0 && devices.Any(device => device.canWrite), devices.Count > 0 || deviceBridge != null, false),
            "Device control is architecturally scaffolded but not yet wired into live adapters.",
            $"Devices: {devices.Count} | Writable: {devices.Count(device => device.canWrite)}",
            "Add device schemas, action permissions, and observe-act-verify loops.");

        AddCategory(snapshot, "self_model_identity", "Self-Model & Identity",
            Score(selfModel != null || hasIdentitySnapshot, hasIdentitySnapshot, false),
            "A self-model category now exists, but identity growth is still mostly implicit in behavior and belief state.",
            $"Self-model present: {BoolLabel(selfModel != null || hasIdentitySnapshot)}",
            "Persist capability self-knowledge, growth focus, limits, and trusted tool preferences.");

        AddCategory(snapshot, "observability_analytics", "Observability & Analytics",
            Score(core != null && memories.Count > 0, true, false),
            "ArTus now exports runtime, belief, concept, graph, and shape analytics to Power BI.",
            $"Exports active with memory count {memories.Count}",
            "Add dashboards for capability growth, resource efficiency, and planner/tool outcomes.");

        AddCategory(snapshot, "resource_efficiency", "Resource & Efficiency Governance",
            Score(certaintyModel != null && knowledgeRecords.Count > 1000, certaintyModel != null, false),
            "ArTus is relatively lightweight, but efficiency is still inferred rather than actively optimized.",
            $"Knowledge records: {knowledgeRecords.Count} | Certainty model present: {BoolLabel(certaintyModel != null)}",
            "Track API cost, retrieval reuse, loop redundancy, and reward per learning action.");

        snapshot.growthPriorities = snapshot.categories
            .OrderBy(record => record.maturityScore)
            .Take(4)
            .Select(record => record.label)
            .ToList();

        return snapshot;
    }

    private static void AddCategory(
        ArTusCapabilityBaselineSnapshot snapshot,
        string key,
        string label,
        int maturityScore,
        string rationale,
        string evidence,
        string nextUpgrade)
    {
        snapshot.categories.Add(new ArTusCapabilityCategoryRecord
        {
            key = key,
            label = label,
            maturityScore = maturityScore,
            baseline = Grade(maturityScore),
            rationale = rationale,
            evidence = evidence,
            nextUpgrade = nextUpgrade
        });
    }

    private static int Score(bool strong, bool moderate, bool foundational)
    {
        if (strong) return 85;
        if (moderate) return 60;
        if (foundational) return 35;
        return 10;
    }

    private static string Grade(int score)
    {
        if (score >= 80) return "strong";
        if (score >= 55) return "moderate";
        if (score >= 30) return "foundational";
        return "missing";
    }

    private static string BoolLabel(bool value) => value ? "yes" : "no";
}
