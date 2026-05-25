using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for YAML support in the JSON Schema, AsyncAPI, and OpenAPI generate commands.
/// </summary>
[TestClass]
public class YamlSupportTests : IDisposable
{
    private readonly string _outputDir;

    public YamlSupportTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [TestMethod]
    public async Task JsonSchema_YamlAutoDetectedFromExtension_ProducesFiles()
    {
        // The jsonschema command requires the explicit --yaml flag;
        // it does not auto-detect from .yaml extension (unlike asyncapi/openapi commands).
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --yaml");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files in {_outputDir}. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task JsonSchema_ExplicitYamlFlag_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --yaml");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task JsonSchema_YamlProducesSameTypesAsJson()
    {
        string yamlSchema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.yaml");
        string jsonSchema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string yamlOutput = Path.Combine(_outputDir, "yaml");
        string jsonOutput = Path.Combine(_outputDir, "json");

        Directory.CreateDirectory(yamlOutput);
        Directory.CreateDirectory(jsonOutput);

        ProcessResult yamlResult = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{yamlSchema}\" --rootNamespace TestGenerated --outputPath \"{yamlOutput}\" --yaml");
        ProcessResult jsonResult = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{jsonSchema}\" --rootNamespace TestGenerated --outputPath \"{jsonOutput}\"");

        Assert.AreEqual(0, yamlResult.ExitCode, $"YAML generation failed. Stderr: {yamlResult.StandardError}");
        Assert.AreEqual(0, jsonResult.ExitCode, $"JSON generation failed. Stderr: {jsonResult.StandardError}");

        string[] yamlFiles = Directory.GetFiles(yamlOutput, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToArray();
        string[] jsonFiles = Directory.GetFiles(jsonOutput, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToArray();

        CollectionAssert.AreEqual(jsonFiles, yamlFiles,
            $"YAML and JSON should produce the same file names. YAML: [{string.Join(", ", yamlFiles)}] JSON: [{string.Join(", ", jsonFiles)}]");
    }

    [TestMethod]
    public async Task AsyncApi_YamlAutoDetectedFromExtension_ProducesFiles()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "streetlights.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"asyncapi-generate \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files in {_outputDir}. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task AsyncApi_ExplicitYamlFlag_ProducesFiles()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "streetlights.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"asyncapi-generate \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --yaml --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task AsyncApi_YamlProducesConsumerAndProducer()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "streetlights.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"asyncapi-generate \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToArray();

        Assert.IsTrue(
            files.Any(f => f.Contains("Handler", StringComparison.OrdinalIgnoreCase)),
            $"Expected a handler file. Files: [{string.Join(", ", files)}]");
    }

    [TestMethod]
    public async Task OpenApi_YamlAutoDetectedFromExtension_ProducesFiles()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "petstore.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"openapi-client \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files in {_outputDir}. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task OpenApi_ExplicitYamlFlag_ProducesFiles()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "petstore.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"openapi-client \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --yaml --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task OpenApi_YamlProducesClientCode()
    {
        string spec = CodeGeneratorRunner.GetFixturePath("Specs", "petstore.yaml");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"openapi-client \"{spec}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToArray();

        Assert.IsTrue(files.Length > 1,
            $"Expected multiple generated files. Files: [{string.Join(", ", files)}]");
    }

    [TestMethod]
    public async Task JsonSchema_YmlExtension_WorksWithYamlFlag()
    {
        // Copy the .yaml file to .yml to test both extension variants
        string yamlSource = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.yaml");
        string ymlPath = Path.Combine(_outputDir, "schema.yml");
        File.Copy(yamlSource, ymlPath);

        string outputDir = Path.Combine(_outputDir, "output");
        Directory.CreateDirectory(outputDir);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{ymlPath}\" --rootNamespace TestGenerated --outputPath \"{outputDir}\" --yaml");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task JsonSchema_JsonExtension_NotTreatedAsYaml()
    {
        // JSON files with .json extension should work without YAML pre-processing
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task AsyncApi_YmlExtension_AutoDetected()
    {
        // Copy the .yaml spec to .yml to test auto-detection from .yml extension
        string yamlSource = CodeGeneratorRunner.GetFixturePath("Specs", "streetlights.yaml");
        string ymlPath = Path.Combine(_outputDir, "spec.yml");
        File.Copy(yamlSource, ymlPath);

        string outputDir = Path.Combine(_outputDir, "output");
        Directory.CreateDirectory(outputDir);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"asyncapi-generate \"{ymlPath}\" --rootNamespace TestGenerated --outputPath \"{outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task OpenApi_YmlExtension_AutoDetected()
    {
        // Copy the .yaml spec to .yml to test auto-detection from .yml extension
        string yamlSource = CodeGeneratorRunner.GetFixturePath("Specs", "petstore.yaml");
        string ymlPath = Path.Combine(_outputDir, "spec.yml");
        File.Copy(yamlSource, ymlPath);

        string outputDir = Path.Combine(_outputDir, "output");
        Directory.CreateDirectory(outputDir);

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"openapi-client \"{ymlPath}\" --rootNamespace TestGenerated --outputPath \"{outputDir}\" --force");

        Assert.AreEqual(0, result.ExitCode, $"Expected exit code 0. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
        Assert.IsTrue(
            Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }
}