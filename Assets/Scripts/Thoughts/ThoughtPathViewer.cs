using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThoughtPathEntry
{
    public string topic;
    public string stepType;  // e.g., "Hypothesis", "Correction", etc.
}

public class ThoughtPathViewer : MonoBehaviour
{
    [Header("Visual Settings")]
    public GameObject nodePrefab;
    public Material hypothesisMat;
    public Material correctionMat;
    public Material finalizationMat;
    public Material defaultMat;

    private Dictionary<string, List<GameObject>> topicVisuals = new();

    [Header("Cognitive Path")]
    public List<ThoughtPathEntry> thoughtPathLog = new();

    void Start()
    {
        RenderAllThoughtPaths();
    }

    public void RenderAllThoughtPaths()
    {
        ClearExistingVisuals();

        var stepsByTopic = new Dictionary<string, List<ThoughtPathEntry>>();

        foreach (var step in thoughtPathLog)
        {
            if (string.IsNullOrWhiteSpace(step.topic)) continue;

            if (!stepsByTopic.ContainsKey(step.topic))
                stepsByTopic[step.topic] = new();

            stepsByTopic[step.topic].Add(step);
        }

        foreach (var kv in stepsByTopic)
        {
            RenderTopicPath(kv.Key, kv.Value);
        }
    }

    private void RenderTopicPath(string topic, List<ThoughtPathEntry> steps)
    {
        if (string.IsNullOrWhiteSpace(topic) || nodePrefab == null || steps == null || steps.Count == 0)
        {
            Debug.LogWarning($"[ThoughtPathViewer] ❌ Cannot render path — missing topic, prefab, or steps.");
            return;
        }

        Vector3 center = Random.onUnitSphere * 3f;
        List<GameObject> orbitNodes = new();

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            Vector3 position = center + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 1.5f;

            GameObject node = Instantiate(nodePrefab, position, Quaternion.identity, this.transform);
            node.name = $"{topic}_{step.stepType}_{i}";
            node.transform.localScale = Vector3.one * 0.25f;

            if (node.TryGetComponent<Renderer>(out var renderer))
            {
                Material mat = GetMaterialForStep(step.stepType);
                renderer.material = mat ?? defaultMat;
            }

            orbitNodes.Add(node);
        }

        topicVisuals[topic] = orbitNodes;
        Debug.Log($"[ThoughtPathViewer] ✅ Rendered path for topic '{topic}' with {steps.Count} steps.");
    }

    private Material GetMaterialForStep(string stepType)
    {
        return stepType switch
        {
            "Hypothesis" => hypothesisMat,
            "Correction" => correctionMat,
            "Finalization" => finalizationMat,
            _ => defaultMat // Simulation removed
        };
    }

    public void ClearExistingVisuals()
    {
        foreach (var list in topicVisuals.Values)
        {
            foreach (var node in list)
                Destroy(node);
        }
        topicVisuals.Clear();
    }
}
