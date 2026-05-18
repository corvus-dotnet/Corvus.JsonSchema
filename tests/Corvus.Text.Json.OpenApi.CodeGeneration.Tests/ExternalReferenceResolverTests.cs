// <copyright file="ExternalReferenceResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class ExternalReferenceResolverTests
{
    // Entry document — references external schemas
    private static readonly string EntryDoc = """
        {
          "components": {
            "schemas": {
              "Pet": { "type": "object", "properties": { "name": { "type": "string" } } }
            },
            "parameters": {
              "PetId": { "name": "petId", "in": "path", "schema": { "type": "integer" } }
            }
          }
        }
        """;

    // External document — a standalone schema file
    private static readonly string ExternalSchemaDoc = """
        {
          "definitions": {
            "Error": { "type": "object", "properties": { "code": { "type": "integer" }, "message": { "type": "string" } } }
          },
          "type": "object",
          "properties": { "id": { "type": "integer" } }
        }
        """;

    // A fake absolute path for the entry document (never actually read from disk)
    private static readonly string EntryDocPath = OperatingSystem.IsWindows()
        ? @"C:\specs\openapi.json"
        : "/specs/openapi.json";

    // ══════════════════════════════════════════════════════════════════
    // Construction
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void Constructor_RelativePath_ThrowsArgumentException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new ExternalReferenceResolver(doc.RootElement, "relative/path.json"));
    }

    [TestMethod]
    public void Constructor_AbsolutePath_Succeeds()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        // No exception — construction succeeded
        Assert.IsNotNull(resolver);
    }

    // ══════════════════════════════════════════════════════════════════
    // Fragment-only $ref (local references)
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void TryResolve_String_FragmentOnly_ResolvesInEntryDoc()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        bool found = resolver.TryResolve("#/components/schemas/Pet", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Utf8_FragmentOnly_ResolvesInEntryDoc()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        bool found = resolver.TryResolve("#/components/parameters/PetId"u8, out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_String_Empty_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        Assert.IsFalse(resolver.TryResolve(string.Empty, out _));
    }

    [TestMethod]
    public void TryResolve_Utf8_Empty_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        Assert.IsFalse(resolver.TryResolve(ReadOnlySpan<byte>.Empty, out _));
    }

    // ══════════════════════════════════════════════════════════════════
    // Pre-registered documents (AddDocument with JsonElement)
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void AddDocument_Uri_ResolvesExternalRef()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        Uri canonicalUri = new(new Uri(EntryDocPath), "./schemas/Error.json");
        resolver.AddDocument(canonicalUri, extParsed.RootElement);

        // Resolve with fragment — navigate into the external doc
        bool found = resolver.TryResolve("./schemas/Error.json#/definitions/Error", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void AddDocument_Uri_NoFragment_ReturnsDocRoot()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        Uri canonicalUri = new(new Uri(EntryDocPath), "./schemas/Error.json");
        resolver.AddDocument(canonicalUri, extParsed.RootElement);

        // Resolve without fragment — returns the root
        bool found = resolver.TryResolve("./schemas/Error.json", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void AddDocument_RelativePath_ResolvesExternalRef()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./schemas/Error.json", extParsed.RootElement);

        bool found = resolver.TryResolve("./schemas/Error.json#/definitions/Error", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    // ══════════════════════════════════════════════════════════════════
    // Pre-registered documents (AddDocument with raw bytes)
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void AddDocument_RawBytes_Uri_ResolvesExternalRef()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        byte[] extBytes = Encoding.UTF8.GetBytes(ExternalSchemaDoc);
        Uri canonicalUri = new(new Uri(EntryDocPath), "./schemas/Error.json");
        resolver.AddDocument(canonicalUri, extBytes);

        bool found = resolver.TryResolve("./schemas/Error.json#/definitions/Error", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void AddDocument_RawBytes_RelativePath_ResolvesExternalRef()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        byte[] extBytes = Encoding.UTF8.GetBytes(ExternalSchemaDoc);
        resolver.AddDocument("./schemas/Error.json", extBytes);

        bool found = resolver.TryResolve("./schemas/Error.json#/definitions/Error", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    // ══════════════════════════════════════════════════════════════════
    // Canonical URI bundling (non-file URIs)
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void AddDocument_AbsoluteHttpUri_ResolvesFromRegistry()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        Uri httpUri = new("https://example.com/schemas/Error.json");
        resolver.AddDocument(httpUri, extParsed.RootElement);

        bool found = resolver.TryResolve("https://example.com/schemas/Error.json#/definitions/Error", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void AddDocument_AbsoluteHttpUri_RawBytes_ResolvesFromRegistry()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        byte[] extBytes = Encoding.UTF8.GetBytes(ExternalSchemaDoc);
        Uri httpUri = new("https://example.com/schemas/Error.json");
        resolver.AddDocument(httpUri, extBytes);

        bool found = resolver.TryResolve("https://example.com/schemas/Error.json", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    // ══════════════════════════════════════════════════════════════════
    // Unregistered / unresolvable references
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void TryResolve_UnregisteredExternalRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        // No documents registered — external ref should fail
        bool found = resolver.TryResolve("./schemas/NotRegistered.json#/foo", out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryResolve_UnregisteredHttpRef_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        bool found = resolver.TryResolve("https://example.com/not-registered.json", out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryResolve_ValidDocButBadFragment_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./schemas/Error.json", extParsed.RootElement);

        // Valid document but bad fragment pointer
        bool found = resolver.TryResolve("./schemas/Error.json#/no/such/path", out _);

        Assert.IsFalse(found);
    }

    // ══════════════════════════════════════════════════════════════════
    // Generic TryResolve<TTarget>
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void TryResolve_Generic_String_FragmentOnly_Resolves()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        bool found = resolver.TryResolve<JsonElement>(
            "#/components/schemas/Pet", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Generic_Utf8_FragmentOnly_Resolves()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        bool found = resolver.TryResolve<JsonElement>(
            "#/components/schemas/Pet"u8, out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Generic_String_ExternalRef_Resolves()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./schemas/Error.json", extParsed.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            "./schemas/Error.json#/definitions/Error", out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void TryResolve_Generic_Utf8_ExternalRef_Resolves()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalSchemaDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./schemas/Error.json", extParsed.RootElement);

        bool found = resolver.TryResolve<JsonElement>(
            Encoding.UTF8.GetBytes("./schemas/Error.json#/definitions/Error"), out JsonElement result);

        Assert.IsTrue(found);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    // ══════════════════════════════════════════════════════════════════
    // File-system lazy loading
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void TryResolve_FileSystemFallback_LoadsFromDisk()
    {
        // Write a temporary external document to disk
        string tempDir = Path.Combine(Path.GetTempPath(), $"corvus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string entryPath = Path.Combine(tempDir, "openapi.json");
            string extPath = Path.Combine(tempDir, "schemas", "Error.json");
            Directory.CreateDirectory(Path.GetDirectoryName(extPath)!);

            File.WriteAllText(entryPath, EntryDoc);
            File.WriteAllText(extPath, ExternalSchemaDoc);

            using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
            using ExternalReferenceResolver resolver = new(entryParsed.RootElement, entryPath);

            // No AddDocument — should fall back to file-system loading
            bool found = resolver.TryResolve("./schemas/Error.json#/definitions/Error", out JsonElement result);

            Assert.IsTrue(found);
            Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void TryResolve_FileSystemFallback_CachesDocument()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"corvus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string entryPath = Path.Combine(tempDir, "openapi.json");
            string extPath = Path.Combine(tempDir, "schemas", "Error.json");
            Directory.CreateDirectory(Path.GetDirectoryName(extPath)!);

            File.WriteAllText(entryPath, EntryDoc);
            File.WriteAllText(extPath, ExternalSchemaDoc);

            using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
            using ExternalReferenceResolver resolver = new(entryParsed.RootElement, entryPath);

            // First call loads from disk
            Assert.IsTrue(resolver.TryResolve("./schemas/Error.json#/definitions/Error", out _));

            // Delete the file — second call should succeed from cache
            File.Delete(extPath);

            Assert.IsTrue(resolver.TryResolve("./schemas/Error.json#/definitions/Error", out JsonElement result));
            Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void TryResolve_FileSystemFallback_NonexistentFile_ReturnsFalse()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"corvus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string entryPath = Path.Combine(tempDir, "openapi.json");
            File.WriteAllText(entryPath, EntryDoc);

            using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
            using ExternalReferenceResolver resolver = new(entryParsed.RootElement, entryPath);

            // External file doesn't exist
            bool found = resolver.TryResolve("./schemas/Missing.json", out _);

            Assert.IsFalse(found);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Dispose behavior
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void Dispose_ReleasesOwnedDocuments()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        byte[] extBytes = Encoding.UTF8.GetBytes(ExternalSchemaDoc);
        resolver.AddDocument("./schemas/Error.json", extBytes);

        // Verify it resolves before dispose
        Assert.IsTrue(resolver.TryResolve("./schemas/Error.json", out _));

        resolver.Dispose();

        // After dispose, should throw ObjectDisposedException
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            resolver.TryResolve("./schemas/Error.json", out _));
    }

    [TestMethod]
    public void Dispose_AddDocument_ThrowsAfterDispose()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);
        resolver.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            resolver.AddDocument(new Uri("https://example.com/x.json"), entryParsed.RootElement));
    }

    [TestMethod]
    public void Dispose_DoubleDispose_DoesNotThrow()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.Dispose();
        resolver.Dispose(); // Should not throw
    }

    // ══════════════════════════════════════════════════════════════════
    // Priority: registered > loaded > file-system
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void TryResolve_RegisteredTakesPriorityOverFileSystem()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"corvus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string entryPath = Path.Combine(tempDir, "openapi.json");
            string extPath = Path.Combine(tempDir, "schemas", "Error.json");
            Directory.CreateDirectory(Path.GetDirectoryName(extPath)!);

            File.WriteAllText(entryPath, EntryDoc);

            // File on disk has one shape
            File.WriteAllText(extPath, """{"onDisk": true}""");

            // Registered document has a different shape
            const string registered = """{"registered": true}""";
            using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
            using ParsedJsonDocument<JsonElement> regParsed = ParsedJsonDocument<JsonElement>.Parse(registered);
            using ExternalReferenceResolver resolver = new(entryParsed.RootElement, entryPath);

            resolver.AddDocument("./schemas/Error.json", regParsed.RootElement);

            // Should get the registered doc, not the file-system one
            Assert.IsTrue(resolver.TryResolve("./schemas/Error.json", out JsonElement result));
            Assert.IsTrue(result.TryGetProperty("registered"u8, out _));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}