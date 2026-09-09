// <copyright file="JsonSchemaDialect.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// The JSON Schema dialects supported by the runtime evaluator.
/// </summary>
public enum JsonSchemaDialect
{
    /// <summary>Draft 4 (http://json-schema.org/draft-04/schema#).</summary>
    Draft4,

    /// <summary>Draft 6 (http://json-schema.org/draft-06/schema#).</summary>
    Draft6,

    /// <summary>Draft 7 (http://json-schema.org/draft-07/schema#).</summary>
    Draft7,

    /// <summary>Draft 2019-09 (https://json-schema.org/draft/2019-09/schema).</summary>
    Draft201909,

    /// <summary>Draft 2020-12 (https://json-schema.org/draft/2020-12/schema).</summary>
    Draft202012,
}

/// <summary>
/// The vocabularies that may be enabled for a schema resource.
/// </summary>
[Flags]
public enum JsonSchemaVocabularies
{
    /// <summary>No vocabularies.</summary>
    None = 0,

    /// <summary>The core vocabulary.</summary>
    Core = 1 << 0,

    /// <summary>The applicator vocabulary.</summary>
    Applicator = 1 << 1,

    /// <summary>The validation vocabulary.</summary>
    Validation = 1 << 2,

    /// <summary>The meta-data vocabulary.</summary>
    MetaData = 1 << 3,

    /// <summary>The format vocabulary in annotation mode.</summary>
    FormatAnnotation = 1 << 4,

    /// <summary>The format vocabulary in assertion mode.</summary>
    FormatAssertion = 1 << 5,

    /// <summary>The content vocabulary.</summary>
    Content = 1 << 6,

    /// <summary>The unevaluated vocabulary.</summary>
    Unevaluated = 1 << 7,

    /// <summary>All vocabularies, with format as annotation.</summary>
    AllAnnotatingFormat = Core | Applicator | Validation | MetaData | FormatAnnotation | Content | Unevaluated,
}
