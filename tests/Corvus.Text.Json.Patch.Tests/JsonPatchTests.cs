// <copyright file="JsonPatchTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json.Patch;
using Xunit;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Tests for RFC 6902 JSON Patch operations using the json-patch-tests test suite.
/// </summary>
public class JsonPatchTests
{
    private static readonly Lazy<JsonDocument> TestsDocument = new(
        () => JsonDocument.Parse(File.ReadAllText(GetTestFilePath("tests.json"))));

    private static readonly Lazy<JsonDocument> SpecTestsDocument = new(
        () => JsonDocument.Parse(File.ReadAllText(GetTestFilePath("spec_tests.json"))));

    public static IEnumerable<object[]> SuccessTestData()
    {
        return GetTestCases(TestsDocument.Value, expectSuccess: true);
    }

    public static IEnumerable<object[]> ErrorTestData()
    {
        return GetTestCases(TestsDocument.Value, expectSuccess: false);
    }

    public static IEnumerable<object[]> SpecSuccessTestData()
    {
        return GetTestCases(SpecTestsDocument.Value, expectSuccess: true);
    }

    public static IEnumerable<object[]> SpecErrorTestData()
    {
        return GetTestCases(SpecTestsDocument.Value, expectSuccess: false);
    }

    [Theory]
    [MemberData(nameof(SuccessTestData))]
    public void SuccessTests(string comment, int index, string docJson, string patchJson, string expectedJson)
    {
        RunSuccessTest(docJson, patchJson, expectedJson);
    }

    [Theory]
    [MemberData(nameof(ErrorTestData))]
    public void ErrorTests(string comment, int index, string docJson, string patchJson)
    {
        RunErrorTest(docJson, patchJson);
    }

    [Theory]
    [MemberData(nameof(SpecSuccessTestData))]
    public void SpecSuccessTests(string comment, int index, string docJson, string patchJson, string expectedJson)
    {
        RunSuccessTest(docJson, patchJson, expectedJson);
    }

    [Theory]
    [MemberData(nameof(SpecErrorTestData))]
    public void SpecErrorTests(string comment, int index, string docJson, string patchJson)
    {
        RunErrorTest(docJson, patchJson);
    }

    private static void RunSuccessTest(string docJson, string patchJson, string expectedJson)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse(docJson);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        bool result = JsonPatchExtensions.TryApplyPatch(ref root, in patch);

        Assert.True(result, "Patch application should succeed.");

        // Compare the result with the expected output.
        JsonElement expected = JsonElement.ParseValue(expectedJson);
        Assert.True(
            root.Equals(expected),
            $"Expected: {expectedJson}\nActual: {root}");
    }

    private static void RunErrorTest(string docJson, string patchJson)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse(docJson);

        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        bool result = JsonPatchExtensions.TryApplyPatch(ref root, in patch);

        Assert.False(result, "Patch application should fail.");
    }

    private static IEnumerable<object[]> GetTestCases(JsonDocument document, bool expectSuccess)
    {
        int index = 0;
        foreach (System.Text.Json.JsonElement testCase in document.RootElement.EnumerateArray())
        {
            int currentIndex = index++;

            // Skip disabled tests.
            if (testCase.TryGetProperty("disabled", out System.Text.Json.JsonElement disabled) &&
                disabled.GetBoolean())
            {
                continue;
            }

            bool hasExpected = testCase.TryGetProperty("expected", out System.Text.Json.JsonElement expected);
            bool hasError = testCase.TryGetProperty("error", out _);

            string comment = testCase.TryGetProperty("comment", out System.Text.Json.JsonElement commentElem)
                ? commentElem.GetString() ?? $"Test #{currentIndex}"
                : $"Test #{currentIndex}";

            string docJson = testCase.GetProperty("doc").GetRawText();
            string patchJson = testCase.GetProperty("patch").GetRawText();

            if (expectSuccess && hasExpected && !hasError)
            {
                yield return new object[] { comment, currentIndex, docJson, patchJson, expected.GetRawText() };
            }
            else if (!expectSuccess && hasError)
            {
                yield return new object[] { comment, currentIndex, docJson, patchJson };
            }
        }
    }

    private static string GetTestFilePath(string fileName)
    {
        // Walk up from the test output directory to find the repo root.
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "tests", "json-patch-tests", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(
            $"Could not find {fileName}. Ensure the json-patch-tests submodule is initialized.");
    }
}