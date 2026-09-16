// <copyright file="JsonSchemaLockFileCliTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Corvus.Json.CodeGenerator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The <c>jsonschema</c> command keeps its output folder's lock: several runs into one folder generate one program
/// for all their schemas, a run that changes nothing is skipped, a run with other options fails unless forced, and
/// files an earlier run wrote that a run no longer generates are deleted.
/// </summary>
[TestClass]
public class JsonSchemaLockFileCliTests
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

    private string Output => Path.Combine(this.root, "Generated");

    private string LockPath => Path.Combine(this.Output, JsonSchemaLockFile.LockFileName);

    [TestInitialize]
    public void Init()
    {
        this.root = Path.Combine(Path.GetTempPath(), "corvusjson-jsonschema-lock-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(this.root, "schemas"));
        File.WriteAllText(Path.Combine(this.root, "schemas", "a.json"), SchemaA);
        File.WriteAllText(Path.Combine(this.root, "schemas", "b.json"), SchemaB);
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
    public async Task TwoRunsIntoOneFolderGenerateOneProgramForBothSchemas()
    {
        await this.GenerateAsync("a.json", "A");
        await this.GenerateAsync("b.json", "B");

        string[] entryPoints = this.EntryPoints();
        Assert.IsTrue(entryPoints.Any(e => e.Contains("a.json#", StringComparison.Ordinal)), "the first run's schema is in the program: " + string.Join(", ", entryPoints));
        Assert.IsTrue(entryPoints.Any(e => e.Contains("b.json#", StringComparison.Ordinal)), "the second run's schema is in the program: " + string.Join(", ", entryPoints));
        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "A.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "B.cs")));

        // The shared string type refers to an entry of the one program, and that entry is a string schema.
        Match entry = Regex.Match(File.ReadAllText(Path.Combine(this.Output, "JsonString.JsonSchema.cs")), @"CorvusJsonSchemaProgram\.Entry\((\d+)\)");
        Assert.IsTrue(entry.Success);
        int index = int.Parse(entry.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(index < entryPoints.Length);
        Assert.IsTrue(entryPoints[index].EndsWith("#/properties/name", StringComparison.Ordinal) || entryPoints[index].EndsWith("#/properties/label", StringComparison.Ordinal), entryPoints[index]);

        Assert.IsTrue(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel lockFile));
        CollectionAssert.AreEqual(new[] { "../schemas/a.json", "../schemas/b.json" }, lockFile.Specification.As<GeneratorConfig>().TypesToGenerate.EnumerateArray().Select(s => (string)s.SchemaFile).ToArray());
    }

    [TestMethod]
    public async Task ARunThatChangesNothingIsSkippedUnlessForced()
    {
        await this.GenerateAsync("a.json", "A");
        byte[] lockBefore = File.ReadAllBytes(this.LockPath);

        ProcessResult skipped = await this.GenerateAsync("a.json", "A");

        StringAssert.Contains(skipped.StandardOutput, "Up to date");
        CollectionAssert.AreEqual(lockBefore, File.ReadAllBytes(this.LockPath), "a skipped run leaves the lock alone");

        ProcessResult forced = await this.GenerateAsync("a.json", "A", "--force");

        Assert.IsFalse(forced.StandardOutput.Contains("Up to date", StringComparison.Ordinal));
        CollectionAssert.AreNotEqual(lockBefore, File.ReadAllBytes(this.LockPath), "a forced run writes a new lock");
    }

    [TestMethod]
    public async Task ARunWithOtherOptionsFailsUnlessForcedAndThenAppliesThemToTheWholeFolder()
    {
        await this.GenerateAsync("a.json", "A");

        ProcessResult refused = await this.RunAsync("b.json", "B", "--nativeEnums None");

        Assert.AreEqual(1, refused.ExitCode, refused.CombinedOutput);
        StringAssert.Contains(refused.StandardOutput, "nativeEnums");
        Assert.IsFalse(File.Exists(Path.Combine(this.Output, "B.cs")), "nothing was generated");

        await this.GenerateAsync("b.json", "B", "--nativeEnums None --force");

        Assert.IsTrue(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel lockFile));
        GeneratorConfig specification = lockFile.Specification.As<GeneratorConfig>();
        Assert.AreEqual("None", specification.NativeEnumsValue?.GetString());
        Assert.AreEqual(2, specification.TypesToGenerate.GetArrayLength());
        Assert.IsTrue(this.EntryPoints().Any(e => e.Contains("a.json#", StringComparison.Ordinal)), "the whole folder was regenerated");
    }

    [TestMethod]
    public async Task FilesAnEarlierRunWroteThatTheRunNoLongerGeneratesAreDeleted()
    {
        await this.GenerateAsync("a.json", "A");
        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "A.cs")));

        await this.GenerateAsync("a.json", "Renamed");

        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "Renamed.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(this.Output, "A.cs")), "the same schema now generates Renamed, so A's files are stale");
        Assert.IsFalse(File.Exists(Path.Combine(this.Output, "A.JsonSchema.cs")));
    }

    [TestMethod]
    public async Task TheV4EngineKeepsTheLockTooAndAnotherEngineIsAConflict()
    {
        await this.GenerateAsync("a.json", "A", "--engine V4");
        await this.GenerateAsync("b.json", "B", "--engine V4");

        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "A.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(this.Output, "B.cs")));
        Assert.IsTrue(JsonSchemaLockFile.TryLoad(this.Output, out JsonSchemaLockFileModel lockFile));
        Assert.AreEqual("V4", lockFile.Engine.GetString());
        Assert.AreEqual(2, lockFile.Specification.As<GeneratorConfig>().TypesToGenerate.GetArrayLength());

        ProcessResult refused = await this.RunAsync("a.json", "A", string.Empty);

        Assert.AreEqual(1, refused.ExitCode, refused.CombinedOutput);
        StringAssert.Contains(refused.StandardOutput, "engine");
    }

    private string[] EntryPoints()
    {
        string program = File.ReadAllText(Path.Combine(this.Output, "CorvusJsonSchemaProgram.cs"));
        return Regex.Matches(program, "^\\s*\"(corvus-schema:[^\"]*)\",\\s*$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToArray();
    }

    private async Task<ProcessResult> GenerateAsync(string schema, string rootTypeName, string extra = "")
    {
        ProcessResult result = await this.RunAsync(schema, rootTypeName, extra);
        Assert.AreEqual(0, result.ExitCode, result.CombinedOutput);
        return result;
    }

    private Task<ProcessResult> RunAsync(string schema, string rootTypeName, string extra)
    {
        string schemaPath = Path.Combine(this.root, "schemas", schema);
        return CodeGeneratorRunner.RunAsync($"jsonschema \"{schemaPath}\" --rootNamespace Test.Models --outputRootTypeName {rootTypeName} --outputPath \"{this.Output}\" {extra}", this.root);
    }
}