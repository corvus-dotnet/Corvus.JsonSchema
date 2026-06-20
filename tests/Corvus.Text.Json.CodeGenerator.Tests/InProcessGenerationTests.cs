using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// In-process code generation tests that exercise name heuristics,
/// format handlers, and validation handlers within the test process
/// so coverage is captured by instrumentation.
/// </summary>
[TestClass]
public class InProcessGenerationTests
{
    private static readonly string SchemasDir = CodeGeneratorRunner.GetFixturePath("Schemas");

    [TestMethod]
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
        Assert.IsTrue((files).Any());

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // The ConstPropertyNameHeuristic should produce "WithKindAlpha" and "WithKindBeta"
        // since these have 1 const property each (within the 3 limit) and no required array
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("Kind", StringComparison.OrdinalIgnoreCase) &&
                              n!.Contains("Alpha", StringComparison.OrdinalIgnoreCase)),
            $"Expected const-named type for 'kind: alpha'. Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
    public async Task GenerateCode_NestedPaths_ExercisesPathNameHeuristic()
    {
        string schemaPath = Path.Combine(SchemasDir, "nested-path-names.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // PathNameHeuristic should produce types named from nested property paths
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("Database", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Database' from path heuristic. Got: {string.Join(", ", typeNames)}");
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("Connection", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Connection' from path heuristic. Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
    public async Task GenerateCode_NameCollisions_ExercisesCollisionResolver()
    {
        string schemaPath = Path.Combine(SchemasDir, "name-collisions.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // Should have at least one Address type
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("Address", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Address' type. Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
    public async Task GenerateCode_NumericFormats_ExercisesFormatHandlers()
    {
        string schemaPath = Path.Combine(SchemasDir, "numeric-and-format.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        // The schema has format: email, date-time, uri, ipv4, ipv6, duration
        // plus multipleOf and min/max — all should produce generated code
        string allCode = string.Join("\n", files.Select(f => f.FileContent));
        Assert.IsTrue(allCode.Length > 0, "Expected non-empty generated code");
    }

    [TestMethod]
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

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Should generate BigInteger validation code
        StringAssert.Contains(allCode, "BigInteger");
    }

    [TestMethod]
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
        Assert.IsTrue((files).Any());

        // With 4 const properties, the heuristic should NOT produce a "WithA..." name
        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        Assert.IsTrue(
            !typeNames.Any(n => n!.StartsWith("WithA", StringComparison.Ordinal)),
            $"Expected const heuristic to bail out with 4+ properties. Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
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
        Assert.IsTrue((files).Any());

        // The fallback should produce a type name with the Entity suffix
        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // At least one type should have the "Entity" suffix from the empty name fallback
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("Entity", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Entity' suffix from empty name fallback. Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
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

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => GenerateInProcessFromContent(schemaContent));
    }

    [TestMethod]
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
        Assert.IsTrue((files).Any());

        // GetFullyQualifiedDotnetTypeName should return namespace-qualified names
        string fqn = CSharpLanguageProvider.GetFullyQualifiedDotnetTypeName(files.First());
        Assert.IsNotNull(fqn);
        StringAssert.Contains(fqn, "TestGenerated");
    }

    [TestMethod]
    public void GetNameHeuristicNames_ReturnsOrderedHeuristics()
    {
        var options = new CSharpLanguageProvider.Options(defaultNamespace: "Test");
        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        IEnumerable<(string Name, bool IsOptional)> heuristics = languageProvider.GetNameHeuristicNames();

        var list = heuristics.ToList();
        Assert.IsTrue((list).Any());

        // Required heuristics should come before optional ones
        int firstOptionalIndex = list.FindIndex(h => h.IsOptional);
        if (firstOptionalIndex > 0)
        {
            Assert.IsTrue(list.Take(firstOptionalIndex).All(h => !h.IsOptional));
        }
    }

    [TestMethod]
    public async Task GenerateCode_EncodedUriDefs_ExercisesBaseSchemaNameHeuristic()
    {
        // $defs keys that are JSON-Pointer-encoded URIs trigger TryGetNameFromEncodedUri
        // in BaseSchemaNameHeuristic, extracting filename stems as type names
        string schemaPath = Path.Combine(SchemasDir, "encoded-uri-defs.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // BaseSchemaNameHeuristic should extract "AsyncAgent" from the encoded URI key
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("AsyncAgent", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'AsyncAgent' from encoded URI def. Got: {string.Join(", ", typeNames)}");

        // And "ServiceConfig" from the second encoded URI key
        Assert.IsTrue(
            typeNames.Any(n => n!.Contains("ServiceConfig", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'ServiceConfig' from encoded URI def. Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
    public async Task GenerateCode_CollidingUriDefs_DisambiguatesWithParentSegment()
    {
        // Two $defs with the same filename stem ("agent.json") but different paths
        // forces BaseSchemaNameHeuristic to use progressively longer path suffixes
        // to disambiguate (e.g. "BackendAgent" and "FrontendAgent")
        string schemaPath = Path.Combine(SchemasDir, "colliding-uri-defs.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // Both should be named differently — at least one should include the parent dir
        int agentCount = typeNames.Count(n => n!.Contains("Agent", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            agentCount >= 2,
            $"Expected at least 2 Agent types (disambiguated). Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
    public async Task GenerateCode_CircularOneOfRefs_FailsWithSchemaLocations()
    {
        // Issue #810: a discriminated union whose oneOf branches $ref back to the union has a
        // circular (same-instance) composition. The branches' Evaluate would recurse forever, so
        // generating Match/TryGetAs that call Evaluate caused a runtime stack overflow. The
        // generator must instead detect the cycle and fail generation, naming the source and
        // target schema locations of the recursion.
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$ref": "#/$defs/miniUnion",
              "$defs": {
                "miniUnion": {
                  "title": "MiniUnion",
                  "type": "object",
                  "required": [ "kind" ],
                  "properties": { "kind": { "type": "string", "enum": [ "LeafA", "LeafB" ] } },
                  "oneOf": [ { "$ref": "#/$defs/miniLeafA" }, { "$ref": "#/$defs/miniLeafB" } ]
                },
                "miniLeafA": {
                  "title": "MiniLeafA",
                  "$ref": "#/$defs/miniUnion",
                  "required": [ "a" ],
                  "properties": { "kind": { "const": "LeafA" }, "a": { "type": "integer" } }
                },
                "miniLeafB": {
                  "title": "MiniLeafB",
                  "$ref": "#/$defs/miniUnion",
                  "required": [ "b" ],
                  "properties": { "kind": { "const": "LeafB" }, "b": { "type": "integer" } }
                }
              }
            }
            """;

        CircularSchemaReferenceException ex = await Assert.ThrowsExactlyAsync<CircularSchemaReferenceException>(
            () => GenerateInProcessFromContent(schemaContent));

        // The error must name the source and target schema locations of the recursion (the cycle
        // runs miniUnion <-> miniLeaf; either edge direction is a valid report).
        string locations = ex.ReferencingLocation + "|" + ex.ReferencedLocation;
        StringAssert.Contains(locations, "miniUnion");
        StringAssert.Contains(locations, "miniLeaf");
        StringAssert.Contains(ex.Message, "Circular schema reference");
    }

    // ── Issue #812: discriminated-union constituent-builder Source wiring ──────────────────────
    // The constituent object-builder Source wiring (a "<Constituent>BuilderInstance" backing field
    // and matching Kind cases) is normally suppressed when the parent has its own properties. It is
    // re-enabled for a discriminated union (a oneOf whose branches all carry a required const
    // discriminator and whose parent requires nothing else). These tests cover the detection branches.

    private const string DiscriminatedUnionDefs = """
        ,
        "$defs": {
          "circle": {
            "title": "Circle",
            "type": "object",
            "required": [ "kind", "radius" ],
            "properties": { "kind": { "const": "Circle" }, "radius": { "type": "number" } }
          },
          "rectangle": {
            "title": "Rectangle",
            "type": "object",
            "required": [ "kind", "width", "height" ],
            "properties": { "kind": { "const": "Rectangle" }, "width": { "type": "number" }, "height": { "type": "number" } }
          }
        }
        """;

    [TestMethod]
    public async Task GenerateCode_DiscriminatedUnion_EnablesConstituentBuilderWiring()
    {
        string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Shape",
              "type": "object",
              "required": [ "kind" ],
              "properties": { "kind": { "type": "string", "enum": [ "Circle", "Rectangle" ] } },
              "oneOf": [ { "$ref": "#/$defs/circle" }, { "$ref": "#/$defs/rectangle" } ]
            """.TrimEnd().TrimEnd('}') + DiscriminatedUnionDefs + "\n}";

        await AssertConstituentBuilderWiring(schema, expectedPresent: true);
    }

    [TestMethod]
    public async Task GenerateCode_DiscriminatedUnion_WithNonStructuralKeywords_StillEnabled()
    {
        // Non-structural keywords (title/description/$comment/examples/deprecated/readOnly) on the
        // base schema must NOT disqualify the discriminated union (they are INonStructuralKeyword).
        string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Shape",
              "description": "A shape that is either a circle or a rectangle.",
              "$comment": "Authoring note: discriminated by kind.",
              "examples": [ { "kind": "Circle", "radius": 1 } ],
              "deprecated": false,
              "readOnly": false,
              "type": "object",
              "required": [ "kind" ],
              "properties": { "kind": { "type": "string", "enum": [ "Circle", "Rectangle" ] } },
              "oneOf": [ { "$ref": "#/$defs/circle" }, { "$ref": "#/$defs/rectangle" } ]
            """.TrimEnd().TrimEnd('}') + DiscriminatedUnionDefs + "\n}";

        await AssertConstituentBuilderWiring(schema, expectedPresent: true);
    }

    [TestMethod]
    public async Task GenerateCode_OneOf_WithExtraRequiredProperty_DoesNotEnableWiring()
    {
        // The parent requires a structural property ("color") that the branches do not carry, so a
        // constituent build would be incomplete: the wiring must stay suppressed.
        string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Shape",
              "type": "object",
              "required": [ "kind", "color" ],
              "properties": {
                "kind": { "type": "string", "enum": [ "Circle", "Rectangle" ] },
                "color": { "type": "string" }
              },
              "oneOf": [ { "$ref": "#/$defs/circle" }, { "$ref": "#/$defs/rectangle" } ]
            """.TrimEnd().TrimEnd('}') + DiscriminatedUnionDefs + "\n}";

        await AssertConstituentBuilderWiring(schema, expectedPresent: false);
    }

    [TestMethod]
    public async Task GenerateCode_OneOf_WithoutConstDiscriminator_DoesNotEnableWiring()
    {
        // The parent has its own property but the branches have no required const discriminator, so
        // there is no discriminator guaranteeing a valid parent: the wiring must stay suppressed.
        string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Shape",
              "type": "object",
              "required": [ "id" ],
              "properties": { "id": { "type": "string" } },
              "oneOf": [
                { "title": "Circle", "type": "object", "properties": { "radius": { "type": "number" } } },
                { "title": "Rectangle", "type": "object", "properties": { "width": { "type": "number" } } }
              ]
            }
            """;

        await AssertConstituentBuilderWiring(schema, expectedPresent: false);
    }

    [TestMethod]
    public async Task GenerateCode_AnyOf_WithProperties_DoesNotEnableOneOfWiring()
    {
        // The detection only relaxes for oneOf; an anyOf parent with its own properties is not
        // treated as a oneOf discriminated union (covers the no-oneOf branch).
        string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Shape",
              "type": "object",
              "required": [ "kind" ],
              "properties": { "kind": { "type": "string", "enum": [ "Circle", "Rectangle" ] } },
              "anyOf": [ { "$ref": "#/$defs/circle" }, { "$ref": "#/$defs/rectangle" } ]
            """.TrimEnd().TrimEnd('}') + DiscriminatedUnionDefs + "\n}";

        await AssertConstituentBuilderWiring(schema, expectedPresent: false);
    }

    private static async Task AssertConstituentBuilderWiring(string schemaContent, bool expectedPresent)
    {
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // "BuilderInstance" appears only as the backing field for a composed constituent builder in
        // a parent Source (the parent's own object builder field is "_objectBuilder").
        bool present = allCode.Contains("BuilderInstance");

        Assert.AreEqual(
            expectedPresent,
            present,
            expectedPresent
                ? "Expected constituent-builder Source wiring for the discriminated union."
                : "Did not expect constituent-builder Source wiring for this schema.");
    }

    [TestMethod]
    public async Task GenerateCode_RecursiveDiscriminatedUnion_SuppressesCyclicConstituentSourceProjection()
    {
        // Regression for the recursive-union ref-struct containment cycle (CS0523). The #812 wiring
        // also projects each constituent's own Source through the union's Source by VALUE
        // (a "_<constituent>SourceInstance" field). For a recursive union — here Node = oneOf(Leaf, Branch)
        // where Branch carries a "child" of type Node — projecting Branch.Source into Node.Source by value
        // closes a cycle (Node.Source -> Branch.Source -> (child _createArg) Node.Source), which the C#
        // compiler rejects as "causes a cycle in the struct layout". The generator must suppress the
        // by-value Source projection for the cyclic constituent (Branch), while keeping it for the
        // acyclic one (Leaf); Branch stays reachable through the builder path.
        string schema = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Node",
              "type": "object",
              "required": [ "kind" ],
              "properties": { "kind": { "type": "string", "enum": [ "Leaf", "Branch" ] } },
              "oneOf": [ { "$ref": "#/$defs/leaf" }, { "$ref": "#/$defs/branch" } ],
              "$defs": {
                "leaf": {
                  "title": "Leaf",
                  "type": "object",
                  "required": [ "kind", "value" ],
                  "properties": { "kind": { "const": "Leaf" }, "value": { "type": "string" } }
                },
                "branch": {
                  "title": "Branch",
                  "type": "object",
                  "required": [ "kind", "child" ],
                  "properties": { "kind": { "const": "Branch" }, "child": { "$ref": "#" } }
                }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schema);
        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // The acyclic constituent keeps its by-value Source projection (proves #812 is active here).
        Assert.IsTrue(
            allCode.Contains("_leafSourceInstance", StringComparison.Ordinal),
            "Expected the acyclic constituent (Leaf) to keep its by-value Source projection.");

        // The cyclic constituent must NOT be projected by value — that is exactly the CS0523 cycle.
        Assert.IsFalse(
            allCode.Contains("_branchSourceInstance", StringComparison.Ordinal),
            "The cyclic constituent (Branch) must not be projected by value (would emit a self-containing ref struct, CS0523).");
        Assert.IsFalse(
            allCode.Contains(".Branch.Source value)", StringComparison.Ordinal),
            "The cyclic constituent (Branch) must not expose a Source-accepting union constructor.");

        // ...but Branch remains reachable through the builder path.
        Assert.IsTrue(
            allCode.Contains(".Branch.Builder.Build value)", StringComparison.Ordinal),
            "The cyclic constituent (Branch) must still be reachable through its builder.");
    }

    [TestMethod]
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
        Assert.IsTrue(
            filesBoth.Count >= filesTypeOnly.Count,
            $"Expected Both ({filesBoth.Count}) >= TypeGeneration ({filesTypeOnly.Count})");
    }

    [TestMethod]
    public async Task GenerateCode_ComplexValidation_ExercisesOneOfAnyOfIfThenElse()
    {
        // Schema with oneOf, anyOf, if/then/else, array constraints, additionalProperties,
        // propertyNames, uniqueItems — exercises TypeDeclarationExtensions reduction methods
        // and multiple CodeGeneratorExtensions paths
        string schemaPath = Path.Combine(SchemasDir, "complex-validation.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Should generate oneOf-related discriminator code
        Assert.IsTrue(allCode.Length > 1000, "Expected substantial generated code for complex schema");
    }

    [TestMethod]
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
        Assert.IsTrue((files).Any());
    }

    [TestMethod]
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

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Should contain const string comparison code (anyOf with string consts)
        StringAssert.Contains(allCode, "active");
        // Should contain validation method calls for the anyOf structure
        StringAssert.Contains(allCode, "AnyOf");
    }

    [TestMethod]
    public async Task GenerateCode_BothMode_AnyOfConst_ExercisesDualPaths()
    {
        // Same schema in Both mode — exercises both type generation and evaluator paths
        string schemaPath = Path.Combine(SchemasDir, "anyof-const-evaluator.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Both mode should produce evaluator and type code
        Assert.IsTrue(allCode.Length > 5000, "Expected substantial code from both-mode generation");
    }

    [TestMethod]
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

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // Verify enum switch is generated (numeric enum with 10 values)
        StringAssert.Contains(allCode, "switch");
        // Verify format validation code is generated
        StringAssert.Contains(allCode, "MatchInt32");
    }

    [TestMethod]
    public async Task GenerateCode_ComposedWithArray_ExercisesArrayBuilderPath()
    {
        // A oneOf with an array branch triggers the ComposedBuilder.ArrayInstanceName path
        // in CodeGeneratorExtensions.Builder (lines 384-407, 809-832, 3520-3536, 3944-3958, 4111-4142)
        string schemaPath = Path.Combine(SchemasDir, "composed-with-array.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));

        // The array branch should produce builder code with array kind handling
        StringAssert.Contains(allCode, "Builder");
    }

    [TestMethod]
    public async Task GenerateCode_ComposedWithArray_InBothMode_ProducesEvaluatorAndBuilder()
    {
        // Both mode exercises more builder paths (property-level composed builders)
        string schemaPath = Path.Combine(SchemasDir, "composed-with-array.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(
            schemaPath,
            CodeGenerationMode.Both);

        Assert.IsTrue((files).Any());

        string allCode = string.Join("\n", files.Select(f => f.FileContent));
        StringAssert.Contains(allCode, "Kind");
    }

    [TestMethod]
    public async Task GenerateCode_NestedNameCollision_ExercisesCollisionResolver()
    {
        // Nested properties with the same name create name collisions
        // that exercise DefaultNameCollisionResolver fragment navigation (lines 82-93)
        string schemaPath = Path.Combine(SchemasDir, "nested-name-collision.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());

        string[] typeNames = files
            .Select(f => CSharpLanguageProvider.GetDotnetTypeName(f))
            .Where(n => n is not null)
            .ToArray()!;

        // Should have types with disambiguation — both "Config" properties can't produce
        // the same type name, so the collision resolver should intervene
        int configCount = typeNames.Count(n => n!.Contains("Config", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(
            configCount >= 2,
            $"Expected at least 2 Config-related types (disambiguated). Got: {string.Join(", ", typeNames)}");
    }

    [TestMethod]
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
        Assert.IsTrue(canReduce, "A pure oneOf schema should be reducible to oneOf");
    }

    [TestMethod]
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
        Assert.IsTrue(canReduce, "A pure anyOf schema should be reducible to anyOf");
    }

    [TestMethod]
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

            Assert.IsTrue((files).Any());

            // Find the nested child type (named "Nested" from the property)
            TypeDeclaration nestedType = rootType.Children()
                .FirstOrDefault(c => c.TryGetDotnetTypeName(out string n) && n.Contains("Nested"));

            if (nestedType is not null)
            {
                // "Items" is a property on the parent, so matching against it should return true
                bool matchesProperty = nestedType.MatchesExistingPropertyNameInParent("Items".AsSpan());
                Assert.IsTrue(matchesProperty, "Expected 'Items' to match a property name in the parent");
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

    [TestMethod]
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

            Assert.IsTrue((files).Any());

            // Get children of root
            var children = rootType.Children().ToList();
            Assert.IsTrue(children.Count >= 2, $"Expected at least 2 children. Got: {children.Count}");

            // Pick a child and check if its name matches another child in the parent
            TypeDeclaration firstChild = children[0];
            if (firstChild.TryGetDotnetTypeName(out string firstName))
            {
                // MatchesExistingTypeInParent should find the same type (it checks ALL children)
                bool matchesType = firstChild.MatchesExistingTypeInParent(firstName.AsSpan());
                Assert.IsTrue(matchesType, $"Expected '{firstName}' to match an existing type in the parent");
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

    [TestMethod]
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
            Assert.IsTrue(requiresNumericValidation, "Expected RequiresNumberValueValidation to be true for schema with numeric constraints");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
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

        Assert.AreEqual(0, (files).Count());
    }

    [TestMethod]
    public async Task GenerateCode_ContentMediaTypePre201909_ExercisesAllFormatPaths()
    {
        // Exercises ContentMediaTypePre201909Keyword.TryGetFormat:
        // - application/json (no encoding) → corvus-json-content-pre201909
        // - application/json + base64 → corvus-base64-content-pre201909
        // - application/octet-stream + base64 → corvus-base64-content
        // - application/json + gzip → returns false (lines 73-75)
        // - text/plain → returns false (lines 94-95)
        string schemaPath = Path.Combine(SchemasDir, "content-media-pre201909.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());
    }

    [TestMethod]
    public async Task GenerateCode_ContentMediaType202012_ExercisesAllFormatPaths()
    {
        // Exercises ContentMediaTypeKeyword.TryGetFormat paths (same structure, different keyword)
        string schemaPath = Path.Combine(SchemasDir, "content-media-202012.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());
    }

    [TestMethod]
    public async Task GenerateCode_RefHidesSiblings_Draft7_ExercisesDollarRefHidesSiblingsKeyword()
    {
        // Exercises DollarRefHidesSiblingsKeyword: in Draft 7, $ref hides sibling keywords
        string schemaPath = Path.Combine(SchemasDir, "ref-hides-siblings-draft7.json");

        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver(),
            new HttpClientDocumentResolver(new HttpClient()));

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        JsonReference reference = new(schemaPath);
        TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
            reference,
            Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary);

        var options = new CSharpLanguageProvider.Options(defaultNamespace: "TestGenerated");
        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
            languageProvider, [rootType], CancellationToken.None);

        Assert.IsTrue((files).Any());

        // Verify that has-siblings-hiding-keyword is detected
        Assert.IsTrue(rootType.HasSiblingHidingKeyword() || rootType.Children().Any(c => c.HasSiblingHidingKeyword()),
            "Expected DollarRefHidesSiblingsKeyword to be detected in Draft 7 schema with $ref");
    }

    [TestMethod]
    public async Task GenerateCode_AnchorsAndDynamicRef_ExercisesAnchorResolution()
    {
        // Exercises Anchors.cs: AddAnchors, GetLocationAndPointerForAnchor, ApplyScopeToNewType
        string schemaPath = Path.Combine(SchemasDir, "anchors-and-dynamic-ref.json");
        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcess(schemaPath);

        Assert.IsTrue((files).Any());
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

    [TestMethod]
    public async Task Validation_TryGetValidationConstantForKeyword_ReturnsMinimumValue()
    {
        // Exercises Validation.TryGetValidationConstantForKeyword (lines 25-38)
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "integer",
              "minimum": 42
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

            // Now rootType.BuildComplete is true; exercise validation constant retrieval
            var minimumKeyword = Corvus.Json.CodeGeneration.Keywords.MinimumKeyword.Instance;

            bool found = rootType.TryGetValidationConstantForKeyword(minimumKeyword, null, out System.Text.Json.JsonElement value);

            Assert.IsTrue(found, "Expected to find validation constant for minimum keyword");
            Assert.AreEqual(42, value.GetInt32());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task Validation_GetValidationConstantCountForKeyword_ReturnsCorrectCount()
    {
        // Exercises Validation.GetValidationConstantCountForKeyword (lines 47-56)
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "string",
              "enum": ["alpha", "beta", "gamma"]
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

            // Exercise GetValidationConstantCountForKeyword
            var enumKeyword = Corvus.Json.CodeGeneration.Keywords.EnumKeyword.Instance;

            int count = rootType.GetValidationConstantCountForKeyword(enumKeyword);

            // enum with 3 values should yield 3 validation constants
            Assert.AreEqual(3, count);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task Validation_TryGetValidationConstant_NotFound_ReturnsFalse()
    {
        // Exercises the false-return path (lines 36-37 of Validation.cs)
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "string"
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

            // No minimum keyword on a plain string type
            var minimumKeyword = Corvus.Json.CodeGeneration.Keywords.MinimumKeyword.Instance;

            bool found = rootType.TryGetValidationConstantForKeyword(minimumKeyword, null, out System.Text.Json.JsonElement value);

            Assert.IsFalse(found);
            Assert.AreEqual(default, value);

            int count = rootType.GetValidationConstantCountForKeyword(minimumKeyword);
            Assert.AreEqual(0, count);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task Validation_TryGetValidationConstant_WithIndex_ReturnsCorrectElement()
    {
        // Exercises the index path (line 26: int i = index ?? 0)
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "string",
              "enum": ["first", "second", "third"]
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

            var enumKeyword = Corvus.Json.CodeGeneration.Keywords.EnumKeyword.Instance;

            // Get the second element (index 1)
            bool found = rootType.TryGetValidationConstantForKeyword(enumKeyword, 1, out System.Text.Json.JsonElement value);
            Assert.IsTrue(found);
            Assert.AreEqual("second", value.GetString());

            // Out-of-range index returns false
            bool outOfRange = rootType.TryGetValidationConstantForKeyword(enumKeyword, 99, out _);
            Assert.IsFalse(outOfRange);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task GenerateCode_PropertyCollidesWithChildType_NoBufferCorruption()
    {
        // Regression test for buffer corruption bug in PropertyDeclarationExtensions.BuildDotnetPropertyName.
        // When a property name collides with a child type (gets "Value" suffix) AND then collides again
        // with another property that already owns that suffixed name, the numeric suffix was written
        // at the stale buffer position (before "Value"), producing corrupted identifiers with NUL bytes.
        //
        // Schema structure: "schema" property collides with child type "SchemaEntity",
        // "$schema" property already takes "SchemaValue", so "schema" must get "SchemaValue1" —
        // but with the bug it would produce "Schema1alue\0".
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "$schema": { "type": "string" },
                "schema": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string" }
                  }
                },
                "value": { "type": "string" }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        Assert.IsTrue(files.Any());

        // Verify no NUL bytes in any generated file (the corruption manifests as \0 characters)
        foreach (GeneratedCodeFile file in files)
        {
            Assert.IsFalse(
                file.FileContent.Contains('\0'),
                $"Generated file '{file.FileName}' contains NUL byte corruption");
        }

        // Verify all property identifier tokens are valid C# identifiers (no embedded digits in wrong positions)
        string allCode = string.Join("\n", files.Select(f => f.FileContent));
        Assert.IsFalse(
            allCode.Contains("1alue", StringComparison.Ordinal),
            "Generated code contains corrupted identifier '1alue' (buffer corruption bug)");
    }

    [TestMethod]
    public async Task GenerateCode_PropertyNamedPatternProperties_WithPatternPropertiesKeyword_NoDuplicateFields()
    {
        // Regression test for duplicate field name bug when a schema has BOTH:
        // 1. A named property literally called "patternProperties" (common in meta-schemas)
        // 2. AND actual patternProperties keyword validation on the same object
        //
        // Both PropertySubschemaChildHandler and PatternPropertiesValidationHandler would emit
        // "PatternPropertiesSchemaEvaluationPath" as a field name. The fix makes the second handler
        // use GetUniquePropertyNameInScope to disambiguate.
        string schemaContent = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "patternProperties": {
                  "type": "object",
                  "additionalProperties": { "type": "string" }
                }
              },
              "patternProperties": {
                "^x-": { "type": "string" }
              }
            }
            """;

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateInProcessFromContent(schemaContent);
        Assert.IsTrue(files.Any());

        // Check that no single file contains duplicate static readonly field declarations
        foreach (GeneratedCodeFile file in files)
        {
            string[] lines = file.FileContent.Split('\n');
            List<string> fieldDeclarations = lines
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("private static readonly JsonSchemaPathProvider ", StringComparison.Ordinal))
                .ToList();

            HashSet<string> uniqueDeclarations = new(fieldDeclarations);
            Assert.AreEqual(
                fieldDeclarations.Count,
                uniqueDeclarations.Count,
                $"File '{file.FileName}' contains duplicate static readonly field declarations: " +
                string.Join(", ", fieldDeclarations.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));
        }
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
