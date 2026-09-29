// Collecting-mode evaluation: evaluates an instance exhaustively against the compiled node graph and reports every
// keyword to a JsonSchemaResultsCollector, reproducing the C# evaluator's Eval<CollectingMode> (keyword order,
// evaluation/schema/instance paths, messages, which subschema results are committed or discarded).
//
// Flag-mode validation never comes here: it runs the generated code. Results collection is exhaustive by nature
// (every keyword is evaluated and reported), so it interprets the graph rather than generating a second program.

import { Dialect } from './dialect.js';
import { formatValidators, legacyHostname, numericFormatValidators, isNumericFormat } from './formats.js';
import { ContentKind, SchemaNode, TypeMask } from './node.js';
import { SchemaEvaluationDepthError } from './options.js';
import { encodePointerSegment, JsonSchemaResultsCollector, Message } from './results.js';
import { codePoints, content, equal, includes, multipleOf } from './runtime.js';

/** What collecting-mode evaluation needs from a compiled schema (also the shape of a serialised program image). */
export interface CollectingProgram {
  readonly nodes: SchemaNode[];
  readonly root: number;
  readonly usesDynamicScope: boolean;
  readonly maxDepth: number;
  /** Custom format assertions by name (in-memory programs only). */
  readonly formats?: Record<string, (s: string) => boolean>;
}

// Messages (Corvus.Text.Json Strings.resx).
const EVALUATED_SUBSCHEMA = 'The value was expected to match the subschema.';
const MATCHED_ALL = 'The value matched all subschema.';
const DID_NOT_MATCH_ALL = 'The value did not match all subschema.';
const MATCHED_AT_LEAST_ONE = 'The value matched at least one subschema.';
const DID_NOT_MATCH_AT_LEAST_ONE = 'The value did not match at least one subschema.';
const MATCHED_NO_SCHEMA = 'The instance matched no schema.';
const MATCHED_EXACTLY_ONE = 'The value matched exactly one subschema.';
const MATCHED_MORE_THAN_ONE = 'The instance matched more than one schema.';
const MATCHED_NOT = 'The value matched the subschema in a not composition, which means the evaluation was not a match.';
const DID_NOT_MATCH_NOT = 'The value did not match the subschema in a not composition, which means the evaluation was a match.';
const MATCHED_IF_FOR_THEN =
  'The value matched the subschema in a binary or ternay if, which means the evaluation will go on to match the then subschema.';
const MATCHED_IF_FOR_ELSE =
  'The value did not match the subschema in a ternary if, which means the evaluation will go on to match the else subschema.';
const MATCHED_THEN = 'The value matched the then subschema corresponding to a binary or ternary if.';
const DID_NOT_MATCH_THEN = 'The value did not match the then subschema corresponding to a binary or ternary if.';
const MATCHED_ELSE = 'The value matched the else subschema corresponding to a ternary if.';
const DID_NOT_MATCH_ELSE = 'The value did not match the else subschema corresponding to a ternary if.';
const UNIQUE_ITEMS = 'The array was expected to contain unique items.';
const PROPERTY_NAME_FAILED = 'The property name did not match the schema.';

const STRING_FORMAT_MESSAGES: Record<string, string> = {
  date: 'Expected an ISO8601 Date string.',
  'date-time': 'Expected an ISO8601 Offset DateTime string.',
  time: 'Expected an ISO8601 Offset Time string.',
  duration: 'Expected an ISO8601 Duration string.',
  email: 'Expected an RFC5321 Section-4.1.2 Email string.',
  'idn-email': 'Expected an RFC6531 IDN Email string.',
  hostname: 'Expected an RFC1035 hostname.',
  'idn-hostname': 'Expected an RFC5890 Section-2.3.2.3 IDN hostname.',
  ipv4: 'Expected an RFC2673 IP V4 address.',
  ipv6: 'Expected an RFC2373 IP V6 address.',
  uri: 'Expected an absolute URI.',
  'uri-reference': 'Expected a URI reference.',
  iri: 'Expected an absolute IRI.',
  'iri-reference': 'Expected an IRI reference.',
  uuid: 'Expected an RFC4122 UUID.',
  'uri-template': 'Expected an RFC6570 URI Template.',
  'json-pointer': 'Expected an RFC6901 JSON Pointer.',
  'relative-json-pointer': 'Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer).',
  regex: 'Expected a regular expression specification.',
};

