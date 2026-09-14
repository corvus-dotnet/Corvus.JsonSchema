// <copyright file="RuntimeProgramGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Emits the schema evaluation program shared by every generated type in a compilation: the schema documents
/// as UTF-8 static data, the entry points (one per generated type and per standalone evaluator) and the
/// <c>Corvus.Text.Json.RuntimeEvaluator</c> setup that compiles them on first use. Generated types validate by
/// calling into this program, so the validation logic itself is no longer emitted per type.
/// </summary>
internal static class RuntimeProgramGenerator
{
    /// <summary>The synthetic scheme under which file-system documents are keyed, so that no build-machine path is emitted.</summary>
    private const string SyntheticScheme = "corvus-schema:///";

    /// <summary>
    /// A document known to the program: the key it is registered under and its JSON text.
    /// </summary>
    /// <param name="key">The absolute URI the document is registered under.</param>
    /// <param name="json">The document text.</param>
    public sealed class SchemaDocumentSource
    {
        private string? json;
        private ReadOnlyMemory<byte>? utf8Json;

        /// <summary>Initializes a new instance of the <see cref="SchemaDocumentSource"/> class from the document text.</summary>
        /// <param name="key">The absolute URI the document is registered under.</param>
        /// <param name="json">The document text.</param>
        public SchemaDocumentSource(string key, string json)
        {
            this.Key = key;
            this.json = json;
        }

        /// <summary>Initializes a new instance of the <see cref="SchemaDocumentSource"/> class from the document's UTF-8 text.</summary>
        /// <param name="key">The absolute URI the document is registered under.</param>
        /// <param name="utf8Json">The document's UTF-8 text.</param>
        public SchemaDocumentSource(string key, ReadOnlyMemory<byte> utf8Json)
        {
            this.Key = key;
            this.utf8Json = utf8Json;
        }

        /// <summary>Gets the absolute URI the document is registered under.</summary>
        public string Key { get; }

        /// <summary>Gets the document text (decoded on first use for a UTF-8 source).</summary>
        public string Json => this.json ??= System.Runtime.InteropServices.MemoryMarshal.TryGetArray(this.utf8Json!.Value, out ArraySegment<byte> segment)
            ? Encoding.UTF8.GetString(segment.Array!, segment.Offset, segment.Count)
            : Encoding.UTF8.GetString(this.utf8Json!.Value.ToArray());

        /// <summary>Gets the document's UTF-8 text (encoded on first use for a text source).</summary>
        public ReadOnlyMemory<byte> Utf8Json => this.utf8Json ??= Encoding.UTF8.GetBytes(this.json!);
    }

    /// <summary>
    /// Maps every root document URI seen at generation time to the key it will have in the emitted program.
    /// File documents (a <c>file:</c> URI or a bare path) are keyed under the synthetic <c>corvus-schema:///</c>
    /// scheme by their path relative to the common directory of all file documents, so that the relative,
    /// path-absolute and <c>file:</c> reference forms the type builder produces all resolve to the same key, relative
    /// references between documents still resolve (every referenced file document lies under that directory), and no
    /// build-machine path reaches the generated code. Every other absolute URI (an absolute <c>$id</c>) is kept
    /// verbatim, since relative references resolve against it. An empty trailing fragment is dropped.
    /// </summary>
    /// <param name="rootDocumentUris">The root document URIs.</param>
    /// <returns>The mapping from root document URI to program key.</returns>
    public static IReadOnlyDictionary<string, string> MapDocumentKeys(IEnumerable<string> rootDocumentUris)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        List<(string Uri, string Path)> files = [];
        foreach (string uri in rootDocumentUris.Distinct(StringComparer.Ordinal))
        {
            if (TryGetFilePath(uri, out string? path))
            {
                files.Add((uri, path!));
            }
            else
            {
                result[uri] = StripEmptyFragment(uri);
            }
        }

        // Only rooted paths carry a build-machine prefix; a bare relative path (an in-memory document's synthetic
        // name, say) is already machine-independent and is kept whole.
        string common = CommonDirectory(files.Where(f => IsBarePath(f.Path)).Select(f => f.Path));
        foreach ((string uri, string path) in files)
        {
            result[uri] = SyntheticScheme + (IsBarePath(path) ? path.Substring(common.Length) : path).TrimStart('/');
        }

