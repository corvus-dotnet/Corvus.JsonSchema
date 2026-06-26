import { isLosslessNumber } from "lossless-json";
import { Temporal } from "@js-temporal/polyfill";
import tr46 from "tr46";

export function __dec(s: string): { sign: bigint; mant: bigint; exp: number } {
  let i = 0; let sign = 1n;
  if (s[i] === "+") { i++; } else if (s[i] === "-") { sign = -1n; i++; }
  let ip = "", fp = "", ep = "";
  while (i < s.length && s[i] >= "0" && s[i] <= "9") { ip += s[i++]; }
  if (s[i] === ".") { i++; while (i < s.length && s[i] >= "0" && s[i] <= "9") { fp += s[i++]; } }
  if (s[i] === "e" || s[i] === "E") { i++; let es = ""; if (s[i] === "+" || s[i] === "-") { es = s[i++]; } while (i < s.length && s[i] >= "0" && s[i] <= "9") { es += s[i++]; } ep = es; }
  return { sign, mant: BigInt((ip + fp) || "0"), exp: (ep ? parseInt(ep, 10) : 0) - fp.length };
}
export function __cmp(aStr: string, bStr: string): number {
  const a = __dec(aStr), b = __dec(bStr); const e = Math.min(a.exp, b.exp);
  const ai = a.sign * a.mant * 10n ** BigInt(a.exp - e); const bi = b.sign * b.mant * 10n ** BigInt(b.exp - e);
  return ai < bi ? -1 : ai > bi ? 1 : 0;
}
export function __multipleOf(vStr: string, dStr: string): boolean {
  const v = __dec(vStr), d = __dec(dStr); if (d.mant === 0n) { return false; }
  const e = Math.min(v.exp, d.exp);
  return (v.mant * 10n ** BigInt(v.exp - e)) % (d.mant * 10n ** BigInt(d.exp - e)) === 0n;
}

export const __isNum = (v: unknown): boolean => isLosslessNumber(v) || typeof v === "number";
export const __isObj = (v: unknown): v is Record<string, unknown> => typeof v === "object" && v !== null && !Array.isArray(v) && !isLosslessNumber(v);
export const __isInt = (s: string): boolean => { const d = __dec(s); return d.exp >= 0 || d.mant % (10n ** BigInt(-d.exp)) === 0n; };

export function __eq(a: unknown, b: unknown): boolean {
  if (__isNum(a) || __isNum(b)) { return __isNum(a) && __isNum(b) && __cmp(String(a), String(b)) === 0; }
  if (a === b) { return true; }
  if (typeof a !== typeof b || a === null || b === null) { return false; }
  if (Array.isArray(a)) { if (!Array.isArray(b) || a.length !== b.length) { return false; } for (let i = 0; i < a.length; i++) { if (!__eq(a[i], b[i])) { return false; } } return true; }
  if (typeof a === "object") { if (Array.isArray(b) || typeof b !== "object") { return false; } const ak = Object.keys(a as object), bk = Object.keys(b as object); if (ak.length !== bk.length) { return false; } for (const k of ak) { if (!Object.prototype.hasOwnProperty.call(b, k) || !__eq((a as Record<string, unknown>)[k], (b as Record<string, unknown>)[k])) { return false; } } return true; }
  return false;
}

