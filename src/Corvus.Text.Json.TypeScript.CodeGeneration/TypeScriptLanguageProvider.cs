using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// Phase 0 provider: idiomatic type surface (real names + built-in scalar mapping + property
// type-references + named enum unions) on top of registry-driven, extensible validators (§13.13).
//
// Emission per type: objects -> `interface`; enums -> `type X = a | b`; scalars/arrays -> no type
// declaration (referenced inline as the primitive / element type). EVERY type still gets an
// `evaluate{Name}` validator composed from the core handler registry, so recursion always resolves.
public sealed class TypeScriptLanguageProvider : IHierarchicalLanguageProvider
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

    // Evaluation tracker for unevaluatedProperties/Items: an index bitmask over property/item positions.
    // The first 32 positions live inline in a number (zero allocation -- the overwhelmingly common case);
    // positions >= 32 (objects/arrays with > 32 members, e.g. an OpenAPI `paths` with 68 routes) overflow
    // into a lazily-allocated Set so large instances validate correctly without slowing the fast path.
    // `local` (this schema's own evaluations) + `applied` (OR-merged from matched in-place applicators).
    // NOEV is the shared no-op tracker passed to sub-instances that do not require tracking.
    private const string EvRuntime =
        "class Ev {\n" +
        "  n = false; pl = 0; pa = 0; il = 0; ia = 0;\n" +
        "  po: Set<number> | null = null; poa: Set<number> | null = null; io: Set<number> | null = null; ioa: Set<number> | null = null;\n" +
        "  markProp(i: number): void { if (this.n) { return; } if (i < 32) { this.pl |= (1 << i) >>> 0; } else { (this.po ??= new Set<number>()).add(i); } }\n" +
        "  hasProp(i: number): boolean { return i < 32 ? (((this.pl | this.pa) & ((1 << i) >>> 0)) !== 0) : ((this.po !== null && this.po.has(i)) || (this.poa !== null && this.poa.has(i))); }\n" +
        "  markItem(i: number): void { if (this.n) { return; } if (i < 32) { this.il |= (1 << i) >>> 0; } else { (this.io ??= new Set<number>()).add(i); } }\n" +
        "  hasItem(i: number): boolean { return i < 32 ? (((this.il | this.ia) & ((1 << i) >>> 0)) !== 0) : ((this.io !== null && this.io.has(i)) || (this.ioa !== null && this.ioa.has(i))); }\n" +
        "  mergeProps(c: Ev): void { if (this.n) { return; } this.pa |= c.pl | c.pa; if (c.po !== null || c.poa !== null) { const s = (this.poa ??= new Set<number>()); if (c.po !== null) { for (const x of c.po) { s.add(x); } } if (c.poa !== null) { for (const x of c.poa) { s.add(x); } } } }\n" +
        "  mergeItems(c: Ev): void { if (this.n) { return; } this.ia |= c.il | c.ia; if (c.io !== null || c.ioa !== null) { const s = (this.ioa ??= new Set<number>()); if (c.io !== null) { for (const x of c.io) { s.add(x); } } if (c.ioa !== null) { for (const x of c.ioa) { s.add(x); } } } }\n" +
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
        // `format: regex` asserts STRICT ECMA-262 validity (Unicode mode): e.g. `\a` is an invalid escape
        // and so not a valid regex. Keep this `u`-only (do NOT fall back to non-`u`).
        function __fmtRegex(s: string): boolean { try { new RegExp(s, "u"); return true; } catch { return false; } }
        // Compile a JSON Schema `pattern`/`patternProperties` regex ONCE (cached). Unlike `format: regex`,
        // the pattern KEYWORD is applied leniently: real-world patterns use identity escapes like \& \%
        // that are valid ECMA-262 without `u` but a SyntaxError with it (e.g. krakend). Try `u` first
        // (correct Unicode semantics, matches the test suite's pattern cases), fall back to non-`u`.
        const __reCache = new Map<string, RegExp>();
        function __re(p: string): RegExp { let r = __reCache.get(p); if (r === undefined) { try { r = new RegExp(p, "u"); } catch { r = new RegExp(p); } __reCache.set(p, r); } return r; }
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

    // Brand/format conversion runtime (design §5.3.1): a nominal `Brand<T,B>` (un-spoofable via a phantom
    // unique-symbol key, zero runtime cost) + `FormatError` thrown by the generated validating factories.
    private const string BrandRuntime = """

        declare const __brand: unique symbol;
        export type Brand<T, B extends string> = T & { readonly [__brand]: B };
        class FormatError extends Error {}
        """;

    // Mutation runtime (design §5.7): idiomatic immer-style `produce` over a typed `Draft<T>`. A tiny
    // clone-and-diff change-recorder over a structural clone; the change-set is RFC 6902 JSON Patch (and lowers to
    // a Model C byte patch). These are GENERIC runtime helpers — generated modules don't emit per-type
    // mutation code; a consumer pairs `produce` with the emitted readonly interface.
    private const string MutationRuntime = """

        export type Draft<T> =
          T extends ReadonlyArray<infer E> ? Draft<E>[] :
          T extends object ? { -readonly [K in keyof T]: Draft<T[K]> } :
          T;
        export interface JsonDocument<T> { readonly value: T; }
        export interface JsonPatchOp { readonly op: "replace" | "add" | "remove"; readonly path: string; readonly value?: unknown; }
        const __escPtr = (k: string): string => k.replace(/~/g, "~0").replace(/\//g, "~1");
        // immer-faithful recorder: structuredClone the substrate, run the recipe DIRECTLY on the clone
        // (plain mutation -- no Proxy), then diff(original, clone) -> a minimal RFC 6902 change-set. Array
        // structural ops (push/splice/pop/unshift) "just work": a length change re-serialises that one array
        // (siblings byte-preserved), a same-length array diffs per element. The change-set lowers to bytes.
        function __diff(orig: any, mut: any, ptr: string, out: JsonPatchOp[]): void {
          if (orig === mut) return;
          const oa = Array.isArray(orig), ma = Array.isArray(mut);
          if (oa && ma) {
            if (orig.length !== mut.length) { out.push({ op: "replace", path: ptr, value: mut }); return; }
            for (let i = 0; i < orig.length; i++) __diff(orig[i], mut[i], ptr + "/" + i, out);
            return;
          }
          const oo = orig !== null && typeof orig === "object" && !oa;
          const mo = mut !== null && typeof mut === "object" && !ma;
          if (oo && mo) {
            for (const k of Object.keys(orig)) { const np = ptr + "/" + __escPtr(k); if (!(k in mut)) out.push({ op: "remove", path: np }); else __diff(orig[k], mut[k], np, out); }
            for (const k of Object.keys(mut)) { if (!(k in orig)) out.push({ op: "add", path: ptr + "/" + __escPtr(k), value: mut[k] }); }
            return;
          }
          out.push({ op: "replace", path: ptr, value: mut });
        }
        function recordChanges<T>(value: T, recipe: (draft: Draft<T>) => void): { next: T; patches: JsonPatchOp[] } {
          const next = structuredClone(value);
          recipe(next as unknown as Draft<T>);
          const patches: JsonPatchOp[] = [];
          __diff(value, next, "", patches);
          return { next, patches };
        }
        
        interface __DraftOp { path: string[]; op: "replace" | "add" | "remove" | "append"; value?: unknown; }
        function __groupHead(ops: __DraftOp[]): Map<string, __DraftOp[]> {
          const g = new Map<string, __DraftOp[]>();
          for (const op of ops) { const h = op.path[0]; const cur = g.get(h); if (cur) cur.push(op); else g.set(h, [op]); }
          return g;
        }
        // Recursive change-set -> byte-patch lowering: descend each container level, locate the sub-span (object
        // scan or array scan), recurse into it, splice back. Reuses the RMW scan/splice primitives.
        function __lower(bytes: Uint8Array, ops: __DraftOp[]): Uint8Array {
          let i = 0;
          if (bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) i = 3;
          while (i < bytes.length && isWsB(bytes[i])) i++;
          return bytes[i] === 0x5b ? __lowerArray(bytes, ops) : __lowerObject(bytes, ops);
        }
        function __lowerObject(bytes: Uint8Array, ops: __DraftOp[]): Uint8Array {
          const enc = new TextEncoder();
          const scan = scanAll(bytes); const members = scan.members; const n = members.length;
          const edits: RmwEdit[] = [];
          const removed: boolean[] = new Array<boolean>(n).fill(false);
          const adds: Array<{ name: string; value: unknown }> = [];
          for (const [key, group] of __groupHead(ops)) {
            const idx = findMember(bytes, members, enc.encode(key));
            const direct = group.find((o) => o.path.length === 1 && o.op !== "append");
            if (direct) {
              if (direct.op === "remove") { if (idx < 0) throw new Error("produce: remove of missing member"); removed[idx] = true; }
              else if (idx < 0) adds.push({ name: key, value: direct.value });
              else edits.push({ offset: members[idx].vs, length: members[idx].ve - members[idx].vs, content: enc.encode(JSON.stringify(direct.value)) });
            } else {
              if (idx < 0) throw new Error("produce: deep path into missing member");
              const newSub = __lower(bytes.subarray(members[idx].vs, members[idx].ve), group.map((o) => ({ path: o.path.slice(1), op: o.op, value: o.value })));
              edits.push({ offset: members[idx].vs, length: members[idx].ve - members[idx].vs, content: newSub });
            }
          }
          const COMMA = new Uint8Array([0x2c]); const EMPTY = new Uint8Array(0);
          let j = 0;
          while (j < n) {
            if (!removed[j]) { j++; continue; }
            let hi = j; while (hi + 1 < n && removed[hi + 1]) hi++;
            if (j === 0 && hi === n - 1) edits.push({ offset: members[0].ns, length: members[n - 1].ve - members[0].ns, content: EMPTY });
            else if (j === 0) edits.push({ offset: members[0].ns, length: members[hi + 1].ns - members[0].ns, content: EMPTY });
            else if (hi === n - 1) edits.push({ offset: members[j - 1].ve, length: members[n - 1].ve - members[j - 1].ve, content: EMPTY });
            else edits.push({ offset: members[j - 1].ve, length: members[hi + 1].ns - members[j - 1].ve, content: COMMA });
            j = hi + 1;
          }
          if (adds.length > 0) {
            let surviving = 0; for (let k = 0; k < n; k++) if (!removed[k]) surviving++;
            const parts: Uint8Array[] = [];
            for (let a = 0; a < adds.length; a++) { if (a > 0 || surviving > 0) parts.push(COMMA); parts.push(memberBytes(enc.encode(adds[a].name), enc.encode(JSON.stringify(adds[a].value)))); }
            edits.push({ offset: scan.close, length: 0, content: concatBytes(parts) });
          }
          if (edits.length === 0) return bytes;
          return applyEditsBytes(bytes, edits);
        }
        function __lowerArray(bytes: Uint8Array, ops: __DraftOp[]): Uint8Array {
          const enc = new TextEncoder();
          const scan = scanArrayElements(bytes); const elems = scan.elems;
          const edits: RmwEdit[] = [];
          // append ops (empty path) -> insert tail elements before the closing ']' (existing element bytes untouched)
          const appendOps = ops.filter((o) => o.path.length === 0 && o.op === "append");
          if (appendOps.length > 0) {
            const parts: Uint8Array[] = []; let had = elems.length > 0;
            for (const ao of appendOps) for (const el of (ao.value as unknown[])) { parts.push(enc.encode((had ? "," : "") + JSON.stringify(el))); had = true; }
            edits.push({ offset: scan.close, length: 0, content: concatBytes(parts) });
          }
          for (const [idx, group] of __groupHead(ops.filter((o) => o.path.length > 0))) {
            const i = Number(idx);
            const direct = group.find((o) => o.path.length === 1 && o.op !== "append");
            if (direct) edits.push({ offset: elems[i].vs, length: elems[i].ve - elems[i].vs, content: enc.encode(JSON.stringify(direct.value)) });
            else {
              const newSub = __lower(bytes.subarray(elems[i].vs, elems[i].ve), group.map((o) => ({ path: o.path.slice(1), op: o.op, value: o.value })));
              edits.push({ offset: elems[i].vs, length: elems[i].ve - elems[i].vs, content: newSub });
            }
          }
          if (edits.length === 0) return bytes;
          return applyEditsBytes(bytes, edits);
        }
        // §13.3 lazy-read produce: a lazy overlay draft replaces JSON.parse(whole-doc). Objects AND arrays
        // navigate by byte span (no value decode); scalars decode on read; an array element edit or a trailing
        // push touches ~nothing (see __makeArrayDraft); only a reordering array op materialises. Writes record
        // a change-set the byte lowering (__lower) consumes -- a pure overwrite (or push) decodes nothing.
        function __decodeValue(bytes: Uint8Array, vs: number, ve: number): unknown { return JSON.parse(new TextDecoder().decode(bytes.subarray(vs, ve))); }
        function __spanKind(bytes: Uint8Array, vs: number): number { const c = bytes[vs]; return c === 0x7b ? 0 : c === 0x5b ? 1 : 2; }
        // Lazy drafts. An object navigates by byte span (no value decode); a scalar decodes on read; an
        // array is element-lazy too: items[i].field = v touches only element i's span, an in-bounds index
        // set becomes a per-element edit, and a trailing push records into `appends` (no decode) -> an
        // 'append' op that inserts before ']'. Only a reordering op (splice/unshift/pop/length-shrink, or a
        // proxy stored at the tail) materialises (decode + fold). Both draft kinds share { proxy, value, finalize }.
        const __ISIDX = /^(0|[1-9][0-9]*)$/;
        const __DRAFT = Symbol("draft");      // tags our draft proxies so a shift (unshift/splice) storing a proxy is detectable
        const __DRAFTVAL = Symbol("draftval"); // reads a draft proxy's resolved plain value (for a relocated element)
        function __isDraftP(v: any): boolean { return v !== null && typeof v === "object" && v[__DRAFT] === true; }
        function __resolveV(v: any): any { return __isDraftP(v) ? v[__DRAFTVAL] : v; }
        interface __Draft { proxy: any; value: () => any; finalize: (out: __DraftOp[]) => void; }
        type __Kid = { kind: 0 | 1; d: __Draft };
        function __makeObjectDraft(bytes: Uint8Array, vs: number, ve: number, path: string[]): __Draft {
          const enc = new TextEncoder();
          let members: RmwMember[] | null = null;
          const written = new Map<string, unknown>();
          const deleted = new Set<string>();
          const children = new Map<string, __Kid>();
          function scan(): RmwMember[] {
            if (members) return members;
            const s = scanAll(bytes.subarray(vs, ve));
            members = s.members.map((m) => ({ ns: vs + m.ns, ne: vs + m.ne, vs: vs + m.vs, ve: vs + m.ve }));
            return members;
          }
          function findKey(key: string): RmwMember | null {
            const ms = scan(); const idx = findMember(bytes, ms, enc.encode(key));
            return idx < 0 ? null : ms[idx];
          }
          const proxy: any = new Proxy({}, {
            get(_t, key) {
              if (key === __DRAFT) return true;
              if (key === __DRAFTVAL) return value();
              if (typeof key === "symbol") return undefined;
              const k = String(key);
              if (deleted.has(k)) return undefined;
              if (written.has(k)) return written.get(k);
              const c = children.get(k);
              if (c) return c.d.proxy;
              const m = findKey(k);
              if (!m) return undefined;
              const kind = __spanKind(bytes, m.vs);
              if (kind === 0) { const d = __makeObjectDraft(bytes, m.vs, m.ve, [...path, k]); children.set(k, { kind: 0, d }); return d.proxy; }
              if (kind === 1) { const d = __makeArrayDraft(bytes, m.vs, m.ve, [...path, k]); children.set(k, { kind: 1, d }); return d.proxy; }
              return __decodeValue(bytes, m.vs, m.ve);
            },
            set(_t, key, val) { const k = String(key); written.set(k, val); deleted.delete(k); children.delete(k); return true; },
            deleteProperty(_t, key) { const k = String(key); deleted.add(k); written.delete(k); children.delete(k); return true; },
            has(_t, key) { const k = String(key); if (deleted.has(k)) return false; if (written.has(k) || children.has(k)) return true; return !!findKey(k); },
          });
          function value(): any { const base: any = __decodeValue(bytes, vs, ve); for (const [k, v] of written) base[k] = v; for (const k of deleted) delete base[k]; for (const [k, c] of children) base[k] = c.d.value(); return base; }
          function finalize(out: __DraftOp[]): void {
            for (const [k, val] of written) { out.push({ path: [...path, k], op: findKey(k) ? "replace" : "add", value: val }); }
            for (const k of deleted) { if (findKey(k)) out.push({ path: [...path, k], op: "remove" }); }
            for (const [, c] of children) c.d.finalize(out);
          }
          return { proxy, value, finalize };
        }
        function __makeArrayDraft(bytes: Uint8Array, vs: number, ve: number, path: string[]): __Draft {
          let elems: Array<{ vs: number; ve: number }> | null = null;
          const overrides = new Map<number, unknown>();
          const children = new Map<number, __Kid>();
          const appends: unknown[] = [];   // trailing pushes (no decode); finalize emits an 'append' op
          let materialized: any[] | null = null;
          function scan(): Array<{ vs: number; ve: number }> {
            if (elems) return elems;
            const s = scanArrayElements(bytes.subarray(vs, ve));
            elems = s.elems.map((x) => ({ vs: vs + x.vs, ve: vs + x.ve }));
            return elems;
          }
          function baseLen(): number { return scan().length; }
          function len(): number { return materialized ? materialized.length : baseLen() + appends.length; }
          function value(): any[] {
            if (materialized) return materialized;
            const es = scan();
            const arr = es.map((e, i) => { const c = children.get(i); if (c) return c.d.value(); if (overrides.has(i)) return overrides.get(i); return __decodeValue(bytes, e.vs, e.ve); });
            for (const a of appends) arr.push(a);
            return arr;
          }
          function materialize(): void { if (!materialized) { materialized = value(); appends.length = 0; } }
          const proxy: any = new Proxy([] as any[], {
            get(_t, key) {
              if (key === __DRAFT) return true;
              if (key === __DRAFTVAL) return value();
              if (key === "length") return len();
              if (typeof key === "symbol") return materialized ? (materialized as any)[key] : (Array.prototype as any)[key];
              if (materialized) return (materialized as any)[key];
              if (!__ISIDX.test(key)) return (Array.prototype as any)[key];
              const i = Number(key); const bl = baseLen();
              if (i >= bl) return (i - bl < appends.length) ? appends[i - bl] : undefined;
              if (overrides.has(i)) return overrides.get(i);
              const c = children.get(i); if (c) return c.d.proxy;
              const e = scan()[i]; const kind = __spanKind(bytes, e.vs);
              if (kind === 0) { const d = __makeObjectDraft(bytes, e.vs, e.ve, [...path, String(i)]); children.set(i, { kind: 0, d }); return d.proxy; }
              if (kind === 1) { const d = __makeArrayDraft(bytes, e.vs, e.ve, [...path, String(i)]); children.set(i, { kind: 1, d }); return d.proxy; }
              return __decodeValue(bytes, e.vs, e.ve);
            },
            set(_t, key, val) {
              if (key === "length") { const n = Number(val); if (!materialized && n === len()) return true; materialize(); materialized!.length = n; return true; }
              if (typeof key === "symbol") return true;
              if (!__ISIDX.test(key)) return true;
              const i = Number(key);
              if (materialized) { materialized[i] = __resolveV(val); return true; }
              const bl = baseLen();
              if (i < bl) { if (appends.length > 0) { materialize(); materialized![i] = __resolveV(val); return true; } overrides.set(i, __resolveV(val)); children.delete(i); return true; }
              if (i === bl + appends.length && !__isDraftP(val)) { appends.push(val); return true; }  // trailing push -> lazy append
              materialize(); materialized![i] = __resolveV(val); return true;   // proxy-at-tail (shift) or sparse growth -> fold
            },
            deleteProperty(_t, key) { if (typeof key === "symbol") return true; if (!__ISIDX.test(key)) return true; materialize(); delete materialized![Number(key)]; return true; },
            has(_t, key) { if (key === "length") return true; const sk = String(key); if (__ISIDX.test(sk)) return Number(sk) < len(); return sk in Array.prototype; },
          });
          function finalize(out: __DraftOp[]): void {
            if (materialized) { out.push({ path, op: "replace", value: materialized }); return; }
            for (const [i, v] of overrides) out.push({ path: [...path, String(i)], op: "replace", value: v });
            for (const [, c] of children) c.d.finalize(out);
            if (appends.length > 0) out.push({ path, op: "append", value: appends.slice() });
          }
          return { proxy, value, finalize };
        }

        // produce(source, recipe): immer-style mutation lowered to a Model C byte patch via lazy reads.
        function produce<T>(source: Uint8Array, recipe: (draft: Draft<T>) => void): Uint8Array {
          const root = __makeObjectDraft(source, 0, source.length, []);
          recipe(root.proxy as Draft<T>);
          const ops: __DraftOp[] = [];
          root.finalize(ops);
          if (ops.length === 0) return source;
          return __lower(source, ops);
        }
        """;

    // Model C byte-level RMW (design §13.4): a wire read-modify-write that copies unchanged
    // bytes through and splices only the changed members' value byte-spans -- the unchanged
    // document is never parsed or re-serialised. scanTargets locates the edited members by name
    // (byte-only; UTF-16 offsets are the separate LSP package's concern, not the wire path);
    // applyEditsBytes is the jsonc-parser byte splice. Ported from the benchmark-proven prototype
    // (prototypes/rmw-scanner/{scanner,rmw}.mjs).
    private const string RmwRuntime = """

        export interface RmwEdit { offset: number; length: number; content: Uint8Array; }
        export interface RmwTarget { name: Uint8Array; content: Uint8Array; vbs: number; vbe: number; }
        const QUOTE = 0x22, BACKSLASH = 0x5c, COMMA = 0x2c, LBRACE = 0x7b, RBRACE = 0x7d, LBRACK = 0x5b, RBRACK = 0x5d;
        function isWsB(b: number): boolean { return b === 0x20 || b === 0x09 || b === 0x0a || b === 0x0d; }
        function skipStringFrom(buf: Uint8Array, i: number): number {
          i++;
          for (;;) {
            const q = buf.indexOf(QUOTE, i);
            if (q < 0) throw new Error("unterminated string");
            let b = q - 1, bs = 0;
            while (b >= 0 && buf[b] === BACKSLASH) { bs++; b--; }
            i = q + 1;
            if ((bs & 1) === 0) return i;
          }
        }
        function skipContainerFrom(buf: Uint8Array, i: number, len: number): number {
          let depth = 0;
          for (;;) {
            if (i >= len) throw new Error("unterminated container");
            const b = buf[i];
            if (b === QUOTE) { i = skipStringFrom(buf, i); continue; }
            if (b === LBRACE || b === LBRACK) { depth++; i++; continue; }
            if (b === RBRACE || b === RBRACK) { depth--; i++; if (depth === 0) return i; continue; }
            i++;
          }
        }
        function skipValueFrom(buf: Uint8Array, i: number, len: number): number {
          const b = buf[i];
          if (b === QUOTE) return skipStringFrom(buf, i);
          if (b === LBRACE || b === LBRACK) return skipContainerFrom(buf, i, len);
          while (i < len) { const c = buf[i]; if (c === COMMA || c === RBRACE || c === RBRACK || isWsB(c)) break; i++; }
          return i;
        }
        function eqName(buf: Uint8Array, start: number, end: number, name: Uint8Array): boolean {
          if (end - start !== name.length) return false;
          for (let k = 0; k < name.length; k++) { if (buf[start + k] !== name[k]) return false; }
          return true;
        }
        function scanTargets(buf: Uint8Array, targets: RmwTarget[]): boolean {
          const len = buf.length;
          let i = 0, remaining = targets.length;
          for (let k = 0; k < targets.length; k++) { targets[k].vbs = -1; }
          if (len >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) { i = 3; }
          while (i < len && isWsB(buf[i])) { i++; }
          i++;
          while (remaining > 0 && i < len) {
            while (i < len && isWsB(buf[i])) { i++; }
            if (buf[i] === RBRACE) break;
            const ns = i + 1;
            i = skipStringFrom(buf, i);
            const ne = i - 1;
            let hit = -1;
            for (let k = 0; k < targets.length; k++) {
              if (targets[k].vbs === -1 && eqName(buf, ns, ne, targets[k].name)) { hit = k; break; }
            }
            while (i < len && isWsB(buf[i])) { i++; }
            i++;
            while (i < len && isWsB(buf[i])) { i++; }
            const vs = i;
            i = skipValueFrom(buf, i, len);
            if (hit >= 0) { targets[hit].vbs = vs; targets[hit].vbe = i; remaining--; }
            while (i < len && isWsB(buf[i])) { i++; }
            if (buf[i] === COMMA) { i++; continue; }
            if (buf[i] === RBRACE) break;
          }
          return remaining === 0;
        }
        function applyEditsBytes(buf: Uint8Array, edits: RmwEdit[]): Uint8Array {
          edits.sort((a, b) => a.offset - b.offset);
          let delta = 0;
          for (let i = 0; i < edits.length; i++) { delta += edits[i].content.length - edits[i].length; }
          const out = new Uint8Array(buf.length + delta);
          let src = 0, dst = 0;
          for (let i = 0; i < edits.length; i++) {
            const e = edits[i];
            out.set(buf.subarray(src, e.offset), dst); dst += e.offset - src;
            out.set(e.content, dst); dst += e.content.length;
            src = e.offset + e.length;
          }
          out.set(buf.subarray(src), dst);
          return out;
        }
        export interface RmwMember { ns: number; ne: number; vs: number; ve: number; }
        export interface FlatArrayOps { set?: { readonly [i: number]: unknown }; insert?: { readonly [i: number]: readonly unknown[] }; removeAt?: readonly number[]; append?: readonly unknown[]; }
        export interface RmwArrayOps extends FlatArrayOps { rest?: FlatArrayOps; }
        export interface ListOps<T> { set?: { readonly [i: number]: T }; insert?: { readonly [i: number]: readonly T[] }; removeAt?: readonly number[]; append?: readonly T[]; }
        export interface RmwArrayEdit { name: Uint8Array; ops: RmwArrayOps; prefixLen: number; }
        const RMW_EMPTY = new Uint8Array(0);
        const RBRACKET = 0x5d;
        function scanAll(buf: Uint8Array): { members: RmwMember[]; close: number } {
          const len = buf.length;
          let i = 0;
          if (len >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) { i = 3; }
          while (i < len && isWsB(buf[i])) { i++; }
          i++;
          const out: RmwMember[] = [];
          for (;;) {
            while (i < len && isWsB(buf[i])) { i++; }
            if (buf[i] === RBRACE) break;
            const ns = i;
            i = skipStringFrom(buf, i);
            const ne = i;
            while (i < len && isWsB(buf[i])) { i++; }
            i++;
            while (i < len && isWsB(buf[i])) { i++; }
            const vs = i;
            i = skipValueFrom(buf, i, len);
            out.push({ ns, ne, vs, ve: i });
            while (i < len && isWsB(buf[i])) { i++; }
            if (buf[i] === COMMA) { i++; continue; }
            if (buf[i] === RBRACE) break;
          }
          return { members: out, close: i };
        }
        function findMember(buf: Uint8Array, members: RmwMember[], name: Uint8Array): number {
          for (let k = 0; k < members.length; k++) {
            const m = members[k];
            if (eqName(buf, m.ns + 1, m.ne - 1, name)) return k;
          }
          return -1;
        }
        function memberBytes(name: Uint8Array, content: Uint8Array): Uint8Array {
          const out = new Uint8Array(name.length + content.length + 3);
          out[0] = 0x22; out.set(name, 1); out[name.length + 1] = 0x22; out[name.length + 2] = 0x3a; out.set(content, name.length + 3);
          return out;
        }
        function concatBytes(parts: Uint8Array[]): Uint8Array {
          let len = 0;
          for (let k = 0; k < parts.length; k++) { len += parts[k].length; }
          const out = new Uint8Array(len);
          let o = 0;
          for (let k = 0; k < parts.length; k++) { out.set(parts[k], o); o += parts[k].length; }
          return out;
        }
        function rmwJoinObject(parts: Uint8Array[]): Uint8Array {
          const seq: Uint8Array[] = [new Uint8Array([0x7b])];
          for (let i = 0; i < parts.length; i++) { if (i > 0) seq.push(new Uint8Array([0x2c])); seq.push(parts[i]); }
          seq.push(new Uint8Array([0x7d]));
          return concatBytes(seq);
        }
        function scanArrayElements(buf: Uint8Array): { elems: Array<{ vs: number; ve: number }>; close: number } {
          const len = buf.length;
          let i = 0;
          if (len >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) { i = 3; }
          while (i < len && isWsB(buf[i])) { i++; }
          i++;
          const out: Array<{ vs: number; ve: number }> = [];
          for (;;) {
            while (i < len && isWsB(buf[i])) { i++; }
            if (buf[i] === RBRACKET) break;
            const vs = i;
            i = skipValueFrom(buf, i, len);
            out.push({ vs, ve: i });
            while (i < len && isWsB(buf[i])) { i++; }
            if (buf[i] === COMMA) { i++; continue; }
            if (buf[i] === RBRACKET) break;
          }
          return { elems: out, close: i };
        }
        function rmwArrayBytes(source: Uint8Array, ops: RmwArrayOps, prefixLen: number): Uint8Array {
          const enc = new TextEncoder();
          const scan = scanArrayElements(source);
          const elems = scan.elems;
          const m = elems.length;
          const edits: RmwEdit[] = [];
          const COMMA = new Uint8Array([0x2c]);
          const removed: boolean[] = new Array<boolean>(m).fill(false);
          const appends: unknown[] = [];
          const applyOps = (o: FlatArrayOps, off: number): void => {
            if (o.set) {
              for (const [k, v] of Object.entries(o.set)) {
                const i = off + Number(k);
                edits.push({ offset: elems[i].vs, length: elems[i].ve - elems[i].vs, content: enc.encode(JSON.stringify(v)) });
              }
            }
            if (o.insert) {
              for (const [k, vals] of Object.entries(o.insert)) {
                const i = off + Number(k);
                const parts: Uint8Array[] = [];
                for (let v = 0; v < vals.length; v++) { parts.push(enc.encode(JSON.stringify(vals[v]))); parts.push(COMMA); }
                edits.push({ offset: elems[i].vs, length: 0, content: concatBytes(parts) });
              }
            }
            if (o.removeAt) { for (let r = 0; r < o.removeAt.length; r++) { removed[off + o.removeAt[r]] = true; } }
            if (o.append) { for (let a = 0; a < o.append.length; a++) { appends.push(o.append[a]); } }
          };
          applyOps(ops, 0);
          if (ops.rest) { applyOps(ops.rest, prefixLen); }
          let j = 0;
          while (j < m) {
            if (!removed[j]) { j++; continue; }
            let hi = j;
            while (hi + 1 < m && removed[hi + 1]) { hi++; }
            if (j === 0 && hi === m - 1) { edits.push({ offset: elems[0].vs, length: elems[m - 1].ve - elems[0].vs, content: RMW_EMPTY }); }
            else if (j === 0) { edits.push({ offset: elems[0].vs, length: elems[hi + 1].vs - elems[0].vs, content: RMW_EMPTY }); }
            else if (hi === m - 1) { edits.push({ offset: elems[j - 1].ve, length: elems[m - 1].ve - elems[j - 1].ve, content: RMW_EMPTY }); }
            else { edits.push({ offset: elems[j - 1].ve, length: elems[hi + 1].vs - elems[j - 1].ve, content: COMMA }); }
            j = hi + 1;
          }
          if (appends.length > 0) {
            let surviving = 0;
            for (let k = 0; k < m; k++) { if (!removed[k]) surviving++; }
            const parts: Uint8Array[] = [];
            for (let a = 0; a < appends.length; a++) {
              if (a > 0 || surviving > 0) { parts.push(COMMA); }
              parts.push(enc.encode(JSON.stringify(appends[a])));
            }
            edits.push({ offset: scan.close, length: 0, content: concatBytes(parts) });
          }
          if (edits.length === 0) return source;
          return applyEditsBytes(source, edits);
        }
        function rmwUpsert(source: Uint8Array, targets: RmwTarget[]): Uint8Array {
          if (targets.length === 0) return source;
          scanTargets(source, targets);
          let anyMissing = false;
          for (let i = 0; i < targets.length; i++) { if (targets[i].vbs < 0) { anyMissing = true; break; } }
          if (!anyMissing) {
            const edits: RmwEdit[] = [];
            for (let i = 0; i < targets.length; i++) { edits.push({ offset: targets[i].vbs, length: targets[i].vbe - targets[i].vbs, content: targets[i].content }); }
            return applyEditsBytes(source, edits);
          }
          return rmwProduceFull(source, targets, [], []);
        }
        function rmwProduceFull(source: Uint8Array, setTargets: RmwTarget[], removeNames: Uint8Array[], arrayEdits: RmwArrayEdit[]): Uint8Array {
          const scan = scanAll(source);
          const members = scan.members;
          const n = members.length;
          const edits: RmwEdit[] = [];
          const adds: RmwTarget[] = [];
          for (let t = 0; t < setTargets.length; t++) {
            const i = findMember(source, members, setTargets[t].name);
            if (i < 0) { adds.push(setTargets[t]); }
            else { edits.push({ offset: members[i].vs, length: members[i].ve - members[i].vs, content: setTargets[t].content }); }
          }
          for (let q = 0; q < arrayEdits.length; q++) {
            const i = findMember(source, members, arrayEdits[q].name);
            if (i < 0) throw new Error("patch: array member not present in source");
            const newArr = rmwArrayBytes(source.subarray(members[i].vs, members[i].ve), arrayEdits[q].ops, arrayEdits[q].prefixLen);
            edits.push({ offset: members[i].vs, length: members[i].ve - members[i].vs, content: newArr });
          }
          const removed: boolean[] = new Array<boolean>(n).fill(false);
          for (let r = 0; r < removeNames.length; r++) {
            const i = findMember(source, members, removeNames[r]);
            if (i < 0) throw new Error("patch: removed member not present in source");
            removed[i] = true;
          }
          const COMMA_BYTE = new Uint8Array([0x2c]);
          let j = 0;
          while (j < n) {
            if (!removed[j]) { j++; continue; }
            let hi = j;
            while (hi + 1 < n && removed[hi + 1]) { hi++; }
            if (j === 0 && hi === n - 1) { edits.push({ offset: members[0].ns, length: members[n - 1].ve - members[0].ns, content: RMW_EMPTY }); }
            else if (j === 0) { edits.push({ offset: members[0].ns, length: members[hi + 1].ns - members[0].ns, content: RMW_EMPTY }); }
            else if (hi === n - 1) { edits.push({ offset: members[j - 1].ve, length: members[n - 1].ve - members[j - 1].ve, content: RMW_EMPTY }); }
            else { edits.push({ offset: members[j - 1].ve, length: members[hi + 1].ns - members[j - 1].ve, content: COMMA_BYTE }); }
            j = hi + 1;
          }
          if (adds.length > 0) {
            let surviving = 0;
            for (let k = 0; k < n; k++) { if (!removed[k]) surviving++; }
            const parts: Uint8Array[] = [];
            for (let a = 0; a < adds.length; a++) {
              if (a > 0 || surviving > 0) { parts.push(COMMA_BYTE); }
              parts.push(memberBytes(adds[a].name, adds[a].content));
            }
            edits.push({ offset: scan.close, length: 0, content: concatBytes(parts) });
          }
          if (edits.length === 0) return source;
          return applyEditsBytes(source, edits);
        }
        """;

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();

    // Every top-level identifier emitted into the current module, so the root entry-point name can be
    // reserved against them and renamed around any collision (like type and union-guard names).
    private readonly HashSet<string> moduleTopLevelNames = new(StringComparer.Ordinal);

    // The shared runtime module (@corvus/json-runtime in production): emitted ONCE, imported by every
    // generated module. Assembled from the helper blocks with each top-level declaration exported, and
    // the third-party imports (lossless-json for source-text numbers, Temporal + tr46 for formats) at top.
    public static string RuntimeModuleSource()
    {
        string body = NumericRuntime + KindRuntime + DeepEqual + EvRuntime + FormatRuntime + BrandRuntime + MutationRuntime + RmwRuntime;
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

    // A stable 1-arg entry point for the generated module, aliasing the root type's validator. Call AFTER
    // GenerateCodeFor (names are assigned, and the module's identifiers recorded). The entry name is
    // RESERVED against every emitted identifier and renamed around any collision (validate, validate2, ...);
    // it is also emitted as the module `default` so consumers can import it without depending on the name.
    public string RootEvaluatorExport(TypeDeclaration rootType)
    {
        TypeDeclaration reduced = rootType.ReducedTypeDeclaration().ReducedType;
        string rootName = reduced.TryGetMetadata<string>(TsFinalKey, out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";

        string entry = "validate";
        for (int i = 2; this.moduleTopLevelNames.Contains(entry); i++)
        {
            entry = "validate" + i;
        }

        this.moduleTopLevelNames.Add(entry);
        return $"\nexport const {entry} = (v: unknown): boolean => evaluate{rootName}(v, fresh());\nexport default {entry};\n";
    }

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

    // Like CreateDefault, but asserts `format` (the optional/format suite). The default leaves format as
    // an annotation (the 2020-12 default), which is why top-level format.json expects everything valid.
    public static TypeScriptLanguageProvider CreateWithFormatAssertion()
    {
        TypeScriptLanguageProvider p = CreateDefault();
        p.RegisterValidationHandlers(new TsFormatHandler(), new TsContentHandler());
        return p;
    }

    // Options for the TypeScript provider (driver entry point; design §7.2). Pragmatic minimal set —
    // the full Options surface (runtime module specifier, emitModel, indentWidth, ...) is a follow-up.
    public sealed record Options(bool AlwaysAssertFormat = false);

    // The driver-facing entry point, mirroring CSharpLanguageProvider.DefaultWithOptions.
    public static TypeScriptLanguageProvider DefaultWithOptions(Options options)
        => options.AlwaysAssertFormat ? CreateWithFormatAssertion() : CreateDefault();

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
        // (@corvus/json-runtime, §5.5) that every generated module imports — never inlined per module.
        var sb = new StringBuilder();
        sb.Append("// AUTO-GENERATED: idiomatic TS types + registry-composed validators.\n");
        sb.Append("import { __isNum, __isObj, __isInt, __cmp, __multipleOf, __eq, __re, Ev, NOEV, fresh, __fmt, __fmtContent, FormatError, produce, type Draft, rmwUpsert, rmwProduceFull, type RmwTarget, type ListOps, type RmwArrayOps, type RmwArrayEdit, type Brand } from \"./corvus-runtime.js\";\n\n");
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

        // The generated types/validators module + the shared runtime, emitted once alongside it. (The
        // test harness consumes files.First() and emits its own single shared runtime for the whole suite.)
        return new[]
        {
            new GeneratedCodeFile("generated.ts", content),
            new GeneratedCodeFile("corvus-runtime.ts", RuntimeModuleSource()),
        };
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
        sb.Append("export interface ").Append(FinalName(td)).Append(" {\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
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
            return FinalName(td);
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

    // camelCase (for match() case keys): Pascal with a lowercased first letter.
    private static string Camel(string value)
    {
        string p = Pascal(value);
        return p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..];
    }
}
