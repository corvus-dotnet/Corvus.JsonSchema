"""Results collection: a port of Corvus.Text.Json's JsonSchemaResultsCollector and JsonSchemaAnnotationProducer.

The evaluator opens a context per subschema application and writes keyword rows into the open context. Closing a
context either commits it (a summary row, then its own rows newest first, after its committed descendants) or pops it
(everything it and its descendants wrote is discarded). Levels decide which rows exist and which carry message text:
Basic has failures without text, Detailed adds text to failures, Verbose keeps every row with text, annotations
included.
"""

from __future__ import annotations

import json
from collections.abc import Callable, Iterator
from dataclasses import dataclass
from enum import IntEnum
from typing import Any


class ResultsLevel(IntEnum):
    """How much a results collector records."""

    BASIC = 0
    """Failures only, without message text (the lowest overhead)."""
    DETAILED = 1
    """Failures only, with message text."""
    VERBOSE = 2
    """Every evaluation, passing and failing, with message text, including annotations."""


@dataclass(frozen=True)
class SchemaResult:
    """One result row."""

    is_match: bool
    message: str
    """The message, or '' when the level records none or the keyword has none. Annotation rows carry raw JSON."""
    evaluation_location: str
    """The path of keywords from the root schema (e.g. ``/properties/name/type``)."""
    schema_evaluation_location: str
    """The JSON pointer of the evaluated schema (or keyword) within its document."""
    document_evaluation_location: str
    """The JSON pointer of the instance location (e.g. ``/name``)."""


Message = str | Callable[[], str] | None
"""A message, or a function producing it (only called when the level records message text)."""


@dataclass
class _Frame:
    eval_path: str
    schema_path: str
    doc_path: str
    commit_index: int
    rows: list[SchemaResult]


def encode_pointer_segment(segment: str) -> str:
    """Encodes a JSON pointer segment (``~`` as ``~0``, ``/`` as ``~1``)."""
    if "~" not in segment and "/" not in segment:
        return segment
    return segment.replace("~", "~0").replace("/", "~1")


