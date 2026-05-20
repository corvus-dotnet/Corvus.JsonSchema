// <copyright file="EndToEndBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using PetstoreBenchmark.CorvusClient;
using PetstoreBenchmark.KiotaClient;
using PetstoreBenchmark.KiotaClient.Pets;

namespace PetstoreBenchmark;

/// <summary>
/// End-to-end GET list: query params + JSON array response + typed iteration.
/// </summary>
[MemoryDiagnoser]
public class GetListBenchmarks
{
    private static readonly byte[] PetListResponseBytes = Encoding.UTF8.GetBytes(
        """[{"id":1,"name":"Fido","tag":"dog"},{"id":2,"name":"Whiskers","tag":"cat"},{"id":3,"name":"Goldie","tag":"fish"},{"id":4,"name":"Rex","tag":"dog"},{"id":5,"name":"Mittens","tag":"cat"},{"id":6,"name":"Bubbles","tag":"fish"},{"id":7,"name":"Spot","tag":"dog"},{"id":8,"name":"Shadow","tag":"cat"},{"id":9,"name":"Nemo","tag":"fish"},{"id":10,"name":"Buddy","tag":"dog"}]""");

    private PetstorePetsClient corvusClient = null!;
    private PetstoreClient kiotaClient = null!;
    private HttpClient kiotaHttpClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.corvusClient = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes));
        this.kiotaHttpClient = new HttpClient(new MockHttpMessageHandler(PetListResponseBytes))
        {
            BaseAddress = new Uri("https://petstore.example.com/v1"),
        };

        this.kiotaClient = new PetstoreClient(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: this.kiotaHttpClient));
    }

    [GlobalCleanup]
    public void Cleanup() => this.kiotaHttpClient?.Dispose();

    [Benchmark(Description = "Corvus: GET /pets (no validation)")]
    public async ValueTask<int> Corvus_NoValidation()
    {
        ListPetsResponse response = await this.corvusClient.ListPetsAsync(
            validationMode: ValidationMode.None,
            responseValidationMode: ValidationMode.None);

        await using (response.ConfigureAwait(false))
        {
            int count = 0;
            foreach (Pet pet in response.OkBody.EnumerateArray())
            {
                _ = pet.Id;
                count++;
            }

            return count;
        }
    }

    [Benchmark(Description = "Corvus: GET /pets (with validation)")]
    public async ValueTask<int> Corvus_WithValidation()
    {
        ListPetsResponse response = await this.corvusClient.ListPetsAsync(
            validationMode: ValidationMode.Basic,
            responseValidationMode: ValidationMode.Basic);

        await using (response.ConfigureAwait(false))
        {
            response.Validate();

            int count = 0;
            foreach (Pet pet in response.OkBody.EnumerateArray())
            {
                _ = pet.Id;
                count++;
            }

            return count;
        }
    }

    [Benchmark(Description = "Kiota: GET /pets (no validation)")]
    public async Task<int> Kiota_NoValidation()
    {
        List<PetstoreBenchmark.KiotaClient.Models.Pet>? pets = await this.kiotaClient.Pets.GetAsync();

        int count = 0;
        if (pets is not null)
        {
            foreach (PetstoreBenchmark.KiotaClient.Models.Pet pet in pets)
            {
                _ = pet.Id;
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// End-to-end GET by ID: path parameter substitution + single object response.
/// </summary>
[MemoryDiagnoser]
public class GetByIdBenchmarks
{
    private static readonly byte[] SinglePetResponseBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"name":"Fido","tag":"dog"}""");

    private PetstorePetsClient corvusClient = null!;
    private PetstoreClient kiotaClient = null!;
    private HttpClient kiotaHttpClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.corvusClient = new PetstorePetsClient(new MockApiTransport(SinglePetResponseBytes));
        this.kiotaHttpClient = new HttpClient(new MockHttpMessageHandler(SinglePetResponseBytes))
        {
            BaseAddress = new Uri("https://petstore.example.com/v1"),
        };

        this.kiotaClient = new PetstoreClient(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: this.kiotaHttpClient));
    }

    [GlobalCleanup]
    public void Cleanup() => this.kiotaHttpClient?.Dispose();

    [Benchmark(Description = "Corvus: GET /pets/{petId} (no validation)")]
    public async ValueTask<long> Corvus_NoValidation()
    {
        ShowPetByIdResponse response = await this.corvusClient.ShowPetByIdAsync(
            "42",
            validationMode: ValidationMode.None,
            responseValidationMode: ValidationMode.None);

        await using (response.ConfigureAwait(false))
        {
            return response.OkBody.Id;
        }
    }

    [Benchmark(Description = "Corvus: GET /pets/{petId} (with validation)")]
    public async ValueTask<long> Corvus_WithValidation()
    {
        ShowPetByIdResponse response = await this.corvusClient.ShowPetByIdAsync(
            "42",
            validationMode: ValidationMode.Basic,
            responseValidationMode: ValidationMode.Basic);

        await using (response.ConfigureAwait(false))
        {
            response.Validate();
            return response.OkBody.Id;
        }
    }

    [Benchmark(Description = "Kiota: GET /pets/{petId} (no validation)")]
    public async Task<long?> Kiota_NoValidation()
    {
        PetstoreBenchmark.KiotaClient.Models.Pet? pet = await this.kiotaClient.Pets["42"].GetAsync();
        return pet?.Id;
    }
}

/// <summary>
/// End-to-end POST: JSON request body serialization + response parsing.
/// </summary>
[MemoryDiagnoser]
public class PostJsonBodyBenchmarks
{
    private static readonly byte[] CreatedPetResponseBytes = Encoding.UTF8.GetBytes(
        """{"id":99,"name":"Rex","tag":"dog"}""");

    private PetstorePetsClient corvusClient = null!;
    private PetstoreClient kiotaClient = null!;
    private HttpClient kiotaHttpClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.corvusClient = new PetstorePetsClient(new MockApiTransport(CreatedPetResponseBytes, statusCode: 201));
        this.kiotaHttpClient = new HttpClient(new MockHttpMessageHandler(CreatedPetResponseBytes, HttpStatusCode.Created))
        {
            BaseAddress = new Uri("https://petstore.example.com/v1"),
        };

        this.kiotaClient = new PetstoreClient(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: this.kiotaHttpClient));
    }

    [GlobalCleanup]
    public void Cleanup() => this.kiotaHttpClient?.Dispose();

    [Benchmark(Description = "Corvus: POST /pets (no validation)")]
    public async ValueTask<long> Corvus_NoValidation()
    {
        CreatePetResponse response = await this.corvusClient.CreatePetAsync(
            NewPet.Build(static (ref NewPet.Builder b) => b.Create("Rex", "dog")),
            validationMode: ValidationMode.None,
            responseValidationMode: ValidationMode.None);

        await using (response.ConfigureAwait(false))
        {
            return response.CreatedBody.Id;
        }
    }

    [Benchmark(Description = "Corvus: POST /pets (with validation)")]
    public async ValueTask<long> Corvus_WithValidation()
    {
        CreatePetResponse response = await this.corvusClient.CreatePetAsync(
            NewPet.Build(static (ref NewPet.Builder b) => b.Create("Rex", "dog")),
            validationMode: ValidationMode.Basic,
            responseValidationMode: ValidationMode.Basic);

        await using (response.ConfigureAwait(false))
        {
            response.Validate();
            return response.CreatedBody.Id;
        }
    }

    [Benchmark(Description = "Kiota: POST /pets (no validation)")]
    public async Task<long?> Kiota_NoValidation()
    {
        PetstoreBenchmark.KiotaClient.Models.NewPet body = new()
        {
            Name = "Rex",
            Tag = "dog",
        };

        PetstoreBenchmark.KiotaClient.Models.Pet? created = await this.kiotaClient.Pets.PostAsync(body);
        return created?.Id;
    }
}

/// <summary>
/// End-to-end PUT with form-urlencoded body + path parameter.
/// </summary>
[MemoryDiagnoser]
public class PutFormEncodedBenchmarks
{
    private static readonly byte[] UpdatedPetResponseBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"name":"Fido Updated","tag":"dog"}""");

    private PetstorePetsClient corvusClient = null!;
    private PetstoreClient kiotaClient = null!;
    private HttpClient kiotaHttpClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.corvusClient = new PetstorePetsClient(new MockApiTransport(UpdatedPetResponseBytes));
        this.kiotaHttpClient = new HttpClient(new MockHttpMessageHandler(UpdatedPetResponseBytes))
        {
            BaseAddress = new Uri("https://petstore.example.com/v1"),
        };

        this.kiotaClient = new PetstoreClient(
            new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: this.kiotaHttpClient));
    }

    [GlobalCleanup]
    public void Cleanup() => this.kiotaHttpClient?.Dispose();

    [Benchmark(Description = "Corvus: PUT /pets/{petId} form-urlencoded (no validation)")]
    public async ValueTask<long> Corvus_NoValidation()
    {
        UpdatePetResponse response = await this.corvusClient.UpdatePetAsync(
            "42",
            NewPet.Build(static (ref NewPet.Builder b) => b.Create("Fido Updated", "dog")),
            validationMode: ValidationMode.None,
            responseValidationMode: ValidationMode.None);

        await using (response.ConfigureAwait(false))
        {
            return response.OkBody.Id;
        }
    }

    [Benchmark(Description = "Corvus: PUT /pets/{petId} form-urlencoded (with validation)")]
    public async ValueTask<long> Corvus_WithValidation()
    {
        UpdatePetResponse response = await this.corvusClient.UpdatePetAsync(
            "42",
            NewPet.Build(static (ref NewPet.Builder b) => b.Create("Fido Updated", "dog")),
            validationMode: ValidationMode.Basic,
            responseValidationMode: ValidationMode.Basic);

        await using (response.ConfigureAwait(false))
        {
            response.Validate();
            return response.OkBody.Id;
        }
    }

    [Benchmark(Description = "Kiota: PUT /pets/{petId} (no validation)")]
    public async Task<long?> Kiota_NoValidation()
    {
        PetstoreBenchmark.KiotaClient.Models.NewPet body = new()
        {
            Name = "Fido Updated",
            Tag = "dog",
        };

        PetstoreBenchmark.KiotaClient.Models.Pet? updated = await this.kiotaClient.Pets["42"].PutAsync(body);
        return updated?.Id;
    }
}

