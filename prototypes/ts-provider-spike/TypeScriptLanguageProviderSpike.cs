using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// Phase 0 provider: idiomatic type surface (real names + built-in scalar mapping + property
// type-references + named enum unions) on top of registry-driven, extensible validators (§13.13).
//
// Emission per type: objects -> `interface`; enums -> `type X = a | b`; scalars/arrays -> no type
// declaration (referenced inline as the primitive / element type). EVERY type still gets an
// `evaluate{Name}` validator composed from the core handler registry, so recursion always resolves.
public sealed class TypeScriptLanguageProviderSpike : IHierarchicalLanguageProvider
{
    private const string TsFinalKey = "Ts_FinalName";

    // JSON deep-equality (object keys order-independent; arrays ordered) for const/enum/uniqueItems.
    // (In production this lives in @corvus/json-runtime; numeric equality is exact per §4.1.)
    private const string DeepEqual =
        "function __eq(a: unknown, b: unknown): boolean {\n" +
        "  if (a === b) { return true; }\n" +
        "  if (typeof a !== typeof b || a === null || b === null) { return false; }\n" +
        "  if (Array.isArray(a)) { if (!Array.isArray(b) || a.length !== b.length) { return false; } for (let i = 0; i < a.length; i++) { if (!__eq(a[i], b[i])) { return false; } } return true; }\n" +
        "  if (typeof a === \"object\") { if (Array.isArray(b) || typeof b !== \"object\") { return false; } const ak = Object.keys(a as object), bk = Object.keys(b as object); if (ak.length !== bk.length) { return false; } for (const k of ak) { if (!Object.prototype.hasOwnProperty.call(b, k) || !__eq((a as Record<string, unknown>)[k], (b as Record<string, unknown>)[k])) { return false; } } return true; }\n" +
        "  return false;\n}\n\n";

    // Evaluation tracker for unevaluatedProperties/Items: an index bitmask over property/item positions
    // (first 32 positions inline as a number — zero extra allocation; production widens to Uint32Array per
    // §13.18). `local` (this schema's own evaluations) + `applied` (OR-merged from matched in-place
    // applicators). NOEV is the shared no-op tracker passed to sub-instances that do not require tracking.
    private const string EvRuntime =
        "class Ev {\n" +
        "  n = false; pl = 0; pa = 0; il = 0; ia = 0;\n" +
        "  markProp(i: number): void { if (!this.n && i < 32) { this.pl |= (1 << i) >>> 0; } }\n" +
        "  hasProp(i: number): boolean { return i < 32 ? (((this.pl | this.pa) & ((1 << i) >>> 0)) !== 0) : false; }\n" +
        "  markItem(i: number): void { if (!this.n && i < 32) { this.il |= (1 << i) >>> 0; } }\n" +
        "  hasItem(i: number): boolean { return i < 32 ? (((this.il | this.ia) & ((1 << i) >>> 0)) !== 0) : false; }\n" +
        "  mergeProps(c: Ev): void { this.pa |= c.pl | c.pa; }\n" +
        "  mergeItems(c: Ev): void { this.ia |= c.il | c.ia; }\n" +
        "}\n" +
        "const NOEV = new Ev(); NOEV.n = true;\n" +
        "function fresh(): Ev { return new Ev(); }\n\n";

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();

    public static TypeScriptLanguageProviderSpike CreateDefault()
    {
        var p = new TypeScriptLanguageProviderSpike();
        p.RegisterValidationHandlers(
            new TsTypeHandler(), new TsConstantBoundHandler(), new TsMembershipHandler(), new TsUniqueItemsHandler(),
            new TsRegexHandler(), new TsPropertiesHandler(), new TsRequiredHandler(),
            new TsPatternPropertiesHandler(), new TsAdditionalPropertiesHandler(),
            new TsAllOfHandler(), new TsAnyOfHandler(), new TsOneOfHandler(),
            new TsPrefixItemsHandler(), new TsItemsHandler(),
            new TsPropertyNamesHandler(), new TsDependentRequiredHandler(), new TsIfThenElseHandler(),
            new TsUnevaluatedPropertiesHandler(), new TsUnevaluatedItemsHandler());
        return p;
    }

