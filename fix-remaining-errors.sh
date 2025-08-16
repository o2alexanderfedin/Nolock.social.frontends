#!/bin/bash

echo "Fixing remaining build errors..."

# 1. Fix EnhancementResult missing properties
cat << 'EOF' >> NoLock.Social.Core/ImageProcessing/Models/EnhancementModels.cs

    public bool Success => ProcessingSuccess;
    public CapturedImage EnhancedImage { get; set; }
}
EOF

# 2. Fix CameraOptions missing property
echo '    public string VideoDeviceId { get; set; } = string.Empty;' >> NoLock.Social.Core/Camera/Models/CameraOptions.cs

# 3. Fix PreventDefault issue (use JS Interop instead)
echo "Note: PreventDefault issues need manual fix in Blazor components"

echo "Fixes applied!"