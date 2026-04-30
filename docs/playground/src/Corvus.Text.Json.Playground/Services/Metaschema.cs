using System.Reflection;
using Corvus.Json;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Loads JSON Schema metaschema documents from embedded resources into a document resolver.
/// This is WASM-safe (no file I/O) — all metaschemas are embedded in the assembly.
/// </summary>
internal static class Metaschema
{
    // Maps schema URI → the trailing portion of the embedded resource name (after the assembly prefix).
    // Hyphens in directory names become underscores in resource names.
    private static readonly (string Uri, string ResourceSuffix)[] MetaschemaEntries =
    [
        ("http://json-schema.org/draft-04/schema", "metaschema.draft4.schema.json"),
        ("http://json-schema.org/draft-06/schema", "metaschema.draft6.schema.json"),
        ("http://json-schema.org/draft-07/schema", "metaschema.draft7.schema.json"),

        ("https://json-schema.org/draft/2019-09/schema", "metaschema.draft2019_09.schema.json"),
        ("https://json-schema.org/draft/2019-09/meta/applicator", "metaschema.draft2019_09.meta.applicator.json"),
        ("https://json-schema.org/draft/2019-09/meta/content", "metaschema.draft2019_09.meta.content.json"),
        ("https://json-schema.org/draft/2019-09/meta/core", "metaschema.draft2019_09.meta.core.json"),
        ("https://json-schema.org/draft/2019-09/meta/format", "metaschema.draft2019_09.meta.format.json"),
        ("https://json-schema.org/draft/2019-09/meta/hyper-schema", "metaschema.draft2019_09.meta.hyper-schema.json"),
        ("https://json-schema.org/draft/2019-09/meta/meta-data", "metaschema.draft2019_09.meta.meta-data.json"),
        ("https://json-schema.org/draft/2019-09/meta/validation", "metaschema.draft2019_09.meta.validation.json"),

        ("https://json-schema.org/draft/2020-12/schema", "metaschema.draft2020_12.schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/applicator", "metaschema.draft2020_12.meta.applicator.json"),
        ("https://json-schema.org/draft/2020-12/meta/content", "metaschema.draft2020_12.meta.content.json"),
        ("https://json-schema.org/draft/2020-12/meta/core", "metaschema.draft2020_12.meta.core.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-annotation", "metaschema.draft2020_12.meta.format-annotation.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-assertion", "metaschema.draft2020_12.meta.format-assertion.json"),
        ("https://json-schema.org/draft/2020-12/meta/hyper-schema", "metaschema.draft2020_12.meta.hyper-schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/meta-data", "metaschema.draft2020_12.meta.meta-data.json"),
        ("https://json-schema.org/draft/2020-12/meta/unevaluated", "metaschema.draft2020_12.meta.unevaluated.json"),
        ("https://json-schema.org/draft/2020-12/meta/validation", "metaschema.draft2020_12.meta.validation.json"),

        ("https://corvus-oss.org/json-schema/2020-12/schema", "metaschema.corvus.schema.json"),
        ("https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions", "metaschema.corvus.meta.corvus-extensions.json"),
    ];

    internal static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        Assembly assembly = typeof(Metaschema).Assembly;
        string[] allResourceNames = assembly.GetManifestResourceNames();

        foreach ((string uri, string suffix) in MetaschemaEntries)
        {
            string? resourceName = Array.Find(allResourceNames, n => n.EndsWith(suffix, StringComparison.Ordinal));
            if (resourceName is null)
            {
                throw new InvalidOperationException(
                    $"Embedded resource ending with '{suffix}' not found. Available: {string.Join(", ", allResourceNames)}");
            }

            using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            documentResolver.AddDocument(uri, System.Text.Json.JsonDocument.Parse(json));
        }

        return documentResolver;
    }
}
