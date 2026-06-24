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
        // duration: RFC 3339 ABNF — a REGULAR grammar, STRICTER than ISO 8601 / Temporal (units must be
        // contiguous & descending: P1Y2D and PT1H2S are invalid; no fractions). Temporal.Duration is the
        // accessor TYPE for a duration, but it is too lenient (ISO 8601) to VALIDATE this format.
        const __durRe = /^P(?:\d+W|(?:\d+Y(?:\d+M(?:\d+D)?)?|\d+M(?:\d+D)?|\d+D)?(?:T(?:\d+H(?:\d+M(?:\d+S)?)?|\d+M(?:\d+S)?|\d+S))?)$/;
        function __fmtDuration(s: string): boolean { return s !== "P" && __durRe.test(s); }
        const __ipv4Re = /^((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$/;
        function __fmtIpv6(s: string): boolean {
          if (s.indexOf(":") < 0) { return false; }
          const parts = s.split("::");
          if (parts.length > 2) { return false; }
          const head = parts[0] === "" ? [] : parts[0].split(":");
          const tail = parts.length === 2 ? (parts[1] === "" ? [] : parts[1].split(":")) : [];
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
        const __jsonPtrRe = /^(?:\/(?:[^~/]|~0|~1)*)*$/;
        const __relJsonPtrRe = /^(?:0|[1-9][0-9]*)(?:#|(?:\/(?:[^~/]|~0|~1)*)*)$/;
        const __uriTemplateRe = /^(?:[^\s{}]|\{[^\s{}]*\})*$/;
        // hostname / idn-hostname via UTS-46 (tr46) for LDH/punycode/length, PLUS the IDNA2008 (RFC 5892)
        // validity rules tr46's UTS-46 processing does not enforce: a small DISALLOWED set and the
        // CONTEXTO rules (MIDDLE DOT between 'l', Greek KERAIA before Greek, Hebrew GERESH/GERSHAYIM after
        // Hebrew), checked on the decoded U-labels.
        const __isGreek = (c: number | undefined): boolean => c !== undefined && ((c >= 0x370 && c <= 0x3FF) || (c >= 0x1F00 && c <= 0x1FFF));
        const __isHebrew = (c: number | undefined): boolean => c !== undefined && ((c >= 0x590 && c <= 0x5FF) || (c >= 0xFB1D && c <= 0xFB4F));
        function __idnaLabel(label: string): boolean {
          const cp = Array.from(label, (c) => c.codePointAt(0)!);
          for (let i = 0; i < cp.length; i++) {
            const c = cp[i];
            if (c === 0x0640 || c === 0x07FA || c === 0x302E || c === 0x302F || (c >= 0x3031 && c <= 0x3035) || c === 0x303B) { return false; }
            if (c === 0x00B7 && !(cp[i - 1] === 0x6C && cp[i + 1] === 0x6C)) { return false; }
            if (c === 0x0375 && !__isGreek(cp[i + 1])) { return false; }
            if ((c === 0x05F3 || c === 0x05F4) && !__isHebrew(cp[i - 1])) { return false; }
          }
          // Katakana Middle Dot (U+30FB, script Common): the label must contain a Hiragana/Katakana/Han char.
          if (cp.indexOf(0x30FB) >= 0 && !cp.some((x) => x !== 0x30FB && ((x >= 0x3040 && x <= 0x309F) || (x >= 0x30A0 && x <= 0x30FF) || (x >= 0x3400 && x <= 0x4DBF) || (x >= 0x4E00 && x <= 0x9FFF)))) { return false; }
          return true;
        }
        const __idnaOpt = { checkHyphens: true, checkBidi: true, checkJoiners: true, useSTD3ASCIIRules: true, verifyDNSLength: true };
        function __fmtHostname(s: string, idn: boolean): boolean {
          if (s.length === 0 || s.length > 253 || s.startsWith(".") || s.endsWith(".") || s.includes("..")) { return false; }
          if (!idn) {
            // RFC 1123 LDH: ASCII letters/digits/hyphen, 1-63 per label, no leading/trailing hyphen
            // (interior "--" is allowed, unlike IDNA). An xn-- label must still decode to a valid A-label.
            for (const label of s.split(".")) {
              if (label.length === 0 || label.length > 63 || label.startsWith("-") || label.endsWith("-")) { return false; }
              for (let i = 0; i < label.length; i++) { const c = label[i]; if (!((c >= "a" && c <= "z") || (c >= "A" && c <= "Z") || (c >= "0" && c <= "9") || c === "-")) { return false; } }
              if (label.length >= 4 && (label[0] === "x" || label[0] === "X") && (label[1] === "n" || label[1] === "N") && label[2] === "-" && label[3] === "-") {
                const u = tr46.toUnicode(label, __idnaOpt);
                if (!u || u.error || !__idnaLabel(u.domain)) { return false; }
              }
            }

            return true;
          }

          const ascii = tr46.toASCII(s, __idnaOpt);
          if (typeof ascii !== "string" || ascii.length === 0) { return false; }
          const uni = tr46.toUnicode(s, __idnaOpt);
          if (!uni || uni.error) { return false; }
          for (const label of uni.domain.split(".")) { if (!__idnaLabel(label)) { return false; } }
          return true;
        }
        // RFC 3986/3987 URI/IRI as a PARSER (not a regex): optional scheme then allowed chars / %XX (+ ucschar).
        const __isHex = (c: string): boolean => (c >= "0" && c <= "9") || (c >= "a" && c <= "f") || (c >= "A" && c <= "F");
        function __validUriPart(s: string, allowUnicode: boolean): boolean {
          for (let i = 0; i < s.length; i++) {
            const c = s[i];
            if (c === "%") { if (!__isHex(s[i + 1]) || !__isHex(s[i + 2])) { return false; } i += 2; continue; }
            if ((c >= "A" && c <= "Z") || (c >= "a" && c <= "z") || (c >= "0" && c <= "9") || "-._~:/?#[]@!$&'()*+,;=".indexOf(c) >= 0) { continue; }
            if (allowUnicode && s.charCodeAt(i) > 127) { continue; }
            return false;
          }
          return true;
        }
        function __hasScheme(s: string): boolean {
          if (s.length === 0 || !((s[0] >= "A" && s[0] <= "Z") || (s[0] >= "a" && s[0] <= "z"))) { return false; }
          let i = 1;
          while (i < s.length && ((s[i] >= "A" && s[i] <= "Z") || (s[i] >= "a" && s[i] <= "z") || (s[i] >= "0" && s[i] <= "9") || s[i] === "+" || s[i] === "." || s[i] === "-")) { i++; }
          return s[i] === ":";
        }
        // Validate the authority component (after "//", up to the next /?#): [userinfo@]host[:port],
        // host = [IP-literal] | IPv4 | reg-name, port = DIGIT*. Catches bad ports and malformed IP-literals.
        function __validAuthority(auth: string, allowUnicode: boolean): boolean {
          const at = auth.lastIndexOf("@");
          const userinfo = at >= 0 ? auth.slice(0, at) : "";
          const host = at >= 0 ? auth.slice(at + 1) : auth;
          if (userinfo.indexOf("[") >= 0 || userinfo.indexOf("]") >= 0) { return false; }
          if (host.startsWith("[")) {
            const close = host.indexOf("]");
            if (close < 0) { return false; }
            const inner = host.slice(1, close);
            if (!__fmtIpv6(inner) && !(inner[0] === "v")) { return false; }
            const rest = host.slice(close + 1);
            return rest === "" || (rest[0] === ":" && __allDigits(rest.slice(1)));
          }
          // reg-name with optional :port -- any further colon means an unbracketed IPv6, which is invalid.
          const colon = host.indexOf(":");
          if (colon >= 0) {
            if (host.indexOf(":", colon + 1) >= 0 || !__allDigits(host.slice(colon + 1))) { return false; }
          }
          return true;
        }
        const __allDigits = (s: string): boolean => { for (let i = 0; i < s.length; i++) { if (s[i] < "0" || s[i] > "9") { return false; } } return true; };
        function __fmtUri(s: string, requireScheme: boolean, allowUnicode: boolean): boolean {
          if (requireScheme && !__hasScheme(s)) { return false; }
          if (!__validUriPart(s, allowUnicode)) { return false; }
          // If an authority is present ("//"), validate its structure.
          const ds = s.indexOf("//");
          if (ds >= 0 && (ds === 0 || s[ds - 1] === ":")) {
            let end = s.length;
            for (let i = ds + 2; i < s.length; i++) { const c = s[i]; if (c === "/" || c === "?" || c === "#") { end = i; break; } }
            if (!__validAuthority(s.slice(ds + 2, end), allowUnicode)) { return false; }
          }
          return true;
        }
        function __fmtEmail(s: string, idn: boolean): boolean {
          const at = s.lastIndexOf("@");
          if (at < 1 || at >= s.length - 1) { return false; }
          const local = s.slice(0, at), domain = s.slice(at + 1);
          if (local.startsWith('"') && local.endsWith('"')) { if (local.length < 2) { return false; } }
          else {
            if (local.startsWith(".") || local.endsWith(".") || local.includes("..")) { return false; }
            for (let i = 0; i < local.length; i++) {
              const c = local[i];
              if ((c >= "A" && c <= "Z") || (c >= "a" && c <= "z") || (c >= "0" && c <= "9") || "!#$%&'*+/=?^_`{|}~.-".indexOf(c) >= 0 || (idn && local.charCodeAt(i) > 127)) { continue; }
              return false;
            }
          }
          if (domain.startsWith("[") && domain.endsWith("]")) { const ip = domain.slice(1, -1); return ip.startsWith("IPv6:") ? __fmtIpv6(ip.slice(5)) : __ipv4Re.test(ip); }
          return __fmtHostname(domain, idn);
        }
        function __fmtRegex(s: string): boolean { try { new RegExp(s, "u"); return true; } catch { return false; } }
        // contentEncoding (base64) + contentMediaType (application/json) assertion -- annotation-only by
        // default, asserted by the optional/content suite.
        function __fmtContent(s: string, encoding: string | null, mediaType: string | null): boolean {
          let content = s;
          if (encoding === "base64") {
            if (s.length % 4 !== 0) { return false; }
            for (let i = 0; i < s.length; i++) { const c = s[i]; if (!((c >= "A" && c <= "Z") || (c >= "a" && c <= "z") || (c >= "0" && c <= "9") || c === "+" || c === "/" || c === "=")) { return false; } }
            try { content = atob(s); } catch { return false; }
          }
          if (mediaType === "application/json") { try { JSON.parse(content); } catch { return false; } }
          return true;
        }
        function __fmt(name: string, s: string): boolean {
          switch (name) {
            case "date": return __fmtDate(s);
            case "date-time": return __fmtDateTime(s);
            case "time": return __fmtTime(s);
            case "duration": return __fmtDuration(s);
            case "email": return __fmtEmail(s, false);
            case "idn-email": return __fmtEmail(s, true);
            case "hostname": return __fmtHostname(s, false);
            case "idn-hostname": return __fmtHostname(s, true);
            case "ipv4": return __ipv4Re.test(s);
            case "ipv6": return __fmtIpv6(s);
            case "uuid": return __uuidRe.test(s);
            case "uri": return __fmtUri(s, true, false);
            case "iri": return __fmtUri(s, true, true);
            case "uri-reference": return __fmtUri(s, false, false);
            case "iri-reference": return __fmtUri(s, false, true);
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
        p.RegisterValidationHandlers(new TsFormatHandler(), new TsContentHandler());
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
        sb.Append("import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, Ev, NOEV, fresh, __fmt, __fmtContent } from \"./corvus-runtime.js\";\n\n");
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
                }
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

    // A pure map / dictionary object (additionalProperties value type, no declared properties): the
    // idiomatic TS surface is `Readonly<Record<string, T>>` (design §5.3).
    private static void EmitMapAlias(StringBuilder sb, TypeDeclaration td, FallbackObjectPropertyType fb)
    {
        sb.Append("export type ").Append(FinalName(td)).Append(" = Readonly<Record<string, ").Append(TsTypeRef(fb.ReducedType)).Append(">>;\n\n");
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
            return FinalName(td);
        }

        List<string> kinds = SchemaTypes(td);
        if (kinds.Contains("array") || td.ExplicitTupleType() is not null || (td.ExplicitNonTupleItemsType() ?? td.ArrayItemsType()) is not null)
        {
            // Guard against recursive array schemas (e.g. items: {$ref: '#'}): a revisited type degrades
            // to the safe `readonly unknown[]` rather than recursing forever.
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
