// <copyright file="FixedDocumentCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="FixedJsonValueDocument{T}"/> and <see cref="FixedStringJsonDocument{T}"/>.
/// </summary>
[TestClass]
public class FixedDocumentCoverageTests
{
    #region FixedJsonValueDocument — Factory methods

    [TestMethod]
    public void ForNumber_CreatesNumberDocument()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        using var doc = (IDisposable)FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        var typedDoc = (FixedJsonValueDocument<JsonElement>)doc;
        JsonElement root = typedDoc.RootElement;
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
    }

    [TestMethod]
    public void ForString_CreatesStringDocument()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        using var doc = (IDisposable)FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        var typedDoc = (FixedJsonValueDocument<JsonElement>)doc;
        JsonElement root = typedDoc.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
    }

    [TestMethod]
    public void ForNumberFromSpan_CreatesNumberDocument()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("123");
        using var doc = (IDisposable)FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(numBytes);
        var typedDoc = (FixedJsonValueDocument<JsonElement>)doc;
        JsonElement root = typedDoc.RootElement;
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
    }

    [TestMethod]
    public void ForStringFromSpan_CreatesStringDocument()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"world\"");
        using var doc = (IDisposable)FixedJsonValueDocument<JsonElement>.ForStringFromSpan(strBytes);
        var typedDoc = (FixedJsonValueDocument<JsonElement>)doc;
        JsonElement root = typedDoc.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
    }

    #endregion

    #region FixedJsonValueDocument — Pool reuse

    [TestMethod]
    public void Pool_ReturnAndReuse_RentFromSpan()
    {
        // Exhaust the pool then exercise RentFromSpan reuse path
        byte[] numBytes = Encoding.UTF8.GetBytes("99");
        var doc1 = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(numBytes);
        ((IDisposable)doc1).Dispose();

        var doc2 = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(numBytes);
        JsonElement root = doc2.RootElement;
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
        ((IDisposable)doc2).Dispose();
    }

    [TestMethod]
    public void Pool_ReturnAndReuse_RentWithMemory()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("7");
        var doc1 = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        ((IDisposable)doc1).Dispose();

        var doc2 = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        JsonElement root = doc2.RootElement;
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
        ((IDisposable)doc2).Dispose();
    }

    [TestMethod]
    public void Pool_MultipleDocuments_ExercisesGrowth()
    {
        // Allocate more than initial pool size (8), dispose all, then reuse
        var docs = new FixedJsonValueDocument<JsonElement>[12];
        for (int i = 0; i < docs.Length; i++)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(i.ToString());
            docs[i] = FixedJsonValueDocument<JsonElement>.ForNumber(bytes);
        }

        for (int i = 0; i < docs.Length; i++)
        {
            ((IDisposable)docs[i]).Dispose();
        }

        // The pool should now have 12 items, exercising the growth path
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(Encoding.UTF8.GetBytes("100"));
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.ValueKind);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — IsImmutable / IsDisposable

    [TestMethod]
    public void FixedValueDoc_IsImmutable_ReturnsTrue()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("1");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.IsImmutable);
        Assert.IsTrue(jsonDoc.IsDisposable);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — GetHashCode

    [TestMethod]
    public void GetHashCode_ForNumber_ReturnsConsistentValue()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int hash1 = jsonDoc.GetHashCode(0);
        int hash2 = jsonDoc.GetHashCode(0);
        Assert.AreEqual(hash1, hash2);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetHashCode_ForString_ReturnsConsistentValue()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int hash1 = jsonDoc.GetHashCode(0);
        int hash2 = jsonDoc.GetHashCode(0);
        Assert.AreEqual(hash1, hash2);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetHashCode_DifferentNumbers_ReturnsValues()
    {
        // Verify GetHashCode doesn't throw for various number forms
        byte[] num1 = Encoding.UTF8.GetBytes("0");
        byte[] num2 = Encoding.UTF8.GetBytes("999999999");
        var doc1 = FixedJsonValueDocument<JsonElement>.ForNumber(num1);
        var doc2 = FixedJsonValueDocument<JsonElement>.ForNumber(num2);
        IJsonDocument jd1 = (IJsonDocument)doc1;
        IJsonDocument jd2 = (IJsonDocument)doc2;
        // Just verify no exception; hash equality is not guaranteed to differ
        _ = jd1.GetHashCode(0);
        _ = jd2.GetHashCode(0);
        ((IDisposable)doc1).Dispose();
        ((IDisposable)doc2).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — TryGetValue numeric types

    [TestMethod]
    public void TryGetValue_Int_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out int value));
        Assert.AreEqual(42, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Int_FailsForString()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetValue(0, out int _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Sbyte_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("-5");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out sbyte value));
        Assert.AreEqual((sbyte)-5, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Byte_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("200");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out byte value));
        Assert.AreEqual((byte)200, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Short_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("1000");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out short value));
        Assert.AreEqual((short)1000, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Ushort_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("60000");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out ushort value));
        Assert.AreEqual((ushort)60000, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Uint_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("3000000000");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out uint value));
        Assert.AreEqual(3000000000u, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Long_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("9000000000");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out long value));
        Assert.AreEqual(9000000000L, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Ulong_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("18000000000000000000");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out ulong value));
        Assert.AreEqual(18000000000000000000UL, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Float_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("3.14");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out float value));
        Assert.AreEqual(3.14f, value, 0.01f);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Double_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("3.14159");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out double value));
        Assert.AreEqual(3.14159, value, 5);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Decimal_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("123.456");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out decimal value));
        Assert.AreEqual(123.456m, value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_BigInteger_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("99999999999999999999");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out System.Numerics.BigInteger value));
        Assert.AreEqual(System.Numerics.BigInteger.Parse("99999999999999999999"), value);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_BigNumber_Success()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("1.23e10");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out Corvus.Numerics.BigNumber _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_ByteArray_ReturnsFalseForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetValue(0, out byte[] _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_DateTime_FailsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetValue(0, out DateTime _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_DateTimeOffset_FailsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetValue(0, out DateTimeOffset _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Guid_FailsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetValue(0, out Guid _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_DateTime_SucceedsForString()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"2024-01-15T10:30:00Z\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTime value));
        Assert.AreEqual(2024, value.Year);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_DateTimeOffset_SucceedsForString()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"2024-01-15T10:30:00+00:00\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTimeOffset value));
        Assert.AreEqual(2024, value.Year);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetValue_Guid_SucceedsForString()
    {
        Guid expected = Guid.NewGuid();
        byte[] strBytes = Encoding.UTF8.GetBytes("\"" + expected.ToString() + "\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out Guid value));
        Assert.AreEqual(expected, value);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — GetString / TryGetString

    [TestMethod]
    public void GetString_ForString_ReturnsUnquotedValue()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        string result = jsonDoc.GetString(0, JsonTokenType.String);
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetString_WithEscapes_ReturnsUnescaped()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        string result = jsonDoc.GetString(0, JsonTokenType.String);
        Assert.AreEqual("hello\nworld", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetString_ForString_Succeeds()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetString(0, JsonTokenType.String, out string result));
        Assert.AreEqual("test", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetString_ForNumber_ReturnsFalse()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetString(0, JsonTokenType.String, out string _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — GetUtf8JsonString / GetUtf16JsonString

    [TestMethod]
    public void GetUtf8JsonString_NoEscape_ReturnsContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf8JsonString utf8Str = jsonDoc.GetUtf8JsonString(0, JsonTokenType.String);
        string result = Encoding.UTF8.GetString(utf8Str.Span.ToArray());
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetUtf8JsonString_WithEscape_ReturnsUnescapedContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\\tworld\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf8JsonString utf8Str = jsonDoc.GetUtf8JsonString(0, JsonTokenType.String);
        string result = Encoding.UTF8.GetString(utf8Str.Span.ToArray());
        Assert.AreEqual("hello\tworld", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetUtf16JsonString_NoEscape_ReturnsContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf16JsonString utf16Str = jsonDoc.GetUtf16JsonString(0, JsonTokenType.String);
        string result = new string(utf16Str.Span.ToArray());
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetUtf16JsonString_WithEscape_ReturnsUnescapedContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf16JsonString utf16Str = jsonDoc.GetUtf16JsonString(0, JsonTokenType.String);
        string result = new string(utf16Str.Span.ToArray());
        Assert.AreEqual("hello\nworld", result);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — TextEquals

    [TestMethod]
    public void TextEquals_Utf8_MatchesStringContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("hello");
        Assert.IsTrue(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TextEquals_Utf8_NoMatchForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("42");
        Assert.IsFalse(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TextEquals_Utf8_WithEscapedContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("hello\nworld");
        Assert.IsTrue(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TextEquals_Char_MatchesStringContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TextEquals(0, "hello".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TextEquals_Char_NoMatchForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TextEquals(0, "42".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TextEquals_Char_WithEscapedContent()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TextEquals(0, "hello\nworld".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TextEquals_Char_Mismatch()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TextEquals(0, "world".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — ToString

    [TestMethod]
    public void ToString_ForNumber_ReturnsNumberText()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("42", jsonDoc.ToString(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void ToString_ForString_ReturnsUnquoted()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("hello", jsonDoc.ToString(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void ToString_ForEscapedString_ReturnsUnescaped()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\\tworld\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("hello\tworld", jsonDoc.ToString(0));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — TryFormat

    [TestMethod]
    public void TryFormat_Char_ForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        char[] dest = new char[10];
        Assert.IsTrue(doc.TryFormat(0, dest, out int charsWritten, default, null));
        Assert.AreEqual("42", new string(dest, 0, charsWritten));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryFormat_Byte_ForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        byte[] dest = new byte[10];
        Assert.IsTrue(doc.TryFormat(0, dest, out int bytesWritten, default, null));
        Assert.AreEqual("42", Encoding.UTF8.GetString(dest, 0, bytesWritten));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryFormat_Byte_DestinationTooSmall()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("12345");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        byte[] dest = new byte[2];
        Assert.IsFalse(doc.TryFormat(0, dest, out int bytesWritten, default, null));
        Assert.AreEqual(0, bytesWritten);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void ToString_WithFormat_ForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        Assert.AreEqual("42", doc.ToString(0, null, null));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — Workspace and MetadataDb

    [TestMethod]
    public void BuildRentedMetadataDb_ReturnsValidDb()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        workspace.RegisterDocument(doc);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int rows = jsonDoc.BuildRentedMetadataDb(0, workspace, out byte[] rentedBacking);
        Assert.IsTrue(rows > 0);
        ArrayPool<byte>.Shared.Return(rentedBacking);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — Unsupported operations throw

    [TestMethod]
    public void GetArrayLength_ThrowsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayLength(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetPropertyCount_ThrowsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetPropertyCount(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetArrayIndexElement_ThrowsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement(0, 0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetNameOfPropertyValue_ThrowsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetNameOfPropertyValue(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetPropertyName_ThrowsForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetPropertyName(0));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — GetRawSimpleValue / GetRawValue

    [TestMethod]
    public void GetRawSimpleValue_WithoutQuotes_ForString()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValue(0, includeQuotes: false);
        string result = Encoding.UTF8.GetString(raw.Span.ToArray());
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetRawSimpleValue_WithQuotes_ForString()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValue(0, includeQuotes: true);
        string result = Encoding.UTF8.GetString(raw.Span.ToArray());
        Assert.AreEqual("\"hello\"", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetRawSimpleValue_ForNumber_ReturnsRawBytes()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValue(0);
        string result = Encoding.UTF8.GetString(raw.Span.ToArray());
        Assert.AreEqual("42", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetRawValueAsString_ForNumber()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("42", jsonDoc.GetRawValueAsString(0));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — ValueIsEscaped / WriteElementTo

    [TestMethod]
    public void ValueIsEscaped_ReturnsFalse()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.ValueIsEscaped(0, false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void WriteElementTo_Number_WritesCorrectly()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            jsonDoc.WriteElementTo(0, writer);
            writer.WriteEndArray();
        }

        string json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.AreEqual("[42]", json);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void WriteElementTo_String_WritesCorrectly()
    {
        byte[] strBytes = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedJsonValueDocument<JsonElement>.ForString(strBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            jsonDoc.WriteElementTo(0, writer);
            writer.WriteEndArray();
        }

        string json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.AreEqual("[\"hello\"]", json);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — TryResolveJsonPointer

    [TestMethod]
    public void TryResolveJsonPointer_EmptyOrSlash_Succeeds()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] pointer = Encoding.UTF8.GetBytes("#");
        Assert.IsTrue(jsonDoc.TryResolveJsonPointer<JsonElement>(pointer, 0, out _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryResolveJsonPointer_DeepPointer_ReturnsFalse()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] pointer = Encoding.UTF8.GetBytes("/foo");
        Assert.IsFalse(jsonDoc.TryResolveJsonPointer<JsonElement>(pointer, 0, out _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — TryGetLineAndOffset

    [TestMethod]
    public void TryGetLineAndOffset_ReturnsFalse()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetLineAndOffset(0, out _, out _, out _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryGetLine_ReturnsFalse()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetLine(0, out ReadOnlyMemory<byte> _));
        Assert.IsFalse(jsonDoc.TryGetLine(0, out string _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedJsonValueDocument — GetDbSize / GetStartIndex / TryFindNextDescendantPropertyValue

    [TestMethod]
    public void GetDbSize_ReturnsDbRowSize()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int size = jsonDoc.GetDbSize(0, false);
        Assert.IsTrue(size > 0);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void GetStartIndex_ReturnsZero()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual(0, jsonDoc.GetStartIndex(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void TryFindNextDescendantPropertyValue_ReturnsFalse()
    {
        byte[] numBytes = Encoding.UTF8.GetBytes("42");
        var doc = FixedJsonValueDocument<JsonElement>.ForNumber(numBytes);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int scanIndex = 0;
        byte[] propName = Encoding.UTF8.GetBytes("foo");
        Assert.IsFalse(jsonDoc.TryFindNextDescendantPropertyValue(0, ref scanIndex, propName, out _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — Parse / Cache

    [TestMethod]
    public void Parse_CreatesStringDocument()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        JsonElement root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void Parse_WithUnescaping_CreatesDocument()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\\"world\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        JsonElement root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void Cache_NestedRent_CreatesNewInstance()
    {
        byte[] rawString1 = Encoding.UTF8.GetBytes("\"first\"");
        byte[] rawString2 = Encoding.UTF8.GetBytes("\"second\"");

        var doc1 = FixedStringJsonDocument<JsonElement>.Parse(rawString1, requiresUnescaping: false);
        var doc2 = FixedStringJsonDocument<JsonElement>.Parse(rawString2, requiresUnescaping: false);

        // Both should be valid
        Assert.AreEqual(JsonValueKind.String, doc1.RootElement.ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc2.RootElement.ValueKind);

        ((IDisposable)doc2).Dispose();
        ((IDisposable)doc1).Dispose();
    }

    [TestMethod]
    public void Cache_ReturnAndReuse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"cached\"");
        var doc1 = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        ((IDisposable)doc1).Dispose();

        var doc2 = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        Assert.AreEqual(JsonValueKind.String, doc2.RootElement.ValueKind);
        ((IDisposable)doc2).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — IsImmutable / IsDisposable

    [TestMethod]
    public void FixedStringDoc_IsImmutable_ReturnsTrue()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.IsImmutable);
        Assert.IsTrue(jsonDoc.IsDisposable);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — GetHashCode

    [TestMethod]
    public void FixedString_GetHashCode_ReturnsConsistentValue()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int hash1 = jsonDoc.GetHashCode(0);
        int hash2 = jsonDoc.GetHashCode(0);
        Assert.AreEqual(hash1, hash2);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — GetString / TryGetString

    [TestMethod]
    public void FixedString_GetString_NoUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        string result = jsonDoc.GetString(0, JsonTokenType.String);
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetString_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        string result = jsonDoc.GetString(0, JsonTokenType.String);
        Assert.AreEqual("hello\nworld", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetString_Succeeds()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"abc\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetString(0, JsonTokenType.String, out string result));
        Assert.AreEqual("abc", result);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — GetUtf8JsonString / GetUtf16JsonString

    [TestMethod]
    public void FixedString_GetUtf8JsonString_NoUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf8JsonString utf8Str = jsonDoc.GetUtf8JsonString(0, JsonTokenType.String);
        string result = Encoding.UTF8.GetString(utf8Str.Span.ToArray());
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetUtf8JsonString_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\tworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf8JsonString utf8Str = jsonDoc.GetUtf8JsonString(0, JsonTokenType.String);
        string result = Encoding.UTF8.GetString(utf8Str.Span.ToArray());
        Assert.AreEqual("hello\tworld", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetUtf16JsonString_NoUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf16JsonString utf16Str = jsonDoc.GetUtf16JsonString(0, JsonTokenType.String);
        string result = new string(utf16Str.Span.ToArray());
        Assert.AreEqual("hello", result);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetUtf16JsonString_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using UnescapedUtf16JsonString utf16Str = jsonDoc.GetUtf16JsonString(0, JsonTokenType.String);
        string result = new string(utf16Str.Span.ToArray());
        Assert.AreEqual("hello\nworld", result);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TextEquals

    [TestMethod]
    public void FixedString_TextEquals_Utf8_Match()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("hello");
        Assert.IsTrue(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TextEquals_Utf8_Mismatch()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("world");
        Assert.IsFalse(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TextEquals_Utf8_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("hello\nworld");
        Assert.IsTrue(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TextEquals_Char_Match()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TextEquals(0, "hello".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TextEquals_Char_Mismatch()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TextEquals(0, "world".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TextEquals_Char_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TextEquals(0, "hello\nworld".AsSpan(), false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TextEquals_Utf8_TooLong_ReturnsFalse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hi\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] otherUtf8 = Encoding.UTF8.GetBytes("toolongstring");
        Assert.IsFalse(jsonDoc.TextEquals(0, otherUtf8, false, true));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — ToString

    [TestMethod]
    public void FixedString_ToString_NoUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("hello", jsonDoc.ToString(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_ToString_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\tworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("hello\tworld", jsonDoc.ToString(0));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryGetValue (numeric types should throw)

    [TestMethod]
    public void FixedString_TryGetValue_Sbyte_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out sbyte _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Byte_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out byte _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Short_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out short _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Ushort_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out ushort _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Int_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out int _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Uint_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out uint _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Long_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out long _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Ulong_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out ulong _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Float_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out float _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Double_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out double _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Decimal_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out decimal _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_BigInteger_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out System.Numerics.BigInteger _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_BigNumber_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"42\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetValue(0, out Corvus.Numerics.BigNumber _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryGetValue base64

    [TestMethod]
    public void FixedString_TryGetValue_Base64_NoUnescape()
    {
        // "SGVsbG8=" is base64 for "Hello"
        byte[] rawString = Encoding.UTF8.GetBytes("\"SGVsbG8=\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out byte[] value));
        Assert.AreEqual("Hello", Encoding.UTF8.GetString(value));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Base64_InvalidData()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"not-base64!!!\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetValue(0, out byte[] _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryGetValue DateTime/Guid

    [TestMethod]
    public void FixedString_TryGetValue_DateTime()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"2024-06-15T12:00:00Z\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTime value));
        Assert.AreEqual(2024, value.Year);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_DateTimeOffset()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"2024-06-15T12:00:00+00:00\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out DateTimeOffset value));
        Assert.AreEqual(2024, value.Year);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetValue_Guid()
    {
        Guid expected = Guid.NewGuid();
        byte[] rawString = Encoding.UTF8.GetBytes("\"" + expected.ToString() + "\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.TryGetValue(0, out Guid value));
        Assert.AreEqual(expected, value);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryFormat

    [TestMethod]
    public void FixedString_TryFormat_Char_NoUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        char[] dest = new char[20];
        Assert.IsTrue(doc.TryFormat(0, dest, out int charsWritten, default, null));
        Assert.AreEqual("hello", new string(dest, 0, charsWritten));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryFormat_Byte_NoUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        byte[] dest = new byte[20];
        Assert.IsTrue(doc.TryFormat(0, dest, out int bytesWritten, default, null));
        Assert.AreEqual("hello", Encoding.UTF8.GetString(dest, 0, bytesWritten));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryFormat_Byte_WithUnescape()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        byte[] dest = new byte[20];
        Assert.IsTrue(doc.TryFormat(0, dest, out int bytesWritten, default, null));
        Assert.AreEqual("hello\nworld", Encoding.UTF8.GetString(dest, 0, bytesWritten));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryFormat_Byte_DestinationTooSmall()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        byte[] dest = new byte[2];
        Assert.IsFalse(doc.TryFormat(0, dest, out int bytesWritten, default, null));
        Assert.AreEqual(0, bytesWritten);
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_ToString_WithFormat()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        Assert.AreEqual("hello", doc.ToString(0, null, null));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — Unsupported operations throw

    [TestMethod]
    public void FixedString_GetArrayLength_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayLength(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetPropertyCount_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetPropertyCount(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetArrayIndexElement_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetArrayIndexElement(0, 0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetNameOfPropertyValue_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.GetNameOfPropertyValue(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_AppendElementToMetadataDb_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using JsonWorkspace workspace = JsonWorkspace.Create();
        MetadataDb db = default;
        Assert.ThrowsExactly<NotSupportedException>(() => jsonDoc.AppendElementToMetadataDb(0, workspace, ref db));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_BuildRentedMetadataDb_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.ThrowsExactly<NotSupportedException>(() => jsonDoc.BuildRentedMetadataDb(0, workspace, out _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — GetRawSimpleValue

    [TestMethod]
    public void FixedString_GetRawSimpleValue_WithQuotes()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValue(0, includeQuotes: true);
        Assert.AreEqual("\"hello\"", Encoding.UTF8.GetString(raw.Span.ToArray()));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetRawSimpleValue_WithoutQuotes()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValue(0, includeQuotes: false);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(raw.Span.ToArray()));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetRawSimpleValue_SingleParam()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        ReadOnlyMemory<byte> raw = jsonDoc.GetRawSimpleValue(0);
        Assert.AreEqual("\"hello\"", Encoding.UTF8.GetString(raw.Span.ToArray()));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetRawValueAsString()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual("\"hello\"", jsonDoc.GetRawValueAsString(0));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — ValueIsEscaped / WriteElementTo

    [TestMethod]
    public void FixedString_ValueIsEscaped_NoUnescape_ReturnsFalse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.ValueIsEscaped(0, false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_ValueIsEscaped_WithUnescape_ReturnsTrue()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\\nworld\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsTrue(jsonDoc.ValueIsEscaped(0, false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_WriteElementTo_WritesCorrectly()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            jsonDoc.WriteElementTo(0, writer);
            writer.WriteEndArray();
        }

        string json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.AreEqual("[\"hello\"]", json);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryResolveJsonPointer

    [TestMethod]
    public void FixedString_TryResolveJsonPointer_Root_Succeeds()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] pointer = Encoding.UTF8.GetBytes("#");
        Assert.IsTrue(jsonDoc.TryResolveJsonPointer<JsonElement>(pointer, 0, out _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryResolveJsonPointer_Deep_ReturnsFalse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] pointer = Encoding.UTF8.GetBytes("/foo");
        Assert.IsFalse(jsonDoc.TryResolveJsonPointer<JsonElement>(pointer, 0, out _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryGetLineAndOffset / TryGetLine

    [TestMethod]
    public void FixedString_TryGetLineAndOffset_ReturnsFalse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetLineAndOffset(0, out _, out _, out _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetLine_ReturnsFalse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.IsFalse(jsonDoc.TryGetLine(0, out ReadOnlyMemory<byte> _));
        Assert.IsFalse(jsonDoc.TryGetLine(0, out string _));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — GetDbSize / GetStartIndex / TryFindNextDescendantPropertyValue / CloneElement

    [TestMethod]
    public void FixedString_GetDbSize_Returns1()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual(1, jsonDoc.GetDbSize(0, false));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetStartIndex_ReturnsZero()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual(0, jsonDoc.GetStartIndex(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryFindNextDescendantPropertyValue_ReturnsFalse()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        int scanIndex = 0;
        byte[] propName = Encoding.UTF8.GetBytes("foo");
        Assert.IsFalse(jsonDoc.TryFindNextDescendantPropertyValue(0, ref scanIndex, propName, out _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_CloneElement_ReturnsElement()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        JsonElement clone = jsonDoc.CloneElement(0);
        Assert.AreEqual(JsonValueKind.String, clone.ValueKind);
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — GetJsonTokenType / GetRawValue

    [TestMethod]
    public void FixedString_GetJsonTokenType_ReturnsString()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.AreEqual(JsonTokenType.String, jsonDoc.GetJsonTokenType(0));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetRawValue_WithQuotes()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        RawUtf8JsonString raw = jsonDoc.GetRawValue(0, includeQuotes: true);
        Assert.AreEqual("\"hello\"", Encoding.UTF8.GetString(raw.Span.ToArray()));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_GetRawValue_WithoutQuotes()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"hello\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        RawUtf8JsonString raw = jsonDoc.GetRawValue(0, includeQuotes: false);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(raw.Span.ToArray()));
        ((IDisposable)doc).Dispose();
    }

    #endregion

    #region FixedStringJsonDocument — TryGetNamedPropertyValue throws

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_Char_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, "foo".AsSpan(), out JsonElement _));
        ((IDisposable)doc).Dispose();
    }

    [TestMethod]
    public void FixedString_TryGetNamedPropertyValue_Utf8_Throws()
    {
        byte[] rawString = Encoding.UTF8.GetBytes("\"test\"");
        var doc = FixedStringJsonDocument<JsonElement>.Parse(rawString, requiresUnescaping: false);
        IJsonDocument jsonDoc = (IJsonDocument)doc;
        byte[] propName = Encoding.UTF8.GetBytes("foo");
        Assert.ThrowsExactly<InvalidOperationException>(() => jsonDoc.TryGetNamedPropertyValue(0, (ReadOnlySpan<byte>)propName, out JsonElement _));
        ((IDisposable)doc).Dispose();
    }

    #endregion
}
