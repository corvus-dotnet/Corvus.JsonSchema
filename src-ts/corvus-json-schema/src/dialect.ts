/** The JSON Schema dialects the evaluator understands, in specification order. */
export enum Dialect {
  Draft4 = 0,
  Draft6 = 1,
  Draft7 = 2,
  Draft201909 = 3,
  Draft202012 = 4,
}

/** The vocabularies in effect for a schema resource (2019-09 and later). */
export const enum Vocab {
  None = 0,
  Core = 1 << 0,
  Applicator = 1 << 1,
  Validation = 1 << 2,
  MetaData = 1 << 3,
  FormatAnnotation = 1 << 4,
  FormatAssertion = 1 << 5,
  Content = 1 << 6,
  Unevaluated = 1 << 7,
  AllAnnotatingFormat = Core | Applicator | Validation | MetaData | FormatAnnotation | Content | Unevaluated,
}

/** Maps a well-known metaschema URI (normalised, no fragment) to its dialect. */
export function knownDialect(uri: string): Dialect | undefined {
  switch (uri) {
    case 'http://json-schema.org/draft-04/schema':
      return Dialect.Draft4;
    case 'http://json-schema.org/draft-06/schema':
      return Dialect.Draft6;
    case 'http://json-schema.org/draft-07/schema':
      return Dialect.Draft7;
    case 'https://json-schema.org/draft/2019-09/schema':
      return Dialect.Draft201909;
    case 'https://json-schema.org/draft/2020-12/schema':
      return Dialect.Draft202012;
    default:
      return undefined;
  }
}

/** Maps a vocabulary URI to its flag. */
export function vocabularyFlag(uri: string): Vocab {
  switch (uri) {
    case 'https://json-schema.org/draft/2020-12/vocab/core':
    case 'https://json-schema.org/draft/2019-09/vocab/core':
      return Vocab.Core;
    case 'https://json-schema.org/draft/2020-12/vocab/applicator':
    case 'https://json-schema.org/draft/2019-09/vocab/applicator':
      return Vocab.Applicator;
    case 'https://json-schema.org/draft/2020-12/vocab/validation':
    case 'https://json-schema.org/draft/2019-09/vocab/validation':
      return Vocab.Validation;
    case 'https://json-schema.org/draft/2020-12/vocab/meta-data':
    case 'https://json-schema.org/draft/2019-09/vocab/meta-data':
      return Vocab.MetaData;
    case 'https://json-schema.org/draft/2020-12/vocab/format-annotation':
    case 'https://json-schema.org/draft/2019-09/vocab/format':
      return Vocab.FormatAnnotation;
    case 'https://json-schema.org/draft/2020-12/vocab/format-assertion':
      return Vocab.FormatAssertion;
    case 'https://json-schema.org/draft/2020-12/vocab/content':
    case 'https://json-schema.org/draft/2019-09/vocab/content':
      return Vocab.Content;
    case 'https://json-schema.org/draft/2020-12/vocab/unevaluated':
      return Vocab.Unevaluated;
    default:
      return Vocab.None;
  }
}

export const enum SubschemaKind {
  None,
  Single,
  SingleOrArray,
  Array,
  Map,
}

/** Which keywords hold subschemas, by dialect (mirrors SchemaKeywords.GetSubschemaKind). */
export function subschemaKind(keyword: string, dialect: Dialect, legacyRefOverridesSiblings: boolean): SubschemaKind {
  if (keyword === 'definitions' || keyword === '$defs') return SubschemaKind.Map;
  if (legacyRefOverridesSiblings) return SubschemaKind.None;
  switch (keyword) {
    case 'properties':
    case 'patternProperties':
    case 'dependencies':
      return SubschemaKind.Map;
    case 'additionalProperties':
    case 'not':
      return SubschemaKind.Single;
    case 'allOf':
    case 'anyOf':
    case 'oneOf':
      return SubschemaKind.Array;
    case 'items':
      return dialect >= Dialect.Draft202012 ? SubschemaKind.Single : SubschemaKind.SingleOrArray;
    case 'additionalItems':
      return dialect <= Dialect.Draft201909 ? SubschemaKind.Single : SubschemaKind.None;
    case 'contains':
    case 'propertyNames':
      return dialect >= Dialect.Draft6 ? SubschemaKind.Single : SubschemaKind.None;
    case 'if':
    case 'then':
    case 'else':
      return dialect >= Dialect.Draft7 ? SubschemaKind.Single : SubschemaKind.None;
    case 'unevaluatedProperties':
    case 'unevaluatedItems':
    case 'contentSchema':
      return dialect >= Dialect.Draft201909 ? SubschemaKind.Single : SubschemaKind.None;
    case 'dependentSchemas':
      return dialect >= Dialect.Draft201909 ? SubschemaKind.Map : SubschemaKind.None;
    case 'prefixItems':
      return dialect >= Dialect.Draft202012 ? SubschemaKind.Array : SubschemaKind.None;
    default:
      return SubschemaKind.None;
  }
}
