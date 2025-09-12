using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoLock.Social.Components.Camera;
using NoLock.Social.Core.Camera.Models;
using BlazorPro.BlazorSize;
using Xunit;

namespace NoLock.Social.Components.Tests.Camera
{
    public class FilmStripIntegrationTests : TestContext
    {
        private readonly Mock<IResizeListener> _mockResizeListener;

        public FilmStripIntegrationTests()
        {
            _mockResizeListener = new Mock<IResizeListener>();
            Services.AddSingleton(_mockResizeListener.Object);
        }

        [Fact]
        public async Task ThumbnailClick_OpensFullscreenViewer()
        {
            // Arrange
            var images = new List<CapturedImage>
            {
                new() { Id = "1", ImageUrl = "test1.jpg", Width = 800, Height = 600 },
                new() { Id = "2", ImageUrl = "test2.jpg", Width = 1024, Height = 768 }
            };

            var component = RenderComponent<FilmStrip>(parameters => parameters
                .Add(p => p.CapturedImages, images)
                .Add(p => p.AllowRemove, true));

            // Act
            var thumbnail = component.Find(".film-thumbnail");
            await thumbnail.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.Contains("fullscreen-backdrop", component.Markup);
            Assert.Contains("test1.jpg", component.Markup);
        }
    }
}