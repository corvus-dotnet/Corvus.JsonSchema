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
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], Options);

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
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["a", "b"], Options);

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
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["a", "b"], Options);

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
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["skipme", "wanted"], Options);

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
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], Options);

        Assert.IsNull(await driver.GetHandle("file", required: false).OpenStreamAsync());
    }

    [TestMethod]
    public async Task Driver_RequiredAbsentPart_Throws()
    {
        byte[] body = BuildBody(TextPart("only", "text"));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], Options);

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
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file", "other"], Options);

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
            async () => await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], tight));
    }

    [TestMethod]
    public async Task Driver_ManyEmptyBodiedNamedParts_EnforceProjectionCap()
    {
        // Empty part bodies charge zero against a body-bytes-only budget, but their
        // names still accumulate in the projection. The cap must bound the projection
        // itself, not just the sum of body bytes.
        (string, byte[])[] parts = new (string, byte[])[2001];
        for (int i = 0; i < 2000; i++)
        {
            parts[i] = ($"Content-Disposition: form-data; name=\"field{i:D6}\"", []);
        }

        parts[2000] = FilePart("file", "F"u8.ToArray());
        byte[] body = BuildBody(parts);

        using MemoryStream source = new(body);
        ApiServerOptions tight = new() { MaxNonBinaryPartsLength = 1024 };

        await Assert.ThrowsExactlyAsync<RequestBodyTooLargeException>(
            async () => await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], tight));
    }

    [TestMethod]
    public async Task Driver_DefaultHandle_OpensAsNull()
    {
        BinaryPartHandle handle = default;
        Assert.IsNull(await handle.OpenStreamAsync());
    }

    [TestMethod]
    public async Task Driver_JsonPartWithCharsetParameter_ProjectsAsRawJson()
    {
        // "application/json; charset=utf-8" must classify as JSON, not binary
        // (media type parameters are stripped, matching MultipartMixedReader).
        byte[] body = BuildBody(
            ("Content-Disposition: form-data; name=\"meta\"\r\nContent-Type: application/json; charset=utf-8", """{"a":1}"""u8.ToArray()),
            FilePart("file", "F"u8.ToArray()));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], Options);

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(driver.ProjectionUtf8Json);
        Assert.AreEqual(1, doc.RootElement.GetProperty("meta"u8).GetProperty("a"u8).GetInt32());
    }

    [TestMethod]
    public async Task Driver_UndeclaredHandleName_Throws()
    {
        byte[] body = BuildBody(FilePart("file", "F"u8.ToArray()));

        using MemoryStream source = new(body);
        await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], Options);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await driver.GetHandle("undeclared", required: false).OpenStreamAsync());
    }

    private static string CreateSpoolDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "corvus-spool-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static ApiServerOptions SpoolOptions(string dir, int threshold = ApiServerOptions.DefaultSpoolMemoryThresholdBytes, long maxSpooled = long.MaxValue) => new()
    {
        MultipartBinaryOrdering = MultipartBinaryOrdering.SpoolOutOfOrder,
        SpoolDirectory = dir,
        SpoolMemoryThresholdBytes = threshold,
        MaxSpooledBodyLength = maxSpooled,
    };

    [TestMethod]
    public async Task Spool_BrowserOrder_ProjectsTextArrivingAfterBinary()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] fileBytes = new byte[5000];
            Random.Shared.NextBytes(fileBytes);
            byte[] body = BuildBody(
                FilePart("file", fileBytes),
                TextPart("caption", "after the file"));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir));

            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(driver.ProjectionUtf8Json);
            Assert.AreEqual("after the file", doc.RootElement.GetProperty("caption"u8).GetString());

            CollectionAssert.AreEqual(fileBytes, await ReadAllAsync((await driver.GetHandle("file", true).OpenStreamAsync())!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_HandlesOpenInAnyOrder()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(
                FilePart("a", "AAA"u8.ToArray()),
                FilePart("b", "BBB"u8.ToArray()));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["a", "b"], SpoolOptions(dir));

            // Reverse wire order: allowed under the spool policy.
            CollectionAssert.AreEqual("BBB"u8.ToArray(), await ReadAllAsync((await driver.GetHandle("b", true).OpenStreamAsync())!));
            CollectionAssert.AreEqual("AAA"u8.ToArray(), await ReadAllAsync((await driver.GetHandle("a", true).OpenStreamAsync())!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_SmallPart_StaysInPooledMemory()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(FilePart("file", "small"u8.ToArray()));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir, threshold: 1024));

            Assert.AreEqual(0, Directory.GetFiles(dir).Length, "a part under the threshold must not create a spool file");
            Stream? stream = await driver.GetHandle("file", true).OpenStreamAsync();
            Assert.IsInstanceOfType<MemoryStream>(stream);
            CollectionAssert.AreEqual("small"u8.ToArray(), await ReadAllAsync(stream!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_LargePart_UsesTempFile_DeletedOnDispose()
    {
        string dir = CreateSpoolDirectory();
        byte[] fileBytes = new byte[8000];
        Random.Shared.NextBytes(fileBytes);
        try
        {
            byte[] body = BuildBody(FilePart("file", fileBytes));

            using MemoryStream source = new(body);
            MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir, threshold: 1024));
            await using (driver)
            {
                Assert.AreEqual(1, Directory.GetFiles(dir).Length, "a part over the threshold must spool to a file");
                Stream? stream = await driver.GetHandle("file", true).OpenStreamAsync();
                Assert.IsInstanceOfType<FileStream>(stream);
                CollectionAssert.AreEqual(fileBytes, await ReadAllAsync(stream!));
            }

            Assert.AreEqual(0, Directory.GetFiles(dir).Length, "spool files must be deleted when the driver is disposed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_FileSpool_IsNotReadableByGroupOrOther()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file mode is not meaningful on Windows.");
            return;
        }

        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(FilePart("file", new byte[8000]));

            using MemoryStream source = new(body);
            MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir, threshold: 1024));
            await using (driver)
            {
                string spoolFile = Directory.GetFiles(dir).Single();
                UnixFileMode mode = File.GetUnixFileMode(spoolFile);
                UnixFileMode exposed = mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
                Assert.AreEqual(UnixFileMode.None, exposed, $"spool file must not be readable or writable by group or other, but was {mode}");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_UnopenedFileSpool_DeletedOnDispose()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(FilePart("file", new byte[8000]));

            using MemoryStream source = new(body);
            MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir, threshold: 1024));
            Assert.AreEqual(1, Directory.GetFiles(dir).Length);

            // The handler never opens the handle; disposal must still clean up.
            await driver.DisposeAsync();
            Assert.AreEqual(0, Directory.GetFiles(dir).Length);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_OpenTwice_Throws()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(FilePart("file", "F"u8.ToArray()));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir));

            _ = await driver.GetHandle("file", true).OpenStreamAsync();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await driver.GetHandle("file", true).OpenStreamAsync());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_RequiredAbsent_Throws_OptionalAbsent_Null()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(TextPart("only", "text"));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file", "thumb"], SpoolOptions(dir));

            Assert.IsNull(await driver.GetHandle("thumb", required: false).OpenStreamAsync());
            await Assert.ThrowsExactlyAsync<RequiredBinaryPartMissingException>(
                async () => await driver.GetHandle("file", required: true).OpenStreamAsync());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_UndeclaredBinaryPart_DrainedWithoutSpooling()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(
                FilePart("attacker", new byte[8000]),
                TextPart("caption", "c"),
                FilePart("file", "F"u8.ToArray()));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir, threshold: 1024));

            Assert.AreEqual(0, Directory.GetFiles(dir).Length, "undeclared binary parts must not be spooled");
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(driver.ProjectionUtf8Json);
            Assert.AreEqual("c", doc.RootElement.GetProperty("caption"u8).GetString());
            CollectionAssert.AreEqual("F"u8.ToArray(), await ReadAllAsync((await driver.GetHandle("file", true).OpenStreamAsync())!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_DuplicatePartName_FirstOccurrenceWins()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(
                FilePart("file", "FIRST"u8.ToArray()),
                FilePart("file", "SECOND"u8.ToArray()));

            using MemoryStream source = new(body);
            await using MultipartStreamingDriver driver = await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir));

            CollectionAssert.AreEqual("FIRST"u8.ToArray(), await ReadAllAsync((await driver.GetHandle("file", true).OpenStreamAsync())!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_MaxSpooledBodyLengthExceeded_Throws_AndCleansUp()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(
                FilePart("file", new byte[8000]),
                TextPart("caption", "c"));

            using MemoryStream source = new(body);
            await Assert.ThrowsExactlyAsync<RequestBodyTooLargeException>(
                async () => await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], SpoolOptions(dir, threshold: 1024, maxSpooled: 4096)));

            Assert.AreEqual(0, Directory.GetFiles(dir).Length, "a failed begin must remove any partial spool files");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Spool_NonBinaryBudget_StillEnforced()
    {
        string dir = CreateSpoolDirectory();
        try
        {
            byte[] body = BuildBody(
                FilePart("file", "F"u8.ToArray()),
                TextPart("huge", new string('x', 5000)));

            using MemoryStream source = new(body);
            ApiServerOptions tight = new()
            {
                MultipartBinaryOrdering = MultipartBinaryOrdering.SpoolOutOfOrder,
                SpoolDirectory = dir,
                MaxNonBinaryPartsLength = 1024,
            };

            await Assert.ThrowsExactlyAsync<RequestBodyTooLargeException>(
                async () => await MultipartStreamingDriver.BeginAsync(source, ContentType, ["file"], tight));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}