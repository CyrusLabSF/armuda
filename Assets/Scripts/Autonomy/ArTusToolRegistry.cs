using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ArTusToolDefinition
{
    public string toolId;
    public string label;
    public string category;
    public string description;
    public string endpoint;
    public bool canRead = true;
    public bool canWrite;
    public bool isConnected;
    public float trustScore = 0.5f;
    public string lastValidatedAt;
    public List<string> capabilities = new();
}

public class ArTusToolRegistry : MonoBehaviour
{
    [SerializeField] private List<ArTusToolDefinition> tools = new();

    public void RegisterTool(
        string toolId,
        string label,
        string category,
        IEnumerable<string> capabilities,
        string description = "",
        string endpoint = "",
        bool canRead = true,
        bool canWrite = false,
        bool isConnected = false,
        float trustScore = 0.5f)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return;

        var existing = tools.FirstOrDefault(tool =>
            string.Equals(tool.toolId, toolId, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            existing = new ArTusToolDefinition();
            tools.Add(existing);
        }

        existing.toolId = toolId.Trim();
        existing.label = string.IsNullOrWhiteSpace(label) ? toolId.Trim() : label.Trim();
        existing.category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim();
        existing.description = description?.Trim() ?? string.Empty;
        existing.endpoint = endpoint?.Trim() ?? string.Empty;
        existing.canRead = canRead;
        existing.canWrite = canWrite;
        existing.isConnected = isConnected;
        existing.trustScore = Mathf.Clamp01(trustScore);
        existing.lastValidatedAt = DateTime.UtcNow.ToString("o");
        existing.capabilities = (capabilities ?? Enumerable.Empty<string>())
            .Where(capability => !string.IsNullOrWhiteSpace(capability))
            .Select(capability => capability.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void UpdateConnectionState(string toolId, bool isConnected, float? trustScore = null)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            return;

        var tool = tools.FirstOrDefault(entry =>
            string.Equals(entry.toolId, toolId, StringComparison.OrdinalIgnoreCase));
        if (tool == null)
            return;

        tool.isConnected = isConnected;
        if (trustScore.HasValue)
            tool.trustScore = Mathf.Clamp01(trustScore.Value);
        tool.lastValidatedAt = DateTime.UtcNow.ToString("o");
    }

    public List<ArTusToolDefinition> GetRegisteredTools()
    {
        return tools
            .Where(tool => tool != null && !string.IsNullOrWhiteSpace(tool.toolId))
            .Select(CloneTool)
            .ToList();
    }

    public int GetConnectedToolCount()
    {
        return tools.Count(tool => tool != null && tool.isConnected);
    }

    private static ArTusToolDefinition CloneTool(ArTusToolDefinition source)
    {
        return new ArTusToolDefinition
        {
            toolId = source.toolId,
            label = source.label,
            category = source.category,
            description = source.description,
            endpoint = source.endpoint,
            canRead = source.canRead,
            canWrite = source.canWrite,
            isConnected = source.isConnected,
            trustScore = source.trustScore,
            lastValidatedAt = source.lastValidatedAt,
            capabilities = source.capabilities?.ToList() ?? new List<string>()
        };
    }
}
