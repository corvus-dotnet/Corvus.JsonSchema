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
from enum import IntEnum
from typing import Any

from . import _native
from ._native import SchemaCompilationError, SchemaEvaluationDepthError, SchemaResult, Validator

__version__ = "0.1.3"

__all__ = [
    "Annotation",
    "CompileOptions",
    "Dialect",
    "DocumentResolver",
    "FormatValidator",
    "JsonSchemaResultsCollector",
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
    ``formats``, ``resolve_document``, ``base_uri``, ``entry_point``, ``max_depth`` and so on).
    """
    fields = asdict(options) if options is not None else {}
    fields.update(kwargs)
    if isinstance(schema, (str, bytes, bytearray)):
        schema = json.loads(schema)
    if "default_dialect" in fields:
        fields["default_dialect"] = int(fields["default_dialect"])
    if "formats" in fields:
        fields["formats"] = dict(fields["formats"]) if fields["formats"] else None
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
