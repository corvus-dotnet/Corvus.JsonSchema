// <copyright file="WellKnownStringFormatHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Helpers for well-known string formats.
/// </summary>
public static class WellKnownStringFormatHelpers
{
    /// <summary>
    /// Gets the .NET type name for the given candidate string format (e.g. JsonUuid, JsonIri etc).
    /// </summary>
    /// <param name="format">The candidate string format.</param>
    /// <returns>The corresponding .NET type name, or <see langword="null"/> if the format is not explicitly supported.</returns>
    public static string? GetDotnetTypeNameFor(string? format)
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
}