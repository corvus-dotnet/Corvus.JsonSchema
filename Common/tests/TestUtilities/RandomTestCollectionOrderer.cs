// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;



namespace TestUtilities;

public class RandomTestCollectionOrderer : ITestCollectionOrderer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public RandomTestCollectionOrderer(IMessageSink diagnosticMessageSink)
    {
        diagnosticMessageSink.OnMessage(new DiagnosticMessage(
                                                $"Using random seed for collections: {RandomTestCaseOrderer.LazySeed.Value}"));
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections)
        => RandomTestCaseOrderer.TryRandomize(testCollections.ToList(), _diagnosticMessageSink, out List<ITestCollection>? randomizedTests)
                    ? randomizedTests
                    : testCollections;
}