"""A high-performance JSON Schema evaluator (draft 4, 6, 7, 2019-09 and 2020-12) that compiles schemas to
specialised Python, with results collection and annotations. A port of the Corvus.Text.Json V5 runtime evaluator, by
way of its TypeScript port.

    >>> import corvus_json_schema as cjs
    >>> validate = cjs.compile({"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}})
    >>> validate({"id": 3})
    True
    >>> validate({"id": "3"})
    False

Instances are the values ``json.loads`` produces (``dict``, ``list``, ``str``, ``int``, ``float``, ``bool`` and
``None``); a validator tests their exact types, so subclasses such as ``OrderedDict`` are not objects to it.
"""

from __future__ import annotations

import builtins
import json
import re
import types
from collections.abc import Mapping
from dataclasses import replace
from typing import Any, Protocol, cast

from . import runtime
from .codegen import CodeGenerator, GeneratedCode
from .collecting import CollectingProgram, evaluate_with_collector, serialize_program
from .compiler import CompiledSchema, SchemaCompiler, resolve_annotations
from .dialect import Dialect
from .options import (
    CompileOptions,
    DocumentResolver,
    FormatValidator,
    SchemaCompilationError,
    SchemaEvaluationDepthError,
)
from .results import (
    Annotation,
    JsonSchemaResultsCollector,
    ResultsLevel,
    SchemaResult,
    collect_annotations,
    enumerate_annotations,
    schema_location_fragment,
)

__version__ = "0.1.0"

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
    "generate_module",
    "schema_location_fragment",
]


class Validator(Protocol):
    """A compiled validator: returns True when the instance is valid against the schema."""

    def __call__(self, instance: Any) -> bool: ...

    def evaluate(self, instance: Any, collector: JsonSchemaResultsCollector | None = None) -> bool:
        """Evaluates the instance, reporting results to the collector when one is given (every keyword is evaluated
        and reported, at the collector's level); without a collector this is the validator itself."""
        ...

    @property
    def source(self) -> str:
        """The generated Python (the body of the validator's module)."""
        ...


def _options(options: CompileOptions | None, overrides: Mapping[str, Any]) -> CompileOptions:
    base = options if options is not None else CompileOptions()
    return replace(base, **overrides) if overrides else base


def _parse_schema(schema: Any) -> Any:
    return json.loads(schema) if isinstance(schema, (str, bytes, bytearray)) else schema


def _generate(schema: Any, options: CompileOptions) -> tuple[GeneratedCode, CompiledSchema]:
    program = SchemaCompiler.compile(_parse_schema(schema), options)
    return CodeGenerator(program).generate(), program


def _collecting_program(program: CompiledSchema) -> CollectingProgram:
    resolve_annotations(program)
    return CollectingProgram(
        program.nodes, program.root, program.uses_dynamic_scope, program.options.max_depth, program.options.formats
    )


def _assemble(g: GeneratedCode) -> str:
    s = ""
    if g.uses_dynamic_scope:
        s += "DS = []\n"
    if g.uses_depth:
        s += f"depth = 0\nMAXDEPTH = {g.max_depth}\n"
    s += g.declarations + "\n"
    if g.uses_dynamic_scope or g.uses_depth:
        s += "def validate(x):\n"
        if g.uses_depth:
            s += "    global depth\n    depth = 0\n"
        if g.uses_dynamic_scope:
            s += f"    DS.clear()\n    DS.append({g.root_resource})\n"
        s += f"    return {g.root}(x)\n"
    else:
        s += f"validate = {g.root}\n"
    return s


def _namespace(custom_formats: list[Any]) -> dict[str, Any]:
    return {
        "__name__": "corvus_json_schema.generated",
        "R": runtime,
        "F": custom_formats,
        "JSON": json,
        "RE": re,
        "NoneType": types.NoneType,
    }


def compile(schema: Any, options: CompileOptions | None = None, **kwargs: Any) -> Validator:
    """Compiles a schema (a parsed JSON value, or JSON text) into a validator.

    The schema is loaded, its references are resolved and analysed, and the result is emitted as specialised Python
    and compiled once. Options are given as a ``CompileOptions`` or as its fields by keyword (``default_dialect``,
    ``assert_format``, ``formats``, ``resolve_document``, ``base_uri``, ``entry_point``, ``max_depth`` and so on).
    """
    opts = _options(options, kwargs)
    g, program = _generate(schema, opts)
    source = _assemble(g)
    namespace = _namespace(g.custom_formats)
    try:
        exec(builtins.compile(source, "<corvus-json-schema>", "exec"), namespace)
    except SyntaxError as e:
        raise SchemaCompilationError(f"The schema produced invalid code: {e}") from e
    validate = namespace["validate"]
    # The collecting program (annotations included) is built on the first evaluation with a collector.
    collecting: list[CollectingProgram] = []

    def evaluate(instance: Any, collector: JsonSchemaResultsCollector | None = None) -> bool:
        if collector is None:
            return bool(validate(instance))
        if not collecting:
            collecting.append(_collecting_program(program))
        return evaluate_with_collector(collecting[0], instance, collector)

    # The generated function itself is the validator, so a call costs nothing beyond the checks. When the module keeps
    # evaluation state (a dynamic scope or a depth count), `validate` is already a wrapper that resets it.
    validate.source = source
    validate.evaluate = evaluate
    return cast(Validator, validate)


def generate_module(
    schema: Any,
    options: CompileOptions | None = None,
    *,
    runtime_import: str = "corvus_json_schema.runtime",
    collecting: bool = True,
    collecting_import: str = "corvus_json_schema.collecting",
    **kwargs: Any,
) -> str:
    """Generates a standalone Python module for the schema, defining ``validate(instance) -> bool``.

    The module depends only on the runtime helpers (``corvus_json_schema.runtime``), so the schema compiler is not
    needed where it runs. With ``collecting`` (the default) it also defines ``evaluate(instance, collector=None)``,
    embedding the program image for results collection. Custom format functions cannot be serialised; supply them to
    ``compile`` instead.
    """
    opts = _options(options, kwargs)
    g, program = _generate(schema, opts)
    if g.custom_formats:
        raise SchemaCompilationError("Custom format functions cannot be emitted into a standalone module.")
    text = "# Generated by corvus-json-schema. Do not edit.\n"
    text += "# ruff: noqa\n# fmt: off\n"
    text += "import json as JSON\nimport re as RE\nfrom types import NoneType\n\n"
    text += f"import {runtime_import} as R\n"
    if collecting:
        text += f"from {collecting_import} import evaluate_with_collector, load_program\n"
    text += "\n" + _assemble(g)
    text += "\n__all__ = ['validate'" + (", 'evaluate'" if collecting else "") + "]\n"
    if collecting:
        image = serialize_program(_collecting_program(program))
        text += (
            f"\n_image = {image!r}\n"
            "_program = None\n\n\n"
            "def evaluate(instance, collector=None):\n"
            '    """Evaluates the instance, reporting to the results collector when one is given."""\n'
            "    global _program\n"
            "    if collector is None:\n"
            "        return validate(instance)\n"
            "    if _program is None:\n"
            "        _program = load_program(_image)\n"
            "    return evaluate_with_collector(_program, instance, collector)\n"
        )
    return text
