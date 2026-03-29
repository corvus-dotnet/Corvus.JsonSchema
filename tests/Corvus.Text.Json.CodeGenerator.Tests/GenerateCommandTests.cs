using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for the default 'generate' command using the V5 engine.
/// </summary>
public class GenerateCommandTests : IDisposable
{
    private readonly string _outputDir;

    public GenerateCommandTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [Fact]
    public async Task Generate_SimpleObject_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files in {_outputDir}. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Generate_ArrayType_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "array-type.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Generate_ComposedType_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "composed-type.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Generate_WithOutputRootTypeName_UsesSpecifiedName()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputRootTypeName MyPerson");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, "Expected generated files");

        // The root type file should contain the specified type name
        bool foundTypeName = files.Any(f => Path.GetFileName(f).Contains("MyPerson", StringComparison.OrdinalIgnoreCase));
        Assert.True(foundTypeName, $"Expected a file containing 'MyPerson'. Files: {string.Join(", ", files.Select(Path.GetFileName))}");
    }

    [Fact]
    public async Task Generate_WithOutputMapFile_CreatesMapFile()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string mapFile = Path.Combine(_outputDir, "output.map.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputMapFile \"{mapFile}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(mapFile), $"Expected map file at {mapFile}. Stdout: {result.StandardOutput}");

        string mapContent = await File.ReadAllTextAsync(mapFile);
        Assert.StartsWith("[", mapContent.TrimStart());
    }

    [Fact]
    public async Task Generate_WithV4Engine_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --engine V4");

        Assert.Equal(0, result.ExitCode);
        Assert.True(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected V4 generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Generate_WithOptionalAsNullable_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"\"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --optionalAsNullable NullOrUndefined");

        Assert.Equal(0, result.ExitCode);
        Assert.True(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }
}