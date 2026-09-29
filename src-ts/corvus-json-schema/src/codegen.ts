// Generates specialised JavaScript from a compiled schema graph.
//
// The C# evaluator interprets its node graph through fused plans chosen at compile time, because emitting IL at
// run time is expensive. In JavaScript, generated source is cheap to produce and the engine's optimising compiler
// specialises it far better than an interpreter loop, so each node becomes one function whose body is exactly the
// checks that node needs (the "plan", unrolled into straight-line code). The analyses that pick C# plans pick the
// shape of the emitted code here: pure-$ref elision, type-only children tested inline, discriminator and type
// dispatch for oneOf/anyOf, dynamic scope only where a $dynamicRef stays dynamic, and evaluated-property/item
// tracking only where unevaluatedProperties/unevaluatedItems consume it.

import { CompiledSchema } from './compiler.js';
import { formatValidators, isNumericFormat } from './formats.js';
import { Dialect } from './dialect.js';
import { ContentKind, Discriminator, SchemaNode, TypeMask } from './node.js';

type Variant = 'v' | 't';
interface Coverage {
  names: Set<string>;
  patterns: string[];
  prefix: number;
  all: boolean;
}
type Kind = 'null' | 'boolean' | 'object' | 'array' | 'number' | 'string';
const KINDS: Kind[] = ['string', 'number', 'object', 'array', 'boolean', 'null'];

const REF_START = '\u0001';
const REF_END = '\u0002';
const REF_RE = /\u0001([^\u0002]+)\u0002/g;

const PROTO_NAMES = new Set([...Object.getOwnPropertyNames(Object.prototype), '__proto__']);
const IDENT_RE = /^[A-Za-z_$][A-Za-z0-9_$]*$/;

const env = (name: string, fallback: number): number => {
  const v = typeof process !== 'undefined' ? process.env?.[name] : undefined;
  return v === undefined ? fallback : Number(v);
};
/** Objects with at most this many declared properties (or only required ones) are checked by direct lookups. */
const MAX_UNROLLED_PROPERTIES = env('CORVUS_TS_UNROLL', 3);
/** Required-only objects are unrolled up to this many properties. */
const MAX_UNROLLED_REQUIRED = env('CORVUS_TS_UNROLL_REQUIRED', 24);
/** The number of names above which property dispatch goes through a Map to a dense switch. */
const MAX_SWITCH_NAMES = env('CORVUS_TS_SWITCH', 4);
const DISPATCH_MAP = env('CORVUS_TS_DISPATCH_MAP', 0) !== 0;
/** Generated programs smaller than this (characters) are compiled eagerly. */
const EAGER_MAX_SOURCE = env('CORVUS_TS_EAGER_MAX', 128 * 1024);

function lit(v: string | number | boolean | null): string {
  return JSON.stringify(v);
}

function kindAllowed(mask: TypeMask, kind: Kind): boolean {
  switch (kind) {
    case 'null':
      return (mask & TypeMask.Null) !== 0;
    case 'boolean':
      return (mask & TypeMask.Boolean) !== 0;
    case 'object':
      return (mask & TypeMask.Object) !== 0;
    case 'array':
      return (mask & TypeMask.Array) !== 0;
    case 'number':
      return (mask & (TypeMask.Number | TypeMask.Integer)) !== 0;
    case 'string':
      return (mask & TypeMask.String) !== 0;
  }
}

function kindTest(kind: Kind, v: string): string {
  switch (kind) {
    case 'null':
      return `${v} === null`;
    case 'boolean':
      return `typeof ${v} === "boolean"`;
    case 'object':
      return `(typeof ${v} === "object" && ${v} !== null && !Array.isArray(${v}))`;
    case 'array':
      return `Array.isArray(${v})`;
    case 'number':
      return `typeof ${v} === "number"`;
    case 'string':
      return `typeof ${v} === "string"`;
  }
}

/** A boolean expression that is true when `v` has one of the types in the mask. */
export function typeExpr(mask: TypeMask, v: string): string {
  const parts: string[] = [];
  if (mask & TypeMask.String) parts.push(`typeof ${v} === "string"`);
  if (mask & TypeMask.Number) parts.push(`typeof ${v} === "number"`);
  else if (mask & TypeMask.Integer) parts.push(`Number.isInteger(${v})`);
  const obj = (mask & TypeMask.Object) !== 0;
  const arr = (mask & TypeMask.Array) !== 0;
  const nul = (mask & TypeMask.Null) !== 0;
  if (obj && arr && nul) parts.push(`typeof ${v} === "object"`);
  else if (obj && arr) parts.push(`(typeof ${v} === "object" && ${v} !== null)`);
  else if (obj && nul) parts.push(`(typeof ${v} === "object" && !Array.isArray(${v}))`);
  else {
    if (obj) parts.push(kindTest('object', v));
    if (arr) parts.push(`Array.isArray(${v})`);
    if (nul) parts.push(`${v} === null`);
  }
  if (mask & TypeMask.Boolean) parts.push(`typeof ${v} === "boolean"`);
  if (parts.length === 0) return 'false';
  return parts.length === 1 ? parts[0] : `(${parts.join(' || ')})`;
}

function getProperty(obj: string, name: string): string {
  if (PROTO_NAMES.has(name)) return `(Object.hasOwn(${obj}, ${lit(name)}) ? ${obj}[${lit(name)}] : undefined)`;
  return IDENT_RE.test(name) ? `${obj}.${name}` : `${obj}[${lit(name)}]`;
}

export interface GeneratedCode {
  /** Constant and function declarations; `ROOT` names the entry function. */
  readonly declarations: string;
  readonly root: string;
  /** Custom format functions referenced as `F[i]` (only when custom formats are used). */
  readonly customFormats: Array<(s: string) => boolean>;
  readonly usesDynamicScope: boolean;
  readonly usesDepth: boolean;
  readonly rootResource: number;
  readonly maxDepth: number;
}

export class CodeGenerator {
  private readonly nodes: SchemaNode[];
  private readonly templates = new Map<string, string>();
  private readonly queue: string[] = [];
  private readonly constants = new Map<string, string>();
  private readonly customFormats: Array<(s: string) => boolean> = [];
  private readonly customFormatIndex = new Map<(s: string) => boolean, number>();
  private usesDepth = false;
  private tmp = 0;

  constructor(private readonly program: CompiledSchema) {
    this.nodes = program.nodes;
  }

  generate(): GeneratedCode {
    const rootName = this.request(this.entryName(this.program.root));
    for (let q = 0; q < this.queue.length; q++) {
      const name = this.queue[q];
      this.templates.set(name, this.generateFunction(name));
    }
    const { declarations, root } = this.link(rootName);
    return {
      declarations,
      root,
      customFormats: this.customFormats,
      usesDynamicScope: this.program.usesDynamicScope,
      usesDepth: this.usesDepth,
      rootResource: this.program.rootResource,
      maxDepth: this.program.options.maxDepth,
    };
  }

  // -------------------------------------------------------------------------------------------------------------
  // Names and references

  private entryName(id: number): string {
    return 'v' + this.elide(id);
  }

  private request(name: string): string {
    if (!this.templates.has(name)) {
      this.templates.set(name, '');
      this.queue.push(name);
    }
    return name;
  }

  private ref(name: string): string {
    return REF_START + this.request(name) + REF_END;
  }

  private constant(init: string): string {
    let name = this.constants.get(init);
    if (name === undefined) {
      name = 'c' + this.constants.size;
      this.constants.set(init, name);
    }
    return name;
  }

  /** Follows pure-$ref chains (Blaze's jump-target inlining); not across resources when a dynamic scope is kept. */
  private elide(id: number): number {
    let n = this.nodes[id];
    for (let hops = 0; hops < 64 && n.isPureRef && !n.inPlaceCycle; hops++) {
      const next = this.nodes[n.ref];
      if (this.program.usesDynamicScope && next.resourceId !== n.resourceId) break;
      n = next;
    }
    return n.id;
  }

