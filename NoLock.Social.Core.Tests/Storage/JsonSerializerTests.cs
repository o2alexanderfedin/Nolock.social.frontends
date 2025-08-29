using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.Tests.Storage
{
    public class JsonSerializerTests
    {
        private JsonSerializer<TestModel> _sut;

        public JsonSerializerTests()
        {
            _sut = new JsonSerializer<TestModel>();
        }

        [Fact]
        public void Constructor_WithDefaultOptions_UsesCorrectDefaults()
        {
            // Act
            var serializer = new JsonSerializer<TestModel>();
            var testObj = new TestModel { Id = 1, Name = "Test", OptionalValue = null };
            var bytes = serializer.Serialize(testObj);
            var json = Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"id\":1"); // CamelCase
            json.Should().NotContain("\"optionalValue\""); // Null values ignored
            json.Should().NotContain("\n"); // Not indented
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new JsonSerializer<TestModel>(null!));
        }

        [Fact]
        public void Constructor_WithCustomOptions_UsesProvidedOptions()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true
            };

            // Act
            var serializer = new JsonSerializer<TestModel>(options);
            var testObj = new TestModel { Id = 1, Name = "Test" };
            var bytes = serializer.Serialize(testObj);
            var json = Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"id\": 1");
            json.Should().Contain("\n"); // Indented
        }

        [Fact]
        public void Serialize_WithValidObject_ReturnsCorrectBytes()
        {
            // Arrange
            var testObj = new TestModel 
            { 
                Id = 42, 
                Name = "Test Name", 
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = TestStatus.Active 
            };

            // Act
            var bytes = _sut.Serialize(testObj);

            // Assert
            bytes.Should().NotBeNull();
            bytes.Should().NotBeEmpty();
            var json = Encoding.UTF8.GetString(bytes);
            json.Should().Contain("\"id\":42");
            json.Should().Contain("\"name\":\"Test Name\"");
            json.Should().Contain("\"status\":\"Active\""); // Enum as string
        }

        [Fact]
        public void Serialize_WithNullValue_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Serialize(null!));
        }

        [Fact]
        public void Serialize_WithCircularReference_ThrowsInvalidOperationException()
        {
            // Arrange
            var serializer = new JsonSerializer<CircularModel>();
            var model = new CircularModel { Id = 1 };
            model.Self = model; // Create circular reference

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(model));
            exception.Message.Should().Contain("Failed to serialize");
            exception.InnerException.Should().NotBeNull();
        }

        [Fact]
        public void Deserialize_WithValidData_ReturnsCorrectObject()
        {
            // Arrange
            var json = "{\"id\":123,\"name\":\"Test Object\",\"status\":\"Inactive\"}";
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act
            var result = _sut.Deserialize(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(123);
            result.Name.Should().Be("Test Object");
            result.Status.Should().Be(TestStatus.Inactive);
        }

        [Fact]
        public void Deserialize_WithNullData_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _sut.Deserialize(null!));
            exception.ParamName.Should().Be("data");
        }

        [Fact]
        public void Deserialize_WithEmptyData_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _sut.Deserialize(Array.Empty<byte>()));
            exception.Message.Should().Contain("cannot be null or empty");
        }

        [Fact]
        public void Deserialize_WithInvalidJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("{ invalid json }");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Deserialize(bytes));
            exception.Message.Should().Contain("Failed to deserialize");
            exception.InnerException.Should().NotBeNull();
        }

        [Fact]
        public void Deserialize_WithMissingRequiredProperties_HandlesGracefully()
        {
            // Arrange
            var json = "{}"; // Empty object
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act
            var result = _sut.Deserialize(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(0); // Default value
            result.Name.Should().BeNull(); // Default for string
        }

        [Fact]
        public void Deserialize_ResultingInNull_ThrowsInvalidOperationException()
        {
            // Arrange
            var json = "null";
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Deserialize(bytes));
            exception.Message.Should().Contain("resulted in null");
        }

        [Theory]
        [InlineData("")]
        public void Deserialize_WithEmptyString_ThrowsArgumentException(string json)
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _sut.Deserialize(bytes));
        }
        
        [Theory]
        [InlineData(" ")]
        [InlineData("\n")]
        [InlineData("\t")]
        public void Deserialize_WithWhitespaceOnly_ThrowsInvalidOperationException(string json)
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Deserialize(bytes));
        }

        [Fact]
        public void SerializeAndDeserialize_RoundTrip_PreservesData()
        {
            // Arrange
            var original = new TestModel
            {
                Id = 999,
                Name = "Round Trip Test",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Active,
                OptionalValue = "Optional",
                Numbers = new List<int> { 1, 2, 3, 4, 5 }
            };

            // Act
            var bytes = _sut.Serialize(original);
            var deserialized = _sut.Deserialize(bytes);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Id.Should().Be(original.Id);
            deserialized.Name.Should().Be(original.Name);
            deserialized.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMilliseconds(1));
            deserialized.Status.Should().Be(original.Status);
            deserialized.OptionalValue.Should().Be(original.OptionalValue);
            deserialized.Numbers.Should().BeEquivalentTo(original.Numbers);
        }

        [Fact]
        public void Serialize_WithComplexNestedObject_HandlesCorrectly()
        {
            // Arrange
            var serializer = new JsonSerializer<ComplexModel>();
            var model = new ComplexModel
            {
                Id = 1,
                Nested = new NestedModel
                {
                    Value = "Nested Value",
                    Count = 42
                },
                Items = new List<NestedModel>
                {
                    new() { Value = "Item1", Count = 1 },
                    new() { Value = "Item2", Count = 2 }
                }
            };

            // Act
            var bytes = serializer.Serialize(model);
            var json = Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"nested\"");
            json.Should().Contain("\"items\"");
            json.Should().Contain("\"value\":\"Nested Value\"");
            json.Should().Contain("\"count\":42");
        }

        [Fact]
        public void Deserialize_WithExtraProperties_IgnoresUnknownFields()
        {
            // Arrange
            var json = "{\"id\":456,\"name\":\"Test\",\"unknownField\":\"value\",\"anotherUnknown\":123}";
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act
            var result = _sut.Deserialize(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(456);
            result.Name.Should().Be("Test");
        }

        [Fact]
        public void Serialize_WithUtf8Characters_HandlesCorrectly()
        {
            // Arrange
            var testObj = new TestModel
            {
                Id = 1,
                Name = "Test with UTF-8: 你好世界 🌍 émojis"
            };

            // Act
            var bytes = _sut.Serialize(testObj);
            var json = Encoding.UTF8.GetString(bytes);

            // Assert - JSON may escape unicode, but should contain the value
            json.Should().NotBeNullOrEmpty();
            // Deserialize to verify the data is preserved
            var deserialized = _sut.Deserialize(bytes);
            deserialized.Name.Should().Be("Test with UTF-8: 你好世界 🌍 émojis");
        }

        [Fact]
        public void Deserialize_WithUtf8Characters_HandlesCorrectly()
        {
            // Arrange
            var json = "{\"id\":1,\"name\":\"UTF-8: 日本語 🎌 français\"}";
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act
            var result = _sut.Deserialize(bytes);

            // Assert
            result.Name.Should().Contain("日本語");
            result.Name.Should().Contain("🎌");
            result.Name.Should().Contain("français");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public void Serialize_WithLargeCollection_HandlesEfficiently(int itemCount)
        {
            // Arrange
            var model = new TestModel
            {
                Id = 1,
                Name = "Large Collection Test",
                Numbers = Enumerable.Range(1, itemCount).ToList()
            };

            // Act
            var bytes = _sut.Serialize(model);

            // Assert
            bytes.Should().NotBeNull();
            bytes.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Deserialize_WithInvalidUtf8Bytes_ThrowsInvalidOperationException()
        {
            // Arrange - Invalid UTF-8 sequence
            var bytes = new byte[] { 0xFF, 0xFE, 0xFD };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _sut.Deserialize(bytes));
            exception.Message.Should().Contain("Failed to deserialize");
        }

        [Fact]
        public void Constructor_DefaultOptions_ConfiguredCorrectly()
        {
            // Arrange & Act
            var serializer = new JsonSerializer<TestModel>();
            var model = new TestModel
            {
                Id = 1,
                Name = "Test",
                Status = TestStatus.Active,
                OptionalValue = null
            };

            var bytes = serializer.Serialize(model);
            var json = Encoding.UTF8.GetString(bytes);

            // Assert - Verify all default options
            json.Should().Contain("\"id\":"); // CamelCase
            json.Should().NotContain("\"Id\":"); // Not PascalCase
            json.Should().NotContain("\"optionalValue\""); // Null ignored
            json.Should().NotContain(" \n"); // Not indented
            json.Should().Contain("\"Active\""); // Enum as string
        }

        // Test Models
        private class TestModel
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public DateTime CreatedAt { get; set; }
            public TestStatus Status { get; set; }
            public string? OptionalValue { get; set; }
            public List<int>? Numbers { get; set; }
        }

        private enum TestStatus
        {
            Active,
            Inactive,
            Pending
        }

        private class CircularModel
        {
            public int Id { get; set; }
            public CircularModel? Self { get; set; }
        }

        private class ComplexModel
        {
            public int Id { get; set; }
            public NestedModel? Nested { get; set; }
            public List<NestedModel>? Items { get; set; }
        }

        private class NestedModel
        {
            public string? Value { get; set; }
            public int Count { get; set; }
        }
    }
}