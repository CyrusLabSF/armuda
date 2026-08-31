using System;
using System.Collections.Generic;

[Serializable]
public class ResourceEndpoint
{
    public string id;                      // Unique internal ID for tracking
    public string topic;                   // The subject or learning focus
    public string domain;                  // Associated domain (e.g., biology, ethics, etc.)
    public string sourceType;              // "url", "local", "api", "zip", "pdf", "txt"
    public string source;                  // Raw URL, filepath, or API endpoint

    public int priority = 1;               // Higher = more urgent
    public string status = "pending";      // "pending", "ingested", "failed", "skipped"
    public bool triggerSimulation = false; // If true, auto-run simulation post-ingest
    public bool triggerReflection = false; // If true, schedule reflection after ingestion

    public bool autoRetry = true;          // Should it attempt retries?
    public int maxRetries = 3;             // Max number of attempts before failure
    public int retryCount = 0;             // How many attempts have been made

    public string lastAttempt;             // Timestamp of last attempt
    public string ingestedTimestamp;       // When it was successfully ingested
    public string format = "txt";          // Format of resource ("pdf", "txt", "zip", etc.)

    public string notes;                   // Optional: semantic trail, error messages, annotations
    public string emotionTag = "neutral";  // Emotion associated after processing (optional)
    public float confidence = 0f;          // Ingest success or understanding score
    public int clarityScore = 0;           // Derived post-ingestion (e.g., from summary clarity or parsing success)
}

[Serializable]
public class ResourceQueueWrapper
{
    public List<ResourceEndpoint> queue = new List<ResourceEndpoint>();
}
