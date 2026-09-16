// <copyright file="JsonSchemaLockFileTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGenerator;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The lock a generation run leaves in its output folder: how a run's configuration is recorded, how a later run
/// merges into it, when nothing needs generating, and which files a run may delete.
/// </summary>
[TestClass]
public class JsonSchemaLockFileTests
{
    private const string SchemaA =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": { "name": { "type": "string" } },
          "required": ["name"]
        }
        """;

    private const string SchemaB =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": { "label": { "type": "string" }, "count": { "type": "integer" } }
        }
        """;

    private string root;

    private string Schemas => Path.Combine(this.root, "schemas");

    private string Output => Path.Combine(this.root, "Generated");

    private string A => Path.Combine(this.Schemas, "a.json");

    private string B => Path.Combine(this.Schemas, "b.json");

    [TestInitialize]
    public void Init()
    {
        this.root = Path.Combine(Path.GetTempPath(), "corvusjson-jsonschema-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.Schemas);
        Directory.CreateDirectory(this.Output);
        File.WriteAllText(this.A, SchemaA);
        File.WriteAllText(this.B, SchemaB);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    [TestMethod]
    public void ToRecorded_MakesLocalPathsRelativeToTheOutputFolderAndDropsOutputPath()
    {
        GeneratorConfig config = this.Config(this.A, "A");

        GeneratorConfig recorded = JsonSchemaLockFile.ToRecorded(config, this.Output);

        Assert.AreEqual("../schemas/a.json", SchemaFile(recorded, 0));
        Assert.IsFalse(recorded.TryGetProperty("outputPath", out JsonAny _), "outputPath is not recorded");

        GeneratorConfig absolute = JsonSchemaLockFile.ToAbsolute(recorded, this.Output);

        Assert.AreEqual(Path.GetFullPath(this.A), SchemaFile(absolute, 0));
        Assert.AreEqual(Path.GetFullPath(this.Output), absolute.OutputPath?.GetString());
    }

    [TestMethod]
    public void ToRecorded_LeavesARemoteSchemaAlone()
    {
        GeneratorConfig config = this.Config("https://example.com/schemas/remote.json", "Remote");

        GeneratorConfig recorded = JsonSchemaLockFile.ToRecorded(config, this.Output);
        GeneratorConfig absolute = JsonSchemaLockFile.ToAbsolute(recorded, this.Output);

        Assert.AreEqual("https://example.com/schemas/remote.json", SchemaFile(recorded, 0));
        Assert.AreEqual("https://example.com/schemas/remote.json", SchemaFile(absolute, 0));
        Assert.AreEqual(0, JsonSchemaLockFile.ComputeHashes(recorded, this.Output).Count, "a remote schema has no hash");
    }

    [TestMethod]
    public void GetOutputPath_IsTheConfiguredPathOrTheFirstSchemaFolder()
    {
        Assert.AreEqual(Path.GetFullPath(this.Output), JsonSchemaLockFile.GetOutputPath(this.Config(this.A, "A")));

        GeneratorConfig withoutOutput = this.Config(this.A, "A").RemoveProperty("outputPath");

        Assert.AreEqual(Path.GetFullPath(this.Schemas), JsonSchemaLockFile.GetOutputPath(withoutOutput));
    }

    [TestMethod]
    public void Merge_AddsTheRunsEntriesAndReplacesTheEntryWithTheSameSchemaAndRootPath()
    {
        GeneratorConfig first = this.Recorded(this.A, "A");
        GeneratorConfig second = this.Recorded(this.B, "B");

        JsonSchemaLockFile.MergeResult merged = JsonSchemaLockFile.Merge(first, second);

        Assert.AreEqual(0, merged.Conflicts.Count);
        CollectionAssert.AreEqual(new[] { "../schemas/a.json", "../schemas/b.json" }, SchemaFiles(merged.Specification));

        JsonSchemaLockFile.MergeResult renamed = JsonSchemaLockFile.Merge(merged.Specification, this.Recorded(this.A, "Renamed"));

        CollectionAssert.AreEqual(new[] { "../schemas/a.json", "../schemas/b.json" }, SchemaFiles(renamed.Specification));
        Assert.AreEqual("Renamed", RootTypeName(renamed.Specification, 0));
        Assert.AreEqual("B", RootTypeName(renamed.Specification, 1));
    }

    [TestMethod]
    public void Merge_WithoutALockKeepsTheRunAsItIs()
    {
        GeneratorConfig run = this.Recorded(this.A, "A");

        JsonSchemaLockFile.MergeResult merged = JsonSchemaLockFile.Merge(null, run);

        Assert.AreEqual(0, merged.Conflicts.Count);
        Assert.IsTrue(merged.Specification.AsAny.Equals(run.AsAny));
    }

    [TestMethod]
    public void Merge_ReportsTheOptionsThatDifferAndTreatsAnAbsentOptionAsItsDefault()
    {
        GeneratorConfig recorded = this.Recorded(this.A, "A");
        GeneratorConfig run = this.Recorded(this.B, "B")
            .SetProperty("assertFormat", new JsonBoolean(true))
            .SetProperty("nativeEnums", new JsonString("None"))
            .SetProperty("rootNamespace", new JsonString("Other.Models"));

        JsonSchemaLockFile.MergeResult merged = JsonSchemaLockFile.Merge(recorded, run);

        CollectionAssert.AreEqual(new[] { "nativeEnums", "rootNamespace" }, merged.Conflicts.ToArray(), "assertFormat true is the default, so spelling it out is not a conflict");
        Assert.AreEqual("None", merged.Specification.NativeEnumsValue?.GetString(), "the run's options are the merged options");
        Assert.AreEqual("Other.Models", (string)merged.Specification.RootNamespace);

        GeneratorConfig recordedWithOption = recorded.SetProperty("unions", new JsonBoolean(false));

        CollectionAssert.AreEqual(new[] { "unions" }, JsonSchemaLockFile.Merge(recordedWithOption, this.Recorded(this.B, "B")).Conflicts.ToArray(), "an option the lock has and the run leaves at its default differs");
    }

    [TestMethod]
    public void Merge_UnionsNamedTypesNamespacesAndAdditionalFilesByTheirIdentity()
    {
        GeneratorConfig recorded = GeneratorConfig.Parse(
            """
            {
              "rootNamespace": "Test.Models",
              "typesToGenerate": [ { "schemaFile": "../schemas/a.json" } ],
              "namedTypes": [ { "reference": "https://example.com/a#/$defs/x", "dotnetTypeName": "X" } ],
              "namespaces": { "https://example.com/a": "Test.A" },
              "additionalFiles": [ { "canonicalUri": "https://example.com/shared", "contentPath": "../schemas/shared.json" } ]
            }
            """);
        GeneratorConfig run = GeneratorConfig.Parse(
            """
            {
              "rootNamespace": "Test.Models",
              "typesToGenerate": [ { "schemaFile": "../schemas/b.json" } ],
              "namedTypes": [ { "reference": "https://example.com/a#/$defs/x", "dotnetTypeName": "Y" }, { "reference": "https://example.com/b#/$defs/z", "dotnetTypeName": "Z" } ],
              "namespaces": { "https://example.com/a": "Test.A2", "https://example.com/b": "Test.B" },
              "additionalFiles": [ { "canonicalUri": "https://example.com/other", "contentPath": "../schemas/other.json" } ]
            }
            """);

        GeneratorConfig merged = JsonSchemaLockFile.Merge(recorded, run).Specification;

        CollectionAssert.AreEqual(new[] { "X:Y", "Z:Z" }, merged.NamedTypes.Value.EnumerateArray().Select(n => $"{(n.Reference.GetString().EndsWith("x", StringComparison.Ordinal) ? "X" : "Z")}:{(string)n.DotnetTypeName}").ToArray());
        Assert.AreEqual("Test.A2", merged.Namespaces.Value.AsObject.TryGetProperty("https://example.com/a", out JsonAny a) ? a.AsString.GetString() : null);
        Assert.AreEqual("Test.B", merged.Namespaces.Value.AsObject.TryGetProperty("https://example.com/b", out JsonAny b) ? b.AsString.GetString() : null);
        CollectionAssert.AreEqual(new[] { "../schemas/shared.json", "../schemas/other.json" }, merged.AdditionalFiles.Value.EnumerateArray().Select(f => (string)f.ContentPath).ToArray());
    }

    [TestMethod]
    public void ComputeHashes_CoversTheLocalFilesAndFollowsTheirContent()
    {
        GeneratorConfig spec = JsonSchemaLockFile.Merge(this.Recorded(this.A, "A"), this.Recorded(this.B, "B")).Specification;

        IReadOnlyDictionary<string, string> hashes = JsonSchemaLockFile.ComputeHashes(spec, this.Output);

        CollectionAssert.AreEqual(new[] { "../schemas/a.json", "../schemas/b.json" }, hashes.Keys.ToArray());
        Assert.AreEqual(64, hashes["../schemas/a.json"].Length);

        // The hash is of the canonical form: whitespace does not change it, a keyword does.
        File.WriteAllText(this.A, SchemaA.Replace("\n", string.Empty).Replace(" ", string.Empty).Replace("\"type\":\"string\"", "\"type\": \"string\""));
        Assert.AreEqual(hashes["../schemas/a.json"], JsonSchemaLockFile.ComputeHashes(spec, this.Output)["../schemas/a.json"]);

        File.WriteAllText(this.A, SchemaA.Replace("\"required\": [\"name\"]", "\"required\": []"));
        Assert.AreNotEqual(hashes["../schemas/a.json"], JsonSchemaLockFile.ComputeHashes(spec, this.Output)["../schemas/a.json"]);

        File.WriteAllText(this.A, "not json");
        Assert.IsFalse(JsonSchemaLockFile.ComputeHashes(spec, this.Output).ContainsKey("../schemas/a.json"), "a file that is not a document has no hash");
    }

    [TestMethod]
    public void IsUpToDate_IsTrueForTheSameInputsAndFalseWhenAnyOfThemChanges()
    {
        GeneratorConfig spec = this.Recorded(this.A, "A");
        IReadOnlyDictionary<string, string> hashes = JsonSchemaLockFile.ComputeHashes(spec, this.Output);
        JsonSchemaLockFileModel lockFile = JsonSchemaLockFile.Create(spec, Engine.V5, CodeGenerationMode.TypeGeneration, hashes, ["A.cs"]);

        Assert.IsTrue(JsonSchemaLockFile.IsUpToDate(in lockFile, spec, Engine.V5, CodeGenerationMode.TypeGeneration, hashes));
        Assert.IsFalse(JsonSchemaLockFile.IsUpToDate(in lockFile, spec, Engine.V4, CodeGenerationMode.TypeGeneration, hashes), "engine");
        Assert.IsFalse(JsonSchemaLockFile.IsUpToDate(in lockFile, spec, Engine.V5, CodeGenerationMode.Both, hashes), "mode");

        GeneratorConfig withB = JsonSchemaLockFile.Merge(spec, this.Recorded(this.B, "B")).Specification;
        Assert.IsFalse(JsonSchemaLockFile.IsUpToDate(in lockFile, withB, Engine.V5, CodeGenerationMode.TypeGeneration, JsonSchemaLockFile.ComputeHashes(withB, this.Output)), "specification");

        File.WriteAllText(this.A, SchemaA.Replace("\"required\": [\"name\"]", "\"required\": []"));
        Assert.IsFalse(JsonSchemaLockFile.IsUpToDate(in lockFile, spec, Engine.V5, CodeGenerationMode.TypeGeneration, JsonSchemaLockFile.ComputeHashes(spec, this.Output)), "schema content");

        File.Delete(this.A);
        Assert.IsFalse(JsonSchemaLockFile.IsUpToDate(in lockFile, spec, Engine.V5, CodeGenerationMode.TypeGeneration, JsonSchemaLockFile.ComputeHashes(spec, this.Output)), "a schema without a hash");
    }

    [TestMethod]
    public void Create_Save_TryLoad_RoundTrips()
    {
        GeneratorConfig spec = this.Recorded(this.A, "A");
        IReadOnlyDictionary<string, string> hashes = JsonSchemaLockFile.ComputeHashes(spec, this.Output);
        JsonSchemaLockFileModel created = JsonSchemaLockFile.Create(spec, Engine.V5, CodeGenerationMode.Both, hashes, ["Models\\Z.cs", "A.cs"]);

        JsonSchemaLockFile.Save(in created, this.Output);

        Assert.IsTrue(File.Exists(Path.Combine(this.Output, JsonSchemaLockFile.LockFileName)));
        Assert.IsTrue(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel loaded));
        Assert.AreEqual("V5", loaded.Engine.GetString());
        Assert.AreEqual("Both", loaded.CodeGenerationMode.GetString());
        CollectionAssert.AreEqual(new[] { "A.cs", "Models/Z.cs" }, loaded.GeneratedFiles.EnumerateArray().Select(f => f.GetString()).ToArray(), "generated files are recorded with forward slashes in ordinal order");
        Assert.IsTrue(loaded.Specification.AsAny.Equals(spec.AsAny));
        Assert.IsTrue(JsonSchemaLockFile.IsUpToDate(in loaded, spec, Engine.V5, CodeGenerationMode.Both, hashes));
    }

    [TestMethod]
    public void TryLoad_ReturnsFalseWithoutAFileOrWithOneThatIsNotALock()
    {
        Assert.IsFalse(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel _));

        File.WriteAllText(Path.Combine(this.Output, JsonSchemaLockFile.LockFileName), "{ not json");
        Assert.IsFalse(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel _), "corrupt");

        File.WriteAllText(Path.Combine(this.Output, JsonSchemaLockFile.LockFileName), """{ "generatorVersion": "1" }""");
        Assert.IsFalse(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel _), "missing required properties");
    }

    [TestMethod]
    public void BackupAndRestore_PutTheLockBackAndDeleteBackupRemovesTheCopy()
    {
        Assert.IsFalse(JsonSchemaLockFile.BackupLockFile(this.Output), "nothing to back up");
        Assert.IsFalse(JsonSchemaLockFile.RestoreLockFile(this.Output), "nothing to restore");

        string lockPath = Path.Combine(this.Output, JsonSchemaLockFile.LockFileName);
        File.WriteAllText(lockPath, "first");
        Assert.IsTrue(JsonSchemaLockFile.BackupLockFile(this.Output));
        File.WriteAllText(lockPath, "second");
        Assert.IsTrue(JsonSchemaLockFile.RestoreLockFile(this.Output));
        Assert.AreEqual("first", File.ReadAllText(lockPath));
        Assert.IsFalse(File.Exists(lockPath + ".bak"));

        Assert.IsTrue(JsonSchemaLockFile.BackupLockFile(this.Output));
        JsonSchemaLockFile.DeleteBackup(this.Output);
        Assert.IsFalse(File.Exists(lockPath + ".bak"));
    }

    [TestMethod]
    public void DeleteStaleFiles_DeletesTheListedFilesTheRunDidNotWriteAndNothingElse()
    {
        File.WriteAllText(Path.Combine(this.Output, "Old.cs"), string.Empty);
        File.WriteAllText(Path.Combine(this.Output, "Kept.cs"), string.Empty);
        File.WriteAllText(Path.Combine(this.Output, "Foreign.cs"), string.Empty);
        File.WriteAllText(Path.Combine(this.root, "Outside.cs"), string.Empty);
        GeneratorConfig spec = this.Recorded(this.A, "A");
        JsonSchemaLockFileModel previous = JsonSchemaLockFile.Create(spec, Engine.V5, CodeGenerationMode.TypeGeneration, JsonSchemaLockFile.ComputeHashes(spec, this.Output), ["Old.cs", "Kept.cs", "../Outside.cs", "Missing.cs"]);

        IReadOnlyList<string> deleted = JsonSchemaLockFile.DeleteStaleFiles(in previous, ["kept.cs"], this.Output);

        CollectionAssert.AreEqual(new[] { "Old.cs" }, deleted.ToArray());
        Assert.IsFalse(File.Exists(Path.Combine(this.Output, "Old.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "Kept.cs")), "written again (compared without case)");
        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "Foreign.cs")), "never listed");
        Assert.IsTrue(File.Exists(Path.Combine(this.root, "Outside.cs")), "outside the folder");
    }

    private static string SchemaFile(GeneratorConfig config, int index)
    {
        return SchemaFiles(config)[index];
    }

    private static string[] SchemaFiles(GeneratorConfig config)
    {
        return config.TypesToGenerate.EnumerateArray().Select(s => (string)s.SchemaFile).ToArray();
    }

    private static string RootTypeName(GeneratorConfig config, int index)
    {
        return config.TypesToGenerate.EnumerateArray().ElementAt(index).OutputRootTypeName?.GetString();
    }

    private GeneratorConfig Recorded(string schemaFile, string rootTypeName)
    {
        return JsonSchemaLockFile.ToRecorded(this.Config(schemaFile, rootTypeName), this.Output);
    }

    private GeneratorConfig Config(string schemaFile, string rootTypeName)
    {
        GeneratorConfig.GenerationSpecification specification = GeneratorConfig.GenerationSpecification.Create(schemaFile: schemaFile, outputRootTypeName: rootTypeName);
        return GeneratorConfig.Create("Test.Models", [specification], outputPath: this.Output);
    }
}