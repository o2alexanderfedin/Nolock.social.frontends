using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera.Models;

public class CameraStreamTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var stream = new CameraStream();

        // Assert
        Assert.NotEqual(string.Empty, stream.StreamId);
        Assert.NotEqual(Guid.Empty.ToString(), stream.StreamId);
        Assert.Equal(string.Empty, stream.StreamUrl);
        Assert.Equal(0, stream.Width);
        Assert.Equal(0, stream.Height);
        Assert.False(stream.IsActive);
        Assert.Equal(string.Empty, stream.DeviceId);
        Assert.True(DateTime.UtcNow.Subtract(stream.StartedAt).TotalSeconds < 1);
    }

    [Fact]
    public void StreamId_GeneratesUniqueIds()
    {
        // Arrange & Act
        var stream1 = new CameraStream();
        var stream2 = new CameraStream();

        // Assert
        Assert.NotEqual(stream1.StreamId, stream2.StreamId);
        Assert.True(Guid.TryParse(stream1.StreamId, out _));
        Assert.True(Guid.TryParse(stream2.StreamId, out _));
    }

    [Theory]
    [InlineData("http://localhost:8080/stream", "Stream URL with HTTP")]
    [InlineData("https://camera.example.com/live", "Stream URL with HTTPS")]
    [InlineData("rtmp://streaming.server/live", "Stream URL with RTMP")]
    [InlineData("", "Empty stream URL")]
    public void StreamUrl_CanBeSetToValidValues(string url, string scenario)
    {
        // Arrange
        var stream = new CameraStream();

        // Act
        stream.StreamUrl = url;

        // Assert
        Assert.Equal(url, stream.StreamUrl);
    }

    [Theory]
    [InlineData(1920, 1080, "Full HD resolution")]
    [InlineData(1280, 720, "HD resolution")]
    [InlineData(640, 480, "Standard resolution")]
    [InlineData(0, 0, "Zero resolution")]
    public void Dimensions_CanBeSetToValidValues(int width, int height, string scenario)
    {
        // Arrange
        var stream = new CameraStream();

        // Act
        stream.Width = width;
        stream.Height = height;

        // Assert
        Assert.Equal(width, stream.Width);
        Assert.Equal(height, stream.Height);
    }

    [Theory]
    [InlineData(true, "Active stream")]
    [InlineData(false, "Inactive stream")]
    public void IsActive_CanBeSetToValidValues(bool isActive, string scenario)
    {
        // Arrange
        var stream = new CameraStream();

        // Act
        stream.IsActive = isActive;

        // Assert
        Assert.Equal(isActive, stream.IsActive);
    }

    [Theory]
    [InlineData("camera-front", "Front camera device")]
    [InlineData("camera-back", "Back camera device")]
    [InlineData("usb-camera-1", "USB camera device")]
    [InlineData("", "Empty device ID")]
    public void DeviceId_CanBeSetToValidValues(string deviceId, string scenario)
    {
        // Arrange
        var stream = new CameraStream();

        // Act
        stream.DeviceId = deviceId;

        // Assert
        Assert.Equal(deviceId, stream.DeviceId);
    }

    [Fact]
    public void StartedAt_CanBeSetToCustomValue()
    {
        // Arrange
        var stream = new CameraStream();
        var customTime = DateTime.UtcNow.AddMinutes(-5);

        // Act
        stream.StartedAt = customTime;

        // Assert
        Assert.Equal(customTime, stream.StartedAt);
    }

    [Fact]
    public void AllProperties_CanBeSetIndependently()
    {
        // Arrange
        var stream = new CameraStream();
        var customStartTime = DateTime.UtcNow.AddHours(-1);

        // Act
        stream.StreamUrl = "https://test.stream/live";
        stream.Width = 1920;
        stream.Height = 1080;
        stream.IsActive = true;
        stream.DeviceId = "test-device";
        stream.StartedAt = customStartTime;

        // Assert
        Assert.Equal("https://test.stream/live", stream.StreamUrl);
        Assert.Equal(1920, stream.Width);
        Assert.Equal(1080, stream.Height);
        Assert.True(stream.IsActive);
        Assert.Equal("test-device", stream.DeviceId);
        Assert.Equal(customStartTime, stream.StartedAt);
        // StreamId should still be auto-generated
        Assert.NotEqual(string.Empty, stream.StreamId);
        Assert.True(Guid.TryParse(stream.StreamId, out _));
    }

    [Fact]
    public void StreamId_CanBeOverridden()
    {
        // Arrange
        var stream = new CameraStream();
        var customId = "custom-stream-id-123";

        // Act
        stream.StreamId = customId;

        // Assert
        Assert.Equal(customId, stream.StreamId);
    }
}