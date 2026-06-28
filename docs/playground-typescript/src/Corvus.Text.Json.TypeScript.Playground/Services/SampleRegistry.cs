using System.Reflection;
using Corvus.Text.Json.TypeScript.Playground.Models;

namespace Corvus.Text.Json.TypeScript.Playground.Services;

/// <summary>
/// The playground samples — the documented TypeScript example recipes (docs/typescript/examples), embedded as
/// resources. Each recipe pairs a JSON Schema with a runnable demo.ts that exercises the generated API
/// (build / evaluate / patch / produce / match / format factories …), so the samples track the docs exactly.
/// </summary>
public class SampleRegistry
{
    private readonly List<PlaygroundSample> samples;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleRegistry"/> class.
    /// </summary>
    public SampleRegistry() => this.samples = LoadRecipes();

    /// <summary>
    /// Gets all samples, ordered by recipe number.
    /// </summary>
    public IReadOnlyList<PlaygroundSample> Samples => this.samples;

    /// <summary>
    /// Gets the default sample (the first recipe).
    /// </summary>
    public PlaygroundSample Default => this.samples[0];

    /// <summary>
    /// Finds a sample by id, or null if not found.
    /// </summary>
    /// <param name="id">The sample id.</param>
    /// <returns>The sample, or null.</returns>
    public PlaygroundSample? FindById(string id) => this.samples.FirstOrDefault(s => s.Id == id);

    private static List<PlaygroundSample> LoadRecipes()
    {
        Assembly assembly = typeof(SampleRegistry).Assembly;
        const string marker = ".Recipes.";

        // Group each recipe's embedded resources (its schema .json + its demo.ts) by the recipe id.
        var grouped = new Dictionary<string, (string? Schema, string? Demo)>(StringComparer.Ordinal);
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            int idx = resource.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            string tail = resource[(idx + marker.Length)..]; // "<recipe>.<filestem>.<ext>"
            string[] parts = tail.Split('.');
            if (parts.Length < 3)
            {
                continue;
            }

            string ext = parts[^1];
            string fileStem = parts[^2];
            string recipe = string.Join('.', parts[..^2]);

            grouped.TryGetValue(recipe, out (string? Schema, string? Demo) entry);
            string content = ReadResource(assembly, resource);
            if (ext.Equals("ts", StringComparison.OrdinalIgnoreCase) && fileStem.Equals("demo", StringComparison.OrdinalIgnoreCase))
            {
                entry.Demo = content;
            }
            else if (ext.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                entry.Schema = content;
            }

            grouped[recipe] = entry;
        }

        var ordered = new List<(int Order, PlaygroundSample Sample)>();
        foreach ((string recipe, (string? Schema, string? Demo) v) in grouped)
        {
            if (v.Schema is null || v.Demo is null)
            {
                continue;
            }

            (int order, string display) = DisplayFor(recipe);
            ordered.Add((order, new PlaygroundSample(recipe, display, v.Schema, v.Demo)));
        }

        return ordered.OrderBy(r => r.Order).ThenBy(r => r.Sample.Id, StringComparer.Ordinal).Select(r => r.Sample).ToList();
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using Stream stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // "001-data-object" → (1, "1 · Data object"). Robust to '-'/'_' separators and resource-name munging.
    private static (int Order, string Display) DisplayFor(string recipe)
    {
        int start = 0;
        while (start < recipe.Length && !char.IsDigit(recipe[start]))
        {
            start++;
        }

        int end = start;
        while (end < recipe.Length && char.IsDigit(recipe[end]))
        {
            end++;
        }

        int order = int.TryParse(recipe[start..end], out int n) ? n : 999;
        string rest = recipe[end..].TrimStart('-', '_', ' ').Replace('-', ' ').Replace('_', ' ').Trim();
        string title = rest.Length > 0 ? char.ToUpperInvariant(rest[0]) + rest[1..] : recipe;
        return (order, order < 999 ? $"{order} · {title}" : title);
    }
}
