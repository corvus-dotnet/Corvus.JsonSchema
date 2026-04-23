// <copyright file="SourceGeneratorDiagnosticTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Text.Json.JMESPath.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Corvus.Text.Json.JMESPath.SourceGenerator.Tests;

/// <summary>
/// Tests that the JMESPath source generator reports the correct diagnostics
/// (JPSG001, JPSG002, JPSG003) for error conditions.
/// </summary>
public class SourceGeneratorDiagnosticTests
{
    private const string SourceWithAttribute = """
        using Corvus.Text.Json.JMESPath;

        namespace TestNamespace;

        [JMESPathExpression("test.jmespath")]
        internal static partial class TestExpr
        {
        }
        """;

    // ----- JPSG001: Expression file not found -----

    [Fact]
    public void JPSG001_WhenExpressionFileNotInAdditionalFiles()
    {
        // No AdditionalTexts provided — the referenced "test.jmespath" is missing
        GeneratorDriverRunResult result = RunGenerator(SourceWithAttribute);

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JPSG001");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test.jmespath", diag.GetMessage());
    }

    // ----- JPSG002: Empty expression file -----

    [Fact]
    public void JPSG002_WhenExpressionFileIsEmpty()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            new InMemoryAdditionalText("test.jmespath", string.Empty));

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JPSG002");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test.jmespath", diag.GetMessage());
    }

    [Fact]
    public void JPSG002_WhenExpressionFileIsWhitespaceOnly()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            new InMemoryAdditionalText("test.jmespath", "   \n  \t  "));

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JPSG002");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    // ----- JPSG003: Code generation failed -----

    [Fact]
    public void JPSG003_WhenExpressionIsInvalid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            new InMemoryAdditionalText("test.jmespath", "###invalid"));

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JPSG003");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("test.jmespath", diag.GetMessage());
    }

    [Fact]
    public void JPSG003_WhenExpressionHasUnterminatedString()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            new InMemoryAdditionalText("test.jmespath", "'unterminated"));

        Diagnostic diag = Assert.Single(result.Diagnostics, d => d.Id == "JPSG003");
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    // ----- No diagnostics for valid expression -----

    [Fact]
    public void NoDiagnostics_WhenExpressionIsValid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            new InMemoryAdditionalText("test.jmespath", "foo.bar"));

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void NoDiagnostics_WhenExpressionIsComplexButValid()
    {
        GeneratorDriverRunResult result = RunGenerator(
            SourceWithAttribute,
            new InMemoryAdditionalText("test.jmespath", "people[?age > `20`].name | sort(@)"));

        Assert.Empty(result.Diagnostics);
    }

    // ----- Helpers -----

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        params InMemoryAdditionalText[] additionalTexts)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        ISourceGenerator generator = new JMESPathSourceGenerator().AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalTexts));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Build references from the trusted platform assemblies
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

    /// <summary>
    /// An in-memory implementation of <see cref="AdditionalText"/> for testing.
    /// </summary>
    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText? GetText(CancellationToken cancellationToken = default)
            => SourceText.From(content, Encoding.UTF8);
    }
}
