using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.ImageProcessing.Models;
using NoLock.Social.Core.ImageProcessing.Services;
using NoLock.Social.Core.ImageProcessing.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.ImageProcessing
{
    /// <summary>
    /// Tests for individual image enhancement algorithms
    /// Validates the acceptance criteria for auto-contrast, shadow removal, perspective correction, and grayscale conversion
    /// </summary>
    public class ImageEnhancementAlgorithmTests
    {
        private readonly Mock<IJSRuntimeWrapper> _mockJSRuntime;
        private readonly ImageEnhancementService _service;
        private const string TestImageData = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAAAAAAAD";

        public ImageEnhancementAlgorithmTests()
        {
            _mockJSRuntime = new Mock<IJSRuntimeWrapper>();
            _service = new ImageEnhancementService(_mockJSRuntime.Object);
        }

        #region Auto-Contrast Adjustment Tests

        [Theory]
        [InlineData(0.5, "minimum contrast strength")]
        [InlineData(1.2, "default contrast strength")]
        [InlineData(2.0, "maximum contrast strength")]
        public async Task AdjustContrastAsync_WithValidStrength_ReturnsEnhancedImage(
            double strength, 
            string scenario)
        {
            // Arrange
            var expectedResult = "data:image/jpeg;base64,enhanced_contrast_image";
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.adjustContrast", TestImageData, strength))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.AdjustContrastAsync(TestImageData, strength);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>("imageEnhancement.adjustContrast", TestImageData, strength), 
                Times.Once, $"Should call contrast adjustment for {scenario}");
        }

        [Theory]
        [InlineData(0.05, "below minimum threshold")]
        [InlineData(2.5, "above maximum threshold")]
        public async Task AdjustContrastAsync_WithInvalidStrength_ThrowsArgumentOutOfRangeException(
            double invalidStrength,
            string scenario)
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => _service.AdjustContrastAsync(TestImageData, invalidStrength));
            
            Assert.Contains("Strength must be between 0.1 and 2.0", exception.Message);
        }

        [Theory]
        [InlineData(null, "null image data")]
        [InlineData("", "empty image data")]
        [InlineData("   ", "whitespace image data")]
        public async Task AdjustContrastAsync_WithInvalidImageData_ThrowsArgumentException(
            string invalidImageData,
            string scenario)
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.AdjustContrastAsync(invalidImageData, 1.2));
            
            Assert.Contains("Image data cannot be null or empty", exception.Message);
        }

        #endregion

        #region Shadow Removal Tests

        [Theory]
        [InlineData(0.3, "low intensity shadow removal")]
        [InlineData(0.7, "default intensity shadow removal")]
        [InlineData(1.0, "maximum intensity shadow removal")]
        public async Task RemoveShadowsAsync_WithValidIntensity_ReturnsEnhancedImage(
            double intensity, 
            string scenario)
        {
            // Arrange
            var expectedResult = "data:image/jpeg;base64,shadow_removed_image";
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.removeShadows", TestImageData, intensity))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.RemoveShadowsAsync(TestImageData, intensity);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>("imageEnhancement.removeShadows", TestImageData, intensity), 
                Times.Once, $"Should call shadow removal for {scenario}");
        }

        [Theory]
        [InlineData(0.05, "below minimum threshold")]
        [InlineData(1.5, "above maximum threshold")]
        public async Task RemoveShadowsAsync_WithInvalidIntensity_ThrowsArgumentOutOfRangeException(
            double invalidIntensity,
            string scenario)
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => _service.RemoveShadowsAsync(TestImageData, invalidIntensity));
            
            Assert.Contains("Intensity must be between 0.1 and 1.0", exception.Message);
        }

        #endregion

        #region Perspective Correction Tests

        [Fact]
        public async Task CorrectPerspectiveAsync_WithValidImageData_ReturnsEnhancedImage()
        {
            // Arrange
            var expectedResult = "data:image/jpeg;base64,perspective_corrected_image";
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.correctPerspective", TestImageData))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.CorrectPerspectiveAsync(TestImageData);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>("imageEnhancement.correctPerspective", TestImageData), 
                Times.Once, "Should call perspective correction");
        }

        [Theory]
        [InlineData(null, "null image data")]
        [InlineData("", "empty image data")]
        public async Task CorrectPerspectiveAsync_WithInvalidImageData_ThrowsArgumentException(
            string invalidImageData,
            string scenario)
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CorrectPerspectiveAsync(invalidImageData));
            
            Assert.Contains("Image data cannot be null or empty", exception.Message);
        }

        #endregion

        #region Grayscale Conversion Tests

        [Fact]
        public async Task ConvertToGrayscaleAsync_WithValidImageData_ReturnsGrayscaleImage()
        {
            // Arrange
            var expectedResult = "data:image/jpeg;base64,grayscale_converted_image";
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.convertToGrayscale", TestImageData))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.ConvertToGrayscaleAsync(TestImageData);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>("imageEnhancement.convertToGrayscale", TestImageData), 
                Times.Once, "Should call grayscale conversion");
        }

        [Theory]
        [InlineData(null, "null image data")]
        [InlineData("", "empty image data")]
        public async Task ConvertToGrayscaleAsync_WithInvalidImageData_ThrowsArgumentException(
            string invalidImageData,
            string scenario)
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.ConvertToGrayscaleAsync(invalidImageData));
            
            Assert.Contains("Image data cannot be null or empty", exception.Message);
        }

        #endregion

        #region Service Initialization Tests

        [Fact]
        public async Task IsAvailableAsync_WhenServiceAvailable_ReturnsTrue()
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAvailableAsync_WhenServiceUnavailable_ReturnsFalse()
        {
            // Arrange
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Enhancement not available"));

            // Act
            var result = await _service.IsAvailableAsync();

            // Assert
            Assert.False(result);
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _service?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}