using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class ResourceQueueEntry
{
    public string id;
    public string topic;
    public string domain;
    public string sourceType;
    public string source;
    public int priority;
    public string status = "queued";
    public bool triggerSimulation = true;
    public bool triggerReflection = true;
    public bool autoRetry = true;
    public int maxRetries = 3;
    public int retryCount = 0;
    public string lastAttempt = "";
    public string ingestedTimestamp = "";
    public string format;
    public string notes;
}

[Serializable]
public class ResourceSource
{
    public string type;
    public string format;
    public string description;
    public string url;
    public bool supportsAutoQueue;
    public bool supportsSimulation;
    public bool supportsReflection;
}

[Serializable]
public class DomainSources
{
    public string status;
    public int defaultPriority;
    public List<ResourceSource> recommendedSources;
}

public class ResourceQueueSeeder : MonoBehaviour
{
    public string registryPath = "UNIVERcity/System/ResourceEndpointRegistry.json";
    public string queuePath = "UNIVERcity/Resource/ResourceQueue.json";

    private string RegistryFilePath =>
        Path.IsPathRooted(registryPath)
            ? registryPath
            : ArTusPathUtility.GetPersistent(registryPath);

    private string QueueFilePath =>
        Path.IsPathRooted(queuePath)
            ? queuePath
            : ArTusPathUtility.GetPersistent(queuePath);

    private void Awake()
    {
        EnsureQueueFilesExist();
    }

    public void GenerateQueueFromRegistry(string domainFilter = null)
    {
        EnsureQueueFilesExist();
        string registryJson = File.ReadAllText(RegistryFilePath);
        Dictionary<string, DomainSources> registry = JsonUtility.FromJson<Wrapper<DomainSources>>(WrapJson(registryJson)).registry;

        List<ResourceQueueEntry> queue = LoadQueue();

        foreach (var kv in registry)
        {
            string domain = kv.Key;
            if (domainFilter != null && !domain.Equals(domainFilter, StringComparison.OrdinalIgnoreCase)) continue;

            DomainSources domainData = kv.Value;
            if (domainData.status != "active") continue;

            foreach (var src in domainData.recommendedSources)
            {
                if (!src.supportsAutoQueue) continue;

                string topic = src.description;
                bool alreadyQueued = queue.Exists(q => q.topic == topic && q.domain == domain);
                if (alreadyQueued) continue;

                ResourceQueueEntry entry = new()
                {
                    id = Guid.NewGuid().ToString(),
                    topic = topic,
                    domain = domain,
                    sourceType = src.type,
                    source = src.url,
                    priority = domainData.defaultPriority,
                    format = src.format,
                    triggerSimulation = src.supportsSimulation,
                    triggerReflection = src.supportsReflection,
                    notes = $"Auto-seeded from registry at {DateTime.Now}"
                };

                queue.Add(entry);
            }
        }

        string updated = JsonUtility.ToJson(new { queue = queue }, true);
        File.WriteAllText(QueueFilePath, updated);
        Debug.Log($"[Seeder] Queue generated. Total entries: {queue.Count}");
    }

    private List<ResourceQueueEntry> LoadQueue()
    {
        if (!File.Exists(QueueFilePath)) return new List<ResourceQueueEntry>();

        string existing = File.ReadAllText(QueueFilePath);
        return JsonUtility.FromJson<QueueWrapper>(existing)?.queue ?? new List<ResourceQueueEntry>();
    }

    private void EnsureQueueFilesExist()
    {
        string registryDir = Path.GetDirectoryName(RegistryFilePath);
        if (!string.IsNullOrWhiteSpace(registryDir) && !Directory.Exists(registryDir))
            Directory.CreateDirectory(registryDir);

        string queueDir = Path.GetDirectoryName(QueueFilePath);
        if (!string.IsNullOrWhiteSpace(queueDir) && !Directory.Exists(queueDir))
            Directory.CreateDirectory(queueDir);

        if (!File.Exists(QueueFilePath))
            File.WriteAllText(QueueFilePath, JsonUtility.ToJson(new QueueWrapper { queue = new List<ResourceQueueEntry>() }, true));

        if (!File.Exists(RegistryFilePath))
        {
            const string sampleRegistry =
                "{\n" +
                "  \"general\": {\n" +
                "    \"status\": \"active\",\n" +
                "    \"defaultPriority\": 50,\n" +
                "    \"recommendedSources\": [\n" +
                "      {\n" +
                "        \"type\": \"url\",\n" +
                "        \"format\": \"html\",\n" +
                "        \"description\": \"systems thinking primer\",\n" +
                "        \"url\": \"https://example.com/systems-thinking\",\n" +
                "        \"supportsAutoQueue\": true,\n" +
                "        \"supportsSimulation\": false,\n" +
                "        \"supportsReflection\": true\n" +
                "      }\n" +
                "    ]\n" +
                "  }\n" +
                "}";

            File.WriteAllText(RegistryFilePath, sampleRegistry);
        }
    }

    private string WrapJson(string json) => $"{{ \"registry\": {json} }}";

    [Serializable]
    private class Wrapper<T>
    {
        public Dictionary<string, T> registry;
    }

    [Serializable]
    private class QueueWrapper
    {
        public List<ResourceQueueEntry> queue;
    }
}
