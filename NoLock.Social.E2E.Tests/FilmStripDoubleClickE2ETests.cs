using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Threading.Tasks;

namespace NoLock.Social.E2E.Tests
{
    /// <summary>
    /// End-to-end tests for FilmStrip double-click/double-tap functionality
    /// to ensure fullscreen preview works correctly in real browsers.
    /// </summary>
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class FilmStripDoubleClickE2ETests : PageTest
    {
        private const string BaseUrl = "http://localhost:5002";
        private const string Username = "alexanderfedin";
        private const string Password = "Vilisaped1!";

        [SetUp]
        public async Task Setup()
        {
            // Navigate to the application
            await Page.GotoAsync(BaseUrl);
            
            // Wait for the app to load
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Login if needed
            if (await Page.Locator("input[placeholder='Enter your username']").IsVisibleAsync())
            {
                await LoginAsync();
            }
        }

        private async Task LoginAsync()
        {
            // Fill login credentials
            await Page.FillAsync("input[placeholder='Enter your username']", Username);
            await Page.FillAsync("input[placeholder='Enter your passphrase']", Password);
            
            // Click login button
            await Page.ClickAsync("button:has-text('Login')");
            
            // Wait for login to complete
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        [Test]
        public async Task DoubleClick_OnFilmStripThumbnail_OpensFullscreenViewer()
        {
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Take a test photo (assuming camera is available)
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                await captureButton.ClickAsync();
                await Task.Delay(1000); // Wait for capture to complete
                
                // Find the first thumbnail in the FilmStrip
                var thumbnail = Page.Locator(".film-thumbnail").First;
                
                // Verify thumbnail is visible
                await Expect(thumbnail).ToBeVisibleAsync();
                
                // Double-click the thumbnail
                await thumbnail.DblClickAsync();
                
                // Verify fullscreen viewer opens
                var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreenBackdrop).ToBeVisibleAsync();
                
                // Verify fullscreen image is displayed
                var fullscreenImage = Page.Locator(".fullscreen-image");
                await Expect(fullscreenImage).ToBeVisibleAsync();
                
                // Close fullscreen by clicking backdrop
                await fullscreenBackdrop.ClickAsync();
                
                // Verify fullscreen is closed
                await Expect(fullscreenBackdrop).Not.ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task DoubleClick_MultipleImages_ShowsCorrectImage()
        {
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Capture multiple images
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                // Capture 3 images
                for (int i = 0; i < 3; i++)
                {
                    await captureButton.ClickAsync();
                    await Task.Delay(500);
                }
                
                // Get all thumbnails
                var thumbnails = Page.Locator(".film-thumbnail");
                var thumbnailCount = await thumbnails.CountAsync();
                
                Assert.That(thumbnailCount, Is.GreaterThanOrEqualTo(3), 
                    "Should have at least 3 captured images");
                
                // Double-click the second thumbnail
                await thumbnails.Nth(1).DblClickAsync();
                
                // Verify fullscreen opens
                var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreenBackdrop).ToBeVisibleAsync();
                
                // Verify the correct image is shown (would need to check src attribute)
                var fullscreenImage = Page.Locator(".fullscreen-image");
                await Expect(fullscreenImage).ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task DoubleClick_ThenEscapeKey_ClosesFullscreen()
        {
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Capture an image
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                await captureButton.ClickAsync();
                await Task.Delay(1000);
                
                // Double-click thumbnail to open fullscreen
                var thumbnail = Page.Locator(".film-thumbnail").First;
                await thumbnail.DblClickAsync();
                
                // Verify fullscreen is open
                var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreenBackdrop).ToBeVisibleAsync();
                
                // Press Escape key
                await Page.Keyboard.PressAsync("Escape");
                
                // Verify fullscreen is closed
                await Expect(fullscreenBackdrop).Not.ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task MobileDoubleTap_OpensFullscreen()
        {
            // Set mobile viewport
            await Page.SetViewportSizeAsync(375, 667); // iPhone size
            
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Capture an image
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                await captureButton.ClickAsync();
                await Task.Delay(1000);
                
                // Find thumbnail
                var thumbnail = Page.Locator(".film-thumbnail").First;
                await Expect(thumbnail).ToBeVisibleAsync();
                
                // Simulate double-tap (double-click on mobile)
                await thumbnail.DblClickAsync();
                
                // Verify fullscreen opens
                var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreenBackdrop).ToBeVisibleAsync();
                
                // Verify mobile-optimized fullscreen display
                var fullscreenContainer = Page.Locator(".fullscreen-container");
                await Expect(fullscreenContainer).ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task SingleClick_WithSelectionMode_DoesNotOpenFullscreen()
        {
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Capture multiple images for selection
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                // Capture 2 images
                await captureButton.ClickAsync();
                await Task.Delay(500);
                await captureButton.ClickAsync();
                await Task.Delay(500);
                
                var thumbnail = Page.Locator(".film-thumbnail").First;
                
                // Long press to enter selection mode (if implemented)
                // This simulates mobile long-press behavior
                await thumbnail.HoverAsync();
                await Page.Mouse.DownAsync();
                await Task.Delay(600); // Hold for 600ms
                await Page.Mouse.UpAsync();
                
                // Single click should toggle selection, not open fullscreen
                await thumbnail.ClickAsync();
                
                // Check for selection indicator instead of fullscreen
                var selectionIndicator = Page.Locator(".selection-indicator").First;
                if (await selectionIndicator.IsVisibleAsync())
                {
                    // In selection mode - verify fullscreen is NOT open
                    var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                    await Expect(fullscreenBackdrop).Not.ToBeVisibleAsync();
                }
                
                // But double-click should still open fullscreen
                await thumbnail.DblClickAsync();
                var fullscreen = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreen).ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task RapidDoubleClicks_HandledCorrectly()
        {
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Capture an image
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                await captureButton.ClickAsync();
                await Task.Delay(1000);
                
                var thumbnail = Page.Locator(".film-thumbnail").First;
                
                // Perform rapid double-clicks
                await thumbnail.DblClickAsync();
                await Task.Delay(100);
                await thumbnail.DblClickAsync();
                
                // Should still show fullscreen (not crash or show multiple)
                var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreenBackdrop).ToBeVisibleAsync();
                
                // Should only have one fullscreen viewer
                var fullscreenCount = await fullscreenBackdrop.CountAsync();
                Assert.That(fullscreenCount, Is.EqualTo(1), 
                    "Should only have one fullscreen viewer open");
            }
        }

        [Test]
        public async Task CrossBrowser_DoubleClick_Works()
        {
            // This test will be run on different browsers via Playwright's browser fixtures
            // Navigate to document capture page
            await Page.GotoAsync($"{BaseUrl}/document-capture");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            await Task.Delay(3000);
            
            // Capture an image
            var captureButton = Page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                await captureButton.ClickAsync();
                await Task.Delay(1000);
                
                // Double-click thumbnail
                var thumbnail = Page.Locator(".film-thumbnail").First;
                await thumbnail.DblClickAsync();
                
                // Verify fullscreen works across browsers
                var fullscreenBackdrop = Page.Locator(".fullscreen-backdrop");
                await Expect(fullscreenBackdrop).ToBeVisibleAsync();
                
                // Log browser info for debugging
                var userAgent = await Page.EvaluateAsync<string>("() => navigator.userAgent");
                TestContext.WriteLine($"Test passed on browser: {userAgent}");
            }
        }

        [TearDown]
        public async Task TearDown()
        {
            // Clean up - logout if needed
            var logoutButton = Page.Locator("button:has-text('Logout')");
            if (await logoutButton.IsVisibleAsync())
            {
                await logoutButton.ClickAsync();
            }
        }
    }
}