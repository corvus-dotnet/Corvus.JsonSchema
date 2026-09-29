// Loads schema documents, identifies resources and anchors, and resolves references.
// A port of Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaLoader: elements are identified by
// (document, JSON pointer) rather than by parsed-document row index.

import { Dialect, knownDialect, SubschemaKind, subschemaKind, Vocab, vocabularyFlag } from './dialect.js';
import { metaschemas } from './metaschemas.js';
import { EvaluatorOptions, SchemaCompilationError } from './options.js';
import { decodeFragment, normalize, resolve, resolvePointer, split } from './uri.js';

const DEFAULT_ROOT_URI = 'https://corvus-oss.org/runtime-evaluator/root.json';

export class SchemaDocument {
  constructor(
    public readonly id: number,
    public readonly root: unknown,
    public readonly retrievalUri: string,
  ) {}
}

export class SchemaResource {
  recursiveAnchor = false;
  anchors: Map<string, string> | undefined;
  dynamicAnchors: Map<string, string> | undefined;

  constructor(
    public readonly id: number,
    public readonly document: SchemaDocument,
    public readonly rootPointer: string,
    public uri: string,
    public readonly dialect: Dialect,
    public readonly vocabularies: Vocab,
  ) {}

  get root(): unknown {
    return valueAt(this.document, this.rootPointer);
  }
}

/** The target of a reference: an element in a document, and the resource it belongs to. */
export interface SchemaTarget {
  readonly document: SchemaDocument;
  readonly pointer: string;
  readonly value: unknown;
  readonly resource: SchemaResource;
}