/// <summary>
/// Response header parsing benchmark.
/// Corvus: zero-allocation lazy header parse from IResponseHeaders.
/// Kiota: no typed header support (returns raw HttpResponseMessage headers).
/// </summary>
[MemoryDiagnoser]
public class ResponseHeaderBenchmarks
{
    private static readonly byte[] PetListResponseBytes = Encoding.UTF8.GetBytes(
        """[{"id":1,"name":"Fido","tag":"dog"}]""");

    private PetstorePetsClient corvusClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        var headers = new Dictionary<string, string> { ["x-next"] = "/pets?cursor=abc123" };
        this.corvusClient = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes, headers: headers));
    }

    [Benchmark(Description = "Corvus: response header (x-next) parsing")]
    public async ValueTask<string?> Corvus_ResponseHeader()
    {
        ListPetsResponse response = await this.corvusClient.ListPetsAsync(
            validationMode: ValidationMode.None,
            responseValidationMode: ValidationMode.None);

        await using (response.ConfigureAwait(false))
        {
            JsonString? xNext = response.XNextHeader;
            return xNext?.ToString();
        }
    }
}

/// <summary>
/// Validation mode comparison: None vs Basic vs Detailed.
/// Shows the cost of each validation level.
/// </summary>
[MemoryDiagnoser]
public class ValidationModeBenchmarks
{
    private static readonly byte[] PetListResponseBytes = Encoding.UTF8.GetBytes(
        """[{"id":1,"name":"Fido","tag":"dog"},{"id":2,"name":"Whiskers","tag":"cat"},{"id":3,"name":"Goldie","tag":"fish"}]""");

