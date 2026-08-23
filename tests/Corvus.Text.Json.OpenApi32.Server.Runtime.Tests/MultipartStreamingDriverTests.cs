// <copyright file="MultipartStreamingDriverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Tests for <see cref="MultipartStreamingDriver"/>: projection building, wire-order
/// handle consumption, ordering policy enforcement, and required-part errors.
/// </summary>
[TestClass]
public class MultipartStreamingDriverTests
{
    private const string Boundary = "drv";
    private const string ContentType = $"multipart/form-data; boundary={Boundary}";

    private static readonly ApiServerOptions Options = new();

    private static byte[] BuildBody(params (string Headers, byte[] Body)[] parts)
    {
        using MemoryStream ms = new();
        foreach ((string headers, byte[] body) in parts)
        {
            ms.Write(Encoding.UTF8.GetBytes($"--{Boundary}\r\n{headers}\r\n\r\n"));
            ms.Write(body);
            ms.Write("\r\n"u8);
        }

        ms.Write(Encoding.UTF8.GetBytes($"--{Boundary}--\r\n"));
        return ms.ToArray();
    }

    private static (string Headers, byte[] Body) TextPart(string name, string value)
        => ($"Content-Disposition: form-data; name=\"{name}\"", Encoding.UTF8.GetBytes(value));

    private static (string Headers, byte[] Body) FilePart(string name, byte[] bytes)
        => ($"Content-Disposition: form-data; name=\"{name}\"; filename=\"{name}.bin\"\r\nContent-Type: application/octet-stream", bytes);

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    [TestMethod]
    public async Task Driver_BinaryLastBody_ProjectsTextAndStreamsBinary()
    {
        byte[] fileBytes = new byte[5000];
        Random.Shared.NextBytes(fileBytes);
        byte[] body = BuildBody(
            TextPart("caption", "hello"),
            TextPart("count", "42"),
            FilePart("file", fileBytes));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(driver.ProjectionUtf8Json);
        Assert.AreEqual("hello", doc.RootElement.GetProperty("caption"u8).GetString());
        Assert.AreEqual(42, doc.RootElement.GetProperty("count"u8).GetInt32());
        Assert.IsFalse(doc.RootElement.TryGetProperty("file"u8, out _), "binary parts must not appear in the projection");

        Stream? stream = await driver.GetHandle("file", required: true).OpenStreamAsync();
        Assert.IsNotNull(stream);
        CollectionAssert.AreEqual(fileBytes, await ReadAllAsync(stream));
    }

    [TestMethod]
    public async Task Driver_TwoBinaryParts_ConsumedInWireOrder()
    {
        byte[] first = "FIRST"u8.ToArray();
        byte[] second = "SECOND"u8.ToArray();
        byte[] body = BuildBody(
            TextPart("note", "n"),
            FilePart("a", first),
            FilePart("b", second));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        CollectionAssert.AreEqual(first, await ReadAllAsync((await driver.GetHandle("a", true).OpenStreamAsync())!));
        CollectionAssert.AreEqual(second, await ReadAllAsync((await driver.GetHandle("b", true).OpenStreamAsync())!));
    }

    [TestMethod]
    public async Task Driver_OpeningLaterPart_PassesEarlierPartPermanently()
    {
        byte[] body = BuildBody(
            FilePart("a", "AAA"u8.ToArray()),
            FilePart("b", "BBB"u8.ToArray()));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        // Open b first: a is skipped and drained.
        CollectionAssert.AreEqual("BBB"u8.ToArray(), await ReadAllAsync((await driver.GetHandle("b", true).OpenStreamAsync())!));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await driver.GetHandle("a", required: false).OpenStreamAsync());
    }

    [TestMethod]
    public async Task Driver_UnreadHandle_IsDrainedByNextOpen()
    {
        byte[] big = new byte[20_000];
        Random.Shared.NextBytes(big);
        byte[] body = BuildBody(
            FilePart("skipme", big),
            FilePart("wanted", "W"u8.ToArray()));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        Stream? first = await driver.GetHandle("skipme", true).OpenStreamAsync();
        Assert.IsNotNull(first);

        // Do not read it; opening the next handle must drain the remainder.
        CollectionAssert.AreEqual("W"u8.ToArray(), await ReadAllAsync((await driver.GetHandle("wanted", true).OpenStreamAsync())!));
    }

    [TestMethod]
    public async Task Driver_OptionalAbsentPart_ReturnsNull()
    {
        byte[] body = BuildBody(TextPart("only", "text"));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        Assert.IsNull(await driver.GetHandle("file", required: false).OpenStreamAsync());
    }

    [TestMethod]
    public async Task Driver_RequiredAbsentPart_Throws()
    {
        byte[] body = BuildBody(TextPart("only", "text"));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        await Assert.ThrowsExactlyAsync<RequiredBinaryPartMissingException>(
            async () => await driver.GetHandle("file", required: true).OpenStreamAsync());
    }

    [TestMethod]
    public async Task Driver_TextAfterBinary_ThrowsOrderingViolation()
    {
        byte[] body = BuildBody(
            FilePart("file", "F"u8.ToArray()),
            TextPart("late", "text"));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, Options);

        // Consume the binary part; the trailing text part is the violation.
        Stream? stream = await driver.GetHandle("file", true).OpenStreamAsync();
        _ = await ReadAllAsync(stream!);

        await Assert.ThrowsExactlyAsync<MultipartOrderingException>(
            async () => await driver.GetHandle("other", required: false).OpenStreamAsync());
    }

    [TestMethod]
    public async Task Driver_NonBinaryBudgetExceeded_Throws()
    {
        byte[] body = BuildBody(
            TextPart("huge", new string('x', 5000)),
            FilePart("file", "F"u8.ToArray()));

        using MemoryStream source = new(body);
        ApiServerOptions tight = new() { MaxNonBinaryPartsLength = 1024 };

        await Assert.ThrowsExactlyAsync<RequestBodyTooLargeException>(
            async () => await MultipartStreamingDriver.BeginAsync(source, ContentType, tight));
    }

    [TestMethod]
    public async Task Driver_DefaultHandle_OpensAsNull()
    {
        BinaryPartHandle handle = default;
        Assert.IsNull(await handle.OpenStreamAsync());
    }
}