export function escapePointerToken(token: string): string {
  if (token.indexOf('~') < 0 && token.indexOf('/') < 0) return token;
  return token.replace(/~/g, '~0').replace(/\//g, '~1');
}

function valueAt(doc: SchemaDocument, pointer: string): unknown {
  return resolvePointer(doc.root, pointer).value;
}

function isSchemaValue(v: unknown): boolean {
  return typeof v === 'boolean' || (v !== null && typeof v === 'object' && !Array.isArray(v));
}

function isObject(v: unknown): v is Record<string, unknown> {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

export class SchemaLoader {
  readonly documents: SchemaDocument[] = [];
  readonly resources: SchemaResource[] = [];
  private readonly resourcesByUri = new Map<string, SchemaResource>();
  private readonly documentsByUri = new Map<string, SchemaDocument>();
  private readonly resourceOfElement = new Map<string, SchemaResource>();
  private readonly metaschemaInfo = new Map<string, [Dialect, Vocab]>();
  private readonly metaschemaLoading = new Set<string>();

  constructor(private readonly options: EvaluatorOptions) {}

  loadRoot(schema: unknown, baseUri: string | undefined): SchemaResource {
    const uri = baseUri === undefined ? DEFAULT_ROOT_URI : normalize(baseUri);
    const doc = this.addDocument(uri, schema);
    return this.resourceOfElement.get(key(doc, ''))!;
  }

  loadRootFromUri(uri: string): SchemaResource {
    const normalized = normalize(uri);
    if (!this.tryLoadDocument(normalized)) {
      throw new SchemaCompilationError(`Unable to resolve the schema document '${uri}'.`);
    }
    return this.resourceOfElement.get(key(this.documentsByUri.get(normalized)!, ''))!;
  }

  resourceOf(document: SchemaDocument, pointer: string): SchemaResource | undefined {
    return this.resourceOfElement.get(key(document, pointer));
  }

  tryResolveReference(from: SchemaResource, reference: string): SchemaTarget | undefined {
    const [uriPart, fragment] = split(reference);
    const absolute = resolve(from.uri, uriPart);
    let resource = this.resourcesByUri.get(absolute);
    if (resource === undefined) {
      if (!this.tryLoadDocument(absolute)) return undefined;
      resource = this.resourcesByUri.get(absolute);
      if (resource === undefined) return undefined;
    }
    return this.tryResolveFragment(resource, decodeFragment(fragment));
  }

  tryResolveFragment(resource: SchemaResource, fragment: string): SchemaTarget | undefined {
    if (fragment.length === 0) {
      return { document: resource.document, pointer: resource.rootPointer, value: resource.root, resource };
    }
    if (fragment[0] === '/') {
      const r = resolvePointer(resource.root, fragment);
      if (!r.found) return undefined;
      let pointer = resource.rootPointer;
      for (const seg of r.path) pointer += '/' + escapePointerToken(String(seg));
      const owner = this.resourceOfElement.get(key(resource.document, pointer)) ?? resource;
      return { document: resource.document, pointer, value: r.value, resource: owner };
    }
    const anchor = resource.anchors?.get(fragment);
    if (anchor !== undefined) {
      return { document: resource.document, pointer: anchor, value: valueAt(resource.document, anchor), resource };
    }
    return undefined;
  }

  getDialectInfo(schemaUri: string): [Dialect, Vocab] {
    const normalized = normalize(schemaUri);
    const known = knownDialect(normalized);
    if (known !== undefined) return [known, Vocab.AllAnnotatingFormat];
    const cached = this.metaschemaInfo.get(normalized);
    if (cached !== undefined) return cached;
    if (this.metaschemaLoading.has(normalized)) return [this.options.defaultDialect, Vocab.AllAnnotatingFormat];
    this.metaschemaLoading.add(normalized);
    try {
      let metaResource = this.resourcesByUri.get(normalized);
      if (metaResource === undefined) {
        if (!this.tryLoadDocument(normalized) || (metaResource = this.resourcesByUri.get(normalized)) === undefined) {
          const info: [Dialect, Vocab] = [this.options.defaultDialect, Vocab.AllAnnotatingFormat];
          this.metaschemaInfo.set(normalized, info);
          return info;
        }
      }
      const meta = metaResource.root;
      let vocabularies = Vocab.AllAnnotatingFormat;
      if (isObject(meta) && isObject(meta.$vocabulary)) {
        vocabularies = Vocab.None;
        for (const name of Object.keys(meta.$vocabulary)) vocabularies |= vocabularyFlag(name);
        vocabularies |= Vocab.Core;
      }
      const info: [Dialect, Vocab] = [metaResource.dialect, vocabularies];
      this.metaschemaInfo.set(normalized, info);
      return info;
    } finally {
      this.metaschemaLoading.delete(normalized);
    }
  }

  private tryLoadDocument(absoluteUri: string): boolean {
    if (this.documentsByUri.has(absoluteUri)) return true;
    const resolved = this.options.resolveDocument?.(absoluteUri);
    if (resolved !== undefined) {
      this.addDocument(absoluteUri, typeof resolved === 'string' ? JSON.parse(resolved) : resolved);
      return true;
    }
    const meta = metaschemas.get(absoluteUri);
    if (meta !== undefined) {
      this.addDocument(absoluteUri, JSON.parse(meta));
      return true;
    }
    return false;
  }

  private addDocument(uri: string, root: unknown): SchemaDocument {
    const doc = new SchemaDocument(this.documents.length, root, uri);
    this.documents.push(doc);
    this.documentsByUri.set(uri, doc);
    const [dialect, vocabularies] = this.getRootDialect(root);
    const resource = this.createResource(doc, '', uri, dialect, vocabularies);
    this.walk(doc, root, '', resource, true);
    return doc;
  }

  private getRootDialect(root: unknown): [Dialect, Vocab] {
    if (isObject(root) && typeof root.$schema === 'string') return this.getDialectInfo(root.$schema);
    return [this.options.defaultDialect, Vocab.AllAnnotatingFormat];
  }

  private createResource(doc: SchemaDocument, pointer: string, uri: string, dialect: Dialect, vocabularies: Vocab): SchemaResource {
    const resource = new SchemaResource(this.resources.length, doc, pointer, uri, dialect, vocabularies);
    this.resources.push(resource);
    if (!this.resourcesByUri.has(uri)) this.resourcesByUri.set(uri, resource);
    return resource;
  }

  private walk(doc: SchemaDocument, element: unknown, pointer: string, resource: SchemaResource, isResourceRoot: boolean): void {
    if (!isObject(element)) {
      this.resourceOfElement.set(key(doc, pointer), resource);
      return;
    }

    let dialect = resource.dialect;
    let vocabularies = resource.vocabularies;
    if (!isResourceRoot && typeof element.$schema === 'string') {
      [dialect, vocabularies] = this.getDialectInfo(element.$schema);
    }

    const legacyRefOverridesSiblings = dialect <= Dialect.Draft7 && typeof element.$ref === 'string';
    if (!legacyRefOverridesSiblings) {
      const idValue = dialect === Dialect.Draft4 ? element.id : element.$id;
      if (typeof idValue === 'string') {
        const [uriPart, fragment] = split(idValue);
        if (uriPart.length === 0) {
          if (fragment.length > 0 && dialect <= Dialect.Draft7) addAnchor(resource, fragment, pointer);
        } else {
          const absolute = resolve(resource.uri, uriPart);
          if (!isResourceRoot || absolute !== resource.uri) {
            if (isResourceRoot) {
              if (!this.resourcesByUri.has(absolute)) this.resourcesByUri.set(absolute, resource);
              resource.uri = absolute;
            } else {
              resource = this.createResource(doc, pointer, absolute, dialect, vocabularies);
              isResourceRoot = true;
            }
          }
          if (fragment.length > 0 && dialect <= Dialect.Draft7) addAnchor(resource, fragment, pointer);
        }
      }
      if (dialect >= Dialect.Draft201909 && typeof element.$anchor === 'string') addAnchor(resource, element.$anchor, pointer);
      if (dialect >= Dialect.Draft202012 && typeof element.$dynamicAnchor === 'string') {
        const name = element.$dynamicAnchor;
        resource.dynamicAnchors ??= new Map();
        if (!resource.dynamicAnchors.has(name)) resource.dynamicAnchors.set(name, pointer);
        addAnchor(resource, name, pointer);
      }
      if (dialect === Dialect.Draft201909 && isResourceRoot && element.$recursiveAnchor === true) resource.recursiveAnchor = true;
    }

    this.resourceOfElement.set(key(doc, pointer), resource);

    for (const name of Object.keys(element)) {
      const value = element[name];
      const base = pointer + '/' + escapePointerToken(name);
      switch (subschemaKind(name, dialect, legacyRefOverridesSiblings)) {
        case SubschemaKind.Single:
          if (isSchemaValue(value)) this.walk(doc, value, base, resource, false);
          break;
        case SubschemaKind.SingleOrArray:
          if (Array.isArray(value)) value.forEach((v, i) => this.walk(doc, v, base + '/' + i, resource, false));
          else if (isSchemaValue(value)) this.walk(doc, value, base, resource, false);
          break;
        case SubschemaKind.Array:
          if (Array.isArray(value)) value.forEach((v, i) => this.walk(doc, v, base + '/' + i, resource, false));
          break;
        case SubschemaKind.Map:
          if (isObject(value)) {
            for (const entry of Object.keys(value)) {
              const v = value[entry];
              if (isSchemaValue(v)) this.walk(doc, v, base + '/' + escapePointerToken(entry), resource, false);
            }
          }
          break;
        default:
          break;
      }
    }
  }
}

function addAnchor(resource: SchemaResource, name: string, pointer: string): void {
  resource.anchors ??= new Map();
  if (!resource.anchors.has(name)) resource.anchors.set(name, pointer);
}

function key(doc: SchemaDocument, pointer: string): string {
  return doc.id + '#' + pointer;
}
