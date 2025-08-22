namespace NoLock.Social.Core.Storage
{
    /// <summary>
    /// Interface for serializing and deserializing objects to/from byte arrays.
    /// </summary>
    /// <typeparam name="T">The type to serialize/deserialize</typeparam>
    public interface ISerializer<T>
    {
        /// <summary>
        /// Serializes an object to a byte array.
        /// </summary>
        /// <param name="value">The object to serialize</param>
        /// <returns>The serialized byte array</returns>
        byte[] Serialize(T value);

        /// <summary>
        /// Deserializes a byte array back to an object.
        /// </summary>
        /// <param name="data">The byte array to deserialize</param>
        /// <returns>The deserialized object</returns>
        T Deserialize(byte[] data);
    }
}