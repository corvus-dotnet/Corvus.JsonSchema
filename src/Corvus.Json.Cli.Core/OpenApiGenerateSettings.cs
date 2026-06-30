// <copyright file="OpenApiGenerateSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Settings for the OpenAPI client code generation command.
/// </summary>
internal sealed class OpenApiGenerateSettings : OpenApiSettings
{
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

    [CommandOption("--spec-url")]
    [Description("The original URL of the API description. When set, the spec is fetched from this URL and stored locally. The URL is recorded in the lock file for update-style re-fetch.")]
    public string? SpecUrl { get; init; }

    [CommandOption("--ignoreEmptyFormUrlEncodedBody")]
    [Description("Treat form-urlencoded request bodies whose schema defines no properties as if the body were absent. Useful for real-world APIs (e.g. Stripe) that emit empty body definitions.")]
    public bool IgnoreEmptyFormUrlEncodedBody { get; init; }

    [CommandOption("--yaml")]
    [Description("Treat the input specification as YAML. If not specified, YAML is auto-detected from .yaml/.yml file extension.")]
    public bool? Yaml { get; init; }

    [CommandOption("--engine")]
    [Description("The code generation engine: 'V5' (default; idiomatic C#) or 'TypeScript' (idiomatic TypeScript client + models). V4 is not supported for OpenAPI client generation.")]
    public Engine? Engine { get; init; }

    [CommandOption("--tsRuntimeModule")]
    [Description("TypeScript engine only. The module specifier the generated models import the shared model runtime from. A relative specifier (e.g. './corvus-runtime.js') re-emits the runtime alongside the models; a bare package specifier imports the installed package and does not re-emit it.")]
    public string? TsRuntimeModule { get; init; }

    [CommandOption("--tsClientRuntimeModule")]
    [Description("TypeScript engine only. The module specifier the generated client imports the byte-native transport runtime from. Defaults to '@endjin/corvus-json-client-runtime'; supply a relative path to resolve the runtime from the working tree without an install step.")]
    public string? TsClientRuntimeModule { get; init; }

    [CommandOption("--tsModulePerType")]
    [Description("TypeScript engine only. Emit one model module per type plus a barrel index.ts instead of a single generated.ts.")]
    public bool? TsModulePerType { get; init; }
}

#endif