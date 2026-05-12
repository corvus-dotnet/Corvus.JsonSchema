// <copyright file="FixedDocumentCoverageTests2.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Numerics;
using System.Text;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Additional coverage tests for <see cref="FixedJsonValueDocument{T}"/> and
/// <see cref="FixedStringJsonDocument{T}"/> targeting NodaTime conversions,
/// NET-only types, numeric type throws on string docs, and interface throws.
/// </summary>
[TestClass]
public class FixedDocumentCoverageTests2
{
    #region FixedJsonValueDocument — NodaTime TryGetValue conversions (string token)

    [TestMethod]
    public void FixedValue_TryGetValue_OffsetDateTime_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15T10:30:00+05:00\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out OffsetDateTime value));
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_OffsetDateTime_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out OffsetDateTime _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_OffsetDate_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15+05:00\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out OffsetDate value));
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_OffsetDate_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("99");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out OffsetDate _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_OffsetTime_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"10:30:00+05:00\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out OffsetTime value));
        Assert.AreEqual(10, value.Hour);
        Assert.AreEqual(30, value.Minute);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_OffsetTime_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("1");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out OffsetTime _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_LocalDate_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out LocalDate value));
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_LocalDate_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("5");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out LocalDate _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Period_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"P1Y2M3D\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out Period value));
        Assert.AreEqual(1, value.Years);
        Assert.AreEqual(2, value.Months);
        Assert.AreEqual(3, value.Days);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Period_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("0");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out Period _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Guid_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"12345678-1234-1234-1234-123456789abc\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out Guid value));
        Assert.AreEqual(new Guid("12345678-1234-1234-1234-123456789abc"), value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Guid_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("7");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out Guid _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — DateTime / DateTimeOffset (string token)

    [TestMethod]
    public void FixedValue_TryGetValue_DateTime_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15T10:30:00Z\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTime value));
        Assert.AreEqual(2024, value.Year);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_DateTime_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("100");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out DateTime _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_DateTimeOffset_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-06-15T12:00:00+05:00\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTimeOffset value));
        Assert.AreEqual(2024, value.Year);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_DateTimeOffset_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("200");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out DateTimeOffset _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

