// <copyright file="AsyncApiGenerateSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Settings for the AsyncAPI code generation command.
/// </summary>
internal sealed class AsyncApiGenerateSettings : AsyncApiSettings
{
    [CommandOption("--rootNamespace")]
    [Description("The root namespace for generated types.")]
    public string? RootNamespace { get; init; }

    [CommandOption("--outputPath")]
    [Description("The path to which to write the generated code.")]
    public string? OutputPath { get; init; }

    [CommandOption("--mode")]
    [Description("Generation mode: producer, consumer, or both (default: both).")]
    [DefaultValue("both")]
    public string Mode { get; init; } = "both";

    [CommandOption("--force")]
    [Description("Force regeneration even if the lock file indicates no changes.")]
    public bool Force { get; init; }

    [CommandOption("--spec-url")]
    [Description("The original URL of the API description. When set, the spec is fetched from this URL and stored locally. The URL is recorded in the lock file for update-style re-fetch.")]
    public string? SpecUrl { get; init; }

    [CommandOption("--yaml")]
    [Description("Enable YAML support. When set, the spec file and any external references may be YAML, JSON, or a mixture. Auto-detected from .yaml/.yml extensions if not explicitly set.")]
    public bool? SupportYaml { get; init; }
}

#endif