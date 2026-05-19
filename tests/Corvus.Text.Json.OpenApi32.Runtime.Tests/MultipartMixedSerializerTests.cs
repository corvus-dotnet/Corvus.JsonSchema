// <copyright file="MultipartMixedSerializerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests for <see cref="MultipartMixedSerializer"/>.
/// </summary>
[TestClass]
public class MultipartMixedSerializerTests
{
    [TestMethod]
    public void GetContentType_ContainsBoundaryPrefix()
    {
        Guid guid = Guid.NewGuid();
        string contentType = MultipartMixedSerializer.GetContentType(guid);

        Assert.IsTrue(
            contentType.StartsWith("multipart/mixed; boundary=----CorvusBoundary"),
            $"Unexpected content type: {contentType}");
    }

    [TestMethod]
    public void GetContentType_ContainsGuidHex()
    {
        Guid guid = Guid.NewGuid();
        string contentType = MultipartMixedSerializer.GetContentType(guid);
        string expectedSuffix = guid.ToString("N");

        Assert.IsTrue(
            contentType.EndsWith(expectedSuffix),
            $"Expected content type to end with guid hex '{expectedSuffix}', got: {contentType}");
    }

    [TestMethod]
    public void WriteJsonPart_WritesCorrectStructure()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();
        JsonElement value = JsonElement.ParseValue("""{"name":"test"}"""u8);

        MultipartMixedSerializer.WriteJsonPart(stream, guid, value);

        string output = Encoding.UTF8.GetString(stream.ToArray());
        string expectedBoundary = $"----CorvusBoundary{guid:N}";

