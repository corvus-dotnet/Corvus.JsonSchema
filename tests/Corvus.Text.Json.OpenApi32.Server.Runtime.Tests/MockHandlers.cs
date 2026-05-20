// <copyright file="MockHandlers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Server;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Mock implementation of <see cref="IApiDefaultHandler"/> for testing.
/// Returns canned responses for each operation.
/// </summary>
internal sealed class MockDefaultHandler : IApiDefaultHandler
{
    private static readonly ItemEntity DefaultItem = ItemEntity.ParseValue("""{"id":"1","name":"Widget","price":9.99}"""u8);

    public ValueTask<ListItemsResult> HandleListItemsAsync(ListItemsParams parameters, CancellationToken cancellationToken = default)
    {
        GetItemsOk body = GetItemsOk.ParseValue("""[{"id":"1","name":"Test Item","price":10}]"""u8);
        return new(ListItemsResult.Ok(body));
    }

    public ValueTask<CreateItemResult> HandleCreateItemAsync(CreateItemParams parameters, CancellationToken cancellationToken = default)
    {
        PostItemsCreated body = PostItemsCreated.ParseValue("""{"id":"new-1","name":"Created"}"""u8);
        return new(CreateItemResult.Created(body));
    }

    public ValueTask<OptionsItemsResult> HandleOptionsItemsAsync(OptionsItemsParams parameters, CancellationToken cancellationToken = default)
        => new(OptionsItemsResult.NoContent());

    public ValueTask<PurgeItemsResult> HandlePurgeItemsAsync(PurgeItemsParams parameters, CancellationToken cancellationToken = default)
        => new(PurgeItemsResult.NoContent());

    public ValueTask<GetItemResult> HandleGetItemAsync(GetItemParams parameters, CancellationToken cancellationToken = default)
    {
        string itemId = (string)parameters.ItemId;
        ItemEntity body = ItemEntity.ParseValue(System.Text.Encoding.UTF8.GetBytes($$"""{"id":"{{itemId}}","name":"Widget","price":9.99}"""));
        return new(GetItemResult.Ok(body));
    }

    public ValueTask<PatchItemsItemIdResult> HandlePatchItemsItemIdAsync(PatchItemsItemIdParams parameters, CancellationToken cancellationToken = default)
        => new(PatchItemsItemIdResult.Ok(DefaultItem));

    public ValueTask<UpdateItemFormResult> HandleUpdateItemFormAsync(UpdateItemFormParams parameters, CancellationToken cancellationToken = default)
        => new(UpdateItemFormResult.Ok(DefaultItem));

    public ValueTask<UploadItemDataResult> HandleUploadItemDataAsync(UploadItemDataParams parameters, CancellationToken cancellationToken = default)
        => new(UploadItemDataResult.Created(DefaultItem));

    public ValueTask<DownloadFileResult> HandleDownloadFileAsync(DownloadFileParams parameters, CancellationToken cancellationToken = default)
        => new(DownloadFileResult.Ok());

    public ValueTask<GetQuirkyResult> HandleGetQuirkyAsync(GetQuirkyParams parameters, CancellationToken cancellationToken = default)
        => new(GetQuirkyResult.Ok(DefaultItem));

    public ValueTask<GetStyledQuirkyResult> HandleGetStyledQuirkyAsync(GetStyledQuirkyParams parameters, CancellationToken cancellationToken = default)
        => new(GetStyledQuirkyResult.Ok(DefaultItem));

    public ValueTask<ExportDataResult> HandleExportDataAsync(ExportDataParams parameters, CancellationToken cancellationToken = default)
        => new(ExportDataResult.Ok());

    public ValueTask<GetEmptyServersResult> HandleGetEmptyServersAsync(GetEmptyServersParams parameters, CancellationToken cancellationToken = default)
    {
        GetEmptyServersOk body = GetEmptyServersOk.ParseValue("""[]"""u8);
        return new(GetEmptyServersResult.Ok(body));
    }

    public ValueTask<HeadHealthResult> HandleHeadHealthAsync(HeadHealthParams parameters, CancellationToken cancellationToken = default)
        => new(HeadHealthResult.Ok());

    public ValueTask<TraceHealthResult> HandleTraceHealthAsync(TraceHealthParams parameters, CancellationToken cancellationToken = default)
        => new(TraceHealthResult.Ok());

