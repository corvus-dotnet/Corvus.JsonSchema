// schema-authoring.js — the schema-editor's shape logic (Layer 0.5, DOM-free; sibling of workflow-graph.js).
// It classifies a JSON Schema 2020-12 node for the visual tier, surfaces the keywords the form does not render,
// and mutates schemas IN PLACE (the document IS the model, so edits are ordinary model ops). It knows nothing
// about SVG or the DOM (schema-form design §3). Combiner normalization for RENDERING lives in its sibling
// `schema-descriptor.js`; this module authors the raw schema and never resolves $ref.
//
//   import { classifyNode, unrenderedKeywords, addProperty, renameProperty, setType, emissionCheck } from './schema-authoring.js';
//
// Losslessness (design §3.3): a visual edit touching only renderable keywords of a non-advanced node leaves every
// other byte (keys, order, values) equal — the mutation helpers preserve key order and never rewrite siblings.

import { normalizeDescriptor } from './schema-descriptor.js';

// The single scalar/structural types the visual tier authors directly. `type: [...]` arrays are advanced.
export const RENDERABLE_TYPES = ['object', 'array', 'string', 'number', 'integer', 'boolean', 'null'];
export const COMBINER_KINDS = ['oneOf', 'anyOf', 'allOf'];

// Constructs that force a node to the advanced (JSON-only) tier (design §3.3).
const ADVANCED_KEYWORDS = ['not', '$ref', 'patternProperties', 'prefixItems', '$defs', 'dependentSchemas', 'if', 'then', 'else'];

// Keywords the visual tier renders for a node (everything else present becomes a "+N more" chip, §3.3).
const RENDERED_KEYWORDS = new Set([
  'type', 'title', 'description', 'properties', 'required', 'additionalProperties', 'items',
  'enum', 'const', 'default', 'format', 'pattern', 'minLength', 'maxLength',
  'minimum', 'maximum', 'exclusiveMinimum', 'exclusiveMaximum', 'multipleOf',
  'minItems', 'maxItems', 'uniqueItems', 'oneOf', 'anyOf', 'allOf', 'discriminator',
]);

/**
 * Classify a schema node for the visual tier.
 * @returns {{kind:'boolean-schema'|'combiner'|'advanced'|'renderable', combiner?:string, advancedBy?:string}}
 */
export function classifyNode(schema) {
  if (schema === true || schema === false) return { kind: 'boolean-schema' };
  if (!isPlainObject(schema)) return { kind: 'renderable' }; // treat a missing node as an editable blank

  const advancedBy = ADVANCED_KEYWORDS.find((k) => schema[k] !== undefined);
  if (advancedBy) return { kind: 'advanced', advancedBy };
  if (Array.isArray(schema.type)) return { kind: 'advanced', advancedBy: 'type[]' };

  const combiners = COMBINER_KINDS.filter((k) => Array.isArray(schema[k]));
  if (combiners.length > 1) return { kind: 'advanced', advancedBy: combiners.join('+') }; // multiple combiners at once
  if (combiners.length === 1) return { kind: 'combiner', combiner: combiners[0] };

  if (schema.type !== undefined && !RENDERABLE_TYPES.includes(schema.type)) return { kind: 'advanced', advancedBy: 'type' };
  return { kind: 'renderable' };
}

/** The present keywords the visual tier does not surface for this node (the "+N more" list). */
export function unrenderedKeywords(schema) {
  if (!isPlainObject(schema)) return [];
  return Object.keys(schema).filter((k) => !RENDERED_KEYWORDS.has(k));
}

// ── property mutations (in place; key order preserved) ────────────────────────────────────────────

/** Append a property (name → sub-schema) without disturbing existing key order. */
export function addProperty(schema, name, sub = { type: 'string' }) {
  schema.properties = schema.properties || {};
  schema.properties[name] = sub;
  return schema;
}

/** Rename a property IN PLACE at its position, rewriting the parent `required` entry so requiredness
 *  survives (an unsynced rename silently drops it and spawns a ghost row). */
