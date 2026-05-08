// <copyright file="CoverageBatch4Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 4: targeting DateTime ParseXxx success paths,
/// URI formatting edge cases, and JsonDocument internal paths.
/// </summary>
[TestClass]
public class CoverageBatch4Tests
{
    #region DateTime ParseXxx success paths

    /// <summary>
    /// Exercises <c>ParsePeriod</c> success return (line 101-102).
    /// </summary>
    [TestMethod]
    public void DateTime_ParsePeriod_Success()
    {
        // ISO 8601 duration: 1 year, 2 months, 3 days
        Period result = JsonElementHelpers.ParsePeriod("P1Y2M3D"u8);
        Assert.AreEqual(1, result.Years);
        Assert.AreEqual(2, result.Months);
        Assert.AreEqual(3, result.Days);
    }

    /// <summary>
    /// Exercises <c>ParseLocalDate</c> success return (line 130-131).
    /// </summary>
    [TestMethod]
    public void DateTime_ParseLocalDate_Success()
    {
        LocalDate result = JsonElementHelpers.ParseLocalDate("2024-03-15"u8);
        Assert.AreEqual(2024, result.Year);
        Assert.AreEqual(3, result.Month);
        Assert.AreEqual(15, result.Day);
    }

    /// <summary>
    /// Exercises <c>ParseOffsetTime</c> success return (line 172-173).
    /// </summary>
    [TestMethod]
    public void DateTime_ParseOffsetTime_Success()
    {
        OffsetTime result = JsonElementHelpers.ParseOffsetTime("10:30:00+05:00"u8);
        Assert.AreEqual(10, result.Hour);
        Assert.AreEqual(30, result.Minute);
        Assert.AreEqual(0, result.Second);
    }

    /// <summary>
    /// Exercises <c>ParseOffsetDateTime</c> success return (line 264-265).
    /// </summary>
    [TestMethod]
    public void DateTime_ParseOffsetDateTime_Success()
    {
        OffsetDateTime result = JsonElementHelpers.ParseOffsetDateTime("2024-03-15T10:30:00+05:00"u8);
        Assert.AreEqual(2024, result.Year);
        Assert.AreEqual(3, result.Month);
        Assert.AreEqual(15, result.Day);
        Assert.AreEqual(10, result.Hour);
    }

    /// <summary>
    /// Exercises <c>ParseOffsetDate</c> success return (line 378-379).
    /// </summary>
    [TestMethod]
    public void DateTime_ParseOffsetDate_Success()
    {
        OffsetDate result = JsonElementHelpers.ParseOffsetDate("2024-03-15+05:00"u8);
        Assert.AreEqual(2024, result.Year);
        Assert.AreEqual(3, result.Month);
        Assert.AreEqual(15, result.Day);
    }

    #endregion

    #region Utf8JsonWriter Grow paths

    /// <summary>
    /// Exercises <c>WriteNumberValueMinimized</c> Grow path for float (lines 57-59)
    /// by writing many float values in stream mode to accumulate BytesPending past the buffer.
    /// </summary>
    [TestMethod]
    public void Writer_Float_GrowPath()
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();
        // Write 30 floats — each takes ~10 bytes, accumulating ~300 BytesPending
        // which exceeds the 256-byte internal buffer, triggering Grow
        for (int i = 0; i < 30; i++)
        {
            writer.WriteNumberValue(1.23456789f + i);
        }

        writer.WriteEndArray();
        writer.Flush();

        Assert.IsTrue(stream.Length > 0);
    }

    /// <summary>
    /// Exercises <c>WriteNumberValueMinimized</c> Grow path for double (lines 57-59)
    /// by writing many double values in stream mode to accumulate BytesPending past the buffer.
    /// </summary>
    [TestMethod]
    public void Writer_Double_GrowPath()
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();
        for (int i = 0; i < 30; i++)
        {
            writer.WriteNumberValue(1.234567890123456789d + i);
        }

        writer.WriteEndArray();
        writer.Flush();

        Assert.IsTrue(stream.Length > 0);
    }

    #endregion

    #region JsonElement TryGetBoolean paths

    /// <summary>
    /// Exercises <c>TryGetBoolean</c> success path for true (lines 521-522).
    /// </summary>
    [TestMethod]
    public void JsonElement_TryGetBoolean_True()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true"u8.ToArray());
        bool success = doc.RootElement.TryGetBoolean(out bool value);
        Assert.IsTrue(success);
        Assert.IsTrue(value);
    }

    /// <summary>
    /// Exercises <c>TryGetBoolean</c> success path for false (lines 524-525).
    /// </summary>
    [TestMethod]
    public void JsonElement_TryGetBoolean_False()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false"u8.ToArray());
        bool success = doc.RootElement.TryGetBoolean(out bool value);
        Assert.IsTrue(success);
        Assert.IsFalse(value);
    }

    #endregion
}
