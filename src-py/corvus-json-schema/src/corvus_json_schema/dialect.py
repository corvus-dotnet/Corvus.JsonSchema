"""JSON Schema dialects, vocabularies and the keywords that hold subschemas."""

from __future__ import annotations

from enum import IntEnum, IntFlag


class Dialect(IntEnum):
    """The JSON Schema dialects the evaluator understands, in specification order."""

    DRAFT4 = 0
    DRAFT6 = 1
    DRAFT7 = 2
    DRAFT201909 = 3
    DRAFT202012 = 4


class Vocab(IntFlag):
    """The vocabularies in effect for a schema resource (2019-09 and later)."""

    NONE = 0
    CORE = 1 << 0
    APPLICATOR = 1 << 1
    VALIDATION = 1 << 2
    META_DATA = 1 << 3
    FORMAT_ANNOTATION = 1 << 4
    FORMAT_ASSERTION = 1 << 5
    CONTENT = 1 << 6
    UNEVALUATED = 1 << 7
    ALL_ANNOTATING_FORMAT = CORE | APPLICATOR | VALIDATION | META_DATA | FORMAT_ANNOTATION | CONTENT | UNEVALUATED


_KNOWN_DIALECTS = {
    "http://json-schema.org/draft-04/schema": Dialect.DRAFT4,
    "http://json-schema.org/draft-06/schema": Dialect.DRAFT6,
    "http://json-schema.org/draft-07/schema": Dialect.DRAFT7,
    "https://json-schema.org/draft/2019-09/schema": Dialect.DRAFT201909,
    "https://json-schema.org/draft/2020-12/schema": Dialect.DRAFT202012,
}


def known_dialect(uri: str) -> Dialect | None:
    """Maps a well-known metaschema URI (normalised, no fragment) to its dialect."""
    return _KNOWN_DIALECTS.get(uri)


_VOCABULARIES = {
    "https://json-schema.org/draft/2020-12/vocab/core": Vocab.CORE,
    "https://json-schema.org/draft/2019-09/vocab/core": Vocab.CORE,
    "https://json-schema.org/draft/2020-12/vocab/applicator": Vocab.APPLICATOR,
    "https://json-schema.org/draft/2019-09/vocab/applicator": Vocab.APPLICATOR,
    "https://json-schema.org/draft/2020-12/vocab/validation": Vocab.VALIDATION,
    "https://json-schema.org/draft/2019-09/vocab/validation": Vocab.VALIDATION,
    "https://json-schema.org/draft/2020-12/vocab/meta-data": Vocab.META_DATA,
    "https://json-schema.org/draft/2019-09/vocab/meta-data": Vocab.META_DATA,
    "https://json-schema.org/draft/2020-12/vocab/format-annotation": Vocab.FORMAT_ANNOTATION,
    "https://json-schema.org/draft/2019-09/vocab/format": Vocab.FORMAT_ANNOTATION,
    "https://json-schema.org/draft/2020-12/vocab/format-assertion": Vocab.FORMAT_ASSERTION,
    "https://json-schema.org/draft/2020-12/vocab/content": Vocab.CONTENT,
    "https://json-schema.org/draft/2019-09/vocab/content": Vocab.CONTENT,
    "https://json-schema.org/draft/2020-12/vocab/unevaluated": Vocab.UNEVALUATED,
}


def vocabulary_flag(uri: str) -> Vocab:
    """Maps a vocabulary URI to its flag."""
    return _VOCABULARIES.get(uri, Vocab.NONE)


# Which keywords hold subschemas (mirrors SchemaKeywords.GetSubschemaKind).
NONE = 0
SINGLE = 1
SINGLE_OR_ARRAY = 2
ARRAY = 3
MAP = 4


def subschema_kind(keyword: str, dialect: Dialect, legacy_ref_overrides_siblings: bool) -> int:
    """Which keywords hold subschemas, by dialect."""
    if keyword in ("definitions", "$defs"):
        return MAP
    if legacy_ref_overrides_siblings:
        return NONE
    if keyword in ("properties", "patternProperties", "dependencies"):
        return MAP
    if keyword in ("additionalProperties", "not"):
        return SINGLE
    if keyword in ("allOf", "anyOf", "oneOf"):
        return ARRAY
    if keyword == "items":
        return SINGLE if dialect >= Dialect.DRAFT202012 else SINGLE_OR_ARRAY
    if keyword == "additionalItems":
        return SINGLE if dialect <= Dialect.DRAFT201909 else NONE
    if keyword in ("contains", "propertyNames"):
        return SINGLE if dialect >= Dialect.DRAFT6 else NONE
    if keyword in ("if", "then", "else"):
        return SINGLE if dialect >= Dialect.DRAFT7 else NONE
    if keyword in ("unevaluatedProperties", "unevaluatedItems", "contentSchema"):
        return SINGLE if dialect >= Dialect.DRAFT201909 else NONE
    if keyword == "dependentSchemas":
        return MAP if dialect >= Dialect.DRAFT201909 else NONE
    if keyword == "prefixItems":
        return ARRAY if dialect >= Dialect.DRAFT202012 else NONE
    return NONE
