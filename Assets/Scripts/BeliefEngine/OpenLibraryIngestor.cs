using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class OpenLibraryIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine beliefEngine;

    private static readonly HttpClient httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private float lastSpeechTime = 0f;
    public float speechCooldown = 10f;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
    }

    public async Task IngestFromOpenLibrary(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return;

        topic = topic.Trim().ToLower();

        string url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(topic)}";

        try
        {
            string json = await httpClient.GetStringAsync(url);
            List<BookEntry> books = ExtractStructuredBooks(json);

            if (books.Count == 0)
            {
                SpeakSafe($"I couldn’t find any useful books on {topic}.");
                return;
            }

            int limit = Mathf.Min(3, books.Count);

            for (int i = 0; i < limit; i++)
            {
                var book = books[i];

                float relevance = ComputeRelevance(book);

                core?.LogMemory(
                    $"📘 OpenLibrary | {book.title}",
                    "OpenLibrary",
                    Mathf.RoundToInt(relevance * 3f),
                    "inspired"
                );

                PersistBookEntry(topic, book, relevance);

                beliefEngine?.LogTopicBelief(book.title, "inspired");

                core?.QueueDeferredReflection(
                    book.title,
                    "OpenLibrary",
                    relevance
                );

                TriggerCuriosityExpansion(book);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenLibrary] Error: {e.Message}");
            SpeakSafe($"Something went wrong while searching for {topic}.");
        }
    }

    private List<BookEntry> ExtractStructuredBooks(string json)
    {
        var books = new List<BookEntry>();

        MatchCollection docs = Regex.Matches(json, "\"title\":\"(.*?)\"");
        int count = Mathf.Min(3, docs.Count);

        for (int i = 0; i < count; i++)
        {
            books.Add(new BookEntry
            {
                title = Clean(docs[i].Groups[1].Value)
            });
        }

        return books;
    }

    private string Clean(string value)
    {
        return value.Replace("\\\"", "\"").Replace("\\n", " ").Trim();
    }

    private float ComputeRelevance(BookEntry book)
    {
        return 0.6f; // simplified (safe baseline)
    }

    private void TriggerCuriosityExpansion(BookEntry book)
    {
        if (string.IsNullOrEmpty(book.title)) return;

        core?.QueueDeferredReflection(
            book.title,
            "OpenLibrary-Curiosity",
            0.4f
        );
    }

    private void PersistBookEntry(string rootTopic, BookEntry book, float relevance)
    {
        string safeTopic = Sanitize(rootTopic);

        string folder = ArTusPathUtility.GetPersistent(
            $"UNIVERcity/Library/OpenLibrary/{safeTopic}"
        );

        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, $"{Sanitize(book.title)}.json");

        var entry = new PersistedBook
        {
            rootTopic = rootTopic,
            title = book.title,
            relevance = relevance,
            ingestedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        File.WriteAllText(path, JsonUtility.ToJson(entry, true));
    }

    private void SpeakSafe(string msg)
    {
        if (Time.time - lastSpeechTime > speechCooldown)
        {
            speech?.TriggerVoice(msg);
            lastSpeechTime = Time.time;
        }
    }

    private string Sanitize(string value)
    {
        return Regex.Replace(value, @"[^\w\-]", "_");
    }

    [Serializable]
    private class BookEntry
    {
        public string title;
    }

    [Serializable]
    private class PersistedBook
    {
        public string rootTopic;
        public string title;
        public float relevance;
        public string ingestedAt;
    }
}