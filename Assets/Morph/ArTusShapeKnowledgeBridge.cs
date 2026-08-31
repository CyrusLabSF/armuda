using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using ArTusTypes;

public class ArTusShapeKnowledgeBridge : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusCoreState core;
    public ArTusShapeIntelligence shapeIntelligence;
    public ArTusMorphController morphController;
    public ArTusShapeReconstruction shapeReconstruction;

    [Header("Knowledge Sources")]
    public string knowledgeIndexRelativePath = "UNIVERcity/Knowledge/External/knowledge_records.json";
    public string verificationAuditRelativePath = "UNIVERcity/Verification/verification_audit.json";
    public string shapeKnowledgeRelativePath = "UNIVERcity/Knowledge/ShapeKnowledge/shape_knowledge.json";
    public string shapeDescriptorRelativePath = "UNIVERcity/Knowledge/ShapeDescriptors";
    public string shapeDescriptorImportRelativePath = "UNIVERcity/Knowledge/ShapeDescriptorImports";
    public string geometryLibraryImportRelativePath = "UNIVERcity/Knowledge/GeometryLibraryImports";
    public string geometryManifestFileName = "geometry_manifest.json";
    public string ingestionAuditRelativePath = "UNIVERcity/Knowledge/ShapeKnowledge/shape_ingestion_audit.json";
    public string powerBiExportRelativePath = "UNIVERcity/Exports/PowerBI";

    [Header("Sync")]
    public bool autoSyncOnAwake = true;
    public bool autoSyncIntervalEnabled = true;
    public float syncInterval = 15f;
    [Range(0f, 1f)] public float minimumKnowledgeConfidence = 0.45f;
    [Range(0f, 1f)] public float minimumShapeConfidenceToApply = 0.55f;
    public bool preferVerifiedTopics = true;
    public bool autoApplyHighestConfidenceTopic = false;

    [Header("Refinement")]
    [Range(0f, 1f)] public float descriptorRefinementTrigger = 0.58f;
    [Range(0f, 1f)] public float descriptorRefinementBlend = 0.35f;
    [Range(0f, 1f)] public float descriptorConfidencePenalty = 0.04f;
    [Range(0f, 1f)] public float descriptorConfidenceRecovery = 0.02f;

    private float nextSyncTime;
    private ShapeKnowledgeWrapper cachedShapeKnowledge = new();
    private ShapeIngestionAuditWrapper ingestionAudit = new();

    private string KnowledgeIndexPath => ArTusPathUtility.GetPersistent(knowledgeIndexRelativePath);
    private string VerificationAuditPath => ArTusPathUtility.GetPersistent(verificationAuditRelativePath);
    private string ShapeKnowledgePath => ArTusPathUtility.GetPersistent(shapeKnowledgeRelativePath);
    private string ShapeDescriptorRootPath => ArTusPathUtility.GetPersistent(shapeDescriptorRelativePath);
    private string ShapeDescriptorImportRootPath => ArTusPathUtility.GetPersistent(shapeDescriptorImportRelativePath);
    private string GeometryLibraryImportRootPath => ArTusPathUtility.GetPersistent(geometryLibraryImportRelativePath);
    private string GeometryManifestPath => Path.Combine(GeometryLibraryImportRootPath, geometryManifestFileName);
    private string IngestionAuditPath => ArTusPathUtility.GetPersistent(ingestionAuditRelativePath);
    private string PowerBiExportRootPath => ArTusPathUtility.GetPersistent(powerBiExportRelativePath);

    private void Awake()
    {
        if (core == null)
            core = FindAnyObjectByType<ArTusCoreState>();
        if (shapeIntelligence == null)
            shapeIntelligence = FindAnyObjectByType<ArTusShapeIntelligence>();
        if (morphController == null)
            morphController = FindAnyObjectByType<ArTusMorphController>();
        if (shapeReconstruction == null)
            shapeReconstruction = FindAnyObjectByType<ArTusShapeReconstruction>();

        EnsureStorageDirectory();
        LoadIngestionAudit();

        if (autoSyncOnAwake)
            SyncKnowledgeShapes();

        nextSyncTime = Time.time + syncInterval;
    }

    private void Update()
    {
        if (!autoSyncIntervalEnabled || syncInterval <= 0f)
            return;

        if (Time.time < nextSyncTime)
            return;

        nextSyncTime = Time.time + syncInterval;
        SyncKnowledgeShapes();
    }

    [ContextMenu("Sync Knowledge Shapes")]
    public void SyncKnowledgeShapes()
    {
        EnsureStorageDirectory();

        var knowledge = LoadKnowledgeRecords();
        var verificationLookup = BuildVerificationLookup();
        var descriptors = LoadShapeFormDescriptors();

        var derived = knowledge
            .Where(record => record != null && !string.IsNullOrWhiteSpace(record.topic))
            .Where(record => Mathf.Clamp01(record.confidence) >= minimumKnowledgeConfidence)
            .Select(record => BuildShapeKnowledgeRecord(record, verificationLookup, descriptors))
            .Where(record => record?.shapeProfile != null)
            .GroupBy(record => $"{Normalize(record.topic)}::{Normalize(record.domain)}")
            .Select(group => group
                .OrderByDescending(entry => ScoreKnowledgeShape(entry))
                .First())
            .OrderByDescending(ScoreKnowledgeShape)
            .ToList();

        cachedShapeKnowledge = new ShapeKnowledgeWrapper
        {
            entries = derived
        };

        PersistShapeKnowledge();
        TeachKnownShapes(cachedShapeKnowledge.entries);

        if (autoApplyHighestConfidenceTopic)
        {
            var best = cachedShapeKnowledge.entries
                .OrderByDescending(ScoreKnowledgeShape)
                .FirstOrDefault(entry => entry.confidence >= minimumShapeConfidenceToApply);

            if (best != null)
                ApplyShapeForTopic(best.topic, best.domain);
        }
    }

    public bool ApplyShapeForTopic(string topic, string domain = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        if (cachedShapeKnowledge?.entries == null || cachedShapeKnowledge.entries.Count == 0)
            SyncKnowledgeShapes();

        string normalizedTopic = Normalize(topic);
        string normalizedDomain = Normalize(domain);

        var match = (cachedShapeKnowledge?.entries ?? new List<ShapeKnowledgeRecord>())
            .Where(entry =>
                entry != null &&
                string.Equals(Normalize(entry.topic), normalizedTopic, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(normalizedDomain) ||
                 string.Equals(Normalize(entry.domain), normalizedDomain, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(ScoreKnowledgeShape)
            .FirstOrDefault();

        if (match?.shapeProfile == null)
            return false;

        shapeIntelligence?.SetKnowledgeContext(match.topic, match.domain, match.verificationState);
        shapeIntelligence?.LearnShape(match.shapeProfile);
        morphController?.ApplyShapeProfile(match.shapeProfile);

        core?.LogMemory(
            $"Applied knowledge-backed shape '{match.shapeProfile.displayName}' for topic '{match.topic}'.",
            "ShapeKnowledge",
            3,
            "curious"
        );

        return true;
    }

    public bool RefineShapeDescriptorForTopic(string topic, string domain = null, float observedScore = -1f)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorRootPath);

        string normalizedTopic = Normalize(topic);
        string normalizedDomain = Normalize(domain);
        var descriptors = LoadShapeFormDescriptors();

        ShapeFormDescriptor descriptor = descriptors.FirstOrDefault(entry =>
            entry != null &&
            string.Equals(Normalize(entry.topic), normalizedTopic, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(normalizedDomain) ||
             string.Equals(Normalize(entry.domain), normalizedDomain, StringComparison.OrdinalIgnoreCase)));

        ShapeKnowledgeRecord knowledge = GetShapeKnowledgeEntries()
            .Where(entry =>
                entry != null &&
                !string.IsNullOrWhiteSpace(entry.topic) &&
                string.Equals(Normalize(entry.topic), normalizedTopic, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(normalizedDomain) ||
                 string.Equals(Normalize(entry.domain), normalizedDomain, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(ScoreKnowledgeShape)
            .FirstOrDefault();

        ArTusShapeProfile profile = morphController != null &&
            string.Equals(Normalize(morphController.GetActiveShapeProfile()?.learnedTopic), normalizedTopic, StringComparison.OrdinalIgnoreCase)
            ? morphController.GetActiveShapeProfile()
            : knowledge?.shapeProfile;

        if (profile == null)
            return false;

        if (descriptor == null)
            descriptor = BuildDescriptorFromProfile(profile, topic, domain, knowledge);

        float effectiveScore = observedScore >= 0f
            ? Mathf.Clamp01(observedScore)
            : shapeReconstruction != null
                ? Mathf.Clamp01(shapeReconstruction.GetLastFinalScore())
                : Mathf.Clamp01(profile.reconstructionScore);

        ApplyDescriptorRefinement(descriptor, profile, effectiveScore);
        PersistDescriptor(descriptor);
        SyncKnowledgeShapes();

        core?.LogMemory(
            $"Refined shape descriptor for '{topic}' after reconstruction score {effectiveScore:F2}.",
            "ShapeDescriptorRefinement",
            3,
            effectiveScore < descriptorRefinementTrigger ? "focused" : "calm"
        );

        return true;
    }

    public List<ShapeKnowledgeRecord> GetShapeKnowledgeEntries()
    {
        return cachedShapeKnowledge?.entries != null
            ? new List<ShapeKnowledgeRecord>(cachedShapeKnowledge.entries)
            : new List<ShapeKnowledgeRecord>();
    }

    public List<ShapeFormDescriptor> GetShapeFormDescriptors()
    {
        return LoadShapeFormDescriptors();
    }

    public List<string> GetShapeKnowledgeSummaryLines(int maxCount = 8)
    {
        return GetShapeKnowledgeEntries()
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.confidence)
            .Take(Mathf.Max(1, maxCount))
            .Select(entry =>
                $"{entry.topic} [{entry.domain}] -> {entry.verificationState} / {entry.confidence:F2}")
            .ToList();
    }

    public List<string> GetDescriptorSummaryLines(int maxCount = 8)
    {
        return GetShapeFormDescriptors()
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.confidence)
            .Take(Mathf.Max(1, maxCount))
            .Select(entry =>
                $"{entry.topic} [{entry.domain}] -> {entry.archetype} / {entry.confidence:F2}")
            .ToList();
    }

    public List<string> GetAnalyticsSummaryLines(int maxCount = 8)
    {
        return BuildAnalyticsRows()
            .Take(Mathf.Max(1, maxCount))
            .Select(row =>
                $"{row.topic} [{row.domain}] -> {row.shapeId} / recon {row.reconstructionScore:F2} / verified {row.verificationState}")
            .ToList();
    }

    public List<string> GetManifestSummaryLines(int maxCount = 8)
    {
        return LoadGeometryManifestEntries()
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.importPriority)
            .Take(Mathf.Max(1, maxCount))
            .Select(entry =>
                $"{ResolveManifestLabel(entry)} / priority {entry.importPriority} / {ResolveLicenseLabel(entry)}")
            .ToList();
    }

    public List<string> GetManifestHighPriorityLines(int maxCount = 8)
    {
        return LoadGeometryManifestEntries()
            .Where(entry => entry != null && entry.enabled)
            .OrderByDescending(entry => entry.importPriority)
            .ThenBy(entry => ResolveManifestLabel(entry))
            .Take(Mathf.Max(1, maxCount))
            .Select(entry =>
                $"{ResolveManifestLabel(entry)} / priority {entry.importPriority} / {entry.sourceSite}")
            .ToList();
    }

    public List<string> GetManifestMissingLicenseLines(int maxCount = 8)
    {
        return LoadGeometryManifestEntries()
            .Where(entry =>
                entry != null &&
                entry.enabled &&
                string.IsNullOrWhiteSpace(entry.sourceLicense))
            .OrderByDescending(entry => entry.importPriority)
            .ThenBy(entry => ResolveManifestLabel(entry))
            .Take(Mathf.Max(1, maxCount))
            .Select(entry =>
                $"{ResolveManifestLabel(entry)} / source {entry.sourceSite ?? "unknown"} / license missing")
            .ToList();
    }

    public List<string> GetManifestWeakLearningLines(int maxCount = 8, float maxReconstructionScore = 0.55f)
    {
        return BuildAnalyticsRows()
            .Where(row =>
                row != null &&
                row.descriptorImportPriority > 0 &&
                row.reconstructionScore <= maxReconstructionScore)
            .OrderByDescending(row => row.descriptorImportPriority)
            .ThenBy(row => row.reconstructionScore)
            .Take(Mathf.Max(1, maxCount))
            .Select(row =>
                $"{row.topic} [{row.domain}] / priority {row.descriptorImportPriority} / recon {row.reconstructionScore:F2} / source {row.descriptorSourceSite}")
            .ToList();
    }

    public List<string> GetHighPriorityMissingLicenseTopics(int maxCount = 5)
    {
        return LoadGeometryManifestEntries()
            .Where(entry =>
                entry != null &&
                entry.enabled &&
                !string.IsNullOrWhiteSpace(entry.topic) &&
                string.IsNullOrWhiteSpace(entry.sourceLicense))
            .OrderByDescending(entry => entry.importPriority)
            .ThenBy(entry => ResolveManifestLabel(entry))
            .Select(entry => entry.topic.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Mathf.Max(1, maxCount))
            .ToList();
    }

    public List<string> GetIngestionAuditSummaryLines(int maxCount = 8)
    {
        return (ingestionAudit?.entries ?? new List<ShapeIngestionAuditEntry>())
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.recordedAt)
            .Take(Mathf.Max(1, maxCount))
            .Select(entry =>
                $"{entry.topic} [{entry.domain}] / {entry.status} / {entry.sourceSite} / recon {entry.reconstructionScore:F2}")
            .ToList();
    }

    public List<string> GetHighPriorityIngestionRiskTopics(int maxCount = 5)
    {
        return (ingestionAudit?.entries ?? new List<ShapeIngestionAuditEntry>())
            .Where(entry =>
                entry != null &&
                entry.importPriority >= 70 &&
                (string.Equals(entry.status, "missing_license", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(entry.status, "weak_learning", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(entry.status, "import_failed", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(entry => entry.importPriority)
            .ThenByDescending(entry => entry.recordedAt)
            .Select(entry => entry.topic)
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Mathf.Max(1, maxCount))
            .ToList();
    }

    public List<string> GetHighPriorityShapeRefinementTopics(
        float maxReconstructionScore = 0.55f,
        float minimumShapeConfidence = 0.55f,
        int maxCount = 5
    )
    {
        return BuildAnalyticsRows()
            .Where(row =>
                row != null &&
                !string.IsNullOrWhiteSpace(row.topic) &&
                row.shapeConfidence >= minimumShapeConfidence &&
                row.reconstructionScore <= maxReconstructionScore)
            .OrderBy(row => row.reconstructionScore)
            .ThenByDescending(row => row.shapeConfidence)
            .Select(row => row.topic)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Mathf.Max(1, maxCount))
            .ToList();
    }

    public void NotifyKnowledgeUpdated(KnowledgeRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.topic))
            return;

        SyncKnowledgeShapes();
        shapeIntelligence?.SetKnowledgeContext(record.topic, record.domain);
    }

    [ContextMenu("Create Shape Descriptor Folder")]
    public void CreateShapeDescriptorFolder()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorRootPath);
        Debug.Log($"[ShapeKnowledge] Descriptor folder ready: {ShapeDescriptorRootPath}");
    }

    [ContextMenu("Create Shape Descriptor Import Folder")]
    public void CreateShapeDescriptorImportFolder()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorImportRootPath);
        Debug.Log($"[ShapeKnowledge] Import folder ready: {ShapeDescriptorImportRootPath}");
    }

    [ContextMenu("Create Geometry Library Import Folder")]
    public void CreateGeometryLibraryImportFolder()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(GeometryLibraryImportRootPath);
        Debug.Log($"[ShapeKnowledge] Geometry library folder ready: {GeometryLibraryImportRootPath}");
    }

    [ContextMenu("Create Geometry Manifest Template")]
    public void CreateGeometryManifestTemplate()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(GeometryLibraryImportRootPath);

        var manifest = new ShapeIngestionManifest
        {
            entries = new List<ShapeIngestionManifestEntry>
            {
                new ShapeIngestionManifestEntry
                {
                    fileName = "bunny.obj",
                    relativePath = "Animals/bunny.obj",
                    topic = "stanford bunny",
                    domain = "animals",
                    archetype = "organic_reference",
                    symbolicMeaning = "Baseline organic geometry reference",
                    sourceSite = "Stanford 3D Scanning Repository",
                    sourceLicense = "See source repository terms",
                    attribution = "Stanford Computer Graphics Laboratory",
                    importPriority = 90,
                    tags = "animal|benchmark|geometry-library",
                    notes = "High-value benchmark reference for organic surface learning.",
                    enabled = true
                }
            }
        };

        File.WriteAllText(GeometryManifestPath, JsonUtility.ToJson(manifest, true));
        Debug.Log($"[ShapeKnowledge] Geometry manifest template created: {GeometryManifestPath}");
    }

    [ContextMenu("Generate Procedural Geometry Seeds")]
    public void GenerateProceduralGeometrySeeds()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorRootPath);

        int created = 0;
        foreach (ShapeFormDescriptor descriptor in BuildProceduralSeedDescriptors())
        {
            PersistDescriptor(descriptor);
            created++;
        }

        SyncKnowledgeShapes();
        Debug.Log($"[ShapeKnowledge] Generated {created} procedural geometry seed descriptors.");
    }

    [ContextMenu("Import Shape Descriptor Database")]
    public void ImportShapeDescriptorDatabase()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorImportRootPath);
        Directory.CreateDirectory(ShapeDescriptorRootPath);

        int imported = 0;

        foreach (string jsonFile in Directory.GetFiles(ShapeDescriptorImportRootPath, "*.json", SearchOption.AllDirectories))
            imported += ImportShapeDescriptorJson(jsonFile);

        foreach (string csvFile in Directory.GetFiles(ShapeDescriptorImportRootPath, "*.csv", SearchOption.AllDirectories))
            imported += ImportShapeDescriptorCsv(csvFile);

        if (imported > 0)
        {
            SyncKnowledgeShapes();
            core?.LogMemory(
                $"Imported {imported} shape descriptor records from database files.",
                "ShapeKnowledgeImport",
                3,
                "curious"
            );
        }

        Debug.Log($"[ShapeKnowledge] Imported {imported} descriptor records.");
    }

    [ContextMenu("Import Geometry Library")]
    public void ImportGeometryLibrary()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(GeometryLibraryImportRootPath);
        Directory.CreateDirectory(ShapeDescriptorRootPath);

        var manifestLookup = LoadGeometryManifestLookup();

        int imported = 0;

        foreach (string objFile in Directory.GetFiles(GeometryLibraryImportRootPath, "*.obj", SearchOption.AllDirectories))
            imported += ImportObjGeometryFile(objFile, manifestLookup);

        if (imported > 0)
        {
            SyncKnowledgeShapes();
            core?.LogMemory(
                $"Imported {imported} geometry-derived descriptors from library files.",
                "GeometryLibraryImport",
                3,
                "curious"
            );
        }

        Debug.Log($"[ShapeKnowledge] Imported {imported} geometry library descriptors.");
    }

    public void RecordShapeIngestionAudit(
        string topic,
        string domain,
        string sourceSite,
        string sourceLicense,
        string attribution,
        string relativePath,
        string status,
        string summary,
        int importPriority,
        float reconstructionScore,
        string goalId = null
    )
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        if (ingestionAudit == null)
            ingestionAudit = new ShapeIngestionAuditWrapper();

        ingestionAudit.entries.Add(new ShapeIngestionAuditEntry
        {
            topic = topic,
            domain = string.IsNullOrWhiteSpace(domain) ? "general" : domain,
            sourceSite = sourceSite,
            sourceLicense = sourceLicense,
            attribution = attribution,
            relativePath = relativePath,
            status = string.IsNullOrWhiteSpace(status) ? "observed" : status,
            summary = summary,
            importPriority = Mathf.Max(0, importPriority),
            reconstructionScore = Mathf.Clamp01(reconstructionScore),
            goalId = goalId
        });

        PersistIngestionAudit();
    }

    [ContextMenu("Export Shape Data For Power BI")]
    public void ExportShapeDataForPowerBI()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(PowerBiExportRootPath);

        ExportShapeKnowledgeCsv();
        ExportShapeDescriptorCsv();
        ExportShapeAnalyticsCsv();
    }

    [ContextMenu("Create Shape Descriptor CSV Template")]
    public void CreateShapeDescriptorCsvTemplate()
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorImportRootPath);

        string csvPath = Path.Combine(ShapeDescriptorImportRootPath, "shape_descriptor_import_template.csv");
        string header = string.Join(",",
            "topic",
            "domain",
            "archetype",
            "symbolicMeaning",
            "tags",
            "axisX",
            "axisY",
            "axisZ",
            "baseScaleX",
            "baseScaleY",
            "baseScaleZ",
            "stability",
            "complexity",
            "confidence",
            "pulseStrength",
            "rippleStrength",
            "orbitStrength",
            "twistStrength",
            "taperStrength",
            "emotionalAffinityCuriosity",
            "emotionalAffinityThinking",
            "emotionalAffinityConflict",
            "emotionalAffinityCalm",
            "emotionalAffinityJoy",
            "notes"
        );

        string example = string.Join(",",
            "tree intelligence",
            "nature",
            "vertical_growth",
            "\"Growth through layered memory\"",
            "\"tree|growth|organic\"",
            "0.15",
            "0.70",
            "0.15",
            "0.95",
            "1.35",
            "0.95",
            "0.78",
            "0.66",
            "0.82",
            "0.34",
            "0.24",
            "0.20",
            "0.28",
            "0.42",
            "0.88",
            "0.72",
            "0.18",
            "0.64",
            "0.54",
            "\"Imported from database row\""
        );

        File.WriteAllLines(csvPath, new[] { header, example });
        Debug.Log($"[ShapeKnowledge] CSV template created: {csvPath}");
    }

    public string SaveDescriptorTemplate(string topic, string domain = "general")
    {
        EnsureStorageDirectory();
        Directory.CreateDirectory(ShapeDescriptorRootPath);

        string normalizedTopic = string.IsNullOrWhiteSpace(topic) ? "topic" : Normalize(topic);
        string normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "general" : Normalize(domain);
        string path = Path.Combine(ShapeDescriptorRootPath, $"{normalizedTopic}_{normalizedDomain}_descriptor.json");

        var descriptor = new ShapeFormDescriptor
        {
            topic = topic,
            domain = domain,
            archetype = "custom_form",
            symbolicMeaning = $"Descriptor template for {topic}",
            notes = "Fill in axisWeights, strengths, and affinities from database-derived form metadata.",
            sourceSite = "manual",
            sourceLicense = "unspecified",
            attribution = "local author",
            sourcePath = path
        };

        File.WriteAllText(path, JsonUtility.ToJson(descriptor, true));
        return path;
    }

    public void ExportShapeKnowledgeCsv()
    {
        try
        {
            Directory.CreateDirectory(PowerBiExportRootPath);
            string path = Path.Combine(PowerBiExportRootPath, "ShapeKnowledge.csv");
            var entries = GetShapeKnowledgeEntries();
            var builder = new StringBuilder();
            builder.AppendLine(
                "topic,domain,verificationState,confidence,descriptorId,knowledgeRecordId,sourceUrl,summary,evidenceCount,archetype,tags"
            );

            foreach (var entry in entries.Where(entry => entry != null))
            {
                builder.AppendLine(string.Join(",",
                    Csv(entry.topic),
                    Csv(entry.domain),
                    Csv(entry.verificationState),
                    entry.confidence.ToString("F3"),
                    Csv(entry.descriptorId),
                    Csv(entry.knowledgeRecordId),
                    Csv(entry.sourceUrl),
                    Csv(entry.summary),
                    (entry.evidence?.Count ?? 0).ToString(),
                    Csv(entry.shapeProfile?.category),
                    Csv(string.Join("|", entry.tags ?? new List<string>()))
                ));
            }

            File.WriteAllText(path, builder.ToString());
            Debug.Log($"[ShapeKnowledge] Power BI shape knowledge export complete: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to export shape knowledge CSV: {ex.Message}");
        }
    }

    public void ExportShapeDescriptorCsv()
    {
        try
        {
            Directory.CreateDirectory(PowerBiExportRootPath);
            string path = Path.Combine(PowerBiExportRootPath, "ShapeDescriptors.csv");
            var entries = GetShapeFormDescriptors();
            var builder = new StringBuilder();
            builder.AppendLine(
                "topic,domain,archetype,symbolicMeaning,confidence,sourceSite,sourceLicense,attribution,importPriority,stability,complexity,axisX,axisY,axisZ,baseScaleX,baseScaleY,baseScaleZ,pulseStrength,rippleStrength,orbitStrength,twistStrength,taperStrength,refinementCount,targetReconstructionScore,lastObservedScore,lastRefinedAt,tags,sourcePath"
            );

            foreach (var entry in entries.Where(entry => entry != null))
            {
                builder.AppendLine(string.Join(",",
                    Csv(entry.topic),
                    Csv(entry.domain),
                    Csv(entry.archetype),
                    Csv(entry.symbolicMeaning),
                    entry.confidence.ToString("F3"),
                    Csv(entry.sourceSite),
                    Csv(entry.sourceLicense),
                    Csv(entry.attribution),
                    entry.importPriority.ToString(),
                    entry.stability.ToString("F3"),
                    entry.complexity.ToString("F3"),
                    entry.axisWeights.x.ToString("F3"),
                    entry.axisWeights.y.ToString("F3"),
                    entry.axisWeights.z.ToString("F3"),
                    entry.baseScale.x.ToString("F3"),
                    entry.baseScale.y.ToString("F3"),
                    entry.baseScale.z.ToString("F3"),
                    entry.pulseStrength.ToString("F3"),
                    entry.rippleStrength.ToString("F3"),
                    entry.orbitStrength.ToString("F3"),
                    entry.twistStrength.ToString("F3"),
                    entry.taperStrength.ToString("F3"),
                    entry.refinementCount.ToString(),
                    entry.targetReconstructionScore.ToString("F3"),
                    entry.lastObservedScore.ToString("F3"),
                    Csv(entry.lastRefinedAt),
                    Csv(string.Join("|", entry.tags ?? new List<string>())),
                    Csv(entry.sourcePath)
                ));
            }

            File.WriteAllText(path, builder.ToString());
            Debug.Log($"[ShapeKnowledge] Power BI descriptor export complete: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to export shape descriptor CSV: {ex.Message}");
        }
    }

    public void ExportShapeAnalyticsCsv()
    {
        try
        {
            Directory.CreateDirectory(PowerBiExportRootPath);
            string path = Path.Combine(PowerBiExportRootPath, "ShapeAnalytics.csv");
            var rows = BuildAnalyticsRows();
            var builder = new StringBuilder();
            builder.AppendLine(
                "topic,domain,shapeId,displayName,verificationState,shapeConfidence,reconstructionScore,successfulReproductions,timesLearned,descriptorConfidence,descriptorArchetype,descriptorSourceSite,descriptorSourceLicense,descriptorAttribution,descriptorImportPriority,descriptorRefinementCount,descriptorTargetReconstructionScore,descriptorLastObservedScore,descriptorLastRefinedAt,lastScaleScore,lastMotionScore,lastStabilityScore,lastFinalScore,isActive,currentKnowledgeTopic,currentKnowledgeDomain,currentVerificationState,symbolicMeaning"
            );

            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(",",
                    Csv(row.topic),
                    Csv(row.domain),
                    Csv(row.shapeId),
                    Csv(row.displayName),
                    Csv(row.verificationState),
                    row.shapeConfidence.ToString("F3"),
                    row.reconstructionScore.ToString("F3"),
                    row.successfulReproductions.ToString(),
                    row.timesLearned.ToString(),
                    row.descriptorConfidence.ToString("F3"),
                    Csv(row.descriptorArchetype),
                    Csv(row.descriptorSourceSite),
                    Csv(row.descriptorSourceLicense),
                    Csv(row.descriptorAttribution),
                    row.descriptorImportPriority.ToString(),
                    row.descriptorRefinementCount.ToString(),
                    row.descriptorTargetReconstructionScore.ToString("F3"),
                    row.descriptorLastObservedScore.ToString("F3"),
                    Csv(row.descriptorLastRefinedAt),
                    row.lastScaleScore.ToString("F3"),
                    row.lastMotionScore.ToString("F3"),
                    row.lastStabilityScore.ToString("F3"),
                    row.lastFinalScore.ToString("F3"),
                    row.isActive ? "1" : "0",
                    Csv(row.currentKnowledgeTopic),
                    Csv(row.currentKnowledgeDomain),
                    Csv(row.currentVerificationState),
                    Csv(row.symbolicMeaning)
                ));
            }

            File.WriteAllText(path, builder.ToString());
            Debug.Log($"[ShapeKnowledge] Power BI analytics export complete: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to export shape analytics CSV: {ex.Message}");
        }
    }

    private List<KnowledgeRecord> LoadKnowledgeRecords()
    {
        if (!File.Exists(KnowledgeIndexPath))
            return new List<KnowledgeRecord>();

        try
        {
            string json = File.ReadAllText(KnowledgeIndexPath);
            var wrapper = JsonUtility.FromJson<KnowledgeRecordWrapper>(json);
            return wrapper?.entries ?? new List<KnowledgeRecord>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to load knowledge records: {ex.Message}");
            return new List<KnowledgeRecord>();
        }
    }

    private Dictionary<string, VerificationAuditEntry> BuildVerificationLookup()
    {
        var lookup = new Dictionary<string, VerificationAuditEntry>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(VerificationAuditPath))
            return lookup;

        try
        {
            string json = File.ReadAllText(VerificationAuditPath);
            var wrapper = JsonUtility.FromJson<VerificationAuditWrapper>(json);

            foreach (var entry in wrapper?.entries ?? new List<VerificationAuditEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.topic))
                    continue;

                string key = Normalize(entry.topic);
                if (!lookup.TryGetValue(key, out var current) ||
                    ParseAuditDate(entry.completedAt) > ParseAuditDate(current.completedAt))
                {
                    lookup[key] = entry;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to load verification audit: {ex.Message}");
        }

        return lookup;
    }

    private List<ShapeFormDescriptor> LoadShapeFormDescriptors()
    {
        var descriptors = new List<ShapeFormDescriptor>();

        if (!Directory.Exists(ShapeDescriptorRootPath))
            return descriptors;

        foreach (string file in Directory.GetFiles(ShapeDescriptorRootPath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(file);

                var wrapper = JsonUtility.FromJson<ShapeFormDescriptorWrapper>(json);
                if (wrapper?.entries != null && wrapper.entries.Count > 0)
                {
                    foreach (var descriptor in wrapper.entries.Where(d => d != null))
                    {
                        descriptor.sourcePath = file;
                        descriptors.Add(descriptor);
                    }

                    continue;
                }

                var single = JsonUtility.FromJson<ShapeFormDescriptor>(json);
                if (single != null && !string.IsNullOrWhiteSpace(single.topic))
                {
                    single.sourcePath = file;
                    descriptors.Add(single);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShapeKnowledge] Failed to load descriptor '{file}': {ex.Message}");
            }
        }

        return descriptors;
    }

    private int ImportShapeDescriptorJson(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            int imported = 0;

            var wrapper = JsonUtility.FromJson<ShapeDatabaseRecordWrapper>(json);
            if (wrapper?.entries != null && wrapper.entries.Count > 0)
            {
                foreach (var row in wrapper.entries.Where(entry => entry != null))
                    imported += PersistImportedDescriptor(row, path) ? 1 : 0;

                return imported;
            }

            var single = JsonUtility.FromJson<ShapeDatabaseRecord>(json);
            if (single != null && !string.IsNullOrWhiteSpace(single.topic))
                return PersistImportedDescriptor(single, path) ? 1 : 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to import JSON database '{path}': {ex.Message}");
        }

        return 0;
    }

    private int ImportShapeDescriptorCsv(string path)
    {
        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2)
                return 0;

            string[] headers = SplitCsvLine(lines[0]);
            int imported = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                string[] values = SplitCsvLine(lines[i]);
                var row = BuildDatabaseRecordFromCsv(headers, values);
                if (row != null)
                    imported += PersistImportedDescriptor(row, path) ? 1 : 0;
            }

            return imported;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to import CSV database '{path}': {ex.Message}");
            return 0;
        }
    }

    private int ImportObjGeometryFile(string path, IReadOnlyDictionary<string, ShapeIngestionManifestEntry> manifestLookup)
    {
        try
        {
            GeometryImportMetrics metrics = AnalyzeObjGeometry(path);
            if (metrics == null || metrics.vertexCount < 3)
                return 0;

            ShapeDatabaseRecord metadata = LoadGeometryMetadata(path);
            ShapeIngestionManifestEntry manifestEntry = FindManifestEntryForGeometry(path, manifestLookup);
            string topic = !string.IsNullOrWhiteSpace(metadata?.topic)
                ? metadata.topic.Trim()
                : !string.IsNullOrWhiteSpace(manifestEntry?.topic)
                    ? manifestEntry.topic.Trim()
                : DeriveTopicFromFilename(path);
            string domain = !string.IsNullOrWhiteSpace(metadata?.domain)
                ? metadata.domain.Trim()
                : !string.IsNullOrWhiteSpace(manifestEntry?.domain)
                    ? manifestEntry.domain.Trim()
                : "geometry_library";
            string classification = ClassifyGeometry(metrics.axisWeights, metrics.symmetry, metrics.curvature, metrics.hollowness);

            var row = new ShapeDatabaseRecord
            {
                topic = topic,
                domain = domain,
                archetype = !string.IsNullOrWhiteSpace(metadata?.archetype)
                    ? metadata.archetype
                    : !string.IsNullOrWhiteSpace(manifestEntry?.archetype)
                        ? manifestEntry.archetype
                    : NormalizeArchetypeLabel(classification),
                symbolicMeaning = !string.IsNullOrWhiteSpace(metadata?.symbolicMeaning)
                    ? metadata.symbolicMeaning
                    : !string.IsNullOrWhiteSpace(manifestEntry?.symbolicMeaning)
                        ? manifestEntry.symbolicMeaning
                    : $"Geometry-learned form from {Path.GetFileName(path)} classified as {classification}",
                sourceKind = "geometry-library",
                sourcePath = path,
                sourceSite = manifestEntry?.sourceSite,
                sourceLicense = manifestEntry?.sourceLicense,
                attribution = manifestEntry?.attribution,
                importPriority = manifestEntry != null ? Mathf.Max(0, manifestEntry.importPriority) : 50,
                notes = BuildGeometryNotes(path, metrics, metadata?.notes),
                tags = BuildGeometryTags(path, classification, metadata?.tags, manifestEntry?.tags),
                axisX = metrics.axisWeights.x,
                axisY = metrics.axisWeights.y,
                axisZ = metrics.axisWeights.z,
                baseScaleX = Mathf.Clamp(metrics.size.x, 0.1f, 3f),
                baseScaleY = Mathf.Clamp(metrics.size.y, 0.1f, 3f),
                baseScaleZ = Mathf.Clamp(metrics.size.z, 0.1f, 3f),
                stability = Mathf.Clamp01(Mathf.Lerp(0.85f, 0.3f, metrics.complexity)),
                complexity = metrics.complexity,
                confidence = Mathf.Clamp01(Mathf.Max(metadata?.confidence ?? 0f, 0.35f + (metrics.vertexCount / 8000f))),
                pulseStrength = Mathf.Clamp01(Mathf.Lerp(0.12f, 0.72f, metrics.hollowness)),
                rippleStrength = Mathf.Clamp01(Mathf.Lerp(0.15f, 0.8f, metrics.complexity)),
                orbitStrength = Mathf.Clamp01(Mathf.Lerp(0.1f, 0.75f, metrics.symmetry)),
                twistStrength = Mathf.Clamp01(Mathf.Lerp(0.12f, 0.85f, metrics.curvature)),
                taperStrength = Mathf.Clamp01(Mathf.Max(metrics.axisWeights.y - Mathf.Max(metrics.axisWeights.x, metrics.axisWeights.z), 0f) + 0.15f),
                emotionalAffinityCuriosity = Mathf.Clamp01(0.55f + metrics.complexity * 0.35f),
                emotionalAffinityThinking = Mathf.Clamp01(0.45f + metrics.symmetry * 0.3f),
                emotionalAffinityConflict = Mathf.Clamp01(0.15f + metrics.curvature * 0.35f),
                emotionalAffinityCalm = Mathf.Clamp01(0.25f + (1f - metrics.curvature) * 0.4f),
                emotionalAffinityJoy = Mathf.Clamp01(0.2f + metrics.symmetry * 0.25f)
            };

            if (metadata != null)
                OverlayMetadata(row, metadata);

            if (manifestEntry != null)
                OverlayManifest(row, manifestEntry);

            RecordShapeIngestionAudit(
                row.topic,
                row.domain,
                row.sourceSite,
                row.sourceLicense,
                row.attribution,
                GetRelativeGeometryPath(path),
                string.IsNullOrWhiteSpace(row.sourceLicense) ? "missing_license" : "imported",
                $"Imported geometry descriptor from {Path.GetFileName(path)}.",
                row.importPriority,
                0f
            );

            return PersistImportedDescriptor(row, path) ? 1 : 0;
        }
        catch (Exception ex)
        {
            RecordShapeIngestionAudit(
                DeriveTopicFromFilename(path),
                "geometry_library",
                null,
                null,
                null,
                GetRelativeGeometryPath(path),
                "import_failed",
                ex.Message,
                50,
                0f
            );
            Debug.LogWarning($"[ShapeKnowledge] Failed to import OBJ geometry '{path}': {ex.Message}");
            return 0;
        }
    }

    private bool PersistImportedDescriptor(ShapeDatabaseRecord row, string sourcePath)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.topic))
            return false;

        var descriptor = ConvertDatabaseRecord(row, sourcePath);
        string normalizedTopic = Normalize(row.topic);
        string normalizedDomain = string.IsNullOrWhiteSpace(row.domain) ? "general" : Normalize(row.domain);
        string outputPath = Path.Combine(
            ShapeDescriptorRootPath,
            $"{normalizedTopic}_{normalizedDomain}_descriptor.json"
        );

        File.WriteAllText(outputPath, JsonUtility.ToJson(descriptor, true));
        return true;
    }

    private ShapeKnowledgeRecord BuildShapeKnowledgeRecord(
        KnowledgeRecord record,
        IReadOnlyDictionary<string, VerificationAuditEntry> verificationLookup,
        IReadOnlyList<ShapeFormDescriptor> descriptors
    )
    {
        verificationLookup.TryGetValue(Normalize(record.topic), out var audit);
        ShapeFormDescriptor descriptor = FindBestDescriptor(record, descriptors);

        string verificationState = audit != null && !string.IsNullOrWhiteSpace(audit.finalState)
            ? audit.finalState.Trim().ToLowerInvariant()
            : "unverified";

        float confidence = ComputeShapeConfidence(record, audit, descriptor);
        var profile = BuildShapeProfile(record, verificationState, confidence, audit, descriptor);

        return new ShapeKnowledgeRecord
        {
            topic = record.topic,
            domain = string.IsNullOrWhiteSpace(record.domain) ? "general" : record.domain,
            knowledgeRecordId = record.id,
            descriptorId = descriptor?.descriptorId,
            verificationState = verificationState,
            sourceUrl = record.sourceUrl,
            summary = record.summary,
            evidence = record.evidence != null ? new List<string>(record.evidence) : new List<string>(),
            tags = BuildMergedTags(record, descriptor),
            confidence = confidence,
            shapeProfile = profile
        };
    }

    private ArTusShapeProfile BuildShapeProfile(
        KnowledgeRecord record,
        string verificationState,
        float confidence,
        VerificationAuditEntry audit,
        ShapeFormDescriptor descriptor
    )
    {
        string semanticText = string.Join(
            " ",
            new[]
            {
                record.topic,
                record.domain,
                record.summary,
                record.rawPayload,
                string.Join(" ", record.tags ?? new List<string>()),
                string.Join(" ", record.evidence ?? new List<string>())
            }.Where(value => !string.IsNullOrWhiteSpace(value))
        ).ToLowerInvariant();

        Vector3 axisWeights = descriptor != null
            ? NormalizeAxisWeights(descriptor.axisWeights)
            : DetermineAxisWeights(semanticText, record.domain);
        float complexity = descriptor != null
            ? Mathf.Clamp01(Mathf.Max(descriptor.complexity, DetermineComplexity(record, semanticText)))
            : DetermineComplexity(record, semanticText);
        float stability = descriptor != null
            ? Mathf.Clamp01(Mathf.Lerp(DetermineStability(verificationState, confidence, complexity), descriptor.stability, 0.65f))
            : DetermineStability(verificationState, confidence, complexity);
        string symbolicMeaning = descriptor != null && !string.IsNullOrWhiteSpace(descriptor.symbolicMeaning)
            ? $"{descriptor.symbolicMeaning} | verification: {verificationState}"
            : BuildSymbolicMeaning(record, verificationState);
        string category = descriptor != null && !string.IsNullOrWhiteSpace(descriptor.archetype)
            ? $"Knowledge/{descriptor.archetype}"
            : $"Knowledge/{(string.IsNullOrWhiteSpace(record.domain) ? "General" : record.domain)}";

        return new ArTusShapeProfile
        {
            shapeId = $"knowledge_{Normalize(record.topic)}_{Normalize(record.domain)}",
            displayName = BuildDisplayName(record.topic),
            category = category,
            archetype = descriptor != null && !string.IsNullOrWhiteSpace(descriptor.archetype)
                ? descriptor.archetype
                : Normalize(record.domain),
            symbolicMeaning = symbolicMeaning,
            stability = stability,
            complexity = complexity,
            confidence = confidence,
            stretchAxisWeights = axisWeights,
            baseScale = descriptor != null ? descriptor.baseScale : Vector3.Lerp(Vector3.one * 0.9f, Vector3.one * 1.3f, complexity),
            pulseStrength = descriptor != null ? descriptor.pulseStrength : DeterminePulse(semanticText, record, verificationState),
            rippleStrength = descriptor != null ? descriptor.rippleStrength : DetermineRipple(semanticText, record),
            orbitStrength = descriptor != null ? descriptor.orbitStrength : DetermineOrbit(semanticText, record),
            twistStrength = descriptor != null ? descriptor.twistStrength : DetermineTwist(semanticText, verificationState),
            taperStrength = descriptor != null ? descriptor.taperStrength : DetermineTaper(semanticText),
            emotionalAffinityCuriosity = descriptor != null ? descriptor.emotionalAffinityCuriosity : Mathf.Clamp01(complexity + 0.1f),
            emotionalAffinityThinking = descriptor != null ? descriptor.emotionalAffinityThinking : Mathf.Clamp01(stability + 0.05f),
            emotionalAffinityConflict = descriptor != null ? descriptor.emotionalAffinityConflict : verificationState == "conflicted" ? 0.85f : 0.25f,
            emotionalAffinityCalm = descriptor != null ? descriptor.emotionalAffinityCalm : verificationState == "verified" ? Mathf.Clamp01(stability + 0.1f) : 0.25f,
            emotionalAffinityJoy = descriptor != null ? descriptor.emotionalAffinityJoy : confidence > 0.8f ? 0.65f : 0.25f,
            isKnowledgeDerived = true,
            learnedTopic = record.topic,
            learnedDomain = string.IsNullOrWhiteSpace(record.domain) ? "general" : record.domain,
            sourceKnowledgeId = record.id,
            verificationState = verificationState,
            sourceTags = BuildMergedTags(record, descriptor),
            reconstructionScore = audit != null
                ? Mathf.Clamp01(audit.supportingEvidenceCount / 4f)
                : Mathf.Clamp01((record.evidence?.Count ?? 0) / 4f)
        };
    }

    private void TeachKnownShapes(IEnumerable<ShapeKnowledgeRecord> records)
    {
        if (shapeIntelligence == null)
            return;

        foreach (var record in records ?? Enumerable.Empty<ShapeKnowledgeRecord>())
        {
            if (record?.shapeProfile == null)
                continue;

            shapeIntelligence.LearnShape(record.shapeProfile);
        }
    }

    private void PersistShapeKnowledge()
    {
        try
        {
            EnsureStorageDirectory();
            File.WriteAllText(
                ShapeKnowledgePath,
                JsonUtility.ToJson(cachedShapeKnowledge ?? new ShapeKnowledgeWrapper(), true)
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to persist shape knowledge: {ex.Message}");
        }
    }

    private void EnsureStorageDirectory()
    {
        try
        {
            string dir = Path.GetDirectoryName(ShapeKnowledgePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to prepare storage: {ex.Message}");
        }
    }

    private float ScoreKnowledgeShape(ShapeKnowledgeRecord record)
    {
        if (record == null)
            return 0f;

        float score = record.confidence;
        score += Mathf.Min(0.2f, (record.evidence?.Count ?? 0) * 0.04f);

        if (string.Equals(record.verificationState, "verified", StringComparison.OrdinalIgnoreCase))
            score += 0.2f;
        else if (string.Equals(record.verificationState, "single_source", StringComparison.OrdinalIgnoreCase))
            score += 0.05f;
        else if (string.Equals(record.verificationState, "conflicted", StringComparison.OrdinalIgnoreCase))
            score -= 0.08f;

        return score;
    }

    private float ComputeShapeConfidence(KnowledgeRecord record, VerificationAuditEntry audit, ShapeFormDescriptor descriptor)
    {
        float confidence = Mathf.Clamp01(record.confidence);
        confidence += Mathf.Min(0.15f, (record.evidence?.Count ?? 0) * 0.04f);

        if (audit != null)
        {
            confidence = Mathf.Max(confidence, Mathf.Clamp01(audit.confidence));

            if (string.Equals(audit.finalState, "verified", StringComparison.OrdinalIgnoreCase))
                confidence += 0.15f;
            else if (string.Equals(audit.finalState, "single_source", StringComparison.OrdinalIgnoreCase))
                confidence += 0.05f;
            else if (string.Equals(audit.finalState, "conflicted", StringComparison.OrdinalIgnoreCase))
                confidence -= 0.08f;
        }

        if (descriptor != null)
            confidence = Mathf.Clamp01(Mathf.Lerp(confidence, descriptor.confidence, 0.5f) + 0.05f);

        if (preferVerifiedTopics && audit == null)
            confidence -= 0.05f;

        return Mathf.Clamp01(confidence);
    }

    private ShapeFormDescriptor FindBestDescriptor(KnowledgeRecord record, IReadOnlyList<ShapeFormDescriptor> descriptors)
    {
        if (record == null || descriptors == null || descriptors.Count == 0)
            return null;

        string normalizedTopic = Normalize(record.topic);
        string normalizedDomain = Normalize(record.domain);

        return descriptors
            .Where(descriptor =>
                descriptor != null &&
                !string.IsNullOrWhiteSpace(descriptor.topic) &&
                string.Equals(Normalize(descriptor.topic), normalizedTopic, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(descriptor.domain) ||
                 string.Equals(Normalize(descriptor.domain), normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Normalize(descriptor.domain), "general", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(descriptor => descriptor.confidence)
            .FirstOrDefault();
    }

    private ShapeFormDescriptor ConvertDatabaseRecord(ShapeDatabaseRecord row, string sourcePath)
    {
        string domain = string.IsNullOrWhiteSpace(row.domain) ? "general" : row.domain;
        return new ShapeFormDescriptor
        {
            topic = row.topic?.Trim(),
            domain = domain.Trim(),
            archetype = string.IsNullOrWhiteSpace(row.archetype) ? "database_form" : row.archetype.Trim(),
            symbolicMeaning = row.symbolicMeaning,
            sourceKind = string.IsNullOrWhiteSpace(row.sourceKind) ? "database" : row.sourceKind.Trim(),
            sourcePath = string.IsNullOrWhiteSpace(row.sourcePath) ? sourcePath : row.sourcePath.Trim(),
            sourceSite = row.sourceSite,
            sourceLicense = row.sourceLicense,
            attribution = row.attribution,
            importPriority = Mathf.Max(0, row.importPriority),
            notes = row.notes,
            axisWeights = NormalizeAxisWeights(new Vector3(row.axisX, row.axisY, row.axisZ)),
            baseScale = new Vector3(
                Mathf.Max(0.1f, row.baseScaleX),
                Mathf.Max(0.1f, row.baseScaleY),
                Mathf.Max(0.1f, row.baseScaleZ)
            ),
            stability = Mathf.Clamp01(row.stability),
            complexity = Mathf.Clamp01(row.complexity),
            confidence = Mathf.Clamp01(row.confidence),
            pulseStrength = Mathf.Clamp01(row.pulseStrength),
            rippleStrength = Mathf.Clamp01(row.rippleStrength),
            orbitStrength = Mathf.Clamp01(row.orbitStrength),
            twistStrength = Mathf.Clamp01(row.twistStrength),
            taperStrength = Mathf.Clamp01(row.taperStrength),
            emotionalAffinityCuriosity = Mathf.Clamp01(row.emotionalAffinityCuriosity),
            emotionalAffinityThinking = Mathf.Clamp01(row.emotionalAffinityThinking),
            emotionalAffinityConflict = Mathf.Clamp01(row.emotionalAffinityConflict),
            emotionalAffinityCalm = Mathf.Clamp01(row.emotionalAffinityCalm),
            emotionalAffinityJoy = Mathf.Clamp01(row.emotionalAffinityJoy),
            tags = ParseDelimitedTags(row.tags),
            targetReconstructionScore = 0.75f,
            lastObservedScore = 0f,
            lastRefinedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private ShapeDatabaseRecord BuildDatabaseRecordFromCsv(string[] headers, string[] values)
    {
        if (headers == null || values == null)
            return null;

        var row = new ShapeDatabaseRecord();

        for (int i = 0; i < headers.Length && i < values.Length; i++)
        {
            string header = headers[i]?.Trim();
            string value = values[i]?.Trim();

            if (string.IsNullOrWhiteSpace(header))
                continue;

            switch (header.ToLowerInvariant())
            {
                case "topic": row.topic = value; break;
                case "domain": row.domain = value; break;
                case "archetype": row.archetype = value; break;
                case "symbolicmeaning": row.symbolicMeaning = value; break;
                case "sourcekind": row.sourceKind = value; break;
                case "sourcepath": row.sourcePath = value; break;
                case "notes": row.notes = value; break;
                case "tags": row.tags = value; break;
                case "axisx": row.axisX = ParseFloat(value, row.axisX); break;
                case "axisy": row.axisY = ParseFloat(value, row.axisY); break;
                case "axisz": row.axisZ = ParseFloat(value, row.axisZ); break;
                case "basescalex": row.baseScaleX = ParseFloat(value, row.baseScaleX); break;
                case "basescaley": row.baseScaleY = ParseFloat(value, row.baseScaleY); break;
                case "basescalez": row.baseScaleZ = ParseFloat(value, row.baseScaleZ); break;
                case "stability": row.stability = ParseFloat(value, row.stability); break;
                case "complexity": row.complexity = ParseFloat(value, row.complexity); break;
                case "confidence": row.confidence = ParseFloat(value, row.confidence); break;
                case "pulsestrength": row.pulseStrength = ParseFloat(value, row.pulseStrength); break;
                case "ripplestrength": row.rippleStrength = ParseFloat(value, row.rippleStrength); break;
                case "orbitstrength": row.orbitStrength = ParseFloat(value, row.orbitStrength); break;
                case "twiststrength": row.twistStrength = ParseFloat(value, row.twistStrength); break;
                case "taperstrength": row.taperStrength = ParseFloat(value, row.taperStrength); break;
                case "emotionalaffinitycuriosity": row.emotionalAffinityCuriosity = ParseFloat(value, row.emotionalAffinityCuriosity); break;
                case "emotionalaffinitythinking": row.emotionalAffinityThinking = ParseFloat(value, row.emotionalAffinityThinking); break;
                case "emotionalaffinityconflict": row.emotionalAffinityConflict = ParseFloat(value, row.emotionalAffinityConflict); break;
                case "emotionalaffinitycalm": row.emotionalAffinityCalm = ParseFloat(value, row.emotionalAffinityCalm); break;
                case "emotionalaffinityjoy": row.emotionalAffinityJoy = ParseFloat(value, row.emotionalAffinityJoy); break;
            }
        }

        return string.IsNullOrWhiteSpace(row.topic) ? null : row;
    }

    private static List<string> BuildMergedTags(KnowledgeRecord record, ShapeFormDescriptor descriptor)
    {
        var tags = new List<string>();

        if (record?.tags != null)
            tags.AddRange(record.tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));

        if (descriptor?.tags != null)
            tags.AddRange(descriptor.tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));

        if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.archetype))
            tags.Add($"archetype:{descriptor.archetype}");

        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ParseDelimitedTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value
            .Split(new[] { '|', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, out float parsed) ? parsed : fallback;
    }

    private static string[] SplitCsvLine(string line)
    {
        var values = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in line ?? string.Empty)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static Vector3 NormalizeAxisWeights(Vector3 axis)
    {
        Vector3 safe = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(axis.x)),
            Mathf.Max(0.01f, Mathf.Abs(axis.y)),
            Mathf.Max(0.01f, Mathf.Abs(axis.z))
        );

        float total = safe.x + safe.y + safe.z;
        return total <= 0.0001f ? new Vector3(0.33f, 0.34f, 0.33f) : safe / total;
    }

    private static Vector3 DetermineAxisWeights(string semanticText, string domain)
    {
        Vector3 axis = new Vector3(0.33f, 0.34f, 0.33f);

        if (ContainsAny(semanticText, "tree", "growth", "tower", "vertical", "rise", "helix", "spiral"))
            axis += new Vector3(0.05f, 0.45f, 0.05f);

        if (ContainsAny(semanticText, "network", "system", "grid", "platform", "landscape", "field"))
            axis += new Vector3(0.3f, 0.05f, 0.3f);

        if (ContainsAny(semanticText, "shield", "security", "defense", "core", "kernel"))
            axis += new Vector3(0.18f, 0.18f, 0.18f);

        if (ContainsAny(semanticText, "wave", "energy", "signal", "flow", "water"))
            axis += new Vector3(0.12f, 0.06f, 0.24f);

        if (string.Equals(domain, "verification", StringComparison.OrdinalIgnoreCase))
            axis += new Vector3(0.08f, 0.16f, 0.08f);

        float total = Mathf.Max(0.0001f, axis.x + axis.y + axis.z);
        return new Vector3(axis.x / total, axis.y / total, axis.z / total);
    }

    private static float DetermineComplexity(KnowledgeRecord record, string semanticText)
    {
        float evidenceWeight = Mathf.Clamp01((record.evidence?.Count ?? 0) / 4f);
        float tagWeight = Mathf.Clamp01((record.tags?.Count ?? 0) / 6f);
        float textWeight = Mathf.Clamp01((semanticText?.Length ?? 0) / 500f);
        return Mathf.Clamp01(0.2f + (evidenceWeight * 0.4f) + (tagWeight * 0.2f) + (textWeight * 0.2f));
    }

    private static float DetermineStability(string verificationState, float confidence, float complexity)
    {
        float stability = confidence;

        if (string.Equals(verificationState, "verified", StringComparison.OrdinalIgnoreCase))
            stability += 0.15f;
        else if (string.Equals(verificationState, "conflicted", StringComparison.OrdinalIgnoreCase))
            stability -= 0.2f;

        stability -= complexity * 0.15f;
        return Mathf.Clamp01(stability);
    }

    private static float DeterminePulse(string semanticText, KnowledgeRecord record, string verificationState)
    {
        float pulse = 0.2f + Mathf.Min(0.25f, (record.evidence?.Count ?? 0) * 0.05f);

        if (ContainsAny(semanticText, "energy", "signal", "pulse", "heartbeat", "rhythm"))
            pulse += 0.3f;

        if (string.Equals(verificationState, "conflicted", StringComparison.OrdinalIgnoreCase))
            pulse += 0.15f;

        return Mathf.Clamp01(pulse);
    }

    private static float DetermineRipple(string semanticText, KnowledgeRecord record)
    {
        float ripple = 0.15f + Mathf.Min(0.2f, (record.tags?.Count ?? 0) * 0.03f);

        if (ContainsAny(semanticText, "wave", "field", "ocean", "diffusion", "spread"))
            ripple += 0.35f;

        return Mathf.Clamp01(ripple);
    }

    private static float DetermineOrbit(string semanticText, KnowledgeRecord record)
    {
        float orbit = 0.1f;

        if (ContainsAny(semanticText, "loop", "cycle", "system", "network", "orbit", "feedback"))
            orbit += 0.45f;

        orbit += Mathf.Min(0.15f, (record.evidence?.Count ?? 0) * 0.03f);
        return Mathf.Clamp01(orbit);
    }

    private static float DetermineTwist(string semanticText, string verificationState)
    {
        float twist = 0.12f;

        if (ContainsAny(semanticText, "spiral", "helix", "dna", "twist", "torsion", "conflict"))
            twist += 0.45f;

        if (string.Equals(verificationState, "conflicted", StringComparison.OrdinalIgnoreCase))
            twist += 0.18f;

        return Mathf.Clamp01(twist);
    }

    private static float DetermineTaper(string semanticText)
    {
        float taper = 0.1f;

        if (ContainsAny(semanticText, "tree", "tower", "cone", "focus", "vector"))
            taper += 0.35f;

        return Mathf.Clamp01(taper);
    }

    private static string BuildDisplayName(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "Knowledge Form";

        string cleaned = topic.Replace("_", " ").Trim();
        return $"{cleaned} Form";
    }

    private static string BuildSymbolicMeaning(KnowledgeRecord record, string verificationState)
    {
        string summary = string.IsNullOrWhiteSpace(record.summary)
            ? record.topic
            : record.summary;

        return $"{summary} | verification: {verificationState}";
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text) || tokens == null)
            return false;

        return tokens.Any(token =>
            !string.IsNullOrWhiteSpace(token) &&
            text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant().Replace(" ", "_");
    }

    private static DateTime ParseAuditDate(string value)
    {
        return DateTime.TryParse(value, out var parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private List<ShapeAnalyticsRow> BuildAnalyticsRows()
    {
        var knowledgeEntries = GetShapeKnowledgeEntries();
        var descriptors = GetShapeFormDescriptors();
        var knownShapes = shapeIntelligence != null
            ? shapeIntelligence.GetKnownShapes()
            : new List<ArTusShapeProfile>();
        var activeShape = morphController != null
            ? morphController.GetActiveShapeProfile()
            : null;

        var rows = new List<ShapeAnalyticsRow>();

        foreach (var entry in knowledgeEntries.Where(entry => entry != null && entry.shapeProfile != null))
        {
            var profile = knownShapes.FirstOrDefault(shape =>
                shape != null &&
                string.Equals(shape.shapeId, entry.shapeProfile.shapeId, StringComparison.OrdinalIgnoreCase))
                ?? entry.shapeProfile;

            var descriptor = descriptors.FirstOrDefault(item =>
                item != null &&
                !string.IsNullOrWhiteSpace(item.topic) &&
                string.Equals(Normalize(item.topic), Normalize(entry.topic), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Normalize(item.domain), Normalize(entry.domain), StringComparison.OrdinalIgnoreCase));

            bool isActive = activeShape != null &&
                string.Equals(activeShape.shapeId, profile.shapeId, StringComparison.OrdinalIgnoreCase);

            rows.Add(new ShapeAnalyticsRow
            {
                topic = entry.topic,
                domain = entry.domain,
                shapeId = profile.shapeId,
                displayName = profile.displayName,
                verificationState = entry.verificationState,
                shapeConfidence = profile.confidence,
                reconstructionScore = profile.reconstructionScore,
                successfulReproductions = profile.successfulReproductions,
                timesLearned = profile.timesLearned,
                descriptorConfidence = descriptor?.confidence ?? 0f,
                descriptorArchetype = descriptor?.archetype,
                descriptorSourceSite = descriptor?.sourceSite,
                descriptorSourceLicense = descriptor?.sourceLicense,
                descriptorAttribution = descriptor?.attribution,
                descriptorImportPriority = descriptor?.importPriority ?? 0,
                descriptorRefinementCount = descriptor?.refinementCount ?? 0,
                descriptorTargetReconstructionScore = descriptor?.targetReconstructionScore ?? 0f,
                descriptorLastObservedScore = descriptor?.lastObservedScore ?? 0f,
                descriptorLastRefinedAt = descriptor?.lastRefinedAt,
                lastScaleScore = ResolveLastReconstructionScore(profile.shapeId, shapeReconstruction?.GetLastScaleScore() ?? 0f),
                lastMotionScore = ResolveLastReconstructionScore(profile.shapeId, shapeReconstruction?.GetLastMotionScore() ?? 0f),
                lastStabilityScore = ResolveLastReconstructionScore(profile.shapeId, shapeReconstruction?.GetLastStabilityScore() ?? 0f),
                lastFinalScore = ResolveLastReconstructionScore(profile.shapeId, shapeReconstruction?.GetLastFinalScore() ?? 0f),
                isActive = isActive,
                currentKnowledgeTopic = shapeIntelligence?.GetCurrentKnowledgeTopic(),
                currentKnowledgeDomain = shapeIntelligence?.GetCurrentKnowledgeDomain(),
                currentVerificationState = shapeIntelligence?.GetCurrentVerificationState(),
                symbolicMeaning = profile.symbolicMeaning
            });
        }

        return rows
            .OrderByDescending(row => row.reconstructionScore + row.shapeConfidence)
            .ToList();
    }

    private float ResolveLastReconstructionScore(string shapeId, float value)
    {
        if (shapeReconstruction == null)
            return 0f;

        return string.Equals(shapeReconstruction.GetLastEvaluatedShapeId(), shapeId, StringComparison.OrdinalIgnoreCase)
            ? value
            : 0f;
    }

    private GeometryImportMetrics AnalyzeObjGeometry(string path)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();

        foreach (string rawLine in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            string line = rawLine.Trim();
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                if (TryParseObjVector(line.Substring(2), out Vector3 vertex))
                    vertices.Add(vertex);
            }
            else if (line.StartsWith("vn ", StringComparison.Ordinal))
            {
                if (TryParseObjVector(line.Substring(3), out Vector3 normal))
                    normals.Add(normal.normalized);
            }
        }

        if (vertices.Count < 3)
            return null;

        Vector3 min = vertices[0];
        Vector3 max = vertices[0];

        foreach (Vector3 vertex in vertices)
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        Vector3 size = max - min;
        float x = Mathf.Max(size.x, 0.0001f);
        float y = Mathf.Max(size.y, 0.0001f);
        float z = Mathf.Max(size.z, 0.0001f);
        float total = x + y + z;
        Vector3 axisWeights = total <= 0.0001f
            ? new Vector3(0.33f, 0.34f, 0.33f)
            : new Vector3(x / total, y / total, z / total);

        float complexity = Mathf.Clamp01(vertices.Count / 5000f);
        float symmetry = EstimateVertexSymmetry(vertices);
        float curvature = EstimateNormalCurvature(normals);
        float hollowness = EstimateGeometryHollowness(size, vertices.Count);

        return new GeometryImportMetrics
        {
            size = size,
            axisWeights = axisWeights,
            complexity = complexity,
            symmetry = symmetry,
            curvature = curvature,
            hollowness = hollowness,
            vertexCount = vertices.Count,
            normalCount = normals.Count
        };
    }

    private ShapeDatabaseRecord LoadGeometryMetadata(string geometryPath)
    {
        string sidecarPath = Path.ChangeExtension(geometryPath, ".json");
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            string json = File.ReadAllText(sidecarPath);
            var wrapper = JsonUtility.FromJson<ShapeDatabaseRecordWrapper>(json);
            if (wrapper?.entries != null && wrapper.entries.Count > 0)
                return wrapper.entries.FirstOrDefault(entry => entry != null);

            var single = JsonUtility.FromJson<ShapeDatabaseRecord>(json);
            return single != null && !string.IsNullOrWhiteSpace(single.topic) ? single : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to load geometry metadata '{sidecarPath}': {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, ShapeIngestionManifestEntry> LoadGeometryManifestLookup()
    {
        var lookup = new Dictionary<string, ShapeIngestionManifestEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in LoadGeometryManifestEntries())
        {
            if (entry == null || !entry.enabled)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.relativePath))
                lookup[NormalizeManifestPath(entry.relativePath)] = entry;

            if (!string.IsNullOrWhiteSpace(entry.fileName))
                lookup[NormalizeManifestPath(entry.fileName)] = entry;
        }

        return lookup;
    }

    private void LoadIngestionAudit()
    {
        try
        {
            string dir = Path.GetDirectoryName(IngestionAuditPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(IngestionAuditPath))
            {
                ingestionAudit = new ShapeIngestionAuditWrapper();
                return;
            }

            string json = File.ReadAllText(IngestionAuditPath);
            ingestionAudit = JsonUtility.FromJson<ShapeIngestionAuditWrapper>(json)
                ?? new ShapeIngestionAuditWrapper();
        }
        catch (Exception ex)
        {
            ingestionAudit = new ShapeIngestionAuditWrapper();
            Debug.LogWarning($"[ShapeKnowledge] Failed to load ingestion audit: {ex.Message}");
        }
    }

    private void PersistIngestionAudit()
    {
        try
        {
            string dir = Path.GetDirectoryName(IngestionAuditPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(
                IngestionAuditPath,
                JsonUtility.ToJson(ingestionAudit ?? new ShapeIngestionAuditWrapper(), true)
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to persist ingestion audit: {ex.Message}");
        }
    }

    private List<ShapeIngestionManifestEntry> LoadGeometryManifestEntries()
    {
        if (!File.Exists(GeometryManifestPath))
            return new List<ShapeIngestionManifestEntry>();

        try
        {
            string json = File.ReadAllText(GeometryManifestPath);
            var manifest = JsonUtility.FromJson<ShapeIngestionManifest>(json);
            return manifest?.entries ?? new List<ShapeIngestionManifestEntry>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShapeKnowledge] Failed to load geometry manifest: {ex.Message}");
            return new List<ShapeIngestionManifestEntry>();
        }
    }

    private ShapeIngestionManifestEntry FindManifestEntryForGeometry(
        string geometryPath,
        IReadOnlyDictionary<string, ShapeIngestionManifestEntry> lookup
    )
    {
        if (lookup == null || lookup.Count == 0 || string.IsNullOrWhiteSpace(geometryPath))
            return null;

        string relativePath = NormalizeManifestPath(GetRelativeGeometryPath(geometryPath));
        if (!string.IsNullOrWhiteSpace(relativePath) && lookup.TryGetValue(relativePath, out var directMatch))
            return directMatch;

        string fileName = NormalizeManifestPath(Path.GetFileName(geometryPath));
        if (!string.IsNullOrWhiteSpace(fileName) && lookup.TryGetValue(fileName, out var fileMatch))
            return fileMatch;

        return null;
    }

    private List<ShapeFormDescriptor> BuildProceduralSeedDescriptors()
    {
        return new List<ShapeFormDescriptor>
        {
            CreateProceduralSeed("sphere", "geometry_primitive", "sphere", "Unity, completeness, calm center",
                new Vector3(0.33f, 0.34f, 0.33f), Vector3.one, 0.92f, 0.18f, 0.75f, 0.18f, 0.08f, 0.15f, 0.06f),
            CreateProceduralSeed("cube", "geometry_primitive", "cube", "Structure, certainty, stability",
                new Vector3(0.33f, 0.34f, 0.33f), Vector3.one * 0.95f, 0.95f, 0.22f, 0.78f, 0.08f, 0.04f, 0.08f, 0.12f),
            CreateProceduralSeed("cylinder", "geometry_primitive", "cylinder", "Flow contained by order",
                new Vector3(0.2f, 0.6f, 0.2f), new Vector3(0.9f, 1.25f, 0.9f), 0.82f, 0.32f, 0.72f, 0.18f, 0.16f, 0.22f, 0.24f),
            CreateProceduralSeed("cone", "geometry_primitive", "cone", "Focus, direction, convergence",
                new Vector3(0.18f, 0.64f, 0.18f), new Vector3(0.9f, 1.3f, 0.9f), 0.72f, 0.34f, 0.68f, 0.22f, 0.12f, 0.16f, 0.58f),
            CreateProceduralSeed("torus", "geometry_primitive", "torus", "Circulation, continuity, recursive thought",
                new Vector3(0.36f, 0.28f, 0.36f), new Vector3(1.2f, 0.85f, 1.2f), 0.8f, 0.48f, 0.86f, 0.34f, 0.28f, 0.62f, 0.16f),
            CreateProceduralSeed("helix", "geometry_primitive", "helix", "Ascent, growth, structured curiosity",
                new Vector3(0.18f, 0.62f, 0.2f), new Vector3(0.9f, 1.4f, 0.9f), 0.58f, 0.84f, 0.7f, 0.26f, 0.34f, 0.32f, 0.22f, 0.82f),
            CreateProceduralSeed("lattice", "geometry_primitive", "lattice", "Networked intelligence, connected systems",
                new Vector3(0.34f, 0.32f, 0.34f), new Vector3(1.2f, 1.1f, 1.2f), 0.66f, 0.72f, 0.69f, 0.18f, 0.42f, 0.56f, 0.18f),
            CreateProceduralSeed("star", "geometry_symbolic", "star", "Radiance, signaling, emergence",
                new Vector3(0.34f, 0.32f, 0.34f), new Vector3(1.15f, 1.15f, 1.15f), 0.52f, 0.74f, 0.63f, 0.3f, 0.46f, 0.24f, 0.28f),
            CreateProceduralSeed("shell", "geometry_symbolic", "shell", "Layered memory, protective recursion",
                new Vector3(0.26f, 0.42f, 0.32f), new Vector3(1.05f, 1.2f, 1.0f), 0.6f, 0.78f, 0.61f, 0.24f, 0.38f, 0.28f, 0.34f, 0.62f)
        };
    }

    private ShapeFormDescriptor CreateProceduralSeed(
        string topic,
        string domain,
        string archetype,
        string symbolicMeaning,
        Vector3 axisWeights,
        Vector3 baseScale,
        float stability,
        float complexity,
        float confidence,
        float pulseStrength,
        float rippleStrength,
        float orbitStrength,
        float taperStrength,
        float twistStrength = 0.12f
    )
    {
        string normalizedTopic = Normalize(topic);
        string normalizedDomain = Normalize(domain);

        return new ShapeFormDescriptor
        {
            topic = topic,
            domain = domain,
            archetype = archetype,
            symbolicMeaning = symbolicMeaning,
            sourceKind = "procedural-seed",
            sourcePath = Path.Combine(ShapeDescriptorRootPath, $"{normalizedTopic}_{normalizedDomain}_descriptor.json"),
            sourceSite = "artus-procedural",
            sourceLicense = "internal-seed",
            attribution = "ArTus procedural geometry seeds",
            importPriority = 40,
            notes = "Auto-generated procedural seed descriptor for autonomous geometric learning.",
            axisWeights = NormalizeAxisWeights(axisWeights),
            baseScale = baseScale,
            stability = Mathf.Clamp01(stability),
            complexity = Mathf.Clamp01(complexity),
            confidence = Mathf.Clamp01(confidence),
            pulseStrength = Mathf.Clamp01(pulseStrength),
            rippleStrength = Mathf.Clamp01(rippleStrength),
            orbitStrength = Mathf.Clamp01(orbitStrength),
            twistStrength = Mathf.Clamp01(twistStrength),
            taperStrength = Mathf.Clamp01(taperStrength),
            emotionalAffinityCuriosity = Mathf.Clamp01(0.55f + complexity * 0.3f),
            emotionalAffinityThinking = Mathf.Clamp01(0.5f + stability * 0.3f),
            emotionalAffinityConflict = Mathf.Clamp01(0.15f + twistStrength * 0.25f),
            emotionalAffinityCalm = Mathf.Clamp01(0.25f + stability * 0.45f),
            emotionalAffinityJoy = Mathf.Clamp01(0.2f + orbitStrength * 0.25f),
            tags = new List<string>
            {
                "procedural-seed",
                $"domain:{domain}",
                $"archetype:{archetype}"
            },
            targetReconstructionScore = 0.78f,
            lastObservedScore = 0f,
            lastRefinedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private static bool TryParseObjVector(string value, out Vector3 vector)
    {
        vector = Vector3.zero;
        string[] parts = value
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
            return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            return false;

        vector = new Vector3(x, y, z);
        return true;
    }

    private static float EstimateVertexSymmetry(IReadOnlyList<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 10)
            return 0.5f;

        int samples = Mathf.Min(vertices.Count, 160);
        int symmetricPairs = 0;

        for (int i = 0; i < samples; i++)
        {
            Vector3 vertex = vertices[i];
            Vector3 mirrored = new Vector3(-vertex.x, vertex.y, vertex.z);

            foreach (Vector3 other in vertices)
            {
                if (Vector3.Distance(other, mirrored) < 0.05f)
                {
                    symmetricPairs++;
                    break;
                }
            }
        }

        return Mathf.Clamp01((float)symmetricPairs / samples);
    }

    private static float EstimateNormalCurvature(IReadOnlyList<Vector3> normals)
    {
        if (normals == null || normals.Count < 10)
            return 0.3f;

        float variance = 0f;

        for (int i = 1; i < normals.Count; i++)
            variance += Vector3.Angle(normals[i - 1], normals[i]);

        variance /= normals.Count;
        return Mathf.Clamp01(variance / 180f);
    }

    private static float EstimateGeometryHollowness(Vector3 size, int vertexCount)
    {
        float volume = Mathf.Max(size.x * size.y * size.z, 0.0001f);
        float density = vertexCount / volume;
        return Mathf.Clamp01(1f - (density / 500f));
    }

    private static string ClassifyGeometry(Vector3 axis, float symmetry, float curvature, float hollowness)
    {
        if (symmetry > 0.85f && curvature < 0.3f)
            return "sphere";

        if (hollowness > 0.6f && symmetry > 0.6f)
            return "torus_like";

        if (axis.y > 0.6f)
            return "vertical_form";

        if (axis.x > 0.6f || axis.z > 0.6f)
            return "horizontal_form";

        if (curvature > 0.7f)
            return "organic_complex";

        if (symmetry < 0.3f)
            return "irregular";

        return "hybrid_form";
    }

    private static string BuildGeometryNotes(string path, GeometryImportMetrics metrics, string existingNotes)
    {
        string summary =
            $"Geometry source={Path.GetFileName(path)} vertices={metrics.vertexCount} normals={metrics.normalCount} " +
            $"symmetry={metrics.symmetry:F2} curvature={metrics.curvature:F2} hollow={metrics.hollowness:F2}";

        if (string.IsNullOrWhiteSpace(existingNotes))
            return summary;

        return $"{existingNotes}{Environment.NewLine}{summary}";
    }

    private static string BuildGeometryTags(string path, string classification, params string[] existingTags)
    {
        var tags = new List<string>();
        foreach (string value in existingTags ?? Array.Empty<string>())
            tags.AddRange(ParseDelimitedTags(value));
        tags.Add("geometry-library");
        tags.Add($"file:{Path.GetExtension(path).Trim('.').ToLowerInvariant()}");
        tags.Add($"class:{classification}");
        return string.Join("|", tags.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string DeriveTopicFromFilename(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path) ?? "geometry_form";
        return name.Replace("_", " ").Replace("-", " ").Trim();
    }

    private static string NormalizeArchetypeLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "geometry_form"
            : value.Trim().ToLowerInvariant().Replace(" ", "_").Replace("/", "_");
    }

    private string GetRelativeGeometryPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return string.Empty;

        if (!fullPath.StartsWith(GeometryLibraryImportRootPath, StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(fullPath) ?? string.Empty;

        string relative = fullPath.Substring(GeometryLibraryImportRootPath.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return relative.Replace('\\', '/');
    }

    private static string NormalizeManifestPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().Replace('\\', '/').ToLowerInvariant();
    }

    private static string ResolveManifestLabel(ShapeIngestionManifestEntry entry)
    {
        if (entry == null)
            return "unknown";

        if (!string.IsNullOrWhiteSpace(entry.topic))
            return entry.topic;

        if (!string.IsNullOrWhiteSpace(entry.relativePath))
            return entry.relativePath;

        if (!string.IsNullOrWhiteSpace(entry.fileName))
            return entry.fileName;

        return "unknown";
    }

    private static string ResolveLicenseLabel(ShapeIngestionManifestEntry entry)
    {
        if (entry == null)
            return "license unknown";

        return string.IsNullOrWhiteSpace(entry.sourceLicense)
            ? "license missing"
            : entry.sourceLicense;
    }

    private static void OverlayMetadata(ShapeDatabaseRecord row, ShapeDatabaseRecord metadata)
    {
        if (row == null || metadata == null)
            return;

        if (!string.IsNullOrWhiteSpace(metadata.topic))
            row.topic = metadata.topic;
        if (!string.IsNullOrWhiteSpace(metadata.domain))
            row.domain = metadata.domain;
        if (!string.IsNullOrWhiteSpace(metadata.archetype))
            row.archetype = metadata.archetype;
        if (!string.IsNullOrWhiteSpace(metadata.symbolicMeaning))
            row.symbolicMeaning = metadata.symbolicMeaning;
        if (!string.IsNullOrWhiteSpace(metadata.notes))
            row.notes = string.IsNullOrWhiteSpace(row.notes)
                ? metadata.notes
                : $"{metadata.notes}{Environment.NewLine}{row.notes}";
        if (!string.IsNullOrWhiteSpace(metadata.tags))
            row.tags = BuildGeometryTags(row.sourcePath, row.archetype, metadata.tags);
        if (!string.IsNullOrWhiteSpace(metadata.sourceKind))
            row.sourceKind = metadata.sourceKind;
        if (!string.IsNullOrWhiteSpace(metadata.sourceSite))
            row.sourceSite = metadata.sourceSite;
        if (!string.IsNullOrWhiteSpace(metadata.sourceLicense))
            row.sourceLicense = metadata.sourceLicense;
        if (!string.IsNullOrWhiteSpace(metadata.attribution))
            row.attribution = metadata.attribution;
        row.importPriority = metadata.importPriority > 0 ? metadata.importPriority : row.importPriority;
        row.confidence = metadata.confidence > 0f ? Mathf.Clamp01(metadata.confidence) : row.confidence;
        row.stability = metadata.stability > 0f ? Mathf.Clamp01(metadata.stability) : row.stability;
        row.complexity = metadata.complexity > 0f ? Mathf.Clamp01(metadata.complexity) : row.complexity;
        row.pulseStrength = metadata.pulseStrength > 0f ? Mathf.Clamp01(metadata.pulseStrength) : row.pulseStrength;
        row.rippleStrength = metadata.rippleStrength > 0f ? Mathf.Clamp01(metadata.rippleStrength) : row.rippleStrength;
        row.orbitStrength = metadata.orbitStrength > 0f ? Mathf.Clamp01(metadata.orbitStrength) : row.orbitStrength;
        row.twistStrength = metadata.twistStrength > 0f ? Mathf.Clamp01(metadata.twistStrength) : row.twistStrength;
        row.taperStrength = metadata.taperStrength > 0f ? Mathf.Clamp01(metadata.taperStrength) : row.taperStrength;
    }

    private static void OverlayManifest(ShapeDatabaseRecord row, ShapeIngestionManifestEntry manifestEntry)
    {
        if (row == null || manifestEntry == null)
            return;

        if (!string.IsNullOrWhiteSpace(manifestEntry.topic))
            row.topic = manifestEntry.topic;
        if (!string.IsNullOrWhiteSpace(manifestEntry.domain))
            row.domain = manifestEntry.domain;
        if (!string.IsNullOrWhiteSpace(manifestEntry.archetype))
            row.archetype = manifestEntry.archetype;
        if (!string.IsNullOrWhiteSpace(manifestEntry.symbolicMeaning))
            row.symbolicMeaning = manifestEntry.symbolicMeaning;
        if (!string.IsNullOrWhiteSpace(manifestEntry.sourceSite))
            row.sourceSite = manifestEntry.sourceSite;
        if (!string.IsNullOrWhiteSpace(manifestEntry.sourceLicense))
            row.sourceLicense = manifestEntry.sourceLicense;
        if (!string.IsNullOrWhiteSpace(manifestEntry.attribution))
            row.attribution = manifestEntry.attribution;
        row.importPriority = manifestEntry.importPriority > 0 ? manifestEntry.importPriority : row.importPriority;
        if (!string.IsNullOrWhiteSpace(manifestEntry.notes))
            row.notes = string.IsNullOrWhiteSpace(row.notes)
                ? manifestEntry.notes
                : $"{manifestEntry.notes}{Environment.NewLine}{row.notes}";
        if (!string.IsNullOrWhiteSpace(manifestEntry.tags))
            row.tags = BuildGeometryTags(row.sourcePath, row.archetype, row.tags, manifestEntry.tags);
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private class ShapeAnalyticsRow
    {
        public string topic;
        public string domain;
        public string shapeId;
        public string displayName;
        public string verificationState;
        public float shapeConfidence;
        public float reconstructionScore;
        public int successfulReproductions;
        public int timesLearned;
        public float descriptorConfidence;
        public string descriptorArchetype;
        public string descriptorSourceSite;
        public string descriptorSourceLicense;
        public string descriptorAttribution;
        public int descriptorImportPriority;
        public int descriptorRefinementCount;
        public float descriptorTargetReconstructionScore;
        public float descriptorLastObservedScore;
        public string descriptorLastRefinedAt;
        public float lastScaleScore;
        public float lastMotionScore;
        public float lastStabilityScore;
        public float lastFinalScore;
        public bool isActive;
        public string currentKnowledgeTopic;
        public string currentKnowledgeDomain;
        public string currentVerificationState;
        public string symbolicMeaning;
    }

    private class GeometryImportMetrics
    {
        public Vector3 size;
        public Vector3 axisWeights;
        public float complexity;
        public float symmetry;
        public float curvature;
        public float hollowness;
        public int vertexCount;
        public int normalCount;
    }

    private ShapeFormDescriptor BuildDescriptorFromProfile(
        ArTusShapeProfile profile,
        string topic,
        string domain,
        ShapeKnowledgeRecord knowledge
    )
    {
        string normalizedTopic = string.IsNullOrWhiteSpace(topic) ? Normalize(profile.learnedTopic) : Normalize(topic);
        string normalizedDomain = string.IsNullOrWhiteSpace(domain)
            ? Normalize(profile.learnedDomain)
            : Normalize(domain);

        return new ShapeFormDescriptor
        {
            topic = string.IsNullOrWhiteSpace(topic) ? profile.learnedTopic : topic.Trim(),
            domain = string.IsNullOrWhiteSpace(domain) ? profile.learnedDomain : domain.Trim(),
            archetype = string.IsNullOrWhiteSpace(profile.category) ? "learned_form" : profile.category.Replace("Knowledge/", string.Empty),
            symbolicMeaning = profile.symbolicMeaning,
            sourceKind = "adaptive-learning",
            sourcePath = Path.Combine(ShapeDescriptorRootPath, $"{normalizedTopic}_{normalizedDomain}_descriptor.json"),
            sourceSite = "artus-adaptive",
            sourceLicense = "internal-learning",
            attribution = "ArTus adaptive refinement",
            importPriority = 60,
            notes = "Auto-generated from learned shape profile for reconstruction refinement.",
            axisWeights = NormalizeAxisWeights(profile.stretchAxisWeights),
            baseScale = profile.baseScale,
            stability = profile.stability,
            complexity = profile.complexity,
            confidence = Mathf.Clamp01(Mathf.Max(profile.confidence, knowledge?.confidence ?? 0.5f)),
            pulseStrength = profile.pulseStrength,
            rippleStrength = profile.rippleStrength,
            orbitStrength = profile.orbitStrength,
            twistStrength = profile.twistStrength,
            taperStrength = profile.taperStrength,
            emotionalAffinityCuriosity = profile.emotionalAffinityCuriosity,
            emotionalAffinityThinking = profile.emotionalAffinityThinking,
            emotionalAffinityConflict = profile.emotionalAffinityConflict,
            emotionalAffinityCalm = profile.emotionalAffinityCalm,
            emotionalAffinityJoy = profile.emotionalAffinityJoy,
            tags = profile.sourceTags != null ? new List<string>(profile.sourceTags) : new List<string>(),
            targetReconstructionScore = Mathf.Clamp01(Mathf.Max(0.72f, profile.reconstructionScore)),
            lastObservedScore = profile.reconstructionScore,
            lastRefinedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private void ApplyDescriptorRefinement(ShapeFormDescriptor descriptor, ArTusShapeProfile profile, float observedScore)
    {
        if (descriptor == null || profile == null)
            return;

        float scaleScore = shapeReconstruction != null ? shapeReconstruction.GetLastScaleScore() : observedScore;
        float motionScore = shapeReconstruction != null ? shapeReconstruction.GetLastMotionScore() : observedScore;
        float stabilityScore = shapeReconstruction != null ? shapeReconstruction.GetLastStabilityScore() : observedScore;
        float blend = Mathf.Clamp01(descriptorRefinementBlend);

        Vector3 currentScale = morphController != null ? morphController.GetCurrentScale() : profile.baseScale;
        Vector3 currentAxis = NormalizeAxisWeights(new Vector3(
            Mathf.Max(0.01f, currentScale.x),
            Mathf.Max(0.01f, currentScale.y),
            Mathf.Max(0.01f, currentScale.z)
        ));

        if (scaleScore < descriptorRefinementTrigger)
        {
            descriptor.axisWeights = NormalizeAxisWeights(Vector3.Lerp(descriptor.axisWeights, currentAxis, blend));
            descriptor.baseScale = Vector3.Lerp(descriptor.baseScale, currentScale, blend * 0.8f);
        }

        if (motionScore < descriptorRefinementTrigger && morphController != null)
        {
            descriptor.pulseStrength = Mathf.Lerp(descriptor.pulseStrength, morphController.GetPulseLevel(), blend);
            descriptor.rippleStrength = Mathf.Lerp(descriptor.rippleStrength, morphController.GetRippleLevel(), blend);
            descriptor.twistStrength = Mathf.Lerp(descriptor.twistStrength, morphController.GetTwistLevel(), blend);
            descriptor.orbitStrength = Mathf.Lerp(descriptor.orbitStrength, profile.orbitStrength, blend * 0.5f);
        }

        if (stabilityScore < descriptorRefinementTrigger)
        {
            float observedStability = morphController != null
                ? Mathf.Clamp01(1f - morphController.GetScaleFluctuation())
                : stabilityScore;

            descriptor.stability = Mathf.Lerp(descriptor.stability, observedStability, blend);
        }

        descriptor.complexity = Mathf.Clamp01(Mathf.Lerp(descriptor.complexity, profile.complexity, blend * 0.35f));
        descriptor.confidence = observedScore < descriptorRefinementTrigger
            ? Mathf.Clamp01(descriptor.confidence - descriptorConfidencePenalty)
            : Mathf.Clamp01(descriptor.confidence + descriptorConfidenceRecovery);
        descriptor.targetReconstructionScore = Mathf.Clamp01(Mathf.Max(descriptor.targetReconstructionScore, 0.75f));
        descriptor.lastObservedScore = observedScore;
        descriptor.lastRefinedAt = DateTime.UtcNow.ToString("o");
        descriptor.refinementCount++;
        descriptor.notes = AppendRefinementNote(descriptor.notes, observedScore, scaleScore, motionScore, stabilityScore);
    }

    private void PersistDescriptor(ShapeFormDescriptor descriptor)
    {
        if (descriptor == null)
            return;

        string normalizedTopic = Normalize(descriptor.topic);
        string normalizedDomain = string.IsNullOrWhiteSpace(descriptor.domain) ? "general" : Normalize(descriptor.domain);
        string outputPath = string.IsNullOrWhiteSpace(descriptor.sourcePath)
            ? Path.Combine(ShapeDescriptorRootPath, $"{normalizedTopic}_{normalizedDomain}_descriptor.json")
            : descriptor.sourcePath;

        descriptor.sourcePath = outputPath;
        File.WriteAllText(outputPath, JsonUtility.ToJson(descriptor, true));
    }

    private static string AppendRefinementNote(
        string notes,
        float observedScore,
        float scaleScore,
        float motionScore,
        float stabilityScore
    )
    {
        string entry =
            $"[{DateTime.UtcNow:O}] refinement observed={observedScore:F2} scale={scaleScore:F2} motion={motionScore:F2} stability={stabilityScore:F2}";

        if (string.IsNullOrWhiteSpace(notes))
            return entry;

        string[] lines = notes
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            Environment.NewLine,
            lines.TakeLast(4).Concat(new[] { entry })
        );
    }
}
