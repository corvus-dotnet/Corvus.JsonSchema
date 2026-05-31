// <copyright file="CreateServerUriTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Client;
using CanonTests32.Client.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests that <c>CreateServerUri()</c> on request structs returns the correct URI.
/// </summary>
[TestClass]
public class CreateServerUriTests
{
    private static readonly Uri Expected = new("https://api.example.com/v1");

    [TestMethod]
    public void EchoTextRequest_CreateServerUri() => Assert.AreEqual(Expected, EchoTextRequest.CreateServerUri());

    [TestMethod]
    public void CreateItemRequest_CreateServerUri() => Assert.AreEqual(Expected, CreateItemRequest.CreateServerUri());

    [TestMethod]
    public void GetItemRequest_CreateServerUri() => Assert.AreEqual(Expected, GetItemRequest.CreateServerUri());

    [TestMethod]
    public void DeleteItemRequest_CreateServerUri() => Assert.AreEqual(Expected, DeleteItemRequest.CreateServerUri());

    [TestMethod]
    public void UpdateItemRequest_CreateServerUri() => Assert.AreEqual(Expected, UpdateItemRequest.CreateServerUri());

    [TestMethod]
    public void PatchItemRequest_CreateServerUri() => Assert.AreEqual(Expected, PatchItemRequest.CreateServerUri());

    [TestMethod]
    public void HeadItemRequest_CreateServerUri() => Assert.AreEqual(Expected, HeadItemRequest.CreateServerUri());

    [TestMethod]
    public void CopyItemRequest_CreateServerUri() => Assert.AreEqual(Expected, CopyItemRequest.CreateServerUri());

    [TestMethod]
    public void TraceItemRequest_CreateServerUri() => Assert.AreEqual(Expected, TraceItemRequest.CreateServerUri());

    [TestMethod]
    public void PurgeItemRequest_CreateServerUri() => Assert.AreEqual(Expected, PurgeItemRequest.CreateServerUri());

    [TestMethod]
    public void OptionsItemsRequest_CreateServerUri() => Assert.AreEqual(Expected, OptionsItemsRequest.CreateServerUri());

    [TestMethod]
    public void GetItemDetailsRequest_CreateServerUri() => Assert.AreEqual(Expected, GetItemDetailsRequest.CreateServerUri());

    [TestMethod]
    public void GetItemTagRequest_CreateServerUri() => Assert.AreEqual(Expected, GetItemTagRequest.CreateServerUri());

    [TestMethod]
    public void GetOrderRequest_CreateServerUri() => Assert.AreEqual(Expected, GetOrderRequest.CreateServerUri());

    [TestMethod]
    public void UpdateOrderRequest_CreateServerUri() => Assert.AreEqual(Expected, UpdateOrderRequest.CreateServerUri());

    [TestMethod]
    public void SearchRequest_CreateServerUri() => Assert.AreEqual(Expected, SearchRequest.CreateServerUri());

    [TestMethod]
    public void SearchWithQuerystringRequest_CreateServerUri() => Assert.AreEqual(Expected, SearchWithQuerystringRequest.CreateServerUri());

    [TestMethod]
    public void QuerySearchRequest_CreateServerUri() => Assert.AreEqual(Expected, QuerySearchRequest.CreateServerUri());

    [TestMethod]
    public void GetDocumentRequest_CreateServerUri() => Assert.AreEqual(Expected, GetDocumentRequest.CreateServerUri());

    [TestMethod]
    public void UploadDocumentRequest_CreateServerUri() => Assert.AreEqual(Expected, UploadDocumentRequest.CreateServerUri());

    [TestMethod]
    public void UploadEncodedDocumentRequest_CreateServerUri() => Assert.AreEqual(Expected, UploadEncodedDocumentRequest.CreateServerUri());

    [TestMethod]
    public void UploadDocMixedRequest_CreateServerUri() => Assert.AreEqual(Expected, UploadDocMixedRequest.CreateServerUri());

    [TestMethod]
    public void DownloadFileRequest_CreateServerUri() => Assert.AreEqual(Expected, DownloadFileRequest.CreateServerUri());

    [TestMethod]
    public void DownloadMixedRequest_CreateServerUri() => Assert.AreEqual(Expected, DownloadMixedRequest.CreateServerUri());

    [TestMethod]
    public void UploadFileRequest_CreateServerUri() => Assert.AreEqual(Expected, UploadFileRequest.CreateServerUri());

    [TestMethod]
    public void GetVendorJsonRequest_CreateServerUri() => Assert.AreEqual(Expected, GetVendorJsonRequest.CreateServerUri());

    [TestMethod]
    public void GetPreferencesRequest_CreateServerUri() => Assert.AreEqual(Expected, GetPreferencesRequest.CreateServerUri());

    [TestMethod]
    public void TrackEventRequest_CreateServerUri() => Assert.AreEqual(Expected, TrackEventRequest.CreateServerUri());

    [TestMethod]
    public void ChatCompletionsRequest_CreateServerUri() => Assert.AreEqual(Expected, ChatCompletionsRequest.CreateServerUri());

    [TestMethod]
    public void StreamEventsRequest_CreateServerUri() => Assert.AreEqual(Expected, StreamEventsRequest.CreateServerUri());

    [TestMethod]
    public void TextToJsonRequest_CreateServerUri() => Assert.AreEqual(Expected, TextToJsonRequest.CreateServerUri());

