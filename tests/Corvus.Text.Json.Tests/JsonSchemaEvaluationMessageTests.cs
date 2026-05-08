// <copyright file="JsonSchemaEvaluationMessageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the message formatting methods and delegate fields in
/// <see cref="JsonSchemaEvaluation"/> and its partial files.
/// </summary>
[TestClass]
public class JsonSchemaEvaluationMessageTests
{
    #region SchemaLocationForIndexedKeyword

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_AdequateBuffer_WritesExpectedOutput()
    {
        byte[] location = Encoding.UTF8.GetBytes("properties");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(location, 3, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("properties/3", output);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_LocationEndsWithSlash_NoDoubleSlash()
    {
        byte[] location = Encoding.UTF8.GetBytes("properties/");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(location, 5, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("properties/5", output);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_LargeIndex_WritesCorrectly()
    {
        byte[] location = Encoding.UTF8.GetBytes("items");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(location, 12345, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("items/12345", output);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_BufferTooSmallForLocation_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("properties");
        Span<byte> buffer = stackalloc byte[5]; // too small for "properties" (10 bytes)

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(location, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_BufferTooSmallForSlash_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("ab");
        Span<byte> buffer = stackalloc byte[2]; // exactly fits "ab" but no room for "/"

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(location, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_BufferTooSmallForIndex_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("ab");
        Span<byte> buffer = stackalloc byte[3]; // fits "ab/" but no room for index

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(location, 99999, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region SchemaLocationForIndexedKeywordWithDependency

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_AdequateBuffer_WritesExpectedOutput()
    {
        byte[] location = Encoding.UTF8.GetBytes("dependencies");
        byte[] dependency = Encoding.UTF8.GetBytes("foo");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 0, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("dependencies/foo/0", output);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_LocationEndsWithSlash_NoDoubleSlash()
    {
        byte[] location = Encoding.UTF8.GetBytes("dependencies/");
        byte[] dependency = Encoding.UTF8.GetBytes("bar");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 2, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("dependencies/bar/2", output);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_DependencyEndsWithSlash_NoDoubleSlash()
    {
        byte[] location = Encoding.UTF8.GetBytes("dependencies");
        byte[] dependency = Encoding.UTF8.GetBytes("baz/");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 7, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("dependencies/baz/7", output);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmallForLocation_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("dependencies");
        byte[] dependency = Encoding.UTF8.GetBytes("foo");
        Span<byte> buffer = stackalloc byte[5];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmallForFirstSlash_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("ab");
        byte[] dependency = Encoding.UTF8.GetBytes("foo");
        Span<byte> buffer = stackalloc byte[2]; // fits "ab" but no room for "/"

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmallForDependency_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("ab");
        byte[] dependency = Encoding.UTF8.GetBytes("longdependencyname");
        Span<byte> buffer = stackalloc byte[5]; // fits "ab/" but not the full dependency

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmallForSecondSlash_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("ab");
        byte[] dependency = Encoding.UTF8.GetBytes("c");
        // "ab" (2) + "/" (1) + "c" (1) = 4; no room for second "/"
        Span<byte> buffer = stackalloc byte[4];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmallForIndex_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("ab");
        byte[] dependency = Encoding.UTF8.GetBytes("c");
        // "ab" (2) + "/" (1) + "c" (1) + "/" (1) = 5; no room for a big index
        Span<byte> buffer = stackalloc byte[5];

        bool result = JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(location, dependency, 99999, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region TryCopyMessage

    [TestMethod]
    public void TryCopyMessage_AdequateBuffer_CopiesToBuffer()
    {
        byte[] message = Encoding.UTF8.GetBytes("hello world");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyMessage(message, buffer, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(message.Length, written);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("hello world", output);
    }

    [TestMethod]
    public void TryCopyMessage_BufferTooSmall_ReturnsFalse()
    {
        byte[] message = Encoding.UTF8.GetBytes("hello world");
        Span<byte> buffer = stackalloc byte[5];

        bool result = JsonSchemaEvaluation.TryCopyMessage(message, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyMessage_ExactSizeBuffer_Succeeds()
    {
        byte[] message = Encoding.UTF8.GetBytes("test");
        Span<byte> buffer = stackalloc byte[4];

        bool result = JsonSchemaEvaluation.TryCopyMessage(message, buffer, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(4, written);
    }

    #endregion

    #region TryCopyPath

    [TestMethod]
    public void TryCopyPath_SimplePath_CopiesToBuffer()
    {
        byte[] path = Encoding.UTF8.GetBytes("properties");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("properties", output);
    }

    [TestMethod]
    public void TryCopyPath_PathWithLeadingHash_StripsHash()
    {
        byte[] path = Encoding.UTF8.GetBytes("#/properties");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("properties", output);
    }

    [TestMethod]
    public void TryCopyPath_PathWithLeadingSlash_StripsSlash()
    {
        byte[] path = Encoding.UTF8.GetBytes("/properties");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("properties", output);
    }

    [TestMethod]
    public void TryCopyPath_PathWithHashAndSlash_StripsBoth()
    {
        byte[] path = Encoding.UTF8.GetBytes("#/properties/items");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("properties/items", output);
    }

    [TestMethod]
    public void TryCopyPath_EmptyPath_WritesNothing()
    {
        byte[] path = Array.Empty<byte>();
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyPath_HashOnly_WritesNothing()
    {
        byte[] path = Encoding.UTF8.GetBytes("#");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyPath_HashSlashOnly_WritesNothing()
    {
        byte[] path = Encoding.UTF8.GetBytes("#/");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsTrue(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyPath_BufferTooSmall_ReturnsFalse()
    {
        byte[] path = Encoding.UTF8.GetBytes("properties/items");
        Span<byte> buffer = stackalloc byte[5];

        bool result = JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region ExpectedType

    [TestMethod]
    public void ExpectedType_AdequateBuffer_WritesMessage()
    {
        byte[] typeName = Encoding.UTF8.GetBytes("string");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.ExpectedType(typeName, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'string'");
    }

    [TestMethod]
    public void ExpectedType_BufferTooSmall_ReturnsFalse()
    {
        byte[] typeName = Encoding.UTF8.GetBytes("string");
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedType(typeName, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedType_BufferTooSmallForAppendedValue_ReturnsFalse()
    {
        byte[] typeName = Encoding.UTF8.GetBytes("object");
        // A buffer big enough for the SR prefix text but not the appended ' 'object''
        // We use a very small buffer that will fail at the append stage.
        Span<byte> buffer = stackalloc byte[10];

        bool result = JsonSchemaEvaluation.ExpectedType(typeName, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region ExpectedMultipleOfDivisor

    [TestMethod]
    public void ExpectedMultipleOfDivisor_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.ExpectedMultipleOfDivisor("7", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'7'");
    }

    [TestMethod]
    public void ExpectedMultipleOfDivisor_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedMultipleOfDivisor("7", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region IgnoredUnrecognizedFormat

    [TestMethod]
    public void IgnoredUnrecognizedFormat_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.IgnoredUnrecognizedFormat(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void IgnoredUnrecognizedFormat_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.IgnoredUnrecognizedFormat(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region IgnoredFormatNotAsserted

    [TestMethod]
    public void IgnoredFormatNotAsserted_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.IgnoredFormatNotAsserted(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void IgnoredFormatNotAsserted_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.IgnoredFormatNotAsserted(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — Composition

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedMoreThanOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedNoSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedAllSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchAllSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedAtLeastOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedExactlyOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchAtLeastOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedAtLeastOneConstantValue))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchAtLeastOneConstantValue))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchNotSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedNotSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedIfForThen))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchThen))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedThen))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedIfForElse))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchElse))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedElse))]
    [DataRow(nameof(JsonSchemaEvaluation.ThenWithoutIf))]
    [DataRow(nameof(JsonSchemaEvaluation.ElseWithoutIf))]
    public void CompositionMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedMoreThanOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedNoSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedAllSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchAllSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedAtLeastOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedExactlyOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchAtLeastOneSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedAtLeastOneConstantValue))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchAtLeastOneConstantValue))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchNotSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedNotSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedIfForThen))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchThen))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedThen))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedIfForElse))]
    [DataRow(nameof(JsonSchemaEvaluation.DidNotMatchElse))]
    [DataRow(nameof(JsonSchemaEvaluation.MatchedElse))]
    [DataRow(nameof(JsonSchemaEvaluation.ThenWithoutIf))]
    [DataRow(nameof(JsonSchemaEvaluation.ElseWithoutIf))]
    public void CompositionMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — Boolean

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeBoolean))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeBoolean))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedBooleanTrue))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedBooleanFalse))]
    public void BooleanMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeBoolean))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeBoolean))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedBooleanTrue))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedBooleanFalse))]
    public void BooleanMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — Null

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeNull))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeNull))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedNull))]
    public void NullMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeNull))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeNull))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedNull))]
    public void NullMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — Number

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeNumber))]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeInteger))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeNumber))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeInteger))]
    public void NumberMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeNumber))]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeInteger))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeNumber))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeInteger))]
    public void NumberMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — String

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeString))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeString))]
    public void StringMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeString))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeString))]
    public void StringMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — Array

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeArray))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeArray))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedUniqueItems))]
    public void ArrayMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeArray))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeArray))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedUniqueItems))]
    public void ArrayMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate fields — Object

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeObject))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeObject))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyNameMatchesSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyMatchesFallbackSchema))]
    public void ObjectMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.IgnoredNotTypeObject))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedTypeObject))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyNameMatchesSchema))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyMatchesFallbackSchema))]
    public void ObjectMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider provider = GetParameterlessProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Parameterless delegate field — EvaluatedSubschema

    [TestMethod]
    public void EvaluatedSubschema_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.EvaluatedSubschema(buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void EvaluatedSubschema_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.EvaluatedSubschema(buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region String-parameterized delegate fields

    [TestMethod]
    public void ExpectedConstant_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedConstant("myValue", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'myValue'");
    }

    [TestMethod]
    public void ExpectedConstant_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedConstant("myValue", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedMultipleOf_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedMultipleOf("3.5", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'3.5'");
    }

    [TestMethod]
    public void ExpectedMultipleOf_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedMultipleOf("3.5", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedEquals_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedEquals("42", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'42'");
    }

    [TestMethod]
    public void ExpectedEquals_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedEquals("42", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedStringEquals_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedStringEquals("hello", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'hello'");
    }

    [TestMethod]
    public void ExpectedStringEquals_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedStringEquals("hello", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedMatchPatternPropertySchema_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedMatchPatternPropertySchema("^foo.*", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'^foo.*'");
    }

    [TestMethod]
    public void ExpectedMatchPatternPropertySchema_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedMatchPatternPropertySchema("^foo.*", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedPropertyNameMatchesRegularExpression_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedPropertyNameMatchesRegularExpression("[a-z]+", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'[a-z]+'");
    }

    [TestMethod]
    public void ExpectedPropertyNameMatchesRegularExpression_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedPropertyNameMatchesRegularExpression("[a-z]+", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedMatchesDependentSchema_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedMatchesDependentSchema("dependencyProp", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'dependencyProp'");
    }

    [TestMethod]
    public void ExpectedMatchesDependentSchema_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedMatchesDependentSchema("dependencyProp", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Int-parameterized delegate fields — Array item counts

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountNotEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountGreaterThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountGreaterThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountLessThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountLessThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountNotEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountGreaterThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountGreaterThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountLessThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountLessThanOrEquals))]
    public void ArrayIntMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider<int> provider = GetIntProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(5, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'5'");
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountNotEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountGreaterThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountGreaterThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountLessThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedItemCountLessThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountNotEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountGreaterThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountGreaterThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountLessThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedContainsCountLessThanOrEquals))]
    public void ArrayIntMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider<int> provider = GetIntProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(5, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Int-parameterized delegate fields — Object property counts

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountNotEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountLessThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountLessThanOrEquals))]
    public void ObjectIntMessageProviders_AdequateBuffer_WriteNonEmptyMessage(string fieldName)
    {
        JsonSchemaMessageProvider<int> provider = GetIntProvider(fieldName);
        Span<byte> buffer = stackalloc byte[512];

        bool result = provider(10, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'10'");
    }

    [TestMethod]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountNotEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanOrEquals))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountLessThan))]
    [DataRow(nameof(JsonSchemaEvaluation.ExpectedPropertyCountLessThanOrEquals))]
    public void ObjectIntMessageProviders_BufferTooSmall_ReturnFalse(string fieldName)
    {
        JsonSchemaMessageProvider<int> provider = GetIntProvider(fieldName);
        Span<byte> buffer = stackalloc byte[2];

        bool result = provider(10, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region ItemIndex path provider

    [TestMethod]
    public void ItemIndex_AdequateBuffer_WritesIndex()
    {
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.ItemIndex(42, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("42", output);
    }

    [TestMethod]
    public void ItemIndex_ZeroIndex_WritesZero()
    {
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.ItemIndex(0, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("0", output);
    }

    [TestMethod]
    public void ItemIndex_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[1];

        bool result = JsonSchemaEvaluation.ItemIndex(42, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region SchemaLocationForItemIndex

    [TestMethod]
    public void SchemaLocationForItemIndex_AdequateBuffer_WritesExpectedOutput()
    {
        byte[] location = Encoding.UTF8.GetBytes("items");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForItemIndex(location, 7, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("items/7", output);
    }

    [TestMethod]
    public void SchemaLocationForItemIndex_LocationEndsWithSlash_NoDoubleSlash()
    {
        byte[] location = Encoding.UTF8.GetBytes("items/");
        Span<byte> buffer = stackalloc byte[256];

        bool result = JsonSchemaEvaluation.SchemaLocationForItemIndex(location, 0, buffer, out int written);

        Assert.IsTrue(result);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        Assert.AreEqual("items/0", output);
    }

    [TestMethod]
    public void SchemaLocationForItemIndex_BufferTooSmall_ReturnsFalse()
    {
        byte[] location = Encoding.UTF8.GetBytes("items");
        Span<byte> buffer = stackalloc byte[3];

        bool result = JsonSchemaEvaluation.SchemaLocationForItemIndex(location, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region RequiredPropertyNotPresent / RequiredPropertyPresent

    [TestMethod]
    public void RequiredPropertyNotPresent_AdequateBuffer_WritesMessage()
    {
        byte[] propName = Encoding.UTF8.GetBytes("name");
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.RequiredPropertyNotPresent(propName, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'name'");
    }

    [TestMethod]
    public void RequiredPropertyNotPresent_BufferTooSmall_ReturnsFalse()
    {
        byte[] propName = Encoding.UTF8.GetBytes("name");
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.RequiredPropertyNotPresent(propName, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void RequiredPropertyPresent_AdequateBuffer_WritesMessage()
    {
        byte[] propName = Encoding.UTF8.GetBytes("id");
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.RequiredPropertyPresent(propName, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'id'");
    }

    [TestMethod]
    public void RequiredPropertyPresent_BufferTooSmall_ReturnsFalse()
    {
        byte[] propName = Encoding.UTF8.GetBytes("id");
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.RequiredPropertyPresent(propName, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Public static value methods from Object.cs

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    [DataRow(999)]
    public void ExpectedPropertyCountEqualsValue_AdequateBuffer_WritesMessage(int value)
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedPropertyCountEqualsValue(value, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, $"'{value}'");
    }

    [TestMethod]
    public void ExpectedPropertyCountEqualsValue_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedPropertyCountEqualsValue(5, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Public static value methods from Array.cs

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(100)]
    public void ExpectedItemCountEqualsValue_AdequateBuffer_WritesMessage(int value)
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedItemCountEqualsValue(value, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, $"'{value}'");
    }

    [TestMethod]
    public void ExpectedItemCountEqualsValue_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedItemCountEqualsValue(3, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedContainsCountEqualsValue_AdequateBuffer_WritesMessage()
    {
        Span<byte> buffer = stackalloc byte[512];

        bool result = JsonSchemaEvaluation.ExpectedContainsCountEqualsValue(2, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
        string output = Encoding.UTF8.GetString(buffer.Slice(0, written).ToArray());
        StringAssert.Contains(output, "'2'");
    }

    [TestMethod]
    public void ExpectedContainsCountEqualsValue_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> buffer = stackalloc byte[2];

        bool result = JsonSchemaEvaluation.ExpectedContainsCountEqualsValue(2, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Buffer too small for appended value (exercises private AppendSingleQuotedValue and AppendQuotedInteger edge cases)

    [TestMethod]
    public void ExpectedConstant_BufferFitsMessagePrefixButNotValue_ReturnsFalse()
    {
        // First call with a large buffer to find the prefix length
        Span<byte> largeBuffer = stackalloc byte[512];
        JsonSchemaEvaluation.ExpectedConstant("x", largeBuffer, out int fullWritten);

        // A buffer slightly smaller than needed (fullWritten - 1) should fail
        Span<byte> buffer = stackalloc byte[512];
        buffer = buffer.Slice(0, fullWritten - 1);

        bool result = JsonSchemaEvaluation.ExpectedConstant("x", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedItemCountEquals_BufferFitsMessagePrefixButNotInteger_ReturnsFalse()
    {
        // First call with a large buffer to find the full length
        Span<byte> largeBuffer = stackalloc byte[512];
        JsonSchemaEvaluation.ExpectedItemCountEqualsValue(99999, largeBuffer, out int fullWritten);

        // A buffer slightly smaller than needed should fail
        Span<byte> buffer = stackalloc byte[512];
        buffer = buffer.Slice(0, fullWritten - 1);

        bool result = JsonSchemaEvaluation.ExpectedItemCountEqualsValue(99999, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedMatchPatternPropertySchema_BufferFitsMessagePrefixButNotPattern_ReturnsFalse()
    {
        // First call with a large buffer to find the full length
        Span<byte> largeBuffer = stackalloc byte[512];
        JsonSchemaEvaluation.ExpectedMatchPatternPropertySchemaValue("^longpattern.*$", largeBuffer, out int fullWritten);

        // A buffer slightly smaller than needed should fail
        Span<byte> buffer = stackalloc byte[512];
        buffer = buffer.Slice(0, fullWritten - 1);

        bool result = JsonSchemaEvaluation.ExpectedMatchPatternPropertySchemaValue("^longpattern.*$", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedType_EmptyTypeName_SucceedsWithPrefixOnly()
    {
        // Empty type name exercises AppendSingleQuotedValue(ReadOnlySpan<byte>) early return (lines 197-199)
        Span<byte> buffer = stackalloc byte[256];
        bool result = JsonSchemaEvaluation.ExpectedType(ReadOnlySpan<byte>.Empty, buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedMultipleOfDivisor_EmptyDivisor_SucceedsWithPrefixOnly()
    {
        // Empty string exercises AppendSingleQuotedValue(string) early return (lines 219-221)
        Span<byte> buffer = stackalloc byte[256];
        bool result = JsonSchemaEvaluation.ExpectedMultipleOfDivisor("", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedConstant_EmptyValue_SucceedsWithPrefixOnly()
    {
        Span<byte> buffer = stackalloc byte[256];
        bool result = JsonSchemaEvaluation.ExpectedConstant("", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedType_BufferFitsPrefixButNotQuotedValue_ReturnsFalse()
    {
        // Get the prefix length by calling with empty typeName
        Span<byte> largeBuffer = stackalloc byte[256];
        JsonSchemaEvaluation.ExpectedType(ReadOnlySpan<byte>.Empty, largeBuffer, out int prefixLen);

        // Buffer = prefix + 2 bytes: enough for prefix but not for ' 'X'' (needs +4)
        byte[] typeName = Encoding.UTF8.GetBytes("X");
        Span<byte> buffer = stackalloc byte[256];
        buffer = buffer.Slice(0, prefixLen + 2);

        bool result = JsonSchemaEvaluation.ExpectedType(typeName, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedMultipleOfDivisor_BufferFitsPrefixButNotQuotedValue_ReturnsFalse()
    {
        // Get the prefix length by calling with empty divisor
        Span<byte> largeBuffer = stackalloc byte[256];
        JsonSchemaEvaluation.ExpectedMultipleOfDivisor("", largeBuffer, out int prefixLen);

        // Buffer = prefix + 2: enough for prefix but not for ' '7'' (needs +4)
        Span<byte> buffer = stackalloc byte[256];
        buffer = buffer.Slice(0, prefixLen + 2);

        bool result = JsonSchemaEvaluation.ExpectedMultipleOfDivisor("7", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedItemCountEquals_BufferFitsPrefixPlusQuoteButNotDigit_ReturnsFalse()
    {
        // Get the prefix length with a known-good call
        Span<byte> largeBuffer = stackalloc byte[512];
        JsonSchemaEvaluation.ExpectedItemCountEqualsValue(5, largeBuffer, out int fullWritten);

        // Buffer = prefix + 1: room for opening quote but not for the integer digit
        // AppendQuotedInteger: after writing opening quote, TryFormat has 0 bytes → fails (lines 252-255)
        Span<byte> buffer = stackalloc byte[512];

        // The full message is: "prefix'5'" — prefix + opening quote (1) + digit (1) + closing quote (1) = prefix + 3
        // Use prefix + 1 to allow only the opening quote
        int prefixPlusQuote = fullWritten - 2; // minus digit and closing quote
        buffer = buffer.Slice(0, prefixPlusQuote);

        bool result = JsonSchemaEvaluation.ExpectedItemCountEqualsValue(5, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedPropertyCountEquals_BufferFitsPrefixButNotInteger_ReturnsFalse()
    {
        Span<byte> largeBuffer = stackalloc byte[512];
        JsonSchemaEvaluation.ExpectedPropertyCountEqualsValue(42, largeBuffer, out int fullWritten);

        // One byte short
        Span<byte> buffer = stackalloc byte[512];
        buffer = buffer.Slice(0, fullWritten - 1);

        bool result = JsonSchemaEvaluation.ExpectedPropertyCountEqualsValue(42, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedStringEquals_BufferFitsPrefixButNotValue_ReturnsFalse()
    {
        // Get prefix length with empty value
        Span<byte> largeBuffer = stackalloc byte[512];
        JsonSchemaEvaluation.ExpectedStringEquals("", largeBuffer, out int prefixLen);

        // Buffer fits prefix but not appended ' 'hello'' (needs value.Length + 4)
        Span<byte> buffer = stackalloc byte[512];
        buffer = buffer.Slice(0, prefixLen + 2);

        bool result = JsonSchemaEvaluation.ExpectedStringEquals("hello", buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void ExpectedStringEquals_EmptyValue_SucceedsWithPrefixOnly()
    {
        Span<byte> buffer = stackalloc byte[256];
        bool result = JsonSchemaEvaluation.ExpectedStringEquals("", buffer, out int written);

        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void SchemaLocationForItemIndex_BufferTooSmallForSlash_ReturnsFalse()
    {
        // A location without trailing slash, buffer just big enough for the location
        // but not for the appended "/" (exercises Array.cs lines 104-107)
        byte[] location = Encoding.UTF8.GetBytes("items");
        Span<byte> buffer = stackalloc byte[5]; // Exactly fits "items" but no room for "/"

        bool result = JsonSchemaEvaluation.SchemaLocationForItemIndex(location, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void SchemaLocationForItemIndex_BufferTooSmallForIndex_ReturnsFalse()
    {
        // Buffer fits "items/" but not the index number (exercises Array.cs lines 113-116)
        byte[] location = Encoding.UTF8.GetBytes("items");
        Span<byte> buffer = stackalloc byte[6]; // Fits "items/" but not the index digit

        bool result = JsonSchemaEvaluation.SchemaLocationForItemIndex(location, 0, buffer, out int written);

        Assert.IsFalse(result);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Helper methods

    private static JsonSchemaMessageProvider GetParameterlessProvider(string fieldName)
    {
        System.Reflection.FieldInfo field = typeof(JsonSchemaEvaluation).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on JsonSchemaEvaluation");
        return (JsonSchemaMessageProvider)(field.GetValue(null) ?? throw new InvalidOperationException($"Field '{fieldName}' value is null"));
    }

    private static JsonSchemaMessageProvider<int> GetIntProvider(string fieldName)
    {
        System.Reflection.FieldInfo field = typeof(JsonSchemaEvaluation).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on JsonSchemaEvaluation");
        return (JsonSchemaMessageProvider<int>)(field.GetValue(null) ?? throw new InvalidOperationException($"Field '{fieldName}' value is null"));
    }

    #endregion
}
