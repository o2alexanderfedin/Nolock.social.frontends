#!/bin/bash

echo "Fixing build errors..."

# 1. Fix CameraControlSettings missing properties
cat << 'EOF' > NoLock.Social.Core/Camera/Models/CameraControlSettings.cs
using System;

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
EOF

# 2. Fix ImageQualityResult missing property
cat << 'EOF' >> NoLock.Social.Core/Camera/Models/ImageQualityResult.cs

    public bool IsAcceptable => OverallScore >= 70;
}
EOF

# 3. Create missing CameraOptions class
cat << 'EOF' > NoLock.Social.Core/Camera/Models/CameraOptions.cs
namespace NoLock.Social.Core.Camera.Models
{
    public class CameraOptions
    {
        public CameraFacingMode FacingMode { get; set; } = CameraFacingMode.Environment;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
    }

    public enum CameraFacingMode
    {
        User,
        Environment
    }
}
EOF

echo "Build error fixes applied!"