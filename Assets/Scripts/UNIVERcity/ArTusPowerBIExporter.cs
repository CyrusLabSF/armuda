using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ArTusTypes;

public class ArTusPowerBIExporter : MonoBehaviour
{
    [Header("References")]
    public ArTusCoreState core;
    public ArTusGoalController goalController;
    public ArTusBeliefEngine beliefEngine;
    public ArTusShapeKnowledgeBridge shapeKnowledgeBridge;
    public ArTusActionPrioritizer actionPrioritizer;
    public ArTusRecursiveModeler recursiveModeler;
    public ArTusCertaintyModel certaintyModel;
    public ArTusSelfCorrectionEngine selfCorrectionEngine;
    public ArTusActionPlanner actionPlanner;
    public ArTusToolRegistry toolRegistry;
    public ArTusCodeIntelligence codeIntelligence;
    public ArTusDeviceBridge deviceBridge;
    public ArTusSelfModel selfModel;

    [Header("Export")]
    public string outputRelativePath = "UNIVERcity/Exports/PowerBI";
    public bool autoExportOnStart = true;
    public float autoExportIntervalSeconds = 60f;
    public bool exportLegacyCsvs = true;
    public bool speakOnManualExport = false;
    public bool logMemoryOnExport = false;

    private float lastExportTime = -999f;
    private string OutputRootPath => ArTusPathUtility.GetPersistent(outputRelativePath);
    private string DiscoveryPath => ArTusPathUtility.GetPersistent("UNIVERcity/Knowledge/ConceptDiscovery/discovered_concepts.json");
    private string KnowledgePath => ArTusPathUtility.GetPersistent("UNIVERcity/Knowledge/External/knowledge_records.json");

    private static readonly Regex TopicPatterns = new Regex(
        "(?:topic:\\s*|for '|around |about |focus on |belief '\\s*|Queued topic:\\s*|Applied Shape Profile:\\s*|Requested external knowledge for '\\s*|Ingested topic '\\s*|Reinforced belief '\\s*)([^\\|\\n\\r']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RoutePattern = new Regex("\"route\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SummaryPattern = new Regex("\"summary\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StructuredTopicPattern = new Regex("\"topic\"\\s*:\\s*\"([^\"]+)\"|topic:\\s*([^\\|\\n\\r]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] InvalidAnalyticsFragments =
    {
        "externalknowledge",
        "reflection dominant emotion",
        "dominant emotion",
        "emotion thinking score",
        "emotion score",
        "fading",
        "belief ",
        "evidence ",
        "local bridge",
        "cycle progress summary",
        "api scheduler triggered",
        "this is how i see myself evolving",
        "preparing daily reflection",
        "summary updated",
        "knowledge ingested for thinking",
        "executor ingested",
        "weak belief audit",
        "connect thinking "
    };

    private void Start()
    {
        ResolveReferences();

        if (autoExportOnStart)
            ExportAllForPowerBI();
    }

    private void Update()
    {
        if (autoExportIntervalSeconds <= 0f)
            return;

        if (Time.time - lastExportTime < autoExportIntervalSeconds)
            return;

        ExportAllForPowerBI();
    }

    [ContextMenu("Export All For Power BI")]
    public void ExportAllForPowerBI()
    {
        ResolveReferences();

        if (core == null)
        {
            Debug.LogError("[PowerBIExporter] Core reference is missing.");
            return;
        }

        Directory.CreateDirectory(OutputRootPath);

        List<KnowledgeRecord> knowledgeRecords = LoadKnowledgeRecords();
        List<DiscoveredConceptRecord> discoveredConcepts = LoadDiscoveredConcepts();
        List<ArTusGoal> goals = CollectGoals();
        List<MemoryEntry> memories = core.memoryLog ?? new List<MemoryEntry>();
        List<BeliefNode> beliefs = core.beliefs?.Values?.Where(b => b != null).ToList() ?? new List<BeliefNode>();
        List<ArTusCoreState.CrossDomainLink> crossDomainLinks = core.crossDomainLinks ?? new List<ArTusCoreState.CrossDomainLink>();
        List<ArTusCoreState.EvolutionLogEntry> evolutionHistory = core.evolutionHistory ?? new List<ArTusCoreState.EvolutionLogEntry>();
        List<ShapeKnowledgeRecord> shapeKnowledge = shapeKnowledgeBridge != null
            ? shapeKnowledgeBridge.GetShapeKnowledgeEntries()
            : new List<ShapeKnowledgeRecord>();

        string activeContext = ResolveActiveContext();
        List<ArTusToolDefinition> tools = toolRegistry?.GetRegisteredTools() ?? BuildFallbackToolDefinitions(activeContext, memories, knowledgeRecords);
        List<ArTusDeviceDefinition> devices = deviceBridge?.GetDevices() ?? BuildFallbackDeviceDefinitions();
        List<ArTusCodeArtifact> codeArtifacts = codeIntelligence?.GetArtifacts() ?? BuildFallbackCodeArtifacts();
        ArTusIdentitySnapshot identitySnapshot = selfModel?.GetSnapshot() ?? BuildFallbackIdentitySnapshot(activeContext, codeArtifacts, tools);
        ArTusCapabilityBaselineSnapshot capabilityBaseline = ArTusCapabilityBaseline.BuildSnapshot(
            core,
            goalController,
            beliefEngine,
            shapeKnowledgeBridge,
            actionPrioritizer,
            recursiveModeler,
            certaintyModel,
            selfCorrectionEngine,
            actionPlanner,
            toolRegistry,
            codeIntelligence,
            deviceBridge,
            selfModel,
            memories,
            goals,
            knowledgeRecords,
            discoveredConcepts,
            shapeKnowledge,
            tools,
            devices,
            codeArtifacts,
            !string.IsNullOrWhiteSpace(identitySnapshot?.currentRole));

        if (exportLegacyCsvs)
        {
            ExportBeliefsCsv(Path.Combine(OutputRootPath, "Beliefs.csv"), beliefs);
            ExportCrossflowCsv(Path.Combine(OutputRootPath, "Crossflow.csv"), crossDomainLinks);
            ExportEvolutionLogCsv(Path.Combine(OutputRootPath, "EvolutionLogs.csv"), evolutionHistory);
        }

        ExportGoalsCsv(Path.Combine(OutputRootPath, "FactGoals.csv"), goals);
        ExportBeliefFactsCsv(Path.Combine(OutputRootPath, "FactBeliefs.csv"), beliefs, activeContext);
        ExportKnowledgeCsv(Path.Combine(OutputRootPath, "FactKnowledgeRecords.csv"), knowledgeRecords);
        ExportConceptsCsv(Path.Combine(OutputRootPath, "FactConcepts.csv"), discoveredConcepts);
        ExportLearningEventsCsv(Path.Combine(OutputRootPath, "FactLearningEvents.csv"), memories, activeContext);
        ExportFiveWCsv(Path.Combine(OutputRootPath, "FactFiveW.csv"), memories, activeContext);
        ExportCapabilitiesCsv(Path.Combine(OutputRootPath, "FactCapabilities.csv"), capabilityBaseline);
        ExportToolsCsv(Path.Combine(OutputRootPath, "FactTools.csv"), tools, devices);
        ExportCodeKnowledgeCsv(Path.Combine(OutputRootPath, "FactCodeKnowledge.csv"), codeArtifacts);
        ExportGraphCsvs(
            Path.Combine(OutputRootPath, "GraphNodes.csv"),
            Path.Combine(OutputRootPath, "GraphEdges.csv"),
            beliefs,
            discoveredConcepts,
            knowledgeRecords,
            goals,
            shapeKnowledge,
            crossDomainLinks);
        ExportDashboardSummary(Path.Combine(OutputRootPath, "DashboardSummary.json"), beliefs, goals, memories, knowledgeRecords, discoveredConcepts, shapeKnowledge, activeContext, tools.Count, devices.Count, codeArtifacts.Count);
        File.WriteAllText(Path.Combine(OutputRootPath, "CapabilityBaseline.json"), JsonUtility.ToJson(capabilityBaseline, true));
        File.WriteAllText(Path.Combine(OutputRootPath, "IdentitySnapshot.json"), JsonUtility.ToJson(identitySnapshot, true));

        shapeKnowledgeBridge?.ExportShapeDataForPowerBI();

        lastExportTime = Time.time;

        if (logMemoryOnExport)
            core.LogMemory("Power BI backend dataset refreshed.", "PowerBIExport", 2, "organized");

        if (speakOnManualExport)
            GetComponent<ArTusSpeechResponder>()?.Speak("I’ve finished preparing my analytics dataset for Power BI.");

        Debug.Log($"[PowerBIExporter] Power BI export package refreshed at {OutputRootPath}");
    }

    private void ResolveReferences()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
        if (goalController == null)
            goalController = GetComponent<ArTusGoalController>() ?? FindAnyObjectByType<ArTusGoalController>();
        if (beliefEngine == null)
            beliefEngine = GetComponent<ArTusBeliefEngine>() ?? FindAnyObjectByType<ArTusBeliefEngine>();
        if (shapeKnowledgeBridge == null)
            shapeKnowledgeBridge = GetComponent<ArTusShapeKnowledgeBridge>() ?? FindAnyObjectByType<ArTusShapeKnowledgeBridge>();
        if (actionPrioritizer == null)
            actionPrioritizer = GetComponent<ArTusActionPrioritizer>() ?? FindAnyObjectByType<ArTusActionPrioritizer>();
        if (recursiveModeler == null)
            recursiveModeler = GetComponent<ArTusRecursiveModeler>() ?? FindAnyObjectByType<ArTusRecursiveModeler>();
        if (certaintyModel == null)
            certaintyModel = GetComponent<ArTusCertaintyModel>() ?? FindAnyObjectByType<ArTusCertaintyModel>();
        if (selfCorrectionEngine == null)
            selfCorrectionEngine = GetComponent<ArTusSelfCorrectionEngine>() ?? FindAnyObjectByType<ArTusSelfCorrectionEngine>();
        if (actionPlanner == null)
            actionPlanner = GetComponent<ArTusActionPlanner>() ?? FindAnyObjectByType<ArTusActionPlanner>();
        if (toolRegistry == null)
            toolRegistry = GetComponent<ArTusToolRegistry>() ?? FindAnyObjectByType<ArTusToolRegistry>();
        if (codeIntelligence == null)
            codeIntelligence = GetComponent<ArTusCodeIntelligence>() ?? FindAnyObjectByType<ArTusCodeIntelligence>();
        if (deviceBridge == null)
            deviceBridge = GetComponent<ArTusDeviceBridge>() ?? FindAnyObjectByType<ArTusDeviceBridge>();
        if (selfModel == null)
            selfModel = GetComponent<ArTusSelfModel>() ?? FindAnyObjectByType<ArTusSelfModel>();
    }

