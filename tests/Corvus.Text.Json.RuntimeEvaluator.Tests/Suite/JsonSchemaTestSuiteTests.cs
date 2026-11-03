using Corvus.Text.Json.RuntimeEvaluator.Tests.Suite;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests;

/// <summary>
/// One test per suite file and draft: the test fails if any case in the file fails, and the message lists them.
/// </summary>
/// <remarks>
/// Each test case in the file decides for itself whether it applies to the draft (its <c>compatibility</c>) and
/// whether <c>format</c> is asserted (its <c>format-assertion</c> tag); see <see cref="SuiteRunner.RunFile"/>.
/// </remarks>
[TestClass]
public class JsonSchemaTestSuiteTests
{
    /// <summary>
    /// Cases that are deliberately not supported, mirroring the exclusions used by Corvus.JsonSchema, keyed by
    /// <c>draft/relative-path</c>.
    /// </summary>
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.Ordinal);

    public static IEnumerable<object[]> RequiredFiles() => FilesFor(optional: false);

    public static IEnumerable<object[]> OptionalFiles() => FilesFor(optional: true);

    public static string DisplayName(System.Reflection.MethodInfo method, object[] data) => $"{data[0]}/{data[1]}";

    [TestMethod]
    [DynamicData(nameof(RequiredFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Required(string draft, string file)
    {
        AssertFile(file, draft);
    }

    [TestMethod]
    [DynamicData(nameof(OptionalFiles), DynamicDataDisplayName = nameof(DisplayName))]
    public void Optional(string draft, string file)
    {
        AssertFile(file, draft);
    }

    private static IEnumerable<object[]> FilesFor(bool optional)
    {
        foreach (string draft in SuiteRunner.Drafts)
        {
            foreach (string file in SuiteRunner.Files(optional))
            {
                if (!ExcludedFiles.Contains(draft + "/" + file))
                {
                    yield return [draft, file];
                }
            }
        }
    }

    private static void AssertFile(string file, string draft)
    {
        List<SuiteRunner.CaseResult> results = SuiteRunner.RunFile(file, draft);
        List<SuiteRunner.CaseResult> failures = SuiteRunner.Failures(results);
        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count}/{results.Count} cases failed in {file}:\n{SuiteRunner.Describe(failures)}");
        }
    }
}