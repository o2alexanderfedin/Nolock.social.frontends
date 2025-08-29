using NoLock.Social.Core.Camera.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.Camera.Models;

public class ImageQualityResultTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var result = new ImageQualityResult();

        // Assert
        Assert.Equal(0, result.OverallScore);
        Assert.Equal(0.0, result.BlurScore);
        Assert.Equal(0.0, result.LightingScore);
        Assert.Equal(0.0, result.EdgeDetectionScore);
        Assert.NotNull(result.Issues);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Suggestions);
        Assert.Empty(result.Suggestions);
        Assert.False(result.HasIssues);
        Assert.False(result.IsAcceptable);
    }

    [Theory]
    [InlineData(0, false, "Zero score not acceptable")]
    [InlineData(50, false, "Low score not acceptable")]
    [InlineData(69, false, "Just below threshold not acceptable")]
    [InlineData(70, true, "Exactly at threshold is acceptable")]
    [InlineData(85, true, "Good score is acceptable")]
    [InlineData(100, true, "Perfect score is acceptable")]
    public void IsAcceptable_ReturnsCorrectValue_BasedOnOverallScore(int overallScore, bool expected, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult
        {
            OverallScore = overallScore
        };

        // Act & Assert
        Assert.Equal(expected, result.IsAcceptable);
    }

    [Theory]
    [InlineData(0, false, "No issues")]
    [InlineData(1, true, "One issue")]
    [InlineData(3, true, "Multiple issues")]
    public void HasIssues_ReturnsCorrectValue_BasedOnIssuesCount(int issueCount, bool expected, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();
        for (int i = 0; i < issueCount; i++)
        {
            result.Issues.Add($"Issue {i + 1}");
        }

        // Act & Assert
        Assert.Equal(expected, result.HasIssues);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0, "Minimum scores")]
    [InlineData(0.5, 0.7, 0.6, "Medium scores")]
    [InlineData(1.0, 1.0, 1.0, "Maximum scores")]
    [InlineData(0.8, 0.3, 0.9, "Mixed scores")]
    public void QualityScores_CanBeSetToValidValues(double blurScore, double lightingScore, double edgeScore, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();

        // Act
        result.BlurScore = blurScore;
        result.LightingScore = lightingScore;
        result.EdgeDetectionScore = edgeScore;

        // Assert
        Assert.Equal(blurScore, result.BlurScore);
        Assert.Equal(lightingScore, result.LightingScore);
        Assert.Equal(edgeScore, result.EdgeDetectionScore);
    }

    [Theory]
    [InlineData(0, "Unacceptable quality")]
    [InlineData(25, "Poor quality")]
    [InlineData(50, "Fair quality")]
    [InlineData(70, "Acceptable quality")]
    [InlineData(85, "Good quality")]
    [InlineData(100, "Perfect quality")]
    public void OverallScore_CanBeSetToValidValues(int score, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();

        // Act
        result.OverallScore = score;

        // Assert
        Assert.Equal(score, result.OverallScore);
    }

    [Fact]
    public void Issues_CanBePopulatedWithMultipleIssues()
    {
        // Arrange
        var result = new ImageQualityResult();
        var expectedIssues = new[]
        {
            "Image is too blurry",
            "Poor lighting conditions",
            "Document edges not detected",
            "Resolution too low"
        };

        // Act
        foreach (var issue in expectedIssues)
        {
            result.Issues.Add(issue);
        }

        // Assert
        Assert.Equal(expectedIssues.Length, result.Issues.Count);
        Assert.True(result.HasIssues);
        for (int i = 0; i < expectedIssues.Length; i++)
        {
            Assert.Equal(expectedIssues[i], result.Issues[i]);
        }
    }

    [Fact]
    public void Suggestions_CanBePopulatedWithMultipleSuggestions()
    {
        // Arrange
        var result = new ImageQualityResult();
        var expectedSuggestions = new[]
        {
            "Hold the camera steady",
            "Improve lighting conditions",
            "Move closer to the document",
            "Ensure document is flat"
        };

        // Act
        foreach (var suggestion in expectedSuggestions)
        {
            result.Suggestions.Add(suggestion);
        }

        // Assert
        Assert.Equal(expectedSuggestions.Length, result.Suggestions.Count);
        for (int i = 0; i < expectedSuggestions.Length; i++)
        {
            Assert.Equal(expectedSuggestions[i], result.Suggestions[i]);
        }
    }

    [Fact]
    public void HasIssues_UpdatesCorrectly_WhenIssuesAreModified()
    {
        // Arrange
        var result = new ImageQualityResult();

        // Initially no issues
        Assert.False(result.HasIssues);

        // Add issue
        result.Issues.Add("Blurry image");
        Assert.True(result.HasIssues);

        // Add more issues
        result.Issues.Add("Poor lighting");
        Assert.True(result.HasIssues);

        // Remove one issue
        result.Issues.RemoveAt(0);
        Assert.True(result.HasIssues); // Still has one issue

        // Remove last issue
        result.Issues.Clear();
        Assert.False(result.HasIssues);
    }

    [Theory]
    [InlineData(65, 0.8, 0.9, 0.7, false, "Good scores but overall below threshold")]
    [InlineData(75, 0.3, 0.9, 0.8, true, "Low blur score but overall acceptable")]
    [InlineData(80, 0.9, 0.2, 0.8, true, "Poor lighting but overall acceptable")]
    [InlineData(90, 0.8, 0.9, 0.1, true, "Poor edge detection but overall acceptable")]
    public void QualityAssessment_CombinesAllFactors(int overall, double blur, double lighting, double edge, bool expectedAcceptable, string scenario)
    {
        // Arrange & Act
        var result = new ImageQualityResult
        {
            OverallScore = overall,
            BlurScore = blur,
            LightingScore = lighting,
            EdgeDetectionScore = edge
        };

        // Assert
        Assert.Equal(overall, result.OverallScore);
        Assert.Equal(blur, result.BlurScore);
        Assert.Equal(lighting, result.LightingScore);
        Assert.Equal(edge, result.EdgeDetectionScore);
        Assert.Equal(expectedAcceptable, result.IsAcceptable);
    }

    [Fact]
    public void CompleteQualityResult_CanBeBuilt()
    {
        // Arrange & Act
        var result = new ImageQualityResult
        {
            OverallScore = 85,
            BlurScore = 0.8,
            LightingScore = 0.9,
            EdgeDetectionScore = 0.7
        };

        result.Issues.Add("Minor blur detected");
        result.Issues.Add("Slight shadow on edge");
        
        result.Suggestions.Add("Hold camera more steady");
        result.Suggestions.Add("Adjust lighting angle");

        // Assert
        Assert.Equal(85, result.OverallScore);
        Assert.Equal(0.8, result.BlurScore);
        Assert.Equal(0.9, result.LightingScore);
        Assert.Equal(0.7, result.EdgeDetectionScore);
        Assert.True(result.IsAcceptable);
        Assert.True(result.HasIssues);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal(2, result.Suggestions.Count);
        Assert.Contains("Minor blur detected", result.Issues);
        Assert.Contains("Hold camera more steady", result.Suggestions);
    }

    [Theory]
    [InlineData(-10, "Negative overall score")]
    [InlineData(150, "Over maximum overall score")]
    [InlineData(999, "Very high overall score")]
    public void OverallScore_AcceptsEdgeValues(int score, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();

        // Act
        result.OverallScore = score;

        // Assert
        Assert.Equal(score, result.OverallScore);
        // IsAcceptable only checks >= 70, so negative values will be false
        Assert.Equal(score >= 70, result.IsAcceptable);
    }

    [Theory]
    [InlineData(-0.5, "Negative blur score")]
    [InlineData(1.5, "Over maximum blur score")]
    [InlineData(99.9, "Very high blur score")]
    public void BlurScore_AcceptsEdgeValues(double score, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();

        // Act
        result.BlurScore = score;

        // Assert
        Assert.Equal(score, result.BlurScore);
    }

    [Theory]
    [InlineData(-1.0, "Negative lighting score")]
    [InlineData(2.0, "Over maximum lighting score")]
    [InlineData(100.0, "Very high lighting score")]
    public void LightingScore_AcceptsEdgeValues(double score, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();

        // Act
        result.LightingScore = score;

        // Assert
        Assert.Equal(score, result.LightingScore);
    }

    [Theory]
    [InlineData(-2.0, "Negative edge detection score")]
    [InlineData(5.0, "High edge detection score")]
    [InlineData(0.0001, "Very small positive score")]
    public void EdgeDetectionScore_AcceptsEdgeValues(double score, string scenario)
    {
        // Arrange
        var result = new ImageQualityResult();

        // Act
        result.EdgeDetectionScore = score;

        // Assert
        Assert.Equal(score, result.EdgeDetectionScore);
    }
}