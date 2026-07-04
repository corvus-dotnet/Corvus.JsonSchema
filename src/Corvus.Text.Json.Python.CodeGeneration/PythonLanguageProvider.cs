// <copyright file="PythonLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

/// <summary>
/// A language provider that emits idiomatic, byte-native Python (TypedDict type surfaces + AOT
/// validators) from JSON Schema — the Python peer of <c>TypeScriptLanguageProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// Module-per-type emission: each type gets a module with its type surface (a <c>TypedDict</c> for objects,
/// an alias for enums / scalars), a threaded validator <c>evaluate_typed(value, ev, il, kl, r)</c> composed
/// from the registered keyword handlers (the <c>Py*Handler</c> family), a public <c>evaluate</c> wrapper, and
/// a <c>parse</c> helper. A <c>__init__.py</c> barrel re-exports every type. Validators delegate to the shared
/// <c>corvus_json_runtime</c> package.
/// </para>
/// <para>
/// The type surface is <c>TypedDict</c> (<c>total=False</c> with <c>Required[...]</c> for required members).
/// The full keyword family and the richer surface (brands / unions / build / produce) grow incrementally; the
/// validator dispatch and the runtime ABI are established here.
/// </para>
/// </remarks>
public sealed class PythonLanguageProvider : IHierarchicalLanguageProvider
{
    private const string PyTypeKey = "Py_TypeName";
    private const string PyModuleKey = "Py_Module";

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();
    private string runtimeModule = "corvus_json_runtime";

    /// <summary>
    /// Gets or sets the root type whose public <c>evaluate</c>/<c>parse</c> entry point the single file exposes.
    /// The driver sets this before <see cref="GenerateCodeFor"/> (the type list is post-order, so the root is
    /// not reliably first); when unset, the first type is used as a fallback.
    /// </summary>
    public TypeDeclaration? RootHint { get; set; }

    /// <summary>
    /// Options for the Python provider (driver entry point).
    /// </summary>
    /// <param name="RuntimeModuleSpecifier">
    /// The import package the generated modules import the shared model runtime from. Defaults to the
    /// published package name <c>corvus_json_runtime</c>.
    /// </param>
    /// <param name="AlwaysAssertFormat">
    /// Whether the provider asserts <c>format</c> / <c>content</c> rather than leaving them annotations (the
    /// 2020-12 default). Enables the optional/format suite.
    /// </param>
    public sealed record Options(string RuntimeModuleSpecifier = "corvus_json_runtime", bool AlwaysAssertFormat = false);

    /// <summary>
    /// Creates a provider with the default keyword handlers registered (format left as an annotation).
    /// </summary>
    /// <returns>A configured <see cref="PythonLanguageProvider"/>.</returns>
    public static PythonLanguageProvider CreateDefault()
    {
        var provider = new PythonLanguageProvider();
        provider.RegisterValidationHandlers(
            new PyTypeHandler(),
            new PyConstantBoundHandler(),
            new PyMembershipHandler(),
            new PyRegexHandler(),
            new PyRequiredHandler(),
            new PyObjectPropertiesHandler(),
            new PyAllOfHandler(),
            new PyAnyOfHandler(),
            new PyOneOfHandler(),
            new PyNotHandler(),
            new PyIfThenElseHandler(),
            new PyPrefixItemsHandler(),
            new PyItemsHandler(),
            new PyContainsHandler(),
            new PyUniqueItemsHandler(),
            new PyPropertyNamesHandler(),
            new PyDependentRequiredHandler(),
            new PyDependentSchemasHandler(),
            new PyUnevaluatedPropertiesHandler(),
            new PyUnevaluatedItemsHandler());
        return provider;
    }

    /// <summary>
    /// Creates a provider like <see cref="CreateDefault"/> but asserting <c>format</c> / <c>content</c> (the
    /// optional/format suite).
    /// </summary>
    /// <returns>A configured <see cref="PythonLanguageProvider"/> that asserts <c>format</c>.</returns>
    public static PythonLanguageProvider CreateWithFormatAssertion()
    {
        PythonLanguageProvider provider = CreateDefault();
        provider.RegisterValidationHandlers(new PyFormatHandler(), new PyContentHandler());
        return provider;
    }

