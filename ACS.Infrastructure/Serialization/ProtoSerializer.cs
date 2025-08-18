using System.Text.Json;
using ProtoBuf;

namespace ACS.Infrastructure;

/// <summary>
/// Binary serialization helper for protobuf serialization of domain commands and results
/// </summary>
public static class ProtoSerializer
{
    /// <summary>
    /// Serialize an object to binary protobuf format
    /// </summary>
    public static byte[] Serialize<T>(T obj)
    {
        if (obj == null)
            return Array.Empty<byte>();

        try
        {
            // Try protobuf-net serialization first
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, obj);
            return stream.ToArray();
        }
        catch
        {
            // Fallback to JSON serialization wrapped in binary
            // This ensures we can always serialize, even for types not decorated with ProtoContract
            var json = JsonSerializer.Serialize(obj);
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            
            // Add a marker byte to indicate JSON fallback
            var result = new byte[jsonBytes.Length + 1];
            result[0] = 0xFF; // Marker for JSON
            Array.Copy(jsonBytes, 0, result, 1, jsonBytes.Length);
            return result;
        }
    }

    /// <summary>
    /// Deserialize binary protobuf data to an object
    /// </summary>
    public static object? Deserialize(Type type, byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;

        try
        {
            // Check for JSON fallback marker
            if (data[0] == 0xFF)
            {
                // Deserialize from JSON
                var jsonBytes = new byte[data.Length - 1];
                Array.Copy(data, 1, jsonBytes, 0, jsonBytes.Length);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                return JsonSerializer.Deserialize(json, type);
            }

            // Try protobuf-net deserialization
            using var stream = new MemoryStream(data);
            return Serializer.Deserialize(type, stream);
        }
        catch
        {
            // If protobuf fails, try JSON deserialization
            var json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize(json, type);
        }
    }

    /// <summary>
    /// Deserialize binary protobuf data to a typed object
    /// </summary>
    public static T? Deserialize<T>(byte[] data)
    {
        var result = Deserialize(typeof(T), data);
        return result == null ? default : (T)result;
    }
}