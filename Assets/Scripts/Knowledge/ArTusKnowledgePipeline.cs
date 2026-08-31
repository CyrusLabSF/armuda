using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArTusKnowledgePipeline : MonoBehaviour
{
    [Header("Core References")]
    public ArTusCoreState core;
    public ArTusCuriosityEngine curiosity;
    public ArTusDomainReflector reflector;
    public ArTusSpeechResponder speech;

    [Header("Pipeline Controls")]
    public bool stableMode = true;
    public bool enableReflection = true;
    public bool enableSpeech = false;

    [Header("Timing")]
    public float ingestInterval = 6f;

    [Header("Queue Safety")]
    public int maxQueueSize = 25;
    public float topicCooldownTime = 60f;

    private readonly Queue<string> topicQueue = new();
    private readonly HashSet<string> topicSet = new();
    private readonly Dictionary<string, float> topicCooldown = new();

    private float lastIngestTime;
    private bool isRunning;

    private float lastReflectionTime = 0f;
    public float reflectionCooldown = 5f;

    private float lastSpeechTime = 0f;
    public float speechCooldown = 10f;

    private void Awake()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>();
    }

    private void Start()
    {
        BeginPipeline();
    }

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time - lastIngestTime >= ingestInterval)
        {
            lastIngestTime = Time.time;
            ProcessNextTopic();
        }
    }

    public void BeginPipeline()
    {
        if (isRunning) return;

        isRunning = true;
        lastIngestTime = Time.time;
    }

    public void StopPipeline()
    {
        isRunning = false;
    }

    public void PullFromCuriosity(string domainHint = "general")
    {
        if (curiosity == null) return;

        var topics = curiosity.GetCuriosityTopics();
        if (topics == null || topics.Count == 0) return;

        foreach (string topic in topics)
        {
            EnqueueTopic(topic);
        }
    }

    public void EnqueueTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return;

        topic = topic.Trim().ToLower();

        if (topic.Length > 120)
            topic = topic.Substring(0, 120);

        if (topicCooldown.TryGetValue(topic, out float lastTime))
        {
            if (Time.time - lastTime < topicCooldownTime)
                return;
        }

        if (topicQueue.Count >= maxQueueSize)
            return;

        if (!topicSet.Contains(topic))
        {
            topicQueue.Enqueue(topic);
            topicSet.Add(topic);
            topicCooldown[topic] = Time.time;
        }
    }

    private void ProcessNextTopic()
    {
        if (topicQueue.Count == 0) return;

        string topic = topicQueue.Dequeue();
        topicSet.Remove(topic);

        if (stableMode)
        {
            topicQueue.Enqueue(topic);
            topicSet.Add(topic);
            return;
        }

        if (enableSpeech && Time.time - lastSpeechTime > speechCooldown)
        {
            speech?.Speak($"Beginning learning for {topic}.");
            lastSpeechTime = Time.time;
        }

        if (!enableReflection || reflector == null)
            return;

        if (Time.time - lastReflectionTime < reflectionCooldown)
            return;

        float imbalance = core?.GetEmotionalImbalance() ?? 0f;

        if (imbalance < 0.6f)
        {
            reflector.ReflectOnDomain(topic);
            lastReflectionTime = Time.time;
        }
    }

    private float GetQueuePressure()
    {
        return Mathf.Clamp01(topicQueue.Count / 10f);
    }

    public int GetQueuedTopicCount() => topicQueue.Count;

    public bool IsRunning() => isRunning;
}