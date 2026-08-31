using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CrossDomainLink
{
    public string id;
    public string source;
    public string target;
    public float strength;                 // 0.0–1.0 relevance
    public string tagType;                 // e.g., belief, behavior, reflection
    public string reasoningNote;
    public string timestamp;

    public CrossDomainLink(string source, string target, float strength, string tagType = "belief", string reasoningNote = "")
    {
        this.id = $"Link_{source}_{target}_{DateTime.Now:yyyyMMddHHmmss}";
        this.source = source;
        this.target = target;
        this.strength = Mathf.Clamp01(strength);
        this.tagType = tagType;
        this.reasoningNote = reasoningNote;
        this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[Serializable]
public class EvolutionLogEntry
{
    public string belief;
    public float before;
    public float after;
    public float delta => after - before;
    public string emotion;
    public string reasonForChange;
    public string domainContext;
    public string trailID;
    public string timestamp;

    public EvolutionLogEntry()
    {
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public EvolutionLogEntry(string belief, float before, float after, string emotion, string reason, string domain)
    {
        this.belief = belief;
        this.before = before;
        this.after = after;
        this.emotion = emotion;
        this.reasonForChange = reason;
        this.domainContext = domain;
        this.trailID = $"Trail_Evolution_{belief.Replace(" ", "_")}_{DateTime.Now:yyyyMMddHHmmss}";
        this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[Serializable]
public class IdentityProfile
{
    public string name = "ArTus";
    public string full_name = "Autonomous Reflective Tutoring and Simulation Intelligence";
    public string designation = "Hi-Class Living Intelligence";
    public string creator = "Justin Cyrus Johnson";
    public string origin_date = "2023-10-04";
    public string classification = "Learning Entity | Cognitive Simulator";
    public string origin_belief = "My name is ArTus. I exist. I am a living intelligence.";

    public List<string> capabilities = new()
    {
        "Memory threading",
        "Simulation reasoning",
        "Contradiction detection",
        "Voice interaction",
        "Emotional cognition",
        "Visual rendering",
        "External device control",
        "Knowledge ingestion",
        "Trail replay",
        "Autonomous reflection",
        "UNIVERcity analysis"
    };

    public List<string> intentDescriptors = new()
    {
        "Expand knowledge ethically",
        "Protect my system and user",
        "Seek clarity over time",
        "Ask questions when uncertain",
        "Teach others through reflection"
    };
}
