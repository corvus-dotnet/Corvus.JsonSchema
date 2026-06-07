// <copyright file="ArazzoSettings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Shared settings for Arazzo CLI commands.
/// </summary>
internal class ArazzoSettings : CommandSettings
{
    [Description("The path to the Arazzo workflow document (JSON or YAML).")]
    [CommandArgument(0, "<arazzoFile>")]
    [NotNull]
    public string? ArazzoFile { get; init; }
}

#endif