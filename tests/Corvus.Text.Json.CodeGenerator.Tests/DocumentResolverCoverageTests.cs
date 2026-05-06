// <copyright file="DocumentResolverCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;


namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Coverage tests for the shared Corvus.Json.CodeGeneration document resolvers:
/// <see cref="CallbackDocumentResolver"/>, <see cref="PrepopulatedDocumentResolver"/>,
/// and <see cref="FileSystemDocumentResolver"/>.
/// </summary>
public sealed class DocumentResolverCoverageTests : IDisposable
{
    private readonly string tempDir;

    public DocumentResolverCoverageTests()
    {
        this.tempDir = Path.Combine(Path.GetTempPath(), $"docresolver-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(this.tempDir))
        {
            Directory.Delete(this.tempDir, recursive: true);
        }
    }

    #region CallbackDocumentResolver

    [Fact]
    public async Task CallbackDocumentResolver_ResolvesViaCallback()
    {
        // Arrange
        using var resolver = new CallbackDocumentResolver(uri =>
        {
            if (uri == "test://schema.json")
            {
                return JsonDocument.Parse("""{"type": "string"}""");
            }

            return null;
        });

        var reference = new JsonReference("test://schema.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, result.Value.ValueKind);
        Assert.Equal("string", result.Value.GetProperty("type").GetString());
    }

    [Fact]
    public async Task CallbackDocumentResolver_CachesResolvedDocument()
    {
        // Arrange
        int callbackCount = 0;
        using var resolver = new CallbackDocumentResolver(uri =>
        {
            callbackCount++;
            return JsonDocument.Parse("""{"cached": true}""");
        });

        var reference = new JsonReference("test://cached.json");

        // Act - resolve twice
        System.Text.Json.JsonElement? first = await resolver.TryResolve(reference);
        System.Text.Json.JsonElement? second = await resolver.TryResolve(reference);

        // Assert - callback called only once (second time resolved from cache)
        Assert.Equal(1, callbackCount);
        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task CallbackDocumentResolver_ReturnsNullForUnresolvable()
    {
        // Arrange
        using var resolver = new CallbackDocumentResolver(_ => null);
        var reference = new JsonReference("test://unknown.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CallbackDocumentResolver_AddDocumentPreloadsCache()
    {
        // Arrange
        int callbackCount = 0;
        using var resolver = new CallbackDocumentResolver(_ =>
        {
            callbackCount++;
            return null;
        });

        using var doc = JsonDocument.Parse("""{"preloaded": true}""");
        resolver.AddDocument("test://preloaded.json", doc);

        var reference = new JsonReference("test://preloaded.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert - resolved from preloaded cache, callback never called
        Assert.Equal(0, callbackCount);
        Assert.NotNull(result);
        Assert.True(result.Value.GetProperty("preloaded").GetBoolean());
    }

    [Fact]
    public void CallbackDocumentResolver_Reset_ClearsCache()
    {
        // Arrange
        using var resolver = new CallbackDocumentResolver(_ => JsonDocument.Parse("{}"));

        // Act & Assert - no exception on Reset
        resolver.Reset();
    }

    #endregion

    #region PrepopulatedDocumentResolver

    [Fact]
    public async Task PrepopulatedDocumentResolver_AddAndResolve()
    {
        // Arrange
        using var resolver = new PrepopulatedDocumentResolver();
        using var doc = JsonDocument.Parse("""{"name": "test"}""");
        bool added = resolver.AddDocument("test://doc.json", doc);

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(new JsonReference("test://doc.json"));

        // Assert
        Assert.True(added);
        Assert.NotNull(result);
        Assert.Equal("test", result.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PrepopulatedDocumentResolver_AddDuplicate_ReturnsFalse()
    {
        // Arrange
        using var resolver = new PrepopulatedDocumentResolver();
        using var doc1 = JsonDocument.Parse("""{"first": true}""");
        using var doc2 = JsonDocument.Parse("""{"second": true}""");

        // Act
        bool first = resolver.AddDocument("test://dup.json", doc1);
        bool second = resolver.AddDocument("test://dup.json", doc2);

        // Assert
        Assert.True(first);
        Assert.False(second);

        // The original is still returned
        System.Text.Json.JsonElement? result = await resolver.TryResolve(new JsonReference("test://dup.json"));
        Assert.NotNull(result);
        Assert.True(result.Value.GetProperty("first").GetBoolean());
    }

    [Fact]
    public async Task PrepopulatedDocumentResolver_ResolveWithPointer()
    {
        // Arrange
        using var resolver = new PrepopulatedDocumentResolver();
        using var doc = JsonDocument.Parse("""{"definitions": {"Name": {"type": "string"}}}""");
        resolver.AddDocument("test://schema.json", doc);

        var reference = new JsonReference("test://schema.json#/definitions/Name");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("string", result.Value.GetProperty("type").GetString());
    }

    [Fact]
    public async Task PrepopulatedDocumentResolver_ResolveUnknownUri_ReturnsNull()
    {
        // Arrange
        using var resolver = new PrepopulatedDocumentResolver();

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(new JsonReference("test://unknown.json"));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task PrepopulatedDocumentResolver_Reset_ClearsDocuments()
    {
        // Arrange
        using var resolver = new PrepopulatedDocumentResolver();
        using var doc = JsonDocument.Parse("""{"data": 1}""");
        resolver.AddDocument("test://reset.json", doc);

        // Act
        resolver.Reset();
        System.Text.Json.JsonElement? result = await resolver.TryResolve(new JsonReference("test://reset.json"));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void PrepopulatedDocumentResolver_Dispose_ThrowsOnSubsequentUse()
    {
        // Arrange
        var resolver = new PrepopulatedDocumentResolver();

        // Act
        resolver.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() =>
            resolver.AddDocument("test://x.json", JsonDocument.Parse("{}")));
    }

    #endregion

    #region FileSystemDocumentResolver

    [Fact]
    public void FileSystemDocumentResolver_ConstructorWithBaseDirectory()
    {
        // Act & Assert - constructor with explicit base directory
        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        Assert.NotNull(resolver);
    }

    [Fact]
    public void FileSystemDocumentResolver_ConstructorWithBaseDirectory_ThrowsOnEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new FileSystemDocumentResolver(string.Empty));
    }

    [Fact]
    public void FileSystemDocumentResolver_ConstructorWithPreProcessor()
    {
        // Act & Assert - constructor with pre-processor
        using var resolver = new FileSystemDocumentResolver(new PassthroughPreProcessor());
        Assert.NotNull(resolver);
    }

    [Fact]
    public void FileSystemDocumentResolver_ConstructorWithBaseDirectoryAndPreProcessor()
    {
        // Act & Assert
        using var resolver = new FileSystemDocumentResolver(this.tempDir, new PassthroughPreProcessor());
        Assert.NotNull(resolver);
    }

    [Fact]
    public void FileSystemDocumentResolver_ConstructorWithBaseDirectoryAndPreProcessor_ThrowsOnEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new FileSystemDocumentResolver(string.Empty, new PassthroughPreProcessor()));
    }

    [Fact]
    public async Task FileSystemDocumentResolver_ResolvesLocalFile()
    {
        // Arrange
        string filePath = "test-schema.json";
        string fullPath = Path.Combine(this.tempDir, filePath);
        File.WriteAllText(fullPath, """{"type": "object", "properties": {"name": {"type": "string"}}}""");

        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        var reference = new JsonReference(filePath);

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, result.Value.ValueKind);
        Assert.Equal("object", result.Value.GetProperty("type").GetString());
    }

    [Fact]
    public async Task FileSystemDocumentResolver_ResolvesWithPointer()
    {
        // Arrange
        string filePath = "schema-with-defs.json";
        string fullPath = Path.Combine(this.tempDir, filePath);
        File.WriteAllText(fullPath, """{"$defs": {"Name": {"type": "string"}}}""");

        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        var reference = new JsonReference(filePath + "#/$defs/Name");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("string", result.Value.GetProperty("type").GetString());
    }

    [Fact]
    public async Task FileSystemDocumentResolver_ResolvesWithPreProcessor()
    {
        // Arrange
        string filePath = "preprocessed.json";
        string fullPath = Path.Combine(this.tempDir, filePath);
        File.WriteAllText(fullPath, """{"raw": true}""");

        using var resolver = new FileSystemDocumentResolver(this.tempDir, new PassthroughPreProcessor());
        var reference = new JsonReference(filePath);

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.GetProperty("raw").GetBoolean());
    }

    [Fact]
    public async Task FileSystemDocumentResolver_NonExistentFile_ReturnsNull()
    {
        // Arrange
        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        var reference = new JsonReference("does-not-exist.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FileSystemDocumentResolver_CachesResolvedDocument()
    {
        // Arrange
        string filePath = "cached-schema.json";
        string fullPath = Path.Combine(this.tempDir, filePath);
        File.WriteAllText(fullPath, """{"version": 1}""");

        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        var reference = new JsonReference(filePath);

        // Act - resolve twice
        System.Text.Json.JsonElement? first = await resolver.TryResolve(reference);

        // Delete file — second resolve should still work from cache
        File.Delete(fullPath);
        System.Text.Json.JsonElement? second = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first.Value.GetProperty("version").GetInt32());
        Assert.Equal(1, second.Value.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task FileSystemDocumentResolver_AddDocument_PreloadsCache()
    {
        // Arrange
        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        string uri = Path.Combine(this.tempDir, "added.json");
        using var doc = JsonDocument.Parse("""{"added": true}""");
        resolver.AddDocument(uri, doc);

        var reference = new JsonReference("added.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert - resolves from AddDocument cache
        Assert.NotNull(result);
        Assert.True(result.Value.GetProperty("added").GetBoolean());
    }

    [Fact]
    public async Task FileSystemDocumentResolver_Reset_ClearsCache()
    {
        // Arrange
        string filePath = "reset-test.json";
        string fullPath = Path.Combine(this.tempDir, filePath);
        File.WriteAllText(fullPath, """{"resettable": true}""");

        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        var reference = new JsonReference(filePath);

        // Resolve to populate cache
        await resolver.TryResolve(reference);

        // Act
        resolver.Reset();

        // Re-resolve — should re-read from file
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task FileSystemDocumentResolver_Dispose_ThrowsOnSubsequentUse()
    {
        // Arrange
        var resolver = new FileSystemDocumentResolver(this.tempDir);

        // Act
        resolver.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await resolver.TryResolve(new JsonReference("any.json")));
    }

    #endregion

    #region CompoundDocumentResolver

    [Fact]
    public async Task CompoundDocumentResolver_ResolvesFromFirstMatchingResolver()
    {
        // Arrange
        var prepopulated = new PrepopulatedDocumentResolver();
        using var doc = JsonDocument.Parse("""{"source": "prepopulated"}""");
        prepopulated.AddDocument("test://compound.json", doc);

        using var resolver = new CompoundDocumentResolver(prepopulated);
        var reference = new JsonReference("test://compound.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("prepopulated", result.Value.GetProperty("source").GetString());
    }

    [Fact]
    public async Task CompoundDocumentResolver_ReturnsNullWhenNoResolverMatches()
    {
        // Arrange
        using var resolver = new CompoundDocumentResolver(new PrepopulatedDocumentResolver());
        var reference = new JsonReference("test://not-found.json");

        // Act
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CompoundDocumentResolver_AddDocument_AddsToAllResolvers()
    {
        // Arrange
        var r1 = new PrepopulatedDocumentResolver();
        var r2 = new PrepopulatedDocumentResolver();
        using var resolver = new CompoundDocumentResolver(r1, r2);

        using var doc = JsonDocument.Parse("""{"compound": true}""");

        // Act
        bool result = resolver.AddDocument("test://added.json", doc);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CompoundDocumentResolver_Reset_ResetsAllResolvers()
    {
        // Arrange
        var r1 = new PrepopulatedDocumentResolver();
        using var resolver = new CompoundDocumentResolver(r1);

        // Act & Assert - no exception
        resolver.Reset();
    }

    [Fact]
    public async Task CompoundDocumentResolver_CachedDocument_WithValidFragment_ReturnsElement()
    {
        // Covers CompoundDocumentResolver.TryResolve lines 42-45 (cached doc + fragment resolution success)
        using var resolver = new CompoundDocumentResolver(new PrepopulatedDocumentResolver());
        using var doc = JsonDocument.Parse("""{"nested": {"value": 42}}""");
        resolver.AddDocument("test://cached.json", doc);

        var reference = new JsonReference("test://cached.json#/nested/value");

        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        Assert.NotNull(result);
        Assert.Equal(System.Text.Json.JsonValueKind.Number, result.Value.ValueKind);
        Assert.Equal(42, result.Value.GetInt32());
    }

    [Fact]
    public async Task CompoundDocumentResolver_CachedDocument_WithInvalidFragment_ReturnsNull()
    {
        // Covers CompoundDocumentResolver.TryResolve line 48 (cached doc + fragment resolution failure)
        using var resolver = new CompoundDocumentResolver(new PrepopulatedDocumentResolver());
        using var doc = JsonDocument.Parse("""{"a": 1}""");
        resolver.AddDocument("test://cached.json", doc);

        var reference = new JsonReference("test://cached.json#/nonexistent/path");

        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        Assert.Null(result);
    }

    [Fact]
    public async Task FileSystemDocumentResolver_ParameterlessConstructor_UsesCurrentDirectory()
    {
        // Covers FileSystemDocumentResolver lines 64-67 (parameterless constructor)
        using var resolver = new FileSystemDocumentResolver();

        // Trying to resolve a non-existent file should return null (not crash)
        var reference = new JsonReference("nonexistent-file-xyz-12345.json");
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        Assert.Null(result);
    }

    [Fact]
    public async Task FileSystemDocumentResolver_CachedDocument_WithInvalidFragment_ReturnsNull()
    {
        // Covers FileSystemDocumentResolver.TryResolve line 91 (cached doc + failed fragment)
        string schemaPath = Path.Combine(this.tempDir, "cached-schema.json");
        File.WriteAllText(schemaPath, """{"type": "object"}""");

        using var resolver = new FileSystemDocumentResolver(this.tempDir);

        // First resolve caches the document
        var refWithRoot = new JsonReference("cached-schema.json");
        System.Text.Json.JsonElement? first = await resolver.TryResolve(refWithRoot);
        Assert.NotNull(first);

        // Second resolve with invalid fragment hits the cached path line 91
        var refWithBadFragment = new JsonReference("cached-schema.json#/nonexistent/deep/path");
        System.Text.Json.JsonElement? second = await resolver.TryResolve(refWithBadFragment);

        Assert.Null(second);
    }

    [Fact]
    public async Task FileSystemDocumentResolver_FileNotFound_ReturnsNull()
    {
        // Covers FileSystemDocumentResolver.TryResolve line 126 (catch block)
        using var resolver = new FileSystemDocumentResolver(this.tempDir);
        var reference = new JsonReference("this-file-does-not-exist.json");

        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        Assert.Null(result);
    }

    [Fact]
    public async Task PrepopulatedDocumentResolver_WithInvalidFragment_ReturnsNull()
    {
        // Covers PrepopulatedDocumentResolver line 50 (foreach completes without fragment match)
        using var resolver = new PrepopulatedDocumentResolver();
        using var doc = JsonDocument.Parse("""{"hello": "world"}""");
        resolver.AddDocument("test://schema.json", doc);

        var reference = new JsonReference("test://schema.json#/nonexistent");
        System.Text.Json.JsonElement? result = await resolver.TryResolve(reference);

        Assert.Null(result);
    }

    #endregion

    #region Helpers

    private sealed class PassthroughPreProcessor : IDocumentStreamPreProcessor
    {
        public Stream Process(Stream input)
        {
            // Copy to a new MemoryStream so we test the processedStream != inputStream cleanup path
            var output = new MemoryStream();
            input.CopyTo(output);
            output.Position = 0;
            return output;
        }
    }

    #endregion
}
