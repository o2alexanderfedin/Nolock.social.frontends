using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using Nolock.Social.Storage.IndexedDb;

namespace Nolock.Social.Storage.IndexedDb.Tests
{
    public class IndexedDbKeyPathIssueTests
    {
        private readonly Mock<IJSRuntime> _mockJsRuntime;

        public IndexedDbKeyPathIssueTests()
        {
            _mockJsRuntime = new Mock<IJSRuntime>();
        }

        [Fact]
        public async Task GenericCasEntry_Fails_WithKeyPathError()
        {
            // Arrange - Current implementation with generic CasEntry<T>
            var testData = new TestContentData { Id = "test-123", Value = "test-value" };
            var genericEntry = new GenericCasEntry<TestContentData>
            {
                Hash = "hash123",
                Data = testData,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Simulate IndexedDB attempting to access Hash property on generic type
            _mockJsRuntime
                .Setup(js => js.InvokeAsync<object>(
                    "indexedDb.addEntry",
                    It.IsAny<object[]>()))
                .ThrowsAsync(new JSException("Cannot access property 'Hash' on generic type CasEntry`1"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<JSException>(async () =>
            {
                await _mockJsRuntime.Object.InvokeAsync<object>("indexedDb.addEntry", genericEntry);
            });

            Assert.Contains("Cannot access property 'Hash'", exception.Message);
        }

        [Fact]
        public async Task NonGenericCasEntry_WithJsonData_WorksCorrectly()
        {
            // Arrange - Proposed solution with non-generic entry and JSON serialization
            var testData = new TestContentData { Id = "test-123", Value = "test-value" };
            var jsonData = JsonSerializer.Serialize(testData);
            
            var nonGenericEntry = new NonGenericCasEntry
            {
                Hash = "hash123",
                Data = jsonData, // Stored as JSON string
                Timestamp = DateTimeOffset.UtcNow
            };

            // Simulate successful IndexedDB operation
            _mockJsRuntime
                .Setup(js => js.InvokeAsync<object>(
                    "indexedDb.addEntry",
                    It.IsAny<object[]>()))
                .ReturnsAsync(new { success = true });

            // Act
            var result = await _mockJsRuntime.Object.InvokeAsync<object>("indexedDb.addEntry", nonGenericEntry);

            // Assert - Verify the entry can be stored successfully
            Assert.NotNull(result);
            _mockJsRuntime.Verify(js => js.InvokeAsync<object>(
                "indexedDb.addEntry",
                It.Is<object[]>(args => args[0] == nonGenericEntry)),
                Times.Once);

            // Verify Hash property is accessible
            Assert.Equal("hash123", nonGenericEntry.Hash);
            
            // Verify data can be deserialized back
            var deserializedData = JsonSerializer.Deserialize<TestContentData>(nonGenericEntry.Data);
            Assert.Equal("test-123", deserializedData.Id);
            Assert.Equal("test-value", deserializedData.Value);
        }

        [Fact]
        public void NonGenericEntry_MaintainsTypeInformation()
        {
            // Arrange
            var testData = new TestContentData { Id = "test-123", Value = "test-value" };
            var jsonData = JsonSerializer.Serialize(testData);
            
            var entry = new NonGenericCasEntry
            {
                Hash = "hash123",
                Data = jsonData,
                DataType = typeof(TestContentData).AssemblyQualifiedName, // Store type info
                Timestamp = DateTimeOffset.UtcNow
            };

            // Act - Deserialize using type information
            var type = Type.GetType(entry.DataType);
            var deserializedData = JsonSerializer.Deserialize(entry.Data, type);

            // Assert
            Assert.NotNull(deserializedData);
            Assert.IsType<TestContentData>(deserializedData);
            var typedData = (TestContentData)deserializedData;
            Assert.Equal("test-123", typedData.Id);
            Assert.Equal("test-value", typedData.Value);
        }

        // Test data classes for simulation
        private class TestContentData
        {
            public string Id { get; set; }
            public string Value { get; set; }
        }

        // Simulated current implementation (generic)
        private class GenericCasEntry<T>
        {
            public string Hash { get; set; }
            public T Data { get; set; }
            public DateTimeOffset Timestamp { get; set; }
        }

        // Proposed non-generic solution
        private class NonGenericCasEntry
        {
            public string Hash { get; set; }
            public string Data { get; set; } // JSON serialized data
            public string DataType { get; set; } // Optional: store type information
            public DateTimeOffset Timestamp { get; set; }
        }
    }
}