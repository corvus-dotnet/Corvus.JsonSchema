// <copyright file="TypeNameFallbackTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// A type that no enabled naming heuristic names takes the formatted fallback name <c>Entity</c>, not the length of that
/// name (issue #968).
/// </summary>
[TestClass]
public class TypeNameFallbackTests
{
    // No title, description, const, required, default or custom keyword, so with BaseSchemaNameHeuristic disabled no
    // heuristic names the root (PathNameHeuristic names only types with a parent).
    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string" }
          }
        }
        """;

    [TestMethod]
    [DataRow("V5")]
    [DataRow("V4")]
    public async Task Generate_TypeNamedByNoHeuristic_TakesTheFallbackName(string engine)
    {
        string directory = Directory.CreateTempSubdirectory("fallback-name-").FullName;
        try
        {
            string schemaFile = Path.Combine(directory, "schema.json");
            await File.WriteAllTextAsync(schemaFile, Schema);
            string outputPath = Path.Combine(directory, "out");
            string[] arguments = ["jsonschema", schemaFile, "--rootNamespace", "Fallback.Tests", "--outputPath", outputPath, "--engine", engine, "--disableNamingHeuristic", "BaseSchemaNameHeuristic"];
            int exitCode = await CliAppFactory.Create("corvusjson").RunAsync(arguments);
            Assert.AreEqual(0, exitCode, $"corvusjson {string.Join(' ', arguments)}");

            string[] names = Directory.Exists(outputPath)
                ? Directory.EnumerateFiles(outputPath, "*.cs", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()
                : [];
            CollectionAssert.Contains(names, "Entity.cs", $"generated files: {string.Join(", ", names)}");
            Assert.IsFalse(names.Any(n => char.IsAsciiDigit(n[0])), $"generated files: {string.Join(", ", names)}");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}