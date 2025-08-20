using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.ImageProcessing.Models;
using NoLock.Social.Core.ImageProcessing.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.ImageProcessing
{
    /// <summary>
    /// Integration tests for complete image enhancement workflows
    /// Tests the full enhancement chain with multiple operations
    /// </summary>
    public class ImageEnhancementIntegrationTests
    {
        private readonly Mock<IJSRuntime> _mockJSRuntime;
        private readonly ImageEnhancementService _service;
        private const string TestImageData = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAAAAAAAD";

        public ImageEnhancementIntegrationTests()
        {
            _mockJSRuntime = new Mock<IJSRuntime>();
            _service = new ImageEnhancementService(_mockJSRuntime.Object);
        }

        [Theory]
        [InlineData(true, true, true, true, "all enhancements enabled")]
        [InlineData(true, false, false, false, "only contrast adjustment")]
        [InlineData(false, true, false, false, "only shadow removal")]
        [InlineData(false, false, true, false, "only perspective correction")]
        [InlineData(false, false, false, true, "only grayscale conversion")]
        [InlineData(true, true, false, false, "contrast and shadow removal")]
        [InlineData(false, false, true, true, "perspective and grayscale")]
        public async Task EnhanceImageAsync_WithDifferentSettingsCombinations_AppliesCorrectOperations(
            bool enableContrast, 
            bool enableShadow, 
            bool enablePerspective, 
            bool enableGrayscale,
            string scenario)
        {
            // Arrange
            var capturedImage = new CapturedImage
            {
                ImageData = TestImageData,
                Quality = 60
            };

            var settings = new EnhancementSettings
            {
                EnableContrastAdjustment = enableContrast,
                EnableShadowRemoval = enableShadow,
                EnablePerspectiveCorrection = enablePerspective,
                ConvertToGrayscale = enableGrayscale
            };

            var expectedResult = "data:image/jpeg;base64,fully_enhanced_image";
            SetupMockJSRuntime(expectedResult);

            // Act
            var result = await _service.EnhanceImageAsync(capturedImage, settings);

            // Assert
            Assert.True(result.IsSuccessful, $"Enhancement should succeed for {scenario}");
            Assert.Equal(expectedResult, result.EnhancedImageData);
            Assert.NotNull(result.AppliedOperations);
            
            // Verify correct number of operations applied
            var expectedOperationCount = GetExpectedOperationCount(enableContrast, enableShadow, enablePerspective, enableGrayscale);
            Assert.Equal(expectedOperationCount, result.AppliedOperations.Count);
            
            // Verify specific operations were applied
            if (enableContrast)
                Assert.Contains(result.AppliedOperations, op => op.OperationType == EnhancementOperationType.ContrastAdjustment);
            if (enableShadow)
                Assert.Contains(result.AppliedOperations, op => op.OperationType == EnhancementOperationType.ShadowRemoval);
            if (enablePerspective)
                Assert.Contains(result.AppliedOperations, op => op.OperationType == EnhancementOperationType.PerspectiveCorrection);
            if (enableGrayscale)
                Assert.Contains(result.AppliedOperations, op => op.OperationType == EnhancementOperationType.GrayscaleConversion);
        }

        [Theory]
        [InlineData(60, 15, "medium quality image")]
        [InlineData(85, 2, "high quality image")]
        public async Task EnhanceImageAsync_WithDifferentQualityLevels_ProducesAppropriateImprovement(
            int originalQuality,
            int minExpectedImprovement,
            string scenario)
        {
            // Arrange
            var capturedImage = new CapturedImage
            {
                ImageData = TestImageData,
                Quality = originalQuality
            };

            var settings = new EnhancementSettings(); // All enhancements enabled by default
            var expectedResult = "data:image/jpeg;base64,quality_improved_image";
            SetupMockJSRuntime(expectedResult);

            // Act
            var result = await _service.EnhanceImageAsync(capturedImage, settings);

            // Assert
            Assert.True(result.IsSuccessful, $"Enhancement should succeed for {scenario}");
            Assert.True(result.QualityScore >= originalQuality + minExpectedImprovement, 
                $"Quality should improve by at least {minExpectedImprovement} points for {scenario}");
            Assert.True(result.QualityScore <= 100, "Quality score should not exceed 100");
        }

        [Theory]
        [InlineData(0.5, 1200, 900, false, "small image")]
        [InlineData(1.5, 1800, 1200, false, "medium image")]
        [InlineData(3.0, 2400, 1800, true, "large image")]
        [InlineData(5.0, 4000, 3000, true, "very large image")]
        public async Task EnhanceImageAsync_WithDifferentImageSizes_HandlesOptimization(
            double imageSizeMB,
            int width,
            int height,
            bool shouldOptimize,
            string scenario)
        {
            // Arrange
            var testImageData = GenerateTestImageData(imageSizeMB);
            var capturedImage = new CapturedImage
            {
                ImageData = testImageData,
                Quality = 70
            };

            var settings = new EnhancementSettings();
            var imageInfo = new ImageInfo
            {
                Width = width,
                Height = height,
                SizeMB = imageSizeMB,
                Format = "jpeg"
            };

            SetupMockJSRuntimeWithImageInfo(imageInfo, testImageData);

            // Act
            var result = await _service.EnhanceImageAsync(capturedImage, settings);

            // Assert
            Assert.True(result.IsSuccessful, $"Enhancement should succeed for {scenario}");
            
            // Verify optimization was used for large images
            if (shouldOptimize)
            {
                _mockJSRuntime.Verify(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()), 
                    Times.Once, $"Should analyze image size for {scenario}");
            }
        }

        [Fact]
        public async Task GetEnhancementPreviewAsync_WithValidImage_ReturnsPreviewWithOperations()
        {
            // Arrange
            var capturedImage = new CapturedImage
            {
                ImageData = TestImageData,
                Quality = 65
            };

            var settings = new EnhancementSettings
            {
                EnableContrastAdjustment = true,
                EnableShadowRemoval = true,
                EnablePerspectiveCorrection = false,
                ConvertToGrayscale = true
            };

            var expectedResult = "data:image/jpeg;base64,preview_enhanced_image";
            SetupMockJSRuntime(expectedResult);

            // Act
            var preview = await _service.GetEnhancementPreviewAsync(capturedImage, settings);

            // Assert
            Assert.NotNull(preview);
            Assert.Equal(TestImageData, preview.OriginalImageData);
            Assert.Equal(expectedResult, preview.PreviewImageData);
            Assert.Contains(EnhancementOperationType.ContrastAdjustment, preview.PlannedOperations);
            Assert.Contains(EnhancementOperationType.ShadowRemoval, preview.PlannedOperations);
            Assert.Contains(EnhancementOperationType.GrayscaleConversion, preview.PlannedOperations);
            Assert.DoesNotContain(EnhancementOperationType.PerspectiveCorrection, preview.PlannedOperations);
            Assert.True(preview.EstimatedProcessingTimeMs > 0);
            Assert.True(preview.PredictedQualityImprovement > 0);
        }

        [Fact]
        public async Task EnhanceImageAsync_WhenJSRuntimeFails_ReturnsFailureResult()
        {
            // Arrange
            var capturedImage = new CapturedImage
            {
                ImageData = TestImageData,
                Quality = 70
            };

            var settings = new EnhancementSettings();

            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);
            _mockJSRuntime.Setup(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()))
                .ReturnsAsync(new ImageInfo { Width = 1200, Height = 900, SizeMB = 1.0, Format = "jpeg" });
            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Enhancement failed"));

            // Act
            var result = await _service.EnhanceImageAsync(capturedImage, settings);

            // Assert
            Assert.False(result.IsSuccessful);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("Enhancement failed", result.ErrorMessage);
            Assert.Equal(TestImageData, result.EnhancedImageData); // Should return original on failure
        }

        [Fact]
        public async Task EnhanceImageAsync_WithCaching_UsesAndStoresCachedResults()
        {
            // Arrange
            var capturedImage = new CapturedImage
            {
                ImageData = TestImageData,
                Quality = 70
            };

            var settings = new EnhancementSettings();
            var expectedResult = "data:image/jpeg;base64,cached_enhanced_image";
            SetupMockJSRuntime(expectedResult);

            // Act - First enhancement
            var result1 = await _service.EnhanceImageAsync(capturedImage, settings);
            
            // Act - Second enhancement with same parameters
            var result2 = await _service.EnhanceImageAsync(capturedImage, settings);

            // Assert
            Assert.True(result1.IsSuccessful);
            Assert.True(result2.IsSuccessful);
            Assert.Equal(result1.EnhancedImageData, result2.EnhancedImageData);
            
            // Second call should be faster due to caching
            Assert.True(result2.ProcessingTimeMs <= result1.ProcessingTimeMs);
            
            // Should have called JS runtime only once for the actual enhancement
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()), 
                Times.Once, "Enhancement should be cached after first call");
        }

        private void SetupMockJSRuntime(string expectedResult)
        {
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            _mockJSRuntime.Setup(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()))
                .ReturnsAsync(new ImageInfo { Width = 1200, Height = 900, SizeMB = 1.0, Format = "jpeg" });

            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()))
                .ReturnsAsync(expectedResult);
        }

        private void SetupMockJSRuntimeWithImageInfo(ImageInfo imageInfo, string testImageData)
        {
            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            _mockJSRuntime.Setup(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()))
                .ReturnsAsync(imageInfo);

            if (imageInfo.SizeMB > 2.0 || imageInfo.Width > 1920 || imageInfo.Height > 1920)
            {
                _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.compressImage", It.IsAny<object[]>()))
                    .ReturnsAsync(testImageData);
            }

            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()))
                .ReturnsAsync(testImageData);
        }

        private static int GetExpectedOperationCount(bool enableContrast, bool enableShadow, bool enablePerspective, bool enableGrayscale)
        {
            int count = 0;
            if (enableContrast) count++;
            if (enableShadow) count++;
            if (enablePerspective) count++;
            if (enableGrayscale) count++;
            return count;
        }

        private static string GenerateTestImageData(double sizeMB)
        {
            var targetBytes = (int)(sizeMB * 1024 * 1024 * 0.75);
            var dataBytes = new byte[targetBytes];
            new Random().NextBytes(dataBytes);
            return $"data:image/jpeg;base64,{Convert.ToBase64String(dataBytes)}";
        }

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