    [TestMethod]
    public void GetTextOrJsonRequest_CreateServerUri() => Assert.AreEqual(Expected, GetTextOrJsonRequest.CreateServerUri());

    [TestMethod]
    public void GetByFlagRequest_CreateServerUri() => Assert.AreEqual(Expected, GetByFlagRequest.CreateServerUri());

    [TestMethod]
    public void GetPageRequest_CreateServerUri() => Assert.AreEqual(Expected, GetPageRequest.CreateServerUri());

    [TestMethod]
    public void GetSessionProfileRequest_CreateServerUri() => Assert.AreEqual(Expected, GetSessionProfileRequest.CreateServerUri());

    [TestMethod]
    public void ProcessBatchRequest_CreateServerUri() => Assert.AreEqual(Expected, ProcessBatchRequest.CreateServerUri());

    [TestMethod]
    public void SubmitContactFormRequest_CreateServerUri() => Assert.AreEqual(Expected, SubmitContactFormRequest.CreateServerUri());

    [TestMethod]
    public void SubmitDefaultFormRequest_CreateServerUri() => Assert.AreEqual(Expected, SubmitDefaultFormRequest.CreateServerUri());

    [TestMethod]
    public void SubmitEncodedContactFormRequest_CreateServerUri() => Assert.AreEqual(Expected, SubmitEncodedContactFormRequest.CreateServerUri());

    [TestMethod]
    public void SubmitNonexplodedFormRequest_CreateServerUri() => Assert.AreEqual(Expected, SubmitNonexplodedFormRequest.CreateServerUri());

    [TestMethod]
    public void SubmitMultipartTypesRequest_CreateServerUri() => Assert.AreEqual(Expected, SubmitMultipartTypesRequest.CreateServerUri());

    [TestMethod]
    public void HeaderArrayRequest_CreateServerUri() => Assert.AreEqual(Expected, HeaderArrayRequest.CreateServerUri());

    [TestMethod]
    public void HeaderNestedArrayRequest_CreateServerUri() => Assert.AreEqual(Expected, HeaderNestedArrayRequest.CreateServerUri());

    [TestMethod]
    public void HeaderObjectRequest_CreateServerUri() => Assert.AreEqual(Expected, HeaderObjectRequest.CreateServerUri());

    [TestMethod]
    public void HeaderObjectExplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, HeaderObjectExplodeRequest.CreateServerUri());

    [TestMethod]
    public void PathArraySimpleRequest_CreateServerUri() => Assert.AreEqual(Expected, PathArraySimpleRequest.CreateServerUri());

    [TestMethod]
    public void PathArrayLabelRequest_CreateServerUri() => Assert.AreEqual(Expected, PathArrayLabelRequest.CreateServerUri());

    [TestMethod]
    public void PathArrayMatrixRequest_CreateServerUri() => Assert.AreEqual(Expected, PathArrayMatrixRequest.CreateServerUri());

    [TestMethod]
    public void PathObjectSimpleRequest_CreateServerUri() => Assert.AreEqual(Expected, PathObjectSimpleRequest.CreateServerUri());

    [TestMethod]
    public void PathObjectSimpleExplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, PathObjectSimpleExplodeRequest.CreateServerUri());

    [TestMethod]
    public void PathObjectLabelRequest_CreateServerUri() => Assert.AreEqual(Expected, PathObjectLabelRequest.CreateServerUri());

    [TestMethod]
    public void PathObjectLabelExplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, PathObjectLabelExplodeRequest.CreateServerUri());

    [TestMethod]
    public void PathObjectMatrixRequest_CreateServerUri() => Assert.AreEqual(Expected, PathObjectMatrixRequest.CreateServerUri());

    [TestMethod]
    public void PathObjectMatrixExplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, PathObjectMatrixExplodeRequest.CreateServerUri());

    [TestMethod]
    public void QueryArrayExplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryArrayExplodeRequest.CreateServerUri());

    [TestMethod]
    public void QueryArrayNonexplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryArrayNonexplodeRequest.CreateServerUri());

    [TestMethod]
    public void QueryArraySpaceRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryArraySpaceRequest.CreateServerUri());

    [TestMethod]
    public void QueryArrayPipeRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryArrayPipeRequest.CreateServerUri());

    [TestMethod]
    public void QueryObjectExplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryObjectExplodeRequest.CreateServerUri());

    [TestMethod]
    public void QueryObjectNonexplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryObjectNonexplodeRequest.CreateServerUri());

    [TestMethod]
    public void QueryObjectDeepRequest_CreateServerUri() => Assert.AreEqual(Expected, QueryObjectDeepRequest.CreateServerUri());

    [TestMethod]
    public void CookieArrayRequest_CreateServerUri() => Assert.AreEqual(Expected, CookieArrayRequest.CreateServerUri());

    [TestMethod]
    public void CookieArrayNonexplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, CookieArrayNonexplodeRequest.CreateServerUri());

    [TestMethod]
    public void CookieObjectRequest_CreateServerUri() => Assert.AreEqual(Expected, CookieObjectRequest.CreateServerUri());

    [TestMethod]
    public void CookieObjectNonexplodeRequest_CreateServerUri() => Assert.AreEqual(Expected, CookieObjectNonexplodeRequest.CreateServerUri());
}