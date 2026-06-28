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

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();

    // Every top-level identifier emitted into the current module, so the root entry-point name can be
    // reserved against them and renamed around any collision (like type and union-guard names).
    private readonly HashSet<string> moduleTopLevelNames = new(StringComparer.Ordinal);

    // Where generated modules import the shared runtime from. A relative specifier ("./corvus-runtime.js")
    // means re-emit the runtime alongside the module (self-contained); a bare specifier
    // ("@endjin/corvus-json-runtime") means import the installed package and DON'T re-emit it.
    private string runtimeModuleSpecifier = "./corvus-runtime.js";

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

        // The root entry point is an EVALUATOR (it will gain an optional results collector, like the .NET
        // EvaluateSchema), so it is named `evaluateRoot` — consistent with the per-type evaluate{Type} and
        // the suite/spike harnesses, NOT `validate`. It seeds a fresh evaluation tracker so callers don't
        // pass one for a whole-document check. (Dedup guards the rare root-type-named-"Root" collision.)
        string entry = "evaluateRoot";
        for (int i = 2; this.moduleTopLevelNames.Contains(entry); i++)
        {
            entry = "evaluateRoot" + i;
        }

        this.moduleTopLevelNames.Add(entry);
        return $"\nexport const {entry} = (v: unknown, results?: Results): boolean => evaluate{rootName}(v, fresh(), \"\", \"\", results ?? null);\nexport default {entry};\n";
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
    public sealed record Options(bool AlwaysAssertFormat = false, string RuntimeModuleSpecifier = "./corvus-runtime.js");

    /// <summary>
    /// The driver-facing entry point, mirroring <c>CSharpLanguageProvider.DefaultWithOptions</c>.
    /// </summary>
    /// <param name="options">The options configuring the provider.</param>
    /// <returns>A configured <see cref="TypeScriptLanguageProvider"/>.</returns>
    public static TypeScriptLanguageProvider DefaultWithOptions(Options options)
    {
        TypeScriptLanguageProvider provider = options.AlwaysAssertFormat ? CreateWithFormatAssertion() : CreateDefault();
        provider.runtimeModuleSpecifier = options.RuntimeModuleSpecifier;
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

        // Pass 0: record, for each type, the property it is the value of — a fallback name source for
        // nested types that the reference/title heuristics can't name (see NameForType).
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

        // Pass 1: assign final, unique TS names.
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

        // Pass 2: emit the type surface + validators. The runtime helpers live in ONE shared module
        // (@endjin/corvus-json-runtime, §5.5) that every generated module imports — never inlined per module.
        var sb = new StringBuilder();
        sb.Append("// AUTO-GENERATED: idiomatic TS types + registry-composed validators.\n");
        sb.Append("import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, __ptr, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand, Results } from \"").Append(this.runtimeModuleSpecifier).Append("\";\n\n");
        var moduleGuards = new HashSet<string>(StringComparer.Ordinal); // union guard names, unique per module
        foreach (TypeDeclaration td in types)
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
                    EmitApplyTo(sb, td);
                    EmitProduceDraft(sb, td);
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

            EmitValidator(sb, td);
        }

        string content = sb.ToString();

        // Record every emitted top-level identifier so the root entry-point name (RootEvaluatorExport) can
        // be reserved against them and renamed around any collision.
        this.moduleTopLevelNames.Clear();
        foreach (Match m in Regex.Matches(content, "(?m)^export (?:interface|type|class|function|const) ([A-Za-z_$][A-Za-z0-9_$]*)"))
        {
            this.moduleTopLevelNames.Add(m.Groups[1].Value);
        }

        // The generated types/validators module. The shared runtime is re-emitted alongside it ONLY when
        // the import specifier is relative (self-contained mode); a bare specifier means the consumer
        // installs @endjin/corvus-json-runtime and we don't duplicate it. (The test harness consumes files.First()
        // and emits its own single shared runtime for the whole suite.)
        var files = new List<GeneratedCodeFile> { new("generated.ts", content) };
        if (this.runtimeModuleSpecifier.StartsWith("./", StringComparison.Ordinal) ||
            this.runtimeModuleSpecifier.StartsWith("../", StringComparison.Ordinal))
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
        sb.Append("export function as").Append(name).Append("(value: string): ").Append(name)
          .Append(" { if (!__fmt(").Append(fmt).Append(", value)) { throw new FormatError(").Append(fmt).Append("); } return value as ").Append(name).Append("; }\n\n");
    }

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

        // A well-known string format -> a branded alias; a 64-bit+ integer format -> bigint (§5.3.1/§4.1).
        if (IsBrandedStringFormat(td))
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

        string loc = td.LocatedSchema.Location.ToString();
        sb.Append("  if (r !== null && r.verbose && ok) {");
        foreach ((string kw, string valueJson) in present)
        {
            sb.Append(" r.annotate(").Append(TsEmit.Str(kw)).Append(", ").Append(valueJson)
              .Append(", kl + ").Append(TsEmit.Str("/" + kw)).Append(", il, ").Append(TsEmit.Str(loc + "/" + kw)).Append(");");
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