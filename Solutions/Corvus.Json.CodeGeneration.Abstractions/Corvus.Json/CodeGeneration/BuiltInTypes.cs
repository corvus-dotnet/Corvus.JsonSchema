// <copyright file="BuiltInTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Gets common type declarations.
/// </summary>
public static class BuiltInTypes
{
    /// <summary>
    /// The {}/true type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) AnyTypeDeclaration = ("Corvus.Json", "JsonAny");

    /// <summary>
    /// The not {}/false type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) NotAnyTypeDeclaration = ("Corvus.Json", "JsonNotAny");

    /// <summary>
    /// The null type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) NullTypeDeclaration = ("Corvus.Json", "JsonNull");

    /// <summary>
    /// The number type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) NumberTypeDeclaration = ("Corvus.Json", "JsonNumber");

    /// <summary>
    /// The integer type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) IntegerTypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// The array type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) ArrayTypeDeclaration = ("Corvus.Json", "JsonArray");

    /// <summary>
    /// The array type declaration.
    /// </summary>
    public static readonly (string Ns, string Type) ObjectTypeDeclaration = ("Corvus.Json", "JsonObject");

    /// <summary>
    /// A clr <see cref="sbyte"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrSByteTypeDeclaration = ("Corvus.Json", "JsonSByte");

    /// <summary>
    /// A clr <see cref="short"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt16TypeDeclaration = ("Corvus.Json", "JsonInt16");

    /// <summary>
    /// A clr <see cref="int"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt32TypeDeclaration = ("Corvus.Json", "JsonInt32");

    /// <summary>
    /// A clr <see cref="long"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt64TypeDeclaration = ("Corvus.Json", "JsonInt64");

#if NET8_0_OR_GREATER
    /// <summary>
    /// A clr <see cref="Int128"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt128TypeDeclaration = ("Corvus.Json", "JsonInt128");
#endif

    /// <summary>
    /// A clr <see cref="byte"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrByteTypeDeclaration = ("Corvus.Json", "JsonByte");

    /// <summary>
    /// A clr <see cref="ushort"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt16TypeDeclaration = ("Corvus.Json", "JsonUInt16");

    /// <summary>
    /// A clr <see cref="uint"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt32TypeDeclaration = ("Corvus.Json", "JsonUInt32");

    /// <summary>
    /// A clr <see cref="ulong"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt64TypeDeclaration = ("Corvus.Json", "JsonUInt64");

#if NET8_0_OR_GREATER
    /// <summary>
    /// A clr <see cref="UInt128"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt128TypeDeclaration = ("Corvus.Json", "JsonUInt128");

    /// <summary>
    /// A clr <see cref="Half"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrHalfTypeDeclaration = ("Corvus.Json", "JsonHalf");
#endif

    /// <summary>
    /// A clr <see cref="float"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrSingleTypeDeclaration = ("Corvus.Json", "JsonSingle");

    /// <summary>
    /// A clr <see cref="double"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDoubleTypeDeclaration = ("Corvus.Json", "JsonDouble");

    /// <summary>
    /// A clr <see cref="decimal"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDecimalTypeDeclaration = ("Corvus.Json", "JsonDecimal");

    /// <summary>
    /// A clr <see cref="string"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrStringTypeDeclaration = ("Corvus.Json", "JsonString");

    /// <summary>
    /// A clr <see cref="bool"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrBoolTypeDeclaration = ("Corvus.Json", "JsonBoolean");

    /// <summary>
    /// A clr <see cref="Guid"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrGuidTypeDeclaration = ("Corvus.Json", "JsonUuid");

    /// <summary>
    /// A clr <see cref="Uri"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUriTypeDeclaration = ("Corvus.Json", "JsonUri");

    /// <summary>
    /// A clr <see cref="Uri"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUriReferenceTypeDeclaration = ("Corvus.Json", "JsonUriReference");

#if NET8_0_OR_GREATER
    /// <summary>
    /// A clr <see cref="JsonUriTemplate"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUriTemplateTypeDeclaration = ("Corvus.Json", "JsonUriTemplate");
#endif

    /// <summary>
    /// A clr IRI type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrIriTypeDeclaration = ("Corvus.Json", "JsonIri");

    /// <summary>
    /// A clr JsonPointer type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrJsonPointerTypeDeclaration = ("Corvus.Json", "JsonPointer");

    /// <summary>
    /// A clr JsonRelativePointer type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrJsonRelativePointerTypeDeclaration = ("Corvus.Json", "JsonRelativePointer");

    /// <summary>
    /// A clr Regex type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrRegexTypeDeclaration = ("Corvus.Json", "JsonRegex");

    /// <summary>
    /// A clr IRI-Reference type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrIriReferenceTypeDeclaration = ("Corvus.Json", "JsonIriReference");

