using System;
using System.Collections.Generic;
using UnityEngine;
using ArTusTypes;

/// <summary>
/// ContradictionLogManager
/// --------------------------------------------------
/// Authoritative, passive storage for all detected contradictions.
/// • Records contradictions
/// • Does NOT resolve, mutate, or react
/// • Returns SAFE COPIES only
/// • Beta-safe and WebGL-safe
/// </summary>
public class ContradictionLogManager : MonoBehaviour
{
    // 🔒 Internal authoritative store (never exposed directly)
    private readonly List<ContradictionEntry> contradictions = new();

    // ==================================================
    // WRITE (STRICTLY CONTROLLED)
    // ==================================================

    /// <summary>
    /// Log a new contradiction entry (authoritative record)
    /// </summary>
    public void LogContradiction(ContradictionEntry entry)
    {
        if (entry == null)
            return;

        if (string.IsNullOrWhiteSpace(entry.topic))
            return;

        // Ensure timestamp exists (UTC, stable across systems)
        if (string.IsNullOrEmpty(entry.timestamp))
            entry.timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        contradictions.Add(entry);

        Debug.Log(
            $"[ContradictionLog] {entry.topic} | Severity: {entry.severity:F2}"
        );
    }

    // ==================================================
    // READ (SAFE ACCESS ONLY)
    // ==================================================

    /// <summary>
    /// Returns a SAFE COPY of all contradictions
    /// </summary>
    public List<ContradictionEntry> GetContradictionEntries()
    {
        return new List<ContradictionEntry>(contradictions);
    }

    /// <summary>
    /// Returns contradictions above a severity threshold
    /// </summary>
    public List<ContradictionEntry> GetSevereContradictions(float threshold = 5f)
    {
        var result = new List<ContradictionEntry>();

        foreach (var c in contradictions)
        {
            if (c != null && c.severity >= threshold)
                result.Add(c);
        }

        return result;
    }

    /// <summary>
    /// Returns the highest-severity contradiction
    /// </summary>
    public ContradictionEntry GetMostSevere()
    {
        ContradictionEntry highest = null;

        foreach (var c in contradictions)
        {
            if (c == null)
                continue;

            if (highest == null || c.severity > highest.severity)
                highest = c;
        }

        return highest;
    }

    /// <summary>
    /// Returns contradictions related to a specific topic
    /// </summary>
    public List<ContradictionEntry> GetByTopic(string topic)
    {
        var result = new List<ContradictionEntry>();

        if (string.IsNullOrWhiteSpace(topic))
            return result;

        topic = topic.ToLowerInvariant();

        foreach (var c in contradictions)
        {
            if (c == null || string.IsNullOrEmpty(c.topic))
                continue;

            if (c.topic.ToLowerInvariant().Contains(topic))
                result.Add(c);
        }

        return result;
    }

    // ==================================================
    // STATE INSPECTION
    // ==================================================

    /// <summary>
    /// Total contradiction count
    /// </summary>
    public int Count => contradictions.Count;

    /// <summary>
    /// Whether any severe contradictions exist
    /// </summary>
    public bool HasSevere(float threshold = 7f)
    {
        foreach (var c in contradictions)
        {
            if (c != null && c.severity >= threshold)
                return true;
        }

        return false;
    }

    // ==================================================
    // CONTROLLED CLEAR (USE CAREFULLY)
    // ==================================================

    /// <summary>
    /// Clears all contradictions (manual use only)
    /// </summary>
    public void ClearContradictions()
    {
        contradictions.Clear();
        Debug.Log("[ContradictionLog] Cleared.");
    }
}