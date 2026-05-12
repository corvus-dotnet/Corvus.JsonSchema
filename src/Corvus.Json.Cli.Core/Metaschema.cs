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
    internal static IDocumentResolver AddMetaschema(this IDocumentResolver documentResolver)
    {
        string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException("Cannot find the executing assembly path.");

        documentResolver.AddDocument(
            "http://json-schema.org/draft-04/schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft4/schema.json"))));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-06/schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft6/schema.json"))));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-07/schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft7/schema.json"))));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/schema.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/applicator",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/applicator.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/content",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/content.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/core",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/core.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/format",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/format.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/hyper-schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/hyper-schema.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/meta-data",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/meta-data.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/validation",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2019-09/meta/validation.json"))));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/schema.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/applicator",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/applicator.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/content",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/content.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/core",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/core.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format-annotation",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/format-annotation.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format-assertion",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/format-assertion.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/hyper-schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/hyper-schema.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/meta-data",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/meta-data.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/unevaluated",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/unevaluated.json"))));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/validation",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/draft2020-12/meta/validation.json"))));

        documentResolver.AddDocument(
            "https://corvus-oss.org/json-schema/2020-12/schema",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/corvus/schema.json"))));
        documentResolver.AddDocument(
            "https://corvus-oss.org/json-schema/2020-12/meta/corvus-extensions",
            JsonDocument.Parse(File.ReadAllText(Path.Combine(assemblyPath, "./metaschema/corvus/meta/corvus-extensions.json"))));

        return documentResolver;
    }
}