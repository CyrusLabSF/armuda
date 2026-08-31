using System;
using System.Collections.Generic;

[Serializable]
public class HeatmapEntry
{
    public List<string> conflicts;
    public string severity;
    public string lastDetected;
    public string timestamp;
    public float severityScore;
    public bool resolved;
}

[Serializable]
public class HeatmapWrapper
{
    public Dictionary<string, Dictionary<string, HeatmapEntry>> heatmap;
}
