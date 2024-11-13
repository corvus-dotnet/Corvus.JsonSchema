// <copyright file="Metaschema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Corvus.Json.SourceGenerator;

/// <summary>
/// Apply the metaschema to a document resolver.
/// </summary>
internal static class Metaschema
{
    /// <summary>
    /// Adds metaschema to a document resolver.
    /// </summary>
    /// <param name="documentResolver">The document resolver to which to add the metaschema.</param>
    /// <returns>A reference to the document resolver once the operation has completed.</returns>
    internal static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        var assembly = Assembly.GetAssembly(typeof(Metaschema));

        Debug.Assert(assembly is not null, "The assembly containing this type must exist");

        documentResolver.AddDocument(
            "http://json-schema.org/draft-04/schema",
            Parse(assembly, "metaschema.draft4.schema.json"));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-06/schema",
            Parse(assembly, "metaschema.draft6.schema.json"));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-07/schema",
            Parse(assembly, "metaschema.draft7.schema.json"));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/schema",
            Parse(assembly, "metaschema.draft2019_09.schema.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/applicator",
            Parse(assembly, "metaschema.draft2019_09.meta.applicator.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/content",
            Parse(assembly, "metaschema.draft2019_09.meta.content.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/core",
            Parse(assembly, "metaschema.draft2019_09.meta.core.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/format",
            Parse(assembly, "metaschema.draft2019_09.meta.format.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/hyper_schema",
            Parse(assembly, "metaschema.draft2019_09.meta.hyper-schema.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/meta_data",
            Parse(assembly, "metaschema.draft2019_09.meta.meta-data.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/validation",
            Parse(assembly, "metaschema.draft2019_09.meta.validation.json"));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/schema",
            Parse(assembly, "metaschema.draft2020_12.schema.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/applicator",
            Parse(assembly, "metaschema.draft2020_12.meta.applicator.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/content",
            Parse(assembly, "metaschema.draft2020_12.meta.content.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/core",
            Parse(assembly, "metaschema.draft2020_12.meta.core.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format_annotation",
            Parse(assembly, "metaschema.draft2020_12.meta.format-annotation.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format_assertion",
            Parse(assembly, "metaschema.draft2020_12.meta.format-assertion.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/hyper_schema",
            Parse(assembly, "metaschema.draft2020_12.meta.hyper-schema.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/meta_data",
            Parse(assembly, "metaschema.draft2020_12.meta.meta-data.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/unevaluated",
            Parse(assembly, "metaschema.draft2020_12.meta.unevaluated.json"));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/validation",
            Parse(assembly, "metaschema.draft2020_12.meta.validation.json"));

        documentResolver.AddDocument(
            "https://corvus-oss.org/json_schema/2020-12/schema",
            Parse(assembly, "metaschema.corvus.schema.json"));
        documentResolver.AddDocument(
            "https://corvus-oss.org/json_schema/2020-12/meta/corvus_extensions",
            Parse(assembly, "metaschema.corvus.meta.corvus-extensions.json"));

        return documentResolver;
    }

    private static JsonDocument Parse(Assembly assembly, string resourceName)
    {
        try
        {
            return JsonDocument.Parse(ReadResource(assembly, resourceName));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The metaschema could not be parsed: {resourceName}", ex);
        }
    }

    private static string ReadResource(Assembly assembly, string path)
    {
        string name = path;
        using Stream? resourceStream = assembly.GetManifestResourceStream(name);
        Debug.Assert(resourceStream is not null, $"The manifest resource stream {name} does not exist.");
        using var reader = new StreamReader(resourceStream);
        return reader.ReadToEnd();
    }
}