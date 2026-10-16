// <copyright file="FixtureCases.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.Fixtures;

/// <summary>
/// Discovers the fixture cases copied beside a test assembly (<c>Fixtures/&lt;document&gt;/*.json</c>).
/// </summary>
public static class FixtureCases
{
    /// <summary>
    /// Enumerates the cases of every fixture file for a document, as test-data rows of file path, case index and
    /// case name.
    /// </summary>
    /// <param name="documentName">The document's directory name under <c>Fixtures</c>.</param>
    /// <returns>The rows.</returns>
    public static IEnumerable<object[]> Enumerate(string documentName)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", documentName);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(file));
            JsonElement cases = document.RootElement.GetProperty("cases");
            int count = cases.GetArrayLength();
            for (int i = 0; i < count; i++)
            {
                yield return [file, i, cases[i].GetProperty("name").GetString()!];
            }
        }
    }

    /// <summary>
    /// Loads a fixture file.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <returns>The parsed document; dispose it after the case has run.</returns>
    public static ParsedJsonDocument<JsonElement> Load(string file) => ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(file));
}