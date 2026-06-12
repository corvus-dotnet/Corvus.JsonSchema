// <copyright file="SpecVersionHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// Detects the OpenAPI specification version from a spec document.
/// </summary>
public static class OpenApiSpecVersion
{
    /// <summary>
    /// Detects the OpenAPI spec version from the spec root, or uses the user-specified version.
    /// </summary>
    /// <param name="specRoot">The root JSON element of the spec.</param>
    /// <param name="userSpecVersion">The user-specified version, or <see langword="null"/> for auto-detection.</param>
    /// <returns>The spec version string ("3.0", "3.1", or "3.2").</returns>
    public static string Detect(JsonElement specRoot, string? userSpecVersion)
    {
        if (userSpecVersion is not null)
        {
            return userSpecVersion;
        }

        if (specRoot.TryGetProperty("openapi"u8, out JsonElement version)
            && version.ValueKind == JsonValueKind.String)
        {
            string? v = version.GetString();
            if (v?.StartsWith("3.0", StringComparison.Ordinal) == true)
            {
                return "3.0";
            }

            if (v?.StartsWith("3.2", StringComparison.Ordinal) == true)
            {
                return "3.2";
            }
        }

        return "3.1";
    }
}

/// <summary>
/// Detects the AsyncAPI specification version from a spec document.
/// </summary>
public static class AsyncApiSpecVersion
{
    /// <summary>
    /// Detects the AsyncAPI spec version from the spec root, or uses the explicitly supplied version.
    /// </summary>
    /// <param name="specRoot">The root JSON element of the spec.</param>
    /// <param name="explicitVersion">The user-specified version, or <see langword="null"/> for auto-detection.</param>
    /// <returns>The spec version string ("2.6", "3.0", or the raw value).</returns>
    public static string Detect(JsonElement specRoot, string? explicitVersion)
    {
        if (explicitVersion is not null)
        {
            return explicitVersion;
        }

        if (specRoot.TryGetProperty("asyncapi"u8, out JsonElement versionEl) &&
            versionEl.ValueKind == JsonValueKind.String)
        {
            string ver = versionEl.GetString()!;
            if (ver.StartsWith("3.0", StringComparison.Ordinal))
            {
                return "3.0";
            }

            if (ver.StartsWith("2.6", StringComparison.Ordinal))
            {
                return "2.6";
            }

            return ver;
        }

        return "3.0";
    }

    /// <summary>
    /// Determines whether the supplied version string denotes AsyncAPI 2.6.
    /// </summary>
    /// <param name="specVersion">The version string.</param>
    /// <returns><see langword="true"/> if the version is 2.6.</returns>
    public static bool Is26(string specVersion)
    {
        return specVersion.StartsWith("2.6", StringComparison.Ordinal) ||
            string.Equals(specVersion, "2.6", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the supplied version string denotes AsyncAPI 3.0.
    /// </summary>
    /// <param name="specVersion">The version string.</param>
    /// <returns><see langword="true"/> if the version is 3.0.</returns>
    public static bool Is30(string specVersion)
    {
        return specVersion.StartsWith("3.0", StringComparison.Ordinal) ||
            string.Equals(specVersion, "3.0", StringComparison.Ordinal);
    }
}

#endif