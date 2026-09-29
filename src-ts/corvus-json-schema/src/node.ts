// The compiled schema graph: one SchemaNode per distinct (document, pointer), with pre-digested keyword data.
// A port of Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaNode, trimmed to what flag-mode code generation needs.

import { Dialect } from './dialect.js';
import { isNumericFormat } from './formats.js';

/** JSON types as bits. */
export const enum TypeMask {
  None = 0,
  Null = 1,
  Boolean = 2,
  Object = 4,
  Array = 8,
  Number = 16,
  String = 32,
  Integer = 64,
  All = 127,
}

export const enum ContentKind {
  None,
  Base64,
  Json,
  Base64Json,
}

/** An annotation-producing keyword and its value (reported in verbose results). */
export interface AnnotationEntry {
  readonly keyword: string;
  readonly value: unknown;
  /** Reported only when the instance is a string (content keywords). */
  readonly stringsOnly: boolean;
}

export interface PatternProperty {
  readonly pattern: string;
  readonly node: number;
}

export interface DependencyEntry {
  /** The keyword the entry came from (C# reports 2019-09+ `dependencies` under the newer names). */
  readonly keyword: 'dependencies' | 'dependentSchemas' | 'dependentRequired';
  readonly name: string;
  readonly required?: string[];
  readonly schema?: number;
}

/** A `$dynamicRef`/`$recursiveRef` that stays dynamic after compile-time analysis. */
export interface DynamicRefTarget {
  readonly anchor: string;
  readonly isRecursive: boolean;
  readonly fallback: number;
  /** resource id -> node id of that resource's matching anchor (only resources that define it). */
  readonly byResource: Map<number, number>;
}

/** Selects oneOf/anyOf branches by the value of one property (see SchemaCompiler.BuildDiscriminator). */
export interface Discriminator {
  readonly property: string;
  /** Known discriminator values (strings, numbers, booleans) and the branches each can select. */
  readonly known: Array<[value: string | number | boolean, branches: number[]]>;
  /** Branches that stay candidates for any value not in `known` (negative and wildcard branches). */
  readonly unknown: number[];
  /** Every branch requires the property, so its absence fails the keyword at once. */
  readonly allRequire: boolean;
}

export class SchemaNode {
  alwaysTrue = false;
  alwaysFalse = false;

  // Assertions.
  type = TypeMask.None;
  hasType = false;
  hasConst = false;
  constValue: unknown = undefined;
  enumValues: unknown[] | undefined;

  // References.
  ref = -1;
  /** A `$dynamicRef`/`$recursiveRef` that compile-time analysis resolved statically, and its keyword. */
  staticDynamicRef = -1;
  staticDynamicKeyword: '$dynamicRef' | '$recursiveRef' | undefined;
  dynamicRef: DynamicRefTarget | undefined;

  // In-place applicators.
  allOf: number[] | undefined;
  anyOf: number[] | undefined;
  oneOf: number[] | undefined;
  not = -1;
  if = -1;
  then = -1;
  else = -1;

  // Objects.
  properties: Map<string, number> | undefined;
  patternProperties: PatternProperty[] | undefined;
  additionalProperties = -1;
  propertyNames = -1;
  required: string[] | undefined;
  /** `required` as written (duplicates kept), for results. */
  requiredList: string[] | undefined;
  dependencies: DependencyEntry[] | undefined;
  minProperties = -1;
  maxProperties = -1;
  unevaluatedProperties = -1;

  // Arrays.
  prefixItems: number[] | undefined;
  /** The keywords behind prefixItems/items: `prefixItems`/`items` (2020-12) or `items`/`additionalItems` (legacy). */
  prefixKeyword: 'prefixItems' | 'items' = 'prefixItems';
  itemsKeyword: 'items' | 'additionalItems' = 'items';
  items = -1;
  contains = -1;
  minContains = 1;
  maxContains = -1;
  containsMarksEvaluated = false;
  minItems = -1;
  maxItems = -1;
  uniqueItems = false;
  unevaluatedItems = -1;

  // Strings.
  minLength = -1;
  maxLength = -1;
  pattern: string | undefined;
  format: string | undefined;
  /** The format this dialect recognises (see formatKind), or 'unknown'. */
  formatKind = 'unknown';
  assertFormat = false;
  content = ContentKind.None;
  assertContent = false;

  // Numbers.
  minimum: number | undefined;
  maximum: number | undefined;
  exclusiveMinimum: number | undefined;
  exclusiveMaximum: number | undefined;
  multipleOf: number | undefined;

  // Annotations, in schema order (SchemaCompiler.CompileNode).
  annotations: AnnotationEntry[] | undefined;

  // Analysis.
  marksProperties = false;
  marksItems = false;
  inPlaceCycle = false;
  oneOfDiscriminator: Discriminator | undefined;
  anyOfDiscriminator: Discriminator | undefined;

