// <copyright file="TypeScriptLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

/// <summary>
/// A language provider that emits idiomatic TypeScript type surfaces and validators from JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// Phase 0 provider: idiomatic type surface (real names + built-in scalar mapping + property
/// type-references + named enum unions) on top of registry-driven, extensible validators (§13.13).
/// </para>
/// <para>
/// Emission per type: objects -> <c>interface</c>; enums -> <c>type X = a | b</c>; scalars/arrays -> no type
/// declaration (referenced inline as the primitive / element type). EVERY type still gets an
/// <c>evaluate{Name}</c> validator composed from the core handler registry, so recursion always resolves.
/// </para>
/// </remarks>
public sealed class TypeScriptLanguageProvider : IHierarchicalLanguageProvider
{
    private const string TsFinalKey = "Ts_FinalName";

    // The TYPE reference used to annotate a value of a type: the final name for named types
    // (objects/enums/unions/branded formats), but a structural type (number/string/...) for plain
    // scalars. Consumers (the OpenAPI emitter) need this distinct from the final name, which doubles
    // as the value companion carrying evaluate()/parse().
    private const string TsTypeRefKey = "Ts_TypeRef";

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();

    // Every top-level identifier emitted into the current module, so the root entry-point name can be
    // reserved against them and renamed around any collision (like type and union-guard names).
    private readonly HashSet<string> moduleTopLevelNames = new(StringComparer.Ordinal);

    // Where generated modules import the shared runtime from. A relative specifier ("./corvus-runtime.js")
    // means re-emit the runtime alongside the module (self-contained); a bare specifier
    // ("@endjin/corvus-json-runtime") means import the installed package and DON'T re-emit it.
    private string runtimeModuleSpecifier = "./corvus-runtime.js";

    // Whether to emit the idiomatic type surface (interfaces / aliases + the build/patch/accessor helpers).
    // false == SchemaEvaluationOnly: emit a validators-only module (just the evaluate{Type} functions + the
    // evaluateRoot entry point). The validators never reference the type surface, so they stand alone.
    private bool emitTypeSurface = true;

    // When true (gap A1, §7.4), emit one module per generated type + a barrel index.ts instead of a single
    // generated.ts. Default false keeps the single-file output every harness / example consumes today.
    private bool modulePerType;

    // Gap A2: types that are ONLY an if/then/else conditional subschema (validator-only) — their type surface
    // (interface/aliases + helpers) is suppressed so the consumer API isn't polluted with If/Then/Else types;
    // their evaluate{Type} is still emitted (the conditional validator calls it). Recomputed per generation.
    private HashSet<TypeDeclaration> conditionalOnly = [];

