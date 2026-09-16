// <copyright file="GeneratedFileNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Generated file names are unique ignoring case. When two names collide, the index goes before the configured extension
/// (issue #965): <c>Foo1.cs</c>, not <c>Foo1..cs</c>.
/// </summary>
[TestClass]
public class GeneratedFileNameTests
{
    // id_def and iddef format to IdDef and Iddef, whose file names are equal ignoring case (the UI5 manifest schema has
    // this pair).
    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "a": { "$ref": "#/$defs/id_def" },
            "b": { "$ref": "#/$defs/iddef" }
          },
          "$defs": {
            "id_def": { "type": "object", "properties": { "x": { "type": "string" } } },
            "iddef": { "type": "object", "properties": { "y": { "type": "string" } } }
          }
        }
        """;

    [TestMethod]
    public async Task GenerateCode_FileNamesEqualIgnoringCase_GetAnIndexBeforeTheExtension()
    {
        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(Schema);
        string[] names = files
            .Select(f => f.FileName)
            .Where(n => n.Contains("IdDef", StringComparison.OrdinalIgnoreCase))
            .Select(n => n[(n.IndexOf('.') + 1)..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        string joined = string.Join(", ", names);
        Assert.IsFalse(names.Any(n => n.Contains("..", StringComparison.Ordinal)), joined);
        CollectionAssert.AreEqual(
            new[] { "IdDef.JsonSchema.cs", "IdDef.Mutable.cs", "IdDef.cs", "Iddef.JsonSchema1.cs", "Iddef.Mutable1.cs", "Iddef1.cs" },
            names,
            joined);
        Assert.AreEqual(files.Count, files.Select(f => f.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "file names are unique ignoring case");
    }
}