export function renameProperty(schema, oldName, newName) {
  if (!isPlainObject(schema.properties) || !(oldName in schema.properties) || oldName === newName) return schema;
  const rebuilt = {};
  for (const [k, v] of Object.entries(schema.properties)) rebuilt[k === oldName ? newName : k] = v;
  schema.properties = rebuilt;
  if (Array.isArray(schema.required)) {
    schema.required = schema.required.map((r) => (r === oldName ? newName : r));
  }
  return schema;
}

/** Move a property to a new index within the object's key order. */
export function reorderProperty(schema, name, toIndex) {
  if (!isPlainObject(schema.properties) || !(name in schema.properties)) return schema;
  const keys = Object.keys(schema.properties);
  const from = keys.indexOf(name);
  keys.splice(from, 1);
  keys.splice(Math.max(0, Math.min(toIndex, keys.length)), 0, name);
  const rebuilt = {};
  for (const k of keys) rebuilt[k] = schema.properties[k];
  schema.properties = rebuilt;
  return schema;
}

/** Remove a property and drop it from `required` (deleting the array when it empties). */
export function removeProperty(schema, name) {
  if (isPlainObject(schema.properties)) delete schema.properties[name];
  if (Array.isArray(schema.required)) {
    schema.required = schema.required.filter((r) => r !== name);
    if (!schema.required.length) delete schema.required;
  }
  return schema;
}

/** Toggle a property's requiredness on the parent `required` array (deleting it when empty). */
export function setRequired(schema, name, on) {
  const set = new Set(Array.isArray(schema.required) ? schema.required : []);
  if (on) set.add(name); else set.delete(name);
  if (set.size) schema.required = [...set]; else delete schema.required;
  return schema;
}

// ── type & combiner mutations ─────────────────────────────────────────────────────────────────────

// Keywords a given renderable type keeps; anything else drops on a type change (returned for the confirm).
const TYPE_CONSTRAINTS = {
  string: ['format', 'pattern', 'minLength', 'maxLength'],
  number: ['minimum', 'maximum', 'exclusiveMinimum', 'exclusiveMaximum', 'multipleOf'],
  integer: ['minimum', 'maximum', 'exclusiveMinimum', 'exclusiveMaximum', 'multipleOf'],
  object: ['properties', 'required', 'additionalProperties'],
  array: ['items', 'minItems', 'maxItems', 'uniqueItems'],
  boolean: [],
  null: [],
};
// Kept across ANY type change.
const TYPE_NEUTRAL = new Set(['title', 'description', 'type', 'enum', 'const', 'default']);

/** Constraint keywords that would drop if this node changed to `newType` (for the confirm — no mutation). */
export function constraintsDroppedOnType(schema, newType) {
  if (!isPlainObject(schema)) return [];
  const kept = new Set([...(TYPE_CONSTRAINTS[newType] || []), ...TYPE_NEUTRAL]);
  return Object.keys(schema).filter((k) => RENDERED_KEYWORDS.has(k) && !kept.has(k) && !COMBINER_KINDS.includes(k));
}

/** Change a node's type, dropping now-inapplicable constraints. Type→combiner seeds variant 1 with the
 *  node's current schema (lossless); combiner→type takes variant 1. Returns the dropped constraint list. */
