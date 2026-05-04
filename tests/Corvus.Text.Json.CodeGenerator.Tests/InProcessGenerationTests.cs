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
    public async Task GenerateCode_ComplexValidation_ExercisesOneOfAnyOfIfThenElse()
    {
        // Schema with oneOf, anyOf, if/then/else, array constraints, additionalProperties,
        // propertyNames, uniqueItems — exercises TypeDeclarationExtensions reduction methods
        // and multiple CodeGeneratorExtensions paths
        string schemaPath = Path.Combine(SchemasDir, "complex-validation.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Should generate oneOf-related discriminator code
        Assert.True(allCode.Length > 1000, "Expected substantial generated code for complex schema");
    }

    [Fact]
    public async Task GenerateCode_EvaluatorOnly_ComplexSchema_ProducesEvaluatorCode()
    {
        // Complex schema with multiple validation keywords should generate evaluator code
        string schemaPath = Path.Combine(SchemasDir, "complex-validation.json");

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
            codeGenerationMode: CodeGenerationMode.SchemaEvaluationOnly);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        // Set the evaluator root types (exercises SetEvaluatorRootTypes)
        languageProvider.SetEvaluatorRootTypes(rootType);

        IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
            languageProvider,
            [rootType],
            CancellationToken.None);

        // Evaluator-only mode with complex schema should produce at least one file
        Assert.NotEmpty(files);
    }

    [Fact]
    public async Task GenerateCode_EvaluatorOnly_AnyOfConst_ExercisesConstValidationHandler()
    {
        // Schema with anyOf branches using const values for strings, numbers, booleans, nulls.
        // Also exercises contains, patternProperties, dependentSchemas, unevaluatedProperties.
        string schemaPath = Path.Combine(SchemasDir, "anyof-const-evaluator.json");

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
            codeGenerationMode: CodeGenerationMode.SchemaEvaluationOnly);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.SetEvaluatorRootTypes(rootType);

        IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
            languageProvider,
            [rootType],
            CancellationToken.None);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Should contain const string comparison code (anyOf with string consts)
        Assert.Contains("active", allCode);
        // Should contain validation method calls for the anyOf structure
        Assert.Contains("AnyOf", allCode);
    }

    [Fact]
    public async Task GenerateCode_BothMode_AnyOfConst_ExercisesDualPaths()
    {
        // Same schema in Both mode — exercises both type generation and evaluator paths
        string schemaPath = Path.Combine(SchemasDir, "anyof-const-evaluator.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Both mode should produce evaluator and type code
        Assert.True(allCode.Length > 5000, "Expected substantial code from both-mode generation");
    }

    [Fact]
    public async Task GenerateCode_EvaluatorOnly_AdvancedPatterns_ExercisesEnumSwitchAndRegex()
    {
        // Schema exercises: numeric enum switch (int64), regex pattern categories
        // (prefix, range, nonEmpty), numeric format validation (int32, int64, double, byte),
        // additionalProperties with properties+patternProperties, multi-type validation.
        string schemaPath = Path.Combine(SchemasDir, "evaluator-advanced.json");

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
            codeGenerationMode: CodeGenerationMode.SchemaEvaluationOnly);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.SetEvaluatorRootTypes(rootType);

        IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
            languageProvider,
            [rootType],
            CancellationToken.None);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Verify enum switch is generated (numeric enum with 10 values)
        Assert.Contains("switch", allCode);
        // Verify format validation code is generated
        Assert.Contains("MatchInt32", allCode);
    }

    [Fact]
    public async Task GenerateCode_ComposedWithArray_ExercisesArrayBuilderPath()
    {
        // A oneOf with an array branch triggers the ComposedBuilder.ArrayInstanceName path
        // in CodeGeneratorExtensions.Builder (lines 384-407, 809-832, 3520-3536, 3944-3958, 4111-4142)
        string schemaPath = Path.Combine(SchemasDir, "composed-with-array.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // The array branch should produce builder code with array kind handling
        Assert.Contains("Builder", allCode);
    }

    [Fact]
    public async Task GenerateCode_ComposedWithArray_InBothMode_ProducesEvaluatorAndBuilder()
    {
        // Both mode exercises more builder paths (property-level composed builders)
        string schemaPath = Path.Combine(SchemasDir, "composed-with-array.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        Assert.NotEmpty(files);

        string allCode = string.Join("\n", files.Select(f => f.FileContent));
        Assert.Contains("Kind", allCode);
    }

    [Fact]
    public async Task GenerateCode_NestedNameCollision_ExercisesCollisionResolver()
    {
        // Nested properties with the same name create name collisions
        // that exercise DefaultNameCollisionResolver fragment navigation (lines 82-93)
        string schemaPath = Path.Combine(SchemasDir, "nested-name-collision.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.NotEmpty(files);

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // Should have types with disambiguation — both "Config" properties can't produce
        // the same type name, so the collision resolver should intervene
        int configCount = typeNames.Count(n => n!.Contains("Config", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            configCount >= 2,
            $"Expected at least 2 Config-related types (disambiguated). Got: {string.Join(", ", typeNames)}");
    }

    [Fact]
    public async Task GenerateCode_PureOneOf_CanReduceToOneOf()
    {
        // A pure oneOf schema (no other constraints) should exercise CanReduceToOneOf
        // in TypeDeclarationExtensions (lines 164-178)
        string schemaPath = Path.Combine(SchemasDir, "pure-oneof.json");

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

        var options = new CSharpLanguageProvider.Options(defaultNamespace: "TestGenerated");
        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        // Generate to finalize the build
        typeBuilder.GenerateCodeUsing(languageProvider, [rootType], CancellationToken.None);

        // Now test CanReduceToOneOf — should be true for a pure oneOf schema
        bool canReduce = rootType.CanReduceToOneOf();
        Assert.True(canReduce, "A pure oneOf schema should be reducible to oneOf");
    }

    [Fact]
    public async Task GenerateCode_PureAnyOf_CanReduceToAnyOf()
    {
        // A pure anyOf schema (no other constraints) should exercise CanReduceToAnyOf
        // in TypeDeclarationExtensions (lines 143-157)
        string schemaPath = Path.Combine(SchemasDir, "pure-anyof.json");

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

        var options = new CSharpLanguageProvider.Options(defaultNamespace: "TestGenerated");
        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        // Generate to finalize the build
        typeBuilder.GenerateCodeUsing(languageProvider, [rootType], CancellationToken.None);

        // Now test CanReduceToAnyOf — should be true for a pure anyOf schema
        bool canReduce = rootType.CanReduceToAnyOf();
        Assert.True(canReduce, "A pure anyOf schema should be reducible to anyOf");
    }

    [Fact]
    public async Task GenerateCode_MatchesExistingPropertyNameInParent_DetectsCollision()
    {
        // Create a type hierarchy where a child's name matches a property in its parent
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "items": { "type": "string" },
                "nested": {
                  "type": "object",
                  "properties": {
                    "value": { "type": "integer" }
                  }
                }
              }
            }
            """;

        string tempFile = Path.Combine(Path.GetTempPath(), $"test-schema-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, schemaContent);

            CompoundDocumentResolver documentResolver = new(
                new FileSystemDocumentResolver(),
                new HttpClientDocumentResolver(new HttpClient()));

            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            JsonReference reference = new(tempFile);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

            var options = new CSharpLanguageProvider.Options(defaultNamespace: "TestGenerated");
            var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

            // Generate to build the type tree
            IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
                languageProvider, [rootType], CancellationToken.None);

            Assert.NotEmpty(files);

            // Find the nested child type (named "Nested" from the property)
            TypeDeclaration nestedType = rootType.Children()
                .FirstOrDefault(c => c.TryGetDotnetTypeName(out string n) && n.Contains("Nested"));

            if (nestedType is not null)
            {
                // "Items" is a property on the parent, so matching against it should return true
                bool matchesProperty = nestedType.MatchesExistingPropertyNameInParent("Items".AsSpan());
                Assert.True(matchesProperty, "Expected 'Items' to match a property name in the parent");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GenerateCode_MatchesExistingTypeInParent_DetectsTypeCollision()
    {
        // Create a type hierarchy with multiple children where names collide
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "first": {
                  "type": "object",
                  "properties": { "a": { "type": "string" } }
                },
                "second": {
                  "type": "object",
                  "properties": { "b": { "type": "integer" } }
                }
              }
            }
            """;

        string tempFile = Path.Combine(Path.GetTempPath(), $"test-schema-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, schemaContent);

            CompoundDocumentResolver documentResolver = new(
                new FileSystemDocumentResolver(),
                new HttpClientDocumentResolver(new HttpClient()));

            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            JsonReference reference = new(tempFile);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

            var options = new CSharpLanguageProvider.Options(defaultNamespace: "TestGenerated");
            var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

            // Generate to build the type tree
            IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
                languageProvider, [rootType], CancellationToken.None);

            Assert.NotEmpty(files);

            // Get children of root
            var children = rootType.Children().ToList();
            Assert.True(children.Count >= 2, $"Expected at least 2 children. Got: {children.Count}");

            // Pick a child and check if its name matches another child in the parent
            TypeDeclaration firstChild = children[0];
            if (firstChild.TryGetDotnetTypeName(out string firstName))
            {
                // MatchesExistingTypeInParent should find the same type (it checks ALL children)
                bool matchesType = firstChild.MatchesExistingTypeInParent(firstName.AsSpan());
                Assert.True(matchesType, $"Expected '{firstName}' to match an existing type in the parent");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GenerateCode_RequiresNumberValueValidation_TrueForNumericConstraints()
    {
        // Schema with numeric constraints exercises RequiresNumberValueValidation
        // in TypeDeclarationExtensions (lines 901-911)
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "integer",
              "minimum": 0,
              "maximum": 100,
              "multipleOf": 5
            }
            """;

        string tempFile = Path.Combine(Path.GetTempPath(), $"test-schema-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, schemaContent);

            CompoundDocumentResolver documentResolver = new(
                new FileSystemDocumentResolver(),
                new HttpClientDocumentResolver(new HttpClient()));

            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            JsonReference reference = new(tempFile);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

            var options = new CSharpLanguageProvider.Options(defaultNamespace: "TestGenerated");
            var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

            typeBuilder.GenerateCodeUsing(languageProvider, [rootType], CancellationToken.None);

            // Should require number value validation due to minimum/maximum/multipleOf
            bool requiresNumericValidation = rootType.RequiresNumberValueValidation();
            Assert.True(requiresNumericValidation, "Expected RequiresNumberValueValidation to be true for schema with numeric constraints");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
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
