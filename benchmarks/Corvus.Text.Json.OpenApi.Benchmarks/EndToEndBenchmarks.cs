// <copyright file="EndToEndBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;
using PetstoreBenchmark.CorvusClient;
using PetstoreBenchmark.KiotaClient;
using PetstoreBenchmark.KiotaClient.Pets;

namespace PetstoreBenchmark;

/// <summary>
/// End-to-end comparison: full generated client pipeline with mock HTTP transport.
/// <para>
/// Measures the complete round-trip: construct request → serialize parameters →
/// send via mock transport → receive response bytes → parse → optionally validate →
/// typed access.
/// </para>
/// <para>
/// Key hypothesis: Corvus WITH full validation is faster than Kiota WITHOUT validation,
/// because Kiota's JsonElement.Clone() + POCO hydration cost exceeds Corvus's single
/// pooled-memory parse + compiled validation.
/// </para>
/// </summary>
[MemoryDiagnoser]
public class EndToEndBenchmarks
{
    private static readonly byte[] SinglePetResponseBytes = Encoding.UTF8.GetBytes(
        """{"id":42,"name":"Fido","tag":"dog"}""");

    private static readonly byte[] PetListResponseBytes = Encoding.UTF8.GetBytes(
        """[{"id":1,"name":"Fido","tag":"dog"},{"id":2,"name":"Whiskers","tag":"cat"},{"id":3,"name":"Goldie","tag":"fish"},{"id":4,"name":"Rex","tag":"dog"},{"id":5,"name":"Mittens","tag":"cat"},{"id":6,"name":"Bubbles","tag":"fish"},{"id":7,"name":"Spot","tag":"dog"},{"id":8,"name":"Shadow","tag":"cat"},{"id":9,"name":"Nemo","tag":"fish"},{"id":10,"name":"Buddy","tag":"dog"}]""");

    private PetstorePetsClient corvusClient = null!;
    private PetstorePetsClient corvusClientWithValidation = null!;
    private PetstoreClient kiotaClient = null!;
    private HttpClient kiotaHttpClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Corvus client with a mock transport returning fixed JSON
        this.corvusClient = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes));
        this.corvusClientWithValidation = new PetstorePetsClient(new MockApiTransport(PetListResponseBytes));

        // Kiota client with a mock HttpMessageHandler
        this.kiotaHttpClient = new HttpClient(new MockHttpMessageHandler(PetListResponseBytes))
        {
            BaseAddress = new Uri("https://petstore.example.com/v1"),
        };

        IRequestAdapter adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(),
            httpClient: this.kiotaHttpClient);

        this.kiotaClient = new PetstoreClient(adapter);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.kiotaHttpClient?.Dispose();
    }

    // ===== Corvus: full pipeline =====

    [Benchmark(Description = "Corvus end-to-end (no validation)")]
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

    [Benchmark(Description = "Corvus end-to-end (with validation)")]
    public async ValueTask<int> Corvus_WithValidation()
    {
        ListPetsResponse response = await this.corvusClientWithValidation.ListPetsAsync(
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

    // ===== Kiota: full pipeline =====

    [Benchmark(Description = "Kiota end-to-end (no validation)")]
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

    /// <summary>
    /// Mock <see cref="IApiTransport"/> that returns fixed response bytes.
    /// Simulates a transport that has already received HTTP response bytes.
    /// </summary>
    private sealed class MockApiTransport(byte[] responseBytes) : IApiTransport
    {
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            in TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            MemoryStream stream = new(responseBytes, writable: false);
            return TResponse.CreateAsync(200, stream, "application/json", cancellationToken: cancellationToken);
        }

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(
            in TRequest request,
            in TBody body,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
        {
            MemoryStream stream = new(responseBytes, writable: false);
            return TResponse.CreateAsync(200, stream, "application/json", cancellationToken: cancellationToken);
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            in TRequest request,
            Stream body,
            string contentType,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            MemoryStream stream = new(responseBytes, writable: false);
            return TResponse.CreateAsync(200, stream, "application/json", cancellationToken: cancellationToken);
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            in TRequest request,
            Action<Stream> bodyWriter,
            string contentType,
            CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
        {
            MemoryStream stream = new(responseBytes, writable: false);
            return TResponse.CreateAsync(200, stream, "application/json", cancellationToken: cancellationToken);
        }

        public ValueTask DisposeAsync() => default;
    }

    /// <summary>
    /// Mock <see cref="HttpMessageHandler"/> that returns fixed response bytes.
    /// </summary>
    private sealed class MockHttpMessageHandler(byte[] responseBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBytes),
            };
            response.Content.Headers.ContentType = new("application/json");
            return Task.FromResult(response);
        }
    }
}
