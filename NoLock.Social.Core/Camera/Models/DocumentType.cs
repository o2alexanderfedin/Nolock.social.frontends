namespace NoLock.Social.Core.Camera.Models;

/// <summary>
/// Defines the types of documents that can be scanned
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// Generic document type (default)
    /// </summary>
    Generic = 0,
    
    /// <summary>
    /// Passport document
    /// </summary>
    Passport,
    
    /// <summary>
    /// Driver's license
    /// </summary>
    DriversLicense,
    
    /// <summary>
    /// ID card
    /// </summary>
    IDCard,
    
    /// <summary>
    /// Receipt document
    /// </summary>
    Receipt
}