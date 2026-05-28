// <copyright file="AsyncApiLockFileTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

[TestClass]
public class AsyncApiLockFileTests
{
    private static JsonElement streetlightsRoot;
    private string tempDir = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "streetlights.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        streetlightsRoot = doc.RootElement.Clone();
    }

    [TestInitialize]
    public void TestInit()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "lockfile-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(this.tempDir);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(this.tempDir))
        {
            Directory.Delete(this.tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Create_ProducesValidModel()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Streetlights.Client",
            mode: "both",
            filter: null,
            generatedFiles: ["TurnOnProducer.cs", "LightMeasuredConsumer.cs"]);

        Assert.AreEqual("3.0", lockFile.SpecVersion.GetString());
        Assert.AreEqual("Streetlights.Client", lockFile.RootNamespace.GetString());
        Assert.AreEqual("both", lockFile.Mode.GetString());
        Assert.IsTrue(lockFile.SpecFileHash.GetString()!.Length > 0);
        Assert.IsTrue(lockFile.GeneratorVersion.GetString()!.Length > 0);
    }

    [TestMethod]
    public void Create_WithFilter_PopulatesIncludeAndExclude()
    {
        OperationFilter filter = new(["events/*"], ["events/internal"]);

        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "producer",
            filter: filter,
            generatedFiles: ["Producer.cs"]);

        // Verify includeChannels and excludeChannels arrays are populated
        int includeCount = 0;
        foreach (JsonElement item in lockFile.IncludeChannels.EnumerateArray())
        {
            if (includeCount == 0)
            {
                Assert.AreEqual("events/*", item.GetString());
            }

            includeCount++;
        }

        Assert.AreEqual(1, includeCount);

        int excludeCount = 0;
        foreach (JsonElement item in lockFile.ExcludeChannels.EnumerateArray())
        {
            if (excludeCount == 0)
            {
                Assert.AreEqual("events/internal", item.GetString());
            }

            excludeCount++;
        }

        Assert.AreEqual(1, excludeCount);
    }

    [TestMethod]
    public void Create_WithTagFilter_PopulatesTags()
    {
        OperationFilter filter = new(tags: ["orders", "notifications"]);

        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "consumer",
            filter: filter,
            generatedFiles: []);

        List<string> tags = [];
        foreach (JsonElement item in lockFile.Tags.EnumerateArray())
        {
            tags.Add(item.GetString()!);
        }

        Assert.AreEqual(2, tags.Count);
        Assert.AreEqual("orders", tags[0]);
        Assert.AreEqual("notifications", tags[1]);
    }

    [TestMethod]
    public void SaveAndLoad_RoundTrips()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Roundtrip.Test",
            mode: "both",
            filter: null,
            generatedFiles: ["FileA.cs", "FileB.cs"]);

        AsyncApiLockFile.Save(in lockFile, this.tempDir);

        Assert.IsTrue(File.Exists(Path.Combine(this.tempDir, "corvusjson-asyncapi.lock")));

        bool loaded = AsyncApiLockFile.TryLoad(this.tempDir, out AsyncApiLockFileModel loaded2);
        Assert.IsTrue(loaded);
        Assert.AreEqual("3.0", loaded2.SpecVersion.GetString());
        Assert.AreEqual("Roundtrip.Test", loaded2.RootNamespace.GetString());
        Assert.AreEqual(lockFile.SpecFileHash.GetString(), loaded2.SpecFileHash.GetString());
    }

    [TestMethod]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        bool loaded = AsyncApiLockFile.TryLoad(this.tempDir, out _);
        Assert.IsFalse(loaded);
    }

    [TestMethod]
    public void TryLoad_InvalidJson_ReturnsFalse()
    {
        File.WriteAllText(Path.Combine(this.tempDir, "corvusjson-asyncapi.lock"), "not valid json");

        bool loaded = AsyncApiLockFile.TryLoad(this.tempDir, out _);
        Assert.IsFalse(loaded);
    }

    [TestMethod]
    public void IsUpToDate_SameParams_ReturnsTrue()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: null,
            generatedFiles: []);

        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Test", "both", null);
        Assert.IsTrue(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_SameFilterWithElements_ReturnsTrue()
    {
        OperationFilter filter = new(["events/*", "orders/*"], ["internal/*"]);
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: filter,
            generatedFiles: []);

        OperationFilter sameFilter = new(["events/*", "orders/*"], ["internal/*"]);
        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Test", "both", sameFilter);
        Assert.IsTrue(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentNamespace_ReturnsFalse()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: null,
            generatedFiles: []);

        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Different", "both", null);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentMode_ReturnsFalse()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: null,
            generatedFiles: []);

        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Test", "producer", null);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentSpec_ReturnsFalse()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: null,
            generatedFiles: []);

        // Use a different spec
        JsonElement differentSpec = JsonElement.ParseValue("""{"asyncapi":"3.0.0","info":{"title":"x","version":"1"}}"""u8);

        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in differentSpec, "3.0", "Test", "both", null);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentSpecVersion_ReturnsFalse()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: null,
            generatedFiles: []);

        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "2.6", "Test", "both", null);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentFilter_ReturnsFalse()
    {
        OperationFilter filter = new(["events/*"], []);
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: filter,
            generatedFiles: []);

        OperationFilter differentFilter = new(["other/*"], []);
        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Test", "both", differentFilter);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentExcludeFilter_ReturnsFalse()
    {
        OperationFilter filter = new([], ["internal/*"]);
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: filter,
            generatedFiles: []);

        OperationFilter differentFilter = new([], ["other/*"]);
        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Test", "both", differentFilter);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void IsUpToDate_DifferentTags_ReturnsFalse()
    {
        OperationFilter filter = new(tags: ["orders"]);
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: filter,
            generatedFiles: []);

        OperationFilter differentFilter = new(tags: ["users"]);
        bool upToDate = AsyncApiLockFile.IsUpToDate(
            in lockFile, in streetlightsRoot, "3.0", "Test", "both", differentFilter);
        Assert.IsFalse(upToDate);
    }

    [TestMethod]
    public void BackupAndRestore_Lifecycle()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Backup.Test",
            mode: "both",
            filter: null,
            generatedFiles: ["File.cs"]);

        AsyncApiLockFile.Save(in lockFile, this.tempDir);

        // Backup
        bool backed = AsyncApiLockFile.BackupLockFile(this.tempDir);
        Assert.IsTrue(backed);
        Assert.IsTrue(File.Exists(Path.Combine(this.tempDir, "corvusjson-asyncapi.lock.bak")));

        // Overwrite with different content
        JsonElement differentSpec = JsonElement.ParseValue("""{"asyncapi":"3.0.0","info":{"title":"x","version":"1"}}"""u8);
        AsyncApiLockFileModel newLock = AsyncApiLockFile.Create(
            in differentSpec, "3.0", "Different", "producer", null, []);
        AsyncApiLockFile.Save(in newLock, this.tempDir);

        // Restore
        bool restored = AsyncApiLockFile.RestoreLockFile(this.tempDir);
        Assert.IsTrue(restored);
        Assert.IsFalse(File.Exists(Path.Combine(this.tempDir, "corvusjson-asyncapi.lock.bak")));

        // Verify restored content
        Assert.IsTrue(AsyncApiLockFile.TryLoad(this.tempDir, out AsyncApiLockFileModel restoredLock));
        Assert.AreEqual("Backup.Test", restoredLock.RootNamespace.GetString());
    }

    [TestMethod]
    public void BackupLockFile_NoFile_ReturnsFalse()
    {
        bool backed = AsyncApiLockFile.BackupLockFile(this.tempDir);
        Assert.IsFalse(backed);
    }

    [TestMethod]
    public void RestoreLockFile_NoBackup_ReturnsFalse()
    {
        bool restored = AsyncApiLockFile.RestoreLockFile(this.tempDir);
        Assert.IsFalse(restored);
    }

    [TestMethod]
    public void DeleteBackup_RemovesBackupFile()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot, "3.0", "Test", "both", null, []);
        AsyncApiLockFile.Save(in lockFile, this.tempDir);
        AsyncApiLockFile.BackupLockFile(this.tempDir);

        Assert.IsTrue(File.Exists(Path.Combine(this.tempDir, "corvusjson-asyncapi.lock.bak")));

        AsyncApiLockFile.DeleteBackup(this.tempDir);

        Assert.IsFalse(File.Exists(Path.Combine(this.tempDir, "corvusjson-asyncapi.lock.bak")));
    }

    [TestMethod]
    public void DeleteBackup_NoBackup_DoesNotThrow()
    {
        // Should not throw even when no backup exists
        AsyncApiLockFile.DeleteBackup(this.tempDir);
    }

    [TestMethod]
    public void Create_WithDescriptionLocation_IncludesIt()
    {
        AsyncApiLockFileModel lockFile = AsyncApiLockFile.Create(
            in streetlightsRoot,
            specVersion: "3.0",
            rootNamespace: "Test",
            mode: "both",
            filter: null,
            generatedFiles: [],
            descriptionLocation: "https://example.com/api.json");

        Assert.AreEqual("https://example.com/api.json", lockFile.DescriptionLocation.GetString());
    }
}