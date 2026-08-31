using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

// Attach this to an object in the scene (e.g., TorusManager).
// Configure material, sample rate, and sample count in the inspector.
// Writes CSV to: D:/ArTusCloud-Deployment/UNIVERcity/Exports/TorusColorLog.csv
// Writes JSON to: D:/ArTusCloud-Deployment/UNIVERcity/Exports/TorusColorSnapshots/<timestamp>.json

[DisallowMultipleComponent]
public class TorusSpectrumLogger : MonoBehaviour
{
    [Header("References")]
    public Material torusMaterial; // assign the material using TorusDynamicSpectrum_Ultimate
    public Transform torusTransform; // optional; used if you want world-space sampling

    [Header("Sampling")]
    public int samplesPerFrame = 8;      // how many UV samples per tick
    public float sampleInterval = 0.5f;  // seconds between writes
    public int maxRowsPerFile = 100000;

    [Header("Export")]
    public string exportFolder = @"D:/ArTusCloud-Deployment/UNIVERcity/Exports";
    public string csvFileName = "TorusColorLog.csv";
    public bool writeJsonSnapshots = true;

    // Derived / internal
    private float timeAccum = 0f;
    private string csvPath;
    private string jsonFolder;
    private StreamWriter csvWriter;
    private object writeLock = new object();

