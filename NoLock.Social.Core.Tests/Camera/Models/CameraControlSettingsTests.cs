using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera.Models;

public class CameraControlSettingsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var settings = new CameraControlSettings();

        // Assert
        Assert.False(settings.IsTorchEnabled);
        Assert.False(settings.TorchEnabled);
        Assert.Equal(1.0, settings.ZoomLevel);
        Assert.Equal(3.0, settings.MaxZoom);
        Assert.Equal(3.0, settings.MaxZoomLevel);
        Assert.False(settings.HasTorchSupport);
        Assert.False(settings.HasZoomSupport);
        Assert.Equal(string.Empty, settings.CurrentCameraId);
        Assert.False(settings.AutoCaptureEnabled);
        Assert.Equal(3, settings.AutoCaptureDelay);
    }

    [Theory]
    [InlineData(true, "Torch enabled")]
    [InlineData(false, "Torch disabled")]
    public void IsTorchEnabled_CanBeSetToValidValues(bool enabled, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.IsTorchEnabled = enabled;

        // Assert
        Assert.Equal(enabled, settings.IsTorchEnabled);
    }

    [Theory]
    [InlineData(true, true, "Torch enabled")]
    [InlineData(false, false, "Torch disabled")]
    public void TorchEnabled_ReflectsIsTorchEnabled(bool isTorchEnabled, bool expectedTorchEnabled, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.IsTorchEnabled = isTorchEnabled;

        // Assert
        Assert.Equal(expectedTorchEnabled, settings.TorchEnabled);
    }

    [Theory]
    [InlineData(1.0, "Minimum zoom")]
    [InlineData(1.5, "Moderate zoom")]
    [InlineData(2.0, "High zoom")]
    [InlineData(3.0, "Maximum zoom")]
    [InlineData(0.5, "Below minimum zoom")]
    [InlineData(5.0, "Above maximum zoom")]
    public void ZoomLevel_CanBeSetToValidValues(double zoomLevel, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.ZoomLevel = zoomLevel;

        // Assert
        Assert.Equal(zoomLevel, settings.ZoomLevel);
    }

    [Theory]
    [InlineData(1.0, "Minimum max zoom")]
    [InlineData(3.0, "Default max zoom")]
    [InlineData(5.0, "High max zoom")]
    [InlineData(10.0, "Very high max zoom")]
    [InlineData(0.5, "Below minimum max zoom")]
    public void MaxZoom_CanBeSetToValidValues(double maxZoom, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.MaxZoom = maxZoom;

        // Assert
        Assert.Equal(maxZoom, settings.MaxZoom);
    }

    [Theory]
    [InlineData(1.0, "Minimum max zoom level")]
    [InlineData(3.0, "Default max zoom level")]
    [InlineData(5.0, "High max zoom level")]
    [InlineData(10.0, "Very high max zoom level")]
    [InlineData(0.5, "Below minimum max zoom level")]
    public void MaxZoomLevel_CanBeSetToValidValues(double maxZoomLevel, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.MaxZoomLevel = maxZoomLevel;

        // Assert
        Assert.Equal(maxZoomLevel, settings.MaxZoomLevel);
    }

    [Theory]
    [InlineData(true, "Has torch support")]
    [InlineData(false, "No torch support")]
    public void HasTorchSupport_CanBeSetToValidValues(bool hasSupport, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.HasTorchSupport = hasSupport;

        // Assert
        Assert.Equal(hasSupport, settings.HasTorchSupport);
    }

    [Theory]
    [InlineData(true, "Has zoom support")]
    [InlineData(false, "No zoom support")]
    public void HasZoomSupport_CanBeSetToValidValues(bool hasSupport, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.HasZoomSupport = hasSupport;

        // Assert
        Assert.Equal(hasSupport, settings.HasZoomSupport);
    }

    [Theory]
    [InlineData("camera-front", "Front camera")]
    [InlineData("camera-back", "Back camera")]
    [InlineData("usb-camera-1", "USB camera")]
    [InlineData("", "Empty camera ID")]
    [InlineData("camera-with-very-long-identifier-name", "Long camera ID")]
    public void CurrentCameraId_CanBeSetToValidValues(string cameraId, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.CurrentCameraId = cameraId;

        // Assert
        Assert.Equal(cameraId, settings.CurrentCameraId);
    }

    [Theory]
    [InlineData(true, "Auto capture enabled")]
    [InlineData(false, "Auto capture disabled")]
    public void AutoCaptureEnabled_CanBeSetToValidValues(bool enabled, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.AutoCaptureEnabled = enabled;

        // Assert
        Assert.Equal(enabled, settings.AutoCaptureEnabled);
    }

    [Theory]
    [InlineData(1, "Minimum delay")]
    [InlineData(3, "Default delay")]
    [InlineData(5, "Longer delay")]
    [InlineData(10, "Maximum practical delay")]
    [InlineData(0, "No delay")]
    [InlineData(-1, "Negative delay")]
    public void AutoCaptureDelay_CanBeSetToValidValues(int delay, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.AutoCaptureDelay = delay;

        // Assert
        Assert.Equal(delay, settings.AutoCaptureDelay);
    }

    [Fact]
    public void AllProperties_CanBeSetIndependently()
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.IsTorchEnabled = true;
        settings.ZoomLevel = 2.5;
        settings.MaxZoom = 5.0;
        settings.MaxZoomLevel = 4.0;
        settings.HasTorchSupport = true;
        settings.HasZoomSupport = true;
        settings.CurrentCameraId = "test-camera";
        settings.AutoCaptureEnabled = true;
        settings.AutoCaptureDelay = 5;

        // Assert
        Assert.True(settings.IsTorchEnabled);
        Assert.True(settings.TorchEnabled); // Should reflect IsTorchEnabled
        Assert.Equal(2.5, settings.ZoomLevel);
        Assert.Equal(5.0, settings.MaxZoom);
        Assert.Equal(4.0, settings.MaxZoomLevel);
        Assert.True(settings.HasTorchSupport);
        Assert.True(settings.HasZoomSupport);
        Assert.Equal("test-camera", settings.CurrentCameraId);
        Assert.True(settings.AutoCaptureEnabled);
        Assert.Equal(5, settings.AutoCaptureDelay);
    }

    [Fact]
    public void TorchEnabled_AlwaysMatchesIsTorchEnabled()
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Test initial state
        Assert.Equal(settings.IsTorchEnabled, settings.TorchEnabled);

        // Test when enabling torch
        settings.IsTorchEnabled = true;
        Assert.Equal(settings.IsTorchEnabled, settings.TorchEnabled);
        Assert.True(settings.TorchEnabled);

        // Test when disabling torch
        settings.IsTorchEnabled = false;
        Assert.Equal(settings.IsTorchEnabled, settings.TorchEnabled);
        Assert.False(settings.TorchEnabled);
    }

    [Theory]
    [InlineData(1.0, 3.0, 3.0, "Standard zoom configuration")]
    [InlineData(2.0, 5.0, 4.0, "High zoom configuration")]
    [InlineData(1.5, 2.0, 2.5, "Mixed zoom configuration")]
    [InlineData(0.8, 1.0, 1.2, "Low zoom configuration")]
    public void ZoomConfiguration_CanBeSetToValidCombinations(double zoomLevel, double maxZoom, double maxZoomLevel, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.ZoomLevel = zoomLevel;
        settings.MaxZoom = maxZoom;
        settings.MaxZoomLevel = maxZoomLevel;

        // Assert
        Assert.Equal(zoomLevel, settings.ZoomLevel);
        Assert.Equal(maxZoom, settings.MaxZoom);
        Assert.Equal(maxZoomLevel, settings.MaxZoomLevel);
    }

    [Theory]
    [InlineData(false, false, false, "No hardware support")]
    [InlineData(true, false, true, "Torch support only")]
    [InlineData(false, true, true, "Zoom support only")]
    [InlineData(true, true, true, "Full hardware support")]
    public void HardwareSupport_CanBeConfigured(bool hasTorchSupport, bool hasZoomSupport, bool anySupport, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.HasTorchSupport = hasTorchSupport;
        settings.HasZoomSupport = hasZoomSupport;

        // Assert
        Assert.Equal(hasTorchSupport, settings.HasTorchSupport);
        Assert.Equal(hasZoomSupport, settings.HasZoomSupport);
        Assert.Equal(anySupport, settings.HasTorchSupport || settings.HasZoomSupport);
    }

    [Theory]
    [InlineData(false, 0, "Auto capture disabled with no delay")]
    [InlineData(true, 1, "Auto capture enabled with minimum delay")]
    [InlineData(true, 3, "Auto capture enabled with default delay")]
    [InlineData(true, 10, "Auto capture enabled with long delay")]
    public void AutoCaptureConfiguration_CanBeSetToValidCombinations(bool enabled, int delay, string scenario)
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act
        settings.AutoCaptureEnabled = enabled;
        settings.AutoCaptureDelay = delay;

        // Assert
        Assert.Equal(enabled, settings.AutoCaptureEnabled);
        Assert.Equal(delay, settings.AutoCaptureDelay);
    }

    [Fact]
    public void EdgeCaseValues_AreHandledCorrectly()
    {
        // Arrange
        var settings = new CameraControlSettings();

        // Act - Set edge case values
        settings.ZoomLevel = 0.0; // Zero zoom
        settings.MaxZoom = 0.0; // Zero max zoom
        settings.MaxZoomLevel = 0.0; // Zero max zoom level
        settings.AutoCaptureDelay = 0; // Zero delay
        settings.CurrentCameraId = string.Empty; // Empty string

        // Assert - Values should be stored as-is
        Assert.Equal(0.0, settings.ZoomLevel);
        Assert.Equal(0.0, settings.MaxZoom);
        Assert.Equal(0.0, settings.MaxZoomLevel);
        Assert.Equal(0, settings.AutoCaptureDelay);
        Assert.Equal(string.Empty, settings.CurrentCameraId);
    }

    [Fact]
    public void ComplexConfiguration_WorksCorrectly()
    {
        // Arrange & Act
        var settings = new CameraControlSettings
        {
            IsTorchEnabled = true,
            ZoomLevel = 2.5,
            MaxZoom = 5.0,
            MaxZoomLevel = 5.0,
            HasTorchSupport = true,
            HasZoomSupport = true,
            CurrentCameraId = "advanced-camera-device",
            AutoCaptureEnabled = true,
            AutoCaptureDelay = 2
        };

        // Assert - All properties maintain their values
        Assert.True(settings.IsTorchEnabled);
        Assert.True(settings.TorchEnabled);
        Assert.Equal(2.5, settings.ZoomLevel);
        Assert.Equal(5.0, settings.MaxZoom);
        Assert.Equal(5.0, settings.MaxZoomLevel);
        Assert.True(settings.HasTorchSupport);
        Assert.True(settings.HasZoomSupport);
        Assert.Equal("advanced-camera-device", settings.CurrentCameraId);
        Assert.True(settings.AutoCaptureEnabled);
        Assert.Equal(2, settings.AutoCaptureDelay);
    }
}