        return result;
    }

    /// <summary>
    /// Rewrites a reference-only document (the wrapper the type builder synthesises for a schema addressed by a
    /// fragment, <c>{"$ref": "&lt;document&gt;#&lt;pointer&gt;"}</c>) so that it names the document by its program
    /// key rather than by the build-machine path the builder resolved. Any other document is returned unchanged.
    /// </summary>
    /// <param name="json">The document text.</param>
    /// <param name="keys">The mapping from root document URI to program key.</param>
    /// <returns>The document text to emit.</returns>
    public static string MapReferenceDocument(string json, IReadOnlyDictionary<string, string> keys)
    {
        string? reference = TryGetSoleReference(json);
        if (reference is null)
        {
            return json;
        }

        int hash = reference.IndexOf('#');
        string document = hash < 0 ? reference : reference.Substring(0, hash);
        string fragment = hash < 0 ? string.Empty : reference.Substring(hash);
        if (!TryMapDocument(document, keys, out string? key) || key == document)
        {
            return json;
        }

        return "{\"$ref\": " + System.Text.Json.JsonSerializer.Serialize(key + fragment) + "}";
    }

    /// <summary>
    /// Re-keys a document that is only a <c>$ref</c> to another document, as <see cref="MapReferenceDocument(string, IReadOnlyDictionary{string, string})"/>
    /// does, working on the document's UTF-8 text.
    /// </summary>
    /// <param name="utf8Json">The document's UTF-8 text.</param>
    /// <param name="keys">The document keys.</param>
    /// <returns>The document, or a re-keyed sole reference.</returns>
    public static ReadOnlyMemory<byte> MapReferenceDocument(ReadOnlyMemory<byte> utf8Json, IReadOnlyDictionary<string, string> keys)
    {
        string? reference = TryGetSoleReference(utf8Json.Span);
        if (reference is null)
        {
            return utf8Json;
        }

        int hash = reference.IndexOf('#');
        string document = hash < 0 ? reference : reference.Substring(0, hash);
        string fragment = hash < 0 ? string.Empty : reference.Substring(hash);
        if (!TryMapDocument(document, keys, out string? key) || key == document)
        {
            return utf8Json;
        }

        return Encoding.UTF8.GetBytes("{\"$ref\": " + System.Text.Json.JsonSerializer.Serialize(key + fragment) + "}");
    }

    /// <summary>
    /// The UTF-8 form of <see cref="TryGetSoleReference(string)"/>: the reference if the document is an object with a single
    /// property, <c>$ref</c>, whose value is a string. A reader stops at the second property instead of parsing the whole
    /// document; a document with one property is still read to its end, so an invalid one gives <see langword="null"/> as the parse did.
    /// </summary>
    private static string? TryGetSoleReference(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            System.Text.Json.Utf8JsonReader reader = new(utf8Json);
            if (!reader.Read() || reader.TokenType != System.Text.Json.JsonTokenType.StartObject ||
                !reader.Read() || reader.TokenType != System.Text.Json.JsonTokenType.PropertyName)
            {
                return null;
            }

            bool isReference = reader.ValueTextEquals("$ref"u8);
            if (!reader.Read())
            {
                return null;
            }

            string? reference = isReference && reader.TokenType == System.Text.Json.JsonTokenType.String ? reader.GetString() : null;
            reader.Skip();
            if (!reader.Read() || reader.TokenType != System.Text.Json.JsonTokenType.EndObject)
            {
                return null;
            }

            return reader.Read() ? null : reference;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? TryGetSoleReference(string json)
    {
        // A document with a second top-level property is never a sole reference. Schema documents almost
        // always show that within their first property, so detect it by scanning a few tokens rather than
        // parsing the whole document again (this runs for every schema document of every generation).
        if (HasSecondTopLevelProperty(json))
        {
            return null;
        }

        try
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
            System.Text.Json.JsonElement root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return null;
            }

            string? reference = null;
            int count = 0;
            foreach (System.Text.Json.JsonProperty property in root.EnumerateObject())
            {
                count++;
                if (property.Name == "$ref" && property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    reference = property.Value.GetString();
                }
            }

            return count == 1 ? reference : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Determines, without parsing the whole document, whether a JSON document is an object whose first
    /// property (with a string or literal value) is followed by another property.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> whenever it cannot tell (a nested first value, anything unexpected), leaving
    /// the answer to the full parse. It returns <see langword="true"/> only for <c>{ "name": value ,</c>, which a
    /// valid document can only continue with a second property, so <see cref="TryGetSoleReference(string)"/> returns
    /// <see langword="null"/> exactly as the full parse would (and an invalid document is <see langword="null"/> either way).
    /// </remarks>
    private static bool HasSecondTopLevelProperty(string json)
    {
        int i = SkipWhiteSpace(json, 0);
        if (i >= json.Length || json[i] != '{')
        {
            return false;
        }

        i = SkipWhiteSpace(json, i + 1);
        if (i >= json.Length || json[i] != '"' || (i = SkipString(json, i)) < 0)
        {
            return false;
        }

        i = SkipWhiteSpace(json, i);
        if (i >= json.Length || json[i] != ':')
        {
            return false;
        }

        i = SkipWhiteSpace(json, i + 1);
        if (i >= json.Length || json[i] == '{' || json[i] == '[')
        {
            return false;
        }

        if (json[i] == '"')
        {
            if ((i = SkipString(json, i)) < 0)
            {
                return false;
            }
        }
        else
        {
            while (i < json.Length && json[i] != ',' && json[i] != '}' && !IsJsonWhiteSpace(json[i]))
            {
                i++;
            }
        }

        i = SkipWhiteSpace(json, i);
        return i < json.Length && json[i] == ',';

        static int SkipWhiteSpace(string text, int index)
        {
            while (index < text.Length && IsJsonWhiteSpace(text[index]))
            {
                index++;
            }

            return index;
        }

        static bool IsJsonWhiteSpace(char c) => c is ' ' or '\t' or '\n' or '\r';

        // Returns the index after the closing quote of the string starting at index, or -1.
        static int SkipString(string text, int index)
        {
            for (int j = index + 1; j < text.Length; j++)
            {
                if (text[j] == '\\')
                {
                    j++;
                }
                else if (text[j] == '"')
                {
                    return j + 1;
                }
            }

            return -1;
        }
    }

    private static bool TryMapDocument(string document, IReadOnlyDictionary<string, string> keys, out string? key)
    {
        if (keys.TryGetValue(document, out key))
        {
            return true;
        }

        if (TryGetFilePath(document, out string? path))
        {
            foreach (KeyValuePair<string, string> candidate in keys)
            {
                if (TryGetFilePath(candidate.Key, out string? candidatePath) && candidatePath == path)
                {
                    key = candidate.Value;
                    return true;
                }
            }
        }

        key = null;
        return false;
    }

    /// <summary>
    /// The longest directory prefix (ending in a separator, or empty) shared by every path.
    /// </summary>
    private static string CommonDirectory(IEnumerable<string> paths)
    {
        string? common = null;
        foreach (string path in paths)
        {
            int slash = path.LastIndexOf('/');
            string directory = slash < 0 ? string.Empty : path.Substring(0, slash + 1);
            if (common is null)
            {
                common = directory;
                continue;
            }

            int length = 0;
            int limit = Math.Min(common.Length, directory.Length);
            for (int i = 0; i < limit && common[i] == directory[i]; i++)
            {
                if (common[i] == '/')
                {
                    length = i + 1;
                }
            }

            common = common.Substring(0, length);
        }

        return common ?? string.Empty;
    }

    /// <summary>
    /// Emits the program class.
    /// </summary>
    /// <param name="ns">The namespace for the program class.</param>
    /// <param name="className">The program class name.</param>
    /// <param name="documents">The documents, keyed as they will be resolved.</param>
    /// <param name="rootDocumentKey">The key of the document compiled first; entry points resolve against it.</param>
    /// <param name="entryPoints">The entry point references, in index order.</param>
    /// <param name="dialect">The <c>JsonSchemaDialect</c> member name applied to documents without <c>$schema</c>.</param>
    /// <param name="alwaysAssertFormat">Whether <c>format</c> is asserted regardless of dialect.</param>
    /// <param name="fileExtension">The generated file extension.</param>
    /// <param name="lineEnd">The line ending sequence.</param>
    /// <param name="image">The program compiled ahead of time, or <see langword="null"/> to embed the documents and
    /// compile at first use.</param>
    /// <returns>The generated file.</returns>
    public static GeneratedCodeFile Generate(
        string ns,
        string className,
        IReadOnlyList<SchemaDocumentSource> documents,
        string rootDocumentKey,
        IReadOnlyList<string> entryPoints,
        string dialect,
        bool alwaysAssertFormat,
        IReadOnlyList<KeyValuePair<string, string>> formatModes,
        string fileExtension,
        string lineEnd,
        SchemaProgramImage? image = null)
    {
        var sb = new StringBuilder();
        void Line(string text = "")
        {
            sb.Append(text).Append(lineEnd);
        }

        Line("//------------------------------------------------------------------------------");
        Line("// <auto-generated>");
        Line("//     This code was generated by a tool.");
        Line("//");
        Line("//     Changes to this file may cause incorrect behavior and will be lost if");
        Line("//     the code is regenerated.");
        Line("// </auto-generated>");
        Line("//------------------------------------------------------------------------------");
        Line("#nullable enable");
        Line("#pragma warning disable");
        Line();
        Line("using System;");
        if (image is not null)
        {
            Line("using System.Buffers.Text;");
            Line("using System.Text.RegularExpressions;");
        }

        Line("using Corvus.Text.Json;");
        Line("using Corvus.Text.Json.RuntimeEvaluator;");
        Line();
        if (ns.Length > 0)
        {
            Line($"namespace {ns};");
            Line();
        }

        Line("/// <summary>");
        Line("/// The JSON Schema evaluation program for the generated types in this assembly. Generated types validate by");
        if (image is null)
        {
            Line("/// evaluating against an entry point of this program, which is compiled from the embedded schema documents");
            Line("/// on first use.");
        }
        else
        {
            Line("/// evaluating against an entry point of this program, which was compiled when the code was generated and is");
            Line("/// loaded from its image on first use.");
        }

        Line("/// </summary>");
        Line("[global::System.CodeDom.Compiler.GeneratedCode(\"Corvus.Text.Json.CodeGeneration\", \"1.0\")]");
        Line(image is null ? $"internal static class {className}" : $"internal static partial class {className}");
        Line("{");
        Line("    private static readonly object Gate = new();");
        Line($"    private static readonly JsonSchemaEvaluator?[] Evaluators = new JsonSchemaEvaluator?[{entryPoints.Count}];");
        Line("    private static JsonSchemaEvaluator? root;");
        Line();
        Line("    /// <summary>");
        Line("    /// Gets the evaluator for an entry point, compiling it on first use. Entry points share one compiled");
        Line("    /// program, so each subschema is compiled once however many entry points reach it.");
        Line("    /// </summary>");
        Line("    /// <param name=\"index\">The entry point index.</param>");
        Line("    /// <returns>The evaluator rooted at that entry point.</returns>");
        Line("    internal static JsonSchemaEvaluator Entry(int index)");
        Line("    {");
        Line("        return Evaluators[index] ?? Create(index);");
        Line("    }");
        Line();
        Line("    private static JsonSchemaEvaluator Create(int index)");
        Line("    {");
        Line("        lock (Gate)");
        Line("        {");
        Line("            if (Evaluators[index] is JsonSchemaEvaluator existing)");
        Line("            {");
        Line("                return existing;");
        Line("            }");
        Line();
        Line(image is null
            ? "            root ??= JsonSchemaEvaluator.CompileFromUri(RootDocument, CreateOptions());"
            : "            root ??= JsonSchemaEvaluator.FromProgramImage(LoadImage(), CreateOptions());");
        Line("            JsonSchemaEvaluator evaluator = root.ForEntryPoint(EntryPoints[index]);");
        Line("            Evaluators[index] = evaluator;");
        Line("            return evaluator;");
        Line("        }");
        Line("    }");
        Line();
        Line("    private static JsonSchemaEvaluatorOptions CreateOptions()");
        Line("    {");
        Line("        return new JsonSchemaEvaluatorOptions");
        Line("        {");
        Line($"            DefaultDialect = JsonSchemaDialect.{dialect},");
        Line($"            AssertFormat = {(alwaysAssertFormat ? "true" : "null")},");
        Line("            AssertFormatInLegacyDrafts = true,");
        Line(image is null ? "            DocumentResolver = TryGetDocument," : "            RegexProvider = GetRegex,");
        if (formatModes.Count > 0)
        {
            Line("            FormatModes = new global::System.Collections.Generic.Dictionary<string, JsonSchemaFormatMode>(global::System.StringComparer.Ordinal)");
            Line("            {");
            foreach (KeyValuePair<string, string> mode in formatModes)
            {
                Line($"                [{Literal(mode.Key)}] = JsonSchemaFormatMode.{mode.Value},");
            }

            Line("            },");
        }

        Line("        };");
        Line("    }");
        Line();
        if (image is null)
        {
            Line($"    private const string RootDocument = {Literal(rootDocumentKey)};");
            Line();
        }

        Line("    private static readonly string[] EntryPoints =");
        Line("    [");
        for (int i = 0; i < entryPoints.Count; i++)
        {
            Line($"        {Literal(entryPoints[i])},");
        }

        Line("    ];");
        Line();
        if (image is not null)
        {
            EmitImage(Line, image);
            Line("}");
            return new GeneratedCodeFile(className + fileExtension, sb.ToString());
        }

        Line("    private static bool TryGetDocument(string uri, out ReadOnlyMemory<byte> utf8Json)");
        Line("    {");
        Line("        switch (uri)");
        Line("        {");
        for (int i = 0; i < documents.Count; i++)
        {
            Line($"            case {Literal(documents[i].Key)}:");
            Line($"                utf8Json = Document{i}.ToArray();");
            Line("                return true;");
        }

        Line("            default:");
        Line("                utf8Json = default;");
        Line("                return false;");
        Line("        }");
        Line("    }");

        // Documents are span properties over static data, so they need no initialisation and cannot be
        // observed before the evaluators are created (static field initialisers run in textual order).
        for (int i = 0; i < documents.Count; i++)
        {
            Line();
            Line($"    // {documents[i].Key}");
            Line($"    private static ReadOnlySpan<byte> Document{i} => {Literal(documents[i].Json)}u8;");
        }

        Line("}");

        return new GeneratedCodeFile(className + fileExtension, sb.ToString());
    }

    /// <summary>
    /// Emits the image (base64 in UTF-8 literals, decoded on first use) and the regular-expression table: one
    /// <c>[GeneratedRegex]</c> method per pattern, in the image's pattern-table order, on runtimes that have the
    /// generator; elsewhere the provider returns <see langword="null"/> and the evaluator constructs the expression.
    /// A program from a Roslyn source generator carries no patterns (generators do not chain, so the regex generator
    /// would never implement the methods) and the evaluator constructs every expression on first use.
    /// </summary>
    private static void EmitImage(Action<string> line, SchemaProgramImage image)
    {
        line("    private static byte[] LoadImage()");
        line("    {");
        line("        ReadOnlySpan<byte> encoded = ImageBase64;");
        line("        byte[] buffer = new byte[Base64.GetMaxDecodedFromUtf8Length(encoded.Length)];");
        line("        Base64.DecodeFromUtf8(encoded, buffer, out _, out int written);");
        line("        return written == buffer.Length ? buffer : buffer.AsSpan(0, written).ToArray();");
        line("    }");
        line(string.Empty);
        line("    private static Regex? GetRegex(int index, string pattern)");
        line("    {");
        if (image.DotNetPatterns.Count > 0)
        {
            line("#if NET8_0_OR_GREATER && !DYNAMIC_BUILD");
            line("        switch (index)");
            line("        {");
            for (int i = 0; i < image.DotNetPatterns.Count; i++)
            {
                line($"            case {i}:");
                line($"                return Regex{i}();");
            }

            line("            default:");
            line("                return null;");
            line("        }");
            line("#else");
            line("        return null;");
            line("#endif");
        }
        else
        {
            line("        return null;");
        }

        line("    }");
        if (image.DotNetPatterns.Count > 0)
        {
            line(string.Empty);
            line("#if NET8_0_OR_GREATER && !DYNAMIC_BUILD");
            for (int i = 0; i < image.DotNetPatterns.Count; i++)
            {
                line($"    [GeneratedRegex({Literal(image.DotNetPatterns[i])}, RegexOptions.CultureInvariant)]");
                line($"    private static partial Regex Regex{i}();");
            }

            line("#endif");
        }

        line(string.Empty);
        string base64 = Convert.ToBase64String(image.Image);
        line($"    // Program image: {image.Image.Length} bytes.");
        line("    private static ReadOnlySpan<byte> ImageBase64 =>");
        const int chunk = 120;
        if (base64.Length == 0)
        {
            line("        \"\"u8;");
        }

        for (int i = 0; i < base64.Length; i += chunk)
        {
            string part = base64.Substring(i, Math.Min(chunk, base64.Length - i));
            bool last = i + chunk >= base64.Length;
            line($"        \"{part}\"u8{(last ? ";" : " +")}");
        }
    }

    /// <summary>
    /// Emits a standalone evaluator class: the public <c>Evaluate&lt;TElement&gt;</c> entry point that the
    /// <c>EmitEvaluator</c> option and the evaluation-only generation mode produce, over a program entry point.
    /// </summary>
    /// <param name="ns">The namespace.</param>
    /// <param name="className">The evaluator class name.</param>
    /// <param name="programClassReference">The fully qualified reference to the program class.</param>
    /// <param name="entry">The entry point index.</param>
    /// <param name="fileExtension">The generated file extension.</param>
    /// <param name="lineEnd">The line ending sequence.</param>
    /// <returns>The generated file.</returns>
    public static GeneratedCodeFile GenerateStandaloneEvaluator(string ns, string className, string programClassReference, int entry, string fileExtension, string lineEnd)
    {
        return GenerateStandaloneEvaluatorCore(
            ns,
            className,
            $"{programClassReference}.Entry({entry.ToString(System.Globalization.CultureInfo.InvariantCulture)})",
            fileExtension,
            lineEnd);
    }

    /// <summary>
    /// Generates a standalone evaluator for a boolean root schema (<c>true</c> or <c>false</c>). Such roots reduce to the
    /// built-in any/not-any types, which have no program entry, so the shim compiles the constant schema itself.
    /// </summary>
    /// <param name="ns">The namespace for the evaluator class.</param>
    /// <param name="className">The evaluator class name.</param>
    /// <param name="alwaysValid"><see langword="true"/> for the <c>true</c> schema; <see langword="false"/> for <c>false</c>.</param>
    /// <param name="fileExtension">The file extension.</param>
    /// <param name="lineEnd">The line-end sequence.</param>
    /// <returns>The generated code file.</returns>
    public static GeneratedCodeFile GenerateBooleanStandaloneEvaluator(string ns, string className, bool alwaysValid, string fileExtension, string lineEnd)
    {
        return GenerateStandaloneEvaluatorCore(
            ns,
            className,
            $"global::Corvus.Text.Json.RuntimeEvaluator.JsonSchemaEvaluator.Compile(\"{(alwaysValid ? "true" : "false")}\")",
            fileExtension,
            lineEnd);
    }

    private static GeneratedCodeFile GenerateStandaloneEvaluatorCore(string ns, string className, string evaluatorExpression, string fileExtension, string lineEnd)
    {
        var sb = new StringBuilder();
        void Line(string text = "")
        {
            sb.Append(text).Append(lineEnd);
        }

        Line("//------------------------------------------------------------------------------");
        Line("// <auto-generated>");
        Line("//     This code was generated by a tool.");
        Line("//");
        Line("//     Changes to this file may cause incorrect behavior and will be lost if");
        Line("//     the code is regenerated.");
        Line("// </auto-generated>");
        Line("//------------------------------------------------------------------------------");
        Line("#nullable enable");
        Line("#pragma warning disable");
        Line();
        Line("using Corvus.Text.Json;");
        Line("using Corvus.Text.Json.Internal;");
        Line();
        if (ns.Length > 0)
        {
            Line($"namespace {ns};");
            Line();
        }

        Line("/// <summary>");
        Line("/// Evaluates instances against the schema, collecting results and annotations on request.");
        Line("/// </summary>");
        Line("[global::System.CodeDom.Compiler.GeneratedCode(\"Corvus.Text.Json.CodeGeneration\", \"1.0\")]");
        Line($"public static class {className}");
        Line("{");
        Line($"    private static readonly global::Corvus.Text.Json.RuntimeEvaluator.JsonSchemaEvaluator Evaluator = {evaluatorExpression};");
        Line();
        Line("    /// <summary>");
        Line("    /// Evaluates the given JSON element against this schema.");
        Line("    /// </summary>");
        Line("    /// <typeparam name=\"TElement\">The type of JSON element.</typeparam>");
        Line("    /// <param name=\"instance\">The instance to evaluate.</param>");
        Line("    /// <param name=\"resultsCollector\">The optional results collector.</param>");
        Line("    /// <returns><see langword=\"true\"/> if the instance is valid against the schema.</returns>");
        Line("    public static bool Evaluate<TElement>(in TElement instance, IJsonSchemaResultsCollector? resultsCollector = null)");
        Line("        where TElement : struct, IJsonElement<TElement>");
        Line("    {");
        Line("        return Evaluator.Evaluate(in instance, resultsCollector);");
        Line("    }");
        Line();
        Line("    /// <summary>");
        Line("    /// Evaluates the element at the given index of a document against this schema.");
        Line("    /// </summary>");
        Line("    /// <param name=\"document\">The document.</param>");
        Line("    /// <param name=\"index\">The element index within the document.</param>");
        Line("    /// <param name=\"resultsCollector\">The optional results collector.</param>");
        Line("    /// <returns><see langword=\"true\"/> if the instance is valid against the schema.</returns>");
        Line("    public static bool Evaluate(IJsonDocument document, int index, IJsonSchemaResultsCollector? resultsCollector = null)");
        Line("    {");
        Line("        return Evaluator.Evaluate(document, index, resultsCollector);");
        Line("    }");
        Line("}");

        return new GeneratedCodeFile(className + ".Evaluator" + fileExtension, sb.ToString());
    }

    /// <summary>
    /// Gets the standalone evaluator class name for a root type: the type's .NET name (or its reduced type's)
    /// followed by <c>Evaluator</c>, else a name derived from the schema location.
    /// </summary>
    /// <param name="rootType">The root type declaration.</param>
    /// <returns>The class name.</returns>
    public static string GetEvaluatorClassName(TypeDeclaration rootType)
    {
        if (rootType.TryGetDotnetTypeName(out string? typeName))
        {
            return typeName + "Evaluator";
        }

        TypeDeclaration reduced = rootType.ReducedTypeDeclaration().ReducedType;
        if (reduced != rootType && reduced.TryGetDotnetTypeName(out typeName))
        {
            return typeName + "Evaluator";
        }

        var sb = new StringBuilder();
        foreach (char c in rootType.LocatedSchema.Location.ToString())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
            {
                sb.Append('_');
            }
        }

        string safeName = sb.ToString().Trim('_');
        if (safeName.Length == 0 || char.IsDigit(safeName[0]))
        {
            safeName = "Schema" + safeName;
        }

        return safeName + "Evaluator";
    }

    /// <summary>
    /// Maps a fallback vocabulary URI to the <c>JsonSchemaDialect</c> member name.
    /// </summary>
    /// <param name="vocabularyUri">The vocabulary URI.</param>
    /// <returns>The dialect member name.</returns>
    public static string DialectFor(string? vocabularyUri)
    {
        return vocabularyUri switch
        {
            "http://json-schema.org/draft-04/schema" or "http://json-schema.org/draft-04/schema#" => "Draft4",
            "http://json-schema.org/draft-06/schema" or "http://json-schema.org/draft-06/schema#" => "Draft6",
            "http://json-schema.org/draft-07/schema" or "http://json-schema.org/draft-07/schema#" => "Draft7",
            "https://json-schema.org/draft/2019-09/schema" => "Draft201909",
            _ => "Draft202012",
        };
    }

    private static string Literal(string text)
    {
        return SymbolDisplay.FormatLiteral(text, true);
    }

    private static string StripEmptyFragment(string uri)
    {
        return uri.Length > 0 && uri[uri.Length - 1] == '#' ? uri.Substring(0, uri.Length - 1) : uri;
    }

    /// <summary>
    /// Classifies a root document URI: a <c>file:</c> URI or a scheme-less string (a rooted or relative path, or a
    /// relative <c>$id</c>) is a file-like key; any other absolute URI is not.
    /// </summary>
    private static bool TryGetFilePath(string uri, out string? path)
    {
        uri = StripEmptyFragment(uri);
        if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? absolute) && !IsBarePath(uri))
        {
            if (absolute.IsFile)
            {
                path = absolute.LocalPath.Replace('\\', '/');
                return true;
            }

            path = null;
            return false;
        }

        path = uri.Replace('\\', '/');
        return true;
    }

    private static bool IsBarePath(string uri)
    {
        // "/home/x.json" and "C:/x.json" parse as absolute file URIs on their platforms; treat them as paths.
        return uri.StartsWith("/", StringComparison.Ordinal) || (uri.Length > 2 && char.IsLetter(uri[0]) && uri[1] == ':');
    }
}