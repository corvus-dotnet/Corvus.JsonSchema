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
            _ => null,
        };
    }

    /// <inheritdoc/>
    public JsonValueKind? GetExpectedValueKind(string format)
    {
        if (this.GetDotnetTypeNameFor(format) is not null)
        {
            return JsonValueKind.Number;
        }

        return null;
    }
}