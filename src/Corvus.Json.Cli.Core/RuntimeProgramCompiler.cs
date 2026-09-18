// <copyright file="RuntimeProgramCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Compiles a schema program ahead of time with the runtime evaluator, so the generated program carries a
/// pre-compiled image and its regular-expression table instead of the schema documents.
/// </summary>
public static class RuntimeProgramCompiler
{
    /// <summary>
    /// Compiles the program exactly as the emitted code would at first use, registers every entry point, and
    /// serialises the result.
    /// </summary>
    /// <param name="source">The program's documents, entry points and options.</param>
    /// <returns>The image and its patterns in .NET syntax.</returns>
    public static SchemaProgramImage Compile(SchemaProgramSource source)
    {
        return Compile(source, emitRegexTable: true);
    }

    /// <summary>
    /// Compiles the program for a Roslyn source generator: the image without the regular-expression table. Roslyn
    /// does not chain generators, so <c>[GeneratedRegex]</c> methods in generated code would never be implemented;
    /// the evaluator constructs the expressions from the image's pattern table on first use instead.
    /// </summary>
    /// <param name="source">The program's documents, entry points and options.</param>
    /// <returns>The image, with no patterns.</returns>
    public static SchemaProgramImage CompileWithoutRegexTable(SchemaProgramSource source)
    {
        return Compile(source, emitRegexTable: false);
    }

    private static SchemaProgramImage Compile(SchemaProgramSource source, bool emitRegexTable)
    {
        // The documents' UTF-8 text goes to the evaluator as it is (for a source created from strings it is encoded once).
        var documents = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, ReadOnlyMemory<byte>> document in source.Utf8Documents)
        {
            documents[document.Key] = document.Value;
        }

        Dictionary<string, JsonSchemaFormatMode>? formatModes = null;
        if (source.FormatModes.Count > 0)
        {
            formatModes = new Dictionary<string, JsonSchemaFormatMode>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> mode in source.FormatModes)
            {
                formatModes[mode.Key] = (JsonSchemaFormatMode)Enum.Parse(typeof(JsonSchemaFormatMode), mode.Value);
            }
        }

        var options = new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = (JsonSchemaDialect)Enum.Parse(typeof(JsonSchemaDialect), source.Dialect),
            AssertFormat = source.AlwaysAssertFormat ? true : null,
            AssertFormatInLegacyDrafts = true,
            FormatModes = formatModes,
            CompileRegularExpressions = false,
            DocumentResolver = (string uri, out ReadOnlyMemory<byte> utf8Json) =>
            {
                if (documents.TryGetValue(uri, out ReadOnlyMemory<byte> bytes))
                {
                    utf8Json = bytes;
                    return true;
                }

                utf8Json = default;
                return false;
            },
        };

        using JsonSchemaEvaluator root = JsonSchemaEvaluator.CompileFromUri(source.RootDocumentKey, options);
        root.RegisterEntryPoints(source.EntryPoints);

        byte[] image = root.ToProgramImage();
        if (!emitRegexTable)
        {
            return new SchemaProgramImage(image, []);
        }

        IReadOnlyList<string> patterns = JsonSchemaEvaluator.GetImagePatterns(image);
        var dotNetPatterns = new string[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            dotNetPatterns[i] = JsonSchemaEvaluator.ToDotNetPattern(patterns[i]);
        }

        return new SchemaProgramImage(image, dotNetPatterns);
    }
}