    /// <summary>
    /// A clr base64 encoded string type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrBase64StringTypeDeclaration = ("Corvus.Json", "JsonBase64String");

    /// <summary>
    /// A clr base64 encoded string type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrBase64StringTypeDeclarationPre201909 = ("Corvus.Json", "JsonBase64StringPre201909");

    /// <summary>
    /// A clr base64 encoded JsonDocument type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrBase64ContentTypeDeclaration = ("Corvus.Json", "JsonBase64Content");

    /// <summary>
    /// A clr base64 encoded JsonDocument type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrBase64ContentTypeDeclarationPre201909 = ("Corvus.Json", "JsonBase64ContentPre201909");

    /// <summary>
    /// A clr string type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrContentTypeDeclaration = ("Corvus.Json", "JsonContent");

    /// <summary>
    /// A clr string type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrContentTypeDeclarationPre201909 = ("Corvus.Json", "JsonContentPre201909");

    /// <summary>
    /// A clr NodaTime.LocalDate type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDateTypeDeclaration = ("Corvus.Json", "JsonDate");

    /// <summary>
    /// A clr NodaTime.OffsetDateTime type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDateTimeTypeDeclaration = ("Corvus.Json", "JsonDateTime");

    /// <summary>
    /// A clr NodaTime.Period type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDurationTypeDeclaration = ("Corvus.Json", "JsonDuration");

    /// <summary>
    /// A clr NodaTime.OffsetTime type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrTimeTypeDeclaration = ("Corvus.Json", "JsonTime");

    /// <summary>
    /// A clr <see cref="string"/> type that matches an email address.
    /// </summary>
    // language=regex
    public static readonly (string Ns, string Type) EmailTypeDeclaration = ("Corvus.Json", "JsonEmail");

    /// <summary>
    /// A clr <see cref="string"/> type that matches an idn-email address.
    /// </summary>
    // language=regex
    public static readonly (string Ns, string Type) IdnEmailTypeDeclaration = ("Corvus.Json", "JsonIdnEmail");

    /// <summary>
    /// A clr <see cref="string"/> type that matches a hostname address.
    /// </summary>
    // language=regex
    public static readonly (string Ns, string Type) HostnameTypeDeclaration = ("Corvus.Json", "JsonHostname");

    /// <summary>
    /// A clr <see cref="string"/> type that matches a hostname address.
    /// </summary>
    // language=regex
    public static readonly (string Ns, string Type) IdnHostnameTypeDeclaration = ("Corvus.Json", "JsonIdnHostname");

    /// <summary>
    /// A clr <see cref="string"/> type that matches a V4 IP address.
    /// </summary>
    // language=regex
    public static readonly (string Ns, string Type) IpV4TypeDeclaration = ("Corvus.Json", "JsonIpV4");

    /// <summary>
    /// A clr <see cref="string"/> type that matches a V6 IP address.
    /// </summary>
    // language=regex
    public static readonly (string Ns, string Type) IpV6TypeDeclaration = ("Corvus.Json", "JsonIpV6");