    /// <summary>
    /// Gets the shared runtime module source (<c>@endjin/corvus-json-runtime</c> in production): emitted ONCE,
    /// imported by every generated module.
    /// </summary>
    /// <returns>The TypeScript source of the shared runtime module.</returns>
    /// <remarks>
    /// The runtime is authored ONCE as the <c>@endjin/corvus-json-runtime</c> npm package
    /// (<c>packages/corvus-json-runtime/src/index.ts</c>) and embedded here verbatim, so the inline runtime
    /// this method returns and the published package can never drift. The embedded source is returned exactly
    /// as authored (the third-party imports and the per-declaration <c>export</c> markers are already present);
    /// it is read with no encoding normalization so the result is byte-identical to the package source.
    /// </remarks>
    public static string RuntimeModuleSource()
    {
        Stream stream = typeof(TypeScriptLanguageProvider).Assembly
            .GetManifestResourceStream("Corvus.Text.Json.TypeScript.CodeGeneration.corvus-runtime.ts")
            ?? throw new InvalidOperationException("The embedded corvus-runtime.ts resource was not found.");
        using (stream)
        using (var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: false))
        {
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Emits a stable 1-arg entry point for the generated module, aliasing the root type's validator.
    /// </summary>
    /// <param name="rootType">The root type whose validator the entry point aliases.</param>
    /// <returns>The TypeScript source declaring the root entry point and the module default export.</returns>
    /// <remarks>
    /// Call AFTER <see cref="GenerateCodeFor(IEnumerable{TypeDeclaration}, CancellationToken)"/> (names are
    /// assigned, and the module's identifiers recorded). The entry name is RESERVED against every emitted
    /// identifier and renamed around any collision (validate, validate2, ...); it is also emitted as the
    /// module <c>default</c> so consumers can import it without depending on the name.
    /// </remarks>
    public string RootEvaluatorExport(TypeDeclaration rootType)
    {
        TypeDeclaration reduced = rootType.ReducedTypeDeclaration().ReducedType;
        string rootName = reduced.TryGetMetadata<string>(TsFinalKey, out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";

        // The companion object for the root type (emitted by ApplyCompanions) is the module's default export,
        // so the entry point is `Root.evaluate(x)` (or `import Root from "./generated.js"; Root.evaluate(x)`),
        // consistent with the per-type `{Type}.evaluate` companions — there is no standalone evaluateRoot.
        return $"\nexport default {rootName};\n";
    }

    /// <summary>
    /// The module-per-type (<see cref="Options.ModulePerType"/>) analog of <see cref="RootEvaluatorExport"/>:
    /// the addition the driver appends to the barrel <c>index.ts</c>, declaring the <c>evaluateRoot</c> entry
    /// point and the module default export.
    /// </summary>
    /// <param name="rootType">The root type whose validator the entry point aliases.</param>
    /// <returns>The TypeScript source (imports + entry point + default export) to append to <c>index.ts</c>.</returns>
    /// <remarks>
    /// Call AFTER <see cref="GenerateCodeFor(IEnumerable{TypeDeclaration}, CancellationToken)"/>. Unlike the
    /// single-file variant, the barrel re-exports siblings via <c>export *</c> (which does not bring names
    /// into local scope), so the entry point explicitly imports the root validator from its own module and
    /// <c>fresh</c>/<c>Results</c> from the runtime. The entry name is reserved against every exported
    /// identifier and renamed around the rare root-type-named-"Root" collision.
    /// </remarks>
    public string RootEvaluatorBarrelExport(TypeDeclaration rootType)
    {
        TypeDeclaration reduced = rootType.ReducedTypeDeclaration().ReducedType;
        string rootName = reduced.TryGetMetadata<string>(TsFinalKey, out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";

        string entry = "evaluateRoot";
        for (int i = 2; this.moduleTopLevelNames.Contains(entry); i++)
        {
            entry = "evaluateRoot" + i;
        }

        this.moduleTopLevelNames.Add(entry);
        return
            $"\nimport {{ evaluate{rootName} }} from \"./{rootName}.js\";\n" +
            $"import {{ fresh, decodeAndParse, type Results }} from \"{this.runtimeModuleSpecifier}\";\n" +
            $"export {{ decodeAndParse }};\n" +
            $"export const {entry} = (v: unknown, results?: Results): boolean => evaluate{rootName}(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), \"\", \"\", results ?? null);\n" +
            $"export default {entry};\n";
    }

    /// <summary>
    /// Creates a provider with the default validation handlers registered (format left as an annotation).
    /// </summary>
    /// <returns>A configured <see cref="TypeScriptLanguageProvider"/>.</returns>
    public static TypeScriptLanguageProvider CreateDefault()
    {
        var p = new TypeScriptLanguageProvider();
        p.RegisterValidationHandlers(
            new TsTypeHandler(), new TsConstantBoundHandler(), new TsMembershipHandler(), new TsUniqueItemsHandler(),
            new TsRegexHandler(), new TsObjectPropertiesHandler(), new TsRequiredHandler(),
            new TsAllOfHandler(), new TsAnyOfHandler(), new TsOneOfHandler(),
            new TsPrefixItemsHandler(), new TsItemsHandler(),
            new TsPropertyNamesHandler(), new TsDependentRequiredHandler(), new TsIfThenElseHandler(),
            new TsDependentSchemasHandler(), new TsContainsHandler(), new TsNotHandler(),
            new TsUnevaluatedPropertiesHandler(), new TsUnevaluatedItemsHandler());
        return p;
    }

    /// <summary>
    /// Creates a provider like <see cref="CreateDefault"/>, but asserting <c>format</c> (the optional/format
    /// suite). The default leaves format as an annotation (the 2020-12 default), which is why top-level
    /// format.json expects everything valid.
    /// </summary>
    /// <returns>A configured <see cref="TypeScriptLanguageProvider"/> that asserts <c>format</c>.</returns>
    public static TypeScriptLanguageProvider CreateWithFormatAssertion()
    {
        TypeScriptLanguageProvider p = CreateDefault();
        p.RegisterValidationHandlers(new TsFormatHandler(), new TsContentHandler());
        return p;
    }

    /// <summary>
    /// Options for the TypeScript provider (driver entry point; design §7.2).
    /// </summary>
    /// <param name="AlwaysAssertFormat">Whether the provider asserts <c>format</c> rather than treating it as an annotation.</param>
    /// <param name="RuntimeModuleSpecifier">Where generated modules import the shared runtime from.</param>
    /// <param name="EmitTypeSurface">
    /// Whether to emit the idiomatic type surface (interfaces / aliases + build/patch/accessor helpers).
    /// The default (<see langword="true"/>) is the CLI's <c>TypeGeneration</c>/<c>Both</c> mode; pass
    /// <see langword="false"/> for <c>SchemaEvaluationOnly</c> — a validators-only module (just the
    /// <c>evaluate{Type}</c> functions + the <c>evaluateRoot</c> entry point). The TypeScript engine always
    /// emits the validators, so <c>TypeGeneration</c> and <c>Both</c> collapse to the same output here.
    /// </param>
    /// <param name="ModulePerType">
    /// When <see langword="true"/> (gap A1, §7.4), emit one <c>.ts</c> module per generated type plus a barrel
    /// <c>index.ts</c> that re-exports them — enabling tree-shaking and IDE navigation. The default
    /// (<see langword="false"/>) emits a single <c>generated.ts</c> with every type in one module.
    /// </param>
    public sealed record Options(bool AlwaysAssertFormat = false, string RuntimeModuleSpecifier = "./corvus-runtime.js", bool EmitTypeSurface = true, bool ModulePerType = false);

    /// <summary>
    /// The driver-facing entry point, mirroring <c>CSharpLanguageProvider.DefaultWithOptions</c>.
    /// </summary>
    /// <param name="options">The options configuring the provider.</param>
    /// <returns>A configured <see cref="TypeScriptLanguageProvider"/>.</returns>
    public static TypeScriptLanguageProvider DefaultWithOptions(Options options)
    {
        TypeScriptLanguageProvider provider = options.AlwaysAssertFormat ? CreateWithFormatAssertion() : CreateDefault();
        provider.runtimeModuleSpecifier = options.RuntimeModuleSpecifier;
        provider.emitTypeSurface = options.EmitTypeSurface;
        provider.modulePerType = options.ModulePerType;
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
        AssignFinalNames(types);
        this.conditionalOnly = ComputeConditionalOnly(types);

        if (this.modulePerType)
        {
            return this.GenerateModulePerType(types);
        }

        // Single module: every type's surface + validator in one file, importing the shared-runtime helpers
        // (@endjin/corvus-json-runtime, §5.5) from the one shared module — never inlined per module.
        var sb = new StringBuilder();
        sb.Append("// AUTO-GENERATED: idiomatic TS types + registry-composed validators.\n");
        sb.Append(this.RuntimeImportLine()).Append('\n');
        var moduleGuards = new HashSet<string>(StringComparer.Ordinal); // union guard names, unique per module
        foreach (TypeDeclaration td in types)
        {
            this.EmitType(sb, td, moduleGuards);
        }

        string content = this.ApplyCompanions(sb.ToString(), types);

        // Record every emitted top-level identifier so the root entry-point name (RootEvaluatorExport) can
        // be reserved against them and renamed around any collision.
        this.moduleTopLevelNames.Clear();
        foreach (Match m in Regex.Matches(content, TopLevelExportPattern))
        {
            this.moduleTopLevelNames.Add(m.Groups[2].Value);
        }

        // The generated types/validators module. The shared runtime is re-emitted alongside it ONLY when
        // the import specifier is relative (self-contained mode); a bare specifier means the consumer
        // installs @endjin/corvus-json-runtime and we don't duplicate it. (The test harness consumes files.First()
        // and emits its own single shared runtime for the whole suite.)
        var files = new List<GeneratedCodeFile> { new("generated.ts", content) };
        if (this.EmitsRuntimeAlongside)
        {
            files.Add(new GeneratedCodeFile("corvus-runtime.ts", RuntimeModuleSource()));
        }

        return files;
    }

    // Matches a top-level `export <keyword> <name>` declaration: group 1 = keyword, group 2 = identifier.
    private const string TopLevelExportPattern = "(?m)^export (interface|type|class|function|const) ([A-Za-z_$][A-Za-z0-9_$]*)";

    // Companion objects (discoverability + per-type evaluate parity with the .NET EvaluateSchema). After the
    // single-file module is emitted, demote the per-type free functions to module-internal and expose each
    // type's operations as members of an `export const {Type}` companion object (declaration-merged with its
    // interface / type alias). The surface becomes `Person.evaluate(x)` / `Person.build({...})` /
    // `Event.match(...)` instead of standalone functions; the root type's companion is the module default
    // export (see RootEvaluatorExport). Every non-conditional type gets at least `{Type}.evaluate`.
    private string ApplyCompanions(string content, List<TypeDeclaration> types)
    {
        // The per-type free functions become module-internal; the companion objects are the public surface.
        content = content.Replace("\nexport function ", "\nfunction ", StringComparison.Ordinal);

        var sb = new StringBuilder(content);
        sb.Append('\n');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeDeclaration td in types)
        {
            if (this.conditionalOnly.Contains(td))
            {
                continue;
            }

            string name = FinalName(td);

            // One companion per name; only for types that actually emitted a validator (all non-conditional do).
            if (!seen.Add(name) || !content.Contains($"\nfunction evaluate{name}(", StringComparison.Ordinal))
            {
                continue;
            }

            sb.Append(BuildCompanion(name, content));
        }

        return sb.ToString();
    }

    private static string BuildCompanion(string name, string content)
    {
        string camel = Camel(name);
        var props = new List<string>
        {
            // evaluate seeds a fresh tracker (like .NET EvaluateSchema); always present. Accepts the parsed
            // value, or UTF-8 JSON bytes (e.g. build/patch/produce output), which it decodes-and-parses first.
            $"  evaluate: (v: unknown, results?: Results): boolean => evaluate{name}(v instanceof Uint8Array ? decodeAndParse(v) : v, fresh(), \"\", \"\", results ?? null)",
        };

        // parse: decode (if bytes) or JSON.parse (if a string), returning the value typed as this type WITHOUT
        // validating — the convenience form of `JSON.parse(decode(bytes)) as {Type}`. Only emitted when {name}
        // names a TYPE (interface or alias); a plain scalar property has a value-only companion (evaluate) and
        // no type to return.
        bool nameIsType =
            content.Contains($"interface {name} ", StringComparison.Ordinal)
            || content.Contains($"interface {name}<", StringComparison.Ordinal)
            || content.Contains($"type {name} ", StringComparison.Ordinal)
            || content.Contains($"type {name}<", StringComparison.Ordinal);
        if (nameIsType)
        {
            props.Add($"  parse: (v: Uint8Array | string): {name} => (v instanceof Uint8Array ? decodeAndParse(v) : JSON.parse(v)) as {name}");
        }

        void Add(string marker, string member, string fn)
        {
            if (content.Contains(marker, StringComparison.Ordinal))
            {
                props.Add($"  {member}: {fn}");
            }
        }

        Add($"\nfunction build{name}(", "build", $"build{name}");
        Add($"\nfunction buildCanonical{name}(", "buildCanonical", $"buildCanonical{name}");
        Add($"\nfunction patch{name}(", "patch", $"patch{name}");
        Add($"\nfunction produce{name}(", "produce", $"produce{name}");
        Add($"\nfunction withDefaults{name}(", "withDefaults", $"withDefaults{name}");
        Add($"\nfunction match{name}<", "match", $"match{name}");

        // RFC 6902 JSON Patch + RFC 7396 Merge Patch over the document bytes: apply -> canonical bytes; diff
        // two documents -> a patch. Emitted for buildable document types (those with a `build`).
        if (content.Contains($"\nfunction build{name}(", StringComparison.Ordinal))
        {
            props.Add($"  applyPatch: (doc: Uint8Array | {name}, patch: JsonPatch): Uint8Array => canonicalize(applyPatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, patch))");
            props.Add($"  applyMergePatch: (doc: Uint8Array | {name}, mergePatch: unknown): Uint8Array => canonicalize(applyMergePatch(doc instanceof Uint8Array ? decodeAndParse(doc) : doc, mergePatch))");
            props.Add($"  createPatch: (source: Uint8Array | {name}, target: Uint8Array | {name}): JsonPatchOp[] => createPatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target)");
            props.Add($"  createMergePatch: (source: Uint8Array | {name}, target: Uint8Array | {name}): unknown => createMergePatch(source instanceof Uint8Array ? decodeAndParse(source) : source, target instanceof Uint8Array ? decodeAndParse(target) : target)");
        }

        // `from` (validate-and-convert a plain value INTO the brand) — NOT `as`, which is TS's unchecked-cast
        // keyword; `toTemporal`/`toExact` convert the brand OUT to another representation.
        Add($"\nfunction from{name}(", "from", $"from{name}");
        Add($"\nfunction is{name}(", "is", $"is{name}");
        Add($"\nfunction {camel}ToTemporal(", "toTemporal", $"{camel}ToTemporal");
        Add($"\nfunction {camel}ToExact(", "toExact", $"{camel}ToExact");

        return $"export const {name} = {{\n{string.Join(",\n", props)},\n}};\n";
    }

    // Pass 0 + Pass 1: assign each type its final, unique TS name (stored as TsFinalKey metadata). Pass 0
    // records, for each type, the property it is the value of — a fallback name source for nested types the
    // reference/title heuristics can't name (see NameForType). Shared by both emission modes.
    private static void AssignFinalNames(List<TypeDeclaration> types)
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

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeDeclaration td in types)
        {
            string baseName = NameForType(td, propertyName);
            string name = baseName;
            int suffix = 2;
            while (!used.Add(name))
            {
                name = baseName + suffix++;
            }

            td.SetMetadata(TsFinalKey, name);
        }

        // Second pass: now that every final name is assigned, record each type's type reference
        // (TsTypeRef reads the final names of this type and the types it references).
        foreach (TypeDeclaration td in types)
        {
            td.SetMetadata(TsTypeRefKey, TsTypeRef(td));
        }
    }

