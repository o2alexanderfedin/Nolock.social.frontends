using System;

namespace NoLock.Social.Core.Camera.Models;

public class CameraStream
{
    public string StreamId { get; set; } = Guid.NewGuid().ToString();
    public string StreamUrl { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsActive { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}