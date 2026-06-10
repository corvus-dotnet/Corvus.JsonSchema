// <copyright file="ArazzoGenerateSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Settings for the <c>arazzo-generate</c> command.
/// </summary>
internal sealed class ArazzoGenerateSettings : ArazzoSettings
{
    [Description("The root namespace for the generated workflows, models, and OpenAPI clients.")]
    [CommandOption("--rootNamespace")]
    public string? RootNamespace { get; init; }

    [Description("The directory to write generated files to (defaults to ./Generated).")]
    [CommandOption("--outputPath")]
    public string? OutputPath { get; init; }

    [Description("The client name prefix for the generated OpenAPI clients.")]
    [CommandOption("--clientName")]
    public string? ClientName { get; init; }

    [Description("Generate durable executors (checkpoint & resume capable) that return WorkflowRunResult<TOutputs>.")]
    [CommandOption("--durable")]
    public bool Durable { get; init; }
}

#endif