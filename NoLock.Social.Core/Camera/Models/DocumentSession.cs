namespace NoLock.Social.Core.Camera.Models;

/// <summary>
/// Represents a multi-page document capture session
/// </summary>
public class DocumentSession
{
    /// <summary>
    /// Unique identifier for the session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of captured pages
    /// </summary>
    public List<CapturedImage> Pages { get; set; } = new();

    /// <summary>
    /// Session creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last activity timestamp for session timeout tracking
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional document title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Type of document that this session is capturing
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Total number of pages in the session
    /// </summary>
    public int TotalPages => Pages.Count;

    /// <summary>
    /// Currently selected page index (0-based)
    /// </summary>
    public int CurrentPageIndex { get; set; } = 0;

    /// <summary>
    /// Gets the currently selected page, if any
    /// </summary>
    public CapturedImage? CurrentPage => 
        CurrentPageIndex >= 0 && CurrentPageIndex < Pages.Count 
            ? Pages[CurrentPageIndex] 
            : null;

    /// <summary>
    /// Indicates whether the session has any pages
    /// </summary>
    public bool HasPages => Pages.Any();

    /// <summary>
    /// Indicates whether there are multiple pages in the session
    /// </summary>
    public bool IsMultiPage => Pages.Count > 1;
    
    /// <summary>
    /// Session timeout duration in minutes (default 30 minutes)
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;
    
    /// <summary>
    /// Checks if the session has expired based on last activity
    /// </summary>
    public bool IsExpired => DateTime.UtcNow.Subtract(LastActivityAt).TotalMinutes > TimeoutMinutes;
    
    /// <summary>
    /// Updates the last activity timestamp to current time
    /// </summary>
    public void UpdateActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }
}