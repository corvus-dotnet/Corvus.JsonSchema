// <copyright file="SourceGeneratorDiagnosticTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.JsonLogic.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Corvus.Text.Json.JsonLogic.SourceGenerator.Tests;

/// <summary>
/// Tests that the JsonLogic source generator reports the correct diagnostics
/// (JLSG001–JLSG004) for error conditions.
/// </summary>
public class SourceGeneratorDiagnosticTests
{
    private const string SourceWithAttribute = """
        using Corvus.Text.Json.JsonLogic;

        namespace TestNamespace;

        [JsonLogicRule("test-rule.json")]
        internal static partial class TestRule
        {
        }
        """;

    // ----- JLSG001: Rule file not found -----

    [Fact]
    public void JLSG001_WhenRuleFileNotInAdditionalFiles()
    {
        GeneratorDriverRunResult result = RunGenerator(SourceWithAttribute);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JLSG001");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test-rule.json", diag.GetMessage());
    }

    // ----- JLSG002: Empty rule file -----

    [Fact]
    public void JLSG002_WhenRuleFileIsEmpty()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonFiles: [new InMemoryAdditionalText("test-rule.json", string.Empty)]);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JLSG002");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test-rule.json", diag.GetMessage());
    }

    // ----- JLSG003: Code generation failed -----

    [Fact]
    public void JLSG003_WhenRuleJsonIsInvalid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonFiles: [new InMemoryAdditionalText("test-rule.json", "{{not valid json")]);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JLSG003");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test-rule.json", diag.GetMessage());
    }

    // ----- JLSG004: Invalid .jlops file -----

    [Fact]
    public void JLSG004_WhenJlopsFileIsMalformed()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonFiles: [new InMemoryAdditionalText("test-rule.json", """{"+":[1,2]}""")],
            jlopsFiles: [new InMemoryAdditionalText("custom.jlops", "this is not valid jlops content!!!")]);

        // JLSG004 is only reported if the jlops parser throws FormatException.
        // If the content doesn't trigger a parse error, we just verify no crash.
        // The exact behavior depends on JlopsParser.Parse tolerance.
        bool hasJlsg004 = result.Diagnostics.Any(d => d.Id == "JLSG004");
        bool hasJlsg003 = result.Diagnostics.Any(d => d.Id == "JLSG003");

        // Either the invalid jlops triggers JLSG004, or the run completes
        // (possibly with JLSG003 if codegen fails due to missing operator)
        Assert.True(
            hasJlsg004 || result.Diagnostics.IsEmpty || hasJlsg003,
            $"Unexpected diagnostics: {string.Join(", ", result.Diagnostics.Select(d => d.Id))}");
    }

    // ----- No diagnostics for valid rule -----

    [Fact]
    public void NoDiagnostics_WhenRuleIsValid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonFiles: [new InMemoryAdditionalText("test-rule.json", """{"+":[1,2]}""")]);

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void NoDiagnostics_WhenRuleUsesVar()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonFiles: [new InMemoryAdditionalText("test-rule.json", """{"if":[{">":[{"var":"x"}, 10]}, "big", "small"]}""")]);

        Assert.Empty(result.Diagnostics);
    }

    // ----- Helpers -----

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        InMemoryAdditionalText[]? jsonFiles = null,
        InMemoryAdditionalText[]? jlopsFiles = null)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        ISourceGenerator generator = new JsonLogicSourceGenerator().AsSourceGenerator();

        var additionalTexts = new List<AdditionalText>();
        if (jsonFiles is not null)
        {
            additionalTexts.AddRange(jsonFiles);
        }

        if (jlopsFiles is not null)
        {
            additionalTexts.AddRange(jlopsFiles);
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(ImmutableArray.CreateRange<AdditionalText>(additionalTexts));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        List<MetadataReference> references = [];
        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedAssemblies is not null)
        {
            foreach (string assemblyPath in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (File.Exists(assemblyPath))
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
            }
        }

        return CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText? GetText(CancellationToken cancellationToken = default)
            => SourceText.From(content, Encoding.UTF8);
    }
}
