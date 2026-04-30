using System.Text.Json;
using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for advanced config options: additionalFiles, namedTypes, namespaces,
/// assertFormat, useUnixLineEndings, addExplicitUsings, etc.
/// </summary>
public class ConfigOptionsTests : IDisposable
{
    private readonly string _outputDir;
    private readonly string _tempConfigDir;

    public ConfigOptionsTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
        _tempConfigDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
        CodeGeneratorRunner.CleanupTempDirectory(_tempConfigDir);
    }

    [Fact]
    public async Task Config_WithAdditionalFiles_ResolvesExternalRef()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.AdditionalFiles",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "person-with-ref.json") }
            },
            additionalFiles = new[]
            {
                new
                {
                    canonicalUri = "http://example.com/schemas/address",
                    contentPath = Path.Combine(schemasDir, "address.json")
                }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // Should have generated types for the address reference too
        bool hasAddressType = files.Any(f =>
            Path.GetFileName(f).Contains("Address", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasAddressType,
            $"Expected an Address type from the additional file. Files: {string.Join(", ", files.Select(Path.GetFileName))}");
    }

    [Fact]
    public async Task Config_WithNamedTypes_UsesSpecifiedTypeName()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string schemaPath = Path.Combine(schemasDir, "person-with-ref.json");
        string addressPath = Path.Combine(schemasDir, "address.json");

        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.NamedTypes",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new { schemaFile = schemaPath, outputRootTypeName = "PersonRecord" }
            },
            additionalFiles = new[]
            {
                new
                {
                    canonicalUri = "http://example.com/schemas/address",
                    contentPath = addressPath
                }
            },
            namedTypes = new[]
            {
                new
                {
                    reference = "http://example.com/schemas/address",
                    dotnetTypeName = "PostalAddress"
                }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // The named type should appear in the generated files
        bool hasNamedType = files.Any(f =>
            Path.GetFileName(f).Contains("PostalAddress", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasNamedType,
            $"Expected a PostalAddress type from namedTypes. Files: {string.Join(", ", files.Select(Path.GetFileName))}");
    }

    [Fact]
    public async Task Config_WithNamedTypeAndNamespace_GeneratesAtSpecifiedNamespace()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string schemaPath = Path.Combine(schemasDir, "person-with-ref.json");
        string addressPath = Path.Combine(schemasDir, "address.json");

        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.WithNamespace",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new { schemaFile = schemaPath, outputRootTypeName = "MyPerson" }
            },
            additionalFiles = new[]
            {
                new
                {
                    canonicalUri = "http://example.com/schemas/address",
                    contentPath = addressPath
                }
            },
            namedTypes = new[]
            {
                new
                {
                    reference = "http://example.com/schemas/address",
                    dotnetTypeName = "MailingAddress",
                    dotnetNamespace = "TestGenerated.Shared"
                }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // The named type with explicit namespace should be generated at root level
        bool hasType = files.Any(f =>
            Path.GetFileName(f).Contains("MailingAddress", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasType,
            $"Expected a MailingAddress type. Files: {string.Join(", ", files.Select(Path.GetFileName))}");

        // Verify the generated file contains the specified namespace
        string matchingFile = files.First(f =>
            Path.GetFileName(f).Contains("MailingAddress", StringComparison.OrdinalIgnoreCase));
        string content = await File.ReadAllTextAsync(matchingFile);
        Assert.Contains("TestGenerated.Shared", content);
    }

    [Fact]
    public async Task Config_WithOutputRootNamespace_OverridesDefault()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Default",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new
                {
                    schemaFile = Path.Combine(schemasDir, "simple-object.json"),
                    outputRootTypeName = "CustomPerson",
                    outputRootNamespace = "TestGenerated.Custom"
                }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // Verify the root type file uses the overridden namespace
        string rootFile = files.First(f =>
            Path.GetFileName(f).Contains("CustomPerson", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(f).Contains('.', StringComparison.Ordinal)
                || Path.GetFileName(f).Equals("CustomPerson.cs", StringComparison.OrdinalIgnoreCase));
        string content = await File.ReadAllTextAsync(rootFile);
        Assert.Contains("TestGenerated.Custom", content);
    }

    [Fact]
    public async Task Config_WithUseUnixLineEndings_GeneratesLfOnly()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Unix",
            outputPath = _outputDir,
            useUnixLineEndings = true,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json") }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, "Expected generated files");

        // Check that the generated files use LF, not CRLF
        string content = await File.ReadAllTextAsync(files[0]);
        Assert.DoesNotContain("\r\n", content);
        Assert.Contains("\n", content);
    }

    [Fact]
    public async Task Config_WithAddExplicitUsings_IncludesUsingStatements()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Usings",
            outputPath = _outputDir,
            addExplicitUsings = true,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json") }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, "Expected generated files");

        // With explicit usings, the GlobalDeclarations file should contain 'using global::System;'
        string globalDecl = files.FirstOrDefault(f =>
            Path.GetFileName(f).Contains("GlobalDeclarations", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(globalDecl);

        string content = await File.ReadAllTextAsync(globalDecl);
        Assert.Contains("using global::System;", content);
    }

    [Fact]
    public async Task Config_WithoutAddExplicitUsings_OmitsStandardUsings()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.NoUsings",
            outputPath = _outputDir,
            addExplicitUsings = false,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json") }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0, "Expected generated files");

        // Without explicit usings, the GlobalDeclarations file should NOT contain 'using global::System;'
        string globalDecl = files.FirstOrDefault(f =>
            Path.GetFileName(f).Contains("GlobalDeclarations", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(globalDecl);

        string content = await File.ReadAllTextAsync(globalDecl);
        Assert.DoesNotContain("using global::System;", content);
    }

    [Fact]
    public async Task Config_WithOutputMapFile_CreatesValidMapFile()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string mapFile = Path.Combine(_outputDir, "generated.map.json");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Map",
            outputPath = _outputDir,
            outputMapFile = mapFile,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json"), outputRootTypeName = "MappedPerson" }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(mapFile), $"Expected map file at {mapFile}");

        string mapContent = await File.ReadAllTextAsync(mapFile);

        // Map file should be a JSON array
        Assert.StartsWith("[", mapContent.TrimStart());

        // Map file should reference the generated type
        Assert.Contains("MappedPerson", mapContent);
    }

    [Fact]
    public async Task Config_WithAssertFormat_ProducesFiles()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Format",
            outputPath = _outputDir,
            assertFormat = true,
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "formatted-strings.json") }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0,
            $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Config_WithOptionalAsNullable_ProducesFiles()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Nullable",
            outputPath = _outputDir,
            optionalAsNullable = "NullOrUndefined",
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json") }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0,
            $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Config_WithNamespaces_MapsSchemaUriToNamespace()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string schemaPath = Path.Combine(schemasDir, "person-with-ref.json");
        string addressPath = Path.Combine(schemasDir, "address.json");

        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.NsMapped",
            outputPath = _outputDir,
            typesToGenerate = new[]
            {
                new { schemaFile = schemaPath, outputRootTypeName = "NsPerson" }
            },
            additionalFiles = new[]
            {
                new
                {
                    canonicalUri = "http://example.com/schemas/address",
                    contentPath = addressPath
                }
            },
            namespaces = new Dictionary<string, string>
            {
                ["http://example.com/schemas/"] = "TestGenerated.External"
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0,
            $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [Fact]
    public async Task Config_WithSchemaVariant_Draft201909_ProducesFiles()
    {
        string schemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");
        string configPath = CreateConfigFile(new {
            rootNamespace = "TestGenerated.Draft201909",
            outputPath = _outputDir,
            useSchema = "Draft201909",
            typesToGenerate = new[]
            {
                new { schemaFile = Path.Combine(schemasDir, "simple-object.json") }
            }
        });

        ProcessResult result = await CodeGeneratorRunner.RunAsync($"config \"{configPath}\"");

        Assert.Equal(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(files.Length > 0,
            $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    private string CreateConfigFile(object config)
    {
        string path = Path.Combine(_tempConfigDir, $"config-{Guid.NewGuid():N}.json");
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }
}