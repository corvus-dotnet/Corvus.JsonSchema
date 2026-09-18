// <copyright file="GeneratedCodeFileChunkTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// A generated file holds its text in chunks below the large object heap; the string, the reader and the writer all
/// give back the same text, and the string is built once.
/// </summary>
[TestClass]
public class GeneratedCodeFileChunkTests
{
    [TestMethod]
    public void ChunkedFile_StringReaderAndWriterAgree_AndTheStringIsBuiltOnce()
    {
        string text = string.Concat(Enumerable.Range(0, 3000).Select(i => $"line {i}: the quick brown fox jumps over the lazy dog\r\n"));
        Assert.IsTrue(text.Length > 2 * GeneratedCodeFile.ChunkSize, "the text spans more than two chunks");
        ReadOnlyMemory<char>[] chunks = Split(text, GeneratedCodeFile.ChunkSize);

        GeneratedCodeFile file = new("Big.cs", chunks);

        Assert.AreEqual(text.Length, file.Length);
        Assert.AreEqual(chunks.Length, file.Chunks.Count);
        Assert.AreEqual(text, file.FileContent);
        Assert.AreSame(file.FileContent, file.FileContent, "materialised once");
        Assert.AreEqual(text, file.OpenReader().ReadToEnd());
        using StringWriter writer = new();
        file.WriteTo(writer);
        Assert.AreEqual(text, writer.ToString());

        using TextReader reader = file.OpenReader();
        char[] buffer = new char[7];
        Assert.AreEqual('l', (char)reader.Peek());
        Assert.AreEqual('l', (char)reader.Read());
        Assert.AreEqual(7, reader.Read(buffer, 0, 7));
        Assert.AreEqual("ine 0: ", new string(buffer));
    }

    [TestMethod]
    public void StringFile_IsOneChunk_AndAnEmptyFileHasNone()
    {
        GeneratedCodeFile file = new("Small.cs", "// small");
        Assert.AreEqual(1, file.Chunks.Count);
        Assert.AreEqual(8, file.Length);
        Assert.AreEqual("// small", file.OpenReader().ReadToEnd());

        GeneratedCodeFile empty = new("Empty.cs", string.Empty);
        Assert.AreEqual(0, empty.Chunks.Count);
        Assert.AreEqual(0, empty.Length);
        Assert.AreEqual(-1, empty.OpenReader().Read());
        Assert.AreEqual(string.Empty, empty.FileContent);
    }

    [TestMethod]
    public async Task GeneratedFiles_AreCapturedInChunks_AndRoundTrip()
    {
        const string Schema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": { "name": { "type": "string" }, "size": { "type": "integer" }, "tags": { "type": "array", "items": { "type": "string" } } },
              "required": ["name"]
            }
            """;
        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(Schema, new CSharpLanguageProvider.Options(defaultNamespace: "Test"));

        Assert.IsTrue(files.Any(f => f.Chunks.Count > 1), "at least one generated partial spans more than one chunk");
        foreach (GeneratedCodeFile file in files)
        {
            using StringWriter writer = new();
            file.WriteTo(writer);
            Assert.AreEqual(file.FileContent, writer.ToString(), file.FileName);
            Assert.AreEqual(file.Length, file.FileContent.Length, file.FileName);
            Assert.IsTrue(file.Chunks.All(c => c.Length <= GeneratedCodeFile.ChunkSize), file.FileName);
        }
    }

    [TestMethod]
    public async Task GeneratedFiles_CapturedAsStrings_ReturnTheBackingStringWithoutACopy()
    {
        const string Schema =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": { "name": { "type": "string" } }
            }
            """;
        IReadOnlyCollection<GeneratedCodeFile> files = await InProcessGenerationTests.GenerateInProcessFromContent(Schema, new CSharpLanguageProvider.Options(defaultNamespace: "Test", storeFilesAsStrings: true));

        Assert.IsTrue(files.Count > 0);
        foreach (GeneratedCodeFile file in files.Where(f => f.Length > 0))
        {
            Assert.AreEqual(1, file.Chunks.Count, file.FileName);
            Assert.IsTrue(System.Runtime.InteropServices.MemoryMarshal.TryGetString(file.Chunks[0], out string backing, out int start, out int count), file.FileName);
            Assert.AreEqual(0, start);
            Assert.AreEqual(backing.Length, count);
            Assert.AreSame(backing, file.FileContent, "no copy for a string-captured file");
        }
    }

    private static ReadOnlyMemory<char>[] Split(string text, int size)
    {
        List<ReadOnlyMemory<char>> chunks = [];
        for (int i = 0; i < text.Length; i += size)
        {
            chunks.Add(text.AsMemory(i, Math.Min(size, text.Length - i)));
        }

        return [.. chunks];
    }
}