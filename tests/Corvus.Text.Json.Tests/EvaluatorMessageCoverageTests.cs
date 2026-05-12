// <copyright file="EvaluatorMessageCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for evaluator error message and schema location helpers.
/// These exercise the public static methods on <see cref="JsonSchemaEvaluation"/>
/// (and its Array/Object partial classes) that generate detailed validation messages.
/// </summary>
[TestClass]
public class EvaluatorMessageCoverageTests
{
    #region SchemaLocationForIndexedKeyword

    [TestMethod]
    [DataRow("/items", 0, "items/0")]
    [DataRow("/items", 5, "items/5")]
    [DataRow("/items/", 3, "items/3")]
    [DataRow("/prefixItems", 99, "prefixItems/99")]
    public void SchemaLocationForIndexedKeyword_Succeeds(string baseLocation, int index, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseLocation);
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(baseBytes, index, buffer, out int written));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_BufferTooSmall_ReturnsFalse()
    {
        byte[] baseBytes = "/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3]; // Too small
        Assert.IsFalse(JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(baseBytes, 0, buffer, out _));
    }

    #endregion

    #region SchemaLocationForIndexedKeywordWithDependency

    [TestMethod]
    [DataRow("/properties", "dependentSchemas", 0, "properties/dependentSchemas/0")]
    [DataRow("/properties/", "dependentSchemas/", 5, "properties/dependentSchemas/5")]
    public void SchemaLocationForIndexedKeywordWithDependency_Succeeds(string baseLocation, string dependency, int index, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseLocation);
        byte[] depBytes = Encoding.UTF8.GetBytes(dependency);
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(baseBytes, depBytes, index, buffer, out int written));
        Assert.IsTrue(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmall_ReturnsFalse()
    {
        byte[] baseBytes = "/properties"u8.ToArray();
        byte[] depBytes = "dependentSchemas"u8.ToArray();
        Span<byte> buffer = stackalloc byte[5]; // Too small
        Assert.IsFalse(JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(baseBytes, depBytes, 0, buffer, out _));
    }

    #endregion

    #region TryCopyPath and TryCopyMessage

    [TestMethod]
    public void TryCopyPath_Succeeds()
    {
        byte[] path = "/foo/bar"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.AreEqual("foo/bar", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void TryCopyPath_BufferTooSmall_ReturnsFalse()
    {
        byte[] path = "/foo/bar"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3]; // Too small
        Assert.IsFalse(JsonSchemaEvaluation.TryCopyPath(path, buffer, out _));
    }

    [TestMethod]
    public void TryCopyMessage_Succeeds()
    {
        byte[] message = "validation failed"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyMessage(message, buffer, out int written));
        Assert.AreEqual("validation failed", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void TryCopyMessage_BufferTooSmall_ReturnsFalse()
    {
        byte[] message = "validation failed"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3]; // Too small
        Assert.IsFalse(JsonSchemaEvaluation.TryCopyMessage(message, buffer, out _));
    }

    #endregion

    #region ExpectedType, ExpectedMultipleOfDivisor, IgnoredFormat*

    [TestMethod]
    public void ExpectedType_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedType("integer"u8, buffer, out int written));
        Assert.IsTrue(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        StringAssert.Contains(result, "integer");
    }

    [TestMethod]
    public void ExpectedType_BufferTooSmall_ReturnsFalse()
    {
        byte[] buffer = new byte[3];
        Assert.IsFalse(JsonSchemaEvaluation.ExpectedType("integer"u8, buffer, out _));
    }

    [TestMethod]
    public void ExpectedMultipleOfDivisor_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedMultipleOfDivisor("3", buffer, out int written));
        Assert.IsTrue(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        StringAssert.Contains(result, "3");
    }

    [TestMethod]
    public void ExpectedMultipleOfDivisor_BufferTooSmall_ReturnsFalse()
    {
        byte[] buffer = new byte[3];
        Assert.IsFalse(JsonSchemaEvaluation.ExpectedMultipleOfDivisor("3", buffer, out _));
    }

    [TestMethod]
    public void IgnoredUnrecognizedFormat_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.IgnoredUnrecognizedFormat(buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void IgnoredFormatNotAsserted_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.IgnoredFormatNotAsserted(buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedType_BufferFitsPrefix_ButNotValue_ReturnsFalse()
    {
        // "The value was expected to be of type" is ~40 chars = 40 UTF-8 bytes.
        // A buffer of 42 fits the prefix but not the appended ' 'integer'' (11 more chars).
        Span<byte> buffer = stackalloc byte[42];
        Assert.IsFalse(JsonSchemaEvaluation.ExpectedType("integer"u8, buffer, out _));
    }

    [TestMethod]
    public void ExpectedMultipleOfDivisor_BufferFitsPrefix_ButNotValue_ReturnsFalse()
    {
        // SR.JsonSchema_ExpectedMultipleOf is "The value was expected to be a multiple of" — ~43 chars.
        // Buffer fits prefix but not the appended ' '3'' (5 more chars).
        Span<byte> buffer = stackalloc byte[45];
        Assert.IsFalse(JsonSchemaEvaluation.ExpectedMultipleOfDivisor("3", buffer, out _));
    }

    [TestMethod]
    public void SchemaLocationForIndexedKeyword_BufferFitsBase_ButNotSlash_ReturnsFalse()
    {
        // "/items" → TryCopyPath strips "/" → "items" (5 bytes). Buffer of 6 fits the base
        // but not the additional "/" + digits.
        byte[] baseBytes = "/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[6];
        Assert.IsFalse(JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(baseBytes, 0, buffer, out _));
    }

    [TestMethod]
    public void TryCopyPath_EmptyInput_ReturnsTrue()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyPath(ReadOnlySpan<byte>.Empty, buffer, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyPath_HashOnly_ReturnsTrue()
    {
        byte[] path = "#"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyPath_HashSlash_ReturnsTrue()
    {
        byte[] path = "#/"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryCopyPath_HashWithContent_StripsHash()
    {
        byte[] path = "#items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.AreEqual("items", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void TryCopyPath_HashSlashWithContent_StripsBoth()
    {
        byte[] path = "#/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.AreEqual("items", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void TryCopyMessage_EmptyInput_ReturnsTrue()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.TryCopyMessage(ReadOnlySpan<byte>.Empty, buffer, out int written));
        Assert.AreEqual(0, written);
    }

    #endregion

    #region Array — SchemaLocationForItemIndex

    [TestMethod]
    [DataRow("/items", 0, "/items/0")]
    [DataRow("/items", 42, "/items/42")]
    [DataRow("/prefixItems/", 7, "/prefixItems/7")]
    public void SchemaLocationForItemIndex_Succeeds(string baseLocation, int index, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseLocation);
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.SchemaLocationForItemIndex(baseBytes, index, buffer, out int written));
        Assert.AreEqual(expected, JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [TestMethod]
    public void SchemaLocationForItemIndex_BufferTooSmall_ReturnsFalse()
    {
        byte[] baseBytes = "/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3];
        Assert.IsFalse(JsonSchemaEvaluation.SchemaLocationForItemIndex(baseBytes, 0, buffer, out _));
    }

    #endregion

    #region Array — Expected*Value message methods

    [TestMethod]
    public void ExpectedItemCountEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedItemCountEqualsValue(5, buffer, out int written));
        Assert.IsTrue(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        StringAssert.Contains(result, "5");
    }

    [TestMethod]
    public void ExpectedItemCountNotEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedItemCountNotEqualsValue(3, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedItemCountGreaterThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedItemCountGreaterThanValue(2, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedItemCountGreaterThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedItemCountGreaterThanOrEqualsValue(1, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedItemCountLessThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedItemCountLessThanValue(10, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedItemCountLessThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedItemCountLessThanOrEqualsValue(10, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedContainsCountEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedContainsCountEqualsValue(1, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedContainsCountNotEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedContainsCountNotEqualsValue(0, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedContainsCountGreaterThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedContainsCountGreaterThanValue(0, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedContainsCountGreaterThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedContainsCountGreaterThanOrEqualsValue(1, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedContainsCountLessThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedContainsCountLessThanValue(10, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedContainsCountLessThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedContainsCountLessThanOrEqualsValue(5, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    #endregion

    #region Array — Match*Count methods (fail path)

    [TestMethod]
    [DataRow(5, 3)] // actual != expected
    [DataRow(1, 0)]
    public void MatchItemCountEquals_Fails_WhenNotEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchItemCountEquals(expected, actual, "minItems"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 5)] // actual == expected
    public void MatchItemCountNotEquals_Fails_WhenEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchItemCountNotEquals(expected, actual, "minItems"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 3)] // actual <= expected
    [DataRow(5, 5)]
    public void MatchItemCountGreaterThan_Fails_WhenNotGreater(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchItemCountGreaterThan(expected, actual, "minItems"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 5)] // actual == expected: fails because not strictly less than
    [DataRow(5, 6)] // actual > expected: fails
    public void MatchItemCountLessThan_Fails_WhenNotLess(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchItemCountLessThan(expected, actual, "maxItems"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 3)] // actual != expected
    public void MatchContainsCountEquals_Fails_WhenNotEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchContainsCountEquals(expected, actual, "contains"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 5)] // actual == expected
    public void MatchContainsCountNotEquals_Fails_WhenEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchContainsCountNotEquals(expected, actual, "contains"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 5)] // actual == expected: fails because not strictly less than
    [DataRow(5, 6)] // actual > expected: fails
    public void MatchContainsCountLessThan_Fails_WhenNotLess(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchContainsCountLessThan(expected, actual, "contains"u8, ref context));
        context.Dispose();
    }

    #endregion

    #region Object — Expected*Value message methods

    [TestMethod]
    public void ExpectedPropertyCountEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyCountEqualsValue(3, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedPropertyCountNotEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyCountNotEqualsValue(0, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedPropertyCountGreaterThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanValue(1, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedPropertyCountGreaterThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanOrEqualsValue(2, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedPropertyCountLessThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyCountLessThanValue(10, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedPropertyCountLessThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyCountLessThanOrEqualsValue(10, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedMatchPatternPropertySchemaValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedMatchPatternPropertySchemaValue("^foo", buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedPropertyNameMatchesRegularExpressionValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedPropertyNameMatchesRegularExpressionValue("^bar$", buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void ExpectedMatchesDependentSchemaValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.ExpectedMatchesDependentSchemaValue("creditCard", buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void RequiredPropertyNotPresent_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.RequiredPropertyNotPresent("name"u8, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void RequiredPropertyPresent_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(JsonSchemaEvaluation.RequiredPropertyPresent("name"u8, buffer, out int written));
        Assert.IsTrue(written > 0);
    }

    #endregion

    #region Object — MatchPropertyCount methods (fail path)

    [TestMethod]
    [DataRow(3, 2)] // actual != expected
    public void MatchPropertyCountEquals_Fails_WhenNotEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchPropertyCountEquals(expected, actual, "minProperties"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(3, 3)] // actual == expected
    public void MatchPropertyCountNotEquals_Fails_WhenEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchPropertyCountNotEquals(expected, actual, "minProperties"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 3)] // actual <= expected
    [DataRow(5, 5)]
    public void MatchPropertyCountGreaterThan_Fails_WhenNotGreater(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchPropertyCountGreaterThan(expected, actual, "minProperties"u8, ref context));
        context.Dispose();
    }

    [TestMethod]
    [DataRow(5, 6)] // actual > expected
    [DataRow(5, 5)]
    public void MatchPropertyCountLessThan_Fails_WhenNotLess(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.IsFalse(JsonSchemaEvaluation.MatchPropertyCountLessThan(expected, actual, "maxProperties"u8, ref context));
        context.Dispose();
    }

    #endregion
}
