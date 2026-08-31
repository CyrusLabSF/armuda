using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;

public class ArTusQuestionBridge : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusSemanticSearch semanticSearch;

    private ArTusCoreState core;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior")]
    public bool enableSpeech = false; // OFF for beta
    public float requestTimeout = 5f;

    private string qaLogPath;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        qaLogPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/QuestionAnswerLog.csv"
        );

        try
        {
            var dir = Path.GetDirectoryName(qaLogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(qaLogPath))
                File.WriteAllText(
                    qaLogPath,
                    "Timestamp,Question,Answer,LatencyMS,Success\n"
                );
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ArTusBridge] Log init skipped: {e.Message}");
        }
    }

    // --------------------------------------------------
    // PUBLIC ENTRY
    // --------------------------------------------------
    public void AskQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return;

        StartCoroutine(SendQueryToFastAPI(question));
    }

    // --------------------------------------------------
    // CORE REQUEST
    // --------------------------------------------------
    private IEnumerator SendQueryToFastAPI(string question)
    {
        string url = "http://localhost:8000/chat";

        string json = "{\"query\":\"" + question.Replace("\"", "\\\"") + "\"}";
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.timeout = Mathf.RoundToInt(requestTimeout);

            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            DateTime start = DateTime.Now;

            yield return request.SendWebRequest();

            double latency = (DateTime.Now - start).TotalMilliseconds;

            bool success = request.result == UnityWebRequest.Result.Success;

            string answer = success
                ? ParseResponse(request.downloadHandler.text)
                : "I’m unable to reach my knowledge service right now.";

            Debug.Log($"[ArTusBridge] Q: {question}");
            Debug.Log($"[ArTusBridge] A: {answer}");

            // -----------------------------
            // ROUTE TO SEMANTIC SYSTEM (SAFE)
            // -----------------------------
            if (semanticSearch != null)
            {
                var semantic = semanticSearch.Query(question);

                if (semantic.success)
                {
                    answer = semantic.answer; // override with internal knowledge
                }
            }

            // -----------------------------
            // SAFE MEMORY LOGGING
            // -----------------------------
            if (!betaMode)
            {
                core?.LogMemory(
                    $"Q: {question} | A: {answer}",
                    "APIResponse",
                    1,
                    "curious",
                    "question-trail"
                );
            }

            // -----------------------------
            // SPEECH CONTROL
            // -----------------------------
            if (!betaMode && enableSpeech)
            {
                core?.TriggerVoice(answer);
            }

            // -----------------------------
            // CSV LOGGING (KEEP ON)
            // -----------------------------
            try
            {
                File.AppendAllText(
                    qaLogPath,
                    $"{DateTime.Now},{Sanitize(question)},{Sanitize(answer)},{latency:F0},{success}\n"
                );
            }
            catch { }

            // -----------------------------
            // 🔥 RETURN HOOK (FOR THOUGHT SYSTEM)
            // -----------------------------
            OnAnswerReady(answer, success);
        }
    }

    // --------------------------------------------------
    // RESPONSE PARSER
    // --------------------------------------------------
    private string ParseResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "No response.";

        try
        {
            if (raw.Contains(":\""))
            {
                int s = raw.IndexOf(":\"") + 2;
                int e = raw.LastIndexOf("\"");

                if (s > 1 && e > s)
                    return raw.Substring(s, e - s);
            }
        }
        catch { }

        return raw;
    }

    private string Sanitize(string input)
    {
        return string.IsNullOrEmpty(input)
            ? ""
            : input.Replace(",", " ").Replace("\n", " ");
    }

    // --------------------------------------------------
    // FINAL OUTPUT HOOK (CRITICAL)
    // --------------------------------------------------
    private void OnAnswerReady(string answer, bool success)
    {
        // This will be wired into Thought System next
        Debug.Log($"[ArTusBridge] Final Output: {answer}");
    }
}