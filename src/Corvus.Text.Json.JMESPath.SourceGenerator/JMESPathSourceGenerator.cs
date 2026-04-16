// <copyright file="JMESPathSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.JMESPath.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.JMESPath.SourceGenerator;

/// <summary>
/// Roslyn incremental source generator that produces a static <c>Evaluate</c> method
/// from a JMESPath expression file referenced via <c>[JMESPathExpression("path.jmespath")]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JMESPathSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        EmitAttribute(context);

        // Discover all additional text files that end with .jmespath
        IncrementalValuesProvider<AdditionalText> jmespathFiles =
            context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith(".jmespath", StringComparison.OrdinalIgnoreCase));

        // Collect into an immutable array so we can look up by path
        IncrementalValueProvider<ImmutableArray<AdditionalText>> collectedFiles =
            jmespathFiles.Collect();

        // Find partial classes/structs annotated with [JMESPathExpression]
        IncrementalValuesProvider<GenerationSpec> specs =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Text.Json.JMESPath.JMESPathExpressionAttribute",
                predicate: IsValidTarget,
                transform: ExtractSpec);

        // Combine each spec with all available .jmespath files
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
                "JMESPathExpressionAttribute.g.cs",
                SourceText.From(
                    """
                    using System;

                    namespace Corvus.Text.Json.JMESPath;

                    /// <summary>
                    /// Marks a partial type for JMESPath source generation.
                    /// The source generator reads the referenced JMESPath expression file and emits
                    /// a static <c>Evaluate</c> method on the type.
                    /// </summary>
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
                    internal sealed class JMESPathExpressionAttribute : Attribute
                    {
                        /// <summary>
                        /// Initializes a new instance of the <see cref="JMESPathExpressionAttribute"/> class.
                        /// </summary>
                        /// <param name="expressionFile">
                        /// The path to the JMESPath expression file, relative to the project directory.
                        /// The file must be included as an <c>AdditionalFiles</c> item in the project.
                        /// </param>
                        public JMESPathExpressionAttribute(string expressionFile)
                        {
                            this.ExpressionFile = expressionFile;
                        }

                        /// <summary>
                        /// Gets the path to the JMESPath expression file.
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

        // Find the matching .jmespath file
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
                    "JPSG001",
                    "Expression file not found",
                    "JMESPath expression file '{0}' was not found in AdditionalFiles. Add <AdditionalFiles Include=\"{0}\" /> to your project.",
                    "Corvus.Text.Json.JMESPath",
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
                    "JPSG002",
                    "Empty expression file",
                    "JMESPath expression file '{0}' is empty.",
                    "Corvus.Text.Json.JMESPath",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile));
            return;
        }

        try
        {
            string generated = JMESPathCodeGenerator.Generate(expression!, spec.TypeName, spec.NamespaceName);

            // Transform the standalone static class into a partial type body
            string source = TransformToPartialType(generated, spec);

            context.AddSource($"{spec.TypeName}.JMESPath.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JPSG003",
                    "Code generation failed",
                    "JMESPath code generation failed for '{0}': {1}",
                    "Corvus.Text.Json.JMESPath",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.ExpressionFile,
                ex.Message));
        }
    }

    /// <summary>
    /// Transforms the output of <see cref="JMESPathCodeGenerator.Generate(string, string, string)"/> from a standalone
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