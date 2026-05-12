using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for the 'validateDocument' command.
/// </summary>
[TestClass]
public class ValidateDocumentCommandTests
{
    [TestMethod]
    public async Task ValidateDocument_ValidDocument_ReturnsZeroExitCode()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "valid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "valid", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ValidateDocument_InvalidDocument_ReportsInvalid()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "invalid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        // The command always returns 0 — it reports results, doesn't signal failure via exit code
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "invalid", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task ValidateDocument_InvalidDocument_OutputContainsFailMarker()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "invalid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        // Should contain fail markers for the errors
        StringAssert.Contains(result.StandardOutput, "fail");
    }

    [TestMethod]
    public async Task ValidateDocument_InvalidDocument_OutputContainsLineAndColumn()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string document = CodeGeneratorRunner.GetFixturePath("Documents", "invalid-person.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"validateDocument \"{schema}\" \"{document}\"");

        // Output should contain line/column location in the format (line,col)
        // Remove line breaks entirely to handle any Spectre.Console wrapping
        string normalized = result.StandardOutput.Replace("\r", "").Replace("\n", "");
        StringAssert.Matches(normalized, new System.Text.RegularExpressions.Regex(@"\(\d+,\d+\)"));
    }
}