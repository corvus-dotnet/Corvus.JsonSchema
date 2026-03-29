using System.IO.Compression;
using System.Text.Json;
using Corvus.Text.Json.Playground.Models;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Imports a playground project from a ZIP archive produced by <see cref="ProjectExporter"/>.
/// Reads ctjplayground.config + schema files + Program.cs; ignores .csproj and model .cs files.
/// </summary>
public static class ProjectImporter
{
    /// <summary>
    /// The result of importing a project ZIP.
    /// </summary>
    public record ImportResult(
        List<SchemaFile> SchemaFiles,
        string UserCode);

    /// <summary>
    /// Import a project from a ZIP byte array.
    /// </summary>
    public static ImportResult Import(byte[] zipBytes)
    {
        using MemoryStream ms = new(zipBytes);
        using ZipArchive archive = new(ms, ZipArchiveMode.Read);

        // Read the config file
        ZipArchiveEntry? configEntry = archive.GetEntry("ctjplayground.config");
        if (configEntry is null)
        {
            throw new InvalidOperationException(
                "Not a valid playground project — missing ctjplayground.config");
        }

        PlaygroundConfig config;
        using (Stream configStream = configEntry.Open())
        {
            config = JsonSerializer.Deserialize(configStream, PlaygroundConfigContext.Default.PlaygroundConfig)
                ?? throw new InvalidOperationException("Failed to parse ctjplayground.config");
        }

        // Read schema files as defined in config
        var schemaFiles = new List<SchemaFile>();
        foreach (PlaygroundConfigSchema schemaConfig in config.Schemas)
        {
            ZipArchiveEntry? schemaEntry = archive.GetEntry(schemaConfig.Name);
            if (schemaEntry is null)
            {
                continue;
            }

            using StreamReader reader = new(schemaEntry.Open());
            schemaFiles.Add(new SchemaFile
            {
                Name = schemaConfig.Name,
                Content = reader.ReadToEnd(),
                IsRootType = schemaConfig.IsRootType,
                TypeName = schemaConfig.TypeName,
            });
        }

        // Read Program.cs
        string userCode = "using Corvus.Text.Json;\nusing Playground;\n";
        ZipArchiveEntry? programEntry = archive.GetEntry("Program.cs");
        if (programEntry is not null)
        {
            using StreamReader reader = new(programEntry.Open());
            userCode = reader.ReadToEnd();
        }

        return new ImportResult(schemaFiles, userCode);
    }
}
