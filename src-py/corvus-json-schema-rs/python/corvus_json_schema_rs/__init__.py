"""A high-performance JSON Schema evaluator (draft 4, 6, 7, 2019-09 and 2020-12) for Python, backed by the
corvus-json-schema Rust crate, with results collection and annotations.

    >>> import corvus_json_schema_rs as cjs
    >>> validate = cjs.compile({"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}})
    >>> validate({"id": 3})
    True
    >>> validate.is_valid_json('{"id": "3"}')
    False

The API is the pure-Python ``corvus_json_schema`` package's, so either can be imported as the other, except that there
is no generated source (``Validator.source``, ``generate_module``) and a validator also takes JSON text
(``is_valid_json``), which it parses in Rust without creating Python objects.

Instances are converted to JSON values in Rust: ``dict`` (string keys), ``list`` and ``tuple``, ``str``, ``int``,
``float``, ``bool`` and ``None``. Integers beyond 64 bits become the nearest double.
"""

from __future__ import annotations

import json
from collections.abc import Callable, Iterator, Mapping
from dataclasses import asdict, dataclass, field
from enum import Enum, IntEnum
from typing import Any

from . import _native
from ._native import (
    PatternMatchLimitError,
    PatternTooLargeError,
    SchemaCompilationError,
    SchemaEvaluationDepthError,
    SchemaResult,
    Validator,
)

__version__ = "0.2.0"

__all__ = [
    "Annotation",
    "CompileOptions",
    "Dialect",
    "DocumentResolver",
    "FormatValidator",
    "JsonSchemaResultsCollector",
    "PatternMatchLimitError",
    "PatternTooLargeError",
    "RegexEdition",
    "RegexUnicodeVersion",
    "ResultsLevel",
    "SchemaCompilationError",
    "SchemaEvaluationDepthError",
    "SchemaResult",
    "Validator",
    "collect_annotations",
    "compile",
    "enumerate_annotations",
    "schema_location_fragment",
]


class Dialect(IntEnum):
    """The JSON Schema dialects the evaluator understands, in specification order."""

    DRAFT4 = 0
    DRAFT6 = 1
    DRAFT7 = 2
    DRAFT201909 = 3
    DRAFT202012 = 4


class RegexEdition(str, Enum):
    """The edition of ECMA-262 whose grammar ``pattern``, ``patternProperties`` and the ``regex`` format are read with."""

    DIALECT = "dialect"
    """The dialect's: ECMAScript 2020 for 2019-09 and 2020-12, which name it, and today's for the drafts before them."""
    CURRENT = "current"
    """Today's grammar, for every dialect."""
    ES2020 = "es2020"
    """The grammar of ECMAScript 2020 (ECMA-262, 11th edition), for every dialect."""


class RegexUnicodeVersion(str, Enum):
    """The version of Unicode patterns are read with: the members of each ``\\p{...}``, the characters of a group name
    and the case folding of the ``i`` flag."""

    DEFAULT = "default"
    """The version of the V8 the answers are held to, Unicode 17 today."""
    LATEST = "latest"
    """The latest version the engine carries, Unicode 18 today."""
    V13 = "13"
    V17 = "17"
    V18 = "18"


class ResultsLevel(IntEnum):
    """How much a results collector records."""

    BASIC = 0
    """Failures only, without message text (the lowest overhead)."""
    DETAILED = 1
    """Failures only, with message text."""
    VERBOSE = 2
    """Every evaluation, passing and failing, with message text, including annotations."""


DocumentResolver = Callable[[str], Any]
"""Resolves a schema document by absolute URI: the parsed JSON (or JSON text), or None if unknown."""

FormatValidator = Callable[[str], bool]
"""A custom format assertion."""