    void Start()
    {
        if (string.IsNullOrEmpty(exportFolder))
            exportFolder = Application.dataPath; // fallback

        Directory.CreateDirectory(exportFolder);
        jsonFolder = Path.Combine(exportFolder, "TorusColorSnapshots");
        Directory.CreateDirectory(jsonFolder);

        csvPath = Path.Combine(exportFolder, csvFileName);

        bool exists = File.Exists(csvPath);
        csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.Read));
        csvWriter.AutoFlush = true;

        if (!exists)
        {
            // write header
            csvWriter.WriteLine("timestamp,frame,uv_x,uv_y,hueSlow,hueFast,hueErratic,hueCombined,n1,n2,n3,flow,frnl, r,g,b,alpha");
        }
    }

    void OnDisable()
    {
        if (csvWriter != null)
        {
            csvWriter.Flush();
            csvWriter.Close();
            csvWriter = null;
        }
    }

    void Update()
    {
        timeAccum += Time.deltaTime;
        if (timeAccum >= sampleInterval)
        {
            SampleAndLog();
            timeAccum = 0f;
        }
    }

    // Basic CPU-side noise & hue math to mirror shader (same hash & noise functions)
    private float Hash(Vector2 p)
    {
        // same hash as shader: frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453)
        double dot = p.x * 127.1 + p.y * 311.7;
        double s = Math.Sin(dot) * 43758.5453;
        return (float)(s - Math.Floor(s)); // frac
    }

    private float Noise(Vector2 p)
    {
        Vector2 i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
        Vector2 f = new Vector2(p.x - i.x, p.y - i.y);

        float a = Hash(i);
        float b = Hash(i + new Vector2(1f, 0f));
        float c = Hash(i + new Vector2(0f, 1f));
        float d = Hash(i + new Vector2(1f, 1f));

        Vector2 u = new Vector2(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));
        float lerp_ab = Mathf.Lerp(a, b, u.x);
        float lerp_cd = Mathf.Lerp(c, d, u.x);

        return Mathf.Lerp(lerp_ab, lerp_cd, u.y);
    }

    // hue->rgb using same cos-approx function as shader
    private Vector3 HueToRGB(float h)
    {
        // h in radians (0..2PI), same formula used in shader
        return new Vector3(
            0.5f + 0.5f * Mathf.Cos(h),
            0.5f + 0.5f * Mathf.Cos(h + 2.094f),
            0.5f + 0.5f * Mathf.Cos(h + 4.188f)
        );
    }

    private void SampleAndLog()
    {
        if (csvWriter == null) return;

        float t = Time.time;
        float noiseSpeed = 1.0f;
        float flowSpeed = 1.2f;
        // attempt to read any relevant properties from the material (if you've adjusted them in inspector)
        if (torusMaterial != null)
        {
            if (torusMaterial.HasProperty("_NoiseSpeed")) noiseSpeed = torusMaterial.GetFloat("_NoiseSpeed");
            if (torusMaterial.HasProperty("_FlowSpeed")) flowSpeed = torusMaterial.GetFloat("_FlowSpeed");
        }

        var snapshot = new List<Dictionary<string, object>>();
        for (int s = 0; s < samplesPerFrame; s++)
        {
            // pick uv sample points: distributed around torus: use polar sampling or random
            float u = (float)s / samplesPerFrame;           // 0..1
            float v = UnityEngine.Random.value;            // random along v
            Vector2 uv = new Vector2(u, v);

            // compute mirrored shader noise values
            float n1 = Noise(uv * 2.0f + new Vector2(t * noiseSpeed, 0f));
            float n2 = Noise(uv * 5.0f + new Vector2(0f, t * (noiseSpeed * 1.5f)));
            float n3 = Noise(uv * 12.0f + new Vector2(0f, t * (noiseSpeed * 2.0f)));
            float swirl = (n1 * 0.5f + n2 * 0.3f + n3 * 0.2f);

            // hue channels (match shader logic)
            float hueSlow = Frac(t * 0.05f + swirl) * Mathf.PI * 2f;
            float hueFast = Frac(t * 0.3f + n2) * Mathf.PI * 2f;
            float hueErratic = Frac(Mathf.Sin(t * 1.7f + n3 * 5.0f)) * Mathf.PI * 2f;

            float hueCombined = hueSlow * 0.5f + hueFast * 0.3f + hueErratic * 0.2f;

            Vector3 rgb = HueToRGB(hueCombined);

            // spectral ghost layering approximately
            Vector3 rgbShift1 = HueToRGB(hueCombined + 1.57f);
            Vector3 rgbShift2 = HueToRGB(hueCombined - 2.0f);
            Vector3 finalRgb = Vector3.Lerp(rgb, rgbShift1, 0.25f) + rgbShift2 * 0.2f;

            // flow mask
            float flow = Mathf.Sin((uv.y + t * flowSpeed) * Mathf.PI * 2f) * 0.5f + 0.5f;
            finalRgb *= Mathf.Lerp(0.8f, 1.4f, flow);

            // approximate fresnel by view-angle vs normal if torusTransform provided (otherwise default 0.5)
            float fresnel = 0.5f;
            if (torusTransform != null)
            {
                // naive world normal approximation: sample torus up-vector at uv
                Vector3 worldNormal = torusTransform.up; // approximate; better if you supply actual mesh normals per UV
                Vector3 viewDir = (Camera.main != null) ? (Camera.main.transform.position - torusTransform.position).normalized : Vector3.forward;
                fresnel = Mathf.Pow(1f - Mathf.Clamp01(Vector3.Dot(worldNormal.normalized, viewDir.normalized)), 3.5f);
            }

            // emission/alpha read from material if available
            float emiss = (torusMaterial != null && torusMaterial.HasProperty("_EmissionIntensity")) ? torusMaterial.GetFloat("_EmissionIntensity") : 8.0f;
            float alpha = (torusMaterial != null && torusMaterial.HasProperty("_Transparency")) ? torusMaterial.GetFloat("_Transparency") : 0.6f;
            float finalAlpha = alpha * (0.5f + 0.5f * fresnel);

            // compose CSV line
            string timeStamp = DateTime.UtcNow.ToString("o");
            string line = string.Format(
                "{0},{1:0},{2:F3},{3:F3},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4}",
                timeStamp, Time.frameCount, uv.x, uv.y,
                hueSlow, hueFast, hueErratic, hueCombined,
                n1, n2, n3, flow,
                fresnel,
                finalRgb.x, finalRgb.y, finalRgb.z
            );

            lock (writeLock)
            {
                csvWriter.WriteLine(line);
            }

            // JSON snapshot entry
            var entry = new Dictionary<string, object>
            {
                {"timestamp", timeStamp},
                {"frame", Time.frameCount},
                {"uv", new float[]{uv.x, uv.y}},
                {"hueSlow", hueSlow},
                {"hueFast", hueFast},
                {"hueErratic", hueErratic},
                {"hueCombined", hueCombined},
                {"n1", n1}, {"n2", n2}, {"n3", n3},
                {"flow", flow},
                {"fresnel", fresnel},
                {"rgb", new float[]{finalRgb.x, finalRgb.y, finalRgb.z}},
                {"alpha", finalAlpha}
            };
            snapshot.Add(entry);
        }

        if (writeJsonSnapshots)
        {
            // write one JSON file per sample tick
            string json = JsonUtilityWrapper.ToJson(snapshot); // helper below
            string fileName = Path.Combine(jsonFolder, "TorusSnap_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json");
            File.WriteAllText(fileName, json);
        }
    }

    // Helper fract
    private float Frac(float v) { return v - Mathf.Floor(v); }

    // Small wrapper for JSON because Unity's JsonUtility doesn't support Lists of Dictionaries well.
    static class JsonUtilityWrapper
    {
        public static string ToJson(object o)
        {
            // quick and dirty: simple converter using StringBuilder for arrays/dicts produced above
            // For production use, replace with a proper JSON library (Newtonsoft/json.net or Unity's newer JsonSerializer).
            return MiniJson.Serialize(o);
        }
    }
}
