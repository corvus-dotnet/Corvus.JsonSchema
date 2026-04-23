// <copyright file="GenerateWithDriverCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Corvus.Json.CodeGenerator;
using Corvus.Text.Json.CodeGeneration;
using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for code generation.
/// </summary>
internal class GenerateWithDriverCommand : AsyncCommand<GenerateWithDriverCommand.Settings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.GenerationSpecificationFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling

        Engine engine = settings.GenerationEngine ?? CliDefaults.DefaultEngine;
        var config = GeneratorConfig.Parse(File.OpenRead(settings.GenerationSpecificationFile));
        return GenerationDriver.GenerateTypes(config, engine, settings.CodeGenerationMode, cancellationToken);
    }

    /// <summary>
    /// Settings for the generate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [Description("The path to the code generation specification file.")]
        [CommandArgument(0, "<generationSpecificationFile>")]
        [NotNull] // <> => NotNull
        public string? GenerationSpecificationFile { get; init; }

        [Description("The code generation engine to use. V4 uses Corvus.Json.ExtendedTypes. V5 uses Corvus.Text.Json. Default: V5 for corvusjson, V4 for generatejsonschematypes.")]
        [CommandOption("--engine")]
        public Engine? GenerationEngine { get; init; }

        [Description("The code generation mode. TypeGeneration emits strongly-typed C# types (default). SchemaEvaluationOnly emits a standalone evaluator for validation and annotation collection. Both emits both.")]
        [CommandOption("--codeGenerationMode")]
        [DefaultValue(CodeGenerationMode.TypeGeneration)]
        public CodeGenerationMode CodeGenerationMode { get; init; }
    }
}