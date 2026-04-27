// <copyright file="JsonPathSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.JsonPath.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.JsonPath.SourceGenerator;

/// <summary>
/// Roslyn incremental source generator that produces a static <c>Evaluate</c> method
/// from a JSONPath expression file referenced via <c>[JsonPathExpression("path.jsonpath")]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsonPathSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        EmitAttribute(context);

        // Discover all additional text files that end with .jsonpath
        IncrementalValuesProvider<AdditionalText> jsonpathFiles =
            context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith(".jsonpath", StringComparison.OrdinalIgnoreCase));

        // Collect into an immutable array so we can look up by path
        IncrementalValueProvider<ImmutableArray<AdditionalText>> collectedFiles =
            jsonpathFiles.Collect();

        // Find partial classes/structs annotated with [JsonPathExpression]
        IncrementalValuesProvider<GenerationSpec> specs =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Text.Json.JsonPath.JsonPathExpressionAttribute",
                predicate: IsValidTarget,
                transform: ExtractSpec);

        // Combine each spec with all available .jsonpath files
        IncrementalValuesProvider<(GenerationSpec Spec, ImmutableArray<AdditionalText> Files)> combined =
            specs
                .Combine(collectedFiles)
                .Select(static (pair, _) => (pair.Left, pair.Right));

        context.RegisterSourceOutput(combined, Execute);
    }

    private static void EmitAttribute(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "JsonPathExpressionAttribute.g.cs",
                SourceText.From(
                    """
                    using System;

                    namespace Corvus.Text.Json.JsonPath;

                    /// <summary>
                    /// Marks a partial type for JSONPath source generation.
                    /// The source generator reads the referenced JSONPath expression file and emits
                    /// a static <c>Evaluate</c> method on the type.
                    /// </summary>
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
                    internal sealed class JsonPathExpressionAttribute : Attribute
                    {
                        /// <summary>
                        /// Initializes a new instance of the <see cref="JsonPathExpressionAttribute"/> class.
                        /// </summary>
                        /// <param name="expressionFile">
                        /// The path to the JSONPath expression file, relative to the project directory.
                        /// The file must be included as an <c>AdditionalFiles</c> item in the project.
                        /// </param>
                        public JsonPathExpressionAttribute(string expressionFile)
                        {
                            this.ExpressionFile = expressionFile;
                        }

                        /// <summary>
                        /// Gets the path to the JSONPath expression file.
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
        (GenerationSpec Spec, ImmutableArray<AdditionalText> Files) input)
    {
        GenerationSpec spec = input.Spec;
        ImmutableArray<AdditionalText> files = input.Files;

        // Find the matching .jsonpath file
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
                    "JPTHSG001",
                    "Expression file not found",
                    "JSONPath expression file '{0}' was not found in AdditionalFiles. Add <AdditionalFiles Include=\"{0}\" /> to your project.",
                    "Corvus.Text.Json.JsonPath",
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
                    "JPTHSG002",
                    "Empty expression file",
                    "JSONPath expression file '{0}' is empty.",
                    "Corvus.Text.Json.JsonPath",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile));
            return;
        }

        try
        {
            string accessibility = spec.IsPublic ? "public" : "internal";
            string source = JsonPathCodeGenerator.Generate(
                expression!,
                spec.TypeName,
                spec.NamespaceName,
                accessibility,
                isPartial: true);

            context.AddSource($"{spec.TypeName}.JsonPath.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JPTHSG003",
                    "Code generation failed",
                    "JSONPath code generation failed for '{0}': {1}",
                    "Corvus.Text.Json.JsonPath",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile,
                ex.Message));
        }
    }

    private readonly record struct GenerationSpec(
        string ExpressionFile,
        string NamespaceName,
        string TypeName,
        bool IsPublic);
}
