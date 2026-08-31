using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ArTusTypes;

/// <summary>
/// Lightweight Unity-facing reporter for verification outcomes.
/// Shows a small on-screen audit summary and exports a CSV snapshot for review.
/// </summary>
public class ArTusVerificationAuditReporter : MonoBehaviour
{
    [Header("Display")]
    public bool showOverlay = true;
    public bool showOnlyInPlayMode = true;
    public int recentEntriesToShow = 5;
    public int priorityTopicsToShow = 5;
    public int domainRowsToShow = 4;
    public int hotTopicsToShow = 4;
    public Rect overlayRect = new Rect(20f, 20f, 720f, 460f);
    public bool enableInteractiveControls = true;

    [Header("Refresh")]
    public bool autoRefresh = true;
    public float refreshInterval = 15f;

    [Header("Export")]
    public bool exportCsvOnRefresh = true;
    public string csvRelativePath = "UNIVERcity/Verification/verification_audit_report.csv";
    public string trendCsvRelativePath = "UNIVERcity/Verification/verification_audit_trends.csv";

    private ArTusGoalController goalController;
    private string csvPath;
    private string trendCsvPath;
    private float lastRefreshTime = -999f;
    private string overlayText = "Verification audit not loaded.";
    private List<string> cachedPriorityTopics = new();
    private List<VerificationAuditEntry> cachedEntries = new();
    private Vector2 recentScroll;

    private void Awake()
    {
        goalController = GetComponent<ArTusGoalController>();
        csvPath = ArTusPathUtility.GetPersistent(csvRelativePath);
        trendCsvPath = ArTusPathUtility.GetPersistent(trendCsvRelativePath);
        RefreshAuditReport();
    }

    private void Update()
    {
        if (!autoRefresh)
            return;

        if (Time.time - lastRefreshTime < refreshInterval)
            return;

        RefreshAuditReport();
    }

    [ContextMenu("Refresh Verification Audit")]
    public void RefreshAuditReport()
    {
        lastRefreshTime = Time.time;

        if (goalController == null)
            goalController = GetComponent<ArTusGoalController>();

        if (goalController == null)
        {
            overlayText = "Verification audit reporter requires ArTusGoalController.";
            return;
        }

        List<VerificationAuditEntry> entries = goalController.GetVerificationAuditEntries();
        List<string> priorityTopics = goalController.GetHighPriorityVerificationTopics(2);
        cachedEntries = entries ?? new List<VerificationAuditEntry>();
        cachedPriorityTopics = priorityTopics ?? new List<string>();

        overlayText = BuildOverlay(cachedEntries, cachedPriorityTopics);

        if (exportCsvOnRefresh)
        {
            ExportCsv(cachedEntries);
            ExportTrendCsv(cachedEntries);
        }
    }

    [ContextMenu("Export Verification Audit CSV")]
    public void ExportVerificationAuditCsv()
    {
        if (goalController == null)
            goalController = GetComponent<ArTusGoalController>();

        ExportCsv(goalController != null
            ? goalController.GetVerificationAuditEntries()
            : new List<VerificationAuditEntry>());
    }

    public string GetOverlaySummary() => overlayText ?? string.Empty;

    public List<string> GetCachedPriorityTopics()
    {
        return new List<string>(cachedPriorityTopics ?? new List<string>());
    }

    public List<VerificationAuditEntry> GetCachedEntries()
    {
        return new List<VerificationAuditEntry>(cachedEntries ?? new List<VerificationAuditEntry>());
    }

    public List<string> GetDomainSuccessLines()
    {
        return BuildDomainSuccessLines(cachedEntries, domainRowsToShow);
    }

    public List<string> GetHotTopicLines()
    {
        return BuildHotTopicLines(cachedEntries, hotTopicsToShow);
    }

