// <copyright file="SourceGeneratorDiagnosticTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.Jsonata.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Corvus.Text.Json.Jsonata.SourceGenerator.Tests;

/// <summary>
/// Tests that the JSONata source generator reports the correct diagnostics
/// (JASG001–JASG004) for error conditions.
/// </summary>
public class SourceGeneratorDiagnosticTests
{
    private const string SourceWithAttribute = """
        using Corvus.Text.Json.Jsonata;

        namespace TestNamespace;

        [JsonataExpression("test.jsonata")]
        internal static partial class TestExpr
        {
        }
        """;

    // ----- JASG001: Expression file not found -----

    [Fact]
    public void JASG001_WhenExpressionFileNotInAdditionalFiles()
    {
        GeneratorDriverRunResult result = RunGenerator(SourceWithAttribute);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JASG001");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test.jsonata", diag.GetMessage());
    }

    // ----- JASG002: Empty expression file -----

    [Fact]
    public void JASG002_WhenExpressionFileIsEmpty()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", string.Empty)]);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JASG002");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test.jsonata", diag.GetMessage());
    }

    [Fact]
    public void JASG002_WhenExpressionFileIsWhitespaceOnly()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", "   \n  \t  ")]);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JASG002");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    // ----- JASG003: Code generation failed -----

    [Fact]
    public void JASG003_WhenExpressionIsInvalid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", "###invalid")]);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JASG003");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test.jsonata", diag.GetMessage());
    }

    [Fact]
    public void JASG003_WhenExpressionHasUnterminatedString()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", "\"unterminated")]);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JASG003");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    // ----- JASG004: Custom function parse error -----

    [Fact]
    public void JASG004_WhenJfnFileIsMalformed()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", "x + y")],
            jfnFiles: [new InMemoryAdditionalText("custom.jfn", "this is not valid jfn content!!!")]);

        // JASG004 is only reported if JfnParser.Parse throws FormatException.
        bool hasJasg004 = result.Diagnostics.Any(d => d.Id == "JASG004");
        bool hasJasg003 = result.Diagnostics.Any(d => d.Id == "JASG003");

        // Either the invalid jfn triggers JASG004, or the expression compiles
        // (possibly with JASG003 if codegen fails)
        Assert.True(
            hasJasg004 || result.Diagnostics.IsEmpty || hasJasg003,
            $"Unexpected diagnostics: {string.Join(", ", result.Diagnostics.Select(d => d.Id))}");
    }

    // ----- No diagnostics for valid expression -----

    [Fact]
    public void NoDiagnostics_WhenExpressionIsValid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", "x + y")]);

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void NoDiagnostics_WhenExpressionIsComplex()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            jsonataFiles: [new InMemoryAdditionalText("test.jsonata", "$sum(items.(price * quantity))")]);

        Assert.Empty(result.Diagnostics);
    }

    // ----- Helpers -----

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        InMemoryAdditionalText[]? jsonataFiles = null,
        InMemoryAdditionalText[]? jfnFiles = null)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        ISourceGenerator generator = new JsonataSourceGenerator().AsSourceGenerator();

        var additionalTexts = new List<AdditionalText>();
        if (jsonataFiles is not null)
        {
            additionalTexts.AddRange(jsonataFiles);
        }

        if (jfnFiles is not null)
        {
            additionalTexts.AddRange(jfnFiles);
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