    public ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers)
    {
        this.validationHandlers.RegisterValidationHandlers(handlers);
        return this;
    }

    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders) => this;

    public ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics) => this;

    public bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
        => this.validationHandlers.TryGetHandlersFor(keyword, out validationHandlers);

    public bool ShouldGenerate(TypeDeclaration type) => true;

    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
    }

    public void SetParent(TypeDeclaration child, TypeDeclaration? parent)
    {
    }

    public TypeDeclaration? GetParent(TypeDeclaration child) => null;

    public IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration) => [];

    public void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName, CancellationToken cancellationToken)
    {
    }

    public void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, IEnumerable<TypeDeclaration> existingTypeDeclarations, CancellationToken cancellationToken)
    {
    }

    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, CancellationToken cancellationToken)
    {
        List<TypeDeclaration> types = typeDeclarations.ToList();

        // Pass 0: a minimal name heuristic — a type is named after its `title`, else the property it
        // is the value of, else a generic fallback.
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
            string baseName = Title(td) ?? (propertyName.TryGetValue(td, out string? pn) ? Pascal(pn) : "Entity");
            string name = baseName;
            int suffix = 2;
            while (!used.Add(name))
            {
                name = baseName + suffix++;
            }

            td.SetMetadata(TsFinalKey, name);
        }

        // Pass 2: emit the type surface + validators.
        var sb = new StringBuilder();
        sb.Append("// AUTO-GENERATED: idiomatic TS types + registry-composed validators.\n\n");
        sb.Append(DeepEqual);
        sb.Append(EvRuntime);
        foreach (TypeDeclaration td in types)
        {
            if (IsObject(td))
            {
                EmitInterface(sb, td);
            }
            else if (IsEnum(td))
            {
                EmitEnumAlias(sb, td);
            }

            EmitValidator(sb, td);
        }

        return new[] { new GeneratedCodeFile("generated.ts", sb.ToString()) };
    }

    // ---- type surface ----
    private static string FinalName(TypeDeclaration td)
        => td.TryGetMetadata<string>(TsFinalKey, out string? n) && !string.IsNullOrEmpty(n) ? n! : "Entity";

    private static void EmitInterface(StringBuilder sb, TypeDeclaration td)
    {
        sb.Append("export interface ").Append(FinalName(td)).Append(" {\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            string opt = p.RequiredOrOptional == RequiredOrOptional.Optional ? "?" : string.Empty;
            sb.Append("  readonly ").Append(Ident(p.JsonPropertyName)).Append(opt).Append(": ").Append(TsTypeRef(p.ReducedPropertyType)).Append(";\n");
        }

        sb.Append("}\n\n");
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

    // The TS type reference for a (property) type: enum/object -> its name; const -> literal; array ->
    // readonly T[] (element typing is Phase 1); scalar -> primitive; else unknown.
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
            return FinalName(td);
        }

        List<string> kinds = SchemaTypes(td);
        if (kinds.Contains("array"))
        {
            return "readonly unknown[]";
        }

        var prims = new List<string>();
        foreach (string k in kinds)
        {
            string? p = Prim(k);
            if (p is not null && !prims.Contains(p))
            {
                prims.Add(p);
            }
        }

        return prims.Count > 0 ? string.Join(" | ", prims) : "unknown";
    }

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

        return SchemaTypes(td).Contains("object");
    }

    private static bool IsEnum(TypeDeclaration td)
        => td.LocatedSchema.Schema.ValueKind == JsonValueKind.Object && td.LocatedSchema.Schema.TryGetProperty("enum", out _);

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

    private static string? Title(TypeDeclaration td)
    {
        JsonElement s = td.LocatedSchema.Schema;
        if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("title", out JsonElement t) && t.ValueKind == JsonValueKind.String)
        {
            string p = Pascal(t.GetString()!);
            return p.Length > 0 ? p : null;
        }

        return null;
    }

    // ---- validators: composed from the core handler registry (§13.13) ----
    private void EmitValidator(StringBuilder sb, TypeDeclaration td)
    {
        sb.Append("export function evaluate").Append(FinalName(td)).Append("(value: unknown, ev: Ev): boolean {\n");

        // boolean schemas: `false` matches nothing, `true` matches everything.
        if (td.LocatedSchema.Schema.ValueKind == JsonValueKind.False)
        {
            sb.Append("  return false;\n}\n\n");
            return;
        }

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

        sb.Append("  return true;\n}\n\n");
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
}
