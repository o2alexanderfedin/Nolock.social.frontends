using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Edge case and boundary condition tests for DocumentProcessingQueue.
    /// Focuses on error scenarios, boundary conditions, and unusual state combinations.
    /// </summary>
    public class DocumentProcessingQueueEdgeCaseTests : IDisposable
    {
        private readonly DocumentProcessingQueue _queue;
        private readonly OCRSubmissionRequest _sampleRequest;

        public DocumentProcessingQueueEdgeCaseTests()
        {
            _queue = new DocumentProcessingQueue();
            _sampleRequest = new OCRSubmissionRequest
            {
                DocumentType = DocumentType.Receipt,
                ImageData = System.Text.Encoding.UTF8.GetBytes("test-image-data")
            };
        }

        #region Metadata and Special Data Tests

        [Fact]
        public async Task EnqueueDocumentAsync_WithNullMetadata_HandlesGracefully()
        {
            // Arrange
            await _queue.StartAsync();

            // Act
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal, null);

            // Assert
            queueId.Should().NotBeNullOrEmpty();
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.Metadata.Should().BeEmpty();
        }

        [Fact]
        public async Task EnqueueDocumentAsync_WithEmptyMetadata_HandlesCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var emptyMetadata = new Dictionary<string, object>();

            // Act
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal, emptyMetadata);

            // Assert
            queueId.Should().NotBeNullOrEmpty();
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.Metadata.Should().NotBeNull();
            document.Metadata.Should().BeEmpty();
        }

        [Fact]
        public async Task EnqueueDocumentAsync_WithComplexMetadata_PreservesAllTypes()
        {
            // Arrange
            await _queue.StartAsync();
            var complexMetadata = new Dictionary<string, object>
            {
                ["StringValue"] = "test",
                ["IntValue"] = 42,
                ["BoolValue"] = true,
                ["DateValue"] = DateTime.UtcNow,
                ["NullValue"] = null,
                ["ArrayValue"] = new[] { 1, 2, 3 },
                ["NestedObject"] = new { Key = "Value" }
            };

            // Act
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal, complexMetadata);

            // Assert
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.Metadata.Should().ContainKey("StringValue");
            document.Metadata.Should().ContainKey("IntValue");
            document.Metadata.Should().ContainKey("BoolValue");
            document.Metadata.Should().ContainKey("NullValue");
            document.Metadata["StringValue"].Should().Be("test");
            document.Metadata["IntValue"].Should().Be(42);
        }

        #endregion

        #region Retry Logic Boundary Conditions

        [Fact]
        public async Task RetryDocumentAsync_WithProcessingStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
            });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _queue.RetryDocumentAsync(queueId));
        }

        [Fact]
        public async Task RetryDocumentAsync_WithCompletedStatus_ThrowsInvalidOperationException()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _queue.RetryDocumentAsync(queueId));
        }

        [Fact]
        public async Task RetryDocumentAsync_RaisesStatusChangedEvent()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Failed, null, "Test failure", "ERR_001", CancellationToken.None 
            });

            QueuedDocumentEventArgs eventArgs = null;
            _queue.ProcessingStatusChanged += (sender, args) => eventArgs = args;

            // Act
            await _queue.RetryDocumentAsync(queueId);

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs.Document.Status.Should().Be(QueuedDocumentStatus.Queued);
        }

        #endregion

        #region Clear Completed Documents Edge Cases

        [Fact]
        public async Task ClearCompletedDocumentsAsync_WithEmptyQueue_ReturnsZero()
        {
            // Arrange
            await _queue.StartAsync();

            // Act
            var result = await _queue.ClearCompletedDocumentsAsync();

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public async Task ClearCompletedDocumentsAsync_WithOnlyQueuedDocuments_ReturnsZero()
        {
            // Arrange
            await _queue.StartAsync();
            for (int i = 0; i < 5; i++)
            {
                await _queue.EnqueueDocumentAsync(_sampleRequest);
            }

            // Act
            var result = await _queue.ClearCompletedDocumentsAsync();

            // Assert
            result.Should().Be(0);
            _queue.QueueCount.Should().Be(5);
        }

        [Fact]
        public async Task ClearCompletedDocumentsAsync_WithMixedStatuses_OnlyRemovesCompleted()
        {
            // Arrange
            await _queue.StartAsync();
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var statuses = new[]
            {
                QueuedDocumentStatus.Queued,
                QueuedDocumentStatus.Processing,
                QueuedDocumentStatus.Completed,
                QueuedDocumentStatus.Failed,
                QueuedDocumentStatus.Cancelled,
                QueuedDocumentStatus.Completed
            };

            var queueIds = new List<string>();
            foreach (var status in statuses)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                queueIds.Add(queueId);
                
                if (status != QueuedDocumentStatus.Queued)
                {
                    await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                        queueId, status, null, null, null, CancellationToken.None 
                    });
                }
            }

            // Act
            var removedCount = await _queue.ClearCompletedDocumentsAsync();

            // Assert
            removedCount.Should().Be(4); // Completed, Failed, Cancelled, and another Completed
            var remainingDocs = await _queue.GetQueuedDocumentsAsync();
            remainingDocs.Should().HaveCount(2);
            remainingDocs.Should().OnlyContain(d => 
                d.Status == QueuedDocumentStatus.Queued || 
                d.Status == QueuedDocumentStatus.Processing);
        }

        #endregion

        #region OCR Status Update Tests

        [Fact]
        public async Task UpdateDocumentStatusAsync_WithCompleteOCRStatus_UpdatesAllFields()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var ocrStatus = new OCRStatusResponse
            {
                TrackingId = Guid.NewGuid().ToString(),
                Status = OCRProcessingStatus.Complete,
                ProgressPercentage = 100,
                ResultData = new OCRResultData
                {
                    ExtractedText = "Sample extracted text",
                    ConfidenceScore = 98.5,
                    Metrics = new ProcessingMetrics
                    {
                        ProcessingTimeMs = 250,
                        CharacterCount = 100,
                        WordCount = 20
                    }
                }
            };

            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act
            var result = await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Completed, ocrStatus, null, null, CancellationToken.None 
            });

            // Assert
            result.Should().BeTrue();
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.ProcessingResult.Should().NotBeNull();
            document.ProcessingResult.TrackingId.Should().Be(ocrStatus.TrackingId);
            document.ProcessingResult.ResultData.ConfidenceScore.Should().Be(98.5);
            document.ProcessingResult.ResultData.Metrics.ProcessingTimeMs.Should().Be(250);
        }

        [Fact]
        public async Task UpdateDocumentStatusAsync_WithPartialOCRStatus_HandlesNullFields()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var ocrStatus = new OCRStatusResponse
            {
                TrackingId = Guid.NewGuid().ToString(),
                Status = OCRProcessingStatus.Processing,
                ProgressPercentage = 50,
                ResultData = null // No result data yet
            };

            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act
            var result = await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Processing, ocrStatus, null, null, CancellationToken.None 
            });

            // Assert
            result.Should().BeTrue();
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.OcrStatus.Should().Be(OCRProcessingStatus.Processing);
            document.ProgressPercentage.Should().Be(50);
            document.ProcessingResult.Should().NotBeNull();
            document.ProcessingResult.ResultData.Should().BeNull();
        }

        #endregion

        #region Statistics Calculation Edge Cases

        [Fact]
        public async Task UpdateStatistics_WithZeroProcessingTime_HandlesCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.ProcessingTimeMs = 0;

            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });

            var stats = await _queue.GetStatisticsAsync();

            // Assert
            stats.AverageProcessingTimeMs.Should().Be(0);
            stats.TotalProcessed.Should().Be(1);
        }

        [Fact]
        public async Task UpdateStatistics_WithVeryLargeProcessingTime_HandlesCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.ProcessingTimeMs = int.MaxValue;

            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });

            var stats = await _queue.GetStatisticsAsync();

            // Assert
            stats.AverageProcessingTimeMs.Should().Be(int.MaxValue);
            stats.TotalProcessed.Should().Be(1);
        }

        [Fact]
        public async Task GetStatisticsAsync_ImmediatelyAfterStart_HasCorrectStartTime()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            var beforeStart = DateTime.UtcNow;
            
            // Act
            await queue.StartAsync();
            var stats = await queue.GetStatisticsAsync();
            var afterStart = DateTime.UtcNow;

            // Assert
            stats.StartedAt.Should().BeOnOrAfter(beforeStart);
            stats.StartedAt.Should().BeOnOrBefore(afterStart);
            stats.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            
            queue.Dispose();
        }

        #endregion

        #region Disposal and Cleanup Tests

        [Fact]
        public async Task Dispose_WithQueuedDocuments_CleansUpProperly()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            await queue.StartAsync();
            
            for (int i = 0; i < 10; i++)
            {
                await queue.EnqueueDocumentAsync(_sampleRequest);
            }

            // Act
            queue.Dispose();

            // Assert - All operations should throw ObjectDisposedException
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => queue.EnqueueDocumentAsync(_sampleRequest));
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => queue.GetQueuedDocumentsAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => queue.StartAsync());
        }

        [Fact]
        public async Task Dispose_DuringActiveOperation_HandlesGracefully()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            await queue.StartAsync();
            
            var enqueueTasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                enqueueTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await queue.EnqueueDocumentAsync(_sampleRequest);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected for operations after disposal
                    }
                }));
            }

            // Act - Dispose while operations are in progress
            await Task.Delay(10);
            queue.Dispose();

            // Assert - Should not throw, some operations might fail with ObjectDisposedException
            await Task.WhenAll(enqueueTasks);
        }

        #endregion

        #region GetNextDocumentForProcessing Edge Cases

        [Fact]
        public void GetNextDocumentForProcessing_WithOnlyProcessingDocuments_ReturnsNull()
        {
            // Arrange
            _queue.StartAsync().Wait();
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            // Add documents and mark them as processing
            for (int i = 0; i < 3; i++)
            {
                var queueId = _queue.EnqueueDocumentAsync(_sampleRequest).Result;
                var task = (Task)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
                });
                task.Wait();
            }

            var getMethod = typeof(DocumentProcessingQueue).GetMethod("GetNextDocumentForProcessing",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            var document = (QueuedDocument)getMethod.Invoke(_queue, null);

            // Assert
            document.Should().BeNull();
        }

        [Fact]
        public void GetNextDocumentForProcessing_WithMixedPrioritiesAndStatuses_ReturnsCorrectDocument()
        {
            // Arrange
            _queue.StartAsync().Wait();
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            // Add documents with different priorities and statuses
            var lowPriorityId = _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Low).Result;
            var highProcessingId = _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.High).Result;
            var criticalQueuedId = _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Critical).Result;
            var normalCompletedId = _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal).Result;
            
            // Mark some as processing/completed
            var task1 = (Task)updateMethod.Invoke(_queue, new object[] { 
                highProcessingId, QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
            });
            task1.Wait();
            var task2 = (Task)updateMethod.Invoke(_queue, new object[] { 
                normalCompletedId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });
            task2.Wait();

            var getMethod = typeof(DocumentProcessingQueue).GetMethod("GetNextDocumentForProcessing",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            var document = (QueuedDocument)getMethod.Invoke(_queue, null);

            // Assert
            document.Should().NotBeNull();
            document.QueueId.Should().Be(criticalQueuedId, "Should return highest priority queued document");
            document.Priority.Should().Be(QueuePriority.Critical);
        }

        #endregion

        #region Property Validation Tests

        [Theory]
        [InlineData(QueuedDocumentStatus.Queued, true, false, false, "queued document")]
        [InlineData(QueuedDocumentStatus.Processing, false, true, false, "processing document")]
        [InlineData(QueuedDocumentStatus.Completed, false, false, true, "completed document")]
        [InlineData(QueuedDocumentStatus.Failed, false, false, true, "failed document")]
        [InlineData(QueuedDocumentStatus.Cancelled, false, false, true, "cancelled document")]
        public async Task DocumentCounts_ReflectCorrectStatusCategories(
            QueuedDocumentStatus status,
            bool shouldBeInQueued,
            bool shouldBeInProcessing,
            bool shouldBeInCompleted,
            string scenario)
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            if (status != QueuedDocumentStatus.Queued)
            {
                var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, status, null, null, null, CancellationToken.None 
                });
            }

            // Act & Assert
            _queue.QueueCount.Should().Be(shouldBeInQueued ? 1 : 0, $"QueueCount for {scenario}");
            _queue.ProcessingCount.Should().Be(shouldBeInProcessing ? 1 : 0, $"ProcessingCount for {scenario}");
            _queue.CompletedCount.Should().Be(shouldBeInCompleted ? 1 : 0, $"CompletedCount for {scenario}");
        }

        #endregion

        public void Dispose()
        {
            _queue?.Dispose();
        }
    }
}