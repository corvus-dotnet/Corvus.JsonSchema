using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for the 'validateDocument' command.
/// </summary>
public class ValidateDocumentCommandTests
{
    [Fact]
    public async Task ValidateDocument_ValidDocument_ReturnsZeroExitCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "valid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("valid", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateDocument_InvalidDocument_ReportsInvalid()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "invalid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        // The command always returns 0 — it reports results, doesn't signal failure via exit code
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("invalid", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateDocument_InvalidDocument_OutputContainsFailMarker()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "invalid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        // Should contain fail markers for the errors
        Assert.Contains("fail", result.StandardOutput);
    }

    [Fact]
    public async Task ValidateDocument_InvalidDocument_OutputContainsLineAndColumn()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "invalid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        // Output should contain line/column location in the format (line,col)
        // The output may be wrapped by Spectre.Console, so check without line breaks
        string normalized = result.StandardOutput.Replace("\r", "").Replace("\n", " ");
        Assert.Matches(@"\(\d+,\d+\)", normalized);
    }
}