using System;

namespace ArTusTypes
{
    [Serializable]
    public class ContradictionTrendEntry
    {
        public string topic;
        public string domain;
        public int conflictCount;
        public string severity;            // severity as a label ("low", "moderate", "high")
        public float certaintyAtDetection; // belief confidence when contradiction was logged
        public string timestamp;

        public ContradictionTrendEntry()
        {
            topic = "unspecified";
            domain = "general";
            conflictCount = 0;
            severity = "low";
            certaintyAtDetection = 0.5f;
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public ContradictionTrendEntry(string topic, string domain, int conflictCount,
                                       string severity, float certaintyAtDetection)
        {
            this.topic = topic;
            this.domain = domain;
            this.conflictCount = conflictCount;
            this.severity = severity;
            this.certaintyAtDetection = certaintyAtDetection;
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
