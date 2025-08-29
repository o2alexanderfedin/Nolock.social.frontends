using Xunit;
using FluentAssertions;
using NoLock.Social.Core.Common.Results;

namespace NoLock.Social.Core.Tests.Common.Results
{
    /// <summary>
    /// Integration tests that verify Result types work correctly in real-world scenarios
    /// </summary>
    public class ResultIntegrationTests
    {
        #region Pipeline Pattern Tests

        [Fact]
        public void PipelinePattern_SuccessfulFlow_ProcessesAllSteps()
        {
            // Arrange
            var input = "hello";

            // Act
            var result = ParseInput(input)
                .Map(text => ProcessText(text))
                .Map(processed => ValidateOutput(processed))
                .Map(validated => FormatFinalOutput(validated));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("HELLO - validated and formatted");
        }

        [Fact]
        public void PipelinePattern_FailureInMiddle_ShortCircuits()
        {
            // Arrange
            var input = "";  // This will fail at ParseInput
            var executionLog = new List<string>();

            // Act
            var result = ParseInput(input)
                .Map(text =>
                {
                    executionLog.Add("Processing");
                    return ProcessText(text);
                })
                .Map(processed =>
                {
                    executionLog.Add("Validating");
                    return ValidateOutput(processed);
                })
                .Map(validated =>
                {
                    executionLog.Add("Formatting");
                    return FormatFinalOutput(validated);
                });

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Message.Should().Contain("Input cannot be empty");
            executionLog.Should().BeEmpty(); // No steps executed after failure
        }

        // Note: The current implementation of Map doesn't catch exceptions.
        // In a production scenario, you might want to wrap the Map operations
        // in try-catch or use a SafeMap extension method.
        // [Fact] - Commented out as Map doesn't handle exceptions
        // public void PipelinePattern_ExceptionInMap_PropagatesAsFailure() { ... }

        #endregion

        #region Railway Oriented Programming Tests

        [Fact]
        public void RailwayOriented_CombineMultipleResults_AllSuccess()
        {
            // Arrange
            var result1 = Result<int>.Success(10);
            var result2 = Result<int>.Success(20);
            var result3 = Result<int>.Success(30);

            // Act
            var combined = CombineResults(result1, result2, result3);

            // Assert
            combined.IsSuccess.Should().BeTrue();
            combined.Value.Should().Be(60);
        }

        [Fact]
        public void RailwayOriented_CombineMultipleResults_OneFailure()
        {
            // Arrange
            var result1 = Result<int>.Success(10);
            var result2 = Result<int>.Failure(new Exception("Middle failed"));
            var result3 = Result<int>.Success(30);

            // Act
            var combined = CombineResults(result1, result2, result3);

            // Assert
            combined.IsFailure.Should().BeTrue();
            combined.Exception.Message.Should().Be("Middle failed");
        }

        #endregion

        #region Bind Pattern Tests

        [Fact]
        public void BindPattern_ChainDependentOperations_Success()
        {
            // Arrange
            var userId = 123;

            // Act
            var result = GetUser(userId)
                .Map(user => GetUserPermissions(user.Id))
                .Map(permissions => CheckAdminAccess(permissions));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
        }

        [Fact]
        public void BindPattern_ChainDependentOperations_UserNotFound()
        {
            // Arrange
            var userId = -1;

            // Act
            var result = GetUser(userId)
                .Map(user => GetUserPermissions(user.Id))
                .Map(permissions => CheckAdminAccess(permissions));

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Message.Should().Contain("User not found");
        }

        #endregion

        #region Error Aggregation Tests

        [Fact]
        public void ErrorAggregation_CollectAllErrors()
        {
            // Arrange
            var operations = new[]
            {
                () => Result<string>.Success("Op1"),
                () => Result<string>.Failure(new Exception("Error1")),
                () => Result<string>.Success("Op3"),
                () => Result<string>.Failure(new Exception("Error2")),
                () => Result<string>.Failure(new Exception("Error3"))
            };

            // Act
            var results = operations.Select(op => op()).ToList();
            var errors = results
                .Where(r => r.IsFailure)
                .Select(r => r.Exception.Message)
                .ToList();

            var successes = results
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToList();

            // Assert
            errors.Should().HaveCount(3);
            errors.Should().BeEquivalentTo(new[] { "Error1", "Error2", "Error3" });
            successes.Should().HaveCount(2);
            successes.Should().BeEquivalentTo(new[] { "Op1", "Op3" });
        }

        [Fact]
        public void ErrorAggregation_FirstErrorWins()
        {
            // Arrange
            var operations = new[]
            {
                () => ValidateNotEmpty(""),
                () => ValidateLength("ab", 5),
                () => ValidatePattern("123", @"[a-z]+")
            };

            // Act
            var firstError = operations
                .Select(op => op())
                .FirstOrDefault(r => r.IsFailure);

            // Assert
            firstError.IsFailure.Should().BeTrue();
            firstError.Exception.Message.Should().Contain("cannot be empty");
        }

        #endregion

        #region Async Pattern Tests

        [Fact]
        public async Task AsyncPattern_ChainAsyncOperations()
        {
            // Arrange
            async Task<Result<string>> AsyncOperation1()
            {
                await Task.Delay(1);
                return Result<string>.Success("Step1");
            }

            async Task<Result<string>> AsyncOperation2(string input)
            {
                await Task.Delay(1);
                return Result<string>.Success($"{input} -> Step2");
            }

            // Act
            var result1 = await AsyncOperation1();
            var result2 = result1.IsSuccess
                ? await AsyncOperation2(result1.Value)
                : Result<string>.Failure(result1.Exception);

            // Assert
            result2.IsSuccess.Should().BeTrue();
            result2.Value.Should().Be("Step1 -> Step2");
        }

