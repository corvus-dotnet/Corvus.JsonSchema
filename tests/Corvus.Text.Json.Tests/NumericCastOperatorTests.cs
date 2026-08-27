// <copyright file="NumericCastOperatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the direct numeric cast operators on generated numeric types (issue #937).
/// Every cast to a numeric CLR type must bind a user-defined operator directly, so an
/// out-of-range value throws <see cref="FormatException"/> rather than being silently
/// truncated by routing through the implicit conversion to <see langword="long"/>.
/// </summary>
[TestClass]
public class NumericCastOperatorTests
{
    [TestMethod]
    public void FormatType_CastToOwnFormatType_ReturnsValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        JsonUInt16 value = doc.RootElement;
        Assert.AreEqual((ushort)42, (ushort)value);
    }

    [TestMethod]
    public void FormatType_CastToNarrowerType_InRange_ReturnsValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        JsonUInt16 value = doc.RootElement;
        Assert.AreEqual((byte)42, (byte)value);
        Assert.AreEqual((sbyte)42, (sbyte)value);
    }

    [TestMethod]
    public void FormatType_CastToNarrowerType_OutOfRange_Throws()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("300");
        JsonUInt16 value = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => _ = (byte)value);
    }

    [TestMethod]
    public void FormatType_CastToWiderTypes_ReturnsValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("300");
        JsonUInt16 value = doc.RootElement;
        Assert.AreEqual((short)300, (short)value);
        Assert.AreEqual(300, (int)value);
        Assert.AreEqual(300U, (uint)value);
        Assert.AreEqual(300UL, (ulong)value);
        Assert.AreEqual(300L, (long)value);
        Assert.AreEqual(300f, (float)value);
        Assert.AreEqual(300d, (double)value);
        Assert.AreEqual(300m, (decimal)value);
    }

    [TestMethod]
    public void PlainInteger_CastToUShort_OutOfRange_Throws()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("70000");
        JsonInteger value = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => _ = (ushort)value);
    }

    [TestMethod]
    public void PlainInteger_CastToInt_OutOfRange_Throws()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("3000000000");
        JsonInteger value = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => _ = (int)value);
    }

    [TestMethod]
    public void PlainInteger_NegativeValue_CastToUnsigned_Throws()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("-5");
        JsonInteger value = doc.RootElement;
        Assert.ThrowsExactly<FormatException>(() => _ = (uint)value);
        Assert.ThrowsExactly<FormatException>(() => _ = (ulong)value);
        Assert.ThrowsExactly<FormatException>(() => _ = (byte)value);
    }

    [TestMethod]
    public void PlainInteger_InRangeCasts_ReturnValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        JsonInteger value = doc.RootElement;
        Assert.AreEqual((sbyte)42, (sbyte)value);
        Assert.AreEqual((byte)42, (byte)value);
        Assert.AreEqual((short)42, (short)value);
        Assert.AreEqual((ushort)42, (ushort)value);
        Assert.AreEqual(42, (int)value);
        Assert.AreEqual(42U, (uint)value);
        Assert.AreEqual(42L, (long)value);
        Assert.AreEqual(42UL, (ulong)value);
        Assert.AreEqual(42f, (float)value);
        Assert.AreEqual(42d, (double)value);
        Assert.AreEqual(42m, (decimal)value);
    }

#if NET
    [TestMethod]
    public void PlainInteger_NetOnlyCasts_ReturnValue()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("42");
        JsonInteger value = doc.RootElement;
        Assert.AreEqual((Int128)42, (Int128)value);
        Assert.AreEqual((UInt128)42, (UInt128)value);
        Assert.AreEqual((Half)42, (Half)value);
    }
#endif
}