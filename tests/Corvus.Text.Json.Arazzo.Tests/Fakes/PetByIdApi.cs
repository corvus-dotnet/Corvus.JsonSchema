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
public readonly struct PetByIdRequest(JsonElement petId) : IApiRequest<PetByIdRequest>
{
    private readonly JsonElement petId = petId;

    public static ReadOnlySpan<byte> PathTemplateUtf8 => "/pets/{petId}"u8;

    public static OperationMethod Method => OperationMethod.Get;

    public static bool HasPathParameters => true;

    public static bool HasQueryParameters => false;

    public static bool HasHeaderParameters => false;

    public static bool HasCookieParameters => false;

    public void WriteResolvedPath(IBufferWriter<byte> writer)
    {
        writer.Write("/pets/"u8);
        string id = this.petId.ValueKind == JsonValueKind.String ? this.petId.GetString()! : this.petId.GetRawText();
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

    public int StatusCode { get; private set; }

    public JsonElement OkBody { get; private set; }

    public readonly bool IsSuccess => this.StatusCode is >= 200 and < 300;

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
        var response = new PetByIdResponse { StatusCode = statusCode };
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