@dataclass(frozen=True)
class CompileOptions:
    """Options for compiling a schema (mirrors JsonSchemaEvaluatorOptions)."""

    default_dialect: Dialect = Dialect.DRAFT202012
    assert_format: bool | None = None
    assert_format_in_legacy_drafts: bool = False
    assert_content: bool = True
    formats: Mapping[str, FormatValidator] = field(default_factory=dict)
    resolve_document: DocumentResolver | None = None
    base_uri: str | None = None
    entry_point: str | None = None
    max_depth: int = 128
    pattern_match_limit: float | None = 1.0
    """The time one match of a ``pattern`` or ``patternProperties`` regular expression against one string may take,
    in seconds. A match that takes longer is abandoned and the validation raises ``PatternMatchLimitError``. ``None``
    or zero turns the limit off, for a caller who trusts both the schema and the instances."""
    regex_edition: RegexEdition = RegexEdition.DIALECT
    """The edition of ECMA-262 patterns are read with. Under ECMAScript 2020, the default for 2019-09 and 2020-12, a
    pattern that uses what later editions added, such as a modifier group ``(?i:...)``, is not valid."""
    regex_unicode_version: RegexUnicodeVersion = RegexUnicodeVersion.DEFAULT
    """The version of Unicode patterns are read with."""


class JsonSchemaResultsCollector(_native.JsonSchemaResultsCollector):
    """Collects the results of an evaluation."""

    @staticmethod
    def create(level: ResultsLevel) -> JsonSchemaResultsCollector:  # type: ignore[override]
        """Creates a collector at the given level."""
        return JsonSchemaResultsCollector(int(level))

    @property
    def level(self) -> ResultsLevel:
        return ResultsLevel(self.level_value)


def compile(schema: Any, options: CompileOptions | None = None, **kwargs: Any) -> Validator:  # noqa: A001
    """Compiles a schema (a parsed JSON value, or JSON text) into a validator.

    Options are given as a ``CompileOptions`` or as its fields by keyword (``default_dialect``, ``assert_format``,
    ``formats``, ``resolve_document``, ``base_uri``, ``entry_point``, ``max_depth``, ``pattern_match_limit`` and so
    on).

    A schema with a regular expression that is not valid raises ``SchemaCompilationError``, and one with a regular
    expression over a limit of size raises ``PatternTooLargeError``, which is a ``SchemaCompilationError``.
    """
    fields = asdict(options) if options is not None else {}
    fields.update(kwargs)
    if isinstance(schema, (str, bytes, bytearray)):
        schema = json.loads(schema)
    if "default_dialect" in fields:
        fields["default_dialect"] = int(fields["default_dialect"])
    if "formats" in fields:
        fields["formats"] = dict(fields["formats"]) if fields["formats"] else None
    for name in ("regex_edition", "regex_unicode_version"):
        if name in fields:
            fields[name] = str(fields[name].value if isinstance(fields[name], Enum) else fields[name])
    return _native.compile(schema, **fields)


@dataclass(frozen=True)
class Annotation:
    """An annotation extracted from verbose results."""

    instance_location: str
    keyword: str
    schema_location: str
    value: str
    """The annotation value as JSON text."""


_JSON_VALUE_START = frozenset('"{[tfn-0123456789')


def enumerate_annotations(collector: JsonSchemaResultsCollector) -> Iterator[Annotation]:
    """The annotations in a verbose collector's results (JsonSchemaAnnotationProducer.EnumerateAnnotations)."""
    for r in collector.results:
        if not r.is_match or len(r.message) == 0:
            continue
        slash = r.evaluation_location.rfind("/")
        if slash < 0 or r.evaluation_location == r.schema_evaluation_location:
            continue
        keyword = r.evaluation_location[slash + 1 :]
        if len(keyword) == 0 or r.message[0] not in _JSON_VALUE_START:
            continue
        yield Annotation(r.document_evaluation_location, keyword, r.schema_evaluation_location, r.message)


def collect_annotations(collector: JsonSchemaResultsCollector) -> dict[str, dict[str, dict[str, Any]]]:
    """Annotations grouped by instance location, then keyword, then schema location fragment, with parsed values:
    ``{"/name": {"title": {"#/properties/name": "Name"}}}``."""
    return collector.collect_annotations()  # type: ignore[no-any-return]


def schema_location_fragment(schema_location: str) -> str:
    """``#`` followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8)."""
    return _native.schema_location_fragment(schema_location)