    /// <summary>
    /// Determines if the value is an integer based on the format.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format represents an integer.</returns>
    public static bool IsIntegerFormat(string? format)
    {
        return GetIntegerFor(format) is not null;
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to sbyte.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to sbyte.</returns>
    public static bool ImplicitConversionToSByte(string[] format)
    {
        return format.Contains("sbyte");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to byte.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to byte.</returns>
    public static bool ImplicitConversionToByte(string[] format)
    {
        return format.Contains("byte");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int16.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to int16.</returns>
    public static bool ImplicitConversionToInt16(string[] format)
    {
        return format.Contains("int16");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint16.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint16.</returns>
    public static bool ImplicitConversionToUInt16(string[] format)
    {
        return format.Contains("uint16");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int32.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to int32.</returns>
    public static bool ImplicitConversionToInt32(string[] format)
    {
        return format.Contains("int32");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint32.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint32.</returns>
    public static bool ImplicitConversionToUInt32(string[] format)
    {
        return format.Contains("uint32");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int64.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint64.</returns>
    public static bool ImplicitConversionToInt64(string[] format)
    {
        return format.Contains("int64");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint64.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint64.</returns>
    public static bool ImplicitConversionToUInt64(string[] format)
    {
        return format.Contains("uint64");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int128.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to int128.</returns>
    public static bool ImplicitConversionToInt128(string[] format)
    {
        return format.Contains("int128");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint128.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint128.</returns>
    public static bool ImplicitConversionToUInt128(string[] format)
    {
        return format.Contains("uint128");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to half.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to half.</returns>
    public static bool ImplicitConversionToHalf(string[] format)
    {
        return format.Contains("half");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to single.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to single.</returns>
    public static bool ImplicitConversionToSingle(string[] format)
    {
        return format.Contains("single");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to double.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to double.</returns>
    public static bool ImplicitConversionToDouble(string[] format)
    {
        return format.Contains("double");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to decimal.
    /// </summary>
    /// <param name="format">The format strings, or the empty array if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to decimal.</returns>
    public static bool ImplicitConversionToDecimal(string[] format)
    {
        return format.Contains("decimal");
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to sbyte.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to sbyte.</returns>
    public static bool ImplicitConversionToSByte(string? format)
    {
        return format is string f && ImplicitConversionToSByte([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to byte.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to byte.</returns>
    public static bool ImplicitConversionToByte(string? format)
    {
        return format is string f && ImplicitConversionToByte([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int16.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to int16.</returns>
    public static bool ImplicitConversionToInt16(string? format)
    {
        return format is string f && ImplicitConversionToInt16([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint16.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint16.</returns>
    public static bool ImplicitConversionToUInt16(string? format)
    {
        return format is string f && ImplicitConversionToUInt16([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int32.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to int32.</returns>
    public static bool ImplicitConversionToInt32(string? format)
    {
        return format is string f && ImplicitConversionToInt32([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint32.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint32.</returns>
    public static bool ImplicitConversionToUInt32(string? format)
    {
        return format is string f && ImplicitConversionToUInt32([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int64.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint64.</returns>
    public static bool ImplicitConversionToInt64(string? format)
    {
        return format is string f && ImplicitConversionToInt64([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint64.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint64.</returns>
    public static bool ImplicitConversionToUInt64(string? format)
    {
        return format is string f && ImplicitConversionToUInt64([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to int128.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to int128.</returns>
    public static bool ImplicitConversionToInt128(string? format)
    {
        return format is string f && ImplicitConversionToInt128([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to uint128.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to uint128.</returns>
    public static bool ImplicitConversionToUInt128(string? format)
    {
        return format is string f && ImplicitConversionToUInt128([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to half.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to half.</returns>
    public static bool ImplicitConversionToHalf(string? format)
    {
        return format is string f && ImplicitConversionToHalf([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to single.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to single.</returns>
    public static bool ImplicitConversionToSingle(string? format)
    {
        return format is string f && ImplicitConversionToSingle([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to double.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to double.</returns>
    public static bool ImplicitConversionToDouble(string? format)
    {
        return format is string f && ImplicitConversionToDouble([f]);
    }

    /// <summary>
    /// Determines if the format allows implicit conversion to decimal.
    /// </summary>
    /// <param name="format">The format string, or null if there is no format.</param>
    /// <returns><see langword="true"/> if the format allows implicit conversion to decimal.</returns>
    public static bool ImplicitConversionToDecimal(string? format)
    {
        return format is string f && ImplicitConversionToDecimal([f]);
    }

    /// <summary>
    /// Gets the built in type and namespace for the given type and optional format.
    /// </summary>
    /// <param name="type">The type for which to get the type declaration.</param>
    /// <param name="format">The format for which to get the type declaration.</param>
    /// <param name="contentEncoding">The content encoding for which to get the type declaration.</param>
    /// <param name="contentMediaType">The content media type for which to get the type declaration.</param>
    /// <param name="pre201909">Whether to get pre-201909 types.</param>
    /// <returns>A tuple of the namespace and type corresponding to the type and optional format.</returns>
    public static (string Ns, string Type) GetTypeNameFor(string? type, string? format, string? contentEncoding, string? contentMediaType, bool pre201909 = false)
    {
        return type switch
        {
            "null" => NullTypeDeclaration,
            "integer" => GetIntegerFor(format) ?? IntegerTypeDeclaration,
            "number" => GetNumberFor(format) ?? NumberTypeDeclaration,
            "boolean" => ClrBoolTypeDeclaration,
            "string" => GetStringFor(format, contentEncoding, contentMediaType, pre201909) ?? ClrStringTypeDeclaration,
            "array" => ArrayTypeDeclaration,
            "object" => ObjectTypeDeclaration,
            null => GetIntegerFor(format) ?? GetNumberFor(format) ?? GetStringFor(format, contentEncoding, contentMediaType) ?? throw new InvalidOperationException($"Unsupported format declaration {format}."),
            _ => throw new InvalidOperationException($"Unsupported type declaration {type}."),
        };
    }

    /// <summary>
    /// Gets the CSharp primitive type for the given numeric format.
    /// </summary>
    /// <param name="formats">The formats for the numeric primitive.</param>
    /// <returns>The CSharp primitive for the given format, or "double" if the format is not recognized.</returns>
    public static string GetCSharpPrimitiveForNumeric(string[] formats)
    {
        foreach (string format in formats)
        {
            if (GetNumberFor(format) is not null || GetIntegerFor(format) is not null)
            {
                return GetCSharpPrimitiveForNumeric(format);
            }
        }

        // Default to double
        return "double";
    }

    /// <summary>
    /// Gets the CSharp primitive type for the given numeric format.
    /// </summary>
    /// <param name="format">The format for the numeric primitive.</param>
    /// <returns>The CSharp primitive for the given format, or "double" if the format is not recognized.</returns>
    public static string GetCSharpPrimitiveForNumeric(string? format)
    {
        return format switch
        {
            "double" => "double",
            "decimal" => "decimal",
#if NET8_0_OR_GREATER
            "half" => "Half",
#endif
            "single" => "float",
            "byte" => "byte",
            "int16" => "short",
            "int32" => "int",
            "int64" => "long",
#if NET8_0_OR_GREATER
            "int128" => "Int128",
#endif
            "sbyte" => "sbyte",
            "uint16" => "ushort",
            "uint32" => "uint",
            "uint64" => "ulong",
#if NET8_0_OR_GREATER
            "uint128" => "UInt128",
#endif
            _ => "double",
        };
    }

    /// <summary>
    /// Gets a value indicating whether the format specified is for a string.
    /// </summary>
    /// <param name="format">The format to test.</param>
    /// <returns><c>True</c> if it is a string format.</returns>
    public static bool IsStringFormat(string format)
    {
        return GetStringFor(format, null, null) is not null;
    }

    private static (string Ns, string Type)? GetStringFor(string? format, string? contentEncoding, string? contentMediaType, bool pre201909 = false)
    {
        return format switch
        {
            "date" => ClrDateTypeDeclaration,
            "date-time" => ClrDateTimeTypeDeclaration,
            "time" => ClrTimeTypeDeclaration,
            "duration" => ClrDurationTypeDeclaration,
            "email" => EmailTypeDeclaration,
            "idn-email" => IdnEmailTypeDeclaration,
            "hostname" => HostnameTypeDeclaration,
            "idn-hostname" => IdnHostnameTypeDeclaration,
            "ipv4" => IpV4TypeDeclaration,
            "ipv6" => IpV6TypeDeclaration,
            "uuid" => ClrGuidTypeDeclaration,
            "uri" => ClrUriTypeDeclaration,
#if NET8_0_OR_GREATER
            "uri-template" => ClrUriTemplateTypeDeclaration,
#endif
            "uri-reference" => ClrUriReferenceTypeDeclaration,
            "iri" => ClrIriTypeDeclaration,
            "iri-reference" => ClrIriReferenceTypeDeclaration,
            "json-pointer" => ClrJsonPointerTypeDeclaration,
            "relative-json-pointer" => ClrJsonRelativePointerTypeDeclaration,
            "regex" => ClrRegexTypeDeclaration,
            _ => GetJsonStringFor(contentMediaType, contentEncoding, pre201909),
        };
    }

    private static (string Ns, string Type)? GetJsonStringFor(string? contentMediaType, string? contentEncoding, bool pre201909 = false)
    {
        return (contentMediaType, contentEncoding) switch
        {
            ("application/json", "base64") => pre201909 ? ClrBase64ContentTypeDeclarationPre201909 : ClrBase64ContentTypeDeclaration,
            (_, "base64") => pre201909 ? ClrBase64StringTypeDeclarationPre201909 : ClrBase64StringTypeDeclaration,
            ("application/json", null) => pre201909 ? ClrContentTypeDeclarationPre201909 : ClrContentTypeDeclaration,
            ////(null, null) => null,
            _ => null,
        };
    }

    private static (string Ns, string Type)? GetNumberFor(string? format)
    {
        return format switch
        {
            "double" => ClrDoubleTypeDeclaration,
            "decimal" => ClrDecimalTypeDeclaration,
#if NET8_0_OR_GREATER
            "half" => ClrHalfTypeDeclaration,
#endif
            "single" => ClrSingleTypeDeclaration,
            _ => null,
        };
    }

    private static (string Ns, string Type)? GetIntegerFor(string? format)
    {
        return format switch
        {
            "byte" => ClrByteTypeDeclaration,
            "int16" => ClrInt16TypeDeclaration,
            "int32" => ClrInt32TypeDeclaration,
            "int64" => ClrInt64TypeDeclaration,
#if NET8_0_OR_GREATER
            "int128" => ClrInt128TypeDeclaration,
#endif
            "sbyte" => ClrSByteTypeDeclaration,
            "uint16" => ClrUInt16TypeDeclaration,
            "uint32" => ClrUInt32TypeDeclaration,
            "uint64" => ClrUInt64TypeDeclaration,
#if NET8_0_OR_GREATER
            "uint128" => ClrUInt128TypeDeclaration,
#endif
            _ => null,
        };
    }
}