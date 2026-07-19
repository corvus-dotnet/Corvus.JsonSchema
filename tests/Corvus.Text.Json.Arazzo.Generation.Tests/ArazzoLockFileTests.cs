// <copyright file="ArazzoLockFileTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Generation;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Tests the Arazzo code-generation lock file (#871): round-tripping the model, and — the Arazzo-specific part —
/// incremental up-to-date checking that re-resolves each recorded source through a loader and compares its digest, so a
/// remote source whose content changed forces a regeneration.
/// </summary>
[TestClass]
public class ArazzoLockFileTests
{
    private const string SourceUri = "file:///sources/api.json";
    private static readonly byte[] SourceV1 = Encoding.UTF8.GetBytes("""{ "openapi": "3.1.0", "paths": {} }""");
    private static readonly byte[] SourceV2 = Encoding.UTF8.GetBytes("""{ "openapi": "3.1.0", "paths": { "/x": {} } }""");

    private static readonly byte[] SampleArazzoBytes = Encoding.UTF8.GetBytes("""
        {
          "arazzo": "1.0.1",
          "info": { "title": "Test", "version": "1.0" },
          "sourceDescriptions": [{ "name": "api", "url": "./api.json", "type": "openapi" }],
          "workflows": []
        }
        """);

    private string tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "corvusjson-arazzo-lockfile-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(this.tempDir))
        {
            Directory.Delete(this.tempDir, recursive: true);
        }
    }

    // ── Round-trip: Create → Save → Load ──────────────────────────────────
    [TestMethod]
    public void Create_Save_TryLoad_RoundTrips()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);

        ArazzoLockFileModel original = ArazzoLockFile.Create(
            doc.RootElement, "TestNs", "MyClient", durable: true, Sources(SourceV1), ["OnboardWorkflowExecutor.cs"]);

        ArazzoLockFile.Save(in original, this.tempDir);

        Assert.IsTrue(ArazzoLockFile.TryLoad(this.tempDir, out ArazzoLockFileModel loaded));
        Assert.AreEqual(original.ArazzoFileHash.GetString(), loaded.ArazzoFileHash.GetString());
        Assert.AreEqual(original.RootNamespace.GetString(), loaded.RootNamespace.GetString());
        Assert.AreEqual(original.ClientName.GetString(), loaded.ClientName.GetString());
        Assert.AreEqual((bool)original.Durable, (bool)loaded.Durable);
        Assert.AreEqual(original.GeneratorVersion.GetString(), loaded.GeneratorVersion.GetString());

        // The one recorded source round-trips with its uri and digest.
        List<(string Uri, string Digest)> sources = [.. loaded.Sources.EnumerateArray().Select(s => ((string)s.UriValue, (string)s.Digest))];
        Assert.AreEqual(1, sources.Count);
        Assert.AreEqual(SourceUri, sources[0].Uri);
        Assert.AreEqual(ArazzoLockFile.ComputeDigest(SourceV1), sources[0].Digest);
    }

    [TestMethod]
    public void Create_Save_LockFileExistsOnDisk()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", null, false, Sources(SourceV1), ["A.cs"]);
        ArazzoLockFile.Save(in lockFile, this.tempDir);

        string expectedPath = Path.Combine(this.tempDir, "corvusjson-arazzo.lock");
        Assert.IsTrue(File.Exists(expectedPath));
        string json = File.ReadAllText(expectedPath);
        Assert.IsTrue(json.Contains("arazzoFileHash", StringComparison.Ordinal));
        Assert.IsTrue(json.Contains("sources", StringComparison.Ordinal));
    }

    // ── TryLoad edge cases ────────────────────────────────────────────────
    [TestMethod]
    public void TryLoad_NoFile_ReturnsFalse() => Assert.IsFalse(ArazzoLockFile.TryLoad(this.tempDir, out _));

    [TestMethod]
    public void TryLoad_CorruptJson_ReturnsFalse()
    {
        File.WriteAllText(Path.Combine(this.tempDir, "corvusjson-arazzo.lock"), "not valid json {{{");
        Assert.IsFalse(ArazzoLockFile.TryLoad(this.tempDir, out _));
    }

    // ── IsUpToDate: sources are re-resolved and compared by digest ─────────
    [TestMethod]
    public void IsUpToDate_SameParametersAndSources_ReturnsTrue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", "Client", true, Sources(SourceV1), ["A.cs"]);

        Assert.IsTrue(ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "Ns", "Client", true, Loader(SourceV1)));
    }

    [TestMethod]
    public void IsUpToDate_ChangedSourceContent_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", null, false, Sources(SourceV1), ["A.cs"]);

        // The source's url now serves different content — its digest no longer matches, so a regeneration is required.
        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "Ns", null, false, Loader(SourceV2)));
    }

    [TestMethod]
    public void IsUpToDate_UnresolvableSource_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", null, false, Sources(SourceV1), ["A.cs"]);

        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "Ns", null, false, _ => null));
    }

    [TestMethod]
    public void IsUpToDate_DifferentArazzoContent_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", null, false, Sources(SourceV1), ["A.cs"]);

        byte[] modified = Encoding.UTF8.GetBytes("""{ "arazzo": "1.0.1", "info": { "title": "Changed", "version": "2.0" }, "workflows": [] }""");
        using ParsedJsonDocument<JsonElement> modifiedDoc = ParsedJsonDocument<JsonElement>.Parse(modified);

        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in lockFile, modifiedDoc.RootElement, "Ns", null, false, Loader(SourceV1)));
    }

    [TestMethod]
    public void IsUpToDate_DifferentRootNamespace_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", null, false, Sources(SourceV1), ["A.cs"]);

        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "DifferentNs", null, false, Loader(SourceV1)));
    }

    [TestMethod]
    public void IsUpToDate_DifferentClientName_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", "ClientA", false, Sources(SourceV1), ["A.cs"]);

        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "Ns", "ClientB", false, Loader(SourceV1)));
    }

    [TestMethod]
    public void IsUpToDate_DifferentDurable_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "Ns", null, false, Sources(SourceV1), ["A.cs"]);

        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "Ns", null, true, Loader(SourceV1)));
    }

    [TestMethod]
    public void IsUpToDate_DifferentGeneratorVersion_ReturnsFalse()
    {
        string lockJson = $$"""
            {
              "arazzoFileHash": "0000000000000000000000000000000000000000000000000000000000000000",
              "rootNamespace": "Ns",
              "durable": false,
              "sources": [],
              "generatedFiles": [],
              "generatedAt": "2025-01-01T00:00:00Z",
              "generatorVersion": "0.0.0-fake"
            }
            """;
        using ParsedJsonDocument<ArazzoLockFileModel> lockDoc = ParsedJsonDocument<ArazzoLockFileModel>.Parse(lockJson);
        ArazzoLockFileModel lockFile = lockDoc.RootElement.Clone();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);

        Assert.IsFalse(
            ArazzoLockFile.IsUpToDate(in lockFile, doc.RootElement, "Ns", null, false, Loader(SourceV1)),
            "A lock file from a different generator version should not be considered up to date");
    }

    // ── Canonical hash: format-insensitive ────────────────────────────────
    [TestMethod]
    public void Create_SameContentDifferentWhitespace_ProducesSameHash()
    {
        byte[] compact = Encoding.UTF8.GetBytes("""{"arazzo":"1.0.1","info":{"title":"Test","version":"1.0"},"workflows":[]}""");
        byte[] pretty = Encoding.UTF8.GetBytes("""
            {
              "arazzo": "1.0.1",
              "info": {
                "title": "Test",
                "version": "1.0"
              },
              "workflows": []
            }
            """);
        using ParsedJsonDocument<JsonElement> compactDoc = ParsedJsonDocument<JsonElement>.Parse(compact);
        using ParsedJsonDocument<JsonElement> prettyDoc = ParsedJsonDocument<JsonElement>.Parse(pretty);

        ArazzoLockFileModel a = ArazzoLockFile.Create(compactDoc.RootElement, "Ns", null, false, [], ["A.cs"]);
        ArazzoLockFileModel b = ArazzoLockFile.Create(prettyDoc.RootElement, "Ns", null, false, [], ["A.cs"]);

        Assert.AreEqual(a.ArazzoFileHash.GetString(), b.ArazzoFileHash.GetString());
    }

    // ── Backup and restore ────────────────────────────────────────────────
    [TestMethod]
    public void BackupThenRestore_RestoresOriginal()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);

        ArazzoLockFileModel original = ArazzoLockFile.Create(doc.RootElement, "OriginalNs", null, false, Sources(SourceV1), ["A.cs"]);
        ArazzoLockFile.Save(in original, this.tempDir);
        Assert.IsTrue(ArazzoLockFile.BackupLockFile(this.tempDir));

        ArazzoLockFileModel overwritten = ArazzoLockFile.Create(doc.RootElement, "OverwrittenNs", null, false, Sources(SourceV1), ["B.cs"]);
        ArazzoLockFile.Save(in overwritten, this.tempDir);

        Assert.IsTrue(ArazzoLockFile.RestoreLockFile(this.tempDir));
        Assert.IsTrue(ArazzoLockFile.TryLoad(this.tempDir, out ArazzoLockFileModel restored));
        Assert.AreEqual("OriginalNs", restored.RootNamespace.GetString());
        Assert.IsFalse(File.Exists(Path.Combine(this.tempDir, "corvusjson-arazzo.lock.bak")));
    }

    // ── Full lifecycle: matches, then a source changes, then regenerates ──
    [TestMethod]
    public void Lifecycle_UpToDate_ThenSourceChanges_ThenRegenerates()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(SampleArazzoBytes);

        // Generate + save.
        ArazzoLockFileModel lockFile = ArazzoLockFile.Create(doc.RootElement, "MyApi", "Petstore", true, Sources(SourceV1), ["Exec.cs"]);
        ArazzoLockFile.Save(in lockFile, this.tempDir);

        // Second run, source unchanged → up to date.
        Assert.IsTrue(ArazzoLockFile.TryLoad(this.tempDir, out ArazzoLockFileModel loaded));
        Assert.IsTrue(ArazzoLockFile.IsUpToDate(in loaded, doc.RootElement, "MyApi", "Petstore", true, Loader(SourceV1)));

        // The referenced source's remote content changes → not up to date.
        Assert.IsFalse(ArazzoLockFile.IsUpToDate(in loaded, doc.RootElement, "MyApi", "Petstore", true, Loader(SourceV2)));

        // Regenerate against the new source content, overwrite the lock → up to date again.
        ArazzoLockFileModel updated = ArazzoLockFile.Create(doc.RootElement, "MyApi", "Petstore", true, Sources(SourceV2), ["Exec.cs"]);
        ArazzoLockFile.Save(in updated, this.tempDir);
        Assert.IsTrue(ArazzoLockFile.TryLoad(this.tempDir, out ArazzoLockFileModel reloaded));
        Assert.IsTrue(ArazzoLockFile.IsUpToDate(in reloaded, doc.RootElement, "MyApi", "Petstore", true, Loader(SourceV2)));
    }

    // A single-source list pinned by the digest of the given bytes.
    private static IReadOnlyList<ArazzoLockFile.LockedSource> Sources(byte[] bytes)
        => [new ArazzoLockFile.LockedSource(SourceUri, ArazzoLockFile.ComputeDigest(bytes))];

    // A loader that serves the given bytes for the sample source uri and nothing else.
    private static Func<Uri, byte[]?> Loader(byte[] bytes)
        => uri => string.Equals(uri.AbsoluteUri, SourceUri, StringComparison.Ordinal) ? bytes : null;
}