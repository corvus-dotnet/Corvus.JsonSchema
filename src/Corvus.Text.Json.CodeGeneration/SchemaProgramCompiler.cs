// <copyright file="SchemaProgramCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Compiles a schema program ahead of time, so that the emitted program carries a pre-compiled image instead of the
/// schema documents. The generator library carries no dependency on the runtime evaluator; a host that has one (the
/// CLI) supplies this through <see cref="CSharpLanguageProvider.Options"/>, and a host that does not emits the
/// documents and compiles at first use.
/// </summary>
/// <param name="source">What the program would compile at first use.</param>
/// <returns>The image and its pattern table.</returns>
public delegate SchemaProgramImage SchemaProgramCompiler(SchemaProgramSource source);

/// <summary>The inputs to a schema program: exactly what the emitted program would compile at first use.</summary>
public sealed class SchemaProgramSource
{
    /// <summary>Initializes a new instance of the <see cref="SchemaProgramSource"/> class.</summary>
    /// <param name="documents">The schema documents, keyed as references resolve them.</param>
    /// <param name="rootDocumentKey">The key of the document compiled first.</param>
    /// <param name="entryPoints">The entry point references (<c>key#pointer</c>), in index order.</param>
    /// <param name="dialect">The default dialect, as a <c>JsonSchemaDialect</c> member name.</param>
    /// <param name="alwaysAssertFormat">Whether <c>format</c> is asserted regardless of dialect.</param>
    /// <param name="formatModes">Per-format assertion modes, as <c>JsonSchemaFormatMode</c> member names.</param>
    public SchemaProgramSource(IReadOnlyList<KeyValuePair<string, string>> documents, string rootDocumentKey, IReadOnlyList<string> entryPoints, string dialect, bool alwaysAssertFormat, IReadOnlyList<KeyValuePair<string, string>> formatModes)
    {
        this.Documents = documents;
        this.RootDocumentKey = rootDocumentKey;
        this.EntryPoints = entryPoints;
        this.Dialect = dialect;
        this.AlwaysAssertFormat = alwaysAssertFormat;
        this.FormatModes = formatModes;
    }

    /// <summary>Gets the schema documents, keyed as references resolve them.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Documents { get; }

    /// <summary>Gets the key of the document compiled first.</summary>
    public string RootDocumentKey { get; }

    /// <summary>Gets the entry point references, in index order.</summary>
    public IReadOnlyList<string> EntryPoints { get; }

    /// <summary>Gets the default dialect, as a <c>JsonSchemaDialect</c> member name.</summary>
    public string Dialect { get; }

    /// <summary>Gets a value indicating whether <c>format</c> is asserted regardless of dialect.</summary>
    public bool AlwaysAssertFormat { get; }

    /// <summary>Gets the per-format assertion modes, as <c>JsonSchemaFormatMode</c> member names.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> FormatModes { get; }
}

/// <summary>A compiled schema program image and the regular expressions it needs.</summary>
public sealed class SchemaProgramImage
{
    /// <summary>Initializes a new instance of the <see cref="SchemaProgramImage"/> class.</summary>
    /// <param name="image">The image bytes.</param>
    /// <param name="dotNetPatterns">The regular-expression patterns the image needs, translated to .NET syntax, in
    /// the index order the evaluator asks for them.</param>
    public SchemaProgramImage(byte[] image, IReadOnlyList<string> dotNetPatterns)
    {
        this.Image = image;
        this.DotNetPatterns = dotNetPatterns;
    }

    /// <summary>Gets the image bytes.</summary>
    public byte[] Image { get; }

    /// <summary>Gets the regular-expression patterns in .NET syntax, in pattern-table order.</summary>
    public IReadOnlyList<string> DotNetPatterns { get; }
}