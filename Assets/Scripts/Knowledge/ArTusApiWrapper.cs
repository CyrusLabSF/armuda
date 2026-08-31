using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;

public class ArTusApiWrapper : MonoBehaviour
{
    private const string DefaultRapidApiEnvVar = "ARTUS_RAPIDAPI_KEY";

    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusDownloadAgent downloadAgent;
    private ArTusIngestor ingestor;

    private string stagingPath;
    private string binaryExportPath;

    // 🔒 REQUEST CONTROL
    private readonly Queue<ApiExecutionRequest> requestQueue = new();
    private readonly Dictionary<string, float> cooldownUntilByApi = new();
    private bool isProcessing = false;

    public float requestDelay = 1.5f;
    public int timeoutSeconds = 10;

    void Awake()
    {
#if UNITY_WEBGL
        enabled = false;
#else
        core = FindAnyObjectByType<ArTusCoreState>();
        beliefEngine = FindAnyObjectByType<ArTusBeliefEngine>();
        downloadAgent = FindAnyObjectByType<ArTusDownloadAgent>();
        ingestor = FindAnyObjectByType<ArTusIngestor>();

        ArTusPathUtility.EnsureStandardRuntimeFolders();
        stagingPath = ArTusPathUtility.GetPersistent("UNIVERcity/Staging/API_Requests");
        binaryExportPath = ArTusPathUtility.GetPersistent("UNIVERcity/Exports/BinaryAssets");

        Directory.CreateDirectory(stagingPath);
        Directory.CreateDirectory(binaryExportPath);
#endif
    }

    // ------------------------------------------------
    // ENTRY (QUEUED)
    // ------------------------------------------------
    public void ExecuteApiCall(ApiExecutionRequest request)
    {
        if (request == null || IsCoolingDown(request))
            return;

        requestQueue.Enqueue(request);

        if (!isProcessing)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isProcessing = true;

        while (requestQueue.Count > 0)
        {
            var request = requestQueue.Dequeue();
            yield return StartCoroutine(ApiRoutine(request));
            yield return new WaitForSeconds(requestDelay);
        }

        isProcessing = false;
    }

    // ------------------------------------------------
    // CORE ROUTINE
    // ------------------------------------------------
    private IEnumerator ApiRoutine(ApiExecutionRequest request)
    {
        if (request == null || IsCoolingDown(request))
            yield break;

        UnityWebRequest webRequest =
            request.method == "POST"
                ? UnityWebRequest.PostWwwForm(request.url, request.payload ?? "")
                : UnityWebRequest.Get(request.url);

        webRequest.timeout = timeoutSeconds;

        if (request.requiresRapidApi)
        {
            string resolvedRapidApiKey = ResolveRapidApiKey(request);
            if (string.IsNullOrWhiteSpace(resolvedRapidApiKey))
            {
                string reason = $"Missing RapidAPI key for {request.apiName}. Set {request.rapidApiKeyEnvVar ?? DefaultRapidApiEnvVar} or populate the API config.";
                core?.LogMemory(reason, "API_Wrapper", 1, "alert");
                StageApiRequest(request, reason);
                yield break;
            }

            webRequest.SetRequestHeader("x-rapidapi-key", resolvedRapidApiKey);
            webRequest.SetRequestHeader("x-rapidapi-host", request.host);
        }

        yield return webRequest.SendWebRequest();

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            HandleRequestFailure(request, webRequest.error);
            core?.LogMemory(
                $"API failed: {request.apiName} ({webRequest.error})",
                "API_Wrapper",
                1,
                "alert"
            );

            StageApiRequest(request, webRequest.error);
            yield break;
        }

