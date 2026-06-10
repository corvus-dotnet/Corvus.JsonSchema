// <copyright file="PetByIdApi.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Tests.Fakes;

/// <summary>
/// A minimal generated-style request (<c>GET /pets/{petId}</c>) for the end-to-end executor test,
/// shaped like the OpenAPI generator's output but using <see cref="JsonElement"/> for the parameter.
/// </summary>
public readonly struct PetByIdRequest : IApiRequest<PetByIdRequest>
{
    public PetByIdRequest(JsonElement petId)
    {
        this.PetId = petId;
    }

    /// <summary>Gets the petId path parameter (mirrors the generator's init-settable request property).</summary>
    public JsonElement PetId { get; init; }

    public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{petId}"u8;

    public static OperationMethod Method => OperationMethod.Get;

    public static bool HasPathParameters => true;

    public static bool HasQueryParameters => false;

    public static bool HasHeaderParameters => false;

    public static bool HasCookieParameters => false;

    public void WriteResolvedPath(IBufferWriter<byte> writer)
    {
        writer.Write("/pets/"u8);
        string id = this.PetId.ValueKind == JsonValueKind.String ? this.PetId.GetString()! : this.PetId.GetRawText();
        int count = Encoding.UTF8.GetByteCount(id);
        Span<byte> destination = writer.GetSpan(count);
        writer.Advance(Encoding.UTF8.GetBytes(id, destination));
    }

    public int WriteQueryString(IBufferWriter<byte> writer) => 0;

    public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
    {
    }

    public int WriteCookies(IBufferWriter<byte> writer) => 0;

    public void Validate(ValidationMode mode = ValidationMode.Basic)
    {
    }
}

/// <summary>
/// A minimal generated-style response for the end-to-end executor test: it parses the JSON body into
/// <see cref="OkBody"/> (the 200 accessor the generator would emit).
/// </summary>
public struct PetByIdResponse : IApiResponse<PetByIdResponse>
{
    private ParsedJsonDocument<JsonElement>? bodyDocument;
    private IResponseHeaders? responseHeaders;

    public int StatusCode { get; private set; }

    public JsonElement OkBody { get; private set; }

    public readonly bool IsSuccess => this.StatusCode is >= 200 and < 300;

    /// <summary>Gets the schema-less <c>X-Flag</c> response header (mirrors the generator's untyped header property).</summary>
    public readonly string? XFlagHeader
        => this.responseHeaders is { } headers && headers.TryGetValue("X-Flag", out string? value) ? value : null;

    public static async ValueTask<PetByIdResponse> CreateAsync(
        int statusCode,
        Stream contentStream,
        string? contentType = null,
        IResponseHeaders? responseHeaders = null,
        IAsyncDisposable? owner = null,
        IApiTransport? transport = null,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await contentStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var response = new PetByIdResponse { StatusCode = statusCode, responseHeaders = responseHeaders };
        if (buffer.Length > 0)
        {
            response.bodyDocument = ParsedJsonDocument<JsonElement>.Parse(buffer.ToArray());
            response.OkBody = response.bodyDocument.RootElement;
        }

        if (owner is not null)
        {
            await owner.DisposeAsync().ConfigureAwait(false);
        }

        return response;
    }

    public readonly void Validate(ValidationMode mode = ValidationMode.Basic)
    {
    }

    public ValueTask DisposeAsync()
    {
        this.bodyDocument?.Dispose();
        this.bodyDocument = null;
        return default;
    }
}

/// <summary>
/// A minimal generated-style request (<c>POST /pets</c>) with a JSON body, for the end-to-end
/// request-body test.
/// </summary>
public readonly struct CreatePetRequest : IApiRequest<CreatePetRequest>
{
    public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets"u8;

    public static OperationMethod Method => OperationMethod.Post;

    public static bool HasPathParameters => false;

    public static bool HasQueryParameters => false;

    public static bool HasHeaderParameters => false;

    public static bool HasCookieParameters => false;

    public void WriteResolvedPath(IBufferWriter<byte> writer) => writer.Write("/pets"u8);

    public int WriteQueryString(IBufferWriter<byte> writer) => 0;

    public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
    {
    }

    public int WriteCookies(IBufferWriter<byte> writer) => 0;

    public void Validate(ValidationMode mode = ValidationMode.Basic)
    {
    }
}

/// <summary>
/// A minimal generated-style client (<c>POST /pets</c>) whose method takes a JSON <c>body</c>
/// parameter, mirroring the real generator's signature. It records the body it received so the
/// end-to-end test can assert the executor resolved and bound the request body.
/// </summary>
public sealed class CreatePetClient(IApiTransport transport)
{
    private readonly IApiTransport transport = transport ?? throw new ArgumentNullException(nameof(transport));

    /// <summary>Gets the raw JSON of each body the client was called with.</summary>
    public static List<string> CapturedBodies { get; } = [];

