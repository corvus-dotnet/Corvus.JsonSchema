using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for error handling when given invalid input.
/// </summary>
public class ErrorHandlingTests
{
    [Fact]
    public async Task Generate_MissingSchemaFile_ReturnsNonZeroExitCode()
    {
        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            "\"nonexistent-schema.json\" --rootNamespace TestGenerated");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Config_MissingConfigFile_ReturnsNonZeroExitCode()
    {
        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            "config \"nonexistent-config.json\"");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task ValidateDocument_MissingSchemaFile_ReturnsNonZeroExitCode()
    {
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "valid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"nonexistent-schema.json\" \"{document}\"");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task ValidateDocument_MissingDocumentFile_ReturnsNonZeroExitCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"nonexistent-document.json\"");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Generate_MissingRequiredNamespace_ReturnsNonZeroExitCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        // --rootNamespace is required; omitting it should fail
        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\"");

        Assert.NotEqual(0, result.ExitCode);
    }
}