    // Gap A2: the set of types that are ONLY an if/then/else conditional subschema, whose type surface should
    // be suppressed (kept validator-only). A conditional subschema's reduced type is excluded if it is ALSO
    // referenced as a value (a property/array-item/tuple-item/composition member) — a $ref shared with a real
    // value type still needs its interface, so it stays in the surface.
    private static HashSet<TypeDeclaration> ComputeConditionalOnly(List<TypeDeclaration> types)
    {
        var conditional = new HashSet<TypeDeclaration>();
        var referencedAsValue = new HashSet<TypeDeclaration>();
        foreach (TypeDeclaration td in types)
        {
            AddConditional(conditional, td.IfSubschemaType());
            AddConditional(conditional, td.ThenSubschemaType());
            AddConditional(conditional, td.ElseSubschemaType());

            foreach (PropertyDeclaration p in td.PropertyDeclarations)
            {
                referencedAsValue.Add(p.ReducedPropertyType);
            }

            if ((td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType()) is { } items)
            {
                referencedAsValue.Add(items.ReducedType);
            }

            if (td.ExplicitTupleType() is { } tuple)
            {
                foreach (ReducedTypeDeclaration it in tuple.ItemsTypes)
                {
                    referencedAsValue.Add(it.ReducedType);
                }
            }

            var members = new List<TypeDeclaration>();
            CollectMembers(td.OneOfCompositionTypes(), members);
            CollectMembers(td.AnyOfCompositionTypes(), members);
            CollectMembers(td.AllOfCompositionTypes(), members);
            foreach (TypeDeclaration m in members)
            {
                referencedAsValue.Add(m);
            }
        }

        conditional.ExceptWith(referencedAsValue);
        return conditional;

        static void AddConditional(HashSet<TypeDeclaration> set, SingleSubschemaKeywordTypeDeclaration? t)
        {
            if (t is not null)
            {
                set.Add(t.ReducedType);
            }
        }
    }

    // The shared-runtime import line every generated module emits (§5.5); identical across emission modes.
    private string RuntimeImportLine()
        => "import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError, type JsonPatch, type JsonPatchOp, __fmt, __fmtContent, FormatError, produce, canonicalize, exactNumber, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results, toPlainDate, toInstant, toPlainTime, toDuration, Temporal } from \""
         + this.runtimeModuleSpecifier + "\";\n"
         + "export { decodeAndParse, applyPatch, createPatch, applyMergePatch, createMergePatch, JsonPatchError };\nexport type { JsonPatch, JsonPatchOp };\n";

    // True when the relative runtime specifier means re-emit corvus-runtime.ts alongside the module(s); a
    // bare package specifier (e.g. "@endjin/corvus-json-runtime") means the consumer installs it instead.
    private bool EmitsRuntimeAlongside
        => this.runtimeModuleSpecifier.StartsWith("./", StringComparison.Ordinal)
        || this.runtimeModuleSpecifier.StartsWith("../", StringComparison.Ordinal);

    // Emit ONE type's surface (gated on emitTypeSurface) + its validator into `sb`. The type surface
    // (interfaces / aliases + the build/patch/accessor helpers) is emitted only in type-generation mode;
    // in SchemaEvaluationOnly mode every type still gets its evaluate{Type}, and the validators never
    // reference the type surface, so they stand alone. `moduleGuards` scopes union-guard name uniqueness to
    // the containing module (one set for the whole single-file module; a fresh set per module-per-type file).
    private void EmitType(StringBuilder sb, TypeDeclaration td, HashSet<string> moduleGuards)
    {
        // A2: a conditional (if/then/else) subschema is validator-only — emit no type surface for it, just its
        // evaluate{Type} below (the conditional validator calls it; nothing references it as a value type).
        if (this.emitTypeSurface && !this.conditionalOnly.Contains(td))
        {
            if (IsObject(td))
            {
                // A pure map (additionalProperties value type, no declared properties) -> a `Record` alias;
                // anything with declared properties stays an interface.
                FallbackObjectPropertyType? mapFb = td.LocalEvaluatedPropertyType();
                if (!td.HasPropertyDeclarations && mapFb is not null && mapFb.ReducedType.LocatedSchema.Schema.ValueKind != JsonValueKind.False)
                {
                    EmitMapAlias(sb, td, mapFb);
                }
                else
                {
                    EmitInterface(sb, td);
                    EmitPatch(sb, td);
                    EmitBuild(sb, td);
                    EmitBuildCanonical(sb, td);
                    EmitApplyTo(sb, td);
                    EmitProduceDraft(sb, td);
                    EmitWithDefaults(sb, td);
                }
            }
            else if (IsEnum(td))
            {
                EmitEnumAlias(sb, td);
            }
            else if (IsUnion(td))
            {
                EmitUnionAlias(sb, td, moduleGuards);
            }
            else if (IsBrandedStringFormat(td))
            {
                EmitBrandAlias(sb, td);
            }
            else if (IsBrandedIntegerFormat(td))
            {
                EmitIntegerBrandAlias(sb, td);
            }
            else if (IsBrandedFloatFormat(td))
            {
                EmitFloatBrandAlias(sb, td);
            }
            else if (ArrayElementType(td) is not null)
            {
                // A named array type gets a type alias, so it is usable as a type AND gains a `parse`
                // companion (parse is gated on a `type {name}` existing in the module) — this lets an
                // array-valued response body decode via `{Name}.parse(bytes)`, matching objects / enums /
                // unions / brands, which all emit a type surface.
                sb.Append("export type ").Append(FinalName(td)).Append(" = ").Append(TsTypeRef(td)).Append(";\n");
            }
        }

        EmitValidator(sb, td);
    }

    // Module-per-type emission (gap A1, §7.4): one .ts module per generated type — its surface + validator —
    // plus a barrel index.ts that re-exports them all. Each module imports the shared-runtime helpers AND the
    // specific sibling-module identifiers it references (validators call each other; interfaces and unions
    // reference sibling type names), discovered by scanning the module's own text for any identifier another
    // module owns. Enables tree-shaking + IDE navigation; the single-file default is unchanged.
    private IReadOnlyCollection<GeneratedCodeFile> GenerateModulePerType(List<TypeDeclaration> types)
    {
        // Emit each type's body into its own buffer; record the identifiers that body exports so a sibling
        // module referencing one can import it. Names are globally unique (Pass 1), so each identifier has a
        // single owning module; the keyword drives whether a cross-module import is `type X` or `X`.
        var modules = new List<(string Name, string Body)>();
        var ownerOf = new Dictionary<string, (string Module, bool IsType)>(StringComparer.Ordinal);
        foreach (TypeDeclaration td in types)
        {
            string name = FinalName(td);
            var buf = new StringBuilder();
            this.EmitType(buf, td, new HashSet<string>(StringComparer.Ordinal));
            string body = buf.ToString();
            modules.Add((name, body));
            foreach (Match m in Regex.Matches(body, TopLevelExportPattern))
            {
                ownerOf[m.Groups[2].Value] = (name, m.Groups[1].Value is "interface" or "type");
            }
        }

        // The root entry point (RootEvaluatorBarrelExport) dedups its name against every exported identifier —
        // a type named "Root" would itself export `evaluateRoot`, colliding with the entry name.
        this.moduleTopLevelNames.Clear();
        foreach (string id in ownerOf.Keys)
        {
            this.moduleTopLevelNames.Add(id);
        }

        var files = new List<GeneratedCodeFile>();
        var indexBarrel = new StringBuilder();
        indexBarrel.Append("// AUTO-GENERATED: barrel — re-exports every generated module (gap A1, §7.4).\n");
        foreach ((string name, string body) in modules)
        {
            // Imports this module needs from its siblings: any other module's identifier that appears in the
            // body (word-boundary, so `Kind` does not match inside `evaluateKind`). Grouped per sibling
            // module; type-only identifiers get the `type` modifier (the runtime import uses verbatim syntax).
            var perSibling = new SortedDictionary<string, (SortedSet<string> Values, SortedSet<string> Types)>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, (string Module, bool IsType)> entry in ownerOf)
            {
                if (entry.Value.Module == name || !Regex.IsMatch(body, "\\b" + Regex.Escape(entry.Key) + "\\b"))
                {
                    continue;
                }

                if (!perSibling.TryGetValue(entry.Value.Module, out (SortedSet<string> Values, SortedSet<string> Types) sets))
                {
                    sets = (new SortedSet<string>(StringComparer.Ordinal), new SortedSet<string>(StringComparer.Ordinal));
                    perSibling[entry.Value.Module] = sets;
                }

                (entry.Value.IsType ? sets.Types : sets.Values).Add(entry.Key);
            }

            var moduleSb = new StringBuilder();
            moduleSb.Append("// AUTO-GENERATED (module-per-type): ").Append(name).Append(" — idiomatic TS type + validator.\n");
            moduleSb.Append(this.RuntimeImportLine());
            foreach (KeyValuePair<string, (SortedSet<string> Values, SortedSet<string> Types)> sibling in perSibling)
            {
                IEnumerable<string> specifiers = sibling.Value.Values.Concat(sibling.Value.Types.Select(t => "type " + t));
                moduleSb.Append("import { ").Append(string.Join(", ", specifiers)).Append(" } from \"./").Append(sibling.Key).Append(".js\";\n");
            }

            moduleSb.Append('\n').Append(body);
            files.Add(new GeneratedCodeFile(name + ".ts", moduleSb.ToString()));
            indexBarrel.Append("export * from \"./").Append(name).Append(".js\";\n");
        }

