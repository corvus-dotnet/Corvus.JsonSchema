using System.Reflection;

namespace Corvus.Text.Json.OpenApi.TypeScript.Playground.Services;

public static class SampleRegistry
{
    public static IReadOnlyList<PlaygroundSample> GetSamples()
    {
        return
        [
            new("petstore-client", "Petstore Client (Basic)"),
            new("petstore-advanced", "Petstore Extended (Advanced)"),
        ];
    }

    /// <summary>
    /// Loads all files for a sample. Returns (name, content, isRoot) tuples.
    /// </summary>
    public static IReadOnlyList<(string Name, string Content, bool IsRoot)> LoadSampleFiles(string sampleId)
    {
        var results = new List<(string Name, string Content, bool IsRoot)>();
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceId = sampleId.Replace('-', '_');
        string prefix = $"Corvus.Text.Json.OpenApi.TypeScript.Playground.Samples.{resourceId}.";
        string[] allResources = assembly.GetManifestResourceNames();

        string[] sampleResources = Array.FindAll(allResources, r =>
            r.StartsWith(prefix, StringComparison.Ordinal) && !r.EndsWith(".ts", StringComparison.Ordinal));

        if (sampleResources.Length == 0)
        {
            string? content = LoadEmbeddedResource($"Corvus.Text.Json.OpenApi.TypeScript.Playground.Samples.{resourceId}.json");
            if (content is not null)
            {
                results.Add(("openapi.json", content, true));
            }

            return results;
        }

        foreach (string resourceName in sampleResources.OrderBy(r => r))
        {
            string fileName = resourceName[prefix.Length..];
            string? content = LoadEmbeddedResource(resourceName);
            if (content is not null)
            {
                bool isRoot = fileName.StartsWith("openapi", StringComparison.OrdinalIgnoreCase)
                    || sampleResources.Length == 1;
                results.Add((fileName, content, isRoot));
            }
        }

        if (results.Count > 0 && !results.Any(r => r.IsRoot))
        {
            (string Name, string Content, bool IsRoot) first = results[0];
            results[0] = (first.Name, first.Content, true);
        }

        return results;
    }

    /// <summary>
    /// Loads the user code for a sample (from the embedded usercode.ts file).
    /// Returns null if no user code file exists for the sample.
    /// </summary>
    public static string? LoadSampleUserCode(string sampleId)
    {
        string resourceId = sampleId.Replace('-', '_');
        string resourceName = $"Corvus.Text.Json.OpenApi.TypeScript.Playground.Samples.{resourceId}.usercode.ts";
        return LoadEmbeddedResource(resourceName);
    }

    public static string? LoadEmbeddedSample(string resourceName)
    {
        return LoadEmbeddedResource(resourceName);
    }

    private static string? LoadEmbeddedResource(string resourceName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

public record PlaygroundSample(string Id, string DisplayName);