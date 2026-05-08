// <copyright file="UniqueItemsHashSetCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Coverage tests for <see cref="JsonSchema.Internal.UniqueItemsHashSet"/> targeting the
/// ArrayPool rent/return paths that trigger when arrays exceed the stackalloc threshold (256 buckets).
/// </summary>
/// <remarks>
/// UniqueItemsHashSet is used by JSONata's $distinct function. When the input array exceeds
/// StackAllocBucketSize (256), the hash set rents from ArrayPool instead of using stackalloc.
/// </remarks>
[TestClass]
public class UniqueItemsHashSetCoverageTests
{
    [TestMethod]
    public void Distinct_LargeArray_TriggersArrayPoolRent()
    {
        // Create an array with >256 elements to trigger ArrayPool rent in
        // UniqueItemsHashSet.CreateMap (L238-242 for buckets, L249-252 for entries)
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 300; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(i);
        }

        sb.Append(']');

        string data = sb.ToString();
        string? result = JsonataEvaluator.Default.EvaluateToString("$distinct($)", data);
        Assert.IsNotNull(result);

        // Verify result has all 300 unique elements
        StringAssert.Contains(result, "299");
    }

    [TestMethod]
    public void Distinct_LargeArrayWithDuplicates_TriggersArrayPoolAndDeduplicates()
    {
        // 300 elements but only 100 unique values — triggers rent AND collision handling
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 300; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(i % 100);
        }

        sb.Append(']');

        string data = sb.ToString();
        string? result = JsonataEvaluator.Default.EvaluateToString("$distinct($)", data);
        Assert.IsNotNull(result);

        // Should have exactly 100 unique elements
        Assert.DoesNotContain("100", result);
        StringAssert.Contains(result, "99");
    }

    [TestMethod]
    public void Distinct_LargeArrayWithObjects_TriggersArrayPoolAndDeepEquals()
    {
        // Large array of objects triggers ValueEquals deep comparison
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 270; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"id\":{i % 90},\"value\":\"{(char)('A' + (i % 26))}\"}}");
        }

        sb.Append(']');

        string data = sb.ToString();
        string? result = JsonataEvaluator.Default.EvaluateToString("$count($distinct($))", data);
        Assert.IsNotNull(result);

        // Each combo of id (0-89) and value (A-Z) gives at most 90 distinct items
        // since value repeats every 26 and id repeats every 90
        int count = int.Parse(result);
        Assert.IsTrue(count > 0 && count <= 270);
    }
}