/** `" 'v'"`, or nothing for an empty value (JsonSchemaEvaluation.AppendSingleQuotedValue). */
function q(v: string): string {
  return v.length === 0 ? '' : ` '${v}'`;
}

function numberText(n: number): string {
  return Number.isFinite(n) ? String(n) : n > 0 ? 'Infinity' : '-Infinity';
}

const TYPE_ORDER: Array<[TypeMask, string]> = [
  [TypeMask.Array, 'array'],
  [TypeMask.Object, 'object'],
  [TypeMask.Null, 'null'],
  [TypeMask.Boolean, 'boolean'],
  [TypeMask.Number, 'number'],
  [TypeMask.Integer, 'integer'],
  [TypeMask.String, 'string'],
];

function typeMessage(mask: TypeMask): string | undefined {
  const names = TYPE_ORDER.filter(([m]) => (mask & m) !== 0).map(([, name]) => name);
  if (names.length === 0) return undefined;
  if (names.length === 1) return `The value was expected to be of type '${names[0]}'`;
  return `The value was expected to be of type '[${names.map((n) => `"${n}"`).join(', ')}]'`;
}

function matchesType(mask: TypeMask, x: unknown): boolean {
  switch (typeof x) {
    case 'string':
      return (mask & TypeMask.String) !== 0;
    case 'number':
      return (mask & TypeMask.Number) !== 0 || ((mask & TypeMask.Integer) !== 0 && Number.isInteger(x));
    case 'boolean':
      return (mask & TypeMask.Boolean) !== 0;
    case 'object':
      if (x === null) return (mask & TypeMask.Null) !== 0;
      return Array.isArray(x) ? (mask & TypeMask.Array) !== 0 : (mask & TypeMask.Object) !== 0;
    default:
      return false;
  }
}

function constMessage(value: unknown): string | undefined {
  switch (typeof value) {
    case 'string':
      return 'Expected the value to be the string' + q(value);
    case 'number':
      return 'The value was expected to be equal to' + q(numberText(value));
    case 'boolean':
      return `Expected the value to be '${value}'`;
    default:
      return value === null ? "Expected the value to be 'null'" : undefined;
  }
}

type Bits = Set<string> | Uint8Array;

const regexCache = new Map<string, RegExp>();
function regex(pattern: string): RegExp {
  let re = regexCache.get(pattern);
  if (re === undefined) {
    try {
      re = new RegExp(pattern, 'u');
    } catch {
      re = new RegExp(pattern);
    }
    regexCache.set(pattern, re);
  }
  return re;
}

function isObject(x: unknown): x is Record<string, unknown> {
  return typeof x === 'object' && x !== null && !Array.isArray(x);
}

/** Evaluates `instance` against the program's entry, reporting to `collector`; returns whether it is valid. */
export function evaluateWithCollector(program: CollectingProgram, instance: unknown, collector: JsonSchemaResultsCollector): boolean {
  return new CollectingEvaluator(program, collector).evaluate(instance);
}

class CollectingEvaluator {
  private readonly nodes: SchemaNode[];
  private readonly scope: number[] = [];
  private depth = 0;

  constructor(
    private readonly program: CollectingProgram,
    private readonly c: JsonSchemaResultsCollector,
  ) {
    this.nodes = program.nodes;
  }

  evaluate(instance: unknown): boolean {
    // A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
    const root = this.resolve(this.program.root).target;
    this.c.beginChildContext(undefined, this.nodes[root].pointer, undefined);
    const ok = this.evalNode(root, instance, undefined);
    this.c.commitChildContext(false, ok, EVALUATED_SUBSCHEMA);
    return ok;
  }

