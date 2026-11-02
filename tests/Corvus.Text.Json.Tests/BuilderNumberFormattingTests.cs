// <copyright file="BuilderNumberFormattingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// A double or float added to a builder is written with enough digits to read back as the same number, on every
/// target. On .NET Framework, <c>Utf8Formatter</c> writes 15 significant digits for a double (7 for a float), so
/// 90.57000000000001 was written as 90.57.
/// </summary>
[TestClass]
public class BuilderNumberFormattingTests
{
    [TestMethod]
    [DataRow(90.57000000000001)]
    [DataRow(45.285000000000004)]
    [DataRow(0.30000000000000004)]
    [DataRow(0.1)]
    [DataRow(1.0 / 3.0)]
    [DataRow(123456789012345680.0)]
    [DataRow(1.7976931348623157E+308)]
    [DataRow(2.2250738585072014E-308)]
    [DataRow(5E-324)]
    [DataRow(-6.02214076E+23)]
    [DataRow(0.0)]
    public void ADoubleReadsBackAsTheSameDouble(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        using var mutable = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{\"items\":[]}");
        mutable.RootElement.SetProperty("x", value);
        Assert.IsTrue(mutable.RootElement.TryGetProperty("items", out JsonElement.Mutable items));
        items.AddItem(value);
        Assert.IsTrue(mutable.RootElement.TryGetProperty("x", out JsonElement.Mutable x));
        AssertReadsBack(value, x.GetRawText(), "Mutable.SetProperty");
        AssertReadsBack(value, items[0].GetRawText(), "Mutable.AddItem");

        using var array = JsonElement.CreateBuilder(workspace, value, static (in double v, ref JsonElement.ArrayBuilder b) => b.AddItem(v), 1);
        AssertReadsBack(value, array.RootElement[0].GetRawText(), "ArrayBuilder.AddItem");

        using var obj = JsonElement.CreateBuilder(workspace, value, static (in double v, ref JsonElement.ObjectBuilder b) => b.AddProperty("x"u8, v), 1);
        Assert.IsTrue(obj.RootElement.TryGetProperty("x", out JsonElement.Mutable property));
        AssertReadsBack(value, property.GetRawText(), "ObjectBuilder.AddProperty");
    }

    [TestMethod]
    [DataRow(0.1f)]
    [DataRow(1.0f / 3.0f)]
    [DataRow(16777217.0f)]
    [DataRow(3.4028235E+38f)]
    [DataRow(1.17549435E-38f)]
    [DataRow(1E-45f)]
    [DataRow(-2.71828183f)]
    public void AFloatReadsBackAsTheSameFloat(float value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        using var mutable = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]");
        mutable.RootElement.AddItem(value);
        AssertReadsBack(value, mutable.RootElement[0].GetRawText(), "Mutable.AddItem");

        using var array = JsonElement.CreateBuilder(workspace, value, static (in float v, ref JsonElement.ArrayBuilder b) => b.AddItem(v), 1);
        AssertReadsBack(value, array.RootElement[0].GetRawText(), "ArrayBuilder.AddItem");

        using var obj = JsonElement.CreateBuilder(workspace, value, static (in float v, ref JsonElement.ObjectBuilder b) => b.AddProperty("x"u8, v), 1);
        Assert.IsTrue(obj.RootElement.TryGetProperty("x", out JsonElement.Mutable property));
        AssertReadsBack(value, property.GetRawText(), "ObjectBuilder.AddProperty");
    }

    private static void AssertReadsBack(double expected, string text, string path)
    {
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)), $"{path} wrote {text}");
    }

    private static void AssertReadsBack(float expected, string text, string path)
    {
        Assert.AreEqual(expected, float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), $"{path} wrote {text}");
    }
}
