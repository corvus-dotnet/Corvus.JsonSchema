// <copyright file="JsonSchemaLockFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.CodeGenerator;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// The <c>corvusjson-jsonschema.lock</c> file a JSON Schema generation run leaves in its output folder. It records the
/// folder's whole generation specification, so a later run into the folder adds its schemas to that specification and
/// regenerates the set as one program, instead of overwriting the program the earlier runs' types were wired to.
/// </summary>
/// <remarks>
/// A generation specification entry is identified by its schema file and root path; additional files by canonical
/// URI; named types by reference; namespaces by base URI. Local paths are recorded relative to the output folder with
/// forward slashes, so the folder can move together with its schemas.
/// </remarks>
public static class JsonSchemaLockFile
{
    /// <summary>The lock file name.</summary>
    public const string LockFileName = "corvusjson-jsonschema.lock";

    private const string BackupSuffix = ".bak";
    private const string TypesToGenerate = "typesToGenerate";
    private const string AdditionalFiles = "additionalFiles";
    private const string NamedTypes = "namedTypes";
    private const string Namespaces = "namespaces";
    private const string OutputPath = "outputPath";
    private const string OutputMapFile = "outputMapFile";

    // The properties merged as lists or maps, and the two that describe the output rather than what is generated.
    private static readonly HashSet<string> NotAnOption = new(StringComparer.Ordinal) { TypesToGenerate, AdditionalFiles, NamedTypes, Namespaces, OutputPath, OutputMapFile };

    // An option that is absent means its default; a run that spells the default out agrees with one that leaves it out.
    private static readonly Dictionary<string, JsonAny> Defaults = new(StringComparer.Ordinal)
    {
        ["assertFormat"] = new JsonBoolean(true).AsAny,
        ["disableOptionalNameHeuristics"] = new JsonBoolean(false).AsAny,
        ["optionalAsNullable"] = new JsonString("None").AsAny,
        ["nativeEnums"] = new JsonString("All").AsAny,
        ["unions"] = new JsonBoolean(true).AsAny,
        ["useImplicitOperatorString"] = new JsonBoolean(false).AsAny,
        ["useUnixLineEndings"] = new JsonBoolean(false).AsAny,
        ["supportYaml"] = new JsonBoolean(false).AsAny,
        ["addExplicitUsings"] = new JsonBoolean(false).AsAny,
        ["buildParametersThreshold"] = new JsonInteger(32).AsAny,
        ["defaultAccessibility"] = new JsonString("Public").AsAny,
        ["useSchema"] = new JsonString("Draft202012").AsAny,
    };

    /// <summary>
    /// The output folder a configuration generates into: its <c>outputPath</c>, else the folder of the first schema
    /// file that exists, else the current directory (the rule the generation drivers apply).
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The full path of the output folder.</returns>
    public static string GetOutputPath(GeneratorConfig config)
    {
        if (config.OutputPath is { } outputPath && !outputPath.IsNullOrUndefined())
        {
            return Path.GetFullPath((string)outputPath);
        }

        foreach (GeneratorConfig.GenerationSpecification specification in config.TypesToGenerate.EnumerateArray())
        {
            string schemaFile = (string)specification.SchemaFile;
            if (Path.Exists(schemaFile) && Path.GetDirectoryName(schemaFile) is { Length: > 0 } directory)
            {
                return Path.GetFullPath(directory);
            }
        }

        return Environment.CurrentDirectory;
    }

    /// <summary>
    /// Rewrites a configuration into the form a lock records: local schema and additional file paths relative to
    /// the output folder with forward slashes, and no <c>outputPath</c>.
    /// </summary>
    /// <param name="config">The configuration as the run supplied it.</param>
    /// <param name="outputPath">The output folder.</param>
    /// <returns>The recorded form.</returns>
    public static GeneratorConfig ToRecorded(GeneratorConfig config, string outputPath)
    {
        string fullOutput = Path.GetFullPath(outputPath);
        return RewritePaths(config.RemoveProperty(OutputPath), path => ToRecordedPath(path, fullOutput));
    }