  /** The single reference of a pure-$ref node (SchemaCompiler.IsPureRef), or -1. */
  private pureRefTarget(n: SchemaNode): number {
    const refs = (n.ref >= 0 ? 1 : 0) + (n.staticDynamicRef >= 0 ? 1 : 0);
    if (refs !== 1 || n.alwaysTrue || n.alwaysFalse || n.annotations !== undefined) return -1;
    if (
      n.hasType ||
      n.hasConst ||
      n.enumValues !== undefined ||
      n.hasNumberKeywords ||
      n.hasStringKeywords ||
      n.hasObjectKeywords ||
      n.hasArrayKeywords ||
      n.dynamicRef !== undefined ||
      n.allOf !== undefined ||
      n.anyOf !== undefined ||
      n.oneOf !== undefined ||
      n.not >= 0 ||
      n.if >= 0
    ) {
      return -1;
    }
    return n.ref >= 0 ? n.ref : n.staticDynamicRef;
  }

  /** Follows pure-$ref hops (at most 16; not across resources when a dynamic scope is kept). */
  private resolve(id: number): { target: number; hops: number } {
    let current = id;
    let hops = 0;
    while (hops < 16) {
      const n = this.nodes[current];
      const next = this.pureRefTarget(n);
      if (next < 0) break;
      if (this.program.usesDynamicScope && this.nodes[next].resourceId !== n.resourceId) break;
      current = next;
      hops++;
    }
    return { target: current, hops };
  }

  private segment(path: string, hops: number): string {
    return hops === 0 ? path : path + '/$ref'.repeat(hops);
  }

  private evalNode(id: number, x: unknown, bits: Bits | undefined): boolean {
    const n = this.nodes[id];
    if (n.alwaysTrue || n.alwaysFalse) {
      this.c.evaluatedBooleanSchema(n.alwaysTrue, undefined);
      return n.alwaysTrue;
    }
    let pushed = false;
    if (this.program.usesDynamicScope && (this.scope.length === 0 || this.scope[this.scope.length - 1] !== n.resourceId)) {
      this.scope.push(n.resourceId);
      pushed = true;
    }
    if (bits === undefined) {
      if (n.unevaluatedProperties >= 0 && isObject(x)) bits = new Set();
      else if (n.unevaluatedItems >= 0 && Array.isArray(x)) bits = new Uint8Array(x.length);
    }
    try {
      return this.evalCore(n, x, bits);
    } finally {
      if (pushed) this.scope.pop();
    }
  }

