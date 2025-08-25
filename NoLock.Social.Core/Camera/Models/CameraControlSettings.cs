namespace NoLock.Social.Core.Camera.Models
{
    public class CameraControlSettings
    {
        public bool IsTorchEnabled { get; set; }
        public bool TorchEnabled => IsTorchEnabled;
        public double ZoomLevel { get; set; } = 1.0;
        public double MaxZoom { get; set; } = 3.0;
        public double MaxZoomLevel { get; set; } = 3.0;
        public bool HasTorchSupport { get; set; }
        public bool HasZoomSupport { get; set; }
        public string CurrentCameraId { get; set; } = string.Empty;
        public bool AutoCaptureEnabled { get; set; }
        public int AutoCaptureDelay { get; set; } = 3;
    }
}
