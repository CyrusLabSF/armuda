using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FirewallGuard
{
    private readonly string[] allowedExtensions = { ".pdf", ".txt", ".csv", ".json", ".zip" };
    private readonly string[] blacklistedDomains = { "malicious.com", "dangerous.net" };
    private readonly string logPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/FirewallScanLog.csv";

    public bool ValidateUrl(string url)
    {
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            return false;

        if (blacklistedDomains.Any(domain => url.Contains(domain, StringComparison.OrdinalIgnoreCase)))
        {
            Log(url, "Blocked - BlacklistedDomain", "shark");
            return false;
        }

        return true;
    }

    public bool ValidateExtension(string url)
    {
        bool valid = allowedExtensions.Any(ext => url.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        if (!valid)
            Log(url, "Blocked - InvalidExtension", "gate");

        return valid;
    }

    public bool ScanFile(string path)
    {
        if (!File.Exists(path))
        {
            Log(path, "Blocked - FileNotFound", "phantom");
            return false;
        }

        string content = File.ReadAllText(path);
        bool containsThreat = content.Contains("<script>") || content.Contains("eval(") || content.Contains("base64,");

        if (containsThreat)
        {
            Log(path, "Blocked - ScriptContent", "trident");
            return false;
        }

        Log(path, "Passed - Clean", "clean");
        return true;
    }

    private void Log(string target, string result, string tag)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{Escape(target)},{result},{tag}";
            File.AppendAllText(logPath, line + "\n");
        }
        catch (IOException ex)
        {
            UnityEngine.Debug.LogError($"[FirewallGuard] ❌ Failed to log scan result: {ex.Message}");
        }
    }

    private string Escape(string input)
    {
        return input.Replace(",", " ").Replace("\n", " ").Replace("\r", " ");
    }
}