    private List<ArTusGoal> CollectGoals()
    {
        var results = new List<ArTusGoal>();

        if (goalController == null)
            return results;

        if (goalController.activeGoals != null)
            results.AddRange(goalController.activeGoals.Where(goal => goal != null));
        if (goalController.completedGoals != null)
            results.AddRange(goalController.completedGoals.Where(goal => goal != null));

        return results
            .GroupBy(goal => string.IsNullOrWhiteSpace(goal.id) ? Guid.NewGuid().ToString("N") : goal.id)
            .Select(group => group.OrderByDescending(goal => SafeDate(goal.lastUpdatedAt, goal.createdAt)).First())
            .ToList();
    }

    private List<KnowledgeRecord> LoadKnowledgeRecords()
    {
        if (!File.Exists(KnowledgePath))
            return new List<KnowledgeRecord>();

        try
        {
            string json = File.ReadAllText(KnowledgePath);
            return JsonUtility.FromJson<KnowledgeRecordWrapper>(json)?.entries ?? new List<KnowledgeRecord>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PowerBIExporter] Failed to load knowledge records: {ex.Message}");
            return new List<KnowledgeRecord>();
        }
    }

    private List<DiscoveredConceptRecord> LoadDiscoveredConcepts()
    {
        if (!File.Exists(DiscoveryPath))
            return new List<DiscoveredConceptRecord>();

        try
        {
            string json = File.ReadAllText(DiscoveryPath);
            return JsonUtility.FromJson<DiscoveredConceptWrapper>(json)?.entries ?? new List<DiscoveredConceptRecord>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PowerBIExporter] Failed to load discovered concepts: {ex.Message}");
            return new List<DiscoveredConceptRecord>();
        }
    }

    private void ExportBeliefsCsv(string path, List<BeliefNode> beliefs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("belief,confidence,origin,emotion,lastUpdated,supportingTrail");

        foreach (BeliefNode belief in beliefs)
        {
            string topic = NormalizeTopic(belief.topic);
            if (!IsAnalyticsTopic(topic))
                continue;

            builder.AppendLine(string.Join(",",
                Csv(topic),
                F(belief.confidenceScore),
                Csv(belief.origin),
                Csv(ResolveEmotion(belief.dominantEmotion, belief.emotion)),
                Csv(belief.lastUpdated),
                Csv(string.Join("|", belief.relatedTrails ?? new List<string>()))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportCrossflowCsv(string path, List<ArTusCoreState.CrossDomainLink> links)
    {
        var builder = new StringBuilder();
        builder.AppendLine("sourceDomain,targetDomain,linkStrength,triggeredOn");

        foreach (ArTusCoreState.CrossDomainLink link in links.Where(link => link != null))
        {
            builder.AppendLine(string.Join(",",
                Csv(link.source),
                Csv(link.target),
                F(link.strength),
                Csv(link.timestamp)));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportEvolutionLogCsv(string path, List<ArTusCoreState.EvolutionLogEntry> history)
    {
        var builder = new StringBuilder();
        builder.AppendLine("belief,confidenceBefore,confidenceAfter,emotion,date");

        foreach (ArTusCoreState.EvolutionLogEntry entry in history.Where(entry => entry != null))
        {
            builder.AppendLine(string.Join(",",
                Csv(entry.belief),
                F(entry.before),
                F(entry.after),
                Csv(entry.emotion),
                Csv(entry.timestamp)));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportGoalsCsv(string path, List<ArTusGoal> goals)
    {
        var builder = new StringBuilder();
        builder.AppendLine("goalId,goalName,description,status,completed,priority,category,focusTopic,domain,source,emotionTag,confidence,createdAt,lastUpdatedAt,evidenceState,citationCount,executionSummary");

        foreach (ArTusGoal goal in goals.OrderByDescending(goal => SafeDate(goal.lastUpdatedAt, goal.createdAt)))
        {
            builder.AppendLine(string.Join(",",
                Csv(goal.id),
                Csv(goal.goalName),
                Csv(goal.description),
                Csv(goal.status.ToString()),
                goal.completed ? "1" : "0",
                goal.priority.ToString(CultureInfo.InvariantCulture),
                Csv(goal.category),
                Csv(NormalizeTopic(goal.focusTopic)),
                Csv(goal.domain),
                Csv(goal.source),
                Csv(goal.emotionTag),
                F(goal.confidence),
                Csv(goal.createdAt),
                Csv(goal.lastUpdatedAt),
                Csv(goal.evidenceState),
                (goal.citations?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                Csv(goal.executionSummary)));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportBeliefFactsCsv(string path, List<BeliefNode> beliefs, string activeContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("beliefTopic,confidence,origin,source,domain,emotion,lastUpdated,reinforcementCount,contradictionCount,isWeak,isAlignedWithActiveContext,activeContext,relatedTrails,tags");

        foreach (BeliefNode belief in beliefs.OrderByDescending(belief => belief.confidenceScore))
        {
            string topic = NormalizeTopic(belief.topic);
            if (!IsAnalyticsTopic(topic))
                continue;

            builder.AppendLine(string.Join(",",
                Csv(topic),
                F(belief.confidenceScore),
                Csv(belief.origin),
                Csv(belief.source),
                Csv(ResolveDomain(belief.domain, topic)),
                Csv(ResolveEmotion(belief.dominantEmotion, belief.emotion)),
                Csv(belief.lastUpdated),
                belief.reinforcementCount.ToString(CultureInfo.InvariantCulture),
                belief.contradictionCount.ToString(CultureInfo.InvariantCulture),
                belief.IsWeak ? "1" : "0",
                IsBeliefAligned(topic, activeContext) ? "1" : "0",
                Csv(activeContext),
                Csv(string.Join("|", belief.relatedTrails ?? new List<string>())),
                Csv(string.Join("|", belief.tags ?? new List<string>()))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportKnowledgeCsv(string path, List<KnowledgeRecord> knowledgeRecords)
    {
        var builder = new StringBuilder();
        builder.AppendLine("knowledgeId,topic,domain,route,sourceType,sourceUrl,confidence,createdAt,summary,evidenceCount,tags");

        foreach (KnowledgeRecord record in knowledgeRecords
                     .Where(record => record != null)
                     .OrderByDescending(record => SafeDate(record.createdAt)))
        {
            builder.AppendLine(string.Join(",",
                Csv(record.id),
                Csv(NormalizeTopic(record.topic)),
                Csv(ResolveDomain(record.domain, record.topic)),
                Csv(record.route),
                Csv(record.sourceType),
                Csv(record.sourceUrl),
                F(record.confidence),
                Csv(record.createdAt),
                Csv(NormalizeEvidenceText(record.summary)),
                (record.evidence?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                Csv(string.Join("|", record.tags ?? new List<string>()))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportConceptsCsv(string path, List<DiscoveredConceptRecord> concepts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("conceptId,concept,domain,seedTopic,noveltyScore,supportCount,promotedCount,status,lastGoalId,createdAt,updatedAt,lastPromotedAt,supportingTopicCount,evidenceCount,supportingTopics,evidence");

        foreach (DiscoveredConceptRecord concept in concepts
                     .Where(concept => concept != null)
                     .GroupBy(concept => NormalizeTopic(concept.concept), StringComparer.OrdinalIgnoreCase)
                     .Select(MergeConceptExportGroup)
                     .Where(concept => concept != null)
                     .OrderByDescending(concept => concept.promotedCount)
                     .ThenByDescending(concept => SafeDate(concept.updatedAt, concept.createdAt)))
        {
            string normalizedConcept = NormalizeTopic(concept.concept);
            if (!IsAnalyticsTopic(normalizedConcept))
                continue;

            List<string> supportingTopics = NormalizeTopicList(concept.supportingTopics);
            List<string> evidence = NormalizeEvidenceList(concept.evidence);
            if (supportingTopics.Count == 0 && evidence.Count == 0 && concept.promotedCount <= 0)
                continue;

            builder.AppendLine(string.Join(",",
                Csv(concept.id),
                Csv(normalizedConcept),
                Csv(ResolveDomain(concept.domain, normalizedConcept)),
                Csv(NormalizeTopic(concept.seedTopic)),
                F(concept.noveltyScore),
                concept.supportCount.ToString(CultureInfo.InvariantCulture),
                concept.promotedCount.ToString(CultureInfo.InvariantCulture),
                Csv(concept.status),
                Csv(concept.lastGoalId),
                Csv(concept.createdAt),
                Csv(concept.updatedAt),
                Csv(concept.lastPromotedAt),
                supportingTopics.Count.ToString(CultureInfo.InvariantCulture),
                evidence.Count.ToString(CultureInfo.InvariantCulture),
                Csv(string.Join("|", supportingTopics)),
                Csv(string.Join("|", evidence))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportLearningEventsCsv(string path, List<MemoryEntry> memories, string activeContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("eventId,timestamp,eventCategory,eventType,topic,domain,route,emotion,importance,confidence,who,why,activeContext,content");

        foreach (MemoryEntry entry in memories
                     .Where(entry => entry != null)
                     .OrderByDescending(entry => entry.timestamp))
        {
            string topic = ExtractTopic(entry.content);
            string domain = ResolveDomain(entry.category, topic);
            builder.AppendLine(string.Join(",",
                Csv(BuildEventId(entry)),
                Csv(entry.timestamp.ToString("o")),
                Csv(entry.category),
                Csv(InferEventType(entry)),
                Csv(topic),
                Csv(domain),
                Csv(ExtractRoute(entry.content, entry.sourceType)),
                Csv(entry.emotion),
                F(entry.importance),
                F(entry.confidence),
                Csv(ResolveWho(entry)),
                Csv(InferWhy(entry)),
                Csv(activeContext),
                Csv(NormalizeEventContent(entry.content))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportCapabilitiesCsv(string path, ArTusCapabilityBaselineSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("categoryKey,label,baseline,maturityScore,rationale,evidence,nextUpgrade");

        foreach (ArTusCapabilityCategoryRecord category in snapshot?.categories ?? new List<ArTusCapabilityCategoryRecord>())
        {
            builder.AppendLine(string.Join(",",
                Csv(category.key),
                Csv(category.label),
                Csv(category.baseline),
                category.maturityScore.ToString(CultureInfo.InvariantCulture),
                Csv(category.rationale),
                Csv(category.evidence),
                Csv(category.nextUpgrade)));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportToolsCsv(string path, List<ArTusToolDefinition> tools, List<ArTusDeviceDefinition> devices)
    {
        var builder = new StringBuilder();
        builder.AppendLine("entryType,id,label,categoryOrType,state,canRead,canWrite,trustOrCommandCount,locationOrEndpoint,capabilities");

        foreach (ArTusToolDefinition tool in tools ?? new List<ArTusToolDefinition>())
        {
            builder.AppendLine(string.Join(",",
                Csv("tool"),
                Csv(tool.toolId),
                Csv(tool.label),
                Csv(tool.category),
                Csv(tool.isConnected ? "connected" : "disconnected"),
                tool.canRead ? "1" : "0",
                tool.canWrite ? "1" : "0",
                F(tool.trustScore),
                Csv(tool.endpoint),
                Csv(string.Join("|", tool.capabilities ?? new List<string>()))));
        }

        foreach (ArTusDeviceDefinition device in devices ?? new List<ArTusDeviceDefinition>())
        {
            builder.AppendLine(string.Join(",",
                Csv("device"),
                Csv(device.deviceId),
                Csv(device.label),
                Csv(device.deviceType),
                Csv(device.state),
                device.canRead ? "1" : "0",
                device.canWrite ? "1" : "0",
                (device.supportedCommands?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                Csv(device.location),
                Csv(string.Join("|", device.supportedCommands ?? new List<string>()))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportCodeKnowledgeCsv(string path, List<ArTusCodeArtifact> codeArtifacts)
    {
        var builder = new StringBuilder();
        builder.AppendLine("artifactId,topic,language,path,summary,symbolCount,referenceCount,confidence,lastUpdatedAt");

        foreach (ArTusCodeArtifact artifact in codeArtifacts ?? new List<ArTusCodeArtifact>())
        {
            builder.AppendLine(string.Join(",",
                Csv(artifact.artifactId),
                Csv(artifact.topic),
                Csv(artifact.language),
                Csv(artifact.path),
                Csv(artifact.summary),
                artifact.symbolCount.ToString(CultureInfo.InvariantCulture),
                artifact.referenceCount.ToString(CultureInfo.InvariantCulture),
                F(artifact.confidence),
                Csv(artifact.lastUpdatedAt)));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportFiveWCsv(string path, List<MemoryEntry> memories, string activeContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("entryId,who,what,when,where,why,topic,category,emotion,importance,confidence,activeContext,content");

        foreach (MemoryEntry entry in memories
                     .Where(entry => entry != null)
                     .OrderByDescending(entry => entry.timestamp))
        {
            builder.AppendLine(string.Join(",",
                Csv(BuildEventId(entry)),
                Csv(ResolveWho(entry)),
                Csv(InferEventType(entry)),
                Csv(entry.timestamp.ToString("o")),
                Csv(ExtractRoute(entry.content, entry.sourceType)),
                Csv(InferWhy(entry)),
                Csv(ExtractTopic(entry.content)),
                Csv(entry.category),
                Csv(entry.emotion),
                F(entry.importance),
                F(entry.confidence),
                Csv(activeContext),
                Csv(NormalizeEventContent(entry.content))));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void ExportGraphCsvs(
        string nodesPath,
        string edgesPath,
        List<BeliefNode> beliefs,
        List<DiscoveredConceptRecord> concepts,
        List<KnowledgeRecord> knowledgeRecords,
        List<ArTusGoal> goals,
        List<ShapeKnowledgeRecord> shapeKnowledge,
        List<ArTusCoreState.CrossDomainLink> crossDomainLinks)
    {
        var nodeBuilder = new StringBuilder();
        var edgeBuilder = new StringBuilder();
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        nodeBuilder.AppendLine("nodeId,label,nodeType,domain,status,confidence,lastUpdated,isActive");
        edgeBuilder.AppendLine("sourceId,targetId,edgeType,weight,context,timestamp");

        void AddNode(string id, string label, string type, string domain, string status, float confidence, string lastUpdated, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(id) || !nodeIds.Add(id))
                return;

            nodeBuilder.AppendLine(string.Join(",",
                Csv(id),
                Csv(label),
                Csv(type),
                Csv(domain),
                Csv(status),
                F(confidence),
                Csv(lastUpdated),
                isActive ? "1" : "0"));
        }

        void AddEdge(string source, string target, string type, float weight, string context, string timestamp)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                return;

            string key = $"{source}|{target}|{type}";
            if (!edgeKeys.Add(key))
                return;

            edgeBuilder.AppendLine(string.Join(",",
                Csv(source),
                Csv(target),
                Csv(type),
                F(weight),
                Csv(context),
                Csv(timestamp)));
        }

        string activeContext = ResolveActiveContext();
        AddNode("artus", "ArTus", "agent", "self", "active", 1f, DateTime.UtcNow.ToString("o"), true);

        foreach (BeliefNode belief in beliefs.Where(belief => belief != null))
        {
            string topic = NormalizeTopic(belief.topic);
            if (!IsAnalyticsTopic(topic))
                continue;

            AddNode(topic, topic, "belief", ResolveDomain(belief.domain, topic), belief.IsWeak ? "weak" : "active", belief.confidenceScore, belief.lastUpdated, IsBeliefAligned(topic, activeContext));
            AddEdge("artus", topic, "holds_belief", Mathf.Clamp01(belief.confidenceScore / 10f), ResolveEmotion(belief.dominantEmotion, belief.emotion), belief.lastUpdated);
        }

        foreach (DiscoveredConceptRecord concept in concepts.Where(concept => concept != null))
        {
            string conceptTopic = NormalizeTopic(concept.concept);
            if (!IsAnalyticsTopic(conceptTopic))
                continue;

            AddNode(conceptTopic, conceptTopic, "concept", ResolveDomain(concept.domain, conceptTopic), concept.status, concept.noveltyScore, concept.updatedAt, string.Equals(conceptTopic, activeContext, StringComparison.OrdinalIgnoreCase));

            foreach (string supportTopic in NormalizeTopicList(concept.supportingTopics))
            {
                if (!IsAnalyticsTopic(supportTopic))
                    continue;
                AddNode(supportTopic, supportTopic, "topic", ResolveDomain(concept.domain, supportTopic), "support", concept.noveltyScore, concept.updatedAt, false);
                AddEdge(supportTopic, conceptTopic, "supports_concept", Mathf.Max(0.1f, concept.supportCount / 10f), concept.seedTopic, concept.updatedAt);
            }
        }

        foreach (KnowledgeRecord knowledge in knowledgeRecords.Where(knowledge => knowledge != null))
        {
            string topic = NormalizeTopic(knowledge.topic);
            if (!IsAnalyticsTopic(topic))
                continue;
            AddNode(topic, topic, "knowledge_topic", ResolveDomain(knowledge.domain, topic), "ingested", knowledge.confidence, knowledge.createdAt, false);
            AddEdge("artus", topic, "learned_about", knowledge.confidence, knowledge.route, knowledge.createdAt);
        }

        foreach (ShapeKnowledgeRecord shape in shapeKnowledge.Where(shape => shape != null && shape.shapeProfile != null))
        {
            string topic = NormalizeTopic(shape.topic);
            if (!IsAnalyticsTopic(topic))
                continue;
            string shapeId = string.IsNullOrWhiteSpace(shape.shapeProfile.shapeId)
                ? $"shape::{NormalizeTopic(shape.shapeProfile.displayName)}"
                : shape.shapeProfile.shapeId;

            AddNode(shapeId, shape.shapeProfile.displayName, "shape", ResolveDomain(shape.domain, topic), shape.verificationState, shape.confidence, shape.createdAt, string.Equals(topic, activeContext, StringComparison.OrdinalIgnoreCase));
            AddNode(topic, topic, "shape_topic", ResolveDomain(shape.domain, topic), "shape_linked", shape.confidence, shape.createdAt, false);
            AddEdge(topic, shapeId, "embodied_as", shape.shapeProfile.reconstructionScore, shape.shapeProfile.archetype, shape.createdAt);
        }

        ArTusGoal priorGoal = null;
        foreach (ArTusGoal goal in goals
                     .Where(goal => goal != null && !string.IsNullOrWhiteSpace(goal.focusTopic))
                     .OrderBy(goal => SafeDate(goal.createdAt, goal.lastUpdatedAt)))
        {
            string topic = NormalizeTopic(goal.focusTopic);
            if (!IsAnalyticsTopic(topic))
                continue;
            string goalNodeId = $"goal::{goal.id}";
            AddNode(goalNodeId, goal.description, "goal", ResolveDomain(goal.domain, topic), goal.status.ToString(), goal.confidence, goal.lastUpdatedAt, !goal.completed);
            AddNode(topic, topic, "goal_topic", ResolveDomain(goal.domain, topic), goal.status.ToString(), goal.confidence, goal.lastUpdatedAt, false);
            AddEdge(goalNodeId, topic, "targets_topic", Mathf.Clamp01(goal.confidence), goal.category, goal.lastUpdatedAt);

            if (priorGoal != null && !string.IsNullOrWhiteSpace(priorGoal.focusTopic))
            {
                AddEdge(
                    NormalizeTopic(priorGoal.focusTopic),
                    topic,
                    "goal_transition",
                    1f,
                    priorGoal.category,
                    goal.lastUpdatedAt);
            }

            priorGoal = goal;
        }

        foreach (ArTusCoreState.CrossDomainLink link in crossDomainLinks.Where(link => link != null))
        {
            string source = NormalizeTopic(link.source);
            string target = NormalizeTopic(link.target);
            if (!IsAnalyticsTopic(source) || !IsAnalyticsTopic(target))
                continue;
            AddNode(source, source, "domain_link", ResolveDomain(null, source), "linked", link.strength, link.timestamp, false);
            AddNode(target, target, "domain_link", ResolveDomain(null, target), "linked", link.strength, link.timestamp, false);
            AddEdge(source, target, "cross_domain", link.strength, "crossflow", link.timestamp);
        }

        File.WriteAllText(nodesPath, nodeBuilder.ToString());
        File.WriteAllText(edgesPath, edgeBuilder.ToString());
    }

    private void ExportDashboardSummary(
        string path,
        List<BeliefNode> beliefs,
        List<ArTusGoal> goals,
        List<MemoryEntry> memories,
        List<KnowledgeRecord> knowledgeRecords,
        List<DiscoveredConceptRecord> concepts,
        List<ShapeKnowledgeRecord> shapeKnowledge,
        string activeContext,
        int toolCount,
        int deviceCount,
        int codeArtifactCount)
    {
        var summary = new PowerBISummary
        {
            generatedAt = DateTime.UtcNow.ToString("o"),
            activeContext = activeContext,
            memoryCount = memories.Count,
            beliefCount = beliefs.Count,
            knowledgeRecordCount = knowledgeRecords.Count,
            discoveredConceptCount = concepts
                .Where(concept => concept != null && IsAnalyticsTopic(NormalizeTopic(concept.concept)))
                .Select(concept => NormalizeTopic(concept.concept))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            activeGoalCount = goals.Count(goal => goal != null && !goal.completed && goal.status != ArTusGoalStatus.Completed),
            completedGoalCount = goals.Count(goal => goal != null && (goal.completed || goal.status == ArTusGoalStatus.Completed)),
            shapeKnowledgeCount = shapeKnowledge.Count,
            toolCount = toolCount,
            deviceCount = deviceCount,
            codeArtifactCount = codeArtifactCount,
            topBeliefs = beliefs
                .Where(belief => belief != null && IsAnalyticsTopic(NormalizeTopic(belief.topic)))
                .OrderByDescending(belief => belief.confidenceScore)
                .GroupBy(belief => NormalizeTopic(belief.topic), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(belief => belief.confidenceScore)
                    .First())
                .Take(8)
                .Select(belief => new SummaryBelief
                {
                    topic = NormalizeTopic(belief.topic),
                    confidence = belief.confidenceScore,
                    emotion = ResolveEmotion(belief.dominantEmotion, belief.emotion)
                })
                .ToList(),
            recentTopics = goals
                .Where(goal => goal != null && !string.IsNullOrWhiteSpace(goal.focusTopic))
                .OrderByDescending(goal => SafeDate(goal.lastUpdatedAt, goal.createdAt))
                .Select(goal => NormalizeTopic(goal.focusTopic))
                .Where(IsAnalyticsTopic)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList(),
            categoryCounts = memories
                .Where(memory => memory != null && !string.IsNullOrWhiteSpace(memory.category))
                .GroupBy(memory => memory.category)
                .OrderByDescending(group => group.Count())
                .Take(20)
                .Select(group => new SummaryCount
                {
                    key = group.Key,
                    count = group.Count()
                })
                .ToList()
        };

        File.WriteAllText(path, JsonUtility.ToJson(summary, true));
    }

    private List<ArTusToolDefinition> BuildFallbackToolDefinitions(string activeContext, List<MemoryEntry> memories, List<KnowledgeRecord> knowledgeRecords)
    {
        var tools = new List<ArTusToolDefinition>();

        tools.Add(new ArTusToolDefinition
        {
            toolId = "knowledge_bridge",
            label = "Knowledge Bridge",
            category = "knowledge",
            description = "External knowledge bridge used for topic fetch and semantic intake.",
            endpoint = "ws/http://127.0.0.1:8000",
            canRead = true,
            canWrite = false,
            isConnected = (memories?.Any(memory => string.Equals(memory.category, "WebSocket", StringComparison.OrdinalIgnoreCase)) ?? false) ||
                          (knowledgeRecords?.Count > 0),
            trustScore = 0.82f,
            lastValidatedAt = DateTime.UtcNow.ToString("o"),
            capabilities = new List<string> { "fetch_knowledge", "semantic_summary", "bridge_intake" }
        });

        tools.Add(new ArTusToolDefinition
        {
            toolId = "autonomy_goal_controller",
            label = "Autonomy Goal Controller",
            category = "cognition",
            description = "Routes curiosity, concept discovery, continuation, and restart decisions.",
            endpoint = "unity://goal-controller",
            canRead = true,
            canWrite = true,
            isConnected = goalController != null,
            trustScore = 0.88f,
            lastValidatedAt = DateTime.UtcNow.ToString("o"),
            capabilities = new List<string> { "goal_generation", "thread_continuation", "concept_deepening" }
        });

        tools.Add(new ArTusToolDefinition
        {
            toolId = "powerbi_exporter",
            label = "Power BI Exporter",
            category = "analytics",
            description = "Exports runtime telemetry, concepts, beliefs, and graphs for backend analytics.",
            endpoint = OutputRootPath,
            canRead = true,
            canWrite = true,
            isConnected = true,
            trustScore = 0.93f,
            lastValidatedAt = DateTime.UtcNow.ToString("o"),
            capabilities = new List<string> { "export_facts", "export_graph", "dashboard_summary" }
        });

        if (shapeKnowledgeBridge != null)
        {
            tools.Add(new ArTusToolDefinition
            {
                toolId = "shape_knowledge_bridge",
                label = "Shape Knowledge Bridge",
                category = "embodiment",
                description = "Maps learned concept threads into morphable shape profiles.",
                endpoint = "unity://shape-knowledge",
                canRead = true,
                canWrite = true,
                isConnected = true,
                trustScore = 0.8f,
                lastValidatedAt = DateTime.UtcNow.ToString("o"),
                capabilities = new List<string> { "shape_binding", "embodiment_export", "morph_alignment" }
            });
        }

        if (IsAnalyticsTopic(activeContext))
        {
            tools.Add(new ArTusToolDefinition
            {
                toolId = "active_context_tracker",
                label = "Active Context Tracker",
                category = "context",
                description = "Tracks the deepest active concept thread for belief and autonomy alignment.",
                endpoint = "unity://active-context",
                canRead = true,
                canWrite = false,
                isConnected = true,
                trustScore = 0.76f,
                lastValidatedAt = DateTime.UtcNow.ToString("o"),
                capabilities = new List<string> { "context_resolution", "thread_alignment" }
            });
        }

        return tools
            .GroupBy(tool => tool.toolId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private List<ArTusDeviceDefinition> BuildFallbackDeviceDefinitions()
    {
        return new List<ArTusDeviceDefinition>();
    }

    private List<ArTusCodeArtifact> BuildFallbackCodeArtifacts()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
            return new List<ArTusCodeArtifact>();

        var artifacts = new List<ArTusCodeArtifact>();

        AddCodeDomainArtifact(artifacts, projectRoot, "autonomy-runtime", "C#", Path.Combine(projectRoot, "Assets", "Scripts", "ArTus"), "Core runtime identity, memory, and autonomy coordination.");
        AddCodeDomainArtifact(artifacts, projectRoot, "belief-engine", "C#", Path.Combine(projectRoot, "Assets", "Scripts", "BeliefEngine"), "Belief formation, reinforcement, contradiction, and goal orchestration.");
        AddCodeDomainArtifact(artifacts, projectRoot, "embodiment-morph", "C#", Path.Combine(projectRoot, "Assets", "Morph"), "Morphing, shape intelligence, and embodiment alignment.");
        AddCodeDomainArtifact(artifacts, projectRoot, "analytics-export", "C#", Path.Combine(projectRoot, "Assets", "Scripts", "UNIVERcity"), "Analytics export, dashboards, and backend observability.");
        AddCodeDomainArtifact(artifacts, projectRoot, "bridge-runtime", "Python", Path.Combine(projectRoot, "Tools", "bridge"), "Local bridge and semantic knowledge intake service.");

        return artifacts;
    }

    private void AddCodeDomainArtifact(List<ArTusCodeArtifact> artifacts, string projectRoot, string topic, string language, string folderPath, string summary)
    {
        if (!Directory.Exists(folderPath))
            return;

        string pattern = string.Equals(language, "Python", StringComparison.OrdinalIgnoreCase) ? "*.py" : "*.cs";
        string[] files = Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories);
        if (files.Length == 0)
            return;

        int symbolCount = 0;
        int referenceCount = 0;

        foreach (string file in files.Take(20))
        {
            try
            {
                string text = File.ReadAllText(file);
                symbolCount += Regex.Matches(text, "\\b(class|interface|struct|enum)\\b").Count;
                referenceCount += Regex.Matches(text, "\\b(public|private|protected|internal)\\b").Count;
            }
            catch
            {
                // Ignore individual file read issues and keep the broader artifact.
            }
        }

        artifacts.Add(new ArTusCodeArtifact
        {
            artifactId = topic,
            topic = topic,
            language = language,
            path = folderPath.Replace(projectRoot, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            summary = $"{summary} ({files.Length} files scanned)",
            symbolCount = Mathf.Max(symbolCount, files.Length),
            referenceCount = Mathf.Max(referenceCount, files.Length),
            confidence = 0.45f,
            lastUpdatedAt = DateTime.UtcNow.ToString("o")
        });
    }

    private ArTusIdentitySnapshot BuildFallbackIdentitySnapshot(string activeContext, List<ArTusCodeArtifact> codeArtifacts, List<ArTusToolDefinition> tools)
    {
        return new ArTusIdentitySnapshot
        {
            currentRole = "autonomous embodied learner",
            identityNarrative = IsAnalyticsTopic(activeContext)
                ? $"ArTus is actively deepening the concept thread '{activeContext}' while expanding its autonomy stack."
                : "ArTus is evolving as an embodied autonomous learning system.",
            growthFocuses = new List<string>
            {
                "decision quality",
                "tool use",
                "code intelligence",
                "device control",
                "self-modeling"
            },
            capabilityDomains = new List<string>(
                new[]
                {
                    "autonomy",
                    "beliefs",
                    "concept discovery",
                    "embodiment",
                    "analytics"
                }
                .Concat(codeArtifacts.Any() ? new[] { "code intelligence" } : Array.Empty<string>())
                .Concat(tools.Any(tool => tool.canWrite) ? new[] { "tool use" } : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)),
            updatedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static DiscoveredConceptRecord MergeConceptExportGroup(IGrouping<string, DiscoveredConceptRecord> group)
    {
        var entries = group?.Where(entry => entry != null).ToList();
        if (entries == null || entries.Count == 0)
            return null;

        DiscoveredConceptRecord primary = entries
            .OrderByDescending(entry => entry.promotedCount)
            .ThenByDescending(entry => entry.supportCount)
            .ThenByDescending(entry => SafeDate(entry.updatedAt, entry.createdAt))
            .First();

        primary.supportingTopics = entries
            .SelectMany(entry => entry.supportingTopics ?? new List<string>())
            .Select(NormalizeTopic)
            .Where(topic => !string.IsNullOrWhiteSpace(topic) && IsAnalyticsTopic(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        primary.evidence = entries
            .SelectMany(entry => entry.evidence ?? new List<string>())
            .Select(NormalizeEvidenceText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        primary.supportCount = Mathf.Max(primary.supportCount, primary.supportingTopics.Count);
        primary.promotedCount = entries.Max(entry => entry.promotedCount);
        if (entries.Any(entry => string.Equals(entry.status, "promoted", StringComparison.OrdinalIgnoreCase)))
            primary.status = "promoted";

        return primary;
    }

    private string ResolveActiveContext()
    {
        if (goalController != null)
        {
            string context = NormalizeTopic(goalController.GetCurrentAutonomyContextTopic());
            if (!string.IsNullOrWhiteSpace(context))
                return context;
        }

        if (shapeKnowledgeBridge?.morphController != null)
        {
            ArTusShapeProfile activeShape = shapeKnowledgeBridge.morphController.GetActiveShapeProfile();
            string learnedTopic = NormalizeTopic(activeShape?.learnedTopic);
            if (!string.IsNullOrWhiteSpace(learnedTopic))
                return learnedTopic;
        }

        return NormalizeTopic(core?.lastIngestedTopic);
    }

    private static string BuildEventId(MemoryEntry entry)
    {
        return $"{entry.timestamp:yyyyMMddHHmmssfff}_{Mathf.Abs((entry.content ?? string.Empty).GetHashCode())}";
    }

    private static string ResolveWho(MemoryEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.executed_by))
            return entry.executed_by;
        if (!string.IsNullOrWhiteSpace(entry.speaker))
            return entry.speaker;
        if (!string.IsNullOrWhiteSpace(entry.sourceType))
            return entry.sourceType;
        return "ArTus";
    }

    private static string InferEventType(MemoryEntry entry)
    {
        string content = entry.content ?? string.Empty;
        string category = entry.category ?? string.Empty;

        if (category.IndexOf("Goal", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Goal";
        if (content.IndexOf("QueueReflection", StringComparison.OrdinalIgnoreCase) >= 0 || content.IndexOf("Queued reflection", StringComparison.OrdinalIgnoreCase) >= 0)
            return "QueueReflection";
        if (content.IndexOf("FetchKnowledge", StringComparison.OrdinalIgnoreCase) >= 0 || content.IndexOf("Requested external knowledge", StringComparison.OrdinalIgnoreCase) >= 0)
            return "FetchKnowledge";
        if (content.IndexOf("Reinforced belief", StringComparison.OrdinalIgnoreCase) >= 0)
            return "ReinforceBelief";
        if (content.IndexOf("Discover emerging concept around", StringComparison.OrdinalIgnoreCase) >= 0)
            return "ConceptDiscovery";
        if (content.IndexOf("Learn about", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Curiosity";
        if (category.IndexOf("ExternalKnowledge", StringComparison.OrdinalIgnoreCase) >= 0)
            return "ExternalKnowledge";
        if (category.IndexOf("Shape", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Shape";

        return string.IsNullOrWhiteSpace(category) ? "Memory" : category;
    }

    private static string InferWhy(MemoryEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.action_origin))
            return entry.action_origin;

        string content = entry.content ?? string.Empty;

        if (content.IndexOf("Learn about ", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Curiosity-driven learning";
        if (content.IndexOf("Discover emerging concept around ", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Concept deepening";
        if (content.IndexOf("Queued reflection", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Reflection scheduling";
        if (content.IndexOf("Requested external knowledge", StringComparison.OrdinalIgnoreCase) >= 0)
            return "External knowledge retrieval";
        if (content.IndexOf("Reinforced belief", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Belief strengthening";
        if (content.IndexOf("Applied Shape Profile", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Embodied concept expression";
        if (!string.IsNullOrWhiteSpace(entry.category))
            return entry.category;

        return "Autonomous cognition";
    }

    private static string ExtractTopic(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        Match topicMatch = TopicPatterns.Match(text);
        if (topicMatch.Success)
        {
            string captured = topicMatch.Groups[1].Value
                .Replace(" Form", string.Empty)
                .Replace("\"", string.Empty)
                .Trim();
            return NormalizeTopic(captured);
        }

        Match summaryTopic = Regex.Match(text, "\"topic\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (summaryTopic.Success)
            return NormalizeTopic(summaryTopic.Groups[1].Value);

        return string.Empty;
    }

    private static string ExtractRoute(string text, string sourceType)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Match routeMatch = RoutePattern.Match(text);
            if (routeMatch.Success)
                return routeMatch.Groups[1].Value.Trim();

            if (text.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0)
                return "websocket";
            if (text.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0)
                return "bridge";
        }

        return string.IsNullOrWhiteSpace(sourceType) ? "internal" : sourceType;
    }

    private static string NormalizeEventContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = text.Replace("\r", " ").Replace("\n", " ").Trim();
        Match summaryMatch = SummaryPattern.Match(cleaned);
        if (summaryMatch.Success)
            return summaryMatch.Groups[1].Value.Trim();

        return cleaned;
    }

    private static string NormalizeEvidenceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = text.Replace("\r", " ").Replace("\n", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = Regex.Replace(cleaned, @"The topic '([^']+)' was received through the '([^']+)' route\.?", "Knowledge ingested for $1.", RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace("Local bridge synthesis:", string.Empty).Trim();
        cleaned = cleaned.Replace("📥 Ingested topic:", "Knowledge ingested for").Trim();

        if (cleaned.IndexOf("[concept_discovery]", StringComparison.OrdinalIgnoreCase) >= 0 ||
            cleaned.IndexOf("(exploratory)", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Match ingestedMatch = Regex.Match(cleaned, @"Knowledge ingested for\s+([^\[\].]+)", RegexOptions.IgnoreCase);
            if (ingestedMatch.Success)
                return $"Knowledge ingested for {NormalizeTopic(ingestedMatch.Groups[1].Value)}.";
        }

        Match normalizedIngestedMatch = Regex.Match(cleaned, @"^Knowledge ingested for\s+([^.]+)\.?", RegexOptions.IgnoreCase);
        if (normalizedIngestedMatch.Success)
            cleaned = $"Knowledge ingested for {NormalizeTopic(normalizedIngestedMatch.Groups[1].Value)}.";

        if (cleaned.StartsWith("Reflection: dominant emotion is", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (cleaned.StartsWith("My belief in ", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (cleaned.StartsWith("This cycle, I deepened work on", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (cleaned.StartsWith("Promoted belief:", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (cleaned.StartsWith("API stage completed:", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return cleaned;
    }

    private static List<string> NormalizeTopicList(IEnumerable<string> topics)
    {
        return (topics ?? Enumerable.Empty<string>())
            .Select(NormalizeTopic)
            .Where(topic => !string.IsNullOrWhiteSpace(topic) && IsAnalyticsTopic(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeEvidenceList(IEnumerable<string> evidence)
    {
        return (evidence ?? Enumerable.Empty<string>())
            .Select(NormalizeEvidenceText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return string.Empty;

        string cleaned = topic.Trim();
        Match structuredTopicMatch = StructuredTopicPattern.Match(cleaned);
        if (structuredTopicMatch.Success)
            cleaned = structuredTopicMatch.Groups[1].Success
                ? structuredTopicMatch.Groups[1].Value
                : structuredTopicMatch.Groups[2].Value;

        cleaned = cleaned.Replace(" Form", string.Empty);
        cleaned = cleaned.Replace("\"", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Replace("{topic:", string.Empty).Trim();

        if (cleaned.EndsWith(" is", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(0, cleaned.Length - 3).Trim();
        if (cleaned.EndsWith(" route", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(0, cleaned.Length - 6).Trim();

        if (cleaned.StartsWith("systems thinking causal loop", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.Contains("diagram", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking causal loop diagrams";

        if (cleaned.StartsWith("systems thinking system", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cleaned, "thinking system dynamics", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking system dynamics";

        if (string.Equals(cleaned, "causal loop diagrams", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking causal loop diagrams";

        if (cleaned.StartsWith("thinking leverage points", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking" + cleaned.Substring("thinking".Length);

        if (cleaned.StartsWith("leverage points", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking " + cleaned;

        if (cleaned.StartsWith("thinking feedback loops", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking" + cleaned.Substring("thinking".Length);

        if (cleaned.StartsWith("feedback loops", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking " + cleaned;

        if (cleaned.StartsWith("thinking emergence", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking" + cleaned.Substring("thinking".Length);

        if (cleaned.StartsWith("thinking causal loop", StringComparison.OrdinalIgnoreCase))
            cleaned = "systems thinking causal loop diagrams";

        if (cleaned.StartsWith("connect thinking ", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return cleaned.Trim();
    }

    private static bool IsAnalyticsTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        string normalized = topic.Trim().ToLowerInvariant();
        if (normalized.Length < 4)
            return false;

        foreach (string fragment in InvalidAnalyticsFragments)
        {
            if (normalized.Contains(fragment))
                return false;
        }

        return normalized.Contains("systems thinking") ||
               normalized.Contains("causal loop diagrams") ||
               normalized.Contains("feedback loops") ||
               normalized.Contains("leverage points") ||
               normalized.Contains("system dynamics") ||
               normalized.Contains("emergence");
    }

    private static string ResolveDomain(string preferredDomain, string topic)
    {
        if (!string.IsNullOrWhiteSpace(preferredDomain) &&
            !string.Equals(preferredDomain, "general", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(preferredDomain, "curiosity", StringComparison.OrdinalIgnoreCase))
            return preferredDomain.Trim();

        string normalizedTopic = NormalizeTopic(topic);
        if (normalizedTopic.StartsWith("systems thinking", StringComparison.OrdinalIgnoreCase))
            return "concept_discovery";

        return "general";
    }

    private static string ResolveEmotion(string primary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
            return primary;
        return string.IsNullOrWhiteSpace(fallback) ? "neutral" : fallback;
    }

    private static bool IsBeliefAligned(string topic, string activeContext)
    {
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(activeContext))
            return false;

        string normalizedTopic = topic.ToLowerInvariant();
        string normalizedContext = activeContext.ToLowerInvariant();
        return normalizedTopic.Contains(normalizedContext) || normalizedContext.Contains(normalizedTopic);
    }

    private static DateTime SafeDate(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed))
                return parsed;
        }

        return DateTime.MinValue;
    }

    private static string Csv(string value)
    {
        string safe = value ?? string.Empty;
        safe = safe.Replace("\r", " ").Replace("\n", " ");
        safe = safe.Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }

    private static string F(float value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }

    [Serializable]
    private class PowerBISummary
    {
        public string generatedAt;
        public string activeContext;
        public int memoryCount;
        public int beliefCount;
        public int knowledgeRecordCount;
        public int discoveredConceptCount;
        public int activeGoalCount;
        public int completedGoalCount;
        public int shapeKnowledgeCount;
        public int toolCount;
        public int deviceCount;
        public int codeArtifactCount;
        public List<SummaryBelief> topBeliefs = new();
        public List<string> recentTopics = new();
        public List<SummaryCount> categoryCounts = new();
    }

    [Serializable]
    private class SummaryBelief
    {
        public string topic;
        public float confidence;
        public string emotion;
    }

    [Serializable]
    private class SummaryCount
    {
        public string key;
        public int count;
    }
}
