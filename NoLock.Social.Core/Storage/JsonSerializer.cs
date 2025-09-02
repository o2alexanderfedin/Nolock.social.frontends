using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// JSON serializer implementation using System.Text.Json.
    /// </summary>
    /// <typeparam name="T">The type to serialize/deserialize</typeparam>
    public class JsonSerializer<T> : ISerializer<T>
    {
        private readonly JsonSerializerOptions _options;

        /// <summary>
        /// Initializes a new instance of the JsonSerializer class with default options.
        /// </summary>
        public JsonSerializer() : this(GetDefaultOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the JsonSerializer class with custom options.
        /// </summary>
        /// <param name="options">Custom JSON serializer options</param>
        public JsonSerializer(JsonSerializerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public byte[] Serialize(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            try
            {
                var json = JsonSerializer.Serialize(value, _options);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to serialize object of type {typeof(T).Name}", ex);
            }
        }

        /// <inheritdoc/>
        public T Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            try
            {
                var json = Encoding.UTF8.GetString(data);
                var result = JsonSerializer.Deserialize<T>(json, _options);
                
                if (result == null)
                {
                    throw new InvalidOperationException($"Deserialization resulted in null for type {typeof(T).Name}");
                }

                return result;
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Failed to deserialize data to type {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Gets the default JSON serializer options.
        /// </summary>
        /// <returns>Default JSON serializer options</returns>
        private static JsonSerializerOptions GetDefaultOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };
        }
    }
}