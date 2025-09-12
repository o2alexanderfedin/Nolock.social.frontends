using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace NoLock.Social.E2E.Tests
{
    /// <summary>
    /// Simple Playwright integration test to verify double-click functionality
    /// Can be run manually without NUnit framework
    /// </summary>
    public class PlaywrightIntegrationTest
    {
        private const string BaseUrl = "http://localhost:5002";
        private const string Username = "alexanderfedin";
        private const string Password = "Vilisaped1!";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Playwright Integration Test...");
            
            try
            {
                await RunDoubleClickTest();
                Console.WriteLine("✅ All tests passed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task RunDoubleClickTest()
        {
            Console.WriteLine("Initializing Playwright...");
            using var playwright = await Playwright.CreateAsync();
            
            Console.WriteLine("Launching browser...");
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            
            Console.WriteLine("Creating browser context...");
            await using var context = await browser.NewContextAsync();
            
            Console.WriteLine("Creating new page...");
            var page = await context.NewPageAsync();
            
            // Navigate to the application
            Console.WriteLine($"Navigating to {BaseUrl}...");
            await page.GotoAsync(BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Login if needed
            var usernameInput = page.Locator("input[placeholder='Enter your username']");
            if (await usernameInput.IsVisibleAsync())
            {
                Console.WriteLine("Logging in...");
                await usernameInput.FillAsync(Username);
                await page.FillAsync("input[placeholder='Enter your passphrase']", Password);
                await page.ClickAsync("button:has-text('Login')");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
            
            // Navigate to document capture page
            Console.WriteLine("Navigating to document capture page...");
            await page.GotoAsync($"{BaseUrl}/document-capture");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for camera to initialize
            Console.WriteLine("Waiting for camera to initialize...");
            await Task.Delay(3000);
            
            // Take a test photo
            var captureButton = page.Locator("button:has-text('Capture')").First;
            if (await captureButton.IsVisibleAsync())
            {
                Console.WriteLine("Capturing an image...");
                await captureButton.ClickAsync();
                await Task.Delay(1000); // Wait for capture to complete
                
                // Find the first thumbnail
                var thumbnail = page.Locator(".film-thumbnail").First;
                
                // Verify thumbnail is visible
                Console.WriteLine("Verifying thumbnail is visible...");
                var thumbnailVisible = await thumbnail.IsVisibleAsync();
                if (!thumbnailVisible)
                {
                    throw new Exception("Thumbnail is not visible after capture");
                }
                
                // Double-click the thumbnail
                Console.WriteLine("Double-clicking thumbnail to open fullscreen...");
                await thumbnail.DblClickAsync();
                
                // Wait a moment for the fullscreen to open
                await Task.Delay(500);
                
                // Verify fullscreen viewer opens
                Console.WriteLine("Verifying fullscreen viewer is open...");
                var fullscreenBackdrop = page.Locator(".fullscreen-backdrop");
                var fullscreenVisible = await fullscreenBackdrop.IsVisibleAsync();
                if (!fullscreenVisible)
                {
                    // Check for alternative fullscreen selectors
                    var fullscreenImage = page.Locator("img[alt='Fullscreen preview of captured image']");
                    fullscreenVisible = await fullscreenImage.IsVisibleAsync();
                }
                
                if (!fullscreenVisible)
                {
                    throw new Exception("Fullscreen viewer did not open after double-click");
                }
                
                Console.WriteLine("✅ Double-click successfully opened fullscreen viewer!");
                
                // Close fullscreen
                Console.WriteLine("Closing fullscreen viewer...");
                await page.Keyboard.PressAsync("Escape");
                await Task.Delay(500);
                
                // Verify fullscreen is closed
                var fullscreenClosed = !await fullscreenBackdrop.IsVisibleAsync();
                if (!fullscreenClosed)
                {
                    throw new Exception("Fullscreen viewer did not close after pressing Escape");
                }
                
                Console.WriteLine("✅ Fullscreen viewer successfully closed!");
            }
            else
            {
                throw new Exception("Capture button not found");
            }
        }
    }
}