    public ValueTask<GetAdvancedStylesResult> HandleGetAdvancedStylesAsync(GetAdvancedStylesParams parameters, CancellationToken cancellationToken = default)
    {
        GetAdvancedStylesByIdsOk body = GetAdvancedStylesByIdsOk.ParseValue("""[]"""u8);
        return new(GetAdvancedStylesResult.Ok(body));
    }

    public ValueTask<GetByMatrixCodesResult> HandleGetByMatrixCodesAsync(GetByMatrixCodesParams parameters, CancellationToken cancellationToken = default)
    {
        GetMatrixTestByCodesOk body = GetMatrixTestByCodesOk.ParseValue("""[]"""u8);
        return new(GetByMatrixCodesResult.Ok(body));
    }

    public ValueTask<GetByMatrixTagsResult> HandleGetByMatrixTagsAsync(GetByMatrixTagsParams parameters, CancellationToken cancellationToken = default)
    {
        GetMatrixNoExplodeByTagsOk body = GetMatrixNoExplodeByTagsOk.ParseValue("""[]"""u8);
        return new(GetByMatrixTagsResult.Ok(body));
    }

    public ValueTask<GetByLabelItemsResult> HandleGetByLabelItemsAsync(GetByLabelItemsParams parameters, CancellationToken cancellationToken = default)
    {
        GetLabelNoExplodeByItemsOk body = GetLabelNoExplodeByItemsOk.ParseValue("""[]"""u8);
        return new(GetByLabelItemsResult.Ok(body));
    }

    public ValueTask<GetByStyledObjectResult> HandleGetByStyledObjectAsync(GetByStyledObjectParams parameters, CancellationToken cancellationToken = default)
    {
        GetStyledObjectByObjOk body = GetStyledObjectByObjOk.ParseValue("""{}"""u8);
        return new(GetByStyledObjectResult.Ok(body));
    }

    public ValueTask<QueryItemsResult> HandleQueryItemsAsync(QueryItemsParams parameters, CancellationToken cancellationToken = default)
    {
        Schema1 body = Schema1.ParseValue("""[]"""u8);
        return new(QueryItemsResult.Ok(body));
    }

    public ValueTask<GetResourceResult> HandleGetResourceAsync(GetResourceParams parameters, CancellationToken cancellationToken = default)
        => new(GetResourceResult.Ok(DefaultItem));

    public ValueTask<CopyResourceResult> HandleCopyResourceAsync(CopyResourceParams parameters, CancellationToken cancellationToken = default)
        => new(CopyResourceResult.Created(DefaultItem));

    public ValueTask<PurgeResourceResult> HandlePurgeResourceAsync(PurgeResourceParams parameters, CancellationToken cancellationToken = default)
        => new(PurgeResourceResult.NoContent());

    public ValueTask<MoveResourceResult> HandleMoveResourceAsync(MoveResourceParams parameters, CancellationToken cancellationToken = default)
        => new(MoveResourceResult.Ok());

    public ValueTask<StreamEventsResult> HandleStreamEventsAsync(StreamEventsParams parameters, CancellationToken cancellationToken = default)
        => new(StreamEventsResult.Ok());

    public ValueTask<SearchWithQuerystringResult> HandleSearchWithQuerystringAsync(SearchWithQuerystringParams parameters, CancellationToken cancellationToken = default)
    {
        GetSearchQsOk body = GetSearchQsOk.ParseValue("""{"results":[]}"""u8);
        return new(SearchWithQuerystringResult.Ok(body));
    }

    public ValueTask<GetDocumentResult> HandleGetDocumentAsync(GetDocumentParams parameters, CancellationToken cancellationToken = default)
    {
        Schema3 body = Schema3.ParseValue("""{}"""u8);
        return new(GetDocumentResult.Ok(body));
    }

    public ValueTask<UpdateDocumentResult> HandleUpdateDocumentAsync(UpdateDocumentParams parameters, CancellationToken cancellationToken = default)
    {
        Schema3 body = Schema3.ParseValue("""{}"""u8);
        return new(UpdateDocumentResult.Ok(body));
    }

    public ValueTask<UploadRawFileResult> HandleUploadRawFileAsync(UploadRawFileParams parameters, CancellationToken cancellationToken = default)
    {
        PostUploadRawCreated body = PostUploadRawCreated.ParseValue("""{"url":"https://example.com/file.bin"}"""u8);
        return new(UploadRawFileResult.Created(body));
    }

    public ValueTask<GetResourceVersionResult> HandleGetResourceVersionAsync(GetResourceVersionParams parameters, CancellationToken cancellationToken = default)
    {
        Schema5 body = Schema5.ParseValue("""{"version":"1.0"}"""u8);
        return new(GetResourceVersionResult.Ok(body));
    }

