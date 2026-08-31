using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Domain rotation manager for ArTus
/// - Loads domain list from file or UNIVERcity folders
/// - Rotates intelligently with cooldowns & hysteresis to prevent rapid loops
/// - Logs every rotation to CSV for Power BI
/// - Triggers ingestion + reflection + growth logging
/// </summary>
public class DomainRotationScheduler : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationCooldownMinutes = 5f;       // prevent rapid cycling
    public float rotationIntervalSeconds = 120f;     // check interval
    public int avoidRecentCount = 5;                 // prevent repeats
    public float activityHighThreshold = 9f;         // trigger spike
    public float activityLowThreshold = 4f;          // must drop below this before retrigger

    private string currentDomain = "general";
    private float domainTimer = 0f;
    private Dictionary<string, DateTime> lastRotationTimes = new();
    private Queue<string> recentDomains = new();

    private bool inSpikeState = false;   // hysteresis tracking

    // Paths
    private string domainListPath = "D:/ArTusCloud-Deployment/UNIVERcity/Config/DomainList.txt";
    private string domainLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/DomainRotationLog.csv";
    private string folderLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/CreatedFolders.csv";

    // References
    private ArTusGlobalIngestor ingestor;
    private ArTusCoreState core;
    private ArTusGrowthLogger growthLogger;
    private ArTusSpeechResponder speech;

    public List<string> domainPool = new();

    void Start()
    {
        ingestor = GetComponent<ArTusGlobalIngestor>();
        core = GetComponent<ArTusCoreState>();
        growthLogger = GetComponent<ArTusGrowthLogger>();
        speech = GetComponent<ArTusSpeechResponder>();

        // Init CSV headers if missing
        if (!File.Exists(domainLogPath))
            FileIOManager.QueueWrite(domainLogPath, "Timestamp,Domain,Reason\n", "DomainRotationHeader");

        if (!File.Exists(folderLogPath))
            FileIOManager.QueueWrite(folderLogPath, "Timestamp,Folder\n", "DomainFolderHeader");

        // Load domains
        LoadDomainPoolFromFile();
        if (domainPool.Count == 0)
            LoadDomainPoolFromUNIVERcityFolders();

        if (domainPool.Count == 0)
        {
            Debug.LogError("[DomainRotation] ❌ No domains available — scheduler disabled.");
            return;
        }

        foreach (var d in domainPool)
            lastRotationTimes[d] = DateTime.MinValue;

        domainTimer = 0f;
        Debug.Log($"[DomainRotation] Loaded {domainPool.Count} domains.");
    }

    void Update()
    {
        domainTimer += Time.deltaTime;

        if (domainTimer >= rotationIntervalSeconds)
        {
            RotateDomain("interval");
            domainTimer = 0f;
        }

        if (core != null)
        {
            float activity = core.activityScore;

            if (!inSpikeState && activity > activityHighThreshold)
            {
                Debug.Log("[DomainRotation] ⚡ Activity spike detected. Switching domain.");
                RotateDomain("activity spike");
                domainTimer = 0f;
                inSpikeState = true;
            }
            else if (inSpikeState && activity < activityLowThreshold)
            {
                // Reset state so future spikes can trigger
                inSpikeState = false;
                Debug.Log("[DomainRotation] Activity cooled below threshold, ready for next spike.");
            }
        }
    }

    private void RotateDomain(string reason)
    {
        if (domainPool.Count == 0)
        {
            Debug.LogWarning("[DomainRotation] No domains in pool.");
            return;
        }

        var eligible = domainPool
            .Where(d => (DateTime.UtcNow - lastRotationTimes[d]).TotalMinutes >= rotationCooldownMinutes)
            .Where(d => !recentDomains.Contains(d))
            .OrderBy(d => lastRotationTimes[d])
            .ToList();

        if (eligible.Count == 0)
        {
            Debug.Log("[DomainRotation] All domains cooling down — reflection instead.");
            core?.LogReflection("Cooldown reached — reflecting instead of switching.");
            return;
        }

        string nextDomain = eligible[UnityEngine.Random.Range(0, eligible.Count)];

        // Reflection + growth before switching
        core?.LogReflection($"Leaving domain: {currentDomain}");
        growthLogger?.CompareBeliefChange();

        // Rotate
        currentDomain = nextDomain;
        lastRotationTimes[currentDomain] = DateTime.UtcNow;

        recentDomains.Enqueue(currentDomain);
        if (recentDomains.Count > avoidRecentCount)
            recentDomains.Dequeue();

        Debug.Log($"[DomainRotation] ➤ Rotated to {currentDomain} (Reason: {reason})");
        speech?.RequestSpeak(
            $"Now exploring {currentDomain}.",
            ArTusSpeechResponder.SpeechCategory.System
        );

        // Trigger ingestion
        ingestor?.IngestTopic(currentDomain);

        // Async CSV log
        string logLine = $"{DateTime.Now},{currentDomain},{reason}\n";
        FileIOManager.QueueWrite(domainLogPath, logLine, "DomainRotation", append: true);

        // Ensure folder exists + log it
        string domainPath = $"D:/ArTusCloud-Deployment/UNIVERcity/{currentDomain}/";
        if (!Directory.Exists(domainPath))
        {
            Directory.CreateDirectory(domainPath);
            string folderLine = $"{DateTime.Now},{currentDomain}\n";
            FileIOManager.QueueWrite(folderLogPath, folderLine, "DomainFolder", append: true);
            Debug.Log($"[DomainRotation] 📁 Created folder for new domain: {currentDomain}");
        }

        // ✅ Diplomatic Trail Integration (Unity 6 safe)
        var cyberSpace = FindAnyObjectByType<CyberSpaceManager>();
        cyberSpace?.RegisterDiplomaticEvent(
            "DomainRotationScheduler",
            "Domain Switch",
            $"Rotated to {currentDomain} (Reason: {reason})",
            "curious"
        );
    }

    private void LoadDomainPoolFromFile()
    {
        if (!File.Exists(domainListPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(domainListPath));
            domainPool = new List<string> { "philosophy", "biology", "history", "emotion", "cybersecurity" };
            FileIOManager.QueueWrite(domainListPath, string.Join("\n", domainPool), "DomainListInit");
            Debug.Log("[DomainRotation] Initialized domain list with defaults.");
        }
        else
        {
            domainPool = new List<string>(File.ReadAllLines(domainListPath));
            Debug.Log($"[DomainRotation] Loaded {domainPool.Count} domains from DomainList.txt");
        }
    }

    private void LoadDomainPoolFromUNIVERcityFolders()
    {
        string rootPath = "D:/ArTusCloud-Deployment/UNIVERcity/";
        if (!Directory.Exists(rootPath)) return;

        domainPool = Directory.GetDirectories(rootPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        Debug.Log($"[DomainRotation] Auto-loaded {domainPool.Count} folders as domains.");
    }
}
