using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoLock.Social.Components.Camera;
using NoLock.Social.Core.Camera.Models;
using BlazorPro.BlazorSize;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    /// <summary>
    /// Tests for FilmStrip component's single-click behavior and fullscreen functionality.
    /// Tests validate that single clicks on thumbnails open the fullscreen viewer.
    /// </summary>
    public class FilmStripClickTests : TestContext
    {
        private readonly Mock<IResizeListener> _mockResizeListener;
        private readonly List<CapturedImage> _testImages;

        public FilmStripClickTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            Services.AddSingleton(_mockResizeListener.Object);
            
            _testImages = new List<CapturedImage>
            {
                new() { 
                    Id = "1", 
                    ImageUrl = "data:image/png;base64,test1", 
                    Timestamp = DateTime.UtcNow,
                    Width = 1920,
                    Height = 1080,
                    Quality = 95
                },
                new() { 
                    Id = "2", 
                    ImageUrl = "data:image/png;base64,test2", 
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    Width = 1280,
                    Height = 720,
                    Quality = 90
                },
                new() { 
                    Id = "3", 
                    ImageUrl = "data:image/png;base64,test3", 
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    Width = 3840,
                    Height = 2160,
                    Quality = 100
                }
            };
        }

        [Theory]
        [InlineData(1, "Single image click")]
        [InlineData(3, "Multiple images click")]
        [InlineData(10, "Many images click")]
        public void SingleClick_OnThumbnail_OpensFullscreenViewer(int imageCount, string scenario)
        {
            // Arrange
            var images = Enumerable.Range(1, imageCount)
                .Select(i => new CapturedImage 
                { 
                    Id = i.ToString(), 
                    ImageUrl = $"data:image/png;base64,test{i}",
                    Timestamp = DateTime.UtcNow.AddMinutes(-i)
                })
                .ToList();

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images));

            // Act - Single click on first thumbnail
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();

            // Assert - Fullscreen viewer should be visible
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull($"Fullscreen viewer should open on single click for scenario: {scenario}");
            
            var displayedImage = fullscreenViewer.QuerySelector("img");
            displayedImage.Should().NotBeNull("Image should be displayed in fullscreen viewer");
            displayedImage?.GetAttribute("src").Should().Contain("test1", "First image should be displayed");
        }

        [Fact]
        public void SingleClick_WithSelectionMode_StillOpensFullscreen()
        {
            // Arrange - Component with selection handlers (checkbox mode)
            var selectedImages = new HashSet<string>();
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    if (selectedImages.Contains(img.Id))
                        selectedImages.Remove(img.Id);
                    else
                        selectedImages.Add(img.Id);
                })));

            // Act - Click on thumbnail (not checkbox)
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();

            // Assert - Should open fullscreen, not toggle selection
            var fullscreenViewer = component.FindAll(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeEmpty("Single click should open fullscreen even with selection mode enabled");
            
            // Selection should not change from clicking the image
            selectedImages.Should().BeEmpty("Clicking image should not toggle selection when checkbox is available");
        }

        [Fact]
        public void SingleClick_OnDifferentImages_ShowsCorrectImage()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Click second thumbnail
            var thumbnails = component.FindAll(".film-thumbnail");
            thumbnails[1].Click();

            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            var displayedImage = fullscreenViewer.QuerySelector("img");
            displayedImage.Should().NotBeNull("Image should be displayed in fullscreen viewer");
            displayedImage?.GetAttribute("src").Should().Contain("test2", "Second image should be displayed");
        }

        [Fact]
        public void SingleClick_ThenEscape_ClosesFullscreen()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Click to open
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();
            
            // Verify opened
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Fullscreen should be open");

            // Act - Press Escape
            fullscreenViewer.KeyDown(new KeyboardEventArgs { Key = "Escape" });

            // Assert - Should be closed
            var closedViewer = component.FindAll(".fullscreen-backdrop");
            closedViewer.Should().BeEmpty("Fullscreen viewer should close on Escape");
        }

        [Fact]
        public void SingleClick_ThenClickBackdrop_ClosesFullscreen()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Click to open
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();

            // Act - Click backdrop
            var backdrop = component.Find(".fullscreen-backdrop");
            backdrop.Click();

            // Assert - Should be closed
            var closedViewer = component.FindAll(".fullscreen-backdrop");
            closedViewer.Should().BeEmpty("Fullscreen viewer should close when backdrop is clicked");
        }

        [Fact]
        public void SingleClick_WithoutSelectionHandlers_OpensFullscreen()
        {
            // Arrange - Simple component without selection handlers
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();

            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Single click should always open fullscreen when no selection handlers");
        }

        [Theory]
        [InlineData("Enter", "Enter key should trigger image click")]
        [InlineData(" ", "Space key should trigger image click")]
        public void KeyboardNavigation_TriggersImageClick(string key, string scenario)
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Simulate keyboard press
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.KeyDown(new KeyboardEventArgs { Key = key });

            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull($"Fullscreen should open for scenario: {scenario}");
        }

        [Fact]
        public void RapidClicks_HandledCorrectly()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Rapid single clicks
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();
            thumbnail.Click(); // Second click while fullscreen is open
            
            // Assert - Should still have fullscreen open (not toggle)
            var fullscreenViewer = component.FindAll(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeEmpty("Rapid clicks should not close fullscreen");
        }

        [Fact(Skip = "FullscreenImageViewer component doesn't currently display image metadata")]
        public void FullscreenViewer_DisplaysImageDetails()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Click to open fullscreen
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();

            // Assert - Check for image details
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            var imageDetails = fullscreenViewer.TextContent;
            
            // Check for expected details (dimensions, timestamp, etc.)
            // NOTE: This functionality doesn't exist in the current FullscreenImageViewer component
            imageDetails.Should().Contain("1920", "Width should be displayed");
            imageDetails.Should().Contain("1080", "Height should be displayed");
            imageDetails.Should().Contain("95", "Quality should be displayed");
        }

        [Fact]
        public void Checkbox_Click_TogglesSelection_NotFullscreen()
        {
            // Arrange
            var selectedImages = new HashSet<string>();
            var selectionToggled = false;
            
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionToggled = true;
                    if (selectedImages.Contains(img.Id))
                        selectedImages.Remove(img.Id);
                    else
                        selectedImages.Add(img.Id);
                })));

            // Act - Click the checkbox (not the image)
            var checkbox = component.Find(".selection-checkbox");
            checkbox.Click();

            // Assert
            selectionToggled.Should().BeTrue("Checkbox click should toggle selection");
            selectedImages.Should().Contain("1", "First image should be selected");
            
            // Fullscreen should NOT open
            var fullscreenViewer = component.FindAll(".fullscreen-backdrop");
            fullscreenViewer.Should().BeEmpty("Checkbox click should not open fullscreen");
        }

        #region Future Double-Click Enhancement Tests (Currently Skipped)
        
        /// <summary>
        /// Future enhancement: Double-click behavior specification tests.
        /// These tests define expected behavior when double-click functionality is implemented.
        /// </summary>
        
        [Fact]
        public void DoubleClick_OnThumbnail_OpensFullscreenDirectly()
        {
            // SPECIFICATION: Double-click should open fullscreen viewer immediately
            // without requiring two single clicks
            
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Simulate double-click
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.DoubleClick();
            
            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Double-click should open fullscreen viewer");
        }

        [Fact]
        public void DoubleClick_WithSelectionMode_OpensFullscreenNotSelection()
        {
            // SPECIFICATION: Double-click should always open fullscreen,
            // even when selection mode is enabled
            
            // Arrange
            var selectedImages = new HashSet<string>();
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    if (selectedImages.Contains(img.Id))
                        selectedImages.Remove(img.Id);
                    else
                        selectedImages.Add(img.Id);
                })));

            // Act - Double-click thumbnail
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.DoubleClick();
            
            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Double-click should open fullscreen");
            selectedImages.Should().BeEmpty("Double-click should not toggle selection");
        }

        [Theory(Skip = "Double-click timing simulation not available in bUnit - functionality verified by actual double-click tests")]
        [InlineData(300, "Standard double-click timing")]
        [InlineData(500, "Slow double-click timing")]
        [InlineData(100, "Fast double-click timing")]
        public void DoubleClick_WithVariousTimings_RecognizedCorrectly(int intervalMs, string scenario)
        {
            // SPECIFICATION: Double-click should be recognized within reasonable timing intervals
            
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Two clicks with specified interval
            var thumbnail = component.Find(".film-thumbnail");
            // TODO: Implement timed double-click simulation
            
            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull($"Double-click should be recognized for {scenario}");
        }

        [Fact]
        public void DoubleTap_OnMobile_OpensFullscreenViewer()
        {
            // SPECIFICATION: Double-tap on mobile should behave like double-click
            
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Simulate double-tap (same as double-click in DOM events)
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.DoubleClick();
            
            // Assert
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Double-tap should open fullscreen viewer on mobile");
        }

        [Fact]
        public void SingleClick_ThenDelayedClick_TreatedAsSeparateClicks()
        {
            // SPECIFICATION: Clicks separated by more than double-click threshold
            // should be treated as separate single clicks
            
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Act - Two single clicks (not double-click)
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click(); // First click opens fullscreen
            // Second single click when fullscreen is already open
            thumbnail.Click(); // Should be handled gracefully
            
            // Assert
            // Should still have fullscreen open (not toggled by second click)
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Delayed second click should not affect already opened fullscreen");
        }
        
        #endregion
    }
}