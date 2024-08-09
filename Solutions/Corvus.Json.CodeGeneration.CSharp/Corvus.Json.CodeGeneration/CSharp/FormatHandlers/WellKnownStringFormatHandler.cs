// <copyright file="WellKnownStringFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Handlers for well-known string formats.
/// </summary>
public class WellKnownStringFormatHandler : IStringFormatHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="WellKnownStringFormatHandler"/>.
    /// </summary>
    public static WellKnownStringFormatHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint Priority => 100_000;

    /// <inheritdoc/>
    public bool AppendFormatConstructors(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return format switch
        {
            "date" => generator.AppendDateFormatConstructors(typeDeclaration),
            "date-time" => generator.AppendDateTimeFormatConstructors(typeDeclaration),
            "time" => generator.AppendTimeFormatConstructors(typeDeclaration),
            "duration" => generator.AppendDurationFormatConstructors(typeDeclaration),
            "ipv4" => generator.AppendIpV4FormatConstructors(typeDeclaration),
            "ipv6" => generator.AppendIpV6FormatConstructors(typeDeclaration),
            "uuid" => generator.AppendUuidFormatConstructors(typeDeclaration),
            "uri" => generator.AppendUriFormatConstructors(typeDeclaration),
            "uri-reference" => generator.AppendUriReferenceFormatConstructors(typeDeclaration),
            "iri" => generator.AppendIriFormatConstructors(typeDeclaration),
            "iri-reference" => generator.AppendIriReferenceFormatConstructors(typeDeclaration),
            "regex" => generator.AppendRegexFormatConstructors(typeDeclaration),
            "corvus-json-content" => generator.AppendJsonContentFormatConstructors(typeDeclaration),
            "corvus-json-content-pre201909" => generator.AppendJsonContentFormatConstructors(typeDeclaration),
            "corvus-base64-content" => generator.AppendBase64ContentFormatConstructors(typeDeclaration),
            "corvus-base64-content-pre201909" => generator.AppendBase64ContentFormatConstructors(typeDeclaration),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool AppendFormatConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return format switch
        {
            "date" => generator.AppendDateFormatConversionOperators(typeDeclaration),
            "date-time" => generator.AppendDateTimeFormatConversionOperators(typeDeclaration),
            "time" => generator.AppendTimeFormatConversionOperators(typeDeclaration),
            "duration" => generator.AppendDurationFormatConversionOperators(typeDeclaration),
            "ipv4" => generator.AppendIpV4FormatConversionOperators(typeDeclaration),
            "ipv6" => generator.AppendIpV6FormatConversionOperators(typeDeclaration),
            "uuid" => generator.AppendUuidFormatConversionOperators(typeDeclaration),
            "uri" => generator.AppendUriFormatConversionOperators(typeDeclaration),
            "uri-reference" => generator.AppendUriReferenceFormatConversionOperators(typeDeclaration),
            "iri" => generator.AppendIriFormatConversionOperators(typeDeclaration),
            "iri-reference" => generator.AppendIriReferenceFormatConversionOperators(typeDeclaration),
            "regex" => generator.AppendRegexFormatConversionOperators(typeDeclaration),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool AppendFormatEqualsTBody(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return format switch
        {
            "date" => generator.AppendDateFormatEqualsTBody(typeDeclaration),
            "date-time" => generator.AppendDateTimeFormatEqualsTBody(typeDeclaration),
            "time" => generator.AppendTimeFormatEqualsTBody(typeDeclaration),
            "duration" => generator.AppendDurationFormatEqualsTBody(typeDeclaration),
            "uuid" => generator.AppendUuidFormatEqualsTBody(typeDeclaration),
            "corvus-json-content" => generator.AppendJsonContentFormatEqualsTBody(typeDeclaration),
            "corvus-json-content-pre201909" => generator.AppendJsonContentFormatEqualsTBody(typeDeclaration),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicStaticMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return format switch
        {
            "date" => generator.AppendDateFormatPublicMethods(typeDeclaration),
            "date-time" => generator.AppendDateTimeFormatPublicMethods(typeDeclaration),
            "time" => generator.AppendTimeFormatPublicMethods(typeDeclaration),
            "duration" => generator.AppendDurationFormatPublicMethods(typeDeclaration),
            "ipv4" => generator.AppendIpV4FormatPublicMethods(typeDeclaration),
            "ipv6" => generator.AppendIpV6FormatPublicMethods(typeDeclaration),
            "uuid" => generator.AppendUuidFormatPublicMethods(typeDeclaration),
            "uri" => generator.AppendUriFormatPublicMethods(typeDeclaration),
            "uri-reference" => generator.AppendUriReferenceFormatPublicMethods(typeDeclaration),
            "iri" => generator.AppendIriFormatPublicMethods(typeDeclaration),
            "iri-reference" => generator.AppendIriReferenceFormatPublicMethods(typeDeclaration),
            "uri-template" => generator.AppendUriTemplateFormatPublicMethods(typeDeclaration),
            "regex" => generator.AppendRegexFormatPublicMethods(typeDeclaration),
            "corvus-base64-string" => generator.AppendBase64StringFormatPublicMethods(typeDeclaration),
            "corvus-base64-string-pre201909" => generator.AppendBase64StringFormatPublicMethods(typeDeclaration),
            "corvus-json-content" => generator.AppendJsonContentFormatPublicMethods(typeDeclaration),
            "corvus-json-content-pre201909" => generator.AppendJsonContentFormatPublicMethods(typeDeclaration),
            "corvus-base64-content" => generator.AppendBase64ContentFormatPublicMethods(typeDeclaration),
            "corvus-base64-content-pre201909" => generator.AppendBase64ContentFormatPublicMethods(typeDeclaration),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool AppendFormatPrivateStaticMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPrivateMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicStaticProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatAssertion(CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier, bool includeType)
    {
        string validator = includeType ? "Validate" : "ValidateWithoutCoreType";

        switch (format)
        {
            case "date":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeDate(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "date-time":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeDateTime(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "time":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeTime(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "duration":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeDuration(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "email":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeEmail(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "idn-email":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeIdnEmail(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "hostname":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeHostname(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "idn-hostname":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeIdnHostName(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "ipv4":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeIpV4(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "ipv6":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeIpV6(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uuid":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeUuid(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uri":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeUri(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uri-template":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeUriTemplate(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uri-reference":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeUriReference(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "iri":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeIri(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "iri-reference":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeIriReference(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "json-pointer":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypePointer(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "relative-json-pointer":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeRelativePointer(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "regex":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeRegex(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-base64-content":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeBase64Content(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-base64-content-pre201909":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeBase64Content(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, false);");
                return true;
            case "corvus-base64-string":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeBase64String(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-base64-string-pre201909":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeBase64String(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, false);");
                return true;
            case "corvus-json-content":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeContent(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-json-content-pre201909":
                generator.AppendLineIndent(
                    "return Corvus.Json.",
                    validator,
                    ".TypeContent(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, false);");
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public string? GetCorvusJsonTypeNameFor(string format)
    {
        return format switch
        {
            "date" => "JsonDate",
            "date-time" => "JsonDateTime",
            "time" => "JsonTime",
            "duration" => "JsonDuration",
            "email" => "JsonEmail",
            "idn-email" => "JsonIdnEmail",
            "hostname" => "JsonHostname",
            "idn-hostname" => "JsonIdnHostname",
            "ipv4" => "JsonIpV4",
            "ipv6" => "JsonIpV6",
            "uuid" => "JsonUuid",
            "uri" => "JsonUri",
            "uri-template" => "JsonUriTemplate",
            "uri-reference" => "JsonUriReference",
            "iri" => "JsonIri",
            "iri-reference" => "JsonIriReference",
            "json-pointer" => "JsonPointer",
            "relative-json-pointer" => "JsonRelativePointer",
            "regex" => "JsonRegex",
            "corvus-base64-content" => "JsonBase64Content",
            "corvus-base64-content-pre201909" => "JsonBase64ContentPre201909",
            "corvus-base64-string" => "JsonBase64String",
            "corvus-base64-string-pre201909" => "JsonBase64StringPre201909",
            "corvus-json-content" => "JsonContent",
            "corvus-json-content-pre201909" => "JsonContentPre201909",
            _ => null,
        };
    }

    /// <inheritdoc/>
    public JsonValueKind? GetExpectedValueKind(string format)
    {
        if (this.GetCorvusJsonTypeNameFor(format) is not null)
        {
            return JsonValueKind.String;
        }

        return null;
    }
}