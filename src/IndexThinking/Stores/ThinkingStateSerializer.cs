using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using IndexThinking.Core;

namespace IndexThinking.Stores;

/// <summary>
/// Serialization utilities for <see cref="ThinkingState"/>.
/// Useful for distributed storage backends (Redis, etc.).
/// </summary>
public static class ThinkingStateSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters =
        {
            new DateTimeOffsetConverter(),
            new ByteArrayConverter()
        }
    };

    /// <summary>
    /// Serializes a thinking state to JSON bytes.
    /// </summary>
    /// <param name="state">The state to serialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>UTF-8 encoded JSON bytes.</returns>
    public static byte[] Serialize(ThinkingState state, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.SerializeToUtf8Bytes(state, options ?? DefaultOptions);
    }

    /// <summary>
    /// Serializes a thinking state to a JSON string.
    /// </summary>
    /// <param name="state">The state to serialize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>JSON string.</returns>
    public static string SerializeToString(ThinkingState state, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserializes a thinking state from JSON bytes.
    /// </summary>
    /// <param name="data">UTF-8 encoded JSON bytes.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized state, or null if data is null/empty.</returns>
    public static ThinkingState? Deserialize(byte[]? data, JsonSerializerOptions? options = null)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ThinkingState>(data, options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserializes a thinking state from a JSON string.
    /// </summary>
    /// <param name="json">JSON string.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The deserialized state, or null if json is null/empty.</returns>
    public static ThinkingState? Deserialize(string? json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ThinkingState>(json, options ?? DefaultOptions);
    }

    /// <summary>
    /// Gets the default JSON serializer options used by this serializer.
    /// </summary>
    public static JsonSerializerOptions GetDefaultOptions() => DefaultOptions;

    /// <summary>
    /// Custom converter for DateTimeOffset that uses ISO 8601 format.
    /// </summary>
    private sealed class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("O"));
        }
    }

    /// <summary>
    /// Custom converter for byte[] that uses Base64 encoding.
    /// </summary>
    private sealed class ByteArrayConverter : JsonConverter<byte[]>
    {
        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var base64 = reader.GetString();
            return base64 is null ? null : Convert.FromBase64String(base64);
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }
}
