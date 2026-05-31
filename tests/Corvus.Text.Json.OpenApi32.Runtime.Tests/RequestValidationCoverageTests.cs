// <copyright file="RequestValidationCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using CanonTests32.Client;
using CanonTests32.Client.Models;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Systematic validation coverage tests exercising:
/// - Validate(None) early return for all request types
/// - Validate(Basic/Detailed) with all-valid params (closing-brace coverage)
/// - WriteHeaders/WriteCookies/WriteQueryString/WriteResolvedPath that throw
/// - First-param validation failures
/// </summary>
[TestClass]
public class RequestValidationCoverageTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Section 1: Validate(None) — covers the immediate return path
    // ═══════════════════════════════════════════════════════════════════════
    [TestMethod]
    public void ValidateNone_GetByFlag() => default(GetByFlagRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetItem() => default(GetItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetOrder() => default(GetOrderRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetItemTag() => default(GetItemTagRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetPage() => default(GetPageRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_Search() => default(SearchRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetSessionProfile() => default(GetSessionProfileRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_HeadItem() => default(HeadItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PurgeItem() => default(PurgeItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_TrackEvent() => default(TrackEventRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetPreferences() => default(GetPreferencesRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_DeleteItem() => default(DeleteItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_CopyItem() => default(CopyItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetDocument() => default(GetDocumentRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetItemDetails() => default(GetItemDetailsRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PatchItem() => default(PatchItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_TraceItem() => default(TraceItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_UpdateOrder() => default(UpdateOrderRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QuerySearch() => default(QuerySearchRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_HeaderArray() => default(HeaderArrayRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_HeaderObjectExplode() => default(HeaderObjectExplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_HeaderObject() => default(HeaderObjectRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryArrayNonexplode() => default(QueryArrayNonexplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryArrayPipe() => default(QueryArrayPipeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryArraySpace() => default(QueryArraySpaceRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryObjectNonexplode() => default(QueryObjectNonexplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_CookieArrayNonexplode() => default(CookieArrayNonexplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_CookieObjectNonexplode() => default(CookieObjectNonexplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_CookieArray() => default(CookieArrayRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_CookieObject() => default(CookieObjectRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathArrayLabel() => default(PathArrayLabelRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathArrayMatrix() => default(PathArrayMatrixRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathArraySimple() => default(PathArraySimpleRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathObjectLabelExplode() => default(PathObjectLabelExplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathObjectLabel() => default(PathObjectLabelRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathObjectMatrixExplode() => default(PathObjectMatrixExplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathObjectMatrix() => default(PathObjectMatrixRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathObjectSimpleExplode() => default(PathObjectSimpleExplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_PathObjectSimple() => default(PathObjectSimpleRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryArrayExplode() => default(QueryArrayExplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryObjectExplode() => default(QueryObjectExplodeRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_QueryObjectDeep() => default(QueryObjectDeepRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_SearchWithQuerystring() => default(SearchWithQuerystringRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_HeaderNestedArray() => default(HeaderNestedArrayRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_ChatCompletions() => default(ChatCompletionsRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_CreateItem() => default(CreateItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_DownloadFile() => default(DownloadFileRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_DownloadMixed() => default(DownloadMixedRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_EchoText() => default(EchoTextRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetTextOrJson() => default(GetTextOrJsonRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_GetVendorJson() => default(GetVendorJsonRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_OptionsItems() => default(OptionsItemsRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_ProcessBatch() => default(ProcessBatchRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_StreamEvents() => default(StreamEventsRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_SubmitContactForm() => default(SubmitContactFormRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_SubmitDefaultForm() => default(SubmitDefaultFormRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_SubmitEncodedContactForm() => default(SubmitEncodedContactFormRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_SubmitMultipartTypes() => default(SubmitMultipartTypesRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_SubmitNonexplodedForm() => default(SubmitNonexplodedFormRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_TextToJson() => default(TextToJsonRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_UpdateItem() => default(UpdateItemRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_UploadDocMixed() => default(UploadDocMixedRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_UploadDocument() => default(UploadDocumentRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_UploadEncodedDocument() => default(UploadEncodedDocumentRequest).Validate(ValidationMode.None);

    [TestMethod]
    public void ValidateNone_UploadFile() => default(UploadFileRequest).Validate(ValidationMode.None);

    // ═══════════════════════════════════════════════════════════════════════
    // Section 2: Validate with all-valid params (covers closing braces)
    // ═══════════════════════════════════════════════════════════════════════
    [TestMethod]
    public void ValidateAllValid_GetByFlag_Basic()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace\""u8),
            JsonInt32.ParseValue("1"u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetByFlag_Detailed()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace\""u8),
            JsonInt32.ParseValue("1"u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetByFlag_DetailedWithOptionals()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace\""u8),
            JsonInt32.ParseValue("1"u8))
        { XDebug = JsonBoolean.ParseValue("false"u8), XScore = JsonDouble.ParseValue("3.14"u8) };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetItem_Basic()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetItem_Detailed()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetItem_DetailedWithOptionals()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        {
            Filter = JsonString.ParseValue("\"active\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
            Verbose = JsonBoolean.ParseValue("true"u8),
            XRequestId = JsonString.ParseValue("\"req-1\""u8),
        };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetOrder_Basic()
    {
        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetOrder_Detailed()
    {
        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetOrder_DetailedWithOptionals()
    {
        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        { XTraceId = JsonString.ParseValue("\"trace\""u8), Fields = JsonString.ParseValue("\"name,price\""u8) };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetItemTag_Basic()
    {
        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("123"u8),
            JsonString.ParseValue("\"tag1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetItemTag_Detailed()
    {
        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("123"u8),
            JsonString.ParseValue("\"tag1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetPage_Basic()
    {
        var request = new GetPageRequest(JsonInt32.ParseValue("1"u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetPage_Detailed()
    {
        var request = new GetPageRequest(JsonInt32.ParseValue("1"u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetPage_DetailedWithOptionals()
    {
        var request = new GetPageRequest(JsonInt32.ParseValue("1"u8))
        { Offset = JsonInteger.ParseValue("0"u8) };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_Search_Basic()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"hello\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_Search_Detailed()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"hello\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_Search_DetailedWithOptionals()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"hello\""u8))
        { Page = JsonInt32.ParseValue("1"u8), Rating = JsonSingle.ParseValue("4.5"u8) };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetSessionProfile_Basic()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetSessionProfile_Detailed()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_HeadItem_Basic()
    {
        var request = new HeadItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_HeadItem_Detailed()
    {
        var request = new HeadItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_PurgeItem_Basic()
    {
        var request = new PurgeItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_PurgeItem_Detailed()
    {
        var request = new PurgeItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_TrackEvent_Basic()
    {
        var request = new TrackEventRequest(JsonString.ParseValue("\"tracker-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_TrackEvent_Detailed()
    {
        var request = new TrackEventRequest(JsonString.ParseValue("\"tracker-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_TrackEvent_DetailedWithOptionals()
    {
        var request = new TrackEventRequest(JsonString.ParseValue("\"tracker-1\""u8))
        { RefUrl = JsonString.ParseValue("\"https://example.com\""u8) };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetPreferences_Basic()
    {
        var request = new GetPreferencesRequest(JsonString.ParseValue("\"sess-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetPreferences_Detailed()
    {
        var request = new GetPreferencesRequest(JsonString.ParseValue("\"sess-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetPreferences_DetailedWithOptionals()
    {
        var request = new GetPreferencesRequest(JsonString.ParseValue("\"sess-1\""u8))
        { Theme = JsonString.ParseValue("\"dark\""u8) };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_DeleteItem_Basic()
    {
        var request = new DeleteItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_DeleteItem_Detailed()
    {
        var request = new DeleteItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_CopyItem_Basic()
    {
        var request = new CopyItemRequest(
            JsonString.ParseValue("\"item-1\""u8),
            JsonString.ParseValue("\"/dest\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_CopyItem_Detailed()
    {
        var request = new CopyItemRequest(
            JsonString.ParseValue("\"item-1\""u8),
            JsonString.ParseValue("\"/dest\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetDocument_Basic()
    {
        var request = new GetDocumentRequest(JsonString.ParseValue("\"doc/path\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetDocument_Detailed()
    {
        var request = new GetDocumentRequest(JsonString.ParseValue("\"doc/path\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_GetItemDetails_Basic()
    {
        var request = new GetItemDetailsRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_GetItemDetails_Detailed()
    {
        var request = new GetItemDetailsRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_PatchItem_Basic()
    {
        var request = new PatchItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_PatchItem_Detailed()
    {
        var request = new PatchItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_TraceItem_Basic()
    {
        var request = new TraceItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_TraceItem_Detailed()
    {
        var request = new TraceItemRequest(JsonString.ParseValue("\"item-1\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_UpdateOrder_Basic()
    {
        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.ParseValue("\"660e8400-e29b-41d4-a716-446655440000\""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_UpdateOrder_Detailed()
    {
        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.ParseValue("\"660e8400-e29b-41d4-a716-446655440000\""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QuerySearch_Basic()
    {
        var request = new QuerySearchRequest { Limit = JsonInt32.ParseValue("10"u8) };
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QuerySearch_Detailed()
    {
        var request = new QuerySearchRequest { Limit = JsonInt32.ParseValue("10"u8) };
        request.Validate(ValidationMode.Detailed);
    }

    // Complex parameter types
    [TestMethod]
    public void ValidateAllValid_HeaderArray_Basic()
    {
        var request = new HeaderArrayRequest(
            GetComplexHeaderArrayXTags.ParseValue("""["a","b"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_HeaderArray_Detailed()
    {
        var request = new HeaderArrayRequest(
            GetComplexHeaderArrayXTags.ParseValue("""["a","b"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_HeaderObjectExplode_Basic()
    {
        var request = new HeaderObjectExplodeRequest(
            GetComplexHeaderObjectExplodeXDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_HeaderObjectExplode_Detailed()
    {
        var request = new HeaderObjectExplodeRequest(
            GetComplexHeaderObjectExplodeXDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_HeaderObject_Basic()
    {
        var request = new HeaderObjectRequest(
            GetComplexHeaderObjectXDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_HeaderObject_Detailed()
    {
        var request = new HeaderObjectRequest(
            GetComplexHeaderObjectXDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArrayNonexplode_Basic()
    {
        var request = new QueryArrayNonexplodeRequest(
            GetComplexQueryArrayNonexplodeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArrayNonexplode_Detailed()
    {
        var request = new QueryArrayNonexplodeRequest(
            GetComplexQueryArrayNonexplodeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArrayPipe_Basic()
    {
        var request = new QueryArrayPipeRequest(
            GetComplexQueryArrayPipeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArrayPipe_Detailed()
    {
        var request = new QueryArrayPipeRequest(
            GetComplexQueryArrayPipeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArraySpace_Basic()
    {
        var request = new QueryArraySpaceRequest(
            GetComplexQueryArraySpaceColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArraySpace_Detailed()
    {
        var request = new QueryArraySpaceRequest(
            GetComplexQueryArraySpaceColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryObjectNonexplode_Basic()
    {
        var request = new QueryObjectNonexplodeRequest(
            GetComplexQueryObjectNonexplodeDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryObjectNonexplode_Detailed()
    {
        var request = new QueryObjectNonexplodeRequest(
            GetComplexQueryObjectNonexplodeDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_CookieArrayNonexplode_Basic()
    {
        var request = new CookieArrayNonexplodeRequest(
            GetComplexCookieArrayNonexplodeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_CookieArrayNonexplode_Detailed()
    {
        var request = new CookieArrayNonexplodeRequest(
            GetComplexCookieArrayNonexplodeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_CookieObjectNonexplode_Basic()
    {
        var request = new CookieObjectNonexplodeRequest(
            GetComplexCookieObjectNonexplodePrefs.ParseValue("""{"theme":"dark","lang":"en"}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_CookieObjectNonexplode_Detailed()
    {
        var request = new CookieObjectNonexplodeRequest(
            GetComplexCookieObjectNonexplodePrefs.ParseValue("""{"theme":"dark","lang":"en"}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_CookieArray_Basic()
    {
        var request = new CookieArrayRequest(
            GetComplexCookieArrayColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_CookieArray_Detailed()
    {
        var request = new CookieArrayRequest(
            GetComplexCookieArrayColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_CookieObject_Basic()
    {
        var request = new CookieObjectRequest(
            GetComplexCookieObjectPrefs.ParseValue("""{"theme":"dark","lang":"en"}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_CookieObject_Detailed()
    {
        var request = new CookieObjectRequest(
            GetComplexCookieObjectPrefs.ParseValue("""{"theme":"dark","lang":"en"}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArrayExplode_Basic()
    {
        var request = new QueryArrayExplodeRequest(
            GetComplexQueryArrayExplodeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryArrayExplode_Detailed()
    {
        var request = new QueryArrayExplodeRequest(
            GetComplexQueryArrayExplodeColors.ParseValue("""["red","blue"]"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryObjectExplode_Basic()
    {
        var request = new QueryObjectExplodeRequest(
            GetComplexQueryObjectExplodeDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryObjectExplode_Detailed()
    {
        var request = new QueryObjectExplodeRequest(
            GetComplexQueryObjectExplodeDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void ValidateAllValid_QueryObjectDeep_Basic()
    {
        var request = new QueryObjectDeepRequest(
            GetComplexQueryObjectDeepDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void ValidateAllValid_QueryObjectDeep_Detailed()
    {
        var request = new QueryObjectDeepRequest(
            GetComplexQueryObjectDeepDims.ParseValue("""{"width":10,"height":20}"""u8));
        request.Validate(ValidationMode.Detailed);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Section 3: Throw methods — WriteHeaders/WriteQueryString/WriteCookies
    // that call ThrowNoXxxParameters (covers the throw call line)
    // ═══════════════════════════════════════════════════════════════════════
    [TestMethod]
    public void WriteHeaders_Throws_HeadItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new HeadItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteHeaders(static (ReadOnlySpan<byte> _, ReadOnlySpan<byte> __, int ___) => { }, 0);
        });
    }

    [TestMethod]
    public void WriteHeaders_Throws_OptionsItems()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = default(OptionsItemsRequest);
            req.WriteHeaders(static (ReadOnlySpan<byte> _, ReadOnlySpan<byte> __, int ___) => { }, 0);
        });
    }

    [TestMethod]
    public void WriteHeaders_Throws_PurgeItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new PurgeItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteHeaders(static (ReadOnlySpan<byte> _, ReadOnlySpan<byte> __, int ___) => { }, 0);
        });
    }

    [TestMethod]
    public void WriteHeaders_Throws_TrackEvent()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new TrackEventRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteHeaders(static (ReadOnlySpan<byte> _, ReadOnlySpan<byte> __, int ___) => { }, 0);
        });
    }

    [TestMethod]
    public void WriteResolvedPath_Throws_QuerySearch()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = default(QuerySearchRequest);
            req.WriteResolvedPath(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteResolvedPath_Throws_OptionsItems()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = default(OptionsItemsRequest);
            req.WriteResolvedPath(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteResolvedPath_Throws_CreateItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = default(CreateItemRequest);
            req.WriteResolvedPath(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteQueryString_Throws_HeadItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new HeadItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteQueryString(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteQueryString_Throws_PurgeItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new PurgeItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteQueryString(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteQueryString_Throws_GetByFlag()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new GetByFlagRequest(
                JsonBoolean.ParseValue("true"u8),
                JsonString.ParseValue("\"t\""u8),
                JsonInt32.ParseValue("1"u8));
            req.WriteQueryString(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteQueryString_Throws_DeleteItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new DeleteItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteQueryString(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteCookies_Throws_GetByFlag()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new GetByFlagRequest(
                JsonBoolean.ParseValue("true"u8),
                JsonString.ParseValue("\"t\""u8),
                JsonInt32.ParseValue("1"u8));
            req.WriteCookies(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteCookies_Throws_HeadItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new HeadItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteCookies(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteCookies_Throws_DeleteItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new DeleteItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteCookies(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteCookies_Throws_PurgeItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new PurgeItemRequest(JsonString.ParseValue("\"x\""u8));
            req.WriteCookies(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteCookies_Throws_GetItemTag()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8));
            req.WriteCookies(new ArrayBufferWriter<byte>());
        });
    }

    [TestMethod]
    public void WriteCookies_Throws_CopyItem()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var req = new CopyItemRequest(JsonString.ParseValue("\"x\""u8), JsonString.ParseValue("\"/d\""u8));
            req.WriteCookies(new ArrayBufferWriter<byte>());
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Section 4: First-param validation failures (covers throw lines for
    // required first parameters in both Basic and Detailed modes)
    // ═══════════════════════════════════════════════════════════════════════
    [TestMethod]
    public void FirstParamInvalid_GetByFlag_Active_Basic()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.From(JsonElement.ParseValue("42"u8)),
            JsonString.ParseValue("\"t\""u8),
            JsonInt32.ParseValue("1"u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "active");
    }

    [TestMethod]
    public void FirstParamInvalid_GetByFlag_Active_Detailed()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.From(JsonElement.ParseValue("42"u8)),
            JsonString.ParseValue("\"t\""u8),
            JsonInt32.ParseValue("1"u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "active");
    }

    [TestMethod]
    public void FirstParamInvalid_GetItem_Basic()
    {
        var request = new GetItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetItem_Detailed()
    {
        var request = new GetItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetOrder_Basic()
    {
        var request = new GetOrderRequest(JsonUuid.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "orderId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetOrder_Detailed()
    {
        var request = new GetOrderRequest(JsonUuid.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "orderId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetItemTag_Basic()
    {
        var request = new GetItemTagRequest(
            JsonInt64.From(JsonElement.ParseValue("\"not-int\""u8)),
            JsonString.ParseValue("\"t\""u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetItemTag_Detailed()
    {
        var request = new GetItemTagRequest(
            JsonInt64.From(JsonElement.ParseValue("\"not-int\""u8)),
            JsonString.ParseValue("\"t\""u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetPage_Basic()
    {
        var request = new GetPageRequest(JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "pageNum");
    }

    [TestMethod]
    public void FirstParamInvalid_GetPage_Detailed()
    {
        var request = new GetPageRequest(JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "pageNum");
    }

    [TestMethod]
    public void FirstParamInvalid_Search_Basic()
    {
        var request = new SearchRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "q");
    }

    [TestMethod]
    public void FirstParamInvalid_Search_Detailed()
    {
        var request = new SearchRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "q");
    }

    [TestMethod]
    public void FirstParamInvalid_GetSessionProfile_Basic()
    {
        var request = new GetSessionProfileRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "session_id");
    }

    [TestMethod]
    public void FirstParamInvalid_GetSessionProfile_Detailed()
    {
        var request = new GetSessionProfileRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "session_id");
    }

    [TestMethod]
    public void FirstParamInvalid_DeleteItem_Basic()
    {
        var request = new DeleteItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_DeleteItem_Detailed()
    {
        var request = new DeleteItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_CopyItem_Basic()
    {
        var request = new CopyItemRequest(
            JsonString.From(JsonElement.ParseValue("42"u8)),
            JsonString.ParseValue("\"/d\""u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_CopyItem_Detailed()
    {
        var request = new CopyItemRequest(
            JsonString.From(JsonElement.ParseValue("42"u8)),
            JsonString.ParseValue("\"/d\""u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetDocument_Basic()
    {
        var request = new GetDocumentRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "docPath");
    }

    [TestMethod]
    public void FirstParamInvalid_GetDocument_Detailed()
    {
        var request = new GetDocumentRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "docPath");
    }

    [TestMethod]
    public void FirstParamInvalid_GetItemDetails_Basic()
    {
        var request = new GetItemDetailsRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_GetItemDetails_Detailed()
    {
        var request = new GetItemDetailsRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_PatchItem_Basic()
    {
        var request = new PatchItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_PatchItem_Detailed()
    {
        var request = new PatchItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_TraceItem_Basic()
    {
        var request = new TraceItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_TraceItem_Detailed()
    {
        var request = new TraceItemRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void FirstParamInvalid_UpdateOrder_Basic()
    {
        var request = new UpdateOrderRequest(
            JsonUuid.From(JsonElement.ParseValue("42"u8)),
            JsonUuid.ParseValue("\"660e8400-e29b-41d4-a716-446655440000\""u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "orderId");
    }

    [TestMethod]
    public void FirstParamInvalid_UpdateOrder_Detailed()
    {
        var request = new UpdateOrderRequest(
            JsonUuid.From(JsonElement.ParseValue("42"u8)),
            JsonUuid.ParseValue("\"660e8400-e29b-41d4-a716-446655440000\""u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "orderId");
    }

    [TestMethod]
    public void FirstParamInvalid_TrackEvent_Basic()
    {
        var request = new TrackEventRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "tracker_id");
    }

    [TestMethod]
    public void FirstParamInvalid_TrackEvent_Detailed()
    {
        var request = new TrackEventRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "tracker_id");
    }

    [TestMethod]
    public void FirstParamInvalid_GetPreferences_Basic()
    {
        var request = new GetPreferencesRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "session_token");
    }

    [TestMethod]
    public void FirstParamInvalid_GetPreferences_Detailed()
    {
        var request = new GetPreferencesRequest(JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "session_token");
    }

    [TestMethod]
    public void FirstParamInvalid_QuerySearch_Basic()
    {
        var request = new QuerySearchRequest { Limit = JsonInt32.From(JsonElement.ParseValue("\"x\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "limit");
    }

    [TestMethod]
    public void FirstParamInvalid_QuerySearch_Detailed()
    {
        var request = new QuerySearchRequest { Limit = JsonInt32.From(JsonElement.ParseValue("\"x\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "limit");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Section 5: Second-param validation failures for 2-param requests
    // (covers throw lines for second required param)
    // ═══════════════════════════════════════════════════════════════════════
    [TestMethod]
    public void SecondParamInvalid_UpdateOrder_Basic()
    {
        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Trace-Id");
    }

    [TestMethod]
    public void SecondParamInvalid_UpdateOrder_Detailed()
    {
        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Trace-Id");
    }

    [TestMethod]
    public void SecondParamInvalid_CopyItem_Basic()
    {
        var request = new CopyItemRequest(
            JsonString.ParseValue("\"item-1\""u8),
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "Destination");
    }

    [TestMethod]
    public void SecondParamInvalid_CopyItem_Detailed()
    {
        var request = new CopyItemRequest(
            JsonString.ParseValue("\"item-1\""u8),
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "Destination");
    }

    [TestMethod]
    public void SecondParamInvalid_GetItemTag_Basic()
    {
        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("123"u8),
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "tagName");
    }

    [TestMethod]
    public void SecondParamInvalid_GetItemTag_Detailed()
    {
        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("123"u8),
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "tagName");
    }
}