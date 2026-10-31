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

        // The settings the generated program loads with, so that its patterns are read here as they will be read
        // there, and the regular expressions listed below are the ones its evaluator compiles.
        if (source.RegexEngine == GeneratedRegexEngine.Ecma262)
        {
#if STJ
            options.RegexEdition = source.RegexEdition is GeneratedRegexEdition edition ? (Corvus.Ecma262.RegularExpressions.CodeGeneration.RegexEdition)(int)edition : null;
            options.RegexUnicodeVersion = source.RegexUnicodeVersion == GeneratedRegexUnicodeVersion.Default ? null : (Corvus.Ecma262.RegularExpressions.CodeGeneration.RegexUnicodeVersion)(int)source.RegexUnicodeVersion;
#else
            options.RegexEdition = source.RegexEdition is GeneratedRegexEdition edition ? (Corvus.Ecma262.RegularExpressions.RegexEdition)(int)edition : null;
            options.RegexUnicodeVersion = source.RegexUnicodeVersion == GeneratedRegexUnicodeVersion.Default ? null : (Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion)(int)source.RegexUnicodeVersion;
#endif
        }

        using JsonSchemaEvaluator root = JsonSchemaEvaluator.CompileFromUri(source.RootDocumentKey, options);
        root.RegisterEntryPoints(source.EntryPoints);

        byte[] image = root.ToProgramImage();
        if (!emitRegexTable)
        {
            return new SchemaProgramImage(image, []);
        }

        IReadOnlyList<string> patterns = JsonSchemaEvaluator.GetImagePatterns(image);
#if !STJ
        if (source.RegexEngine == GeneratedRegexEngine.Ecma262)
        {
            // A compiled matcher for each pattern the evaluator matches with Corvus.Regex.Ecma262, which then compiles
            // none of them.
            var regularExpressions = new List<SchemaProgramRegularExpression>();
            foreach (JsonSchemaImageRegularExpression regex in JsonSchemaEvaluator.GetImageRegularExpressions(image, options))
            {
                regularExpressions.Add(new SchemaProgramRegularExpression(
                    regex.Index,
                    regex.Pattern,
                    (regex.Flags & Corvus.Ecma262.RegularExpressions.RegexFlags.Unicode) != 0,
                    (GeneratedRegexEdition)(int)regex.Edition,
                    (GeneratedRegexUnicodeVersion)(int)regex.UnicodeVersion));
            }

            return new SchemaProgramImage(image, [], patterns, regularExpressions);
        }
#else
        if (source.RegexEngine == GeneratedRegexEngine.Ecma262)
        {
            // The source generator's build cannot load the image with the library, so its own matchers say which
            // patterns need a regular expression; it decides as the library's build does.
            var regularExpressions = new List<SchemaProgramRegularExpression>();
            foreach ((int index, string pattern, bool unicode, int edition, int version) in root.GetProgramRegularExpressions(patterns, options))
            {
                regularExpressions.Add(new SchemaProgramRegularExpression(index, pattern, unicode, (GeneratedRegexEdition)edition, (GeneratedRegexUnicodeVersion)version));
            }

            return new SchemaProgramImage(image, [], patterns, regularExpressions);
        }

#endif
        var dotNetPatterns = new string[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            dotNetPatterns[i] = JsonSchemaEvaluator.ToDotNetPattern(patterns[i]);
        }

        return new SchemaProgramImage(image, dotNetPatterns, patterns);
    }
}