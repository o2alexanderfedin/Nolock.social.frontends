using System;
using FluentAssertions;
using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera.Models
{
    public class CameraOptionsTests
    {
        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            // Act
            var options = new CameraOptions();

            // Assert
            options.FacingMode.Should().Be(CameraFacingMode.Environment);
            options.Width.Should().Be(1920);
            options.Height.Should().Be(1080);
            options.VideoDeviceId.Should().BeEmpty();
        }

        [Theory]
        [InlineData(CameraFacingMode.User)]
        [InlineData(CameraFacingMode.Environment)]
        public void FacingMode_CanBeSetAndRetrieved(CameraFacingMode mode)
        {
            // Arrange
            var options = new CameraOptions();

            // Act
            options.FacingMode = mode;

            // Assert
            options.FacingMode.Should().Be(mode);
        }

        [Theory]
        [InlineData(640, 480, "Basic VGA resolution")]
        [InlineData(1280, 720, "HD 720p resolution")]
        [InlineData(1920, 1080, "Full HD 1080p resolution")]
        [InlineData(3840, 2160, "4K UHD resolution")]
        [InlineData(0, 0, "Zero dimensions")]
        [InlineData(-1, -1, "Negative dimensions")]
        [InlineData(int.MaxValue, int.MaxValue, "Maximum integer dimensions")]
        public void Dimensions_CanBeSetAndRetrieved(int width, int height, string scenario)
        {
            // Arrange
            var options = new CameraOptions();

            // Act
            options.Width = width;
            options.Height = height;

            // Assert
            options.Width.Should().Be(width, $"Width should match for {scenario}");
            options.Height.Should().Be(height, $"Height should match for {scenario}");
        }

        [Theory]
        [InlineData("", "Empty device ID")]
        [InlineData("device123", "Standard device ID")]
        [InlineData("webcam-front-facing", "Descriptive device ID")]
        [InlineData("00000000-0000-0000-0000-000000000000", "GUID-like device ID")]
        [InlineData(null, "Null device ID")]
        [InlineData("   ", "Whitespace device ID")]
        [InlineData("device with spaces", "Device ID with spaces")]
        public void VideoDeviceId_CanBeSetAndRetrieved(string deviceId, string scenario)
        {
            // Arrange
            var options = new CameraOptions();

            // Act
            options.VideoDeviceId = deviceId;

            // Assert
            options.VideoDeviceId.Should().Be(deviceId, $"VideoDeviceId should match for {scenario}");
        }

        [Fact]
        public void MultiplePropertyChanges_MaintainIndependence()
        {
            // Arrange
            var options = new CameraOptions();
            
            // Act - Change properties in sequence
            options.FacingMode = CameraFacingMode.User;
            options.Width = 640;
            options.Height = 480;
            options.VideoDeviceId = "test-device";

            // Assert - All properties should maintain their values
            options.FacingMode.Should().Be(CameraFacingMode.User);
            options.Width.Should().Be(640);
            options.Height.Should().Be(480);
            options.VideoDeviceId.Should().Be("test-device");
        }

        [Fact]
        public void CreateMultipleInstances_HaveIndependentDefaults()
        {
            // Act
            var options1 = new CameraOptions();
            var options2 = new CameraOptions();
            
            options1.FacingMode = CameraFacingMode.User;
            options1.Width = 640;

            // Assert
            options2.FacingMode.Should().Be(CameraFacingMode.Environment, "Second instance should have default facing mode");
            options2.Width.Should().Be(1920, "Second instance should have default width");
            options1.Should().NotBeSameAs(options2, "Instances should be different objects");
        }

        [Fact]
        public void CameraOptions_SupportsObjectInitializer()
        {
            // Act
            var options = new CameraOptions
            {
                FacingMode = CameraFacingMode.User,
                Width = 2560,
                Height = 1440,
                VideoDeviceId = "integrated-camera"
            };

            // Assert
            options.FacingMode.Should().Be(CameraFacingMode.User);
            options.Width.Should().Be(2560);
            options.Height.Should().Be(1440);
            options.VideoDeviceId.Should().Be("integrated-camera");
        }

        [Theory]
        [InlineData(1920, 1080, 16.0/9.0, "16:9 aspect ratio")]
        [InlineData(1280, 720, 16.0/9.0, "16:9 HD aspect ratio")]
        [InlineData(640, 480, 4.0/3.0, "4:3 aspect ratio")]
        [InlineData(1024, 768, 4.0/3.0, "4:3 XGA aspect ratio")]
        public void CommonResolutions_MaintainProperAspectRatio(int width, int height, double expectedRatio, string scenario)
        {
            // Arrange
            var options = new CameraOptions { Width = width, Height = height };

            // Act
            var actualRatio = (double)options.Width / options.Height;

            // Assert
            actualRatio.Should().BeApproximately(expectedRatio, 0.01, $"Aspect ratio should be correct for {scenario}");
        }
    }

    public class CameraFacingModeTests
    {
        [Fact]
        public void CameraFacingMode_HasExpectedValues()
        {
            // Assert
            Enum.GetValues<CameraFacingMode>().Should().HaveCount(2);
            Enum.IsDefined(typeof(CameraFacingMode), CameraFacingMode.User).Should().BeTrue();
            Enum.IsDefined(typeof(CameraFacingMode), CameraFacingMode.Environment).Should().BeTrue();
        }

        [Theory]
        [InlineData(CameraFacingMode.User, 0)]
        [InlineData(CameraFacingMode.Environment, 1)]
        public void CameraFacingMode_HasExpectedNumericValues(CameraFacingMode mode, int expectedValue)
        {
            // Assert
            ((int)mode).Should().Be(expectedValue);
        }

        [Theory]
        [InlineData("User", CameraFacingMode.User)]
        [InlineData("Environment", CameraFacingMode.Environment)]
        public void CameraFacingMode_CanBeParsedFromString(string value, CameraFacingMode expected)
        {
            // Act
            var result = Enum.Parse<CameraFacingMode>(value);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("user")]
        [InlineData("ENVIRONMENT")]
        [InlineData("Invalid")]
        [InlineData("")]
        public void CameraFacingMode_ParseInvalidString_ThrowsArgumentException(string invalidValue)
        {
            // Act & Assert
            var action = () => Enum.Parse<CameraFacingMode>(invalidValue);
            action.Should().Throw<ArgumentException>();
        }
    }
}