  private evalCore(n: SchemaNode, x: unknown, bits: Bits | undefined): boolean {
    const c = this.c;
    let ok = true;
    if (n.hasType) {
      const m = matchesType(n.type, x);
      c.evaluatedKeyword(m, typeMessage(n.type), 'type');
      ok &&= m;
    }
    if (n.hasConst) {
      const m = equal(x, n.constValue);
      c.evaluatedKeyword(m, constMessage(n.constValue), 'const');
      ok &&= m;
    }
    if (n.enumValues !== undefined) {
      const m = includes(n.enumValues, x);
      c.evaluatedKeyword(m, m ? MATCHED_AT_LEAST_ONE : DID_NOT_MATCH_AT_LEAST_ONE, 'enum');
      ok &&= m;
    }
    if (typeof x === 'number') {
      if (n.hasNumberKeywords) ok = this.evalNumber(n, x) && ok;
    } else if (typeof x === 'string') {
      if (n.hasStringKeywords) ok = this.evalString(n, x) && ok;
    } else if (isObject(x)) {
      if (n.hasObjectKeywords) ok = this.evalObject(n, x, bits as Set<string> | undefined) && ok;
    } else if (Array.isArray(x)) {
      if (n.hasArrayKeywords) ok = this.evalArray(n, x, bits as Uint8Array | undefined) && ok;
    }
    ok = this.evalInPlace(n, x, bits) && ok;
    if (isObject(x) && n.unevaluatedProperties >= 0) ok = this.evalUnevaluatedProperties(n, x, bits as Set<string>) && ok;
    else if (Array.isArray(x) && n.unevaluatedItems >= 0) ok = this.evalUnevaluatedItems(n, x, bits as Uint8Array) && ok;
    if (n.annotations !== undefined) {
      for (const a of n.annotations) {
        if (a.stringsOnly && typeof x !== 'string') continue;
        c.ignoredKeyword(() => JSON.stringify(a.value), a.keyword);
      }
    }
    return ok;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Numbers and strings

  private evalNumber(n: SchemaNode, x: number): boolean {
    const c = this.c;
    let ok = true;
    if (n.assertFormat && n.format !== undefined && isNumericFormat(n.formatKind)) {
      const custom = this.program.formats?.[n.format];
      const m = custom !== undefined ? custom(String(x)) : numericFormatValidators[n.formatKind](x);
      c.evaluatedKeyword(m, `The value was expected to be in a supported format, and within bounds for '${n.formatKind}'`, 'format');
      ok &&= m;
    }
    const bound = (value: number | undefined, test: (v: number) => boolean, text: string, keyword: string): void => {
      if (value === undefined) return;
      const m = test(value);
      c.evaluatedKeyword(m, () => text + q(numberText(value)), keyword);
      ok &&= m;
    };
    bound(n.minimum, (v) => x >= v, 'The value was expected to be greater than or equal to', 'minimum');
    bound(n.maximum, (v) => x <= v, 'The value was expected to be less than or equal to', 'maximum');
    bound(n.exclusiveMinimum, (v) => x > v, 'The value was expected to be greater than', 'exclusiveMinimum');
    bound(n.exclusiveMaximum, (v) => x < v, 'The value was expected to be less than', 'exclusiveMaximum');
    bound(
      n.multipleOf,
      (d) => (Number.isInteger(d) && Math.abs(d) < 2 ** 53 ? x % d === 0 : multipleOf(x, d)),
      'The value was expected to be a multiple of',
      'multipleOf',
    );
    return ok;
  }

  private evalString(n: SchemaNode, x: string): boolean {
    const c = this.c;
    let ok = true;
    if (n.minLength >= 0) {
      const m = codePoints(x) >= n.minLength;
      c.evaluatedKeyword(m, `Expected the length of the value to be greater than or equal to '${n.minLength}'`, 'minLength');
      ok &&= m;
    }
    if (n.maxLength >= 0) {
      const m = codePoints(x) <= n.maxLength;
      c.evaluatedKeyword(m, `Expected the length of the value to be less than or equal to '${n.maxLength}'`, 'maxLength');
      ok &&= m;
    }
    if (n.pattern !== undefined) {
      const m = regex(n.pattern).test(x);
      c.evaluatedKeyword(m, () => 'Expected the value to match the regular expression' + q(n.pattern!), 'pattern');
      ok &&= m;
    }
    if (n.assertFormat && n.format !== undefined && !isNumericFormat(n.formatKind)) {
      const custom = this.program.formats?.[n.format];
      let m: boolean;
      let message: string | undefined;
      if (custom !== undefined) {
        m = custom(x);
        message = `Expected a string in the '${n.format}' format.`;
      } else if (n.formatKind === 'unknown') {
        m = true;
      } else {
        const validate = n.formatKind === 'hostname' && n.dialect <= Dialect.Draft6 ? legacyHostname : formatValidators[n.formatKind];
        m = validate(x);
        message = STRING_FORMAT_MESSAGES[n.formatKind];
      }
      c.evaluatedKeyword(m, message, 'format');
      ok &&= m;
    }
    if (n.assertContent) {
      const kind = n.content === ContentKind.Base64 ? 1 : n.content === ContentKind.Json ? 2 : 3;
      const m = content(x, kind);
      const message = kind === 1 ? 'Expected a valid Base64-encoded string.' : kind === 2 ? 'Expected valid JSON content.' : 'Expected valid Base64-encoded JSON content.';
      c.evaluatedKeyword(m, message, kind === 1 ? 'contentEncoding' : 'contentMediaType');
      ok &&= m;
    }
    return ok;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Objects

  /** A child application at a new instance location (a property value or an array item). */
  private evalAt(childId: number, path: string, value: unknown, docSegment: string): boolean {
    const { target, hops } = this.resolve(childId);
    this.c.beginChildContext(this.segment(path, hops), this.nodes[target].pointer, docSegment);
    const ok = this.evalNode(target, value, undefined);
    this.c.commitChildContext(ok, ok, EVALUATED_SUBSCHEMA);
    return ok;
  }

  private evalObject(n: SchemaNode, x: Record<string, unknown>, bits: Set<string> | undefined): boolean {
    const c = this.c;
    let ok = true;
    const keys = Object.keys(x);
    if (n.minProperties >= 0) {
      const m = keys.length >= n.minProperties;
      c.evaluatedKeyword(m, `Expected the property count to be greater than or equal to '${n.minProperties}'`, 'minProperties');
      ok &&= m;
    }
    if (n.maxProperties >= 0) {
      const m = keys.length <= n.maxProperties;
      c.evaluatedKeyword(m, `Expected the property count to be less than or equal to '${n.maxProperties}'`, 'maxProperties');
      ok &&= m;
    }
    if (n.properties !== undefined || n.patternProperties !== undefined || n.additionalProperties >= 0 || n.propertyNames >= 0) {
      for (const k of keys) {
        const v = x[k];
        const doc = encodePointerSegment(k);
        let matched = false;
        const p = n.properties?.get(k);
        if (p !== undefined) {
          matched = true;
          bits?.add(k);
          ok = this.evalAt(p, 'properties/' + encodePointerSegment(k), v, doc) && ok;
        }
        if (n.patternProperties !== undefined) {
          for (const pp of n.patternProperties) {
            if (!regex(pp.pattern).test(k)) continue;
            matched = true;
            bits?.add(k);
            ok = this.evalAt(pp.node, 'patternProperties/' + encodePointerSegment(pp.pattern), v, doc) && ok;
          }
        }
        if (n.additionalProperties >= 0 && !matched) {
          bits?.add(k);
          ok = this.evalAt(n.additionalProperties, 'additionalProperties', v, doc) && ok;
        }
        if (n.propertyNames >= 0) {
          // Not elided; the document path stays the object's.
          c.beginChildContext('propertyNames', this.nodes[n.propertyNames].pointer, undefined);
          const m = this.evalNode(n.propertyNames, k, undefined);
          c.commitChildContext(m, m, EVALUATED_SUBSCHEMA);
          if (!m) {
            c.evaluatedKeyword(false, PROPERTY_NAME_FAILED, 'propertyNames');
            ok = false;
          }
        }
      }
    }
    const has = (name: string): boolean => Object.prototype.hasOwnProperty.call(x, name);
    if (n.requiredList !== undefined) {
      for (const r of n.requiredList) {
        const present = has(r);
        c.evaluatedKeywordForProperty(present, () => `Required property ${present ? '' : 'not '}present '${r}'`, r, 'required');
        ok &&= present;
      }
    }
    if (n.dependencies !== undefined) {
      const modern = n.dialect >= Dialect.Draft201909;
      for (const d of n.dependencies) {
        if (!has(d.name)) continue;
        for (const r of d.required ?? []) {
          const present = has(r);
          c.evaluatedKeywordForProperty(present, () => `Required property ${present ? '' : 'not '}present '${r}'`, r, modern ? 'dependentRequired' : 'dependencies');
          ok &&= present;
        }
        if (d.schema !== undefined) {
          const m = this.evalInPlaceChild(d.schema, d.keyword + '/' + encodePointerSegment(d.name), x, bits, true).ok;
          c.evaluatedKeywordForProperty(
            m,
            () => `The value did match the schema applied because it contained the property '${d.name}'`,
            d.name,
            modern ? 'dependentSchemas' : 'dependencies',
          );
          ok &&= m;
        }
      }
    }
    return ok;
  }

  private evalUnevaluatedProperties(n: SchemaNode, x: Record<string, unknown>, bits: Set<string>): boolean {
    let ok = true;
    for (const k of Object.keys(x)) {
      if (bits.has(k)) continue;
      bits.add(k);
      ok = this.evalAt(n.unevaluatedProperties, 'unevaluatedProperties', x[k], encodePointerSegment(k)) && ok;
    }
    this.c.evaluatedKeyword(ok, undefined, 'unevaluatedProperties');
    return ok;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Arrays

  private evalArray(n: SchemaNode, x: unknown[], bits: Uint8Array | undefined): boolean {
    const c = this.c;
    let ok = true;
    const len = x.length;
    if (n.minItems >= 0) {
      const m = len >= n.minItems;
      c.evaluatedKeyword(m, `Expected the item count to be greater than or equal to '${n.minItems}'`, 'minItems');
      ok &&= m;
    }
    if (n.maxItems >= 0) {
      const m = len <= n.maxItems;
      c.evaluatedKeyword(m, `Expected the item count to be less than or equal to '${n.maxItems}'`, 'maxItems');
      ok &&= m;
    }
    if (n.prefixItems === undefined && n.items < 0 && n.contains < 0 && !n.uniqueItems) return ok;
    let count = 0;
    let unique = true;
    for (let i = 0; i < len; i++) {
      if (n.prefixItems !== undefined && i < n.prefixItems.length) {
        if (bits !== undefined) bits[i] = 1;
        ok = this.evalAt(n.prefixItems[i], n.prefixKeyword + '/' + i, x[i], String(i)) && ok;
      } else if (n.items >= 0) {
        if (bits !== undefined) bits[i] = 1;
        ok = this.evalAt(n.items, n.itemsKeyword, x[i], String(i)) && ok;
      }
      if (n.contains >= 0) {
        const { target, hops } = this.resolve(n.contains);
        c.beginChildContext(this.segment('contains', hops), this.nodes[target].pointer, String(i));
        if (this.evalNode(target, x[i], undefined)) {
          c.commitChildContext(true, true, EVALUATED_SUBSCHEMA);
          count++;
          if (n.containsMarksEvaluated && bits !== undefined) bits[i] = 1;
        } else {
          c.popChildContext();
        }
      }
      if (n.uniqueItems && unique) {
        for (let j = 0; j < i; j++) {
          if (equal(x[i], x[j])) {
            unique = false;
            break;
          }
        }
      }
    }
    if (n.uniqueItems) {
      c.evaluatedKeyword(unique, UNIQUE_ITEMS, 'uniqueItems');
      ok &&= unique;
    }
    if (n.contains >= 0) {
      const max = n.maxContains;
      const m = count >= n.minContains && (max < 0 || count <= max);
      const message =
        max >= 0 && count > max
          ? `Expected the contains count to be less than or equal to '${max}'`
          : `Expected the contains count to be greater than or equal to '${n.minContains}'`;
      c.evaluatedKeyword(m, message, 'contains');
      ok &&= m;
    }
    return ok;
  }

  private evalUnevaluatedItems(n: SchemaNode, x: unknown[], bits: Uint8Array): boolean {
    let ok = true;
    for (let i = 0; i < x.length; i++) {
      if (bits[i]) continue;
      bits[i] = 1;
      ok = this.evalAt(n.unevaluatedItems, 'unevaluatedItems', x[i], String(i)) && ok;
    }
    this.c.evaluatedKeyword(ok, undefined, 'unevaluatedItems');
    return ok;
  }

  // -------------------------------------------------------------------------------------------------------------
  // In-place applicators

  private canMark(id: number, x: unknown): boolean {
    const n = this.nodes[id];
    return isObject(x) ? n.marksProperties : n.marksItems;
  }

  private scratchFor(x: unknown): Bits {
    return isObject(x) ? new Set<string>() : new Uint8Array((x as unknown[]).length);
  }

  private merge(bits: Bits, scratch: Bits): void {
    if (bits instanceof Set) for (const k of scratch as Set<string>) bits.add(k);
    else {
      const s = scratch as Uint8Array;
      for (let i = 0; i < s.length; i++) if (s[i]) bits[i] = 1;
    }
  }

  /**
   * Evaluates an in-place child (EvalInPlaceCore): a new context at the same instance location, on a fresh scratch
   * set of evaluated properties/items merged into the parent's on success. A failing child is committed or popped.
   */
  private evalInPlaceChild(
    childId: number,
    path: string,
    x: unknown,
    bits: Bits | undefined,
    commitOnFailure: boolean,
    elide = true,
  ): { ok: boolean; scratch: Bits | undefined } {
    const { target, hops } = elide ? this.resolve(childId) : { target: childId, hops: 0 };
    const scratch = bits !== undefined && this.canMark(childId, x) ? this.scratchFor(x) : undefined;
    const guarded = this.nodes[target].inPlaceCycle;
    if (guarded && ++this.depth > this.program.maxDepth) {
      this.depth = 0;
      throw new SchemaEvaluationDepthError();
    }
    try {
      this.c.beginChildContext(this.segment(path, hops), this.nodes[target].pointer, undefined);
      const ok = this.evalNode(target, x, scratch);
      if (ok || commitOnFailure) this.c.commitChildContext(ok, ok, EVALUATED_SUBSCHEMA);
      else this.c.popChildContext();
      if (ok && scratch !== undefined && bits !== undefined) this.merge(bits, scratch);
      return { ok, scratch };
    } finally {
      if (guarded) this.depth--;
    }
  }

  private resolveDynamic(n: SchemaNode): number {
    const d = n.dynamicRef!;
    for (const resource of this.scope) {
      const target = d.byResource.get(resource);
      if (target !== undefined) return target;
    }
    return d.fallback;
  }

  private evalInPlace(n: SchemaNode, x: unknown, bits: Bits | undefined): boolean {
    const c = this.c;
    let ok = true;
    if (n.ref >= 0) {
      const m = this.evalInPlaceChild(n.ref, '$ref', x, bits, true).ok;
      c.evaluatedKeyword(m, m ? MATCHED_ALL : DID_NOT_MATCH_ALL, '$ref');
      ok &&= m;
    }
    if (n.staticDynamicRef >= 0) {
      const keyword = n.staticDynamicKeyword!;
      const m = this.evalInPlaceChild(n.staticDynamicRef, keyword, x, bits, true).ok;
      c.evaluatedKeyword(m, m ? MATCHED_ALL : DID_NOT_MATCH_ALL, keyword);
      ok &&= m;
    }
    if (n.dynamicRef !== undefined) {
      const keyword = n.dynamicRef.isRecursive ? '$recursiveRef' : '$dynamicRef';
      // The resolved target is elided, with no $ref hops in the path.
      const target = this.resolve(this.resolveDynamic(n)).target;
      const m = this.evalInPlaceChild(target, keyword, x, bits, true, false).ok;
      c.evaluatedKeyword(m, m ? MATCHED_ALL : DID_NOT_MATCH_ALL, keyword);
      ok &&= m;
    }
    if (n.allOf !== undefined) {
      let all = true;
      n.allOf.forEach((b, i) => {
        if (!this.evalInPlaceChild(b, 'allOf/' + i, x, bits, true).ok) all = false;
      });
      c.evaluatedKeyword(all, all ? MATCHED_ALL : DID_NOT_MATCH_ALL, 'allOf');
      ok &&= all;
    }
    if (n.anyOf !== undefined) {
      let any = false;
      n.anyOf.forEach((b, i) => {
        if (this.evalInPlaceChild(b, 'anyOf/' + i, x, bits, false).ok) any = true;
      });
      c.evaluatedKeyword(any, any ? MATCHED_AT_LEAST_ONE : DID_NOT_MATCH_AT_LEAST_ONE, 'anyOf');
      ok &&= any;
    }
    if (n.oneOf !== undefined) {
      let matched = 0;
      let only: Bits | undefined;
      n.oneOf.forEach((b, i) => {
        // Evaluated properties/items are merged only when exactly one branch matched, so collect them aside.
        const r = this.evalInPlaceChild(b, 'oneOf/' + i, x, bits === undefined ? undefined : this.scratchFor(x), false);
        if (r.ok) {
          matched++;
          only = r.scratch;
        }
      });
      if (matched === 1 && bits !== undefined && only !== undefined) this.merge(bits, only);
      c.evaluatedKeyword(matched === 1, matched === 0 ? MATCHED_NO_SCHEMA : matched === 1 ? MATCHED_EXACTLY_ONE : MATCHED_MORE_THAN_ONE, 'oneOf');
      ok &&= matched === 1;
    }
    if (n.not >= 0) {
      // Not elided, never contributes results or evaluated properties/items.
      c.beginChildContext('not', this.nodes[n.not].pointer, undefined);
      const inner = this.evalNode(n.not, x, undefined);
      c.popChildContext();
      c.evaluatedKeyword(!inner, inner ? MATCHED_NOT : DID_NOT_MATCH_NOT, 'not');
      ok &&= !inner;
    }
    if (n.if >= 0) {
      const cond = this.evalInPlaceChild(n.if, 'if', x, bits, false).ok;
      c.evaluatedKeyword(true, cond ? MATCHED_IF_FOR_THEN : MATCHED_IF_FOR_ELSE, 'if');
      if (cond && n.then >= 0) {
        const m = this.evalInPlaceChild(n.then, 'then', x, bits, true).ok;
        c.evaluatedKeyword(m, m ? MATCHED_THEN : DID_NOT_MATCH_THEN, 'then');
        ok &&= m;
      } else if (!cond && n.else >= 0) {
        const m = this.evalInPlaceChild(n.else, 'else', x, bits, true).ok;
        c.evaluatedKeyword(m, m ? MATCHED_ELSE : DID_NOT_MATCH_ELSE, 'else');
        ok &&= m;
      }
    }
    return ok;
  }
}

// -----------------------------------------------------------------------------------------------------------------
// Program images: the node graph as JSON, so that standalone modules can collect results without the compiler.

const MAP_FIELDS = ['properties'] as const;

/** Serialises a program for collecting-mode evaluation (custom formats are not included). */
export function serializeProgram(program: CollectingProgram): string {
  const nodes = program.nodes.map((n) => {
    const o: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(n)) {
      if (v === undefined || k === 'oneOfDiscriminator' || k === 'anyOfDiscriminator' || k === 'location') continue;
      if (v instanceof Map) o[k] = [...v];
      else if (k === 'dynamicRef') {
        const d = v as SchemaNode['dynamicRef'] & object;
        o[k] = { ...d, byResource: [...d.byResource] };
      } else o[k] = v;
    }
    return o;
  });
  return JSON.stringify({ root: program.root, usesDynamicScope: program.usesDynamicScope, maxDepth: program.maxDepth, nodes });
}

/** Loads a program image produced by {@link serializeProgram}. */
export function loadProgram(image: string): CollectingProgram {
  const data = JSON.parse(image) as { root: number; usesDynamicScope: boolean; maxDepth: number; nodes: Array<Record<string, unknown>> };
  const nodes = data.nodes.map((o) => {
    const n = new SchemaNode(o.id as number, o.resourceId as number, o.dialect as Dialect, '', o.pointer as string);
    for (const [k, v] of Object.entries(o)) {
      if (k === 'id' || k === 'resourceId' || k === 'dialect' || k === 'pointer') continue;
      if ((MAP_FIELDS as readonly string[]).includes(k)) (n as unknown as Record<string, unknown>)[k] = new Map(v as Array<[string, number]>);
      else if (k === 'dynamicRef') {
        const d = v as { byResource: Array<[number, number]> };
        (n as unknown as Record<string, unknown>)[k] = { ...d, byResource: new Map(d.byResource) };
      } else (n as unknown as Record<string, unknown>)[k] = v;
    }
    return n;
  });
  return { nodes, root: data.root, usesDynamicScope: data.usesDynamicScope, maxDepth: data.maxDepth };
}
