using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ArTusApiManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField]
    private string configRelativePath =
        "UNIVERcity/Configs/ArTusApiConfig.json";

    [Header("Execution")]
    public float apiDelaySeconds = 2f;

    [Header("Control")]
    public bool runStagesSequentially = true;

    private string configPath;
    private string sharedConfigPath;

    private ArTusApiWrapper apiWrapper;
    private ArTusCoreState core;
    private ArTusIngestor ingestor;

    private ApiConfigRoot configRoot;

    private bool isRunning = false;
    private readonly HashSet<string> runningStages = new();

    // ------------------------------------------------
    // INIT
    // ------------------------------------------------
    void Awake()
    {
        apiWrapper = FindAnyObjectByType<ArTusApiWrapper>();
        core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
        ingestor = GetComponent<ArTusIngestor>() ?? FindAnyObjectByType<ArTusIngestor>();

        if (apiWrapper == null)
            Debug.LogError("[API Manager] ArTusApiWrapper not found.");

        configPath = ArTusPathUtility.GetPersistent(configRelativePath);
        sharedConfigPath = ArTusPathUtility.GetDev("UNIVERcity/Configs/ArTusApiConfig.json");

        LoadConfig();
    }

    // ------------------------------------------------
    // CONFIG LOADING
    // ------------------------------------------------
    private void LoadConfig()
    {
        string preferredPath = ResolvePreferredConfigPath();

        if (!File.Exists(preferredPath))
        {
            CreateDefaultConfig();
            Debug.LogWarning($"[API Manager] Config file not found. Template created at: {configPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(preferredPath);
            configRoot = JsonUtility.FromJson<ApiConfigRoot>(json);

            if (!IsConfigValid(configRoot))
            {
                if (!string.IsNullOrWhiteSpace(sharedConfigPath) &&
                    !string.Equals(preferredPath, configPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[API Manager] Shared config invalid at: {preferredPath}");
                    CreateDefaultConfig();
                }
                else
                {
                    CreateDefaultConfig();
                    Debug.LogWarning($"[API Manager] Config invalid. Template reset at: {configPath}");
                }
                return;
            }

            Debug.Log($"[API Manager] Loaded {configRoot.stages.Count} stages from: {preferredPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[API Manager] Load failed: {ex.Message}");
            CreateDefaultConfig();
        }
    }

    private string ResolvePreferredConfigPath()
    {
        if (!string.IsNullOrWhiteSpace(sharedConfigPath) && File.Exists(sharedConfigPath))
            return sharedConfigPath;

        return configPath;
    }

    private bool IsConfigValid(ApiConfigRoot root)
    {
        if (root == null || root.stages == null)
            return false;

        foreach (var stage in root.stages)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.stageName) || stage.apis == null)
                return false;
        }

        return true;
    }

    private void CreateDefaultConfig()
    {
        string dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        configRoot = new ApiConfigRoot
        {
            stages = new List<ApiStage>
            {
                new ApiStage
                {
                    stageName = "sample_stage",
                    apis = new List<ApiConfig>
                    {
                        new ApiConfig
                        {
                            name = "ExampleKnowledgeSource",
                            url = "https://example.com/api/topic",
                            host = "",
                            method = "GET",
                            rapidApiKeyEnvVar = "ARTUS_RAPIDAPI_KEY"
                        }
                    }
                }
            }
        };

        File.WriteAllText(configPath, JsonUtility.ToJson(configRoot, true));
    }

    // ------------------------------------------------
    // PUBLIC EXECUTION
    // ------------------------------------------------
    public void RunStage(string stageName)
    {
        if (configRoot == null || apiWrapper == null)
            return;

        if (runningStages.Contains(stageName))
            return;

        ApiStage stage =
            configRoot.stages.Find(s => s.stageName == stageName);

        if (stage == null)
        {
            Debug.LogWarning($"[API Manager] Stage '{stageName}' not found.");
            return;
        }

        if (!stage.enabled)
        {
            Debug.Log($"[API Manager] Stage '{stageName}' is disabled.");
            return;
        }

        runningStages.Add(stageName);

        core?.LogMemory(
            $"🌐 API stage started: {stage.stageName}",
            "ApiStage",
            2,
            "curious"
        );

        StartCoroutine(RunStageCoroutine(stage));
    }

    public void RunAllStages()
    {
        if (configRoot == null || configRoot.stages == null)
            return;

        if (runStagesSequentially)
        {
            if (!isRunning)
                StartCoroutine(RunAllSequential());
        }
        else
        {
            foreach (var stage in configRoot.stages)
                RunStage(stage.stageName);
        }
    }

    public void RunRelevantStageForTopic(string topic)
    {
        if (configRoot?.stages == null || string.IsNullOrWhiteSpace(topic))
            return;

        string normalized = topic.Trim().ToLowerInvariant();
        ApiStage stage =
            configRoot.stages.FirstOrDefault(s =>
                s != null &&
                !string.IsNullOrWhiteSpace(s.stageName) &&
                string.Equals(s.stageName, topic, System.StringComparison.OrdinalIgnoreCase))
            ?? configRoot.stages.FirstOrDefault(s =>
                s != null &&
                ((s.description ?? string.Empty).ToLowerInvariant().Contains(normalized) ||
                 (s.stageName ?? string.Empty).ToLowerInvariant().Contains(normalized)))
            ?? configRoot.stages.FirstOrDefault(s =>
                s != null &&
                s.apis != null &&
                s.apis.Any(api =>
                    api != null &&
                    !string.IsNullOrWhiteSpace(api.domain) &&
                    (normalized.Contains(api.domain.ToLowerInvariant()) ||
                     api.domain.ToLowerInvariant().Contains(normalized))))
            ?? configRoot.stages.FirstOrDefault(s =>
                s != null &&
                string.Equals(s.stageName, "Foundations", System.StringComparison.OrdinalIgnoreCase))
            ?? configRoot.stages.FirstOrDefault(s => s != null && s.enabled);

        if (stage != null)
            RunStage(stage.stageName);
    }

    private IEnumerator RunAllSequential()
    {
        isRunning = true;

        foreach (var stage in configRoot.stages)
        {
            yield return StartCoroutine(RunStageCoroutine(stage));
        }

        isRunning = false;
    }

    // ------------------------------------------------
    // EXECUTION COROUTINE
    // ------------------------------------------------
    private IEnumerator RunStageCoroutine(ApiStage stage)
    {
        int index = 0;

        foreach (var api in stage.apis)
        {
            if (api == null || !api.enabled)
                continue;

            index++;

            string context =
                $"Stage '{stage.stageName}' ({index}/{stage.apis.Count})";

            string resolvedTopic = string.IsNullOrWhiteSpace(api.queryTopic)
                ? api.name
                : api.queryTopic;
            string resolvedUrl = ResolveApiUrl(api, resolvedTopic);

            Debug.Log($"[API Manager] Executing {api.name} ({context})");

            apiWrapper.CallApi(
                api.name,
                resolvedUrl,
                api.host,
                api.method,
                "",
                context,
                api.rapidApiKey,
                api.rapidApiKeyEnvVar,
                resolvedTopic
            );

            // 🧠 Optional cognitive tie-in
            ingestor?.IngestSmartTopic(
                resolvedTopic,
                string.IsNullOrWhiteSpace(api.domain) ? "api" : api.domain,
                0.4f
            );

            yield return new WaitForSeconds(apiDelaySeconds);
        }

        core?.LogMemory(
            $"🌐 API stage completed: {stage.stageName}",
            "ApiStageComplete",
            2,
            "satisfied"
        );

        runningStages.Remove(stage.stageName);
    }

    // ------------------------------------------------
    // CONFIG DATA MODELS
    // ------------------------------------------------
    [System.Serializable]
    public class ApiConfigRoot
    {
        public List<ApiStage> stages = new();
    }

    [System.Serializable]
    public class ApiStage
    {
        public string stageName;
        public bool enabled = true;
        public string description;
        public List<ApiConfig> apis = new();
    }

    [System.Serializable]
    public class ApiConfig
    {
        public bool enabled = true;
        public string name;
        public string domain;
        public string queryTopic;
        public string url;
        public string host;
        public string method = "GET";
        public string rapidApiKey;
        public string rapidApiKeyEnvVar = "ARTUS_RAPIDAPI_KEY";
        public string sourceTier = "core";
        public string trustLevel = "medium";
        public string notes;
    }

    private static string ResolveApiUrl(ApiConfig api, string topic)
    {
        if (api == null || string.IsNullOrWhiteSpace(api.url))
            return string.Empty;

        string safeTopic = string.IsNullOrWhiteSpace(topic)
            ? "systems thinking"
            : topic.Trim();

        return api.url
            .Replace("{topic}", UnityEngine.Networking.UnityWebRequest.EscapeURL(safeTopic))
            .Replace("{topic_raw}", safeTopic);
    }
}
