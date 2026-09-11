// <copyright file="Metaschemas.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// Provides the standard JSON Schema metaschemas as embedded resources.
/// </summary>
internal static class Metaschemas
{
    private static readonly Dictionary<string, string> ResourceNames = new(StringComparer.Ordinal)
    {
        ["http://json-schema.org/draft-04/schema"] = "metaschema.draft4.schema.json",
        ["http://json-schema.org/draft-06/schema"] = "metaschema.draft6.schema.json",
        ["http://json-schema.org/draft-07/schema"] = "metaschema.draft7.schema.json",
        ["https://json-schema.org/draft/2019-09/schema"] = "metaschema.draft2019-09.schema.json",
        ["https://json-schema.org/draft/2019-09/meta/applicator"] = "metaschema.draft2019-09.meta.applicator.json",
        ["https://json-schema.org/draft/2019-09/meta/content"] = "metaschema.draft2019-09.meta.content.json",
        ["https://json-schema.org/draft/2019-09/meta/core"] = "metaschema.draft2019-09.meta.core.json",
        ["https://json-schema.org/draft/2019-09/meta/format"] = "metaschema.draft2019-09.meta.format.json",
        ["https://json-schema.org/draft/2019-09/meta/hyper-schema"] = "metaschema.draft2019-09.meta.hyper-schema.json",
        ["https://json-schema.org/draft/2019-09/meta/meta-data"] = "metaschema.draft2019-09.meta.meta-data.json",
        ["https://json-schema.org/draft/2019-09/meta/validation"] = "metaschema.draft2019-09.meta.validation.json",
        ["https://json-schema.org/draft/2020-12/schema"] = "metaschema.draft2020-12.schema.json",
        ["https://json-schema.org/draft/2020-12/meta/applicator"] = "metaschema.draft2020-12.meta.applicator.json",
        ["https://json-schema.org/draft/2020-12/meta/content"] = "metaschema.draft2020-12.meta.content.json",
        ["https://json-schema.org/draft/2020-12/meta/core"] = "metaschema.draft2020-12.meta.core.json",
        ["https://json-schema.org/draft/2020-12/meta/format-annotation"] = "metaschema.draft2020-12.meta.format-annotation.json",
        ["https://json-schema.org/draft/2020-12/meta/format-assertion"] = "metaschema.draft2020-12.meta.format-assertion.json",
        ["https://json-schema.org/draft/2020-12/meta/hyper-schema"] = "metaschema.draft2020-12.meta.hyper-schema.json",
        ["https://json-schema.org/draft/2020-12/meta/meta-data"] = "metaschema.draft2020-12.meta.meta-data.json",
        ["https://json-schema.org/draft/2020-12/meta/unevaluated"] = "metaschema.draft2020-12.meta.unevaluated.json",
        ["https://json-schema.org/draft/2020-12/meta/validation"] = "metaschema.draft2020-12.meta.validation.json",
    };

    private static readonly Dictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Tries to get the well-known dialect for a metaschema URI.
    /// </summary>
    public static bool TryGetDialect(string uri, out JsonSchemaDialect dialect)
    {
        switch (uri)
        {
            case "http://json-schema.org/draft-04/schema":
                dialect = JsonSchemaDialect.Draft4;
                return true;
            case "http://json-schema.org/draft-06/schema":
                dialect = JsonSchemaDialect.Draft6;
                return true;
            case "http://json-schema.org/draft-07/schema":
                dialect = JsonSchemaDialect.Draft7;
                return true;
            case "https://json-schema.org/draft/2019-09/schema":
                dialect = JsonSchemaDialect.Draft201909;
                return true;
            case "https://json-schema.org/draft/2020-12/schema":
                dialect = JsonSchemaDialect.Draft202012;
                return true;
            default:
                dialect = default;
                return false;
        }
    }

    /// <summary>
    /// Tries to get the UTF-8 JSON for a standard metaschema.
    /// </summary>
    public static bool TryGet(string uri, out ReadOnlyMemory<byte> utf8Json)
    {
        if (!ResourceNames.TryGetValue(uri, out string? resourceName))
        {
            utf8Json = default;
            return false;
        }

        lock (Cache)
        {
            if (!Cache.TryGetValue(uri, out byte[]? bytes))
            {
                using Stream stream = typeof(Metaschemas).Assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                bytes = ms.ToArray();
                Cache[uri] = bytes;
            }

            utf8Json = bytes;
            return true;
        }
    }
}