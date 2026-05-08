using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for the 'config' command that generates from a configuration file.
/// </summary>
[TestClass]
public class ConfigCommandTests : IDisposable
{
    private readonly string _outputDir;
    private readonly string _tempConfigDir;

    public ConfigCommandTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
        _tempConfigDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
        CodeGeneratorRunner.CleanupTempDirectory(_tempConfigDir);
    }

    [TestMethod]
    public async Task Config_SingleType_ProducesFiles()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json"), outputRootTypeName = "ConfigPerson" }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task Config_MultipleTypes_ProducesFilesForAll()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated",
            outputPath = _outputDir,
            typesToGenerate = new object[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json"), outputRootTypeName = "PersonType" },
                new { schemaFile = Path.Combine(schemasDir, "array-type.json"), outputRootTypeName = "ItemList" }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.AreEqual(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 1, $"Expected multiple generated files for multi-type config. Got {files.Length}. Stdout: {result.StandardOutput}");
    }

    [TestMethod]
    public async Task Config_WithV4Engine_ProducesFiles()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json"), outputRootTypeName = "V4Person" }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\" --engine V4");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected V4 generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    private string CreateConfigFile(object config)
    {
        string path = Path.Combine(_tempConfigDir, $"config-{Guid.NewGuid():N}.json");
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }
}