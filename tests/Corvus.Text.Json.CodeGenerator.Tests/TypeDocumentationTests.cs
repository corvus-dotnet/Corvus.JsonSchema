// <copyright file="TypeDocumentationTests.cs" company="Endjin Limited">
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
/// A generated type's documentation and its <c>[DebuggerDisplay]</c> are emitted once, in its own Core partial: not
/// again in its Mutable and JsonSchema partials, and not on the parent declarations that nest a child type.
/// </summary>
[TestClass]
public class TypeDocumentationTests
{
    // Three levels of nesting, each with its own title and description.
    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Order",
          "description": "An order placed by a customer.",
          "type": "object",
          "properties": {
            "customer": {
              "title": "Customer",
              "description": "The customer who placed the order.",
              "type": "object",
              "properties": {
                "address": {
                  "title": "Address",
                  "description": "Where the order is delivered.",
                  "type": "object",
                  "properties": { "postcode": { "type": "string" } }
                }
              }
            }
          }
        }
        """;

    private static readonly string[] Descriptions = ["An order placed by a customer.", "The customer who placed the order.", "Where the order is delivered."];

    [TestMethod]
    public async Task GenerateCode_NestedTypes_EmitDocumentationOncePerType()
    {
        IReadOnlyCollection<GeneratedCodeFile> files = await Generate();

        // Every struct declaration that carries documentation, by fully qualified name, with the file it is in and
        // the documentation text.
        Dictionary<string, List<string>> documentedIn = new(StringComparer.Ordinal);
        Dictionary<string, string> documentation = new(StringComparer.Ordinal);
        foreach (GeneratedCodeFile file in files.Where(f => f.FileName.EndsWith(".cs", StringComparison.Ordinal)))
        {
            SyntaxNode root = CSharpSyntaxTree.ParseText(file.FileContent).GetRoot();
            foreach (StructDeclarationSyntax declaration in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                SyntaxTrivia[] docs = declaration.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).ToArray();
                if (docs.Length == 0)
                {
                    continue;
                }

                string name = string.Join(".", declaration.AncestorsAndSelf().OfType<StructDeclarationSyntax>().Reverse().Select(d => d.Identifier.Text));
                if (!documentedIn.TryGetValue(name, out List<string> where))
                {
                    documentedIn[name] = where = [];
                }

                where.Add(file.FileName);
                documentation[name] = string.Concat(docs.Select(d => d.ToFullString()));
            }
        }

        // The root is named from its file, so the names are not pinned; the nesting is (root, Customer, Address) and
        // the shared JsonString type for postcode has its own documentation.
        string report = string.Join(Environment.NewLine, documentedIn.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}"));
        Assert.AreEqual(4, documentedIn.Count, report);
        Assert.IsTrue(documentedIn.Keys.Any(k => k.EndsWith(".Customer.Address", StringComparison.Ordinal)), report);
        foreach ((string name, List<string> where) in documentedIn)
        {
            Assert.AreEqual(1, where.Count, $"{name} is documented in {where.Count} files: {report}");
            Assert.AreEqual($"{name}.cs", where[0], $"{name} is documented outside its Core partial: {report}");
        }

        // The one copy is the type's own documentation (its description), not a placeholder.
        foreach ((string name, string description) in new[] { ("", Descriptions[0]), (".Customer", Descriptions[1]), (".Customer.Address", Descriptions[2]) })
        {
            string type = documentation.Keys.Single(k => k.EndsWith(name, StringComparison.Ordinal) && k.Count(c => c == '.') == name.Count(c => c == '.') && !k.StartsWith("Json", StringComparison.Ordinal));
            StringAssert.Contains(documentation[type], description, $"{type} documentation:{Environment.NewLine}{documentation[type]}");
        }
    }

    [TestMethod]
    public async Task GenerateCode_DebuggerDisplayAndSummary_AppearOncePerType()
    {
        IReadOnlyCollection<GeneratedCodeFile> files = await Generate();
        CSharpCompilation compilation = DeterministicGenerationTests.CreateCompilation(files);
        INamespaceSymbol test = compilation.GlobalNamespace.GetNamespaceMembers().Single(n => n.Name == "Test");

        List<INamedTypeSymbol> generated = [];
        void Collect(INamedTypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Struct && type.IsReadOnly)
            {
                generated.Add(type);
            }

            foreach (INamedTypeSymbol nested in type.GetTypeMembers())
            {
                Collect(nested);
            }
        }

        foreach (INamedTypeSymbol type in test.GetTypeMembers())
        {
            Collect(type);
        }

        string[] names = generated.Select(t => t.ToDisplayString()).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.IsTrue(names.Any(n => n.EndsWith(".Customer.Address", StringComparison.Ordinal)), string.Join(", ", names));
        foreach (INamedTypeSymbol type in generated)
        {
            int debuggerDisplays = type.GetAttributes().Count(a => a.AttributeClass?.Name == "DebuggerDisplayAttribute");
            Assert.AreEqual(1, debuggerDisplays, $"{type.ToDisplayString()} has {debuggerDisplays} [DebuggerDisplay] attributes");

            string xml = type.GetDocumentationCommentXml() ?? string.Empty;
            int summaries = xml.Split("<summary>").Length - 1;
            Assert.AreEqual(1, summaries, $"{type.ToDisplayString()} documentation has {summaries} <summary> elements:{Environment.NewLine}{xml}");
        }
    }

    private static Task<IReadOnlyCollection<GeneratedCodeFile>> Generate()
    {
        return InProcessGenerationTests.GenerateInProcessFromContent(Schema, new CSharpLanguageProvider.Options(defaultNamespace: "Test"));
    }
}