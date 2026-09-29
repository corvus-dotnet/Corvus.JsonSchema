// Results collection: a port of Corvus.Text.Json's JsonSchemaResultsCollector and JsonSchemaAnnotationProducer.
//
// The evaluator opens a context per subschema application and writes keyword rows into the open context. Closing a
// context either commits it (a summary row, then its own rows newest first, after its committed descendants) or
// pops it (everything it and its descendants wrote is discarded). Levels decide which rows exist and which carry
// message text: Basic has failures without text, Detailed adds text to failures, Verbose keeps every row with text,
// annotations included.

/** How much a results collector records. */
export enum ResultsLevel {
  /** Failures only, without message text (the lowest overhead). */
  Basic = 0,
  /** Failures only, with message text. */
  Detailed = 1,
  /** Every evaluation, passing and failing, with message text, including annotations. */
  Verbose = 2,
}

/** One result row. */
export interface SchemaResult {
  readonly isMatch: boolean;
  /** The message, or '' when the level records none or the keyword has none. Annotation rows carry raw JSON. */
  readonly message: string;
  /** The path of keywords from the root schema (e.g. `/properties/name/type`). */
  readonly evaluationLocation: string;
  /** The JSON pointer of the evaluated schema (or keyword) within its document (e.g. `/properties/name/type`). */
  readonly schemaEvaluationLocation: string;
  /** The JSON pointer of the instance location (e.g. `/name`). */
  readonly documentEvaluationLocation: string;
}

/** A message, or a function producing it (only called when the level records message text). */
export type Message = string | (() => string) | undefined;

interface Frame {
  readonly evalPath: string;
  readonly schemaPath: string;
  readonly docPath: string;
  readonly commitIndex: number;
  readonly rows: SchemaResult[];
}

