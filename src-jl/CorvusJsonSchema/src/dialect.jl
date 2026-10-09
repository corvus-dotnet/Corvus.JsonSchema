# Dialects, vocabularies and which keywords hold subschemas. Ported from dialect.go.

"""
    Dialect

A JSON Schema dialect the evaluator understands. The values are in specification order: `Draft4`, `Draft6`,
`Draft7`, `Draft201909` and `Draft202012`.
"""
@enum Dialect::Int8 begin
    Draft4 = 0
    Draft6 = 1
    Draft7 = 2
    Draft201909 = 3
    Draft202012 = 4
end

@doc "JSON Schema draft 4. See [`Dialect`](@ref)." Draft4
@doc "JSON Schema draft 6. See [`Dialect`](@ref)." Draft6
@doc "JSON Schema draft 7. See [`Dialect`](@ref)." Draft7
@doc "JSON Schema draft 2019-09. See [`Dialect`](@ref)." Draft201909
@doc "JSON Schema draft 2020-12. See [`Dialect`](@ref)." Draft202012

# The dialect's usual name.
function dialect_name(d::Dialect)
    d == Draft4 && return "draft4"
    d == Draft6 && return "draft6"
    d == Draft7 && return "draft7"
    d == Draft201909 && return "draft2019-09"
    return "draft2020-12"
end

# Draft 7 and earlier, where $ref replaces its siblings and there are no vocabularies.
is_legacy(d::Dialect) = d <= Draft7

# The vocabularies in effect for a schema resource (2019-09 and later), as bits.
const VOCAB_NONE = UInt32(0)
const VOCAB_CORE = UInt32(1) << 0
const VOCAB_APPLICATOR = UInt32(1) << 1
const VOCAB_VALIDATION = UInt32(1) << 2
const VOCAB_META_DATA = UInt32(1) << 3
const VOCAB_FORMAT_ANNOTATION = UInt32(1) << 4
const VOCAB_FORMAT_ASSERTION = UInt32(1) << 5
const VOCAB_CONTENT = UInt32(1) << 6
const VOCAB_UNEVALUATED = UInt32(1) << 7
const VOCAB_ALL_ANNOTATING = VOCAB_CORE | VOCAB_APPLICATOR | VOCAB_VALIDATION | VOCAB_META_DATA |
                             VOCAB_FORMAT_ANNOTATION | VOCAB_CONTENT | VOCAB_UNEVALUATED

# Maps a well-known metaschema URI (normalised, no fragment) to its dialect, or nothing.
function known_dialect(uri::String)
    uri == "http://json-schema.org/draft-04/schema" && return Draft4
    uri == "http://json-schema.org/draft-06/schema" && return Draft6
    uri == "http://json-schema.org/draft-07/schema" && return Draft7
    uri == "https://json-schema.org/draft/2019-09/schema" && return Draft201909
    uri == "https://json-schema.org/draft/2020-12/schema" && return Draft202012
    return nothing
end

# Maps a vocabulary URI to its bit.
function vocabulary_flag(uri::String)
    for prefix in ("https://json-schema.org/draft/2020-12/vocab/", "https://json-schema.org/draft/2019-09/vocab/")
        startswith(uri, prefix) || continue
        name = uri[ncodeunits(prefix)+1:end]
        new = prefix == "https://json-schema.org/draft/2020-12/vocab/"
        name == "core" && return VOCAB_CORE
        name == "applicator" && return VOCAB_APPLICATOR
        name == "validation" && return VOCAB_VALIDATION
        name == "meta-data" && return VOCAB_META_DATA
        name == "content" && return VOCAB_CONTENT
        if new
            name == "format-annotation" && return VOCAB_FORMAT_ANNOTATION
            name == "format-assertion" && return VOCAB_FORMAT_ASSERTION
            name == "unevaluated" && return VOCAB_UNEVALUATED
        else
            name == "format" && return VOCAB_FORMAT_ANNOTATION
        end
    end
    return VOCAB_NONE
end

# How a keyword holds subschemas.
const SUBSCHEMA_NONE = 0
const SUBSCHEMA_SINGLE = 1
const SUBSCHEMA_SINGLE_OR_ARRAY = 2
const SUBSCHEMA_ARRAY = 3
const SUBSCHEMA_MAP = 4

# Says which keywords hold subschemas, by dialect.
function subschema_kind_of(keyword::String, dialect::Dialect, legacy_ref_overrides_siblings::Bool)
    (keyword == "definitions" || keyword == "\$defs") && return SUBSCHEMA_MAP
    legacy_ref_overrides_siblings && return SUBSCHEMA_NONE
    if keyword == "properties" || keyword == "patternProperties" || keyword == "dependencies"
        return SUBSCHEMA_MAP
    elseif keyword == "additionalProperties" || keyword == "not"
        return SUBSCHEMA_SINGLE
    elseif keyword == "allOf" || keyword == "anyOf" || keyword == "oneOf"
        return SUBSCHEMA_ARRAY
    elseif keyword == "items"
        return dialect >= Draft202012 ? SUBSCHEMA_SINGLE : SUBSCHEMA_SINGLE_OR_ARRAY
    elseif keyword == "additionalItems"
        dialect <= Draft201909 && return SUBSCHEMA_SINGLE
    elseif keyword == "contains" || keyword == "propertyNames"
        dialect >= Draft6 && return SUBSCHEMA_SINGLE
    elseif keyword == "if" || keyword == "then" || keyword == "else"
        dialect >= Draft7 && return SUBSCHEMA_SINGLE
    elseif keyword == "unevaluatedProperties" || keyword == "unevaluatedItems" || keyword == "contentSchema"
        dialect >= Draft201909 && return SUBSCHEMA_SINGLE
    elseif keyword == "dependentSchemas"
        dialect >= Draft201909 && return SUBSCHEMA_MAP
    elseif keyword == "prefixItems"
        dialect >= Draft202012 && return SUBSCHEMA_ARRAY
    end
    return SUBSCHEMA_NONE
end
