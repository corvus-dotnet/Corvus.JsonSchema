// <copyright file="GeneratedRegularExpressionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Reflection;
using Corvus.Text.Json.AsyncApi26;

namespace Corvus.Text.Json.AsyncApi26.Tests;

/// <summary>
/// The checked-in generated code carries each regular expression as Corvus.Regex.Ecma262's compiled matcher, with the
/// compiled image of it, which the library it references must read: an image it ignores still answers rightly, but
/// is compiled from its pattern when first used, which is the work the image is there to save. The library reports an
/// ignored image on its event source, which this listens to from before any test runs, since a holder's regular
/// expression is made once, by whichever test first needs it.
/// </summary>
[TestClass]
public class GeneratedRegularExpressionTests
{
    private static ImageListener? listener;

    [AssemblyInitialize]
    public static void ListenFromTheStart(TestContext context)
    {
        _ = context;
        listener = new ImageListener();
    }

    [AssemblyCleanup]
    public static void StopListening() => listener?.Dispose();

    [TestMethod]
    public void EveryCheckedInImageIsReadByTheLibrary()
    {
        Type? holders = typeof(AsyncApiDocument).Assembly.GetType("Corvus.Text.Json.AsyncApi26.CorvusJsonSchemaRegularExpressions");
        Assert.IsNotNull(holders, "The generated code has no shared regular expressions.");
        PropertyInfo[] regexes = holders.GetProperties(BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsTrue(regexes.Length > 0);
        foreach (PropertyInfo regex in regexes)
        {
            Assert.IsNotNull(regex.GetValue(null), regex.Name);
        }

        Assert.AreEqual(0, listener!.Ignored.Count, "Images ignored: " + string.Join(", ", listener.Ignored));
    }

    private sealed class ImageListener : EventListener
    {
        public ConcurrentQueue<string> Ignored { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Corvus.Regex.Ecma262")
            {
                this.EnableEvents(eventSource, EventLevel.Warning);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName == "ImageIgnored")
            {
                this.Ignored.Enqueue(eventData.Payload is { Count: > 1 } payload ? payload[1]?.ToString() ?? "?" : "?");
            }
        }
    }
}