  /**
   * An expression that evaluates child `id` against `v` from a node in resource `from`. `e` is the evaluated
   * set/array to mark (tracking variant) or undefined (flag variant).
   */
  private call(id: number, v: string, from: number, e?: string): string {
    const target = this.elide(id);
    const n = this.nodes[target];
    if (n.alwaysTrue) return 'true';
    if (n.alwaysFalse) return 'false';
    const crosses = this.program.usesDynamicScope && n.resourceId !== from;
    if (e === undefined && !crosses && n.isTypeOnly) return typeExpr(n.type, v);
    if (e === undefined && !crosses && this.isInlineLeaf(n)) return this.inlineLeaf(n, v);
    const variant: Variant = e === undefined ? 'v' : 't';
    const name = crosses ? 's' + variant + target : variant + target;
    return `${this.ref(name)}(${v}${e === undefined ? '' : ', ' + e})`;
  }

  /** A `{type, enum}`/`{const}`/`{enum}` leaf small enough to test at the call site. */
  private isInlineLeaf(n: SchemaNode): boolean {
    if (n.hasObjectKeywords || n.hasArrayKeywords || n.hasStringKeywords || n.hasNumberKeywords || n.hasInPlaceApplicators) return false;
    const primitive = (x: unknown): boolean => x === null || typeof x === 'string' || typeof x === 'boolean' || (typeof x === 'number' && Number.isFinite(x));
    if (n.hasConst && !primitive(n.constValue)) return false;
    if (n.enumValues !== undefined && (n.enumValues.length > 4 || !n.enumValues.every(primitive))) return false;
    return n.hasConst || n.enumValues !== undefined;
  }

  private inlineLeaf(n: SchemaNode, v: string): string {
    const parts: string[] = [];
    if (n.hasType) parts.push(typeExpr(n.type, v));
    if (n.hasConst) parts.push(`${v} === ${lit(n.constValue as string)}`);
    if (n.enumValues !== undefined) {
      parts.push(n.enumValues.length === 0 ? 'false' : '(' + n.enumValues.map((x) => `${v} === ${lit(x as string)}`).join(' || ') + ')');
    }
    return parts.length === 1 ? parts[0] : '(' + parts.join(' && ') + ')';
  }

