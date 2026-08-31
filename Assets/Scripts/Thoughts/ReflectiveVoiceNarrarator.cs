using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class MemoryTrailVisualizer : MonoBehaviour
{
    [Header("Optional Prefabs")]
    public GameObject beliefNodePrefab;
    public GameObject contradictionNodePrefab;

    [Header("Optional Trail Source")]
    public string jsonPath =
        "D:/ArTusCloud-Deployment/UNIVERcity/Exports/MemoryTrail.json";

    public float spread = 10f;

    [System.Serializable]
    public class MemoryTrailEntry
    {
        public string id;
        public string belief;
        public float confidence;
        public string emotion;
        public string trail;
        public string domain;
        public string type;
    }

    [System.Serializable]
    public class MemoryTrailWrapper
    {
        public List<MemoryTrailEntry> trails = new();
    }

    private void Start()
    {
        // 🔕 Silent exit — visualization is optional
        if (!File.Exists(jsonPath))
            return;

        try
        {
            string json = File.ReadAllText(jsonPath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            MemoryTrailWrapper wrapper;

            // Support both raw array and wrapped formats
            if (json.TrimStart().StartsWith("["))
            {
                var trails = JsonConvert.DeserializeObject<List<MemoryTrailEntry>>(json);
                wrapper = new MemoryTrailWrapper { trails = trails };
            }
            else
            {
                wrapper = JsonConvert.DeserializeObject<MemoryTrailWrapper>(json);
            }

            if (wrapper?.trails == null || wrapper.trails.Count == 0)
                return;

            SpawnNodes(wrapper.trails);
        }
        catch
        {
            // 🔇 Silent fail — visualization must never block cognition
        }
    }

    private void SpawnNodes(List<MemoryTrailEntry> trails)
    {
        Vector3 origin = transform.position;

        foreach (var entry in trails)
        {
            GameObject prefab =
                entry.type == "contradiction"
                ? contradictionNodePrefab
                : beliefNodePrefab;

            if (!prefab)
                continue;

            GameObject node = Instantiate(
                prefab,
                origin + Random.insideUnitSphere * spread,
                Quaternion.identity,
                transform
            );

            float scale = Mathf.Lerp(0.5f, 2.0f, Mathf.Clamp01(entry.confidence));
            node.transform.localScale = Vector3.one * scale;
            node.name = entry.id ?? "TrailNode";

            var label = node.GetComponentInChildren<TextMesh>();
            if (label != null)
                label.text = entry.belief;
        }
    }
}