export function setType(schema, newType) {
  const dropped = COMBINER_KINDS.includes(newType) ? [] : constraintsDroppedOnType(schema, newType);
  if (COMBINER_KINDS.includes(newType)) {
    const seed = { ...schema };
    for (const k of ['title', 'description', ...COMBINER_KINDS]) delete seed[k]; // the combiner node keeps title/desc
    const variants = COMBINER_KINDS.map((k) => schema[k]).find(Array.isArray) || [Object.keys(seed).length ? seed : { type: 'string' }];
    for (const k of COMBINER_KINDS) delete schema[k];
    for (const k of Object.keys(schema)) { if (k !== 'title' && k !== 'description') delete schema[k]; }
    schema[newType] = variants;
    return dropped;
  }
  // combiner → type: take variant 1 as the node, keeping title/description.
  const activeCombiner = COMBINER_KINDS.find((k) => Array.isArray(schema[k]));
  if (activeCombiner) {
    const v1 = schema[activeCombiner][0];
    const title = schema.title;
    const description = schema.description;
    for (const k of Object.keys(schema)) delete schema[k];
    if (isPlainObject(v1)) Object.assign(schema, v1);
    schema.type = newType;
    if (title !== undefined && schema.title === undefined) schema.title = title;
    if (description !== undefined && schema.description === undefined) schema.description = description;
    return dropped;
  }
  for (const k of dropped) delete schema[k];
  schema.type = newType;
  return dropped;
}

/** Switch a combiner node between oneOf/anyOf/allOf, keeping its variants. */
export function setCombinerKind(schema, kind) {
  const from = COMBINER_KINDS.find((k) => Array.isArray(schema[k]));
  if (!from || from === kind) return schema;
  schema[kind] = schema[from];
  delete schema[from];
  return schema;
}

export function addVariant(schema, variant = { type: 'string' }) {
  const kind = COMBINER_KINDS.find((k) => Array.isArray(schema[k]));
  if (kind) schema[kind].push(variant);
  return schema;
}

export function removeVariant(schema, index) {
  const kind = COMBINER_KINDS.find((k) => Array.isArray(schema[k]));
  if (kind && index >= 0 && index < schema[kind].length) schema[kind].splice(index, 1);
  return schema;
}

export function reorderVariant(schema, from, to) {
  const kind = COMBINER_KINDS.find((k) => Array.isArray(schema[k]));
  if (!kind) return schema;
  const list = schema[kind];
  if (from < 0 || from >= list.length) return schema;
  const [item] = list.splice(from, 1);
  list.splice(Math.max(0, Math.min(to, list.length)), 0, item);
  return schema;
}

/** Set a constraint keyword, DELETING the key when the value is blank rather than writing null/undefined. */
export function setConstraint(schema, key, value) {
  if (value === undefined || value === null || value === '') delete schema[key];
  else schema[key] = value;
  return schema;
}

// ── emission check ──────────────────────────────────────────────────────────────────────────────

/** True iff every RENDERABLE node in the schema stays inside value-editor's consumable set once combiners are
 *  normalized (schema-descriptor.js) — i.e. the visual tier never produces a raw-JSON fallback. Advanced nodes
 *  are excluded by definition (they route to the JSON tier). Used by tests, not at runtime. */
export function emissionCheck(schema) {
  if (schema === true || schema === false) return true;
  if (!isPlainObject(schema)) return true;
  const d = normalizeDescriptor(schema);
  if (isPlainObject(d) && d.type === 'union' && Array.isArray(d.variants)) {
    return d.variants.every((v) => emissionCheck(v));
  }
  const cls = classifyNode(schema);
  if (cls.kind === 'advanced') return true; // routed to JSON tier — not a renderable-set violation
  if (cls.kind === 'combiner') {
    const kind = cls.combiner;
    if (kind === 'allOf') return isPlainObject(d) && d.type === 'object'; // simple merge, else raw fallback = violation
    return Array.isArray(schema[kind]) && schema[kind].every((v) => emissionCheck(v));
  }
  // renderable: a single known type, or typeless-with-structure (object/array inferred). Recurse into children.
  const type = schema.type ?? (isPlainObject(schema.properties) ? 'object' : (schema.items ? 'array' : undefined));
  if (type === 'object') return Object.values(schema.properties || {}).every((v) => emissionCheck(v))
    && (schema.additionalProperties === undefined || typeof schema.additionalProperties === 'boolean' || emissionCheck(schema.additionalProperties));
  if (type === 'array') return schema.items === undefined || emissionCheck(schema.items);
  if (type === undefined) return false; // a typeless node renders as the raw-JSON fallback
  return RENDERABLE_TYPES.includes(type);
}

function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}
