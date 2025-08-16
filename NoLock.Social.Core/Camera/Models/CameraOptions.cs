namespace NoLock.Social.Core.Camera.Models
{
    public class CameraOptions
    {
        public CameraFacingMode FacingMode { get; set; } = CameraFacingMode.Environment;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public string VideoDeviceId { get; set; } = string.Empty;
    }

    public enum CameraFacingMode
    {
        User,
        Environment
    }
}
