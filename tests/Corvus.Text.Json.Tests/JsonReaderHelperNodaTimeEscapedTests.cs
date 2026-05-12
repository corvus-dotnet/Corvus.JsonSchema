// <copyright file="JsonReaderHelperNodaTimeEscapedTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that NodaTime TryGetValue methods correctly parse escaped JSON string values.
/// Exercises the hasComplexChildren=true paths.
/// </summary>
[TestClass]
public class JsonReaderHelperNodaTimeEscapedTests
{
    [TestMethod]
    public void TryGetValue_OffsetDateTime_WithEscapedPlus()
    {
        // "2024-01-15T10:30:00+05:00" with the '+' encoded as \u002B
        // The raw segment bytes represent the string content with the escape sequence
        byte[] segment = Encoding.UTF8.GetBytes("2024-01-15T10:30:00\\u002B05:00");
        // Replace the literal backslash-u sequence with the actual JSON escape bytes
        segment = Encoding.UTF8.GetBytes("2024-01-15T10:30:00\u002B05:00");

        // When hasComplexChildren is false, the segment is the unescaped value already
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: false, out OffsetDateTime value);
        Assert.IsTrue(result);
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
    }

    [TestMethod]
    public void TryGetValue_OffsetDateTime_WithEscapeSequence_HasComplexChildren()
    {
        // Simulate a segment that contains a JSON escape: the '+' is escaped as \u002B
        // In the raw JSON bytes, this appears as the literal bytes: 0x5C 0x75 0x30 0x30 0x32 0x42
        byte[] segment = Encoding.UTF8.GetBytes("2024-01-15T10:30:00\\u002B05:00");

        // With hasComplexChildren=true, the method should unescape first, then parse
        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: true, out OffsetDateTime value);
        Assert.IsTrue(result, "Should parse successfully after unescaping \\u002B to '+'");
        Assert.AreEqual(2024, value.Year);
    }

    [TestMethod]
    public void TryGetValue_OffsetDate_WithEscapeSequence_HasComplexChildren()
    {
        // "2024-01-15+05:00" with '+' escaped as \u002B
        byte[] segment = Encoding.UTF8.GetBytes("2024-01-15\\u002B05:00");

        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: true, out OffsetDate value);
        Assert.IsTrue(result, "Should parse successfully after unescaping \\u002B to '+'");
        Assert.AreEqual(2024, value.Year);
    }

    [TestMethod]
    public void TryGetValue_OffsetTime_WithEscapeSequence_HasComplexChildren()
    {
        // "10:30:00+05:00" with '+' escaped as \u002B
        byte[] segment = Encoding.UTF8.GetBytes("10:30:00\\u002B05:00");

        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: true, out OffsetTime value);
        Assert.IsTrue(result, "Should parse successfully after unescaping \\u002B to '+'");
        Assert.AreEqual(10, value.Hour);
    }

    [TestMethod]
    public void TryGetValue_LocalDate_WithEscapeSequence_HasComplexChildren()
    {
        // "2024-01-15" with first '-' escaped as \u002D
        byte[] segment = Encoding.UTF8.GetBytes("2024\\u002D01-15");

        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: true, out LocalDate value);
        Assert.IsTrue(result, "Should parse successfully after unescaping \\u002D to '-'");
        Assert.AreEqual(2024, value.Year);
        Assert.AreEqual(1, value.Month);
        Assert.AreEqual(15, value.Day);
    }

    [TestMethod]
    public void TryGetValue_Period_WithEscapeSequence_HasComplexChildren()
    {
        // "P1Y2M3D" with 'Y' escaped as \u0059
        byte[] segment = Encoding.UTF8.GetBytes("P1\\u005902M3D");

        bool result = JsonReaderHelper.TryGetValue(segment, hasComplexChildren: true, out Period value);
        Assert.IsTrue(result, "Should parse successfully after unescaping \\u0059 to 'Y'");
        Assert.AreEqual(1, value.Years);
        Assert.AreEqual(2, value.Months);
        Assert.AreEqual(3, value.Days);
    }
}
