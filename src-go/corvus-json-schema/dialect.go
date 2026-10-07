package jsonschema

// Dialect is a JSON Schema dialect the evaluator understands. The values are in specification order.
type Dialect int

// The dialects.
const (
	Draft4 Dialect = iota
	Draft6
	Draft7
	Draft201909
	Draft202012
)

// String returns the dialect's usual name.
func (d Dialect) String() string {
	switch d {
	case Draft4:
		return "draft4"
	case Draft6:
		return "draft6"
	case Draft7:
		return "draft7"
	case Draft201909:
		return "draft2019-09"
	case Draft202012:
		return "draft2020-12"
	}
	return "unknown"
}

// isLegacy reports draft 7 and earlier, where $ref replaces its siblings and there are no vocabularies.
func (d Dialect) isLegacy() bool {
	return d <= Draft7
}

// The vocabularies in effect for a schema resource (2019-09 and later), as bits.
const (
	vocabNone             uint32 = 0
	vocabCore             uint32 = 1 << 0
	vocabApplicator       uint32 = 1 << 1
	vocabValidation       uint32 = 1 << 2
	vocabMetaData         uint32 = 1 << 3
	vocabFormatAnnotation uint32 = 1 << 4
	vocabFormatAssertion  uint32 = 1 << 5
	vocabContent          uint32 = 1 << 6
	vocabUnevaluated      uint32 = 1 << 7
	vocabAllAnnotating           = vocabCore | vocabApplicator | vocabValidation | vocabMetaData |
		vocabFormatAnnotation | vocabContent | vocabUnevaluated
)

// knownDialect maps a well-known metaschema URI (normalised, no fragment) to its dialect.
func knownDialect(uri string) (Dialect, bool) {
	switch uri {
	case "http://json-schema.org/draft-04/schema":
		return Draft4, true
	case "http://json-schema.org/draft-06/schema":
		return Draft6, true
	case "http://json-schema.org/draft-07/schema":
		return Draft7, true
	case "https://json-schema.org/draft/2019-09/schema":
		return Draft201909, true
	case "https://json-schema.org/draft/2020-12/schema":
		return Draft202012, true
	}
	return 0, false
}

// vocabularyFlag maps a vocabulary URI to its bit.
func vocabularyFlag(uri string) uint32 {
	switch uri {
	case "https://json-schema.org/draft/2020-12/vocab/core", "https://json-schema.org/draft/2019-09/vocab/core":
		return vocabCore
	case "https://json-schema.org/draft/2020-12/vocab/applicator",
		"https://json-schema.org/draft/2019-09/vocab/applicator":
		return vocabApplicator
	case "https://json-schema.org/draft/2020-12/vocab/validation",
		"https://json-schema.org/draft/2019-09/vocab/validation":
		return vocabValidation
	case "https://json-schema.org/draft/2020-12/vocab/meta-data",
		"https://json-schema.org/draft/2019-09/vocab/meta-data":
		return vocabMetaData
	case "https://json-schema.org/draft/2020-12/vocab/format-annotation",
		"https://json-schema.org/draft/2019-09/vocab/format":
		return vocabFormatAnnotation
	case "https://json-schema.org/draft/2020-12/vocab/format-assertion":
		return vocabFormatAssertion
	case "https://json-schema.org/draft/2020-12/vocab/content", "https://json-schema.org/draft/2019-09/vocab/content":
		return vocabContent
	case "https://json-schema.org/draft/2020-12/vocab/unevaluated":
		return vocabUnevaluated
	}
	return vocabNone
}

// How a keyword holds subschemas.
type subschemaKind int

const (
	subschemaNone subschemaKind = iota
	subschemaSingle
	subschemaSingleOrArray
	subschemaArray
	subschemaMap
)

// subschemaKindOf says which keywords hold subschemas, by dialect.
func subschemaKindOf(keyword string, dialect Dialect, legacyRefOverridesSiblings bool) subschemaKind {
	if keyword == "definitions" || keyword == "$defs" {
		return subschemaMap
	}
	if legacyRefOverridesSiblings {
		return subschemaNone
	}
	switch keyword {
	case "properties", "patternProperties", "dependencies":
		return subschemaMap
	case "additionalProperties", "not":
		return subschemaSingle
	case "allOf", "anyOf", "oneOf":
		return subschemaArray
	case "items":
		if dialect >= Draft202012 {
			return subschemaSingle
		}
		return subschemaSingleOrArray
	case "additionalItems":
		if dialect <= Draft201909 {
			return subschemaSingle
		}
	case "contains", "propertyNames":
		if dialect >= Draft6 {
			return subschemaSingle
		}
	case "if", "then", "else":
		if dialect >= Draft7 {
			return subschemaSingle
		}
	case "unevaluatedProperties", "unevaluatedItems", "contentSchema":
		if dialect >= Draft201909 {
			return subschemaSingle
		}
	case "dependentSchemas":
		if dialect >= Draft201909 {
			return subschemaMap
		}
	case "prefixItems":
		if dialect >= Draft202012 {
			return subschemaArray
		}
	}
	return subschemaNone
}
