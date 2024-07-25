// <copyright file="WellKnownStringFormatProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Helpers for well-known string formats.
/// </summary>
public class WellKnownStringFormatProvider : IStringFormatProvider
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="WellKnownStringFormatProvider"/>.
    /// </summary>
    public static WellKnownStringFormatProvider Instance { get; } = new();

    /// <inheritdoc/>
    public bool AppendFormatAssertion(CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier)
    {
        switch (format)
        {
            case "date":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeDate(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "date-time":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeDateTime(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "time":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeTime(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "duration":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeDuration(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "email":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeEmail(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "idn-email":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeIdnEmail(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "hostname":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeHostname(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "idn-hostname":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeIdnHostName(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "ipv4":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeIpV4(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "ipv6":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeIpV6(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uuid":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUuid(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uri":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUri(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uri-template":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUriTemplate(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uri-reference":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUriReference(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "iri":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeIri(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "iri-reference":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeIriReference(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "json-pointer":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypePointer(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "relative-json-pointer":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeRelativePointer(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "regex":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeRegex(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-base64-content":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeBase64Content(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-base64-content-pre201909":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeBase64Content(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, false);");
                return true;
            case "corvus-base64-string":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeBase64String(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-base64-string-pre201909":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeBase64String(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, false);");
                return true;
            case "corvus-json-content":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeContent(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "corvus-json-content-pre201909":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeContent(",
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
    public string? GetDotnetTypeNameFor(string format)
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
        if (this.GetDotnetTypeNameFor(format) is not null)
        {
            return JsonValueKind.String;
        }

        return null;
    }
}