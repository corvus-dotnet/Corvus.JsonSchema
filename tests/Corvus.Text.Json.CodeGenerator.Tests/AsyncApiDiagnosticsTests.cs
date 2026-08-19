using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Verifies the asyncapi-generate diagnostics surface: a reference the generator cannot
/// resolve is reported as a warning, generation still completes, and <c>--strict</c>
/// turns the warnings into a failed run.
/// </summary>
[TestClass]
public class AsyncApiDiagnosticsTests
{
    private string _outputDir;

    [TestInitialize]
    public void Initialize()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    [TestCleanup]
    public void Cleanup()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [TestMethod]
    public async Task AsyncApiGenerate_DanglingReference_WarnsAndSucceeds()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "dangling-ref-3.0.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"asyncapi-generate \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Generation should succeed without --strict. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        StringAssert.Contains(result.StandardOutput, "Warning:", "The dangling reference should be reported");
        StringAssert.Contains(result.StandardOutput, "#/operations/brokenOp", "The warning should carry the specification location");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            "The intact operation should still generate files");
    }

    [TestMethod]
    public async Task AsyncApiGenerate_DanglingReferenceWithStrict_Fails()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "dangling-ref-3.0.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"asyncapi-generate \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --force --strict");

        Assert.AreEqual(1, result.ExitCode, $"--strict should fail the run when generation produced warnings. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        StringAssert.Contains(result.StandardOutput, "--strict", "The failure should say --strict caused it");
    }
}