// <copyright file="PackageReferenceAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// CVJ025: Detects V4 Corvus.Json assembly references and recommends
/// replacing them with the V5 equivalents.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PackageReferenceAnalyzer : DiagnosticAnalyzer
{
    private static readonly Dictionary<string, string> s_v4ToV5PackageMap = new()
    {
        ["Corvus.Json.ExtendedTypes"] = "Corvus.Text.Json",
        ["Corvus.Json.JsonSchema.Draft201909"] = "Corvus.Text.Json",
        ["Corvus.Json.JsonSchema.Draft202012"] = "Corvus.Text.Json",
        ["Corvus.Json.Patch"] = "Corvus.Text.Json",
        ["Corvus.Json.SourceGeneratorTools"] = "Corvus.Text.Json.SourceGenerator",
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.PackageReferenceMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        foreach (MetadataReference reference in context.Compilation.References)
        {
            if (reference is not PortableExecutableReference peRef)
            {
                continue;
            }

            ISymbol? assemblySymbol = context.Compilation.GetAssemblyOrModuleSymbol(peRef);
            if (assemblySymbol is not IAssemblySymbol assembly)
            {
                continue;
            }

            string assemblyName = assembly.Name;

            if (s_v4ToV5PackageMap.TryGetValue(assemblyName, out string? v5Package))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.PackageReferenceMigration,
                    Location.None,
                    assemblyName,
                    v5Package));
            }
        }

        // The V4 source generator is a Roslyn analyzer, so it doesn't appear in
        // Compilation.References. Instead, detect it by looking for the attribute
        // it emits into the compilation: Corvus.Json.JsonSchemaTypeGeneratorAttribute.
        INamedTypeSymbol? v4GeneratorAttribute = context.Compilation
            .GetTypeByMetadataName("Corvus.Json.JsonSchemaTypeGeneratorAttribute");

        if (v4GeneratorAttribute is not null &&
            v4GeneratorAttribute.ContainingAssembly.Name == context.Compilation.AssemblyName)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.PackageReferenceMigration,
                Location.None,
                "Corvus.Json.SourceGenerator",
                "Corvus.Text.Json.SourceGenerator"));
        }
    }
}