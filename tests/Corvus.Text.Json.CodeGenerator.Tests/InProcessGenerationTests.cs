using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Xunit;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// In-process code generation tests that exercise name heuristics,
/// format handlers, and validation handlers within the test process
/// so coverage is captured by instrumentation.
/// </summary>
public class InProcessGenerationTests
{
    private static readonly string SchemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");

    [Fact]
    public async Task GenerateCode_ConstProperties_ExercisesConstNameHeuristic()
    {
        // Schema with oneOf variants distinguished by a single const property
        // ensures ConstPropertyNameHeuristic is evaluated (even if outranked by another heuristic)
        string schemaContent = """
            {
              "type": "object",
              "properties": {
                "item": {
                  "oneOf": [
                    {
                      "type": "object",
                      "properties": {
                        "kind": { "const": "alpha" },
                        "value": { "type": "string" }
                      }
                    },
                    {
                      "type": "object",
                      "properties": {
                        "kind": { "const": "beta" },
                        "count": { "type": "integer" }
                      }
                    }
                  ]
                }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        Assert.NotEmpty(files);

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // The ConstPropertyNameHeuristic should produce "WithKindAlpha" and "WithKindBeta"
        // since these have 1 const property each (within the 3 limit) and no required array
        Assert.True(
            typeNames.Any(n => n!.Contains("Kind", StringComparison.OrdinalIgnoreCase) &&
                              n!.Contains("Alpha", StringComparison.OrdinalIgnoreCase)),
            $"Expected const-named type for 'kind: alpha'. Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_NestedPaths_ExercisesPathNameHeuristic()
    {
        string schemaPath = Path.Combine(SchemasDir, "nested-path-names.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // PathNameHeuristic should produce types named from nested property paths
        Assert.True(
            typeNames.Any(n => n!.Contains("Database", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Database' from path heuristic. Got: {string.Join(", ", typeNames)}");
        Assert.True(
            typeNames.Any(n => n!.Contains("Connection", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Connection' from path heuristic. Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_NameCollisions_ExercisesCollisionResolver()
    {
        string schemaPath = Path.Combine(SchemasDir, "name-collisions.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // Should have at least one Address type
        Assert.True(
            typeNames.Any(n => n!.Contains("Address", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Address' type. Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_NumericFormats_ExercisesFormatHandlers()
    {
        string schemaPath = Path.Combine(SchemasDir, "numeric-and-format.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        // The schema has format: email, date-time, uri, ipv4, ipv6, duration
        // plus multipleOf and min/max — all should produce generated code
        string allCode = string.Join("\n", files.Select(f => f.FileContent));
        Assert.True(allCode.Length > 0, "Expected non-empty generated code");
    }

    [Fact]
    public async Task GenerateCode_BigMultipleOf_ExercisesBigIntegerPath()
    {
        // A multipleOf value exceeding UInt64.MaxValue triggers the BigInteger code path
        string schemaContent = """
            {
              "type": "object",
              "properties": {
                "value": {
                  "type": "number",
                  "multipleOf": 18446744073709551617
                }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Should generate BigInteger validation code
        Assert.Contains("BigInteger", allCode);
    }

    [Fact]
    public async Task GenerateCode_ManyConstProperties_BailsOutOfConstHeuristic()
    {
        // More than 3 const properties causes ConstPropertyNameHeuristic to bail out
        string schemaContent = """
            {
              "type": "object",
              "properties": {
                "items": {
                  "type": "array",
                  "items": {
                    "oneOf": [
                      {
                        "type": "object",
                        "properties": {
                          "a": { "const": "x" },
                          "b": { "const": "y" },
                          "c": { "const": "z" },
                          "d": { "const": "w" },
                          "e": { "type": "string" }
                        },
                        "required": ["a", "b", "c", "d"]
                      }
                    ]
                  }
                }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        Assert.NotEmpty(files);

        // With 4 const properties, the heuristic should NOT produce a "WithA..." name
        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        Assert.True(
            !typeNames.Any(n => n!.StartsWith("WithA", StringComparison.Ordinal)),
            $"Expected const heuristic to bail out with 4+ properties. Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_SpecialCharPropertyNames_ExercisesEmptyNameFallback()
    {
        // Property names with only special characters (no letters or digits)
        // cause FormatTypeNameComponent to produce 0 characters, hitting the empty name fallback
        string schemaContent = """
            {
              "type": "object",
              "properties": {
                "---": {
                  "type": "object",
                  "properties": {
                    "value": { "type": "string" }
                  }
                }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        Assert.NotEmpty(files);

        // The fallback should produce a type name with the Entity suffix
        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // At least one type should have the "Entity" suffix from the empty name fallback
        Assert.True(
            typeNames.Any(n => n!.Contains("Entity", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Entity' suffix from empty name fallback. Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_VeryLongPropertyName_ThrowsIdentifierTooLong()
    {
        // A property name longer than 512 characters should trigger ThrowIdentifierTooLongException
        string longName = new string('a', 520);
        string schemaContent = $$"""
            {
              "type": "object",
              "properties": {
                "{{longName}}": {
                  "type": "object",
                  "properties": {
                    "value": { "type": "string" }
                  }
                }
              }
            }
            """;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GenerateInProcessFromContent(schemaContent));
    }

    [Fact]
    public async Task GenerateCode_GetFullyQualifiedDotnetTypeName_ReturnsQualifiedName()
    {
        string schemaContent = """
            {
              "type": "object",
              "properties": {
                "name": { "type": "string" }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        Assert.NotEmpty(files);

        // GetFullyQualifiedDotnetTypeName should return namespace-qualified names
        string fqn = CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(files.First());
        Assert.NotNull(fqn);
        Assert.Contains("TestGenerated", fqn);
    }

    [Fact]
    public void GetNameHeuristicNames_ReturnsOrderedHeuristics()
    {
        var options = new CSharpLanguageProvider.Options(defaultNamespace: "Test");
        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        IEnumerable<(string Name, bool IsOptional)> heuristics = languageProvider.GetNameHeuristicNames();

        var list = heuristics.ToList();
        Assert.NotEmpty(list);

        // Required heuristics should come before optional ones
        int firstOptionalIndex = list.FindIndex(h => h.IsOptional);
        if (firstOptionalIndex > 0)
        {
            Assert.True(list.Take(firstOptionalIndex).All(h => !h.IsOptional));
        }
    }

    [Fact]
    public async Task GenerateCode_EncodedUriDefs_ExercisesBaseSchemaNameHeuristic()
    {
        // $defs keys that are JSON-Pointer-encoded URIs trigger TryGetNameFromEncodedUri
        // in BaseSchemaNameHeuristic, extracting filename stems as type names
        string schemaPath = Path.Combine(SchemasDir, "encoded-uri-defs.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // BaseSchemaNameHeuristic should extract "AsyncAgent" from the encoded URI key
        Assert.True(
            typeNames.Any(n => n!.Contains("AsyncAgent", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'AsyncAgent' from encoded URI def. Got: {string.Join(", ", typeNames)}");

        // And "ServiceConfig" from the second encoded URI key
        Assert.True(
            typeNames.Any(n => n!.Contains("ServiceConfig", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'ServiceConfig' from encoded URI def. Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_CollidingUriDefs_DisambiguatesWithParentSegment()
    {
        // Two $defs with the same filename stem ("agent.json") but different paths
        // forces BaseSchemaNameHeuristic to use progressively longer path suffixes
        // to disambiguate (e.g. "BackendAgent" and "FrontendAgent")
        string schemaPath = Path.Combine(SchemasDir, "colliding-uri-defs.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // Both should be named differently — at least one should include the parent dir
        int agentCount = typeNames.Count(n => n!.Contains("Agent", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            agentCount >= 2,
            $"Expected at least 2 Agent types (disambiguated). Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_BothMode_ProducesMoreOrEqualFiles()
    {
        string schemaPath = Path.Combine(SchemasDir, "numeric-and-format.json");
        IReadOnlyCollection<GeneratedCodeFile> filesTypeOnly = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.TypeGeneration);

        IReadOnlyCollection<GeneratedCodeFile> filesBoth = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        // Both mode should produce at least as many files as type-only
        Assert.True(
            filesBoth.Count >= filesTypeOnly.Count,
            $"Expected Both ({filesBoth.Count}) >= TypeGeneration ({filesTypeOnly.Count})");
    }

    [Fact]
    public async Task GenerateCode_CancelledToken_ReturnsEmpty()
    {
        string schemaPath = Path.Combine(SchemasDir, "numeric-and-format.json");

        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver(),
            new HttpClientDocumentResolver(new HttpClient()));

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        JsonReference reference = new(schemaPath);
        TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
            reference,
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

        var options = new CSharpLanguageProvider.Options(
            defaultNamespace: "TestGenerated",
            codeGenerationMode: CodeGenerationMode.TypeGeneration);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        // Use a pre-cancelled token — should hit IsCancellationRequested early and return empty
        using CancellationTokenSource cts = new();
        cts.Cancel();

        IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
            languageProvider,
            [rootType],
            cts.Token);

        Assert.Empty(files);
    }

    private static async Task<IReadOnlyCollection<GeneratedCodeFile>> GenerateInProcess(
        string schemaPath,
        CodeGenerationMode mode = CodeGenerationMode.TypeGeneration)
    {
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver(),
            new HttpClientDocumentResolver(new HttpClient()));

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        JsonReference reference = new(schemaPath);
        TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
            reference,
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

        var options = new CSharpLanguageProvider.Options(
            defaultNamespace: "TestGenerated",
            codeGenerationMode: mode);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        return typeBuilder.GenerateCodeUsing(
            languageProvider,
            [rootType],
            CancellationToken.None);
    }

    private static async Task<IReadOnlyCollection<GeneratedCodeFile>> GenerateInProcessFromContent(
        string schemaContent)
    {
        // Write schema to a temp file
        string tempFile = Path.Combine(Path.GetTempPath(), $"test-schema-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, schemaContent);
            return await GenerateInProcess(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
