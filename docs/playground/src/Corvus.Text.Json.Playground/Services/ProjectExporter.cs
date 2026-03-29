using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Corvus.Text.Json.Playground.Models;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Exports the current playground state as a ZIP archive containing a
/// fully functional .NET project that can be built with dotnet build.
/// </summary>
public static partial class ProjectExporter
{
    private const string ProjectName = "PlaygroundProject";
    private const string RootNamespace = "Playground";

    /// <summary>
    /// Creates a ZIP archive containing the full project.
    /// </summary>
    public static byte[] Export(IReadOnlyList<SchemaFile> schemaFiles, string userCode)
    {
        using MemoryStream ms = new();
        using (ZipArchive archive = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. Schema files
            foreach (SchemaFile schema in schemaFiles)
            {
                AddTextEntry(archive, schema.Name, schema.Content);
            }

            // 2. Program.cs (user code)
            AddTextEntry(archive, "Program.cs", userCode);

            // 3. Model files — one per root schema with [JsonSchemaTypeGenerator]
            foreach (SchemaFile schema in schemaFiles)
            {
                if (!schema.IsRootType)
                {
                    continue;
                }

                string typeName = GetTypeName(schema);
                string modelContent = GenerateModelFile(schema.Name, typeName);
                AddTextEntry(archive, $"{typeName}.cs", modelContent);
            }

            // 4. .csproj
            AddTextEntry(archive, $"{ProjectName}.csproj", GenerateCsproj());

            // 5. ctjplayground.config
            string config = GenerateConfig(schemaFiles);
            AddTextEntry(archive, "ctjplayground.config", config);
        }

        ms.Position = 0;
        return ms.ToArray();
    }

    private static string GetTypeName(SchemaFile schema)
    {
        if (!string.IsNullOrEmpty(schema.TypeName))
        {
            return schema.TypeName;
        }

        // Derive from filename: "person-constraints.json" → "PersonConstraints"
        string nameWithoutExt = Path.GetFileNameWithoutExtension(schema.Name);
        return ToPascalCase(nameWithoutExt);
    }

    private static string ToPascalCase(string input)
    {
        // Split on hyphens, underscores, dots, and spaces
        string[] parts = PascalCaseSplitter().Split(input);
        StringBuilder sb = new();
        foreach (string part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                sb.Append(part[1..]);
            }
        }

        return sb.ToString();
    }

    private static string GenerateModelFile(string schemaFileName, string typeName)
    {
        return $"""
            using Corvus.Text.Json;

            namespace {RootNamespace};

            [JsonSchemaTypeGenerator("{schemaFileName}")]
            public readonly partial struct {typeName};
            """;
    }

    private static string GenerateCsproj()
    {
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net9.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>{RootNamespace}</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Corvus.Text.Json" Version="4.6.*" />
                <PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="4.6.*">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;
    }

    private static string GenerateConfig(IReadOnlyList<SchemaFile> schemaFiles)
    {
        var config = new PlaygroundConfig
        {
            Schemas = schemaFiles.Select(s => new PlaygroundConfigSchema
            {
                Name = s.Name,
                IsRootType = s.IsRootType,
                TypeName = s.IsRootType ? GetTypeName(s) : null,
            }).ToList(),
        };

        return JsonSerializer.Serialize(config, PlaygroundConfigContext.Default.PlaygroundConfig);
    }

    private static void AddTextEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
        using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    [GeneratedRegex(@"[-_.\s]+")]
    private static partial Regex PascalCaseSplitter();
}

/// <summary>
/// The ctjplayground.config file format.
/// </summary>
internal class PlaygroundConfig
{
    [JsonPropertyName("schemas")]
    public List<PlaygroundConfigSchema> Schemas { get; set; } = [];
}

/// <summary>
/// A schema entry in the playground config.
/// </summary>
internal class PlaygroundConfigSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("isRootType")]
    public bool IsRootType { get; set; }

    [JsonPropertyName("typeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeName { get; set; }
}

[JsonSerializable(typeof(PlaygroundConfig))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class PlaygroundConfigContext : JsonSerializerContext;