    public ValueTask<PetByIdResponse> CreatePetAsync(
        JsonElement.Source body,
        CancellationToken cancellationToken = default,
        ValidationMode validationMode = ValidationMode.Basic,
        ValidationMode responseValidationMode = ValidationMode.None)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        JsonElement bodyValue = JsonElement.CreateBuilder(workspace, body).RootElement;
        CapturedBodies.Add(bodyValue.GetRawText());
        var request = default(CreatePetRequest);
        request.Validate(validationMode);
        return SendCore(this.transport, workspace, request, cancellationToken);
    }

    private static async ValueTask<PetByIdResponse> SendCore(IApiTransport transport, JsonWorkspace workspace, CreatePetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await transport.SendAsync<CreatePetRequest, PetByIdResponse>(in request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
    }
}

/// <summary>
/// A minimal generated-style client (<c>GET /pets/{petId}</c>) for the end-to-end executor test,
/// shaped like the OpenAPI generator's output: it owns the protocol (builds and validates the
/// request, sends it via the transport, returns the typed response), so the executor only calls the
/// method.
/// </summary>
public sealed class PetByIdClient(IApiTransport transport)
{
    private readonly IApiTransport transport = transport ?? throw new ArgumentNullException(nameof(transport));

    public ValueTask<PetByIdResponse> GetPetAsync(
        JsonElement.Source petId,
        CancellationToken cancellationToken = default,
        ValidationMode validationMode = ValidationMode.Basic,
        ValidationMode responseValidationMode = ValidationMode.None)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        JsonElement petIdValue = JsonElement.CreateBuilder(workspace, petId).RootElement;
        var request = new PetByIdRequest(petIdValue);
        request.Validate(validationMode);
        return SendCore(this.transport, workspace, request, cancellationToken);
    }

    private static async ValueTask<PetByIdResponse> SendCore(IApiTransport transport, JsonWorkspace workspace, PetByIdRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await transport.SendAsync<PetByIdRequest, PetByIdResponse>(in request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
    }
}
/// <summary>
/// A generated-style request (<c>GET /pets/{petId}?status=</c>) with both a path and a query parameter,
/// for exercising the <c>$url</c> path's query handling.
/// </summary>
public readonly struct SearchPetsRequest : IApiRequest<SearchPetsRequest>
{
    /// <summary>Gets the petId path parameter.</summary>
    public JsonElement PetId { get; init; }

    /// <summary>Gets the status query parameter.</summary>
    public JsonElement Status { get; init; }

    public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{petId}"u8;

    public static OperationMethod Method => OperationMethod.Get;

    public static bool HasPathParameters => true;

    public static bool HasQueryParameters => true;

    public static bool HasHeaderParameters => false;

    public static bool HasCookieParameters => false;

    public void WriteResolvedPath(IBufferWriter<byte> writer)
    {
        writer.Write("/pets/"u8);
        WriteUtf8(writer, this.PetId.ValueKind == JsonValueKind.String ? this.PetId.GetString()! : this.PetId.GetRawText());
    }

    public int WriteQueryString(IBufferWriter<byte> writer)
    {
        if (this.Status.ValueKind != JsonValueKind.String)
        {
            return 0;
        }

        writer.Write("status="u8);
        string status = this.Status.GetString()!;
        WriteUtf8(writer, status);
        return 7 + Encoding.UTF8.GetByteCount(status);
    }

    public void WriteHeaders<TState>(HeaderCallback<TState> callback, TState state)
    {
    }

    public int WriteCookies(IBufferWriter<byte> writer) => 0;

    public void Validate(ValidationMode mode = ValidationMode.Basic)
    {
    }

    private static void WriteUtf8(IBufferWriter<byte> writer, string value)
    {
        int count = Encoding.UTF8.GetByteCount(value);
        Span<byte> destination = writer.GetSpan(count);
        writer.Advance(Encoding.UTF8.GetBytes(value, destination));
    }
}

/// <summary>
/// A generated-style client for <see cref="SearchPetsRequest"/> (<c>GET /pets/{petId}?status=</c>).
/// </summary>
public sealed class SearchPetsClient(IApiTransport transport)
{
    private readonly IApiTransport transport = transport ?? throw new ArgumentNullException(nameof(transport));

    public ValueTask<PetByIdResponse> SearchPetsAsync(
        JsonElement.Source petId,
        JsonElement.Source status,
        CancellationToken cancellationToken = default,
        ValidationMode validationMode = ValidationMode.Basic,
        ValidationMode responseValidationMode = ValidationMode.None)
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        var request = new SearchPetsRequest
        {
            PetId = JsonElement.CreateBuilder(workspace, petId).RootElement,
            Status = JsonElement.CreateBuilder(workspace, status).RootElement,
        };
        request.Validate(validationMode);
        return SendCore(this.transport, workspace, request, cancellationToken);
    }

    private static async ValueTask<PetByIdResponse> SendCore(IApiTransport transport, JsonWorkspace workspace, SearchPetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await transport.SendAsync<SearchPetsRequest, PetByIdResponse>(in request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            workspace.Dispose();
        }
    }
}