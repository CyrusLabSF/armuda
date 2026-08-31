using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ArTusIdentitySnapshot
{
    public string currentRole = "autonomous learner";
    public string identityNarrative = "ArTus is evolving as an embodied autonomous learning system.";
    public List<string> growthFocuses = new();
    public List<string> capabilityDomains = new();
    public string updatedAt;
}

public class ArTusSelfModel : MonoBehaviour
{
    [SerializeField] private ArTusIdentitySnapshot identity = new ArTusIdentitySnapshot
    {
        growthFocuses = new List<string> { "decision quality", "tool use", "code intelligence", "self-modeling" },
        capabilityDomains = new List<string> { "autonomy", "beliefs", "concept discovery", "embodiment", "analytics" }
    };

    public ArTusIdentitySnapshot GetSnapshot()
    {
        identity.updatedAt = DateTime.UtcNow.ToString("o");
        return new ArTusIdentitySnapshot
        {
            currentRole = identity.currentRole,
            identityNarrative = identity.identityNarrative,
            growthFocuses = identity.growthFocuses?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
            capabilityDomains = identity.capabilityDomains?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
            updatedAt = identity.updatedAt
        };
    }

    public void AddGrowthFocus(string focus)
    {
        if (string.IsNullOrWhiteSpace(focus))
            return;

        identity.growthFocuses ??= new List<string>();
        if (!identity.growthFocuses.Any(entry => string.Equals(entry, focus, StringComparison.OrdinalIgnoreCase)))
            identity.growthFocuses.Add(focus.Trim());
    }

    public void AddCapabilityDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return;

        identity.capabilityDomains ??= new List<string>();
        if (!identity.capabilityDomains.Any(entry => string.Equals(entry, domain, StringComparison.OrdinalIgnoreCase)))
            identity.capabilityDomains.Add(domain.Trim());
    }
}
