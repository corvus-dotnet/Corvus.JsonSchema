// Compiles loaded schema documents into a SchemaNode graph and runs the compile-time analyses.
// A port of Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaCompiler (flag-mode subset).

import { Dialect, Vocab } from './dialect.js';
import { escapePointerToken, SchemaLoader, SchemaResource, SchemaTarget } from './loader.js';
import { AnnotationEntry, ContentKind, DependencyEntry, Discriminator, SchemaNode, TypeMask } from './node.js';

/** Keywords SchemaCompiler.CompileNode handles itself; anything else is an unknown keyword (an annotation from 2019-09). */
const KNOWN_KEYWORDS = new Set([
  'if', 'not', 'type', 'enum', '$ref', 'then', 'else', 'const', 'items', 'allOf', 'anyOf', 'oneOf', 'title', '$defs',
  'format', 'pattern', 'maximum', 'minimum', 'default', '$schema', '$anchor', 'required', 'contains', 'maxItems',
  'minItems', 'examples', 'readOnly', '$comment', 'maxLength', 'minLength', 'writeOnly', 'properties', 'multipleOf',
  'deprecated', 'uniqueItems', 'prefixItems', 'minContains', 'maxContains', 'description', '$vocabulary', 'definitions',
  '$dynamicRef', 'dependencies', 'propertyNames', 'maxProperties', 'minProperties', 'contentSchema', '$recursiveRef',
  '$dynamicAnchor', 'contentEncoding', 'additionalItems', 'exclusiveMaximum', 'exclusiveMinimum', 'unevaluatedItems',
  'contentMediaType', '$recursiveAnchor', 'dependentSchemas', 'patternProperties', 'dependentRequired',
  'additionalProperties', 'unevaluatedProperties', 'id', '$id',
]);
import { formatKind } from './formats.js';
import { EvaluatorOptions, normalizeOptions, CompileOptions, SchemaCompilationError } from './options.js';
import { decodeFragment, split } from './uri.js';

interface PendingDynamicRef {
  nodeId: number;
  anchor: string;
  isRecursive: boolean;
  initialTarget: SchemaTarget;
  seenResources: Set<number>;
  candidates: Array<[resourceId: number, nodeId: number]>;
}

/** The compiled program: the node graph, its entry node and whether it maintains a dynamic scope. */
export interface CompiledSchema {
  readonly nodes: SchemaNode[];
  readonly root: number;
  readonly rootResource: number;
  readonly usesDynamicScope: boolean;
  readonly options: EvaluatorOptions;
  /**
   * What each node's annotations are computed from, indexed by node id, until {@link resolveAnnotations} fills
   * {@link SchemaNode.annotations}. Only results collection needs annotations, so flag-mode compilation skips them.
   */
  annotationSources: Array<AnnotationSource | undefined> | undefined;
}

/** The inputs to a node's annotation keywords. */
interface AnnotationSource {
  readonly e: Record<string, unknown>;
  readonly dialect: Dialect;
  readonly vocab: Vocab;
  readonly legacy: boolean;
  readonly content: boolean;
}

/** Fills every node's annotations (once), for results collection. */
export function resolveAnnotations(program: CompiledSchema): void {
  const sources = program.annotationSources;
  if (sources === undefined) return;
  program.annotationSources = undefined;
  for (let id = 0; id < sources.length; id++) {
    const src = sources[id];
    if (src !== undefined) program.nodes[id].annotations = collectAnnotations(src, program.options.assertFormat !== undefined);
  }
}

