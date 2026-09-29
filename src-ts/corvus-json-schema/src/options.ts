import { Dialect } from './dialect.js';

/** Resolves a schema document by absolute URI; return the parsed JSON (or JSON text), or undefined if unknown. */
export type DocumentResolver = (uri: string) => unknown | undefined;

/** A custom format assertion. */
export type FormatValidator = (value: string) => boolean;

/** Options for compiling a schema (mirrors JsonSchemaEvaluatorOptions). */
export interface CompileOptions {
  /** The dialect for documents without `$schema`. Defaults to 2020-12. */
  defaultDialect?: Dialect;
  /**
   * Whether `format` is asserted. `undefined` (the default) follows the vocabularies: 2020-12
   * `format-assertion` asserts, everything else annotates.
   */
  assertFormat?: boolean;
  /** When `assertFormat` is undefined, assert `format` in draft 4 to 7 too. */
  assertFormatInLegacyDrafts?: boolean;
  /** Assert `contentEncoding`/`contentMediaType` in draft 7 (the only draft that asserts them). Defaults to true. */
  assertContent?: boolean;
  /** Custom format assertions, by format name; they take precedence over the built-in set. */
  formats?: Record<string, FormatValidator>;
  /** Resolves remote documents. The standard metaschemas are always available. */
  resolveDocument?: DocumentResolver;
  /** The base URI of the root document. */
  baseUri?: string;
  /** A reference (relative to the root) to evaluate from, e.g. `#/$defs/item`. Defaults to the root. */
  entryPoint?: string;
  /** Maximum depth of in-place recursion on a cycle before evaluation is abandoned. Defaults to 128. */
  maxDepth?: number;
}

export interface EvaluatorOptions {
  defaultDialect: Dialect;
  assertFormat: boolean | undefined;
  assertFormatInLegacyDrafts: boolean;
  assertContent: boolean;
  formats: Record<string, FormatValidator>;
  resolveDocument: DocumentResolver | undefined;
  baseUri: string | undefined;
  entryPoint: string | undefined;
  maxDepth: number;
}

export function normalizeOptions(o: CompileOptions | undefined): EvaluatorOptions {
  return {
    defaultDialect: o?.defaultDialect ?? Dialect.Draft202012,
    assertFormat: o?.assertFormat,
    assertFormatInLegacyDrafts: o?.assertFormatInLegacyDrafts ?? false,
    assertContent: o?.assertContent ?? true,
    formats: o?.formats ?? {},
    resolveDocument: o?.resolveDocument,
    baseUri: o?.baseUri,
    entryPoint: o?.entryPoint,
    maxDepth: o?.maxDepth ?? 128,
  };
}

/** Thrown when a schema cannot be compiled (an unresolvable reference, an invalid pattern). */
export class SchemaCompilationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'SchemaCompilationError';
  }
}

/** Thrown when evaluation recurses in place beyond `maxDepth` (a schema that loops without consuming the instance). */
export class SchemaEvaluationDepthError extends Error {
  constructor() {
    super('The schema recursed in place beyond the maximum depth.');
    this.name = 'SchemaEvaluationDepthError';
  }
}
