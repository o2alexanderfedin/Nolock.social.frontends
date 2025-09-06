using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Components.Camera;
using NoLock.Social.Core.Camera.Models;
using BlazorPro.BlazorSize;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    public class FullscreenImageViewerTests : TestContext
    {
        private readonly Mock<IResizeListener> _mockResizeListener;
        private readonly Mock<ILogger<FullscreenImageViewer>> _mockLogger;

        public FullscreenImageViewerTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            _mockLogger = new Mock<ILogger<FullscreenImageViewer>>();

            // Register services
            Services.AddSingleton(_mockResizeListener.Object);
            Services.AddSingleton(_mockLogger.Object);
        }

        [Fact]
        public void Component_InitialRender_DoesNotShowModal()
        {
            // Arrange & Act
            var component = RenderComponent<FullscreenImageViewer>();

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }

        [Fact]
        public async Task ShowAsync_WithValidImage_ShowsModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage 
            { 
                Id = "test-id",
                ImageUrl = "data:image/jpeg;base64,test-data",
                Width = 800,
                Height = 600
            };

            // Act
            await component.Instance.ShowAsync(image);

            // Assert
            Assert.Contains("fullscreen-backdrop", component.Markup);
            Assert.Contains("test-data", component.Markup);
        }

        [Fact]
        public async Task CloseAsync_WhenVisible_HidesModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage { ImageUrl = "test.jpg" };
            await component.Instance.ShowAsync(image);

            // Act
            await component.Instance.CloseAsync();

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }

        [Fact]
        public async Task BackdropClick_WhenVisible_ClosesModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage { ImageUrl = "test.jpg" };
            await component.Instance.ShowAsync(image);

            // Act
            var backdrop = component.Find(".fullscreen-backdrop");
            await backdrop.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }

        [Fact]
        public async Task ShowAsync_WithNullImage_ThrowsArgumentNullException()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                component.Instance.ShowAsync(null!));
        }

        [Fact]
        public async Task EscapeKey_WhenVisible_ClosesModal()
        {
            // Arrange
            var component = RenderComponent<FullscreenImageViewer>();
            var image = new CapturedImage { ImageUrl = "test.jpg" };
            await component.Instance.ShowAsync(image);

            // Act
            var backdrop = component.Find(".fullscreen-backdrop");
            await backdrop.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

            // Assert
            Assert.DoesNotContain("fullscreen-backdrop", component.Markup);
        }
    }
}