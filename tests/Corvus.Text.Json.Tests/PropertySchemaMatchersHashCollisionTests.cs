// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class PropertySchemaMatchersHashCollisionTests
{
    [TestMethod]
    public void ShortKeyCollidingWithLongerSiblingResolvesToItsOwnMatcher()
    {
        // "NewLine" (7 bytes) and "NewLinesForBracesInMethods" share their first 7 bytes, and the longer
        // key's hash top byte ((26 + 's' + 's') % 256) is 0 — the marker a fully-encoded short key uses — so
        // before the fix the two keys hash-collided and the short-key lookup fast-path skipped the full key
        // comparison, resolving "NewLine" to its sibling's matcher. This reproduces the OmniSharp
        // FormattingOptions case where {"NewLine":"..."} was validated against NewLinesForBracesInMethods's
        // boolean subschema and wrongly rejected.
        var matchers = new PropertySchemaMatchers<string>(
        [
            (static () => "NewLine"u8, "newline"),
            (static () => "NewLinesForBracesInMethods"u8, "braces"),
        ]);

        Assert.IsTrue(matchers.TryGetNamedMatcher("NewLine"u8, out string? newLine));
        Assert.AreEqual("newline", newLine);

        Assert.IsTrue(matchers.TryGetNamedMatcher("NewLinesForBracesInMethods"u8, out string? braces));
        Assert.AreEqual("braces", braces);
    }
}