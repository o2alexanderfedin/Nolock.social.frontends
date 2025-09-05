using System.Linq;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using CoreImageProcessingException = NoLock.Social.Core.Camera.Interfaces.ImageProcessingException;

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
    /// <exception cref="CoreImageProcessingException">Thrown when processing fails due to conversion, storage, or assessment errors</exception>
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
            throw new CoreImageProcessingException("Failed to process captured image", ex);
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
            throw new CoreImageProcessingException("Invalid base64 image data format", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert image data");
            throw new CoreImageProcessingException("Image data conversion failed", ex);
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
    /// <exception cref="CoreImageProcessingException">Thrown when storage operation fails</exception>
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
            throw new CoreImageProcessingException("Image storage operation failed", ex);
        }
    }
    
    /// <summary>
    /// Assesses the quality of a captured image using real computer vision algorithms.
    /// Uses SixLabors.ImageSharp for proper blur detection, lighting analysis, and edge detection.
    /// </summary>
    private ImageQualityResult AssessQuality(CapturedImage capturedImage)
    {
        _logger.LogDebug("Starting quality assessment for captured image using ImageSharp");
        
        var result = new ImageQualityResult();
        
        try
        {
            // Extract image bytes from base64 data
            var imageBytes = ExtractImageBytes(capturedImage.ImageData);
            
            using var image = Image.Load<Rgb24>(imageBytes);
            _logger.LogDebug("Loaded image: {Width}x{Height} pixels", image.Width, image.Height);
            
            // Real blur detection using edge detection variance
            result.BlurScore = CalculateSharpnessScore(image);
            
            // Real lighting assessment using histogram analysis
            result.LightingScore = CalculateLightingScore(image);
            
            // Real edge detection using gradient analysis
            result.EdgeDetectionScore = CalculateEdgeScore(image);
            
            // Use provided quality score if available, otherwise calculate from component scores
            if (capturedImage.Quality > 0)
            {
                result.OverallScore = capturedImage.Quality;
            }
            else
            {
                // Calculate overall score from component scores when no input quality is provided
                result.OverallScore = (int)Math.Round(
                    (result.BlurScore * 40 + result.LightingScore * 30 + result.EdgeDetectionScore * 30) * 100
                );
            }
            
            // Assess quality issues based on actual measurements
            AssessQualityIssues(result);
            
            _logger.LogDebug("Quality assessment completed. OverallScore: {Score}, BlurScore: {Blur:F3}, LightingScore: {Lighting:F3}, EdgeDetectionScore: {Edge:F3}",
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
    /// Extracts raw image bytes from base64 data URL or raw base64 string.
    /// </summary>
    private byte[] ExtractImageBytes(string imageData)
    {
        var (_, base64Data) = ExtractMimeTypeAndData(imageData);
        return Convert.FromBase64String(base64Data);
    }
    
    /// <summary>
    /// Calculates image sharpness using gradient magnitude variance.
    /// Higher variance indicates sharper image (more edges).
    /// Based on pixel intensity differences - simplified but effective method.
    /// </summary>
    private double CalculateSharpnessScore(Image<Rgb24> image)
    {
        try
        {
            // Convert to grayscale for edge detection
            using var grayscale = image.CloneAs<L8>();
            var pixels = new byte[grayscale.Width * grayscale.Height];
            grayscale.CopyPixelDataTo(pixels);
            
            var width = grayscale.Width;
            var height = grayscale.Height;
            var gradientMagnitudes = new List<double>();
            
            // Calculate gradient magnitude using simple difference method
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    var index = y * width + x;
                    var current = pixels[index];
                    
                    // Calculate horizontal and vertical gradients
                    var gx = Math.Abs(pixels[index + 1] - pixels[index - 1]) / 2.0;
                    var gy = Math.Abs(pixels[(y + 1) * width + x] - pixels[(y - 1) * width + x]) / 2.0;
                    
                    // Gradient magnitude
                    var magnitude = Math.Sqrt(gx * gx + gy * gy);
                    gradientMagnitudes.Add(magnitude);
                }
            }
            
            var variance = CalculateVarianceFromDoubles(gradientMagnitudes);
            
            // Normalize variance to 0-1 score (typical range: 0-500 for good images)
            var normalizedScore = Math.Min(1.0, Math.Max(0.0, variance / 300.0));
            
            _logger.LogDebug("Sharpness calculation: variance={Variance:F2}, normalized={Score:F3}", variance, normalizedScore);
            return normalizedScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate sharpness score, returning default");
            return 0.7; // Default reasonable sharpness
        }
    }
    
    /// <summary>
    /// Calculates lighting quality using histogram analysis.
    /// Analyzes brightness distribution to detect over/under-exposure.
    /// </summary>
    private double CalculateLightingScore(Image<Rgb24> image)
    {
        try
        {
            using var grayscale = image.CloneAs<L8>();
            var pixels = new byte[grayscale.Width * grayscale.Height];
            grayscale.CopyPixelDataTo(pixels);
            
            // Calculate histogram
            var histogram = new int[256];
            foreach (var pixel in pixels)
            {
                histogram[pixel]++;
            }
            
            var totalPixels = pixels.Length;
            
            // Check for over-exposure (too many bright pixels)
            var overExposed = histogram.Skip(240).Sum() / (double)totalPixels;
            
            // Check for under-exposure (too many dark pixels)
            var underExposed = histogram.Take(15).Sum() / (double)totalPixels;
            
            // Calculate mean brightness
            var meanBrightness = pixels.Average(p => (double)p);
            
            // Ideal brightness range: 80-180, with good distribution
            var brightnessScore = 1.0 - Math.Abs(meanBrightness - 128) / 128.0;
            brightnessScore = Math.Max(0.0, brightnessScore);
            
            // Penalize over/under-exposure
            var exposureScore = 1.0 - Math.Max(overExposed, underExposed) * 2.0;
            exposureScore = Math.Max(0.0, exposureScore);
            
            var lightingScore = (brightnessScore + exposureScore) / 2.0;
            
            _logger.LogDebug("Lighting analysis: mean={Mean:F1}, overExposed={Over:P1}, underExposed={Under:P1}, score={Score:F3}", 
                meanBrightness, overExposed, underExposed, lightingScore);
            
            return lightingScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate lighting score, returning default");
            return 0.7; // Default reasonable lighting
        }
    }
    
    /// <summary>
    /// Calculates edge detection quality using simple gradient analysis.
    /// Measures the strength and consistency of edges in the image.
    /// </summary>
    private double CalculateEdgeScore(Image<Rgb24> image)
    {
        try
        {
            using var grayscale = image.CloneAs<L8>();
            var pixels = new byte[grayscale.Width * grayscale.Height];
            grayscale.CopyPixelDataTo(pixels);
            
            var width = grayscale.Width;
            var height = grayscale.Height;
            var edgeStrengths = new List<double>();
            
            // Simple edge detection using intensity differences
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    var index = y * width + x;
                    var center = pixels[index];
                    
                    // Check differences with neighbors (simplified Sobel-like approach)
                    var left = pixels[index - 1];
                    var right = pixels[index + 1];
                    var top = pixels[(y - 1) * width + x];
                    var bottom = pixels[(y + 1) * width + x];
                    
                    // Calculate edge strength as maximum intensity difference
                    var horizontalDiff = Math.Abs(right - left);
                    var verticalDiff = Math.Abs(bottom - top);
                    var edgeStrength = Math.Max(horizontalDiff, verticalDiff);
                    
                    edgeStrengths.Add(edgeStrength);
                }
            }
            
            var averageEdgeStrength = edgeStrengths.Average();
            
            // Normalize to 0-1 score (typical range: 0-60 for good document edges)
            var normalizedScore = Math.Min(1.0, Math.Max(0.0, averageEdgeStrength / 40.0));
            
            _logger.LogDebug("Edge detection: average strength={Strength:F2}, normalized={Score:F3}", averageEdgeStrength, normalizedScore);
            return normalizedScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate edge score, returning default");
            return 0.7; // Default reasonable edge score
        }
    }
    
    /// <summary>
    /// Calculates variance of pixel values for sharpness assessment.
    /// Higher variance indicates more detail and sharpness.
    /// </summary>
    private double CalculateVariance(byte[] pixels)
    {
        if (pixels.Length == 0) return 0;
        
        var mean = pixels.Average(p => (double)p);
        var variance = pixels.Select(p => Math.Pow(p - mean, 2)).Average();
        
        return variance;
    }
    
    /// <summary>
    /// Calculates variance of double values for gradient magnitude analysis.
    /// </summary>
    private double CalculateVarianceFromDoubles(List<double> values)
    {
        if (values.Count == 0) return 0;
        
        var mean = values.Average();
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
        
        return variance;
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