// ==========================================================
// ArmudaXRBridge.cs — Unity XR Bridge for Magic Leap
// ==========================================================
// Purpose:
//   Reads Armuda scene export (JSON) and renders nodes as
//   holographic entities in Magic Leap's spatial environment.
//
//   Data Source:
//   D:\ArTusCloud-Deployment\Armuda\Exports\armuda_scene_export.json
//
// Requirements:
//   - Unity 2022+
//   - Magic Leap SDK (XR Management + Spatial Anchors)
//   - TextMeshPro for node labeling
// ==========================================================

using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using TMPro;

#if UNITY_MAGICLEAP || PLATFORM_LUMIN
using UnityEngine.XR.MagicLeap;
#endif

[System.Serializable]
public class ArmudaNode
{
    public string id;
    public float[] pos;
    public string color;
}

[System.Serializable]
public class ArmudaExportData
{
    public List<ArmudaNode> nodes;
    public List<ArmudaNode> artifacts;
}

public class ArmudaXRBridge : MonoBehaviour
{
    [Header("Scene Export Source")]
    public string exportPath = @"D:\ArTusCloud-Deployment\Armuda\Exports\armuda_scene_export.json";

    [Header("Spawn Settings")]
    public GameObject nodePrefab;
    public GameObject artifactPrefab;
    public float scale = 0.25f;
    public float spacing = 0.1f;

    private readonly List<GameObject> spawnedObjects = new();

#if UNITY_MAGICLEAP || PLATFORM_LUMIN
    private bool spatialInitialized = false;
#endif

    void Start()
    {
        StartCoroutine(LoadAndRenderRoutine());
    }

    // ------------------------------------------------------
    IEnumerator LoadAndRenderRoutine()
    {
        yield return new WaitForSeconds(2f);

#if UNITY_MAGICLEAP || PLATFORM_LUMIN
        if (!spatialInitialized)
        {
            MLPermissions.RequestPermission(MLPermission.SpatialMapping);
            MLPermissions.RequestPermission(MLPermission.SpatialAnchors);
            spatialInitialized = true;
            Debug.Log("🔮 [ArmudaXRBridge] Magic Leap spatial permissions granted.");
        }
#endif

        if (!File.Exists(exportPath))
        {
            Debug.LogWarning($"[ArmudaXRBridge] Export not found at: {exportPath}");
            yield break;
        }

        string json = File.ReadAllText(exportPath);
        ArmudaExportData data = JsonUtility.FromJson<ArmudaExportData>(json);

        if (data == null || data.nodes == null)
        {
            Debug.LogError("[ArmudaXRBridge] Invalid or empty export data.");
            yield break;
        }

        ClearOldObjects();

        foreach (ArmudaNode node in data.nodes)
        {
            SpawnNode(node, nodePrefab);
            yield return new WaitForSeconds(spacing);
        }

        foreach (ArmudaNode art in data.artifacts)
        {
            SpawnNode(art, artifactPrefab);
            yield return new WaitForSeconds(spacing);
        }

        Debug.Log($"🌊 [ArmudaXRBridge] Spawned {spawnedObjects.Count} holograms from Armuda export.");
    }

    // ------------------------------------------------------
    void SpawnNode(ArmudaNode node, GameObject prefab)
    {
        if (node == null || prefab == null) return;

        Vector3 position = new(node.pos[0], node.pos[1], node.pos[2]);
        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        obj.transform.localScale = Vector3.one * scale;

        var color = Color.cyan;
        if (ColorUtility.TryParseHtmlString(node.color, out Color parsed))
            color = parsed;

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;

        var label = new GameObject("Label");
        var tmp = label.AddComponent<TextMeshPro>();
        tmp.text = node.id;
        tmp.fontSize = 0.3f;
        tmp.color = Color.white;
        label.transform.SetParent(obj.transform);
        label.transform.localPosition = new Vector3(0, 0.3f, 0);

#if UNITY_MAGICLEAP || PLATFORM_LUMIN
        // Create a spatial anchor so the hologram persists in the real world
        var anchor = obj.AddComponent<MLSpatialAnchorBehavior>();
        anchor.DestroyAnchorOnDestroy = true;
#endif

        spawnedObjects.Add(obj);
    }

    // ------------------------------------------------------
    void ClearOldObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }

    // ------------------------------------------------------
    [ContextMenu("Reload Scene Export")]
    public void Reload()
    {
        StartCoroutine(LoadAndRenderRoutine());
    }
}