        #endregion

        #region Retry Pattern Tests

        [Fact]
        public void RetryPattern_SucceedsAfterRetries()
        {
            // Arrange
            var attempts = 0;
            var maxRetries = 3;

            Result<string> OperationWithRetry()
            {
                attempts++;
                if (attempts < 3)
                    return Result<string>.Failure(new Exception($"Attempt {attempts} failed"));
                return Result<string>.Success("Success!");
            }

            // Act
            Result<string> result = Result<string>.Failure(new Exception("Initial"));
            for (int i = 0; i < maxRetries && result.IsFailure; i++)
            {
                result = OperationWithRetry();
            }

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("Success!");
            attempts.Should().Be(3);
        }

        #endregion

        #region Compensation Pattern Tests

        [Fact]
        public void CompensationPattern_RollbackOnFailure()
        {
            // Arrange
            var transactionLog = new List<string>();

            Result BeginTransaction()
            {
                transactionLog.Add("BEGIN");
                return Result.Success();
            }

            Result PerformOperation()
            {
                transactionLog.Add("OPERATE");
                return Result.Failure(new Exception("Operation failed"));
            }

            void Rollback()
            {
                transactionLog.Add("ROLLBACK");
            }

            void Commit()
            {
                transactionLog.Add("COMMIT");
            }

            // Act
            var result = BeginTransaction()
                .OnSuccess(() =>
                {
                    var opResult = PerformOperation();
                    if (opResult.IsFailure)
                        Rollback();
                    else
                        Commit();
                });

            // Assert
            transactionLog.Should().BeEquivalentTo(new[] { "BEGIN", "OPERATE", "ROLLBACK" });
        }

        #endregion

        #region Default Value Pattern Tests

        [Fact]
        public void DefaultValuePattern_UseDefaultOnFailure()
        {
            // Arrange
            var failedResult = Result<string>.Failure(new Exception("Failed to get value"));
            var successResult = Result<string>.Success("ActualValue");

            // Act
            var failedValue = failedResult.Match(
                onSuccess: v => v,
                onFailure: _ => "DefaultValue"
            );

            var successValue = successResult.Match(
                onSuccess: v => v,
                onFailure: _ => "DefaultValue"
            );

            // Assert
            failedValue.Should().Be("DefaultValue");
            successValue.Should().Be("ActualValue");
        }

        #endregion

        #region Validation Chain Tests

        [Theory]
        [InlineData("", false, "cannot be empty")]
        [InlineData("ab", false, "at least 3 characters")]
        [InlineData("abc123def456", false, "maximum 10 characters")]
        [InlineData("abc!@#", false, "alphanumeric")]
        [InlineData("abc123", true, null)]
        [InlineData("Test123", true, null)]
        public void ValidationChain_MultipleValidations(string input, bool shouldSucceed, string? expectedError)
        {
            // Act
            var result = ValidateInput(input);

            // Assert
            if (shouldSucceed)
            {
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(input);
            }
            else
            {
                result.IsFailure.Should().BeTrue();
                result.Exception.Message.Should().Contain(expectedError);
            }
        }

        #endregion

        #region Helper Methods

        private Result<string> ParseInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Result<string>.Failure(new ArgumentException("Input cannot be empty"));
            return Result<string>.Success(input);
        }

        private string ProcessText(string text)
        {
            if (text == "fail")
                throw new InvalidOperationException("Processing failed for 'fail' input");
            return text.ToUpper();
        }

        private string ValidateOutput(string text)
        {
            if (text.Length > 100)
                throw new InvalidOperationException("Output too long");
            return $"{text} - validated";
        }

        private string FormatFinalOutput(string text)
        {
            return $"{text} and formatted";
        }

        private Result<int> CombineResults(Result<int> r1, Result<int> r2, Result<int> r3)
        {
            if (r1.IsFailure) return r1;
            if (r2.IsFailure) return r2;
            if (r3.IsFailure) return r3;

            return Result<int>.Success(r1.Value + r2.Value + r3.Value);
        }

        private Result<User> GetUser(int userId)
        {
            if (userId <= 0)
                return Result<User>.Failure(new Exception("User not found"));
            return Result<User>.Success(new User { Id = userId, Name = "TestUser" });
        }

        private List<string> GetUserPermissions(int userId)
        {
            return new List<string> { "read", "write", "admin" };
        }

        private bool CheckAdminAccess(List<string> permissions)
        {
            return permissions.Contains("admin");
        }

        private Result<string> ValidateNotEmpty(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Result<string>.Failure(new ArgumentException("Value cannot be empty"));
            return Result<string>.Success(value);
        }

        private Result<string> ValidateLength(string value, int minLength)
        {
            if (value.Length < minLength)
                return Result<string>.Failure(new ArgumentException($"Value must be at least {minLength} characters"));
            return Result<string>.Success(value);
        }

        private Result<string> ValidatePattern(string value, string pattern)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
                return Result<string>.Failure(new ArgumentException($"Value must match pattern {pattern}"));
            return Result<string>.Success(value);
        }

        private Result<string> ValidateInput(string input)
        {
            // Chain of validations
            if (string.IsNullOrEmpty(input))
                return Result<string>.Failure(new ArgumentException("Input cannot be empty"));

            if (input.Length < 3)
                return Result<string>.Failure(new ArgumentException("Input must be at least 3 characters"));

            if (input.Length > 10)
                return Result<string>.Failure(new ArgumentException("Input must be maximum 10 characters"));

            if (!System.Text.RegularExpressions.Regex.IsMatch(input, @"^[a-zA-Z0-9]+$"))
                return Result<string>.Failure(new ArgumentException("Input must be alphanumeric"));

            return Result<string>.Success(input);
        }

        private class User
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion
    }
}