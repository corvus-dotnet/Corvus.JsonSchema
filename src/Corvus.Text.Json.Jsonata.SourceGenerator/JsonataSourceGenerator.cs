// <copyright file="JsonataSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.Jsonata.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.Jsonata.SourceGenerator;

/// <summary>
/// Roslyn incremental source generator that produces a static <c>Evaluate</c> method
/// from a JSONata expression file referenced via <c>[JsonataExpression("path.jsonata")]</c>.
/// Custom function definitions from <c>.jfn</c> files are discovered automatically.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsonataSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        EmitAttribute(context);

        // Discover all additional text files that end with .jsonata
        IncrementalValuesProvider<AdditionalText> jsonataFiles =
            context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith(".jsonata", StringComparison.OrdinalIgnoreCase));

        // Collect into an immutable array so we can look up by path
        IncrementalValueProvider<ImmutableArray<AdditionalText>> collectedFiles =
            jsonataFiles.Collect();

        // Discover all .jfn files for custom function definitions
        IncrementalValuesProvider<AdditionalText> jfnFiles =
            context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith(".jfn", StringComparison.OrdinalIgnoreCase));

        IncrementalValueProvider<ImmutableArray<AdditionalText>> collectedJfnFiles =
            jfnFiles.Collect();

        // Find partial classes/structs annotated with [JsonataExpression]
        IncrementalValuesProvider<GenerationSpec> specs =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Text.Json.Jsonata.JsonataExpressionAttribute",
                predicate: IsValidTarget,
                transform: ExtractSpec);

        // Combine each spec with all available .jsonata and .jfn files
        IncrementalValuesProvider<(GenerationSpec Spec, ImmutableArray<AdditionalText> Files, ImmutableArray<AdditionalText> JfnFiles)> combined =
            specs
                .Combine(collectedFiles)
                .Combine(collectedJfnFiles)
                .Select(static (pair, _) => (pair.Left.Left, pair.Left.Right, pair.Right));

        context.RegisterSourceOutput(combined, Execute);
    }

    private static void EmitAttribute(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "JsonataExpressionAttribute.g.cs",
                SourceText.From(
                    """
                    using System;

                    namespace Corvus.Text.Json.Jsonata;

                    /// <summary>
                    /// Marks a partial type for JSONata source generation.
                    /// The source generator reads the referenced JSONata expression file and emits
                    /// a static <c>Evaluate</c> method on the type.
                    /// </summary>
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
                    internal sealed class JsonataExpressionAttribute : Attribute
                    {
                        /// <summary>
                        /// Initializes a new instance of the <see cref="JsonataExpressionAttribute"/> class.
                        /// </summary>
                        /// <param name="expressionFile">
                        /// The path to the JSONata expression file, relative to the project directory.
                        /// The file must be included as an <c>AdditionalFiles</c> item in the project.
                        /// </param>
                        public JsonataExpressionAttribute(string expressionFile)
                        {
                            this.ExpressionFile = expressionFile;
                        }

                        /// <summary>
                        /// Gets the path to the JSONata expression file.
                        /// </summary>
                        public string ExpressionFile { get; }
                    }
                    """,
                    Encoding.UTF8));
        });
    }

    private static bool IsValidTarget(SyntaxNode node, CancellationToken token)
    {
        return node is TypeDeclarationSyntax typeDecl &&
               typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword) &&
               typeDecl.Parent is (FileScopedNamespaceDeclarationSyntax or NamespaceDeclarationSyntax or CompilationUnitSyntax);
    }

    private static GenerationSpec ExtractSpec(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        AttributeData attr = context.Attributes[0];
        string expressionFile = attr.ConstructorArguments[0].Value as string ?? string.Empty;
        string namespaceName = context.TargetSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : context.TargetSymbol.ContainingNamespace.ToDisplayString();
        string typeName = context.TargetSymbol.Name;
        bool isPublic = context.TargetSymbol.DeclaredAccessibility == Accessibility.Public;

        return new GenerationSpec(expressionFile, namespaceName, typeName, isPublic);
    }

    private static void Execute(
        SourceProductionContext context,
        (GenerationSpec Spec, ImmutableArray<AdditionalText> Files, ImmutableArray<AdditionalText> JfnFiles) input)
    {
        GenerationSpec spec = input.Spec;
        ImmutableArray<AdditionalText> files = input.Files;

        // Find the matching .jsonata file
        AdditionalText? matchingFile = null;
        foreach (AdditionalText file in files)
        {
            if (file.Path.EndsWith(spec.ExpressionFile, StringComparison.OrdinalIgnoreCase) ||
                file.Path.Replace('\\', '/').EndsWith(spec.ExpressionFile.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                matchingFile = file;
                break;
            }
        }

        if (matchingFile is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JASG001",
                    "Expression file not found",
                    "JSONata expression file '{0}' was not found in AdditionalFiles. Add <AdditionalFiles Include=\"{0}\" /> to your project.",
                    "Corvus.Text.Json.Jsonata",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile));
            return;
        }

        string? expression = matchingFile.GetText(context.CancellationToken)?.ToString()?.Trim();

        if (string.IsNullOrEmpty(expression))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JASG002",
                    "Empty expression file",
                    "JSONata expression file '{0}' is empty.",
                    "Corvus.Text.Json.Jsonata",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile));
            return;
        }

        // Parse custom functions from .jfn files
        IReadOnlyList<CustomFunction>? customFunctions = ParseJfnFiles(context, input.JfnFiles);

        try
        {
            string generated = JsonataCodeGenerator.Generate(expression!, spec.TypeName, spec.NamespaceName, customFunctions);

            // Transform the standalone static class into a partial type body
            string source = TransformToPartialType(generated, spec);

            context.AddSource($"{spec.TypeName}.Jsonata.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JASG003",
                    "Code generation failed",
                    "JSONata code generation failed for '{0}': {1}",
                    "Corvus.Text.Json.Jsonata",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile,
                ex.Message));
        }
    }

    private static IReadOnlyList<CustomFunction>? ParseJfnFiles(
        SourceProductionContext context,
        ImmutableArray<AdditionalText> jfnFiles)
    {
        if (jfnFiles.IsDefaultOrEmpty)
        {
            return null;
        }

        List<CustomFunction> allFunctions = new();
        foreach (AdditionalText jfnFile in jfnFiles)
        {
            string? content = jfnFile.GetText(context.CancellationToken)?.ToString();
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            try
            {
                IReadOnlyList<CustomFunction> fns = JfnParser.Parse(content!);
                allFunctions.AddRange(fns);
            }
            catch (FormatException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "JASG004",
                        "Custom function parse error",
                        "Failed to parse .jfn file '{0}': {1}",
                        "Corvus.Text.Json.Jsonata",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    jfnFile.Path,
                    ex.Message));
            }
        }

        return allFunctions.Count > 0 ? allFunctions : null;
    }

    /// <summary>
    /// Transforms the output of <see cref="JsonataCodeGenerator.Generate(string, string, string)"/> from a standalone
    /// static class into a partial type body matching the user's declaration.
    /// </summary>
    private static string TransformToPartialType(string generated, GenerationSpec spec)
    {
        string accessibility = spec.IsPublic ? "public" : "internal";
        string oldDecl = $"internal static class {spec.TypeName}";
        string newDecl = $"{accessibility} static partial class {spec.TypeName}";

        return generated.Replace(oldDecl, newDecl);
    }

    private readonly record struct GenerationSpec(
        string ExpressionFile,
        string NamespaceName,
        string TypeName,
        bool IsPublic);
}