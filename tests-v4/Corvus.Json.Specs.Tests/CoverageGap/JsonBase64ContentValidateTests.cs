// <copyright file="JsonBase64ContentValidateTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Regression tests for issue #940: <see cref="JsonBase64Content"/> validation must
/// not throw for a well-formed base64 string whose decoded bytes happen to look like
/// a malformed JSON-string escape.
/// </summary>
[TestClass]
public class JsonBase64ContentValidateTests
{
    [TestMethod]
    [DataRow("XHU=", DisplayName = "decoded is \\u (issue minimal repro)")]
    [DataRow("XA==", DisplayName = "decoded ends with a backslash")]
    [DataRow("XHVEQkNC", DisplayName = "decoded is \\uDBCB, an unpaired surrogate")]
    public void Validate_WellFormedBase64_MalformedDecodedEscape_DoesNotThrow(string base64)
    {
        JsonBase64Content value = JsonBase64Content.ParseValue($"\"{base64}\"");

        // The whole point of the bug: validation itself must not throw, so a caller
        // can rely on ValidationContext.IsValid instead of wrapping it in try/catch.
        ValidationContext result = value.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);

        // Under draft 2020-12 semantics contentEncoding/contentMediaType are
        // annotations, so a non-decoding payload still validates.
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_Base64OfValidJsonWithStringEscape_DecodesToDocument()
    {
        // A valid JSON document containing a JSON string escape must round-trip: it is
        // the content, not a doubly-escaped string, so it parses directly.
        string decoded = "{\"a\":\"b\\nc\"}";
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded));
        JsonBase64Content value = JsonBase64Content.ParseValue($"\"{base64}\"");

        EncodedContentMediaTypeParseStatus status = value.TryGetJsonDocument(out System.Text.Json.JsonDocument? document);

        Assert.AreEqual(EncodedContentMediaTypeParseStatus.Success, status);
        Assert.IsNotNull(document);
        Assert.AreEqual("b\nc", document.RootElement.GetProperty("a").GetString());
        document.Dispose();
    }
}