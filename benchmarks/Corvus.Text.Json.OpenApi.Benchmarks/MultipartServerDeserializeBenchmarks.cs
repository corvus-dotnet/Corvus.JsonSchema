// <copyright file="MultipartServerDeserializeBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace PetstoreBenchmark;

/// <summary>
/// Compares the server-side multipart deserialize-and-bind path used by generated
/// endpoints before and after the owned-body change: copying each binary part with
/// ToArray versus slicing the retained body bytes.
/// </summary>
[MemoryDiagnoser]
public class MultipartServerDeserializeBenchmarks
{
    private const string Boundary = "bench-boundary";
    private byte[] body = [];

    /// <summary>
    /// Gets or sets the size of the binary file part in bytes.
    /// </summary>
    [Params(4096, 1048576)]
    public int FileSize { get; set; }

    /// <summary>
    /// Builds a multipart body with one binary part and two small text fields.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        byte[] filePayload = new byte[this.FileSize];
        Random.Shared.NextBytes(filePayload);

        using MemoryStream ms = new();
        void WriteText(string s) => ms.Write(Encoding.UTF8.GetBytes(s));
        WriteText($"--{Boundary}\r\nContent-Disposition: form-data; name=\"description\"\r\n\r\nA benchmark file\r\n");
        WriteText($"--{Boundary}\r\nContent-Disposition: form-data; name=\"category\"\r\n\r\nbench\r\n");
        WriteText($"--{Boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"payload.bin\"\r\nContent-Type: application/octet-stream\r\n\r\n");
        ms.Write(filePayload);
        WriteText($"\r\n--{Boundary}--\r\n");
        this.body = ms.ToArray();
    }

    /// <summary>
    /// The pre-change path: deserialize and copy the binary part with ToArray.
    /// </summary>
    /// <returns>The captured part length, to defeat dead-code elimination.</returns>
    [Benchmark(Baseline = true)]
    public async Task<int> CopyBinaryPart()
    {
        using MemoryStream stream = new(this.body);
        byte[]? captured = null;
        using ParsedJsonDocument<JsonElement> doc = await MultipartFormDataSerializer.DeserializeAsync<JsonElement>(
            stream,
            $"multipart/form-data; boundary={Boundary}",
            binaryPartCallback: part => captured = part.Data.ToArray());
        ReadOnlyMemory<byte> bound = captured ?? ReadOnlyMemory<byte>.Empty;
        return bound.Length + doc.RootElement.GetProperty("category"u8).GetString()!.Length;
    }

    /// <summary>
    /// The owned path: deserialize once and bind the binary part as a slice of the
    /// retained body bytes.
    /// </summary>
    /// <returns>The captured part length, to defeat dead-code elimination.</returns>
    [Benchmark]
    public async Task<int> SliceOwnedBinaryPart()
    {
        using MemoryStream stream = new(this.body);
        int offset = -1;
        int length = 0;
        using OwnedMultipartBody<JsonElement> owned = await MultipartFormDataSerializer.DeserializeOwnedAsync<JsonElement>(
            stream,
            $"multipart/form-data; boundary={Boundary}",
            binaryPartCallback: part =>
            {
                offset = part.BodyOffset;
                length = part.Data.Length;
            });
        ReadOnlyMemory<byte> bound = offset >= 0 ? owned.BodyBytes.Slice(offset, length) : ReadOnlyMemory<byte>.Empty;
        return bound.Length + owned.Document.RootElement.GetProperty("category"u8).GetString()!.Length;
    }
}