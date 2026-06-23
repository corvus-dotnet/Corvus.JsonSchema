using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests that verify name heuristics are correctly applied during code generation.
/// These tests exercise PathNameHeuristic, ConstPropertyNameHeuristic,
/// and name collision resolution through the full code generation pipeline.
/// </summary>
[TestClass]
public class NameHeuristicTests : IDisposable
{
    private readonly string _outputDir;

    public NameHeuristicTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [TestMethod]
    public async Task Generate_ConstProperties_ProducesTypesNamedFromConstants()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "const-properties.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // The ConstPropertyNameHeuristic should produce types named with "With" prefix
        // from the const property values (e.g., "WithKindAlpha", "WithKindBeta")
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
        string joined = string.Join(", ", fileNames);

        // Verify at least some const-named types exist
        Assert.IsTrue(
            fileNames.Any(f => f.Contains("Kind", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("With", StringComparison.OrdinalIgnoreCase)),
            $"Expected const-based type names. Got: {joined}");
    }

    [TestMethod]
    public async Task Generate_NestedPathNames_ProducesTypesNamedFromPath()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "nested-path-names.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // PathNameHeuristic uses the last path segment to name types
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
        string joined = string.Join(", ", fileNames);

        // Nested properties "database", "connection", "settings", "endpoints" should produce types
        Assert.IsTrue(
            fileNames.Any(f => f.Contains("Database", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Database' type from path heuristic. Got: {joined}");
        Assert.IsTrue(
            fileNames.Any(f => f.Contains("Connection", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Connection' type from path heuristic. Got: {joined}");

        // A property name starting with a digit should get a prefix
        Assert.IsTrue(
            fileNames.Any(f => f.Contains("InvalidStartName", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("0invalid", StringComparison.OrdinalIgnoreCase)),
            $"Expected type for digit-prefixed property. Got: {joined}");
    }

    [TestMethod]
    public async Task Generate_NameCollisions_ResolvesConflicts()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "name-collisions.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // The schema has a $defs/Address and a nested property also called "Address"
        // The collision resolver should produce distinct names
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
        string joined = string.Join(", ", fileNames);

        // Should have at least one "Address" type from $defs
        Assert.IsTrue(
            fileNames.Any(f => f.Contains("Address", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Address' type. Got: {joined}");
    }

    [TestMethod]
    public async Task Generate_ConstProperties_FourConstantsBailsOut()
    {
        // The ConstPropertyNameHeuristic bails out when there are > 3 const properties
        // Create an inline schema with 4+ const properties
        string schemaContent = """
            {
              "type": "object",
              "properties": {
                "items": {
                  "type": "array",
                  "items": {
                    "oneOf": [
                      {
                        "type": "object",
                        "properties": {
                          "a": { "const": "1" },
                          "b": { "const": "2" },
                          "c": { "const": "3" },
                          "d": { "const": "4" },
                          "e": { "const": "5" }
                        },
                        "required": ["a", "b", "c", "d", "e"]
                      }
                    ]
                  }
                }
              }
            }
            """;

        string schemaPath = Path.Combine(_outputDir, "too-many-consts.json");
        await File.WriteAllTextAsync(schemaPath, schemaContent);

        string outputDir = Path.Combine(_outputDir, "output");
        Directory.CreateDirectory(outputDir);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schemaPath}\" --rootNamespace TestGenerated --outputPath \"{outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);

        string[] files = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // With > 3 const properties, the heuristic falls through to PathNameHeuristic instead
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();

        // Should NOT have a "WithA1AndB2AndC3AndD4AndE5" name — that would mean the heuristic didn't bail
        Assert.IsTrue(
            !fileNames.Any(f => f.Contains("WithA1AndB2AndC3AndD4AndE5", StringComparison.Ordinal)),
            $"Expected const heuristic to bail out with 5 properties. Got: {string.Join(", ", fileNames)}");
    }

    [TestMethod]
    public async Task Generate_LongDescriptionAtNewlineBoundary_NamesFromDocumentationNotPlatformDependent()
    {
        // Regression for cross-platform naming divergence (issue #825): the documentation
        // name heuristic gated on a length that included a trailing Environment.NewLine
        // ("\n" on Linux vs "\r\n" on Windows), so a description whose length landed on the
        // < 64 boundary produced the documentation name on Linux but fell back to the
        // required-property name on Windows.
        //
        // This description is exactly 63 characters with no `title`, so it lands on that
        // boundary. The generated type must be named from the description text alone, on
        // every platform — never from the `identifier` required property.
        const string description = "Sphinx canonical reference for the associated stored documents.";
        Assert.AreEqual(63, description.Length, "Test description must be exactly 63 chars to sit on the boundary.");

        string schemaContent = $$"""
            {
              "type": "object",
              "properties": {
                "widget": {
                  "type": "object",
                  "description": "{{description}}",
                  "required": ["identifier"],
                  "properties": { "identifier": { "type": "string" } }
                }
              }
            }
            """;

        string[] fileNames = await GenerateInlineAndGetTypeNamesAsync(schemaContent);
        string joined = string.Join(", ", fileNames);

        Assert.IsTrue(
            fileNames.Any(f => f.Contains("SphinxCanonicalReference", StringComparison.Ordinal)),
            $"Expected the inline type to be named from its description. Got: {joined}");
        Assert.IsFalse(
            fileNames.Any(f => f.Contains("RequiredIdentifier", StringComparison.Ordinal)),
            $"Expected NO required-property fallback name (that is the Windows-only regression). Got: {joined}");
    }

    [TestMethod]
    public async Task Generate_LongDescriptionWithTrailingWhitespace_NamesFromTrimmedDocumentation()
    {
        // Regression for issue #825: trailing whitespace (or a separator added while
        // assembling multi-keyword documentation) must not push the documentation length
        // over the < 64 gate. The name must derive from the trimmed description.
        string description = "Obelisk summary of the associated stored record" + new string(' ', 20);
        Assert.IsTrue(description.Length >= 64, "Trailing whitespace must take the raw length over the 64 gate.");

        string schemaContent = $$"""
            {
              "type": "object",
              "properties": {
                "widget": {
                  "type": "object",
                  "description": "{{description}}",
                  "required": ["identifier"],
                  "properties": { "identifier": { "type": "string" } }
                }
              }
            }
            """;

        string[] fileNames = await GenerateInlineAndGetTypeNamesAsync(schemaContent);
        string joined = string.Join(", ", fileNames);

        Assert.IsTrue(
            fileNames.Any(f => f.Contains("ObeliskSummaryOfTheAssociatedStoredRecord", StringComparison.Ordinal)),
            $"Expected the inline type to be named from its trimmed description. Got: {joined}");
        Assert.IsFalse(
            fileNames.Any(f => f.Contains("RequiredIdentifier", StringComparison.Ordinal)),
            $"Expected NO required-property fallback name. Got: {joined}");
    }

    private async Task<string[]> GenerateInlineAndGetTypeNamesAsync(string schemaContent)
    {
        string schemaPath = Path.Combine(_outputDir, "schema.json");
        await File.WriteAllTextAsync(schemaPath, schemaContent);

        string outputDir = Path.Combine(_outputDir, "output");
        Directory.CreateDirectory(outputDir);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schemaPath}\" --rootNamespace TestGenerated --outputPath \"{outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Generation failed. Stderr: {result.StandardError}");

        string[] files = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        return files.Select(Path.GetFileNameWithoutExtension).ToArray()!;
    }
}
