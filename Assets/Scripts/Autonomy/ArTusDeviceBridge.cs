using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ArTusDeviceDefinition
{
    public string deviceId;
    public string label;
    public string deviceType;
    public string location;
    public string state = "unknown";
    public bool canRead = true;
    public bool canWrite;
    public string lastSeenAt;
    public List<string> supportedCommands = new();
}

public class ArTusDeviceBridge : MonoBehaviour
{
    [SerializeField] private List<ArTusDeviceDefinition> devices = new();

    public void RegisterDevice(
        string deviceId,
        string label,
        string deviceType,
        IEnumerable<string> supportedCommands,
        string location = "",
        bool canRead = true,
        bool canWrite = false,
        string state = "available")
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        var device = devices.FirstOrDefault(entry =>
            string.Equals(entry.deviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (device == null)
        {
            device = new ArTusDeviceDefinition();
            devices.Add(device);
        }

        device.deviceId = deviceId.Trim();
        device.label = string.IsNullOrWhiteSpace(label) ? deviceId.Trim() : label.Trim();
        device.deviceType = string.IsNullOrWhiteSpace(deviceType) ? "generic" : deviceType.Trim();
        device.location = location?.Trim() ?? string.Empty;
        device.state = string.IsNullOrWhiteSpace(state) ? "available" : state.Trim();
        device.canRead = canRead;
        device.canWrite = canWrite;
        device.lastSeenAt = DateTime.UtcNow.ToString("o");
        device.supportedCommands = (supportedCommands ?? Enumerable.Empty<string>())
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(command => command.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void UpdateDeviceState(string deviceId, string state)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        var device = devices.FirstOrDefault(entry =>
            string.Equals(entry.deviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (device == null)
            return;

        device.state = string.IsNullOrWhiteSpace(state) ? device.state : state.Trim();
        device.lastSeenAt = DateTime.UtcNow.ToString("o");
    }

    public List<ArTusDeviceDefinition> GetDevices()
    {
        return devices
            .Where(device => device != null && !string.IsNullOrWhiteSpace(device.deviceId))
            .Select(CloneDevice)
            .ToList();
    }

    private static ArTusDeviceDefinition CloneDevice(ArTusDeviceDefinition source)
    {
        return new ArTusDeviceDefinition
        {
            deviceId = source.deviceId,
            label = source.label,
            deviceType = source.deviceType,
            location = source.location,
            state = source.state,
            canRead = source.canRead,
            canWrite = source.canWrite,
            lastSeenAt = source.lastSeenAt,
            supportedCommands = source.supportedCommands?.ToList() ?? new List<string>()
        };
    }
}
