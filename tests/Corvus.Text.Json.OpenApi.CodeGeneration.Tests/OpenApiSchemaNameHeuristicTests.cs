// <copyright file="OpenApiSchemaNameHeuristicTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// Integration tests for <see cref="OpenApiSchemaNameHeuristic"/>.
/// Exercises the full codegen pipeline with the heuristic registered to verify
/// that inline schemas in OpenAPI specs receive contextual type names.
/// </summary>
[TestClass]
public class OpenApiSchemaNameHeuristicTests
{
    private static readonly string SpecPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "TestData", "naming-heuristic-3.1.json"));

    private static IReadOnlyCollection<GeneratedCodeFile>? generatedFiles;
    private static Dictionary<string, string>? pointerToTypeName;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver());

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(
            documentResolver, vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        string[] pointers = OpenApi31CodeGenerator.CollectSchemaPointers(
            LoadSpec(SpecPath), out _);

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
        languageProvider.RegisterNameHeuristics(OpenApiSchemaNameHeuristic.Instance);

        generatedFiles = typeBuilder.GenerateCodeUsing(
            languageProvider, typesToGenerate, CancellationToken.None);

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
    public void Response200_NamedOk()
    {
        AssertTypeName("#/paths/~1items/get/responses/200/content/application~1json/schema", "GetItemsOk");
    }

    [TestMethod]
    public void Response201_NamedCreated()
    {
        AssertTypeName("#/paths/~1items/get/responses/201/content/application~1json/schema", "GetItemsCreated");
    }

    [TestMethod]
    public void Response202_NamedAccepted()
    {
        AssertTypeName("#/paths/~1items/get/responses/202/content/application~1json/schema", "GetItemsAccepted");
    }

    [TestMethod]
    public void Response301_NamedMovedPermanently()
    {
        AssertTypeName("#/paths/~1items/get/responses/301/content/application~1json/schema", "GetItemsMovedPermanently");
    }

    [TestMethod]
    public void Response304_NamedNotModified()
    {
        AssertTypeName("#/paths/~1items/get/responses/304/content/application~1json/schema", "GetItemsNotModified");
    }

    [TestMethod]
    public void Response400_NamedBadRequest()
    {
        AssertTypeName("#/paths/~1items/get/responses/400/content/application~1json/schema", "GetItemsBadRequest");
    }

    [TestMethod]
    public void Response401_NamedUnauthorized()
    {
        AssertTypeName("#/paths/~1items/get/responses/401/content/application~1json/schema", "GetItemsUnauthorized");
    }

    [TestMethod]
    public void Response403_NamedForbidden()
    {
        AssertTypeName("#/paths/~1items/get/responses/403/content/application~1json/schema", "GetItemsForbidden");
    }

    [TestMethod]
    public void Response404_NamedNotFound()
    {
        AssertTypeName("#/paths/~1items/get/responses/404/content/application~1json/schema", "GetItemsNotFound");
    }

    [TestMethod]
    public void Response409_NamedConflict()
    {
        AssertTypeName("#/paths/~1items/get/responses/409/content/application~1json/schema", "GetItemsConflict");
    }

    [TestMethod]
    public void Response422_NamedUnprocessableEntity()
    {
        AssertTypeName("#/paths/~1items/get/responses/422/content/application~1json/schema", "GetItemsUnprocessableEntity");
    }

    [TestMethod]
    public void Response429_NamedTooManyRequests()
    {
        AssertTypeName("#/paths/~1items/get/responses/429/content/application~1json/schema", "GetItemsTooManyRequests");
    }

    [TestMethod]
    public void Response500_NamedInternalServerError()
    {
        AssertTypeName("#/paths/~1items/get/responses/500/content/application~1json/schema", "GetItemsInternalServerError");
    }

    [TestMethod]
    public void Response502_NamedBadGateway()
    {
        AssertTypeName("#/paths/~1items/get/responses/502/content/application~1json/schema", "GetItemsBadGateway");
    }

    [TestMethod]
    public void Response503_NamedServiceUnavailable()
    {
        AssertTypeName("#/paths/~1items/get/responses/503/content/application~1json/schema", "GetItemsServiceUnavailable");
    }

    [TestMethod]
    public void ResponseDefault_NamedDefault()
    {
        AssertTypeName("#/paths/~1items/get/responses/default/content/application~1json/schema", "GetItemsDefault");
    }

    [TestMethod]
    public void UnknownStatusCode_NamedStatusNNN()
    {
        AssertTypeName(
            "#/paths/~1items~1{itemId}~1sub-items~1{subId}/get/responses/418/content/application~1json/schema",
            "GetItemsByItemIdSubItemsBySubIdStatus418");
    }

    [TestMethod]
    public void PostMethod_NamedPost()
    {
        AssertTypeName("#/paths/~1items/post/responses/201/content/application~1json/schema", "PostItemsCreated");
    }

    [TestMethod]
    public void PutMethod_NamedPut()
    {
        AssertTypeName("#/paths/~1items/put/responses/200/content/application~1json/schema", "PutItemsOk");
    }

    [TestMethod]
    public void DeleteMethod_NamedDelete()
    {
        AssertTypeName("#/paths/~1items/delete/responses/200/content/application~1json/schema", "DeleteItemsOk");
    }

    [TestMethod]
    public void PatchMethod_NamedPatch()
    {
        AssertTypeName("#/paths/~1items/patch/responses/200/content/application~1json/schema", "PatchItemsOk");
    }

    [TestMethod]
    public void OptionsMethod_NamedOptions()
    {
        AssertTypeName("#/paths/~1items/options/responses/200/content/application~1json/schema", "OptionsItemsOk");
    }

    [TestMethod]
    public void HeadMethod_NamedHead()
    {
        AssertTypeName("#/paths/~1items/head/responses/200/content/application~1json/schema", "HeadItemsOk");
    }

    [TestMethod]
    public void TraceMethod_NamedTrace()
    {
        AssertTypeName("#/paths/~1items/trace/responses/200/content/application~1json/schema", "TraceItemsOk");
    }

    [TestMethod]
    public void RequestBody_NamedBody()
    {
        AssertTypeName("#/paths/~1items/post/requestBody/content/application~1json/schema", "PostItemsBody");
    }

    [TestMethod]
    public void OperationParameter_NamedParam()
    {
        AssertTypeName("#/paths/~1items/get/parameters/0/schema", "GetItemsParam0");
    }

    [TestMethod]
    public void PathLevelParameter_NamedParam()
    {
        AssertTypeName("#/paths/~1items/parameters/0/schema", "ItemsParam0");
    }

    [TestMethod]
    public void PathParameter_NamedByParam()
    {
        AssertTypeName(
            "#/paths/~1items~1{itemId}/get/responses/200/content/application~1json/schema",
            "GetItemsByItemIdOk");
    }

    [TestMethod]
    public void MultiSegmentPath_NamedWithAllSegments()
    {
        AssertTypeName(
            "#/paths/~1items~1{itemId}~1details/get/responses/200/content/application~1json/schema",
            "GetItemsByItemIdDetailsOk");
    }

    [TestMethod]
    public void MultiplePathParameters_NamedByAll()
    {
        AssertTypeName(
            "#/paths/~1items~1{itemId}~1sub-items~1{subId}/get/responses/200/content/application~1json/schema",
            "GetItemsByItemIdSubItemsBySubIdOk");
    }

    [TestMethod]
    public void Response204_NamedNoContent()
    {
        AssertTypeName(
            "#/paths/~1items~1{itemId}~1sub-items~1{subId}/get/responses/204/content/application~1json/schema",
            "GetItemsByItemIdSubItemsBySubIdNoContent");
    }

    [TestMethod]
    public void TildeEncodedPath_NamedCorrectly()
    {
        // Path /data~items is encoded as ~1data~0items in JSON pointer (~0 = ~)
        AssertTypeName(
            "#/paths/~1data~0items/get/responses/200/content/application~1json/schema",
            "GetDataItemsOk");
    }

    [TestMethod]
    public void ResponseHeader_NamedWithStatusAndHeader()
    {
        AssertTypeName("#/paths/~1items/get/responses/200/headers/x-next/schema", "GetItemsOkXNext");
    }

    [TestMethod]
    public void NonOpenApiFragment_ReturnsNoName()
    {
        JsonReferenceBuilder reference = JsonReferenceBuilder.From("#/components/schemas/Error");
        Span<char> buffer = stackalloc char[256];
        bool result = OpenApiSchemaNameHeuristic.Instance.TryGetName(
            null!, null!, reference, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void NoFragment_ReturnsNoName()
    {
        JsonReferenceBuilder reference = JsonReferenceBuilder.From("https://example.com/schema.json");
        Span<char> buffer = stackalloc char[256];
        bool result = OpenApiSchemaNameHeuristic.Instance.TryGetName(
            null!, null!, reference, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void IsOptional_ReturnsFalse()
    {
        Assert.IsFalse(OpenApiSchemaNameHeuristic.Instance.IsOptional);
    }

    [TestMethod]
    public void Priority_Returns500()
    {
        Assert.AreEqual(500u, OpenApiSchemaNameHeuristic.Instance.Priority);
    }

    [TestMethod]
    public void Instance_ReturnsSingleton()
    {
        Assert.AreSame(OpenApiSchemaNameHeuristic.Instance, OpenApiSchemaNameHeuristic.Instance);
    }

    [TestMethod]
    public void IncompletePathFragment_ReturnsNoName()
    {
        // Starts with /paths/ but has no method/responses — TryParseOpenApiFragment fails (lines 91-92)
        AssertDirectCallFails("#/paths/~1items/get");
    }

    [TestMethod]
    public void NoMethodNoParameters_ReturnsNoName()
    {
        // Has /paths/<url>/ but no HTTP method and no "parameters" segment (lines 159-160)
        AssertDirectCallFails("#/paths/~1items/unknown/responses/200/content/application~1json/schema");
    }

    [TestMethod]
    public void StatusCodeWithoutContent_ReturnsNoName()
    {
        // Response with status code but nothing after it (line 176-178)
        AssertDirectCallFails("#/paths/~1items/get/responses/204");
    }

    [TestMethod]
    public void HeaderNameWithoutTrailingSlash_ReturnsNoName()
    {
        // Header name with no /schema suffix (lines 189-191)
        AssertDirectCallFails("#/paths/~1items/get/responses/200/headers/x-next");
    }

    [TestMethod]
    public void ParameterIndexWithoutTrailingSlash_ReturnsNoName()
    {
        // Parameter index with no /schema suffix (lines 215-217)
        AssertDirectCallFails("#/paths/~1items/get/parameters/0");
    }

    [TestMethod]
    public void UnrecognizedAfterMethod_ReturnsNoName()
    {
        // After method, neither responses/ nor requestBody/ nor parameters/ (line 225)
        AssertDirectCallFails("#/paths/~1items/get/callbacks/myCallback/schema");
    }

    [TestMethod]
    public void MethodSegmentWithoutSlash_ReturnsNoName()
    {
        // HTTP method found but no slash after it (lines 146-147)
        AssertDirectCallFails("#/paths/~1items/get");
    }

    [TestMethod]
    public void ContentWithoutMediaType_ReturnsNoName()
    {
        // content/ path with no media type slash — malformed (lines 240-241)
        AssertDirectCallFails("#/paths/~1items/get/responses/200/content");
    }

    [TestMethod]
    public void SubschemaPath_ReturnsNoName()
    {
        // Child schema like /schema/properties/id — not root, heuristic should not match
        AssertDirectCallFails("#/paths/~1items/get/responses/200/content/application~1json/schema/properties/id");
    }

    private static void AssertDirectCallFails(string fragmentUri)
    {
        JsonReferenceBuilder reference = JsonReferenceBuilder.From(fragmentUri);
        Span<char> buffer = stackalloc char[256];
        bool result = OpenApiSchemaNameHeuristic.Instance.TryGetName(
            null!, null!, reference, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    private static JsonElement LoadSpec(string path)
    {
        string json = File.ReadAllText(path);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }

    private static void AssertTypeName(string pointer, string expectedName)
    {
        Assert.IsNotNull(pointerToTypeName, "ClassInitialize did not run — generated files are null");

        if (!pointerToTypeName.TryGetValue(pointer, out string? actualName))
        {
            Assert.Fail($"No type declaration found for pointer: {pointer}. " +
                $"Available pointers: {string.Join(", ", pointerToTypeName.Keys.OrderBy(k => k))}");
        }

        Assert.AreEqual(expectedName, actualName,
            $"Type name mismatch for pointer: {pointer}");
    }
}