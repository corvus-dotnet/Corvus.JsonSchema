// <copyright file="SchemaClassifier.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Classifies a JSON Schema element for parameter serialization purposes.
/// </summary>
/// <remarks>
/// <para>
/// This classifier inspects the <c>type</c> and <c>format</c> keywords of a
/// JSON Schema element to determine the <see cref="ParameterSerializationKind"/>.
/// It is called by the spec walkers (OpenApi31, OpenApi30) during parameter
/// extraction, using the schema element obtained through typed model access.
/// </para>
/// <para>
/// No strings are allocated. All comparisons use <c>ValueEquals</c> on
/// UTF-8 byte sequences.
/// </para>
/// </remarks>
public static class SchemaClassifier
{
    /// <summary>
    /// Classifies a schema element's <c>type</c> and <c>format</c> keywords
    /// into a <see cref="ParameterSerializationKind"/>.
    /// </summary>
    /// <param name="schema">
    /// The schema element. Must be a valid JSON object. If the schema lacks
    /// a <c>type</c> keyword, or the type is unrecognised, returns
    /// <see cref="ParameterSerializationKind.String"/>.
    /// </param>
    /// <returns>The serialization kind for the parameter.</returns>
    public static ParameterSerializationKind Classify(JsonElement schema)
    {
        if (!schema.TryGetProperty("type"u8, out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            return ParameterSerializationKind.String;
        }

        if (typeElement.ValueEquals("string"u8))
        {
            return ParameterSerializationKind.String;
        }

        if (typeElement.ValueEquals("boolean"u8))
        {
            return ParameterSerializationKind.Boolean;
        }

        if (typeElement.ValueEquals("integer"u8))
        {
            return ClassifyIntegerFormat(schema);
        }

        if (typeElement.ValueEquals("number"u8))
        {
            return ClassifyNumberFormat(schema);
        }

        if (typeElement.ValueEquals("object"u8))
        {
            return ParameterSerializationKind.Object;
        }

        if (typeElement.ValueEquals("array"u8))
        {
            return ParameterSerializationKind.Array;
        }

        return ParameterSerializationKind.String;
    }

    /// <summary>
    /// Gets the maximum UTF-8 buffer size required to format a value of
    /// the given serialization kind.
    /// </summary>
    /// <param name="kind">The serialization kind.</param>
    /// <returns>
    /// The maximum number of bytes needed, or 0 for kinds that do not use
    /// fixed-size formatting (String, UnboundedNumber, Object, Array).
    /// </returns>
    public static int GetMaxFormattedSize(ParameterSerializationKind kind)
    {
        return kind switch
        {
            ParameterSerializationKind.Boolean => 5,
            ParameterSerializationKind.Byte => 3,
            ParameterSerializationKind.UInt16 => 5,
            ParameterSerializationKind.UInt32 => 10,
            ParameterSerializationKind.UInt64 => 20,
            ParameterSerializationKind.UInt128 => 39,
            ParameterSerializationKind.SByte => 4,
            ParameterSerializationKind.Int16 => 6,
            ParameterSerializationKind.Int32 => 11,
            ParameterSerializationKind.Int64 => 20,
            ParameterSerializationKind.Int128 => 40,
            ParameterSerializationKind.Half => 16,
            ParameterSerializationKind.Single => 32,
            ParameterSerializationKind.Double => 32,
            ParameterSerializationKind.Decimal => 32,
            _ => 0,
        };
    }

    /// <summary>
    /// Gets a value indicating whether this kind uses <c>TryFormat</c>
    /// for serialization (all bounded numeric types and boolean).
    /// </summary>
    /// <param name="kind">The serialization kind.</param>
    /// <returns><see langword="true"/> if the kind is formattable.</returns>
    public static bool IsFormattable(ParameterSerializationKind kind)
    {
        return kind is
            ParameterSerializationKind.Boolean or
            ParameterSerializationKind.Byte or
            ParameterSerializationKind.UInt16 or
            ParameterSerializationKind.UInt32 or
            ParameterSerializationKind.UInt64 or
            ParameterSerializationKind.UInt128 or
            ParameterSerializationKind.SByte or
            ParameterSerializationKind.Int16 or
            ParameterSerializationKind.Int32 or
            ParameterSerializationKind.Int64 or
            ParameterSerializationKind.Int128 or
            ParameterSerializationKind.Half or
            ParameterSerializationKind.Single or
            ParameterSerializationKind.Double or
            ParameterSerializationKind.Decimal;
    }

    private static ParameterSerializationKind ClassifyIntegerFormat(JsonElement schema)
    {
        if (schema.TryGetProperty("format"u8, out JsonElement fmt)
            && fmt.ValueKind == JsonValueKind.String)
        {
            if (fmt.ValueEquals("byte"u8))
            {
                return ParameterSerializationKind.Byte;
            }

            if (fmt.ValueEquals("uint16"u8))
            {
                return ParameterSerializationKind.UInt16;
            }

            if (fmt.ValueEquals("uint32"u8))
            {
                return ParameterSerializationKind.UInt32;
            }

            if (fmt.ValueEquals("uint64"u8))
            {
                return ParameterSerializationKind.UInt64;
            }

            if (fmt.ValueEquals("uint128"u8))
            {
                return ParameterSerializationKind.UInt128;
            }

            if (fmt.ValueEquals("sbyte"u8))
            {
                return ParameterSerializationKind.SByte;
            }

            if (fmt.ValueEquals("int16"u8))
            {
                return ParameterSerializationKind.Int16;
            }

            if (fmt.ValueEquals("int32"u8))
            {
                return ParameterSerializationKind.Int32;
            }

            if (fmt.ValueEquals("int64"u8))
            {
                return ParameterSerializationKind.Int64;
            }

            if (fmt.ValueEquals("int128"u8))
            {
                return ParameterSerializationKind.Int128;
            }
        }

        return ParameterSerializationKind.UnboundedNumber;
    }

    private static ParameterSerializationKind ClassifyNumberFormat(JsonElement schema)
    {
        if (schema.TryGetProperty("format"u8, out JsonElement fmt)
            && fmt.ValueKind == JsonValueKind.String)
        {
            if (fmt.ValueEquals("half"u8))
            {
                return ParameterSerializationKind.Half;
            }

            if (fmt.ValueEquals("single"u8) || fmt.ValueEquals("float"u8))
            {
                return ParameterSerializationKind.Single;
            }

            if (fmt.ValueEquals("double"u8))
            {
                return ParameterSerializationKind.Double;
            }

            if (fmt.ValueEquals("decimal"u8))
            {
                return ParameterSerializationKind.Decimal;
            }
        }

        return ParameterSerializationKind.UnboundedNumber;
    }

    /// <summary>
    /// Classifies the element type for an array schema by inspecting its
    /// <c>items</c> sub-schema.
    /// </summary>
    /// <param name="schema">The array schema element.</param>
    /// <returns>
    /// The serialization kind for the array element type.
    /// Returns <see cref="ParameterSerializationKind.String"/> if no
    /// <c>items</c> sub-schema is present.
    /// </returns>
    public static ParameterSerializationKind ClassifyArrayElement(JsonElement schema)
    {
        if (schema.TryGetProperty("items"u8, out JsonElement items)
            && items.ValueKind == JsonValueKind.Object)
        {
            return Classify(items);
        }

        return ParameterSerializationKind.String;
    }

    /// <summary>
    /// Classifies the value type for an object schema by inspecting its
    /// <c>additionalProperties</c> sub-schema.
    /// </summary>
    /// <param name="schema">The object schema element.</param>
    /// <returns>
    /// The serialization kind for the object value type.
    /// Returns <see cref="ParameterSerializationKind.String"/> if no
    /// <c>additionalProperties</c> sub-schema is present.
    /// </returns>
    public static ParameterSerializationKind ClassifyObjectValue(JsonElement schema)
    {
        if (schema.TryGetProperty("additionalProperties"u8, out JsonElement addlProps)
            && addlProps.ValueKind == JsonValueKind.Object)
        {
            return Classify(addlProps);
        }

        return ParameterSerializationKind.String;
    }

    /// <summary>
    /// Checks whether a schema classified as <see cref="ParameterSerializationKind.Object"/>
    /// or <see cref="ParameterSerializationKind.Array"/> contains nested composite types
    /// (objects or arrays within its property values or array items).
    /// </summary>
    /// <param name="schema">The schema element.</param>
    /// <returns>
    /// <see langword="true"/> if the schema has nested composite types whose behaviour
    /// is undefined under OpenAPI style serialization.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The OpenAPI specification states that behaviour is undefined for deeply nested
    /// objects and arrays in style serialization (query, path, header, cookie).
    /// When this method returns <see langword="true"/>, the code generator should emit
    /// a <c>#warning</c> directive to alert consumers.
    /// </para>
    /// </remarks>
    public static bool HasDeepNesting(JsonElement schema)
    {
        // For objects: check if any declared property has a composite schema.
        if (schema.TryGetProperty("properties"u8, out JsonElement properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in properties.EnumerateObject())
            {
                if (IsCompositeType(prop.Value))
                {
                    return true;
                }
            }
        }

        // For objects: check additionalProperties if it is a schema (not bool).
        if (schema.TryGetProperty("additionalProperties"u8, out JsonElement addlProps)
            && addlProps.ValueKind == JsonValueKind.Object
            && IsCompositeType(addlProps))
        {
            return true;
        }

        // For arrays: check if items schema is composite.
        if (schema.TryGetProperty("items"u8, out JsonElement items)
            && items.ValueKind == JsonValueKind.Object
            && IsCompositeType(items))
        {
            return true;
        }

        return false;
    }

    private static bool IsCompositeType(JsonElement schema)
    {
        return schema.TryGetProperty("type"u8, out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            && (typeElement.ValueEquals("object"u8) || typeElement.ValueEquals("array"u8));
    }
}