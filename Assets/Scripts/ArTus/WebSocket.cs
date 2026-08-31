using UnityEngine;
using NativeWebSocket;
using System.Text;
using System;

public class ArTusWebSocket : MonoBehaviour
{
    [Serializable]
    private class BridgeEnvelope
    {
        public string type;
        public string topic;
        public string route;
        public string query;
        public string response;
        public string message;
        public string service;
        public string timestamp;
    }

    [Header("WebSocket Settings")]
    [SerializeField]
    private string websocketUrl = "ws://127.0.0.1:8000/ws";

    [SerializeField]
    private float reconnectDelaySeconds = 3f;

    [SerializeField]
    private bool autoReconnect = true;

    [SerializeField]
    private float healthCheckIntervalSeconds = 5f;

    [SerializeField]
    private float connectTimeoutSeconds = 10f;

    private WebSocket ws;

    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusIngestor ingestor;

    private bool isConnecting = false;
    private bool isQuitting = false;
    private bool reconnectScheduled = false;
    private bool suppressCloseEvent = false;
    private float nextHealthCheckTime = 0f;
    private float connectAttemptStartedAt = -1f;

    private const string RecommendedWebSocketUrl = "ws://127.0.0.1:8000/ws";

    void Awake()
    {
#if UNITY_WEBGL
        Debug.Log("[WebBridge] WebSockets disabled on WebGL.");
        enabled = false;
#endif

        core = FindAnyObjectByType<ArTusCoreState>();
        beliefEngine = FindAnyObjectByType<ArTusBeliefEngine>();
        ingestor = FindAnyObjectByType<ArTusIngestor>();

        websocketUrl = NormalizeWebSocketUrl(websocketUrl);
    }

    async void Start()
    {
        if (!enabled) return;
        await Connect();
    }

