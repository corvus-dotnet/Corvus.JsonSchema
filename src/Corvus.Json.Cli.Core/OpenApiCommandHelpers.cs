// <copyright file="OpenApiCommandHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Spectre.Console;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Shared helper methods for OpenAPI CLI commands.
/// </summary>
internal static class OpenApiCommandHelpers
{
    /// <summary>
    /// Builds an <see cref="OperationFilter"/> from the settings, merging <c>--filter</c>
    /// (deprecated) and <c>--include-path</c> / <c>--exclude-path</c>.
    /// </summary>
    /// <param name="settings">The command settings.</param>
    /// <returns>An <see cref="OperationFilter"/>, or <see langword="null"/> if no filters are specified.</returns>
    internal static OperationFilter? BuildFilter(OpenApiSettings settings)
    {
        List<string> includePatterns = [];
        List<string> excludePatterns = [];

        // --filter is the deprecated alias for --include-path
        if (settings.Filter is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] --filter is deprecated. Use --include-path instead.");
            foreach (string f in settings.Filter)
            {
                includePatterns.AddRange(f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (settings.IncludePath is { Length: > 0 })
        {
            foreach (string f in settings.IncludePath)
            {
                includePatterns.AddRange(f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (settings.ExcludePath is { Length: > 0 })
        {
            foreach (string f in settings.ExcludePath)
            {
                excludePatterns.AddRange(f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        if (includePatterns.Count == 0 && excludePatterns.Count == 0)
        {
            return null;
        }

        return new OperationFilter(
            includePatterns.Count > 0 ? includePatterns : null,
            excludePatterns.Count > 0 ? excludePatterns : null);
    }

    /// <summary>
    /// Detects the OpenAPI spec version from the spec root, or uses the user-specified version.
    /// </summary>
    /// <param name="specRoot">The root JSON element of the spec.</param>
    /// <param name="userSpecVersion">The user-specified version, or <see langword="null"/> for auto-detection.</param>
    /// <returns>The spec version string ("3.0" or "3.1").</returns>
    internal static string DetectSpecVersion(JsonElement specRoot, string? userSpecVersion)
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
        }

        return "3.1";
    }

    /// <summary>
    /// Gets the API title from the spec root.
    /// </summary>
    /// <param name="specRoot">The root JSON element of the spec.</param>
    /// <returns>The title, or <see langword="null"/> if not present.</returns>
    internal static string? GetTitle(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info)
            && info.TryGetProperty("title"u8, out JsonElement title)
            && title.ValueKind == JsonValueKind.String)
        {
            return title.GetString();
        }

        return null;
    }

    /// <summary>
    /// Gets the API version from the spec root.
    /// </summary>
    /// <param name="specRoot">The root JSON element of the spec.</param>
    /// <returns>The version, or <see langword="null"/> if not present.</returns>
    internal static string? GetVersion(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("info"u8, out JsonElement info)
            && info.TryGetProperty("version"u8, out JsonElement ver)
            && ver.ValueKind == JsonValueKind.String)
        {
            return ver.GetString();
        }

        return null;
    }
}

#endif