// schema-descriptor.js — the shared combiner normalizer (Layer 0.5, DOM-free; sibling of workflow-graph.js).
// It rewrites a RAW JSON Schema 2020-12 node into the descriptor shape value-editor renders, for the exact
// combiner cases the baked generator handles (schema-form design §3.2a). It is the ONE JS implementation of
// those rules — value-editor applies it at its buildField boundary (slice C) and the schema editor's preview
// feeds it — pinned to the .NET generator (WorkflowSchemaMetadataGenerator.WriteTypeDescriptor / :993-1266) by
// mirrored test fixtures. It does NOT resolve $ref (no document root at this seam — that stays the baked path's
// job) and does NOT recurse into a node's children (value-editor recurses and re-normalizes each node).
//
//   import { normalizeDescriptor } from './schema-descriptor.js';
//   const d = normalizeDescriptor(rawSchemaNode); // raw oneOf/anyOf → {type:'union', variants}; simple allOf → merged
//
// Rules (mirroring the generator exactly):
//   - oneOf/anyOf (oneOf first): pure-null branches collapse to `nullable`; a lone remaining variant unwraps;
//     two+ become {type:'union', variants} (variants left RAW — value-editor re-normalizes each). Zero → null/unknown.
//   - allOf, simple property-merge case only: every branch an object schema contributing properties/required
//     (+ title/description); merge = properties ∪ required, same-key overlaps allowed only when the subschemas
//     agree (ORDER-INSENSITIVE structural equality — never JSON.stringify, which the generator does not use).
//     Any conflict / non-object branch / other branch keyword ⇒ raw fallback (the node passes through unchanged,
//     never a guessed last-writer-wins merge).
//   - Everything else (already-normalized descriptors, plain typed schemas, null) passes through unchanged.

// Keywords an allOf branch may carry and still count as a "simple" mergeable object contribution.
const SIMPLE_ALLOF_KEYS = new Set(['type', 'properties', 'required', 'title', 'description']);

export function normalizeDescriptor(schema) {
  if (schema == null || typeof schema !== 'object' || Array.isArray(schema)) return schema;

  // Polymorphic union (oneOf/anyOf). oneOf takes precedence, matching the generator's read order.
  const union = readUnion(schema);
  if (union) {
    const { variants, hasNull } = union;
    if (variants.length === 1) {
      const inner = normalizeDescriptor(variants[0]);
      return hasNull && isPlainObject(inner) ? { ...inner, nullable: true } : inner;
    }
    if (variants.length === 0) return { type: hasNull ? 'null' : 'unknown' };
    const out = { type: 'union' };
    if (hasNull) out.nullable = true;
    if (typeof schema.description === 'string') out.description = schema.description;
    const discriminator = discriminatorOf(schema);
    if (discriminator) out.discriminator = discriminator;
    out.variants = variants; // RAW — value-editor's unionField recurses through buildField (re-normalized there)
    return out;
  }

  // allOf simple property-merge (no union present).
  if (Array.isArray(schema.allOf)) {
    const merged = mergeSimpleAllOf(schema);
    if (merged) return merged;
    return schema; // not-simple ⇒ raw fallback (value-editor degrades to the JSON editor)
  }

  return schema; // already a descriptor / plain typed schema
}

// oneOf/anyOf (oneOf first) → { variants (non-null branches, RAW), hasNull } | null.
function readUnion(schema) {
  const list = Array.isArray(schema.oneOf) ? schema.oneOf : (Array.isArray(schema.anyOf) ? schema.anyOf : null);
  if (!list) return null;
  const variants = [];
  let hasNull = false;
  for (const branch of list) {
    if (isPureNullSchema(branch)) hasNull = true;
    else variants.push(branch);
  }
  return { variants, hasNull };
}

// { "type": "null" } or { "type": ["null"] } — mirrors the generator's IsPureNullSchema.
function isPureNullSchema(schema) {
  if (!isPlainObject(schema) || schema.type === undefined) return false;
  if (typeof schema.type === 'string') return schema.type === 'null';
  if (Array.isArray(schema.type)) return schema.type.length > 0 && schema.type.every((t) => t === 'null');
  return false;
}

// The explicit OpenAPI discriminator.propertyName (JSON Schema has none) — read opportunistically.
function discriminatorOf(schema) {
  const d = schema.discriminator;
  return isPlainObject(d) && typeof d.propertyName === 'string' && d.propertyName.length > 0 ? d.propertyName : null;
}

// The simple property-merge case, or null if not simple.
function mergeSimpleAllOf(schema) {
  const branches = schema.allOf;
  const properties = {};
  const required = [];
  for (const branch of branches) {
    if (!isPlainObject(branch)) return null; // non-object branch (e.g. a boolean or $ref)
    for (const key of Object.keys(branch)) {
      if (!SIMPLE_ALLOF_KEYS.has(key)) return null; // a branch keyword beyond the mergeable set
    }
    if (branch.type !== undefined && branch.type !== 'object') return null;
    if (branch.properties !== undefined && !isPlainObject(branch.properties)) return null;
    for (const [name, sub] of Object.entries(branch.properties || {})) {
      if (name in properties && !deepEqualUnordered(properties[name], sub)) return null; // conflicting overlap
      properties[name] = sub; // agreeing overlap or new property — RAW subschema (value-editor recurses)
    }
    for (const name of branch.required || []) {
      if (!required.includes(name)) required.push(name);
    }
  }
  const out = { type: 'object', properties };
  if (required.length) out.required = required;
  // Siblings on the node itself (title/description alongside allOf) survive.
  if (typeof schema.title === 'string') out.title = schema.title;
  if (typeof schema.description === 'string') out.description = schema.description;
  return out;
}

function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

// Order-insensitive structural equality (the generator's "agree" for overlapping allOf properties — deliberately
// NOT JSON.stringify, which is key-order-sensitive and would disagree with the .NET side on reordered keys).
export function deepEqualUnordered(a, b) {
  if (a === b) return true;
  if (a == null || b == null) return a === b;
  if (typeof a !== typeof b) return false;
  if (typeof a !== 'object') return a === b;
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false;
    return a.every((x, i) => deepEqualUnordered(x, b[i]));
  }
  const ak = Object.keys(a);
  const bk = Object.keys(b);
  if (ak.length !== bk.length) return false;
  return ak.every((k) => Object.prototype.hasOwnProperty.call(b, k) && deepEqualUnordered(a[k], b[k]));
}
