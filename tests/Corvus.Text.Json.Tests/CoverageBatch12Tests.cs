// Copyright (c) Endjin Limited. All rights reserved.

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 12: DateOnly TryGetValue failure, TryUnescapeAndEncodePointer large buffer,
/// and JsonWorkspace edge cases.
/// </summary>
[TestClass]
public class CoverageBatch12Tests
{
    #region DateOnly TryGetValue failure (JsonReaderHelper.cs line 344-345)

#if NET
    /// <summary>
    /// TryGetValue(DateOnly) with an invalid date segment returns false.
    /// Target: JsonReaderHelper.cs lines 344-345.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryGetValue_DateOnly_InvalidDate_ReturnsFalse()
    {
        // "not-a-date" is not a valid ISO 8601 date
        byte[] segment = "not-a-date"u8.ToArray();
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: false, out DateOnly _);
        Assert.IsFalse(result);
    }
#endif

    #endregion

    #region TryUnescapeAndEncodePointer — large buffer (lines 410-414)

    /// <summary>
    /// TryUnescapeAndEncodePointer with input > 256 bytes after the backslash
    /// triggers the ArrayPool rent path and finally cleanup.
    /// Target: JsonReaderHelper.cs lines 410, 412-414.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void TryUnescapeAndEncodePointer_LargeBuffer_TriggersRentPath()
    {
        // Build input: 1 byte prefix, then \n, then 300 'a' bytes
        // The backslash at index 1 means length - idx = 302 > 256 → ArrayPool path
        byte[] input = new byte[303];
        input[0] = (byte)'x';
        input[1] = (byte)'\\';
        input[2] = (byte)'n'; // Valid escape: \n
        for (int i = 3; i < 303; i++)
        {
            input[i] = (byte)'a';
        }

        // Destination large enough for the result (prefix + unescaped)
        Span<byte> destination = stackalloc byte[512];
        bool result = JsonReaderHelper.TryUnescapeAndEncodePointer(input, destination, out int written);

        // The method should succeed: "x" encoded + unescape("\naaa...") = LF + 300 'a's
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    #endregion

    #region JsonWorkspace.GetDocument out-of-range (lines 236-237)

    /// <summary>
    /// GetDocument with negative index throws ArgumentOutOfRangeException.
    /// Target: JsonWorkspace.cs lines 236-237.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void JsonWorkspace_GetDocument_NegativeIndex_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => workspace.GetDocument(-1));
    }

    /// <summary>
    /// GetDocument with index beyond length throws ArgumentOutOfRangeException.
    /// Target: JsonWorkspace.cs lines 236-237.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void JsonWorkspace_GetDocument_IndexBeyondLength_Throws()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => workspace.GetDocument(999));
    }

    #endregion

    #region JsonWorkspace.Reset with capacity growth (lines 323-326)

    /// <summary>
    /// Reset workspace with a larger initial document capacity triggers re-rent.
    /// Target: JsonWorkspace.cs lines 323-326.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void JsonWorkspace_Reset_WithLargerCapacity_ReRents()
    {
        // CreateUnrented so we have explicit control over lifetime
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            // Create a builder which registers a document in the workspace
            using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"a":1}"""u8.ToArray());

            // Reset with a very large capacity → should trigger re-rent (lines 323-326)
            workspace.Reset(1024, null);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    #endregion

    #region JsonWorkspace.Reset with existing documents (lines 330-332)

    /// <summary>
    /// Reset workspace that has documents uses Array.Clear path.
    /// Target: JsonWorkspace.cs lines 330-332.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void JsonWorkspace_Reset_WithDocuments_ClearsArray()
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            // Create a builder which registers a document in the workspace
            using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"b":2}"""u8.ToArray());

            // Reset with same or smaller capacity → Array.Clear path (lines 330-332)
            workspace.Reset(4, null);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    #endregion

    #region JsonWorkspace.ResetAllStateForCacheReuse on disposed (line 356)

    /// <summary>
    /// ResetAllStateForCacheReuse on an already-disposed workspace throws.
    /// Target: JsonWorkspace.cs line 356.
    /// </summary>
    [TestMethod]
    [TestCategory("coverage")]
    public void JsonWorkspace_ResetAllStateForCacheReuse_AfterDispose_Throws()
    {
        JsonWorkspace workspace = JsonWorkspace.CreateUnrented();

        // Must add a document so _length > 0; Dispose only sets _length = -1 when _length > 0
        using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"x":1}"""u8.ToArray());
        builder.Dispose();
        workspace.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => workspace.ResetAllStateForCacheReuse());
    }

    #endregion
}