        Assert.IsTrue(output.StartsWith($"--{expectedBoundary}\r\n"), $"Missing boundary line. Got:\n{output}");
        Assert.IsTrue(output.Contains("Content-Type: application/json\r\n\r\n"), $"Missing content type header. Got:\n{output}");
        Assert.IsTrue(output.Contains("""{"name":"test"}"""), $"Missing JSON body. Got:\n{output}");
        Assert.IsTrue(output.EndsWith("\r\n"), "Should end with CRLF");
    }

    [TestMethod]
    public void WriteJsonPart_CustomContentType()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();
        JsonElement value = JsonElement.ParseValue("42"u8);

        MultipartMixedSerializer.WriteJsonPart(stream, guid, value, "application/vnd.custom+json"u8);

        string output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.IsTrue(output.Contains("Content-Type: application/vnd.custom+json\r\n\r\n"), $"Got:\n{output}");
    }

    [TestMethod]
    public void WriteBinaryPart_WritesContentViaCallback()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();
        byte[] fileBytes = [0x89, 0x50, 0x4E, 0x47]; // PNG header
        BinaryPartData part = new(s => s.Write(fileBytes), "image/png", "photo.png");

        MultipartMixedSerializer.WriteBinaryPart(stream, guid, part);

        string output = Encoding.UTF8.GetString(stream.ToArray());
        string expectedBoundary = $"----CorvusBoundary{guid:N}";

        Assert.IsTrue(output.Contains($"--{expectedBoundary}\r\n"), $"Missing boundary. Got:\n{output}");
        Assert.IsTrue(output.Contains("Content-Type: image/png"), $"Missing content type. Got:\n{output}");
        Assert.IsTrue(output.Contains("Content-Disposition: attachment; filename=\"photo.png\""), $"Missing filename. Got:\n{output}");

        // Verify the binary bytes are present
        byte[] outputBytes = stream.ToArray();
        int pngIndex = FindSubArray(outputBytes, fileBytes);
        Assert.IsTrue(pngIndex >= 0, "Binary content not found in output");
    }

    [TestMethod]
    public void WriteBinaryPart_NoFileName_OmitsContentDisposition()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();
        BinaryPartData part = new(s => s.Write([0xFF]), "application/octet-stream", null);

        MultipartMixedSerializer.WriteBinaryPart(stream, guid, part);

        string output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.IsFalse(output.Contains("Content-Disposition"), $"Should not have Content-Disposition. Got:\n{output}");
    }

    [TestMethod]
    public void WriteTextPart_WritesCorrectStructure()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();

        MultipartMixedSerializer.WriteTextPart(stream, guid, "Hello, World!"u8);

        string output = Encoding.UTF8.GetString(stream.ToArray());
        string expectedBoundary = $"----CorvusBoundary{guid:N}";

        Assert.IsTrue(output.Contains($"--{expectedBoundary}\r\n"), $"Missing boundary. Got:\n{output}");
        Assert.IsTrue(output.Contains("Content-Type: text/plain\r\n\r\n"), $"Missing content type. Got:\n{output}");
        Assert.IsTrue(output.Contains("Hello, World!\r\n"), $"Missing text body. Got:\n{output}");
    }

    [TestMethod]
    public void WriteClosingBoundary_WritesTerminator()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();

        MultipartMixedSerializer.WriteClosingBoundary(stream, guid);

        string output = Encoding.UTF8.GetString(stream.ToArray());
        string expectedBoundary = $"----CorvusBoundary{guid:N}";

        Assert.AreEqual($"--{expectedBoundary}--\r\n", output);
    }

    [TestMethod]
    public void FullMultipartMessage_PrefixPattern()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();
        string boundary = $"----CorvusBoundary{guid:N}";

        // Simulate a prefix-encoded message: JSON metadata + binary file
        JsonElement metadata = JsonElement.ParseValue("""{"title":"Report Q4"}"""u8);
        byte[] fileData = Encoding.UTF8.GetBytes("PDF content here");
        BinaryPartData filePart = new(s => s.Write(fileData), "application/pdf", "report.pdf");

        MultipartMixedSerializer.WriteJsonPart(stream, guid, metadata);
        MultipartMixedSerializer.WriteBinaryPart(stream, guid, filePart);
        MultipartMixedSerializer.WriteClosingBoundary(stream, guid);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        // Verify structure: two parts + closing boundary
        int boundaryCount = CountOccurrences(output, $"--{boundary}");
        Assert.AreEqual(3, boundaryCount, $"Expected 3 boundary markers (2 parts + closing), got {boundaryCount}.\n{output}");

        // Verify correct order
        int jsonPartIndex = output.IndexOf("application/json");
        int pdfPartIndex = output.IndexOf("application/pdf");
        int closingIndex = output.IndexOf($"--{boundary}--");
        Assert.IsTrue(jsonPartIndex < pdfPartIndex, "JSON part should precede binary part");
        Assert.IsTrue(pdfPartIndex < closingIndex, "Binary part should precede closing boundary");
    }

    [TestMethod]
    public void FullMultipartMessage_ItemPattern()
    {
        using MemoryStream stream = new();
        Guid guid = Guid.NewGuid();
        string boundary = $"----CorvusBoundary{guid:N}";

        // Simulate an item-encoded message: homogeneous JSON batch
        JsonElement item1 = JsonElement.ParseValue("""{"action":"delete","id":1}"""u8);
        JsonElement item2 = JsonElement.ParseValue("""{"action":"archive","id":2}"""u8);
        JsonElement item3 = JsonElement.ParseValue("""{"action":"restore","id":3}"""u8);

        MultipartMixedSerializer.WriteJsonPart(stream, guid, item1);
        MultipartMixedSerializer.WriteJsonPart(stream, guid, item2);
        MultipartMixedSerializer.WriteJsonPart(stream, guid, item3);
        MultipartMixedSerializer.WriteClosingBoundary(stream, guid);

        string output = Encoding.UTF8.GetString(stream.ToArray());

        // Verify structure: three parts + closing boundary
        int boundaryCount = CountOccurrences(output, $"--{boundary}");
        Assert.AreEqual(4, boundaryCount, $"Expected 4 boundary markers (3 parts + closing), got {boundaryCount}.\n{output}");

        // All parts should have application/json
        int jsonCount = CountOccurrences(output, "Content-Type: application/json");
        Assert.AreEqual(3, jsonCount, $"Expected 3 JSON content-type headers, got {jsonCount}");
    }

    [TestMethod]
    public void SameGuid_ProducesSameBoundary()
    {
        Guid guid = Guid.NewGuid();

        using MemoryStream stream1 = new();
        MultipartMixedSerializer.WriteClosingBoundary(stream1, guid);

        using MemoryStream stream2 = new();
        MultipartMixedSerializer.WriteClosingBoundary(stream2, guid);

        string output1 = Encoding.UTF8.GetString(stream1.ToArray());
        string output2 = Encoding.UTF8.GetString(stream2.ToArray());

        Assert.AreEqual(output1, output2, "Same Guid should produce identical boundary output");
    }

    [TestMethod]
    public void ContentTypeHeader_MatchesBoundaryInBody()
    {
        Guid guid = Guid.NewGuid();
        string contentType = MultipartMixedSerializer.GetContentType(guid);
        string headerBoundary = contentType.Substring(contentType.IndexOf("boundary=") + "boundary=".Length);

        using MemoryStream stream = new();
        MultipartMixedSerializer.WriteClosingBoundary(stream, guid);
        string output = Encoding.UTF8.GetString(stream.ToArray());

        Assert.IsTrue(
            output.Contains($"--{headerBoundary}--"),
            $"Body boundary '{output}' should match header boundary '{headerBoundary}'");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static int FindSubArray(byte[] source, byte[] pattern)
    {
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return i;
            }
        }

        return -1;
    }
}