using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NoLock.Social.Core.Common.Guards
{
    /// <summary>
    /// Provides guard clauses to reduce null-checking boilerplate and improve code readability.
    /// </summary>
    public static class Guard
    {
        /// <summary>
        /// Throws ArgumentNullException if the value is null.
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value to check</param>
        /// <param name="parameterName">The name of the parameter (automatically captured)</param>
        /// <returns>The non-null value</returns>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        [return: NotNull]
        public static T AgainstNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
            where T : class
        {
            if (value is null)
            {
                throw new ArgumentNullException(parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentNullException if the value is null, with a custom message.
        /// </summary>
        [return: NotNull]
        public static T AgainstNull<T>([NotNull] T? value, string message, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
            where T : class
        {
            if (value is null)
            {
                if (string.IsNullOrEmpty(message))
                {
                    throw new ArgumentNullException(parameterName);
                }
                else
                {
                    throw new ArgumentNullException(parameterName, message);
                }
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentException if the string is null or empty.
        /// </summary>
        /// <param name="value">The string to check</param>
        /// <param name="parameterName">The name of the parameter (automatically captured)</param>
        /// <returns>The non-null, non-empty string</returns>
        /// <exception cref="ArgumentException">Thrown when string is null or empty</exception>
        [return: NotNull]
        public static string AgainstNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentException if the string is null or empty, with a custom message.
        /// </summary>
        [return: NotNull]
        public static string AgainstNullOrEmpty([NotNull] string? value, string message, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (string.IsNullOrEmpty(message))
                {
                    throw new ArgumentException("Value cannot be null or empty", parameterName);
                }
                else
                {
                    throw new ArgumentException(message, parameterName);
                }
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentException if the string is null, empty, or whitespace.
        /// </summary>
        [return: NotNull]
        public static string AgainstNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Value cannot be null, empty, or whitespace", parameterName);
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentException if the string is null, empty, or whitespace, with a custom message.
        /// </summary>
        [return: NotNull]
        public static string AgainstNullOrWhiteSpace([NotNull] string? value, string message, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (string.IsNullOrEmpty(message))
                {
                    throw new ArgumentException("Value cannot be null, empty, or whitespace", parameterName);
                }
                else
                {
                    throw new ArgumentException(message, parameterName);
                }
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the value is negative.
        /// </summary>
        public static int AgainstNegative(int value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value cannot be negative");
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the value is zero or negative.
        /// </summary>
        public static int AgainstZeroOrNegative(int value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be positive");
            }
            return value;
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the value is outside the specified range.
        /// </summary>
        public static int AgainstOutOfRange(int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {min} and {max}");
            }
            return value;
        }

        /// <summary>
        /// Throws InvalidOperationException if the condition is false.
        /// </summary>
        public static void AgainstInvalidOperation(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}