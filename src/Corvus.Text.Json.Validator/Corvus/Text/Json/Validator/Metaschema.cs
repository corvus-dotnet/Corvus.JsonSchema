// <copyright file="Metaschema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Reflection;
using System.Text.Json;
using Corvus.Json;

namespace Corvus.Text.Json.Validator;

/// <summary>
/// Apply the metaschema to a document resolver.
/// </summary>
internal static class Metaschema
{
    private static readonly Assembly ResourceAssembly = typeof(Metaschema).Assembly;

    internal static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        AddResource(documentResolver, "http://json-schema.org/draft-04/schema", "metaschema.draft4.schema.json");
        AddResource(documentResolver, "http://json-schema.org/draft-06/schema", "metaschema.draft6.schema.json");
        AddResource(documentResolver, "http://json-schema.org/draft-07/schema", "metaschema.draft7.schema.json");

        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/schema", "metaschema.draft2019-09.schema.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/applicator", "metaschema.draft2019-09.meta.applicator.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/content", "metaschema.draft2019-09.meta.content.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/core", "metaschema.draft2019-09.meta.core.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/format", "metaschema.draft2019-09.meta.format.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/hyper-schema", "metaschema.draft2019-09.meta.hyper-schema.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/meta-data", "metaschema.draft2019-09.meta.meta-data.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2019-09/meta/validation", "metaschema.draft2019-09.meta.validation.json");

        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/schema", "metaschema.draft2020-12.schema.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/applicator", "metaschema.draft2020-12.meta.applicator.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/content", "metaschema.draft2020-12.meta.content.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/core", "metaschema.draft2020-12.meta.core.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/format-annotation", "metaschema.draft2020-12.meta.format-annotation.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/format-assertion", "metaschema.draft2020-12.meta.format-assertion.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/hyper-schema", "metaschema.draft2020-12.meta.hyper-schema.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/meta-data", "metaschema.draft2020-12.meta.meta-data.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/unevaluated", "metaschema.draft2020-12.meta.unevaluated.json");
        AddResource(documentResolver, "https://json-schema.org/draft/2020-12/meta/validation", "metaschema.draft2020-12.meta.validation.json");

        AddResource(documentResolver, "https://corvus-oss.org/json-schema/2020-12/schema", "metaschema.corvus.schema.json");
        AddResource(documentResolver, "https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions", "metaschema.corvus.meta.corvus-extensions.json");

        return documentResolver;
    }

    private static void AddResource(IDocumentResolver documentResolver, string uri, string resourceName)
    {
        using Stream stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        documentResolver.AddDocument(uri, JsonDocument.Parse(stream));
    }
}