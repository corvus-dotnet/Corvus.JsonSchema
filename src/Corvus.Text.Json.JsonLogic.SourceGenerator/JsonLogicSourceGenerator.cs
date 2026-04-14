// <copyright file="JsonLogicSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator;

/// <summary>
/// Roslyn incremental source generator that produces a static <c>Evaluate</c> method
/// from a JsonLogic rule JSON file referenced via <c>[JsonLogicRule("path.json")]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsonLogicSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        EmitAttribute(context);

        // Discover all additional text files that end with .json
        IncrementalValuesProvider<AdditionalText> jsonFiles =
            context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        // Collect into an immutable array so we can look up by path
        IncrementalValueProvider<ImmutableArray<AdditionalText>> collectedJsonFiles =
            jsonFiles.Collect();

        // Discover all .jlops files for custom operators
        IncrementalValuesProvider<AdditionalText> jlopsFiles =
            context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith(".jlops", StringComparison.OrdinalIgnoreCase));

        IncrementalValueProvider<ImmutableArray<AdditionalText>> collectedJlopsFiles =
            jlopsFiles.Collect();

        // Find partial classes/structs annotated with [JsonLogicRule]
        IncrementalValuesProvider<GenerationSpec> specs =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Text.Json.JsonLogic.JsonLogicRuleAttribute",
                predicate: IsValidTarget,
                transform: ExtractSpec);

        // Combine each spec with all available JSON and .jlops files
        IncrementalValuesProvider<(GenerationSpec Spec, ImmutableArray<AdditionalText> JsonFiles, ImmutableArray<AdditionalText> JlopsFiles)> combined =
            specs
                .Combine(collectedJsonFiles)
                .Combine(collectedJlopsFiles)
                .Select(static (pair, _) => (pair.Left.Left, pair.Left.Right, pair.Right));

        context.RegisterSourceOutput(combined, Execute);
    }

    private static void EmitAttribute(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "JsonLogicRuleAttribute.g.cs",
                SourceText.From(
                    """
                    using System;

                    namespace Corvus.Text.Json.JsonLogic;

                    /// <summary>
                    /// Marks a partial type for JsonLogic source generation.
                    /// The source generator reads the referenced JSON rule file and emits
                    /// a static <c>Evaluate</c> method on the type.
                    /// </summary>
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
                    internal sealed class JsonLogicRuleAttribute : Attribute
                    {
                        /// <summary>
                        /// Initializes a new instance of the <see cref="JsonLogicRuleAttribute"/> class.
                        /// </summary>
                        /// <param name="ruleFile">
                        /// The path to the JSON rule file, relative to the project directory.
                        /// The file must be included as an <c>AdditionalFiles</c> item in the project.
                        /// </param>
                        public JsonLogicRuleAttribute(string ruleFile)
                        {
                            this.RuleFile = ruleFile;
                        }

                        /// <summary>
                        /// Gets the path to the JSON rule file.
                        /// </summary>
                        public string RuleFile { get; }
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
        string ruleFile = attr.ConstructorArguments[0].Value as string ?? string.Empty;
        string namespaceName = context.TargetSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : context.TargetSymbol.ContainingNamespace.ToDisplayString();
        string typeName = context.TargetSymbol.Name;
        bool isPublic = context.TargetSymbol.DeclaredAccessibility == Accessibility.Public;

        return new GenerationSpec(ruleFile, namespaceName, typeName, isPublic);
    }

    private static void Execute(
        SourceProductionContext context,
        (GenerationSpec Spec, ImmutableArray<AdditionalText> JsonFiles, ImmutableArray<AdditionalText> JlopsFiles) input)
    {
        GenerationSpec spec = input.Spec;
        ImmutableArray<AdditionalText> files = input.JsonFiles;

        // Parse all .jlops files into custom operators
        IReadOnlyList<CustomOperator>? customOperators = ParseJlopsFiles(context, input.JlopsFiles);

        // Find the matching JSON file
        AdditionalText? matchingFile = null;
        foreach (AdditionalText file in files)
        {
            if (file.Path.EndsWith(spec.RuleFile, StringComparison.OrdinalIgnoreCase) ||
                file.Path.Replace('\\', '/').EndsWith(spec.RuleFile.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                matchingFile = file;
                break;
            }
        }

        if (matchingFile is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JLSG001",
                    "Rule file not found",
                    "JsonLogic rule file '{0}' was not found in AdditionalFiles. Add <AdditionalFiles Include=\"{0}\" /> to your project.",
                    "Corvus.Text.Json.JsonLogic",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.RuleFile));
            return;
        }

        string? ruleJson = matchingFile.GetText(context.CancellationToken)?.ToString();

        if (string.IsNullOrEmpty(ruleJson))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JLSG002",
                    "Empty rule file",
                    "JsonLogic rule file '{0}' is empty.",
                    "Corvus.Text.Json.JsonLogic",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.RuleFile));
            return;
        }

        try
        {
            string generated = JsonLogicCodeGenerator.Generate(ruleJson!, spec.TypeName, spec.NamespaceName, customOperators);

            // The code generator emits a static class; we need to transform it into
            // a partial type body. Replace the generated class wrapper with a partial
            // type declaration matching the user's type.
            string source = TransformToPartialType(generated, spec);

            context.AddSource($"{spec.TypeName}.JsonLogic.g.cs", SourceText.From(source, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "JLSG003",
                    "Code generation failed",
                    "JsonLogic code generation failed for '{0}': {1}",
                    "Corvus.Text.Json.JsonLogic",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                spec.RuleFile,
                ex.Message));
        }
    }

    /// <summary>
    /// Parses all <c>.jlops</c> additional files into a combined list of custom operators.
    /// </summary>
    private static IReadOnlyList<CustomOperator>? ParseJlopsFiles(
        SourceProductionContext context,
        ImmutableArray<AdditionalText> jlopsFiles)
    {
        if (jlopsFiles.IsDefaultOrEmpty)
        {
            return null;
        }

        List<CustomOperator> allOps = new();
        foreach (AdditionalText jlopsFile in jlopsFiles)
        {
            string? content = jlopsFile.GetText(context.CancellationToken)?.ToString();
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            try
            {
                IReadOnlyList<CustomOperator> ops = JlopsParser.Parse(content!);
                allOps.AddRange(ops);
            }
            catch (FormatException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "JLSG004",
                        "Invalid .jlops file",
                        "Failed to parse custom operators file '{0}': {1}",
                        "Corvus.Text.Json.JsonLogic",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    jlopsFile.Path,
                    ex.Message));
            }
        }

        return allOps.Count > 0 ? allOps : null;
    }

    /// <summary>
    /// Transforms the output of <see cref="JsonLogicCodeGenerator.Generate"/> from a standalone
    /// static class into a partial type body matching the user's declaration.
    /// </summary>
    private static string TransformToPartialType(string generated, GenerationSpec spec)
    {
        // The generator outputs:
        //   // <auto-generated/>
        //   using Corvus.Numerics;
        //   using Corvus.Text.Json;
        //   using Corvus.Text.Json.JsonLogic;
        //
        //   namespace <ns>;
        //
        //   internal static class <TypeName>
        //   {
        //       public static JsonElement Evaluate(...)
        //       { ... }
        //
        //       // helper methods
        //   }
        //
        // We replace the class declaration line with a partial type declaration.
        string accessibility = spec.IsPublic ? "public" : "internal";
        string oldDecl = $"internal static class {spec.TypeName}";
        string newDecl = $"{accessibility} static partial class {spec.TypeName}";

        return generated.Replace(oldDecl, newDecl);
    }

    private readonly record struct GenerationSpec(
        string RuleFile,
        string NamespaceName,
        string TypeName,
        bool IsPublic);
}