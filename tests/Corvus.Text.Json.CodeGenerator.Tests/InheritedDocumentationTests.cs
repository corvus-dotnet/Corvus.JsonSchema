// <copyright file="InheritedDocumentationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Generated members that mirror a <c>JsonElement</c> member carry a single <c>inheritdoc</c> line naming the
/// runtime member instead of a copy of its documentation, and every such cref resolves.
/// </summary>
[TestClass]
public class InheritedDocumentationTests
{
    private const string RuntimeCrefPrefix = "<inheritdoc cref=\"global::Corvus.Text.Json.JsonElement";

    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Widget",
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "size": { "type": "integer" },
            "tags": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["name"]
        }
        """;

    /// <summary>
    /// Members for which every occurrence must inherit its documentation.
    /// </summary>
    private static readonly HashSet<string> AlwaysInherited = new(StringComparer.Ordinal)
    {
        "operator ==",
        "operator !=",
        "TryGetProperty",
        "this[]",
        "EnumerateObject",
        "GetPropertyCount",
        "EnumerateArray",
        "GetArrayLength",
        "EvaluateSchema",
        "Equals<T>",
        "From<T>",
        "ParseValue",
        "TryParseValue",
        "Clone",
        "Freeze",
    };

    /// <summary>
    /// Members with other overloads that keep their own documentation, so at least one occurrence must inherit.
    /// </summary>
    private static readonly HashSet<string> SometimesInherited = new(StringComparer.Ordinal)
    {
        "SetProperty",
        "RemoveProperty",
        "ValueEquals",
        "CreateBuilder",
        "IsUndefined",
    };

    private string _outputDir;

    [TestInitialize]
    public void Initialize()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    [TestCleanup]
    public void Cleanup()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [TestMethod]
    public async Task GenerateCode_MembersMirroringJsonElement_InheritTheRuntimeDocumentation()
    {
        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(Schema, new CSharpLanguageProvider.Options(defaultNamespace: "Test"));

        Dictionary<string, int> inherited = new(StringComparer.Ordinal);
        List<string> copied = [];
        List<string> mixed = [];
        foreach (GeneratedCodeFile file in files.Where(f => f.FileName.EndsWith(".cs", StringComparison.Ordinal)))
        {
            File.WriteAllText(Path.Combine(_outputDir, file.FileName), file.FileContent);
            SyntaxNode root = CSharpSyntaxTree.ParseText(file.FileContent).GetRoot();
            foreach (MemberDeclarationSyntax member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                string name = GetName(member);
                if (name is null || (!AlwaysInherited.Contains(name) && !SometimesInherited.Contains(name)))
                {
                    continue;
                }

                string documentation = string.Concat(member.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).Select(t => t.ToFullString()));
                bool inherits = documentation.Contains(RuntimeCrefPrefix, StringComparison.Ordinal);
                bool summarised = documentation.Contains("<summary>", StringComparison.Ordinal);
                if (inherits && summarised)
                {
                    mixed.Add($"{file.FileName}: {name}");
                }

                if (inherits)
                {
                    inherited[name] = inherited.GetValueOrDefault(name) + 1;
                }
                else if (AlwaysInherited.Contains(name))
                {
                    copied.Add($"{file.FileName}: {name}");
                }
            }
        }

        Assert.AreEqual(0, mixed.Count, "Members carrying both an inheritdoc and a summary:\n" + string.Join("\n", mixed));
        Assert.AreEqual(0, copied.Count, "Members still carrying copied documentation:\n" + string.Join("\n", copied));
        IEnumerable<string> missing = AlwaysInherited.Concat(SometimesInherited).Where(n => !inherited.ContainsKey(n));
        Assert.AreEqual(0, missing.Count(), "Members never seen with inherited documentation: " + string.Join(", ", missing));

        // Every cref must bind against the built runtime, and no other documentation diagnostic may appear.
        GeneratedDocumentationTests.AssertNoDocumentationMismatchDiagnostics(_outputDir);
    }

    private static string GetName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax m => m.TypeParameterList is null ? m.Identifier.Text : m.Identifier.Text + "<T>",
            OperatorDeclarationSyntax o => "operator " + o.OperatorToken.Text,
            IndexerDeclarationSyntax => "this[]",
            PropertyDeclarationSyntax p => p.Identifier.Text,
            _ => null,
        };
    }
}