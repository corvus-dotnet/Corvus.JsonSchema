// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class JsonSchemaResultsCollectorTests
{
    private static readonly ExpectedResult[] s_expectedForPushAndPopNestedSchemaLocation_WithResults =
        [
        new ("", "$defs/Name", "/someProperty/nextProperty", "Committed the third and fourth properties.", true),
        new ("", "$defs/Name", "/someProperty/nextProperty", "Evaluated a fourth property.", false),
        new ("", "$defs/Name", "/someProperty/nextProperty", "Evaluated a third property.", true),
        new ("", "$defs/Name", "/someProperty", "Committed the first and second properties.", true),
        new ("", "$defs/Name", "/someProperty", "Evaluated a second property.", false),
        new ("", "$defs/Name", "/someProperty", "Evaluated a first property.", true),
        ];

    private static readonly ExpectedResult[] s_expectedForPushAndPopNestedSchemaLocationReverseCommit_WithResults =
        [
        new ("", "$defs/Name", "/someProperty/anotherProperty", "Committed the fifth and sixth properties.", true),
        new ("", "$defs/Name", "/someProperty/anotherProperty", "Evaluated a sixth property.", false),
        new ("", "$defs/Name", "/someProperty/anotherProperty", "Evaluated a fifth property.", true),
        new ("", "$defs/Name", "/someProperty", "Committed the first and second properties.", true),
        new ("", "$defs/Name", "/someProperty", "Evaluated a second property.", false),
        new ("", "$defs/Name", "/someProperty", "Evaluated a first property.", true),
        ];

    [Fact]
    public void All_Providers_Throw_If_Value_Too_Large()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Create a large (>8k) UTF-8 buffer
        byte[] largeBuffer = new byte[9000];
        for (int i = 0; i < largeBuffer.Length; ++i) largeBuffer[i] = (byte)('A' + (i % 26));

        // PathProvider that always tries to write the large buffer
        bool largePathProvider(Span<byte> buffer, out int written)
        {
            written = largeBuffer.Length;
            largeBuffer.CopyTo(buffer);
            return true;
        }

        // MessageProvider that always tries to write the large buffer
        bool largeMessageProvider(Span<byte> buffer, out int written)
        {
            written = largeBuffer.Length;
            largeBuffer.CopyTo(buffer);
            return true;
        }

        // 1. BeginChildContext with large path provider
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, largePathProvider, null));
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, reducedEvaluationPath: null, documentEvaluationPath: largePathProvider));
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, largePathProvider, largePathProvider));

        // 2. BeginChildContext (escaped property) with large path provider
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, "prop"u8, largePathProvider));

        // 3. BeginChildContextUnescaped with large path provider
        Assert.Throws<ArgumentException>(() => c.BeginChildContextUnescaped(0, "prop"u8, largePathProvider));

        // 4. BeginChildContext<T> with large path provider
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, 42, (ctx, buffer, out written) => largePathProvider(buffer, out written), null, null));
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, 42, null, (ctx, buffer, out written) => largePathProvider(buffer, out written), null));
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, 42, null, null, (ctx, buffer, out written) => largePathProvider(buffer, out written)));
        Assert.Throws<ArgumentException>(() => c.BeginChildContext(0, 42, (ctx, buffer, out written) => largePathProvider(buffer, out written), (ctx, buffer, out written) => largePathProvider(buffer, out written), (ctx, buffer, out written) => largePathProvider(buffer, out written)));

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        // 8. EvaluatedKeywordPath with large path provider
        Assert.Throws<ArgumentException>(() => c.EvaluatedKeywordPath(true, largeMessageProvider, largePathProvider));
        Assert.Throws<ArgumentException>(() => c.EvaluatedKeywordPath(false, 42, (ctx, buffer, out written) => largeMessageProvider(buffer, out written), (ctx, buffer, out written) => largePathProvider(buffer, out written)));

        // 10. EvaluatedBooleanSchema with large message provider
        Assert.Throws<ArgumentException>(() => c.EvaluatedBooleanSchema(true, largeMessageProvider));
        Assert.Throws<ArgumentException>(() => c.EvaluatedBooleanSchema(false, 42, (ctx, buffer, out written) => largeMessageProvider(buffer, out written)));

        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void BeginChildContext_Generic_WithNonNullPathProviders_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        // Non-null reducedEvaluationPath and documentEvaluationPath for generic overload

        int seq = c.BeginChildContext(
            0,
            123,
            static (ctx, buffer, out written) =>
            {
                ReadOnlySpan<byte> prefix = "reduced/"u8;
                prefix.CopyTo(buffer);
                if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx, buffer.Slice(prefix.Length), out int intWritten))
                {
                    written = 0;
                    return false;
                }
                written = prefix.Length + intWritten;
                return true;
            },
            static (ctx, buffer, out written) =>
            {
                ReadOnlySpan<byte> prefix = "#/$defs/someSchema/"u8;
                prefix.CopyTo(buffer);
                if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx, buffer.Slice(prefix.Length), out int intWritten))
                {
                    written = 0;
                    return false;
                }
                written = prefix.Length + intWritten;
                return true;
            },
            static (ctx, buffer, out written) =>
            {
                ReadOnlySpan<byte> prefix = "doc/"u8;
                prefix.CopyTo(buffer);
                if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx, buffer.Slice(prefix.Length), out int intWritten))
                {
                    written = 0;
                    return false;
                }
                written = prefix.Length + intWritten;
                return true;
            });
        Assert.True(seq > 0);
        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void BeginChildContext_Overloads_Work(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        // Using BeginChildContext with reducedEvaluationPath and documentEvaluationPath as null
        int seq1 = c.BeginChildContext(0);
        Assert.True(seq1 >= 0);

        // Using BeginChildContext with property name (escaped)
        int seq2 = c.BeginChildContext(0, "propertyName"u8);
        Assert.True(seq2 >= 0);

        // Using BeginChildContextUnescaped
        int seq3 = c.BeginChildContextUnescaped(0, "unescapedName"u8);
        Assert.True(seq3 >= 0);

        // Using generic BeginChildContext
        int seq4 = c.BeginChildContext(0, 42, null, null, null);
        Assert.True(seq4 >= 0);

        // Pop all contexts
        c.PopChildContext(seq4);
        c.PopChildContext(seq3);
        c.PopChildContext(seq2);
        c.PopChildContext(seq1);

        collector.Dispose();
    }

    [Fact]
    public void BeginChildContext_WithEscapedPropertyName_UnescapesForDocumentLocation()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // "hell\u006F" is "hello" in UTF-8
        ReadOnlySpan<byte> escapedProperty = "hell\\u006F"u8;

        c.BeginChildContext(0, escapedProperty);

        // SchemaLocation and EvaluationLocation should be empty for property context
        Assert.Equal("", collector.SchemaLocation);
        Assert.Equal("", collector.EvaluationLocation);

        // DocumentLocation should be "/hello"
        Assert.Equal("/hello", collector.DocumentLocation);

        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void BeginChildContext_WithNonNullPathProviders_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        // Non-null reducedEvaluationPath and documentEvaluationPath
        int seq = c.BeginChildContext(
            0,
            (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("reduced/path"u8, buffer, out written),
            (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("doc/path"u8, buffer, out written));
        Assert.True(seq > 0);
        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void BeginChildContext_WithNonNullReducedEvaluationPath_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        // Non-null reducedEvaluationPath for escaped property
        int seq = c.BeginChildContext(
            0,
            "property"u8,
            (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("reduced/escaped"u8, buffer, out written));
        Assert.True(seq > 0);
        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void BeginChildContextUnescaped_WithNonNullReducedEvaluationPath_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        // Non-null reducedEvaluationPath for unescaped property
        int seq = c.BeginChildContextUnescaped(
            0,
            "property"u8,
            (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("reduced/unescaped"u8, buffer, out written));
        Assert.True(seq > 0);
        collector.Dispose();
    }

    [Fact]
    public void BeginChildContextUnescaped_WithUnescapedPropertyName_EncodesForDocumentLocation()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // The property name contains both '/' and '~'
        ReadOnlySpan<byte> unescapedProperty = "he/l~o"u8;

        c.BeginChildContextUnescaped(0, unescapedProperty);

        // SchemaLocation and EvaluationLocation should be empty for property context
        Assert.Equal("", collector.SchemaLocation);
        Assert.Equal("", collector.EvaluationLocation);

        // DocumentLocation should be "/he~0l~1o"
        Assert.Equal("/he~1l~0o", collector.DocumentLocation);

        collector.Dispose();
    }

    [Fact]
    public void Collector_Can_Be_Created_Disposed_And_Recreated_With_Larger_Capacity()
    {
        // Create and dispose a collector with default capacity
        var collector1 = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.NotNull(collector1);
        collector1.Dispose();

        // Create a new collector with a larger initial capacity
        int largeCapacity = 500;
        var collector2 = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose, largeCapacity);
        Assert.NotNull(collector2);

        // Verify that the collector works as expected
        IJsonSchemaResultsCollector c2 = collector2;
        int seq = c2.BeginChildContext(0);
        c2.CommitChildContext(seq, true, true, null);

        // Should be able to enumerate results
        int resultCount = collector2.GetResultCount();
        Assert.Equal(1, resultCount);

        collector2.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void CommitChildContext_Generic_Overload_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, true, false, 123, (ctx, buffer, out written) =>
        {
            Assert.Equal(123, ctx);
            return JsonSchemaEvaluation.TryCopyMessage("Generic commit"u8, buffer, out written);
        });

        collector.Dispose();
    }

    [Fact]
    public void Construct_JsonSchemaResultsCollector()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.NotNull(collector);
        collector.Dispose();
    }

    private record ExpectedResult(string evaluationLocation, string schemaLocation, string documentLocation, string message, bool isMatch)
    {
        internal void AssertEqual(JsonSchemaResultsCollector.Result result)
        {
            Assert.Equal(evaluationLocation, result.GetEvaluationLocationText());
            Assert.Equal(schemaLocation, result.GetSchemaEvaluationLocationText());
            Assert.Equal(documentLocation, result.GetDocumentEvaluationLocationText());
            Assert.Equal(message, result.GetMessageText());
            Assert.Equal(isMatch, result.IsMatch);
        }
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void CreateUnrented_And_Dispose_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.CreateUnrented(level);
        Assert.NotNull(collector);
        collector.Dispose();
        // Double dispose should not throw
        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void DoubleDispose_DoesNotThrow(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        collector.Dispose();
        collector.Dispose(); // Should not throw
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void EvaluatedBooleanSchema_Generic_Overload_Works(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        c.EvaluatedBooleanSchema(true, 5, (ctx, buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Generic boolean"u8, buffer, out written));

        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed boolean"u8, buffer, out written));

        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void EvaluatedKeyword_Overloads_Work(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        // Non-generic
        c.EvaluatedKeyword(true, (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Keyword evaluated"u8, buffer, out written), "keyword"u8);

        // Generic
        c.EvaluatedKeyword(false, 99, (ctx, buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Generic keyword"u8, buffer, out written), "keyword"u8);

        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed boolean"u8, buffer, out written));

        collector.Dispose();
    }

    [Fact]
    public void EvaluatedKeyword_Throws_If_Keyword_Exceeds_Maximum_Length()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Allocate a keyword buffer just over 0x7000_0000 bytes
        long tooLargeLength = 0x7000_0000L + 1;

        // We'll simulate the provider reporting a too-large written value
        bool TooLargeKeywordProvider(Span<byte> buffer, out int written)
        {
            written = unchecked((int)tooLargeLength);
            // We don't actually write data, just simulate the length
            return true;
        }

        // The collector will call the provider and see the too-large written value
        Assert.Throws<ArgumentException>(() =>
        {
            int seq = c.BeginChildContext(0);
            c.CommitChildContext(seq, true, false, TooLargeKeywordProvider);
        });

        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void EvaluatedKeywordForProperty_Overloads_Work(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: (b, out w) => JsonSchemaEvaluation.TryCopyPath("#/$defs/schema"u8, b, out w));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        // Non-generic
        c.EvaluatedKeywordForProperty(true, (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Property keyword"u8, buffer, out written), "property"u8, "keyword"u8);

        // Generic
        c.EvaluatedKeywordForProperty(false, 77, (ctx, buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Generic property keyword"u8, buffer, out written), "property"u8, "keyword"u8);

        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed boolean"u8, buffer, out written));
        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void EvaluatedKeywordPath_Overloads_Work(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        // Non-generic
        c.EvaluatedKeywordPath(true, (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Keyword path"u8, buffer, out written), (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyPath("keyword/path"u8, buffer, out written));

        // Generic
        c.EvaluatedKeywordPath(false, 55, (ctx, buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Generic keyword path"u8, buffer, out written),
            (ctx, buffer, out written) =>
            JsonSchemaEvaluation.TryCopyPath("keyword/path"u8, buffer, out written));

        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed boolean"u8, buffer, out written));

        collector.Dispose();
    }

    [Theory]
    [InlineData(JsonSchemaResultsLevel.Basic)]
    [InlineData(JsonSchemaResultsLevel.Detailed)]
    [InlineData(JsonSchemaResultsLevel.Verbose)]
    public void IgnoredKeyword_Overloads_Work(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        // Non-generic
        c.IgnoredKeyword((buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Ignored keyword"u8, buffer, out written), "keyword"u8);

        // Generic
        c.IgnoredKeyword(11, (ctx, buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage("Generic ignored"u8, buffer, out written), "keyword"u8);

        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed boolean"u8, buffer, out written));

        collector.Dispose();
    }

    [Fact]
    public void Keywords_Do_Not_Throw_If_Value_Too_Large()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Create a large (>8k) UTF-8 buffer
        byte[] largeBuffer = new byte[9000];
        for (int i = 0; i < largeBuffer.Length; ++i) largeBuffer[i] = (byte)('A' + (i % 26));

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);

        // 1. EvaluatedKeyword with large keyword
        c.EvaluatedKeyword(true, null, largeBuffer.AsSpan());
        c.EvaluatedKeyword(false, 42, null, largeBuffer.AsSpan());

        // 2. EvaluatedKeywordForProperty with large keyword
        c.EvaluatedKeywordForProperty(true, null, "prop"u8, largeBuffer.AsSpan());
        c.EvaluatedKeywordForProperty(false, 42, null, "prop"u8, largeBuffer.AsSpan());

        // 3. IgnoredKeyword with large keyword
        c.IgnoredKeyword(null, largeBuffer.AsSpan());
        c.IgnoredKeyword(42, null, largeBuffer.AsSpan());

        collector.Dispose();
    }

    [Fact]
    public void Locations_Are_Concatenated_On_Multiple_BeginChildContext_GenericProvider_Overloads()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // First generic path providers
        static bool evalPath1(int ctx, Span<byte> buffer, out int written)
        {
            // Write "eval" + ctx as UTF-8
            ReadOnlySpan<byte> prefix = "eval"u8;
            prefix.CopyTo(buffer);
            if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx, buffer.Slice(prefix.Length), out int intWritten))
            {
                written = 0;
                return false;
            }
            written = prefix.Length + intWritten;
            return true;
        }
        static bool schemaPath1(int ctx, Span<byte> buffer, out int written)
        {
            // Write "eval" + ctx as UTF-8
            ReadOnlySpan<byte> prefix = "schema"u8;
            prefix.CopyTo(buffer);
            if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx, buffer.Slice(prefix.Length), out int intWritten))
            {
                written = 0;
                return false;
            }
            written = prefix.Length + intWritten;
            return true;
        }
        static bool docPath1(int ctx, Span<byte> buffer, out int written)
        {
            ReadOnlySpan<byte> prefix = "doc"u8;
            prefix.CopyTo(buffer);
            if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx, buffer.Slice(prefix.Length), out int intWritten))
            {
                written = 0;
                return false;
            }
            written = prefix.Length + intWritten;
            return true;
        }

        // Second generic path providers
        static bool evalPath2(int ctx, Span<byte> buffer, out int written)
        {
            ReadOnlySpan<byte> prefix = "eval"u8;
            prefix.CopyTo(buffer);
            if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx + 1, buffer.Slice(prefix.Length), out int intWritten))
            {
                written = 0;
                return false;
            }
            written = prefix.Length + intWritten;
            return true;
        }
        static bool schemaPath2(int ctx, Span<byte> buffer, out int written)
        {
            ReadOnlySpan<byte> prefix = "schema"u8;
            prefix.CopyTo(buffer);
            if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx + 1, buffer.Slice(prefix.Length), out int intWritten))
            {
                written = 0;
                return false;
            }
            written = prefix.Length + intWritten;
            return true;
        }
        static bool docPath2(int ctx, Span<byte> buffer, out int written)
        {
            ReadOnlySpan<byte> prefix = "doc"u8;
            prefix.CopyTo(buffer);
            if (!System.Buffers.Text.Utf8Formatter.TryFormat(ctx + 1, buffer.Slice(prefix.Length), out int intWritten))
            {
                written = 0;
                return false;
            }
            written = prefix.Length + intWritten;
            return true;
        }

        // First context
        int parentSequenceNumber = c.BeginChildContext(0, 1, evalPath1, schemaPath1, docPath1);
        Assert.Equal("schema1", collector.SchemaLocation);
        Assert.Equal("/doc1", collector.DocumentLocation);
        Assert.Equal("/eval1", collector.EvaluationLocation);

        // Second context (should concatenate)
        c.BeginChildContext(parentSequenceNumber, 2, evalPath2, schemaPath2, docPath2);
        Assert.Equal("schema3", collector.SchemaLocation);
        Assert.Equal("/doc1/doc3", collector.DocumentLocation);
        Assert.Equal("/eval1/eval3", collector.EvaluationLocation);

        collector.Dispose();
    }

    [Fact]
    public void Locations_Are_Concatenated_On_Multiple_BeginChildContext_PropertyName_Overloads()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Use property names
        ReadOnlySpan<byte> property1 = "property1"u8;
        ReadOnlySpan<byte> property2 = "property2"u8;

        // Begin with first property
        int parentSequenceNumber = c.BeginChildContext(0, property1);
        Assert.Equal("", collector.SchemaLocation);
        Assert.Equal("", collector.EvaluationLocation);
        // DocumentLocation is encoded/escaped, but for ASCII should match
        Assert.Equal("/property1", collector.DocumentLocation);

        // Second keyword
        c.BeginChildContext(parentSequenceNumber, property2);
        Assert.Equal("", collector.SchemaLocation);
        Assert.Equal("", collector.EvaluationLocation);
        Assert.Equal("/property1/property2", collector.DocumentLocation);

        collector.Dispose();
    }

    [Fact]
    public void Locations_Are_Concatenated_On_Multiple_BeginChildContext_Provider_Overloads()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // First path providers
        JsonSchemaPathProvider evalPath1 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "eval1"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };
        JsonSchemaPathProvider schemaPath1 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "schema1"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };

        JsonSchemaPathProvider docPath1 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "doc1"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };

        // Second path providers
        JsonSchemaPathProvider evalPath2 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "eval2"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };
        JsonSchemaPathProvider schemaPath2 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "schema2"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };

        JsonSchemaPathProvider docPath2 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "doc2"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };

        // Third path providers
        JsonSchemaPathProvider evalPath3 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "eval3"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };
        JsonSchemaPathProvider schemaPath3 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "schema3"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };

        JsonSchemaPathProvider docPath3 = (buffer, out written) =>
        {
            ReadOnlySpan<byte> value = "doc3"u8;
            value.CopyTo(buffer);
            written = value.Length;
            return true;
        };

        // First context
        int parentSequenceNumber = c.BeginChildContext(0, evalPath1, schemaPath1, docPath1);
        Assert.Equal("schema1", collector.SchemaLocation); // SchemaLocation uses evaluationPath provider
        Assert.Equal("/doc1", collector.DocumentLocation);
        Assert.Equal("/eval1", collector.EvaluationLocation);

        // Second context (should concatenate)
        c.BeginChildContext(parentSequenceNumber, evalPath2, schemaPath2, docPath2);
        Assert.Equal("schema2", collector.SchemaLocation);
        Assert.Equal("/doc1/doc2", collector.DocumentLocation);
        Assert.Equal("/eval1/eval2", collector.EvaluationLocation);

        // Parallel context (should concatenate with parent)
        c.BeginChildContext(parentSequenceNumber, evalPath3, schemaPath3, docPath3);
        Assert.Equal("schema3", collector.SchemaLocation);
        Assert.Equal("/doc1/doc3", collector.DocumentLocation);
        Assert.Equal("/eval1/eval3", collector.EvaluationLocation);

        collector.Dispose();
    }

    [Fact]
    public void Locations_Are_Concatenated_On_Multiple_BeginChildContextUnescaped_PropertyName_Overloads()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Use property names as keywords
        ReadOnlySpan<byte> property1 = "property1"u8;
        ReadOnlySpan<byte> property2 = "property2"u8;

        // Begin with first keyword (unescaped)
        int parentSequenceNumber = c.BeginChildContextUnescaped(0, property1);
        Assert.Equal("", collector.SchemaLocation);
        Assert.Equal("", collector.EvaluationLocation);
        Assert.Equal("/property1", collector.DocumentLocation);

        // Second keyword (unescaped)
        c.BeginChildContextUnescaped(parentSequenceNumber, property2);
        Assert.Equal("", collector.SchemaLocation);
        Assert.Equal("", collector.EvaluationLocation);
        Assert.Equal("/property1/property2", collector.DocumentLocation);

        collector.Dispose();
    }

    [Fact]
    public void PushAndPopNestedSchemaLocation_WithResults()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a first property."u8, buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a second property."u8, buffer, out written));
        int sequenceNumber2 = c.BeginChildContext(sequenceNumber, "nextProperty"u8);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a third property."u8, buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a fourth property."u8, buffer, out written));
        c.CommitChildContext(sequenceNumber2, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed the third and fourth properties."u8, buffer, out written));
        int sequenceNumber3 = c.BeginChildContext(sequenceNumber, "anotherProperty"u8);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a fifth property."u8, buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a sixth property."u8, buffer, out written));
        c.PopChildContext(sequenceNumber3);
        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed the first and second properties."u8, buffer, out written));

        int expectedResultIndex = 0;

        JsonSchemaResultsCollector.ResultsEnumerator enumerator = collector.EnumerateResults();

        while (enumerator.MoveNext())
        {
            JsonSchemaResultsCollector.Result result = enumerator.Current;
            ExpectedResult expected = s_expectedForPushAndPopNestedSchemaLocation_WithResults[expectedResultIndex++];
            expected.AssertEqual(result);
        }

        collector.Dispose();
    }

    [Fact]
    public void PushAndPopNestedSchemaLocationReverseCommit_WithResults()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath("#/$defs/Name"u8, buffer, out written));
        int sequenceNumber = c.BeginChildContext(parent, "someProperty"u8);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a first property."u8, buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a second property."u8, buffer, out written));
        int sequenceNumber2 = c.BeginChildContext(sequenceNumber, "nextProperty"u8);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a third property."u8, buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a fourth property."u8, buffer, out written));
        c.PopChildContext(sequenceNumber2);
        int sequenceNumber3 = c.BeginChildContext(sequenceNumber, "anotherProperty"u8);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a fifth property."u8, buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Evaluated a sixth property."u8, buffer, out written));
        c.CommitChildContext(sequenceNumber3, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed the fifth and sixth properties."u8, buffer, out written));
        c.CommitChildContext(sequenceNumber, true, true, static (buffer, out written) => JsonSchemaEvaluation.TryCopyMessage("Committed the first and second properties."u8, buffer, out written));

        int expectedResultIndex = 0;

        JsonSchemaResultsCollector.ResultsEnumerator enumerator = collector.EnumerateResults();

        while (enumerator.MoveNext())
        {
            JsonSchemaResultsCollector.Result result = enumerator.Current;
            ExpectedResult expected = s_expectedForPushAndPopNestedSchemaLocationReverseCommit_WithResults[expectedResultIndex++];
            expected.AssertEqual(result);
        }

        collector.Dispose();
    }

    /****/
}