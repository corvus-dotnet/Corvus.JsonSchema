using Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests;

/// <summary>
/// One test per suite file: the test fails if any case in the file fails, and the message lists them.
/// </summary>
[TestClass]
public class JsonSchemaTestSuiteTests
{
    /// <summary>
    /// Cases that are deliberately not supported, mirroring the exclusions used by Corvus.JsonSchema.
    /// </summary>
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.Ordinal);

    private static readonly string[] LeapSecondFragments = ["leap second", "60"];

    public static IEnumerable<object[]> RequiredFiles()
    {
        foreach (string draft in SuiteRunner.Drafts)
        {
            foreach (string file in SuiteRunner.Files(draft))
            {
                yield return [draft, Path.GetFileName(file), file];
            }
        }
    }

    public static IEnumerable<object[]> OptionalFiles()
    {
        foreach (string draft in SuiteRunner.Drafts)
        {
            foreach (string file in SuiteRunner.Files(draft, "optional"))
            {
                string key = draft + "/optional/" + Path.GetFileName(file);
                if (!ExcludedFiles.Contains(key))
                {
                    yield return [draft, "optional/" + Path.GetFileName(file), file];
                }
            }
        }
    }

    public static IEnumerable<object[]> FormatFiles()
    {
        foreach (string draft in SuiteRunner.Drafts)
        {
            foreach (string file in SuiteRunner.Files(draft, Path.Combine("optional", "format")))
            {
                yield return [draft, "optional/format/" + Path.GetFileName(file), file];
            }
        }
    }

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
        List<SuiteRunner.CaseResult> results = SuiteRunner.RunFile(file, draft, assertFormat);
        List<SuiteRunner.CaseResult> failures = results.Where(r => !r.Passed).ToList();
        if (skipLeapSeconds)
        {
            // Leap seconds are not supported by the shared date/time parsers (as in Corvus.JsonSchema).
            failures = failures.Where(f => !f.Test.Contains("leap second", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count}/{results.Count} cases failed in {Path.GetFileName(file)}:\n{SuiteRunner.Describe(failures)}");
        }
    }
}
