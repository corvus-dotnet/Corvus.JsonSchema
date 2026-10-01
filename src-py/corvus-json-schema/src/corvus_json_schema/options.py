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


class SchemaCompilationError(Exception):
    """Raised when a schema cannot be compiled (an unresolvable reference, an invalid pattern)."""


class SchemaEvaluationDepthError(Exception):
    """Raised when evaluation recurses in place beyond ``max_depth`` (a schema that loops without consuming the
    instance)."""

    def __init__(self) -> None:
        super().__init__("The schema recursed in place beyond the maximum depth.")