export class Ev {
  n = false; pl = 0; pa = 0; il = 0; ia = 0;
  po: Set<number> | null = null; poa: Set<number> | null = null; io: Set<number> | null = null; ioa: Set<number> | null = null;
  markProp(i: number): void { if (this.n) { return; } if (i < 32) { this.pl |= (1 << i) >>> 0; } else { (this.po ??= new Set<number>()).add(i); } }
  hasProp(i: number): boolean { return i < 32 ? (((this.pl | this.pa) & ((1 << i) >>> 0)) !== 0) : ((this.po !== null && this.po.has(i)) || (this.poa !== null && this.poa.has(i))); }
  markItem(i: number): void { if (this.n) { return; } if (i < 32) { this.il |= (1 << i) >>> 0; } else { (this.io ??= new Set<number>()).add(i); } }
  hasItem(i: number): boolean { return i < 32 ? (((this.il | this.ia) & ((1 << i) >>> 0)) !== 0) : ((this.io !== null && this.io.has(i)) || (this.ioa !== null && this.ioa.has(i))); }
  mergeProps(c: Ev): void { if (this.n) { return; } this.pa |= c.pl | c.pa; if (c.po !== null || c.poa !== null) { const s = (this.poa ??= new Set<number>()); if (c.po !== null) { for (const x of c.po) { s.add(x); } } if (c.poa !== null) { for (const x of c.poa) { s.add(x); } } } }
  mergeItems(c: Ev): void { if (this.n) { return; } this.ia |= c.il | c.ia; if (c.io !== null || c.ioa !== null) { const s = (this.ioa ??= new Set<number>()); if (c.io !== null) { for (const x of c.io) { s.add(x); } } if (c.ioa !== null) { for (const x of c.ioa) { s.add(x); } } } }
}
export const NOEV = new Ev(); NOEV.n = true;
export function fresh(): Ev { return new Ev(); }

