using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.Camera.Services;

/// <summary>
/// Service implementation for processing captured camera images.
/// Follows Single Responsibility Principle by handling image conversion, storage, and quality assessment.
/// Dependencies injected through constructor following Dependency Inversion Principle.
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;
    private readonly IContentAddressableStorage<ContentData<byte[]>> _storage;

    /// <summary>
    /// Initializes a new instance of ImageProcessingService with required dependencies.
    /// </summary>
    /// <param name="logger">Logger for tracking processing operations and errors</param>
    /// <param name="storage">Content-addressable storage for persisting processed images</param>
    public ImageProcessingService(
        ILogger<ImageProcessingService> logger,
        IContentAddressableStorage<ContentData<byte[]>> storage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Processes a captured image through the complete workflow: conversion, storage, and quality assessment.
    /// </summary>
    /// <param name="capturedImage">The image captured from the camera component</param>
    /// <returns>Result containing the content hash and quality assessment</returns>
    /// <exception cref="ImageProcessingException">Thrown when processing fails due to conversion, storage, or assessment errors</exception>
    public async Task<ImageProcessingResult> ProcessAsync(CapturedImage capturedImage)
    {
        _logger.LogInformation("Starting image processing workflow");
        
        try
        {
            // Convert captured image data to storage format
            _logger.LogDebug("Converting image data for storage");
            var contentData = ConvertImageData(capturedImage);
            _logger.LogDebug("Image data converted successfully. MimeType: {MimeType}, DataLength: {DataLength} bytes", 
                contentData.MimeType, contentData.Data?.Length ?? 0);
            
            // Store the image in content-addressable storage
            _logger.LogDebug("Storing image in content-addressable storage");
            var contentHash = await StoreImage(contentData);
            _logger.LogDebug("Image stored successfully with hash: {Hash}", contentHash);
            
            // Assess image quality
            _logger.LogDebug("Assessing image quality");
            var qualityResult = AssessQuality(capturedImage);
            _logger.LogDebug("Quality assessment completed. Overall score: {Score}", qualityResult.OverallScore);
            
            var result = new ImageProcessingResult
            {
                ContentHash = contentHash,
                QualityResult = qualityResult
            };
            
            _logger.LogInformation("Image processing workflow completed successfully. Hash: {Hash}", contentHash);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image processing workflow failed");
            throw new ImageProcessingException("Failed to process captured image", ex);
        }
    }
    
    /// <summary>
    /// Converts captured image data to content data format for storage.
    /// Extracts MIME type and converts base64 data URL to byte array following DRY principle.
    /// </summary>
    private ContentData<byte[]> ConvertImageData(CapturedImage capturedImage)
    {
        _logger.LogDebug("Converting image data - Input length: {Length} chars", capturedImage.ImageData.Length);
        
        try
        {
            // Extract MIME type and base64 data from data URL (KISS: simple extraction logic)
            var (mimeType, base64Data) = ExtractMimeTypeAndData(capturedImage.ImageData);
            _logger.LogDebug("Extracted MimeType: {MimeType}, Base64 length: {Length} chars", mimeType, base64Data.Length);
            
            // Convert base64 to byte array with proper error handling
            var bytes = Convert.FromBase64String(base64Data);
            _logger.LogDebug("Converted to {ByteCount} bytes", bytes.Length);
            
            // Create ContentData object (SOLID: Single responsibility for data structure)
            var result = new ContentData<byte[]>(bytes, mimeType);
            
            _logger.LogDebug("Created ContentData with MimeType={MimeType}, DataLength={DataLength}", 
                result.MimeType, result.Data.Length);
            
            return result;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid base64 format in image data");
            throw new ImageProcessingException("Invalid base64 image data format", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert image data");
            throw new ImageProcessingException("Image data conversion failed", ex);
        }
    }
    
    /// <summary>
    /// Extracts MIME type and base64 data from data URL format.
    /// Handles both full data URLs and fallback for raw base64 (DRY: reusable extraction logic).
    /// </summary>
    private (string mimeType, string base64Data) ExtractMimeTypeAndData(string dataUrl)
    {
        // Handle data URL format: "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEA..."
        if (dataUrl.StartsWith("data:"))
        {
            var parts = dataUrl.Split(',');
            if (parts.Length == 2)
            {
                // Extract MIME type from header part
                var header = parts[0]; // "data:image/jpeg;base64"
                var mimeType = header.Substring(5); // Remove "data:"
                
                // Remove encoding specification if present (KISS: simple string manipulation)
                if (mimeType.Contains(";"))
                {
                    mimeType = mimeType.Split(';')[0];
                }
                
                return (mimeType, parts[1]);
            }
        }
        
        // Fallback for raw base64 data without data URL prefix
        _logger.LogWarning("No data URL prefix found, assuming JPEG format");
        return ("image/jpeg", dataUrl);
    }
    
    /// <summary>
    /// Stores image content data in content-addressable storage.
    /// Uses dependency-injected storage service following KISS principle.
    /// </summary>
    /// <param name="contentData">The image content data to store</param>
    /// <returns>Content hash identifying the stored image</returns>
    /// <exception cref="ImageProcessingException">Thrown when storage operation fails</exception>
    private async Task<string> StoreImage(ContentData<byte[]> contentData)
    {
        try
        {
            _logger.LogDebug("Storing image data in content-addressable storage. DataSize: {Size} bytes, MimeType: {MimeType}", 
                contentData.Data?.Length ?? 0, contentData.MimeType);
            
            // Use storage service to persist the image and get content hash (KISS: direct storage call)
            var contentHash = await _storage.StoreAsync(contentData);
            
            _logger.LogDebug("Successfully stored image with hash: {Hash}", contentHash);
            return contentHash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store image in content-addressable storage");
            throw new ImageProcessingException("Image storage operation failed", ex);
        }
    }
    
    /// <summary>
    /// Assesses the quality of a captured image using basic metrics.
    /// Applies KISS principle with simple quality calculation based on available data.
    /// YAGNI: Uses basic assessment for now, can be enhanced with computer vision later.
    /// </summary>
    private ImageQualityResult AssessQuality(CapturedImage capturedImage)
    {
        _logger.LogDebug("Starting quality assessment for captured image");
        
        var result = new ImageQualityResult();
        
        try
        {
            // Use component's quality score if available, otherwise calculate basic score
            var baseScore = capturedImage.Quality > 0 ? capturedImage.Quality : 80;
            
            // Apply KISS: Basic quality assessment using available data
            result.OverallScore = Math.Max(0, Math.Min(100, baseScore));
            
            // Basic blur assessment (YAGNI: simple heuristic for now)
            // Larger images tend to have better quality for document capture
            var imageSizeScore = Math.Min(1.0, capturedImage.ImageData.Length / 100000.0);
            result.BlurScore = Math.Max(0.5, Math.Min(1.0, 0.7 + imageSizeScore * 0.3));
            
            // Basic lighting assessment (KISS: reasonable default with minor variation)
            result.LightingScore = Math.Max(0.6, Math.Min(1.0, 0.8 + (result.OverallScore - 80) * 0.002));
            
            // Basic edge detection score (YAGNI: simple calculation for now)
            result.EdgeDetectionScore = Math.Max(0.5, Math.Min(1.0, result.BlurScore * 0.9));
            
            // Assess quality issues based on scores (DRY: centralized issue detection)
            AssessQualityIssues(result);
            
            _logger.LogDebug("Quality assessment completed. OverallScore: {Score}, BlurScore: {Blur}, LightingScore: {Lighting}, EdgeDetectionScore: {Edge}",
                result.OverallScore, result.BlurScore, result.LightingScore, result.EdgeDetectionScore);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quality assessment, returning default values");
            
            // Fallback quality result (KISS: safe defaults)
            return new ImageQualityResult
            {
                OverallScore = 75,
                BlurScore = 0.7,
                LightingScore = 0.7,
                EdgeDetectionScore = 0.7,
                Issues = { "Quality assessment failed, using default values" }
            };
        }
    }
    
    /// <summary>
    /// Assesses quality issues and provides suggestions based on quality scores.
    /// DRY principle: Centralized issue detection logic.
    /// </summary>
    private void AssessQualityIssues(ImageQualityResult result)
    {
        // Check for blur issues
        if (result.BlurScore < 0.6)
        {
            result.Issues.Add("Image appears blurry or out of focus");
            result.Suggestions.Add("Hold the camera steady and ensure the document is in focus");
        }
        
        // Check for lighting issues
        if (result.LightingScore < 0.6)
        {
            result.Issues.Add("Poor lighting conditions detected");
            result.Suggestions.Add("Ensure adequate lighting on the document");
        }
        
        // Check for edge detection issues
        if (result.EdgeDetectionScore < 0.6)
        {
            result.Issues.Add("Document edges may not be clearly visible");
            result.Suggestions.Add("Position the document fully within the camera frame");
        }
        
        // Overall quality check
        if (result.OverallScore < 70)
        {
            result.Issues.Add("Overall image quality is below recommended threshold");
            result.Suggestions.Add("Consider retaking the photo for better results");
        }
        
        _logger.LogDebug("Quality issues assessment: {IssueCount} issues found", result.Issues.Count);
    }
}