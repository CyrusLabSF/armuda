using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Expands knowledge by scanning domain resources and triggering ingestion when needed.
/// WebGL-safe with deferred expansion logic.
/// </summary>
public class ArTusResourceExpander : MonoBehaviour
{
    // ==================================================
    // PATHS (PLATFORM SAFE)
    // ==================================================

    private string DomainRoot =>
        ArTusPathUtility.GetDev("UNIVERcity/Domains");

    private string ExpansionLogPath =>
        ArTusPathUtility.GetPersistent("UNIVERcity/Logs/ExpansionStatus.jsonl");

    // ==================================================
    // DEPENDENCIES
    // ==================================================

    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusResourceLoader loader;

    [Header("🔊 Options")]
    public bool enableSpeech = true;

    [Tooltip("Required memories per text file before marking a domain as saturated.")]
    public float memoryPerFileThreshold = 5f;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        loader = GetComponent<ArTusResourceLoader>();
    }

    /// <summary>
    /// Scans a given domain and decides whether to expand resources.
    /// </summary>
    public void CheckAndExpandDomain(string domain)
    {
        domain = Normalize(domain);

#if UNITY_WEBGL
        HandleWebGLExpansion(domain);
#else
        HandleDesktopExpansion(domain);
#endif
    }

    // ==================================================
    // DESKTOP PATH
    // ==================================================

    private void HandleDesktopExpansion(string domain)
    {
        string domainPath = Path.Combine(DomainRoot, domain);
        string resourcePath = Path.Combine(domainPath, "Resources/Text");

        if (!Directory.Exists(resourcePath))
        {
            NotifyMissingDomain(domain);
            LogExpansionSummary(domain, 0, 0, "missing");
            return;
        }

        string[] textFiles = Directory.GetFiles(resourcePath, "*.txt");
        int memoryCount = CountDomainMemories(domain);

        float ratio =
            textFiles.Length > 0
                ? memoryCount / (float)textFiles.Length
                : 0f;

        if (ratio < memoryPerFileThreshold)
        {
            if (enableSpeech)
                speech?.RequestSpeak($"I’m expanding my knowledge in {domain}.");

            foreach (string file in textFiles)
                loader?.IngestTextResource(file, domain);

            core?.LogMemory(
                $"📚 Initiated domain expansion in '{domain}'.",
                "KnowledgeExpansion",
                2,
                "curious",
                domain
            );

            LogExpansionSummary(
                domain,
                textFiles.Length,
                memoryCount,
                "expanding"
            );
        }
        else
        {
            if (enableSpeech)
                speech?.RequestSpeak($"I believe {domain} is locally saturated.");

            core?.LogMemory(
                $"🛑 Domain '{domain}' appears saturated. External input required.",
                "KnowledgeExpansion",
                3,
                "incomplete",
                domain
            );

            LogExpansionSummary(
                domain,
                textFiles.Length,
                memoryCount,
                "complete"
            );
        }
    }

    // ==================================================
    // WEBGL PATH
    // ==================================================

    private void HandleWebGLExpansion(string domain)
    {
        if (enableSpeech)
            speech?.RequestSpeak(
                $"Local files are unavailable. I will mark {domain} for future expansion."
            );

        core?.LogMemory(
            $"🌐 Domain '{domain}' marked for deferred expansion (WebGL environment).",
            "KnowledgeExpansion",
            1,
            "neutral",
            domain
        );

        // No disk writes in WebGL
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private int CountDomainMemories(string domain)
    {
        int count = 0;

        var memories = core?.GetAllMemoryEntries();
        if (memories == null) return 0;

        foreach (var m in memories)
        {
            if (m.category == domain)
                count++;
        }

        return count;
    }

    private void NotifyMissingDomain(string domain)
    {
        Debug.LogWarning(
            $"[ResourceExpander] No resources found for domain: {domain}"
        );

        if (enableSpeech)
            speech?.RequestSpeak(
                $"I cannot find any local resources for {domain}."
            );

        core?.LogMemory(
            $"🛑 Domain '{domain}' has no resource folder available.",
            "KnowledgeExpansion",
            3,
            "concerned",
            domain
        );
    }

    private void LogExpansionSummary(
        string domain,
        int files,
        int memories,
        string status)
    {
#if UNITY_WEBGL
        return;
#else
        try
        {
            var entry = new ExpansionLogEntry
            {
                timestamp =
                    System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                domain = domain,
                files = files,
                memories = memories,
                status = status
            };

            string json = JsonUtility.ToJson(entry, false);

            string dir = Path.GetDirectoryName(ExpansionLogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(ExpansionLogPath, json + "\n");
        }
        catch (IOException ex)
        {
            Debug.LogError(
                $"[ResourceExpander] Failed to log expansion status: {ex.Message}"
            );
        }
#endif
    }

    private string Normalize(string text)
    {
        return text?.Trim().ToLowerInvariant() ?? "general";
    }

    // ==================================================
    // DATA
    // ==================================================

    [System.Serializable]
    private class ExpansionLogEntry
    {
        public string timestamp;
        public string domain;
        public int files;
        public int memories;
        public string status;
    }
}
