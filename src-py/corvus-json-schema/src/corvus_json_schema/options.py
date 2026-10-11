"""Compile options and the evaluator's exceptions."""

from __future__ import annotations

from collections.abc import Callable, Mapping
from dataclasses import dataclass, field
from typing import Any

from .dialect import Dialect

DocumentResolver = Callable[[str], Any]
"""Resolves a schema document by absolute URI: the parsed JSON (or JSON text), or None if unknown."""

FormatValidator = Callable[[str], bool]
"""A custom format assertion."""


@dataclass(frozen=True)
class CompileOptions:
    """Options for compiling a schema (mirrors JsonSchemaEvaluatorOptions)."""

    default_dialect: Dialect = Dialect.DRAFT202012
    """The dialect for documents without ``$schema``."""

    assert_format: bool | None = None
    """Whether ``format`` is asserted. None (the default) follows the vocabularies: 2020-12 ``format-assertion``
    asserts, everything else annotates."""

    assert_format_in_legacy_drafts: bool = False
    """When ``assert_format`` is None, assert ``format`` in draft 4 to 7 too."""

    assert_content: bool = True
    """Assert ``contentEncoding``/``contentMediaType`` in draft 7 (the only draft that asserts them)."""

    formats: Mapping[str, FormatValidator] = field(default_factory=dict)
    """Custom format assertions, by format name; they take precedence over the built-in set."""

    resolve_document: DocumentResolver | None = None
    """Resolves remote documents. The standard metaschemas are always available."""

    base_uri: str | None = None
    """The base URI of the root document."""

    entry_point: str | None = None
    """A reference (relative to the root) to evaluate from, e.g. ``#/$defs/item``. Defaults to the root."""

    max_depth: int = 128
    """Maximum depth of in-place recursion on a cycle before evaluation is abandoned."""

    pattern_match_limit: float | None = 1.0
    """The time one match of a ``pattern`` or ``patternProperties`` regular expression against one string may take,
    in seconds. A match that takes longer is abandoned and the validation raises ``PatternMatchLimitError``. ``None``
    or zero turns the limit off, for a caller who trusts both the schema and the instances.

    A pattern that is proved to match in time proportional to the text is run by the standard library's ``re``,
    which needs no limit and has none. Every other pattern is run by the ``regex`` package, and the limit is of
    time on the clock: the error is not raised before nine tenths of the limit have passed, and a match is bounded
    at about twice the limit whatever other threads are doing (see the package's README)."""


class SchemaCompilationError(Exception):
    """Raised when a schema cannot be compiled (an unresolvable reference, an invalid pattern)."""


class PatternTooLargeError(SchemaCompilationError):
    """Raised when a schema cannot be compiled because one of its regular expressions is over a limit of size
    (32,768 characters of pattern or of its translation for Python's engines, 100 levels of groups, or 10,000 copies
    of what the ``regex`` package writes out for a count). It is never raised for a pattern that is not valid, which
    is a plain ``SchemaCompilationError``."""


class PatternMatchLimitError(Exception):
    """Raised when one match of a ``pattern`` or ``patternProperties`` regular expression reaches its limit (see the
    ``pattern_match_limit`` option of ``compile``). The instance is then neither valid nor not valid."""

    def __init__(self, pattern: str, limit: float) -> None:
        shown = pattern if len(pattern) <= 200 else f"{pattern[:200]}... ({len(pattern)} characters)"
        # The limit as corvus-json-schema-rs writes it: 1s, 250ms, 1.5s.
        if limit >= 1:
            time = f"{limit:g}s"
        elif limit >= 1e-3:
            time = f"{limit * 1e3:g}ms"
        else:
            time = f"{limit * 1e6:g}\u00b5s"
        super().__init__(
            f"A match of the regular expression '{shown}' was abandoned: it took longer than the limit of {time}."
        )
        self.pattern = pattern
        """The regular expression."""
        self.limit = limit
        """The time the match was allowed, in seconds."""


def match_limit(seconds: float | None) -> float | None:
    """The limit of a match as the engines take it: the seconds, or None when ``seconds`` is None, zero or less
    (the limit is off). A value that is not a number of seconds raises ValueError."""
    if seconds is None:
        return None
    if isinstance(seconds, bool) or not isinstance(seconds, (int, float)) or seconds != seconds:
        raise ValueError("pattern_match_limit is not a number of seconds.")
    return float(seconds) if seconds > 0 else None


class SchemaEvaluationDepthError(Exception):
    """Raised when evaluation recurses in place beyond ``max_depth`` (a schema that loops without consuming the
    instance)."""

    def __init__(self) -> None:
        super().__init__("The schema recursed in place beyond the maximum depth.")
