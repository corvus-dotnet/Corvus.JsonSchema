// <copyright file="OpenApiGenerateSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Settings for the OpenAPI client code generation command.
/// </summary>
internal sealed class OpenApiGenerateSettings : CommandSettings
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

    [CommandOption("--rootNamespace")]
    [Description("The root namespace for generated types.")]
    public string? RootNamespace { get; init; }

    [CommandOption("--outputPath")]
    [Description("The path to which to write the generated code.")]
    public string? OutputPath { get; init; }

    [CommandOption("--clientName")]
    [Description("The prefix for generated client type names. Defaults to the API title.")]
    public string? ClientName { get; init; }

    [CommandOption("--force")]
    [Description("Force regeneration even if the lock file indicates no changes.")]
    public bool Force { get; init; }
}

#endif