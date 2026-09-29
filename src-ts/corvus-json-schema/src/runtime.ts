// Helpers called by generated validators. Everything here is allocation-free on the common paths.
// Standalone modules emitted by generateModule() import this file, so its exports are public API.

import { formatValidators, legacyHostname, numericFormatValidators } from './formats.js';

export { formatValidators, legacyHostname, numericFormatValidators };
import { SchemaEvaluationDepthError } from './options.js';

export { SchemaEvaluationDepthError };

/** JSON equality: numbers by value, objects by their property sets, arrays element-wise. */
export function equal(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) return false;
  if (Array.isArray(a)) {
    if (!Array.isArray(b) || a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) if (!equal(a[i], b[i])) return false;
    return true;
  }
  if (Array.isArray(b)) return false;
  const ao = a as Record<string, unknown>;
  const bo = b as Record<string, unknown>;
  let count = 0;
  for (const k in ao) {
    if (!Object.prototype.hasOwnProperty.call(bo, k) || !equal(ao[k], bo[k])) return false;
    count++;
  }
  for (const _ in bo) count--;
  return count === 0;
}

/** True when some element of `values` is JSON-equal to `x`. */
export function includes(values: readonly unknown[], x: unknown): boolean {
  for (let i = 0; i < values.length; i++) if (equal(values[i], x)) return true;
  return false;
}

/** A hash of a JSON value that agrees with JSON equality (object hashing is order-independent). */
function hash(v: unknown): number {
  switch (typeof v) {
    case 'string': {
      let h = 0x811c9dc5;
      for (let i = 0; i < v.length; i++) h = Math.imul(h ^ v.charCodeAt(i), 0x01000193);
      return h;
    }
    case 'number':
      return Math.imul((v | 0) ^ Math.floor(v * 1024), 0x9e3779b1) ^ 0x1234;
    case 'boolean':
      return v ? 0x51 : 0x52;
    case 'object': {
      if (v === null) return 0x53;
      if (Array.isArray(v)) {
        let h = 0x54 + v.length;
        for (let i = 0; i < v.length; i++) h = Math.imul(h, 31) + hash(v[i]);
        return h | 0;
      }
      let h = 0x55;
      const o = v as Record<string, unknown>;
      for (const k in o) h = (h + (Math.imul(hash(k), 0x2c1b3c6d) ^ hash(o[k]))) | 0;
      return h;
    }
    default:
      return 0;
  }
}

/** `uniqueItems`: primitives through a Set (SameValueZero agrees with JSON equality), structures by hash + equality. */
export function unique(a: readonly unknown[]): boolean {
  const n = a.length;
  if (n < 2) return true;
  let structured = false;
  for (let i = 0; i < n; i++) {
    const v = a[i];
    if (typeof v === 'object' && v !== null) {
      structured = true;
      break;
    }
  }
  if (!structured) {
    if (n <= 8) {
      for (let i = 1; i < n; i++) for (let j = 0; j < i; j++) if (a[i] === a[j]) return false;
      return true;
    }
    return new Set(a).size === n;
  }
  if (n <= 8) {
    for (let i = 1; i < n; i++) for (let j = 0; j < i; j++) if (equal(a[i], a[j])) return false;
    return true;
  }
  const buckets = new Map<number, number[]>();
  for (let i = 0; i < n; i++) {
    const h = hash(a[i]);
    const b = buckets.get(h);
    if (b === undefined) {
      buckets.set(h, [i]);
    } else {
      for (const j of b) if (equal(a[i], a[j])) return false;
      b.push(i);
    }
  }
  return true;
}

/** The length of a string in code points (what `minLength`/`maxLength` count). */
export function codePoints(s: string): number {
  let count = s.length;
  for (let i = 0; i < s.length; i++) {
    const c = s.charCodeAt(i);
    if (c >= 0xd800 && c <= 0xdbff && i + 1 < s.length) {
      const d = s.charCodeAt(i + 1);
      if (d >= 0xdc00 && d <= 0xdfff) {
        count--;
        i++;
      }
    }
  }
  return count;
}

interface Decimal {
  m: bigint;
  e: number;
}

function toDecimal(x: number): Decimal {
  // The shortest round-trip decimal form of the double, which is what the JSON text said for any
  // value JSON.parse produced.
  const s = String(x);
  const match = /^(-?)(\d+)(?:\.(\d+))?(?:e([+-]?\d+))?$/.exec(s)!;
  const frac = match[3] ?? '';
  const m = BigInt(match[1] + match[2] + frac);
  return { m, e: Number(match[4] ?? '0') - frac.length };
}

/** Exact `multipleOf` over the decimal forms of both numbers (divisors that are not integers). */
export function multipleOf(x: number, d: number): boolean {
  if (!Number.isFinite(x)) return false;
  const q = x / d;
  if (Number.isFinite(q) && Math.abs(q) < 2 ** 52 && !Number.isInteger(q) && Math.abs(q - Math.round(q)) > 1e-6) {
    return false;
  }
  const a = toDecimal(x);
  const b = toDecimal(d);
  if (b.m === 0n) return false;
  // x / d is an integer iff a.m * 10^(a.e - b.e) is divisible by b.m.
  let num = a.m;
  let den = b.m;
  const shift = a.e - b.e;
  if (shift >= 0) num *= 10n ** BigInt(shift);
  else den *= 10n ** BigInt(-shift);
  return num % den === 0n;
}

const BASE64_RE = /^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/;

function decodeBase64(s: string): string | undefined {
  if (!BASE64_RE.test(s)) return undefined;
  const binary = atob(s);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return new TextDecoder().decode(bytes);
}

function isJson(s: string): boolean {
  try {
    JSON.parse(s);
    return true;
  } catch {
    return false;
  }
}

/** Draft 7 content assertion: 1 = base64, 2 = application/json, 3 = both. */
export function content(s: string, kind: number): boolean {
  switch (kind) {
    case 1:
      return BASE64_RE.test(s);
    case 2:
      return isJson(s);
    case 3: {
      const decoded = decodeBase64(s);
      return decoded !== undefined && isJson(decoded);
    }
    default:
      return true;
  }
}

/** Builds a Map from names to small integers for property dispatch. */
export function nameMap(names: readonly string[]): Map<string, number> {
  const m = new Map<string, number>();
  names.forEach((n, i) => m.set(n, i));
  return m;
}

/** Called by generated code when in-place recursion exceeds the configured depth. */
export function depthExceeded(): never {
  throw new SchemaEvaluationDepthError();
}
