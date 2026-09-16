// <copyright file="NonPublicDocumentationTests.cs" company="Endjin Limited">
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
/// Non-public members of generated types carry no XML documentation: a consumer never sees it, so the emitter does
/// not write it. Public members and internal types keep theirs.
/// </summary>
[TestClass]
public class NonPublicDocumentationTests
{
    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Widget",
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "The widget's name." },
            "size": { "type": "integer", "minimum": 0 },
            "colour": { "enum": ["red", "green"] }
          },
          "required": ["name"]
        }
        """;

    [TestMethod]
    public async Task GenerateCode_NonPublicMembers_CarryNoDocumentation()
    {
        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(Schema, new CSharpLanguageProvider.Options(defaultNamespace: "Test"));

        int documentedPublicMembers = 0;
        int documentedInternalTypes = 0;
        int undocumentedNonPublicMembers = 0;
        List<string> offenders = [];
        foreach (GeneratedCodeFile file in files.Where(f => f.FileName.EndsWith(".cs", StringComparison.Ordinal)))
        {
            SyntaxNode root = CSharpSyntaxTree.ParseText(file.FileContent).GetRoot();
            foreach (MemberDeclarationSyntax member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (member is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
                {
                    continue;
                }

                bool documented = member.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
                bool isPrivate = member.Modifiers.Any(SyntaxKind.PrivateKeyword);
                bool isInternal = member.Modifiers.Any(SyntaxKind.InternalKeyword);
                bool isType = member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;
                bool nonPublicMember = isPrivate || (isInternal && !isType);

                if (nonPublicMember)
                {
                    if (documented)
                    {
                        offenders.Add($"{file.FileName}: {member.Modifiers} {member.Kind()} at line {member.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
                    }
                    else
                    {
                        undocumentedNonPublicMembers++;
                    }
                }
                else if (documented && member.Modifiers.Any(SyntaxKind.PublicKeyword))
                {
                    documentedPublicMembers++;
                }
                else if (documented && isInternal && isType)
                {
                    documentedInternalTypes++;
                }
            }
        }

        Assert.AreEqual(0, offenders.Count, string.Join(Environment.NewLine, offenders.Take(20)));
        Assert.IsTrue(undocumentedNonPublicMembers > 0, "the generated code has non-public members");
        Assert.IsTrue(documentedPublicMembers > 0, "public members keep their documentation");
        Assert.IsTrue(documentedInternalTypes > 0, "internal types (the schema program) keep their documentation");
    }
}