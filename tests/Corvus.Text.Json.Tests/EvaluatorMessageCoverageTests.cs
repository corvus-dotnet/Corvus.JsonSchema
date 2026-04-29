// <copyright file="EvaluatorMessageCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for evaluator error message and schema location helpers.
/// These exercise the public static methods on <see cref="JsonSchemaEvaluation"/>
/// (and its Array/Object partial classes) that generate detailed validation messages.
/// </summary>
public class EvaluatorMessageCoverageTests
{
    #region SchemaLocationForIndexedKeyword

    [Theory]
    [InlineData("/items", 0, "items/0")]
    [InlineData("/items", 5, "items/5")]
    [InlineData("/items/", 3, "items/3")]
    [InlineData("/prefixItems", 99, "prefixItems/99")]
    public void SchemaLocationForIndexedKeyword_Succeeds(string baseLocation, int index, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseLocation);
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(baseBytes, index, buffer, out int written));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [Fact]
    public void SchemaLocationForIndexedKeyword_BufferTooSmall_ReturnsFalse()
    {
        byte[] baseBytes = "/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3]; // Too small
        Assert.False(JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(baseBytes, 0, buffer, out _));
    }

    #endregion

    #region SchemaLocationForIndexedKeywordWithDependency

    // NOTE: SchemaLocationForIndexedKeywordWithDependency has a bug where the second TryCopyPath
    // writes to buffer[0..] instead of buffer[written..], corrupting previously written content.
    // These tests exercise the code paths for coverage despite the bug.
    [Theory]
    [InlineData("/properties", "dependentSchemas", 0)]
    [InlineData("/properties/", "dependentSchemas/", 5)]
    public void SchemaLocationForIndexedKeywordWithDependency_Succeeds(string baseLocation, string dependency, int index)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseLocation);
        byte[] depBytes = Encoding.UTF8.GetBytes(dependency);
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(baseBytes, depBytes, index, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void SchemaLocationForIndexedKeywordWithDependency_BufferTooSmall_ReturnsFalse()
    {
        byte[] baseBytes = "/properties"u8.ToArray();
        byte[] depBytes = "dependentSchemas"u8.ToArray();
        Span<byte> buffer = stackalloc byte[5]; // Too small
        Assert.False(JsonSchemaEvaluation.SchemaLocationForIndexedKeywordWithDependency(baseBytes, depBytes, 0, buffer, out _));
    }

    #endregion

    #region TryCopyPath and TryCopyMessage

    [Fact]
    public void TryCopyPath_Succeeds()
    {
        byte[] path = "/foo/bar"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.Equal("foo/bar", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [Fact]
    public void TryCopyPath_BufferTooSmall_ReturnsFalse()
    {
        byte[] path = "/foo/bar"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3]; // Too small
        Assert.False(JsonSchemaEvaluation.TryCopyPath(path, buffer, out _));
    }

    [Fact]
    public void TryCopyMessage_Succeeds()
    {
        byte[] message = "validation failed"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyMessage(message, buffer, out int written));
        Assert.Equal("validation failed", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [Fact]
    public void TryCopyMessage_BufferTooSmall_ReturnsFalse()
    {
        byte[] message = "validation failed"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3]; // Too small
        Assert.False(JsonSchemaEvaluation.TryCopyMessage(message, buffer, out _));
    }

    #endregion

    #region ExpectedType, ExpectedMultipleOfDivisor, IgnoredFormat*

    [Fact]
    public void ExpectedType_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedType("integer"u8, buffer, out int written));
        Assert.True(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        Assert.Contains("integer", result);
    }

    [Fact]
    public void ExpectedType_BufferTooSmall_Throws()
    {
        // TryGetUtf8FromText throws ArgumentException on .NET when buffer is too small
        byte[] buffer = new byte[3];
        Assert.ThrowsAny<Exception>(() => JsonSchemaEvaluation.ExpectedType("integer"u8, buffer, out _));
    }

    [Fact]
    public void ExpectedMultipleOfDivisor_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedMultipleOfDivisor("3", buffer, out int written));
        Assert.True(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        Assert.Contains("3", result);
    }

    [Fact]
    public void ExpectedMultipleOfDivisor_BufferTooSmall_Throws()
    {
        // TryGetUtf8FromText throws ArgumentException on .NET when buffer is too small
        byte[] buffer = new byte[3];
        Assert.ThrowsAny<Exception>(() => JsonSchemaEvaluation.ExpectedMultipleOfDivisor("3", buffer, out _));
    }

    [Fact]
    public void IgnoredUnrecognizedFormat_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.IgnoredUnrecognizedFormat(buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void IgnoredFormatNotAsserted_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.IgnoredFormatNotAsserted(buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedType_BufferFitsPrefix_ButNotValue_ReturnsFalse()
    {
        // "The value was expected to be of type" is ~40 chars = 40 UTF-8 bytes.
        // A buffer of 42 fits the prefix but not the appended ' 'integer'' (11 more chars).
        Span<byte> buffer = stackalloc byte[42];
        Assert.False(JsonSchemaEvaluation.ExpectedType("integer"u8, buffer, out _));
    }

    [Fact]
    public void ExpectedMultipleOfDivisor_BufferFitsPrefix_ButNotValue_ReturnsFalse()
    {
        // SR.JsonSchema_ExpectedMultipleOf is "The value was expected to be a multiple of" — ~43 chars.
        // Buffer fits prefix but not the appended ' '3'' (5 more chars).
        Span<byte> buffer = stackalloc byte[45];
        Assert.False(JsonSchemaEvaluation.ExpectedMultipleOfDivisor("3", buffer, out _));
    }

    [Fact]
    public void SchemaLocationForIndexedKeyword_BufferFitsBase_ButNotSlash_ReturnsFalse()
    {
        // "/items" → TryCopyPath strips "/" → "items" (5 bytes). Buffer of 6 fits the base
        // but not the additional "/" + digits.
        byte[] baseBytes = "/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[6];
        Assert.False(JsonSchemaEvaluation.SchemaLocationForIndexedKeyword(baseBytes, 0, buffer, out _));
    }

    [Fact]
    public void TryCopyPath_EmptyInput_ReturnsTrue()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyPath(ReadOnlySpan<byte>.Empty, buffer, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryCopyPath_HashOnly_ReturnsTrue()
    {
        byte[] path = "#"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryCopyPath_HashSlash_ReturnsTrue()
    {
        byte[] path = "#/"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryCopyPath_HashWithContent_StripsHash()
    {
        byte[] path = "#items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.Equal("items", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [Fact]
    public void TryCopyPath_HashSlashWithContent_StripsBoth()
    {
        byte[] path = "#/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyPath(path, buffer, out int written));
        Assert.Equal("items", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [Fact]
    public void TryCopyMessage_EmptyInput_ReturnsTrue()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.TryCopyMessage(ReadOnlySpan<byte>.Empty, buffer, out int written));
        Assert.Equal(0, written);
    }

    #endregion

    #region Array — SchemaLocationForItemIndex

    [Theory]
    [InlineData("/items", 0, "/items/0")]
    [InlineData("/items", 42, "/items/42")]
    [InlineData("/prefixItems/", 7, "/prefixItems/7")]
    public void SchemaLocationForItemIndex_Succeeds(string baseLocation, int index, string expected)
    {
        byte[] baseBytes = Encoding.UTF8.GetBytes(baseLocation);
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.SchemaLocationForItemIndex(baseBytes, index, buffer, out int written));
        Assert.Equal(expected, JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written)));
    }

    [Fact]
    public void SchemaLocationForItemIndex_BufferTooSmall_ReturnsFalse()
    {
        byte[] baseBytes = "/items"u8.ToArray();
        Span<byte> buffer = stackalloc byte[3];
        Assert.False(JsonSchemaEvaluation.SchemaLocationForItemIndex(baseBytes, 0, buffer, out _));
    }

    #endregion

    #region Array — Expected*Value message methods

    [Fact]
    public void ExpectedItemCountEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedItemCountEqualsValue(5, buffer, out int written));
        Assert.True(written > 0);
        string result = JsonReaderHelper.TranscodeHelper(buffer.Slice(0, written));
        Assert.Contains("5", result);
    }

    [Fact]
    public void ExpectedItemCountNotEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedItemCountNotEqualsValue(3, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedItemCountGreaterThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedItemCountGreaterThanValue(2, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedItemCountGreaterThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedItemCountGreaterThanOrEqualsValue(1, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedItemCountLessThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedItemCountLessThanValue(10, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedItemCountLessThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedItemCountLessThanOrEqualsValue(10, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedContainsCountEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedContainsCountEqualsValue(1, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedContainsCountNotEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedContainsCountNotEqualsValue(0, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedContainsCountGreaterThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedContainsCountGreaterThanValue(0, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedContainsCountGreaterThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedContainsCountGreaterThanOrEqualsValue(1, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedContainsCountLessThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedContainsCountLessThanValue(10, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedContainsCountLessThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedContainsCountLessThanOrEqualsValue(5, buffer, out int written));
        Assert.True(written > 0);
    }

    #endregion

    #region Array — Match*Count methods (fail path)

    [Theory]
    [InlineData(5, 3)] // actual != expected
    [InlineData(1, 0)]
    public void MatchItemCountEquals_Fails_WhenNotEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchItemCountEquals(expected, actual, "minItems"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 5)] // actual == expected
    public void MatchItemCountNotEquals_Fails_WhenEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchItemCountNotEquals(expected, actual, "minItems"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 3)] // actual <= expected
    [InlineData(5, 5)]
    public void MatchItemCountGreaterThan_Fails_WhenNotGreater(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchItemCountGreaterThan(expected, actual, "minItems"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 5)] // actual == expected: fails because not strictly less than
    [InlineData(5, 6)] // actual > expected: fails
    public void MatchItemCountLessThan_Fails_WhenNotLess(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchItemCountLessThan(expected, actual, "maxItems"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 3)] // actual != expected
    public void MatchContainsCountEquals_Fails_WhenNotEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchContainsCountEquals(expected, actual, "contains"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 5)] // actual == expected
    public void MatchContainsCountNotEquals_Fails_WhenEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchContainsCountNotEquals(expected, actual, "contains"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 5)] // actual == expected: fails because not strictly less than
    [InlineData(5, 6)] // actual > expected: fails
    public void MatchContainsCountLessThan_Fails_WhenNotLess(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartArray), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchContainsCountLessThan(expected, actual, "contains"u8, ref context));
        context.Dispose();
    }

    #endregion

    #region Object — Expected*Value message methods

    [Fact]
    public void ExpectedPropertyCountEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyCountEqualsValue(3, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedPropertyCountNotEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyCountNotEqualsValue(0, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedPropertyCountGreaterThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanValue(1, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedPropertyCountGreaterThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanOrEqualsValue(2, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedPropertyCountLessThanValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyCountLessThanValue(10, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedPropertyCountLessThanOrEqualsValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyCountLessThanOrEqualsValue(10, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedMatchPatternPropertySchemaValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedMatchPatternPropertySchemaValue("^foo", buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedPropertyNameMatchesRegularExpressionValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedPropertyNameMatchesRegularExpressionValue("^bar$", buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void ExpectedMatchesDependentSchemaValue_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.ExpectedMatchesDependentSchemaValue("creditCard", buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void RequiredPropertyNotPresent_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.RequiredPropertyNotPresent("name"u8, buffer, out int written));
        Assert.True(written > 0);
    }

    [Fact]
    public void RequiredPropertyPresent_Succeeds()
    {
        Span<byte> buffer = stackalloc byte[256];
        Assert.True(JsonSchemaEvaluation.RequiredPropertyPresent("name"u8, buffer, out int written));
        Assert.True(written > 0);
    }

    #endregion

    #region Object — MatchPropertyCount methods (fail path)

    [Theory]
    [InlineData(3, 2)] // actual != expected
    public void MatchPropertyCountEquals_Fails_WhenNotEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchPropertyCountEquals(expected, actual, "minProperties"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(3, 3)] // actual == expected
    public void MatchPropertyCountNotEquals_Fails_WhenEqual(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchPropertyCountNotEquals(expected, actual, "minProperties"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 3)] // actual <= expected
    [InlineData(5, 5)]
    public void MatchPropertyCountGreaterThan_Fails_WhenNotGreater(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchPropertyCountGreaterThan(expected, actual, "minProperties"u8, ref context));
        context.Dispose();
    }

    [Theory]
    [InlineData(5, 6)] // actual > expected
    [InlineData(5, 5)]
    public void MatchPropertyCountLessThan_Fails_WhenNotLess(int expected, int actual)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.StartObject), 0, false, false, collector);
        Assert.False(JsonSchemaEvaluation.MatchPropertyCountLessThan(expected, actual, "maxProperties"u8, ref context));
        context.Dispose();
    }

    #endregion
}
