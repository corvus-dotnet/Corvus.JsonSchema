// <copyright file="OpenApiSchemaNameHeuristic20ShapesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// Integration tests for the OpenAPI 2.0 (Swagger) fragment shapes handled by
/// <see cref="OpenApiSchemaNameHeuristic"/>: Parameter Objects and response Header
/// Objects addressed without a <c>/schema</c> tail, and response schemas without a
/// <c>content</c> media-type map.
/// </summary>
/// <remarks>
/// The 2.0 spec walker does not exist yet, so the schema pointers are hand-listed here
/// rather than collected; the pointer shapes match what
/// <c>SchemaPointerBuilder.BuildParameterObjectPointer</c> and friends produce.
/// </remarks>
[TestClass]
public class OpenApiSchemaNameHeuristic20ShapesTests
{
    private static readonly string SpecPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "TestData", "naming-heuristic-2.0.json"));

    private static Dictionary<string, string>? pointerToTypeName;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver());

        // The heuristic is vocabulary-agnostic; use 2020-12 like the sibling test class.
        // (The real 2.0 pipeline will select the OpenApi20 dialect vocabulary.)
        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(
            documentResolver, vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        // Hand-listed 2.0 schema positions (see remarks).
        string[] pointers =
        [
            "#/paths/~1pets/get/parameters/0",
            "#/paths/~1pets/parameters/0",
            "#/paths/~1pets/get/responses/200/schema",
            "#/paths/~1pets/get/responses/200/headers/X-Rate-Limit",
        ];

        Dictionary<string, string> parameterNames = new(StringComparer.Ordinal)
        {
            ["/paths/~1pets/get/parameters/0"] = "limit",
            ["/paths/~1pets/parameters/0"] = "tenant",
        };

        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (string pointer in pointers)
        {
            JsonReference reference = new(SpecPath, pointer);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
                rebaseAsRoot: false);

            pointerToType[pointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        CSharpLanguageProvider.Options options = new("TestApi");
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(new OpenApiSchemaNameHeuristic(parameterNames));

        typeBuilder.GenerateCodeUsing(languageProvider, typesToGenerate, CancellationToken.None);

        pointerToTypeName = new(StringComparer.Ordinal);
        foreach ((string pointer, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;
            if (reduced.HasDotnetTypeName())
            {
                pointerToTypeName[pointer] = reduced.DotnetTypeName()?.ToString() ?? string.Empty;
            }
        }
    }

    [TestMethod]
    public void OperationParameterObject_NamedFromParameterName()
    {
        Assert.AreEqual("GetPetsLimit", pointerToTypeName!["#/paths/~1pets/get/parameters/0"]);
    }

    [TestMethod]
    public void PathLevelParameterObject_NamedFromParameterName()
    {
        Assert.AreEqual("PetsTenant", pointerToTypeName!["#/paths/~1pets/parameters/0"]);
    }

    [TestMethod]
    public void ResponseSchemaWithoutContentMap_NamedFromStatusCode()
    {
        Assert.AreEqual("GetPetsOk", pointerToTypeName!["#/paths/~1pets/get/responses/200/schema"]);
    }

    [TestMethod]
    public void ResponseHeaderObject_NamedFromHeaderName()
    {
        Assert.AreEqual(
            "GetPetsOkXRateLimit",
            pointerToTypeName!["#/paths/~1pets/get/responses/200/headers/X-Rate-Limit"]);
    }
}