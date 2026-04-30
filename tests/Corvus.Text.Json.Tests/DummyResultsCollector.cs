// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Xunit;

namespace Corvus.Text.Json.Tests;

internal class DummyResultsCollector : IJsonSchemaResultsCollector
{
    private int _childContextCount = 0;
    private int _schemaLocationCount = 0;

    public void AssertState()
    {
        Assert.Equal(1, _childContextCount);
        Assert.Equal(0, _schemaLocationCount);
    }

    public int BeginChildContext(int parentSequenceNumber, JsonSchemaPathProvider reducedEvaluationPath = null, JsonSchemaPathProvider schemaEvaluationPath = null, JsonSchemaPathProvider documentEvaluationPath = null) => _childContextCount++;

    public int BeginChildContext(int parentSequenceNumber, ReadOnlySpan<byte> propertyName, JsonSchemaPathProvider reducedEvaluationPath = null, JsonSchemaPathProvider? schemaEvaluationPath = null) => _childContextCount++;

    public int BeginChildContext<TProviderContext>(int parentSequenceNumber, TProviderContext providerContext, JsonSchemaPathProvider<TProviderContext> reducedEvaluationPath, JsonSchemaPathProvider<TProviderContext> schemaEvaluationPath, JsonSchemaPathProvider<TProviderContext> documentEvaluationPath) => _childContextCount++;
    public int BeginChildContext(int parentSequenceNumber, int itemIndex, JsonSchemaPathProvider reducedEvaluationPath = null, JsonSchemaPathProvider schemaEvaluationPath = null) => _childContextCount++;
    public int BeginChildContextUnescaped(int parentSequenceNumber, ReadOnlySpan<byte> unescapedPropertyName, JsonSchemaPathProvider reducedEvaluationPath = null, JsonSchemaPathProvider? schemaEvaluationPath = null) => _childContextCount++;

    public void CommitChildContext(int sequenceNumber, bool parentIsMatch, bool childIsMatch, JsonSchemaMessageProvider messageProvider) => _childContextCount--;

    public void CommitChildContext<TProviderContext>(int sequenceNumber, bool parentIsMatch, bool childIsMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider) => _childContextCount--;

    public void Dispose()
    { }

    public void EvaluatedBooleanSchema(bool isMatch, JsonSchemaMessageProvider messageProvider)
    { }

    public void EvaluatedBooleanSchema<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider)
    { }

    public void EvaluatedKeyword(bool isMatch, JsonSchemaMessageProvider messageProvider, ReadOnlySpan<byte> encodedKeyword)
    { }

    public void EvaluatedKeyword<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider, ReadOnlySpan<byte> encodedKeyword)
    { }

    public void EvaluatedKeywordForProperty(bool isMatch, JsonSchemaMessageProvider messageProvider, ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> encodedKeyword)
    { }

    public void EvaluatedKeywordForProperty<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider, ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> encodedKeyword)
    { }

    public void EvaluatedKeywordPath(bool isMatch, JsonSchemaMessageProvider messageProvider, JsonSchemaPathProvider encodedKeywordPath)
    { }

    public void EvaluatedKeywordPath<TProviderContext>(bool isMatch, TProviderContext? providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider, JsonSchemaPathProvider<TProviderContext> encodedKeywordPath)
    { }

    public void IgnoredKeyword(JsonSchemaMessageProvider messageProvider, ReadOnlySpan<byte> encodedKeyword)
    { }

    public void IgnoredKeyword<TProviderContext>(TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider, ReadOnlySpan<byte> encodedKeyword)
    { }

    public void PopChildContext(int sequenceNumber) => _childContextCount--;
}