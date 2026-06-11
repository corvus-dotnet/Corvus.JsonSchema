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
/// When a schema is a local <c>$ref</c> (e.g. <c>{"$ref":"#/components/schemas/Foo"}</c>)
/// it has no <c>type</c> keyword of its own. The classifier resolves such references
/// against the supplied OpenAPI document root before inspecting <c>type</c>/<c>format</c>,
/// so a parameter referencing a named integer/array/object schema is classified by the
/// resolved schema rather than falling back to <see cref="ParameterSerializationKind.String"/>.
/// Nested sub-schemas (<c>items</c>, <c>additionalProperties</c>, <c>properties</c> values)
/// are resolved the same way.
/// </para>
/// <para>
/// No strings are allocated. All comparisons use <c>ValueEquals</c> on
/// UTF-8 byte sequences.
/// </para>
/// </remarks>
public static class SchemaClassifier
{
    /// <summary>
    /// The maximum number of transitive <c>$ref</c> hops to follow before giving up.
    /// Guards against reference cycles and pathologically deep chains.
    /// </summary>
    private const int MaxRefDepth = 32;

    /// <summary>
    /// Classifies a schema element's <c>type</c> and <c>format</c> keywords
    /// into a <see cref="ParameterSerializationKind"/>.
    /// </summary>
    /// <param name="schema">
    /// The schema element. Must be a valid JSON object. If the schema lacks
    /// a <c>type</c> keyword, or the type is unrecognised, returns
    /// <see cref="ParameterSerializationKind.String"/>.
    /// </param>
    /// <param name="documentRoot">
    /// The root element of the OpenAPI document, used to resolve local <c>$ref</c>
    /// schemas. When <see cref="JsonValueKind.Undefined"/> (the default), no
    /// reference resolution is performed and the schema is classified as-is.
    /// </param>
    /// <returns>The serialization kind for the parameter.</returns>
    public static ParameterSerializationKind Classify(JsonElement schema, JsonElement documentRoot = default)
    {
        schema = ResolveRef(schema, documentRoot);

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

    /// <summary>
    /// Resolves a (possibly chained) local <c>$ref</c> schema against the OpenAPI
    /// document root.
    /// </summary>
    /// <param name="schema">The schema element, which may be a <c>$ref</c> object.</param>
    /// <param name="documentRoot">
    /// The root element of the OpenAPI document. When
    /// <see cref="JsonValueKind.Undefined"/>, the input is returned unchanged.
    /// </param>
    /// <returns>
    /// The resolved schema element. If <paramref name="schema"/> is not a local
    /// <c>$ref</c>, or the reference cannot be resolved, the input is returned
    /// unchanged. Transitive references are followed up to <see cref="MaxRefDepth"/>
    /// hops to guard against cycles.
    /// </returns>
    /// <remarks>
    /// Only local JSON-pointer references of the form <c>"#/..."</c> are followed.
    /// Pointer tokens are unescaped per RFC 6901 (<c>~1</c>→<c>/</c>, <c>~0</c>→<c>~</c>).
    /// External references (anything not beginning with <c>#/</c>, or a bare <c>#</c>)
    /// are left unresolved.
    /// </remarks>
    public static JsonElement ResolveRef(JsonElement schema, JsonElement documentRoot)
    {
        if (documentRoot.ValueKind == JsonValueKind.Undefined
            || schema.ValueKind != JsonValueKind.Object)
        {
            return schema;
        }

        JsonElement current = schema;
        for (int depth = 0; depth < MaxRefDepth; depth++)
        {
            if (!current.TryGetProperty("$ref"u8, out JsonElement refElement)
                || refElement.ValueKind != JsonValueKind.String)
            {
                return current;
            }

            if (!TryResolvePointer(refElement, documentRoot, out JsonElement resolved))
            {
                // Unresolvable / external reference: return the $ref object unchanged.
                return current;
            }

            current = resolved;
        }

        // Depth cap reached (likely a cycle): return whatever we have.
        return current;
    }

    /// <summary>
    /// Classifies the element type for an array schema by inspecting its
    /// <c>items</c> sub-schema.
    /// </summary>
    /// <param name="schema">The array schema element.</param>
    /// <param name="documentRoot">
    /// The root element of the OpenAPI document, used to resolve local <c>$ref</c>
    /// schemas on the array itself and on its <c>items</c> sub-schema.
    /// </param>
    /// <returns>
    /// The serialization kind for the array element type.
    /// Returns <see cref="ParameterSerializationKind.String"/> if no
    /// <c>items</c> sub-schema is present.
    /// </returns>
    public static ParameterSerializationKind ClassifyArrayElement(JsonElement schema, JsonElement documentRoot = default)
    {
        schema = ResolveRef(schema, documentRoot);

        if (schema.TryGetProperty("items"u8, out JsonElement items)
            && items.ValueKind == JsonValueKind.Object)
        {
            return Classify(items, documentRoot);
        }

        return ParameterSerializationKind.String;
    }

    /// <summary>
    /// Classifies the value type for an object schema by inspecting its
    /// <c>additionalProperties</c> sub-schema.
    /// </summary>
    /// <param name="schema">The object schema element.</param>
    /// <param name="documentRoot">
    /// The root element of the OpenAPI document, used to resolve local <c>$ref</c>
    /// schemas on the object itself and on its <c>additionalProperties</c> sub-schema.
    /// </param>
    /// <returns>
    /// The serialization kind for the object value type.
    /// Returns <see cref="ParameterSerializationKind.String"/> if no
    /// <c>additionalProperties</c> sub-schema is present.
    /// </returns>
    public static ParameterSerializationKind ClassifyObjectValue(JsonElement schema, JsonElement documentRoot = default)
    {
        schema = ResolveRef(schema, documentRoot);

        if (schema.TryGetProperty("additionalProperties"u8, out JsonElement addlProps)
            && addlProps.ValueKind == JsonValueKind.Object)
        {
            return Classify(addlProps, documentRoot);
        }

        return ParameterSerializationKind.String;
    }

    /// <summary>
    /// Checks whether a schema classified as <see cref="ParameterSerializationKind.Object"/>
    /// or <see cref="ParameterSerializationKind.Array"/> contains nested composite types
    /// (objects or arrays within its property values or array items).
    /// </summary>
    /// <param name="schema">The schema element.</param>
    /// <param name="documentRoot">
    /// The root element of the OpenAPI document, used to resolve local <c>$ref</c>
    /// schemas on the schema itself and on its nested sub-schemas.
    /// </param>
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
    public static bool HasDeepNesting(JsonElement schema, JsonElement documentRoot = default)
    {
        schema = ResolveRef(schema, documentRoot);

        // For objects: check if any declared property has a composite schema.
        if (schema.TryGetProperty("properties"u8, out JsonElement properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in properties.EnumerateObject())
            {
                if (IsCompositeType(prop.Value, documentRoot))
                {
                    return true;
                }
            }
        }

        // For objects: check additionalProperties if it is a schema (not bool).
        if (schema.TryGetProperty("additionalProperties"u8, out JsonElement addlProps)
            && addlProps.ValueKind == JsonValueKind.Object
            && IsCompositeType(addlProps, documentRoot))
        {
            return true;
        }

        // For arrays: check if items schema is composite.
        if (schema.TryGetProperty("items"u8, out JsonElement items)
            && items.ValueKind == JsonValueKind.Object
            && IsCompositeType(items, documentRoot))
        {
            return true;
        }

        return false;
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

    private static bool IsCompositeType(JsonElement schema, JsonElement documentRoot)
    {
        schema = ResolveRef(schema, documentRoot);

        return schema.TryGetProperty("type"u8, out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            && (typeElement.ValueEquals("object"u8) || typeElement.ValueEquals("array"u8));
    }

    /// <summary>
    /// Resolves a single local JSON-pointer <c>$ref</c> (one hop) against the document root.
    /// </summary>
    /// <param name="refElement">The <c>$ref</c> string element.</param>
    /// <param name="documentRoot">The OpenAPI document root.</param>
    /// <param name="resolved">On success, the element the pointer addresses.</param>
    /// <returns>
    /// <see langword="true"/> if the reference is a local <c>#/...</c> pointer that
    /// resolves to an existing element; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryResolvePointer(JsonElement refElement, JsonElement documentRoot, out JsonElement resolved)
    {
        resolved = default;

        string reference = refElement.GetString() ?? string.Empty;

        // Only local fragment pointers of the form "#/..." are followed.
        if (reference.Length < 2 || reference[0] != '#' || reference[1] != '/')
        {
            return false;
        }

        JsonElement current = documentRoot;

        // Skip the leading "#/" then walk each "/"-separated token.
        int index = 2;
        int length = reference.Length;
        while (index <= length)
        {
            int slash = reference.IndexOf('/', index);
            string rawToken = slash < 0
                ? reference[index..]
                : reference[index..slash];

            string token = UnescapePointerToken(rawToken);

            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(token, out JsonElement next))
            {
                return false;
            }

            current = next;

            if (slash < 0)
            {
                break;
            }

            index = slash + 1;
        }

        resolved = current;
        return true;
    }

    /// <summary>
    /// Unescapes a single JSON-pointer reference token per RFC 6901
    /// (<c>~1</c>→<c>/</c>, <c>~0</c>→<c>~</c>).
    /// </summary>
    /// <param name="token">The raw (escaped) token.</param>
    /// <returns>The unescaped token.</returns>
    private static string UnescapePointerToken(string token)
    {
        if (token.IndexOf('~') < 0)
        {
            return token;
        }

        // Order matters: ~1 -> / must precede ~0 -> ~ to avoid double-unescaping.
        return token.Replace("~1", "/").Replace("~0", "~");
    }
}