/** Encodes a JSON pointer segment (`~` as `~0`, `/` as `~1`). */
export function encodePointerSegment(segment: string): string {
  if (segment.indexOf('~') < 0 && segment.indexOf('/') < 0) return segment;
  return segment.replace(/~/g, '~0').replace(/\//g, '~1');
}

/** Collects the results of an evaluation (JsonSchemaResultsCollector). */
export class JsonSchemaResultsCollector {
  private readonly committed: SchemaResult[] = [];
  private readonly frames: Frame[] = [];
  private evalPath = '';
  private schemaPath = '';
  private docPath = '';

  constructor(public readonly level: ResultsLevel) {}

  /** Creates a collector at the given level. */
  static create(level: ResultsLevel): JsonSchemaResultsCollector {
    return new JsonSchemaResultsCollector(level);
  }

  /** The results, in commit order. */
  get results(): readonly SchemaResult[] {
    return this.committed;
  }

  get resultCount(): number {
    return this.committed.length;
  }

  // ---------------------------------------------------------------------------------------------------------------
  // The evaluator's side (IJsonSchemaResultsCollector)

  /**
   * Opens a child context. The evaluation path is extended by `evalSegment` (verbatim), the schema path is replaced
   * by `schemaLocation`, and the document path is extended by `docSegment` (already pointer-encoded) when given.
   */
  beginChildContext(evalSegment: string | undefined, schemaLocation: string | undefined, docSegment: string | undefined): void {
    this.frames.push({
      evalPath: this.evalPath,
      schemaPath: this.schemaPath,
      docPath: this.docPath,
      commitIndex: this.committed.length,
      rows: [],
    });
    if (evalSegment !== undefined) this.evalPath += '/' + evalSegment;
    if (schemaLocation !== undefined) this.schemaPath = schemaLocation;
    if (docSegment !== undefined) this.docPath += '/' + docSegment;
  }

  /**
   * Closes a child context. When the parent does not need the child's results (`parentIsMatch`) they are discarded
   * below Verbose; otherwise the context's summary row is written and its rows are committed.
   */
  commitChildContext(parentIsMatch: boolean, childIsMatch: boolean, message: Message): void {
    if (parentIsMatch && this.level !== ResultsLevel.Verbose) {
      this.popChildContext();
      return;
    }
    const frame = this.frames[this.frames.length - 1];
    frame.rows.push(this.row(childIsMatch, message, this.evalPath, this.schemaPath, this.docPath));
    for (let i = frame.rows.length - 1; i >= 0; i--) this.committed.push(frame.rows[i]);
    this.restore(this.frames.pop()!);
  }

  /** Closes a child context and discards everything it and its descendants wrote. */
  popChildContext(): void {
    const frame = this.frames.pop()!;
    this.committed.length = frame.commitIndex;
    this.restore(frame);
  }

  evaluatedKeyword(isMatch: boolean, message: Message, keyword: string): void {
    if (!isMatch || this.level === ResultsLevel.Verbose) {
      const k = '/' + encodePointerSegment(keyword);
      this.write(this.row(isMatch, message, this.evalPath + k, this.schemaPath + k, this.docPath));
    }
  }

  evaluatedKeywordForProperty(isMatch: boolean, message: Message, propertyName: string, keyword: string): void {
    if (!isMatch || this.level === ResultsLevel.Verbose) {
      const k = '/' + encodePointerSegment(keyword);
      this.write(this.row(isMatch, message, this.evalPath + k, this.schemaPath + k, this.docPath + '/' + encodePointerSegment(propertyName)));
    }
  }

  /** An annotation: Verbose only; the keyword extends the evaluation path but not the schema path. */
  ignoredKeyword(message: Message, keyword: string): void {
    if (this.level === ResultsLevel.Verbose) {
      this.write(this.row(true, message, this.evalPath + '/' + encodePointerSegment(keyword), this.schemaPath, this.docPath));
    }
  }

  evaluatedBooleanSchema(isMatch: boolean, message: Message): void {
    if (!isMatch || this.level === ResultsLevel.Verbose) {
      this.write(this.row(isMatch, message, this.evalPath, this.schemaPath, this.docPath));
    }
  }

  private row(isMatch: boolean, message: Message, evaluationLocation: string, schemaEvaluationLocation: string, documentEvaluationLocation: string): SchemaResult {
    const withText = this.level === ResultsLevel.Verbose || (!isMatch && this.level >= ResultsLevel.Detailed);
    const text = withText && message !== undefined ? (typeof message === 'string' ? message : message()) : '';
    return { isMatch, message: text, evaluationLocation, schemaEvaluationLocation, documentEvaluationLocation };
  }

  private write(row: SchemaResult): void {
    this.frames[this.frames.length - 1].rows.push(row);
  }

  private restore(frame: Frame): void {
    this.evalPath = frame.evalPath;
    this.schemaPath = frame.schemaPath;
    this.docPath = frame.docPath;
  }
}

/** An annotation extracted from verbose results. */
export interface Annotation {
  /** The instance location (JSON pointer). */
  readonly instanceLocation: string;
  readonly keyword: string;
  /** The JSON pointer of the schema object that holds the keyword. */
  readonly schemaLocation: string;
  /** The annotation value as JSON text. */
  readonly value: string;
}

const JSON_VALUE_START = /^["{[tfn\-0-9]/;

/** The annotations in a verbose collector's results (JsonSchemaAnnotationProducer.EnumerateAnnotations). */
export function* enumerateAnnotations(collector: JsonSchemaResultsCollector): Generator<Annotation> {
  for (const r of collector.results) {
    if (!r.isMatch || r.message.length === 0) continue;
    const slash = r.evaluationLocation.lastIndexOf('/');
    if (slash < 0 || r.evaluationLocation === r.schemaEvaluationLocation) continue;
    const keyword = r.evaluationLocation.slice(slash + 1);
    if (keyword.length === 0 || !JSON_VALUE_START.test(r.message)) continue;
    yield { instanceLocation: r.documentEvaluationLocation, keyword, schemaLocation: r.schemaEvaluationLocation, value: r.message };
  }
}

const FRAGMENT_SAFE = /^[A-Za-z0-9\-._~!$&'()*+,;=:@/?]$/;

/** `#` followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8). */
export function schemaLocationFragment(schemaLocation: string): string {
  let out = '#';
  for (const byte of new TextEncoder().encode(schemaLocation)) {
    const c = String.fromCharCode(byte);
    out += byte < 128 && FRAGMENT_SAFE.test(c) ? c : '%' + byte.toString(16).toUpperCase().padStart(2, '0');
  }
  return out;
}

/**
 * Annotations grouped by instance location, then keyword, then schema location fragment, with parsed values
 * (JsonSchemaAnnotationProducer.WriteAnnotationsTo, as an object):
 * `{ "/name": { "title": { "#/properties/name": "Name" } } }`.
 */
export function collectAnnotations(collector: JsonSchemaResultsCollector): Record<string, Record<string, Record<string, unknown>>> {
  const out: Record<string, Record<string, Record<string, unknown>>> = {};
  for (const a of enumerateAnnotations(collector)) {
    const byKeyword = entry(out, a.instanceLocation, () => ({}));
    const bySchema = entry(byKeyword, a.keyword, () => ({}));
    set(bySchema, schemaLocationFragment(a.schemaLocation), JSON.parse(a.value));
  }
  return out;
}

// Own-property access that is safe for keys such as "__proto__" (instance locations and keywords are data).
function set<T>(o: Record<string, T>, key: string, value: T): void {
  Object.defineProperty(o, key, { value, writable: true, enumerable: true, configurable: true });
}

function entry<T>(o: Record<string, T>, key: string, create: () => T): T {
  if (Object.prototype.hasOwnProperty.call(o, key)) return o[key];
  const value = create();
  set(o, key, value);
  return value;
}
