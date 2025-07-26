// <copyright file="Metaschema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp.QuickStart;

/// <summary>
/// Apply the metaschema to a document resolver.
/// </summary>
internal static class Metaschema
{
    /// <summary>
    /// Add the standard metaschema to the document resolver.
    /// </summary>
    /// <param name="documentResolver">The document resolver to which to apply the metaschema.</param>
    /// <returns>A reference to the <see cref="IDocumentResolver"/> after the operation has completed.</returns>
    internal static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        var assembly = Assembly.GetAssembly(typeof(Metaschema));

        Debug.Assert(assembly is not null, "The assembly containing this type must exist");

        documentResolver.AddDocument(
            "http://json-schema.org/draft-06/schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft6.schema.json")));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-07/schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft7.schema.json")));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/applicator",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.applicator.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/content",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.content.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/core",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.core.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/format",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.format.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/hyper-schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.hyper-schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/meta-data",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.meta-data.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/validation",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2019_09.meta.validation.json")));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/applicator",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.applicator.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/content",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.content.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/core",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.core.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format-annotation",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.format-annotation.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format-assertion",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.format-assertion.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/hyper-schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.hyper-schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/meta-data",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.meta-data.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/unevaluated",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.unevaluated.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/validation",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.draft2020_12.meta.validation.json")));

        documentResolver.AddDocument(
            "https://corvus-oss.org/json-schema/2020-12/schema",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.corvus.schema.json")));
        documentResolver.AddDocument(
            "https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions",
            JsonDocument.Parse(ReadResource(assembly, "metaschema.corvus.meta.corvus-extensions.json")));

        return documentResolver;
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using Stream? resourceStream = assembly.GetManifestResourceStream(name);
        Debug.Assert(resourceStream is not null, $"The manifest resource stream {name} does not exist.");
        using var reader = new StreamReader(resourceStream);
        return reader.ReadToEnd();
    }
}