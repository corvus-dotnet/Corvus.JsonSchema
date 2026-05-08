// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class JsonReaderStateAndOptionsTests
{
    [TestMethod]
    public void DefaultJsonReaderOptions()
    {
        JsonReaderOptions options = default;

        var expectedOption = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 0
        };
        Assert.AreEqual(expectedOption, options);

        options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow };
        Assert.AreEqual(expectedOption, options);

        options = new JsonReaderOptions { MaxDepth = 0 };
        Assert.AreEqual(expectedOption, options);

        options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 0 };
        Assert.AreEqual(expectedOption, options);
    }

    [TestMethod]
    public void DefaultJsonReaderState()
    {
        JsonReaderState state = default;

        var expectedOption = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 0
        };
        Assert.AreEqual(expectedOption, state.Options);
    }

    [TestMethod]
    public void JsonReaderStateCtor()
    {
        var state = new JsonReaderState(default);

        var expectedOption = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 0
        };
        Assert.AreEqual(expectedOption, state.Options);

        state = new JsonReaderState(new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 0 });
        Assert.AreEqual(expectedOption, state.Options);

        expectedOption = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        };
        state = new JsonReaderState(new JsonReaderOptions { MaxDepth = 32 });
        Assert.AreEqual(32, state.Options.MaxDepth);
        Assert.AreEqual(expectedOption, state.Options);
    }

    [TestMethod]
    public void JsonReaderStateDefaultCtor()
    {
        var state = new JsonReaderState();

        var expectedOption = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 0
        };
        Assert.AreEqual(expectedOption, state.Options);
    }

    [TestMethod]
    public void MaxDepthIsHonored()
    {
        byte[] dataUtf8 = "{}"u8.ToArray();

        ReadOnlyMemory<byte> dataMemory = dataUtf8;
        var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, 1));
        ReadOnlyMemory<byte> secondMem = dataMemory.Slice(1);
        BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);

        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 };
        var state = new JsonReaderState(options);
        var reader = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        Assert.AreEqual(64, options.MaxDepth);
        Assert.AreEqual(64, reader.CurrentState.Options.MaxDepth);

        state = new JsonReaderState(options);
        reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
        Assert.AreEqual(64, options.MaxDepth);
        Assert.AreEqual(64, reader.CurrentState.Options.MaxDepth);

        options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32 };
        state = new JsonReaderState(options);
        reader = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        Assert.AreEqual(32, options.MaxDepth);
        Assert.AreEqual(32, reader.CurrentState.Options.MaxDepth);

        state = new JsonReaderState(options);
        reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
        Assert.AreEqual(32, options.MaxDepth);
        Assert.AreEqual(32, reader.CurrentState.Options.MaxDepth);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    [DataRow(byte.MaxValue)]
    [DataRow(byte.MaxValue + 4)] // Other values, like byte.MaxValue + 1 overflows to 0 (i.e. JsonCommentHandling.Disallow), which is valid.
    public void TestCommentHandlingInvalid(int enumValue)
    {
        AssertEx.ThrowsExactly<ArgumentOutOfRangeException>("value", () => new JsonReaderOptions { CommentHandling = (JsonCommentHandling)enumValue });
        AssertEx.ThrowsExactly<ArgumentOutOfRangeException>("value", () => new JsonReaderState(new JsonReaderOptions { CommentHandling = (JsonCommentHandling)enumValue }));
    }

    [TestMethod]
    [DataRow(-1)]
    public void TestDepthInvalid(int depth)
    {
        AssertEx.ThrowsExactly<ArgumentOutOfRangeException>("value", () => new JsonReaderOptions { MaxDepth = depth });
        AssertEx.ThrowsExactly<ArgumentOutOfRangeException>("value", () => new JsonReaderState(new JsonReaderOptions { MaxDepth = depth }));
    }

    [TestMethod]
    public void ZeroMaxDepthDefaultsTo64()
    {
        byte[] dataUtf8 = "{}"u8.ToArray();

        ReadOnlyMemory<byte> dataMemory = dataUtf8;
        var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, 1));
        ReadOnlyMemory<byte> secondMem = dataMemory.Slice(1);
        BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);

        var state = new JsonReaderState();
        var reader = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        Assert.AreEqual(64, reader.CurrentState.Options.MaxDepth);

        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 0 };
        state = new JsonReaderState(options);
        reader = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        Assert.AreEqual(0, options.MaxDepth);
        Assert.AreEqual(64, reader.CurrentState.Options.MaxDepth);

        state = new JsonReaderState();
        reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
        Assert.AreEqual(64, reader.CurrentState.Options.MaxDepth);

        state = new JsonReaderState(options);
        reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
        Assert.AreEqual(0, options.MaxDepth);
        Assert.AreEqual(64, reader.CurrentState.Options.MaxDepth);
    }
}
