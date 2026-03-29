// <copyright file="GenerationDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.CodeGenerator;
using Corvus.Json.Internal;
using Corvus.Text.Json.CodeGeneration;
using Spectre.Console;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Drives code generation from our command line model.
/// </summary>
public static class GenerationDriver
{
    internal static async Task<int> GenerateTypes(GeneratorConfig generatorConfig, Engine generationEngine, CodeGenerationMode codeGenerationMode, CancellationToken cancellationToken)
    {
        return generationEngine switch
        {
            Engine.V4 => await GenerationDriverV4.GenerateTypes(generatorConfig, cancellationToken).ConfigureAwait(false),
            Engine.V5 => await GenerationDriverV5.GenerateTypes(generatorConfig, codeGenerationMode, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Unsupported generation engine: {generationEngine}")
        };
    }
}