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

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Apply the metaschema to a document resolver.
/// </summary>
internal static class Metaschema
{
    // The metaschemas are parsed once per process and shared by every resolver they are added to:
    // documents are immutable once parsed, and nothing in the CLI disposes or resets its resolvers.
    private static readonly Lazy<(string Uri, JsonDocument Document)[]> Documents = new(LoadDocuments);

    internal static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        foreach ((string uri, JsonDocument document) in Documents.Value)
        {
            documentResolver.AddDocument(uri, document);
        }

        return documentResolver;
    }

    private static (string Uri, JsonDocument Document)[] LoadDocuments()
    {
        string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException("Cannot find the executing assembly path.");

        return
        [
            ("http://json-schema.org/draft-04/schema", Parse(assemblyPath, "./metaschema/draft4/schema.json")),
            ("http://json-schema.org/draft-06/schema", Parse(assemblyPath, "./metaschema/draft6/schema.json")),
            ("http://json-schema.org/draft-07/schema", Parse(assemblyPath, "./metaschema/draft7/schema.json")),
            ("https://json-schema.org/draft/2019-09/schema", Parse(assemblyPath, "./metaschema/draft2019-09/schema.json")),
            ("https://json-schema.org/draft/2019-09/meta/applicator", Parse(assemblyPath, "./metaschema/draft2019-09/meta/applicator.json")),
            ("https://json-schema.org/draft/2019-09/meta/content", Parse(assemblyPath, "./metaschema/draft2019-09/meta/content.json")),
            ("https://json-schema.org/draft/2019-09/meta/core", Parse(assemblyPath, "./metaschema/draft2019-09/meta/core.json")),
            ("https://json-schema.org/draft/2019-09/meta/format", Parse(assemblyPath, "./metaschema/draft2019-09/meta/format.json")),
            ("https://json-schema.org/draft/2019-09/meta/hyper-schema", Parse(assemblyPath, "./metaschema/draft2019-09/meta/hyper-schema.json")),
            ("https://json-schema.org/draft/2019-09/meta/meta-data", Parse(assemblyPath, "./metaschema/draft2019-09/meta/meta-data.json")),
            ("https://json-schema.org/draft/2019-09/meta/validation", Parse(assemblyPath, "./metaschema/draft2019-09/meta/validation.json")),
            ("https://json-schema.org/draft/2020-12/schema", Parse(assemblyPath, "./metaschema/draft2020-12/schema.json")),
            ("https://json-schema.org/draft/2020-12/meta/applicator", Parse(assemblyPath, "./metaschema/draft2020-12/meta/applicator.json")),
            ("https://json-schema.org/draft/2020-12/meta/content", Parse(assemblyPath, "./metaschema/draft2020-12/meta/content.json")),
            ("https://json-schema.org/draft/2020-12/meta/core", Parse(assemblyPath, "./metaschema/draft2020-12/meta/core.json")),
            ("https://json-schema.org/draft/2020-12/meta/format-annotation", Parse(assemblyPath, "./metaschema/draft2020-12/meta/format-annotation.json")),
            ("https://json-schema.org/draft/2020-12/meta/format-assertion", Parse(assemblyPath, "./metaschema/draft2020-12/meta/format-assertion.json")),
            ("https://json-schema.org/draft/2020-12/meta/hyper-schema", Parse(assemblyPath, "./metaschema/draft2020-12/meta/hyper-schema.json")),
            ("https://json-schema.org/draft/2020-12/meta/meta-data", Parse(assemblyPath, "./metaschema/draft2020-12/meta/meta-data.json")),
            ("https://json-schema.org/draft/2020-12/meta/unevaluated", Parse(assemblyPath, "./metaschema/draft2020-12/meta/unevaluated.json")),
            ("https://json-schema.org/draft/2020-12/meta/validation", Parse(assemblyPath, "./metaschema/draft2020-12/meta/validation.json")),
            ("https://corvus-oss.org/json-schema/2020-12/schema", Parse(assemblyPath, "./metaschema/corvus/schema.json")),
            ("https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions", Parse(assemblyPath, "./metaschema/corvus/meta/corvus-extensions.json")),
        ];
    }

    private static JsonDocument Parse(string assemblyPath, string relativePath)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, relativePath)));
    }
}