export const __dim = (y: number, m: number): number => m === 2 ? (((y % 4 === 0 && y % 100 !== 0) || y % 400 === 0) ? 29 : 28) : [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][m - 1];
export function __fmtDate(s: string): boolean {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
  if (m === null) { return false; }
  const mo = Number(m[2]), d = Number(m[3]);
  return mo >= 1 && mo <= 12 && d >= 1 && d <= __dim(Number(m[1]), mo);
}
export function __fmtTime(s: string): boolean {
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
export function __fmtDateTime(s: string): boolean {
  const m = /^(\d{4}-\d{2}-\d{2})[tT](\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[zZ]|[+-]\d{2}:\d{2}))$/.exec(s);
  return m !== null && __fmtDate(m[1]) && __fmtTime(m[2]);
}
// duration: RFC 3339 ABNF — a REGULAR grammar, STRICTER than ISO 8601 / Temporal (units must be
// contiguous & descending: P1Y2D and PT1H2S are invalid; no fractions). Temporal.Duration is the
// accessor TYPE for a duration, but it is too lenient (ISO 8601) to VALIDATE this format.
export const __durRe = /^P(?:\d+W|(?:\d+Y(?:\d+M(?:\d+D)?)?|\d+M(?:\d+D)?|\d+D)?(?:T(?:\d+H(?:\d+M(?:\d+S)?)?|\d+M(?:\d+S)?|\d+S))?)$/;
export function __fmtDuration(s: string): boolean { return s !== "P" && __durRe.test(s); }
export const __ipv4Re = /^((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$/;
export function __fmtIpv6(s: string): boolean {
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
export const __uuidRe = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
export const __jsonPtrRe = /^(?:\/(?:[^~/]|~0|~1)*)*$/;
export const __relJsonPtrRe = /^(?:0|[1-9][0-9]*)(?:#|(?:\/(?:[^~/]|~0|~1)*)*)$/;
export const __uriTemplateRe = /^(?:[^\s{}]|\{[^\s{}]*\})*$/;
// hostname / idn-hostname via UTS-46 (tr46) for LDH/punycode/length, PLUS the IDNA2008 (RFC 5892)
// validity rules tr46's UTS-46 processing does not enforce: a small DISALLOWED set and the
// CONTEXTO rules (MIDDLE DOT between 'l', Greek KERAIA before Greek, Hebrew GERESH/GERSHAYIM after
// Hebrew), checked on the decoded U-labels.
export const __isGreek = (c: number | undefined): boolean => c !== undefined && ((c >= 0x370 && c <= 0x3FF) || (c >= 0x1F00 && c <= 0x1FFF));
export const __isHebrew = (c: number | undefined): boolean => c !== undefined && ((c >= 0x590 && c <= 0x5FF) || (c >= 0xFB1D && c <= 0xFB4F));
export function __idnaLabel(label: string): boolean {
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
export const __idnaOpt = { checkHyphens: true, checkBidi: true, checkJoiners: true, useSTD3ASCIIRules: true, verifyDNSLength: true };
export function __fmtHostname(s: string, idn: boolean): boolean {
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
export const __isHex = (c: string): boolean => (c >= "0" && c <= "9") || (c >= "a" && c <= "f") || (c >= "A" && c <= "F");
export function __validUriPart(s: string, allowUnicode: boolean): boolean {
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (c === "%") { if (!__isHex(s[i + 1]) || !__isHex(s[i + 2])) { return false; } i += 2; continue; }
    if ((c >= "A" && c <= "Z") || (c >= "a" && c <= "z") || (c >= "0" && c <= "9") || "-._~:/?#[]@!$&'()*+,;=".indexOf(c) >= 0) { continue; }
    if (allowUnicode && s.charCodeAt(i) > 127) { continue; }
    return false;
  }
  return true;
}
export function __hasScheme(s: string): boolean {
  if (s.length === 0 || !((s[0] >= "A" && s[0] <= "Z") || (s[0] >= "a" && s[0] <= "z"))) { return false; }
  let i = 1;
  while (i < s.length && ((s[i] >= "A" && s[i] <= "Z") || (s[i] >= "a" && s[i] <= "z") || (s[i] >= "0" && s[i] <= "9") || s[i] === "+" || s[i] === "." || s[i] === "-")) { i++; }
  return s[i] === ":";
}
// Validate the authority component (after "//", up to the next /?#): [userinfo@]host[:port],
// host = [IP-literal] | IPv4 | reg-name, port = DIGIT*. Catches bad ports and malformed IP-literals.
export function __validAuthority(auth: string, allowUnicode: boolean): boolean {
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
export const __allDigits = (s: string): boolean => { for (let i = 0; i < s.length; i++) { if (s[i] < "0" || s[i] > "9") { return false; } } return true; };
export function __fmtUri(s: string, requireScheme: boolean, allowUnicode: boolean): boolean {
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
export function __fmtEmail(s: string, idn: boolean): boolean {
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
export function __fmtRegex(s: string): boolean { try { new RegExp(s, "u"); return true; } catch { return false; } }
// Compile a JSON Schema `pattern`/`patternProperties` regex ONCE (cached). Unlike `format: regex`,
// the pattern KEYWORD is applied leniently: real-world patterns use identity escapes like \& \%
// that are valid ECMA-262 without `u` but a SyntaxError with it (e.g. krakend). Try `u` first
// (correct Unicode semantics, matches the test suite's pattern cases), fall back to non-`u`.
export const __reCache = new Map<string, RegExp>();
export function __re(p: string): RegExp { let r = __reCache.get(p); if (r === undefined) { try { r = new RegExp(p, "u"); } catch { r = new RegExp(p); } __reCache.set(p, r); } return r; }
// contentEncoding (base64) + contentMediaType (application/json) assertion -- annotation-only by
// default, asserted by the optional/content suite.
export function __fmtContent(s: string, encoding: string | null, mediaType: string | null): boolean {
  let content = s;
  if (encoding === "base64") {
    if (s.length % 4 !== 0) { return false; }
    for (let i = 0; i < s.length; i++) { const c = s[i]; if (!((c >= "A" && c <= "Z") || (c >= "a" && c <= "z") || (c >= "0" && c <= "9") || c === "+" || c === "/" || c === "=")) { return false; } }
    try { content = atob(s); } catch { return false; }
  }
  if (mediaType === "application/json") { try { JSON.parse(content); } catch { return false; } }
  return true;
}
export function __fmt(name: string, s: string): boolean {
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

declare const __brand: unique symbol;
export type Brand<T, B extends string> = T & { readonly [__brand]: B };
export class FormatError extends Error {}
export type Draft<T> =
  T extends ReadonlyArray<infer E> ? Draft<E>[] :
  T extends object ? { -readonly [K in keyof T]: Draft<T[K]> } :
  T;
export interface JsonDocument<T> { readonly value: T; }
export interface JsonPatchOp { readonly op: "replace" | "add" | "remove"; readonly path: string; readonly value?: unknown; }
export const __escPtr = (k: string): string => k.replace(/~/g, "~0").replace(/\//g, "~1");
// immer-faithful recorder: structuredClone the substrate, run the recipe DIRECTLY on the clone
// (plain mutation -- no Proxy), then diff(original, clone) -> a minimal RFC 6902 change-set. Array
// structural ops (push/splice/pop/unshift) "just work": a length change re-serialises that one array
// (siblings byte-preserved), a same-length array diffs per element. The change-set lowers to bytes.
export function __diff(orig: any, mut: any, ptr: string, out: JsonPatchOp[]): void {
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
export function recordChanges<T>(value: T, recipe: (draft: Draft<T>) => void): { next: T; patches: JsonPatchOp[] } {
  const next = structuredClone(value);
  recipe(next as unknown as Draft<T>);
  const patches: JsonPatchOp[] = [];
  __diff(value, next, "", patches);
  return { next, patches };
}

interface __DraftOp { path: string[]; op: "replace" | "add" | "remove" | "append"; value?: unknown; }
export function __groupHead(ops: __DraftOp[]): Map<string, __DraftOp[]> {
  const g = new Map<string, __DraftOp[]>();
  for (const op of ops) { const h = op.path[0]; const cur = g.get(h); if (cur) cur.push(op); else g.set(h, [op]); }
  return g;
}
// Recursive change-set -> byte-patch lowering: descend each container level, locate the sub-span (object
// scan or array scan), recurse into it, splice back. Reuses the RMW scan/splice primitives.
export function __lower(bytes: Uint8Array, ops: __DraftOp[]): Uint8Array {
  let i = 0;
  if (bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) i = 3;
  while (i < bytes.length && isWsB(bytes[i])) i++;
  return bytes[i] === 0x5b ? __lowerArray(bytes, ops) : __lowerObject(bytes, ops);
}
export function __lowerObject(bytes: Uint8Array, ops: __DraftOp[]): Uint8Array {
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
export function __lowerArray(bytes: Uint8Array, ops: __DraftOp[]): Uint8Array {
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
export function __decodeValue(bytes: Uint8Array, vs: number, ve: number): unknown { return JSON.parse(new TextDecoder().decode(bytes.subarray(vs, ve))); }
export function __spanKind(bytes: Uint8Array, vs: number): number { const c = bytes[vs]; return c === 0x7b ? 0 : c === 0x5b ? 1 : 2; }
// Lazy drafts. An object navigates by byte span (no value decode); a scalar decodes on read; an
// array is element-lazy too: items[i].field = v touches only element i's span, an in-bounds index
// set becomes a per-element edit, and a trailing push records into `appends` (no decode) -> an
// 'append' op that inserts before ']'. Only a reordering op (splice/unshift/pop/length-shrink, or a
// proxy stored at the tail) materialises (decode + fold). Both draft kinds share { proxy, value, finalize }.
export const __ISIDX = /^(0|[1-9][0-9]*)$/;
export const __DRAFT = Symbol("draft");      // tags our draft proxies so a shift (unshift/splice) storing a proxy is detectable
export const __DRAFTVAL = Symbol("draftval"); // reads a draft proxy's resolved plain value (for a relocated element)
export function __isDraftP(v: any): boolean { return v !== null && typeof v === "object" && v[__DRAFT] === true; }
export function __resolveV(v: any): any { return __isDraftP(v) ? v[__DRAFTVAL] : v; }
interface __Draft { proxy: any; value: () => any; finalize: (out: __DraftOp[]) => void; }
type __Kid = { kind: 0 | 1; d: __Draft };
export function __makeObjectDraft(bytes: Uint8Array, vs: number, ve: number, path: string[]): __Draft {
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
export function __makeArrayDraft(bytes: Uint8Array, vs: number, ve: number, path: string[]): __Draft {
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
export function produce<T>(source: Uint8Array, recipe: (draft: Draft<T>) => void): Uint8Array {
  const root = __makeObjectDraft(source, 0, source.length, []);
  recipe(root.proxy as Draft<T>);
  const ops: __DraftOp[] = [];
  root.finalize(ops);
  if (ops.length === 0) return source;
  return __lower(source, ops);
}
export interface RmwEdit { offset: number; length: number; content: Uint8Array; }
export interface RmwTarget { name: Uint8Array; content: Uint8Array; vbs: number; vbe: number; }
export const QUOTE = 0x22, BACKSLASH = 0x5c, COMMA = 0x2c, LBRACE = 0x7b, RBRACE = 0x7d, LBRACK = 0x5b, RBRACK = 0x5d;
export function isWsB(b: number): boolean { return b === 0x20 || b === 0x09 || b === 0x0a || b === 0x0d; }
export function skipStringFrom(buf: Uint8Array, i: number): number {
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
export function skipContainerFrom(buf: Uint8Array, i: number, len: number): number {
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
export function skipValueFrom(buf: Uint8Array, i: number, len: number): number {
  const b = buf[i];
  if (b === QUOTE) return skipStringFrom(buf, i);
  if (b === LBRACE || b === LBRACK) return skipContainerFrom(buf, i, len);
  while (i < len) { const c = buf[i]; if (c === COMMA || c === RBRACE || c === RBRACK || isWsB(c)) break; i++; }
  return i;
}
export function eqName(buf: Uint8Array, start: number, end: number, name: Uint8Array): boolean {
  if (end - start !== name.length) return false;
  for (let k = 0; k < name.length; k++) { if (buf[start + k] !== name[k]) return false; }
  return true;
}
export function scanTargets(buf: Uint8Array, targets: RmwTarget[]): boolean {
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
export function applyEditsBytes(buf: Uint8Array, edits: RmwEdit[]): Uint8Array {
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
export const RMW_EMPTY = new Uint8Array(0);
export const RBRACKET = 0x5d;
export function scanAll(buf: Uint8Array): { members: RmwMember[]; close: number } {
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
export function findMember(buf: Uint8Array, members: RmwMember[], name: Uint8Array): number {
  for (let k = 0; k < members.length; k++) {
    const m = members[k];
    if (eqName(buf, m.ns + 1, m.ne - 1, name)) return k;
  }
  return -1;
}
export function memberBytes(name: Uint8Array, content: Uint8Array): Uint8Array {
  const out = new Uint8Array(name.length + content.length + 3);
  out[0] = 0x22; out.set(name, 1); out[name.length + 1] = 0x22; out[name.length + 2] = 0x3a; out.set(content, name.length + 3);
  return out;
}
export function concatBytes(parts: Uint8Array[]): Uint8Array {
  let len = 0;
  for (let k = 0; k < parts.length; k++) { len += parts[k].length; }
  const out = new Uint8Array(len);
  let o = 0;
  for (let k = 0; k < parts.length; k++) { out.set(parts[k], o); o += parts[k].length; }
  return out;
}
export function rmwJoinObject(parts: Uint8Array[]): Uint8Array {
  const seq: Uint8Array[] = [new Uint8Array([0x7b])];
  for (let i = 0; i < parts.length; i++) { if (i > 0) seq.push(new Uint8Array([0x2c])); seq.push(parts[i]); }
  seq.push(new Uint8Array([0x7d]));
  return concatBytes(seq);
}
export function scanArrayElements(buf: Uint8Array): { elems: Array<{ vs: number; ve: number }>; close: number } {
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
export function rmwArrayBytes(source: Uint8Array, ops: RmwArrayOps, prefixLen: number): Uint8Array {
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
export function rmwUpsert(source: Uint8Array, targets: RmwTarget[]): Uint8Array {
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
export function rmwProduceFull(source: Uint8Array, setTargets: RmwTarget[], removeNames: Uint8Array[], arrayEdits: RmwArrayEdit[]): Uint8Array {
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