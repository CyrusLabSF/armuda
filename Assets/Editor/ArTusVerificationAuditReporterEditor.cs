using System.Linq;
using UnityEditor;
using UnityEngine;
using ArTusTypes;

[CustomEditor(typeof(ArTusVerificationAuditReporter))]
public class ArTusVerificationAuditReporterEditor : Editor
{
    private Vector2 inspectorScroll;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Verification Tools", EditorStyles.boldLabel);

        var reporter = (ArTusVerificationAuditReporter)target;
        var goalController = reporter.GetComponent<ArTusGoalController>();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Audit"))
            {
                reporter.RefreshAuditReport();
                Repaint();
            }

            if (GUILayout.Button("Export CSV"))
            {
                reporter.ExportVerificationAuditCsv();
            }

            if (GUILayout.Button("Open Window"))
            {
                ArTusVerificationAuditWindow.Open(reporter);
            }
        }

        if (goalController != null && GUILayout.Button("Clear Resolved Audit"))
        {
            int removed = goalController.ClearResolvedVerificationAudit();
            reporter.RefreshAuditReport();
            Debug.Log($"[VerificationAuditEditor] Cleared {removed} verified audit entries.");
        }

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Live data is available while the scene is running."
                : "Enter Play Mode to populate live verification metrics from the runtime reporter.",
            MessageType.Info
        );

        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.Height(260f));
        DrawSummary(reporter);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawSummary(ArTusVerificationAuditReporter reporter)
    {
        string summary = reporter.GetOverlaySummary();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(summary, MessageType.None);
        }

        var priorityTopics = reporter.GetCachedPriorityTopics();
        if (priorityTopics.Count > 0)
        {
            EditorGUILayout.LabelField("Priority Topics", EditorStyles.boldLabel);
            foreach (string topic in priorityTopics.Take(5))
                EditorGUILayout.LabelField("• " + topic);
        }

        var domainLines = reporter.GetDomainSuccessLines();
        if (domainLines.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Domain Success", EditorStyles.boldLabel);
            foreach (string line in domainLines)
                EditorGUILayout.LabelField("• " + line);
        }

        var hotspotLines = reporter.GetHotTopicLines();
        if (hotspotLines.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Conflict Hotspots", EditorStyles.boldLabel);
            foreach (string line in hotspotLines)
                EditorGUILayout.LabelField("• " + line);
        }

        var entries = reporter.GetCachedEntries();
        if (entries.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Recent Outcomes", EditorStyles.boldLabel);
            foreach (VerificationAuditEntry entry in entries
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.completedAt)
                .Take(5))
            {
                EditorGUILayout.LabelField(
                    $"• {entry.topic}: {entry.requestedState} -> {entry.finalState} ({entry.confidence:F2})"
                );
            }
        }
    }
}

public class ArTusVerificationAuditWindow : EditorWindow
{
    private ArTusVerificationAuditReporter reporter;
    private Vector2 scroll;

    [MenuItem("Window/ArTus/Verification Audit")]
    public static void OpenWindow()
    {
        GetWindow<ArTusVerificationAuditWindow>("ArTus Verification Audit");
    }

    public static void Open(ArTusVerificationAuditReporter reporter)
    {
        var window = GetWindow<ArTusVerificationAuditWindow>("ArTus Verification Audit");
        window.reporter = reporter;
        window.Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ArTus Verification Audit", EditorStyles.boldLabel);
        reporter = (ArTusVerificationAuditReporter)EditorGUILayout.ObjectField(
            "Reporter",
            reporter,
            typeof(ArTusVerificationAuditReporter),
            true
        );

        if (reporter == null)
        {
            EditorGUILayout.HelpBox("Assign an ArTusVerificationAuditReporter from the scene.", MessageType.Info);
            return;
        }

        var goalController = reporter.GetComponent<ArTusGoalController>();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh"))
            {
                reporter.RefreshAuditReport();
            }

            if (GUILayout.Button("Export CSV"))
            {
                reporter.ExportVerificationAuditCsv();
            }

            if (goalController != null && GUILayout.Button("Clear Resolved"))
            {
                goalController.ClearResolvedVerificationAudit();
                reporter.RefreshAuditReport();
            }
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        string summary = reporter.GetOverlaySummary();
        if (!string.IsNullOrWhiteSpace(summary))
            EditorGUILayout.HelpBox(summary, MessageType.None);

        DrawTopicButtons(goalController, reporter.GetCachedPriorityTopics());
        DrawSection("Domain Success", reporter.GetDomainSuccessLines());
        DrawSection("Conflict Hotspots", reporter.GetHotTopicLines());
        DrawRecentEntries(reporter.GetCachedEntries());

        EditorGUILayout.EndScrollView();
    }

    private static void DrawTopicButtons(ArTusGoalController goalController, System.Collections.Generic.List<string> topics)
    {
        EditorGUILayout.LabelField("Priority Topics", EditorStyles.boldLabel);
        if (topics == null || topics.Count == 0)
        {
            EditorGUILayout.LabelField("No priority topics.");
            return;
        }

        foreach (string topic in topics.Take(8))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(topic);
                if (goalController != null && GUILayout.Button("Queue Verify", GUILayout.Width(100f)))
                {
                    goalController.QueuePriorityVerificationTopic(topic, 0.82f);
                }
            }
        }
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
            EditorGUILayout.LabelField("• " + line, EditorStyles.wordWrappedLabel);
    }

    private static void DrawRecentEntries(System.Collections.Generic.List<VerificationAuditEntry> entries)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Recent Outcomes", EditorStyles.boldLabel);

        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.LabelField("No verification outcomes recorded yet.");
            return;
        }

        foreach (VerificationAuditEntry entry in entries
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.completedAt)
            .Take(10))
        {
            EditorGUILayout.LabelField(
                $"{entry.topic}: {entry.requestedState} -> {entry.finalState} ({entry.confidence:F2})",
                EditorStyles.wordWrappedLabel
            );
            if (!string.IsNullOrWhiteSpace(entry.summary))
                EditorGUILayout.LabelField(entry.summary, EditorStyles.miniLabel);
            EditorGUILayout.Space(2f);
        }
    }
}
