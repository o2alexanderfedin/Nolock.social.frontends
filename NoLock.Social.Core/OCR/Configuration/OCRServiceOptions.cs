using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.OCR.Configuration
{
    /// <summary>
    /// Options for OCR service configuration following IOptions pattern
    /// </summary>
    public class OCRServiceOptions : OCRServiceConfiguration
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "OCRService";

        /// <summary>
        /// Polling configuration for status checking operations.
        /// If null, defaults to PollingConfiguration.OCRDefault.
        /// </summary>
        public PollingConfiguration PollingConfiguration { get; set; }

        /// <summary>
        /// Minimum confidence threshold for automatic document type detection.
        /// Below this threshold, manual selection will be recommended.
        /// Default is 0.7 (70% confidence).
        /// </summary>
        public double MinimumConfidenceThreshold { get; set; } = 0.7;

        /// <summary>
        /// Whether to enable caching of OCR results.
        /// Default is true.
        /// </summary>
        public bool? EnableCaching { get; set; }

        /// <summary>
        /// Cache expiration time in minutes.
        /// Default is 60 minutes.
        /// </summary>
        public int? CacheExpirationMinutes { get; set; }

        /// <summary>
        /// Whether to cache only completed OCR results.
        /// Default is true.
        /// </summary>
        public bool? CacheOnlyCompleteResults { get; set; }

        /// <summary>
        /// Whether to enable Wake Lock functionality during OCR processing operations.
        /// When enabled, prevents the device from entering sleep mode while processing documents.
        /// Default is true for better user experience.
        /// </summary>
        public bool EnableWakeLock { get; set; } = true;

        /// <summary>
        /// Custom reason text displayed to users explaining why Wake Lock is active.
        /// Should provide clear context about the ongoing OCR operation.
        /// Default is "Processing document - preventing device sleep".
        /// </summary>
        public string WakeLockReason { get; set; } = "Processing document - preventing device sleep";

        /// <summary>
        /// Validates the configuration settings
        /// </summary>
        /// <returns>Validation results containing any configuration errors</returns>
        public IValidationResult Validate()
        {
            var validationResult = new ValidationResult();

            // Validate BaseUrl
            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                validationResult.AddError("BaseUrl is required for OCR service.");
            }
            else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || 
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                validationResult.AddError("BaseUrl must be a valid HTTP or HTTPS URL.");
            }

            // Validate ApiKey
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                validationResult.AddError("ApiKey is required for OCR service authentication.");
            }

            // Validate TimeoutSeconds
            if (TimeoutSeconds <= 0)
            {
                validationResult.AddError("TimeoutSeconds must be greater than 0.");
            }
            else if (TimeoutSeconds > 300)
            {
                validationResult.AddWarning("TimeoutSeconds is set to a high value (>300 seconds).");
            }

            // Validate MaxRetryAttempts
            if (MaxRetryAttempts < 0)
            {
                validationResult.AddError("MaxRetryAttempts cannot be negative.");
            }
            else if (MaxRetryAttempts > 10)
            {
                validationResult.AddWarning("MaxRetryAttempts is set to a high value (>10).");
            }

            // Validate MinimumConfidenceThreshold
            if (MinimumConfidenceThreshold < 0.0 || MinimumConfidenceThreshold > 1.0)
            {
                validationResult.AddError("MinimumConfidenceThreshold must be between 0.0 and 1.0.");
            }
            else if (MinimumConfidenceThreshold < 0.5)
            {
                validationResult.AddWarning("MinimumConfidenceThreshold is set to a low value (<0.5).");
            }

            // Validate CacheExpirationMinutes if provided
            if (CacheExpirationMinutes.HasValue)
            {
                if (CacheExpirationMinutes.Value <= 0)
                {
                    validationResult.AddError("CacheExpirationMinutes must be greater than 0.");
                }
                else if (CacheExpirationMinutes.Value > 1440) // 24 hours
                {
                    validationResult.AddWarning("CacheExpirationMinutes is set to a high value (>24 hours).");
                }
            }

            // Validate PollingConfiguration if provided
            if (PollingConfiguration != null)
            {
                try
                {
                    PollingConfiguration.Validate();
                }
                catch (ArgumentException ex)
                {
                    validationResult.AddError($"Polling configuration error: {ex.Message}");
                }
            }

            // Validate WakeLockReason
            if (EnableWakeLock && string.IsNullOrWhiteSpace(WakeLockReason))
            {
                validationResult.AddError("WakeLockReason cannot be empty when EnableWakeLock is true.");
            }
            else if (EnableWakeLock && !string.IsNullOrEmpty(WakeLockReason) && WakeLockReason.Length > 200)
            {
                validationResult.AddWarning("WakeLockReason is quite long (>200 characters). Consider a shorter, clearer message.");
            }

            return validationResult;
        }

        /// <summary>
        /// Interface for validation results
        /// </summary>
        public interface IValidationResult
        {
            bool IsValid { get; }
            string[] Errors { get; }
            string[] Warnings { get; }
        }

        /// <summary>
        /// Implementation of validation results
        /// </summary>
        private class ValidationResult : IValidationResult
        {
            private readonly List<string> _errors = new();
            private readonly List<string> _warnings = new();

            public bool IsValid => _errors.Count == 0;
            public string[] Errors => _errors.ToArray();
            public string[] Warnings => _warnings.ToArray();

            public void AddError(string error)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _errors.Add(error);
                }
            }

            public void AddWarning(string warning)
            {
                if (!string.IsNullOrWhiteSpace(warning))
                {
                    _warnings.Add(warning);
                }
            }
        }
    }
}