// <copyright file="CoverageBatch5Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 5: targeting JsonHelpers.Date (DateOnly, year==0),
/// RawUtf8JsonString (TakeOwnership, Dispose), JsonCanonicalizer (max depth, overflow),
/// and JsonWorkspace (GetDocument throw, Reset with capacity/length).
/// </summary>
public static class CoverageBatch5Tests
{
    #region JsonHelpers.Date — DateOnly parse paths (lines 132-143)

#if NET
    /// <summary>
    /// Exercises <c>TryParseAsIso(ReadOnlySpan&lt;byte&gt;, out DateOnly)</c> success path (lines 132-138).
    /// </summary>
    [Fact]
    public static void Date_TryParseAsIso_DateOnly_Success()
    {
        bool result = JsonHelpers.TryParseAsIso("2024-03-15"u8, out DateOnly value);
        Assert.True(result);
        Assert.Equal(new DateOnly(2024, 3, 15), value);
    }

    /// <summary>
    /// Exercises <c>TryParseAsIso(ReadOnlySpan&lt;byte&gt;, out DateOnly)</c> failure path (lines 141-143).
    /// A datetime string with time component is not a calendar-date-only.
    /// </summary>
    [Fact]
    public static void Date_TryParseAsIso_DateOnly_Failure_HasTime()
    {
        bool result = JsonHelpers.TryParseAsIso("2024-03-15T10:30:00Z"u8, out DateOnly value);
        Assert.False(result);
        Assert.Equal(default, value);
    }
#endif

    #endregion

    #region JsonHelpers.Date — TryCreateDateTime year==0 (lines 523-525)

    /// <summary>
    /// Exercises <c>TryCreateDateTime</c> with year 0000, which returns false (lines 523-525).
    /// ISO 8601 year 0000 is not a valid DateTime year.
    /// </summary>
    [Fact]
    public static void Date_TryParseAsISO_Year0000_ReturnsFalse()
    {
        // "0000-01-01" has year=0 which TryCreateDateTime rejects
        bool result = JsonHelpers.TryParseAsISO("0000-01-01"u8, out DateTimeOffset value);
        Assert.False(result);
        Assert.Equal(default, value);
    }

    #endregion

    #region RawUtf8JsonString — TakeOwnership and Dispose (lines 56-77)

    /// <summary>
    /// Exercises <c>RawUtf8JsonString.TakeOwnership</c> with extra rented bytes (lines 56-59).
    /// </summary>
    [Fact]
    public static void RawUtf8JsonString_TakeOwnership_WithExtraRentedBytes()
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(64);
        byte[] data = Encoding.UTF8.GetBytes("\"hello\"");
        // Copy data into rented array to simulate real usage
        data.CopyTo(rented.AsSpan());

        RawUtf8JsonString str = new(rented.AsMemory(0, data.Length), rented);
        ReadOnlyMemory<byte> owned = str.TakeOwnership(out byte[]? extraBytes);

        Assert.NotNull(extraBytes);
        Assert.Equal(rented, extraBytes);
        Assert.Equal(data.Length, owned.Length);

