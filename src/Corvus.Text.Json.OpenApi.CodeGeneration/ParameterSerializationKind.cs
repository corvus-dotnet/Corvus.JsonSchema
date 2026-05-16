// <copyright file="ParameterSerializationKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Classifies how a parameter value should be serialized to UTF-8 bytes
/// in the generated client code.
/// </summary>
/// <remarks>
/// <para>
/// This classification is determined by the code generator using the typed
/// schema model (schema <c>type</c> + <c>format</c> keywords). It is
/// used to select the correct serialization strategy without
/// re-inspecting the raw JSON.
/// </para>
/// <para>
/// Numeric kinds mirror the .NET type names and match the formats
/// recognised by <c>WellKnownNumericFormatHandler</c>. The OpenAPI
/// standard format <c>float</c> maps to <see cref="Single"/>.
/// </para>
/// </remarks>
public enum ParameterSerializationKind
{
    /// <summary>
    /// String value: use <c>GetUtf8String()</c> to get unescaped UTF-8 bytes directly.
    /// </summary>
    String,

    /// <summary>
    /// Boolean value: use <c>TryFormat</c> with a 5-byte buffer.
    /// </summary>
    Boolean,

    /// <summary>
    /// Unsigned 8-bit integer (<c>format: byte</c>): max 3 digits.
    /// </summary>
    Byte,

    /// <summary>
    /// Unsigned 16-bit integer (<c>format: uint16</c>): max 5 digits.
    /// </summary>
    UInt16,

    /// <summary>
    /// Unsigned 32-bit integer (<c>format: uint32</c>): max 10 digits.
    /// </summary>
    UInt32,

    /// <summary>
    /// Unsigned 64-bit integer (<c>format: uint64</c>): max 20 digits.
    /// </summary>
    UInt64,

    /// <summary>
    /// Unsigned 128-bit integer (<c>format: uint128</c>): max 39 digits.
    /// </summary>
    UInt128,

    /// <summary>
    /// Signed 8-bit integer (<c>format: sbyte</c>): max 4 chars.
    /// </summary>
    SByte,

    /// <summary>
    /// Signed 16-bit integer (<c>format: int16</c>): max 6 chars.
    /// </summary>
    Int16,

    /// <summary>
    /// Signed 32-bit integer (<c>format: int32</c>): max 11 chars.
    /// </summary>
    Int32,

    /// <summary>
    /// Signed 64-bit integer (<c>format: int64</c>): max 20 chars.
    /// </summary>
    Int64,

    /// <summary>
    /// Signed 128-bit integer (<c>format: int128</c>): max 40 chars.
    /// </summary>
    Int128,

    /// <summary>
    /// 16-bit floating point (<c>format: half</c>).
    /// </summary>
    Half,

    /// <summary>
    /// 32-bit floating point (<c>format: single</c> or <c>float</c>).
    /// </summary>
    Single,

    /// <summary>
    /// 64-bit floating point (<c>format: double</c>).
    /// </summary>
    Double,

    /// <summary>
    /// 128-bit decimal (<c>format: decimal</c>).
    /// </summary>
    Decimal,

    /// <summary>
    /// Unbounded scalar number (no recognised format constraint): use
    /// <c>JsonMarshal.GetRawUtf8Value</c> to get the raw UTF-8 bytes.
    /// </summary>
    UnboundedNumber,

    /// <summary>
    /// Object value: serialize with style/explode rules, or write the JSON
    /// representation via <c>WriteTo(Utf8JsonWriter)</c> over the output buffer.
    /// </summary>
    Object,

    /// <summary>
    /// Array value: serialize with style/explode rules, or write the JSON
    /// representation via <c>WriteTo(Utf8JsonWriter)</c> over the output buffer.
    /// </summary>
    Array,
}