    /// <summary>
    /// Rewrites a recorded specification into the form the generation drivers take: local paths absolute, and
    /// <c>outputPath</c> set to the output folder.
    /// </summary>
    /// <param name="specification">The recorded specification.</param>
    /// <param name="outputPath">The output folder.</param>
    /// <returns>The configuration to generate with.</returns>
    public static GeneratorConfig ToAbsolute(GeneratorConfig specification, string outputPath)
    {
        string fullOutput = Path.GetFullPath(outputPath);
        return RewritePaths(specification, path => ToAbsolutePath(path, fullOutput)).SetProperty(OutputPath, new JsonString(fullOutput));
    }

    /// <summary>
    /// Merges a run's configuration into the specification a lock recorded: the run's entries are added to the
    /// recorded lists, replacing an entry with the same identity; the options are the run's, and every option that
    /// differs from the recorded one is reported as a conflict.
    /// </summary>
    /// <param name="recorded">The specification the lock recorded, or <see langword="null"/> when there is no lock.</param>
    /// <param name="run">The run's configuration in recorded form (see <see cref="ToRecorded"/>).</param>
    /// <returns>The merged specification and the conflicting option names.</returns>
    public static MergeResult Merge(GeneratorConfig? recorded, GeneratorConfig run)
    {
        if (recorded is not GeneratorConfig previous)
        {
            return new MergeResult(run, []);
        }

        List<string> conflicts = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonObjectProperty property in run.EnumerateObject())
        {
            string name = property.Name.GetString();
            if (NotAnOption.Contains(name))
            {
                continue;
            }

            seen.Add(name);
            if (!OptionEquals(name, property.Value, GetProperty(previous, name)))
            {
                conflicts.Add(name);
            }
        }

        foreach (JsonObjectProperty property in previous.EnumerateObject())
        {
            string name = property.Name.GetString();
            if (NotAnOption.Contains(name) || seen.Contains(name))
            {
                continue;
            }

            if (!OptionEquals(name, JsonAny.Undefined, property.Value))
            {
                conflicts.Add(name);
            }
        }

        conflicts.Sort(StringComparer.Ordinal);