        // Return the rented array ourselves since TakeOwnership transfers ownership
        ArrayPool<byte>.Shared.Return(extraBytes!);
    }

    /// <summary>
    /// Exercises <c>RawUtf8JsonString.Dispose</c> with extra rented bytes (lines 67-77).
    /// </summary>
    [Fact]
    public static void RawUtf8JsonString_Dispose_WithExtraRentedBytes()
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(64);
        byte[] data = Encoding.UTF8.GetBytes("\"world\"");
        data.CopyTo(rented.AsSpan());

        RawUtf8JsonString str = new(rented.AsMemory(0, data.Length), rented);
        // Dispose should return the rented array to the pool
        str.Dispose();

        // After dispose, TakeOwnership should return null for extraBytes
        ReadOnlyMemory<byte> owned = str.TakeOwnership(out byte[]? extraBytes);
        Assert.Null(extraBytes);
    }

    #endregion

    #region JsonCanonicalizer — max depth exceeded (lines 123-124)

    /// <summary>
    /// Exercises the max depth check in <c>JsonCanonicalizer</c> (lines 122-124).
    /// Creates JSON nested 65 levels deep (MaxDepth=64).
    /// </summary>
    [Fact]
    public static void Canonicalizer_MaxDepthExceeded_ThrowsInvalidOperation()
    {
        // Build JSON nested 65 levels: [[[[...]]]]
        StringBuilder sb = new();
        for (int i = 0; i < 65; i++)
        {
            sb.Append('[');
        }

        sb.Append("1");

        for (int i = 0; i < 65; i++)
        {
            sb.Append(']');
        }

        // Parse with higher max depth so the parser accepts 65 levels
        JsonDocumentOptions parseOptions = new() { MaxDepth = 128 };
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(
                Encoding.UTF8.GetBytes(sb.ToString()),
                parseOptions);

        byte[] buffer = new byte[256];
        InvalidOperationException? ex = null;
        try
        {
            JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out _);
        }
        catch (InvalidOperationException e)
        {
            ex = e;
        }

        Assert.NotNull(ex);
        Assert.Contains("depth", ex!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region JsonCanonicalizer — overflow then number (lines 284-285)

    /// <summary>
    /// Exercises the overflow early-return in <c>WriteNumber</c> (lines 283-285).
    /// Uses a tiny buffer so the first element (true) overflows via WriteBytes,
    /// then the second element (42) hits the overflow check in WriteNumber.
    /// </summary>
    [Fact]
    public static void Canonicalizer_OverflowBeforeNumber_EarlyReturn()
    {
        // [true,42] — "true" needs 4 bytes, after "[" (1 byte) we have only 1 byte left
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""[true,42]"""u8.ToArray());

        // Buffer of 2 bytes: "[" writes 1 byte, then "true" overflows (needs 4, only 1 left)
        // Then "," and "42" are attempted — WriteNumber hits overflow early-return
        Span<byte> buffer = stackalloc byte[2];
        bool result = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int bytesWritten);
        Assert.False(result);
    }

    #endregion

    #region JsonCanonicalizer — overflow then WriteBytes (lines 396-397)

    /// <summary>
    /// Exercises the overflow early-return in <c>WriteBytes</c> (lines 395-397).
    /// Uses a tiny buffer so the first element (a number) overflows,
    /// then the second element (null) hits the overflow check in WriteBytes/WriteLiteral.
    /// </summary>
    [Fact]
    public static void Canonicalizer_OverflowBeforeWriteBytes_EarlyReturn()
    {
        // [42,null] — "42" formatted by Es6NumberFormatter won't fit in tiny remaining space
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse("""[42,null]"""u8.ToArray());

        // Buffer of 2 bytes: "[" takes 1 byte, number formatting fails (overflow set),
        // then "null" WriteBytes hits overflow early-return
        Span<byte> buffer = stackalloc byte[2];
        bool result = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int bytesWritten);
        Assert.False(result);
    }

    #endregion

    #region JsonWorkspace — GetDocument out of range (lines 236-237)

    /// <summary>
    /// Exercises <c>JsonWorkspace.GetDocument</c> with negative index (lines 236-237).
    /// </summary>
    [Fact]
    public static void Workspace_GetDocument_NegativeIndex_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetDocument(-1));
    }

    /// <summary>
    /// Exercises <c>JsonWorkspace.GetDocument</c> with index beyond length (lines 236-237).
    /// </summary>
    [Fact]
    public static void Workspace_GetDocument_BeyondLength_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.GetDocument(0));
    }

    #endregion

    #region JsonWorkspace — Reset with documents present (lines 330-332)

    /// <summary>
    /// Exercises <c>JsonWorkspace.Reset</c> when _length > 0 (lines 330-332).
    /// Creates a builder (which adds a document), then calls Reset with same capacity.
    /// </summary>
    [Fact]
    public static void Workspace_Reset_WithExistingDocuments_ClearsArray()
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented(initialDocumentCapacity: 5);

        // Add a document by creating a builder from parsed JSON
        using ParsedJsonDocument<JsonElement> source =
            ParsedJsonDocument<JsonElement>.Parse("""{"key":"value"}"""u8.ToArray());

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            source.RootElement.CreateBuilder(workspace);

        // Now workspace has _length > 0. Reset with same capacity (no regrow needed)
        // so it takes the else branch and hits Array.Clear (lines 330-332).
        workspace.Reset(initialDocumentCapacity: 5, options: null);
    }

    #endregion

    #region JsonWorkspace — Reset with larger capacity (lines 323-326)

    /// <summary>
    /// Exercises <c>JsonWorkspace.Reset</c> when new capacity exceeds current array (lines 323-326).
    /// </summary>
    [Fact]
    public static void Workspace_Reset_WithLargerCapacity_ReallocatesArray()
    {
        // Create with small initial capacity
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented(initialDocumentCapacity: 2);

        // Reset with much larger capacity — triggers the regrow path (lines 323-326)
        workspace.Reset(initialDocumentCapacity: 100, options: null);
    }

    #endregion

    #region JsonWorkspace — ResetAllStateForCacheReuse when disposed (line 356)

    /// <summary>
    /// Exercises <c>JsonWorkspace.ResetAllStateForCacheReuse</c> when already disposed (line 356).
    /// </summary>
    [Fact]
    public static void Workspace_ResetAllStateForCacheReuse_WhenDisposed_Throws()
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();

        // Add a document so _length > 0, then Dispose sets _length = -1
        using ParsedJsonDocument<JsonElement> source =
            ParsedJsonDocument<JsonElement>.Parse("""{"a":1}"""u8.ToArray());
        source.RootElement.CreateBuilder(workspace).Dispose();

        workspace.Dispose(); // sets _length = -1 because _length > 0

        // ResetAllStateForCacheReuse checks _length >= 0, fails → ThrowObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => workspace.ResetAllStateForCacheReuse());
    }

    #endregion
}
