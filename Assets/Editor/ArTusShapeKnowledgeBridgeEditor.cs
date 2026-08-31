using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArTusShapeKnowledgeBridge))]
public class ArTusShapeKnowledgeBridgeEditor : Editor
{
    private Vector2 inspectorScroll;
    private string previewTopic = "";
    private string previewDomain = "general";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape Knowledge Tools", EditorStyles.boldLabel);

        var bridge = (ArTusShapeKnowledgeBridge)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync Shapes"))
                bridge.SyncKnowledgeShapes();

            if (GUILayout.Button("Import DB"))
                bridge.ImportShapeDescriptorDatabase();

            if (GUILayout.Button("Import Geometry"))
                bridge.ImportGeometryLibrary();

            if (GUILayout.Button("Export Power BI"))
                bridge.ExportShapeDataForPowerBI();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Procedural Seeds"))
                bridge.GenerateProceduralGeometrySeeds();

            if (GUILayout.Button("Create Import Folder"))
                bridge.CreateShapeDescriptorImportFolder();

            if (GUILayout.Button("Geometry Folder"))
                bridge.CreateGeometryLibraryImportFolder();

            if (GUILayout.Button("Manifest Template"))
                bridge.CreateGeometryManifestTemplate();

            if (GUILayout.Button("CSV Template"))
                bridge.CreateShapeDescriptorCsvTemplate();

            if (GUILayout.Button("Open Window"))
                ArTusShapeKnowledgeBridgeWindow.Open(bridge);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Preview Topic Application", EditorStyles.boldLabel);
        previewTopic = EditorGUILayout.TextField("Topic", previewTopic);
        previewDomain = EditorGUILayout.TextField("Domain", previewDomain);

        if (GUILayout.Button("Apply Shape For Topic"))
        {
            bool applied = bridge.ApplyShapeForTopic(previewTopic, previewDomain);
            Debug.Log(applied
                ? $"[ShapeKnowledgeEditor] Applied shape for '{previewTopic}'."
                : $"[ShapeKnowledgeEditor] No shape found for '{previewTopic}'.");
        }

