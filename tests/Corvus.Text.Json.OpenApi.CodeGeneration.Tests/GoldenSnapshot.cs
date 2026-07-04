// <copyright file="GoldenSnapshot.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// Byte-exact golden-snapshot helper for the OpenAPI client generators (the IR-refactor safety net,
/// Workstream A / Stage 0). It pins the concatenated generated output so the upcoming refactor can
/// prove it changes nothing the ~846 substring tests would miss — whitespace, file ordering, and
/// member ordering. The snapshots are temporary scaffolding: they are retired once the three C#
/// emitters collapse onto the shared IR.
/// </summary>
/// <remarks>
/// Set the environment variable <c>UPDATE_OPENAPI_GOLDEN=1</c> and run the tests to (re)write the
/// golden files; otherwise the helper asserts the freshly generated output matches the checked-in
/// golden exactly.
/// </remarks>
internal static class GoldenSnapshot
{
    /// <summary>
    /// Asserts that the rendered <paramref name="files"/> match the checked-in golden named
    /// <paramref name="name"/>, or writes the golden when <c>UPDATE_OPENAPI_GOLDEN=1</c>.
    /// </summary>
    /// <param name="name">The snapshot name (e.g. <c>petstore-3.0</c>).</param>
    /// <param name="files">The generated files to pin.</param>
    internal static void Verify(string name, IReadOnlyList<GeneratedFile> files)
    {
        string actual = Render(files);
        string dir = Path.Combine(AppContext.BaseDirectory, "TestData", "golden");
        string path = Path.Combine(dir, name + ".txt");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_GOLDEN") == "1")
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, actual);
            Assert.Inconclusive($"Wrote golden snapshot '{name}' ({files.Count} file(s)).");
            return;
        }

        Assert.IsTrue(
            File.Exists(path),
            $"Golden snapshot '{name}' is missing. Run the tests with UPDATE_OPENAPI_GOLDEN=1 to create it.");

        // Normalise line endings on read so the snapshot is robust to git autocrlf and cross-OS checkouts.
        string expected = Normalize(File.ReadAllText(path));
        Assert.AreEqual(
            expected,
            actual,
            $"Generated output for '{name}' drifted from the golden snapshot. If the change is intended, "
            + "re-run with UPDATE_OPENAPI_GOLDEN=1 and review the diff.");
    }

    /// <summary>
    /// Renders the generated files into one deterministic string: files ordered by name (ordinal),
    /// each prefixed with a banner, each guaranteed to end with a newline before the next banner.
    /// </summary>
    /// <param name="files">The files to render.</param>
    /// <returns>The deterministic concatenation.</returns>
    private static string Render(IReadOnlyList<GeneratedFile> files)
    {
        StringBuilder sb = new();
        foreach (GeneratedFile file in files.OrderBy(f => f.FileName, StringComparer.Ordinal))
        {
            sb.Append("// ===== ").Append(file.FileName).Append(" =====\n");
            sb.Append(file.Content);
            if (file.Content.Length == 0 || file.Content[^1] != '\n')
            {
                sb.Append('\n');
            }

            sb.Append('\n');
        }

        return Normalize(sb.ToString());
    }

    /// <summary>Collapses CRLF/CR to LF so snapshots compare equal regardless of line-ending normalisation.</summary>
    /// <param name="text">The text to normalise.</param>
    /// <returns>The text with all line endings as <c>\n</c>.</returns>
    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n");
}