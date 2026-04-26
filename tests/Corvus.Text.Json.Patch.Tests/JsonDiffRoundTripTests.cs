// <copyright file="JsonDiffRoundTripTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Patch;

using StjJsonDocument = System.Text.Json.JsonDocument;
using StjJsonElement = System.Text.Json.JsonElement;
using Xunit;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Data-driven round-trip tests for <see cref="JsonDiffExtensions.CreatePatch"/>.
/// Test cases are drawn from json-patch/json-patch-tests, json-everything,
/// SystemTextJson.JsonDiffPatch, and hand-crafted edge cases.
/// Each test: generates a diff (source → target), applies the patch to source,
/// and verifies the result equals target.
/// </summary>
public class JsonDiffRoundTripTests
{
    private static readonly Lazy<DiffTestCase[]> LazyTestCases = new(LoadTestCases);

    public static IEnumerable<object[]> TestData()
    {
        DiffTestCase[] cases = LazyTestCases.Value;
        for (int i = 0; i < cases.Length; i++)
        {
            yield return [cases[i]];
        }
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void DiffRoundTrip(DiffTestCase testCase)
    {
        using ParsedJsonDocument<JsonElement> sourceDoc =
            ParsedJsonDocument<JsonElement>.Parse(testCase.Source);
        using ParsedJsonDocument<JsonElement> targetDoc =
            ParsedJsonDocument<JsonElement>.Parse(testCase.Target);

        JsonPatchDocument patch = JsonDiffExtensions.CreatePatch(
            sourceDoc.RootElement, targetDoc.RootElement);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable mutable = builder.RootElement;
        bool applied = mutable.TryApplyPatch(in patch);
        Assert.True(applied, $"Patch application should succeed for: {testCase.Comment}");

        JsonElement patchedElement = mutable.Clone();
        Assert.Equal(targetDoc.RootElement, patchedElement);
    }

    private static DiffTestCase[] LoadTestCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "diff-tests.json");
        string json = File.ReadAllText(path);

        using StjJsonDocument doc = StjJsonDocument.Parse(json);
        StjJsonElement root = doc.RootElement;

        var cases = new List<DiffTestCase>();
        int index = 0;
        foreach (StjJsonElement element in root.EnumerateArray())
        {
            string comment = element.TryGetProperty("comment", out StjJsonElement commentEl)
                ? commentEl.GetString() ?? $"Test #{index}"
                : $"Test #{index}";

            string source = element.GetProperty("source").GetRawText();
            string target = element.GetProperty("target").GetRawText();

            cases.Add(new DiffTestCase(comment, source, target));
            index++;
        }

        return cases.ToArray();
    }

    public sealed class DiffTestCase
    {
        public DiffTestCase(string comment, string source, string target)
        {
            Comment = comment;
            Source = source;
            Target = target;
        }

        public string Comment { get; }
        public string Source { get; }
        public string Target { get; }

        public override string ToString() => Comment;
    }
}
