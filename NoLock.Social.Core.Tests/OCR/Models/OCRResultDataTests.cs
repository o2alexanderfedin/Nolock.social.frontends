using NoLock.Social.Core.OCR.Models;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the OCRResultData model and related classes.
    /// </summary>
    public class OCRResultDataTests
    {
        [Fact]
        public void OCRResultData_DefaultValues()
        {
            // Arrange & Act
            var resultData = new OCRResultData();

            // Assert
            Assert.Null(resultData.ExtractedText);
            Assert.Equal(0, resultData.ConfidenceScore);
            Assert.Null(resultData.DetectedLanguage);
            Assert.Equal(0, resultData.PageCount);
            Assert.Null(resultData.StructuredData);
            Assert.NotNull(resultData.ExtractedFields);
            Assert.Empty(resultData.ExtractedFields);
            Assert.Null(resultData.Metrics);
        }

        [Theory]
        [InlineData("Sample extracted text", 85.5, "en", 3)]
        [InlineData("", 0, "unknown", 0)]
        [InlineData("Multi-page document content", 92.3, "fr", 10)]
        public void OCRResultData_PropertiesSetCorrectly(
            string extractedText, 
            double confidence, 
            string language, 
            int pageCount)
        {
            // Arrange & Act
            var resultData = new OCRResultData
            {
                ExtractedText = extractedText,
                ConfidenceScore = confidence,
                DetectedLanguage = language,
                PageCount = pageCount,
                StructuredData = "{\"field\":\"value\"}"
            };

            // Assert
            Assert.Equal(extractedText, resultData.ExtractedText);
            Assert.Equal(confidence, resultData.ConfidenceScore);
            Assert.Equal(language, resultData.DetectedLanguage);
            Assert.Equal(pageCount, resultData.PageCount);
            Assert.Equal("{\"field\":\"value\"}", resultData.StructuredData);
        }

        [Fact]
        public void OCRResultData_ExtractedFieldsCollection()
        {
            // Arrange
            var resultData = new OCRResultData();
            var field1 = new ExtractedField 
            { 
                FieldName = "Name", 
                Value = "John Doe", 
                Confidence = 95.0 
            };
            var field2 = new ExtractedField 
            { 
                FieldName = "Date", 
                Value = "2024-01-15", 
                Confidence = 88.5 
            };

            // Act
            resultData.ExtractedFields.Add(field1);
            resultData.ExtractedFields.Add(field2);

            // Assert
            Assert.Equal(2, resultData.ExtractedFields.Count);
            Assert.Contains(field1, resultData.ExtractedFields);
            Assert.Contains(field2, resultData.ExtractedFields);
            Assert.Equal("Name", resultData.ExtractedFields[0].FieldName);
            Assert.Equal("Date", resultData.ExtractedFields[1].FieldName);
        }

        [Fact]
        public void ProcessingMetrics_DefaultValues()
        {
            // Arrange & Act
            var metrics = new ProcessingMetrics();

            // Assert
            Assert.Equal(0, metrics.ProcessingTimeMs);
            Assert.Equal(0, metrics.CharacterCount);
            Assert.Equal(0, metrics.WordCount);
            Assert.Equal(0, metrics.LineCount);
            Assert.Equal(0, metrics.ImageQualityScore);
        }

        [Theory]
        [InlineData(1500, 5000, 850, 45, 92.5)]
        [InlineData(0, 0, 0, 0, 0)]
        [InlineData(long.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, 100.0)]
        public void ProcessingMetrics_PropertiesSetCorrectly(
            long processingTime, 
            int charCount, 
            int wordCount, 
            int lineCount, 
            double imageQuality)
        {
            // Arrange & Act
            var metrics = new ProcessingMetrics
            {
                ProcessingTimeMs = processingTime,
                CharacterCount = charCount,
                WordCount = wordCount,
                LineCount = lineCount,
                ImageQualityScore = imageQuality
            };

            // Assert
            Assert.Equal(processingTime, metrics.ProcessingTimeMs);
            Assert.Equal(charCount, metrics.CharacterCount);
            Assert.Equal(wordCount, metrics.WordCount);
            Assert.Equal(lineCount, metrics.LineCount);
            Assert.Equal(imageQuality, metrics.ImageQualityScore);
        }

        [Fact]
        public void OCRResultData_WithCompleteMetrics()
        {
            // Arrange
            var metrics = new ProcessingMetrics
            {
                ProcessingTimeMs = 2500,
                CharacterCount = 3500,
                WordCount = 650,
                LineCount = 75,
                ImageQualityScore = 88.7
            };

            // Act
            var resultData = new OCRResultData
            {
                Metrics = metrics
            };

            // Assert
            Assert.NotNull(resultData.Metrics);
            Assert.Equal(2500, resultData.Metrics.ProcessingTimeMs);
            Assert.Equal(3500, resultData.Metrics.CharacterCount);
            Assert.Equal(650, resultData.Metrics.WordCount);
            Assert.Equal(75, resultData.Metrics.LineCount);
            Assert.Equal(88.7, resultData.Metrics.ImageQualityScore);
        }

        [Theory]
        [InlineData(0.0, "Zero confidence")]
        [InlineData(50.0, "Medium confidence")]
        [InlineData(100.0, "Perfect confidence")]
        [InlineData(150.0, "Over 100% (invalid but settable)")]
        [InlineData(-10.0, "Negative (invalid but settable)")]
        public void OCRResultData_ConfidenceScoreRanges(double confidence, string scenario)
        {
            // Arrange & Act
            var resultData = new OCRResultData
            {
                ConfidenceScore = confidence
            };

            // Assert
            Assert.Equal(confidence, resultData.ConfidenceScore);
        }

        [Fact]
        public void OCRResultData_ComplexStructuredData()
        {
            // Arrange
            var structuredJson = @"{
                ""invoice"": {
                    ""number"": ""INV-001"",
                    ""date"": ""2024-01-15"",
                    ""items"": [
                        {""name"": ""Item1"", ""price"": 100.00},
                        {""name"": ""Item2"", ""price"": 200.00}
                    ],
                    ""total"": 300.00
                }
            }";

            // Act
            var resultData = new OCRResultData
            {
                StructuredData = structuredJson
            };

            // Assert
            Assert.Equal(structuredJson, resultData.StructuredData);
        }

        [Theory]
        [InlineData("en", "English")]
        [InlineData("es", "Spanish")]
        [InlineData("fr", "French")]
        [InlineData("de", "German")]
        [InlineData("zh", "Chinese")]
        [InlineData("", "Empty language")]
        [InlineData(null, "Null language")]
        public void OCRResultData_VariousLanguages(string language, string scenario)
        {
            // Arrange & Act
            var resultData = new OCRResultData
            {
                DetectedLanguage = language
            };

            // Assert
            Assert.Equal(language, resultData.DetectedLanguage);
        }

        [Theory]
        [InlineData(1, "Single page")]
        [InlineData(10, "Multi-page document")]
        [InlineData(100, "Large document")]
        [InlineData(0, "No pages")]
        [InlineData(int.MaxValue, "Maximum pages")]
        public void OCRResultData_PageCountVariations(int pageCount, string scenario)
        {
            // Arrange & Act
            var resultData = new OCRResultData
            {
                PageCount = pageCount
            };

            // Assert
            Assert.Equal(pageCount, resultData.PageCount);
        }

        [Fact]
        public void OCRResultData_MultipleFieldsWithDifferentConfidences()
        {
            // Arrange
            var resultData = new OCRResultData();
            var fields = new[]
            {
                new ExtractedField { FieldName = "High", Value = "Value1", Confidence = 99.9 },
                new ExtractedField { FieldName = "Medium", Value = "Value2", Confidence = 75.0 },
                new ExtractedField { FieldName = "Low", Value = "Value3", Confidence = 45.5 },
                new ExtractedField { FieldName = "VeryLow", Value = "Value4", Confidence = 10.0 }
            };

            // Act
            foreach (var field in fields)
            {
                resultData.ExtractedFields.Add(field);
            }

            // Assert
            Assert.Equal(4, resultData.ExtractedFields.Count);
            Assert.All(resultData.ExtractedFields, field => Assert.NotNull(field.FieldName));
            Assert.All(resultData.ExtractedFields, field => Assert.NotNull(field.Value));
            
            // Verify confidences are preserved
            Assert.Equal(99.9, resultData.ExtractedFields[0].Confidence);
            Assert.Equal(75.0, resultData.ExtractedFields[1].Confidence);
            Assert.Equal(45.5, resultData.ExtractedFields[2].Confidence);
            Assert.Equal(10.0, resultData.ExtractedFields[3].Confidence);
        }

        [Fact]
        public void OCRResultData_ExtractedFieldsCanBeCleared()
        {
            // Arrange
            var resultData = new OCRResultData();
            resultData.ExtractedFields.Add(new ExtractedField { FieldName = "Test", Value = "Value" });
            resultData.ExtractedFields.Add(new ExtractedField { FieldName = "Test2", Value = "Value2" });

            // Act
            resultData.ExtractedFields.Clear();

            // Assert
            Assert.Empty(resultData.ExtractedFields);
        }

        [Fact]
        public void ProcessingMetrics_PerformanceCalculations()
        {
            // Arrange
            var metrics = new ProcessingMetrics
            {
                ProcessingTimeMs = 1500,
                CharacterCount = 3000,
                WordCount = 500
            };

            // Act
            var charactersPerSecond = (metrics.CharacterCount * 1000.0) / metrics.ProcessingTimeMs;
            var wordsPerSecond = (metrics.WordCount * 1000.0) / metrics.ProcessingTimeMs;

            // Assert
            Assert.Equal(2000.0, charactersPerSecond);
            Assert.Equal(333.33, wordsPerSecond, 2);
        }

        [Fact]
        public void OCRResultData_NullMetrics_HandledGracefully()
        {
            // Arrange
            var resultData = new OCRResultData
            {
                ExtractedText = "Some text",
                Metrics = null
            };

            // Act & Assert
            Assert.Null(resultData.Metrics);
            Assert.NotNull(resultData.ExtractedText);
        }

        [Fact]
        public void OCRResultData_EmptyExtractedFieldsList_IsValid()
        {
            // Arrange & Act
            var resultData = new OCRResultData();

            // Assert
            Assert.NotNull(resultData.ExtractedFields);
            Assert.Empty(resultData.ExtractedFields);
            Assert.IsType<List<ExtractedField>>(resultData.ExtractedFields);
        }
    }
}