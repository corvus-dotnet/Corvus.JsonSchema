using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for error handling when given invalid input.
/// </summary>
[TestClass]
public class ErrorHandlingTests
{
    [TestMethod]
    public async Task Generate_MissingSchemaFile_ReturnsNonZeroExitCode()
    {
        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            "jsonschema \"nonexistent-schema.json\" --rootNamespace TestGenerated");

        Assert.AreNotEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task Config_MissingConfigFile_ReturnsNonZeroExitCode()
    {
        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            "config \"nonexistent-config.json\"");

        Assert.AreNotEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task ValidateDocument_MissingSchemaFile_ReturnsNonZeroExitCode()
    {
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "valid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"nonexistent-schema.json\" \"{document}\"");

        Assert.AreNotEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task ValidateDocument_MissingDocumentFile_ReturnsNonZeroExitCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"nonexistent-document.json\"");

        Assert.AreNotEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task Generate_MissingRequiredNamespace_ReturnsNonZeroExitCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        // --rootNamespace is required; omitting it should fail
        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\"");

        Assert.AreNotEqual(0, result.ExitCode);
    }
}