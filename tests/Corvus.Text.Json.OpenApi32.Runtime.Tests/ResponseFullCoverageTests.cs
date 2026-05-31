// <copyright file="ResponseFullCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Client;
using CanonTests32.Client.Models;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

[TestClass]
public class ResponseFullCoverageTests
{
    private const string ApplicationJsonContentType = "application/json";
    private const string VendorJsonContentType = "application/vnd.api+json";
    private const string EmptyObjectBody = "{}";
    private const string InvalidBody = "42";
    private const string GenericObjectBody = """{"key":"value"}""";
    private const string ReceivedBody = """{"received":true}""";
    private const string UploadedBody = """{"uploaded":true}""";
    private const string ProcessBatchBody = """{"processed":1,"failed":0}""";
    private const string PatchItemBody = """{"id":"x","name":"t"}""";
    private const string TextToJsonBody = """{"parsed":"hello"}""";
    private const string OrderBody = """{"orderId":"123e4567-e89b-12d3-a456-426614174000","status":"shipped","total":1}""";
    private const string SearchWithQuerystringBody = """{"count":0,"results":[]}""";
    private const string VendorJsonBody = """{"data":"test"}""";
    private const string UploadFileBody = """{"id":"x"}""";
    private const string UploadDocMixedBody = """{"documentId":"doc-1","url":"https://example.com/doc-1"}""";

    private delegate ValueTask<TResponse> CreateResponse<TResponse>(int statusCode, Stream contentStream, string? contentType)
        where TResponse : IAsyncDisposable;

    private delegate bool TryGetBody<TResponse, TBody>(TResponse response, out TBody body);

    private delegate bool IsSuccess<TResponse>(TResponse response);

    private delegate string MatchResponse<TResponse>(TResponse response);

    private delegate string MatchResponseWithContext<TResponse>(TResponse response, int context);

    private delegate void ValidateResponse<TResponse>(TResponse response, ValidationMode mode);

    private static MemoryStream MakeStream(string json) => new(System.Text.Encoding.UTF8.GetBytes(json));

