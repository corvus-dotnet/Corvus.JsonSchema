// <copyright file="BuiltInTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.JsonSchema.TypeBuilder.Draft202012
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Corvus.Json.JsonSchema.Draft202012;

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
        /// Gets a type declaration instance for the Any type declaration.
        /// </summary>
        public static readonly TypeDeclaration AnyTypeDeclarationInstance = new(new Schema(true));

        /// <summary>
        /// Gets a type declaration instance for the NotAny type declaration.
        /// </summary>
        public static readonly TypeDeclaration NotAnyTypeDeclarationInstance = new(new Schema(false));

        /// <summary>
        /// The not {}/false type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) NullTypeDeclaration = ("Corvus.Json", "JsonNull");

        /// <summary>
        /// The not {}/false type declaration.
        /// </summary>
        public static readonly (string Ns, string Type) NumberTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// The not {}/false type declaration.
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
        /// A clr <see cref="int"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrInt32TypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// A clr <see cref="long"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrInt64TypeDeclaration = ("Corvus.Json", "JsonInteger");

        /// <summary>
        /// A clr <see cref="float"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrFloatTypeDeclaration = ("Corvus.Json", "JsonNumber");

        /// <summary>
        /// A clr <see cref="double"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDoubleTypeDeclaration = ("Corvus.Json", "JsonNumber");

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
        /// A clr RelativeJsonPointer type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrRelativeJsonPointerTypeDeclaration = ("Corvus.Json", "RelativeJsonPointer");

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
        /// A clr base64 encoded JsonDocument type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrBase64ContentTypeDeclaration = ("Corvus.Json", "JsonBase64Content");

        /// <summary>
        /// A clr string type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrContentTypeDeclaration = ("Corvus.Json", "JsonContent");

        /// <summary>
        /// A clr <see cref="NodaTime.LocalDate"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDateTypeDeclaration = ("Corvus.Json", "JsonDate");

        /// <summary>
        /// A clr <see cref="NodaTime.OffsetDateTime"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDateTimeTypeDeclaration = ("Corvus.Json", "JsonDateTime");

        /// <summary>
        /// A clr <see cref="NodaTime.Period"/> type.
        /// </summary>
        public static readonly (string Ns, string Type) ClrDurationTypeDeclaration = ("Corvus.Json", "JsonDuration");

        /// <summary>
        /// A clr <see cref="NodaTime.OffsetTime"/> type.
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
        /// Gets the built in type and namespace for the given type and optional format.
        /// </summary>
        /// <param name="type">The type for which to get the type declaration.</param>
        /// <param name="format">The format for which to get the type declaration.</param>
        /// <param name="contentEncoding">The content encoding for which to get the type declaration.</param>
        /// <param name="contentMediaType">The content media type for which to get the type declaration.</param>
        /// <returns>A tuple of the namespace and type corresponding to the type and optional format.</returns>
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
                _ => throw new InvalidOperationException($"Unsupported type declaration {type}."),
            };
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
