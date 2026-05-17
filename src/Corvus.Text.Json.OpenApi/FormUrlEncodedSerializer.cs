// <copyright file="FormUrlEncodedSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Serializes a JSON object to <c>application/x-www-form-urlencoded</c> format.
/// </summary>
/// <remarks>
/// <para>
/// Per OAS §4.8.14.4, the default serialization for form-urlencoded content is:
/// </para>
/// <list type="bullet">
/// <item><description>Primitive properties (<c>string</c>, <c>number</c>, <c>integer</c>,
/// <c>boolean</c>): the value is percent-encoded.</description></item>
/// <item><description>Complex properties (<c>object</c>, <c>array</c>): the value is
/// JSON-stringified, then percent-encoded.</description></item>
/// </list>
/// <para>
/// This serializer applies default behavior (no Encoding Object overrides).
/// When an Encoding Object with <c>style</c>/<c>explode</c> is specified, the
/// generated code emits property-specific serialization instead.
/// </para>
/// </remarks>
public static class FormUrlEncodedSerializer
{
    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>application/x-www-form-urlencoded</c> format.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the encoded body to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(in T value, Stream output)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        using StreamWriter writer = new(output, new System.Text.UTF8Encoding(false), bufferSize: 256, leaveOpen: true);
        bool first = true;

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            if (!first)
            {
                writer.Write('&');
            }

            first = false;

            string name = property.Name;
            writer.Write(Uri.EscapeDataString(name));
            writer.Write('=');

            JsonElement propValue = property.Value;

            switch (propValue.ValueKind)
            {
                case JsonValueKind.String:
                    writer.Write(Uri.EscapeDataString(propValue.GetString()!));
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    writer.Write(Uri.EscapeDataString(propValue.ToString()));
                    break;

                case JsonValueKind.Null:
                    // Null values are encoded as empty strings.
                    break;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    // Complex values are JSON-stringified then percent-encoded.
                    writer.Write(Uri.EscapeDataString(propValue.ToString()));
                    break;

                default:
                    break;
            }
        }
    }
}