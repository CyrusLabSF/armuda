using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Networking;

public class ArTusGlobalIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    // ✅ Platform-safe paths (resolved at runtime, NOT static init)
    private string cachePath;
    private string failedPath;
    private string controlPath;

    private readonly Dictionary<string, DateTime> ingestionCache = new();
    private readonly Queue<string> retryQueue = new();
    private readonly Dictionary<string, DateTime> sourceCooldowns = new();
    private readonly Dictionary<string, DateTime> shortCooldowns = new();

    private float ingestionScale = 1.0f;
    private float reflectionDelay = 0.0f;

    [Header("Concurrency")]
    public int maxConcurrentIngestions = 3;
    private int activeIngestions = 0;

    [Header("Retry Safety")]
    public int maxRetryQueueSize = 50;
    public int maxRetryAttempts = 3;

    [Header("Topic Cooldowns")]
    public float shortCooldownSeconds = 30f;
    public float cacheHours = 24f;

    [Header("Control Signal Polling")]
    public float controlInitialDelay = 30f;
    public float controlPollInterval = 120f;

    [Header("Retry Polling")]
    public float retryInitialDelay = 60f;
    public float retryPollInterval = 300f;

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        // ✅ Path resolution moved to Awake (WebGL safe)
        cachePath = ArTusPathUtility.GetPersistent("UNIVERcity/IngestionCache.json");
        failedPath = ArTusPathUtility.GetPersistent("UNIVERcity/FailedIngestions.json");
        controlPath = ArTusPathUtility.GetPersistent("UNIVERcity/Signals/ingestion_control.json");

        EnsureParentDirectory(cachePath);
        EnsureParentDirectory(failedPath);
        EnsureParentDirectory(controlPath);

        LoadCache();
        LoadFailed();

        InvokeRepeating(nameof(ProcessRetryQueue), retryInitialDelay, retryPollInterval);
        InvokeRepeating(nameof(ReadControlSignal), controlInitialDelay, controlPollInterval);
    }

    // =========================================================
    // 🔁 CONTROL SIGNAL
    // =========================================================

    private void ReadControlSignal()
    {
        try
        {
            if (!File.Exists(controlPath))
                return;

            string json = File.ReadAllText(controlPath);
            var control = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            if (control == null)
                return;

            if (control.TryGetValue("scale", out var s))
                ingestionScale = Mathf.Clamp(Convert.ToSingle(s), 0.1f, 5f);

            if (control.TryGetValue("delay", out var d))
                reflectionDelay = Mathf.Max(0f, Convert.ToSingle(d));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GlobalIngestor] Control read failed: {e.Message}");
        }
    }

    // =========================================================
    // 🌍 ENTRY POINT
    // =========================================================

    public void IngestTopic(string topic, string domain = "general")
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        topic = NormalizeTopic(topic);
        domain = NormalizeDomain(domain);

        if (activeIngestions >= maxConcurrentIngestions)
        {
            Debug.Log($"[GlobalIngestor] Max concurrent ingestions reached. Skipping topic: {topic}");
            return;
        }

        if (shortCooldowns.TryGetValue(topic, out var shortUntil) &&
            DateTime.UtcNow < shortUntil)
        {
            return;
        }

        shortCooldowns[topic] = DateTime.UtcNow.AddSeconds(shortCooldownSeconds);

        StartCoroutine(IngestRoutine(topic, domain));
    }

    private IEnumerator IngestRoutine(string topic, string domain)
    {
        activeIngestions++;

        try
        {
            yield return ApplyAdaptiveDelay();

            if (ingestionCache.TryGetValue(topic, out var last) &&
                (DateTime.UtcNow - last).TotalHours < cacheHours)
            {
                yield break;
            }

            ingestionCache[topic] = DateTime.UtcNow;
            SaveCache();

            string query = Uri.EscapeDataString(topic);

            yield return QuerySource(
                "Wikipedia",
                $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={query}&format=json",
                topic,
                domain
            );

            yield return QuerySource(
                "PubMed",
                $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&retmode=json&term={query}",
                topic,
                domain
            );

            yield return QuerySource(
                "OpenLibrary",
                $"https://openlibrary.org/search.json?q={query}",
                topic,
                domain
            );
        }
        finally
        {
            activeIngestions = Mathf.Max(0, activeIngestions - 1);
        }
    }

    // =========================================================
    // 🔎 SOURCE QUERY (WebGL SAFE)
    // =========================================================

    private IEnumerator QuerySource(string source, string url, string topic, string domain, int attempt = 1)
    {
        string cooldownKey = $"{source}|{topic}";

        if (sourceCooldowns.TryGetValue(cooldownKey, out var until) &&
            DateTime.UtcNow < until)
        {
            yield break;
        }

        sourceCooldowns[cooldownKey] =
            DateTime.UtcNow.AddSeconds(Mathf.Max(1f, 2f / ingestionScale));

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;
        req.SetRequestHeader("User-Agent", "ArTus/1.0");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (attempt < maxRetryAttempts)
            {
                yield return new WaitForSeconds(Mathf.Pow(2, attempt));
                yield return QuerySource(source, url, topic, domain, attempt + 1);
            }
            else
            {
                EnqueueRetry(source, url, topic, domain, attempt);
                SaveFailed();
            }

            yield break;
        }

        string json = req.downloadHandler.text;
        string safeTopic = MakeSafeFileName(topic);

        string outputPath =
            ArTusPathUtility.GetPersistent(
                $"UNIVERcity/External/{source}_{safeTopic}.json"
            );

        FileIOManager.QueueWrite(
            outputPath,
            json,
            "GlobalIngest"
        );

        core?.LogMemory(
            $"Ingested {source} data for topic {topic} in domain {domain}.",
            "GlobalIngest",
            2,
            "curious"
        );
    }

    // =========================================================
    // 💤 ADAPTIVE DELAY
    // =========================================================

    private IEnumerator ApplyAdaptiveDelay()
    {
        if (reflectionDelay <= 0f)
            yield break;

        yield return new WaitForSeconds(
            reflectionDelay * UnityEngine.Random.Range(0.8f, 1.2f)
        );
    }

    // =========================================================
    // 🔁 RETRY + CACHE
    // =========================================================

    private void ProcessRetryQueue()
    {
        int count = retryQueue.Count;

        for (int i = 0; i < count; i++)
        {
            if (activeIngestions >= maxConcurrentIngestions)
                break;

            string packed = retryQueue.Dequeue();
            string[] parts = packed.Split('|');

            if (parts.Length != 5)
                continue;

            string source = parts[0];
            string url = parts[1];
            string topic = parts[2];
            string domain = parts[3];

            if (!int.TryParse(parts[4], out int attempt))
                attempt = 1;

            if (attempt >= maxRetryAttempts)
                continue;

            StartCoroutine(QuerySource(source, url, topic, domain, attempt + 1));
        }

        SaveFailed();
    }

    private void EnqueueRetry(string source, string url, string topic, string domain, int attempt)
    {
        if (retryQueue.Count >= maxRetryQueueSize)
            retryQueue.Dequeue();

        retryQueue.Enqueue($"{source}|{url}|{topic}|{domain}|{attempt}");
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(cachePath))
                return;

            var loaded =
                JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(
                    File.ReadAllText(cachePath)
                );

            ingestionCache.Clear();

            if (loaded != null)
            {
                foreach (var kvp in loaded)
                    ingestionCache[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GlobalIngestor] Cache load failed: {e.Message}");
        }
    }

    private void SaveCache()
    {
        FileIOManager.QueueWrite(
            cachePath,
            JsonConvert.SerializeObject(ingestionCache, Formatting.Indented),
            "IngestionCache"
        );
    }

    private void LoadFailed()
    {
        try
        {
            if (!File.Exists(failedPath))
                return;

            var loaded =
                JsonConvert.DeserializeObject<List<string>>(
                    File.ReadAllText(failedPath)
                );

            retryQueue.Clear();

            if (loaded != null)
            {
                foreach (string item in loaded)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                        retryQueue.Enqueue(item);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GlobalIngestor] Failed queue load failed: {e.Message}");
        }
    }

    private void SaveFailed()
    {
        FileIOManager.QueueWrite(
            failedPath,
            JsonConvert.SerializeObject(retryQueue.ToArray(), Formatting.Indented),
            "RetryQueue"
        );
    }

    // =========================================================
    // 🧰 HELPERS
    // =========================================================

    private static string NormalizeTopic(string topic)
    {
        return topic.Trim().ToLower();
    }

    private static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return "general";

        return domain.Trim().ToLower();
    }

    private static string MakeSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(" ", "_");
    }

    private static void EnsureParentDirectory(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}