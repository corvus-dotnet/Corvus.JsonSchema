// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Regression test for schema-location handling. A keyword key (e.g. a <c>patternProperties</c> regex) is a
/// valid JSON Pointer token but may contain characters that are unsafe in a URI fragment (such as <c>^</c>).
/// Schema locations are kept as raw JSON Pointers, so the generated <c>JsonSchemaEvaluation.TryCopyPath(...)</c>
/// literal must contain the character verbatim (not percent-encoded); percent-encoding is applied only when a
/// location is rendered as a URI, and <c>TryCopyPath</c> tolerates the raw character without faulting.
/// </summary>
[TestClass]
public class SchemaLocationEncodingTests
{
    private const string PatternPropertiesWithUriUnsafeKey =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": { "^x-": { "type": "string" } }
        }
        """;

    [TestMethod]
    public async Task PatternPropertiesWithUriUnsafeKey_EmitsRawJsonPointerSchemaLocation()
    {
        string code = await TestJsonSchemaCodeGenerator.GenerateCodeTextForVirtualFile(
            "schema_location_encoding.json",
            PatternPropertiesWithUriUnsafeKey,
            "Corvus.Text.Json.Tests.SchemaLocationGenerated",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            formatModeOverrides: null);

        // The '^' of the patternProperties key is a valid JSON Pointer token character; the schema location is
        // a JSON Pointer, not a URI, so it is kept verbatim — and must not be percent-encoded.
        StringAssert.Contains(code, "patternProperties/^x-", "patternProperties location must keep the raw '^'.");
        Assert.IsFalse(
            code.Contains("%5E", System.StringComparison.Ordinal),
            "A schema-location JSON Pointer must not be percent-encoded.");
    }
}