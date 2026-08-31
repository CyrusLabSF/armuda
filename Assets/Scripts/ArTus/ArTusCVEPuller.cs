using UnityEngine;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class ArTusCvePuller : MonoBehaviour
{
    private static readonly HttpClient httpClient = new HttpClient();
    private ArTusCoreState core;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        // ✅ Ensure User-Agent header (many security APIs block missing UA)
        if (!httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("ArTus/1.0 (Defense Manager)"))
            Debug.LogWarning("[CVE Puller] ⚠️ Failed to set User-Agent header.");
    }

    public async Task FetchLatestCves(string keyword = "windows")
    {
        string nvdUrl = $"https://services.nvd.nist.gov/rest/json/cves/2.0?keywordSearch={Uri.EscapeDataString(keyword)}";
        string mitreUrl = $"https://cveawg.mitre.org/api/cve/{keyword}"; // fallback, requires specific CVE IDs

        try
        {
            string json = await SafeGet(nvdUrl);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[CVE Puller] NVD returned no data, falling back to MITRE.");
                json = await SafeGet(mitreUrl);
            }

            if (!string.IsNullOrEmpty(json))
            {
                ProcessCveJson(json, keyword);
            }
            else
            {
                Debug.LogError("[CVE Puller] ❌ Both NVD and MITRE failed.");
                core?.LogMemory($"CVE pull failed for {keyword}.", "CVEPuller", 2, "alert");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CVE Puller] ❌ Error fetching CVEs: {ex.Message}");
        }
    }

    private async Task<string> SafeGet(string url, int retries = 3)
    {
        for (int i = 0; i < retries; i++)
        {
            try
            {
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    Debug.LogWarning($"[CVE Puller] {url} failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.LogWarning($"[CVE Puller] Network error: {ex.Message}");
            }

            await Task.Delay(1000 * (i + 1)); // exponential backoff
        }

        return null;
    }

    private void ProcessCveJson(string json, string keyword)
    {
        try
        {
            JObject root = JObject.Parse(json);

            // ✅ NVD 2.0 JSON parsing example
            var cves = root["vulnerabilities"];
            if (cves == null)
            {
                Debug.LogWarning("[CVE Puller] No vulnerabilities field found.");
                return;
            }

            // 🔍 Cache once (Unity 6 safe)
            var cyberSpace = FindAnyObjectByType<CyberSpaceManager>();

            foreach (var item in cves)
            {
                string cveId =
                    item["cve"]?["id"]?.ToString() ?? "Unknown";

                string desc =
                    item["cve"]?["descriptions"]?[0]?["value"]?.ToString()
                    ?? "No description";

                string summary = $"⚠️ CVE Detected: {cveId} — {desc}";

                core?.LogMemory(
                    summary,
                    "CVE",
                    4,
                    "alert"
                );

                cyberSpace?.EscalateToDefense(
                    cveId,
                    desc
                );
            }

            core?.LogMemory(
                $"CVE pull for {keyword} completed successfully.",
                "CVEPullerSummary",
                3,
                "thinking"
            );

            Debug.Log(
                $"[CVE Puller] ✅ Successfully processed CVEs for {keyword}"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[CVE Puller] ❌ Failed to parse CVE JSON: {ex.Message}"
            );
        }
    }
}
