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

    [TestMethod]
    public void TryResolve_Generic_String_SchemaValidationFails_ReturnsFalse()
    {
        // Resolve an object value as JsonUri — schema validation will fail
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        bool found = resolver.TryResolve<JsonUri>(
            "#/components/schemas/Pet", out JsonUri result);

        Assert.IsFalse(found);
        Assert.AreEqual(default(JsonUri), result);
    }

    [TestMethod]
    public void TryResolve_Generic_Utf8_SchemaValidationFails_ReturnsFalse()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, EntryDocPath);

        bool found = resolver.TryResolve<JsonUri>(
            "#/components/schemas/Pet"u8, out JsonUri result);

        Assert.IsFalse(found);
        Assert.AreEqual(default(JsonUri), result);
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

    // ══════════════════════════════════════════════════════════════════
    // RFC 3986 §5 — PushResolvedBase and base URI tracking
    // ══════════════════════════════════════════════════════════════════

    // External document with internal $ref
    private static readonly string ExternalDocWithInternalRef = """
        {
          "components": {
            "headers": {
              "RequestId": { "schema": { "type": "string", "format": "uuid" } }
            },
            "responses": {
              "ItemResponse": {
                "description": "An item",
                "headers": {
                  "X-Request-Id": { "$ref": "#/components/headers/RequestId" }
                }
              }
            }
          }
        }
        """;

    // Second-level external document (referenced by first external doc)
    private static readonly string DeepExternalDoc = """
        {
          "definitions": {
            "Timestamp": { "type": "string", "format": "date-time" }
          }
        }
        """;

    // External document that references another relative file
    private static readonly string ExternalDocWithRelativeRef = """
        {
          "components": {
            "schemas": {
              "Audit": {
                "type": "object",
                "properties": {
                  "createdAt": { "$ref": "./deep/timestamps.json#/definitions/Timestamp" }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public void PushResolvedBase_FragmentOnly_ReturnsNoOpScope()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        // Fragment-only ref should not push anything
        IDisposable scope = resolver.PushResolvedBase("#/components/schemas/Pet");
        Assert.IsNotNull(scope);

        // Fragment-only resolution should still use entry doc
        Assert.IsTrue(resolver.TryResolve("#/components/schemas/Pet", out JsonElement result));
        Assert.IsTrue(result.TryGetProperty("type"u8, out _));

        scope.Dispose(); // no-op dispose should not throw
    }

    [TestMethod]
    public void PushResolvedBase_EmptyString_ReturnsNoOpScope()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        IDisposable scope = resolver.PushResolvedBase(string.Empty);
        Assert.IsNotNull(scope);
        scope.Dispose();
    }

    [TestMethod]
    public void PushResolvedBase_UnregisteredDocument_ReturnsNoOpScope()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        // No document registered for this path
        IDisposable scope = resolver.PushResolvedBase("./unknown.json#/something");
        Assert.IsNotNull(scope);

        // Fragment resolution still uses entry doc
        Assert.IsTrue(resolver.TryResolve("#/components/schemas/Pet", out _));

        scope.Dispose();
    }

    [TestMethod]
    public void PushResolvedBase_ExternalDoc_FragmentResolvesAgainstExternalDoc()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalDocWithInternalRef);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./common.json", extParsed.RootElement);

        // Push the base for common.json
        using (resolver.PushResolvedBase("./common.json#/components/responses/ItemResponse"))
        {
            // Fragment-only ref should now resolve against the external doc
            Assert.IsTrue(
                resolver.TryResolve("#/components/headers/RequestId", out JsonElement header),
                "Fragment should resolve against external doc after PushResolvedBase");
            Assert.IsTrue(header.TryGetProperty("schema"u8, out JsonElement schema));
            Assert.IsTrue(schema.TryGetProperty("format"u8, out JsonElement format));
            Assert.AreEqual("uuid", format.GetString());
        }

        // After dispose, fragment resolution reverts to entry doc
        Assert.IsTrue(
            resolver.TryResolve("#/components/schemas/Pet", out JsonElement pet),
            "After scope disposal, fragments should resolve against entry doc again");
        Assert.IsTrue(pet.TryGetProperty("type"u8, out _));
    }

    [TestMethod]
    public void PushResolvedBase_ExternalDoc_Utf8FragmentResolvesAgainstExternalDoc()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalDocWithInternalRef);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./common.json", extParsed.RootElement);

        using (resolver.PushResolvedBase("./common.json#/components/responses/ItemResponse"))
        {
            // UTF-8 overload should also resolve against external doc
            Assert.IsTrue(
                resolver.TryResolve("#/components/headers/RequestId"u8, out JsonElement header),
                "UTF-8 fragment should resolve against external doc after PushResolvedBase");
            Assert.IsTrue(header.TryGetProperty("schema"u8, out _));
        }
    }

    [TestMethod]
    public void PushResolvedBase_WithoutFragment_PushesDocumentBase()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalDocWithInternalRef);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./common.json", extParsed.RootElement);

        // Push with no fragment — just the document URI
        using (resolver.PushResolvedBase("./common.json"))
        {
            Assert.IsTrue(
                resolver.TryResolve("#/components/headers/RequestId", out _),
                "Ref without fragment should still push the external doc as base");
        }
    }

    [TestMethod]
    public void PushResolvedBase_NestedPush_ResolvesAgainstInnermostDoc()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalDocWithInternalRef);
        using ParsedJsonDocument<JsonElement> deepParsed = ParsedJsonDocument<JsonElement>.Parse(DeepExternalDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./common.json", extParsed.RootElement);
        resolver.AddDocument("./deep.json", deepParsed.RootElement);

        // Level 1: push common.json
        using (resolver.PushResolvedBase("./common.json#/something"))
        {
            Assert.IsTrue(
                resolver.TryResolve("#/components/headers/RequestId", out _),
                "Level 1 should resolve fragments against common.json");

            // Level 2: push deep.json
            using (resolver.PushResolvedBase("./deep.json#/definitions/Timestamp"))
            {
                Assert.IsTrue(
                    resolver.TryResolve("#/definitions/Timestamp", out JsonElement ts),
                    "Level 2 should resolve fragments against deep.json");
                Assert.IsTrue(ts.TryGetProperty("format"u8, out JsonElement fmt));
                Assert.AreEqual("date-time", fmt.GetString());

                // common.json's fragment should NOT resolve here
                Assert.IsFalse(
                    resolver.TryResolve("#/components/headers/RequestId", out _),
                    "Level 2 should not resolve common.json fragments");
            }

            // Back to level 1 after inner scope disposed
            Assert.IsTrue(
                resolver.TryResolve("#/components/headers/RequestId", out _),
                "After inner scope disposal, should revert to common.json");
        }

        // Back to entry doc
        Assert.IsTrue(
            resolver.TryResolve("#/components/schemas/Pet", out _),
            "After all scopes disposed, should revert to entry doc");
    }

    [TestMethod]
    public void PushResolvedBase_RelativeRefResolvesAgainstCurrentBase()
    {
        // Entry at /specs/openapi.json
        // common.json registered at ./sub/common.json → resolves to /specs/sub/common.json
        // Within common.json scope, ./deep/timestamps.json should resolve to /specs/sub/deep/timestamps.json
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> commonParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalDocWithRelativeRef);
        using ParsedJsonDocument<JsonElement> deepParsed = ParsedJsonDocument<JsonElement>.Parse(DeepExternalDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./sub/common.json", commonParsed.RootElement);

        // Register deep doc at the URI that ./deep/timestamps.json resolves to
        // relative to /specs/sub/common.json → /specs/sub/deep/timestamps.json
        string deepKey = OperatingSystem.IsWindows()
            ? "file:///C:/specs/sub/deep/timestamps.json"
            : "file:///specs/sub/deep/timestamps.json";
        resolver.AddDocument(new Uri(deepKey), deepParsed.RootElement);

        // Push into sub/common.json
        using (resolver.PushResolvedBase("./sub/common.json#/components/schemas/Audit"))
        {
            // Now a relative ref ./deep/timestamps.json should resolve against
            // the current base (sub/common.json's directory) not the entry doc's directory
            Assert.IsTrue(
                resolver.TryResolve("./deep/timestamps.json#/definitions/Timestamp", out JsonElement ts),
                "Relative ref should resolve against current base URI (sub/common.json's dir)");
            Assert.IsTrue(ts.TryGetProperty("format"u8, out JsonElement fmt));
            Assert.AreEqual("date-time", fmt.GetString());
        }
    }

    [TestMethod]
    public void PushResolvedBase_ScopeDispose_IsIdempotent()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> extParsed = ParsedJsonDocument<JsonElement>.Parse(ExternalDocWithInternalRef);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        resolver.AddDocument("./common.json", extParsed.RootElement);

        IDisposable scope = resolver.PushResolvedBase("./common.json#/x");

        // Double dispose should not throw or corrupt the stack
        scope.Dispose();
        scope.Dispose();

        // Entry doc should still work
        Assert.IsTrue(resolver.TryResolve("#/components/schemas/Pet", out _));
    }

    [TestMethod]
    public void PushResolvedBase_RawBytesDocument_FragmentResolvesCorrectly()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        // Register via raw bytes (the loadedDocuments path)
        byte[] extBytes = Encoding.UTF8.GetBytes(ExternalDocWithInternalRef);
        resolver.AddDocument("./common.json", extBytes);

        using (resolver.PushResolvedBase("./common.json#/components/responses/ItemResponse"))
        {
            Assert.IsTrue(
                resolver.TryResolve("#/components/headers/RequestId", out JsonElement header),
                "Fragment should resolve against raw-bytes-loaded external doc");
            Assert.IsTrue(header.TryGetProperty("schema"u8, out _));
        }
    }

    [TestMethod]
    public void PushResolvedBase_AfterDispose_ThrowsObjectDisposedException()
    {
        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);
        resolver.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(
            () => resolver.PushResolvedBase("./common.json"));
    }

    [TestMethod]
    public void PushResolvedBase_MultiLevel_ExternalRefsChainedAcrossThreeDocuments()
    {
        // Simulates: entry → common.json → deep/types.json
        // Entry doc references common.json; common.json has a $ref to ./deep/types.json
        // After pushing common.json's base, resolving ./deep/types.json uses common.json's
        // directory as base (not entry doc's directory).
        const string commonJson = """
            {
              "schemas": {
                "Widget": {
                  "type": "object",
                  "properties": {
                    "timestamp": { "$ref": "./deep/types.json#/definitions/Timestamp" }
                  }
                }
              }
            }
            """;

        const string deepTypesJson = """
            {
              "definitions": {
                "Timestamp": { "type": "string", "format": "date-time" },
                "Counter": { "type": "integer", "minimum": 0 }
              }
            }
            """;

        using ParsedJsonDocument<JsonElement> entryParsed = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ParsedJsonDocument<JsonElement> commonParsed = ParsedJsonDocument<JsonElement>.Parse(commonJson);
        using ParsedJsonDocument<JsonElement> deepParsed = ParsedJsonDocument<JsonElement>.Parse(deepTypesJson);
        using ExternalReferenceResolver resolver = new(entryParsed.RootElement, EntryDocPath);

        // Register common.json at ./lib/common.json (relative to entry)
        resolver.AddDocument("./lib/common.json", commonParsed.RootElement);

        // deep/types.json relative to common.json's dir → /specs/lib/deep/types.json
        string deepUri = OperatingSystem.IsWindows()
            ? "file:///C:/specs/lib/deep/types.json"
            : "file:///specs/lib/deep/types.json";
        resolver.AddDocument(new Uri(deepUri), deepParsed.RootElement);

        // Push level 1: common.json
        using (resolver.PushResolvedBase("./lib/common.json#/schemas/Widget"))
        {
            // From common.json's scope, resolve ./deep/types.json (relative to common.json)
            Assert.IsTrue(
                resolver.TryResolve("./deep/types.json#/definitions/Timestamp", out JsonElement ts),
                "Relative ref from within common.json scope should resolve against common.json's directory");
            Assert.IsTrue(ts.TryGetProperty("format"u8, out JsonElement fmt));
            Assert.AreEqual("date-time", fmt.GetString());

            // Push level 2: deep/types.json
            using (resolver.PushResolvedBase("./deep/types.json#/definitions/Timestamp"))
            {
                // Fragment resolution should now use deep/types.json
                Assert.IsTrue(
                    resolver.TryResolve("#/definitions/Counter", out JsonElement counter),
                    "Fragment should resolve against deep/types.json at level 2");
                Assert.IsTrue(counter.TryGetProperty("minimum"u8, out JsonElement min));
                Assert.AreEqual(0, min.GetInt32());
            }

            // Back to common.json — fragment #/schemas/Widget should resolve
            Assert.IsTrue(
                resolver.TryResolve("#/schemas/Widget", out JsonElement widget),
                "After level 2 pop, fragment should resolve against common.json");
            Assert.IsTrue(widget.TryGetProperty("type"u8, out _));
        }

        // Back to entry — #/components/schemas/Pet should resolve
        Assert.IsTrue(
            resolver.TryResolve("#/components/schemas/Pet", out _),
            "After all pops, fragment should resolve against entry doc");
    }

    // ══════════════════════════════════════════════════════════════════
    // Uri base constructor + virtualized external-document loader
    // ══════════════════════════════════════════════════════════════════
    [TestMethod]
    public void Constructor_NullBaseUri_ThrowsArgumentNullException()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ExternalReferenceResolver(doc.RootElement, (Uri)null!));
    }

    [TestMethod]
    public void UriBase_FragmentOnly_ResolvesInEntryDoc()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(doc.RootElement, new Uri("https://example.com/api/openapi.json"));

        Assert.IsTrue(resolver.TryResolve("#/components/schemas/Pet", out JsonElement pet));
        Assert.IsTrue(pet.TryGetProperty("properties"u8, out _));
    }

    [TestMethod]
    public void ExternalDocumentLoader_ResolvesRelativeRefAgainstNonFileBase()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        byte[] externalBytes = Encoding.UTF8.GetBytes(ExternalSchemaDoc);

        // The loader serves the document the relative ref resolves to (an http URI, never fetched).
        using ExternalReferenceResolver resolver = new(
            doc.RootElement,
            new Uri("https://example.com/api/openapi.json"),
            uri => uri.AbsoluteUri == "https://example.com/api/common.json" ? externalBytes : null);

        Assert.IsTrue(resolver.TryResolve("./common.json#/definitions/Error", out JsonElement error));
        Assert.IsTrue(error.TryGetProperty("properties"u8, out _));

        // A second resolution of the same doc is served from the owned cache (loader not needed again).
        Assert.IsTrue(resolver.TryResolve("./common.json#/properties/id", out _));
    }

    [TestMethod]
    public void ExternalDocumentLoader_ReturningNull_DoesNotResolve()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(EntryDoc);
        using ExternalReferenceResolver resolver = new(
            doc.RootElement, new Uri("https://example.com/api/openapi.json"), _ => null);

        Assert.IsFalse(resolver.TryResolve("./missing.json#/definitions/Error", out _));
    }
}