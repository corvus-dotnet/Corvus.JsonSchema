// <copyright file="Rfc3339OffsetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NodaTime;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// A date-time or time written from a value has an RFC 3339 offset, with hours and minutes, so it is valid against
/// its own format. (The ISO 8601 form without minutes, such as "+01", is not an RFC 3339 offset.)
/// </summary>
[TestClass]
public class Rfc3339OffsetTests
{
    [TestMethod]
    [DataRow(1, 0, "2025-04-09T12:00:00+01:00")]
    [DataRow(-5, -30, "2025-04-09T12:00:00-05:30")]
    [DataRow(0, 0, "2025-04-09T12:00:00Z")]
    public void ADateTimeIsWrittenWithHoursAndMinutesInItsOffset(int hours, int minutes, string expected)
    {
        var value = new JsonDateTime(new DateTimeOffset(2025, 4, 9, 12, 0, 0, new TimeSpan(hours, minutes, 0)));
        Assert.AreEqual(expected, (string)value.AsString);
        Assert.IsTrue(value.IsValid());
    }

    [TestMethod]
    [DataRow(1, 0, "12:00:00+01:00")]
    [DataRow(0, 0, "12:00:00Z")]
    public void ATimeIsWrittenWithHoursAndMinutesInItsOffset(int hours, int minutes, string expected)
    {
        var value = new JsonTime(new OffsetTime(new LocalTime(12, 0), Offset.FromHoursAndMinutes(hours, minutes)));
        Assert.AreEqual(expected, (string)value.AsString);
        Assert.IsTrue(value.IsValid());
    }

    [TestMethod]
    [DataRow("2025-04-09T12:00:00+01")]
    [DataRow("2025-04-09T12:00:00+0100")]
    public void ADateTimeWithAnOffsetWithoutMinutesIsNotValid(string text)
    {
        Assert.IsFalse(new JsonDateTime(text).IsValid());
    }
}