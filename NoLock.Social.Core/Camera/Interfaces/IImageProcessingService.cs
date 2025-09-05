using NoLock.Social.Core.Camera.Models;

namespace NoLock.Social.Core.Camera.Interfaces;

/// <summary>
/// Service responsible for processing captured camera images for document capture workflows.
/// Follows Single Responsibility Principle by focusing solely on image processing logic.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Processes a captured image through the complete workflow: conversion, storage, and quality assessment.
    /// </summary>
    /// <param name="capturedImage">The image captured from the camera component</param>
    /// <returns>Result containing the content hash and quality assessment</returns>
    /// <exception cref="ImageProcessingException">Thrown when processing fails due to conversion, storage, or assessment errors</exception>
    Task<ImageProcessingResult> ProcessAsync(CapturedImage capturedImage);
}

/// <summary>
/// Result of image processing operation containing all necessary outputs.
/// Immutable record following functional programming principles.
/// </summary>
public record ImageProcessingResult
{
    /// <summary>
    /// Content-addressable hash of the stored image
    /// </summary>
    public required string ContentHash { get; init; }
    
    /// <summary>
    /// Quality assessment result for the processed image
    /// </summary>
    public required ImageQualityResult QualityResult { get; init; }
}

/// <summary>
/// Specific exception for image processing operations.
/// Allows calling code to distinguish processing errors from other system errors.
/// </summary>
public class ImageProcessingException : Exception
{
    public ImageProcessingException(string message) : base(message) { }
    public ImageProcessingException(string message, Exception innerException) : base(message, innerException) { }
}