    public ValueTask<GetMonitoringStatusResult> HandleGetMonitoringStatusAsync(GetMonitoringStatusParams parameters, CancellationToken cancellationToken = default)
    {
        GetMonitoringStatusOk body = GetMonitoringStatusOk.ParseValue("""{"status":"ok"}"""u8);
        return new(GetMonitoringStatusResult.Ok(body));
    }

    public ValueTask<PutMonitoringStatusResult> HandlePutMonitoringStatusAsync(PutMonitoringStatusParams parameters, CancellationToken cancellationToken = default)
    {
        PutMonitoringStatusOk body = PutMonitoringStatusOk.ParseValue("""{"status":"updated"}"""u8);
        return new(PutMonitoringStatusResult.Ok(body));
    }

    public ValueTask<PostMonitoringStatusResult> HandlePostMonitoringStatusAsync(PostMonitoringStatusParams parameters, CancellationToken cancellationToken = default)
    {
        PostMonitoringStatusCreated body = PostMonitoringStatusCreated.ParseValue("""{"status":"created"}"""u8);
        return new(PostMonitoringStatusResult.Created(body));
    }

    public ValueTask<DeleteMonitoringStatusResult> HandleDeleteMonitoringStatusAsync(DeleteMonitoringStatusParams parameters, CancellationToken cancellationToken = default)
        => new(DeleteMonitoringStatusResult.NoContent());

    public ValueTask<QueryMonitoringStatusResult> HandleQueryMonitoringStatusAsync(QueryMonitoringStatusParams parameters, CancellationToken cancellationToken = default)
    {
        Schema7 body = Schema7.ParseValue("""[]"""u8);
        return new(QueryMonitoringStatusResult.Ok(body));
    }
}

/// <summary>
/// Mock implementation of <see cref="IApiItemsHandler"/> for testing.
/// </summary>
internal sealed class MockItemsHandler : IApiItemsHandler
{
    private static readonly ItemEntity DefaultItem = ItemEntity.ParseValue("""{"id":"1","name":"Widget","price":9.99}"""u8);

    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, CancellationToken cancellationToken = default)
    {
        GetSearchOk body = GetSearchOk.ParseValue("""{"results":[{"id":"1","name":"Found Item"}]}"""u8);
        return new(SearchItemsResult.Ok(body));
    }

    public ValueTask<UploadFileResult> HandleUploadFileAsync(UploadFileParams parameters, CancellationToken cancellationToken = default)
        => new(UploadFileResult.Created(DefaultItem));

    public ValueTask<SubmitFeedbackResult> HandleSubmitFeedbackAsync(SubmitFeedbackParams parameters, CancellationToken cancellationToken = default)
        => new(SubmitFeedbackResult.Created(DefaultItem));

    public ValueTask<UploadAttachmentResult> HandleUploadAttachmentAsync(UploadAttachmentParams parameters, CancellationToken cancellationToken = default)
        => new(UploadAttachmentResult.Created(DefaultItem));

    public ValueTask<SubmitFeedbackEncodedResult> HandleSubmitFeedbackEncodedAsync(SubmitFeedbackEncodedParams parameters, CancellationToken cancellationToken = default)
        => new(SubmitFeedbackEncodedResult.Created(DefaultItem));

    public ValueTask<UploadAttachmentEncodedResult> HandleUploadAttachmentEncodedAsync(UploadAttachmentEncodedParams parameters, CancellationToken cancellationToken = default)
        => new(UploadAttachmentEncodedResult.Created(DefaultItem));
}

/// <summary>
/// Mock implementation of <see cref="IApiSearchHandler"/> for testing.
/// </summary>
internal sealed class MockSearchHandler : IApiSearchHandler
{
    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, CancellationToken cancellationToken = default)
    {
        GetSearchOk body = GetSearchOk.ParseValue("""{"results":[{"id":"s1","name":"Search Result"}]}"""u8);
        return new(SearchItemsResult.Ok(body));
    }

    public ValueTask<SearchItemsAdvancedResult> HandleSearchItemsAdvancedAsync(SearchItemsAdvancedParams parameters, CancellationToken cancellationToken = default)
    {
        PostSearchOk body = PostSearchOk.ParseValue("""{"results":[{"id":"a1","name":"Advanced Result"}]}"""u8);
        return new(SearchItemsAdvancedResult.Ok(body));
    }
}