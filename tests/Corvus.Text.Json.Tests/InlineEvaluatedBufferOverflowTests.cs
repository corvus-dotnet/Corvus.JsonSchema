// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Regression tests for the inline evaluated-bits buffer overflow. The inline buffer (8 ints = 256
/// bits) flagged inline-vs-rented storage using bit 7 of its last int — which is the bit for property
/// index 231. Validating an object with 232..255 evaluation-tracked properties marked index 231,
/// corrupting the flag so the buffer accessor mis-routed to the rented path and threw
/// <see cref="System.ArgumentOutOfRangeException"/>. The flag now uses the top (255th) bit, which is
/// never a valid inline data index.
/// </summary>
[TestClass]
public class InlineEvaluatedBufferOverflowTests
{
    private static string ObjectWithStringProperties(int count)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("\"k").Append(i).Append("\":\"v\"");
        }

        return sb.Append('}').ToString();
    }

    // The crash window was [232, 255]; counts <= 231 (inline, no collision) and >= 256 (rented buffer)
    // were always fine. Sweep across and past the boundary.
    [TestMethod]
    [DataRow(0)]
    [DataRow(200)]
    [DataRow(231)]
    [DataRow(232)]
    [DataRow(240)]
    [DataRow(254)]
    [DataRow(255)]
    [DataRow(256)]
    [DataRow(300)]
    [DataRow(1000)]
    public void ObjectWithManyTrackedProperties_ValidatesWithoutFaulting(int propertyCount)
    {
        InlineEvaluatedBuffer instance =
            InlineEvaluatedBuffer.ParseValue(ObjectWithStringProperties(propertyCount));

        // Every property is a string matched by additionalProperties, so the instance is valid; the
        // point of the regression test is that validation must not throw for counts in the
        // inline-flag-collision window.
        Assert.IsTrue(instance.EvaluateSchema());
    }
}