    private PetstorePetsClient corvusClientNone = null!;
    private PetstorePetsClient corvusClientBasic = null!;
    private PetstorePetsClient corvusClientDetailed = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.corvusClientNone = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes));
        this.corvusClientBasic = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes));
        this.corvusClientDetailed = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes));
    }

    [Benchmark(Description = "Corvus: ValidationMode.None", Baseline = true)]
    public async ValueTask<int> Corvus_None()
    {
        ListPetsResponse response = await this.corvusClientNone.ListPetsAsync(
            validationMode: ValidationMode.None,
            responseValidationMode: ValidationMode.None);

        await using (response.ConfigureAwait(false))
        {
            return response.OkBody.GetArrayLength();
        }
    }

    [Benchmark(Description = "Corvus: ValidationMode.Basic (request + response)")]
    public async ValueTask<int> Corvus_Basic()
    {
        ListPetsResponse response = await this.corvusClientBasic.ListPetsAsync(
            validationMode: ValidationMode.Basic,
            responseValidationMode: ValidationMode.Basic);

        await using (response.ConfigureAwait(false))
        {
            response.Validate();
            return response.OkBody.GetArrayLength();
        }
    }

    [Benchmark(Description = "Corvus: ValidationMode.Detailed (request + response)")]
    public async ValueTask<int> Corvus_Detailed()
    {
        ListPetsResponse response = await this.corvusClientDetailed.ListPetsAsync(
            validationMode: ValidationMode.Detailed,
            responseValidationMode: ValidationMode.Detailed);

        await using (response.ConfigureAwait(false))
        {
            response.Validate(ValidationMode.Detailed);
            return response.OkBody.GetArrayLength();
        }
    }
}