        files.Add(new GeneratedCodeFile("index.ts", indexBarrel.ToString()));
        if (this.EmitsRuntimeAlongside)
        {
            files.Add(new GeneratedCodeFile("corvus-runtime.ts", RuntimeModuleSource()));
        }

        return files;
    }

    // ---- type surface ----
    private static string FinalName(TypeDeclaration td)
        => td.TryGetMetadata<string>(TsFinalKey, out string? n) && !string.IsNullOrEmpty(n) ? n! : "Entity";

    // Classify an array/tuple property for the patch `arrays` surface: returns the strongly-typed ops type
    // and the prefix length (for the rest-offset), or null if the property is not an array.
    //   list  T[]           -> ListOps<T>
    //   pure tuple [A,B,C]   -> { set?: { 0?: A; 1?: B; 2?: C } }
    //   prefix tuple [A,...C]-> { set?: { 0?: A }; rest?: ListOps<C> }  (prefixLen = 1)
    private static (string OpsType, int PrefixLen)? ClassifyArrayProp(TypeDeclaration td)
    {
        TupleTypeDeclaration? tuple = td.ExplicitTupleType();
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        bool hasRest = items is not null && items.ReducedType.LocatedSchema.Schema.ValueKind != JsonValueKind.False;
        if (tuple is not null)
        {
            var positions = tuple.ItemsTypes.ToList();
            var setProps = new StringBuilder();
            for (int i = 0; i < positions.Count; i++)
            {
                if (i > 0)
                {
                    setProps.Append("; ");
                }

                setProps.Append("readonly ").Append(i).Append("?: ").Append(TsTypeRef(positions[i].ReducedType));
            }

            string setObj = "{ readonly set?: { " + setProps + " }";
            return hasRest
                ? (setObj + "; readonly rest?: ListOps<" + TsTypeRef(items!.ReducedType) + "> }", positions.Count)
                : (setObj + " }", 0);
        }

        return hasRest ? ("ListOps<" + TsTypeRef(items!.ReducedType) + ">", 0) : null;
    }

    // Model C byte-level RMW (design §13.4): produce a new document by splicing ONLY the changed members'
    // value byte-spans into the source and copying the rest through verbatim. `changes` upserts members,
    // `removals` deletes optional members, `arrays` edits array elements in place.
    private static void EmitPatch(StringBuilder sb, TypeDeclaration td)
    {
        if (!td.HasPropertyDeclarations)
        {
            return;
        }

        string name = FinalName(td);
        var arrayProps = new List<(PropertyDeclaration P, string OpsType, int PrefixLen)>();
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            (string OpsType, int PrefixLen)? cls = ClassifyArrayProp(p.ReducedPropertyType);
            if (cls is not null)
            {
                arrayProps.Add((p, cls.Value.OpsType, cls.Value.PrefixLen));
            }
        }

        var optionalKeys = td.PropertyDeclarations
            .Where(p => p.RequiredOrOptional == RequiredOrOptional.Optional)
            .Select(p => TsEmit.Str(p.JsonPropertyName))
            .ToList();
        bool hasArrays = arrayProps.Count > 0;
        bool hasRemovals = optionalKeys.Count > 0;

        sb.Append("export function patch").Append(name).Append("(source: Uint8Array, changes: Partial<").Append(name).Append(">");
        if (hasRemovals)
        {
            sb.Append(", removals?: ReadonlyArray<").Append(string.Join(" | ", optionalKeys)).Append(">");
        }

        if (hasArrays)
        {
            sb.Append(", arrays?: ").Append(name).Append("ArrayOps");
        }

        sb.Append("): Uint8Array {\n");
        sb.Append("  const enc = new TextEncoder();\n");
        sb.Append("  const targets: RmwTarget[] = [];\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            string k = TsEmit.Str(p.JsonPropertyName);
            sb.Append("  if (changes[").Append(k).Append("] !== undefined) { targets.push({ name: enc.encode(").Append(k).Append("), content: enc.encode(JSON.stringify(changes[").Append(k).Append("])), vbs: -1, vbe: -1 }); }\n");
        }

        if (hasArrays)
        {
            sb.Append("  const arrayEdits: RmwArrayEdit[] = [];\n");
            foreach ((PropertyDeclaration p, string _, int prefixLen) in arrayProps)
            {
                string k = TsEmit.Str(p.JsonPropertyName);
                sb.Append("  if (arrays !== undefined && arrays[").Append(k).Append("] !== undefined) { arrayEdits.push({ name: enc.encode(").Append(k).Append("), ops: arrays[").Append(k).Append("]! as RmwArrayOps, prefixLen: ").Append(prefixLen).Append(" }); }\n");
            }
        }

        if (hasRemovals || hasArrays)
        {
            sb.Append("  if (");
            bool needOr = false;
            if (hasRemovals)
            {
                sb.Append("(removals !== undefined && removals.length > 0)");
                needOr = true;
            }

            if (hasArrays)
            {
                if (needOr)
                {
                    sb.Append(" || ");
                }

                sb.Append("arrayEdits.length > 0");
            }

            sb.Append(") {\n");
            sb.Append("    const removeNames: Uint8Array[] = [];\n");
            if (hasRemovals)
            {
                sb.Append("    if (removals !== undefined) { for (let ri = 0; ri < removals.length; ri++) { removeNames.push(enc.encode(String(removals[ri]))); } }\n");
            }

            sb.Append("    return rmwProduceFull(source, targets, removeNames, ").Append(hasArrays ? "arrayEdits" : "[]").Append(");\n");
            sb.Append("  }\n");
        }

        sb.Append("  return rmwUpsert(source, targets);\n");
        sb.Append("}\n\n");

        if (hasArrays)
        {
            sb.Append("export interface ").Append(name).Append("ArrayOps {\n");
            foreach ((PropertyDeclaration p, string opsType, int _) in arrayProps)
            {
                sb.Append("  readonly ").Append(Ident(p.JsonPropertyName)).Append("?: ").Append(opsType).Append(";\n");
            }

            sb.Append("}\n\n");
        }
    }

    private static void EmitBuild(StringBuilder sb, TypeDeclaration td)
    {
        if (!td.HasPropertyDeclarations)
        {
            return;
        }

        string name = FinalName(td);

        // build = bytes from a full typed T at the native serialiser's floor: one JSON.stringify + one UTF-8
        // encode (caller key order). Deterministic/canonical byte output is a SEPARATE opt-in method (a future
        // full recursive canonical-write, mirroring the C# canonicaliser) -- see design doc roadmap, not here.
        sb.Append("export function build").Append(name).Append("(props: ").Append(name).Append("): Uint8Array {\n");
        sb.Append("  return new TextEncoder().encode(JSON.stringify(props));\n");
        sb.Append("}\n\n");
    }

    // buildCanonical = deterministic/canonical bytes from a full typed T (gap F1, §5.7): RFC 8785 (JCS) via the
    // shared runtime `canonicalize` — object keys recursively sorted by UTF-16 code unit, ECMAScript number
    // forms, minimal string escaping, mirroring the C# JsonCanonicalizer. A SEPARATE opt-in method so the fast
    // `build` stays at the native floor (caller key order). For content-addressing / hashing / signatures /
    // cache keys / golden-file determinism.
    private static void EmitBuildCanonical(StringBuilder sb, TypeDeclaration td)
    {
        if (!td.HasPropertyDeclarations)
        {
            return;
        }

        string name = FinalName(td);
        sb.Append("export function buildCanonical").Append(name).Append("(props: ").Append(name).Append("): Uint8Array {\n");
        sb.Append("  return canonicalize(props);\n");
        sb.Append("}\n\n");
    }

    // V5 allOf composition (Apply): overlay one of the allOf constituent types onto the
    // document. Emitted ONLY for composition types; the argument is the union of the members.
    private static void EmitApplyTo(StringBuilder sb, TypeDeclaration td)
    {
        if (!td.HasPropertyDeclarations)
        {
            return;
        }

        var members = new List<TypeDeclaration>();
        CollectMembers(td.AllOfCompositionTypes(), members);
        if (members.Count == 0)
        {
            return;
        }

        string name = FinalName(td);
        string union = string.Join(" | ", members.Select(TsTypeRef).Distinct());
        sb.Append("export function applyTo").Append(name).Append("(source: Uint8Array, member: ").Append(union).Append("): Uint8Array {\n");
        sb.Append("  return patch").Append(name).Append("(source, member as Partial<").Append(name).Append(">);\n");
        sb.Append("}\n\n");
    }

    // The immer-style draft (design §5.7): produce<T>(source, recipe) records mutations on a typed
    // Draft<T> and lowers the change-set to a Model C byte patch (arbitrary depth). Typed per object.
    private static void EmitProduceDraft(StringBuilder sb, TypeDeclaration td)
    {
        if (!td.HasPropertyDeclarations)
        {
            return;
        }

        string name = FinalName(td);
        sb.Append("export function produce").Append(name).Append("(source: Uint8Array, recipe: (draft: Draft<").Append(name).Append(">) => void): Uint8Array {\n");
        sb.Append("  return produce<").Append(name).Append(">(source, recipe);\n");
        sb.Append("}\n\n");
    }

    // Gap B1 (defaults), APPLY half: withDefaults{T}(value) returns a shallow-clone with every ABSENT
    // (`!(key in value)`) property that declares a `default` filled with that default, and every PRESENT
    // nested object / array-of-object whose own type has defaults recursed through that type's withDefaults.
    // A present scalar/value is NEVER overridden. Emitted ONLY when T's subtree actually carries a default
    // somewhere (the optimization the task permits) — a defaults-free tree gets no function. This is a pure,
    // tree-shakeable surface helper; the validators and the shared runtime are untouched (`default` stays an
    // annotation), so the boolean/validation path is byte-stable.
    private static void EmitWithDefaults(StringBuilder sb, TypeDeclaration td)
    {
        if (!td.HasPropertyDeclarations || !SubtreeHasDefault(td, new HashSet<TypeDeclaration>()))
        {
            return;
        }

        string name = FinalName(td);
        sb.Append("export function withDefaults").Append(name).Append("(value: ").Append(name).Append("): ").Append(name).Append(" {\n");

        // Shallow clone so the caller's input is never mutated; absent defaults and recursed nested values
        // are written onto the clone. Typed `Record<string, unknown>` for index assignment, cast back on return.
        // The casts go via `unknown`: a CLOSED interface (no index signature) doesn't overlap
        // `Record<string, unknown>`, so a direct `as` is a strict-tsc error (TS2352).
        sb.Append("  const out: Record<string, unknown> = { ...(value as unknown as Record<string, unknown>) };\n");

        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            string k = TsEmit.Str(p.JsonPropertyName);

            // (a) Fill an ABSENT property that declares a default. Key-presence (`!(k in value)`), NOT
            // value-undefined: a property explicitly present-but-undefined is still "present", never overridden.
            if (TryGetPropertyDefault(p, out string? defaultJson))
            {
                sb.Append("  if (!(").Append(k).Append(" in value)) { out[").Append(k).Append("] = ").Append(defaultJson).Append("; }\n");
            }

            // (b) Recurse into a PRESENT nested object / array-of-object whose own type carries defaults, so a
            // nested default fills even when the parent property itself is supplied.
            TypeDeclaration propType = p.ReducedPropertyType;
            if (IsObject(propType) && SubtreeHasDefault(propType, new HashSet<TypeDeclaration>()))
            {
                string nested = FinalName(propType);
                sb.Append("  if (").Append(k).Append(" in value && out[").Append(k)
                  .Append("] !== null && typeof out[").Append(k).Append("] === \"object\" && !Array.isArray(out[").Append(k).Append("])) { out[").Append(k)
                  .Append("] = withDefaults").Append(nested).Append("(out[").Append(k).Append("] as ").Append(nested).Append("); }\n");
            }
            else if (ArrayElementType(propType) is TypeDeclaration elem && IsObject(elem) && SubtreeHasDefault(elem, new HashSet<TypeDeclaration>()))
            {
                string elemName = FinalName(elem);
                sb.Append("  if (").Append(k).Append(" in value && Array.isArray(out[").Append(k).Append("])) { out[").Append(k)
                  .Append("] = (out[").Append(k).Append("] as ").Append(elemName).Append("[]).map((e) => (e !== null && typeof e === \"object\" && !Array.isArray(e)) ? withDefaults")
                  .Append(elemName).Append("(e) : e); }\n");
            }
        }

        sb.Append("  return out as unknown as ").Append(name).Append(";\n");
        sb.Append("}\n\n");
    }

    // The compact-JSON form of a property's authored `default` (the SAME `UnreducedPropertyType` subschema the
    // per-property JSDoc reads, so a `$ref`+`default` site's default isn't lost to reduction), or null if the
    // property declares no default. Reparsed-and-reserialised so a multi-line authored literal is single-line.
    private static bool TryGetPropertyDefault(PropertyDeclaration p, out string? defaultJson)
    {
        defaultJson = null;
        JsonElement schema = p.UnreducedPropertyType.LocatedSchema.Schema;
        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("default", out JsonElement d))
        {
            using JsonDocument compact = JsonDocument.Parse(d.GetRawText());
            defaultJson = JsonSerializer.Serialize(compact.RootElement);
            return true;
        }

        return false;
    }

    // The reduced element TypeDeclaration of an array/tuple property (the same items pair TsArrayTypeRef uses),
    // or null when the type is not an array — used to decide whether to recurse into array-of-object elements.
    private static TypeDeclaration? ArrayElementType(TypeDeclaration td)
    {
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        return items?.ReducedType;
    }

    // True when `td` (an object) OR any nested object/array-element-object it reaches declares a `default` on a
    // property. Cycle-safe via `visited` (a recursive schema returns false on re-entry rather than looping).
    // This is what makes withDefaults{T} tree-shakeable: a type with no default anywhere in its subtree emits
    // no function, and a present-value recursion is only generated toward a subtree that can actually fill one.
    private static bool SubtreeHasDefault(TypeDeclaration td, HashSet<TypeDeclaration> visited)
    {
        if (!visited.Add(td) || !IsObject(td))
        {
            return false;
        }

        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            if (TryGetPropertyDefault(p, out _))
            {
                return true;
            }

            TypeDeclaration propType = p.ReducedPropertyType;
            if (IsObject(propType) && SubtreeHasDefault(propType, visited))
            {
                return true;
            }

            if (ArrayElementType(propType) is TypeDeclaration elem && IsObject(elem) && SubtreeHasDefault(elem, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static void EmitInterface(StringBuilder sb, TypeDeclaration td)
    {
        EmitJsDoc(sb, td.LocatedSchema.Schema, string.Empty);
        sb.Append("export interface ").Append(FinalName(td)).Append(" {\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            // The per-property JSDoc comes from the property's OWN subschema (before reduction) so it reflects
            // the annotations authored at the property site, not those of a shared/$ref'd target type.
            EmitJsDoc(sb, p.UnreducedPropertyType.LocatedSchema.Schema, "  ");
            string opt = p.RequiredOrOptional == RequiredOrOptional.Optional ? "?" : string.Empty;
            sb.Append("  readonly ").Append(Ident(p.JsonPropertyName)).Append(opt).Append(": ").Append(TsTypeRef(p.ReducedPropertyType)).Append(";\n");
        }

        sb.Append("}\n\n");
    }

    // A well-known string format (design §5.3.1): a branded alias `Brand<string, "format">` + a validating
    // factory that mints the brand only after the format check (the JS analog of V5's conversion operators).
    private static void EmitBrandAlias(StringBuilder sb, TypeDeclaration td)
    {
        string? format = td.Format();
        if (format is null)
        {
            return;
        }

        string name = FinalName(td);
        string fmt = TsEmit.Str(format);
        sb.Append("export type ").Append(name).Append(" = Brand<string, ").Append(fmt).Append(">;\n");
        sb.Append("export function from").Append(name).Append("(value: string): ").Append(name)
          .Append(" { if (!__fmt(").Append(fmt).Append(", value)) { throw new FormatError(").Append(fmt).Append("); } return value as ").Append(name).Append("; }\n");

        // Gap B2 (§5.3.1): the FOUR RFC 3339 temporal formats additionally get a `{Name}AsTemporal` accessor
        // that parses the branded string into its matching Temporal value type (date -> PlainDate, date-time ->
        // the absolute Instant, time -> PlainTime, duration -> Duration), delegating to the shared runtime
        // converter. Always emitted (not assertion-gated) — it is a pure parse helper; validation is untouched.
        if (KnownTemporalFormats.TryGetValue(format, out (string TemporalType, string Converter) temporal))
        {
            sb.Append("export function ").Append(Camel(name)).Append("ToTemporal(value: ").Append(name).Append("): Temporal.")
              .Append(temporal.TemporalType).Append(" { return ").Append(temporal.Converter).Append("(value); }\n");
        }

        sb.Append('\n');
    }

    // A sub-64-bit integer format (design §5.3.1/§4.1): a branded alias `Brand<number, "format">` + a
    // validating factory that mints the brand only after the integer-and-range check (the JS analog of the
    // string-format factory). Always emitted (not assertion-gated) — the brand is the static type surface;
    // the validator's range check is the assertion-gated counterpart.
    private static void EmitIntegerBrandAlias(StringBuilder sb, TypeDeclaration td)
    {
        string? format = td.Format();
        if (format is null || !KnownIntegerFormats.TryGetValue(format, out (long Min, long Max) range))
        {
            return;
        }

        string name = FinalName(td);
        string fmt = TsEmit.Str(format);
        string min = range.Min.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string max = range.Max.ToString(System.Globalization.CultureInfo.InvariantCulture);
        sb.Append("export type ").Append(name).Append(" = Brand<number, ").Append(fmt).Append(">;\n");
        sb.Append("export function from").Append(name).Append("(value: number): ").Append(name)
          .Append(" { if (!Number.isInteger(value) || value < ").Append(min).Append(" || value > ").Append(max)
          .Append(") { throw new FormatError(").Append(fmt).Append("); } return value as ").Append(name).Append("; }\n\n");
    }

    // A floating-point / arbitrary-precision numeric format (gap B5, §5.3.1): a branded alias
    // `Brand<number, "format">` + a validating factory. The factory range-checks (NOT integer); an unbounded
    // format (single/double) mints with no check (a pure type tag). `decimal` additionally gets the gap-B3
    // `{name}AsExact` accessor — the exact decimal digits as a string, the zero-dependency big-number seam.
    private static void EmitFloatBrandAlias(StringBuilder sb, TypeDeclaration td)
    {
        string? format = td.Format();
        if (format is null || !KnownFloatFormats.TryGetValue(format, out (double? Min, double? Max) range))
        {
            return;
        }

        string name = FinalName(td);
        string fmt = TsEmit.Str(format);
        sb.Append("export type ").Append(name).Append(" = Brand<number, ").Append(fmt).Append(">;\n");
        sb.Append("export function from").Append(name).Append("(value: number): ").Append(name);
        if (range is { Min: double min, Max: double max })
        {
            sb.Append(" { if (value < ").Append(JsNum(min)).Append(" || value > ").Append(JsNum(max))
              .Append(") { throw new FormatError(").Append(fmt).Append("); } return value as ").Append(name).Append("; }\n");
        }
        else
        {
            sb.Append(" { return value as ").Append(name).Append("; }\n");
        }

        // Gap B3 (§5.3): a `decimal` value read as a JS number loses precision beyond ~15-17 significant digits.
        // `{name}AsExact` returns the exact decimal digits as a string (when the document was parsed with the
        // runtime's `parseLossless`), the plug point for a big-number adapter (decimal.js / bignumber.js).
        if (format == "decimal")
        {
            sb.Append("export function ").Append(Camel(name)).Append("ToExact(value: ").Append(name).Append("): string { return exactNumber(value); }\n");
        }

        sb.Append('\n');
    }

    // Format a double bound as a round-trippable, JS-valid numeric literal (e.g. 65504, 7.922816251426434e+28).
    private static string JsNum(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    // A pure map / dictionary object (additionalProperties value type, no declared properties): the
    // idiomatic TS surface is `Readonly<Record<string, T>>` (design §5.3).
    private static void EmitMapAlias(StringBuilder sb, TypeDeclaration td, FallbackObjectPropertyType fb)
    {
        // An interface with an index signature, NOT `type X = Readonly<Record<...>>`: a Record-mapped-type
        // alias cannot anchor a recursive cycle (TS2456), but an interface (an object type) can.
        sb.Append("export interface ").Append(FinalName(td)).Append(" {\n  readonly [key: string]: ").Append(TsTypeRef(fb.ReducedType)).Append(";\n}\n\n");
    }

    // A oneOf/anyOf union (design §5.2): `type X = A | B`, per-member type guards (the V5
    // TryGetAs{Branch} analog, backed by the member validator), and a guard-based `matchX` (the V5
    // Match() analog; the discriminator fast-path is an optimization, not required for correctness).
    private static bool IsUnion(TypeDeclaration td)
        => (td.OneOfCompositionTypes()?.Count ?? 0) > 0 || (td.AnyOfCompositionTypes()?.Count ?? 0) > 0;

    private static void CollectMembers<TKeyword>(IReadOnlyDictionary<TKeyword, IReadOnlyCollection<TypeDeclaration>>? comp, List<TypeDeclaration> into)
        where TKeyword : notnull
    {
        if (comp is null)
        {
            return;
        }

        foreach (KeyValuePair<TKeyword, IReadOnlyCollection<TypeDeclaration>> kv in comp)
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

    private static void EmitUnionAlias(StringBuilder sb, TypeDeclaration td, HashSet<string> moduleGuards)
    {
        var members = new List<TypeDeclaration>();
        CollectMembers(td.OneOfCompositionTypes(), members);
        CollectMembers(td.AnyOfCompositionTypes(), members);
        if (members.Count == 0)
        {
            return;
        }

        string name = FinalName(td);

        // Drop a member whose ref is the union's OWN name: `type X = ... | X` is a circular alias (TS2456).
        // TS can't express a recursive union via an alias; the validator still enforces the recursive branch.
        var refs = new List<string>();
        foreach (TypeDeclaration m in members)
        {
            string r = TsTypeRef(m);
            if (r != name && !refs.Contains(r))
            {
                refs.Add(r);
            }
        }

        if (refs.Count == 0)
        {
            refs.Add("unknown");
        }

        sb.Append("export type ").Append(name).Append(" = ").Append(string.Join(" | ", refs)).Append(";\n");

        // Per-member guards + collect the match() case entries (unique camelCase keys). Guard names are
        // module-scoped (a module with several unions can share a member name, so a per-union set would
        // emit duplicate top-level `is{Member}` functions -> tsc redeclare error).
        var cases = new List<(string Key, string Ref, string Guard)>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeDeclaration m in members)
        {
            string? ev = TsEmit.EvalName(m);
            if (ev is null || TsTypeRef(m) == name)
            {
                continue;
            }

            string mName = FinalName(m);
            string guard = "is" + mName;
            int gs = 2;
            while (!moduleGuards.Add(guard))
            {
                guard = "is" + mName + gs++;
            }

            sb.Append("export function ").Append(guard).Append("(value: unknown): value is ").Append(TsTypeRef(m)).Append(" { return ").Append(ev).Append("(value, fresh()); }\n");

            string key = Camel(mName);
            int ks = 2;
            while (!usedKeys.Add(key))
            {
                key = Camel(mName) + ks++;
            }

            cases.Add((key, TsTypeRef(m), guard));
        }

        sb.Append("export function match").Append(name).Append("<R>(value: ").Append(name).Append(", cases: { ")
          .Append(string.Join("; ", cases.Select(c => c.Key + ": (v: " + c.Ref + ") => R")))
          .Append(" }): R {\n");
        foreach ((string key, _, string guard) in cases)
        {
            sb.Append("  if (").Append(guard).Append("(value)) { return cases.").Append(key).Append("(value); }\n");
        }

        sb.Append("  throw new Error(\"no ").Append(name).Append(" branch matched\");\n}\n\n");
    }

    private static void EmitEnumAlias(StringBuilder sb, TypeDeclaration td)
    {
        if (td.LocatedSchema.Schema.TryGetProperty("enum", out JsonElement en) && en.ValueKind == JsonValueKind.Array)
        {
            var members = new List<string>();
            foreach (JsonElement m in en.EnumerateArray())
            {
                // scalar enum values are literal types; object/array values can't be a literal TS type,
                // so widen to `unknown` (the validator still enforces exact deep-equal membership).
                string member = m.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
                    ? m.GetRawText()
                    : "unknown";
                if (!members.Contains(member))
                {
                    members.Add(member);
                }
            }

            // an empty enum matches nothing -> `never` (an empty union is a syntax error)
            sb.Append("export type ").Append(FinalName(td)).Append(" = ").Append(members.Count > 0 ? string.Join(" | ", members) : "never").Append(";\n\n");
        }
    }

    // Re-entrancy guard for TsArrayTypeRef (recursive array schemas). Generation is single-threaded.
    [ThreadStatic]
    private static HashSet<TypeDeclaration>? visiting;

    // The TS type reference for a (property) type: enum/object/map -> its name; const -> literal;
    // array -> typed `readonly T[]` / tuple / variadic tuple (recursive for tensors); scalar -> primitive.
    private static string TsTypeRef(TypeDeclaration td)
    {
        if (IsEnum(td))
        {
            return FinalName(td);
        }

        if (TryConstLiteral(td, out string? lit))
        {
            return lit!;
        }

        if (IsObject(td))
        {
            // Pure object -> the interface. Object as ONE branch of a multi-type (`type: ["object",
            // "string"]`) -> a union of the object-shape interface and the other branches (the same
            // drop-fix as for arrays, applied in the object path).
            List<string> objOthers = ScalarAndArrayBranches(td);
            if (objOthers.Count == 0)
            {
                return FinalName(td);
            }

            objOthers.Insert(0, FinalName(td));
            return string.Join(" | ", objOthers);
        }

        if (IsUnion(td))
        {
            return FinalName(td);
        }

        // A well-known string format -> a branded alias; a sub-64-bit integer format -> a branded number
        // alias; a 64-bit+ integer format -> bigint (§5.3.1/§4.1).
        if (IsBrandedStringFormat(td))
        {
            return FinalName(td);
        }

        if (IsBrandedIntegerFormat(td))
        {
            return FinalName(td);
        }

        if (IsBrandedFloatFormat(td))
        {
            return FinalName(td);
        }

        if (IsBigIntFormat(td))
        {
            return "bigint";
        }

        // A multi-type schema is the UNION of its per-type branches. Object is handled above (it owns the
        // generated interface); here a schema is the union of its primitive branches and its array branch.
        List<string> branches = ScalarAndArrayBranches(td);
        return branches.Count > 0 ? string.Join(" | ", branches) : "unknown";
    }

    // The non-object type branches of a (possibly multi-type) schema: each primitive kind mapped to its TS
    // primitive, plus the array branch if present. The object branch (if any) is the caller's
    // responsibility (it owns the generated interface). This is what stops a multi-type collapsing to a
    // single branch — `type: ["string","array"]` was becoming `readonly unknown[]` (dropping `string`),
    // and `type: ["object","string"]` an object-only interface (dropping `string`).
    private static List<string> ScalarAndArrayBranches(TypeDeclaration td)
    {
        List<string> kinds = SchemaTypes(td);
        bool hasArray = kinds.Contains("array") || td.ExplicitTupleType() is not null || (td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType()) is not null;

        var parts = new List<string>();
        foreach (string k in kinds)
        {
            if (k is "array" or "object")
            {
                continue;
            }

            string? p = Prim(k);
            if (p is not null && !parts.Contains(p))
            {
                parts.Add(p);
            }
        }

        if (hasArray)
        {
            string arr = ArrayRefGuarded(td);
            if (!parts.Contains(arr))
            {
                parts.Add(arr);
            }
        }

        // A `null` core type implied by a keyword other than `type` (OpenAPI 3.0 `nullable:true`) is not in
        // the literal `type` list read above, so add the `null` branch from the declaration-level aggregate.
        // This makes a nullable scalar emit `T | null` (and a nullable object `Interface | null`), matching
        // the 3.1-style `type:["string","null"]` surface and the null-accepting validator. A wholly
        // unconstrained schema reports `Any` (which includes Null) but must stay `unknown`, so only the
        // constrained-but-includes-Null case adds the branch.
        if (td.BuildComplete)
        {
            CoreTypes allowed = td.AllowedCoreTypes();
            if (allowed != CoreTypes.Any && allowed.HasFlag(CoreTypes.Null) && !parts.Contains("null"))
            {
                parts.Add("null");
            }
        }

        return parts;
    }

    // TsArrayTypeRef with the recursion guard: a recursive array schema (e.g. items: {$ref: '#'}) degrades
    // to the safe `readonly unknown[]` rather than recursing forever.
    private static string ArrayRefGuarded(TypeDeclaration td)
    {
        visiting ??= new HashSet<TypeDeclaration>();
        if (!visiting.Add(td))
        {
            return "readonly unknown[]";
        }

        try
        {
            return TsArrayTypeRef(td);
        }
        finally
        {
            visiting.Remove(td);
        }
    }

    // Array/tuple element typing (design §5.3): plain `readonly T[]`, pure tuple `readonly [A,B,C]`,
    // prefix+tail variadic tuple `readonly [A, ...T[]]`, recursive for multi-dimension (matrices/tensors).
    private static string TsArrayTypeRef(TypeDeclaration td)
    {
        TupleTypeDeclaration? tuple = td.ExplicitTupleType();
        ArrayItemsTypeDeclaration? items = td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType();
        if (tuple is not null)
        {
            var parts = new List<string>();
            foreach (var it in tuple.ItemsTypes)
            {
                parts.Add(TsTypeRef(it.ReducedType));
            }

            // prefixItems + a tail `items` schema -> variadic tuple; `items:false` (the never-type)
            // closes the tuple with no tail.
            if (items is not null && items.ReducedType.LocatedSchema.Schema.ValueKind != JsonValueKind.False)
            {
                parts.Add("..." + ElemWrap(TsTypeRef(items.ReducedType)) + "[]");
            }

            return "readonly [" + string.Join(", ", parts) + "]";
        }

        if (items is not null)
        {
            return "readonly " + ElemWrap(TsTypeRef(items.ReducedType)) + "[]";
        }

        return "readonly unknown[]";
    }

    // Parenthesise an array element type when bare `T[]` would mis-parse (unions, nested readonly arrays).
    private static string ElemWrap(string t) => t.Contains(" | ") || t.StartsWith("readonly", StringComparison.Ordinal) ? "(" + t + ")" : t;

    // Emit a property name unquoted when it is a valid TS identifier, quoted otherwise.
    private static string Ident(string name)
    {
        if (name.Length == 0)
        {
            return TsEmit.Str(name);
        }

        char c0 = name[0];
        if (!(char.IsLetter(c0) || c0 == '_' || c0 == '$'))
        {
            return TsEmit.Str(name);
        }

        foreach (char c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$'))
            {
                return TsEmit.Str(name);
            }
        }

        return name;
    }

    private static string? Prim(string t) => t switch
    {
        "string" => "string",
        "number" or "integer" => "number",
        "boolean" => "boolean",
        "null" => "null",
        _ => null,
    };

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

        // allOf/dependentSchemas merge object properties into the parent as COMPOSED declarations; a type
        // carrying them is object-like even with no direct `properties` (e.g. allOf:[{props:x},{props:y}]
        // -> `interface { x; y }`, mirroring the C# engine's merged accessors — it was falling through to
        // `unknown`). Gate on Composed (not required-only Local names) so a constraint-only schema that
        // also admits non-objects isn't mis-claimed as an object.
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

    // Well-known string formats that map to a branded string + validating factory (design §5.3.1).
    private static readonly HashSet<string> KnownStringFormats = new(StringComparer.Ordinal)
    {
        "uuid", "email", "idn-email", "hostname", "idn-hostname", "ipv4", "ipv6",
        "uri", "uri-reference", "uri-template", "iri", "iri-reference",
        "date", "date-time", "time", "duration", "json-pointer", "relative-json-pointer", "regex",
    };

    private static bool IsBrandedStringFormat(TypeDeclaration td)
        => SchemaTypes(td).Contains("string") && td.Format() is string f && KnownStringFormats.Contains(f);

    // The FOUR RFC 3339 temporal formats that, in addition to the branded string + `as{Name}` factory, get a
    // `{Name}AsTemporal` accessor parsing into a Temporal value (design §5.3.1, gap B2): the Temporal type the
    // accessor returns and the shared-runtime converter it delegates to. `date-time` -> the absolute Instant
    // (offset-preserving ZonedDateTime is a noted follow-on, not generated here).
    private static readonly IReadOnlyDictionary<string, (string TemporalType, string Converter)> KnownTemporalFormats =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["date"] = ("PlainDate", "toPlainDate"),
            ["date-time"] = ("Instant", "toInstant"),
            ["time"] = ("PlainTime", "toPlainTime"),
            ["duration"] = ("Duration", "toDuration"),
        };

    // Sub-64-bit integer formats (OpenAPI int16/int32/uint16/uint32) -> a branded `Brand<number, "fmt">` +
    // a validating factory, mirroring the string-format brand (§5.3.1). The inclusive [Min, Max] range is
    // asserted in the factory (always) and in the validator (only under format assertion). 64-bit+ formats
    // exceed JS safe-integer range and stay `bigint` (IsBigIntFormat), so they are NOT in this table.
    private static readonly IReadOnlyDictionary<string, (long Min, long Max)> KnownIntegerFormats =
        new Dictionary<string, (long, long)>(StringComparer.Ordinal)
        {
            ["byte"] = (0, 255),
            ["sbyte"] = (-128, 127),
            ["int16"] = (-32768, 32767),
            ["uint16"] = (0, 65535),
            ["int32"] = (-2147483648, 2147483647),
            ["uint32"] = (0, 4294967295),
        };

    // Floating-point / arbitrary-precision numeric formats (gap B5, mirroring the C# WellKnownNumericFormatHandler):
    // a branded `Brand<number, "fmt">` + a validating factory. The inclusive [Min, Max] range is a RANGE check
    // (NOT integer); `half` is bounded to its representable range, `single`/`double` accept any number (the C#
    // cast saturates, so they are brand-only type tags), `decimal` is bounded to the .NET decimal range. A null
    // bound means "no check on that side". `decimal` additionally gets the gap-B3 `{name}AsExact` seam.
    private static readonly IReadOnlyDictionary<string, (double? Min, double? Max)> KnownFloatFormats =
        new Dictionary<string, (double?, double?)>(StringComparer.Ordinal)
        {
            ["half"] = (-65504, 65504),
            ["single"] = (null, null),
            ["float"] = (null, null),
            ["double"] = (null, null),
            ["decimal"] = (-7.922816251426434e28, 7.922816251426434e28),
        };

    // A numeric format only brands a numeric value: an OpenAPI `format: byte` (and friends) on a STRING is a
    // different feature (base64), so an explicitly string-typed schema is never numeric-format-branded.
    private static bool IsNumericFormatBrandable(TypeDeclaration td) => !SchemaTypes(td).Contains("string");

    private static bool IsBrandedIntegerFormat(TypeDeclaration td)
        => td.Format() is string f && KnownIntegerFormats.ContainsKey(f) && IsNumericFormatBrandable(td);

    private static bool IsBrandedFloatFormat(TypeDeclaration td)
        => td.Format() is string f && KnownFloatFormats.ContainsKey(f) && IsNumericFormatBrandable(td);

    // 64-bit+ integer formats exceed JS safe-integer range -> bigint (§4.1).
    private static bool IsBigIntFormat(TypeDeclaration td)
        => td.Format() is "int64" or "uint64" or "int128" or "uint128";

    private static bool TryConstLiteral(TypeDeclaration td, out string? literal)
    {
        literal = null;
        JsonElement s = td.LocatedSchema.Schema;
        if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("const", out JsonElement c) &&
            c.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
        {
            literal = c.GetRawText();
            return true;
        }

        return false;
    }

    private static List<string> SchemaTypes(TypeDeclaration td)
    {
        var r = new List<string>();
        JsonElement s = td.LocatedSchema.Schema;
        if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("type", out JsonElement t))
        {
            if (t.ValueKind == JsonValueKind.String)
            {
                r.Add(t.GetString()!);
            }
            else if (t.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement i in t.EnumerateArray())
                {
                    if (i.ValueKind == JsonValueKind.String)
                    {
                        r.Add(i.GetString()!);
                    }
                }
            }
        }

        return r;
    }

    // PascalCase identifiers that would shadow a TS global type or a generated-runtime import; a name
    // colliding with one of these is suffixed "Entity" (mirrors the C# Formatting reserved-word pass,
    // but with TS's reserved set rather than C#'s).
    private static readonly HashSet<string> TsReserved = new(StringComparer.Ordinal)
    {
        "String", "Number", "Boolean", "Object", "Array", "Function", "Symbol", "Date", "RegExp",
        "Error", "Map", "Set", "Promise", "BigInt", "JSON", "Math", "Proxy", "WeakMap", "WeakSet",
        "Uint8Array", "Partial", "Record", "ReadonlyArray", "Readonly",
        "Ev", "NOEV", "Draft", "RmwTarget", "ListOps", "RmwArrayOps", "RmwArrayEdit", "Brand", "FormatError",

        // "Rmw" is reserved because the generated array-ops interface is `{Name}ArrayOps` — a type named
        // "Rmw" would derive "RmwArrayOps", colliding with the imported runtime type of that name.
        "Rmw",
    };

    // The TS analog of the C# NameHeuristic chain (§ NameHeuristics): a faithful port of the
    // load-bearing heuristics — BaseSchema (root/$defs → fragment key or path basename),
    // Documentation (title, but ONLY when short and usable), and Subschema (the fragment segment a
    // nested type sits under). This is what turns `definitions/cypressConfig` into `CypressConfig`
    // and a long descriptive `title` into a path-derived name instead of a sentence-long identifier.
    private static string NameForType(TypeDeclaration td, Dictionary<TypeDeclaration, string> propertyName)
    {
        // The TS provider does not track parent relationships (GetParent is a stub), so root-vs-nested
        // is read off the location instead of Parent(): a $defs member via IsInDefinitionsContainer
        // (core), the document root via the absence of a fragment, everything else nested.
        JsonReferenceBuilder reference = JsonReferenceBuilder.From(td.LocatedSchema.Location.ToString());
        string? raw;
        if (td.IsInDefinitionsContainer())
        {
            // BaseSchemaNameHeuristic: a $defs member is named from its key (the last fragment
            // segment), NEVER the title (the C# Documentation heuristic excludes $defs).
            raw = NameFromReference(reference, allowPath: false) ?? TitleIfShort(td);
        }
        else if (!reference.HasFragment)
        {
            // The document root: a short usable title, else the path basename, else a generic fallback.
            // (Title-first here is a deliberate divergence from C#'s path-first root naming: a short
            // `title` like "Person" reads better than a generic filename, and a long unusable title
            // still falls through to the path — which is what defuses sentence-long root names.)
            raw = TitleIfShort(td) ?? NameFromReference(reference, allowPath: true) ?? "Schema";
        }
        else
        {
            // A nested subschema: a short usable title, else the fragment segment it sits under, else
            // the property it is the value of.
            raw = TitleIfShort(td)
                ?? NameFromReference(reference, allowPath: false)
                ?? (propertyName.TryGetValue(td, out string? pn) ? Pascal(pn) : null);
        }

        string name = raw ?? string.Empty;
        if (name.Length == 0)
        {
            return "Entity";
        }

        return TsReserved.Contains(name) ? name + "Entity" : name;
    }

    // DocumentationNameHeuristic: use the `title` ONLY when it is short (< 64 chars) and pascalises to
    // a usable identifier (2..63 chars). A long descriptive title is rejected — that is exactly what
    // stops a sentence-long `title` becoming the type name.
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

    // BaseSchema/Subschema/Path: derive a name from the type's decomposed location — the last fragment
    // segment (skipping a trailing numeric index, e.g. `allOf/0` → `AllOf`), else (root only) the
    // document path's basename without its extension.
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

    // ---- validators: composed from the core handler registry (§13.13) ----
    private void EmitValidator(StringBuilder sb, TypeDeclaration td)
    {
        // `il` is the instance-location JSON Pointer (built only when collecting; see §15). `ok` accumulates
        // the verdict: on the boolean hot path a failing check `return false`s immediately (ev.r === null);
        // when collecting it records via ev.r and sets ok = false, falling through to gather every failure.
        sb.Append("export function evaluate").Append(FinalName(td)).Append("(value: unknown, ev: Ev, il: string = \"\", kl: string = \"\", r: Results | null = null): boolean {\n");

        // boolean schemas: `false` matches nothing, `true` matches everything.
        if (td.LocatedSchema.Schema.ValueKind == JsonValueKind.False)
        {
            sb.Append("  if (r !== null) { ").Append(TsEmit.FailRecord(td, null)).Append(" } return false;\n}\n\n");
            return;
        }

        sb.Append("  let ok = true;\n");

        var steps = new List<(uint Priority, ITsKeywordEmitter Emitter, IKeyword Keyword)>();
        foreach (IKeyword keyword in td.Keywords())
        {
            if (this.validationHandlers.TryGetHandlersFor(keyword, out IReadOnlyCollection<IKeywordValidationHandler>? handlers))
            {
                foreach (IKeywordValidationHandler handler in handlers)
                {
                    if (handler is ITsKeywordEmitter emitter)
                    {
                        steps.Add((handler.ValidationHandlerPriority, emitter, keyword));
                    }
                }
            }
        }

        foreach ((uint _, ITsKeywordEmitter emitter, IKeyword keyword) in steps.OrderBy(s => s.Priority))
        {
            emitter.Emit(sb, td, keyword);
        }

        EmitAnnotations(sb, td);
        sb.Append("  return ok;\n}\n\n");
    }

    // Verbose mode (§15 inc 5): a SUCCESSFULLY validated subschema contributes its annotation keywords
    // (title/description/default/examples/readOnly/writeOnly/deprecated/format) at the current instance
    // location. Gated on `r !== null && r.verbose && ok` so boolean and detailed modes pay nothing — the
    // values are emitted as inline JSON literals, referenced only inside that branch.
    private static readonly string[] AnnotationKeywords = ["title", "description", "default", "examples", "readOnly", "writeOnly", "deprecated", "format"];

    private static void EmitAnnotations(StringBuilder sb, TypeDeclaration td)
    {
        JsonElement schema = td.LocatedSchema.Schema;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var present = new List<(string Keyword, string ValueJson)>();
        foreach (string kw in AnnotationKeywords)
        {
            if (schema.TryGetProperty(kw, out JsonElement v))
            {
                present.Add((kw, v.GetRawText()));
            }
        }

        if (present.Count == 0)
        {
            return;
        }

        sb.Append("  if (r !== null && r.verbose && ok) {");
        foreach ((string kw, string valueJson) in present)
        {
            sb.Append(" r.annotate(").Append(TsEmit.Str(kw)).Append(", ").Append(valueJson)
              .Append(", kl + ").Append(TsEmit.Str("/" + kw)).Append(", il, ").Append(TsEmit.Str(TsEmit.AbsoluteKeywordLocation(td, kw))).Append(");");
        }

        sb.Append(" }\n");
    }

    // The TS-surface analog of EmitAnnotations: a `/** ... */` JSDoc block emitted into the generated module
    // (always on, comment-only — the shared runtime and validators are untouched) from the SAME annotation
    // keywords EmitAnnotations reads off the schema (title/description for the body; deprecated/examples/
    // readOnly/writeOnly as tags). `indent` is the leading whitespace of the declaration the block sits above
    // (string.Empty for a top-level interface, "  " for a property), so the comment matches the file's style.
    private static void EmitJsDoc(StringBuilder sb, JsonElement schema, string indent)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var lines = new List<string>();

        if (schema.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(title.GetString()))
        {
            lines.Add(title.GetString()!);
        }

        if (schema.TryGetProperty("description", out JsonElement description) && description.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(description.GetString()))
        {
            lines.Add(description.GetString()!);
        }

        if (schema.TryGetProperty("readOnly", out JsonElement readOnly) && readOnly.ValueKind == JsonValueKind.True)
        {
            lines.Add("@readonly");
        }

        if (schema.TryGetProperty("writeOnly", out JsonElement writeOnly) && writeOnly.ValueKind == JsonValueKind.True)
        {
            lines.Add("@writeonly");
        }

        if (schema.TryGetProperty("deprecated", out JsonElement deprecated) && deprecated.ValueKind == JsonValueKind.True)
        {
            lines.Add("@deprecated");
        }

        if (schema.TryGetProperty("examples", out JsonElement examples) && examples.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement example in examples.EnumerateArray())
            {
                // One @example per entry, the value as compact (single-line) JSON; GetRawText() preserves the
                // authored literal, and JsonDocument re-parse strips the whitespace a multi-line literal carries.
                using JsonDocument compact = JsonDocument.Parse(example.GetRawText());
                lines.Add("@example " + JsonSerializer.Serialize(compact.RootElement));
            }
        }

        if (schema.TryGetProperty("default", out JsonElement defaultValue))
        {
            // @default carries the schema's default value as compact (single-line) JSON — the same
            // GetRawText()-then-reparse compaction as @example, so a multi-line authored literal collapses
            // to one tag line. This is annotation-only documentation; the matching withDefaults{T} applies it.
            using JsonDocument compact = JsonDocument.Parse(defaultValue.GetRawText());
            lines.Add("@default " + JsonSerializer.Serialize(compact.RootElement));
        }

        if (lines.Count == 0)
        {
            return;
        }

        sb.Append(indent).Append("/**\n");
        foreach (string line in lines)
        {
            // Neutralise any `*/` (would close the block early) and normalise CR/CRLF to LF, then split so every
            // physical line of a multi-line title/description/example becomes its own ` * ` continuation line.
            string sanitised = line
                .Replace("*/", "* /", StringComparison.Ordinal)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            foreach (string physical in sanitised.Split('\n'))
            {
                sb.Append(indent).Append(" * ").Append(physical).Append('\n');
            }
        }

        sb.Append(indent).Append(" */\n");
    }

    private static string Pascal(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        bool upperNext = true;
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        if (sb.Length > 0 && char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }

    // camelCase (for match() case keys): Pascal with a lowercased first letter.
    private static string Camel(string value)
    {
        string p = Pascal(value);
        return p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..];
    }
}