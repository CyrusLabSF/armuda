using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ArTusCodeArtifact
{
    public string artifactId;
    public string topic;
    public string language;
    public string path;
    public string summary;
    public int symbolCount;
    public int referenceCount;
    public float confidence;
    public string lastUpdatedAt;
}

public class ArTusCodeIntelligence : MonoBehaviour
{
    [SerializeField] private List<ArTusCodeArtifact> artifacts = new();

    public void IngestCodeArtifact(
        string topic,
        string language,
        string path,
        string summary,
        int symbolCount = 0,
        int referenceCount = 0,
        float confidence = 0.5f)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        string artifactId = string.IsNullOrWhiteSpace(path)
            ? topic.Trim().ToLowerInvariant()
            : path.Trim().ToLowerInvariant();

        var artifact = artifacts.FirstOrDefault(entry =>
            string.Equals(entry.artifactId, artifactId, StringComparison.OrdinalIgnoreCase));

        if (artifact == null)
        {
            artifact = new ArTusCodeArtifact();
            artifacts.Add(artifact);
        }

        artifact.artifactId = artifactId;
        artifact.topic = topic.Trim();
        artifact.language = string.IsNullOrWhiteSpace(language) ? "unknown" : language.Trim();
        artifact.path = path?.Trim() ?? string.Empty;
        artifact.summary = summary?.Trim() ?? string.Empty;
        artifact.symbolCount = Mathf.Max(0, symbolCount);
        artifact.referenceCount = Mathf.Max(0, referenceCount);
        artifact.confidence = Mathf.Clamp01(confidence);
        artifact.lastUpdatedAt = DateTime.UtcNow.ToString("o");
    }

    public List<ArTusCodeArtifact> GetArtifacts()
    {
        return artifacts
            .Where(artifact => artifact != null && !string.IsNullOrWhiteSpace(artifact.artifactId))
            .Select(CloneArtifact)
            .ToList();
    }

    public bool HasWorkingKnowledge()
    {
        return artifacts.Any(artifact => artifact != null && artifact.confidence >= 0.4f);
    }

    private static ArTusCodeArtifact CloneArtifact(ArTusCodeArtifact source)
    {
        return new ArTusCodeArtifact
        {
            artifactId = source.artifactId,
            topic = source.topic,
            language = source.language,
            path = source.path,
            summary = source.summary,
            symbolCount = source.symbolCount,
            referenceCount = source.referenceCount,
            confidence = source.confidence,
            lastUpdatedAt = source.lastUpdatedAt
        };
    }
}
