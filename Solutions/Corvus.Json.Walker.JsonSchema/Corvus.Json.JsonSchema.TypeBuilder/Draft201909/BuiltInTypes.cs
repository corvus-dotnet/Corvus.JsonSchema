// <copyright file="BuiltInTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.JsonSchema.TypeBuilder.Draft201909
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Corvus.Json.JsonSchema.Draft201909;

    /// <summary>
    /// Gets common Type declarations.
    /// </summary>
    public static class BuiltInTypes
    {
        /// <summary>
        /// The {}/true Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) AnyTypeDeclaration = ("Corvus.Json", "JsonAny");

        /// <summary>
        /// The not {}/false Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) NotAnyTypeDeclaration = ("Corvus.Json", "JsonNotAny");

        /// <summary>
        /// Gets a Type declaration instance for the Any Type declaration.
        /// </summary>
        public static readonly TypeDeclaration AnyTypeDeclarationInstance = new(new Schema(true));

        /// <summary>
        /// Gets a Type declaration instance for the NotAny Type declaration.
        /// </summary>
        public static readonly TypeDeclaration NotAnyTypeDeclarationInstance = new(new Schema(false));

        /// <summary>
        /// The not {}/false Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) NullTypeDeclaration = ("Corvus.Json", "JsonNull");

        /// <summary>
        /// The not {}/false Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) NumberTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// The not {}/false Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) IntegerTypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// The array Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) ArrayTypeDeclaration = ("Corvus.Json", "JsonArray");

        /// <summary>
        /// The array Type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) ObjectTypeDeclaration = ("Corvus.Json", "JsonObject");

        /// <summary>
        /// A clr <see cref="int"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrInt32TypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// A clr <see cref="long"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrInt64TypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// A clr <see cref="float"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrFloatTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// A clr <see cref="double"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDoubleTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// A clr <see cref="string"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrStringTypeDeclaration = ("Corvus.Json", "JsonString");

        /// <summary>
        /// A clr <see cref="bool"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrBoolTypeDeclaration = ("Corvus.Json", "JsonBoolean");

        /// <summary>
        /// A clr <see cref="Guid"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrGuidTypeDeclaration = ("Corvus.Json", "JsonUuid");

        /// <summary>
        /// A clr <see cref="Uri"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrUriTypeDeclaration = ("Corvus.Json", "JsonUri");

        /// <summary>
        /// A clr <see cref="Uri"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrUriReferenceTypeDeclaration = ("Corvus.Json", "JsonUriReference");

        /// <summary>
        /// A clr <see cref="Uri"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrUriTemplateTypeDeclaration = ("Corvus.Json", "JsonUriTemplate");

        /// <summary>
        /// A clr IRI Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrIriTypeDeclaration = ("Corvus.Json", "JsonIri");

        /// <summary>
        /// A clr JsonPointer Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrJsonPointerTypeDeclaration = ("Corvus.Json", "JsonPointer");

        /// <summary>
        /// A clr RelativeJsonPointer Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrRelativeJsonPointerTypeDeclaration = ("Corvus.Json", "RelativeJsonPointer");

        /// <summary>
        /// A clr Regex Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrRegexTypeDeclaration = ("Corvus.Json", "JsonRegex");

        /// <summary>
        /// A clr IRI-Reference Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrIriReferenceTypeDeclaration = ("Corvus.Json", "JsonIriReference");

        /// <summary>
        /// A clr base64 encoded string Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrBase64StringTypeDeclaration = ("Corvus.Json", "JsonBase64String");

        /// <summary>
        /// A clr base64 encoded JsonDocument Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrBase64ContentTypeDeclaration = ("Corvus.Json", "JsonBase64Content");

        /// <summary>
        /// A clr string Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrContentTypeDeclaration = ("Corvus.Json", "JsonContent");

        /// <summary>
        /// A clr <see cref="NodaTime.LocalDate"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDateTypeDeclaration = ("Corvus.Json", "JsonDate");

        /// <summary>
        /// A clr <see cref="NodaTime.OffsetDateTime"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDateTimeTypeDeclaration = ("Corvus.Json", "JsonDateTime");

        /// <summary>
        /// A clr <see cref="NodaTime.Period"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDurationTypeDeclaration = ("Corvus.Json", "JsonDuration");

        /// <summary>
        /// A clr <see cref="NodaTime.OffsetTime"/> Type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrTimeTypeDeclaration = ("Corvus.Json", "JsonTime");

        /// <summary>
        /// A clr <see cref="string"/> Type that matches an email address.
        /// </summary>
        // language=regex
        public static readonly (string Ns, string Type) EmailTypeDeclaration = ("Corvus.Json", "JsonEmail");

        /// <summary>
        /// A clr <see cref="string"/> Type that matches an idn-email address.
        /// </summary>
        // language=regex
        public static readonly (string Ns, string Type) IdnEmailTypeDeclaration = ("Corvus.Json", "JsonIdnEmail");

        /// <summary>
        /// A clr <see cref="string"/> Type that matches a hostname address.
        /// </summary>
        // language=regex
        public static readonly (string Ns, string Type) HostnameTypeDeclaration = ("Corvus.Json", "JsonHostname");

        /// <summary>
        /// A clr <see cref="string"/> Type that matches a hostname address.
        /// </summary>
        // language=regex
        public static readonly (string Ns, string Type) IdnHostnameTypeDeclaration = ("Corvus.Json", "JsonIdnHostname");

        /// <summary>
        /// A clr <see cref="string"/> Type that matches a V4 IP address.
        /// </summary>
        // language=regex
        public static readonly (string Ns, string Type) IpV4TypeDeclaration = ("Corvus.Json", "JsonIpV4");

        /// <summary>
        /// A clr <see cref="string"/> Type that matches a V6 IP address.
        /// </summary>
        // language=regex
        public static readonly (string Ns, string Type) IpV6TypeDeclaration = ("Corvus.Json", "JsonIpV6");

        /// <summary>
        /// Gets the built in Type and namespace for the given Type and optional format.
        /// </summary>
        /// <param name="type">The Type for which to get the Type declaration.</param>
        /// <param name="format">The format for which to get the Type declaration.</param>
        /// <param name="contentEncoding">The content encoding for which to get the Type declaration.</param>
        /// <param name="contentMediaType">The content media Type for which to get the Type declaration.</param>
        /// <returns>A tuple of the namespace and Type corresponding to the Type and optional format.</returns>
        public static (string Ns, string Type) GetTypeNameFor(string? type, string? format, string? contentEncoding, string? contentMediaType)
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
                _ => throw new InvalidOperationException($"Unsupported Type declaration {type}."),
            };
        }

        /// <summary>
        /// Gets the conversions for a built-in Type declaration.
        /// </summary>
        /// <param name="typeDeclaration">The built-in Type declaration for which to get valid conversions.</param>
        /// <returns>The list of Type declarations for the conversions.</returns>
        public static IEnumerable<TypeDeclaration> GetConversionsFor(TypeDeclaration typeDeclaration)
        {
            if (!typeDeclaration.IsBuiltInType)
            {
                throw new ArgumentException("The Type declaration must be a built-in Type.", nameof(typeDeclaration));
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
                _ => throw new InvalidOperationException($"Unsupported built-in Type {typeDeclaration.FullyQualifiedDotnetTypeName}"),
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

        private static (string Ns, string Type)? GetStringFor(string? format, string? contentEcoding, string? contentMediaType)
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

        private static (string Ns, string Type)? GetJsonStringFor(string? contentMediaType, string? contentEncoding)
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

        private static (string Ns, string Type)? GetNumberFor(string? format)
        {
            return format switch
            {
                "single" => ClrFloatTypeDeclaration,
                "double" => ClrDoubleTypeDeclaration,
                _ => null,
            };
        }

        private static (string Ns, string Type)? GetIntegerFor(string? format)
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
