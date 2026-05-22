// <copyright file="AsyncApiSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Shared settings for AsyncAPI CLI commands.
/// </summary>
internal class AsyncApiSettings : CommandSettings
{
    [Description("The path to the AsyncAPI specification file (JSON or YAML).")]
    [CommandArgument(0, "<specFile>")]
    [NotNull]
    public string? SpecFile { get; init; }

    [CommandOption("--include-channel")]
    [Description("Glob patterns for channels to include (e.g. user/**). Specify multiple times or comma-separate.")]
    public string[]? IncludeChannel { get; init; }

    [CommandOption("--exclude-channel")]
    [Description("Glob patterns for channels to exclude (e.g. internal/**). Specify multiple times or comma-separate.")]
    public string[]? ExcludeChannel { get; init; }

    [CommandOption("--specVersion")]
    [Description("The AsyncAPI spec version to use (2.6 or 3.0). If not specified, auto-detected from the spec.")]
    [DefaultValue(null)]
    public string? SpecVersion { get; init; }
}

#endif