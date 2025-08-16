using System;
using NoLock.Social.Core.OCR.Configuration;
using NoLock.Social.Core.OCR.Interfaces;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Configuration
{
    /// <summary>
    /// Unit tests for OCRServiceOptions configuration validation,
    /// including Wake Lock configuration options.
    /// </summary>
    public class OCRServiceOptionsTests
    {
        #region Constructor and Default Values Tests

        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Act
            var options = new OCRServiceOptions();

            // Assert
            Assert.Equal(0.7, options.MinimumConfidenceThreshold);
            Assert.True(options.EnableWakeLock);
            Assert.Equal("Processing document - preventing device sleep", options.WakeLockReason);
            Assert.Null(options.EnableCaching);
            Assert.Null(options.CacheExpirationMinutes);
            Assert.Null(options.CacheOnlyCompleteResults);
        }

        #endregion

        #region Wake Lock Configuration Tests

        [Theory]
        [InlineData(true, "OCR Processing", true)]
        [InlineData(true, "Document Analysis", true)]
        [InlineData(false, "", true)]
        [InlineData(false, null, true)]
        public void WakeLockConfiguration_WithValidSettings_IsValid(
            bool enableWakeLock, 
            string wakeLockReason, 
            bool expectedValid)
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = enableWakeLock;
            options.WakeLockReason = wakeLockReason;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Fact]
        public void WakeLockReason_WhenEnabledAndEmpty_ValidationFails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = true;
            options.WakeLockReason = "";

            // Act
            var result = options.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("WakeLockReason cannot be empty when EnableWakeLock is true", result.Errors);
        }

        [Fact]
        public void WakeLockReason_WhenEnabledAndNull_ValidationFails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = true;
            options.WakeLockReason = null;

            // Act
            var result = options.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("WakeLockReason cannot be empty when EnableWakeLock is true", result.Errors);
        }

        [Fact]
        public void WakeLockReason_WhenEnabledAndWhitespace_ValidationFails()
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = true;
            options.WakeLockReason = "   ";

            // Act
            var result = options.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("WakeLockReason cannot be empty when EnableWakeLock is true", result.Errors);
        }

        [Fact]
        public void WakeLockReason_WhenTooLong_ValidationWarns()
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = true;
            options.WakeLockReason = new string('A', 201); // 201 characters

            // Act
            var result = options.Validate();

            // Assert
            Assert.True(result.IsValid);
            Assert.Contains("WakeLockReason is quite long", result.Warnings);
        }

        [Theory]
        [InlineData(200, false)] // Exactly 200 characters - should not warn
        [InlineData(199, false)] // Under 200 characters - should not warn
        [InlineData(201, true)]  // Over 200 characters - should warn
        [InlineData(300, true)]  // Much longer - should warn
        public void WakeLockReason_LengthValidation_BehavesCorrectly(int length, bool shouldWarn)
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = true;
            options.WakeLockReason = new string('A', length);

            // Act
            var result = options.Validate();

            // Assert
            Assert.True(result.IsValid);
            if (shouldWarn)
            {
                Assert.Contains("WakeLockReason is quite long", result.Warnings);
            }
            else
            {
                Assert.DoesNotContain(result.Warnings, w => w.Contains("WakeLockReason is quite long"));
            }
        }

        [Fact]
        public void WakeLockReason_WhenDisabledAndEmpty_ValidationPasses()
        {
            // Arrange
            var options = CreateValidOptions();
            options.EnableWakeLock = false;
            options.WakeLockReason = "";

            // Act
            var result = options.Validate();

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        #endregion

        #region Base Configuration Validation Tests

        [Theory]
        [InlineData("", false, "BaseUrl is required")]
        [InlineData(null, false, "BaseUrl is required")]
        [InlineData("   ", false, "BaseUrl is required")]
        [InlineData("not-a-url", false, "valid HTTP or HTTPS URL")]
        [InlineData("ftp://example.com", false, "valid HTTP or HTTPS URL")]
        [InlineData("http://example.com", true, "")]
        [InlineData("https://api.example.com", true, "")]
        public void BaseUrl_Validation_BehavesCorrectly(string baseUrl, bool expectedValid, string expectedError)
        {
            // Arrange
            var options = CreateValidOptions();
            options.BaseUrl = baseUrl;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Contains(expectedError));
            }
        }

        [Theory]
        [InlineData("", false, "ApiKey is required")]
        [InlineData(null, false, "ApiKey is required")]
        [InlineData("   ", false, "ApiKey is required")]
        [InlineData("valid-api-key", true, "")]
        public void ApiKey_Validation_BehavesCorrectly(string apiKey, bool expectedValid, string expectedError)
        {
            // Arrange
            var options = CreateValidOptions();
            options.ApiKey = apiKey;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Contains(expectedError));
            }
        }

        [Theory]
        [InlineData(0, false, "must be greater than 0")]
        [InlineData(-1, false, "must be greater than 0")]
        [InlineData(1, true, "")]
        [InlineData(30, true, "")]
        [InlineData(300, true, "")]
        [InlineData(301, true, "high value")]
        public void TimeoutSeconds_Validation_BehavesCorrectly(int timeoutSeconds, bool expectedValid, string expectedMessage)
        {
            // Arrange
            var options = CreateValidOptions();
            options.TimeoutSeconds = timeoutSeconds;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Contains(expectedMessage));
            }
            else if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.Contains(result.Warnings, warning => warning.Contains(expectedMessage));
            }
        }

        [Theory]
        [InlineData(-1, false, "cannot be negative")]
        [InlineData(0, true, "")]
        [InlineData(3, true, "")]
        [InlineData(10, true, "")]
        [InlineData(11, true, "high value")]
        public void MaxRetryAttempts_Validation_BehavesCorrectly(int maxRetryAttempts, bool expectedValid, string expectedMessage)
        {
            // Arrange
            var options = CreateValidOptions();
            options.MaxRetryAttempts = maxRetryAttempts;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Contains(expectedMessage));
            }
            else if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.Contains(result.Warnings, warning => warning.Contains(expectedMessage));
            }
        }

        [Theory]
        [InlineData(-0.1, false, "between 0.0 and 1.0")]
        [InlineData(1.1, false, "between 0.0 and 1.0")]
        [InlineData(0.0, true, "")]
        [InlineData(0.4, true, "low value")]
        [InlineData(0.5, true, "")]
        [InlineData(0.7, true, "")]
        [InlineData(1.0, true, "")]
        public void MinimumConfidenceThreshold_Validation_BehavesCorrectly(double threshold, bool expectedValid, string expectedMessage)
        {
            // Arrange
            var options = CreateValidOptions();
            options.MinimumConfidenceThreshold = threshold;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Contains(expectedMessage));
            }
            else if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.Contains(result.Warnings, warning => warning.Contains(expectedMessage));
            }
        }

        #endregion

        #region Cache Configuration Tests

        [Theory]
        [InlineData(null, true, "")]
        [InlineData(0, false, "must be greater than 0")]
        [InlineData(-1, false, "must be greater than 0")]
        [InlineData(1, true, "")]
        [InlineData(60, true, "")]
        [InlineData(1440, true, "")]
        [InlineData(1441, true, "high value")]
        public void CacheExpirationMinutes_Validation_BehavesCorrectly(int? cacheExpiration, bool expectedValid, string expectedMessage)
        {
            // Arrange
            var options = CreateValidOptions();
            options.CacheExpirationMinutes = cacheExpiration;

            // Act
            var result = options.Validate();

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Contains(expectedMessage));
            }
            else if (!string.IsNullOrEmpty(expectedMessage))
            {
                Assert.Contains(result.Warnings, warning => warning.Contains(expectedMessage));
            }
        }

        #endregion

        #region Polling Configuration Tests

        [Fact]
        public void PollingConfiguration_WhenNull_ValidationPasses()
        {
            // Arrange
            var options = CreateValidOptions();
            options.PollingConfiguration = null;

            // Act
            var result = options.Validate();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void PollingConfiguration_WhenInvalid_ValidationFails()
        {
            // Arrange
            var options = CreateValidOptions();
            // Create invalid polling configuration (negative initial interval)
            options.PollingConfiguration = new PollingConfiguration
            {
                InitialIntervalSeconds = -1,
                MaxIntervalSeconds = 30,
                BackoffMultiplier = 2.0
            };

            // Act
            var result = options.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Polling configuration error", result.Errors);
        }

        #endregion

        #region Multiple Errors Tests

        [Fact]
        public void Validate_WithMultipleErrors_ReturnsAllErrors()
        {
            // Arrange
            var options = new OCRServiceOptions
            {
                BaseUrl = "", // Invalid
                ApiKey = "", // Invalid
                TimeoutSeconds = -1, // Invalid
                MinimumConfidenceThreshold = 1.5, // Invalid
                EnableWakeLock = true,
                WakeLockReason = "", // Invalid when EnableWakeLock is true
                CacheExpirationMinutes = -1 // Invalid
            };

            // Act
            var result = options.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Length >= 5); // Should have multiple errors
            Assert.Contains("BaseUrl is required", result.Errors);
            Assert.Contains("ApiKey is required", result.Errors);
            Assert.Contains("TimeoutSeconds must be greater than 0", result.Errors);
            Assert.Contains("MinimumConfidenceThreshold must be between 0.0 and 1.0", result.Errors);
            Assert.Contains("WakeLockReason cannot be empty when EnableWakeLock is true", result.Errors);
            Assert.Contains("CacheExpirationMinutes must be greater than 0", result.Errors);
        }

        [Fact]
        public void Validate_WithMultipleWarnings_ReturnsAllWarnings()
        {
            // Arrange
            var options = CreateValidOptions();
            options.TimeoutSeconds = 301; // Warning
            options.MaxRetryAttempts = 11; // Warning
            options.MinimumConfidenceThreshold = 0.4; // Warning
            options.CacheExpirationMinutes = 1441; // Warning
            options.WakeLockReason = new string('A', 201); // Warning

            // Act
            var result = options.Validate();

            // Assert
            Assert.True(result.IsValid);
            Assert.True(result.Warnings.Length >= 5);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a valid OCRServiceOptions instance for testing.
        /// </summary>
        private static OCRServiceOptions CreateValidOptions()
        {
            return new OCRServiceOptions
            {
                BaseUrl = "https://api.example.com",
                ApiKey = "valid-api-key-123",
                TimeoutSeconds = 30,
                MaxRetryAttempts = 3,
                MinimumConfidenceThreshold = 0.7,
                EnableWakeLock = true,
                WakeLockReason = "Processing document",
                CacheExpirationMinutes = 60
            };
        }

        #endregion
    }
}