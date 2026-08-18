// <copyright file="ParsedJsonDocumentOwnerParseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the ownership-transfer Parse overload that takes an <see cref="IDisposable"/>
/// owner of the memory backing the document (for example a pooled memory owner handed out
/// by a transport SDK).
/// </summary>
[TestClass]
public class ParsedJsonDocumentOwnerParseTests
{
    [TestMethod]
    public void ParseWithOwnerDisposesOwnerExactlyOnceWithDocument()
    {
        byte[] payload = """{"a":1}"""u8.ToArray();
        TrackingOwner owner = new();

        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(payload.AsMemory(), owner);

        Assert.AreEqual(0, owner.DisposeCount, "The owner must remain live while the document is in use.");
        Assert.AreEqual(1, doc.RootElement.GetProperty("a"u8).GetInt32());

        doc.Dispose();
        Assert.AreEqual(1, owner.DisposeCount, "Disposing the document must dispose the owner.");

        doc.Dispose();
        Assert.AreEqual(1, owner.DisposeCount, "A second document dispose must not dispose the owner again.");
    }

    [TestMethod]
    public void ParseWithOwnerLeavesOwnershipWithCallerOnParseFailure()
    {
        byte[] payload = "{not-json"u8.ToArray();
        TrackingOwner owner = new();

        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(payload.AsMemory(), owner));

        Assert.AreEqual(0, owner.DisposeCount, "On parse failure the caller keeps ownership, matching the rented-array overload.");
    }

    private sealed class TrackingOwner : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            this.DisposeCount++;
        }
    }
}