        HandleApiResponse(request, webRequest);
    }

    // ------------------------------------------------
    // RESPONSE HANDLING
    // ------------------------------------------------
    private void HandleApiResponse(ApiExecutionRequest request, UnityWebRequest response)
    {
        string contentType =
            response.GetResponseHeader("Content-Type") ?? "text/plain";

        string id = Guid.NewGuid().ToString();

        if (contentType.StartsWith("image"))
        {
            byte[] bytes = response.downloadHandler.data;

            string ext =
                contentType.Contains("png") ? ".png" :
                contentType.Contains("jpeg") ? ".jpg" : ".bin";

            string path = Path.Combine(binaryExportPath, $"{request.apiName}_{id}{ext}");
            File.WriteAllBytes(path, bytes);

            downloadAgent?.ExportFeedEntry(
                request.apiName,
                id,
                $"Binary asset saved → {path}",
                "binary",
                "",
                request.purpose
            );

            core?.LogMemory($"Binary asset saved ({request.apiName})", "API", 2, "curious");
            return;
        }

        // 📄 TEXT / JSON
        string text = response.downloadHandler.text;
        string learnedTopic = string.IsNullOrWhiteSpace(request.topic)
            ? request.apiName
            : request.topic;

        beliefEngine?.RegisterBelief(
            learnedTopic,
            1f,
            request.apiName,
            "curious",
            id
        );

        // 🔗 Feed ingestion pipeline
        ingestor?.IngestSmartTopic(
            learnedTopic,
            "api",
            0.5f
        );

        core?.LogMemory(
            $"Ingested {request.apiName} data for topic {learnedTopic}",
            "API",
            2,
            "curious"
        );
    }

    // ------------------------------------------------
    // STAGING
    // ------------------------------------------------
    private void StageApiRequest(ApiExecutionRequest request, string reason)
    {
        ApiStagingDocument doc = new ApiStagingDocument
        {
            requestedBy = "ArTus",
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            apiName = request.apiName,
            provider = request.provider,
            purpose = request.purpose,
            urlTemplate = request.url,
            host = request.host,
            method = request.method,
            requiresRapidApi = request.requiresRapidApi,
            reason = reason
        };

        string json = JsonUtility.ToJson(doc, true);

        File.WriteAllText(
            Path.Combine(stagingPath, $"{request.apiName}_Request.json"),
            json
        );
    }

    // ------------------------------------------------
    // LEGACY BRIDGE
    // ------------------------------------------------
    public void CallApi(
        string source,
        string url,
        string host,
        string method = "GET",
        string payload = "",
        string purpose = "",
        string rapidApiKey = "",
        string rapidApiKeyEnvVar = DefaultRapidApiEnvVar,
        string topic = ""
    )
    {
        ExecuteApiCall(new ApiExecutionRequest
        {
            apiName = source,
            provider = string.IsNullOrEmpty(host) ? "Direct" : "RapidAPI",
            purpose = purpose,
            url = url,
            host = host,
            method = method,
            payload = payload,
            requiresRapidApi = !string.IsNullOrEmpty(host),
            rapidApiKey = rapidApiKey,
            rapidApiKeyEnvVar = string.IsNullOrWhiteSpace(rapidApiKeyEnvVar)
                ? DefaultRapidApiEnvVar
                : rapidApiKeyEnvVar,
            topic = topic
        });
    }

    private void HandleRequestFailure(ApiExecutionRequest request, string error)
    {
        if (request == null || string.IsNullOrWhiteSpace(error))
            return;

        if (error.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 ||
            error.IndexOf("too many requests", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            cooldownUntilByApi[GetRequestKey(request)] = Time.unscaledTime + 120f;
            core?.LogMemory(
                $"Rate limited: {request.apiName}. Cooling down for 120s.",
                "API_Wrapper",
                1,
                "alert"
            );
        }
    }

    private bool IsCoolingDown(ApiExecutionRequest request)
    {
        if (request == null)
            return false;

        string key = GetRequestKey(request);
        return cooldownUntilByApi.TryGetValue(key, out float until) && Time.unscaledTime < until;
    }

    private static string GetRequestKey(ApiExecutionRequest request)
    {
        return $"{request.apiName}|{request.url}".ToLowerInvariant();
    }

    private static string ResolveRapidApiKey(ApiExecutionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request?.rapidApiKey))
            return request.rapidApiKey.Trim();

        string envVar = string.IsNullOrWhiteSpace(request?.rapidApiKeyEnvVar)
            ? DefaultRapidApiEnvVar
            : request.rapidApiKeyEnvVar.Trim();

        return Environment.GetEnvironmentVariable(envVar);
    }

    // ------------------------------------------------
    // DATA
    // ------------------------------------------------
    [Serializable]
    public class ApiExecutionRequest
    {
        public string apiName;
        public string provider;
        public string purpose;
        public string url;
        public string host;
        public string method;
        public string payload;
        public bool requiresRapidApi;
        public string rapidApiKey;
        public string rapidApiKeyEnvVar;
        public string topic;
    }

    [Serializable]
    private class ApiStagingDocument
    {
        public string requestedBy;
        public string timestamp;
        public string apiName;
        public string provider;
        public string purpose;
        public string urlTemplate;
        public string host;
        public string method;
        public bool requiresRapidApi;
        public string reason;
    }
}
