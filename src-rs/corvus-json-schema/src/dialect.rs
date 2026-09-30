//! Dialects, vocabularies and which keywords hold subschemas (a port of `SchemaKeywords`).

/// The JSON Schema dialects the evaluator understands, in specification order.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub enum Dialect {
    Draft4,
    Draft6,
    Draft7,
    Draft201909,
    Draft202012,
}

impl Dialect {
    /// Draft 7 and earlier, where `$ref` replaces its siblings and there are no vocabularies.
    pub(crate) fn is_legacy(self) -> bool {
        self <= Dialect::Draft7
    }
}

/// The vocabularies in effect for a schema resource (2019-09 and later), as bits.
pub(crate) mod vocab {
    pub const NONE: u32 = 0;
    pub const CORE: u32 = 1 << 0;
    pub const APPLICATOR: u32 = 1 << 1;
    pub const VALIDATION: u32 = 1 << 2;
    pub const META_DATA: u32 = 1 << 3;
    pub const FORMAT_ANNOTATION: u32 = 1 << 4;
    pub const FORMAT_ASSERTION: u32 = 1 << 5;
    pub const CONTENT: u32 = 1 << 6;
    pub const UNEVALUATED: u32 = 1 << 7;
    pub const ALL_ANNOTATING_FORMAT: u32 =
        CORE | APPLICATOR | VALIDATION | META_DATA | FORMAT_ANNOTATION | CONTENT | UNEVALUATED;
}

/// Maps a well-known metaschema URI (normalised, no fragment) to its dialect.
pub(crate) fn known_dialect(uri: &str) -> Option<Dialect> {
    match uri {
        "http://json-schema.org/draft-04/schema" => Some(Dialect::Draft4),
        "http://json-schema.org/draft-06/schema" => Some(Dialect::Draft6),
        "http://json-schema.org/draft-07/schema" => Some(Dialect::Draft7),
        "https://json-schema.org/draft/2019-09/schema" => Some(Dialect::Draft201909),
        "https://json-schema.org/draft/2020-12/schema" => Some(Dialect::Draft202012),
        _ => None,
    }
}

/// Maps a vocabulary URI to its bit.
pub(crate) fn vocabulary_flag(uri: &str) -> u32 {
    match uri {
        "https://json-schema.org/draft/2020-12/vocab/core" | "https://json-schema.org/draft/2019-09/vocab/core" => {
            vocab::CORE
        }
        "https://json-schema.org/draft/2020-12/vocab/applicator"
        | "https://json-schema.org/draft/2019-09/vocab/applicator" => vocab::APPLICATOR,
        "https://json-schema.org/draft/2020-12/vocab/validation"
        | "https://json-schema.org/draft/2019-09/vocab/validation" => vocab::VALIDATION,
        "https://json-schema.org/draft/2020-12/vocab/meta-data"
        | "https://json-schema.org/draft/2019-09/vocab/meta-data" => vocab::META_DATA,
        "https://json-schema.org/draft/2020-12/vocab/format-annotation"
        | "https://json-schema.org/draft/2019-09/vocab/format" => vocab::FORMAT_ANNOTATION,
        "https://json-schema.org/draft/2020-12/vocab/format-assertion" => vocab::FORMAT_ASSERTION,
        "https://json-schema.org/draft/2020-12/vocab/content"
        | "https://json-schema.org/draft/2019-09/vocab/content" => vocab::CONTENT,
        "https://json-schema.org/draft/2020-12/vocab/unevaluated" => vocab::UNEVALUATED,
        _ => vocab::NONE,
    }
}

/// How a keyword holds subschemas.
#[derive(Clone, Copy, PartialEq, Eq)]
pub(crate) enum SubschemaKind {
    None,
    Single,
    SingleOrArray,
    Array,
    Map,
}

/// Which keywords hold subschemas, by dialect (`SchemaKeywords.GetSubschemaKind`).
pub(crate) fn subschema_kind(keyword: &str, dialect: Dialect, legacy_ref_overrides_siblings: bool) -> SubschemaKind {
    if keyword == "definitions" || keyword == "$defs" {
        return SubschemaKind::Map;
    }
    if legacy_ref_overrides_siblings {
        return SubschemaKind::None;
    }
    match keyword {
        "properties" | "patternProperties" | "dependencies" => SubschemaKind::Map,
        "additionalProperties" | "not" => SubschemaKind::Single,
        "allOf" | "anyOf" | "oneOf" => SubschemaKind::Array,
        "items" => {
            if dialect >= Dialect::Draft202012 {
                SubschemaKind::Single
            } else {
                SubschemaKind::SingleOrArray
            }
        }
        "additionalItems" if dialect <= Dialect::Draft201909 => SubschemaKind::Single,
        "contains" | "propertyNames" if dialect >= Dialect::Draft6 => SubschemaKind::Single,
        "if" | "then" | "else" if dialect >= Dialect::Draft7 => SubschemaKind::Single,
        "unevaluatedProperties" | "unevaluatedItems" | "contentSchema" if dialect >= Dialect::Draft201909 => {
            SubschemaKind::Single
        }
        "dependentSchemas" if dialect >= Dialect::Draft201909 => SubschemaKind::Map,
        "prefixItems" if dialect >= Dialect::Draft202012 => SubschemaKind::Array,
        _ => SubschemaKind::None,
    }
}
