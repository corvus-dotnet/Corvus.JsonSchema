// Copyright (c) Endjin Limited. All rights reserved.

using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="JsonElementTensorHelpers"/> error paths.
/// Targets the FormatException branches (numeric values that can't parse to target type)
/// and the InvalidOperationException catch blocks (rank mismatch in multi-rank arrays).
/// </summary>
public static class JsonElementTensorHelpersCoverageTests
{
    #region TryCopyTo — FormatException when number can't parse to target type

    // Values like 1.5 are valid JSON Number tokens but Utf8Parser can't parse them as integers.
    // This triggers: TryGetValue returns false → ThrowHelper.ThrowFormatException

    [Fact]
    public static void TryCopyTo_Long_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        long[] output = new long[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_ULong_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        ulong[] output = new ulong[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_Int_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        int[] output = new int[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_UInt_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        uint[] output = new uint[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_Short_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        short[] output = new short[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_UShort_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        ushort[] output = new ushort[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_SByte_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        sbyte[] output = new sbyte[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_Byte_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        byte[] output = new byte[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    // Note: Float/Double use Utf8Parser which parses overflow to Infinity, so TryGetValue
    // always returns true for valid JSON numbers. The FormatException path for float/double
    // is effectively dead code — there's no valid JSON number that Utf8Parser.TryParse
    // fails to parse for these types.

    [Fact]
    public static void TryCopyTo_Decimal_ThrowsFormatException_WhenNumberCantParse()
    {
        // Decimal can't handle very large exponents
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1e400, 3]");
        IJsonElement root = doc.RootElement;
        decimal[] output = new decimal[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

#if NET
    [Fact]
    public static void TryCopyTo_Int128_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        Int128[] output = new Int128[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    [Fact]
    public static void TryCopyTo_UInt128_ThrowsFormatException_WhenNumberCantParse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 1.5, 3]");
        IJsonElement root = doc.RootElement;
        UInt128[] output = new UInt128[3];

        Assert.Throws<FormatException>(() =>
            JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, output, out _));
    }

    // Note: Half.TryParse parses overflow to Infinity — the FormatException path
    // for Half is effectively dead code (similar to float/double).
#endif

    #endregion

    #region TryCopyArrayOfRankTo — InvalidOperationException catch blocks

    // When rank > 1, the code recurses. If an inner element is not an array,
    // GetArrayLength throws InvalidOperationException which is caught by the
    // TryCopyArbitraryRankUnsafe catch block.

    [Fact]
    public static void TryCopyArrayOfRankTo_Long_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        long[] output = new long[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_ULong_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        ulong[] output = new ulong[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Int_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        int[] output = new int[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_UInt_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        uint[] output = new uint[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Short_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        short[] output = new short[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_UShort_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        ushort[] output = new ushort[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_SByte_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        sbyte[] output = new sbyte[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Byte_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        byte[] output = new byte[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Float_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        float[] output = new float[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Double_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        double[] output = new double[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Decimal_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        decimal[] output = new decimal[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

#if NET
    [Fact]
    public static void TryCopyArrayOfRankTo_Int128_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        Int128[] output = new Int128[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_UInt128_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        UInt128[] output = new UInt128[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }

    [Fact]
    public static void TryCopyArrayOfRankTo_Half_ThrowsInvalidOperation_WhenInnerElementIsNotArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[[1,2], "not_array"]""");
        IJsonElement root = doc.RootElement;
        Half[] output = new Half[4];

        Assert.Throws<InvalidOperationException>(() =>
            JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, output, 2, out _));
    }
#endif

    #endregion
}