/** The annotation keywords of a schema object, in the order SchemaCompiler.CompileNode records them. */
function collectAnnotations(src: AnnotationSource, assertFormatSet: boolean): AnnotationEntry[] | undefined {
  const { e, dialect, vocab, legacy, content } = src;
  const metaData = legacy || (vocab & Vocab.MetaData) !== 0;
  const formatAnnotate = legacy || (vocab & (Vocab.FormatAnnotation | Vocab.FormatAssertion)) !== 0 || assertFormatSet;
  const out: AnnotationEntry[] = [];
  const add = (keyword: string, stringsOnly = false): void => {
    out.push({ keyword, value: e[keyword], stringsOnly });
  };
  for (const name of Object.keys(e)) {
    switch (name) {
      case 'title':
      case 'description':
      case 'default':
        if (metaData) add(name);
        break;
      case 'examples':
        if (metaData && dialect >= Dialect.Draft6) add(name);
        break;
      case 'readOnly':
      case 'writeOnly':
        if (metaData && dialect >= Dialect.Draft7) add(name);
        break;
      case 'deprecated':
        if (metaData && dialect >= Dialect.Draft201909) add(name);
        break;
      case 'format':
        if (typeof e.format === 'string' && formatAnnotate) add(name);
        break;
      default:
        // Unknown keywords are collected as annotations from 2019-09 onwards.
        if (dialect >= Dialect.Draft201909 && !KNOWN_KEYWORDS.has(name)) add(name);
        break;
    }
  }
  if (content && dialect >= Dialect.Draft7 && (e.contentEncoding !== undefined || e.contentMediaType !== undefined || e.contentSchema !== undefined)) {
    if (e.contentEncoding !== undefined) add('contentEncoding', true);
    if (e.contentMediaType !== undefined) {
      add('contentMediaType', true);
      // contentSchema is only meaningful alongside contentMediaType.
      if (e.contentSchema !== undefined && dialect >= Dialect.Draft201909) add('contentSchema', true);
    }
  }
  return out.length > 0 ? out : undefined;
}

