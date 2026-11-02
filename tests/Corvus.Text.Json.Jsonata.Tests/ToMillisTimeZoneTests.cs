// <copyright file="ToMillisTimeZoneTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Tests;

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// <c>$toMillis</c> reads a timestamp with no offset as UTC whatever the time zone of the machine
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1045, conformance case function-tomillis/case003).
/// </summary>
/// <remarks>
/// The tests move the process to a time zone that is not UTC, which .NET reads from <c>TZ</c> on Linux and macOS
/// only, so they run there.
/// </remarks>
[TestClass]
[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
public class ToMillisTimeZoneTests
{
    [TestMethod]
    [DataRow("2017-10-30", 1509321600000d)]
    [DataRow("2017-10-30T10:00", 1509357600000d)]
    [DataRow("2017-10-30T10:00:00.500", 1509357600500d)]
    [DataRow("2017-10-30T10:00:00Z", 1509357600000d)]
    [DataRow("2017-10-30T10:00:00+02:00", 1509350400000d)]
    [DataRow("2017-10-30T10:00:00-05:00", 1509375600000d)]
    public void ToMillis_DoesNotDependOnTheTimeZoneOfTheMachine(string timestamp, double expected)
    {
        InTimeZone("Europe/Oslo", () =>
        {
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes($"\"{timestamp}\""));

            JsonElement runtime = new JsonataEvaluator().Evaluate("$toMillis($)", doc.RootElement, workspace);
            Assert.AreEqual(expected, runtime.GetDouble(), "runtime");

            JsonElement generated = JsonataCodeGenHelpers.ToMillis(doc.RootElement, default, workspace);
            Assert.AreEqual(expected, generated.GetDouble(), "generated code");
        });
    }

    private static void InTimeZone(string timeZone, Action action)
    {
        string? previous = System.Environment.GetEnvironmentVariable("TZ");
        System.Environment.SetEnvironmentVariable("TZ", timeZone);
        TimeZoneInfo.ClearCachedData();
        try
        {
            Assert.AreNotEqual(TimeSpan.Zero, TimeZoneInfo.Local.BaseUtcOffset, "The process did not move out of UTC.");
            action();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TZ", previous);
            TimeZoneInfo.ClearCachedData();
        }
    }
}