  private scratch(prefix: string): string {
    return prefix + this.tmp++;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Functions

  private generateFunction(name: string): string {
    const kind = name[0];
    if (name[0] === 's') {
      const variant = name[1] as Variant;
      const id = Number(name.slice(2));
      const n = this.nodes[id];
      const params = variant === 'v' ? 'x' : 'x, ev';
      return `function @(${params}) {\n  DS[dsp++] = ${n.resourceId};\n  const r = ${this.ref(variant + id)}(${params});\n  dsp--;\n  return r;\n}`;
    }
    if (kind === 'd') {
      return this.generateDynamicResolver(Number(name.slice(2)), name[1] as Variant);
    }
    if (kind === 'b') {
      return this.generateNodeFunction(Number(name.slice(2)), name[1] as Variant);
    }
    const variant = kind as Variant;
    const id = Number(name.slice(1));
    const n = this.nodes[id];
    if (n.inPlaceCycle) {
      this.usesDepth = true;
      const params = variant === 'v' ? 'x' : 'x, ev';
      return `function @(${params}) {\n  if (++depth > MAXDEPTH) R.depthExceeded();\n  const r = ${this.ref('b' + variant + id)}(${params});\n  depth--;\n  return r;\n}`;
    }
    return this.generateNodeFunction(id, variant);
  }

  private generateDynamicResolver(id: number, variant: Variant): string {
    const n = this.nodes[id];
    const d = n.dynamicRef!;
    const e = variant === 't' ? 'ev' : undefined;
    const lines = [`function @(x${e ? ', ev' : ''}) {`, '  for (let i = 0; i < dsp; i++) {', '    switch (DS[i]) {'];
    for (const [resource, target] of d.byResource) {
      lines.push(`      case ${resource}: return ${this.call(target, 'x', n.resourceId, e)};`);
    }
    lines.push('    }', '  }', `  return ${this.call(d.fallback, 'x', n.resourceId, e)};`, '}');
    return lines.join('\n');
  }

  private generateNodeFunction(id: number, variant: Variant): string {
    const n = this.nodes[id];
    // Scratch names restart per function so that structurally identical functions produce identical text.
    this.tmp = 0;
    const tracking = variant === 't' || n.unevaluatedProperties >= 0 || n.unevaluatedItems >= 0;
    const body = tracking ? this.trackingBody(n, variant) : this.flagBody(n);
    const params = variant === 'v' ? 'x' : 'x, ev';
    return `function @(${params}) {\n${indent(body, 1).join('\n')}\n}`;
  }

  // Flag mode: type-directed blocks, then const/enum, then in-place applicators.
  private flagBody(n: SchemaNode): string[] {
    const out: string[] = [];
    if (n.alwaysFalse) return ['return false;'];
    if (n.alwaysTrue) return ['return true;'];
    out.push(...this.kindBlocks(n, (kind) => this.kindSection(n, kind, undefined, true)));
    out.push(...this.constEnum(n));
    out.push(...this.inPlace(n, undefined, undefined));
    out.push('return true;');
    return out;
  }

  /**
   * Emits the per-kind sections. Kinds with work get a branch; allowed kinds without work fall through; a type
   * restriction fails every other kind.
   */
  private kindBlocks(n: SchemaNode, section: (kind: Kind) => string[]): string[] {
    const mask = n.hasType ? n.type : TypeMask.All;
    const allowed = KINDS.filter((k) => kindAllowed(mask, k));
    const withWork = allowed.map((k) => [k, section(k)] as const).filter(([, lines]) => lines.length > 0);
    const out: string[] = [];
    if (allowed.length === 0) return ['return false;'];
    if (n.hasType && allowed.length === 1) {
      out.push(`if (!${wrap(kindTest(allowed[0], 'x'))}) return false;`);
      if (withWork.length > 0) out.push(...withWork[0][1]);
      return out;
    }
    const restOk = allowed.filter((k) => !withWork.some(([w]) => w === k));
    for (let i = 0; i < withWork.length; i++) {
      const [k, lines] = withWork[i];
      out.push(`${i === 0 ? 'if' : '} else if'} (${kindTest(k, 'x')}) {`);
      out.push(...indent(lines, 1));
    }
    const restricted = n.hasType && allowed.length < KINDS.length;
    if (withWork.length > 0) {
      if (restricted) {
        out.push(restOk.length === 0 ? '} else {' : `} else if (!(${restOk.map((k) => kindTest(k, 'x')).join(' || ')})) {`);
        out.push('  return false;');
      }
      out.push('}');
    } else if (restricted) {
      out.push(`if (!(${restOk.map((k) => kindTest(k, 'x')).join(' || ')})) return false;`);
    }
    return out;
  }

  private kindSection(n: SchemaNode, kind: Kind, e: string | undefined, flagLayout: boolean): string[] {
    const integerOnly = n.hasType && (n.type & TypeMask.Integer) !== 0 && (n.type & TypeMask.Number) === 0;
    switch (kind) {
      case 'object':
        return this.objectSection(n, e, flagLayout);
      case 'array':
        return this.arraySection(n, e);
      case 'string':
        return this.stringSection(n);
      case 'number': {
        const lines = integerOnly ? ['if (!Number.isInteger(x)) return false;'] : [];
        return [...lines, ...this.numberSection(n)];
      }
      default:
        return [];
    }
  }

  private constEnum(n: SchemaNode): string[] {
    const out: string[] = [];
    const primitive = (x: unknown): boolean => x === null || typeof x === 'string' || typeof x === 'boolean' || (typeof x === 'number' && Number.isFinite(x));
    if (n.hasConst) {
      if (primitive(n.constValue)) out.push(`if (x !== ${lit(n.constValue as string)}) return false;`);
      else out.push(`if (!R.equal(x, ${this.constant(`JSON.parse(${lit(JSON.stringify(n.constValue))})`)})) return false;`);
    }
    if (n.enumValues !== undefined) {
      const values = n.enumValues;
      if (values.length === 0) out.push('return false;');
      else if (values.every(primitive)) {
        if (values.length <= 6) out.push(`if (${values.map((v) => `x !== ${lit(v as string)}`).join(' && ')}) return false;`);
        else out.push(`if (!${this.constant(`new Set(JSON.parse(${lit(JSON.stringify(values))}))`)}.has(x)) return false;`);
      } else {
        out.push(`if (!R.includes(${this.constant(`JSON.parse(${lit(JSON.stringify(values))})`)}, x)) return false;`);
      }
    }
    return out;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Objects

  private objectSection(n: SchemaNode, e: string | undefined, flagLayout: boolean): string[] {
    const out: string[] = [];
    const props = n.properties;
    const required = n.required ?? [];
    // A pattern property whose schema is `true` does nothing unless marking or additionalProperties needs it.
    const allPatterns = n.patternProperties ?? [];
    const additionalNeedsMatch = n.additionalProperties >= 0 && !this.nodes[this.elide(n.additionalProperties)].alwaysTrue;
    const patterns = e !== undefined || additionalNeedsMatch ? allPatterns : allPatterns.filter((p) => !this.nodes[this.elide(p.node)].alwaysTrue);
    const ap = n.additionalProperties >= 0 ? this.nodes[this.elide(n.additionalProperties)] : undefined;
    const pn = n.propertyNames >= 0 ? this.nodes[this.elide(n.propertyNames)] : undefined;
    const apNeedsLoop = ap !== undefined && !ap.alwaysTrue;
    const pnNeedsLoop = pn !== undefined && !pn.alwaysTrue;
    const needsCount = n.minProperties > 0 || n.maxProperties >= 0;
    const propCount = props?.size ?? 0;
    const requiredNames = new Set(required);
    const allRequired = props === undefined || [...props.keys()].every((k) => requiredNames.has(k));
    const unrollable = propCount <= MAX_UNROLLED_PROPERTIES || (allRequired && propCount <= MAX_UNROLLED_REQUIRED);
    const loop = e !== undefined || patterns.length > 0 || apNeedsLoop || pnNeedsLoop || needsCount || !unrollable;
    const dependencies = n.dependencies ?? [];

    if (!loop) out.push(...this.objectProbe(n, props, required));
    else if (e === undefined && propCount === 0 && patterns.length === 0 && !pnNeedsLoop && apNeedsLoop) out.push(...this.objectValues(n, ap!, required));
    else out.push(...this.objectLoop(n, e, required, patterns, ap, pn, apNeedsLoop, pnNeedsLoop, needsCount));

    if (dependencies.length > 0) out.push(...this.objectDependencies(n, e, flagLayout));
    return out;
  }

  /** Unrolled: each declared name looked up directly (required first), as Blaze does for small objects. */
  private objectProbe(n: SchemaNode, props: Map<string, number> | undefined, required: string[]): string[] {
    const out: string[] = [];
    const names = [...(props?.keys() ?? [])];
    const requiredSet = new Set(required);
    names.sort((a, b) => Number(requiredSet.has(b)) - Number(requiredSet.has(a)));
    const v = this.scratch('p');
    if (names.length > 0) out.push(`let ${v};`);
    for (const name of names) {
      const child = props!.get(name)!;
      const check = this.call(child, v, n.resourceId);
      const isRequired = requiredSet.has(name);
      if (check === 'true') {
        if (isRequired) out.push(`if (${getProperty('x', name)} === undefined) return false;`);
      } else if (isRequired) {
        out.push(`if ((${v} = ${getProperty('x', name)}) === undefined || !${wrap(check)}) return false;`);
      } else {
        out.push(`if ((${v} = ${getProperty('x', name)}) !== undefined && !${wrap(check)}) return false;`);
      }
    }
    for (const name of required) {
      if (!props?.has(name)) out.push(`if (${getProperty('x', name)} === undefined) return false;`);
    }
    return out;
  }

  /** A map: only the values matter, and Object.values beats for-in over the per-object key shapes of maps. */
  private objectValues(n: SchemaNode, ap: SchemaNode, required: string[]): string[] {
    const out: string[] = [];
    const check = this.call(ap!.id, 'vs[i]', n.resourceId);
    out.push('const vs = Object.values(x);');
    if (n.minProperties > 0) out.push(`if (vs.length < ${n.minProperties}) return false;`);
    if (n.maxProperties >= 0) out.push(`if (vs.length > ${n.maxProperties}) return false;`);
    out.push(check === 'false' ? 'if (vs.length > 0) return false;' : `for (let i = 0; i < vs.length; i++) if (!${wrap(check)}) return false;`);
    for (const name of required) out.push(`if (${getProperty('x', name)} === undefined) return false;`);
    return out;
  }

  /** One pass over the instance's properties: names dispatched to their subschemas, then patterns and additional. */
  private objectLoop(
    n: SchemaNode,
    e: string | undefined,
    required: string[],
    patterns: Array<{ pattern: string; node: number }>,
    ap: SchemaNode | undefined,
    pn: SchemaNode | undefined,
    apNeedsLoop: boolean,
    pnNeedsLoop: boolean,
    needsCount: boolean,
  ): string[] {
    const out: string[] = [];
    const props = n.properties;
    const names = [...(props?.keys() ?? [])];
    // Required names that are also declared are counted as seen in the loop; the rest are looked up.
    const requiredBits = new Map<string, number>();
    for (const r of required) if (props?.has(r) && requiredBits.size < 30) requiredBits.set(r, requiredBits.size);
    const seen = requiredBits.size > 0 ? this.scratch('seen') : undefined;
    const count = needsCount ? this.scratch('n') : undefined;
    if (seen) out.push(`let ${seen} = 0;`);
    if (count) out.push(`let ${count} = 0;`);
    out.push('for (const k in x) {');
    const body: string[] = [];
    if (count) body.push(`${count}++;`);
    if (pnNeedsLoop) body.push(`if (!${wrap(this.call(pn!.id, 'k', n.resourceId))}) return false;`);
    const needsValue = names.length > 0 || patterns.length > 0 || apNeedsLoop;
    if (needsValue) body.push('const v = x[k];');
    const mark = e !== undefined ? `${e}.add(k);` : undefined;
    const hasTail = patterns.length > 0 || apNeedsLoop || (mark !== undefined && ap !== undefined);
    const matched = patterns.length > 0 && (apNeedsLoop || (mark !== undefined && ap !== undefined)) ? this.scratch('m') : undefined;
    if (matched) body.push(`let ${matched} = false;`);
    if (names.length > 0) {
      const caseBody = (name: string): string[] => {
        const lines: string[] = [];
        const check = this.call(props!.get(name)!, 'v', n.resourceId);
        if (check !== 'true') lines.push(`if (!${wrap(check)}) return false;`);
        const bit = requiredBits.get(name);
        if (bit !== undefined) lines.push(`${seen} |= ${1 << bit};`);
        if (mark) lines.push(mark);
        if (matched) lines.push(`${matched} = true;`);
        lines.push(hasTail && patterns.length > 0 ? 'break;' : 'continue;');
        return lines;
      };
      if (names.length <= MAX_SWITCH_NAMES) {
        body.push('switch (k) {');
        for (const name of names) {
          body.push(`  case ${lit(name)}:`);
          body.push(...indent(caseBody(name), 2));
        }
        body.push('}');
      } else if (DISPATCH_MAP) {
        const map = this.constant(`R.nameMap(JSON.parse(${lit(JSON.stringify(names))}))`);
        body.push(`switch (${map}.get(k)) {`);
        names.forEach((name, i) => {
          body.push(`  case ${i}:`);
          body.push(...indent(caseBody(name), 2));
        });
        body.push('}');
      } else {
        // Length first, then the few names of that length (SchemaCompiler's Utf8NameMap: length, then bytes).
        const byLength = new Map<number, string[]>();
        for (const name of names) {
          const list = byLength.get(name.length) ?? [];
          list.push(name);
          byLength.set(name.length, list);
        }
        body.push('switch (k.length) {');
        for (const [length, group] of [...byLength].sort((a, b) => a[0] - b[0])) {
          body.push(`  case ${length}:`);
          group.forEach((name, i) => {
            body.push(`    ${i === 0 ? 'if' : '} else if'} (k === ${lit(name)}) {`);
            body.push(...indent(caseBody(name), 3));
          });
          body.push('    }');
          body.push('    break;');
        }
        body.push('}');
      }
    }
    for (const p of patterns) {
      const check = this.call(p.node, 'v', n.resourceId);
      const lines: string[] = [];
      if (check !== 'true') lines.push(`if (!${wrap(check)}) return false;`);
      if (mark) lines.push(mark);
      if (matched) lines.push(`${matched} = true;`);
      if (lines.length > 0) body.push(`if (${this.patternTest(p.pattern, 'k')}) {`, ...indent(lines, 1), '}');
    }
    if (ap !== undefined && (apNeedsLoop || mark)) {
      const lines: string[] = [];
      const check = this.call(ap.id, 'v', n.resourceId);
      if (check !== 'true') lines.push(`if (!${wrap(check)}) return false;`);
      if (mark) lines.push(mark);
      if (lines.length > 0) {
        if (matched) body.push(`if (!${matched}) {`, ...indent(lines, 1), '}');
        else body.push(...lines);
      }
    }
    out.push(...indent(body, 1));
    out.push('}');
    if (seen) {
      const all = (1 << requiredBits.size) - 1;
      out.push(`if (${seen} !== ${all}) return false;`);
    }
    for (const r of required) if (!requiredBits.has(r)) out.push(`if (${getProperty('x', r)} === undefined) return false;`);
    if (count) {
      if (n.minProperties > 0) out.push(`if (${count} < ${n.minProperties}) return false;`);
      if (n.maxProperties >= 0) out.push(`if (${count} > ${n.maxProperties}) return false;`);
    }
    return out;
  }

  /** Dependencies: required lists always; schemas here only in the flag layout (tracking evaluates them in place). */
  private objectDependencies(n: SchemaNode, e: string | undefined, flagLayout: boolean): string[] {
    const out: string[] = [];
    const dependencies = n.dependencies ?? [];
    for (const d of dependencies) {
      const lines: string[] = [];
      for (const r of d.required ?? []) lines.push(`if (${getProperty('x', r)} === undefined) return false;`);
      if (d.schema !== undefined && flagLayout && e === undefined) {
        const check = this.call(d.schema, 'x', n.resourceId);
        if (check !== 'true') lines.push(`if (!${wrap(check)}) return false;`);
      }
      if (lines.length > 0) out.push(`if (${getProperty('x', d.name)} !== undefined) {`, ...indent(lines, 1), '}');
    }
    return out;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Arrays

  private arraySection(n: SchemaNode, e: string | undefined): string[] {
    const out: string[] = [];
    const prefix = n.prefixItems ?? [];
    const hasLength = n.minItems > 0 || n.maxItems >= 0 || prefix.length > 0 || n.items >= 0 || n.contains >= 0 || e !== undefined;
    if (!hasLength && !n.uniqueItems) return out;
    const len = this.scratch('n');
    out.push(`const ${len} = x.length;`);
    if (n.minItems > 0) out.push(`if (${len} < ${n.minItems}) return false;`);
    if (n.maxItems >= 0) out.push(`if (${len} > ${n.maxItems}) return false;`);
    prefix.forEach((child, i) => {
      const check = this.call(child, `x[${i}]`, n.resourceId);
      if (check === 'true') return;
      out.push(`if (${len} > ${i} && !${wrap(check)}) return false;`);
    });
    if (e !== undefined && prefix.length > 0) out.push(`for (let i = 0; i < ${len} && i < ${prefix.length}; i++) ${e}[i] = 1;`);
    if (n.items >= 0) {
      const item = this.nodes[this.elide(n.items)];
      if (item.alwaysFalse) {
        out.push(`if (${len} > ${prefix.length}) return false;`);
      } else {
        const check = this.call(item.id, 'x[i]', n.resourceId);
        if (check !== 'true') out.push(`for (let i = ${prefix.length}; i < ${len}; i++) if (!${wrap(check)}) return false;`);
        if (e !== undefined) out.push(`for (let i = ${prefix.length}; i < ${len}; i++) ${e}[i] = 1;`);
      }
    }
    if (n.contains >= 0) {
      const check = this.call(n.contains, 'x[i]', n.resourceId);
      const min = n.minContains;
      const max = n.maxContains;
      const markContains = e !== undefined && n.containsMarksEvaluated;
      if (min <= 0 && max < 0 && !markContains) {
        // minContains 0 and no maximum: contains always holds.
      } else {
        const c = this.scratch('c');
        out.push(`let ${c} = 0;`);
        const early = max < 0 && !markContains ? ` if (${c} >= ${min}) break;` : '';
        const markLine = markContains ? ` ${e}[i] = 1;` : '';
        out.push(`for (let i = 0; i < ${len}; i++) if (${check}) { ${c}++;${markLine}${early} }`);
        if (min > 0) out.push(`if (${c} < ${min}) return false;`);
        if (max >= 0) out.push(`if (${c} > ${max}) return false;`);
      }
    }
    if (n.uniqueItems) out.push(`if (${len} > 1 && !R.unique(x)) return false;`);
    return out;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Strings and numbers

  private stringSection(n: SchemaNode): string[] {
    const out: string[] = [];
    if (n.minLength > 0) {
      out.push(`if (x.length < ${n.minLength} || (x.length < ${2 * n.minLength} && R.codePoints(x) < ${n.minLength})) return false;`);
    }
    if (n.maxLength >= 0) out.push(`if (x.length > ${n.maxLength} && R.codePoints(x) > ${n.maxLength}) return false;`);
    if (n.pattern !== undefined) out.push(`if (!${wrap(this.patternTest(n.pattern, 'x'))}) return false;`);
    if (n.assertFormat && n.format !== undefined && !isNumericFormat(n.formatKind)) {
      const f = this.formatFunction(n.format, n.formatKind, n.dialect);
      if (f !== undefined) out.push(`if (!${f}(x)) return false;`);
    }
    if (n.assertContent) {
      const kind = n.content === ContentKind.Base64 ? 1 : n.content === ContentKind.Json ? 2 : 3;
      out.push(`if (!R.content(x, ${kind})) return false;`);
    }
    return out;
  }

  private numberSection(n: SchemaNode): string[] {
    const out: string[] = [];
    const num = (x: number): string => (Number.isFinite(x) ? String(x) : x > 0 ? 'Infinity' : '-Infinity');
    if (n.assertFormat && isNumericFormat(n.formatKind) && this.program.options.formats[n.format!] === undefined) {
      out.push(`if (!${this.constant(`R.numericFormatValidators[${lit(n.formatKind)}]`)}(x)) return false;`);
    }
    if (n.minimum !== undefined) out.push(`if (x < ${num(n.minimum)}) return false;`);
    if (n.maximum !== undefined) out.push(`if (x > ${num(n.maximum)}) return false;`);
    if (n.exclusiveMinimum !== undefined) out.push(`if (x <= ${num(n.exclusiveMinimum)}) return false;`);
    if (n.exclusiveMaximum !== undefined) out.push(`if (x >= ${num(n.exclusiveMaximum)}) return false;`);
    if (n.multipleOf !== undefined) {
      const d = n.multipleOf;
      if (Number.isInteger(d) && Math.abs(d) < 2 ** 53) out.push(`if (x % ${d} !== 0) return false;`);
      else out.push(`if (!R.multipleOf(x, ${num(d)})) return false;`);
    }
    return out;
  }

  /**
   * A test of `v` against an ECMAScript pattern. Common shapes match without the regular expression engine (as
   * PatternMatcher does in C#): a literal prefix, an exact literal or literal alternation, a contained literal, and
   * a run of one ASCII character class (`^[a-z0-9_-]+$`). Anything else uses a RegExp.
   */
  private patternTest(pattern: string, v: string): string {
    // The shape of a pattern's test is cached per process; only the constant names are per program.
    let shape = PATTERN_SHAPES.get(pattern);
    if (shape === undefined) {
      shape = patternShape(pattern);
      PATTERN_SHAPES.set(pattern, shape);
    }
    switch (shape.kind) {
      case 'expr':
        return shape.expr.replace(/\$V/g, v);
      case 'set':
        return `${this.constant(shape.init)}.has(${v})`;
      case 'fn':
        return `${this.constant(shape.init)}(${v})`;
      default:
        return `${this.constant(shape.init)}.test(${v})`;
    }
  }

  private regex(pattern: string): string {
    return this.constant(regexInit(pattern));
  }

  private formatFunction(format: string, kind: string, dialect: Dialect): string | undefined {
    const custom = this.program.options.formats[format];
    if (custom !== undefined) {
      let i = this.customFormatIndex.get(custom);
      if (i === undefined) {
        i = this.customFormats.length;
        this.customFormats.push(custom);
        this.customFormatIndex.set(custom, i);
      }
      return `F[${i}]`;
    }
    // Draft 4 and 6 host names are RFC 1123 names; later drafts apply the IDNA rules.
    if (kind === 'hostname' && dialect <= Dialect.Draft6) return this.constant('R.legacyHostname');
    // Names the dialect does not define are unknown formats, which always match.
    if (formatValidators[kind] === undefined) return undefined;
    return this.constant(`R.formatValidators[${lit(kind)}]`);
  }

  // -------------------------------------------------------------------------------------------------------------
  // In-place applicators

  /**
   * In-place applicators. `e` is the evaluated set (objects) or array (arrays) being marked, with `kind` saying which;
   * undefined for flag evaluation.
   */
  private inPlace(n: SchemaNode, e: string | undefined, kind: 'object' | 'array' | undefined): string[] {
    const out: string[] = [];
    const marks = (id: number): boolean => {
      if (e === undefined) return false;
      const c = this.nodes[this.elide(id)];
      return kind === 'object' ? c.marksProperties : c.marksItems;
    };
    const direct = (id: number): string => this.call(id, 'x', n.resourceId, marks(id) ? e : undefined);
    const fresh = kind === 'object' ? 'new Set()' : `new Uint8Array(x.length)`;
    const merge = (s: string): string =>
      kind === 'object' ? `for (const k of ${s}) ${e}.add(k);` : `for (let i = 0; i < ${s}.length; i++) if (${s}[i]) ${e}[i] = 1;`;

    if (n.ref >= 0) push(out, direct(n.ref));
    if (n.staticDynamicRef >= 0) push(out, direct(n.staticDynamicRef));
    if (n.dynamicRef !== undefined) {
      const variant: Variant = e !== undefined && (kind === 'object' ? this.dynamicMarks(n, 'object') : this.dynamicMarks(n, 'array')) ? 't' : 'v';
      out.push(`if (!${this.ref('d' + variant + n.id)}(x${variant === 't' ? ', ' + e : ''})) return false;`);
    }
    for (const c of n.allOf ?? []) push(out, direct(c));

    if (n.anyOf !== undefined) {
      if (e !== undefined && n.anyOf.some(marks)) {
        // Every passing branch contributes its annotations, so every branch is evaluated.
        const any = this.scratch('any');
        out.push(`let ${any} = false;`);
        for (const c of n.anyOf) {
          if (marks(c)) {
            const s = this.scratch('s');
            out.push(`{ const ${s} = ${fresh}; if (${this.call(c, 'x', n.resourceId, s)}) { ${any} = true; ${merge(s)} } }`);
          } else {
            out.push(`if (!${any} && ${wrap(this.call(c, 'x', n.resourceId))}) ${any} = true;`);
          }
        }
        out.push(`if (!${any}) return false;`);
      } else {
        out.push(...this.selectBranches(n, n.anyOf, n.anyOfDiscriminator, (branches) => this.anyOfCheck(n, branches)));
      }
    }

    if (n.oneOf !== undefined) {
      if (e !== undefined && n.oneOf.some(marks)) {
        const count = this.scratch('one');
        out.push(`let ${count} = 0;`);
        for (const c of n.oneOf) {
          if (marks(c)) {
            const s = this.scratch('s');
            out.push(`{ const ${s} = ${fresh}; if (${this.call(c, 'x', n.resourceId, s)}) { ${count}++; ${merge(s)} } }`);
          } else {
            out.push(`if (${wrap(this.call(c, 'x', n.resourceId))}) ${count}++;`);
          }
        }
        out.push(`if (${count} !== 1) return false;`);
      } else {
        out.push(...this.selectBranches(n, n.oneOf, n.oneOfDiscriminator, (branches) => this.oneOfCheck(n, branches)));
      }
    }

    if (n.not >= 0) {
      const check = this.call(n.not, 'x', n.resourceId);
      if (check === 'true') out.push('return false;');
      else if (check !== 'false') out.push(`if (${check}) return false;`);
    }

    if (n.if >= 0) {
      const thenCheck = n.then >= 0 ? direct(n.then) : 'true';
      const elseCheck = n.else >= 0 ? direct(n.else) : 'true';
      if (thenCheck !== 'true' || elseCheck !== 'true' || marks(n.if)) {
        let cond: string;
        if (marks(n.if)) {
          const s = this.scratch('s');
          out.push(`const ${s} = ${fresh};`);
          cond = this.call(n.if, 'x', n.resourceId, s);
          out.push(`if (${cond}) {`, `  ${merge(s)}`);
        } else {
          cond = this.call(n.if, 'x', n.resourceId);
          out.push(`if (${cond}) {`);
        }
        if (thenCheck !== 'true') out.push(`  if (!${wrap(thenCheck)}) return false;`);
        if (elseCheck !== 'true') out.push('} else {', `  if (!${wrap(elseCheck)}) return false;`);
        out.push('}');
      }
    }

    // Dependent schemas are in-place applicators; the flag layout emits them in the object section.
    if (e !== undefined && kind === 'object') {
      for (const d of n.dependencies ?? []) {
        if (d.schema === undefined) continue;
        const check = direct(d.schema);
        if (check !== 'true') out.push(`if (${getProperty('x', d.name)} !== undefined && !${wrap(check)}) return false;`);
      }
    }
    return out;
  }

  private dynamicMarks(n: SchemaNode, kind: 'object' | 'array'): boolean {
    const d = n.dynamicRef!;
    const ids = [d.fallback, ...d.byResource.values()];
    return ids.some((id) => {
      const c = this.nodes[this.elide(id)];
      return kind === 'object' ? c.marksProperties : c.marksItems;
    });
  }

  private anyOfCheck(n: SchemaNode, branches: number[]): string[] {
    if (branches.length === 0) return ['return false;'];
    const dispatch = this.typeDispatch(n, branches, false);
    if (dispatch !== undefined) return dispatch;
    const checks = branches.map((b) => this.call(b, 'x', n.resourceId));
    if (checks.includes('true')) return [];
    return [`if (!${wrap(checks.join(' || '))}) return false;`];
  }

  private oneOfCheck(n: SchemaNode, branches: number[]): string[] {
    if (branches.length === 0) return ['return false;'];
    if (branches.length === 1) {
      const check = this.call(branches[0], 'x', n.resourceId);
      return check === 'true' ? [] : [`if (!${wrap(check)}) return false;`];
    }
    const dispatch = this.typeDispatch(n, branches, true);
    if (dispatch !== undefined) return dispatch;
    const count = this.scratch('one');
    const out = [`let ${count} = 0;`];
    branches.forEach((b, i) => {
      const check = this.call(b, 'x', n.resourceId);
      if (i === 0) out.push(`if (${check}) ${count} = 1;`);
      else out.push(`if (${check} && ++${count} > 1) return false;`);
    });
    out.push(`if (${count} !== 1) return false;`);
    return out;
  }

  /**
   * When every branch asserts a type and no two accept the same kind, the instance's kind selects the only branch
   * that can pass (SchemaCompiler.ComputeTypeDispatch). Correct for anyOf and oneOf alike.
   */
  private typeDispatch(n: SchemaNode, branches: number[], _oneOf: boolean): string[] | undefined {
    const owner = new Map<Kind, number>();
    for (const b of branches) {
      const c = this.nodes[this.elide(b)];
      if (c.alwaysFalse) continue;
      if (c.alwaysTrue || !c.hasType) return undefined;
      for (const k of KINDS) {
        if (!kindAllowed(c.type, k)) continue;
        if (owner.has(k)) return undefined;
        owner.set(k, b);
      }
    }
    if (branches.length < 2) return undefined;
    const out: string[] = [];
    let first = true;
    for (const b of branches) {
      const kinds = KINDS.filter((k) => owner.get(k) === b);
      if (kinds.length === 0) continue;
      const check = this.call(b, 'x', n.resourceId);
      const test = kinds.map((k) => kindTest(k, 'x')).join(' || ');
      out.push(`${first ? 'if' : '} else if'} (${test}) {`);
      if (check !== 'true') out.push(`  if (!${wrap(check)}) return false;`);
      first = false;
    }
    if (first) return ['return false;'];
    out.push('} else {', '  return false;', '}');
    return out;
  }

  /** Narrows oneOf/anyOf branches by a discriminator property when the instance is an object that has it. */
  private selectBranches(n: SchemaNode, branches: number[], disc: Discriminator | undefined, check: (subset: number[]) => string[]): string[] {
    if (disc === undefined || disc.known.some(([v]) => typeof v === 'number' && !Number.isFinite(v))) return check(branches);
    const d = this.scratch('d');
    const out: string[] = [];
    out.push(`if (${kindTest('object', 'x')}) {`);
    out.push(`  const ${d} = ${getProperty('x', disc.property)};`);
    out.push(`  if (${d} === undefined) {`);
    out.push(...indent(disc.allRequire ? ['return false;'] : check(branches), 2));
    out.push('  } else {');
    out.push(`    switch (${d}) {`);
    // Group values selecting the same branch subset.
    const groups = new Map<string, Array<string | number | boolean>>();
    for (const [value, subset] of disc.known) {
      const key = subset.join(',');
      const list = groups.get(key) ?? [];
      list.push(value);
      groups.set(key, list);
    }
    for (const [key, values] of groups) {
      const subset = key === '' ? [] : key.split(',').map((i) => branches[Number(i)]);
      for (const v of values) out.push(`      case ${lit(v)}:`);
      out.push(`      {`);
      out.push(...indent(check(subset), 4));
      out.push('        break;', '      }');
    }
    out.push('      default: {');
    out.push(...indent(check(disc.unknown.map((i) => branches[i])), 4));
    out.push('      }', '    }', '  }', '} else {');
    out.push(...indent(check(branches), 1));
    out.push('}');
    return out;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Tracking layout (unevaluatedProperties/unevaluatedItems)

  /**
   * When every in-place contributor of evaluated properties/items is unconditional ($ref and allOf chains, with no
   * marking anyOf/oneOf/if/dependentSchemas/$dynamicRef), the evaluated set is static: declared names, patterns, a
   * prefix length, or everything. unevaluatedProperties/unevaluatedItems then needs no tracking at run time (the
   * idea behind the C# fused object plan). Returns undefined when some contribution is conditional.
   */
  private staticCoverage(n: SchemaNode, kind: 'object' | 'array'): Coverage | undefined {
    const empty = (): Coverage => ({ names: new Set(), patterns: [], prefix: 0, all: false });
    const main = empty();
    const conditional: Coverage[] = [];
    const marks = (id: number): boolean => {
      const c = this.nodes[this.elide(id)];
      return kind === 'object' ? c.marksProperties : c.marksItems;
    };
    const visit = (id: number, root: boolean, coverage: Coverage, visited: Set<number>): boolean => {
      const m = this.nodes[id];
      if (visited.has(id)) return true;
      visited.add(id);
      if (m.alwaysTrue || m.alwaysFalse) return true;
      // Conditional contributors (a branch may pass or fail) must have a static coverage of their own; they are
      // harmless when it adds nothing to the unconditional coverage (checked by the caller).
      const conditionalChildren = [
        ...(m.anyOf ?? []),
        ...(m.oneOf ?? []),
        ...[m.if, m.then, m.else].filter((c) => c >= 0),
        ...(m.dependencies ?? []).flatMap((d) => (d.schema !== undefined ? [d.schema] : [])),
        ...(m.dynamicRef !== undefined ? [m.dynamicRef.fallback, ...m.dynamicRef.byResource.values()] : []),
      ];
      for (const c of conditionalChildren) {
        if (!marks(c)) continue;
        const sub = empty();
        if (!visit(this.elide(c), false, sub, new Set())) return false;
        conditional.push(sub);
      }
      if (kind === 'object') {
        for (const name of m.properties?.keys() ?? []) coverage.names.add(name);
        for (const p of m.patternProperties ?? []) if (!coverage.patterns.includes(p.pattern)) coverage.patterns.push(p.pattern);
        if (m.additionalProperties >= 0 || (!root && m.unevaluatedProperties >= 0)) coverage.all = true;
      } else {
        coverage.prefix = Math.max(coverage.prefix, m.prefixItems?.length ?? 0);
        if (m.items >= 0 || (!root && m.unevaluatedItems >= 0)) coverage.all = true;
        if (m.contains >= 0 && m.containsMarksEvaluated) return false;
      }
      for (const c of [m.ref, m.staticDynamicRef, ...(m.allOf ?? [])]) {
        if (c >= 0 && marks(c) && !visit(this.elide(c), false, coverage, visited)) return false;
      }
      return true;
    };
    if (!visit(n.id, true, main, new Set())) return undefined;
    const within = (c: Coverage): boolean =>
      main.all ||
      (!c.all && c.prefix <= main.prefix && [...c.names].every((x) => main.names.has(x)) && c.patterns.every((x) => main.patterns.includes(x)));
    return conditional.every(within) ? main : undefined;
  }

  /** The object or array branch of a node whose unevaluated keyword is decided by static coverage. */
  private fusedUnevaluated(n: SchemaNode, kind: 'object' | 'array', coverage: Coverage, variant: Variant): string[] {
    // Other-kind tracking for an enclosing consumer is not needed here: this branch only runs for `kind`.
    const lines: string[] = [];
    lines.push(...this.kindSection(n, kind, undefined, true));
    lines.push(...this.inPlace(n, undefined, undefined));
    if (!coverage.all) {
      if (kind === 'object') {
        const check = this.call(n.unevaluatedProperties, 'x[k]', n.resourceId);
        if (check !== 'true') {
          lines.push('for (const k in x) {');
          const names = [...coverage.names];
          if (names.length > 0) {
            if (names.length <= MAX_SWITCH_NAMES) {
              lines.push(`  switch (k) {`, ...names.map((name) => `    case ${lit(name)}:`), '      continue;', '  }');
            } else {
              const byLength = new Map<number, string[]>();
              for (const name of names) byLength.set(name.length, [...(byLength.get(name.length) ?? []), name]);
              lines.push('  switch (k.length) {');
              for (const [length, group] of [...byLength].sort((a, b) => a[0] - b[0])) {
                lines.push(`    case ${length}:`, `      if (${group.map((g) => `k === ${lit(g)}`).join(' || ')}) continue;`, '      break;');
              }
              lines.push('  }');
            }
          }
          for (const p of coverage.patterns) lines.push(`  if (${this.patternTest(p, 'k')}) continue;`);
          lines.push(check === 'false' ? '  return false;' : `  if (!${wrap(check)}) return false;`);
          lines.push('}');
        }
      } else {
        const check = this.call(n.unevaluatedItems, 'x[i]', n.resourceId);
        if (check !== 'true') {
          lines.push(
            check === 'false'
              ? `if (x.length > ${coverage.prefix}) return false;`
              : `for (let i = ${coverage.prefix}; i < x.length; i++) if (!${wrap(check)}) return false;`,
          );
        }
      }
    }
    if (variant === 't') lines.push(kind === 'object' ? 'for (const k in x) ev.add(k);' : 'ev.fill(1);');
    lines.push('return true;');
    return lines;
  }

  private trackingBody(n: SchemaNode, variant: Variant): string[] {
    const out: string[] = [];
    if (n.alwaysFalse) return ['return false;'];
    if (n.alwaysTrue) return ['return true;'];
    out.push(...this.constEnum(n));
    const mask = n.hasType ? n.type : TypeMask.All;

    for (const kind of ['array', 'object'] as const) {
      const test = kindTest(kind, 'x');
      if (!kindAllowed(mask, kind)) {
        out.push(`if (${test}) return false;`);
        continue;
      }
      const own = kind === 'object' ? n.unevaluatedProperties >= 0 : n.unevaluatedItems >= 0;
      const coverage = own ? this.staticCoverage(n, kind) : undefined;
      if (coverage !== undefined) {
        out.push(`if (${test}) {`, ...indent(this.fusedUnevaluated(n, kind, coverage, variant), 1), '}');
        continue;
      }
      const e = own ? this.scratch('e') : variant === 't' ? 'ev' : undefined;
      const lines: string[] = [];
      if (own) lines.push(`const ${e} = ${kind === 'object' ? 'new Set()' : 'new Uint8Array(x.length)'};`);
      lines.push(...this.kindSection(n, kind, e, e === undefined));
      lines.push(...this.inPlace(n, e, e === undefined ? undefined : kind));
      if (own) {
        if (kind === 'object') {
          const check = this.call(n.unevaluatedProperties, 'x[k]', n.resourceId);
          if (check !== 'true') lines.push(`for (const k in x) if (!${e}.has(k) && !${wrap(check)}) return false;`);
          if (variant === 't') lines.push('for (const k in x) ev.add(k);');
        } else {
          const check = this.call(n.unevaluatedItems, 'x[i]', n.resourceId);
          if (check !== 'true') lines.push(`for (let i = 0; i < x.length; i++) if (${e}[i] === 0 && !${wrap(check)}) return false;`);
          if (variant === 't') lines.push('ev.fill(1);');
        }
      }
      lines.push('return true;');
      out.push(`if (${test}) {`, ...indent(lines, 1), '}');
    }

    // Scalars: flag evaluation.
    const scalarKinds = KINDS.filter((k) => k !== 'object' && k !== 'array');
    const allowedScalars = scalarKinds.filter((k) => kindAllowed(mask, k));
    if (allowedScalars.length === 0) {
      out.push('return false;');
      return out;
    }
    const withWork = allowedScalars.map((k) => [k, this.kindSection(n, k, undefined, true)] as const).filter(([, l]) => l.length > 0);
    for (const [k, lines] of withWork) out.push(`if (${kindTest(k, 'x')}) {`, ...indent(lines, 1), '}');
    if (n.hasType && allowedScalars.length < scalarKinds.length) {
      out.push(`if (!(${allowedScalars.map((k) => kindTest(k, 'x')).join(' || ')})) return false;`);
    }
    out.push(...this.inPlace(n, undefined, undefined));
    out.push('return true;');
    return out;
  }

  // -------------------------------------------------------------------------------------------------------------
  // Linking: merge structurally identical functions, then emit the reachable ones.

  private link(rootName: string): { declarations: string; root: string } {
    const names = [...this.templates.keys()];
    const indexOf = new Map<string, number>();
    names.forEach((name, i) => indexOf.set(name, i));
    // Split each template once: literal parts (even indices) around references (odd indices).
    const parts = names.map((name) => this.templates.get(name)!.split(REF_RE));
    const refs = parts.map((p) => {
      const r = new Int32Array(p.length >> 1);
      for (let k = 1, j = 0; k < p.length; k += 2, j++) r[j] = indexOf.get(p[k])!;
      return r;
    });

    // Partition refinement: start from the literal parts alone, refine by the classes of the references until
    // the number of classes stops growing. Bisimilar functions (identical text up to equivalent callees) merge.
    let classOf: Int32Array = new Int32Array(names.length);
    let count: number;
    {
      const intern = new Map<string, number>();
      for (let i = 0; i < names.length; i++) {
        const p = parts[i];
        let sig = p[0];
        for (let k = 2; k < p.length; k += 2) sig += REF_START + p[k];
        let c = intern.get(sig);
        if (c === undefined) intern.set(sig, (c = intern.size));
        classOf[i] = c;
      }
      count = intern.size;
    }
    // Only when some functions share text can any merge; the refinement is then compiled and run.
    classOf = count < names.length ? refineClasses(classOf, count, refs) : Int32Array.from(names, (_, i) => i);

    const root = indexOf.get(rootName)!;
    const representative = new Int32Array(names.length).fill(-1);
    representative[classOf[root]] = root;
    for (let i = 0; i < names.length; i++) if (representative[classOf[i]] < 0) representative[classOf[i]] = i;
    const canonical = (i: number): number => representative[classOf[i]];

    // Emit the reachable representatives.
    const emitted = new Uint8Array(names.length);
    const out: string[] = [];
    for (const [init, name] of this.constants) out.push(`const ${name} = ${init};`);
    const stack = [canonical(root)];
    while (stack.length > 0) {
      const i = stack.pop()!;
      if (emitted[i]) continue;
      emitted[i] = 1;
      const p = parts[i];
      let text = p[0].replace('function @(', `function ${names[i]}(`);
      for (let k = 1, j = 0; k < p.length; k += 2, j++) {
        const target = canonical(refs[i][j]);
        text += names[target] + p[k + 1];
        if (!emitted[target]) stack.push(target);
      }
      out.push(text);
    }
    // Below a size bound, wrap each function in parentheses so that V8 compiles it with the program instead of
    // pre-parsing now and parsing again on its first call: the first (cold) pass gets faster for little extra compile.
    // Large programs keep lazy compilation, as much of their code never runs.
    const size = out.reduce((a, t) => a + t.length, 0);
    if (size < EAGER_MAX_SOURCE) {
      for (let k = this.constants.size; k < out.length; k++) {
        const name = /^function (\w+)\(/.exec(out[k])![1];
        out[k] = `const ${name} = (${out[k]});`;
      }
    }
    return { declarations: out.join('\n'), root: names[canonical(root)] };
  }
}

/**
 * Partition refinement: from classes of equal literal text, refines by the classes of the references until the number
 * of classes stops growing, so that bisimilar functions (identical text up to equivalent callees) share a class.
 */
function refineClasses(initial: Int32Array, initialCount: number, refs: Int32Array[]): Int32Array {
  let classOf = initial;
  let count = initialCount;
  const n = classOf.length;
  // Only functions that still share a class can split; singletons keep theirs.
  let size = new Int32Array(count);
  for (let i = 0; i < n; i++) size[classOf[i]]++;
  for (;;) {
    const intern = new Map<string, number>();
    const next = new Int32Array(n);
    let nextCount = 0;
    const singleton = new Int32Array(count).fill(-1);
    for (let i = 0; i < n; i++) {
      const cls = classOf[i];
      if (size[cls] === 1) {
        if (singleton[cls] < 0) singleton[cls] = nextCount++;
        next[i] = singleton[cls];
        continue;
      }
      const r = refs[i];
      let sig = String(cls);
      for (let j = 0; j < r.length; j++) sig += ',' + classOf[r[j]];
      let c = intern.get(sig);
      if (c === undefined) {
        c = nextCount++;
        intern.set(sig, c);
      }
      next[i] = c;
    }
    classOf = next;
    if (nextCount === count) return classOf;
    count = nextCount;
    size = new Int32Array(count);
    for (let i = 0; i < n; i++) size[classOf[i]]++;
  }
}

function indent(lines: string[], depth: number): string[] {
  const pad = '  '.repeat(depth);
  return lines.map((l) => pad + l);
}

function wrap(expr: string): string {
  return /^[\w$.\u0001\u0002]+\([^()]*\)$/.test(expr) || /^[\w$]+$/.test(expr) || (expr.startsWith('(') && balanced(expr)) ? expr : `(${expr})`;
}

function balanced(expr: string): boolean {
  let depth = 0;
  for (let i = 0; i < expr.length; i++) {
    if (expr[i] === '(') depth++;
    else if (expr[i] === ')') {
      depth--;
      if (depth === 0 && i < expr.length - 1) return false;
    }
  }
  return depth === 0;
}

function push(out: string[], check: string): void {
  if (check === 'true') return;
  if (check === 'false') out.push('return false;');
  else out.push(`if (!${wrap(check)}) return false;`);
}


type Ranges = Array<[number, number]>;
interface SeqAtom {
  kind: 'atom';
  ranges: Ranges;
  min: number;
  max: number; // -1 for unbounded
}
interface SeqGroup {
  kind: 'group';
  alternatives: Ranges[][];
}
type SeqItem = SeqAtom | SeqGroup;

const DIGIT: Ranges = [[48, 57]];
const WORD: Ranges = [[48, 57], [65, 90], [97, 122], [95, 95]];

/**
 * Compiles an anchored pattern made of ASCII classes and literals into a matcher function (source text), or returns
 * undefined. Every item but the last has a fixed length (groups of equal-length alternatives are fixed), so no
 * backtracking is needed: PatternMatcher.TryParseClassSequence in the C# evaluator.
 */
export function compileClassSequence(pattern: string): string | undefined {
  if (!pattern.startsWith('^')) return undefined;
  let i = 1;
  let anchoredEnd = false;
  const parseClass = (): Ranges | undefined => {
    // at '['
    i++;
    if (pattern[i] === '^') return undefined;
    const ranges: Ranges = [];
    while (i < pattern.length && pattern[i] !== ']') {
      const a = parseClassAtom();
      if (a === undefined) return undefined;
      if (Array.isArray(a)) {
        ranges.push(...a);
        continue;
      }
      if (pattern[i] === '-' && pattern[i + 1] !== ']' && i + 1 < pattern.length) {
        i++;
        const b = parseClassAtom();
        if (b === undefined || Array.isArray(b) || b < a) return undefined;
        ranges.push([a, b]);
      } else {
        ranges.push([a, a]);
      }
    }
    if (pattern[i] !== ']') return undefined;
    i++;
    return ranges.length > 0 ? ranges : undefined;
  };
  const parseClassAtom = (): number | Ranges | undefined => {
    let c = pattern[i++];
    if (c === '\\') {
      c = pattern[i++];
      if (c === 'd') return DIGIT;
      if (c === 'w') return WORD;
      if (c === undefined || !/[.\-_/\\\][+*?(){}|^$]/.test(c)) return undefined;
    }
    const code = c.charCodeAt(0);
    return code < 128 ? code : undefined;
  };
  const parseAtom = (): Ranges | undefined => {
    const c = pattern[i];
    if (c === undefined) return undefined;
    if (c === '[') return parseClass();
    if (c === '\\') {
      const d = pattern[i + 1];
      i += 2;
      if (d === 'd') return DIGIT;
      if (d === 'w') return WORD;
      if (d !== undefined && /[.\-_/\\\][+*?(){}|^$]/.test(d)) return [[d.charCodeAt(0), d.charCodeAt(0)]];
      return undefined;
    }
    if (/[.()|?*+{}$^\]]/.test(c)) return undefined;
    const code = c.charCodeAt(0);
    if (code >= 128) return undefined;
    i++;
    return [[code, code]];
  };
  const parseQuantifier = (): [number, number] | undefined => {
    const c = pattern[i];
    if (c === '?') return (i++, [0, 1]);
    if (c === '*') return (i++, [0, -1]);
    if (c === '+') return (i++, [1, -1]);
    if (c === '{') {
      const m = /^\{(\d+)(,(\d*))?\}/.exec(pattern.slice(i));
      if (m === null) return undefined;
      i += m[0].length;
      const min = Number(m[1]);
      const max = m[2] === undefined ? min : m[3] === '' ? -1 : Number(m[3]);
      return max >= 0 && max < min ? undefined : [min, max];
    }
    return [1, 1];
  };
  const items: SeqItem[] = [];
  while (i < pattern.length) {
    if (pattern[i] === '$' && i === pattern.length - 1) {
      anchoredEnd = true;
      i++;
      break;
    }
    if (pattern[i] === '(') {
      i += pattern.startsWith('(?:', i) ? 3 : 1;
      const alternatives: Ranges[][] = [[]];
      for (;;) {
        if (i >= pattern.length) return undefined;
        if (pattern[i] === ')') {
          i++;
          break;
        }
        if (pattern[i] === '|') {
          i++;
          alternatives.push([]);
          continue;
        }
        const atom = parseAtom();
        if (atom === undefined) return undefined;
        const q = parseQuantifier();
        if (q === undefined || q[0] !== q[1] || q[0] > 8) return undefined;
        for (let k = 0; k < q[0]; k++) alternatives[alternatives.length - 1].push(atom);
      }
      const length = alternatives[0].length;
      if (length === 0 || alternatives.some((a) => a.length !== length)) return undefined;
      if (/[?*+{]/.test(pattern[i] ?? '')) return undefined;
      items.push({ kind: 'group', alternatives });
      continue;
    }
    const atom = parseAtom();
    if (atom === undefined) return undefined;
    const q = parseQuantifier();
    if (q === undefined) return undefined;
    items.push({ kind: 'atom', ranges: atom, min: q[0], max: q[1] });
  }
  if (i !== pattern.length || items.length === 0) return undefined;
  // Only the last item may vary in length.
  for (let k = 0; k < items.length - 1; k++) {
    const it = items[k];
    if (it.kind === 'atom' && it.min !== it.max) return undefined;
  }
  const test = (ranges: Ranges, c: string): string =>
    ranges.map(([a, b]) => (a === b ? `${c} === ${a}` : `(${c} >= ${a} && ${c} <= ${b})`)).join(' || ');
  const body: string[] = ['const n = s.length;', 'let p = 0;'];
  items.forEach((it, index) => {
    const last = index === items.length - 1;
    if (it.kind === 'group') {
      const length = it.alternatives[0].length;
      const alt = it.alternatives.map((a) => '(' + a.map((r, o) => `(${test(r, `s.charCodeAt(p + ${o})`)})`).join(' && ') + ')');
      body.push(`if (p + ${length} > n || !(${alt.join(' || ')})) return false;`, `p += ${length};`);
      return;
    }
    if (it.min === it.max && !last) {
      body.push(`if (p + ${it.min} > n) return false;`);
      body.push(`for (let j = 0; j < ${it.min}; j++) { const c = s.charCodeAt(p + j); if (!(${test(it.ranges, 'c')})) return false; }`);
      body.push(`p += ${it.min};`);
      return;
    }
    // The last item: a run of min..max, as long as possible (nothing follows it to backtrack for).
    body.push('let q = p;');
    body.push(`while (q < n${it.max >= 0 ? ` && q - p < ${it.max}` : ''}) { const c = s.charCodeAt(q); if (!(${test(it.ranges, 'c')})) break; q++; }`);
    body.push(`if (q - p < ${it.min}) return false;`, 'p = q;');
  });
  body.push(anchoredEnd ? 'return p === n;' : 'return true;');
  return `(s) => { ${body.join(' ')} }`;
}


type PatternShape = { kind: 'expr'; expr: string } | { kind: 'set' | 'fn' | 'regex'; init: string };
const PATTERN_SHAPES = new Map<string, PatternShape>();

/** The RegExp constructor call for a pattern: the `u` flag, or none for patterns that only parse without it. */
function regexInit(pattern: string): string {
  let flags = 'u';
  try {
    new RegExp(pattern, 'u');
  } catch {
    try {
      new RegExp(pattern);
      flags = '';
    } catch {
      // An invalid pattern is a schema error; the compile surfaces it when the constant is evaluated.
    }
  }
  return `new RegExp(${lit(pattern)}, ${lit(flags)})`;
}

/** How a pattern is tested (see CodeGenerator.patternTest); `$V` stands for the value. */
function patternShape(pattern: string): PatternShape {
  const plain = (t: string): boolean => /^[A-Za-z0-9 _\-/:@,;=!%&'"<>~`#]*$/.test(t);
  let m: RegExpExecArray | null;
  if ((m = /^\^([^]*)$/.exec(pattern)) !== null && plain(m[1]) && !m[1].endsWith('$')) return { kind: 'expr', expr: `$V.startsWith(${lit(m[1])})` };
  if ((m = /^\^([^]*)\$$/.exec(pattern)) !== null && plain(m[1])) return { kind: 'expr', expr: `$V === ${lit(m[1])}` };
  if ((m = /^\^\(\?:([^()]*)\)\$$|^\^\(([^()]*)\)\$$/.exec(pattern)) !== null) {
    const alternatives = (m[1] ?? m[2]).split('|');
    if (alternatives.every(plain)) {
      if (alternatives.length <= 6) return { kind: 'expr', expr: '(' + alternatives.map((a) => `$V === ${lit(a)}`).join(' || ') + ')' };
      return { kind: 'set', init: `new Set(JSON.parse(${lit(JSON.stringify(alternatives))}))` };
    }
  }
  if (pattern.length > 0 && plain(pattern)) return { kind: 'expr', expr: `$V.includes(${lit(pattern)})` };
  const sequence = compileClassSequence(pattern);
  if (sequence !== undefined) return { kind: 'fn', init: sequence };
  return { kind: 'regex', init: regexInit(pattern) };
}