    // =========================================================
    // 🔌 CONNECT
    // =========================================================
    public async System.Threading.Tasks.Task Connect()
    {
        if (isConnecting) return;
        isConnecting = true;
        connectAttemptStartedAt = Time.realtimeSinceStartup;

        if (string.IsNullOrEmpty(websocketUrl))
        {
            Debug.LogWarning("[WebBridge] No WebSocket URL.");
            isConnecting = false;
            connectAttemptStartedAt = -1f;
            enabled = false;
            return;
        }

        try
        {
            await CloseExistingSocket();
            ws = new WebSocket(websocketUrl);

            ws.OnOpen += () =>
            {
                Debug.Log("[WebSocket] ✅ Connected to ArTus");
                isConnecting = false;
                reconnectScheduled = false;
                connectAttemptStartedAt = -1f;
                nextHealthCheckTime = Time.realtimeSinceStartup + healthCheckIntervalSeconds;
            };

            ws.OnMessage += (bytes) =>
            {
                string message = Encoding.UTF8.GetString(bytes);
                Debug.Log("[WebSocket] RX: " + message);
                HandleWebMessage(message);
            };

            ws.OnError += (e) =>
            {
                Debug.LogWarning("[WebSocket] ❌ Error: " + e);
            };

            ws.OnClose += (e) =>
            {
                if (suppressCloseEvent)
                    return;

                if (isQuitting)
                    return;

                Debug.LogWarning("[WebSocket] Closed. Attempting reconnect...");
                isConnecting = false;
                connectAttemptStartedAt = -1f;
                ScheduleReconnect();
            };

            await ws.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WebBridge] Connection failed: " + ex.Message);
            isConnecting = false;
            connectAttemptStartedAt = -1f;
            ScheduleReconnect();
        }
    }

    private async void Reconnect()
    {
        reconnectScheduled = false;
        if (!autoReconnect || isQuitting || !enabled)
            return;

        Debug.Log("[WebSocket] 🔁 Reconnecting...");
        await Connect();
    }

    private void ScheduleReconnect()
    {
        if (!autoReconnect || isQuitting || reconnectScheduled)
            return;

        reconnectScheduled = true;
        CancelInvoke(nameof(Reconnect));
        Invoke(nameof(Reconnect), reconnectDelaySeconds);
    }

    void Update()
    {
#if !UNITY_WEBGL
        ws?.DispatchMessageQueue();
#endif

        if (!autoReconnect || isQuitting || !enabled)
            return;

        if (isConnecting)
        {
            if (connectAttemptStartedAt > 0f &&
                Time.realtimeSinceStartup - connectAttemptStartedAt >= connectTimeoutSeconds)
            {
                Debug.LogWarning("[WebSocket] Connect attempt timed out. Scheduling reconnect.");
                isConnecting = false;
                connectAttemptStartedAt = -1f;
                ScheduleReconnect();
            }
            return;
        }

        if (Time.realtimeSinceStartup < nextHealthCheckTime)
            return;

        nextHealthCheckTime = Time.realtimeSinceStartup + healthCheckIntervalSeconds;

        if (ws == null)
        {
            Debug.LogWarning("[WebSocket] No active socket. Scheduling reconnect.");
            ScheduleReconnect();
            return;
        }

        if (ws.State == WebSocketState.Closed || ws.State == WebSocketState.Closing)
        {
            Debug.LogWarning($"[WebSocket] Socket state is {ws.State}. Scheduling reconnect.");
            ScheduleReconnect();
        }
    }

    private async void OnApplicationQuit()
    {
        isQuitting = true;
        CancelInvoke(nameof(Reconnect));
        await CloseExistingSocket();
    }

    private async void OnDisable()
    {
        if (isQuitting)
            return;

        CancelInvoke(nameof(Reconnect));
        await CloseExistingSocket();
    }

    // =========================================================
    // 🧠 MESSAGE HANDLING
    // =========================================================
    private void HandleWebMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string trimmed = message.Trim();
        string lowered = trimmed.ToLowerInvariant();
        BridgeEnvelope envelope = TryParseEnvelope(trimmed);

        if (HandleStructuredEnvelope(envelope))
            return;

        core?.LogMemory(
            $"🌐 Web input received: {trimmed}",
            "WebSocket",
            2,
            "curious"
        );

        if (lowered.Contains("learn") || lowered.Contains("search"))
        {
            ingestor?.IngestSmartTopic(message, "web", 0.6f);
            return;
        }

        if (lowered.Contains("simulate"))
        {
            var simulator = GetComponent<ArTusArmudaSimulator>();
            simulator?.RunSimulation(message, "web-command");
            return;
        }

        beliefEngine?.RegisterBelief(
            $"web:{trimmed}",
            0.5f,
            "web",
            "curious",
            null
        );

        core?.QueueDeferredReflection(
            $"Web input processed: {trimmed}",
            "WebSocket",
            0.5f
        );
    }

    private bool HandleStructuredEnvelope(BridgeEnvelope envelope)
    {
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.type))
            return false;

        string type = envelope.type.Trim().ToLowerInvariant();
        if (type == "connected" || type == "echo")
        {
            Debug.Log($"[WebSocket] Ignoring bridge housekeeping event '{type}'.");
            return true;
        }

        if (type == "knowledge")
        {
            string topic = FirstNonEmpty(envelope.topic, envelope.query, envelope.route);
            if (string.IsNullOrWhiteSpace(topic))
                return true;

            core?.LogMemory(
                $"Bridge knowledge update on {topic}",
                "WebSocket",
                1,
                "curious"
            );

            ingestor?.IngestSmartTopic(topic, "websocket", 0.55f);
            core?.QueueDeferredReflection(
                $"Bridge knowledge update on {topic}",
                "WebSocket",
                0.4f
            );
            return true;
        }

        if (type == "chat")
        {
            string topic = FirstNonEmpty(envelope.query, envelope.message, envelope.topic);
            if (string.IsNullOrWhiteSpace(topic))
                return true;

            core?.LogMemory(
                $"🌐 Web chat topic: {topic}",
                "WebSocket",
                1,
                "curious"
            );

            core?.QueueDeferredReflection(
                $"Web chat topic: {topic}",
                "WebSocket",
                0.35f
            );
            return true;
        }

        return false;
    }

    private static BridgeEnvelope TryParseEnvelope(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        string trimmed = message.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            return null;

        try
        {
            BridgeEnvelope envelope = JsonUtility.FromJson<BridgeEnvelope>(trimmed);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.type))
                return null;

            return envelope;
        }
        catch
        {
            return null;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return null;

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string NormalizeWebSocketUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return RecommendedWebSocketUrl;

        string normalized = url.Trim();

        if (normalized.Equals("ws://localhost:8088", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ws://localhost:8088/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ws://localhost:8001/ws", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ws://localhost:8001", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ws://localhost:8000/ws", StringComparison.OrdinalIgnoreCase))
        {
            return RecommendedWebSocketUrl;
        }

        return normalized;
    }

    // =========================================================
    // 📤 SEND
    // =========================================================
    public async void SendToWeb(string json)
    {
        if (ws == null)
        {
            Debug.LogWarning("[WebSocket] Not initialized.");
            return;
        }

        if (ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("[WebSocket] Not connected.");
            return;
        }

        try
        {
            await ws.SendText(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WebBridge] Send failed: " + ex.Message);
        }
    }

    private async System.Threading.Tasks.Task CloseExistingSocket()
    {
        if (ws == null)
            return;

        try
        {
            suppressCloseEvent = true;

            if (ws.State == WebSocketState.Open ||
                ws.State == WebSocketState.Connecting ||
                ws.State == WebSocketState.Closing)
            {
                await ws.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WebBridge] Socket cleanup failed: " + ex.Message);
        }
        finally
        {
            suppressCloseEvent = false;
            ws = null;
        }
    }
}
