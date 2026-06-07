// <copyright file="ComparandKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The kind of a resolved <see cref="Comparand"/>.
/// </summary>
internal enum ComparandKind
{
    /// <summary>The operand could not be resolved.</summary>
    Undefined,

    /// <summary>A JSON null.</summary>
    Null,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A string.</summary>
    String,

    /// <summary>An opaque JSON object or array, compared only for equality by canonical text.</summary>
    Json,
}