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
    public static readonly (string Ns, string Type) ClrSByteTypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="short"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt16TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="int"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt32TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="long"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt64TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="Int128"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrInt128TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="byte"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrByteTypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="ushort"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt16TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="uint"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt32TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="ulong"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt64TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="UInt128"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUInt128TypeDeclaration = ("Corvus.Json", "JsonInteger");

    /// <summary>
    /// A clr <see cref="Half"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrHalfTypeDeclaration = ("Corvus.Json", "JsonNumber");

    /// <summary>
    /// A clr <see cref="float"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrSingleTypeDeclaration = ("Corvus.Json", "JsonNumber");

    /// <summary>
    /// A clr <see cref="double"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDoubleTypeDeclaration = ("Corvus.Json", "JsonNumber");

    /// <summary>
    /// A clr <see cref="decimal"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrDecimalTypeDeclaration = ("Corvus.Json", "JsonNumber");

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

    /// <summary>
    /// A clr <see cref="Uri"/> type.
    /// </summary>
    public static readonly (string Ns, string Type) ClrUriTemplateTypeDeclaration = ("Corvus.Json", "JsonUriTemplate");

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
    /// Gets a value indiciating whether the format specified is for a string.
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
            "uri-template" => ClrUriTemplateTypeDeclaration,
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
            (null, null) => ClrStringTypeDeclaration,
            _ => null,
        };
    }

    private static (string Ns, string Type)? GetNumberFor(string? format)
    {
        return format switch
        {
            "double" => ClrDoubleTypeDeclaration,
            "decimal" => ClrDecimalTypeDeclaration,
            "half" => ClrHalfTypeDeclaration,
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
            "int128" => ClrInt128TypeDeclaration,
            "sbyte" => ClrSByteTypeDeclaration,
            "uint16" => ClrUInt16TypeDeclaration,
            "uint32" => ClrUInt32TypeDeclaration,
            "uint64" => ClrUInt64TypeDeclaration,
            "uint128" => ClrUInt128TypeDeclaration,

            _ => null,
        };
    }
}