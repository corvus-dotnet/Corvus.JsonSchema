// <copyright file="V4SourceGeneratorToolsHintNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

extern alias V4Tools;

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tools = V4Tools::Corvus.Json.SourceGeneratorTools.SourceGeneratorHelpers;

namespace Corvus.Text.Json.SourceGenerator.Tests;

/// <summary>
/// A generator that calls <c>SourceGeneratorHelpers.GenerateCode()</c> (Corvus.Json.SourceGeneratorTools) more than
/// once can generate types of the same name in different namespaces without a clash of hint names
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/651).
/// </summary>
[TestClass]
public class V4SourceGeneratorToolsHintNameTests
{
    private static readonly string SchemaPath = Path.Combine(Path.GetTempPath(), "corvus-v4-tools-hint-names", "foo.json");

    [TestMethod]
    public void TypesOfTheSameNameInDifferentNamespaces_HaveDistinctHintNames()
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll" or "mscorlib.dll")
            .Select(p => MetadataReference.CreateFromFile(p));

        CSharpCompilation compilation = CSharpCompilation.Create("HintNameTests", [], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TwoGenerationsGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText(SchemaPath, """{ "type": "object", "properties": { "name": { "type": "string" } } }""")]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> _);
        GeneratorRunResult result = driver.GetRunResult().Results[0];

        Assert.IsNull(result.Exception, $"The generator threw: {result.Exception}");
        string[] fooHints = [.. result.GeneratedSources.Select(s => s.HintName).Where(h => h.StartsWith("Foo.", StringComparison.Ordinal))];
        Assert.IsTrue(fooHints.Length >= 2, string.Join(", ", result.GeneratedSources.Select(s => s.HintName)));
        string[] hashes = [.. fooHints.Select(h => System.Text.RegularExpressions.Regex.Match(h, @"\.([0-9a-f]{8})\.g\.cs$")).Select(m => m.Success ? m.Groups[1].Value : "none").Distinct()];
        CollectionAssert.DoesNotContain(hashes, "none", string.Join(", ", fooHints));
        Assert.AreEqual(2, hashes.Length, string.Join(", ", fooHints));
        Assert.IsTrue(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("namespace First", StringComparison.Ordinal)));
        Assert.IsTrue(result.GeneratedSources.Any(s => s.SourceText.ToString().Contains("namespace Second", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Generates the same schema as the type <c>Foo</c> in two namespaces, with a call to <c>GenerateCode()</c> for each.
    /// </summary>
    private sealed class TwoGenerationsGenerator : IIncrementalGenerator
    {
        private static readonly V4Tools::Corvus.Json.CodeGeneration.VocabularyRegistry VocabularyRegistry = Tools.CreateVocabularyRegistry(Tools.CreateMetaSchemaResolver());

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.AdditionalTextsProvider.Collect(), static (production, texts) =>
            {
                Tools.GenerationContext generationContext = new(
                    Tools.BuildDocumentResolver(texts, production.CancellationToken),
                    new Tools.GlobalOptions(V4Tools::Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary, false, true, false, []));

                foreach (string ns in new[] { "First", "Second" })
                {
                    Tools.GenerateCode(production, new Tools.TypesToGenerate([new Tools.GenerationSpecification(ns, SchemaPath, false, "Foo")], generationContext), VocabularyRegistry);
                }
            });
        }
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText sourceText = SourceText.From(text, Encoding.UTF8);

        public override string Path => path;

        public override SourceText? GetText(CancellationToken cancellationToken = default) => this.sourceText;
    }
}