        GeneratorConfig merged = run;
        merged = SetList(merged, TypesToGenerate, MergeList(GetProperty(previous, TypesToGenerate), GetProperty(run, TypesToGenerate), SpecificationKey));
        merged = SetList(merged, AdditionalFiles, MergeList(GetProperty(previous, AdditionalFiles), GetProperty(run, AdditionalFiles), item => StringProperty(item, "canonicalUri")));
        merged = SetList(merged, NamedTypes, MergeList(GetProperty(previous, NamedTypes), GetProperty(run, NamedTypes), item => StringProperty(item, "reference")));
        merged = SetList(merged, Namespaces, MergeMap(GetProperty(previous, Namespaces), GetProperty(run, Namespaces)));
        return new MergeResult(merged, conflicts);
    }

    /// <summary>
    /// Computes the hash of every local schema file and additional file of a recorded specification: the SHA-256 of
    /// the RFC 8785 canonical form of the document, keyed by the path as recorded. A file that cannot be read or
    /// parsed has no entry.
    /// </summary>
    /// <param name="specification">The recorded specification.</param>
    /// <param name="outputPath">The output folder the recorded paths are relative to.</param>
    /// <returns>The hashes, in ordinal key order.</returns>
    public static IReadOnlyDictionary<string, string> ComputeHashes(GeneratorConfig specification, string outputPath)
    {
        string fullOutput = Path.GetFullPath(outputPath);
        bool yaml = specification.SupportYaml ?? false;
        SortedDictionary<string, string> hashes = new(StringComparer.Ordinal);
        foreach (GeneratorConfig.GenerationSpecification entry in specification.TypesToGenerate.EnumerateArray())
        {
            AddHash(hashes, (string)entry.SchemaFile, fullOutput, yaml);
        }

        if (specification.AdditionalFiles is GeneratorConfig.FileList files)
        {
            foreach (GeneratorConfig.FileSpecification file in files.EnumerateArray())
            {
                AddHash(hashes, (string)file.ContentPath, fullOutput, yaml);
            }
        }

        return hashes;
    }

    /// <summary>
    /// Decides whether a lock already describes what a run would generate: the same generator, engine and mode, the
    /// same specification, and the same hash for every schema.
    /// </summary>
    /// <param name="lockFile">The lock.</param>
    /// <param name="specification">The merged specification in recorded form.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="codeGenerationMode">The code generation mode.</param>
    /// <param name="hashes">The current hashes (see <see cref="ComputeHashes"/>).</param>
    /// <returns><see langword="true"/> when generation can be skipped.</returns>
    public static bool IsUpToDate(in JsonSchemaLockFileModel lockFile, GeneratorConfig specification, Engine engine, CodeGenerationMode codeGenerationMode, IReadOnlyDictionary<string, string> hashes)
    {
        if (lockFile.GeneratorVersion.GetString() != GetGeneratorVersion()
            || lockFile.Engine.GetString() != engine.ToString()
            || lockFile.CodeGenerationMode.GetString() != codeGenerationMode.ToString()
            || !lockFile.Specification.AsAny.Equals(specification.AsAny))
        {
            return false;
        }

        int count = 0;
        foreach (JsonObjectProperty property in lockFile.SchemaHashes.EnumerateObject())
        {
            count++;
            if (!hashes.TryGetValue(property.Name.GetString(), out string? hash) || hash != property.Value.AsString.GetString())
            {
                return false;
            }
        }

        return count == hashes.Count;
    }

    /// <summary>Creates the lock for a completed run.</summary>
    /// <param name="specification">The merged specification in recorded form.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="codeGenerationMode">The code generation mode.</param>
    /// <param name="hashes">The schema hashes.</param>
    /// <param name="generatedFiles">The files written, relative to the output folder.</param>
    /// <returns>The lock.</returns>
    public static JsonSchemaLockFileModel Create(GeneratorConfig specification, Engine engine, CodeGenerationMode codeGenerationMode, IReadOnlyDictionary<string, string> hashes, IReadOnlyList<string> generatedFiles)
    {
        List<(JsonPropertyName Name, JsonAny Value)> hashProperties = [];
        foreach (string key in hashes.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            hashProperties.Add(((JsonPropertyName)key, new JsonString(hashes[key]).AsAny));
        }

        return JsonObject.FromProperties(
            ((JsonPropertyName)"generatorVersion", new JsonString(GetGeneratorVersion()).AsAny),
            ((JsonPropertyName)"generatedAt", new JsonString(DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).AsAny),
            ((JsonPropertyName)"engine", new JsonString(engine.ToString()).AsAny),
            ((JsonPropertyName)"codeGenerationMode", new JsonString(codeGenerationMode.ToString()).AsAny),
            ((JsonPropertyName)"specification", specification.AsAny),
            ((JsonPropertyName)"schemaHashes", JsonObject.FromProperties([.. hashProperties]).AsAny),
            ((JsonPropertyName)"generatedFiles", JsonArray.FromRange(generatedFiles.Select(NormalizeSeparators).OrderBy(f => f, StringComparer.Ordinal)).AsAny))
            .As<JsonSchemaLockFileModel>();
    }

    /// <summary>Tries to load the lock of an output folder.</summary>
    /// <param name="outputPath">The output folder.</param>
    /// <param name="result">The lock, when the folder has a valid one.</param>
    /// <returns><see langword="true"/> when a valid lock was loaded.</returns>
    public static bool TryLoad(string outputPath, out JsonSchemaLockFileModel result)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        if (!File.Exists(filePath))
        {
            result = default;
            return false;
        }

        try
        {
            result = JsonSchemaLockFileModel.Parse(File.ReadAllBytes(filePath));
            return result.IsValid();
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }

    /// <summary>Saves the lock into an output folder.</summary>
    /// <param name="lockFile">The lock.</param>
    /// <param name="outputPath">The output folder.</param>
    public static void Save(in JsonSchemaLockFileModel lockFile, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            lockFile.WriteTo(writer);
        }

        File.WriteAllBytes(Path.Combine(outputPath, LockFileName), buffer.WrittenSpan.ToArray());
    }

    /// <summary>
    /// Copies the lock aside before a run, so it can be put back if the run fails.
    /// </summary>
    /// <param name="outputPath">The output folder.</param>
    /// <returns><see langword="true"/> when there was a lock to copy.</returns>
    public static bool BackupLockFile(string outputPath)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Copy(filePath, filePath + BackupSuffix, overwrite: true);
        return true;
    }

    /// <summary>Puts the lock back from its copy after a failed run.</summary>
    /// <param name="outputPath">The output folder.</param>
    /// <returns><see langword="true"/> when there was a copy to put back.</returns>
    public static bool RestoreLockFile(string outputPath)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        string backupPath = filePath + BackupSuffix;
        if (!File.Exists(backupPath))
        {
            return false;
        }

        File.Copy(backupPath, filePath, overwrite: true);
        File.Delete(backupPath);
        return true;
    }

    /// <summary>Deletes the copy of the lock after a successful run.</summary>
    /// <param name="outputPath">The output folder.</param>
    public static void DeleteBackup(string outputPath)
    {
        string backupPath = Path.Combine(outputPath, LockFileName + BackupSuffix);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    /// <summary>
    /// Deletes the files a previous lock listed that the run did not write again. Only files inside the output
    /// folder are deleted; files the lock never listed are never touched.
    /// </summary>
    /// <param name="previous">The previous lock.</param>
    /// <param name="generatedFiles">The files the run wrote, relative to the output folder.</param>
    /// <param name="outputPath">The output folder.</param>
    /// <returns>The files deleted, relative to the output folder.</returns>
    public static IReadOnlyList<string> DeleteStaleFiles(in JsonSchemaLockFileModel previous, IReadOnlyList<string> generatedFiles, string outputPath)
    {
        string fullOutput = Path.GetFullPath(outputPath);
        string prefix = fullOutput.EndsWith(Path.DirectorySeparatorChar) ? fullOutput : fullOutput + Path.DirectorySeparatorChar;
        HashSet<string> kept = new(generatedFiles.Select(NormalizeSeparators), StringComparer.OrdinalIgnoreCase);
        List<string> deleted = [];
        foreach (JsonString file in previous.GeneratedFiles.EnumerateArray())
        {
            string relative = NormalizeSeparators((string)file);
            if (kept.Contains(relative))
            {
                continue;
            }

            string full = Path.GetFullPath(Path.Combine(fullOutput, relative));
            if (full.StartsWith(prefix, StringComparison.Ordinal) && File.Exists(full))
            {
                File.Delete(full);
                deleted.Add(relative);
            }
        }

        return deleted;
    }

    /// <summary>Turns a path written by a driver into the recorded form: relative to the output folder with forward slashes.</summary>
    /// <param name="fullPath">The path the driver wrote.</param>
    /// <param name="outputPath">The output folder.</param>
    /// <returns>The recorded form.</returns>
    public static string ToGeneratedFile(string fullPath, string outputPath)
    {
        return NormalizeSeparators(Path.GetRelativePath(Path.GetFullPath(outputPath), Path.GetFullPath(fullPath)));
    }

    private static string NormalizeSeparators(string path) => path.Replace('\\', '/');

    private static bool IsLocal(string path)
    {
        return !(Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && !uri.IsFile);
    }

    private static string ToRecordedPath(string path, string fullOutput)
    {
        return IsLocal(path) ? NormalizeSeparators(Path.GetRelativePath(fullOutput, Path.GetFullPath(path))) : path;
    }

    private static string ToAbsolutePath(string recorded, string fullOutput)
    {
        return IsLocal(recorded) ? Path.GetFullPath(Path.Combine(fullOutput, recorded)) : recorded;
    }

    private static GeneratorConfig RewritePaths(GeneratorConfig config, Func<string, string> rewrite)
    {
        List<GeneratorConfig.GenerationSpecification> specifications = [];
        foreach (GeneratorConfig.GenerationSpecification entry in config.TypesToGenerate.EnumerateArray())
        {
            specifications.Add(entry.SetProperty("schemaFile", new JsonString(rewrite((string)entry.SchemaFile))));
        }

        GeneratorConfig result = config.SetProperty(TypesToGenerate, JsonArray.FromRange(specifications));
        if (config.AdditionalFiles is GeneratorConfig.FileList files)
        {
            List<GeneratorConfig.FileSpecification> rewritten = [];
            foreach (GeneratorConfig.FileSpecification file in files.EnumerateArray())
            {
                rewritten.Add(file.SetProperty("contentPath", new JsonString(rewrite((string)file.ContentPath))));
            }

            result = result.SetProperty(AdditionalFiles, JsonArray.FromRange(rewritten));
        }

        return result;
    }

    private static JsonAny GetProperty(GeneratorConfig config, string name)
    {
        return config.TryGetProperty(name, out JsonAny value) ? value : JsonAny.Undefined;
    }

    private static string StringProperty(JsonAny item, string name)
    {
        return item.AsObject.TryGetProperty(name, out JsonAny value) && value.ValueKind == JsonValueKind.String ? (string)value.AsString : string.Empty;
    }

    private static string SpecificationKey(JsonAny item)
    {
        return StringProperty(item, "schemaFile") + "#" + StringProperty(item, "rootPath");
    }

    private static bool OptionEquals(string name, JsonAny run, JsonAny recorded)
    {
        if (run.IsUndefined() && Defaults.TryGetValue(name, out JsonAny runDefault))
        {
            run = runDefault;
        }

        if (recorded.IsUndefined() && Defaults.TryGetValue(name, out JsonAny recordedDefault))
        {
            recorded = recordedDefault;
        }

        if (run.IsUndefined() || recorded.IsUndefined())
        {
            return run.IsUndefined() && recorded.IsUndefined();
        }

        return run.Equals(recorded);
    }

    private static GeneratorConfig SetList(GeneratorConfig config, string name, JsonAny? merged)
    {
        return merged is JsonAny value ? config.SetProperty(name, value) : config;
    }

    private static JsonAny? MergeList(JsonAny recorded, JsonAny run, Func<JsonAny, string> key)
    {
        if (recorded.ValueKind != JsonValueKind.Array && run.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<JsonAny> items = [];
        Dictionary<string, int> index = new(StringComparer.Ordinal);
        if (recorded.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonAny item in recorded.AsArray.EnumerateArray())
            {
                index[key(item)] = items.Count;
                items.Add(item);
            }
        }

        if (run.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonAny item in run.AsArray.EnumerateArray())
            {
                if (index.TryGetValue(key(item), out int existing))
                {
                    items[existing] = item;
                }
                else
                {
                    index[key(item)] = items.Count;
                    items.Add(item);
                }
            }
        }

        return JsonArray.FromRange(items).AsAny;
    }

    private static JsonAny? MergeMap(JsonAny recorded, JsonAny run)
    {
        if (recorded.ValueKind != JsonValueKind.Object && run.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        List<(JsonPropertyName Name, JsonAny Value)> properties = [];
        Dictionary<string, int> index = new(StringComparer.Ordinal);
        foreach (JsonAny source in new[] { recorded, run })
        {
            if (source.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (JsonObjectProperty property in source.AsObject.EnumerateObject())
            {
                string name = property.Name.GetString();
                if (index.TryGetValue(name, out int existing))
                {
                    properties[existing] = (property.Name, property.Value);
                }
                else
                {
                    index[name] = properties.Count;
                    properties.Add((property.Name, property.Value));
                }
            }
        }

        return JsonObject.FromProperties([.. properties]).AsAny;
    }

    private static void AddHash(SortedDictionary<string, string> hashes, string recorded, string fullOutput, bool yaml)
    {
        if (!IsLocal(recorded) || hashes.ContainsKey(recorded))
        {
            return;
        }

        string path = ToAbsolutePath(recorded, fullOutput);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            byte[] bytes;
            string extension = Path.GetExtension(path);
            if (yaml && (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)))
            {
                using FileStream input = File.OpenRead(path);
                using Stream processed = new YamlPreProcessor().Process(input);
                using MemoryStream memory = new();
                processed.CopyTo(memory);
                bytes = memory.ToArray();
            }
            else
            {
                bytes = File.ReadAllBytes(path);
            }

            using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(bytes);
            Corvus.Text.Json.JsonElement root = document.RootElement;
            hashes[recorded] = Convert.ToHexStringLower(SHA256.HashData(JsonCanonicalizer.Canonicalize(in root)));
        }
        catch (Exception exception) when (exception is JsonException or Corvus.Text.Json.JsonException or IOException or InvalidOperationException or FormatException)
        {
            // The file is not a JSON document the generator can use; the run reports that, and the folder is never up to date.
        }
    }

    private static string GetGeneratorVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
    }

    /// <summary>The result of merging a run into a recorded specification.</summary>
    /// <param name="specification">The merged specification in recorded form.</param>
    /// <param name="conflicts">The options whose value differs between the run and the lock, in ordinal order.</param>
    public sealed class MergeResult(GeneratorConfig specification, IReadOnlyList<string> conflicts)
    {
        /// <summary>Gets the merged specification in recorded form.</summary>
        public GeneratorConfig Specification { get; } = specification;

        /// <summary>Gets the options whose value differs between the run and the lock.</summary>
        public IReadOnlyList<string> Conflicts { get; } = conflicts;
    }
}