class JsonSchemaResultsCollector:
    """Collects the results of an evaluation (JsonSchemaResultsCollector)."""

    def __init__(self, level: ResultsLevel) -> None:
        self.level = level
        self._committed: list[SchemaResult] = []
        self._frames: list[_Frame] = []
        self._eval_path = ""
        self._schema_path = ""
        self._doc_path = ""

    @staticmethod
    def create(level: ResultsLevel) -> JsonSchemaResultsCollector:
        """Creates a collector at the given level."""
        return JsonSchemaResultsCollector(level)

    @property
    def results(self) -> list[SchemaResult]:
        """The results, in commit order."""
        return self._committed

    @property
    def result_count(self) -> int:
        return len(self._committed)

    # ---------------------------------------------------------------------------------------------------------------
    # The evaluator's side (IJsonSchemaResultsCollector)

    def begin_child_context(
        self, eval_segment: str | None, schema_location: str | None, doc_segment: str | None
    ) -> None:
        """Opens a child context. The evaluation path is extended by ``eval_segment`` (verbatim), the schema path is
        replaced by ``schema_location``, and the document path is extended by ``doc_segment`` (already
        pointer-encoded) when given."""
        self._frames.append(_Frame(self._eval_path, self._schema_path, self._doc_path, len(self._committed), []))
        if eval_segment is not None:
            self._eval_path += "/" + eval_segment
        if schema_location is not None:
            self._schema_path = schema_location
        if doc_segment is not None:
            self._doc_path += "/" + doc_segment

    def commit_child_context(self, parent_is_match: bool, child_is_match: bool, message: Message) -> None:
        """Closes a child context. When the parent does not need the child's results (``parent_is_match``) they are
        discarded below Verbose; otherwise the context's summary row is written and its rows are committed."""
        if parent_is_match and self.level != ResultsLevel.VERBOSE:
            self.pop_child_context()
            return
        frame = self._frames[-1]
        frame.rows.append(self._row(child_is_match, message, self._eval_path, self._schema_path, self._doc_path))
        self._committed.extend(reversed(frame.rows))
        self._restore(self._frames.pop())

    def pop_child_context(self) -> None:
        """Closes a child context and discards everything it and its descendants wrote."""
        frame = self._frames.pop()
        del self._committed[frame.commit_index :]
        self._restore(frame)

    def evaluated_keyword(self, is_match: bool, message: Message, keyword: str) -> None:
        if not is_match or self.level == ResultsLevel.VERBOSE:
            k = "/" + encode_pointer_segment(keyword)
            self._write(self._row(is_match, message, self._eval_path + k, self._schema_path + k, self._doc_path))

    def evaluated_keyword_for_property(
        self, is_match: bool, message: Message, property_name: str, keyword: str
    ) -> None:
        if not is_match or self.level == ResultsLevel.VERBOSE:
            k = "/" + encode_pointer_segment(keyword)
            doc = self._doc_path + "/" + encode_pointer_segment(property_name)
            self._write(self._row(is_match, message, self._eval_path + k, self._schema_path + k, doc))

    def ignored_keyword(self, message: Message, keyword: str) -> None:
        """An annotation: Verbose only; the keyword extends the evaluation path but not the schema path."""
        if self.level == ResultsLevel.VERBOSE:
            path = self._eval_path + "/" + encode_pointer_segment(keyword)
            self._write(self._row(True, message, path, self._schema_path, self._doc_path))

    def evaluated_boolean_schema(self, is_match: bool, message: Message) -> None:
        if not is_match or self.level == ResultsLevel.VERBOSE:
            self._write(self._row(is_match, message, self._eval_path, self._schema_path, self._doc_path))

    def _row(
        self, is_match: bool, message: Message, evaluation_location: str, schema_location: str, document_location: str
    ) -> SchemaResult:
        with_text = self.level == ResultsLevel.VERBOSE or (not is_match and self.level >= ResultsLevel.DETAILED)
        text = (message if isinstance(message, str) else message()) if with_text and message is not None else ""
        return SchemaResult(is_match, text, evaluation_location, schema_location, document_location)

    def _write(self, row: SchemaResult) -> None:
        self._frames[-1].rows.append(row)

    def _restore(self, frame: _Frame) -> None:
        self._eval_path = frame.eval_path
        self._schema_path = frame.schema_path
        self._doc_path = frame.doc_path


@dataclass(frozen=True)
class Annotation:
    """An annotation extracted from verbose results."""

    instance_location: str
    """The instance location (JSON pointer)."""
    keyword: str
    schema_location: str
    """The JSON pointer of the schema object that holds the keyword."""
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


_FRAGMENT_SAFE = frozenset(b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~!$&'()*+,;=:@/?")


def schema_location_fragment(schema_location: str) -> str:
    """``#`` followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8)."""
    out = ["#"]
    for byte in schema_location.encode("utf-8", errors="surrogatepass"):
        out.append(chr(byte) if byte in _FRAGMENT_SAFE else f"%{byte:02X}")
    return "".join(out)


def collect_annotations(collector: JsonSchemaResultsCollector) -> dict[str, dict[str, dict[str, Any]]]:
    """Annotations grouped by instance location, then keyword, then schema location fragment, with parsed values
    (JsonSchemaAnnotationProducer.WriteAnnotationsTo, as a dict):
    ``{"/name": {"title": {"#/properties/name": "Name"}}}``."""
    out: dict[str, dict[str, dict[str, Any]]] = {}
    for a in enumerate_annotations(collector):
        by_keyword = out.setdefault(a.instance_location, {})
        by_schema = by_keyword.setdefault(a.keyword, {})
        by_schema[schema_location_fragment(a.schema_location)] = json.loads(a.value)
    return out
