// <copyright file="OpenApiSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Shared settings for OpenAPI CLI commands.
/// </summary>
internal class OpenApiSettings : CommandSettings
{
    [Description("The path to the OpenAPI specification file (JSON or YAML).")]
    [CommandArgument(0, "<specFile>")]
    [NotNull]
    public string? SpecFile { get; init; }

    [CommandOption("--include-path")]
    [Description("Glob patterns for paths to include (e.g. /pets/**). Specify multiple times or comma-separate.")]
    public string[]? IncludePath { get; init; }

    [CommandOption("--exclude-path")]
    [Description("Glob patterns for paths to exclude (e.g. /admin/**). Specify multiple times or comma-separate.")]
    public string[]? ExcludePath { get; init; }

    [CommandOption("--filter")]
    [Description("(Deprecated: use --include-path) Glob patterns to filter which paths to include. Comma-separated or specify multiple times.")]
    public string[]? Filter { get; init; }

    [CommandOption("--specVersion")]
    [Description("The OpenAPI spec version to use (3.0 or 3.1). If not specified, auto-detected from the spec.")]
    [DefaultValue(null)]
    public string? SpecVersion { get; init; }

    [CommandOption("--group-by")]
    [Description("How to group operations in the output: 'path' (default) or 'tag'.")]
    [DefaultValue("path")]
    public string GroupBy { get; init; } = "path";
}

#endif