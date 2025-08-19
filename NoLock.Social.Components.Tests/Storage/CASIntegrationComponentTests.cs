/*
REASON FOR COMMENTING: CASIntegrationComponent does not exist yet.
This test file was created for a component that hasn't been implemented.
Uncomment when the component is created.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Components;
using NoLock.Social.Core.Cryptography;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Storage.Interfaces;
using Xunit;

namespace NoLock.Social.Components.Tests.Storage
{
    public class CASIntegrationComponentTests : TestContext
    {
        private readonly Mock<IStorageAdapterService> _mockStorageAdapter;
        private readonly Mock<ISigningService> _mockSigningService;
        private readonly Mock<ISessionStateService> _mockSessionService;
        private readonly Mock<ICryptoErrorHandlingService> _mockErrorHandler;
        private readonly Mock<ILogger<CASIntegrationComponent>> _mockLogger;

        public CASIntegrationComponentTests()
        {
            _mockStorageAdapter = new Mock<IStorageAdapterService>();
            _mockSigningService = new Mock<ISigningService>();
            _mockSessionService = new Mock<ISessionStateService>();
            _mockErrorHandler = new Mock<ICryptoErrorHandlingService>();
            _mockLogger = new Mock<ILogger<CASIntegrationComponent>>();

            // Setup default error handler behavior
            _mockErrorHandler.Setup(x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorContext>()))
                .ReturnsAsync(new ErrorInfo
                {
                    UserMessage = "An error occurred",
                    RecoverySuggestions = new[] { "Please try again" },
                    ErrorCode = "TEST001",
                    Category = ErrorCategory.Storage
                });

            Services.AddSingleton(_mockStorageAdapter.Object);
            Services.AddSingleton(_mockSigningService.Object);
            Services.AddSingleton(_mockSessionService.Object);
            Services.AddSingleton(_mockErrorHandler.Object);
            Services.AddSingleton(_mockLogger.Object);
        }

        [Fact]
        public void Component_Renders_Successfully()
        {
            // Arrange
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            // Act
            var component = RenderComponent<CASIntegrationComponent>();

            // Assert
            Assert.NotNull(component);
            Assert.Contains("Content Addressable Storage", component.Markup);
            Assert.Contains("Store Signed Content", component.Markup);
            Assert.Contains("Retrieve Content by Address", component.Markup);
            Assert.Contains("Browse Stored Content", component.Markup);
        }

        [Fact]
        public async Task StoreContent_Success_CallsServicesCorrectly()
        {
            // Arrange
            var mockSecureBuffer = new Mock<ISecureBuffer>();
            var privateKeyData = new byte[32];
            for (var i = 0; i < 32; i++)
                privateKeyData[i] = 0xFF;
            mockSecureBuffer.Setup(x => x.Data)
                .Returns(privateKeyData);
            
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32],
                PrivateKeyBuffer = mockSecureBuffer.Object,
                IsLocked = false
            };
            
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            _mockSessionService.Setup(x => x.CurrentSession)
                .Returns(session);
            
            var signedContent = new SignedContent
            {
                Content = "Test content to store",
                ContentHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[32],
                Algorithm = "Ed25519",
                Version = "1.0",
                Timestamp = DateTime.UtcNow
            };
            
            var tcs = new TaskCompletionSource<SignedContent>();
            tcs.SetResult(signedContent);
            
            _mockSigningService.Setup(x => x.SignContentAsync(
                    It.IsAny<string>(), 
                    It.IsAny<byte[]>(), 
                    It.IsAny<byte[]>()))
                .Returns(tcs.Task);
            
            var metadata = new StorageMetadata
            {
                ContentAddress = "abc123def456",
                Size = 100,
                Timestamp = DateTime.UtcNow,
                Algorithm = "Ed25519",
                Version = "1.0"
            };
            
            var storageTcs = new TaskCompletionSource<StorageMetadata>();
            storageTcs.SetResult(metadata);
            
            _mockStorageAdapter.Setup(x => x.StoreSignedContentAsync(It.IsAny<SignedContent>()))
                .Returns(storageTcs.Task);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            // Act
            var component = RenderComponent<CASIntegrationComponent>();
            
            // Set the content
            var textarea = component.Find("textarea#contentInput");
            await textarea.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = "Test content to store" 
            });
            
            // Click the store button
            var storeButton = component.Find("button:contains('Store Content')");
            await storeButton.ClickAsync(new MouseEventArgs());
            
            // Give it a moment to process
            await Task.Delay(100);
            
            // Assert - Verify the services were called
            _mockSigningService.Verify(x => x.SignContentAsync(
                "Test content to store",
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>()), Times.Once);
                
            _mockStorageAdapter.Verify(x => x.StoreSignedContentAsync(
                It.Is<SignedContent>(sc => sc.Content == "Test content to store")), Times.Once);
        }

        [Fact]
        public async Task StoreContent_SessionInactive_ShowsWarning()
        {
            // Arrange
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(false);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            // Act
            var component = RenderComponent<CASIntegrationComponent>();

            // Assert
            Assert.Contains("Please unlock your identity first", component.Markup);
            var storeButton = component.Find("button:contains('Store Content')");
            Assert.True(storeButton.HasAttribute("disabled"));
        }

        [Fact]
        public async Task RetrieveContent_Success_DisplaysContent()
        {
            // Arrange
            var signedContent = new SignedContent
            {
                Content = "Retrieved test content",
                ContentHash = new byte[32],
                Signature = new byte[64],
                PublicKey = new byte[32],
                Algorithm = "Ed25519",
                Version = "1.0",
                Timestamp = DateTime.UtcNow
            };
            
            _mockStorageAdapter.Setup(x => x.RetrieveSignedContentAsync("test-address"))
                .ReturnsAsync(signedContent);
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            var component = RenderComponent<CASIntegrationComponent>();

            // Act
            var input = component.Find("input#addressInput");
            await input.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = "test-address" 
            });
            
            var retrieveButton = component.Find("button:contains('Retrieve Content')");
            await retrieveButton.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.Contains("Retrieved test content", component.Markup);
            Assert.Contains("Signature Verified", component.Markup);
            Assert.Contains("Ed25519", component.Markup);
        }

        [Fact]
        public async Task RetrieveContent_NotFound_ShowsError()
        {
            // Arrange
            _mockStorageAdapter.Setup(x => x.RetrieveSignedContentAsync("invalid-address"))
                .ReturnsAsync((SignedContent?)null);
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            var component = RenderComponent<CASIntegrationComponent>();

            // Act
            var input = component.Find("input#addressInput");
            await input.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = "invalid-address" 
            });
            
            var retrieveButton = component.Find("button:contains('Retrieve Content')");
            await retrieveButton.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.Contains("Content not found", component.Markup);
        }

        [Fact]
        public async Task RetrieveContent_VerificationFailed_ShowsError()
        {
            // Arrange
            _mockStorageAdapter.Setup(x => x.RetrieveSignedContentAsync("bad-content"))
                .ThrowsAsync(new StorageVerificationException("Verification failed", "bad-content"));
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            var component = RenderComponent<CASIntegrationComponent>();

            // Act
            var input = component.Find("input#addressInput");
            await input.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = "bad-content" 
            });
            
            var retrieveButton = component.Find("button:contains('Retrieve Content')");
            await retrieveButton.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.Contains("failed signature verification", component.Markup);
            Assert.Contains("tampered", component.Markup);
        }

        [Fact]
        public async Task BrowseContent_DisplaysContentList()
        {
            // Arrange
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata
                {
                    ContentAddress = "address1",
                    Size = 1024,
                    Timestamp = DateTime.UtcNow.AddHours(-1),
                    Algorithm = "Ed25519"
                },
                new StorageMetadata
                {
                    ContentAddress = "address2",
                    Size = 2048,
                    Timestamp = DateTime.UtcNow,
                    Algorithm = "Ed25519"
                }
            };

            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);

            // Act
            var component = RenderComponent<CASIntegrationComponent>();

            // Assert
            Assert.Contains("address1", component.Markup);
            Assert.Contains("address2", component.Markup);
            Assert.Contains("1 KB", component.Markup);
            Assert.Contains("2 KB", component.Markup);
        }

        [Fact]
        public async Task DeleteContent_Success_RefreshesListAndShowsMessage()
        {
            // Arrange
            var initialList = new List<StorageMetadata>
            {
                new StorageMetadata { ContentAddress = "to-delete", Size = 100 }
            };
            
            _mockStorageAdapter.SetupSequence(x => x.ListAllContentAsync())
                .Returns(initialList.ToAsyncEnumerable())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());
            
            _mockStorageAdapter.Setup(x => x.DeleteContentAsync("to-delete"))
                .ReturnsAsync(true);
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);

            var component = RenderComponent<CASIntegrationComponent>();

            // Act
            var deleteButton = component.Find("button:contains('Delete')");
            await deleteButton.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.Contains("Content deleted successfully", component.Markup);
            _mockStorageAdapter.Verify(x => x.DeleteContentAsync("to-delete"), Times.Once);
            _mockStorageAdapter.Verify(x => x.ListAllContentAsync(), Times.Exactly(2));
        }

        [Fact]
        public void FormatBytes_CorrectlyFormatsVariousSizes()
        {
            // Arrange
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            
            var contentList = new List<StorageMetadata>
            {
                new StorageMetadata { ContentAddress = "a1", Size = 512 },      // 512 B
                new StorageMetadata { ContentAddress = "a2", Size = 1024 },     // 1 KB
                new StorageMetadata { ContentAddress = "a3", Size = 1048576 },  // 1 MB
                new StorageMetadata { ContentAddress = "a4", Size = 1073741824 } // 1 GB
            };
            
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(contentList.ToAsyncEnumerable());

            // Act
            var component = RenderComponent<CASIntegrationComponent>();

            // Assert
            Assert.Contains("512 B", component.Markup);
            Assert.Contains("1 KB", component.Markup);
            Assert.Contains("1 MB", component.Markup);
            Assert.Contains("1 GB", component.Markup);
        }

        [Fact]
        public async Task ErrorHandling_DisplaysUserFriendlyMessages()
        {
            // Arrange
            var mockSecureBuffer = new Mock<ISecureBuffer>();
            var privateKeyData = new byte[32];
            for (var i = 0; i < 32; i++)
                privateKeyData[i] = 0xFF;
            mockSecureBuffer.Setup(x => x.Data)
                .Returns(privateKeyData);
            
            var session = new IdentitySession
            {
                Username = "testuser",
                PublicKey = new byte[32],
                PrivateKeyBuffer = mockSecureBuffer.Object,
                IsLocked = false
            };
            
            _mockSessionService.Setup(x => x.IsUnlocked)
                .Returns(true);
            _mockSessionService.Setup(x => x.CurrentSession)
                .Returns(session);
            _mockSigningService.Setup(x => x.SignContentAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<byte[]>()))
                .ThrowsAsync(new Exception("Test error"));
            
            var errorInfo = new ErrorInfo
            {
                UserMessage = "A friendly error message",
                RecoverySuggestions = new List<string> 
                { 
                    "Try again",
                    "Check your connection" 
                }
            };
            
            _mockErrorHandler.Setup(x => x.HandleErrorAsync(
                    It.IsAny<Exception>(), 
                    It.IsAny<ErrorContext>()))
                .ReturnsAsync(errorInfo);
            _mockStorageAdapter.Setup(x => x.ListAllContentAsync())
                .Returns(Enumerable.Empty<StorageMetadata>().ToAsyncEnumerable());

            var component = RenderComponent<CASIntegrationComponent>();

            // Act
            var textarea = component.Find("textarea#contentInput");
            await textarea.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs 
            { 
                Value = "Test content" 
            });
            
            var storeButton = component.Find("button:contains('Store Content')");
            await storeButton.ClickAsync(new MouseEventArgs());

            // Assert
            Assert.Contains("A friendly error message", component.Markup);
            Assert.Contains("Try again", component.Markup);
            Assert.Contains("Check your connection", component.Markup);
        }
    }

    // Helper extension for converting IEnumerable to IAsyncEnumerable
    public static class AsyncEnumerableExtensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
                await Task.CompletedTask;
            }
        }
    }
}
*/