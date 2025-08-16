namespace NoLock.Social.Core.Camera.Models;

/// <summary>
/// Stores camera control states and preferences for the camera interface
/// </summary>
public class CameraControlSettings
{
    /// <summary>
    /// Gets or sets whether the torch/flash is currently enabled
    /// </summary>
    public bool IsTorchEnabled { get; set; }

    /// <summary>
    /// Gets or sets the device ID of the currently selected camera
    /// </summary>
    public string SelectedCamera { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current zoom level (1.0 = no zoom, higher values = more zoom)
    /// </summary>
    public double ZoomLevel { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the maximum zoom level supported by the current camera
    /// </summary>
    public double MaxZoomLevel { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether the current camera supports torch/flash functionality
    /// </summary>
    public bool HasTorchSupport { get; set; }

    /// <summary>
    /// Gets or sets whether multiple cameras are available on the device
    /// </summary>
    public bool HasMultipleCameras { get; set; }

    /// <summary>
    /// Gets or sets the current camera ID
    /// </summary>
    public string CurrentCameraId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the current camera supports zoom functionality
    /// </summary>
    public bool HasZoomSupport { get; set; }
}