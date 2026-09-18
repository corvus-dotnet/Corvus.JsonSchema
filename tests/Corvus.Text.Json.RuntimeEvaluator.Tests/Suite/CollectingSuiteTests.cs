// <copyright file="CollectingSuiteTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests;

/// <summary>
/// The whole suite evaluated with a results collector: collecting mode takes the engine's general path, while flag
/// mode dispatches to the compile-time plans, so together the two runs check that every plan agrees with the general
/// path on every case.
/// </summary>
[TestClass]
public class CollectingSuiteTests
{
    public static IEnumerable<object[]> RequiredFiles() => JsonSchemaTestSuiteTests.RequiredFiles();

    public static IEnumerable<object[]> OptionalFiles() => JsonSchemaTestSuiteTests.OptionalFiles();

    public static string DisplayName(System.Reflection.MethodInfo method, object[] data) => $"{data[0]}/{data[1]}";

    [TestMethod]
    [DynamicData(nameof(RequiredFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Required(string draft, string name, string file)
    {
        AssertFile(file, draft);
    }

    [TestMethod]
    [DynamicData(nameof(OptionalFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Optional(string draft, string name, string file)
    {
        AssertFile(file, draft);
    }

    private static void AssertFile(string file, string draft)
    {
        List<SuiteRunner.CaseResult> results = SuiteRunner.RunFile(file, draft, assertFormat: false, collecting: true);
        List<SuiteRunner.CaseResult> failures = results.Where(r => !r.Passed).ToList();
        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count}/{results.Count} cases failed in {Path.GetFileName(file)} (collecting):\n{SuiteRunner.Describe(failures)}");
        }
    }
}
