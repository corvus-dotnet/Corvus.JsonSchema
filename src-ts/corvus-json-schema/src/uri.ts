// URI handling for schema identification and reference resolution (RFC 3986 section 5).
// Mirrors Corvus.Text.Json.RuntimeEvaluator.Compilation.UriUtilities, but implements reference
// resolution directly so that opaque bases (urn:, tag:) resolve the same way on every host.

interface UriParts {
  scheme?: string;
  authority?: string;
  path: string;
  query?: string;
  fragment?: string;
}

const URI_RE = /^(?:([A-Za-z][A-Za-z0-9+.-]*):)?(?:\/\/([^/?#]*))?([^?#]*)(?:\?([^#]*))?(?:#(.*))?$/s;

function parse(uri: string): UriParts {
  const m = URI_RE.exec(uri)!;
  return {
    scheme: m[1],
    authority: m[2],
    path: m[3] ?? '',
    query: m[4],
    fragment: m[5],
  };
}

function format(p: UriParts): string {
  let s = '';
  if (p.scheme !== undefined) s += p.scheme + ':';
  if (p.authority !== undefined) s += '//' + p.authority;
  s += p.path;
  if (p.query !== undefined) s += '?' + p.query;
  if (p.fragment !== undefined) s += '#' + p.fragment;
  return s;
}

function removeDotSegments(path: string): string {
  if (path.indexOf('.') < 0) return path;
  const input = path.split('/');
  const out: string[] = [];
  for (let i = 0; i < input.length; i++) {
    const seg = input[i];
    if (seg === '.') {
      if (i === input.length - 1) out.push('');
      continue;
    }
    if (seg === '..') {
      if (out.length > 1 || (out.length === 1 && out[0] !== '')) out.pop();
      if (i === input.length - 1) out.push('');
      continue;
    }
    out.push(seg);
  }
  return out.join('/');
}

function merge(base: UriParts, refPath: string): string {
  if (base.authority !== undefined && base.path === '') return '/' + refPath;
  const i = base.path.lastIndexOf('/');
  return i >= 0 ? base.path.slice(0, i + 1) + refPath : refPath;
}

/** True when the reference starts with a URI scheme. */
export function hasScheme(reference: string): boolean {
  return /^[A-Za-z][A-Za-z0-9+.-]*:/.test(reference);
}

/** Splits a reference at its first '#'. */
export function split(reference: string): [uriPart: string, fragment: string] {
  const hash = reference.indexOf('#');
  return hash < 0 ? [reference, ''] : [reference.slice(0, hash), reference.slice(hash + 1)];
}

function normalizeParts(p: UriParts): string {
  const n: UriParts = { ...p, fragment: undefined };
  if (n.scheme !== undefined) n.scheme = n.scheme.toLowerCase();
  if (n.authority !== undefined) {
    n.authority = n.authority.toLowerCase();
    if ((n.scheme === 'http' && n.authority.endsWith(':80')) || (n.scheme === 'https' && n.authority.endsWith(':443'))) {
      n.authority = n.authority.slice(0, n.authority.lastIndexOf(':'));
    }
    if (n.path === '') n.path = '/';
  }
  n.path = removeDotSegments(n.path);
  return format(n);
}

/** Normalises an absolute URI (without its fragment) so that equivalent spellings compare equal. */
export function normalize(uri: string): string {
  const [uriPart] = split(uri);
  if (!hasScheme(uriPart)) return uriPart;
  return normalizeParts(parse(uriPart));
}

/** Resolves a reference (without fragment) against a base URI, returning the normalised absolute URI. */
export function resolve(baseUri: string, reference: string): string {
  if (reference.length === 0) return baseUri;
  const r = parse(reference);
  if (r.scheme !== undefined) return normalizeParts(r);
  if (baseUri.length === 0) return reference;
  const b = parse(baseUri);
  const t: UriParts = { path: '' };
  if (r.authority !== undefined) {
    t.authority = r.authority;
    t.path = removeDotSegments(r.path);
    t.query = r.query;
  } else {
    if (r.path === '') {
      t.path = b.path;
      t.query = r.query !== undefined ? r.query : b.query;
    } else {
      t.path = r.path.startsWith('/') ? removeDotSegments(r.path) : removeDotSegments(merge(b, r.path));
      t.query = r.query;
    }
    t.authority = b.authority;
  }
  t.scheme = b.scheme;
  return t.scheme !== undefined ? normalizeParts(t) : format(t);
}

/** Percent-decodes a fragment. */
export function decodeFragment(fragment: string): string {
  if (fragment.indexOf('%') < 0) return fragment;
  try {
    return decodeURIComponent(fragment);
  } catch {
    return fragment;
  }
}

/** Resolves an RFC 6901 JSON pointer against a JSON value. */
export function resolvePointer(root: unknown, pointer: string): { found: boolean; value: unknown; path: (string | number)[] } {
  if (pointer === '') return { found: true, value: root, path: [] };
  if (pointer[0] !== '/') return { found: false, value: undefined, path: [] };
  const tokens = pointer.slice(1).split('/').map((t) => t.replace(/~1/g, '/').replace(/~0/g, '~'));
  let current: unknown = root;
  const path: (string | number)[] = [];
  for (const token of tokens) {
    if (Array.isArray(current)) {
      if (!/^(0|[1-9][0-9]*)$/.test(token)) return { found: false, value: undefined, path };
      const i = Number(token);
      if (i >= current.length) return { found: false, value: undefined, path };
      current = current[i];
      path.push(i);
    } else if (current !== null && typeof current === 'object') {
      if (!Object.prototype.hasOwnProperty.call(current, token)) return { found: false, value: undefined, path };
      current = (current as Record<string, unknown>)[token];
      path.push(token);
    } else {
      return { found: false, value: undefined, path };
    }
  }
  return { found: true, value: current, path };
}
