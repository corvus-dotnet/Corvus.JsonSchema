"""The Corvus JSON Schema Python model runtime.

The single module imported by generated Python models. Re-exports the exact-number primitives, the
evaluation tracker and failure collector, the core JSON helpers, the format checks, the temporal
converters, and the JSON Patch / Merge Patch surface. The Python peer of ``@endjin/corvus-json-runtime``.
"""

from __future__ import annotations

from .core import build, canonicalize, decode_and_parse, eq, is_arr, is_obj, ptr, re_test
from .formats import FormatError, fmt, fmt_content
from .numbers import cmp, exact_number, is_int, is_num, multiple_of, num_eq
from .patch import (
    JsonPatchError,
    apply_merge_patch,
    apply_patch,
    create_merge_patch,
    create_patch,
)
from .produce import produce, rmw_produce_full, rmw_upsert
from .results import NOEV, Annotation, Ev, Failure, Results, fresh
from .temporal import to_duration, to_instant, to_plain_date, to_plain_time

__all__ = [
    "NOEV",
    "Annotation",
    "Ev",
    "Failure",
    "FormatError",
    "JsonPatchError",
    "Results",
    "apply_merge_patch",
    "apply_patch",
    "build",
    "canonicalize",
    "cmp",
    "create_merge_patch",
    "create_patch",
    "decode_and_parse",
    "eq",
    "exact_number",
    "fmt",
    "fmt_content",
    "fresh",
    "is_arr",
    "is_int",
    "is_num",
    "is_obj",
    "multiple_of",
    "num_eq",
    "produce",
    "ptr",
    "re_test",
    "rmw_produce_full",
    "rmw_upsert",
    "to_duration",
    "to_instant",
    "to_plain_date",
    "to_plain_time",
]