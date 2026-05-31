// <copyright file="RequestInterfaceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using CanonTests32.Client;
using CanonTests32.Client.Models;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

[TestClass]
public class RequestInterfaceTests
{
    private static readonly ArrayBufferWriter<byte> DummyWriter = new();

    private static void AssertWriteResolvedPathThrows<TRequest>()
        where TRequest : struct, IApiRequest<TRequest>
    {
        TRequest request = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteResolvedPath(DummyWriter));
    }

    private static void AssertWriteQueryStringThrows<TRequest>()
        where TRequest : struct, IApiRequest<TRequest>
    {
        TRequest request = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteQueryString(DummyWriter));
    }

    private static void AssertWriteCookiesThrows<TRequest>()
        where TRequest : struct, IApiRequest<TRequest>
    {
        TRequest request = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteCookies(DummyWriter));
    }

    // ── ChatCompletionsRequest ──
    [TestMethod]
    public void ChatCompletionsRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<ChatCompletionsRequest>();
    }

    [TestMethod]
    public void ChatCompletionsRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<ChatCompletionsRequest>();
    }

    [TestMethod]
    public void ChatCompletionsRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<ChatCompletionsRequest>();
    }

    // ── CookieArrayNonexplodeRequest ──
    [TestMethod]
    public void CookieArrayNonexplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<CookieArrayNonexplodeRequest>();
    }

    [TestMethod]
    public void CookieArrayNonexplodeRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<CookieArrayNonexplodeRequest>();
    }

    // ── CookieArrayRequest ──
    [TestMethod]
    public void CookieArrayRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<CookieArrayRequest>();
    }

    [TestMethod]
    public void CookieArrayRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<CookieArrayRequest>();
    }

    // ── CookieObjectNonexplodeRequest ──
    [TestMethod]
    public void CookieObjectNonexplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<CookieObjectNonexplodeRequest>();
    }

    [TestMethod]
    public void CookieObjectNonexplodeRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<CookieObjectNonexplodeRequest>();
    }

    // ── CookieObjectRequest ──
    [TestMethod]
    public void CookieObjectRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<CookieObjectRequest>();
    }

    [TestMethod]
    public void CookieObjectRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<CookieObjectRequest>();
    }

    // ── CopyItemRequest ──
    [TestMethod]
    public void CopyItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<CopyItemRequest>();
    }

    [TestMethod]
    public void CopyItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<CopyItemRequest>();
    }

    // ── CreateItemRequest ──
    [TestMethod]
    public void CreateItemRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<CreateItemRequest>();
    }

    [TestMethod]
    public void CreateItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<CreateItemRequest>();
    }

    [TestMethod]
    public void CreateItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<CreateItemRequest>();
    }

    // ── DeleteItemRequest ──
    [TestMethod]
    public void DeleteItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<DeleteItemRequest>();
    }

    [TestMethod]
    public void DeleteItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<DeleteItemRequest>();
    }

    // ── DownloadFileRequest ──
    [TestMethod]
    public void DownloadFileRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<DownloadFileRequest>();
    }

    [TestMethod]
    public void DownloadFileRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<DownloadFileRequest>();
    }

    [TestMethod]
    public void DownloadFileRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<DownloadFileRequest>();
    }

    // ── DownloadMixedRequest ──
    [TestMethod]
    public void DownloadMixedRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<DownloadMixedRequest>();
    }

    [TestMethod]
    public void DownloadMixedRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<DownloadMixedRequest>();
    }

    [TestMethod]
    public void DownloadMixedRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<DownloadMixedRequest>();
    }

    // ── EchoTextRequest ──
    [TestMethod]
    public void EchoTextRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<EchoTextRequest>();
    }

    [TestMethod]
    public void EchoTextRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<EchoTextRequest>();
    }

    [TestMethod]
    public void EchoTextRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<EchoTextRequest>();
    }

    // ── GetByFlagRequest ──
    [TestMethod]
    public void GetByFlagRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetByFlagRequest>();
    }

    [TestMethod]
    public void GetByFlagRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetByFlagRequest>();
    }

    // ── GetDocumentRequest ──
    [TestMethod]
    public void GetDocumentRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetDocumentRequest>();
    }

    [TestMethod]
    public void GetDocumentRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetDocumentRequest>();
    }

    // ── GetItemDetailsRequest ──
    [TestMethod]
    public void GetItemDetailsRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetItemDetailsRequest>();
    }

    [TestMethod]
    public void GetItemDetailsRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetItemDetailsRequest>();
    }

    // ── GetItemRequest ──
    [TestMethod]
    public void GetItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetItemRequest>();
    }

    // ── GetItemTagRequest ──
    [TestMethod]
    public void GetItemTagRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetItemTagRequest>();
    }

    // ── GetOrderRequest ──
    [TestMethod]
    public void GetOrderRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetOrderRequest>();
    }

    // ── GetPageRequest ──
    [TestMethod]
    public void GetPageRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetPageRequest>();
    }

    // ── GetPreferencesRequest ──
    [TestMethod]
    public void GetPreferencesRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<GetPreferencesRequest>();
    }

    [TestMethod]
    public void GetPreferencesRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetPreferencesRequest>();
    }

    // ── GetSessionProfileRequest ──
    [TestMethod]
    public void GetSessionProfileRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<GetSessionProfileRequest>();
    }

    [TestMethod]
    public void GetSessionProfileRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetSessionProfileRequest>();
    }

    // ── GetTextOrJsonRequest ──
    [TestMethod]
    public void GetTextOrJsonRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<GetTextOrJsonRequest>();
    }

    [TestMethod]
    public void GetTextOrJsonRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetTextOrJsonRequest>();
    }

    [TestMethod]
    public void GetTextOrJsonRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetTextOrJsonRequest>();
    }

    // ── GetVendorJsonRequest ──
    [TestMethod]
    public void GetVendorJsonRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<GetVendorJsonRequest>();
    }

    [TestMethod]
    public void GetVendorJsonRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<GetVendorJsonRequest>();
    }

    [TestMethod]
    public void GetVendorJsonRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<GetVendorJsonRequest>();
    }

    // ── HeaderArrayRequest ──
    [TestMethod]
    public void HeaderArrayRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<HeaderArrayRequest>();
    }

    [TestMethod]
    public void HeaderArrayRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<HeaderArrayRequest>();
    }

    [TestMethod]
    public void HeaderArrayRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<HeaderArrayRequest>();
    }

    // ── HeaderNestedArrayRequest ──
    [TestMethod]
    public void HeaderNestedArrayRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<HeaderNestedArrayRequest>();
    }

    [TestMethod]
    public void HeaderNestedArrayRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<HeaderNestedArrayRequest>();
    }

    [TestMethod]
    public void HeaderNestedArrayRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<HeaderNestedArrayRequest>();
    }

    // ── HeaderObjectExplodeRequest ──
    [TestMethod]
    public void HeaderObjectExplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<HeaderObjectExplodeRequest>();
    }

    [TestMethod]
    public void HeaderObjectExplodeRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<HeaderObjectExplodeRequest>();
    }

    [TestMethod]
    public void HeaderObjectExplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<HeaderObjectExplodeRequest>();
    }

    // ── HeaderObjectRequest ──
    [TestMethod]
    public void HeaderObjectRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<HeaderObjectRequest>();
    }

    [TestMethod]
    public void HeaderObjectRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<HeaderObjectRequest>();
    }

    [TestMethod]
    public void HeaderObjectRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<HeaderObjectRequest>();
    }

    // ── HeadItemRequest ──
    [TestMethod]
    public void HeadItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<HeadItemRequest>();
    }

    [TestMethod]
    public void HeadItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<HeadItemRequest>();
    }

    // ── OptionsItemsRequest ──
    [TestMethod]
    public void OptionsItemsRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<OptionsItemsRequest>();
    }

    [TestMethod]
    public void OptionsItemsRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<OptionsItemsRequest>();
    }

    [TestMethod]
    public void OptionsItemsRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<OptionsItemsRequest>();
    }

    // ── PatchItemRequest ──
    [TestMethod]
    public void PatchItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PatchItemRequest>();
    }

    [TestMethod]
    public void PatchItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PatchItemRequest>();
    }

    // ── PathArrayLabelRequest ──
    [TestMethod]
    public void PathArrayLabelRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathArrayLabelRequest>();
    }

    [TestMethod]
    public void PathArrayLabelRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathArrayLabelRequest>();
    }

    // ── PathArrayMatrixRequest ──
    [TestMethod]
    public void PathArrayMatrixRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathArrayMatrixRequest>();
    }

    [TestMethod]
    public void PathArrayMatrixRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathArrayMatrixRequest>();
    }

    // ── PathArraySimpleRequest ──
    [TestMethod]
    public void PathArraySimpleRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathArraySimpleRequest>();
    }

    [TestMethod]
    public void PathArraySimpleRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathArraySimpleRequest>();
    }

    // ── PathObjectLabelExplodeRequest ──
    [TestMethod]
    public void PathObjectLabelExplodeRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathObjectLabelExplodeRequest>();
    }

    [TestMethod]
    public void PathObjectLabelExplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathObjectLabelExplodeRequest>();
    }

    // ── PathObjectLabelRequest ──
    [TestMethod]
    public void PathObjectLabelRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathObjectLabelRequest>();
    }

    [TestMethod]
    public void PathObjectLabelRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathObjectLabelRequest>();
    }

    // ── PathObjectMatrixExplodeRequest ──
    [TestMethod]
    public void PathObjectMatrixExplodeRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathObjectMatrixExplodeRequest>();
    }

    [TestMethod]
    public void PathObjectMatrixExplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathObjectMatrixExplodeRequest>();
    }

    // ── PathObjectMatrixRequest ──
    [TestMethod]
    public void PathObjectMatrixRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathObjectMatrixRequest>();
    }

    [TestMethod]
    public void PathObjectMatrixRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathObjectMatrixRequest>();
    }

    // ── PathObjectSimpleExplodeRequest ──
    [TestMethod]
    public void PathObjectSimpleExplodeRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathObjectSimpleExplodeRequest>();
    }

    [TestMethod]
    public void PathObjectSimpleExplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathObjectSimpleExplodeRequest>();
    }

    // ── PathObjectSimpleRequest ──
    [TestMethod]
    public void PathObjectSimpleRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PathObjectSimpleRequest>();
    }

    [TestMethod]
    public void PathObjectSimpleRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PathObjectSimpleRequest>();
    }

    // ── ProcessBatchRequest ──
    [TestMethod]
    public void ProcessBatchRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<ProcessBatchRequest>();
    }

    [TestMethod]
    public void ProcessBatchRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<ProcessBatchRequest>();
    }

    [TestMethod]
    public void ProcessBatchRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<ProcessBatchRequest>();
    }

    // ── PurgeItemRequest ──
    [TestMethod]
    public void PurgeItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<PurgeItemRequest>();
    }

    [TestMethod]
    public void PurgeItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<PurgeItemRequest>();
    }

    // ── QueryArrayExplodeRequest ──
    [TestMethod]
    public void QueryArrayExplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryArrayExplodeRequest>();
    }

    [TestMethod]
    public void QueryArrayExplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryArrayExplodeRequest>();
    }

    // ── QueryArrayNonexplodeRequest ──
    [TestMethod]
    public void QueryArrayNonexplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryArrayNonexplodeRequest>();
    }

    [TestMethod]
    public void QueryArrayNonexplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryArrayNonexplodeRequest>();
    }

    // ── QueryArrayPipeRequest ──
    [TestMethod]
    public void QueryArrayPipeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryArrayPipeRequest>();
    }

    [TestMethod]
    public void QueryArrayPipeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryArrayPipeRequest>();
    }

    // ── QueryArraySpaceRequest ──
    [TestMethod]
    public void QueryArraySpaceRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryArraySpaceRequest>();
    }

    [TestMethod]
    public void QueryArraySpaceRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryArraySpaceRequest>();
    }

    // ── QueryObjectDeepRequest ──
    [TestMethod]
    public void QueryObjectDeepRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryObjectDeepRequest>();
    }

    [TestMethod]
    public void QueryObjectDeepRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryObjectDeepRequest>();
    }

    // ── QueryObjectExplodeRequest ──
    [TestMethod]
    public void QueryObjectExplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryObjectExplodeRequest>();
    }

    [TestMethod]
    public void QueryObjectExplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryObjectExplodeRequest>();
    }

    // ── QueryObjectNonexplodeRequest ──
    [TestMethod]
    public void QueryObjectNonexplodeRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QueryObjectNonexplodeRequest>();
    }

    [TestMethod]
    public void QueryObjectNonexplodeRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QueryObjectNonexplodeRequest>();
    }

    // ── QuerySearchRequest ──
    [TestMethod]
    public void QuerySearchRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<QuerySearchRequest>();
    }

    [TestMethod]
    public void QuerySearchRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<QuerySearchRequest>();
    }

    // ── SearchRequest ──
    [TestMethod]
    public void SearchRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SearchRequest>();
    }

    [TestMethod]
    public void SearchRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SearchRequest>();
    }

    // ── SearchWithQuerystringRequest ──
    [TestMethod]
    public void SearchWithQuerystringRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SearchWithQuerystringRequest>();
    }

    [TestMethod]
    public void SearchWithQuerystringRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SearchWithQuerystringRequest>();
    }

    // ── StreamEventsRequest ──
    [TestMethod]
    public void StreamEventsRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<StreamEventsRequest>();
    }

    [TestMethod]
    public void StreamEventsRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<StreamEventsRequest>();
    }

    [TestMethod]
    public void StreamEventsRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<StreamEventsRequest>();
    }

    // ── SubmitContactFormRequest ──
    [TestMethod]
    public void SubmitContactFormRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SubmitContactFormRequest>();
    }

    [TestMethod]
    public void SubmitContactFormRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<SubmitContactFormRequest>();
    }

    [TestMethod]
    public void SubmitContactFormRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SubmitContactFormRequest>();
    }

    // ── SubmitDefaultFormRequest ──
    [TestMethod]
    public void SubmitDefaultFormRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SubmitDefaultFormRequest>();
    }

    [TestMethod]
    public void SubmitDefaultFormRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<SubmitDefaultFormRequest>();
    }

    [TestMethod]
    public void SubmitDefaultFormRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SubmitDefaultFormRequest>();
    }

    // ── SubmitEncodedContactFormRequest ──
    [TestMethod]
    public void SubmitEncodedContactFormRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SubmitEncodedContactFormRequest>();
    }

    [TestMethod]
    public void SubmitEncodedContactFormRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<SubmitEncodedContactFormRequest>();
    }

    [TestMethod]
    public void SubmitEncodedContactFormRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SubmitEncodedContactFormRequest>();
    }

    // ── SubmitMultipartTypesRequest ──
    [TestMethod]
    public void SubmitMultipartTypesRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SubmitMultipartTypesRequest>();
    }

    [TestMethod]
    public void SubmitMultipartTypesRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<SubmitMultipartTypesRequest>();
    }

    [TestMethod]
    public void SubmitMultipartTypesRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SubmitMultipartTypesRequest>();
    }

    // ── SubmitNonexplodedFormRequest ──
    [TestMethod]
    public void SubmitNonexplodedFormRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<SubmitNonexplodedFormRequest>();
    }

    [TestMethod]
    public void SubmitNonexplodedFormRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<SubmitNonexplodedFormRequest>();
    }

    [TestMethod]
    public void SubmitNonexplodedFormRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<SubmitNonexplodedFormRequest>();
    }

    // ── TextToJsonRequest ──
    [TestMethod]
    public void TextToJsonRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<TextToJsonRequest>();
    }

    [TestMethod]
    public void TextToJsonRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<TextToJsonRequest>();
    }

    [TestMethod]
    public void TextToJsonRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<TextToJsonRequest>();
    }

    // ── TraceItemRequest ──
    [TestMethod]
    public void TraceItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<TraceItemRequest>();
    }

    [TestMethod]
    public void TraceItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<TraceItemRequest>();
    }

    // ── TrackEventRequest ──
    [TestMethod]
    public void TrackEventRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<TrackEventRequest>();
    }

    [TestMethod]
    public void TrackEventRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<TrackEventRequest>();
    }

    // ── UpdateItemRequest ──
    [TestMethod]
    public void UpdateItemRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<UpdateItemRequest>();
    }

    [TestMethod]
    public void UpdateItemRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<UpdateItemRequest>();
    }

    [TestMethod]
    public void UpdateItemRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<UpdateItemRequest>();
    }

    // ── UpdateOrderRequest ──
    [TestMethod]
    public void UpdateOrderRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<UpdateOrderRequest>();
    }

    [TestMethod]
    public void UpdateOrderRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<UpdateOrderRequest>();
    }

    // ── UploadDocMixedRequest ──
    [TestMethod]
    public void UploadDocMixedRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<UploadDocMixedRequest>();
    }

    [TestMethod]
    public void UploadDocMixedRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<UploadDocMixedRequest>();
    }

    [TestMethod]
    public void UploadDocMixedRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<UploadDocMixedRequest>();
    }

    // ── UploadDocumentRequest ──
    [TestMethod]
    public void UploadDocumentRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<UploadDocumentRequest>();
    }

    [TestMethod]
    public void UploadDocumentRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<UploadDocumentRequest>();
    }

    [TestMethod]
    public void UploadDocumentRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<UploadDocumentRequest>();
    }

    // ── UploadEncodedDocumentRequest ──
    [TestMethod]
    public void UploadEncodedDocumentRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<UploadEncodedDocumentRequest>();
    }

    [TestMethod]
    public void UploadEncodedDocumentRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<UploadEncodedDocumentRequest>();
    }

    [TestMethod]
    public void UploadEncodedDocumentRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<UploadEncodedDocumentRequest>();
    }

    // ── UploadFileRequest ──
    [TestMethod]
    public void UploadFileRequest_WriteResolvedPath_Throws()
    {
        AssertWriteResolvedPathThrows<UploadFileRequest>();
    }

    [TestMethod]
    public void UploadFileRequest_WriteQueryString_Throws()
    {
        AssertWriteQueryStringThrows<UploadFileRequest>();
    }

    [TestMethod]
    public void UploadFileRequest_WriteCookies_Throws()
    {
        AssertWriteCookiesThrows<UploadFileRequest>();
    }
}