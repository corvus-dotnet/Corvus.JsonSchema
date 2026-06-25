// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Regression tests for schema-location URI encoding. A keyword key (e.g. a <c>patternProperties</c>
/// regex) is a valid JSON Pointer token but may contain characters that are invalid in a URI fragment
/// (such as <c>^</c>). The generated <c>JsonSchemaEvaluation.TryCopyPath(...)</c> literal must therefore
/// be percent-encoded, otherwise detailed-results validation faults when it validates the path as a URI.
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
    public async Task PatternPropertiesWithUriUnsafeKey_EmitsPercentEncodedSchemaLocation()
    {
        string code = await TestJsonSchemaCodeGenerator.GenerateCodeTextForVirtualFile(
            "schema_location_encoding.json",
            PatternPropertiesWithUriUnsafeKey,
            "Corvus.Text.Json.Tests.SchemaLocationGenerated",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            formatModeOverrides: null);

        // The '^' of the patternProperties key must be percent-encoded (%5E) in the schema-location URI
        // literal — and the raw, URI-invalid form must not appear in a TryCopyPath literal.
        StringAssert.Contains(code, "patternProperties/%5Ex-", "patternProperties location must percent-encode '^'.");
        Assert.IsFalse(
            code.Contains("TryCopyPath(\"#/patternProperties/^", System.StringComparison.Ordinal),
            "A raw, URI-invalid '^' must not appear in a schema-location literal.");
    }
}