  constructor(
    public readonly id: number,
    public readonly resourceId: number,
    public readonly dialect: Dialect,
    public readonly location: string,
    /** The JSON pointer of the schema within its document (C#'s SchemaLocation). */
    public readonly pointer: string,
  ) {}

  /** Keywords that apply only to objects. */
  get hasObjectKeywords(): boolean {
    return (
      this.properties !== undefined ||
      this.patternProperties !== undefined ||
      this.additionalProperties >= 0 ||
      this.propertyNames >= 0 ||
      this.required !== undefined ||
      this.dependencies !== undefined ||
      this.minProperties >= 0 ||
      this.maxProperties >= 0 ||
      this.unevaluatedProperties >= 0
    );
  }

  get hasArrayKeywords(): boolean {
    return (
      this.prefixItems !== undefined ||
      this.items >= 0 ||
      this.contains >= 0 ||
      this.minItems >= 0 ||
      this.maxItems >= 0 ||
      this.uniqueItems ||
      this.unevaluatedItems >= 0
    );
  }

  get hasStringKeywords(): boolean {
    return this.minLength >= 0 || this.maxLength >= 0 || this.pattern !== undefined || (this.assertFormat && this.format !== undefined && !isNumericFormat(this.formatKind)) || this.assertContent;
  }

  get hasNumberKeywords(): boolean {
    return (
      this.minimum !== undefined ||
      this.maximum !== undefined ||
      this.exclusiveMinimum !== undefined ||
      this.exclusiveMaximum !== undefined ||
      this.multipleOf !== undefined ||
      (this.assertFormat && isNumericFormat(this.formatKind))
    );
  }

  get hasInPlaceApplicators(): boolean {
    return (
      this.ref >= 0 ||
      this.staticDynamicRef >= 0 ||
      this.dynamicRef !== undefined ||
      this.allOf !== undefined ||
      this.anyOf !== undefined ||
      this.oneOf !== undefined ||
      this.not >= 0 ||
      this.if >= 0 ||
      (this.dependencies?.some((d) => d.schema !== undefined) ?? false)
    );
  }

  /** Only `type` (a token-type test at the call site). */
  get isTypeOnly(): boolean {
    return (
      this.hasType &&
      !this.hasConst &&
      this.enumValues === undefined &&
      !this.hasObjectKeywords &&
      !this.hasArrayKeywords &&
      !this.hasStringKeywords &&
      !this.hasNumberKeywords &&
      !this.hasInPlaceApplicators
    );
  }

  /** Nothing but `$ref` (plus, possibly, nothing else that asserts). */
  get isPureRef(): boolean {
    return (
      this.ref >= 0 &&
      !this.hasType &&
      !this.hasConst &&
      this.enumValues === undefined &&
      !this.hasObjectKeywords &&
      !this.hasArrayKeywords &&
      !this.hasStringKeywords &&
      !this.hasNumberKeywords &&
      this.dynamicRef === undefined &&
      this.staticDynamicRef < 0 &&
      this.allOf === undefined &&
      this.anyOf === undefined &&
      this.oneOf === undefined &&
      this.not < 0 &&
      this.if < 0 &&
      this.dependencies === undefined
    );
  }

  /** In-place children (the instance is evaluated at the same location). */
  inPlaceChildren(includeNot: boolean): number[] {
    const out: number[] = [];
    if (this.ref >= 0) out.push(this.ref);
    if (this.staticDynamicRef >= 0) out.push(this.staticDynamicRef);
    if (this.dynamicRef !== undefined) {
      out.push(this.dynamicRef.fallback);
      for (const n of this.dynamicRef.byResource.values()) out.push(n);
    }
    if (this.allOf) out.push(...this.allOf);
    if (this.anyOf) out.push(...this.anyOf);
    if (this.oneOf) out.push(...this.oneOf);
    if (includeNot && this.not >= 0) out.push(this.not);
    if (this.if >= 0) out.push(this.if);
    if (this.then >= 0) out.push(this.then);
    if (this.else >= 0) out.push(this.else);
    if (this.dependencies) for (const d of this.dependencies) if (d.schema !== undefined) out.push(d.schema);
    return out;
  }

  /** Every child node. */
  children(): number[] {
    const out = this.inPlaceChildren(true);
    if (this.properties) out.push(...this.properties.values());
    if (this.patternProperties) for (const p of this.patternProperties) out.push(p.node);
    for (const c of [this.additionalProperties, this.propertyNames, this.unevaluatedProperties, this.items, this.contains, this.unevaluatedItems]) {
      if (c >= 0) out.push(c);
    }
    if (this.prefixItems) out.push(...this.prefixItems);
    return out;
  }
}
