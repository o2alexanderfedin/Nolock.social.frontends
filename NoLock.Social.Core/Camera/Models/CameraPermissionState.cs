namespace NoLock.Social.Core.Camera.Models;

/// <summary>
/// Represents the state of camera permission in the browser
/// </summary>
public enum CameraPermissionState
{
    /// <summary>
    /// Permission has been granted
    /// </summary>
    Granted,
    
    /// <summary>
    /// Permission has been denied
    /// </summary>
    Denied,
    
    /// <summary>
    /// Permission state is prompt (not yet decided)
    /// </summary>
    Prompt
}