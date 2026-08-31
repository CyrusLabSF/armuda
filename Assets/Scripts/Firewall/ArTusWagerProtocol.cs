using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class ArTusWagerProtocol : MonoBehaviour
{
    [System.Serializable]
    public class WagerAttempt
    {
        public string sourceIP;
        public string symbolicThreat;       // e.g. "Trident"
        public string wageredValue;         // e.g. "insight into attacker method"
        public string reasonForWager;       // e.g. "non-lethal intrusion pattern"
        public string resolutionOutcome;    // e.g. "knowledge gained", "retaliated"
        public float trustScore;            // 0–1
        public DateTime timestamp;
    }

    public List<WagerAttempt> wagerHistory = new();
    private ArTusCoreState core;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
    }

    public void OfferWager(string ip, string symbolicThreat, string reason)
    {
        float trust = CalculateTrust(ip, symbolicThreat, reason);

        var wager = new WagerAttempt
        {
            sourceIP = ip,
            symbolicThreat = symbolicThreat,
            wageredValue = DetermineWagerValue(symbolicThreat),
            reasonForWager = reason,
            trustScore = trust,
            resolutionOutcome = "pending",
            timestamp = DateTime.UtcNow
        };

        wagerHistory.Add(wager);

        // 🧠 Log memory for the wager
        string log = $"🧭 Wager offered to {ip} for threat [{symbolicThreat}]. Reason: {reason}. Trust = {trust:F2}";
        core?.LogMemory(log, "WagerProtocol", 2, "curious");

        Debug.Log($"[WagerProtocol] Offering wager to {ip} — Trust {trust}");
    }

    private float CalculateTrust(string ip, string threat, string reason)
    {
        if (reason.ToLower().Contains("learning")) return 0.75f;
        if (threat == "Ghost") return 0.3f; // stealthy = low trust
        return UnityEngine.Random.Range(0.4f, 0.8f);
    }

    private string DetermineWagerValue(string threat)
    {
        return threat switch
        {
            "Trident" => "Request attacker’s method via mirrored echo packet",
            "Shark" => "Exchange system fingerprint in hash form",
            "Ghost" => "Offer audit trail trace in return for intent reveal",
            _ => "Query for motivation signature"
        };
    }

    public void ResolveWager(string ip, string outcome, bool learnedSomething)
    {
        var wager = wagerHistory.FindLast(w => w.sourceIP == ip && w.resolutionOutcome == "pending");
        if (wager == null)
        {
            Debug.LogWarning($"[WagerProtocol] No pending wager for {ip}");
            return;
        }

        wager.resolutionOutcome = outcome;

        string mood = learnedSomething ? "reflective" : "defensive";
        string msg = $"🎯 Wager resolution with {ip}: {outcome}.";
        core?.LogMemory(msg, "WagerProtocol", learnedSomething ? 3 : 1, mood);

        Debug.Log($"[WagerProtocol] Resolved wager with {ip} as: {outcome}");
    }

    public void ExportWagersToCSV()
    {
        string path = Application.dataPath + "/Firewall/WagerLog.csv";
        using StreamWriter writer = new(path);
        writer.WriteLine("IP,Threat,Wager,Reason,Outcome,Trust,Timestamp");

        foreach (var w in wagerHistory)
        {
            writer.WriteLine($"{w.sourceIP},{w.symbolicThreat},{Escape(w.wageredValue)},{Escape(w.reasonForWager)},{w.resolutionOutcome},{w.trustScore},{w.timestamp}");
        }

        Debug.Log($"✅ Exported {wagerHistory.Count} wagers to CSV.");
    }

    private string Escape(string input) => $"\"{input.Replace("\"", "\"\"")}\"";
}
