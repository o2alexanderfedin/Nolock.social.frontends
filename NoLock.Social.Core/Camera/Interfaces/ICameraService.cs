using System.Threading.Tasks;

namespace NoLock.Social.Core.Camera.Interfaces
{
    public interface ICameraService
    {
        Task<CameraPermissionState> RequestPermission();
    }
}