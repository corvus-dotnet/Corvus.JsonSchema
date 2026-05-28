// <copyright file="ResponseAndClientCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests32.Client;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests covering Response structural members (IsSuccess, TryGet, MatchResult)
/// and Client body validation failure paths to close generated code coverage gaps.
/// </summary>
[TestClass]
public class ResponseAndClientCoverageTests
{
    // ── Response structural tests: IsSuccess, TryGetOk, MatchResult ──────
    [TestMethod]
    public async Task GetSessionProfile_IsSuccess_TrueFor200()
    {
        await using GetSessionProfileResponse response = await CreateResponse<GetSessionProfileResponse>(200, """{}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task GetSessionProfile_IsSuccess_FalseFor404()
    {
        await using GetSessionProfileResponse response = await CreateResponse<GetSessionProfileResponse>(404, """{}""");
        Assert.IsFalse(response.IsSuccess);
    }

    [TestMethod]
    public async Task GetSessionProfile_TryGetOk_TrueFor200()
    {
        await using GetSessionProfileResponse response = await CreateResponse<GetSessionProfileResponse>(200, """{"key":"val"}""");
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual(JsonValueKind.Object, body.ValueKind);
    }

    [TestMethod]
    public async Task GetSessionProfile_TryGetOk_FalseFor404()
    {
        await using GetSessionProfileResponse response = await CreateResponse<GetSessionProfileResponse>(404, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetSessionProfile_MatchResult_OkPath()
    {
        await using GetSessionProfileResponse response = await CreateResponse<GetSessionProfileResponse>(200, """{}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task GetSessionProfile_MatchResultContext_DefaultPath()
    {
        await using GetSessionProfileResponse response = await CreateResponse<GetSessionProfileResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task GetByFlag_IsSuccess_TrueFor200()
    {
        await using GetByFlagResponse response = await CreateResponse<GetByFlagResponse>(200, """{}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task GetByFlag_TryGetOk_TrueFor200()
    {
        await using GetByFlagResponse response = await CreateResponse<GetByFlagResponse>(200, """{"flag":true}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetByFlag_TryGetOk_FalseFor500()
    {
        await using GetByFlagResponse response = await CreateResponse<GetByFlagResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetByFlag_MatchResultContext_DefaultPath()
    {
        await using GetByFlagResponse response = await CreateResponse<GetByFlagResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task GetDocument_IsSuccess_TrueFor200()
    {
        await using GetDocumentResponse response = await CreateResponse<GetDocumentResponse>(200, """{"title":"doc","content":"text"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task GetDocument_TryGetOk_TrueFor200()
    {
        await using GetDocumentResponse response = await CreateResponse<GetDocumentResponse>(200, """{"title":"doc","content":"text"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetDocument_TryGetOk_FalseFor500()
    {
        await using GetDocumentResponse response = await CreateResponse<GetDocumentResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetDocument_MatchResultContext_DefaultPath()
    {
        await using GetDocumentResponse response = await CreateResponse<GetDocumentResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task GetPreferences_IsSuccess_TrueFor200()
    {
        await using GetPreferencesResponse response = await CreateResponse<GetPreferencesResponse>(200, """{"theme":"dark"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task GetPreferences_TryGetOk_TrueFor200()
    {
        await using GetPreferencesResponse response = await CreateResponse<GetPreferencesResponse>(200, """{"theme":"dark"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetPreferences_TryGetOk_FalseFor500()
    {
        await using GetPreferencesResponse response = await CreateResponse<GetPreferencesResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetPreferences_MatchResultContext_DefaultPath()
    {
        await using GetPreferencesResponse response = await CreateResponse<GetPreferencesResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task HeaderNestedArray_MatchResultContext_DefaultPath()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectLabel_MatchResultContext_DefaultPath()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectMatrix_MatchResultContext_DefaultPath()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectSimple_MatchResultContext_DefaultPath()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_MatchResultContext_DefaultPath()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_MatchResultContext_DefaultPath()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_MatchResultContext_DefaultPath()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(500, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task PathObjectLabel_TryGetOk_TrueFor200()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectLabel_TryGetOk_FalseFor500()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectLabel_IsSuccess_TrueFor200()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task PathObjectLabel_MatchResult_OkPath()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectLabel_MatchResultContext_OkPath()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectLabel_Validate_Basic_InvalidBody_Throws()
    {
        await using PathObjectLabelResponse response = await CreateResponse<PathObjectLabelResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task PathObjectMatrix_TryGetOk_TrueFor200()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectMatrix_TryGetOk_FalseFor500()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectMatrix_IsSuccess_TrueFor200()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task PathObjectMatrix_MatchResult_OkPath()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectMatrix_MatchResultContext_OkPath()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectMatrix_Validate_Basic_InvalidBody_Throws()
    {
        await using PathObjectMatrixResponse response = await CreateResponse<PathObjectMatrixResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task PathObjectSimple_TryGetOk_TrueFor200()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectSimple_TryGetOk_FalseFor500()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectSimple_IsSuccess_TrueFor200()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task PathObjectSimple_MatchResult_OkPath()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectSimple_MatchResultContext_OkPath()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectSimple_Validate_Basic_InvalidBody_Throws()
    {
        await using PathObjectSimpleResponse response = await CreateResponse<PathObjectSimpleResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_TryGetOk_TrueFor200()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_TryGetOk_FalseFor500()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_IsSuccess_TrueFor200()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_MatchResult_OkPath()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_MatchResultContext_OkPath()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_Validate_Basic_InvalidBody_Throws()
    {
        await using PathObjectLabelExplodeResponse response = await CreateResponse<PathObjectLabelExplodeResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_TryGetOk_TrueFor200()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_TryGetOk_FalseFor500()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_IsSuccess_TrueFor200()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_MatchResult_OkPath()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_MatchResultContext_OkPath()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_Validate_Basic_InvalidBody_Throws()
    {
        await using PathObjectMatrixExplodeResponse response = await CreateResponse<PathObjectMatrixExplodeResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_TryGetOk_TrueFor200()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_TryGetOk_FalseFor500()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_IsSuccess_TrueFor200()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(200, """{"a":"1"}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_MatchResult_OkPath()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_MatchResultContext_OkPath()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(200, """{"a":"1"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_Validate_Basic_InvalidBody_Throws()
    {
        await using PathObjectSimpleExplodeResponse response = await CreateResponse<PathObjectSimpleExplodeResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task GetPreferences_MatchResult_OkPath()
    {
        await using GetPreferencesResponse response = await CreateResponse<GetPreferencesResponse>(200, """{"theme":"dark"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task GetPreferences_MatchResultContext_OkPath()
    {
        await using GetPreferencesResponse response = await CreateResponse<GetPreferencesResponse>(200, """{"theme":"dark"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task GetDocument_MatchResult_OkPath()
    {
        await using GetDocumentResponse response = await CreateResponse<GetDocumentResponse>(200, """{"title":"t","content":"c"}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task GetDocument_MatchResultContext_OkPath()
    {
        await using GetDocumentResponse response = await CreateResponse<GetDocumentResponse>(200, """{"title":"t","content":"c"}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task GetItem_Validate_Basic_Invalid404_Throws()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(404, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task GetItem_TryGetDefault_TrueForNon200Non404()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(503, """{"error":"x"}""");
        Assert.IsTrue(response.TryGetDefault(out _));
    }

    [TestMethod]
    public async Task GetItem_TryGetDefault_FalseFor200()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(200, """{"id":"1","name":"x"}""");
        Assert.IsFalse(response.TryGetDefault(out _));
    }

    [TestMethod]
    public async Task GetItem_Validate_Basic_InvalidDefault_Throws()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(503, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task UpdateItem_Validate_Basic_InvalidBody_Throws()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task UpdateItem_Validate_Detailed_InvalidBody_Throws()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task DeleteItem_Validate_Basic_InvalidBody_Throws()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(404, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task DeleteItem_Validate_Detailed_InvalidBody_Throws()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(404, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task HeaderNestedArray_TryGetOk_TrueFor200()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(200, """{}""");
        Assert.IsTrue(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task HeaderNestedArray_TryGetOk_FalseFor500()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(500, """{}""");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task HeaderNestedArray_IsSuccess_TrueFor200()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(200, """{}""");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task HeaderNestedArray_MatchResult_OkPath()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(200, """{}""");
        int result = response.MatchResult(
            static body => 200,
            static code => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task HeaderNestedArray_MatchResultContext_OkPath()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(200, """{}""");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task HeaderNestedArray_Validate_Basic_InvalidBody_Throws()
    {
        await using HeaderNestedArrayResponse response = await CreateResponse<HeaderNestedArrayResponse>(200, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task EchoText_IsSuccess_TrueFor200()
    {
        await using EchoTextResponse response = await CreateTextResponse<EchoTextResponse>(200, "text/plain", "hello");
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task EchoText_MatchResultContext_OkPath()
    {
        await using EchoTextResponse response = await CreateTextResponse<EchoTextResponse>(200, "text/plain", "hi");
        string result = response.MatchResult<string, string>(
            "prefix",
            static (text, in ctx) => ctx + ":" + text,
            static (code, in ctx) => ctx + ":" + code);
        Assert.AreEqual("prefix:hi", result);
    }

    [TestMethod]
    public async Task EchoText_MatchResultContext_DefaultPath()
    {
        await using EchoTextResponse response = await CreateTextResponse<EchoTextResponse>(500, "text/plain", "err");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (text, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task EchoText_MatchResult_DefaultPath()
    {
        await using EchoTextResponse response = await CreateTextResponse<EchoTextResponse>(500, "text/plain", "err");
        int result = response.MatchResult(
            static (string? text) => 200,
            static (int code) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task TraceItem_IsSuccess_TrueFor200()
    {
        await using TraceItemResponse response = await CreateTextResponse<TraceItemResponse>(200, "text/plain", "hello");
        Assert.IsTrue(response.IsSuccess);
    }

    // ── Multi-status response Detailed validation ────────────────────────
    [TestMethod]
    public async Task GetItem_Validate_Detailed_ValidOk_Passes()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(200, """{"id":"1","name":"Widget"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetItem_Validate_Detailed_Valid404_Passes()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(404, """{"code":404,"message":"nope"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetItem_Validate_Detailed_ValidDefault_Passes()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(503, """{"error":"down"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetItem_Validate_Detailed_Invalid404_Throws()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(404, """{}""");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "failed schema validation");
    }

    [TestMethod]
    public async Task GetItem_Validate_Detailed_InvalidDefault_Throws()
    {
        await using GetItemResponse response = await CreateResponse<GetItemResponse>(503, """42""");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "failed schema validation");
    }

    [TestMethod]
    public async Task CreateItem_Validate_Detailed_Valid201_Passes()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(201, """{"id":"new","name":"x"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CreateItem_Validate_Detailed_Valid422_Passes()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(422, """{"errors":["name is required"]}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CreateItem_Validate_Detailed_Invalid201_Throws()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(201, """{}""");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "failed schema validation");
    }

    [TestMethod]
    public async Task CreateItem_Validate_Detailed_Invalid422_Throws()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(422, """42""");
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "failed schema validation");
    }

    [TestMethod]
    public async Task CreateItem_TryGetUnprocessableEntity_TrueFor422()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(422, """{"errors":[]}""");
        Assert.IsTrue(response.TryGetUnprocessableEntity(out _));
    }

    [TestMethod]
    public async Task CreateItem_TryGetUnprocessableEntity_FalseFor201()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(201, """{"id":"x","name":"y"}""");
        Assert.IsFalse(response.TryGetUnprocessableEntity(out _));
    }

    // ── Non-seekable stream tests ────────────────────────────────────────
    [TestMethod]
    public async Task EchoText_NonSeekableStream_ReadsCorrectly()
    {
        byte[] data = Encoding.UTF8.GetBytes("hello world");
        using NonSeekableStream stream = new(data);
        await using EchoTextResponse response = await EchoTextResponse.CreateAsync(
            200, stream, "text/plain");

        Assert.AreEqual("hello world", response.OkText);
    }

    [TestMethod]
    public async Task EchoText_NonSeekableStream_LargePayload_BufferGrows()
    {
        string bigText = new('X', 5000);
        byte[] data = Encoding.UTF8.GetBytes(bigText);
        using NonSeekableStream stream = new(data);
        await using EchoTextResponse response = await EchoTextResponse.CreateAsync(
            200, stream, "text/plain");

        Assert.AreEqual(bigText, response.OkText);
    }

    [TestMethod]
    public async Task TraceItem_NonSeekableStream_ReadsCorrectly()
    {
        byte[] data = Encoding.UTF8.GetBytes("TRACE response body");
        using NonSeekableStream stream = new(data);
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(
            200, stream, "text/plain");

        Assert.AreEqual("TRACE response body", response.OkText);
    }

    [TestMethod]
    public async Task TraceItem_NonSeekableStream_LargePayload_BufferGrows()
    {
        string bigText = new('Y', 5000);
        byte[] data = Encoding.UTF8.GetBytes(bigText);
        using NonSeekableStream stream = new(data);
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(
            200, stream, "text/plain");

        Assert.AreEqual(bigText, response.OkText);
    }

    [TestMethod]
    public async Task GetTextOrJson_NonSeekableStream_TextPath()
    {
        byte[] data = Encoding.UTF8.GetBytes("plain text content");
        using NonSeekableStream stream = new(data);
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            200, stream, "text/plain");

        Assert.AreEqual("plain text content", response.OkText);
    }

    [TestMethod]
    public async Task GetTextOrJson_NonSeekableStream_LargePayload()
    {
        string bigText = new('Z', 5000);
        byte[] data = Encoding.UTF8.GetBytes(bigText);
        using NonSeekableStream stream = new(data);
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            200, stream, "text/plain");

        Assert.AreEqual(bigText, response.OkText);
    }

    // ── Client body validation failure tests (Detailed mode) ─────────────
    [TestMethod]
    public async Task ApiFormsClient_SubmitContactForm_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsContactBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitContactFormAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiFormsClient_UploadDocument_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsUploadBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UploadDocumentAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitEncodedContact_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsEncodedContactBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitEncodedContactFormAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiFormsClient_UploadEncodedDocument_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsEncodedUploadBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UploadEncodedDocumentAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitDefaultForm_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsDefaultFormBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitDefaultFormAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitNonexplodedForm_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsNonexplodedFormBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitNonexplodedFormAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitMultipartTypes_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsMultipartTypesBody>.Parse("""42""");
        BinaryPartData dummyFile = new((s, ct) => { s.Write([0xFF]); return default; }, "application/octet-stream", "test.bin");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitMultipartTypesAsync(
                doc.RootElement,
                dummyFile,
                validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiStreamingClient_ChatCompletions_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"choices":[]}""");
        var client = new ApiStreamingClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostChatCompletionsBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.ChatCompletionsAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiItemsClient_UpdateItem_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"id":"1","name":"x"}""");
        var client = new ApiItemsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PutItemsBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UpdateItemAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiItemsClient_CreateItem_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.Created, """{"id":"new","name":"x"}""");
        var client = new ApiItemsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostItemsBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.CreateItemAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiOrdersClient_UpdateOrder_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"ok","total":1}""");
        var client = new ApiOrdersClient(harness.Transport);
        using var doc = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{}""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UpdateOrderAsync(
                Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                doc.RootElement,
                validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiTrackingClient_TrackEvent_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.Accepted, "");
        var client = new ApiTrackingClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostTrackingBody>.Parse("""42""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.TrackEventAsync("t123", doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task ApiSearchClient_QuerySearch_InvalidBody_Detailed_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{}""");
        var client = new ApiSearchClient(harness.Transport);
        using var doc = ParsedJsonDocument<Schema1>.Parse("""{}""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.QuerySearchAsync(doc.RootElement, validationMode: ValidationMode.Detailed));
    }

    // ── GetItemTag with response headers ────────────────────────────────
    [TestMethod]
    public async Task GetItemTag_Headers_XTotalCount_Parsed()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Total-Count"] = "42",
            ["X-Request-Id"] = "req-123",
            ["X-Tags"] = "alpha, beta, gamma",
            ["X-Metadata"] = "key1, val1, key2, val2",
            ["X-Page-Sizes"] = "10, 25, 50",
            ["X-Flags"] = "true, false, true",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);
        Assert.AreEqual(42, (int)response.XTotalCountHeader);
    }

    [TestMethod]
    public async Task GetItemTag_Headers_XRequestId_Parsed()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Request-Id"] = "req-abc",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);
        Assert.AreEqual("req-abc", (string)response.XRequestIdHeader);
    }

    [TestMethod]
    public async Task GetItemTag_Headers_XTags_ParsedAsArray()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Tags"] = "alpha, beta",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);
        var tags = response.XTagsHeader;
        Assert.AreEqual(JsonValueKind.Array, tags.ValueKind);
    }

    [TestMethod]
    public async Task GetItemTag_Headers_XMetadata_ParsedAsObject()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Metadata"] = "name, value, other, data",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);
        var metadata = response.XMetadataHeader;
        Assert.AreEqual(JsonValueKind.Object, metadata.ValueKind);
    }

    [TestMethod]
    public async Task GetItemTag_Headers_XPageSizes_ParsedAsIntArray()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Page-Sizes"] = "10, 25, 50",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);
        var pageSizes = response.XPageSizesHeader;
        Assert.AreEqual(JsonValueKind.Array, pageSizes.ValueKind);
    }

    [TestMethod]
    public async Task GetItemTag_Headers_XFlags_ParsedAsBoolArray()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Flags"] = "true, false, true",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);
        var flags = response.XFlagsHeader;
        Assert.AreEqual(JsonValueKind.Array, flags.ValueKind);
    }

    [TestMethod]
    public async Task GetItemTag_Headers_CachedOnSecondAccess()
    {
        var headers = new TestResponseHeaders(new Dictionary<string, string>
        {
            ["X-Total-Count"] = "99",
            ["X-Request-Id"] = "cached-id",
            ["X-Page-Sizes"] = "5, 10",
            ["X-Flags"] = "false",
        });
        byte[] body = Encoding.UTF8.GetBytes("""{"name":"tag"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json", headers);

        // First access parses
        _ = response.XTotalCountHeader;
        _ = response.XRequestIdHeader;
        _ = response.XPageSizesHeader;
        _ = response.XFlagsHeader;

        // Second access uses cached value (covers the xHeaderParsed=true early return)
        Assert.AreEqual(99, (int)response.XTotalCountHeader);
        Assert.AreEqual("cached-id", (string)response.XRequestIdHeader);
        Assert.AreEqual(JsonValueKind.Array, response.XPageSizesHeader.ValueKind);
        Assert.AreEqual(JsonValueKind.Array, response.XFlagsHeader.ValueKind);
    }

    [TestMethod]
    public async Task GetItemTag_TryGetOk_FalseFor500()
    {
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(500, new MemoryStream([]), null);
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetItemTag_MatchResultContext_OkPath()
    {
        byte[] body = Encoding.UTF8.GetBytes("""{"key":"val"}""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json");
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(200, result);
    }

    [TestMethod]
    public async Task GetItemTag_MatchResultContext_DefaultPath()
    {
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(500, new MemoryStream([]), null);
        int result = response.MatchResult<string, int>(
            "ctx",
            static (body, in ctx) => 200,
            static (code, in ctx) => code);
        Assert.AreEqual(500, result);
    }

    [TestMethod]
    public async Task GetItemTag_Validate_Basic_InvalidBody_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task GetItemTag_Validate_Detailed_InvalidBody_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using GetItemTagResponse response = await GetItemTagResponse.CreateAsync(200, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    // ── GetTextOrJson remaining paths ────────────────────────────────────
    [TestMethod]
    public async Task GetTextOrJson_TextPlain_OkUtf8Bytes()
    {
        byte[] body = Encoding.UTF8.GetBytes("hello world");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(200, new MemoryStream(body), "text/plain");
        Assert.IsFalse(response.OkUtf8Bytes.IsEmpty);
        Assert.AreEqual("hello world", response.OkText);
    }

    [TestMethod]
    public async Task GetTextOrJson_TryGetOk_FalseFor404()
    {
        byte[] body = Encoding.UTF8.GetBytes("""{"error":"nope"}""");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(404, new MemoryStream(body), "application/json");
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetTextOrJson_TryGetOkString_FalseFor404()
    {
        byte[] body = Encoding.UTF8.GetBytes("""{"error":"nope"}""");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(404, new MemoryStream(body), "application/json");
        Assert.IsFalse(response.TryGetOkString(out _));
    }

    [TestMethod]
    public async Task GetTextOrJson_TryGetNotFound_FalseFor200()
    {
        byte[] body = Encoding.UTF8.GetBytes("""{"value":"ok"}""");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(200, new MemoryStream(body), "application/json");
        Assert.IsFalse(response.TryGetNotFound(out _));
    }

    [TestMethod]
    public async Task GetTextOrJson_TryGetOkString_TrueFor200Text()
    {
        byte[] body = Encoding.UTF8.GetBytes("some text");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(200, new MemoryStream(body), "text/plain");
        Assert.IsTrue(response.TryGetOkString(out string? text));
        Assert.AreEqual("some text", text);
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Basic_200Json_InvalidBody_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(200, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Detailed_404_InvalidBody_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(404, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Basic_404_InvalidBody_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(404, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    // ── ApiFormsClient Basic mode body validation ────────────────────────
    [TestMethod]
    public async Task ApiFormsClient_SubmitContactForm_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsContactBody>.Parse("""42""");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitContactFormAsync(doc.RootElement, validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiFormsClient_UploadDocument_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsUploadBody>.Parse("""42""");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UploadDocumentAsync(doc.RootElement, validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitEncodedContact_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsEncodedContactBody>.Parse("""42""");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitEncodedContactFormAsync(doc.RootElement, validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiFormsClient_UploadEncodedDocument_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsEncodedUploadBody>.Parse("""42""");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UploadEncodedDocumentAsync(doc.RootElement, validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitDefaultForm_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsDefaultFormBody>.Parse("""42""");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitDefaultFormAsync(doc.RootElement, validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitNonexplodedForm_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsNonexplodedFormBody>.Parse("""42""");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitNonexplodedFormAsync(doc.RootElement, validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitMultipartTypes_InvalidBody_Basic_Throws()
    {
        using var harness = new ClientTestHarness(HttpStatusCode.OK, """{"ok":true}""");
        var client = new ApiFormsClient(harness.Transport);
        using var doc = ParsedJsonDocument<PostFormsMultipartTypesBody>.Parse("""42""");
        var file = new BinaryPartData((s, ct) => { s.Write([0xFF]); return default; }, "application/octet-stream", "test.bin");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.SubmitMultipartTypesAsync(doc.RootElement, file, validationMode: ValidationMode.Basic));
    }

    // ── Request first-param validation failures ──────────────────────────
    [TestMethod]
    public void GetItemRequest_Validate_Basic_InvalidItemId_Throws()
    {
        GetItemRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void GetItemRequest_Validate_Detailed_InvalidItemId_Throws()
    {
        GetItemRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetSessionProfileRequest_Validate_Basic_InvalidSessionId_Throws()
    {
        GetSessionProfileRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void GetSessionProfileRequest_Validate_Detailed_InvalidSessionId_Throws()
    {
        GetSessionProfileRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetByFlagRequest_Validate_Basic_InvalidFlag_Throws()
    {
        GetByFlagRequest request = new(JsonBoolean.ParseValue("42"u8), JsonString.ParseValue("\"trace\""u8), JsonInt32.ParseValue("1"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void GetByFlagRequest_Validate_Detailed_InvalidFlag_Throws()
    {
        GetByFlagRequest request = new(JsonBoolean.ParseValue("42"u8), JsonString.ParseValue("\"trace\""u8), JsonInt32.ParseValue("1"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void SearchRequest_Validate_Basic_InvalidQuery_Throws()
    {
        SearchRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void SearchRequest_Validate_Detailed_InvalidQuery_Throws()
    {
        SearchRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetItemTagRequest_Validate_Basic_InvalidItemId_Throws()
    {
        GetItemTagRequest request = new(JsonInt64.ParseValue("\"not-a-number\""u8), JsonString.ParseValue("\"tag\""u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void GetItemTagRequest_Validate_Detailed_InvalidItemId_Throws()
    {
        GetItemTagRequest request = new(JsonInt64.ParseValue("\"not-a-number\""u8), JsonString.ParseValue("\"tag\""u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetOrderRequest_Validate_Basic_InvalidOrderId_Throws()
    {
        GetOrderRequest request = new(JsonUuid.ParseValue("\"not-a-uuid\""u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void GetOrderRequest_Validate_Detailed_InvalidOrderId_Throws()
    {
        GetOrderRequest request = new(JsonUuid.ParseValue("\"not-a-uuid\""u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void TrackEventRequest_Validate_Basic_InvalidTrackerId_Throws()
    {
        TrackEventRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public void TrackEventRequest_Validate_Detailed_InvalidTrackerId_Throws()
    {
        TrackEventRequest request = new(JsonString.ParseValue("42"u8));
        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    // ── Response validation: CreateItem remaining Basic branch ──────────
    [TestMethod]
    public async Task CreateItem_Validate_Basic_201_InvalidBody_Throws()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(201, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task DownloadFile_TryGetOkStream_FalseFor404()
    {
        await using DownloadFileResponse response = await DownloadFileResponse.CreateAsync(404, new MemoryStream([]), null);
        Assert.IsFalse(response.TryGetOkStream(out _));
    }

    [TestMethod]
    public async Task DownloadMixed_Validate_Basic_Invalid404_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(404, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task DownloadMixed_Validate_Detailed_Invalid404_Throws()
    {
        byte[] body = Encoding.UTF8.GetBytes("""42""");
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(404, new MemoryStream(body), "application/json");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    // ── Client DisposeAsync coverage ────────────────────────────────────────
    [TestMethod]
    public async Task ApiItemsClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiOrdersClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiOrdersClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiSearchClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiSearchClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiTrackingClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiTrackingClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiStreamingClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiStreamingClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiDocsClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiDocsClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiFormsClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiComplexClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiComplexClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiComplexParamsClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiComplexParamsClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiDocumentsClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiDocumentsClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiFilesClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFilesClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiFlagsClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFlagsClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiPagesClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiPagesClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiPreferencesClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiPreferencesClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiSessionClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiSessionClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ApiTextClient_DisposeAsync()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiTextClient client = new(harness.Transport);
        await client.DisposeAsync();
    }

    // ── Response default/unmatched status paths ─────────────────────────────
    [TestMethod]
    public async Task HeadItem_CreateAsync_Status404()
    {
        await using HeadItemResponse response = await HeadItemResponse.CreateAsync(404, new MemoryStream());
        Assert.AreEqual(404, response.StatusCode);
    }

    [TestMethod]
    public async Task HeadItem_CreateAsync_DefaultStatus()
    {
        await using HeadItemResponse response = await HeadItemResponse.CreateAsync(500, new MemoryStream());
        Assert.AreEqual(500, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
    }

    [TestMethod]
    public async Task OptionsItems_CreateAsync_NonMatchingStatus()
    {
        await using OptionsItemsResponse response = await OptionsItemsResponse.CreateAsync(200, new MemoryStream());
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task OptionsItems_IsSuccess_FalseFor500()
    {
        await using OptionsItemsResponse response = await OptionsItemsResponse.CreateAsync(500, new MemoryStream());
        Assert.IsFalse(response.IsSuccess);
    }

    [TestMethod]
    public async Task TrackEvent_CreateAsync_NonMatchingStatus()
    {
        await using TrackEventResponse response = await TrackEventResponse.CreateAsync(200, new MemoryStream());
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task TrackEvent_IsSuccess_FalseFor500()
    {
        await using TrackEventResponse response = await TrackEventResponse.CreateAsync(500, new MemoryStream());
        Assert.IsFalse(response.IsSuccess);
    }

    [TestMethod]
    public async Task PurgeItem_CreateAsync_NonMatchingStatus()
    {
        await using PurgeItemResponse response = await PurgeItemResponse.CreateAsync(200, new MemoryStream());
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteItem_CreateAsync_DefaultStatus()
    {
        await using DeleteItemResponse response = await DeleteItemResponse.CreateAsync(500, new MemoryStream());
        Assert.AreEqual(500, response.StatusCode);
    }

    [TestMethod]
    public async Task StreamEvents_CreateAsync_DefaultStatus()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(500, new MemoryStream());
        Assert.AreEqual(500, response.StatusCode);
    }

    // ── MatchResult<TContext, TResult> (with context) ───────────────────────
    [TestMethod]
    public async Task DownloadFile_MatchResult_WithContext_Status200()
    {
        byte[] content = Encoding.UTF8.GetBytes("file data");
        await using DownloadFileResponse response = await DownloadFileResponse.CreateAsync(200, new MemoryStream(content));
        string result = response.MatchResult(
            "ctx",
            static (stream, in ctx) => $"ok:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    [TestMethod]
    public async Task DownloadFile_MatchResult_WithContext_DefaultStatus()
    {
        await using DownloadFileResponse response = await DownloadFileResponse.CreateAsync(500, new MemoryStream());
        string result = response.MatchResult(
            42,
            static (stream, in ctx) => $"ok:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("default:500:42", result);
    }

    [TestMethod]
    public async Task UpdateItem_MatchResult_WithContext_Status200()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(200, """{}""");
        string result = response.MatchResult(
            "x",
            static (body, in ctx) => $"ok:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("ok:x", result);
    }

    [TestMethod]
    public async Task UpdateItem_MatchResult_WithContext_DefaultStatus()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(404, """{}""");
        string result = response.MatchResult(
            "x",
            static (body, in ctx) => $"ok:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("default:404:x", result);
    }

    [TestMethod]
    public async Task CreateItem_MatchResult_WithContext_DefaultStatus()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(500, """{}""");
        string result = response.MatchResult(
            "y",
            static (body, in ctx) => $"created:{ctx}",
            static (body, in ctx) => $"422:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("default:500:y", result);
    }

    [TestMethod]
    public async Task DeleteItem_MatchResult_WithContext_Status404()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(404, """{}""");
        string result = response.MatchResult(
            "z",
            static (body, in ctx) => $"notfound:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("notfound:z", result);
    }

    [TestMethod]
    public async Task DeleteItem_MatchResult_WithContext_DefaultStatus()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(500, """{}""");
        string result = response.MatchResult(
            "z",
            static (body, in ctx) => $"notfound:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("default:500:z", result);
    }

    [TestMethod]
    public async Task StreamEvents_MatchResult_WithContext_Status401()
    {
        await using StreamEventsResponse response = await CreateResponse<StreamEventsResponse>(401, """{}""");
        string result = response.MatchResult(
            "a",
            static (body, in ctx) => $"unauth:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("unauth:a", result);
    }

    [TestMethod]
    public async Task StreamEvents_MatchResult_WithContext_DefaultStatus()
    {
        await using StreamEventsResponse response = await CreateResponse<StreamEventsResponse>(500, """{}""");
        string result = response.MatchResult(
            "a",
            static (body, in ctx) => $"unauth:{ctx}",
            static (status, in ctx) => $"default:{status}:{ctx}");
        Assert.AreEqual("default:500:a", result);
    }

    // ── TryGet false paths ──────────────────────────────────────────────────
    [TestMethod]
    public async Task DownloadMixed_TryGetNotFound_FalseWhenNot404()
    {
        byte[] content = Encoding.UTF8.GetBytes("file data");
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(200, new MemoryStream(content));
        bool found = response.TryGetNotFound(out _);
        Assert.IsFalse(found);
    }

    // ── Response Validate pass-through (valid body, no throw) ───────────────
    [TestMethod]
    public async Task DeleteItem_Validate_Detailed_ValidBody_Passes()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(404, """{"code":404,"message":"gone"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task DeleteItem_Validate_Basic_ValidBody_Passes()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(404, """{"code":404,"message":"gone"}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task DeleteItem_Validate_Detailed_NonMatchingStatus()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(200, """{}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task DeleteItem_Validate_Basic_NonMatchingStatus()
    {
        await using DeleteItemResponse response = await CreateResponse<DeleteItemResponse>(200, """{}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task UpdateItem_Validate_Detailed_ValidBody_Passes()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(200, """{"id":"up","name":"Updated"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task UpdateItem_Validate_Basic_ValidBody_Passes()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(200, """{"id":"up","name":"Updated"}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task UpdateItem_Validate_Detailed_NonMatchingStatus()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(404, """{}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task UpdateItem_Validate_Basic_NonMatchingStatus()
    {
        await using UpdateItemResponse response = await CreateResponse<UpdateItemResponse>(404, """{}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CreateItem_Validate_Basic_ValidBody_Status201()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(201, """{"id":"new","name":"Widget"}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CreateItem_Validate_Basic_ValidBody_Status422()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(422, """{"errors":["invalid"]}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CreateItem_Validate_Basic_NonMatchingStatus()
    {
        await using CreateItemResponse response = await CreateResponse<CreateItemResponse>(500, """{}""");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Detailed_ValidBody_Status404()
    {
        await using GetTextOrJsonResponse response = await CreateResponse<GetTextOrJsonResponse>(404, """{"error":"not found"}""");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Basic_ValidBody_Status200()
    {
        await using GetTextOrJsonResponse response = await CreateResponse<GetTextOrJsonResponse>(200, """{"value":"hello"}""");
        response.Validate(ValidationMode.Basic);
    }

    // ── Response Validate throws (Basic mode, invalid body) ─────────────────
    [TestMethod]
    public async Task CopyItem_Validate_Basic_InvalidBody_Throws()
    {
        await using CopyItemResponse response = await CreateResponse<CopyItemResponse>(201, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task StreamEvents_Validate_Basic_InvalidBody_Throws()
    {
        await using StreamEventsResponse response = await CreateResponse<StreamEventsResponse>(401, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ChatCompletions_Validate_Basic_InvalidBody_Throws()
    {
        await using ChatCompletionsResponse response = await CreateResponse<ChatCompletionsResponse>(400, """42""");
        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    // ── Client body validation: Basic mode throw (invalid body) ─────────────
    [TestMethod]
    public async Task ApiStreamingClient_ChatCompletions_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiStreamingClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.ChatCompletionsAsync(
                PostChatCompletionsBody.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiOrdersClient_UpdateOrder_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiOrdersClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UpdateOrderAsync(
                CanonTests32.Client.JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
                CanonTests32.Client.JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440001\""u8),
                PutOrdersByOrderIdBody.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiSearchClient_QuerySearch_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiSearchClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.QuerySearchAsync(
                Schema1.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiTrackingClient_TrackEvent_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiTrackingClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.TrackEventAsync(
                CanonTests32.Client.JsonString.ParseValue("\"session-123\""u8),
                PostTrackingBody.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiItemsClient_PatchItem_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.PatchItemAsync(
                CanonTests32.Client.JsonString.ParseValue("\"item-1\""u8),
                PatchItemsByItemIdBody.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiItemsClient_UpdateItem_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.UpdateItemAsync(
                PutItemsBody.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    [TestMethod]
    public async Task ApiItemsClient_CreateItem_Basic_InvalidBody_Throws()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await client.CreateItemAsync(
                PostItemsBody.ParseValue("42"u8),
                validationMode: ValidationMode.Basic));
    }

    // ── Client body validation: Detailed mode pass-through (valid body) ─────
    [TestMethod]
    public async Task ApiStreamingClient_ChatCompletions_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiStreamingClient client = new(harness.Transport);
        await using ChatCompletionsResponse response = await client.ChatCompletionsAsync(
            PostChatCompletionsBody.ParseValue("""{"model":"gpt","messages":[]}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiOrdersClient_UpdateOrder_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiOrdersClient client = new(harness.Transport);
        await using UpdateOrderResponse response = await client.UpdateOrderAsync(
            CanonTests32.Client.JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            CanonTests32.Client.JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440001\""u8),
            PutOrdersByOrderIdBody.ParseValue("""{"status":"confirmed"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiSearchClient_QuerySearch_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiSearchClient client = new(harness.Transport);
        await using QuerySearchResponse response = await client.QuerySearchAsync(
            Schema1.ParseValue("""{"query":"test","tags":["a"]}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiTrackingClient_TrackEvent_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiTrackingClient client = new(harness.Transport);
        await using TrackEventResponse response = await client.TrackEventAsync(
            CanonTests32.Client.JsonString.ParseValue("\"session-123\""u8),
            PostTrackingBody.ParseValue("""{"event":"click"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiItemsClient_PatchItem_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await using PatchItemResponse response = await client.PatchItemAsync(
            CanonTests32.Client.JsonString.ParseValue("\"item-1\""u8),
            PatchItemsByItemIdBody.ParseValue("""{"name":"patched"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiItemsClient_UpdateItem_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await using UpdateItemResponse response = await client.UpdateItemAsync(
            PutItemsBody.ParseValue("""{"id":"up","name":"Updated"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiItemsClient_CreateItem_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiItemsClient client = new(harness.Transport);
        await using CreateItemResponse response = await client.CreateItemAsync(
            PostItemsBody.ParseValue("""{"name":"test"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitContactForm_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await using SubmitContactFormResponse response = await client.SubmitContactFormAsync(
            PostFormsContactBody.ParseValue("""{"name":"Alice","email":"a@b.com"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_UploadDocument_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await using UploadDocumentResponse response = await client.UploadDocumentAsync(
            PostFormsUploadBody.ParseValue("""{"title":"Doc","category":"r"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitEncodedContact_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await using SubmitEncodedContactFormResponse response = await client.SubmitEncodedContactFormAsync(
            PostFormsEncodedContactBody.ParseValue("""{"name":"A","email":"a@b.com","tags":["x"]}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_UploadEncodedDocument_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await using UploadEncodedDocumentResponse response = await client.UploadEncodedDocumentAsync(
            PostFormsEncodedUploadBody.ParseValue("""{"title":"D","metadata":{"k":"v"}}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitDefault_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await using SubmitDefaultFormResponse response = await client.SubmitDefaultFormAsync(
            PostFormsDefaultFormBody.ParseValue("""{"name":"Alice"}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitNonexploded_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        await using SubmitNonexplodedFormResponse response = await client.SubmitNonexplodedFormAsync(
            PostFormsNonexplodedFormBody.ParseValue("""{"keywords":["a"],"data":{"x":"1"}}"""u8),
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    [TestMethod]
    public async Task ApiFormsClient_SubmitMultipartTypes_Detailed_ValidBody_Passes()
    {
        using ClientTestHarness harness = new(HttpStatusCode.OK, """{}""");
        await using ApiFormsClient client = new(harness.Transport);
        BinaryPartData file = new((s, ct) => default);
        await using SubmitMultipartTypesResponse response = await client.SubmitMultipartTypesAsync(
            PostFormsMultipartTypesBody.ParseValue("""{"count":1,"active":true,"title":"T"}"""u8),
            file,
            validationMode: ValidationMode.Detailed);
        Assert.AreEqual(200, response.StatusCode);
    }

    // ── Helper methods ───────────────────────────────────────────────────
    private static async Task<TResponse> CreateResponse<TResponse>(int statusCode, string json)
        where TResponse : struct, IApiResponse<TResponse>
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        MemoryStream stream = new(bytes);
        return await TResponse.CreateAsync(statusCode, stream, "application/json");
    }

    private static async Task<TResponse> CreateTextResponse<TResponse>(int statusCode, string contentType, string text)
        where TResponse : struct, IApiResponse<TResponse>
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        MemoryStream stream = new(bytes);
        return await TResponse.CreateAsync(statusCode, stream, contentType);
    }

    /// <summary>
    /// A stream wrapper that reports CanSeek = false to exercise the non-seekable buffer path.
    /// </summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream inner;

        public NonSeekableStream(byte[] data)
        {
            this.inner = new MemoryStream(data);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => this.inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A minimal HTTP test harness that mocks responses for client tests.
    /// </summary>
    private sealed class ClientTestHarness : IDisposable
    {
        private readonly MockHandler handler;
        private readonly HttpClient client;

        public ClientTestHarness(HttpStatusCode statusCode, string responseBody)
        {
            this.handler = new MockHandler(statusCode, responseBody);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public void Dispose()
        {
            this.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            this.client.Dispose();
            this.handler.Dispose();
        }
    }

    private sealed class MockHandler : DelegatingHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;

        public MockHandler(HttpStatusCode statusCode, string responseBody)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.InnerHandler = new HttpClientHandler();
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpContent content = string.IsNullOrEmpty(this.responseBody)
                ? new ByteArrayContent([])
                : new StringContent(this.responseBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = new(this.statusCode) { Content = content };
            return Task.FromResult(response);
        }
    }

    private sealed class TestResponseHeaders : IResponseHeaders
    {
        private readonly Dictionary<string, string> headers;

        public TestResponseHeaders(Dictionary<string, string> headers)
        {
            this.headers = new(headers, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetValue(string headerName, out string? value)
        {
            if (this.headers.TryGetValue(headerName, out string? rawValue))
            {
                value = rawValue;
                return true;
            }

            value = null;
            return false;
        }
    }
}