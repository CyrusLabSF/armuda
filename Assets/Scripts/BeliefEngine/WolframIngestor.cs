using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ArTusTypes;

public class WolframIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine belief;

    [SerializeField] private string wolframAppId = "H6K8Y9-74WUJPA97T";

    private string summaryFolder;
    private string csvPath;
    private string trailCsvPath;

    private float lastSpeechTime = 0f;
    public float speechCooldown = 10f;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        belief = GetComponent<ArTusBeliefEngine>();

        summaryFolder = ArTusPathUtility.GetPersistent("UNIVERcity/ExternalSummaries/Wolfram");
        csvPath = ArTusPathUtility.GetPersistent("UNIVERcity/Exports/WolframInsights.csv");
        trailCsvPath = ArTusPathUtility.GetPersistent("UNIVERcity/Exports/WolframTrailMap.csv");

        Directory.CreateDirectory(summaryFolder);
    }

    public void IngestFromWolfram(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        query = query.Trim().ToLower();

        StartCoroutine(WolframRoutine(query));
    }

    private IEnumerator WolframRoutine(string query)
    {
        string url =
            $"https://api.wolframalpha.com/v2/query" +
            $"?input={UnityWebRequest.EscapeURL(query)}" +
            $"&appid={wolframAppId}&output=json";

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            SpeakSafe($"I couldn't reach Wolfram Alpha for {query}.");
            yield break;
        }

        WolframResult result;

        try
        {
            result = JsonUtility.FromJson<WolframResult>(req.downloadHandler.text);
        }
        catch
        {
            SpeakSafe($"Wolfram returned unreadable data for {query}.");
            yield break;
        }

        if (result == null || !result.queryresult.success)
        {
            SpeakSafe($"Wolfram Alpha could not process {query}.");
            yield break;
        }

        string output = ExtractPrimaryContent(result);

        if (string.IsNullOrWhiteSpace(output))
            yield break;

        if (output.Length > 500)
            output = output.Substring(0, 500);

        core?.LogMemory(
            $"🔢 Wolfram Result for '{query}': {output}",
            "WolframAlpha",
            4,
            "analytical"
        );

        SaveSummary(query, output);
        ExportToCSV(query, output);
        AssignWolframTrail(query, output);
    }

    private string ExtractPrimaryContent(WolframResult result)
    {
        foreach (var pod in result.queryresult.pods)
        {
            if (pod.primary || (pod.title?.ToLower().Contains("definition") ?? false))
            {
                foreach (var sub in pod.subpods)
                {
                    if (!string.IsNullOrWhiteSpace(sub.plaintext))
                        return sub.plaintext.Trim();
                }
            }
        }
        return null;
    }

    private void SaveSummary(string topic, string content)
    {
        string safe = SafeFile(topic);
        string path = Path.Combine(summaryFolder, $"{safe}.json");

        var entry = new WolframEntry
        {
            topic = topic,
            content = content,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        File.WriteAllText(path, JsonUtility.ToJson(entry, true));
    }

    private void ExportToCSV(string topic, string content)
    {
        bool exists = File.Exists(csvPath);

        using StreamWriter writer = new StreamWriter(csvPath, true);

        if (!exists)
            writer.WriteLine("Timestamp,Topic,Summary");

        writer.WriteLine($"{DateTime.Now},{Csv(topic)},{Csv(content)}");
    }

    private void AssignWolframTrail(string topic, string summary)
    {
        if (belief == null)
            return;

        string trailID = $"WolframTrail_{DateTime.Now:yyyyMMdd_HHmmss}";

        belief.LogTopicBelief(topic, "analytical");

        core?.LogMemory(
            $"🧩 Wolfram trail '{trailID}' assigned to '{topic}'.",
            "WolframTrail",
            2,
            "analytical"
        );

        ExportTrail(trailID, topic, summary);
    }

    private void ExportTrail(string trailID, string topic, string summary)
    {
        bool exists = File.Exists(trailCsvPath);

        using StreamWriter writer = new StreamWriter(trailCsvPath, true);

        if (!exists)
            writer.WriteLine("Timestamp,TrailID,Topic,Source,Emotion,Summary");

        writer.WriteLine($"{DateTime.Now},{trailID},{Csv(topic)},Wolfram,analytical,{Csv(summary)}");
    }

    private string SafeFile(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(" ", "_");
    }

    private string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        value = value.Replace("\"", "\"\"");

        if (value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
            return $"\"{value}\"";

        return value;
    }

    private void SpeakSafe(string msg)
    {
        if (Time.time - lastSpeechTime > speechCooldown)
        {
            speech?.TriggerVoice(msg);
            lastSpeechTime = Time.time;
        }
    }

    [Serializable] public class WolframEntry { public string topic; public string content; public string timestamp; }
    [Serializable] public class WolframResult { public QueryResult queryresult; }
    [Serializable] public class QueryResult { public bool success; public Pod[] pods; }
    [Serializable] public class Pod { public string title; public bool primary; public Subpod[] subpods; }
    [Serializable] public class Subpod { public string plaintext; }
}