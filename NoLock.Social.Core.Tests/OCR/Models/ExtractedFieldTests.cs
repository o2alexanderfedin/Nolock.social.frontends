using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the ExtractedField model.
    /// </summary>
    public class ExtractedFieldTests
    {
        [Fact]
        public void ExtractedField_DefaultValues()
        {
            // Arrange & Act
            var field = new ExtractedField();

            // Assert
            Assert.Null(field.FieldName);
            Assert.Null(field.Value);
            Assert.Equal(0, field.Confidence);
            Assert.Null(field.BoundingBox);
        }

        [Theory]
        [InlineData("Name", "John Doe", 95.5)]
        [InlineData("Date", "2024-01-15", 88.0)]
        [InlineData("Amount", "$150.00", 99.9)]
        [InlineData("AccountNumber", "123456789", 75.5)]
        public void ExtractedField_PropertiesSetCorrectly(string fieldName, string value, double confidence)
        {
            // Arrange & Act
            var field = new ExtractedField
            {
                FieldName = fieldName,
                Value = value,
                Confidence = confidence
            };

            // Assert
            Assert.Equal(fieldName, field.FieldName);
            Assert.Equal(value, field.Value);
            Assert.Equal(confidence, field.Confidence);
        }

        [Fact]
        public void ExtractedField_WithBoundingBox()
        {
            // Arrange
            var boundingBox = new BoundingBox
            {
                X = 100,
                Y = 200,
                Width = 300,
                Height = 50,
                PageNumber = 1
            };

            // Act
            var field = new ExtractedField
            {
                FieldName = "Address",
                Value = "123 Main St",
                Confidence = 92.3,
                BoundingBox = boundingBox
            };

            // Assert
            Assert.NotNull(field.BoundingBox);
            Assert.Equal(100, field.BoundingBox.X);
            Assert.Equal(200, field.BoundingBox.Y);
            Assert.Equal(300, field.BoundingBox.Width);
            Assert.Equal(50, field.BoundingBox.Height);
            Assert.Equal(1, field.BoundingBox.PageNumber);
        }

        [Theory]
        [InlineData(0.0, "Zero confidence")]
        [InlineData(50.0, "Medium confidence")]
        [InlineData(100.0, "Perfect confidence")]
        [InlineData(150.0, "Over 100% (invalid but settable)")]
        [InlineData(-10.0, "Negative (invalid but settable)")]
        public void Confidence_VariousValues(double confidence, string scenario)
        {
            // Arrange & Act
            var field = new ExtractedField
            {
                FieldName = "TestField",
                Value = "TestValue",
                Confidence = confidence
            };

            // Assert
            Assert.Equal(confidence, field.Confidence);
        }

        [Theory]
        [InlineData("", "Empty field name")]
        [InlineData(null, "Null field name")]
        [InlineData("Very_Long_Field_Name_With_Underscores", "Long field name")]
        [InlineData("Field.With.Dots", "Field name with dots")]
        [InlineData("Field-With-Dashes", "Field name with dashes")]
        public void FieldName_VariousValues(string fieldName, string scenario)
        {
            // Arrange & Act
            var field = new ExtractedField { FieldName = fieldName };

            // Assert
            Assert.Equal(fieldName, field.FieldName);
        }

        [Theory]
        [InlineData("", "Empty value")]
        [InlineData(null, "Null value")]
        [InlineData("Simple text", "Simple text value")]
        [InlineData("123.45", "Numeric string")]
        [InlineData("2024-01-15T10:30:00Z", "DateTime string")]
        [InlineData("{\"nested\":\"json\"}", "JSON string")]
        public void Value_VariousTypes(string value, string scenario)
        {
            // Arrange & Act
            var field = new ExtractedField
            {
                FieldName = "TestField",
                Value = value
            };

            // Assert
            Assert.Equal(value, field.Value);
        }

        [Fact]
        public void ExtractedField_IndependentInstances()
        {
            // Arrange & Act
            var field1 = new ExtractedField
            {
                FieldName = "Field1",
                Value = "Value1",
                Confidence = 90.0
            };

            var field2 = new ExtractedField
            {
                FieldName = "Field2",
                Value = "Value2",
                Confidence = 85.0
            };

            // Assert
            Assert.NotEqual(field1.FieldName, field2.FieldName);
            Assert.NotEqual(field1.Value, field2.Value);
            Assert.NotEqual(field1.Confidence, field2.Confidence);
        }

        [Fact]
        public void ExtractedField_BoundingBoxCanBeNull()
        {
            // Arrange & Act
            var field = new ExtractedField
            {
                FieldName = "TestField",
                Value = "TestValue",
                Confidence = 95.0,
                BoundingBox = null
            };

            // Assert
            Assert.Null(field.BoundingBox);
        }

        [Fact]
        public void ExtractedField_BoundingBoxCanBeModified()
        {
            // Arrange
            var field = new ExtractedField
            {
                FieldName = "TestField",
                BoundingBox = new BoundingBox { X = 10, Y = 20 }
            };

            // Act
            field.BoundingBox.X = 50;
            field.BoundingBox.Y = 100;

            // Assert
            Assert.Equal(50, field.BoundingBox.X);
            Assert.Equal(100, field.BoundingBox.Y);
        }

        [Fact]
        public void ExtractedField_CollectionUsage()
        {
            // Arrange
            var fields = new List<ExtractedField>();

            // Act
            fields.Add(new ExtractedField { FieldName = "Name", Value = "John", Confidence = 95 });
            fields.Add(new ExtractedField { FieldName = "Age", Value = "30", Confidence = 90 });
            fields.Add(new ExtractedField { FieldName = "City", Value = "New York", Confidence = 88 });

            // Assert
            Assert.Equal(3, fields.Count);
            Assert.All(fields, f => Assert.NotNull(f.FieldName));
            Assert.All(fields, f => Assert.NotNull(f.Value));
            Assert.All(fields, f => Assert.True(f.Confidence > 0));
        }

        [Theory]
        [InlineData("SSN", "***-**-6789", 100.0, "Masked sensitive data")]
        [InlineData("CreditCard", "****-****-****-1234", 100.0, "Masked credit card")]
        [InlineData("Email", "user@example.com", 95.5, "Email field")]
        [InlineData("Phone", "+1-555-0123", 92.0, "Phone number")]
        public void ExtractedField_SensitiveData(string fieldName, string value, double confidence, string scenario)
        {
            // Arrange & Act
            var field = new ExtractedField
            {
                FieldName = fieldName,
                Value = value,
                Confidence = confidence
            };

            // Assert
            Assert.Equal(fieldName, field.FieldName);
            Assert.Equal(value, field.Value);
            Assert.Equal(confidence, field.Confidence);
        }

        [Fact]
        public void ExtractedField_WithMultiPageDocument()
        {
            // Arrange
            var fieldsAcrossPages = new List<ExtractedField>
            {
                new ExtractedField 
                { 
                    FieldName = "Page1Field",
                    Value = "Value1",
                    BoundingBox = new BoundingBox { PageNumber = 1 }
                },
                new ExtractedField 
                { 
                    FieldName = "Page2Field",
                    Value = "Value2",
                    BoundingBox = new BoundingBox { PageNumber = 2 }
                },
                new ExtractedField 
                { 
                    FieldName = "Page3Field",
                    Value = "Value3",
                    BoundingBox = new BoundingBox { PageNumber = 3 }
                }
            };

            // Assert
            Assert.Equal(3, fieldsAcrossPages.Count);
            Assert.Equal(1, fieldsAcrossPages[0].BoundingBox.PageNumber);
            Assert.Equal(2, fieldsAcrossPages[1].BoundingBox.PageNumber);
            Assert.Equal(3, fieldsAcrossPages[2].BoundingBox.PageNumber);
        }

        [Fact]
        public void ExtractedField_LargeValueHandling()
        {
            // Arrange
            var largeValue = string.Join("\n", Enumerable.Repeat("This is a line of text.", 100));

            // Act
            var field = new ExtractedField
            {
                FieldName = "LargeTextField",
                Value = largeValue,
                Confidence = 85.0
            };

            // Assert
            Assert.Equal(largeValue, field.Value);
            Assert.True(field.Value.Length > 2000);
        }

        [Theory]
        [InlineData(90.0, true, "High confidence field")]
        [InlineData(70.0, true, "Medium confidence field")]
        [InlineData(50.0, false, "Low confidence field")]
        [InlineData(30.0, false, "Very low confidence field")]
        public void ExtractedField_ConfidenceThreshold(double confidence, bool isReliable, string scenario)
        {
            // Arrange
            const double reliabilityThreshold = 60.0;
            
            // Act
            var field = new ExtractedField
            {
                FieldName = "TestField",
                Value = "TestValue",
                Confidence = confidence
            };

            var isFieldReliable = field.Confidence >= reliabilityThreshold;

            // Assert
            Assert.Equal(isReliable, isFieldReliable);
        }
    }
}