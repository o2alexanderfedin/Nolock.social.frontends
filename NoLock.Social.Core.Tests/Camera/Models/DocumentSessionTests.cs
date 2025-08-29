using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera.Models;

public class DocumentSessionTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var session = new DocumentSession();

        // Assert
        Assert.Equal(string.Empty, session.SessionId);
        Assert.NotNull(session.Pages);
        Assert.Empty(session.Pages);
        Assert.True(DateTime.UtcNow.Subtract(session.CreatedAt).TotalSeconds < 1);
        Assert.True(DateTime.UtcNow.Subtract(session.LastActivityAt).TotalSeconds < 1);
        Assert.Null(session.Title);
        Assert.Equal(string.Empty, session.DocumentType);
        Assert.Equal(0, session.CurrentPageIndex);
        Assert.Equal(30, session.TimeoutMinutes);
    }

    [Theory]
    [InlineData(0, "Single page")]
    [InlineData(1, "Two pages")]
    [InlineData(5, "Multiple pages")]
    public void TotalPages_ReturnsCorrectCount(int pageCount, string scenario)
    {
        // Arrange
        var session = new DocumentSession();
        for (int i = 0; i < pageCount; i++)
        {
            session.Pages.Add(new CapturedImage { Id = $"page-{i}" });
        }

        // Act & Assert
        Assert.Equal(pageCount, session.TotalPages);
    }

    [Theory]
    [InlineData(0, false, "No pages")]
    [InlineData(1, true, "One page")]
    [InlineData(3, true, "Multiple pages")]
    public void HasPages_ReturnsCorrectValue(int pageCount, bool expected, string scenario)
    {
        // Arrange
        var session = new DocumentSession();
        for (int i = 0; i < pageCount; i++)
        {
            session.Pages.Add(new CapturedImage { Id = $"page-{i}" });
        }

        // Act & Assert
        Assert.Equal(expected, session.HasPages);
    }

    [Theory]
    [InlineData(0, false, "No pages")]
    [InlineData(1, false, "Single page")]
    [InlineData(2, true, "Two pages")]
    [InlineData(5, true, "Multiple pages")]
    public void IsMultiPage_ReturnsCorrectValue(int pageCount, bool expected, string scenario)
    {
        // Arrange
        var session = new DocumentSession();
        for (int i = 0; i < pageCount; i++)
        {
            session.Pages.Add(new CapturedImage { Id = $"page-{i}" });
        }

        // Act & Assert
        Assert.Equal(expected, session.IsMultiPage);
    }

    [Theory]
    [InlineData(0, 0, "page-0", "First page selected")]
    [InlineData(1, 2, "page-1", "Second page selected from three pages")]
    [InlineData(2, 4, "page-2", "Third page selected from five pages")]
    public void CurrentPage_ReturnsCorrectPage_WhenValidIndex(int index, int totalPages, string expectedId, string scenario)
    {
        // Arrange
        var session = new DocumentSession();
        for (int i = 0; i < totalPages + 1; i++)
        {
            session.Pages.Add(new CapturedImage { Id = $"page-{i}" });
        }
        session.CurrentPageIndex = index;

        // Act
        var currentPage = session.CurrentPage;

        // Assert
        Assert.NotNull(currentPage);
        Assert.Equal(expectedId, currentPage.Id);
    }

    [Theory]
    [InlineData(-1, 3, "Negative index with pages")]
    [InlineData(3, 3, "Index equal to page count")]
    [InlineData(5, 2, "Index greater than page count")]
    [InlineData(0, 0, "Valid index with no pages")]
    public void CurrentPage_ReturnsNull_WhenInvalidIndex(int index, int pageCount, string scenario)
    {
        // Arrange
        var session = new DocumentSession();
        for (int i = 0; i < pageCount; i++)
        {
            session.Pages.Add(new CapturedImage { Id = $"page-{i}" });
        }
        session.CurrentPageIndex = index;

        // Act
        var currentPage = session.CurrentPage;

        // Assert
        Assert.Null(currentPage);
    }

    [Theory]
    [InlineData(0, false, "No timeout")]
    [InlineData(29, false, "Just before timeout")]
    [InlineData(30, true, "Exactly at timeout")]
    [InlineData(31, true, "Past timeout")]
    [InlineData(60, true, "Well past timeout")]
    public void IsExpired_ReturnsCorrectValue_BasedOnLastActivity(int minutesAgo, bool expectedExpired, string scenario)
    {
        // Arrange
        var session = new DocumentSession
        {
            TimeoutMinutes = 30,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
        };

        // Act
        var isExpired = session.IsExpired;

        // Assert
        Assert.Equal(expectedExpired, isExpired);
    }

    [Theory]
    [InlineData(15, 14, false, "Custom timeout not exceeded")]
    [InlineData(15, 15, true, "Custom timeout exactly reached")]
    [InlineData(15, 20, true, "Custom timeout exceeded")]
    [InlineData(60, 45, false, "Longer timeout not exceeded")]
    [InlineData(1, 2, true, "Very short timeout exceeded")]
    public void IsExpired_RespectsCustomTimeout(int timeoutMinutes, int minutesAgo, bool expectedExpired, string scenario)
    {
        // Arrange
        var session = new DocumentSession
        {
            TimeoutMinutes = timeoutMinutes,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
        };

        // Act
        var isExpired = session.IsExpired;

        // Assert
        Assert.Equal(expectedExpired, isExpired);
    }

    [Fact]
    public void UpdateActivity_UpdatesLastActivityToCurrentTime()
    {
        // Arrange
        var session = new DocumentSession
        {
            LastActivityAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var beforeUpdate = DateTime.UtcNow.AddSeconds(-1);

        // Act
        session.UpdateActivity();

        // Assert
        var afterUpdate = DateTime.UtcNow.AddSeconds(1);
        Assert.True(session.LastActivityAt > beforeUpdate);
        Assert.True(session.LastActivityAt < afterUpdate);
    }

    [Fact]
    public void UpdateActivity_ResetsExpirationStatus()
    {
        // Arrange
        var session = new DocumentSession
        {
            TimeoutMinutes = 30,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-35) // Expired
        };
        Assert.True(session.IsExpired); // Verify it's expired

        // Act
        session.UpdateActivity();

        // Assert
        Assert.False(session.IsExpired);
    }

    [Theory]
    [InlineData("invoice-123", "Invoice document")]
    [InlineData("", "Empty session ID")]
    [InlineData("multi-page-scan-2023", "Multi-page scan session")]
    public void SessionId_CanBeSetToValidValues(string sessionId, string scenario)
    {
        // Arrange
        var session = new DocumentSession();

        // Act
        session.SessionId = sessionId;

        // Assert
        Assert.Equal(sessionId, session.SessionId);
    }

    [Theory]
    [InlineData("Invoice #12345", "Invoice title")]
    [InlineData("", "Empty title")]
    [InlineData(null, "Null title")]
    [InlineData("Meeting Notes - Q4 2023", "Meeting notes title")]
    public void Title_CanBeSetToValidValues(string? title, string scenario)
    {
        // Arrange
        var session = new DocumentSession();

        // Act
        session.Title = title;

        // Assert
        Assert.Equal(title, session.Title);
    }

    [Theory]
    [InlineData("invoice", "Invoice document type")]
    [InlineData("receipt", "Receipt document type")]
    [InlineData("contract", "Contract document type")]
    [InlineData("", "Empty document type")]
    public void DocumentType_CanBeSetToValidValues(string documentType, string scenario)
    {
        // Arrange
        var session = new DocumentSession();

        // Act
        session.DocumentType = documentType;

        // Assert
        Assert.Equal(documentType, session.DocumentType);
    }

    [Fact]
    public void PageManagement_WorksCorrectly()
    {
        // Arrange
        var session = new DocumentSession();
        var page1 = new CapturedImage { Id = "page-1" };
        var page2 = new CapturedImage { Id = "page-2" };
        var page3 = new CapturedImage { Id = "page-3" };

        // Act - Add pages
        session.Pages.Add(page1);
        Assert.Equal(1, session.TotalPages);
        Assert.True(session.HasPages);
        Assert.False(session.IsMultiPage);
        Assert.Equal(page1, session.CurrentPage);

        session.Pages.Add(page2);
        Assert.Equal(2, session.TotalPages);
        Assert.True(session.IsMultiPage);

        session.Pages.Add(page3);
        Assert.Equal(3, session.TotalPages);

        // Act - Navigate pages
        session.CurrentPageIndex = 1;
        Assert.Equal(page2, session.CurrentPage);

        session.CurrentPageIndex = 2;
        Assert.Equal(page3, session.CurrentPage);

        // Act - Remove page
        session.Pages.RemoveAt(1); // Remove page2
        Assert.Equal(2, session.TotalPages);
        // CurrentPageIndex is now out of bounds
        Assert.Null(session.CurrentPage);

        // Fix index
        session.CurrentPageIndex = 1;
        Assert.Equal(page3, session.CurrentPage);
    }

    [Fact]
    public void AllComputedProperties_UpdateCorrectly_WhenPagesChange()
    {
        // Arrange
        var session = new DocumentSession();

        // Initially empty
        Assert.Equal(0, session.TotalPages);
        Assert.False(session.HasPages);
        Assert.False(session.IsMultiPage);
        Assert.Null(session.CurrentPage);

        // Add first page
        session.Pages.Add(new CapturedImage { Id = "page-1" });
        Assert.Equal(1, session.TotalPages);
        Assert.True(session.HasPages);
        Assert.False(session.IsMultiPage);
        Assert.NotNull(session.CurrentPage);

        // Add second page
        session.Pages.Add(new CapturedImage { Id = "page-2" });
        Assert.Equal(2, session.TotalPages);
        Assert.True(session.HasPages);
        Assert.True(session.IsMultiPage);

        // Clear all pages
        session.Pages.Clear();
        Assert.Equal(0, session.TotalPages);
        Assert.False(session.HasPages);
        Assert.False(session.IsMultiPage);
        Assert.Null(session.CurrentPage);
    }
}