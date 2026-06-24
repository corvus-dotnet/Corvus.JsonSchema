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

    // Instances are parsed with lossless-json so JSON numbers arrive as LosslessNumber, retaining their
    // exact SOURCE TEXT (design §4.1) — String(value) is the source digits, so numeric validation is exact
    // (incl. values beyond IEEE-754 range). Kind tests are centralised here because a LosslessNumber is a
    // JS object and would otherwise collide with the object check.
    private const string KindRuntime =
        "import { isLosslessNumber } from \"lossless-json\";\n" +
        "const __isNum = (v: unknown): boolean => isLosslessNumber(v) || typeof v === \"number\";\n" +
        "const __isObj = (v: unknown): v is Record<string, unknown> => typeof v === \"object\" && v !== null && !Array.isArray(v) && !isLosslessNumber(v);\n" +
        "const __isInt = (s: string): boolean => { const d = __dec(s); return d.exp >= 0 || d.mant % (10n ** BigInt(-d.exp)) === 0n; };\n\n";

    // JSON deep-equality (object keys order-independent; arrays ordered; numbers compared on exact text)
    // for const/enum/uniqueItems. (In production this lives in @corvus/json-runtime.)
    private const string DeepEqual =
        "function __eq(a: unknown, b: unknown): boolean {\n" +
        "  if (__isNum(a) || __isNum(b)) { return __isNum(a) && __isNum(b) && __cmp(String(a), String(b)) === 0; }\n" +
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
        "  mergeProps(c: Ev): void { if (!this.n) { this.pa |= c.pl | c.pa; } }\n" +
        "  mergeItems(c: Ev): void { if (!this.n) { this.ia |= c.il | c.ia; } }\n" +
        "}\n" +
        "const NOEV = new Ev(); NOEV.n = true;\n" +
        "function fresh(): Ev { return new Ev(); }\n\n";

    // Exact numeric validation on the number's TEXT, not the lossy JS double (design §4.1, mirroring the
    // C# BigNumber/ASCII-number approach; ported from prototypes/number-exact.mjs). Parse a decimal literal
    // to sign * mantissa * 10^exp (BigInt) and do exact arithmetic: bounds via cross-scaled BigInt compare,
    // multipleOf via scaled BigInt modulo. The schema operand uses its exact source literal; the instance
    // value uses String(value) (which round-trips JSON numbers; production retains the true source token).
    private const string NumericRuntime =
        "function __dec(s: string): { sign: bigint; mant: bigint; exp: number } {\n" +
        "  let i = 0; let sign = 1n;\n" +
        "  if (s[i] === \"+\") { i++; } else if (s[i] === \"-\") { sign = -1n; i++; }\n" +
        "  let ip = \"\", fp = \"\", ep = \"\";\n" +
        "  while (i < s.length && s[i] >= \"0\" && s[i] <= \"9\") { ip += s[i++]; }\n" +
        "  if (s[i] === \".\") { i++; while (i < s.length && s[i] >= \"0\" && s[i] <= \"9\") { fp += s[i++]; } }\n" +
        "  if (s[i] === \"e\" || s[i] === \"E\") { i++; let es = \"\"; if (s[i] === \"+\" || s[i] === \"-\") { es = s[i++]; } while (i < s.length && s[i] >= \"0\" && s[i] <= \"9\") { es += s[i++]; } ep = es; }\n" +
        "  return { sign, mant: BigInt((ip + fp) || \"0\"), exp: (ep ? parseInt(ep, 10) : 0) - fp.length };\n" +
        "}\n" +
        "function __cmp(aStr: string, bStr: string): number {\n" +
        "  const a = __dec(aStr), b = __dec(bStr); const e = Math.min(a.exp, b.exp);\n" +
        "  const ai = a.sign * a.mant * 10n ** BigInt(a.exp - e); const bi = b.sign * b.mant * 10n ** BigInt(b.exp - e);\n" +
        "  return ai < bi ? -1 : ai > bi ? 1 : 0;\n" +
        "}\n" +
        "function __multipleOf(vStr: string, dStr: string): boolean {\n" +
        "  const v = __dec(vStr), d = __dec(dStr); if (d.mant === 0n) { return false; }\n" +
        "  const e = Math.min(v.exp, d.exp);\n" +
        "  return (v.mant * 10n ** BigInt(v.exp - e)) % (d.mant * 10n ** BigInt(d.exp - e)) === 0n;\n" +
        "}\n\n";

    // Format assertion runtime (design §5.5): one validator per standard format. `format` only constrains
    // strings (non-strings are always valid). date/time/date-time/duration use RFC 3339 semantics; the rest
    // are regex/parse. __fmt(name, s) returns true for unknown formats (unknown formats are not asserted).
    private const string FormatRuntime = """
        const __dim = (y: number, m: number): number => m === 2 ? (((y % 4 === 0 && y % 100 !== 0) || y % 400 === 0) ? 29 : 28) : [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][m - 1];
        function __fmtDate(s: string): boolean {
          const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
          if (m === null) { return false; }
          const mo = Number(m[2]), d = Number(m[3]);
          return mo >= 1 && mo <= 12 && d >= 1 && d <= __dim(Number(m[1]), mo);
        }
        function __fmtTime(s: string): boolean {
          const m = /^(\d{2}):(\d{2}):(\d{2})(\.\d+)?([zZ]|[+-]\d{2}:\d{2})$/.exec(s);
          if (m === null) { return false; }
          const hh = Number(m[1]), mm = Number(m[2]), ss = Number(m[3]);
          let offMin = 0; const off = m[5];
          if (off !== "z" && off !== "Z") {
            const om = /^([+-])(\d{2}):(\d{2})$/.exec(off);
            if (om === null || Number(om[2]) > 23 || Number(om[3]) > 59) { return false; }
            offMin = (om[1] === "-" ? -1 : 1) * (Number(om[2]) * 60 + Number(om[3]));
          }
          if (hh > 23 || mm > 59) { return false; }
          if (ss > 59) {
            if (ss !== 60) { return false; }
            const utc = ((((hh * 60 + mm) - offMin) % 1440) + 1440) % 1440;
            if (utc !== 23 * 60 + 59) { return false; }
          }
          return true;
        }
        function __fmtDateTime(s: string): boolean {
          const m = /^(\d{4}-\d{2}-\d{2})[tT](\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[zZ]|[+-]\d{2}:\d{2}))$/.exec(s);
          return m !== null && __fmtDate(m[1]) && __fmtTime(m[2]);
        }
        function __fmtDuration(s: string): boolean {
          if (s === "P" || !s.startsWith("P")) { return false; }
          if (/^P\d+W$/.test(s)) { return true; }
          const m = /^P(\d+Y)?(\d+M)?(\d+D)?(T(\d+H)?(\d+M)?(\d+S)?)?$/.exec(s);
          if (m === null) { return false; }
          if (!m[1] && !m[2] && !m[3] && !m[4]) { return false; }
          if (m[4] && !m[5] && !m[6] && !m[7]) { return false; }
          return true;
        }
        const __ipv4Re = /^((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$/;
        function __fmtIpv6(s: string): boolean {
          if (s.indexOf(":") < 0) { return false; }
          const parts = s.split("::");
          if (parts.length > 2) { return false; }
          const head = parts[0] === "" ? [] : parts[0].split(":");
          let tail = parts.length === 2 ? (parts[1] === "" ? [] : parts[1].split(":")) : [];
          const all = parts.length === 2 ? head.concat(tail) : head;
          let count = all.length; let v4 = false;
          if (all.length > 0 && all[all.length - 1].indexOf(".") >= 0) {
            if (!__ipv4Re.test(all[all.length - 1])) { return false; }
            v4 = true; count += 1;
          }
          for (let i = 0; i < all.length; i++) {
            if (v4 && i === all.length - 1) { continue; }
            if (!/^[0-9a-fA-F]{1,4}$/.test(all[i])) { return false; }
          }
          return parts.length === 2 ? count <= 7 : count === 8;
        }
        const __uuidRe = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
        const __hostnameRe = /^(?=.{1,253}$)([a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)(\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$/;
        const __jsonPtrRe = /^(?:\/(?:[^~/]|~0|~1)*)*$/;
        const __relJsonPtrRe = /^(?:0|[1-9][0-9]*)(?:#|(?:\/(?:[^~/]|~0|~1)*)*)$/;
        // RFC 3986: scheme then only unreserved/reserved chars or %XX (rejects raw spaces/non-ASCII and bad %).
        const __uriRe = /^[A-Za-z][A-Za-z0-9+.-]*:(?:%[0-9A-Fa-f]{2}|[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=])*$/;
        const __uriRefRe = /^(?:[A-Za-z][A-Za-z0-9+.-]*:)?(?:%[0-9A-Fa-f]{2}|[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=])*$/;
        // RFC 3987 IRI: like URI but additionally allows non-ASCII (ucschar).
        const __iriRe = /^[A-Za-z][A-Za-z0-9+.-]*:(?:%[0-9A-Fa-f]{2}|[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;= -￿])*$/u;
        const __iriRefRe = /^(?:[A-Za-z][A-Za-z0-9+.-]*:)?(?:%[0-9A-Fa-f]{2}|[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;= -￿])*$/u;
        const __uriTemplateRe = /^(?:[^\s{}]|\{[^\s{}]*\})*$/;
        const __emailLocalRe = /^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*$/;
        function __fmtEmail(s: string): boolean {
          const at = s.lastIndexOf("@");
          if (at < 1 || at >= s.length - 1) { return false; }
          const local = s.slice(0, at), domain = s.slice(at + 1);
          if (local.startsWith('"') && local.endsWith('"')) { if (local.length < 2) { return false; } }
          else if (!__emailLocalRe.test(local)) { return false; }
          if (domain.startsWith("[") && domain.endsWith("]")) {
            const ip = domain.slice(1, -1);
            return ip.startsWith("IPv6:") ? __fmtIpv6(ip.slice(5)) : __ipv4Re.test(ip);
          }
          return __hostnameRe.test(domain);
        }
        function __fmtRegex(s: string): boolean { try { new RegExp(s, "u"); return true; } catch { return false; } }
        function __fmt(name: string, s: string): boolean {
          switch (name) {
            case "date": return __fmtDate(s);
            case "date-time": return __fmtDateTime(s);
            case "time": return __fmtTime(s);
            case "duration": return __fmtDuration(s);
            case "email": case "idn-email": return __fmtEmail(s);
            case "hostname": case "idn-hostname": return __hostnameRe.test(s);
            case "ipv4": return __ipv4Re.test(s);
            case "ipv6": return __fmtIpv6(s);
            case "uuid": return __uuidRe.test(s);
            case "uri": return __uriRe.test(s);
            case "iri": return __iriRe.test(s);
            case "uri-reference": return __uriRefRe.test(s);
            case "iri-reference": return __iriRefRe.test(s);
            case "uri-template": return __uriTemplateRe.test(s);
            case "json-pointer": return __jsonPtrRe.test(s);
            case "relative-json-pointer": return __relJsonPtrRe.test(s);
            case "regex": return __fmtRegex(s);
            default: return true;
          }
        }

        """;

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();

    private bool assertFormat;

    // The shared runtime module (@corvus/json-runtime in production): emitted ONCE, imported by every
    // generated module. Assembled from the helper blocks with each top-level declaration exported, and
    // the third-party imports (lossless-json for source-text numbers, Temporal + tr46 for formats) at top.
    public static string RuntimeModuleSource()
    {
        string body = NumericRuntime + KindRuntime + DeepEqual + EvRuntime + FormatRuntime;
        body = body.Replace("import { isLosslessNumber } from \"lossless-json\";\n", string.Empty);
        body = body.Replace("\nfunction ", "\nexport function ")
                   .Replace("\nconst ", "\nexport const ")
                   .Replace("\nclass ", "\nexport class ");
        if (body.StartsWith("function ", StringComparison.Ordinal))
        {
            body = "export " + body;
        }

        return "import { isLosslessNumber } from \"lossless-json\";\n" +
               "import { Temporal } from \"@js-temporal/polyfill\";\n" +
               "import tr46 from \"tr46\";\n\n" + body;
    }

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
            new TsDependentSchemasHandler(), new TsContainsHandler(), new TsNotHandler(),
            new TsUnevaluatedPropertiesHandler(), new TsUnevaluatedItemsHandler());
        return p;
    }

    // Like CreateDefault, but asserts `format` (the optional/format suite). The default leaves format as
    // an annotation (the 2020-12 default), which is why top-level format.json expects everything valid.
    public static TypeScriptLanguageProviderSpike CreateWithFormatAssertion()
    {
        TypeScriptLanguageProviderSpike p = CreateDefault();
        p.assertFormat = true;
        p.RegisterValidationHandlers(new TsFormatHandler());
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

        // Pass 2: emit the type surface + validators. The runtime helpers live in ONE shared module
        // (@corvus/json-runtime, §5.5) that every generated module imports — never inlined per module.
        var sb = new StringBuilder();
        sb.Append("// AUTO-GENERATED: idiomatic TS types + registry-composed validators.\n");
        sb.Append("import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, Ev, NOEV, fresh, __fmt } from \"./corvus-runtime.js\";\n\n");
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
