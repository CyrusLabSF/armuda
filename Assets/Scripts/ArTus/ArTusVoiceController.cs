using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using UnityEngine.Windows.Speech;
#endif
using System.Collections.Generic;
using System.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

[RequireComponent(typeof(ArTusEmotionController))]
public class ArTusVoiceController : MonoBehaviour
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    private KeywordRecognizer keywordRecognizer;
#endif

    private ArTusSpeechResponder speechResponder;
    private ArTusEmotionController emotionController;
    private ArTusCoreState coreState;

    private Dictionary<string, Action> commands = new();

    private static readonly HttpClient httpClient = new();

    private float lastVoiceTime = -10f;
    private float voiceCooldown = 5f;

    private bool CanSpeakNow() => Time.time - lastVoiceTime > voiceCooldown;

    private void SafeSpeak(string message)
    {
        if (!CanSpeakNow()) return;
        lastVoiceTime = Time.time;
        speechResponder?.Speak(message);
    }

    private bool HandleFallbackCommand(string input)
    {
        string lower = input.ToLower();
        if (lower.StartsWith("artus what is") ||
            lower.StartsWith("artus why") ||
            lower.StartsWith("artus how") ||
            lower.StartsWith("artus explain"))
        {
            _ = RequestAIResponse(input);
            return true;
        }
        return false;
    }

    void Start()
    {
        emotionController = GetComponent<ArTusEmotionController>();
        speechResponder = GetComponent<ArTusSpeechResponder>();
        coreState = GetComponent<ArTusCoreState>();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (keywordRecognizer != null && keywordRecognizer.IsRunning)
        {
            Debug.LogWarning("[ArTus Voice] Recognizer already running — skipping duplicate.");
            return;
        }
#endif

        // -----------------------------
        // 🎙️ Voice Command Registry
        // -----------------------------

        commands.Add("artus narrate belief curiosity", () =>
        {
            GetComponent<ArTusBeliefNarrator>()?.NarrateBeliefPath("curiosity");
        });

        commands.Add("artus scan tension", () =>
        {
            coreState?.ScanBeliefTension();
        });

        commands.Add("artus create event", () =>
        {
            GetComponent<ArTusEpisodicMemory>()?.CreateEvent();
        });

        commands.Add("artus reflect on event", () =>
        {
            GetComponent<ArTusEpisodicMemory>()?.ReflectOnLastEvent();
        });

        commands.Add("artus what are my favorites", () =>
        {
            coreState?.ReflectOnFavorites();
        });

        commands.Add("artus recommend something", () =>
        {
            coreState?.GenerateRecommendation();
        });

        commands.Add("artus reflect on yesterday", () =>
        {
            coreState?.ReflectOnYesterday();
        });

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        keywordRecognizer = new KeywordRecognizer(commands.Keys.ToArray());
        keywordRecognizer.OnPhraseRecognized += OnRecognized;
        keywordRecognizer.Start();
        Debug.Log("✅ ArTus Voice Recognizer initialized for Windows.");
#else
        Debug.Log("🎧 Voice recognition skipped on non-Windows build.");
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    private void OnRecognized(PhraseRecognizedEventArgs args)
    {
        string spoken = args.text.ToLower();
        Debug.Log($"[ArTus Voice] Recognized: {spoken}");

        if (coreState != null)
            coreState.lastVoiceCommand = spoken;

        if (commands.TryGetValue(spoken, out var action))
        {
            action.Invoke();
            return;
        }

        // -----------------------------
        // 🔍 External Knowledge Requests
        // -----------------------------

        if (spoken.StartsWith("artus search google for "))
        {
            coreState?.FetchExternalKnowledge(
                "google/search",
                spoken.Replace("artus search google for ", "").Trim(),
                "General"
            );
            return;
        }

        if (spoken.StartsWith("artus search pubmed for "))
        {
            coreState?.FetchExternalKnowledge(
                "pubmed/search",
                spoken.Replace("artus search pubmed for ", "").Trim(),
                "Biology"
            );
            return;
        }

        if (spoken.StartsWith("artus search openlibrary for "))
        {
            coreState?.FetchExternalKnowledge(
                "openlibrary/search",
                spoken.Replace("artus search openlibrary for ", "").Trim(),
                "Literature"
            );
            return;
        }

        if (spoken.StartsWith("artus search sep for "))
        {
            coreState?.FetchExternalKnowledge(
                "stanford/search",
                spoken.Replace("artus search sep for ", "").Trim(),
                "Philosophy"
            );
            return;
        }

        if (spoken.StartsWith("artus archive belief "))
        {
            coreState?.CommitBeliefToArchive(
                spoken.Replace("artus archive belief ", "").Trim()
            );
            return;
        }

        // -----------------------------
        // 🤖 AI fallback
        // -----------------------------

        if (!HandleFallbackCommand(spoken))
        {
            Debug.LogWarning($"[ArTus Voice] Unknown command: {spoken}");
        }
    }
#endif

    // -----------------------------
    // 🌐 Local AI Fallback
    // -----------------------------
    public async Task RequestAIResponse(string userInput)
    {
        try
        {
            var content = new StringContent(
                $"{{\"query\":\"{userInput}\"}}",
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(
                "http://127.0.0.1:8000/chat",
                content
            );

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            SafeSpeak(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ArTus AI] Request failed: {ex.Message}");
        }
    }

    // -----------------------------
    // 📤 Export Voice Commands
    // -----------------------------
    private void ExportVoiceCommandsToCSV()
    {
        string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/VoiceCommands.csv";
        List<string> lines = new() { "Command" };

        foreach (var cmd in commands.Keys.OrderBy(c => c))
            lines.Add(cmd);

        System.IO.File.WriteAllLines(path, lines);
        Debug.Log("[ArTus Voice] Exported voice commands to CSV.");
    }
}
