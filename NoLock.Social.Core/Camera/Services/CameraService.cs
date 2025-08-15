using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;

namespace NoLock.Social.Core.Camera.Services
{
    public class CameraService : ICameraService
    {
        private readonly IJSRuntime _jsRuntime;
        private CameraPermissionState _currentPermissionState = CameraPermissionState.Prompt;

        public CameraService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public async Task<CameraPermissionState> RequestPermission()
        {
            // Call JavaScript to request camera permission
            var result = await _jsRuntime.InvokeAsync<string>("cameraPermissions.request");
            
            // Parse result string to CameraPermissionState enum
            _currentPermissionState = result?.ToLowerInvariant() switch
            {
                "granted" => CameraPermissionState.Granted,
                "denied" => CameraPermissionState.Denied,
                "prompt" => CameraPermissionState.Prompt,
                _ => CameraPermissionState.Denied
            };
            
            return _currentPermissionState;
        }
    }
}