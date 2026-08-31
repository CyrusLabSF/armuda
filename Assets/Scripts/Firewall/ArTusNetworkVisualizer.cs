using UnityEngine;
using System.Collections.Generic;

public class ArTusNetworkVisualizer : MonoBehaviour
{
    [Header("References (DO NOT SPAWN CORE)")]
    public Transform arTusCore;   // <-- drag the existing ArTus here

    [Header("Prefabs")]
    public GameObject nodePrefab;
    public GameObject connectionPrefab;

    [Header("Orbit Settings")]
    public float orbitRadius = 10f;
    public float orbitSpacing = 2f;
    public float orbitSpeed = 15f;

    private Dictionary<string, GameObject> nodes = new();
    private Dictionary<string, float> nodeAngles = new();
    private Dictionary<string, string> nodeStages = new();

    void Awake()
    {
        if (arTusCore == null)
        {
            Debug.LogError("[NetworkVisualizer] ArTus core not assigned.");
        }
    }

    void Update()
    {
        foreach (var kvp in nodes)
        {
            string id = kvp.Key;
            GameObject node = kvp.Value;

            if (!nodeAngles.ContainsKey(id)) continue;

            string stage = nodeStages.ContainsKey(id) ? nodeStages[id] : "Normal";

            if (stage == "Diplomacy")
            {
                float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                UpdateNodeGlow(node, 0.6f + pulse * 0.6f, Color.yellow);
            }
            else
            {
                nodeAngles[id] += orbitSpeed * Time.deltaTime;
                float angleRad = nodeAngles[id] * Mathf.Deg2Rad;

                float index = new List<string>(nodes.Keys).IndexOf(id);
                float radius = orbitRadius + (index * orbitSpacing);

                Vector3 offset = new Vector3(
                    Mathf.Cos(angleRad),
                    0,
                    Mathf.Sin(angleRad)
                ) * radius;

                node.transform.position = arTusCore.position + offset;
            }
        }
    }

    public void RegisterConnection(string src, string dest, float severity, string stage)
    {
        if (arTusCore == null) return;

        Normalize(ref src);
        Normalize(ref dest);

        if (!nodes.ContainsKey(src))
        {
            nodes[src] = CreateOrbitingNode(src);
            nodeAngles[src] = Random.Range(0f, 360f);
        }

        if (!nodes.ContainsKey(dest))
        {
            nodes[dest] = CreateOrbitingNode(dest);
            nodeAngles[dest] = Random.Range(0f, 360f);
        }

        nodeStages[src] = stage;
        nodeStages[dest] = stage;

        var line = Instantiate(connectionPrefab);
        var lr = line.GetComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, nodes[src].transform.position);
        lr.SetPosition(1, nodes[dest].transform.position);

        Color c = StageColor(stage);
        lr.startColor = c;
        lr.endColor = c;

        UpdateNodeGlow(nodes[src], severity, c);
        UpdateNodeGlow(nodes[dest], severity, c);
    }

    private void Normalize(ref string id)
    {
        if (id == "ArTus" || id == "core" || id == "localhost")
            id = "ArTus";
    }

    private GameObject CreateOrbitingNode(string id)
    {
        var go = Instantiate(nodePrefab, arTusCore.position, Quaternion.identity);
        go.name = id;

        var text = go.GetComponentInChildren<TextMesh>();
        if (text != null) text.text = id;

        return go;
    }

    private Color StageColor(string stage)
    {
        return stage switch
        {
            "Diplomacy" => Color.yellow,
            "Alert" => Color.red,
            "Critical" => new Color(0.5f, 0, 0),
            _ => Color.blue
        };
    }

    private void UpdateNodeGlow(GameObject node, float intensity, Color color)
    {
        var renderer = node.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.SetColor("_EmissionColor", color * intensity);
        }
    }
}