    private static async Task AssertTryGetSuccessReturnsTrue<TResponse, TBody>(
        int successStatusCode,
        string validBody,
        CreateResponse<TResponse> create,
        TryGetBody<TResponse, TBody> tryGet,
        string contentType)
        where TResponse : IAsyncDisposable
        where TBody : struct
    {
        TResponse response = await create(successStatusCode, MakeStream(validBody), contentType);
        try
        {
            Assert.IsTrue(tryGet(response, out TBody body));
            Assert.AreEqual(JsonValueKind.Object, ((dynamic)body).ValueKind);
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    private static async Task AssertTryGetSuccessReturnsFalse<TResponse, TBody>(
        CreateResponse<TResponse> create,
        TryGetBody<TResponse, TBody> tryGet,
        string contentType)
        where TResponse : IAsyncDisposable
        where TBody : struct
    {
        TResponse response = await create(404, MakeStream(EmptyObjectBody), contentType);
        try
        {
            Assert.IsFalse(tryGet(response, out _));
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    private static async Task AssertIsSuccess<TResponse>(
        int statusCode,
        string body,
        CreateResponse<TResponse> create,
        IsSuccess<TResponse> isSuccess,
        bool expected,
        string contentType)
        where TResponse : IAsyncDisposable
    {
        TResponse response = await create(statusCode, MakeStream(body), contentType);
        try
        {
            Assert.AreEqual(expected, isSuccess(response));
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    private static async Task AssertMatchResult<TResponse>(
        int statusCode,
        string body,
        CreateResponse<TResponse> create,
        MatchResponse<TResponse> match,
        string expected,
        string contentType)
        where TResponse : IAsyncDisposable
    {
        TResponse response = await create(statusCode, MakeStream(body), contentType);
        try
        {
            Assert.AreEqual(expected, match(response));
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    private static async Task AssertMatchResultWithContext<TResponse>(
        int statusCode,
        string body,
        CreateResponse<TResponse> create,
        MatchResponseWithContext<TResponse> match,
        int context,
        string expected,
        string contentType)
        where TResponse : IAsyncDisposable
    {
        TResponse response = await create(statusCode, MakeStream(body), contentType);
        try
        {
            Assert.AreEqual(expected, match(response, context));
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    private static async Task AssertValidateDoesNotThrow<TResponse>(
        int statusCode,
        string body,
        CreateResponse<TResponse> create,
        ValidateResponse<TResponse> validate,
        ValidationMode mode,
        string contentType)
        where TResponse : IAsyncDisposable
    {
        TResponse response = await create(statusCode, MakeStream(body), contentType);
        try
        {
            validate(response, mode);
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    private static async Task AssertValidateThrows<TResponse>(
        int statusCode,
        CreateResponse<TResponse> create,
        ValidateResponse<TResponse> validate,
        ValidationMode mode,
        string contentType)
        where TResponse : IAsyncDisposable
    {
        TResponse response = await create(statusCode, MakeStream(InvalidBody), contentType);
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => validate(response, mode));
        }
        finally
        {
            await response.DisposeAsync();
        }
    }

    // ── QueryArrayNonexplode ──
    [TestMethod]
    public Task QueryArrayNonexplode_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<QueryArrayNonexplodeResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<QueryArrayNonexplodeResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_IsSuccess_True() =>
        AssertIsSuccess<QueryArrayNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_IsSuccess_False() =>
        AssertIsSuccess<QueryArrayNonexplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_MatchResult_Ok() =>
        AssertMatchResult<QueryArrayNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_MatchResult_Default() =>
        AssertMatchResult<QueryArrayNonexplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<QueryArrayNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_MatchResultContext_Default() =>
        AssertMatchResultWithContext<QueryArrayNonexplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<QueryArrayNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<QueryArrayNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArrayNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayNonexplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArrayNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QueryArrayPipe ──
    [TestMethod]
    public Task QueryArrayPipe_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<QueryArrayPipeResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<QueryArrayPipeResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_IsSuccess_True() =>
        AssertIsSuccess<QueryArrayPipeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_IsSuccess_False() =>
        AssertIsSuccess<QueryArrayPipeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_MatchResult_Ok() =>
        AssertMatchResult<QueryArrayPipeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_MatchResult_Default() =>
        AssertMatchResult<QueryArrayPipeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<QueryArrayPipeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_MatchResultContext_Default() =>
        AssertMatchResultWithContext<QueryArrayPipeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<QueryArrayPipeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<QueryArrayPipeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArrayPipeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayPipe_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArrayPipeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayPipeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QueryArraySpace ──
    [TestMethod]
    public Task QueryArraySpace_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<QueryArraySpaceResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<QueryArraySpaceResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_IsSuccess_True() =>
        AssertIsSuccess<QueryArraySpaceResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_IsSuccess_False() =>
        AssertIsSuccess<QueryArraySpaceResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_MatchResult_Ok() =>
        AssertMatchResult<QueryArraySpaceResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_MatchResult_Default() =>
        AssertMatchResult<QueryArraySpaceResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<QueryArraySpaceResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_MatchResultContext_Default() =>
        AssertMatchResultWithContext<QueryArraySpaceResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<QueryArraySpaceResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<QueryArraySpaceResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArraySpaceResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArraySpace_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArraySpaceResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArraySpaceResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QueryObjectDeep ──
    [TestMethod]
    public Task QueryObjectDeep_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<QueryObjectDeepResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<QueryObjectDeepResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_IsSuccess_True() =>
        AssertIsSuccess<QueryObjectDeepResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_IsSuccess_False() =>
        AssertIsSuccess<QueryObjectDeepResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_MatchResult_Ok() =>
        AssertMatchResult<QueryObjectDeepResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_MatchResult_Default() =>
        AssertMatchResult<QueryObjectDeepResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<QueryObjectDeepResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_MatchResultContext_Default() =>
        AssertMatchResultWithContext<QueryObjectDeepResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<QueryObjectDeepResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<QueryObjectDeepResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryObjectDeepResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectDeep_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryObjectDeepResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectDeepResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QueryObjectExplode ──
    [TestMethod]
    public Task QueryObjectExplode_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<QueryObjectExplodeResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<QueryObjectExplodeResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_IsSuccess_True() =>
        AssertIsSuccess<QueryObjectExplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_IsSuccess_False() =>
        AssertIsSuccess<QueryObjectExplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_MatchResult_Ok() =>
        AssertMatchResult<QueryObjectExplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_MatchResult_Default() =>
        AssertMatchResult<QueryObjectExplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<QueryObjectExplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_MatchResultContext_Default() =>
        AssertMatchResultWithContext<QueryObjectExplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<QueryObjectExplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<QueryObjectExplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryObjectExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectExplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryObjectExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QueryObjectNonexplode ──
    [TestMethod]
    public Task QueryObjectNonexplode_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<QueryObjectNonexplodeResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<QueryObjectNonexplodeResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_IsSuccess_True() =>
        AssertIsSuccess<QueryObjectNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_IsSuccess_False() =>
        AssertIsSuccess<QueryObjectNonexplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_MatchResult_Ok() =>
        AssertMatchResult<QueryObjectNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_MatchResult_Default() =>
        AssertMatchResult<QueryObjectNonexplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<QueryObjectNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_MatchResultContext_Default() =>
        AssertMatchResultWithContext<QueryObjectNonexplodeResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<QueryObjectNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<QueryObjectNonexplodeResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryObjectNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryObjectNonexplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryObjectNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryObjectNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── Search ──
    [TestMethod]
    public Task Search_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SearchResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SearchResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_IsSuccess_True() =>
        AssertIsSuccess<SearchResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_IsSuccess_False() =>
        AssertIsSuccess<SearchResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_MatchResult_Ok() =>
        AssertMatchResult<SearchResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_MatchResult_Default() =>
        AssertMatchResult<SearchResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SearchResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SearchResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SearchResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SearchResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SearchResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task Search_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SearchResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetPage ──
    [TestMethod]
    public Task GetPage_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<GetPageResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<GetPageResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_IsSuccess_True() =>
        AssertIsSuccess<GetPageResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_IsSuccess_False() =>
        AssertIsSuccess<GetPageResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_MatchResult_Ok() =>
        AssertMatchResult<GetPageResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_MatchResult_Default() =>
        AssertMatchResult<GetPageResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<GetPageResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_MatchResultContext_Default() =>
        AssertMatchResultWithContext<GetPageResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<GetPageResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<GetPageResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetPageResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPage_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetPageResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPageResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetItemDetails ──
    [TestMethod]
    public Task GetItemDetails_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<GetItemDetailsResponse, JsonObject>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<GetItemDetailsResponse, JsonObject>(
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, out JsonObject body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_IsSuccess_True() =>
        AssertIsSuccess<GetItemDetailsResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_IsSuccess_False() =>
        AssertIsSuccess<GetItemDetailsResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_MatchResult_Ok() =>
        AssertMatchResult<GetItemDetailsResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_MatchResult_Default() =>
        AssertMatchResult<GetItemDetailsResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response) => response.MatchResult(static (JsonObject _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<GetItemDetailsResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_MatchResultContext_Default() =>
        AssertMatchResultWithContext<GetItemDetailsResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, int context) => response.MatchResult(context, static (JsonObject _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<GetItemDetailsResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<GetItemDetailsResponse>(
            200,
            GenericObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetItemDetailsResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetItemDetails_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetItemDetailsResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetItemDetailsResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── SubmitContactForm ──
    [TestMethod]
    public Task SubmitContactForm_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SubmitContactFormResponse, PostFormsContactOk>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, out PostFormsContactOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SubmitContactFormResponse, PostFormsContactOk>(
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, out PostFormsContactOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_IsSuccess_True() =>
        AssertIsSuccess<SubmitContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_IsSuccess_False() =>
        AssertIsSuccess<SubmitContactFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_MatchResult_Ok() =>
        AssertMatchResult<SubmitContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response) => response.MatchResult(static (PostFormsContactOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_MatchResult_Default() =>
        AssertMatchResult<SubmitContactFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response) => response.MatchResult(static (PostFormsContactOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SubmitContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, int context) => response.MatchResult(context, static (PostFormsContactOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SubmitContactFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, int context) => response.MatchResult(context, static (PostFormsContactOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitContactFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitContactForm_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitContactFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── SubmitEncodedContactForm ──
    [TestMethod]
    public Task SubmitEncodedContactForm_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SubmitEncodedContactFormResponse, PostFormsEncodedContactOk>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, out PostFormsEncodedContactOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SubmitEncodedContactFormResponse, PostFormsEncodedContactOk>(
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, out PostFormsEncodedContactOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_IsSuccess_True() =>
        AssertIsSuccess<SubmitEncodedContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_IsSuccess_False() =>
        AssertIsSuccess<SubmitEncodedContactFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_MatchResult_Ok() =>
        AssertMatchResult<SubmitEncodedContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response) => response.MatchResult(static (PostFormsEncodedContactOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_MatchResult_Default() =>
        AssertMatchResult<SubmitEncodedContactFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response) => response.MatchResult(static (PostFormsEncodedContactOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SubmitEncodedContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, int context) => response.MatchResult(context, static (PostFormsEncodedContactOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SubmitEncodedContactFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, int context) => response.MatchResult(context, static (PostFormsEncodedContactOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitEncodedContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitEncodedContactFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitEncodedContactFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitEncodedContactForm_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitEncodedContactFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitEncodedContactFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── SubmitMultipartTypes ──
    [TestMethod]
    public Task SubmitMultipartTypes_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SubmitMultipartTypesResponse, PostFormsMultipartTypesOk>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, out PostFormsMultipartTypesOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SubmitMultipartTypesResponse, PostFormsMultipartTypesOk>(
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, out PostFormsMultipartTypesOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_IsSuccess_True() =>
        AssertIsSuccess<SubmitMultipartTypesResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_IsSuccess_False() =>
        AssertIsSuccess<SubmitMultipartTypesResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_MatchResult_Ok() =>
        AssertMatchResult<SubmitMultipartTypesResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response) => response.MatchResult(static (PostFormsMultipartTypesOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_MatchResult_Default() =>
        AssertMatchResult<SubmitMultipartTypesResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response) => response.MatchResult(static (PostFormsMultipartTypesOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SubmitMultipartTypesResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, int context) => response.MatchResult(context, static (PostFormsMultipartTypesOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SubmitMultipartTypesResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, int context) => response.MatchResult(context, static (PostFormsMultipartTypesOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitMultipartTypesResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitMultipartTypesResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitMultipartTypesResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitMultipartTypes_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitMultipartTypesResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitMultipartTypesResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── SubmitNonexplodedForm ──
    [TestMethod]
    public Task SubmitNonexplodedForm_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SubmitNonexplodedFormResponse, PostFormsNonexplodedFormOk>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, out PostFormsNonexplodedFormOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SubmitNonexplodedFormResponse, PostFormsNonexplodedFormOk>(
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, out PostFormsNonexplodedFormOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_IsSuccess_True() =>
        AssertIsSuccess<SubmitNonexplodedFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_IsSuccess_False() =>
        AssertIsSuccess<SubmitNonexplodedFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_MatchResult_Ok() =>
        AssertMatchResult<SubmitNonexplodedFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response) => response.MatchResult(static (PostFormsNonexplodedFormOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_MatchResult_Default() =>
        AssertMatchResult<SubmitNonexplodedFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response) => response.MatchResult(static (PostFormsNonexplodedFormOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SubmitNonexplodedFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, int context) => response.MatchResult(context, static (PostFormsNonexplodedFormOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SubmitNonexplodedFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, int context) => response.MatchResult(context, static (PostFormsNonexplodedFormOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitNonexplodedFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitNonexplodedFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitNonexplodedFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitNonexplodedForm_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitNonexplodedFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitNonexplodedFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── UploadDocument ──
    [TestMethod]
    public Task UploadDocument_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<UploadDocumentResponse, PostFormsUploadOk>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, out PostFormsUploadOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<UploadDocumentResponse, PostFormsUploadOk>(
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, out PostFormsUploadOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_IsSuccess_True() =>
        AssertIsSuccess<UploadDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_IsSuccess_False() =>
        AssertIsSuccess<UploadDocumentResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_MatchResult_Ok() =>
        AssertMatchResult<UploadDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response) => response.MatchResult(static (PostFormsUploadOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_MatchResult_Default() =>
        AssertMatchResult<UploadDocumentResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response) => response.MatchResult(static (PostFormsUploadOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<UploadDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, int context) => response.MatchResult(context, static (PostFormsUploadOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_MatchResultContext_Default() =>
        AssertMatchResultWithContext<UploadDocumentResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, int context) => response.MatchResult(context, static (PostFormsUploadOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<UploadDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<UploadDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<UploadDocumentResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocument_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<UploadDocumentResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── UploadEncodedDocument ──
    [TestMethod]
    public Task UploadEncodedDocument_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<UploadEncodedDocumentResponse, PostFormsEncodedUploadOk>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, out PostFormsEncodedUploadOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<UploadEncodedDocumentResponse, PostFormsEncodedUploadOk>(
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, out PostFormsEncodedUploadOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_IsSuccess_True() =>
        AssertIsSuccess<UploadEncodedDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_IsSuccess_False() =>
        AssertIsSuccess<UploadEncodedDocumentResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_MatchResult_Ok() =>
        AssertMatchResult<UploadEncodedDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response) => response.MatchResult(static (PostFormsEncodedUploadOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_MatchResult_Default() =>
        AssertMatchResult<UploadEncodedDocumentResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response) => response.MatchResult(static (PostFormsEncodedUploadOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<UploadEncodedDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, int context) => response.MatchResult(context, static (PostFormsEncodedUploadOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_MatchResultContext_Default() =>
        AssertMatchResultWithContext<UploadEncodedDocumentResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, int context) => response.MatchResult(context, static (PostFormsEncodedUploadOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<UploadEncodedDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<UploadEncodedDocumentResponse>(
            200,
            UploadedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<UploadEncodedDocumentResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadEncodedDocument_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<UploadEncodedDocumentResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadEncodedDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── SubmitDefaultForm ──
    [TestMethod]
    public Task SubmitDefaultForm_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SubmitDefaultFormResponse, PostFormsDefaultFormOk>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, out PostFormsDefaultFormOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SubmitDefaultFormResponse, PostFormsDefaultFormOk>(
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, out PostFormsDefaultFormOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_IsSuccess_True() =>
        AssertIsSuccess<SubmitDefaultFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_IsSuccess_False() =>
        AssertIsSuccess<SubmitDefaultFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_MatchResult_Ok() =>
        AssertMatchResult<SubmitDefaultFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response) => response.MatchResult(static (PostFormsDefaultFormOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_MatchResult_Default() =>
        AssertMatchResult<SubmitDefaultFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response) => response.MatchResult(static (PostFormsDefaultFormOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SubmitDefaultFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, int context) => response.MatchResult(context, static (PostFormsDefaultFormOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SubmitDefaultFormResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, int context) => response.MatchResult(context, static (PostFormsDefaultFormOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitDefaultFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SubmitDefaultFormResponse>(
            200,
            ReceivedBody,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitDefaultFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SubmitDefaultForm_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SubmitDefaultFormResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SubmitDefaultFormResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── ProcessBatch ──
    [TestMethod]
    public Task ProcessBatch_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<ProcessBatchResponse, PostDocsBatchProcessOk>(
            200,
            ProcessBatchBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, out PostDocsBatchProcessOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<ProcessBatchResponse, PostDocsBatchProcessOk>(
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, out PostDocsBatchProcessOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_IsSuccess_True() =>
        AssertIsSuccess<ProcessBatchResponse>(
            200,
            ProcessBatchBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_IsSuccess_False() =>
        AssertIsSuccess<ProcessBatchResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_MatchResult_Ok() =>
        AssertMatchResult<ProcessBatchResponse>(
            200,
            ProcessBatchBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response) => response.MatchResult(static (PostDocsBatchProcessOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_MatchResult_Default() =>
        AssertMatchResult<ProcessBatchResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response) => response.MatchResult(static (PostDocsBatchProcessOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<ProcessBatchResponse>(
            200,
            ProcessBatchBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, int context) => response.MatchResult(context, static (PostDocsBatchProcessOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_MatchResultContext_Default() =>
        AssertMatchResultWithContext<ProcessBatchResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, int context) => response.MatchResult(context, static (PostDocsBatchProcessOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<ProcessBatchResponse>(
            200,
            ProcessBatchBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<ProcessBatchResponse>(
            200,
            ProcessBatchBody,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<ProcessBatchResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task ProcessBatch_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<ProcessBatchResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (ProcessBatchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PatchItem ──
    [TestMethod]
    public Task PatchItem_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<PatchItemResponse, PatchItemsByItemIdOk>(
            200,
            PatchItemBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, out PatchItemsByItemIdOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<PatchItemResponse, PatchItemsByItemIdOk>(
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, out PatchItemsByItemIdOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_IsSuccess_True() =>
        AssertIsSuccess<PatchItemResponse>(
            200,
            PatchItemBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_IsSuccess_False() =>
        AssertIsSuccess<PatchItemResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_MatchResult_Ok() =>
        AssertMatchResult<PatchItemResponse>(
            200,
            PatchItemBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response) => response.MatchResult(static (PatchItemsByItemIdOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_MatchResult_Default() =>
        AssertMatchResult<PatchItemResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response) => response.MatchResult(static (PatchItemsByItemIdOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<PatchItemResponse>(
            200,
            PatchItemBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, int context) => response.MatchResult(context, static (PatchItemsByItemIdOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_MatchResultContext_Default() =>
        AssertMatchResultWithContext<PatchItemResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, int context) => response.MatchResult(context, static (PatchItemsByItemIdOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<PatchItemResponse>(
            200,
            PatchItemBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<PatchItemResponse>(
            200,
            PatchItemBody,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PatchItemResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PatchItem_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PatchItemResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PatchItemResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── TextToJson ──
    [TestMethod]
    public Task TextToJson_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<TextToJsonResponse, PostTextToJsonOk>(
            200,
            TextToJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, out PostTextToJsonOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<TextToJsonResponse, PostTextToJsonOk>(
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, out PostTextToJsonOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_IsSuccess_True() =>
        AssertIsSuccess<TextToJsonResponse>(
            200,
            TextToJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_IsSuccess_False() =>
        AssertIsSuccess<TextToJsonResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_MatchResult_Ok() =>
        AssertMatchResult<TextToJsonResponse>(
            200,
            TextToJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response) => response.MatchResult(static (PostTextToJsonOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_MatchResult_Default() =>
        AssertMatchResult<TextToJsonResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response) => response.MatchResult(static (PostTextToJsonOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<TextToJsonResponse>(
            200,
            TextToJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, int context) => response.MatchResult(context, static (PostTextToJsonOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_MatchResultContext_Default() =>
        AssertMatchResultWithContext<TextToJsonResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, int context) => response.MatchResult(context, static (PostTextToJsonOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<TextToJsonResponse>(
            200,
            TextToJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<TextToJsonResponse>(
            200,
            TextToJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<TextToJsonResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task TextToJson_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<TextToJsonResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (TextToJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── UpdateOrder ──
    [TestMethod]
    public Task UpdateOrder_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<UpdateOrderResponse, PutOrdersByOrderIdOk>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, out PutOrdersByOrderIdOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<UpdateOrderResponse, PutOrdersByOrderIdOk>(
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, out PutOrdersByOrderIdOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_IsSuccess_True() =>
        AssertIsSuccess<UpdateOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_IsSuccess_False() =>
        AssertIsSuccess<UpdateOrderResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_MatchResult_Ok() =>
        AssertMatchResult<UpdateOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response) => response.MatchResult(static (PutOrdersByOrderIdOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_MatchResult_Default() =>
        AssertMatchResult<UpdateOrderResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response) => response.MatchResult(static (PutOrdersByOrderIdOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<UpdateOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, int context) => response.MatchResult(context, static (PutOrdersByOrderIdOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_MatchResultContext_Default() =>
        AssertMatchResultWithContext<UpdateOrderResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, int context) => response.MatchResult(context, static (PutOrdersByOrderIdOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<UpdateOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<UpdateOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<UpdateOrderResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UpdateOrder_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<UpdateOrderResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UpdateOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── SearchWithQuerystring ──
    [TestMethod]
    public Task SearchWithQuerystring_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<SearchWithQuerystringResponse, GetSearchWithQuerystringOk>(
            200,
            SearchWithQuerystringBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, out GetSearchWithQuerystringOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<SearchWithQuerystringResponse, GetSearchWithQuerystringOk>(
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, out GetSearchWithQuerystringOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_IsSuccess_True() =>
        AssertIsSuccess<SearchWithQuerystringResponse>(
            200,
            SearchWithQuerystringBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_IsSuccess_False() =>
        AssertIsSuccess<SearchWithQuerystringResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_MatchResult_Ok() =>
        AssertMatchResult<SearchWithQuerystringResponse>(
            200,
            SearchWithQuerystringBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response) => response.MatchResult(static (GetSearchWithQuerystringOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_MatchResult_Default() =>
        AssertMatchResult<SearchWithQuerystringResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response) => response.MatchResult(static (GetSearchWithQuerystringOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<SearchWithQuerystringResponse>(
            200,
            SearchWithQuerystringBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, int context) => response.MatchResult(context, static (GetSearchWithQuerystringOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_MatchResultContext_Default() =>
        AssertMatchResultWithContext<SearchWithQuerystringResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, int context) => response.MatchResult(context, static (GetSearchWithQuerystringOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<SearchWithQuerystringResponse>(
            200,
            SearchWithQuerystringBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<SearchWithQuerystringResponse>(
            200,
            SearchWithQuerystringBody,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<SearchWithQuerystringResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task SearchWithQuerystring_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<SearchWithQuerystringResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType),
            static (SearchWithQuerystringResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetOrder ──
    [TestMethod]
    public Task GetOrder_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<GetOrderResponse, GetOrdersByOrderIdOk>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, out GetOrdersByOrderIdOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<GetOrderResponse, GetOrdersByOrderIdOk>(
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, out GetOrdersByOrderIdOk body) => response.TryGetOk(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_IsSuccess_True() =>
        AssertIsSuccess<GetOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_IsSuccess_False() =>
        AssertIsSuccess<GetOrderResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_MatchResult_Ok() =>
        AssertMatchResult<GetOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response) => response.MatchResult(static (GetOrdersByOrderIdOk _) => "ok", static (int _) => "default"),
            "ok",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_MatchResult_Default() =>
        AssertMatchResult<GetOrderResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response) => response.MatchResult(static (GetOrdersByOrderIdOk _) => "ok", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<GetOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, int context) => response.MatchResult(context, static (GetOrdersByOrderIdOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_MatchResultContext_Default() =>
        AssertMatchResultWithContext<GetOrderResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, int context) => response.MatchResult(context, static (GetOrdersByOrderIdOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<GetOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<GetOrderResponse>(
            200,
            OrderBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetOrderResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetOrder_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetOrderResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetOrderResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetVendorJson ──
    [TestMethod]
    public Task GetVendorJson_TryGetOk_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<GetVendorJsonResponse, GetFilesVendorJsonOk>(
            200,
            VendorJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, out GetFilesVendorJsonOk body) => response.TryGetOk(out body),
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_TryGetOk_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<GetVendorJsonResponse, GetFilesVendorJsonOk>(
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, out GetFilesVendorJsonOk body) => response.TryGetOk(out body),
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_IsSuccess_True() =>
        AssertIsSuccess<GetVendorJsonResponse>(
            200,
            VendorJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response) => response.IsSuccess,
            true,
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_IsSuccess_False() =>
        AssertIsSuccess<GetVendorJsonResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response) => response.IsSuccess,
            false,
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_MatchResult_Ok() =>
        AssertMatchResult<GetVendorJsonResponse>(
            200,
            VendorJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response) => response.MatchResult(static (GetFilesVendorJsonOk _) => "ok", static (int _) => "default"),
            "ok",
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_MatchResult_Default() =>
        AssertMatchResult<GetVendorJsonResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response) => response.MatchResult(static (GetFilesVendorJsonOk _) => "ok", static (int _) => "default"),
            "default",
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_MatchResultContext_Ok() =>
        AssertMatchResultWithContext<GetVendorJsonResponse>(
            200,
            VendorJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, int context) => response.MatchResult(context, static (GetFilesVendorJsonOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "ok-42",
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_MatchResultContext_Default() =>
        AssertMatchResultWithContext<GetVendorJsonResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, int context) => response.MatchResult(context, static (GetFilesVendorJsonOk _, in int ctx) => $"ok-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<GetVendorJsonResponse>(
            200,
            VendorJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<GetVendorJsonResponse>(
            200,
            VendorJsonBody,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetVendorJsonResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            VendorJsonContentType);

    [TestMethod]
    public Task GetVendorJson_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetVendorJsonResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetVendorJsonResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            VendorJsonContentType);

    // ── UploadDocMixed ──
    [TestMethod]
    public Task UploadDocMixed_TryGetCreated_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<UploadDocMixedResponse, PostDocsUploadMixedCreated>(
            201,
            UploadDocMixedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, out PostDocsUploadMixedCreated body) => response.TryGetCreated(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_TryGetCreated_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<UploadDocMixedResponse, PostDocsUploadMixedCreated>(
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, out PostDocsUploadMixedCreated body) => response.TryGetCreated(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_IsSuccess_True() =>
        AssertIsSuccess<UploadDocMixedResponse>(
            201,
            UploadDocMixedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_IsSuccess_False() =>
        AssertIsSuccess<UploadDocMixedResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_MatchResult_Created() =>
        AssertMatchResult<UploadDocMixedResponse>(
            201,
            UploadDocMixedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response) => response.MatchResult(static (PostDocsUploadMixedCreated _) => "created", static (int _) => "default"),
            "created",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_MatchResult_Default() =>
        AssertMatchResult<UploadDocMixedResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response) => response.MatchResult(static (PostDocsUploadMixedCreated _) => "created", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_MatchResultContext_Created() =>
        AssertMatchResultWithContext<UploadDocMixedResponse>(
            201,
            UploadDocMixedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, int context) => response.MatchResult(context, static (PostDocsUploadMixedCreated _, in int ctx) => $"created-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "created-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_MatchResultContext_Default() =>
        AssertMatchResultWithContext<UploadDocMixedResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, int context) => response.MatchResult(context, static (PostDocsUploadMixedCreated _, in int ctx) => $"created-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<UploadDocMixedResponse>(
            201,
            UploadDocMixedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<UploadDocMixedResponse>(
            201,
            UploadDocMixedBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<UploadDocMixedResponse>(
            201,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadDocMixed_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<UploadDocMixedResponse>(
            201,
            static (int statusCode, Stream contentStream, string? contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadDocMixedResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── UploadFile ──
    [TestMethod]
    public Task UploadFile_TryGetCreated_ReturnsTrue() =>
        AssertTryGetSuccessReturnsTrue<UploadFileResponse, PostFilesUploadCreated>(
            201,
            UploadFileBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, out PostFilesUploadCreated body) => response.TryGetCreated(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_TryGetCreated_ReturnsFalse() =>
        AssertTryGetSuccessReturnsFalse<UploadFileResponse, PostFilesUploadCreated>(
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, out PostFilesUploadCreated body) => response.TryGetCreated(out body),
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_IsSuccess_True() =>
        AssertIsSuccess<UploadFileResponse>(
            201,
            UploadFileBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response) => response.IsSuccess,
            true,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_IsSuccess_False() =>
        AssertIsSuccess<UploadFileResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response) => response.IsSuccess,
            false,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_MatchResult_Created() =>
        AssertMatchResult<UploadFileResponse>(
            201,
            UploadFileBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response) => response.MatchResult(static (PostFilesUploadCreated _) => "created", static (int _) => "default"),
            "created",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_MatchResult_Default() =>
        AssertMatchResult<UploadFileResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response) => response.MatchResult(static (PostFilesUploadCreated _) => "created", static (int _) => "default"),
            "default",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_MatchResultContext_Created() =>
        AssertMatchResultWithContext<UploadFileResponse>(
            201,
            UploadFileBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, int context) => response.MatchResult(context, static (PostFilesUploadCreated _, in int ctx) => $"created-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "created-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_MatchResultContext_Default() =>
        AssertMatchResultWithContext<UploadFileResponse>(
            404,
            EmptyObjectBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, int context) => response.MatchResult(context, static (PostFilesUploadCreated _, in int ctx) => $"created-{ctx}", static (int _, in int ctx) => $"default-{ctx}"),
            42,
            "default-42",
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_Validate_Detailed_ValidBody() =>
        AssertValidateDoesNotThrow<UploadFileResponse>(
            201,
            UploadFileBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_Validate_Basic_ValidBody() =>
        AssertValidateDoesNotThrow<UploadFileResponse>(
            201,
            UploadFileBody,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<UploadFileResponse>(
            201,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task UploadFile_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<UploadFileResponse>(
            201,
            static (int statusCode, Stream contentStream, string? contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (UploadFileResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── CookieArray ──
    [TestMethod]
    public Task CookieArray_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<CookieArrayResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieArrayResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieArrayResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task CookieArray_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<CookieArrayResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieArrayResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieArrayResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── CookieArrayNonexplode ──
    [TestMethod]
    public Task CookieArrayNonexplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<CookieArrayNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieArrayNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task CookieArrayNonexplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<CookieArrayNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieArrayNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── CookieObject ──
    [TestMethod]
    public Task CookieObject_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<CookieObjectResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieObjectResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieObjectResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task CookieObject_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<CookieObjectResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieObjectResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieObjectResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── CookieObjectNonexplode ──
    [TestMethod]
    public Task CookieObjectNonexplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<CookieObjectNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieObjectNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task CookieObjectNonexplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<CookieObjectNonexplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => CookieObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (CookieObjectNonexplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── HeaderArray ──
    [TestMethod]
    public Task HeaderArray_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<HeaderArrayResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => HeaderArrayResponse.CreateAsync(statusCode, contentStream, contentType),
            static (HeaderArrayResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task HeaderArray_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<HeaderArrayResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => HeaderArrayResponse.CreateAsync(statusCode, contentStream, contentType),
            static (HeaderArrayResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── HeaderObject ──
    [TestMethod]
    public Task HeaderObject_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<HeaderObjectResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => HeaderObjectResponse.CreateAsync(statusCode, contentStream, contentType),
            static (HeaderObjectResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task HeaderObject_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<HeaderObjectResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => HeaderObjectResponse.CreateAsync(statusCode, contentStream, contentType),
            static (HeaderObjectResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── HeaderObjectExplode ──
    [TestMethod]
    public Task HeaderObjectExplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<HeaderObjectExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => HeaderObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (HeaderObjectExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task HeaderObjectExplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<HeaderObjectExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => HeaderObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (HeaderObjectExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathArraySimple ──
    [TestMethod]
    public Task PathArraySimple_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathArraySimpleResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathArraySimpleResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathArraySimpleResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathArraySimple_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathArraySimpleResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathArraySimpleResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathArraySimpleResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathArrayMatrix ──
    [TestMethod]
    public Task PathArrayMatrix_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathArrayMatrixResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathArrayMatrixResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathArrayMatrixResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathArrayMatrix_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathArrayMatrixResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathArrayMatrixResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathArrayMatrixResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathArrayLabel ──
    [TestMethod]
    public Task PathArrayLabel_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathArrayLabelResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathArrayLabelResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathArrayLabelResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathArrayLabel_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathArrayLabelResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathArrayLabelResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathArrayLabelResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathObjectSimple ──
    [TestMethod]
    public Task PathObjectSimple_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectSimpleResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectSimpleResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectSimpleResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathObjectSimple_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectSimpleResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectSimpleResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectSimpleResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathObjectSimpleExplode ──
    [TestMethod]
    public Task PathObjectSimpleExplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectSimpleExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectSimpleExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectSimpleExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathObjectSimpleExplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectSimpleExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectSimpleExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectSimpleExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathObjectMatrix ──
    [TestMethod]
    public Task PathObjectMatrix_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectMatrixResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectMatrixResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectMatrixResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathObjectMatrix_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectMatrixResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectMatrixResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectMatrixResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathObjectMatrixExplode ──
    [TestMethod]
    public Task PathObjectMatrixExplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectMatrixExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectMatrixExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectMatrixExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathObjectMatrixExplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectMatrixExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectMatrixExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectMatrixExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathObjectLabel ──
    [TestMethod]
    public Task PathObjectLabel_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectLabelResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectLabelResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectLabelResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathObjectLabel_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectLabelResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectLabelResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectLabelResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── PathObjectLabelExplode ──
    [TestMethod]
    public Task PathObjectLabelExplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectLabelExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectLabelExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectLabelExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task PathObjectLabelExplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<PathObjectLabelExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => PathObjectLabelExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (PathObjectLabelExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QueryArrayExplode ──
    [TestMethod]
    public Task QueryArrayExplode_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArrayExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QueryArrayExplode_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QueryArrayExplodeResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QueryArrayExplodeResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QueryArrayExplodeResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetByFlag ──
    [TestMethod]
    public Task GetByFlag_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetByFlagResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetByFlagResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetByFlagResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetByFlag_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetByFlagResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetByFlagResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetByFlagResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetSessionProfile ──
    [TestMethod]
    public Task GetSessionProfile_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetSessionProfileResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetSessionProfileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetSessionProfileResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetSessionProfile_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetSessionProfileResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetSessionProfileResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetSessionProfileResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetDocument ──
    [TestMethod]
    public Task GetDocument_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetDocumentResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetDocument_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetDocumentResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetDocumentResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetDocumentResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── GetPreferences ──
    [TestMethod]
    public Task GetPreferences_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<GetPreferencesResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetPreferencesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPreferencesResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task GetPreferences_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<GetPreferencesResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => GetPreferencesResponse.CreateAsync(statusCode, contentStream, contentType),
            static (GetPreferencesResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);

    // ── QuerySearch ──
    [TestMethod]
    public Task QuerySearch_Validate_Detailed_InvalidBody_Throws() =>
        AssertValidateThrows<QuerySearchResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QuerySearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QuerySearchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Detailed,
            ApplicationJsonContentType);

    [TestMethod]
    public Task QuerySearch_Validate_Basic_InvalidBody_Throws() =>
        AssertValidateThrows<QuerySearchResponse>(
            200,
            static (int statusCode, Stream contentStream, string? contentType) => QuerySearchResponse.CreateAsync(statusCode, contentStream, contentType),
            static (QuerySearchResponse response, ValidationMode mode) => response.Validate(mode),
            ValidationMode.Basic,
            ApplicationJsonContentType);
}