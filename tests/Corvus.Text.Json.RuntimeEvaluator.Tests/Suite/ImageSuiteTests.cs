// <copyright file="ImageSuiteTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests;

/// <summary>
/// The whole suite again, with every schema round-tripped through a program image before evaluation: a program
/// loaded from an image must behave exactly as the compiled program it was written from.
/// </summary>
[TestClass]
public class ImageSuiteTests
{
    public static IEnumerable<object[]> RequiredFiles() => JsonSchemaTestSuiteTests.RequiredFiles();

    public static IEnumerable<object[]> OptionalFiles() => JsonSchemaTestSuiteTests.OptionalFiles();

    public static IEnumerable<object[]> FormatFiles() => JsonSchemaTestSuiteTests.FormatFiles();

    public static string DisplayName(System.Reflection.MethodInfo method, object[] data) => $"{data[0]}/{data[1]}";

    [TestMethod]
    [DynamicData(nameof(RequiredFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Required(string draft, string name, string file)
    {
        AssertFile(file, draft, assertFormat: false);
    }

    [TestMethod]
    [DynamicData(nameof(OptionalFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Optional(string draft, string name, string file)
    {
        AssertFile(file, draft, assertFormat: false);
    }

    [TestMethod]
    [DynamicData(nameof(FormatFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Format(string draft, string name, string file)
    {
        AssertFile(file, draft, assertFormat: true, skipLeapSeconds: true);
    }

    private static void AssertFile(string file, string draft, bool assertFormat, bool skipLeapSeconds = false)
    {
        List<SuiteRunner.CaseResult> results = SuiteRunner.RunFile(file, draft, assertFormat, throughImage: true);
        List<SuiteRunner.CaseResult> failures = results.Where(r => !r.Passed).ToList();
        if (skipLeapSeconds)
        {
            failures = failures.Where(f => !f.Test.Contains("leap second", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count}/{results.Count} cases failed in {Path.GetFileName(file)} (through image):\n{SuiteRunner.Describe(failures)}");
        }
    }
}
