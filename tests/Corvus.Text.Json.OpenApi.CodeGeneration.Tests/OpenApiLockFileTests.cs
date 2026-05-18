// <copyright file="OpenApiLockFileTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OpenApiLockFileTests
{
    private static readonly byte[] SampleSpec = Encoding.UTF8.GetBytes("""
        {
          "openapi": "3.1.0",
          "info": { "title": "Test", "version": "1.0" },
          "paths": {
            "/items": {
              "get": {
                "operationId": "listItems",
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """);

    private string tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), "corvusjson-lockfile-test-" + Guid.NewGuid().ToString("N"));
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
        OpenApiLockFileModel original = OpenApiLockFile.Create(
            SampleSpec,
            "3.1",
            "TestNamespace",
            "MyClient",
            filter: null,
            generatedFiles: ["ListItemsRequest.cs", "ListItemsResponse.cs"]);

        OpenApiLockFile.Save(in original, this.tempDir);

        Assert.IsTrue(OpenApiLockFile.TryLoad(this.tempDir, out OpenApiLockFileModel loaded));

        Assert.AreEqual(original.SpecFileHash.GetString(), loaded.SpecFileHash.GetString());
        Assert.AreEqual(original.SpecVersion.GetString(), loaded.SpecVersion.GetString());
        Assert.AreEqual(original.RootNamespace.GetString(), loaded.RootNamespace.GetString());
        Assert.AreEqual(original.ClientName.GetString(), loaded.ClientName.GetString());
        Assert.AreEqual(original.GeneratorVersion.GetString(), loaded.GeneratorVersion.GetString());
        AssertJsonStringArraysEqual(original.GeneratedFiles, loaded.GeneratedFiles);
        AssertJsonStringArraysEqual(original.IncludePaths, loaded.IncludePaths);
        AssertJsonStringArraysEqual(original.ExcludePaths, loaded.ExcludePaths);
    }

    [TestMethod]
    public void Create_Save_LockFileExistsOnDisk()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);

        OpenApiLockFile.Save(in lockFile, this.tempDir);

        string expectedPath = Path.Combine(this.tempDir, "corvusjson-openapi.lock");
        Assert.IsTrue(File.Exists(expectedPath));

        string json = File.ReadAllText(expectedPath);
        Assert.IsTrue(json.Contains("specFileHash", StringComparison.Ordinal));
        Assert.IsTrue(json.Contains("generatedFiles", StringComparison.Ordinal));
    }

    // ── TryLoad edge cases ────────────────────────────────────────────────
    [TestMethod]
    public void TryLoad_NoFile_ReturnsFalse()
    {
        Assert.IsFalse(OpenApiLockFile.TryLoad(this.tempDir, out _));
    }

    [TestMethod]
    public void TryLoad_CorruptJson_ReturnsFalse()
    {
        string lockPath = Path.Combine(this.tempDir, "corvusjson-openapi.lock");
        File.WriteAllText(lockPath, "not valid json {{{");

        Assert.IsFalse(OpenApiLockFile.TryLoad(this.tempDir, out _));
    }

    [TestMethod]
    public void TryLoad_EmptyJson_ReturnsFalse()
    {
        string lockPath = Path.Combine(this.tempDir, "corvusjson-openapi.lock");
        File.WriteAllText(lockPath, string.Empty);

        Assert.IsFalse(OpenApiLockFile.TryLoad(this.tempDir, out _));
    }

    // ── IsUpToDate: identical parameters ──────────────────────────────────
    [TestMethod]
    public void IsUpToDate_SameParameters_ReturnsTrue()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", "Client", filter: null, generatedFiles: ["A.cs"]);

        bool result = OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", "Client", filter: null);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsUpToDate_AfterRoundTrip_ReturnsTrue()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        OpenApiLockFile.Save(in lockFile, this.tempDir);
        Assert.IsTrue(OpenApiLockFile.TryLoad(this.tempDir, out OpenApiLockFileModel loaded));

        bool result = OpenApiLockFile.IsUpToDate(in loaded, SampleSpec, "3.1", "TestNs", null, filter: null);

        Assert.IsTrue(result);
    }

    // ── IsUpToDate: each parameter change triggers false ──────────────────
    [TestMethod]
    public void IsUpToDate_DifferentSpecContent_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        byte[] modifiedSpec = Encoding.UTF8.GetBytes("""{ "openapi": "3.1.0", "info": { "title": "Modified", "version": "2.0" }, "paths": {} }""");

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, modifiedSpec, "3.1", "TestNs", null, filter: null));
    }

    [TestMethod]
    public void IsUpToDate_DifferentSpecVersion_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.0", "TestNs", null, filter: null));
    }

    [TestMethod]
    public void IsUpToDate_DifferentRootNamespace_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "DifferentNs", null, filter: null));
    }

    [TestMethod]
    public void IsUpToDate_DifferentClientName_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", "ClientA", filter: null, generatedFiles: ["A.cs"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", "ClientB", filter: null));
    }

    [TestMethod]
    public void IsUpToDate_ClientNameNullVsSet_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", "SomeClient", filter: null));
    }

    [TestMethod]
    public void IsUpToDate_IncludePathAdded_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        OperationFilter filter = new(["/items"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", null, filter));
    }

    [TestMethod]
    public void IsUpToDate_ExcludePathAdded_ReturnsFalse()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter: null, generatedFiles: ["A.cs"]);

        OperationFilter filter = new(null, ["/admin*"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", null, filter));
    }

    [TestMethod]
    public void IsUpToDate_IncludePathChanged_ReturnsFalse()
    {
        OperationFilter filterA = new(["/items"]);
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filterA, generatedFiles: ["A.cs"]);

        OperationFilter filterB = new(["/pets"]);

        Assert.IsFalse(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", null, filterB));
    }

    [TestMethod]
    public void IsUpToDate_SameFilter_ReturnsTrue()
    {
        OperationFilter filter = new(["/items"], ["/admin*"]);
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "TestNs", null, filter, generatedFiles: ["A.cs"]);

        OperationFilter sameFilter = new(["/items"], ["/admin*"]);

        Assert.IsTrue(OpenApiLockFile.IsUpToDate(in lockFile, SampleSpec, "3.1", "TestNs", null, sameFilter));
    }

    // ── Hash determinism ──────────────────────────────────────────────────
    [TestMethod]
    public void Create_SameContent_ProducesSameHash()
    {
        OpenApiLockFileModel a = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);
        OpenApiLockFileModel b = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);

        Assert.AreEqual(a.SpecFileHash.GetString(), b.SpecFileHash.GetString());
    }

    [TestMethod]
    public void Create_DifferentContent_ProducesDifferentHash()
    {
        byte[] otherSpec = Encoding.UTF8.GetBytes("{}");

        OpenApiLockFileModel a = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);
        OpenApiLockFileModel b = OpenApiLockFile.Create(
            otherSpec, "3.1", "Ns", null, null, ["A.cs"]);

        Assert.AreNotEqual(a.SpecFileHash.GetString(), b.SpecFileHash.GetString());
    }

    // ── Generated files list ──────────────────────────────────────────────
    [TestMethod]
    public void Create_RecordsGeneratedFiles()
    {
        string[] files = ["Req.cs", "Resp.cs", "Client.cs"];
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, files);

        List<string> actual = [];
        foreach (JsonElement item in lockFile.GeneratedFiles.EnumerateArray())
        {
            actual.Add(item.GetString()!);
        }

        CollectionAssert.AreEqual(files, actual.ToArray());
    }

    [TestMethod]
    public void Create_SetsTimestamp()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);

        string generatedAtStr = lockFile.GeneratedAt.GetString()!;
        Assert.IsTrue(DateTimeOffset.TryParse(generatedAtStr, out DateTimeOffset generatedAt));
        Assert.IsTrue(generatedAt >= before.AddSeconds(-1));
        Assert.IsTrue(generatedAt <= DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [TestMethod]
    public void Create_SetsGeneratorVersion()
    {
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);

        Assert.IsFalse(string.IsNullOrEmpty(lockFile.GeneratorVersion.GetString()));
    }

    // ── Full lifecycle simulation ─────────────────────────────────────────
    [TestMethod]
    public void Lifecycle_CreateSaveLoadCompare_MatchesThenDiverges()
    {
        // Step 1: "Generate" and save lock file
        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "MyApi", "Petstore", filter: null,
            generatedFiles: ["ListItemsRequest.cs", "ListItemsResponse.cs", "IApiItemsClient.cs", "ApiItemsClient.cs"]);
        OpenApiLockFile.Save(in lockFile, this.tempDir);

        // Step 2: Simulate second run — load and compare with same inputs
        Assert.IsTrue(OpenApiLockFile.TryLoad(this.tempDir, out OpenApiLockFileModel loaded));
        Assert.IsTrue(
            OpenApiLockFile.IsUpToDate(in loaded, SampleSpec, "3.1", "MyApi", "Petstore", filter: null),
            "Second run with identical inputs should be up to date");

        // Step 3: Spec content changes (user edits the OpenAPI file)
        byte[] updatedSpec = Encoding.UTF8.GetBytes("""
            {
              "openapi": "3.1.0",
              "info": { "title": "Test", "version": "1.0" },
              "paths": {
                "/items": {
                  "get": {
                    "operationId": "listItems",
                    "responses": { "200": { "description": "ok" } }
                  },
                  "post": {
                    "operationId": "createItem",
                    "responses": { "201": { "description": "created" } }
                  }
                }
              }
            }
            """);
        Assert.IsFalse(
            OpenApiLockFile.IsUpToDate(in loaded, updatedSpec, "3.1", "MyApi", "Petstore", filter: null),
            "Changed spec content should require regeneration");

        // Step 4: "Regenerate" with new spec, overwrite lock file
        OpenApiLockFileModel updatedLock = OpenApiLockFile.Create(
            updatedSpec, "3.1", "MyApi", "Petstore", filter: null,
            generatedFiles: ["ListItemsRequest.cs", "ListItemsResponse.cs", "CreateItemRequest.cs", "CreateItemResponse.cs", "IApiItemsClient.cs", "ApiItemsClient.cs"]);
        OpenApiLockFile.Save(in updatedLock, this.tempDir);

        // Step 5: Third run — now up to date again
        Assert.IsTrue(OpenApiLockFile.TryLoad(this.tempDir, out OpenApiLockFileModel reloaded));
        Assert.IsTrue(
            OpenApiLockFile.IsUpToDate(in reloaded, updatedSpec, "3.1", "MyApi", "Petstore", filter: null),
            "After regeneration, lock file should match updated spec");

        int fileCount = 0;
        foreach (JsonElement item in reloaded.GeneratedFiles.EnumerateArray())
        {
            _ = item;
            fileCount++;
        }

        Assert.AreEqual(6, fileCount);

        // Step 6: User changes a CLI parameter (add filter)
        OperationFilter newFilter = new(["/items"]);
        Assert.IsFalse(
            OpenApiLockFile.IsUpToDate(in reloaded, updatedSpec, "3.1", "MyApi", "Petstore", newFilter),
            "Adding a filter should require regeneration");
    }

    // ── Save creates directory ────────────────────────────────────────────
    [TestMethod]
    public void Save_CreatesOutputDirectoryIfNeeded()
    {
        string nestedDir = Path.Combine(this.tempDir, "nested", "output");
        Assert.IsFalse(Directory.Exists(nestedDir));

        OpenApiLockFileModel lockFile = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns", null, null, ["A.cs"]);
        OpenApiLockFile.Save(in lockFile, nestedDir);

        Assert.IsTrue(Directory.Exists(nestedDir));
        Assert.IsTrue(OpenApiLockFile.TryLoad(nestedDir, out _));
    }

    // ── Save overwrites existing lock file ────────────────────────────────
    [TestMethod]
    public void Save_OverwritesExistingLockFile()
    {
        OpenApiLockFileModel first = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns1", null, null, ["A.cs"]);
        OpenApiLockFile.Save(in first, this.tempDir);

        OpenApiLockFileModel second = OpenApiLockFile.Create(
            SampleSpec, "3.1", "Ns2", null, null, ["B.cs"]);
        OpenApiLockFile.Save(in second, this.tempDir);

        Assert.IsTrue(OpenApiLockFile.TryLoad(this.tempDir, out OpenApiLockFileModel loaded));
        Assert.AreEqual("Ns2", loaded.RootNamespace.GetString());

        List<string> files = [];
        foreach (JsonElement item in loaded.GeneratedFiles.EnumerateArray())
        {
            files.Add(item.GetString()!);
        }

        CollectionAssert.AreEqual(new[] { "B.cs" }, files.ToArray());
    }

    private static void AssertJsonStringArraysEqual(JsonElement expected, JsonElement actual)
    {
        List<string> expectedList = [];
        foreach (JsonElement item in expected.EnumerateArray())
        {
            expectedList.Add(item.GetString()!);
        }

        List<string> actualList = [];
        foreach (JsonElement item in actual.EnumerateArray())
        {
            actualList.Add(item.GetString()!);
        }

        CollectionAssert.AreEqual(expectedList, actualList);
    }
}