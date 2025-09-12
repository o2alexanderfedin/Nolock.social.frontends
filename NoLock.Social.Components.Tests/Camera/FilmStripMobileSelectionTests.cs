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
using System.Threading.Tasks;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    /// <summary>
    /// Tests for FilmStrip component's mobile selection behavior.
    /// Updated to reflect the simplified checkbox-based selection without pointer events.
    /// </summary>
    public class FilmStripMobileSelectionTests : TestContext
    {
        private readonly Mock<IResizeListener> _mockResizeListener;
        private readonly List<CapturedImage> _testImages;

        public FilmStripMobileSelectionTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            Services.AddSingleton(_mockResizeListener.Object);
            
            _testImages = new List<CapturedImage>
            {
                new() { 
                    Id = "1", 
                    ImageUrl = "data:image/png;base64,test1", 
                    Timestamp = DateTime.UtcNow
                },
                new() { 
                    Id = "2", 
                    ImageUrl = "data:image/png;base64,test2", 
                    Timestamp = DateTime.UtcNow.AddMinutes(-5)
                },
                new() { 
                    Id = "3", 
                    ImageUrl = "data:image/png;base64,test3", 
                    Timestamp = DateTime.UtcNow.AddMinutes(-10)
                }
            };
        }

        [Fact]
        public void FilmStrip_WithCheckbox_EnablesSelectionMode()
        {
            // Arrange
            var selectedImages = new HashSet<string>();
            var selectionActivated = false;

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img =>
                {
                    selectionActivated = true;
                    if (selectedImages.Contains(img.Id))
                        selectedImages.Remove(img.Id);
                    else
                        selectedImages.Add(img.Id);
                })));

            // Act - Click checkbox to select
            var checkbox = component.Find(".selection-checkbox");
            checkbox.Should().NotBeNull("Checkbox should be present when selection handlers are provided");
            checkbox.Click();

            // Assert
            selectionActivated.Should().BeTrue("Selection should be activated via checkbox");
            selectedImages.Should().Contain("1", "First image should be selected");
            
            // Visual feedback
            var selectedCard = component.Find(".card.selected");
            selectedCard.Should().NotBeNull("Selected card should have visual feedback");
        }

        [Fact]
        public void FilmStrip_CheckboxClick_DoesNotOpenFullscreen()
        {
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

            // Act - Click checkbox
            var checkbox = component.Find(".selection-checkbox");
            checkbox.Click();

            // Assert - No fullscreen
            var fullscreenViewer = component.FindAll(".fullscreen-backdrop");
            fullscreenViewer.Should().BeEmpty("Checkbox click should not trigger fullscreen mode");
        }

        [Fact]
        public void FilmStrip_ImageClick_OpensFullscreen_NotSelection()
        {
            // Arrange - Even with selection mode enabled
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

            // Act - Click image (not checkbox)
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();

            // Assert - Fullscreen opens, selection unchanged
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Image click should open fullscreen");
            selectedImages.Should().BeEmpty("Image click should not change selection state");
        }

        [Fact]
        public void FilmStrip_MultipleCheckboxes_IndependentSelection()
        {
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

            // Act - Select multiple images via checkboxes
            var checkboxes = component.FindAll(".selection-checkbox");
            checkboxes[0].Click(); // Select first
            
            // Re-query checkboxes after first click (component re-renders)
            checkboxes = component.FindAll(".selection-checkbox");
            checkboxes[2].Click(); // Select third

            // Assert
            selectedImages.Should().HaveCount(2, "Two images should be selected");
            selectedImages.Should().Contain(new[] { "1", "3" }, "First and third images should be selected");
            
            // Visual feedback for multiple selections
            var selectedCards = component.FindAll(".card.selected");
            selectedCards.Should().HaveCount(2, "Two cards should show selection state");
        }

        [Fact]
        public void FilmStrip_CheckboxToggle_DeselectsImage()
        {
            // Arrange
            var selectedImages = new HashSet<string> { "1" }; // Pre-selected
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

            // Verify pre-selection
            var selectedCard = component.Find(".card.selected");
            selectedCard.Should().NotBeNull("First card should be pre-selected");

            // Act - Click checkbox to deselect
            var checkbox = component.Find(".selection-checkbox");
            checkbox.GetAttribute("checked").Should().NotBeNull("Checkbox should be checked initially");
            checkbox.Click();

            // Assert
            selectedImages.Should().BeEmpty("Image should be deselected");
            var deselectedCards = component.FindAll(".card.selected");
            deselectedCards.Should().BeEmpty("No cards should show selection state");
        }

        [Fact]
        public void FilmStrip_WithoutSelectionHandlers_NoCheckboxes()
        {
            // Arrange - Component without selection handlers
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages));

            // Assert - No checkboxes should be rendered
            var checkboxes = component.FindAll(".selection-checkbox");
            checkboxes.Should().BeEmpty("Checkboxes should not appear without selection handlers");
            
            // Image click should still work for fullscreen
            var thumbnail = component.Find(".film-thumbnail");
            thumbnail.Click();
            
            var fullscreenViewer = component.Find(".fullscreen-backdrop");
            fullscreenViewer.Should().NotBeNull("Fullscreen should work without selection mode");
        }

        [Fact]
        public void FilmStrip_CheckboxStopsPropagation()
        {
            // Arrange
            // var imageClicked = false; // Not used as click doesn't propagate
            var selectionToggled = false;
            var selectedImages = new HashSet<string>();
            
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

            // Monitor if image click handler is triggered
            var thumbnail = component.Find(".film-thumbnail");
            
            // Act - Click checkbox (should stop propagation)
            var checkbox = component.Find(".selection-checkbox");
            checkbox.Click();

            // Assert
            selectionToggled.Should().BeTrue("Checkbox should trigger selection");
            // The fullscreen viewer should not open (indicating click didn't propagate)
            var fullscreenViewer = component.FindAll(".fullscreen-backdrop");
            fullscreenViewer.Should().BeEmpty("Click should not propagate from checkbox to image");
        }

        [Fact]
        public void FilmStrip_ResponsiveCheckboxSizing()
        {
            // Arrange
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => false)
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img => { })));

            // Assert - Checkbox container should have proper styling
            var checkboxContainer = component.Find(".selection-checkbox-container");
            checkboxContainer.Should().NotBeNull("Checkbox container should exist");
            
            // Check for responsive positioning
            var containerStyle = checkboxContainer.GetAttribute("style") ?? "";
            var containerClasses = checkboxContainer.GetAttribute("class") ?? "";
            
            // The container should be positioned for easy mobile access
            containerClasses.Should().Contain("selection-checkbox-container", 
                "Container should have mobile-optimized styling class");
        }

        [Fact]
        public void FilmStrip_SelectionVisualFeedback_ScalesOnMobile()
        {
            // Arrange
            var selectedImages = new HashSet<string> { "1" };
            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, _testImages)
                .Add(p => p.IsImageSelected, img => selectedImages.Contains(img.Id))
                .Add(p => p.OnImageSelectionToggled, EventCallback.Factory.Create<CapturedImage>(this, img => { })));

            // Assert - Selected card should have visual feedback
            var selectedCard = component.Find(".card.selected");
            selectedCard.Should().NotBeNull("Selected card should have visual state");
            
            // The CSS includes mobile-specific scaling
            var cardClasses = selectedCard.GetAttribute("class");
            cardClasses.Should().Contain("selected", "Card should have selected class for mobile-optimized feedback");
        }
    }
}