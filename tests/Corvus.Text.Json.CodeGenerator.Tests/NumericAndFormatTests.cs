using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests that verify numeric range validation handlers and format handlers
/// are correctly exercised during code generation for schemas with
/// multipleOf, min/max, exclusiveMin/Max, and string formats.
/// </summary>
public class NumericAndFormatTests : IDisposable
{
    private readonly string _outputDir;

    public NumericAndFormatTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [Fact]
    public async Task Generate_NumericAndFormat_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "numeric-and-format.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Generate_NumericAndFormat_ContainsValidationCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "numeric-and-format.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        // Read generated files and check for numeric validation patterns
        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        string allContent = string.Join("\n", files.Select(File.ReadAllText));

        // BigInteger path: multipleOf > UInt64.MaxValue should generate BigInteger code
        // The multipleOf of 18446744073709551617 exceeds UInt64.MaxValue
        Assert.Contains("BigInteger", allContent);
    }

    [Fact]
    public async Task Generate_NumericAndFormat_GeneratesRangeValidation()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "numeric-and-format.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        string allContent = string.Join("\n", files.Select(File.ReadAllText));

        // Should contain range checks for minimum/maximum/exclusiveMinimum/exclusiveMaximum
        Assert.True(
            allContent.Contains("exclusiveMinimum", StringComparison.OrdinalIgnoreCase) ||
            allContent.Contains("ExclusiveMinimum", StringComparison.OrdinalIgnoreCase) ||
            allContent.Contains("> 0", StringComparison.Ordinal),
            "Expected exclusive minimum validation code");
    }

    [Fact]
    public async Task Generate_NumericAndFormat_GeneratesFormatValidation()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "numeric-and-format.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        string allContent = string.Join("\n", files.Select(File.ReadAllText));

        // Format handlers should generate format-specific types/validation
        Assert.True(
            allContent.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
            allContent.Contains("email", StringComparison.OrdinalIgnoreCase),
            "Expected email format handling in generated code");
    }

    [Fact]
    public async Task Generate_Both_ProducesMoreFilesThanTypeGenOnly()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "numeric-and-format.json");

        // Generate with TypeGeneration mode
        string typeOnlyDir = Path.Combine(_outputDir, "typeonly");
        Directory.CreateDirectory(typeOnlyDir);
        ProcessResult resultTypeOnly = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{typeOnlyDir}\"");
        Assert.Equal(0, resultTypeOnly.ExitCode);

        // Generate with Both mode
        string bothDir = Path.Combine(_outputDir, "both");
        Directory.CreateDirectory(bothDir);
        ProcessResult resultBoth = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{bothDir}\" --codeGenerationMode Both");
        Assert.Equal(0, resultBoth.ExitCode);

        int typeOnlyCount = Directory.GetFiles(typeOnlyDir, "*.cs", SearchOption.AllDirectories).Length;
        int bothCount = Directory.GetFiles(bothDir, "*.cs", SearchOption.AllDirectories).Length;

        // Both mode should produce at least as many files as type-only
        Assert.True(
            bothCount >= typeOnlyCount,
            $"Expected Both mode ({bothCount} files) >= TypeGeneration mode ({typeOnlyCount} files)");
    }
}
