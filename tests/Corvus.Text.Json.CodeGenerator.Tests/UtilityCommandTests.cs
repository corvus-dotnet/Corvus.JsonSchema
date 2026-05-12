using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for the 'listNameHeuristics' and 'version' utility commands.
/// </summary>
[TestClass]
public class UtilityCommandTests
{
    [TestMethod]
    public async Task ListNameHeuristics_OutputsHeuristicNames()
    {
        ProcessResult result = await CodeGeneratorRunner.RunAsync("listNameHeuristics");

        // The command returns 1 as its success exit code
        Assert.AreEqual(1, result.ExitCode);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.StandardOutput),
            "Expected non-empty output from listNameHeuristics");

        // Should contain at least one heuristic entry
        string[] lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.IsTrue(lines.Length > 0, "Expected at least one heuristic in output");
    }

    [TestMethod]
    public async Task Version_OutputsVersionInfo()
    {
        ProcessResult result = await CodeGeneratorRunner.RunAsync("version");

        // The command returns 1 as its success exit code
        Assert.AreEqual(1, result.ExitCode);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.StandardOutput),
            "Expected non-empty version output");
    }
}