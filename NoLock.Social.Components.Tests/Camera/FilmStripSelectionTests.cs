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
    /// Comprehensive tests for FilmStrip (digital filmstrip) selection enhancement.
    /// Tests both backward compatibility and new selection functionality.
    /// The FilmStrip component displays captured images as a digital filmstrip.
    /// </summary>
    public class FilmStripSelectionTests : TestContext
    {
        private readonly List<CapturedImage> _testImages;
        private readonly Mock<IResizeListener> _mockResizeListener;

        public FilmStripSelectionTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            Services.AddSingleton(_mockResizeListener.Object);
            
            _testImages = new List<CapturedImage>
            {
                new() { Id = "1", ImageUrl = "data:image/png;base64,test1", Timestamp = DateTime.UtcNow },
                new() { Id = "2", ImageUrl = "data:image/png;base64,test2", Timestamp = DateTime.UtcNow },
                new() { Id = "3", ImageUrl = "data:image/png;base64,test3", Timestamp = DateTime.UtcNow }
            };
        }

        [Fact]
        public void FilmStrip_WithoutSelectionParameters_ShowsFullscreen()
        {
            // Arrange
            var images = _testImages.Take(1);

            // Act - No selection parameters provided (backward compatibility)
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images));

            // Assert - Should not show selection indicator without selection parameters
            var selectionIndicators = component.FindAll(".selection-indicator");
            selectionIndicators.Should().BeEmpty("No selection indicator should appear without selection parameters");

            // Should not have selected CSS class
            var cardElement = component.Find(".card");
            var cardClasses = cardElement.GetAttribute("class");
            cardClasses.Should().NotContain("selected", "Card should not have selected class without selection parameters");
        }

        [Fact]
        public void FilmStrip_WithSelectionParameters_EnablesSelection()
        {
            // Arrange
            var images = _testImages.Take(1);
            var selectedImages = new HashSet<string>();
            var selectionToggled = false;
            CapturedImage? toggledImage = null;

            // Act - With selection parameters provided
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionToggled = true;
                    toggledImage = img;
                    if (selectedImages.Contains(img.Id))
                        selectedImages.Remove(img.Id);
                    else
                        selectedImages.Add(img.Id);
                })));

            // Trigger click event
            var imageElement = component.Find(".film-thumbnail");
            imageElement.Click();

            // Assert - Should trigger selection, not fullscreen
            selectionToggled.Should().BeTrue("Selection callback should be invoked");
            toggledImage.Should().NotBeNull("Toggled image should be provided to callback");
            toggledImage!.Id.Should().Be("1", "Correct image should be passed to callback");
            selectedImages.Should().Contain("1", "Image should be selected after click");
        }

        [Fact]
        public void FilmStrip_SelectedImage_ShowsSelectionIndicator()
        {
            // Arrange
            var images = _testImages.Take(1);
            var selectedImages = new HashSet<string> { "1" }; // Pre-select image

            // Act
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img => { })));

            // Assert
            var selectionIndicator = component.Find(".selection-indicator");
            selectionIndicator.Should().NotBeNull("Selected image should show selection indicator");
            
            var icon = selectionIndicator.QuerySelector("i.bi-check-circle-fill");
            icon.Should().NotBeNull("Selection indicator should contain check circle icon");

            var cardElement = component.Find(".card");
            var cardClasses = cardElement.GetAttribute("class");
            cardClasses.Should().Contain("selected", "Selected image should have 'selected' CSS class");
        }

        [Fact]
        public void FilmStrip_UnselectedImage_DoesNotShowSelectionIndicator()
        {
            // Arrange
            var images = _testImages.Take(1);
            var selectedImages = new HashSet<string>(); // No images selected

            // Act
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img => { })));

            // Assert
            var selectionIndicators = component.FindAll(".selection-indicator");
            selectionIndicators.Should().BeEmpty("Unselected image should not show selection indicator");

            var cardElement = component.Find(".card");
            var cardClasses = cardElement.GetAttribute("class");
            cardClasses.Should().NotContain("selected", "Unselected image should not have 'selected' CSS class");
        }

        [Fact]
        public void FilmStrip_KeyboardInteraction_TriggersSelection()
        {
            // Arrange
            var images = _testImages.Take(1);
            var selectionToggled = false;

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => false)
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionToggled = true;
                })));

            // Act - Simulate Enter key press
            var imageElement = component.Find(".film-thumbnail");
            imageElement.KeyDown(new KeyboardEventArgs { Key = "Enter" });

            // Assert
            selectionToggled.Should().BeTrue("Enter key should trigger selection");
        }

        [Fact]
        public void FilmStrip_SpaceKeyInteraction_TriggersSelection()
        {
            // Arrange
            var images = _testImages.Take(1);
            var selectionToggled = false;

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => false)
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionToggled = true;
                })));

            // Act - Simulate Space key press
            var imageElement = component.Find(".film-thumbnail");
            imageElement.KeyDown(new KeyboardEventArgs { Key = " " });

            // Assert
            selectionToggled.Should().BeTrue("Space key should trigger selection");
        }

        [Fact]
        public void FilmStrip_MultipleImages_IndependentSelection()
        {
            // Arrange
            var selectedImages = new HashSet<string> { "2" }; // Pre-select second image
            var toggleCalls = new List<string>();

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    toggleCalls.Add(img.Id);
                })));

            // Assert - Only second image should be visually selected
            var selectionIndicators = component.FindAll(".selection-indicator");
            selectionIndicators.Should().HaveCount(1, "Only one image should be selected");

            var selectedCards = component.FindAll(".card.selected");
            selectedCards.Should().HaveCount(1, "Only one card should have selected class");

            // Act - Click first image
            var allImages = component.FindAll(".film-thumbnail");
            allImages[0].Click();

            // Assert - First image callback should be triggered
            toggleCalls.Should().Contain("1", "First image toggle should be called");
        }

        [Fact]
        public void FilmStrip_WithoutOnImageSelectionToggled_DoesNotShowSelectionIndicator()
        {
            // Arrange - Only IsImageSelected provided, but no OnImageSelectionToggled
            var images = _testImages.Take(1);

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.IsImageSelected, img => true)); // No OnImageSelectionToggled provided

            // Assert - Should not show selection indicator without both parameters
            var selectionIndicators = component.FindAll(".selection-indicator");
            selectionIndicators.Should().BeEmpty("Selection indicator should not appear without OnImageSelectionToggled");

            var cardElement = component.Find(".card");
            var cardClasses = cardElement.GetAttribute("class");
            cardClasses.Should().NotContain("selected", "Card should not have selected class without OnImageSelectionToggled");
        }

        [Fact]
        public void FilmStrip_ExistingFeatures_StillWork()
        {
            // Arrange
            var images = _testImages.Take(1);
            var removeCallbackTriggered = false;

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.Title, "Test Gallery")
                .Add(p => p.AllowRemove, true)
                .Add(p => p.OnRemoveImage, EventCallback.Factory.Create<int>(this, index =>
                {
                    removeCallbackTriggered = true;
                })));

            // Assert - Title should be displayed
            var titleElement = component.Find("h6");
            titleElement.TextContent.Should().Contain("Test Gallery (1)", "Title should show correct count");

            // Assert - Remove button should be present
            var removeButton = component.Find(".delete-btn-circular");
            removeButton.Should().NotBeNull("Remove button should be present when AllowRemove is true");

            // Act - Click remove button
            removeButton.Click();

            // Assert - Remove callback should be triggered
            removeCallbackTriggered.Should().BeTrue("Remove callback should be triggered");
        }

        [Fact]
        public void FilmStrip_EmptyCollection_ShowsEmptyState()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, new List<CapturedImage>()));

            // Assert
            var emptyStateElement = component.Find(".text-center.text-muted.py-3");
            emptyStateElement.Should().NotBeNull("Empty state should be displayed");

            var emptyMessage = component.Find("p.mb-0");
            emptyMessage.TextContent.Should().Be("No images captured yet", "Empty message should be displayed");

            var emptyIcon = component.Find("i.bi-camera");
            emptyIcon.Should().NotBeNull("Camera icon should be displayed in empty state");
        }

        [Fact]
        public void FilmStrip_NullCollection_ShowsEmptyState()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, (IEnumerable<CapturedImage>?)null));

            // Assert
            var emptyStateElement = component.Find(".text-center.text-muted.py-3");
            emptyStateElement.Should().NotBeNull("Empty state should be displayed for null collection");
        }
    }
}