function isObject(v: unknown): v is Record<string, unknown> {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

/** Spreads marksProperties/marksItems from each marking node to its in-place parents (excluding `not` edges). */
function propagateMarks(nodes: SchemaNode[], edges: number[][]): void {
  const count = nodes.length;
  const parents: number[][] = new Array(count);
  for (let i = 0; i < count; i++) parents[i] = [];
  for (let i = 0; i < count; i++) {
    // `not` does not contribute annotations: nodes with one use the edges without it.
    const children = nodes[i].not >= 0 ? nodes[i].inPlaceChildren(false) : edges[i];
    for (const c of children) parents[c].push(i);
  }
  const work: number[] = [];
  for (let i = 0; i < count; i++) if (nodes[i].marksProperties) work.push(i);
  while (work.length > 0) {
    for (const p of parents[work.pop()!]) {
      if (!nodes[p].marksProperties) {
        nodes[p].marksProperties = true;
        work.push(p);
      }
    }
  }
  for (let i = 0; i < count; i++) if (nodes[i].marksItems) work.push(i);
  while (work.length > 0) {
    for (const p of parents[work.pop()!]) {
      if (!nodes[p].marksItems) {
        nodes[p].marksItems = true;
        work.push(p);
      }
    }
  }
}

function num(v: unknown): number | undefined {
  return typeof v === 'number' ? v : undefined;
}

/** Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive. */
function compileDraft4Bounds(node: SchemaNode, e: Record<string, unknown>): void {
  const max = num(e.maximum);
  const min = num(e.minimum);
  if (max !== undefined) {
    if (e.exclusiveMaximum === true) node.exclusiveMaximum = max;
    else node.maximum = max;
  }
  if (min !== undefined) {
    if (e.exclusiveMinimum === true) node.exclusiveMinimum = min;
    else node.minimum = min;
  }
}

function getInt(v: unknown, fallback: number): number {
  return typeof v === 'number' && Number.isFinite(v) && Number.isInteger(v) ? v : fallback;
}

function typeMaskOf(name: unknown): TypeMask {
  switch (name) {
    case 'null':
      return TypeMask.Null;
    case 'boolean':
      return TypeMask.Boolean;
    case 'object':
      return TypeMask.Object;
    case 'array':
      return TypeMask.Array;
    case 'number':
      return TypeMask.Number;
    case 'string':
      return TypeMask.String;
    case 'integer':
      return TypeMask.Integer;
    default:
      return TypeMask.None;
  }
}

export class SchemaCompiler {
  private readonly nodes: SchemaNode[] = [];
  private readonly targets: SchemaTarget[] = [];
  private readonly worklist: number[] = [];
  private worklistHead = 0;
  private readonly pendingDynamicRefs: PendingDynamicRef[] = [];
  private readonly annotationSources: Array<AnnotationSource | undefined> = [];
  private entryNode = -1;

  private constructor(
    private readonly loader: SchemaLoader,
    private readonly options: EvaluatorOptions,
  ) {}

  static compile(schema: unknown, compileOptions?: CompileOptions): CompiledSchema {
    const options = normalizeOptions(compileOptions);
    const loader = new SchemaLoader(options);
    const rootResource = loader.loadRoot(schema, options.baseUri);
    const compiler = new SchemaCompiler(loader, options);
    let target: SchemaTarget = { document: rootResource.document, pointer: '', value: schema, resource: rootResource };
    if (options.entryPoint !== undefined) {
      const resolved = loader.tryResolveReference(rootResource, options.entryPoint);
      if (resolved === undefined) throw new SchemaCompilationError(`Unable to resolve the entry point '${options.entryPoint}'.`);
      target = resolved;
    }
    compiler.entryNode = compiler.getNode(target);
    compiler.compileAll();
    const usesDynamicScope = compiler.nodes.some((n) => n.dynamicRef !== undefined);
    compiler.analyse();
    return {
      nodes: compiler.nodes,
      root: compiler.entryNode,
      rootResource: compiler.nodes[compiler.entryNode].resourceId,
      usesDynamicScope,
      options,
      annotationSources: compiler.annotationSources,
    };
  }

  private getNode(target: SchemaTarget): number {
    let id = target.document.nodeOf.get(target.value, target.pointer);
    if (id === undefined) {
      id = this.nodes.length;
      const location = target.document.retrievalUri + '#' + target.pointer;
      this.nodes.push(new SchemaNode(id, target.resource.id, target.resource.dialect, location, target.pointer));
      this.targets.push(target);
      target.document.nodeOf.set(target.value, target.pointer, id);
      this.worklist.push(id);
    }
    return id;
  }

  private compileAll(): void {
    for (;;) {
      while (this.worklistHead < this.worklist.length) {
        const id = this.worklist[this.worklistHead++];
        this.compileNode(this.nodes[id], this.targets[id]);
      }
      if (this.pendingDynamicRefs.length === 0 || (!this.expandDynamicRefs() && this.worklistHead === this.worklist.length)) break;
    }
    // Most schemas have no dynamic reference: skip (and so never compile) the finalisation.
    if (this.pendingDynamicRefs.length > 0) this.finalizeDynamicRefs();
  }

  private child(parent: SchemaTarget, value: unknown, relative: string): number {
    const pointer = parent.pointer + relative;
    const resource = this.loader.resourceOf(parent.document, value, pointer) ?? parent.resource;
    return this.getNode({ document: parent.document, pointer, value, resource });
  }

  private childArray(parent: SchemaTarget, value: unknown, keyword: string): number[] | undefined {
    if (!Array.isArray(value)) return undefined;
    return value.map((v, i) => this.child(parent, v, '/' + keyword + '/' + i));
  }

  private compileNode(node: SchemaNode, target: SchemaTarget): void {
    const element = target.value;
    if (element === true) {
      node.alwaysTrue = true;
      return;
    }
    if (element === false) {
      node.alwaysFalse = true;
      return;
    }
    if (!isObject(element)) {
      node.alwaysTrue = true;
      return;
    }

    const dialect = target.resource.dialect;
    const vocab = target.resource.vocabularies;
    const legacy = dialect <= Dialect.Draft7;

    if (legacy && typeof element.$ref === 'string') {
      // In draft 7 and earlier, $ref replaces every sibling keyword.
      this.compileRef(node, target, element.$ref);
      return;
    }

    const applicator = legacy || (vocab & Vocab.Applicator) !== 0;
    const validation = legacy || (vocab & Vocab.Validation) !== 0;
    const unevaluated = dialect === Dialect.Draft201909 ? applicator : (vocab & Vocab.Unevaluated) !== 0;
    const content = legacy || (vocab & Vocab.Content) !== 0;
    const formatAssert =
      this.options.assertFormat ?? ((legacy && this.options.assertFormatInLegacyDrafts) || (vocab & Vocab.FormatAssertion) !== 0);

    const e = element;
    const kw = (name: string): string => '/' + escapePointerToken(name);
    const has = (name: string): boolean => Object.prototype.hasOwnProperty.call(e, name);

    let requiredSet: Set<string> | undefined;
    let dependencies: DependencyEntry[] | undefined;

    // References.
    if (typeof e.$ref === 'string') this.compileRef(node, target, e.$ref);
    if (dialect >= Dialect.Draft202012 && typeof e.$dynamicRef === 'string') this.compileDynamicRef(node, target, e.$dynamicRef, false);
    if (dialect === Dialect.Draft201909 && typeof e.$recursiveRef === 'string') this.compileDynamicRef(node, target, e.$recursiveRef, true);

    // Applicators.
    if (applicator) {
      if (has('allOf')) node.allOf = this.childArray(target, e.allOf, 'allOf');
      if (has('anyOf')) node.anyOf = this.childArray(target, e.anyOf, 'anyOf');
      if (has('oneOf')) node.oneOf = this.childArray(target, e.oneOf, 'oneOf');
      if (has('not')) node.not = this.child(target, e.not, kw('not'));
      if (dialect >= Dialect.Draft7 && has('if')) {
        node.if = this.child(target, e.if, kw('if'));
        if (has('then')) node.then = this.child(target, e.then, kw('then'));
        if (has('else')) node.else = this.child(target, e.else, kw('else'));
      }
      if (isObject(e.properties)) {
        node.properties = new Map();
        for (const name of Object.keys(e.properties)) {
          node.properties.set(name, this.child(target, e.properties[name], '/properties/' + escapePointerToken(name)));
        }
      }
      if (isObject(e.patternProperties)) {
        node.patternProperties = Object.keys(e.patternProperties).map((pattern) => ({
          pattern,
          node: this.child(target, (e.patternProperties as Record<string, unknown>)[pattern], '/patternProperties/' + escapePointerToken(pattern)),
        }));
      }
      if (has('additionalProperties')) node.additionalProperties = this.child(target, e.additionalProperties, kw('additionalProperties'));
      if (dialect >= Dialect.Draft6 && has('propertyNames')) node.propertyNames = this.child(target, e.propertyNames, kw('propertyNames'));
      if (dialect >= Dialect.Draft6 && has('contains')) node.contains = this.child(target, e.contains, kw('contains'));

      // "dependencies" is honoured in every dialect: in 2019-09+ it is an optional compatibility keyword.
      if (isObject(e.dependencies) || (dialect >= Dialect.Draft201909 && isObject(e.dependentSchemas))) {
        dependencies = this.compileDependencySchemas(target, e, dialect);
      }

      // Array applicators.
      if (dialect >= Dialect.Draft202012) {
        if (Array.isArray(e.prefixItems)) node.prefixItems = this.childArray(target, e.prefixItems, 'prefixItems');
        if (has('items') && !Array.isArray(e.items)) node.items = this.child(target, e.items, kw('items'));
      } else if (has('items')) {
        if (Array.isArray(e.items)) this.compileItemsArray(node, target, e);
        else node.items = this.child(target, e.items, kw('items'));
      }
      node.containsMarksEvaluated = dialect >= Dialect.Draft202012;
    }

    if (unevaluated && dialect >= Dialect.Draft201909) {
      if (has('unevaluatedProperties')) node.unevaluatedProperties = this.child(target, e.unevaluatedProperties, kw('unevaluatedProperties'));
      if (has('unevaluatedItems')) node.unevaluatedItems = this.child(target, e.unevaluatedItems, kw('unevaluatedItems'));
    }

    if (validation) {
      if (has('type')) this.compileType(node, e.type);
      if (dialect >= Dialect.Draft6 && has('const')) {
        node.hasConst = true;
        node.constValue = e.const;
      }
      if (Array.isArray(e.enum)) node.enumValues = e.enum;
      if (Array.isArray(e.required)) {
        node.requiredList = e.required.filter((x): x is string => typeof x === 'string');
        requiredSet = new Set(node.requiredList);
        node.required = [...requiredSet];
      }
      if (dialect >= Dialect.Draft201909 && isObject(e.dependentRequired)) {
        dependencies ??= [];
        for (const name of Object.keys(e.dependentRequired)) {
          const v = e.dependentRequired[name];
          if (Array.isArray(v)) dependencies.push({ keyword: 'dependentRequired', name, required: v.filter((x): x is string => typeof x === 'string') });
        }
      }
      node.minProperties = getInt(e.minProperties, -1);
      node.maxProperties = getInt(e.maxProperties, -1);
      node.minItems = getInt(e.minItems, -1);
      node.maxItems = getInt(e.maxItems, -1);
      node.uniqueItems = e.uniqueItems === true;
      node.minLength = getInt(e.minLength, -1);
      node.maxLength = getInt(e.maxLength, -1);
      if (typeof e.pattern === 'string') node.pattern = e.pattern;
      if (typeof e.multipleOf === 'number') node.multipleOf = e.multipleOf;

      if (dialect === Dialect.Draft4) {
        compileDraft4Bounds(node, e);
      } else {
        node.maximum = num(e.maximum);
        node.minimum = num(e.minimum);
        node.exclusiveMaximum = num(e.exclusiveMaximum);
        node.exclusiveMinimum = num(e.exclusiveMinimum);
      }

      if (dialect >= Dialect.Draft201909 && node.contains >= 0) {
        if (has('minContains')) node.minContains = getInt(e.minContains, 1);
        if (has('maxContains')) node.maxContains = getInt(e.maxContains, -1);
      }
    }

    if (typeof e.format === 'string') {
      node.format = e.format;
      node.formatKind = formatKind(e.format, dialect);
      node.assertFormat = formatAssert;
    }

    // Content keywords are asserted only in draft 7 (and annotations elsewhere).
    if (content && dialect >= Dialect.Draft7 && (has('contentEncoding') || has('contentMediaType'))) this.compileContent(node, e, dialect);

    if (dependencies !== undefined) {
      // In the order the three keywords appear in the schema, as SchemaCompiler.CompileNode meets them.
      const order = Object.keys(e);
      dependencies.sort((a, b) => order.indexOf(a.keyword) - order.indexOf(b.keyword));
    }
    node.dependencies = dependencies;
    this.annotationSources[node.id] = { e, dialect, vocab, legacy, content };
  }

  // The less common keyword groups, out of compileNode so that schemas without them never compile them.

  private compileDependencySchemas(target: SchemaTarget, e: Record<string, unknown>, dialect: Dialect): DependencyEntry[] {
    const dependencies: DependencyEntry[] = [];
    if (isObject(e.dependencies)) {
      for (const name of Object.keys(e.dependencies)) {
        const v = e.dependencies[name];
        if (Array.isArray(v)) dependencies.push({ keyword: 'dependencies', name, required: v.filter((x): x is string => typeof x === 'string') });
        else dependencies.push({ keyword: 'dependencies', name, schema: this.child(target, v, '/dependencies/' + escapePointerToken(name)) });
      }
    }
    if (dialect >= Dialect.Draft201909 && isObject(e.dependentSchemas)) {
      for (const name of Object.keys(e.dependentSchemas)) {
        dependencies.push({ keyword: 'dependentSchemas', name, schema: this.child(target, e.dependentSchemas[name], '/dependentSchemas/' + escapePointerToken(name)) });
      }
    }
    return dependencies;
  }

  /** Array-form items (before 2020-12): positional schemas, then additionalItems for the rest. */
  private compileItemsArray(node: SchemaNode, target: SchemaTarget, e: Record<string, unknown>): void {
    node.prefixItems = this.childArray(target, e.items, 'items');
    node.prefixKeyword = 'items';
    if (Object.prototype.hasOwnProperty.call(e, 'additionalItems')) {
      node.items = this.child(target, e.additionalItems, '/additionalItems');
      node.itemsKeyword = 'additionalItems';
    }
  }

  private compileContent(node: SchemaNode, e: Record<string, unknown>, dialect: Dialect): void {
    const base64 = e.contentEncoding === 'base64';
    const json = e.contentMediaType === 'application/json';
    node.content = base64 ? (json ? ContentKind.Base64Json : ContentKind.Base64) : json ? ContentKind.Json : ContentKind.None;
    node.assertContent = dialect === Dialect.Draft7 && this.options.assertContent && node.content !== ContentKind.None;
  }

  private compileType(node: SchemaNode, value: unknown): void {
    let mask = TypeMask.None;
    if (Array.isArray(value)) for (const t of value) mask |= typeMaskOf(t);
    else mask = typeMaskOf(value);
    // "number" accepts integers; draft 4 treats integer as a subset too.
    node.type = mask;
    node.hasType = true;
  }

  private compileRef(node: SchemaNode, target: SchemaTarget, reference: string): void {
    const resolved = this.loader.tryResolveReference(target.resource, reference);
    if (resolved === undefined) {
      throw new SchemaCompilationError(`Unable to resolve reference '${reference}' from '${target.resource.uri}'.`);
    }
    node.ref = this.getNode(resolved);
  }

  private compileDynamicRef(node: SchemaNode, target: SchemaTarget, reference: string, isRecursive: boolean): void {
    const resolved = this.loader.tryResolveReference(target.resource, reference);
    if (resolved === undefined) {
      throw new SchemaCompilationError(`Unable to resolve reference '${reference}' from '${target.resource.uri}'.`);
    }
    const fragment = decodeFragment(split(reference)[1]);
    let dynamic: boolean;
    if (isRecursive) {
      dynamic = resolved.resource.recursiveAnchor && resolved.pointer === resolved.resource.rootPointer;
    } else {
      dynamic =
        fragment.length > 0 &&
        fragment[0] !== '/' &&
        resolved.resource.dynamicAnchors !== undefined &&
        resolved.resource.dynamicAnchors.get(fragment) === resolved.pointer;
    }
    if (!dynamic) {
      // A static reference, kept apart from any sibling $ref (C# overwrites the $ref; both apply here).
      node.staticDynamicRef = this.getNode(resolved);
      node.staticDynamicKeyword = isRecursive ? '$recursiveRef' : '$dynamicRef';
      return;
    }
    this.pendingDynamicRefs.push({
      nodeId: node.id,
      anchor: fragment,
      isRecursive,
      initialTarget: resolved,
      seenResources: new Set(),
      candidates: [],
    });
  }

  private expandDynamicRefs(): boolean {
    let added = false;
    for (const pending of this.pendingDynamicRefs) {
      for (const resource of this.loader.resources) {
        if (pending.seenResources.has(resource.id)) continue;
        pending.seenResources.add(resource.id);
        let pointer: string | undefined;
        if (pending.isRecursive) {
          if (!resource.recursiveAnchor) continue;
          pointer = resource.rootPointer;
        } else {
          pointer = resource.dynamicAnchors?.get(pending.anchor);
          if (pointer === undefined) continue;
        }
        const before = this.nodes.length;
        const nodeId = this.getNode(this.targetIn(resource, pointer));
        added ||= this.nodes.length !== before;
        pending.candidates.push([resource.id, nodeId]);
      }
    }
    return added;
  }

  private targetIn(resource: SchemaResource, pointer: string): SchemaTarget {
    const t = this.loader.tryResolveFragment(resource, pointer === resource.rootPointer ? '' : pointer.slice(resource.rootPointer.length));
    return t ?? { document: resource.document, pointer, value: resource.root, resource };
  }

  private finalizeDynamicRefs(): void {
    let reachable: boolean[] | undefined;
    for (const pending of this.pendingDynamicRefs) {
      const node = this.nodes[pending.nodeId];
      const fallback = this.getNode(pending.initialTarget);
      const setStatic = (target: number): void => {
        node.staticDynamicRef = target;
        node.staticDynamicKeyword = pending.isRecursive ? '$recursiveRef' : '$dynamicRef';
      };
      if (pending.candidates.length <= 1) {
        // Only the initial target's resource defines the anchor: resolution is static.
        setStatic(fallback);
        continue;
      }

      // The dynamic scope is searched outermost-first and its outermost entry is always the resource evaluation
      // started in. When the entry resource defines the anchor, that target is the answer on every path.
      reachable ??= this.computeReachability();
      if (!reachable[pending.nodeId]) {
        setStatic(fallback);
        continue;
      }
      const entryResource = this.nodes[this.entryNode].resourceId;
      const uniform = pending.candidates.find(([r]) => r === entryResource);
      if (uniform !== undefined) {
        setStatic(uniform[1]);
        continue;
      }

      node.dynamicRef = {
        anchor: pending.anchor,
        isRecursive: pending.isRecursive,
        fallback,
        byResource: new Map(pending.candidates),
      };
    }
    while (this.worklistHead < this.worklist.length) {
      const id = this.worklist[this.worklistHead++];
      this.compileNode(this.nodes[id], this.targets[id]);
    }
  }

  /** Nodes reachable from the entry, counting every candidate of a pending dynamic reference as a child. */
  private computeReachability(): boolean[] {
    const extra = new Map<number, number[]>();
    for (const p of this.pendingDynamicRefs) {
      const list = extra.get(p.nodeId) ?? [];
      list.push(this.getNode(p.initialTarget), ...p.candidates.map(([, n]) => n));
      extra.set(p.nodeId, list);
    }
    const reached = new Array<boolean>(this.nodes.length).fill(false);
    const stack = [this.entryNode];
    reached[this.entryNode] = true;
    while (stack.length > 0) {
      const id = stack.pop()!;
      for (const c of [...this.nodes[id].children(), ...(extra.get(id) ?? [])]) {
        if (!reached[c]) {
          reached[c] = true;
          stack.push(c);
        }
      }
    }
    return reached;
  }

  // ---------------------------------------------------------------------------------------------------------------
  // Analyses

  private analyse(): void {
    // The in-place edges, built once for both analyses. Discriminators only where some node has a oneOf/anyOf: small
    // schemas often have neither in-place applicators nor branches, and then those analyses are never compiled.
    const count = this.nodes.length;
    const edges: number[][] = new Array(count);
    let inPlace = false;
    let branches = false;
    for (let i = 0; i < count; i++) {
      const n = this.nodes[i];
      edges[i] = n.inPlaceChildren(true);
      if (edges[i].length > 0) inPlace = true;
      if (n.oneOf !== undefined || n.anyOf !== undefined) branches = true;
    }
    this.computeMarking(inPlace ? edges : undefined);
    if (inPlace) this.computeInPlaceCycles(edges);
    if (branches) this.computeDiscriminators();
  }

  /**
   * Which nodes can contribute evaluated-property/item annotations (ComputeMarking): a node marks if it has the
   * keywords itself or any in-place child (not counting `not`) marks, propagated from the marking nodes to their
   * in-place parents.
   */
  private computeMarking(edges: number[][] | undefined): void {
    const nodes = this.nodes;
    for (const n of nodes) {
      n.marksProperties = n.properties !== undefined || n.patternProperties !== undefined || n.additionalProperties >= 0 || n.unevaluatedProperties >= 0;
      n.marksItems = n.prefixItems !== undefined || n.items >= 0 || (n.contains >= 0 && n.containsMarksEvaluated) || n.unevaluatedItems >= 0;
    }
    if (edges !== undefined) propagateMarks(nodes, edges);
  }

  /** Marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need a depth guard. */
  private computeInPlaceCycles(edges: number[][]): void {
    const count = this.nodes.length;
    const index = new Int32Array(count).fill(-1);
    const low = new Int32Array(count);
    const onStack = new Uint8Array(count);
    const stack: number[] = [];
    let next = 0;
    for (let start = 0; start < count; start++) {
      if (index[start] >= 0) continue;
      const work: Array<[number, number]> = [[start, 0]];
      index[start] = low[start] = next++;
      stack.push(start);
      onStack[start] = 1;
      while (work.length > 0) {
        const frame = work[work.length - 1];
        const [v, ei] = frame;
        if (ei < edges[v].length) {
          frame[1]++;
          const w = edges[v][ei];
          if (index[w] < 0) {
            index[w] = low[w] = next++;
            stack.push(w);
            onStack[w] = 1;
            work.push([w, 0]);
          } else if (onStack[w]) {
            low[v] = Math.min(low[v], index[w]);
          }
        } else {
          work.pop();
          if (work.length > 0) {
            const parent = work[work.length - 1][0];
            low[parent] = Math.min(low[parent], low[v]);
          }
          if (low[v] === index[v]) {
            const component: number[] = [];
            let w: number;
            do {
              w = stack.pop()!;
              onStack[w] = 0;
              component.push(w);
            } while (w !== v);
            if (component.length > 1 || edges[v].includes(v)) {
              for (const c of component) this.nodes[c].inPlaceCycle = true;
            }
          }
        }
      }
    }
  }

  /** Follows pure `$ref` nodes to the node that carries constraints. */
  effectiveNode(id: number): SchemaNode {
    let node = this.nodes[id];
    for (let hops = 0; hops < 16 && node.isPureRef; hops++) node = this.nodes[node.ref];
    return node;
  }

  private computeDiscriminators(): void {
    for (const n of this.nodes) {
      if (n.oneOf && n.oneOf.length > 1) n.oneOfDiscriminator = this.buildDiscriminator(n.oneOf);
      if (n.anyOf && n.anyOf.length > 1) n.anyOfDiscriminator = this.buildDiscriminator(n.anyOf);
    }
  }

  /**
   * Classifies branches by the constraint their `properties[X]` places on the value (positive: const/enum of
   * primitives; negative: string not in an enum; wildcard: anything else), and builds the value -> branches table.
   */
  private buildDiscriminator(branches: number[]): Discriminator | undefined {
    const candidates: string[] = [];
    for (const b of branches) {
      const eff = this.effectiveNode(b);
      if (eff.properties === undefined) continue;
      for (const name of eff.properties.keys()) {
        if (this.classify(eff, name).kind !== 'wildcard') candidates.push(name);
      }
      break;
    }
    for (const name of candidates) {
      const classes = branches.map((b) => this.classify(this.effectiveNode(b), name));
      if (classes.filter((c) => c.kind !== 'wildcard').length < 2) continue;
      const values: Array<string | number | boolean> = [];
      for (const c of classes) for (const v of c.set ?? []) if (!values.includes(v)) values.push(v);
      const known: Array<[string | number | boolean, number[]]> = values.map((value) => [
        value,
        classes.flatMap((c, i) => {
          const contains = c.set?.includes(value) ?? false;
          const candidate = c.kind === 'positive' ? contains : c.kind === 'negative' ? !contains : true;
          return candidate ? [i] : [];
        }),
      ]);
      const unknown = classes.flatMap((c, i) => (c.kind !== 'positive' ? [i] : []));
      const allRequire = branches.every((b) => this.effectiveNode(b).required?.includes(name) ?? false);
      return { property: name, known, unknown, allRequire };
    }
    return undefined;
  }

  private classify(branch: SchemaNode, name: string): { kind: 'positive' | 'negative' | 'wildcard'; set?: Array<string | number | boolean> } {
    const child = branch.properties?.get(name);
    if (child === undefined) return { kind: 'wildcard' };
    const p = this.effectiveNode(child);
    const primitive = (v: unknown): v is string | number | boolean => typeof v === 'string' || typeof v === 'number' || typeof v === 'boolean';
    if (p.hasConst && primitive(p.constValue)) return { kind: 'positive', set: [p.constValue] };
    if (p.enumValues !== undefined && p.enumValues.length > 0 && p.enumValues.every(primitive)) {
      return { kind: 'positive', set: p.enumValues as Array<string | number | boolean> };
    }
    if (p.not >= 0 && p.hasType && p.type === TypeMask.String) {
      const not = this.effectiveNode(p.not);
      if (
        not.enumValues !== undefined &&
        not.enumValues.every((v) => typeof v === 'string') &&
        !not.hasType &&
        !not.hasConst &&
        !not.hasStringKeywords &&
        !not.hasInPlaceApplicators
      ) {
        return { kind: 'negative', set: not.enumValues as string[] };
      }
    }
    return { kind: 'wildcard' };
  }
}
