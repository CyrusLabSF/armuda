using UnityEngine;
using System;
using System.IO;
using System.Linq;

public class ArTusAuditManager : MonoBehaviour
{
    [Header("Mode")]
    public bool betaMode = true;

    [Header("Audit Timing")]
    public float auditInterval = 300f; // 5 minutes
    private float auditTimer;

    // Core references
    private ArTusCoreState core;
    private ArTusEmotionController emotion;
    private ArTusIngestor ingestor;
    private ArTusBeliefEngine beliefEngine;
    private ArTusSpeechResponder speech;
    private ContradictionLogManager contradictionManager;

    // Export path (WebGL safe)
    private string exportPath;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        emotion = GetComponent<ArTusEmotionController>();
        ingestor = GetComponent<ArTusIngestor>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        speech = GetComponent<ArTusSpeechResponder>();

        contradictionManager =
            GetComponent<ContradictionLogManager>()
            ?? gameObject.AddComponent<ContradictionLogManager>();

        exportPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/InternalAuditLog.csv"
        );

        InitializeAuditLog();
    }

    void Update()
    {
        auditTimer += Time.deltaTime;

        if (auditTimer >= auditInterval)
        {
            auditTimer = 0f;
            RunInternalAudit();
        }
    }

    // ------------------------------------------------------
    // INITIALIZATION
    // ------------------------------------------------------
    private void InitializeAuditLog()
    {
        try
        {
            string dir = Path.GetDirectoryName(exportPath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(exportPath))
            {
                FileIOManager.QueueWrite(
                    exportPath,
                    "Timestamp,Emotion,MemoryCount,AvgClarity,Activity,FadingBeliefs,Contradictions,Simulation,NightMode\n",
                    "AuditHeader",
                    append: false
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuditManager] Init failed: {ex.Message}");
        }
    }

    // ------------------------------------------------------
    // CORE AUDIT
    // ------------------------------------------------------
    private void RunInternalAudit()
    {
        if (core == null)
            return;

        string emotionState = core.GetCurrentEmotion();
        int memoryCount = core.memoryLog?.Count ?? 0;
        float avgClarity = core.GetAverageMemoryClarity();
        float activity = core.activityScore;

        bool isLearning = ingestor != null && ingestor.IsIngesting();
        bool nightMode = core.isNightMode;

        bool isSimulating = false;

        int totalBeliefs = beliefEngine?.beliefs.Count ?? 0;
        int fadingBeliefs = 0;

        if (beliefEngine?.beliefs != null)
        {
            fadingBeliefs = beliefEngine.beliefs.Values
                .Count(b => b.confidenceScore < 1f);
        }

        int contradictionCount =
            contradictionManager?.GetContradictionEntries()?.Count ?? 0;

        // -------------------------------
        // HUMAN READABLE SUMMARY
        // -------------------------------
        string summary =
            $"🧠 Internal Audit\n" +
            $"Emotion: {emotionState}\n" +
            $"Memory: {memoryCount}, Avg Clarity: {avgClarity:F2}\n" +
            $"Activity: {activity:F2}, Learning: {(isLearning ? "Active" : "Idle")}\n" +
            $"Beliefs: {totalBeliefs}, Fading: {fadingBeliefs}\n" +
            $"Contradictions: {contradictionCount}\n" +
            $"Simulation: {(isSimulating ? "Active" : "Idle")}\n" +
            $"Mode: {(nightMode ? "🌙 Night" : "☀ Day")}";

        // -------------------------------
        // SAFE MEMORY LOGGING (BETA CONTROLLED)
        // -------------------------------
        if (!betaMode)
        {
            core.LogMemory(summary, "Audit", 2, emotionState);
        }
        else
        {
            core.LogMemory("Audit checkpoint recorded.", "Audit", 1, "neutral");
        }

        Debug.Log(summary);

        // -------------------------------
        // CSV EXPORT (KEEP THIS ON)
        // -------------------------------
        try
        {
            string row =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                $"{emotionState}," +
                $"{memoryCount}," +
                $"{avgClarity:F2}," +
                $"{activity:F2}," +
                $"{fadingBeliefs}," +
                $"{contradictionCount}," +
                $"{(isSimulating ? 1 : 0)}," +
                $"{(nightMode ? 1 : 0)}\n";

            FileIOManager.QueueWrite(
                exportPath,
                row,
                "AuditRow",
                append: true
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuditManager] CSV write failed: {ex.Message}");
        }

        // -------------------------------
        // DIPLOMATIC REGISTRATION (OPTIONAL SAFE)
        // -------------------------------
        if (!betaMode)
        {
            var cyberSpace = FindAnyObjectByType<CyberSpaceManager>();

            cyberSpace?.RegisterDiplomaticEvent(
                "AuditManager",
                "Completed Audit",
                $"Emotion: {emotionState}, Clarity: {avgClarity:F2}, Contradictions: {contradictionCount}",
                "thinking"
            );
        }

        // -------------------------------
        // REFLECTION CONTROL (DISABLED IN BETA)
        // -------------------------------
        if (!betaMode && fadingBeliefs > 0)
        {
            core.ScheduleReflection(
                "fading_beliefs",
                "self_review"
            );
        }

        // -------------------------------
        // VOICE CONTROL (DISABLED IN BETA)
        // -------------------------------
        if (!betaMode)
        {
            speech?.RequestSpeak(
                $"Audit complete. I feel {emotionState}. My clarity is {avgClarity:F2}.",
                ArTusSpeechResponder.SpeechCategory.System
            );
        }

        // -------------------------------
        // SAFETY GUARD (CRITICAL)
        // -------------------------------
        if (memoryCount > 10000)
        {
            Debug.LogWarning("[AuditManager] Memory overload risk detected.");
        }
    }
}