using UnityEngine;
using System;

/// <summary>
/// Central Unity-side controller for ArTus.
/// Manages references to core subsystems and orchestrates domain ingestion
/// and knowledge pipeline events.
/// </summary>
public class ArTusController : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusObserver observer;
    private ArTusApiManager apiManager;
    private ArTusIngestor ingestor;
    private ArTusKnowledgePipeline knowledgePipeline;
    private ArTusDownloadAgent downloadAgent;
    private ArTusPurposeLoop purposeLoop;
    private ArTusGoalController goalController;
    private ArTusGoalExecutor goalExecutor;

    [Header("Controller Settings")]
    public bool autoStart = true;
    public bool enableDebug = true;

    void Awake()
    {
        ArTusPathUtility.EnsureStandardRuntimeFolders();

        core = FindAnyObjectByType<ArTusCoreState>();
        observer = FindAnyObjectByType<ArTusObserver>();
        apiManager = FindAnyObjectByType<ArTusApiManager>();
        ingestor = FindAnyObjectByType<ArTusIngestor>();
        knowledgePipeline = FindAnyObjectByType<ArTusKnowledgePipeline>();
        downloadAgent = FindAnyObjectByType<ArTusDownloadAgent>();
        purposeLoop = FindAnyObjectByType<ArTusPurposeLoop>();
        goalController = FindAnyObjectByType<ArTusGoalController>();
        goalExecutor = FindAnyObjectByType<ArTusGoalExecutor>();

        if (enableDebug)
        {
            Debug.Log("[ArTusController] Subsystems initialized.");

            if (core == null) Debug.LogWarning("[ArTusController] CoreState missing.");
            if (observer == null) Debug.LogWarning("[ArTusController] Observer missing.");
            if (apiManager == null) Debug.LogWarning("[ArTusController] ApiManager missing.");
            if (ingestor == null) Debug.LogWarning("[ArTusController] Ingestor missing.");
            if (knowledgePipeline == null) Debug.LogWarning("[ArTusController] KnowledgePipeline missing.");
            if (downloadAgent == null) Debug.LogWarning("[ArTusController] DownloadAgent missing.");
            if (purposeLoop == null) Debug.LogWarning("[ArTusController] PurposeLoop missing.");
            if (goalController == null) Debug.LogWarning("[ArTusController] GoalController missing.");
            if (goalExecutor == null) Debug.LogWarning("[ArTusController] GoalExecutor missing.");
        }
    }

    void Start()
    {
        if (autoStart)
        {
            if (enableDebug)
                Debug.Log("[ArTusController] Auto-start enabled. Running boot sequence...");
            BootSequence();
        }
    }

    public void StartIngestion()
    {
        Debug.Log("[Ingestor] StartIngestion() called — pulling from queue.");
        core?.LogMemory("Ingestion pipeline started (manual trigger).", "Ingestion", 2, "curious");
        ingestor?.StartIngestion();
    }

    public void BootSequence()
    {
        observer?.StartCoroutine("ObservationLoop");
        ingestor?.StartIngestion();
        knowledgePipeline?.BeginPipeline();
        purposeLoop?.BeginLoop();

        if (goalExecutor != null && enableDebug)
            Debug.Log("[ArTusController] Goal executor online.");

        if (enableDebug)
            Debug.Log("[ArTusController] Boot sequence completed.");
    }

    public void ExpandDomain(string domainName)
    {
        if (core == null)
        {
            Debug.LogWarning("[ArTusController] CoreState not found for domain expansion.");
            return;
        }

        core.ScheduleDomainExpansion(domainName);
        core.LogMemory($"Domain expansion scheduled for {domainName}.", "DomainExpansion", 2, "curious");

        if (enableDebug)
            Debug.Log($"[ArTusController] Domain expansion scheduled: {domainName}");
    }

    public void RunIngestion()
    {
        if (ingestor != null)
        {
            ingestor.StartIngestion();
            core?.LogMemory("Manual ingestion triggered.", "Ingestion", 2, "curious");
        }
        else
        {
            Debug.LogWarning("[ArTusController] No Ingestor found.");
        }
    }

    public void RunKnowledgePipeline()
    {
        if (knowledgePipeline != null)
        {
            knowledgePipeline.BeginPipeline();
            core?.LogMemory("Knowledge pipeline manually triggered.", "KnowledgePipeline", 2, "thinking");
        }
        else
        {
            Debug.LogWarning("[ArTusController] No KnowledgePipeline found.");
        }
    }

    /// <summary>
    /// Run all configured API stages via ApiManager.
    /// </summary>
    public void RunApiStages()
    {
        if (apiManager != null)
        {
            apiManager.RunAllStages();
            core?.LogMemory("API stages executed by controller.", "ApiExecution", 2, "curious");
        }
        else
        {
            Debug.LogWarning("[ArTusController] No ApiManager found.");
        }
    }
}
