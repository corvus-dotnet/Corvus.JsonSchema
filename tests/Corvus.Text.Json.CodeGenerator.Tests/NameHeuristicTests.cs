using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests that verify name heuristics are correctly applied during code generation.
/// These tests exercise PathNameHeuristic, ConstPropertyNameHeuristic,
/// and name collision resolution through the full code generation pipeline.
/// </summary>
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

    [Fact]
    public async Task Generate_ConstProperties_ProducesTypesNamedFromConstants()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "const-properties.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // The ConstPropertyNameHeuristic should produce types named with "With" prefix
        // from the const property values (e.g., "WithKindAlpha", "WithKindBeta")
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
        string joined = string.Join(", ", fileNames);

        // Verify at least some const-named types exist
        Assert.True(
            fileNames.Any(f => f.Contains("Kind", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("With", StringComparison.OrdinalIgnoreCase)),
            $"Expected const-based type names. Got: {joined}");
    }

    [Fact]
    public async Task Generate_NestedPathNames_ProducesTypesNamedFromPath()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "nested-path-names.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // PathNameHeuristic uses the last path segment to name types
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
        string joined = string.Join(", ", fileNames);

        // Nested properties "database", "connection", "settings", "endpoints" should produce types
        Assert.True(
            fileNames.Any(f => f.Contains("Database", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Database' type from path heuristic. Got: {joined}");
        Assert.True(
            fileNames.Any(f => f.Contains("Connection", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Connection' type from path heuristic. Got: {joined}");

        // A property name starting with a digit should get a prefix
        Assert.True(
            fileNames.Any(f => f.Contains("InvalidStartName", StringComparison.OrdinalIgnoreCase) ||
                              f.Contains("0invalid", StringComparison.OrdinalIgnoreCase)),
            $"Expected type for digit-prefixed property. Got: {joined}");
    }

    [Fact]
    public async Task Generate_NameCollisions_ResolvesConflicts()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "name-collisions.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // The schema has a $defs/Address and a nested property also called "Address"
        // The collision resolver should produce distinct names
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();
        string joined = string.Join(", ", fileNames);

        // Should have at least one "Address" type from $defs
        Assert.True(
            fileNames.Any(f => f.Contains("Address", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Address' type. Got: {joined}");
    }

    [Fact]
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

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");

        // With > 3 const properties, the heuristic falls through to PathNameHeuristic instead
        string[] fileNames = files.Select(Path.GetFileNameWithoutExtension).ToArray();

        // Should NOT have a "WithA1AndB2AndC3AndD4AndE5" name — that would mean the heuristic didn't bail
        Assert.True(
            !fileNames.Any(f => f.Contains("WithA1AndB2AndC3AndD4AndE5", StringComparison.Ordinal)),
            $"Expected const heuristic to bail out with 5 properties. Got: {string.Join(", ", fileNames)}");
    }
}
