using System.Globalization;
using System.Text;
using CanonTests31.Server;
using CanonTests31.Server.Models;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi31.Server.Runtime.Tests;

internal sealed class MockDefaultHandler : IApiDefaultHandler
{
    private static readonly ItemEntity DefaultItem = MockResponseFactory.CreateItem(1, "Widget");
    private static readonly PostItemsCreated CreatedItemBody = PostItemsCreated.ParseValue("""{"id":"new-1","name":"Created"}"""u8);
    private static readonly JsonInt32 RateLimitHeader = JsonInt32.ParseValue("100"u8);
    private static readonly JsonBoolean ActiveHeader = JsonBoolean.ParseValue("true"u8);
    private static readonly JsonElement RequestIdHeader = JsonElement.ParseValue(Encoding.UTF8.GetBytes("\"req-123\""));
    private static readonly GetItemsByItemIdOkXTags TagsHeader = GetItemsByItemIdOkXTags.ParseValue("""["alpha","beta"]"""u8);
    private static readonly GetItemsByItemIdOkXPageSizes PageSizesHeader = GetItemsByItemIdOkXPageSizes.ParseValue("""[10,25]"""u8);
    private static readonly GetItemsByItemIdOkXFlags FlagsHeader = GetItemsByItemIdOkXFlags.ParseValue("""[true,false]"""u8);
    private static readonly GetEmptyServersOk EmptyServersBody = GetEmptyServersOk.ParseValue("""{"ok":true}"""u8);
    private static readonly Schema HealthBody = Schema.ParseValue("""{"status":"ok"}"""u8);
    private static readonly GetAdvancedStylesByIdsOk AdvancedStylesBody = GetAdvancedStylesByIdsOk.ParseValue("""{"items":[],"total":0}"""u8);
    private static readonly GetMatrixTestByCodesOk MatrixCodesBody = GetMatrixTestByCodesOk.ParseValue("""{"count":0}"""u8);
    private static readonly GetMatrixNoExplodeByTagsOk MatrixTagsBody = GetMatrixNoExplodeByTagsOk.ParseValue("""{"count":0}"""u8);
    private static readonly GetLabelNoExplodeByItemsOk LabelItemsBody = GetLabelNoExplodeByItemsOk.ParseValue("""{"count":0}"""u8);
    private static readonly GetStyledObjectByObjOk StyledObjectBody = GetStyledObjectByObjOk.ParseValue("""{}"""u8);

    internal static bool ReturnInvalidResponse { get; set; }

    public ValueTask<ListItemsResult> HandleListItemsAsync(ListItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetItemsOk body = ReturnInvalidResponse
            ? GetItemsOk.ParseValue("""[{"missing":"fields"}]"""u8)
            : MockResponseFactory.CreateListItemsBody(parameters);
        return new(ListItemsResult.Ok(body, workspace));
    }

    public ValueTask<CreateItemResult> HandleCreateItemAsync(CreateItemParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostItemsCreated body = ReturnInvalidResponse
            ? PostItemsCreated.ParseValue("""{}"""u8)
            : CreatedItemBody;
        return new(CreateItemResult.Created(body, workspace));
    }

    public ValueTask<GetItemResult> HandleGetItemAsync(GetItemParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ItemEntity body = ReturnInvalidResponse
            ? ItemEntity.ParseValue("""{"broken":true}"""u8)
            : MockResponseFactory.CreateItem(42, $"Widget-{parameters.ItemId}");
        return new(GetItemResult.Ok(body, workspace, RateLimitHeader, ActiveHeader, RequestIdHeader, TagsHeader, PageSizesHeader, FlagsHeader));
    }

    public ValueTask<UpdateItemFormResult> HandleUpdateItemFormAsync(UpdateItemFormParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = parameters.Body.Name.IsUndefined() ? "Updated" : parameters.Body.Name.ToString();
        return new(UpdateItemFormResult.Ok(MockResponseFactory.CreateItemWithPayload(7, name, parameters.Body.ToString()), workspace));
    }

    public ValueTask<UploadItemDataResult> HandleUploadItemDataAsync(UploadItemDataParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = parameters.Body.Description.IsUndefined() ? "Uploaded" : parameters.Body.Description.ToString();
        ItemEntity body = ReturnInvalidResponse
            ? ItemEntity.ParseValue("""{}"""u8)
            : MockResponseFactory.CreateItem(8, name);
        return new(UploadItemDataResult.Created(body, workspace));
    }