/// <summary>
/// Mock <see cref="IApiTransport"/> that returns fixed response bytes.
/// </summary>
internal sealed class MockApiTransport : IApiTransport
{
    private readonly byte[] responseBytes;
    private readonly int statusCode;
    private readonly IResponseHeaders? headers;

    public MockApiTransport(byte[] responseBytes, int statusCode = 200, Dictionary<string, string>? headers = null)
    {
        this.responseBytes = responseBytes;
        this.statusCode = statusCode;
        this.headers = headers is not null ? new DictionaryResponseHeaders(headers) : null;
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        MemoryStream stream = new(this.responseBytes, writable: false);
        return TResponse.CreateAsync(this.statusCode, stream, "application/json", this.headers, cancellationToken: cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
        in TRequest request,
        in TBody body,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TBody : struct, IJsonElement<TBody>
        where TResponse : struct, IApiResponse<TResponse>
    {
        MemoryStream stream = new(this.responseBytes, writable: false);
        return TResponse.CreateAsync(this.statusCode, stream, "application/json", this.headers, cancellationToken: cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Stream body,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        MemoryStream stream = new(this.responseBytes, writable: false);
        return TResponse.CreateAsync(this.statusCode, stream, "application/json", this.headers, cancellationToken: cancellationToken);
    }

    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        in TRequest request,
        Action<Stream> bodyWriter,
        string contentType,
        CancellationToken cancellationToken = default)
        where TRequest : struct, IApiRequest<TRequest>
        where TResponse : struct, IApiResponse<TResponse>
    {
        MemoryStream stream = new(this.responseBytes, writable: false);
        return TResponse.CreateAsync(this.statusCode, stream, "application/json", this.headers, cancellationToken: cancellationToken);
    }

    public ValueTask DisposeAsync() => default;
}

/// <summary>
/// Mock <see cref="HttpMessageHandler"/> for Kiota benchmarks.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly byte[] responseBytes;
    private readonly HttpStatusCode statusCode;

    public MockHttpMessageHandler(byte[] responseBytes, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        this.responseBytes = responseBytes;
        this.statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(this.statusCode)
        {
            Content = new ByteArrayContent(this.responseBytes),
        };
        response.Content.Headers.ContentType = new("application/json");
        return Task.FromResult(response);
    }
}

/// <summary>
/// Simple dictionary-backed <see cref="IResponseHeaders"/> for benchmarks.
/// </summary>
internal sealed class DictionaryResponseHeaders(Dictionary<string, string> headers) : IResponseHeaders
{
    public bool TryGetValue(string name, out string? value) => headers.TryGetValue(name, out value);
}