    private void OnGUI()
    {
        if (!showOverlay)
            return;

        if (showOnlyInPlayMode && !Application.isPlaying)
            return;

        GUI.Box(overlayRect, "ArTus Verification Audit");

        Rect contentRect = new Rect(
            overlayRect.x + 10f,
            overlayRect.y + 24f,
            overlayRect.width - 20f,
            overlayRect.height - 34f
        );

        if (!enableInteractiveControls)
        {
            GUI.Label(contentRect, overlayText);
            return;
        }

        float lineHeight = 22f;
        float buttonWidth = 88f;
        float buttonGap = 8f;
        float currentY = contentRect.y;

        if (GUI.Button(new Rect(contentRect.x, currentY, buttonWidth, lineHeight), "Refresh"))
            RefreshAuditReport();

        if (GUI.Button(new Rect(contentRect.x + buttonWidth + buttonGap, currentY, buttonWidth, lineHeight), "Export CSV"))
        {
            ExportVerificationAuditCsv();
            ExportTrendCsv(cachedEntries);
        }

        if (GUI.Button(new Rect(contentRect.x + (buttonWidth + buttonGap) * 2f, currentY, 112f, lineHeight), "Clear Resolved"))
        {
            goalController?.ClearResolvedVerificationAudit();
            RefreshAuditReport();
        }

        currentY += lineHeight + 8f;
        GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, 48f), overlayText);
        currentY += 58f;

        GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "Priority Topics (click to verify):");
        currentY += lineHeight;

        float topicButtonWidth = Mathf.Min(220f, contentRect.width - 10f);
        foreach (string topic in cachedPriorityTopics.Take(priorityTopicsToShow))
        {
            if (GUI.Button(new Rect(contentRect.x, currentY, topicButtonWidth, lineHeight), topic))
            {
                goalController?.QueuePriorityVerificationTopic(topic, 0.82f);
                RefreshAuditReport();
            }

            currentY += lineHeight + 4f;
        }

        if (cachedPriorityTopics.Count == 0)
        {
            GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "No priority verification topics.");
            currentY += lineHeight + 4f;
        }

        List<string> domainLines = BuildDomainSuccessLines(cachedEntries, domainRowsToShow);
        GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "Domain Success Rates:");
        currentY += lineHeight;

        foreach (string line in domainLines)
        {
            GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), line);
            currentY += lineHeight;
        }

        if (domainLines.Count == 0)
        {
            GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "No domain trend data yet.");
            currentY += lineHeight;
        }

        currentY += 4f;
        List<string> hotTopicLines = BuildHotTopicLines(cachedEntries, hotTopicsToShow);
        GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "Conflict Hotspots:");
        currentY += lineHeight;

        foreach (string line in hotTopicLines)
        {
            GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), line);
            currentY += lineHeight;
        }

        if (hotTopicLines.Count == 0)
        {
            GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "No repeated conflict hotspots yet.");
            currentY += lineHeight;
        }

        currentY += 4f;
        GUI.Label(new Rect(contentRect.x, currentY, contentRect.width, lineHeight), "Recent Outcomes:");
        currentY += lineHeight;

        Rect scrollViewRect = new Rect(contentRect.x, currentY, contentRect.width, contentRect.height - (currentY - contentRect.y));
        float innerHeight = Mathf.Max(scrollViewRect.height, cachedEntries.Take(recentEntriesToShow).Count() * (lineHeight + 20f));
        Rect innerRect = new Rect(0f, 0f, scrollViewRect.width - 20f, innerHeight);
        recentScroll = GUI.BeginScrollView(scrollViewRect, recentScroll, innerRect);

        float entryY = 0f;
        foreach (VerificationAuditEntry entry in cachedEntries
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.completedAt)
            .Take(recentEntriesToShow))
        {
            string line =
                $"{entry.topic}: {entry.requestedState} -> {entry.finalState} " +
                $"(conf {entry.confidence:F2}, src {entry.supportingEvidenceCount})";
            GUI.Label(new Rect(0f, entryY, innerRect.width, 36f), line);
            entryY += lineHeight + 20f;
        }

        if (cachedEntries.Count == 0)
            GUI.Label(new Rect(0f, 0f, innerRect.width, lineHeight), "No verification outcomes recorded yet.");

        GUI.EndScrollView();
    }

    private string BuildOverlay(
        List<VerificationAuditEntry> entries,
        List<string> priorityTopics
    )
    {
        var builder = new StringBuilder();
        entries ??= new List<VerificationAuditEntry>();
        priorityTopics ??= new List<string>();

        int verified = entries.Count(entry => string.Equals(entry.finalState, "verified", StringComparison.OrdinalIgnoreCase));
        int conflicted = entries.Count(entry => string.Equals(entry.finalState, "conflicted", StringComparison.OrdinalIgnoreCase));
        int unresolved = entries.Count(entry =>
            entry != null &&
            !string.Equals(entry.finalState, "verified", StringComparison.OrdinalIgnoreCase));

        builder.AppendLine($"Entries: {entries.Count}  Verified: {verified}  Conflicted: {conflicted}  Unresolved: {unresolved}");
        builder.AppendLine();
        builder.AppendLine("Top Domain Success Rates:");
        foreach (string line in BuildDomainSuccessLines(entries, domainRowsToShow))
            builder.AppendLine($"  {line}");
        builder.AppendLine();
        builder.AppendLine("Conflict Hotspots:");
        foreach (string line in BuildHotTopicLines(entries, hotTopicsToShow))
            builder.AppendLine($"  {line}");
        builder.AppendLine();
        builder.AppendLine("Priority Topics:");

        if (priorityTopics.Count == 0)
        {
            builder.AppendLine("  None");
        }
        else
        {
            foreach (string topic in priorityTopics.Take(priorityTopicsToShow))
                builder.AppendLine($"  - {topic}");
        }

        builder.AppendLine();
        builder.AppendLine("Recent Verification Outcomes:");

        foreach (VerificationAuditEntry entry in entries
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.completedAt)
            .Take(recentEntriesToShow))
        {
            builder.AppendLine(
                $"  - {entry.topic}: {entry.requestedState} -> {entry.finalState} " +
                $"(conf {entry.confidence:F2}, src {entry.supportingEvidenceCount})"
            );
        }

        if (entries.Count == 0)
            builder.AppendLine("  No verification outcomes recorded yet.");

        return builder.ToString();
    }

    private void ExportCsv(List<VerificationAuditEntry> entries)
    {
        try
        {
            string dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var builder = new StringBuilder();
            builder.AppendLine("CompletedAt,Topic,Query,Domain,RequestedState,FinalState,Confidence,SupportingEvidenceCount,CitationCount,Summary");

            foreach (VerificationAuditEntry entry in entries ?? new List<VerificationAuditEntry>())
            {
                if (entry == null)
                    continue;

                builder.AppendLine(string.Join(",",
                    Csv(entry.completedAt),
                    Csv(entry.topic),
                    Csv(entry.query),
                    Csv(entry.domain),
                    Csv(entry.requestedState),
                    Csv(entry.finalState),
                    entry.confidence.ToString("F2"),
                    entry.supportingEvidenceCount.ToString(),
                    (entry.citations?.Count ?? 0).ToString(),
                    Csv(entry.summary)
                ));
            }

            File.WriteAllText(csvPath, builder.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VerificationAuditReporter] CSV export failed: {ex.Message}");
        }
    }

    private void ExportTrendCsv(List<VerificationAuditEntry> entries)
    {
        try
        {
            string dir = Path.GetDirectoryName(trendCsvPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var builder = new StringBuilder();
            builder.AppendLine("Section,Name,MetricA,MetricB,MetricC");

            foreach (var row in BuildDomainTrendRows(entries))
            {
                builder.AppendLine(string.Join(",",
                    Csv("domain_success"),
                    Csv(row.name),
                    Csv(row.metricA),
                    Csv(row.metricB),
                    Csv(row.metricC)
                ));
            }

            foreach (var row in BuildHotTopicTrendRows(entries))
            {
                builder.AppendLine(string.Join(",",
                    Csv("conflict_hotspot"),
                    Csv(row.name),
                    Csv(row.metricA),
                    Csv(row.metricB),
                    Csv(row.metricC)
                ));
            }

            File.WriteAllText(trendCsvPath, builder.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VerificationAuditReporter] Trend CSV export failed: {ex.Message}");
        }
    }

    private List<string> BuildDomainSuccessLines(List<VerificationAuditEntry> entries, int maxRows)
    {
        return BuildDomainTrendRows(entries)
            .Take(maxRows)
            .Select(row => $"{row.name}: {row.metricA} verified, {row.metricB} total, {row.metricC}")
            .ToList();
    }

    private List<string> BuildHotTopicLines(List<VerificationAuditEntry> entries, int maxRows)
    {
        return BuildHotTopicTrendRows(entries)
            .Take(maxRows)
            .Select(row => $"{row.name}: {row.metricA} conflicts, {row.metricB} unresolved, last {row.metricC}")
            .ToList();
    }

    private List<TrendRow> BuildDomainTrendRows(List<VerificationAuditEntry> entries)
    {
        return (entries ?? new List<VerificationAuditEntry>())
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.domain))
            .GroupBy(entry => entry.domain.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                int total = group.Count();
                int verified = group.Count(entry => string.Equals(entry.finalState, "verified", StringComparison.OrdinalIgnoreCase));
                float rate = total == 0 ? 0f : (float)verified / total;
                return new TrendRow
                {
                    name = group.Key,
                    metricA = $"{verified}",
                    metricB = $"{total}",
                    metricC = $"{rate:P0}"
                };
            })
            .OrderByDescending(row => ParsePercent(row.metricC))
            .ThenByDescending(row => SafeParseInt(row.metricB))
            .ToList();
    }

    private List<TrendRow> BuildHotTopicTrendRows(List<VerificationAuditEntry> entries)
    {
        return (entries ?? new List<VerificationAuditEntry>())
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.topic))
            .GroupBy(entry => entry.topic.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                int conflicts = group.Count(entry => string.Equals(entry.finalState, "conflicted", StringComparison.OrdinalIgnoreCase));
                int unresolved = group.Count(entry => !string.Equals(entry.finalState, "verified", StringComparison.OrdinalIgnoreCase));
                string lastSeen = group.Max(entry => entry.completedAt ?? string.Empty);
                return new TrendRow
                {
                    name = group.Key,
                    metricA = $"{conflicts}",
                    metricB = $"{unresolved}",
                    metricC = string.IsNullOrWhiteSpace(lastSeen) ? "n/a" : lastSeen
                };
            })
            .Where(row => SafeParseInt(row.metricA) > 0 || SafeParseInt(row.metricB) > 1)
            .OrderByDescending(row => SafeParseInt(row.metricA))
            .ThenByDescending(row => SafeParseInt(row.metricB))
            .ThenByDescending(row => row.metricC)
            .ToList();
    }

    private static string Csv(string value)
    {
        string clean = value ?? string.Empty;
        clean = clean.Replace("\"", "\"\"");
        return $"\"{clean}\"";
    }

    private static int SafeParseInt(string value)
    {
        return int.TryParse(value, out int parsed) ? parsed : 0;
    }

    private static float ParsePercent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0f;

        string trimmed = value.Replace("%", string.Empty);
        return float.TryParse(trimmed, out float parsed) ? parsed : 0f;
    }

    private class TrendRow
    {
        public string name;
        public string metricA;
        public string metricB;
        public string metricC;
    }
}