    /// <summary>
    /// The driver-facing entry point, mirroring <c>TypeScriptLanguageProvider.DefaultWithOptions</c>.
    /// </summary>
    /// <param name="options">The options configuring the provider.</param>
    /// <returns>A configured <see cref="PythonLanguageProvider"/>.</returns>
    public static PythonLanguageProvider DefaultWithOptions(Options options)
    {
        PythonLanguageProvider provider = options.AlwaysAssertFormat ? CreateWithFormatAssertion() : CreateDefault();
        provider.runtimeModule = options.RuntimeModuleSpecifier;
        return provider;
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers)
    {
        this.validationHandlers.RegisterValidationHandlers(handlers);
        return this;
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders) => this;

    /// <inheritdoc/>
    public ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics) => this;

    /// <inheritdoc/>
    public bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
        => this.validationHandlers.TryGetHandlersFor(keyword, out validationHandlers);

    /// <inheritdoc/>
    public bool ShouldGenerate(TypeDeclaration type) => true;

    /// <inheritdoc/>
    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc/>
    public void SetParent(TypeDeclaration child, TypeDeclaration? parent)
    {
    }

    /// <inheritdoc/>
    public TypeDeclaration? GetParent(TypeDeclaration child) => null;

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration) => [];

    /// <inheritdoc/>
    public void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName, CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc/>
    public void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, IEnumerable<TypeDeclaration> existingTypeDeclarations, CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, CancellationToken cancellationToken)
    {
        List<TypeDeclaration> types = typeDeclarations.ToList();
        AssignNames(types);

        // Single-file emission (the TypeScript engine's model): every type's TypedDict surface + `_eval_<name>`
        // validator go in ONE module, so cross-type references are unqualified function calls — no per-module
        // import, no `_m_` aliasing, and recursive/mutually-referential types resolve for free (Python looks up
        // a called function at call time). The module-level dispatch tables are emitted after all validators.
        var mod = new PyModule(this.runtimeModule);
        foreach (TypeDeclaration td in types)
        {
            this.EmitType(td, mod);
        }

        // The public `evaluate`/`parse` entry point for the ROOT type. Emitting it here — rather than via
        // RootEvaluateExport after the header is assembled — keeps its imports (json/cast/Results and the exact
        // tracker) in the single header. The driver sets RootHint (the type list is post-order, so the root is
        // not reliably first); fall back to the first type only if unset.
        TypeDeclaration? root = this.RootHint ?? (types.Count > 0 ? types[0] : null);
        if (root is not null)
        {
            EmitRootEntry(root, mod);
        }

        var body = new StringBuilder(mod.Body.ToString().TrimEnd('\n'));
        if (mod.ModuleLevel.Length > 0)
        {
            body.Append("\n\n\n").Append(mod.ModuleLevel.ToString().TrimEnd('\n'));
        }

        string content = AssembleHeader(mod) + "\n\n\n" + body.ToString();
        return [new GeneratedCodeFile("__init__.py", content)];
    }

    /// <summary>
    /// Re-exports the root type's validator as the package-level <c>evaluate</c> entry point, for appending to
    /// the barrel <c>__init__.py</c> after <see cref="GenerateCodeFor(IEnumerable{TypeDeclaration}, CancellationToken)"/>
    /// has assigned names.
    /// </summary>
    /// <param name="rootType">The root type whose validator the entry point aliases.</param>
    /// <returns>The Python source (an import) to append to <c>__init__.py</c>.</returns>
    public string RootEvaluateExport(TypeDeclaration rootType)
    {
        // The root's public evaluate/parse are emitted inline by GenerateCodeFor into the single module, so
        // there is nothing to append after the fact.
        _ = rootType;
        return string.Empty;
    }

    /// <summary>The snake_case module name assigned to a type declaration (unique across the single file).</summary>
    /// <param name="td">The type declaration.</param>
    /// <returns>The module name.</returns>
    internal static string ModuleNameOf(TypeDeclaration td)
        => td.TryGetMetadata<string>(PyModuleKey, out string? n) && !string.IsNullOrEmpty(n) ? n! : "entity";

    /// <summary>The unique name of a type's threaded validator function (<c>_eval_&lt;module&gt;</c>).</summary>
    /// <param name="td">The type declaration.</param>
    /// <returns>The validator function name.</returns>
    internal static string EvalNameOf(TypeDeclaration td) => "_eval_" + ModuleNameOf(td);

    /// <summary>The PascalCase Python type name assigned to a type declaration.</summary>
    /// <param name="td">The type declaration.</param>
    /// <returns>The type name.</returns>
    internal static string TypeNameOf(TypeDeclaration td)
        => td.TryGetMetadata<string>(PyTypeKey, out string? n) && !string.IsNullOrEmpty(n) ? n! : "Entity";

    /// <summary>True when a module was generated for this type (it was in the generated set and named).</summary>
    /// <param name="td">The type declaration.</param>
    /// <returns>Whether a module exists for the type.</returns>
    internal static bool HasModule(TypeDeclaration td)
        => td.TryGetMetadata<string>(PyModuleKey, out string? n) && !string.IsNullOrEmpty(n);

    // A faithful port of the TypeScript NameForType heuristic chain (itself the port of the C# NameHeuristic
    // chain): Pass 0 records, per type, the property it is the value of (a fallback name source for nested
    // types); then each type is named and deduplicated.
    private static void AssignNames(List<TypeDeclaration> types)
    {
        var propertyName = new Dictionary<TypeDeclaration, string>();
        foreach (TypeDeclaration td in types)
        {
            foreach (PropertyDeclaration p in td.PropertyDeclarations)
            {
                if (!propertyName.ContainsKey(p.ReducedPropertyType))
                {
                    propertyName[p.ReducedPropertyType] = p.JsonPropertyName;
                }
            }
        }

        var usedTypes = new HashSet<string>(StringComparer.Ordinal);
        var usedModules = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeDeclaration td in types)
        {
            // The module and type names must both be valid Python identifiers that are not keywords (a
            // subschema under `not`/`if`/`then`/`else` derives those very names; a module named `not` makes
            // `from .not import ...` a syntax error). NameForType already avoids the reserved TYPE names; the
            // `_` guard covers a keyword surfacing as a module (snake) name.
            string baseName = NameForType(td, propertyName);
            if (!IsIdentifier(baseName))
            {
                baseName += "_";
            }

            string name = baseName;
            for (int i = 2; !usedTypes.Add(name); i++)
            {
                name = baseName + i;
            }

            string module = Snake(name);
            if (!IsIdentifier(module))
            {
                module += "_";
            }

            string moduleName = module;
            for (int i = 2; !usedModules.Add(moduleName); i++)
            {
                moduleName = module + "_" + i;
            }

            td.SetMetadata(PyTypeKey, name);
            td.SetMetadata(PyModuleKey, moduleName);
        }
    }

    // PascalCase identifiers that would shadow a runtime/typing import a generated module relies on, or a
    // Python keyword that pascalises to itself (True/False/None); a name colliding with one is suffixed
    // "Entity" (the Python peer of the TypeScript reserved-word pass).
    private static readonly HashSet<string> PyReserved = new(StringComparer.Ordinal)
    {
        "True", "False", "None",
        "Any", "Sequence", "Required", "TypedDict", "Literal", "cast",
        "Ev", "NOEV", "Results", "Annotation", "Failure",
    };

    // BaseSchema/Documentation/Subschema heuristic chain (faithful port of TypeScriptLanguageProvider.NameForType).
    private static string NameForType(TypeDeclaration td, Dictionary<TypeDeclaration, string> propertyName)
    {
        JsonReferenceBuilder reference = JsonReferenceBuilder.From(td.LocatedSchema.Location.ToString());
        string? raw;
        if (td.IsInDefinitionsContainer())
        {
            // A $defs member is named from its key (the last fragment segment), never the title.
            raw = NameFromReference(reference, allowPath: false) ?? TitleIfShort(td);
        }
        else if (!reference.HasFragment)
        {
            // The document root: a short usable title, else the path basename, else a generic fallback.
            raw = TitleIfShort(td) ?? NameFromReference(reference, allowPath: true) ?? "Schema";
        }
        else
        {
            // A nested subschema: a short usable title, else the fragment segment, else the property it is the value of.
            raw = TitleIfShort(td)
                ?? NameFromReference(reference, allowPath: false)
                ?? (propertyName.TryGetValue(td, out string? pn) ? Pascal(pn) : null);
        }

        string name = raw ?? string.Empty;
        if (name.Length == 0)
        {
            return "Entity";
        }

        return PyReserved.Contains(name) ? name + "Entity" : name;
    }

    // Use the `title` ONLY when it is short (< 64 chars) and pascalises to a usable identifier (2..63 chars).
    private static string? TitleIfShort(TypeDeclaration td)
    {
        JsonElement s = td.LocatedSchema.Schema;
        if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String)
        {
            string raw = t.GetString()!;
            if (raw.Length > 0 && raw.Length < 64)
            {
                string p = Pascal(raw);
                if (p.Length > 1 && p.Length < 64)
                {
                    return p;
                }
            }
        }

        return null;
    }

    // Derive a name from the type's decomposed location: the last fragment segment (skipping a trailing
    // numeric index, e.g. allOf/0 -> AllOf), else (root only) the document path's basename without extension.
    private static string? NameFromReference(JsonReferenceBuilder reference, bool allowPath)
    {
        if (reference.HasFragment)
        {
            ReadOnlySpan<char> frag = reference.Fragment;
            int lastSlash = frag.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < frag.Length - 1)
            {
                if (char.IsDigit(frag[lastSlash + 1]))
                {
                    int prev = frag[..lastSlash].LastIndexOf('/');
                    if (prev >= 0)
                    {
                        string keyword = Pascal(frag[(prev + 1)..lastSlash].ToString());
                        if (keyword.Length > 0)
                        {
                            return keyword;
                        }
                    }
                }

                string seg = Pascal(frag[(lastSlash + 1)..].ToString());
                if (seg.Length > 0)
                {
                    return seg;
                }
            }
        }

        if (allowPath && reference.HasPath)
        {
            ReadOnlySpan<char> path = reference.Path;
            int lastSlash = path.LastIndexOf('/');
            ReadOnlySpan<char> baseSeg = lastSlash >= 0 && lastSlash < path.Length - 1 ? path[(lastSlash + 1)..] : path;
            int dot = baseSeg.LastIndexOf('.');
            if (dot > 0)
            {
                baseSeg = baseSeg[..dot];
            }

            string p = Pascal(baseSeg.ToString());
            if (p.Length > 0)
            {
                return p;
            }
        }

        return null;
    }

    // Emit ONE type's module: type surface + threaded validator + evaluate wrapper + parse.
    // Emit one type's TypedDict surface + its `_eval_<name>` validator into the shared single-file body. No
    // per-type public evaluate/parse (only the root gets those, via EmitRootEntry); the module-level dispatch
    // tables a handler defers stay in mod.ModuleLevel and are emitted after ALL validators.
    private void EmitType(TypeDeclaration td, PyModule mod)
    {
        if (mod.Body.Length > 0)
        {
            mod.Body.Append("\n\n\n");
        }

        if (IsObject(td))
        {
            EmitObjectSurface(td, mod);
        }
        else if (IsEnum(td))
        {
            EmitEnumSurface(td, mod);
        }
        else if (IsUnion(td))
        {
            EmitUnionSurface(td, mod);
        }
        else
        {
            EmitScalarSurface(td, mod);
        }

        mod.Body.Append("\n\n");
        this.EmitValidator(td, mod);
        EmitConstruction(td, mod);
    }

    // The construction surface a named type exposes alongside its validator (the Python peer of the TypeScript
    // companion's build/buildCanonical). An object serialises its TypedDict props to JSON bytes — at the native
    // floor (`build`, caller key order) or canonically (`canonicalize`, RFC 8785 recursive key sort). produce /
    // patch (Model C) and the RFC-patch wrappers await the runtime's Phase-1 byte primitives.
    private static void EmitConstruction(TypeDeclaration td, PyModule mod)
    {
        if (!IsObject(td))
        {
            return;
        }

        string module = ModuleNameOf(td);
        string typeRef = TypeNameOf(td);
        mod.Body.Append("\n\n\ndef build_").Append(module).Append("(value: ").Append(typeRef).Append(") -> bytes:\n");
        mod.Body.Append("    return ").Append(mod.Rt("build")).Append("(value)\n");
        mod.Body.Append("\n\ndef build_canonical_").Append(module).Append("(value: ").Append(typeRef).Append(") -> bytes:\n");
        mod.Body.Append("    return ").Append(mod.Rt("canonicalize")).Append("(value)\n");
        mod.Typing.Add("cast");
        mod.Body.Append("\n\ndef parse_").Append(module).Append("(data: bytes | str) -> ").Append(typeRef).Append(":\n");
        mod.Body.Append("    return cast(\"").Append(typeRef).Append("\", ").Append(mod.Rt("decode_and_parse")).Append("(data))\n");

        // RFC 7396 merge patch (runtime-ready). apply returns canonical bytes; create returns the patch value.
        // Each side accepts a decoded value or the wire bytes, decoding via decode_and_parse (the byte-native seam).
        string decodeDoc = mod.Rt("decode_and_parse") + "(doc) if isinstance(doc, bytes) else doc";
        mod.Body.Append("\n\ndef apply_merge_patch_").Append(module).Append("(doc: ").Append(typeRef).Append(" | bytes, merge_patch: object) -> bytes:\n");
        mod.Body.Append("    return ").Append(mod.Rt("canonicalize")).Append("(").Append(mod.Rt("apply_merge_patch")).Append("(").Append(decodeDoc).Append(", merge_patch))\n");
        mod.Body.Append("\n\ndef create_merge_patch_").Append(module).Append("(source: ").Append(typeRef).Append(" | bytes, target: ").Append(typeRef).Append(" | bytes) -> object:\n");
        mod.Body.Append("    return ").Append(mod.Rt("create_merge_patch")).Append("(\n");
        mod.Body.Append("        ").Append(mod.Rt("decode_and_parse")).Append("(source) if isinstance(source, bytes) else source,\n");
        mod.Body.Append("        ").Append(mod.Rt("decode_and_parse")).Append("(target) if isinstance(target, bytes) else target,\n");
        mod.Body.Append("    )\n");
    }

    private static string AssembleHeader(PyModule mod)
    {
        var h = new StringBuilder();
        h.Append("# AUTO-GENERATED by Corvus.Text.Json (Python engine, single-file). Do not edit.\n");
        h.Append("from __future__ import annotations\n");
        if (mod.NeedsJson)
        {
            h.Append("import json\n");
        }

        if (mod.NeedsSequence)
        {
            h.Append("from collections.abc import Sequence\n");
        }

        if (mod.Typing.Count > 0)
        {
            h.Append("from typing import ").Append(string.Join(", ", mod.Typing)).Append('\n');
        }

        if (mod.Runtime.Count > 0)
        {
            h.Append("from ").Append(mod.RuntimeModule).Append(" import ").Append(string.Join(", ", mod.Runtime)).Append('\n');
        }

        return h.ToString().TrimEnd('\n');
    }

    private void EmitValidator(TypeDeclaration td, PyModule mod)
    {
        mod.Body.Append("def ").Append(EvalNameOf(td)).Append("(value: object, ev: ").Append(mod.Rt("Ev"))
                .Append(", r: ").Append(mod.Rt("Results")).Append(" | None) -> bool:\n");

        // A boolean `false` schema matches nothing; `true` (and an empty schema) matches everything.
        if (td.LocatedSchema.Schema.ValueKind == JsonValueKind.False)
        {
            mod.Body.Append("    if r is not None:\n");
            mod.Body.Append("        r.fail(\"\", ").Append(PyEmit.Str(PyEmit.AbsoluteKeywordLocation(td, null))).Append(")\n");
            mod.Body.Append("    return False\n");
            return;
        }

        mod.Body.Append("    ok = True\n");

        var steps = new List<(uint Priority, IPyKeywordEmitter Emitter, IKeyword Keyword)>();
        foreach (IKeyword keyword in td.Keywords())
        {
            if (this.validationHandlers.TryGetHandlersFor(keyword, out IReadOnlyCollection<IKeywordValidationHandler>? handlers))
            {
                foreach (IKeywordValidationHandler handler in handlers)
                {
                    if (handler is IPyKeywordEmitter emitter)
                    {
                        steps.Add((handler.ValidationHandlerPriority, emitter, keyword));
                    }
                }
            }
        }

        foreach ((uint _, IPyKeywordEmitter emitter, IKeyword keyword) in steps.OrderBy(s => s.Priority))
        {
            emitter.Emit(mod, td, keyword);
        }

        EmitAnnotations(td, mod);
        mod.Body.Append("    return ok\n");
    }

    // Verbose mode: a SUCCESSFULLY validated subschema contributes its annotation keywords at the current
    // instance location (the analog of the .NET EvaluateSchema annotation walk). Gated on
    // `r is not None and r.verbose and ok`, so boolean and detailed modes pay nothing.
    private static readonly string[] AnnotationKeywords = ["title", "description", "default", "examples", "readOnly", "writeOnly", "deprecated", "format"];

    private static void EmitAnnotations(TypeDeclaration td, PyModule mod)
    {
        JsonElement schema = td.LocatedSchema.Schema;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var present = new List<(string Keyword, JsonElement Value)>();
        foreach (string kw in AnnotationKeywords)
        {
            if (schema.TryGetProperty(kw, out JsonElement v))
            {
                present.Add((kw, v));
            }
        }

        if (present.Count == 0)
        {
            return;
        }

        mod.Body.Append("    if r is not None and r.verbose and ok:\n");
        foreach ((string kw, JsonElement value) in present)
        {
            mod.Body.Append("        r.annotate(").Append(PyEmit.Str(kw)).Append(", ").Append(PyEmit.Literal(value))
                    .Append(", ").Append(PyEmit.Str("/" + kw)).Append(", ").Append(PyEmit.Str(PyEmit.AbsoluteKeywordLocation(td, kw))).Append(")\n");
        }
    }

    // The single file's public entry point, for the ROOT type only. A $ref/$dynamicRef-only root reduces to
    // its target (the generated validator), mirroring the TypeScript RootEvaluatorExport.
    private static void EmitRootEntry(TypeDeclaration rootType, PyModule mod)
    {
        TypeDeclaration td = rootType.ReducedTypeDeclaration().ReducedType;
        string name = TypeNameOf(td);

        // Pass the no-op tracker unless the root itself consults `unevaluated*` — a fresh collecting tracker is
        // only needed when something reads the marks. This turns off the whole mark/merge apparatus at runtime
        // (gated on `ev.n`) for the common schema with no unevaluated* at the root.
        string rootEv = td.RequiresPropertyEvaluationTracking() || td.RequiresItemsEvaluationTracking()
            ? mod.Rt("fresh") + "()"
            : mod.Rt("NOEV");

        // Byte-native entry: a Uint8Array-equivalent (bytes) is decoded to a Python value first, mirroring the
        // TypeScript companion's `v instanceof Uint8Array ? decodeAndParse(v) : v`, so evaluate/parse accept
        // both a decoded value and the wire bytes produced by build/build_canonical.
        mod.Body.Append("\n\n\ndef evaluate(value: object, results: ").Append(mod.Rt("Results")).Append(" | None = None) -> bool:\n");
        mod.Body.Append("    if isinstance(value, bytes):\n");
        mod.Body.Append("        value = ").Append(mod.Rt("decode_and_parse")).Append("(value)\n");
        mod.Body.Append("    return ").Append(EvalNameOf(td)).Append("(value, ").Append(rootEv).Append(", results)\n");

        mod.Typing.Add("cast");
        mod.Body.Append("\n\ndef parse(data: bytes | str) -> ").Append(name).Append(":\n");
        mod.Body.Append("    return cast(\"").Append(name).Append("\", ").Append(mod.Rt("decode_and_parse")).Append("(data))\n");
    }

    private static void EmitObjectSurface(TypeDeclaration td, PyModule mod)
    {
        string name = TypeNameOf(td);
        mod.Typing.Add("TypedDict");

        var props = td.PropertyDeclarations.ToList();
        if (props.Count == 0)
        {
            mod.Body.Append("class ").Append(name).Append("(TypedDict, total=False):\n    pass\n");
            return;
        }

        // A TypedDict class body only accepts identifier keys; any non-identifier (or Python-keyword) member
        // name forces the functional TypedDict("Name", {...}) form. (Functional-form values are evaluated at
        // import, but this only triggers for the rare non-identifier key.)
        bool classForm = props.All(p => IsIdentifier(p.JsonPropertyName));
        if (classForm)
        {
            mod.Body.Append("class ").Append(name).Append("(TypedDict, total=False):\n");
            foreach (PropertyDeclaration p in props)
            {
                mod.Body.Append("    ").Append(p.JsonPropertyName).Append(": ").Append(MemberType(p, mod)).Append('\n');
            }
        }
        else
        {
            // Functional form (a non-identifier key like "$ref" forces it): the member types are string
            // forward references so they are NOT eagerly evaluated at import (which would deadlock a recursive
            // schema); the class-body form above relies on `from __future__ import annotations` for the same.
            mod.Body.Append(name).Append(" = TypedDict(\"").Append(name).Append("\", {\n");
            foreach (PropertyDeclaration p in props)
            {
                mod.Body.Append("    ").Append(PyEmit.Str(p.JsonPropertyName)).Append(": ").Append(PyEmit.Str(MemberType(p, mod))).Append(",\n");
            }

            mod.Body.Append("}, total=False)\n");
        }
    }

    private static string MemberType(PropertyDeclaration p, PyModule mod)
    {
        string typeRef = PyTypeRef(p.ReducedPropertyType, mod);
        if (p.RequiredOrOptional != RequiredOrOptional.Optional)
        {
            mod.Typing.Add("Required");
            typeRef = "Required[" + typeRef + "]";
        }

        return typeRef;
    }

    private static void EmitEnumSurface(TypeDeclaration td, PyModule mod)
    {
        string name = TypeNameOf(td);
        var literals = new List<string>();
        int total = 0;
        if (td.LocatedSchema.Schema.TryGetProperty("enum", out JsonElement en) && en.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement m in en.EnumerateArray())
            {
                total++;
                if (m.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
                {
                    string lit = PyEmit.Literal(m);
                    if (!literals.Contains(lit))
                    {
                        literals.Add(lit);
                    }
                }
            }
        }

        if (literals.Count == total && literals.Count > 0)
        {
            mod.Typing.Add("Literal");
            mod.Body.Append("type ").Append(name).Append(" = Literal[").Append(string.Join(", ", literals)).Append("]\n");
        }
        else
        {
            mod.Typing.Add("Any");
            mod.Body.Append("type ").Append(name).Append(" = Any\n");
        }
    }

    // A oneOf/anyOf union: `X = A | B` plus per-member type guards (the V5 TryGetAs{Branch} analog, backed by
    // the member's boolean validator). The alias drops any self-reference (a recursive union can't be an alias
    // of itself); the validator (the anyOf/oneOf handler) still enforces the recursive branch.
    private static void EmitUnionSurface(TypeDeclaration td, PyModule mod)
    {
        string name = TypeNameOf(td);
        List<TypeDeclaration> members = UnionMembers(td);

        var refs = new List<string>();
        foreach (TypeDeclaration m in members)
        {
            string rf = PyTypeRef(m, mod);
            if (rf != name && !refs.Contains(rf))
            {
                refs.Add(rf);
            }
        }

        if (refs.Count == 0)
        {
            refs.Add(Any(mod));
        }

        // A PEP 695 `type` alias (lazily evaluated, type-only — the analog of TypeScript's erased `type X = ...`),
        // NOT a runtime `X = A | B` expression: the latter eagerly evaluates the member references at import and
        // deadlocks on a recursive/circular schema.
        mod.Body.Append("type ").Append(name).Append(" = ").Append(string.Join(" | ", refs)).Append('\n');

        foreach (TypeDeclaration m in members)
        {
            TypeDeclaration reduced = m.ReducedTypeDeclaration().ReducedType;
            if (!HasModule(reduced))
            {
                continue;
            }

            string rf = PyTypeRef(reduced, mod);
            if (rf == name)
            {
                continue;
            }

            // Dedup guard functions across the WHOLE single file — the same member type reached from two
            // different unions must define `is_<name>` only once (the guard body is identical).
            string guard = "is_" + Snake(TypeNameOf(reduced));
            if (!mod.EmittedGuards.Add(guard))
            {
                continue;
            }

            mod.Typing.Add("TypeGuard");
            mod.Body.Append("\n\ndef ").Append(guard).Append("(value: object) -> TypeGuard[").Append(rf).Append("]:\n")
                    .Append("    return ").Append(EvalNameOf(reduced)).Append("(value, ").Append(mod.Rt("fresh")).Append("(), None)\n");
        }
    }

    private static void EmitScalarSurface(TypeDeclaration td, PyModule mod)
    {
        string name = TypeNameOf(td);
        var prims = new List<string>();
        foreach (string k in SchemaTypes(td))
        {
            if (Prim(k) is string p && !prims.Contains(p))
            {
                prims.Add(p);
            }
        }

        if (prims.Count == 0)
        {
            mod.Typing.Add("Any");
            mod.Body.Append("type ").Append(name).Append(" = Any\n");
        }
        else
        {
            mod.Body.Append("type ").Append(name).Append(" = ").Append(string.Join(" | ", prims)).Append('\n');
        }
    }

    // The Python type reference for a member type: a named, GENERATED (object/enum/union) type is its
    // unqualified name (all types share the single file); a scalar maps to its builtin; an array to
    // Sequence[...]; anything else (including an object type with no generated validator) falls back to Any.
    private static string PyTypeRef(TypeDeclaration td, PyModule mod)
    {
        TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;
        if ((IsObject(reduced) || IsEnum(reduced) || IsUnion(reduced)) && HasModule(reduced))
        {
            return TypeNameOf(reduced);
        }

        List<string> kinds = SchemaTypes(reduced);
        if (kinds.Contains("array") || (reduced.ExplicitNonTupleItemsType() ?? reduced.ArrayItemsType()) is not null)
        {
            mod.NeedsSequence = true;
            TypeDeclaration? elem = (reduced.ExplicitNonTupleItemsType() ?? reduced.ArrayItemsType())?.ReducedType;
            string elemRef = elem is not null ? PyTypeRef(elem, mod) : Any(mod);
            return "Sequence[" + elemRef + "]";
        }

        var prims = new List<string>();
        foreach (string k in kinds)
        {
            if (Prim(k) is string p && !prims.Contains(p))
            {
                prims.Add(p);
            }
        }

        return prims.Count > 0 ? string.Join(" | ", prims) : Any(mod);
    }

    private static string Any(PyModule mod)
    {
        mod.Typing.Add("Any");
        return "Any";
    }

    private static List<string> SchemaTypes(TypeDeclaration td)
    {
        var result = new List<string>();
        JsonElement s = td.LocatedSchema.Schema;
        if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("type", out JsonElement t))
        {
            if (t.ValueKind == JsonValueKind.String && t.GetString() is { } single)
            {
                result.Add(single);
            }
            else if (t.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement e in t.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.String && e.GetString() is { } member)
                    {
                        result.Add(member);
                    }
                }
            }
        }

        return result;
    }

    private static bool IsObject(TypeDeclaration td)
    {
        JsonElement s = td.LocatedSchema.Schema;
        if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("properties", out _))
        {
            return true;
        }

        if (SchemaTypes(td).Contains("object"))
        {
            return true;
        }

        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            if (p.LocalOrComposed == LocalOrComposed.Composed)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnum(TypeDeclaration td)
        => td.LocatedSchema.Schema.ValueKind == JsonValueKind.Object && td.LocatedSchema.Schema.TryGetProperty("enum", out _);

    private static bool IsUnion(TypeDeclaration td)
        => (td.OneOfCompositionTypes()?.Count ?? 0) > 0 || (td.AnyOfCompositionTypes()?.Count ?? 0) > 0;

    private static List<TypeDeclaration> UnionMembers(TypeDeclaration td)
    {
        var members = new List<TypeDeclaration>();
        CollectInto(td.OneOfCompositionTypes(), members);
        CollectInto(td.AnyOfCompositionTypes(), members);
        return members;
    }

    private static void CollectInto<TKeyword>(IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? composition, List<TypeDeclaration> into)
        where TKeyword : notnull
    {
        if (composition is null)
        {
            return;
        }

        foreach (KeyValuePair<TKeyword, IReadOnlyCollection<TypeDeclaration>> kv in composition)
        {
            foreach (TypeDeclaration m in kv.Value)
            {
                TypeDeclaration r = m.ReducedTypeDeclaration().ReducedType;
                if (!into.Contains(r))
                {
                    into.Add(r);
                }
            }
        }
    }

    private static string? Prim(string jsonType) => jsonType switch
    {
        "string" => "str",
        "integer" => "int",
        "number" => "float",
        "boolean" => "bool",
        "null" => "None",
        _ => null,
    };

    // Python hard keywords (soft keywords match/case/type are valid identifiers, so excluded).
    private static readonly HashSet<string> PythonKeywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue",
        "def", "del", "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in",
        "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while", "with", "yield",
    };

    // True when `name` is usable as a bare TypedDict class-body member (a valid Python identifier that is not
    // a keyword); anything else forces the functional TypedDict form.
    private static bool IsIdentifier(string name)
    {
        if (name.Length == 0 || PythonKeywords.Contains(name))
        {
            return false;
        }

        char c0 = name[0];
        if (!(char.IsLetter(c0) || c0 == '_'))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Pascal(string raw)
    {
        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            else
            {
                upper = true;
            }
        }

        if (sb.Length == 0 || char.IsDigit(sb[0]))
        {
            sb.Insert(0, 'T');
        }

        return sb.ToString();
    }

    private static string Snake(string pascal)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(pascal[i - 1]) || char.IsDigit(pascal[i - 1])))
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.Length == 0 ? "entity" : sb.ToString();
    }
}