    public ValueTask<DownloadFileResult> HandleDownloadFileAsync(DownloadFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(DownloadFileResult.Ok("file-content"u8.ToArray()));

    public ValueTask<GetQuirkyResult> HandleGetQuirkyAsync(GetQuirkyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetQuirkyResult.Ok(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<GetStyledQuirkyResult> HandleGetStyledQuirkyAsync(GetStyledQuirkyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetStyledQuirkyResult.Ok(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<ExportDataResult> HandleExportDataAsync(ExportDataParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(ExportDataResult.Ok("export-data"u8.ToArray()));

    public ValueTask<GetEmptyServersResult> HandleGetEmptyServersAsync(GetEmptyServersParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetEmptyServersResult.Ok(ReturnInvalidResponse ? GetEmptyServersOk.ParseValue("""[]"""u8) : EmptyServersBody, workspace));

    public ValueTask<HealthCheckResult> HandleHealthCheckAsync(HealthCheckParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(HealthCheckResult.Ok(HealthBody, workspace));

    public ValueTask<GetAdvancedStylesResult> HandleGetAdvancedStylesAsync(GetAdvancedStylesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetAdvancedStylesResult.Ok(ReturnInvalidResponse ? GetAdvancedStylesByIdsOk.ParseValue("""[]"""u8) : AdvancedStylesBody, workspace));

    public ValueTask<GetByMatrixCodesResult> HandleGetByMatrixCodesAsync(GetByMatrixCodesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetByMatrixCodesResult.Ok(ReturnInvalidResponse ? GetMatrixTestByCodesOk.ParseValue("""[]"""u8) : MatrixCodesBody, workspace));

    public ValueTask<GetByMatrixTagsResult> HandleGetByMatrixTagsAsync(GetByMatrixTagsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetByMatrixTagsResult.Ok(ReturnInvalidResponse ? GetMatrixNoExplodeByTagsOk.ParseValue("""[]"""u8) : MatrixTagsBody, workspace));

    public ValueTask<GetByLabelItemsResult> HandleGetByLabelItemsAsync(GetByLabelItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetByLabelItemsResult.Ok(ReturnInvalidResponse ? GetLabelNoExplodeByItemsOk.ParseValue("""[]"""u8) : LabelItemsBody, workspace));

    public ValueTask<GetByStyledObjectResult> HandleGetByStyledObjectAsync(GetByStyledObjectParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetByStyledObjectResult.Ok(ReturnInvalidResponse ? GetStyledObjectByObjOk.ParseValue("""[]"""u8) : StyledObjectBody, workspace));
}

internal sealed class MockItemsHandler : IApiItemsHandler
{
    private static readonly ItemEntity DefaultItem = MockResponseFactory.CreateItem(1, "Widget");

    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetSearchOk body = MockDefaultHandler.ReturnInvalidResponse
            ? GetSearchOk.ParseValue("""{}"""u8)
            : MockResponseFactory.CreateSearchItemsBody(parameters);
        return new(SearchItemsResult.Ok(body, workspace));
    }

    public ValueTask<UploadFileResult> HandleUploadFileAsync(UploadFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadFileResult.Created(MockDefaultHandler.ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<SubmitFeedbackResult> HandleSubmitFeedbackAsync(SubmitFeedbackParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        long id = long.Parse(parameters.Body.Rating.ToString(), CultureInfo.InvariantCulture);
        string name = parameters.Body.Comment.IsUndefined() ? "Feedback" : parameters.Body.Comment.ToString();
        return new(SubmitFeedbackResult.Created(MockResponseFactory.CreateItemWithPayload(id, name, parameters.Body.ToString()), workspace));
    }

    public ValueTask<UploadAttachmentResult> HandleUploadAttachmentAsync(UploadAttachmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadAttachmentResult.Created(MockDefaultHandler.ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<SubmitFeedbackEncodedResult> HandleSubmitFeedbackEncodedAsync(SubmitFeedbackEncodedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        long id = parameters.Body.Tags.GetArrayLength();
        string name = parameters.Body.Comment.IsUndefined() ? parameters.Body.Tags[0].ToString() : parameters.Body.Comment.ToString();
        return new(SubmitFeedbackEncodedResult.Created(MockResponseFactory.CreateItemWithPayload(id, name, parameters.Body.ToString()), workspace));
    }

    public ValueTask<UploadAttachmentEncodedResult> HandleUploadAttachmentEncodedAsync(UploadAttachmentEncodedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadAttachmentEncodedResult.Created(MockDefaultHandler.ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));
}

internal sealed class MockSearchHandler : IApiSearchHandler
{
    private static readonly PostSearchOk SearchItemsAdvancedBody = PostSearchOk.ParseValue("""{"results":[{"id":1,"name":"Advanced Result"}]}"""u8);

    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(SearchItemsResult.Ok(MockResponseFactory.CreateSearchItemsBody(parameters), workspace));

    public ValueTask<SearchItemsAdvancedResult> HandleSearchItemsAdvancedAsync(SearchItemsAdvancedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(SearchItemsAdvancedResult.Ok(MockDefaultHandler.ReturnInvalidResponse ? PostSearchOk.ParseValue("""[]"""u8) : SearchItemsAdvancedBody, workspace));
}

internal static class MockResponseFactory
{
    public static ItemEntity CreateItem(long id, string name)
        => ItemEntity.ParseValue(Encoding.UTF8.GetBytes($$"""{"id":{{id}},"name":"{{EscapeJsonString(name)}}"}"""));

    public static ItemEntity CreateItemWithPayload(long id, string name, string payloadJson)
        => ItemEntity.ParseValue(Encoding.UTF8.GetBytes($$"""{"id":{{id}},"name":"{{EscapeJsonString(name)}}","payload":{{payloadJson}}}"""));

    public static GetItemsOk CreateListItemsBody(ListItemsParams parameters)
        => GetItemsOk.ParseValue(Encoding.UTF8.GetBytes($$"""[{"id":1,"name":"Test Item","payload":{{BuildListItemsPayload(parameters)}}}]"""));

    public static GetSearchOk CreateSearchItemsBody(SearchItemsParams parameters)
    {
        string name = parameters.Q.IsUndefined() ? "undefined" : parameters.Q.ToString();
        return GetSearchOk.ParseValue(Encoding.UTF8.GetBytes($$"""[{"id":1,"name":"{{EscapeJsonString(name)}}","payload":{{BuildSearchItemsPayload(parameters)}}}]"""));
    }

    private static string BuildListItemsPayload(ListItemsParams parameters)
    {
        StringBuilder builder = new();
        builder.Append('{');
        bool hasProperty = false;

        if (!parameters.Active.IsUndefined())
        {
            AppendRawProperty(builder, ref hasProperty, "active", ((bool)parameters.Active) ? "true" : "false");
        }

        if (!parameters.Category.IsUndefined())
        {
            AppendStringProperty(builder, ref hasProperty, "category", parameters.Category.ToString());
        }

        if (!parameters.Page.IsUndefined())
        {
            AppendRawProperty(builder, ref hasProperty, "page", parameters.Page.ToString());
        }

        if (!parameters.Sort.IsUndefined())
        {
            AppendStringProperty(builder, ref hasProperty, "sort", parameters.Sort.ToString());
        }

        if (!parameters.Verbose.IsUndefined())
        {
            AppendRawProperty(builder, ref hasProperty, "verbose", ((bool)parameters.Verbose) ? "true" : "false");
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string BuildSearchItemsPayload(SearchItemsParams parameters)
    {
        StringBuilder builder = new();
        builder.Append('{');
        bool hasProperty = false;

        if (!parameters.Q.IsUndefined())
        {
            AppendStringProperty(builder, ref hasProperty, "q", parameters.Q.ToString());
        }

        if (!parameters.Tags.IsUndefined())
        {
            AppendRawProperty(builder, ref hasProperty, "tags", parameters.Tags.ToString());
        }

        if (!parameters.Coords.IsUndefined())
        {
            AppendRawProperty(builder, ref hasProperty, "coords", parameters.Coords.ToString());
        }

        if (!parameters.Filter.IsUndefined() && parameters.Filter.GetPropertyCount() > 0)
        {
            AppendRawProperty(builder, ref hasProperty, "filter", parameters.Filter.ToString());
        }

        if (!parameters.Session.IsUndefined())
        {
            AppendStringProperty(builder, ref hasProperty, "session", parameters.Session.ToString());
        }

        if (!parameters.Prefs.IsUndefined())
        {
            AppendStringProperty(builder, ref hasProperty, "prefs", parameters.Prefs.ToString());
        }

        if (!parameters.XCorrelationId.IsUndefined())
        {
            AppendStringProperty(builder, ref hasProperty, "xCorrelationId", parameters.XCorrelationId.ToString());
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendRawProperty(StringBuilder builder, ref bool hasProperty, string name, string jsonValue)
    {
        if (hasProperty)
        {
            builder.Append(',');
        }

        builder.Append('"');
        builder.Append(name);
        builder.Append("\":");
        builder.Append(jsonValue);
        hasProperty = true;
    }

    private static void AppendStringProperty(StringBuilder builder, ref bool hasProperty, string name, string value)
        => AppendRawProperty(builder, ref hasProperty, name, $"\"{EscapeJsonString(value)}\"");

    private static string EscapeJsonString(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
}