        if (GUILayout.Button("Refine Descriptor For Topic"))
        {
            bool refined = bridge.RefineShapeDescriptorForTopic(previewTopic, previewDomain);
            Debug.Log(refined
                ? $"[ShapeKnowledgeEditor] Refined descriptor for '{previewTopic}'."
                : $"[ShapeKnowledgeEditor] No descriptor refinement available for '{previewTopic}'.");
        }

        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.Height(260f));
        DrawSummary(bridge);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawSummary(ArTusShapeKnowledgeBridge bridge)
    {
        var knowledgeLines = bridge.GetShapeKnowledgeSummaryLines();
        EditorGUILayout.LabelField("Shape Knowledge", EditorStyles.boldLabel);
        if (knowledgeLines.Count == 0)
        {
            EditorGUILayout.LabelField("No cached shape knowledge yet.");
        }
        else
        {
            foreach (string line in knowledgeLines)
                EditorGUILayout.LabelField("- " + line, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(4f);
        var descriptorLines = bridge.GetDescriptorSummaryLines();
        EditorGUILayout.LabelField("Descriptors", EditorStyles.boldLabel);
        if (descriptorLines.Count == 0)
        {
            EditorGUILayout.LabelField("No descriptors imported yet.");
        }
        else
        {
            foreach (string line in descriptorLines)
                EditorGUILayout.LabelField("- " + line, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(4f);
        var manifestLines = bridge.GetManifestSummaryLines();
        EditorGUILayout.LabelField("Manifest", EditorStyles.boldLabel);
        if (manifestLines.Count == 0)
        {
            EditorGUILayout.LabelField("No manifest entries yet.");
        }
        else
        {
            foreach (string line in manifestLines)
                EditorGUILayout.LabelField("- " + line, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(4f);
        var analyticsLines = bridge.GetAnalyticsSummaryLines();
        EditorGUILayout.LabelField("Analytics", EditorStyles.boldLabel);
        if (analyticsLines.Count == 0)
        {
            EditorGUILayout.LabelField("No analytics rows yet.");
        }
        else
        {
            foreach (string line in analyticsLines)
                EditorGUILayout.LabelField("- " + line, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(4f);
        var auditLines = bridge.GetIngestionAuditSummaryLines();
        EditorGUILayout.LabelField("Ingestion Audit", EditorStyles.boldLabel);
        if (auditLines.Count == 0)
        {
            EditorGUILayout.LabelField("No ingestion audit entries yet.");
        }
        else
        {
            foreach (string line in auditLines)
                EditorGUILayout.LabelField("- " + line, EditorStyles.wordWrappedLabel);
        }
    }
}

public class ArTusShapeKnowledgeBridgeWindow : EditorWindow
{
    private ArTusShapeKnowledgeBridge bridge;
    private Vector2 scroll;
    private string previewTopic = "";
    private string previewDomain = "general";

    [MenuItem("Window/ArTus/Shape Knowledge")]
    public static void OpenWindow()
    {
        GetWindow<ArTusShapeKnowledgeBridgeWindow>("ArTus Shape Knowledge");
    }

    public static void Open(ArTusShapeKnowledgeBridge targetBridge)
    {
        var window = GetWindow<ArTusShapeKnowledgeBridgeWindow>("ArTus Shape Knowledge");
        window.bridge = targetBridge;
        window.Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ArTus Shape Knowledge", EditorStyles.boldLabel);
        bridge = (ArTusShapeKnowledgeBridge)EditorGUILayout.ObjectField(
            "Bridge",
            bridge,
            typeof(ArTusShapeKnowledgeBridge),
            true
        );

        if (bridge == null)
        {
            EditorGUILayout.HelpBox("Assign an ArTusShapeKnowledgeBridge from the scene.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync"))
                bridge.SyncKnowledgeShapes();

            if (GUILayout.Button("Import DB"))
                bridge.ImportShapeDescriptorDatabase();

            if (GUILayout.Button("Import Geometry"))
                bridge.ImportGeometryLibrary();

            if (GUILayout.Button("Export Power BI"))
                bridge.ExportShapeDataForPowerBI();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Procedural Seeds"))
                bridge.GenerateProceduralGeometrySeeds();

            if (GUILayout.Button("Create Import Folder"))
                bridge.CreateShapeDescriptorImportFolder();

            if (GUILayout.Button("Geometry Folder"))
                bridge.CreateGeometryLibraryImportFolder();

            if (GUILayout.Button("Manifest Template"))
                bridge.CreateGeometryManifestTemplate();

            if (GUILayout.Button("CSV Template"))
                bridge.CreateShapeDescriptorCsvTemplate();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Preview Topic", EditorStyles.boldLabel);
        previewTopic = EditorGUILayout.TextField("Topic", previewTopic);
        previewDomain = EditorGUILayout.TextField("Domain", previewDomain);

        if (GUILayout.Button("Apply Shape For Topic"))
            bridge.ApplyShapeForTopic(previewTopic, previewDomain);

        if (GUILayout.Button("Refine Descriptor For Topic"))
            bridge.RefineShapeDescriptorForTopic(previewTopic, previewDomain);

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawSection("Shape Knowledge", bridge.GetShapeKnowledgeSummaryLines(12));
        DrawSection("Descriptors", bridge.GetDescriptorSummaryLines(12));
        DrawSection("Manifest", bridge.GetManifestSummaryLines(12));
        DrawSection("High Priority Imports", bridge.GetManifestHighPriorityLines(8));
        DrawSection("Missing Licenses", bridge.GetManifestMissingLicenseLines(8));
        DrawSection("Weak Learning", bridge.GetManifestWeakLearningLines(8));
        DrawSection("Ingestion Audit", bridge.GetIngestionAuditSummaryLines(12));
        DrawSection("Analytics", bridge.GetAnalyticsSummaryLines(12));

        var descriptors = bridge.GetShapeFormDescriptors()
            .Where(descriptor => descriptor != null)
            .OrderByDescending(descriptor => descriptor.confidence)
            .Take(10)
            .ToList();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Top Descriptor Details", EditorStyles.boldLabel);
        if (descriptors.Count == 0)
        {
            EditorGUILayout.LabelField("No descriptors imported yet.");
        }
        else
        {
            foreach (var descriptor in descriptors)
            {
                EditorGUILayout.LabelField(
                    $"{descriptor.topic} [{descriptor.domain}] / {descriptor.archetype}",
                    EditorStyles.boldLabel
                );
                EditorGUILayout.LabelField(
                    $"Axis {descriptor.axisWeights.x:F2}, {descriptor.axisWeights.y:F2}, {descriptor.axisWeights.z:F2} | " +
                    $"Stability {descriptor.stability:F2} | Complexity {descriptor.complexity:F2} | " +
                    $"Refinements {descriptor.refinementCount}",
                    EditorStyles.wordWrappedLabel
                );
                EditorGUILayout.LabelField(
                    $"Target Recon {descriptor.targetReconstructionScore:F2} | " +
                    $"Last Observed {descriptor.lastObservedScore:F2} | " +
                    $"Last Refined {descriptor.lastRefinedAt}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"Source {descriptor.sourceSite} | License {descriptor.sourceLicense} | Priority {descriptor.importPriority}",
                    EditorStyles.miniLabel
                );
                if (!string.IsNullOrWhiteSpace(descriptor.symbolicMeaning))
                    EditorGUILayout.LabelField(descriptor.symbolicMeaning, EditorStyles.miniLabel);
                EditorGUILayout.Space(2f);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawSection(string title, System.Collections.Generic.List<string> lines)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        if (lines == null || lines.Count == 0)
        {
            EditorGUILayout.LabelField("No data.");
            return;
        }

        foreach (string line in lines)
            EditorGUILayout.LabelField("- " + line, EditorStyles.wordWrappedLabel);
    }
}
