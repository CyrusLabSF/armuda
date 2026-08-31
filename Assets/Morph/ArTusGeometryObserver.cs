using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArTusGeometryObserver : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusShapeIntelligence shapeIntelligence;

    [Header("Scan Settings")]
    public bool autoScan = true;
    public float scanInterval = 12f;
    public int maxMeshesPerScan = 3;

    [Header("Filters")]
    public bool ignoreSmallMeshes = true;
    public float minBoundsSize = 0.05f;

    private float nextScanTime;

    private void Awake()
    {
        if (shapeIntelligence == null)
            shapeIntelligence = FindAnyObjectByType<ArTusShapeIntelligence>();

        nextScanTime = Time.time + Random.Range(2f, 5f);
    }

    private void Update()
    {
        if (!autoScan) return;
        if (Time.time < nextScanTime) return;

        nextScanTime = Time.time + scanInterval;

        ScanSceneForGeometry();
    }

    // =========================================
    // MAIN SCAN
    // =========================================
    public void ScanSceneForGeometry()
    {
        MeshFilter[] filters = FindObjectsByType<MeshFilter>();

        if (filters == null || filters.Length == 0)
            return;

        // Randomize selection
        var shuffled = filters.OrderBy(x => Random.value).Take(maxMeshesPerScan);

        foreach (var filter in shuffled)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            Mesh mesh = filter.sharedMesh;

            if (ignoreSmallMeshes && IsTooSmall(mesh))
                continue;

            ArTusShapeProfile profile = AnalyzeMesh(mesh);

            if (profile != null)
            {
                shapeIntelligence?.LearnShape(profile);

                Debug.Log($"[Observer] Learned shape: {profile.displayName} | Complexity: {profile.complexity:F2}");
            }
        }
    }

    // =========================================
    // CORE ANALYSIS
    // =========================================
    private ArTusShapeProfile AnalyzeMesh(Mesh mesh)
    {
        if (mesh == null) return null;

        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;

        float x = Mathf.Max(size.x, 0.0001f);
        float y = Mathf.Max(size.y, 0.0001f);
        float z = Mathf.Max(size.z, 0.0001f);

        float total = x + y + z;

        Vector3 axisWeights = new Vector3(
            x / total,
            y / total,
            z / total
        );

        int vertexCount = mesh.vertexCount;

        float complexity = Mathf.Clamp01(vertexCount / 5000f);
        float symmetry = EstimateSymmetry(mesh);
        float curvature = EstimateCurvature(mesh);
        float hollow = EstimateHollowness(mesh);

        string classification = ClassifyShape(axisWeights, symmetry, curvature, hollow);

        // Build profile
        ArTusShapeProfile profile = new ArTusShapeProfile();

        profile.shapeId = "observed_" + Random.Range(1000, 999999);
        profile.displayName = classification;
        profile.category = "Observed";
        profile.archetype = classification.ToLowerInvariant();

        profile.stretchAxisWeights = axisWeights;
        profile.complexity = complexity;
        profile.stability = Mathf.Lerp(1f, 0.2f, complexity);

        // Behavior mapping
        profile.twistStrength = curvature;
        profile.rippleStrength = complexity * 0.6f;
        profile.pulseStrength = hollow * 0.8f;
        profile.orbitStrength = symmetry * 0.7f;

        profile.confidence = 0.25f; // low initial confidence
        profile.reconstructionScore = 0f;

        return profile;
    }

    // =========================================
    // METRIC FUNCTIONS
    // =========================================

    private bool IsTooSmall(Mesh mesh)
    {
        Bounds b = mesh.bounds;
        return b.size.magnitude < minBoundsSize;
    }

    private float EstimateSymmetry(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;

        if (verts.Length < 10)
            return 0.5f;

        int samples = Mathf.Min(verts.Length, 200);
        int symmetricPairs = 0;

        for (int i = 0; i < samples; i++)
        {
            Vector3 v = verts[i];
            Vector3 mirrored = new Vector3(-v.x, v.y, v.z);

            foreach (var other in verts)
            {
                if (Vector3.Distance(other, mirrored) < 0.05f)
                {
                    symmetricPairs++;
                    break;
                }
            }
        }

        return Mathf.Clamp01((float)symmetricPairs / samples);
    }

    private float EstimateCurvature(Mesh mesh)
    {
        Vector3[] normals = mesh.normals;

        if (normals == null || normals.Length < 10)
            return 0.3f;

        float variance = 0f;

        for (int i = 1; i < normals.Length; i++)
        {
            variance += Vector3.Angle(normals[i - 1], normals[i]);
        }

        variance /= normals.Length;

        return Mathf.Clamp01(variance / 180f);
    }

    private float EstimateHollowness(Mesh mesh)
    {
        Bounds bounds = mesh.bounds;
        float volumeEstimate = bounds.size.x * bounds.size.y * bounds.size.z;

        float density = mesh.vertexCount / Mathf.Max(volumeEstimate, 0.0001f);

        return Mathf.Clamp01(1f - (density / 500f));
    }

    // =========================================
    // CLASSIFICATION
    // =========================================
    private string ClassifyShape(Vector3 axis, float symmetry, float curvature, float hollow)
    {
        if (symmetry > 0.85f && curvature < 0.3f)
            return "Sphere";

        if (hollow > 0.6f && symmetry > 0.6f)
            return "Torus-like";

        if (axis.y > 0.6f)
            return "Vertical Form";

        if (axis.x > 0.6f || axis.z > 0.6f)
            return "Horizontal Form";

        if (curvature > 0.7f)
            return "Organic/Complex";

        if (symmetry < 0.3f)
            return "Irregular";

        return "Hybrid Form";
    }
}
