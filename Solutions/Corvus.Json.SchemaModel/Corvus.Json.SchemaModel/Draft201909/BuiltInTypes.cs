// <copyright file="BuiltInTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.SchemaModel.Draft201909
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Corvus.Json.Draft201909;

    /// <summary>
    /// Gets common type declarations.
    /// </summary>
    public static class BuiltInTypes
    {
        /// <summary>
        /// The {}/true type declaration.
        /// </summary>
        public static readonly (string ns, string type) AnyTypeDeclaration = ("Corvus.Json", "JsonAny");

        /// <summary>
        /// The not {}/false type declaration.
        /// </summary>
        public static readonly (string ns, string type) NotAnyTypeDeclaration = ("Corvus.Json", "JsonNotAny");

        /// <summary>
        /// Gets a type declaration instance for the Any type declaration.
        /// </summary>
        public static readonly TypeDeclaration AnyTypeDeclarationInstance = new (new Schema(true));

        /// <summary>
        /// Gets a type declaration instance for the NotAny type declaration.
        /// </summary>
        public static readonly TypeDeclaration NotAnyTypeDeclarationInstance = new (new Schema(false));

        /// <summary>
        /// The not {}/false type declaration.
        /// </summary>
        public static readonly (string ns, string type) NullTypeDeclaration = ("Corvus.Json", "JsonNull");

        /// <summary>
        /// The not {}/false type declaration.
        /// </summary>
        public static readonly (string ns, string type) NumberTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// The not {}/false type declaration.
        /// </summary>
        public static readonly (string ns, string type) IntegerTypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// The array type declaration.
        /// </summary>
        public static readonly (string ns, string type) ArrayTypeDeclaration = ("Corvus.Json", "JsonArray");

        /// <summary>
        /// The array type declaration.
        /// </summary>
        public static readonly (string ns, string type) ObjectTypeDeclaration = ("Corvus.Json", "JsonObject");

        /// <summary>
        /// A clr <see cref="int"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrInt32TypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// A clr <see cref="long"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrInt64TypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// A clr <see cref="float"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrFloatTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// A clr <see cref="double"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrDoubleTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// A clr <see cref="string"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrStringTypeDeclaration = ("Corvus.Json", "JsonString");

        /// <summary>
        /// A clr <see cref="bool"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrBoolTypeDeclaration = ("Corvus.Json", "JsonBoolean");

        /// <summary>
        /// A clr <see cref="Guid"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrGuidTypeDeclaration = ("Corvus.Json", "JsonUuid");

        /// <summary>
        /// A clr <see cref="Uri"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrUriTypeDeclaration = ("Corvus.Json", "JsonUri");

        /// <summary>
        /// A clr <see cref="Uri"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrUriReferenceTypeDeclaration = ("Corvus.Json", "JsonUriReference");

        /// <summary>
        /// A clr <see cref="Uri"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrUriTemplateTypeDeclaration = ("Corvus.Json", "JsonUriTemplate");

        /// <summary>
        /// A clr IRI type.
        /// </summary>
        public static readonly (string ns, string type) ClrIriTypeDeclaration = ("Corvus.Json", "JsonIri");

        /// <summary>
        /// A clr JsonPointer type.
        /// </summary>
        public static readonly (string ns, string type) ClrJsonPointerTypeDeclaration = ("Corvus.Json", "JsonPointer");

        /// <summary>
        /// A clr RelativeJsonPointer type.
        /// </summary>
        public static readonly (string ns, string type) ClrRelativeJsonPointerTypeDeclaration = ("Corvus.Json", "RelativeJsonPointer");

        /// <summary>
        /// A clr Regex type.
        /// </summary>
        public static readonly (string ns, string type) ClrRegexTypeDeclaration = ("Corvus.Json", "JsonRegex");

        /// <summary>
        /// A clr IRI-Reference type.
        /// </summary>
        public static readonly (string ns, string type) ClrIriReferenceTypeDeclaration = ("Corvus.Json", "JsonIriReference");

        /// <summary>
        /// A clr base64 encoded string type.
        /// </summary>
        public static readonly (string ns, string type) ClrBase64StringTypeDeclaration = ("Corvus.Json", "JsonBase64String");

        /// <summary>
        /// A clr base64 encoded JsonDocument type.
        /// </summary>
        public static readonly (string ns, string type) ClrBase64ContentTypeDeclaration = ("Corvus.Json", "JsonBase64Content");

        /// <summary>
        /// A clr string type.
        /// </summary>
        public static readonly (string ns, string type) ClrContentTypeDeclaration = ("Corvus.Json", "JsonContent");

        /// <summary>
        /// A clr <see cref="NodaTime.LocalDate"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrDateTypeDeclaration = ("Corvus.Json", "JsonDate");

        /// <summary>
        /// A clr <see cref="NodaTime.OffsetDateTime"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrDateTimeTypeDeclaration = ("Corvus.Json", "JsonDateTime");

        /// <summary>
        /// A clr <see cref="NodaTime.Period"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrDurationTypeDeclaration = ("Corvus.Json", "JsonDuration");

        /// <summary>
        /// A clr <see cref="NodaTime.OffsetTime"/> type.
        /// </summary>
        public static readonly (string ns, string type) ClrTimeTypeDeclaration = ("Corvus.Json", "JsonTime");

        /// <summary>
        /// A clr <see cref="string"/> type that matches an email address.
        /// </summary>
        // language=regex
        public static readonly (string ns, string type) EmailTypeDeclaration = ("Corvus.Json", "JsonEmail");

        /// <summary>
        /// A clr <see cref="string"/> type that matches an idn-email address.
        /// </summary>
        // language=regex
        public static readonly (string ns, string type) IdnEmailTypeDeclaration = ("Corvus.Json", "JsonIdnEmail");

        /// <summary>
        /// A clr <see cref="string"/> type that matches a hostname address.
        /// </summary>
        // language=regex
        public static readonly (string ns, string type) HostnameTypeDeclaration = ("Corvus.Json", "JsonHostname");

        /// <summary>
        /// A clr <see cref="string"/> type that matches a hostname address.
        /// </summary>
        // language=regex
        public static readonly (string ns, string type) IdnHostnameTypeDeclaration = ("Corvus.Json", "JsonIdnHostname");

        /// <summary>
        /// A clr <see cref="string"/> type that matches a V4 IP address.
        /// </summary>
        // language=regex
        public static readonly (string ns, string type) IpV4TypeDeclaration = ("Corvus.Json", "JsonIpV4");

        /// <summary>
        /// A clr <see cref="string"/> type that matches a V6 IP address.
        /// </summary>
        // language=regex
        public static readonly (string ns, string type) IpV6TypeDeclaration = ("Corvus.Json", "JsonIpV6");

        /// <summary>
        /// Gets the built in type and namespace for the given type and optional format.
        /// </summary>
        /// <param name="type">The type for which to get the type declaration.</param>
        /// <param name="format">The format for which to get the type declaration.</param>
        /// <param name="contentEncoding">The content encoding for which to get the type declaration.</param>
        /// <param name="contentMediaType">The content media type for which to get the type declaration.</param>
        /// <returns>A tuple of the namespace and type corresponding to the type and optional format.</returns>
        public static (string ns, string type) GetTypeNameFor(string? type, string? format, string? contentEncoding, string? contentMediaType)
        {
            return type switch
            {
                "null" => NullTypeDeclaration,
                "integer" => GetIntegerFor(format) ?? IntegerTypeDeclaration,
                "number" => GetNumberFor(format) ?? NumberTypeDeclaration,
                "boolean" => ClrBoolTypeDeclaration,
                "string" => GetStringFor(format, contentEncoding, contentMediaType) ?? ClrStringTypeDeclaration,
                "array" => ArrayTypeDeclaration,
                "object" => ObjectTypeDeclaration,
                null => GetIntegerFor(format) ?? GetNumberFor(format) ?? GetStringFor(format, contentEncoding, contentMediaType) ?? throw new InvalidOperationException($"Unsupported format declaration {format}."),
                _ => throw new InvalidOperationException($"Unsupported type declaration {type}."),
            };
        }

        /// <summary>
        /// Gets the conversions for a built-in type declaration.
        /// </summary>
        /// <param name="typeDeclaration">The built-in type declaration for which to get valid conversions.</param>
        /// <returns>The list of type declarations for the conversions.</returns>
        public static IEnumerable<TypeDeclaration> GetConversionsFor(TypeDeclaration typeDeclaration)
        {
            if (!typeDeclaration.IsBuiltInType)
            {
                throw new ArgumentException("The type declaration must be a built-in type.", nameof(typeDeclaration));
            }

            return typeDeclaration.FullyQualifiedDotnetTypeName switch
            {
                "Corvus.Json.JsonAny" => Enumerable.Empty<TypeDeclaration>(),
                "Corvus.Json.JsonNotAny" => Enumerable.Empty<TypeDeclaration>(),
                "Corvus.Json.JsonNull" => Enumerable.Empty<TypeDeclaration>(),
                "Corvus.Json.JsonNumber" => new[] { new TypeDeclaration(null, "int"), new TypeDeclaration(null, "long"), new TypeDeclaration(null, "double"), new TypeDeclaration(null, "float") },
                "Corvus.Json.JsonInteger" => new[] { new TypeDeclaration("Corvus.Json", "JsonNumber"), new TypeDeclaration(null, "int"), new TypeDeclaration(null, "long"), new TypeDeclaration(null, "double"), new TypeDeclaration(null, "float") },
                "Corvus.Json.JsonString" => new[] { new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonBoolean" => new[] { new TypeDeclaration(null, "bool") },
                "Corvus.Json.JsonUuid" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "Guid") },
                "Corvus.Json.JsonUri" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "Uri") },
                "Corvus.Json.JsonUriReference" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "Uri") },
                "Corvus.Json.JsonUriTemplate" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonIri" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("System", "Uri") },
                "Corvus.Json.JsonPointer" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.RelativeJsonPointer" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonRegex" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonIriReference" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("System", "Uri") },
                "Corvus.Json.JsonBase64String" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("System", "ReadOnlySpan<byte>"), new TypeDeclaration("System", "ReadOnlyMemory<byte>") },
                "Corvus.Json.JsonBase64Content" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("System.Text.Json", "JsonDocument") },
                "Corvus.Json.JsonContent" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("System.Text.Json", "JsonDocument") },
                "Corvus.Json.JsonDate" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("NodaTime", "LocalDate") },
                "Corvus.Json.JsonDateTime" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("NodaTime", "OffsetDateTime") },
                "Corvus.Json.JsonTime" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("NodaTime", "OffsetTime") },
                "Corvus.Json.JsonDuration" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>"), new TypeDeclaration("NodaTime", "Period") },
                "Corvus.Json.JsonEmail" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonIdnEmail" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonHostname" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonIdnHostname" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonIpV4" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                "Corvus.Json.JsonIpV6" => new[] { new TypeDeclaration("Corvus.Json", "JsonString"), new TypeDeclaration(null, "string"), new TypeDeclaration("System", "ReadOnlySpan<char>"), new TypeDeclaration("System", "ReadOnlyMemory<char>") },
                _ => throw new InvalidOperationException($"Unsupported built-in type {typeDeclaration.FullyQualifiedDotnetTypeName}"),
            };
        }

        /// <summary>
        /// Gets a value indicating whether the supplied format is a numeric format.
        /// </summary>
        /// <param name="format">The format to check.</param>
        /// <returns>True if this is a numeric format.</returns>
        public static bool IsNumericFormat(string format)
        {
            return (GetNumberFor(format) ?? GetIntegerFor(format)) is not null;
        }

        private static (string ns, string type)? GetStringFor(string? format, string? contentEcoding, string? contentMediaType)
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
                "relative-json-pointer" => ClrRelativeJsonPointerTypeDeclaration,
                "regex" => ClrRegexTypeDeclaration,
                _ => GetJsonStringFor(contentMediaType, contentEcoding),
            };
        }

        private static (string ns, string type)? GetJsonStringFor(string? contentMediaType, string? contentEncoding)
        {
            return (contentMediaType, contentEncoding) switch
            {
                ("application/json", "base64") => ClrBase64ContentTypeDeclaration,
                (_, "base64") => ClrBase64StringTypeDeclaration,
                ("application/json", null) => ClrContentTypeDeclaration,
                (null, null) => ClrStringTypeDeclaration,
                _ => null,
            };
        }

        private static (string ns, string type)? GetNumberFor(string? format)
        {
            return format switch
            {
                "single" => ClrFloatTypeDeclaration,
                "double" => ClrDoubleTypeDeclaration,
                _ => null,
            };
        }

        private static (string ns, string type)? GetIntegerFor(string? format)
        {
            return format switch
            {
                "int32" => ClrInt32TypeDeclaration,
                "int64" => ClrInt64TypeDeclaration,
                _ => null,
            };
        }
    }
}
