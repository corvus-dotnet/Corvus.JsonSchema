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
public static class Metaschema
{
    // The metaschema files are read from disk once per process. Each resolver still parses its own documents: a
    // CompoundDocumentResolver or PrepopulatedDocumentResolver disposes every document it was given when it is
    // disposed or reset, and this seam is public — the control plane bakes many versions in one process and the
    // playgrounds dispose a resolver per generation — so a shared parsed set would be disposed from under the next
    // caller. Parsing twenty-six small documents is cheap; the disk read was the repeated cost.
    private static readonly Lazy<(string Uri, byte[] Utf8)[]> Documents = new(LoadDocuments);

    /// <summary>
    /// Adds the bundled JSON Schema metaschema documents (draft-04 through 2020-12, plus the
    /// Corvus extensions) to a document resolver so they resolve offline during generation.
    /// </summary>
    /// <param name="documentResolver">The resolver to register the metaschema documents with.</param>
    /// <returns>The same <paramref name="documentResolver"/>, for chaining.</returns>
    public static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        foreach ((string uri, byte[] utf8) in Documents.Value)
        {
            documentResolver.AddDocument(uri, JsonDocument.Parse(utf8));
        }

        return documentResolver;
    }

    private static (string Uri, byte[] Utf8)[] LoadDocuments()
    {
        string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException("Cannot find the executing assembly path.");

        return
        [
            ("http://json-schema.org/draft-04/schema", Read(assemblyPath, "./metaschema/draft4/schema.json")),
            ("http://json-schema.org/draft-06/schema", Read(assemblyPath, "./metaschema/draft6/schema.json")),
            ("http://json-schema.org/draft-07/schema", Read(assemblyPath, "./metaschema/draft7/schema.json")),
            ("https://json-schema.org/draft/2019-09/schema", Read(assemblyPath, "./metaschema/draft2019-09/schema.json")),
            ("https://json-schema.org/draft/2019-09/meta/applicator", Read(assemblyPath, "./metaschema/draft2019-09/meta/applicator.json")),
            ("https://json-schema.org/draft/2019-09/meta/content", Read(assemblyPath, "./metaschema/draft2019-09/meta/content.json")),
            ("https://json-schema.org/draft/2019-09/meta/core", Read(assemblyPath, "./metaschema/draft2019-09/meta/core.json")),
            ("https://json-schema.org/draft/2019-09/meta/format", Read(assemblyPath, "./metaschema/draft2019-09/meta/format.json")),
            ("https://json-schema.org/draft/2019-09/meta/hyper-schema", Read(assemblyPath, "./metaschema/draft2019-09/meta/hyper-schema.json")),
            ("https://json-schema.org/draft/2019-09/meta/meta-data", Read(assemblyPath, "./metaschema/draft2019-09/meta/meta-data.json")),
            ("https://json-schema.org/draft/2019-09/meta/validation", Read(assemblyPath, "./metaschema/draft2019-09/meta/validation.json")),
            ("https://json-schema.org/draft/2020-12/schema", Read(assemblyPath, "./metaschema/draft2020-12/schema.json")),
            ("https://json-schema.org/draft/2020-12/meta/applicator", Read(assemblyPath, "./metaschema/draft2020-12/meta/applicator.json")),
            ("https://json-schema.org/draft/2020-12/meta/content", Read(assemblyPath, "./metaschema/draft2020-12/meta/content.json")),
            ("https://json-schema.org/draft/2020-12/meta/core", Read(assemblyPath, "./metaschema/draft2020-12/meta/core.json")),
            ("https://json-schema.org/draft/2020-12/meta/format-annotation", Read(assemblyPath, "./metaschema/draft2020-12/meta/format-annotation.json")),
            ("https://json-schema.org/draft/2020-12/meta/format-assertion", Read(assemblyPath, "./metaschema/draft2020-12/meta/format-assertion.json")),
            ("https://json-schema.org/draft/2020-12/meta/hyper-schema", Read(assemblyPath, "./metaschema/draft2020-12/meta/hyper-schema.json")),
            ("https://json-schema.org/draft/2020-12/meta/meta-data", Read(assemblyPath, "./metaschema/draft2020-12/meta/meta-data.json")),
            ("https://json-schema.org/draft/2020-12/meta/unevaluated", Read(assemblyPath, "./metaschema/draft2020-12/meta/unevaluated.json")),
            ("https://json-schema.org/draft/2020-12/meta/validation", Read(assemblyPath, "./metaschema/draft2020-12/meta/validation.json")),
            ("https://corvus-oss.org/json-schema/2020-12/schema", Read(assemblyPath, "./metaschema/corvus/schema.json")),
            ("https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions", Read(assemblyPath, "./metaschema/corvus/meta/corvus-extensions.json")),
        ];
    }

    private static byte[] Read(string assemblyPath, string relativePath)
    {
        return File.ReadAllBytes(Path.Combine(assemblyPath, relativePath));
    }
}