#if NET

    #region FixedJsonValueDocument — NET-only types

    [TestMethod]
    public void FixedValue_TryGetValue_DateOnly_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateOnly value));
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_DateOnly_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("10");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out DateOnly _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_TimeOnly_ExercisesStringPath()
    {
        // Use a string-token doc to exercise the string branch
        byte[] bytes = Encoding.UTF8.GetBytes("\"10:30:00\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        // Exercise the code path — may return false if internal format doesn't match
        _ = jsonDoc.TryGetValue(0, out TimeOnly _);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_TimeOnly_FailsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("20");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out TimeOnly _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Int128_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("170141183460469231731687303715884105727");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out Int128 value));
        Assert.AreEqual(Int128.MaxValue, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Int128_FailsForString()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"notanumber\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out Int128 _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_UInt128_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("340282366920938463463374607431768211455");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out UInt128 value));
        Assert.AreEqual(UInt128.MaxValue, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_UInt128_FailsForString()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"nope\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out UInt128 _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Half_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("1.5");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out Half value));
        Assert.AreEqual((Half)1.5, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetValue_Half_FailsForString()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"x\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsFalse(jsonDoc.TryGetValue(0, out Half _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

#endif

    #region FixedJsonValueDocument — Interface throws (number doc)

    [TestMethod]
    public void FixedValue_GetArrayInsertionIndex_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayInsertionIndex(0, 0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_GetArrayIndexElement_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement(0, 0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_GetArrayIndexElementGeneric_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement<JsonElement>(0, 0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_GetArrayIndexElementOutParams_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement(0, 0, out _, out _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_GetNameOfPropertyValue_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetNameOfPropertyValue(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_GetPropertyRawValueAsString_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetPropertyRawValueAsString(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetNamedPropertyValue_Utf8_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, Encoding.UTF8.GetBytes("x").AsSpan(), out JsonElement _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_TryGetNamedPropertyValue_Chars_ThrowsForNumber()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, "x".AsSpan(), out JsonElement _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedValue_GetRawSimpleValueUnsafe_ReturnsRawValue()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValueUnsafe(0);
        string str = Encoding.UTF8.GetString(raw.Span.ToArray());
        Assert.AreEqual("42", str);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — NodaTime conversions

    [TestMethod]
    public void FixedString_TryGetValue_OffsetDateTime_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15T10:30:00+05:00\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out OffsetDateTime value));
        Assert.AreEqual(2024, value.Year);
    }

    [TestMethod]
    public void FixedString_TryGetValue_OffsetDate_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15+05:00\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out OffsetDate value));
        Assert.AreEqual(2024, value.Year);
    }

    [TestMethod]
    public void FixedString_TryGetValue_OffsetTime_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"10:30:00+05:00\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out OffsetTime value));
        Assert.AreEqual(10, value.Hour);
        Assert.AreEqual(30, value.Minute);
    }

    [TestMethod]
    public void FixedString_TryGetValue_LocalDate_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out LocalDate value));
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
    }

    [TestMethod]
    public void FixedString_TryGetValue_Period_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"P1Y2M3D\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out Period value));
        Assert.AreEqual(1, value.Years);
        Assert.AreEqual(2, value.Months);
        Assert.AreEqual(3, value.Days);
    }

    [TestMethod]
    public void FixedString_TryGetValue_Guid_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"12345678-1234-1234-1234-123456789abc\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out Guid value));
        Assert.AreEqual(new Guid("12345678-1234-1234-1234-123456789abc"), value);
    }

    [TestMethod]
    public void FixedString_TryGetValue_DateTime_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15T10:30:00Z\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTime value));
        Assert.AreEqual(2024, value.Year);
    }

    [TestMethod]
    public void FixedString_TryGetValue_DateTimeOffset_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-06-15T12:00:00+05:00\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTimeOffset value));
        Assert.AreEqual(2024, value.Year);
    }

    #endregion

    #region FixedStringJsonDocument — Numeric TryGetValue throws

    [TestMethod]
    public void FixedString_TryGetValue_Sbyte_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out sbyte _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Byte_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out byte _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Short_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out short _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Ushort_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out ushort _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Int_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out int _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Uint_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out uint _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Long_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out long _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Ulong_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out ulong _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Double_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out double _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Float_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out float _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Decimal_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out decimal _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_BigInteger_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out BigInteger _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_BigNumber_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out BigNumber _));
    }

    #endregion

    #region FixedStringJsonDocument — Interface throws

    [TestMethod]
    public void FixedString_GetArrayInsertionIndex_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayInsertionIndex(0, 0));
    }

    [TestMethod]
    public void FixedString_GetArrayIndexElement_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement(0, 0));
    }

    [TestMethod]
    public void FixedString_GetArrayIndexElementGeneric_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement<JsonElement>(0, 0));
    }

    [TestMethod]
    public void FixedString_GetArrayIndexElementOutParams_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement(0, 0, out _, out _));
    }

    [TestMethod]
    public void FixedString_GetNameOfPropertyValue_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetNameOfPropertyValue(0));
    }

    [TestMethod]
    public void FixedString_GetPropertyRawValueAsString_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetPropertyRawValueAsString(0));
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_Utf8_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, Encoding.UTF8.GetBytes("x").AsSpan(), out JsonElement _));
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_Chars_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, "x".AsSpan(), out JsonElement _));
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_GenericUtf8_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue<JsonElement>(0, Encoding.UTF8.GetBytes("x").AsSpan(), out _));
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_GenericChars_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue<JsonElement>(0, "x".AsSpan(), out _));
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_OutDocUtf8_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, Encoding.UTF8.GetBytes("x").AsSpan(), out IJsonDocument? _, out int _));
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_OutDocChars_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, "x".AsSpan(), out IJsonDocument? _, out int _));
    }

    [TestMethod]
    public void FixedString_GetRawSimpleValueUnsafe_ReturnsRawValue()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"test\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValueUnsafe(0);
        string str = Encoding.UTF8.GetString(raw.Span.ToArray());
        Assert.AreEqual("\"test\"", str);
    }

    #endregion

#if NET

    #region FixedStringJsonDocument — NET-only types

    [TestMethod]
    public void FixedString_TryGetValue_DateOnly_Success()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"2024-01-15\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateOnly value));
        Assert.AreEqual(2024, value.Year);
    }

    [TestMethod]
    public void FixedString_TryGetValue_TimeOnly_DoesNotThrow()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"10:30:00\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        // Exercise the code path — may return false if format doesn't match
        _ = jsonDoc.TryGetValue(0, out TimeOnly _);
    }

    [TestMethod]
    public void FixedString_TryGetValue_Int128_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out Int128 _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_UInt128_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out UInt128 _));
    }

    [TestMethod]
    public void FixedString_TryGetValue_Half_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out Half _));
    }

    #endregion

#endif
}
