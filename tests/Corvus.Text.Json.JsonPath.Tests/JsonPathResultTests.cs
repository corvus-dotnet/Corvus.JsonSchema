// <copyright file="JsonPathResultTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.JsonPath;
using Xunit;

namespace Corvus.Text.Json.JsonPath.Tests;

/// <summary>
/// Tests for <see cref="JsonPathResult"/> covering the fits-in-initial-buffer,
/// spills-once, and spills-with-enlargement scenarios.
/// </summary>
public class JsonPathResultTests
{
    /// <summary>
    /// When the query result fits in the initial pooled buffer (16 elements),
    /// <see cref="JsonPathResult.HasSpilled"/> stays true because CreatePooled
    /// always uses ArrayPool. But the key metric is 0 B managed allocation.
    /// </summary>
    [Fact]
    public void QueryNodes_FitsInBuffer_ReturnsCorrectResults()
    {
        // $.store.book[0].title returns 1 node — well within any buffer
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.store.book[0].title", data);

        Assert.Equal(1, result.Count);
        Assert.Equal(JsonValueKind.String, result[0].ValueKind);
        Assert.Equal("Sayings of the Century", result[0].GetString());
    }

    /// <summary>
    /// $.store.book[*] returns 4 nodes — fits in initial 16-element pooled buffer.
    /// </summary>
    [Fact]
    public void QueryNodes_MultipleResults_ReturnsAll()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$.store.book[*]", data);

        Assert.Equal(4, result.Count);
    }

    /// <summary>
    /// $..* on the bookstore JSON returns many nodes — may spill beyond initial buffer.
    /// </summary>
    [Fact]
    public void QueryNodes_RecursiveDescent_SpillsCorrectly()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes("$..*", data);

        // The exact count depends on the document structure; verify we get a reasonable number
        Assert.True(result.Count > 16, $"Expected more than 16 results for $..*; got {result.Count}");
    }

    /// <summary>
    /// Verifies that JsonPathResult with a caller-provided span works correctly
    /// when the result fits.
    /// </summary>
    [Fact]
    public void QueryNodes_WithInitialBuffer_FitsInBuffer()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        JsonElement[] buf = new JsonElement[32];
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.store.book[*].author", data, buf.AsSpan());

        Assert.Equal(4, result.Count);
        Assert.False(result.HasSpilled);
    }

    /// <summary>
    /// Verifies that JsonPathResult with a small caller-provided buffer spills correctly.
    /// </summary>
    [Fact]
    public void QueryNodes_WithSmallBuffer_Spills()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        JsonElement[] buf = new JsonElement[2]; // Only 2 slots, but 4 results
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.store.book[*].author", data, buf.AsSpan());

        Assert.Equal(4, result.Count);
        Assert.True(result.HasSpilled);
    }

    /// <summary>
    /// Verifies that a large recursive query causes multiple enlargements and still
    /// produces correct results.
    /// </summary>
    [Fact]
    public void QueryNodes_WithTinyBuffer_SpillsWithEnlargement()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        JsonElement[] buf = new JsonElement[1]; // Tiny buffer forces multiple growths
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$..*", data, buf.AsSpan());

        Assert.True(result.Count > 16, $"Expected more than 16 results for $..*; got {result.Count}");
        Assert.True(result.HasSpilled);
    }

    /// <summary>
    /// Verifies Query convenience method still works correctly.
    /// </summary>
    [Fact]
    public void Query_ConvenienceMethod_ReturnsMaterializedArray()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        JsonElement result = JsonPathEvaluator.Default.Query("$.store.book[*].author", data);

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(4, result.GetArrayLength());
    }

    /// <summary>
    /// Verifies that an empty result returns the empty array.
    /// </summary>
    [Fact]
    public void Query_NoMatch_ReturnsEmptyArray()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        JsonElement result = JsonPathEvaluator.Default.Query("$.nonexistent", data);

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(0, result.GetArrayLength());
    }

    /// <summary>
    /// Verifies the Nodes span provides the correct elements.
    /// </summary>
    [Fact]
    public void QueryNodes_NodesSpan_MatchesIndexer()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);
        using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
            "$.store.book[*].title", data);

        ReadOnlySpan<JsonElement> nodes = result.Nodes;
        Assert.Equal(result.Count, nodes.Length);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(result[i].GetString(), nodes[i].GetString());
        }
    }

    /// <summary>
    /// Verifies that when a caller-rented buffer spills, the caller can still
    /// safely return the original buffer to the pool without a double-return.
    /// <see cref="JsonPathResult.Dispose"/> only returns the internally-rented
    /// overflow array; the caller's original buffer is untouched.
    /// </summary>
    [Fact]
    public void QueryNodes_CallerRentedBuffer_SpillDoesNotDoubleReturn()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);

        // Rent a buffer that is too small (2 slots, but 4 results)
        JsonElement[] callerBuf = ArrayPool<JsonElement>.Shared.Rent(2);
        try
        {
            using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
                "$.store.book[*].author", data, callerBuf.AsSpan(0, 2));

            Assert.Equal(4, result.Count);
            Assert.True(result.HasSpilled);

            // Verify all results are correct even after spill
            Assert.Equal("Nigel Rees", result[0].GetString());
            Assert.Equal("J. R. R. Tolkien", result[3].GetString());
        }
        finally
        {
            // This must succeed — the caller's buffer was NOT returned by Dispose
            ArrayPool<JsonElement>.Shared.Return(callerBuf);
        }
    }

    /// <summary>
    /// Verifies that when a caller-rented buffer spills multiple times (tiny buffer,
    /// many results), only the final overflow array is returned by Dispose and the
    /// caller's original buffer remains theirs to return.
    /// </summary>
    [Fact]
    public void QueryNodes_CallerRentedBuffer_MultiGrowDoesNotDoubleReturn()
    {
        JsonElement data = JsonElement.ParseValue(BookstoreJson);

        // Rent a tiny buffer — forces multiple grow operations
        JsonElement[] callerBuf = ArrayPool<JsonElement>.Shared.Rent(1);
        try
        {
            using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
                "$..*", data, callerBuf.AsSpan(0, 1));

            Assert.True(result.Count > 16, $"Expected more than 16 results; got {result.Count}");
            Assert.True(result.HasSpilled);
        }
        finally
        {
            // Must succeed without double-return
            ArrayPool<JsonElement>.Shared.Return(callerBuf);
        }
    }

    private static readonly byte[] BookstoreJson = """
        {
          "store": {
            "book": [
              {
                "category": "reference",
                "author": "Nigel Rees",
                "title": "Sayings of the Century",
                "price": 8.95
              },
              {
                "category": "fiction",
                "author": "Evelyn Waugh",
                "title": "Sword of Honour",
                "price": 12.99
              },
              {
                "category": "fiction",
                "author": "Herman Melville",
                "title": "Moby Dick",
                "isbn": "0-553-21311-3",
                "price": 8.99
              },
              {
                "category": "fiction",
                "author": "J. R. R. Tolkien",
                "title": "The Lord of the Rings",
                "isbn": "0-395-19395-8",
                "price": 22.99
              }
            ],
            "bicycle": {
              "color": "red",
              "price": 19.95
            }
          }
        }
        """u8.ToArray();
}
