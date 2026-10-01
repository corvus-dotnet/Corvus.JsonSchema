"""Collecting-mode evaluation: evaluates an instance exhaustively against the compiled node graph and reports every
keyword to a JsonSchemaResultsCollector, reproducing the C# evaluator's Eval<CollectingMode> (keyword order,
evaluation/schema/instance paths, messages, which subschema results are committed or discarded).

Flag-mode validation never comes here: it runs the generated code. Results collection is exhaustive by nature (every
keyword is evaluated and reported), so it interprets the graph rather than generating a second program.
"""

from __future__ import annotations

import json
from collections.abc import Callable, Mapping
from dataclasses import dataclass, fields
from typing import Any

from .dialect import Dialect
from .formats import FORMAT_VALIDATORS, NUMERIC_FORMAT_VALIDATORS, is_numeric_format, legacy_hostname
from .node import (
    CONTENT_BASE64,
    CONTENT_JSON,
    T_ARRAY,
    T_BOOLEAN,
    T_INTEGER,
    T_NULL,
    T_NUMBER,
    T_OBJECT,
    T_STRING,
    DependencyEntry,
    DynamicRefTarget,
    PatternProperty,
    SchemaNode,
)
from .options import SchemaEvaluationDepthError
from .pattern import compile_pattern
from .results import JsonSchemaResultsCollector, Message, encode_pointer_segment
from .runtime import content, equal, includes, multiple_of


@dataclass
class CollectingProgram:
    """What collecting-mode evaluation needs from a compiled schema (also the shape of a serialised program image)."""

    nodes: list[SchemaNode]
    root: int
    uses_dynamic_scope: bool
    max_depth: int
    formats: Mapping[str, Callable[[str], bool]] | None = None
    """Custom format assertions by name (in-memory programs only)."""


# Messages (Corvus.Text.Json Strings.resx).
EVALUATED_SUBSCHEMA = "The value was expected to match the subschema."
MATCHED_ALL = "The value matched all subschema."
DID_NOT_MATCH_ALL = "The value did not match all subschema."
MATCHED_AT_LEAST_ONE = "The value matched at least one subschema."
DID_NOT_MATCH_AT_LEAST_ONE = "The value did not match at least one subschema."
MATCHED_NO_SCHEMA = "The instance matched no schema."
MATCHED_EXACTLY_ONE = "The value matched exactly one subschema."
MATCHED_MORE_THAN_ONE = "The instance matched more than one schema."
MATCHED_NOT = "The value matched the subschema in a not composition, which means the evaluation was not a match."
DID_NOT_MATCH_NOT = (
    "The value did not match the subschema in a not composition, which means the evaluation was a match."
)
MATCHED_IF_FOR_THEN = (
    "The value matched the subschema in a binary or ternay if, which means the evaluation will go on to match the then "
    "subschema."
)
MATCHED_IF_FOR_ELSE = (
    "The value did not match the subschema in a ternary if, which means the evaluation will go on to match the else "
    "subschema."
)
MATCHED_THEN = "The value matched the then subschema corresponding to a binary or ternary if."
DID_NOT_MATCH_THEN = "The value did not match the then subschema corresponding to a binary or ternary if."
MATCHED_ELSE = "The value matched the else subschema corresponding to a ternary if."
DID_NOT_MATCH_ELSE = "The value did not match the else subschema corresponding to a ternary if."
UNIQUE_ITEMS = "The array was expected to contain unique items."
PROPERTY_NAME_FAILED = "The property name did not match the schema."

STRING_FORMAT_MESSAGES = {
    "date": "Expected an ISO8601 Date string.",
    "date-time": "Expected an ISO8601 Offset DateTime string.",
    "time": "Expected an ISO8601 Offset Time string.",
    "duration": "Expected an ISO8601 Duration string.",
    "email": "Expected an RFC5321 Section-4.1.2 Email string.",
    "idn-email": "Expected an RFC6531 IDN Email string.",
    "hostname": "Expected an RFC1035 hostname.",
    "idn-hostname": "Expected an RFC5890 Section-2.3.2.3 IDN hostname.",
    "ipv4": "Expected an RFC2673 IP V4 address.",
    "ipv6": "Expected an RFC2373 IP V6 address.",
    "uri": "Expected an absolute URI.",
    "uri-reference": "Expected a URI reference.",
    "iri": "Expected an absolute IRI.",
    "iri-reference": "Expected an IRI reference.",
    "uuid": "Expected an RFC4122 UUID.",
    "uri-template": "Expected an RFC6570 URI Template.",
    "json-pointer": "Expected an RFC6901 JSON Pointer.",
    "relative-json-pointer": "Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer).",
    "regex": "Expected a regular expression specification.",
}


def _q(v: str) -> str:
    """``" 'v'"``, or nothing for an empty value (JsonSchemaEvaluation.AppendSingleQuotedValue)."""
    return "" if len(v) == 0 else f" '{v}'"


def number_text(n: int | float) -> str:
    """A number as the Rust evaluator writes it (serde_json): integers exactly, a float in its shortest round-trip form
    with ``.0`` kept on integral values and no leading zeros in the exponent (``2.0``, ``1e+21``, ``1.5e-7``)."""
    if type(n) is int:
        return str(n)
    if n != n:
        return "NaN"
    if n in (float("inf"), float("-inf")):
        return "Infinity" if n > 0 else "-Infinity"
    text = repr(n)
    if "e" in text:
        mantissa, exponent = text.split("e")
        sign = "-" if exponent.startswith("-") else "+"
        text = f"{mantissa.removesuffix('.0')}e{sign}{exponent.lstrip('+-').lstrip('0') or '0'}"
    return text


_TYPE_ORDER = (
    (T_ARRAY, "array"),
    (T_OBJECT, "object"),
    (T_NULL, "null"),
    (T_BOOLEAN, "boolean"),
    (T_NUMBER, "number"),
    (T_INTEGER, "integer"),
    (T_STRING, "string"),
)


def _type_message(mask: int) -> str | None:
    names = [name for m, name in _TYPE_ORDER if mask & m]
    if not names:
        return None
    if len(names) == 1:
        return f"The value was expected to be of type '{names[0]}'"
    return "The value was expected to be of type '[" + ", ".join(f'"{n}"' for n in names) + "]'"


def _matches_type(mask: int, x: Any, dialect: Dialect) -> bool:
    t = type(x)
    if t is str:
        return (mask & T_STRING) != 0
    if t is int:
        return (mask & (T_NUMBER | T_INTEGER)) != 0
    if t is float:
        # In draft 4 an integer is a number written without a fractional part (json.loads gives 1.0 a float).
        return (mask & T_NUMBER) != 0 or ((mask & T_INTEGER) != 0 and dialect != Dialect.DRAFT4 and x.is_integer())
    if t is bool:
        return (mask & T_BOOLEAN) != 0
    if x is None:
        return (mask & T_NULL) != 0
    if t is list:
        return (mask & T_ARRAY) != 0
    if t is dict:
        return (mask & T_OBJECT) != 0
    return False


def _const_message(value: Any) -> str | None:
    t = type(value)
    if t is str:
        return "Expected the value to be the string" + _q(value)
    if t is bool:
        return f"Expected the value to be '{'true' if value else 'false'}'"
    if t is int or t is float:
        return "The value was expected to be equal to" + _q(number_text(value))
    if value is None:
        return "Expected the value to be 'null'"
    return None


def _json_text(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def _search(pattern: str, s: str) -> bool:
    m = compile_pattern(pattern)
    return m is not None and m(s) is not None


def evaluate_with_collector(program: CollectingProgram, instance: Any, collector: JsonSchemaResultsCollector) -> bool:
    """Evaluates ``instance`` against the program's entry, reporting to ``collector``; returns whether it is valid."""
    return _CollectingEvaluator(program, collector).evaluate(instance)


class _CollectingEvaluator:
    def __init__(self, program: CollectingProgram, collector: JsonSchemaResultsCollector) -> None:
        self.program = program
        self.c = collector
        self.nodes = program.nodes
        self.scope: list[int] = []
        self.depth = 0

    def evaluate(self, instance: Any) -> bool:
        # A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
        root = self.resolve(self.program.root)[0]
        self.c.begin_child_context(None, self.nodes[root].pointer, None)
        ok = self.eval_node(root, instance, None)
        self.c.commit_child_context(False, ok, EVALUATED_SUBSCHEMA)
        return ok

    def pure_ref_target(self, n: SchemaNode) -> int:
        """The single reference of a pure-$ref node (SchemaCompiler.IsPureRef), or -1."""
        refs = (1 if n.ref >= 0 else 0) + (1 if n.static_dynamic_ref >= 0 else 0)
        if refs != 1 or n.always_true or n.always_false or n.annotations is not None:
            return -1
        if (
            n.has_type
            or n.has_const
            or n.enum_values is not None
            or n.has_number_keywords
            or n.has_string_keywords
            or n.has_object_keywords
            or n.has_array_keywords
            or n.dynamic_ref is not None
            or n.all_of is not None
            or n.any_of is not None
            or n.one_of is not None
            or n.not_ >= 0
            or n.if_ >= 0
        ):
            return -1
        return n.ref if n.ref >= 0 else n.static_dynamic_ref

    def resolve(self, node_id: int) -> tuple[int, str]:
        """Follows pure-reference hops (at most 16; not across resources when a dynamic scope is kept). The suffix
        extends the evaluation path by the keyword of each hop: ``/$ref``, or ``/$dynamicRef``/``/$recursiveRef`` for a
        statically resolved dynamic reference."""
        current = node_id
        suffix = ""
        for _ in range(16):
            n = self.nodes[current]
            nxt = self.pure_ref_target(n)
            if nxt < 0:
                break
            if self.program.uses_dynamic_scope and self.nodes[nxt].resource_id != n.resource_id:
                break
            suffix += "/$ref" if n.ref >= 0 else "/" + (n.static_dynamic_keyword or "")
            current = nxt
        return current, suffix

    def eval_node(self, node_id: int, x: Any, bits: set[Any] | None) -> bool:
        n = self.nodes[node_id]
        if n.always_true or n.always_false:
            self.c.evaluated_boolean_schema(n.always_true, None)
            return n.always_true
        pushed = False
        if self.program.uses_dynamic_scope and (not self.scope or self.scope[-1] != n.resource_id):
            self.scope.append(n.resource_id)
            pushed = True
        if bits is None:
            if (n.unevaluated_properties >= 0 and type(x) is dict) or (n.unevaluated_items >= 0 and type(x) is list):
                bits = set()
        try:
            return self.eval_core(n, x, bits)
        finally:
            if pushed:
                self.scope.pop()

    def eval_core(self, n: SchemaNode, x: Any, bits: set[Any] | None) -> bool:
        c = self.c
        ok = True
        if n.has_type:
            m = _matches_type(n.type, x, n.dialect)
            c.evaluated_keyword(m, _type_message(n.type), "type")
            ok = ok and m
        if n.has_const:
            m = equal(x, n.const_value)
            c.evaluated_keyword(m, _const_message(n.const_value), "const")
            ok = ok and m
        if n.enum_values is not None:
            m = includes(n.enum_values, x)
            c.evaluated_keyword(m, MATCHED_AT_LEAST_ONE if m else DID_NOT_MATCH_AT_LEAST_ONE, "enum")
            ok = ok and m
        t = type(x)
        if t is int or t is float:
            if n.has_number_keywords:
                ok = self.eval_number(n, x) and ok
        elif t is str:
            if n.has_string_keywords:
                ok = self.eval_string(n, x) and ok
        elif t is dict:
            if n.has_object_keywords:
                ok = self.eval_object(n, x, bits) and ok
        elif t is list:
            if n.has_array_keywords:
                ok = self.eval_array(n, x, bits) and ok
        ok = self.eval_in_place(n, x, bits) and ok
        if t is dict and n.unevaluated_properties >= 0:
            assert bits is not None
            ok = self.eval_unevaluated_properties(n, x, bits) and ok
        elif t is list and n.unevaluated_items >= 0:
            assert bits is not None
            ok = self.eval_unevaluated_items(n, x, bits) and ok
        if n.annotations is not None:
            for a in n.annotations:
                if a.strings_only and t is not str:
                    continue
                value = a.value
                c.ignored_keyword(lambda value=value: _json_text(value), a.keyword)  # type: ignore[misc]
        return ok

    # -------------------------------------------------------------------------------------------------------------
    # Numbers and strings

    def eval_number(self, n: SchemaNode, x: int | float) -> bool:
        c = self.c
        ok = True
        if n.assert_format and n.format is not None and is_numeric_format(n.format_kind):
            custom = (self.program.formats or {}).get(n.format)
            m = custom(number_text(x)) if custom is not None else NUMERIC_FORMAT_VALIDATORS[n.format_kind](x)
            c.evaluated_keyword(
                m,
                f"The value was expected to be in a supported format, and within bounds for '{n.format_kind}'",
                "format",
            )
            ok = ok and m

        def bound(value: int | float | None, test: Callable[[int | float], bool], text: str, keyword: str) -> None:
            nonlocal ok
            if value is None:
                return
            m = test(value)
            c.evaluated_keyword(m, lambda: text + _q(number_text(value)), keyword)
            ok = ok and m

        bound(n.minimum, lambda v: x >= v, "The value was expected to be greater than or equal to", "minimum")
        bound(n.maximum, lambda v: x <= v, "The value was expected to be less than or equal to", "maximum")
        bound(n.exclusive_minimum, lambda v: x > v, "The value was expected to be greater than", "exclusiveMinimum")
        bound(n.exclusive_maximum, lambda v: x < v, "The value was expected to be less than", "exclusiveMaximum")
        bound(
            n.multiple_of,
            lambda d: (x % d == 0) if type(d) is int and d != 0 else multiple_of(x, d),
            "The value was expected to be a multiple of",
            "multipleOf",
        )
        return ok

    def eval_string(self, n: SchemaNode, x: str) -> bool:
        c = self.c
        ok = True
        if n.min_length >= 0:
            m = len(x) >= n.min_length
            c.evaluated_keyword(
                m, f"Expected the length of the value to be greater than or equal to '{n.min_length}'", "minLength"
            )
            ok = ok and m
        if n.max_length >= 0:
            m = len(x) <= n.max_length
            c.evaluated_keyword(
                m, f"Expected the length of the value to be less than or equal to '{n.max_length}'", "maxLength"
            )
            ok = ok and m
        if n.pattern is not None:
            pattern = n.pattern
            m = _search(pattern, x)
            c.evaluated_keyword(
                m, lambda: "Expected the value to match the regular expression" + _q(pattern), "pattern"
            )
            ok = ok and m
        if n.assert_format and n.format is not None and not is_numeric_format(n.format_kind):
            custom = (self.program.formats or {}).get(n.format)
            message: str | None = None
            if custom is not None:
                m = custom(x)
                message = f"Expected a string in the '{n.format}' format."
            elif n.format_kind == "unknown":
                m = True
            else:
                validate = (
                    legacy_hostname
                    if n.format_kind == "hostname" and n.dialect <= Dialect.DRAFT6
                    else FORMAT_VALIDATORS[n.format_kind]
                )
                m = validate(x)
                message = STRING_FORMAT_MESSAGES.get(n.format_kind)
            c.evaluated_keyword(m, message, "format")
            ok = ok and m
        if n.assert_content:
            kind = 1 if n.content == CONTENT_BASE64 else 2 if n.content == CONTENT_JSON else 3
            m = content(x, kind)
            text = (
                "Expected a valid Base64-encoded string."
                if kind == 1
                else "Expected valid JSON content."
                if kind == 2
                else "Expected valid Base64-encoded JSON content."
            )
            c.evaluated_keyword(m, text, "contentEncoding" if kind == 1 else "contentMediaType")
            ok = ok and m
        return ok

    # -------------------------------------------------------------------------------------------------------------
    # Objects

    def eval_at(self, child_id: int, path: str, value: Any, doc_segment: str) -> bool:
        """A child application at a new instance location (a property value or an array item)."""
        target, suffix = self.resolve(child_id)
        self.c.begin_child_context(path + suffix, self.nodes[target].pointer, doc_segment)
        ok = self.eval_node(target, value, None)
        self.c.commit_child_context(ok, ok, EVALUATED_SUBSCHEMA)
        return ok

    def eval_object(self, n: SchemaNode, x: dict[str, Any], bits: set[Any] | None) -> bool:
        c = self.c
        ok = True
        if n.min_properties >= 0:
            m = len(x) >= n.min_properties
            c.evaluated_keyword(
                m, f"Expected the property count to be greater than or equal to '{n.min_properties}'", "minProperties"
            )
            ok = ok and m
        if n.max_properties >= 0:
            m = len(x) <= n.max_properties
            c.evaluated_keyword(
                m, f"Expected the property count to be less than or equal to '{n.max_properties}'", "maxProperties"
            )
            ok = ok and m
        if (
            n.properties is not None
            or n.pattern_properties is not None
            or n.additional_properties >= 0
            or n.property_names >= 0
        ):
            for k, v in x.items():
                doc = encode_pointer_segment(k)
                matched = False
                p = n.properties.get(k) if n.properties is not None else None
                if p is not None:
                    matched = True
                    if bits is not None:
                        bits.add(k)
                    ok = self.eval_at(p, "properties/" + encode_pointer_segment(k), v, doc) and ok
                for pp in n.pattern_properties or ():
                    if not _search(pp.pattern, k):
                        continue
                    matched = True
                    if bits is not None:
                        bits.add(k)
                    ok = self.eval_at(pp.node, "patternProperties/" + encode_pointer_segment(pp.pattern), v, doc) and ok
                if n.additional_properties >= 0 and not matched:
                    if bits is not None:
                        bits.add(k)
                    ok = self.eval_at(n.additional_properties, "additionalProperties", v, doc) and ok
                if n.property_names >= 0:
                    # Not elided; the document path stays the object's.
                    c.begin_child_context("propertyNames", self.nodes[n.property_names].pointer, None)
                    m = self.eval_node(n.property_names, k, None)
                    c.commit_child_context(m, m, EVALUATED_SUBSCHEMA)
                    if not m:
                        c.evaluated_keyword(False, PROPERTY_NAME_FAILED, "propertyNames")
                        ok = False
        if n.required_list is not None:
            for r in n.required_list:
                present = r in x
                c.evaluated_keyword_for_property(present, _required_message(present, r), r, "required")
                ok = ok and present
        if n.dependencies is not None:
            # Rows are reported under the keyword the schema used (dependencies, dependentRequired, dependentSchemas).
            for d in n.dependencies:
                if d.name not in x:
                    continue
                for r in d.required or ():
                    present = r in x
                    c.evaluated_keyword_for_property(present, _required_message(present, r), r, d.keyword)
                    ok = ok and present
                if d.schema is not None:
                    m = self.eval_in_place_child(
                        d.schema, d.keyword + "/" + encode_pointer_segment(d.name), x, bits, True
                    )[0]
                    c.evaluated_keyword_for_property(m, _dependency_message(d.name), d.name, d.keyword)
                    ok = ok and m
        return ok

    def eval_unevaluated_properties(self, n: SchemaNode, x: dict[str, Any], bits: set[Any]) -> bool:
        ok = True
        for k, v in x.items():
            if k in bits:
                continue
            bits.add(k)
            ok = self.eval_at(n.unevaluated_properties, "unevaluatedProperties", v, encode_pointer_segment(k)) and ok
        self.c.evaluated_keyword(ok, None, "unevaluatedProperties")
        return ok

    # -------------------------------------------------------------------------------------------------------------
    # Arrays

    def eval_array(self, n: SchemaNode, x: list[Any], bits: set[Any] | None) -> bool:
        c = self.c
        ok = True
        length = len(x)
        if n.min_items >= 0:
            m = length >= n.min_items
            c.evaluated_keyword(
                m, f"Expected the item count to be greater than or equal to '{n.min_items}'", "minItems"
            )
            ok = ok and m
        if n.max_items >= 0:
            m = length <= n.max_items
            c.evaluated_keyword(m, f"Expected the item count to be less than or equal to '{n.max_items}'", "maxItems")
            ok = ok and m
        if n.prefix_items is None and n.items < 0 and n.contains < 0 and not n.unique_items:
            return ok
        count = 0
        unique = True
        for i in range(length):
            if n.prefix_items is not None and i < len(n.prefix_items):
                if bits is not None:
                    bits.add(i)
                ok = self.eval_at(n.prefix_items[i], f"{n.prefix_keyword}/{i}", x[i], str(i)) and ok
            elif n.items >= 0:
                if bits is not None:
                    bits.add(i)
                ok = self.eval_at(n.items, n.items_keyword, x[i], str(i)) and ok
            if n.contains >= 0:
                target, suffix = self.resolve(n.contains)
                c.begin_child_context("contains" + suffix, self.nodes[target].pointer, str(i))
                if self.eval_node(target, x[i], None):
                    c.commit_child_context(True, True, EVALUATED_SUBSCHEMA)
                    count += 1
                    if n.contains_marks_evaluated and bits is not None:
                        bits.add(i)
                else:
                    c.pop_child_context()
            if n.unique_items and unique:
                for j in range(i):
                    if equal(x[i], x[j]):
                        unique = False
                        break
        if n.unique_items:
            c.evaluated_keyword(unique, UNIQUE_ITEMS, "uniqueItems")
            ok = ok and unique
        if n.contains >= 0:
            hi = n.max_contains
            m = count >= n.min_contains and (hi < 0 or count <= hi)
            message = (
                f"Expected the contains count to be less than or equal to '{hi}'"
                if hi >= 0 and count > hi
                else f"Expected the contains count to be greater than or equal to '{n.min_contains}'"
            )
            c.evaluated_keyword(m, message, "contains")
            ok = ok and m
        return ok

    def eval_unevaluated_items(self, n: SchemaNode, x: list[Any], bits: set[Any]) -> bool:
        ok = True
        for i, v in enumerate(x):
            if i in bits:
                continue
            bits.add(i)
            ok = self.eval_at(n.unevaluated_items, "unevaluatedItems", v, str(i)) and ok
        self.c.evaluated_keyword(ok, None, "unevaluatedItems")
        return ok

    # -------------------------------------------------------------------------------------------------------------
    # In-place applicators

    def can_mark(self, node_id: int, x: Any) -> bool:
        n = self.nodes[node_id]
        return n.marks_properties if type(x) is dict else n.marks_items

    def eval_in_place_child(
        self, child_id: int, path: str, x: Any, bits: set[Any] | None, commit_on_failure: bool, elide: bool = True
    ) -> tuple[bool, set[Any] | None]:
        """Evaluates an in-place child (EvalInPlaceCore): a new context at the same instance location, on a fresh
        scratch set of evaluated properties/items merged into the parent's on success. A failing child is committed or
        popped."""
        target, suffix = self.resolve(child_id) if elide else (child_id, "")
        scratch: set[Any] | None = set() if bits is not None and self.can_mark(child_id, x) else None
        guarded = self.nodes[target].in_place_cycle
        if guarded:
            self.depth += 1
            if self.depth > self.program.max_depth:
                self.depth = 0
                raise SchemaEvaluationDepthError()
        try:
            self.c.begin_child_context(path + suffix, self.nodes[target].pointer, None)
            ok = self.eval_node(target, x, scratch)
            if ok or commit_on_failure:
                self.c.commit_child_context(ok, ok, EVALUATED_SUBSCHEMA)
            else:
                self.c.pop_child_context()
            if ok and scratch is not None and bits is not None:
                bits |= scratch
            return ok, scratch
        finally:
            if guarded:
                self.depth -= 1

    def resolve_dynamic(self, d: DynamicRefTarget) -> int:
        for resource in self.scope:
            target = d.by_resource.get(resource)
            if target is not None:
                return target
        return d.fallback

    def eval_in_place(self, n: SchemaNode, x: Any, bits: set[Any] | None) -> bool:
        c = self.c
        ok = True
        if n.ref >= 0:
            m = self.eval_in_place_child(n.ref, "$ref", x, bits, True)[0]
            c.evaluated_keyword(m, MATCHED_ALL if m else DID_NOT_MATCH_ALL, "$ref")
            ok = ok and m
        if n.static_dynamic_ref >= 0:
            keyword = n.static_dynamic_keyword or "$dynamicRef"
            m = self.eval_in_place_child(n.static_dynamic_ref, keyword, x, bits, True)[0]
            c.evaluated_keyword(m, MATCHED_ALL if m else DID_NOT_MATCH_ALL, keyword)
            ok = ok and m
        if n.dynamic_ref is not None:
            keyword = "$recursiveRef" if n.dynamic_ref.is_recursive else "$dynamicRef"
            # The resolved target is elided, with no hops in the path.
            target = self.resolve(self.resolve_dynamic(n.dynamic_ref))[0]
            m = self.eval_in_place_child(target, keyword, x, bits, True, False)[0]
            c.evaluated_keyword(m, MATCHED_ALL if m else DID_NOT_MATCH_ALL, keyword)
            ok = ok and m
        if n.all_of is not None:
            all_ok = True
            for i, b in enumerate(n.all_of):
                if not self.eval_in_place_child(b, f"allOf/{i}", x, bits, True)[0]:
                    all_ok = False
            c.evaluated_keyword(all_ok, MATCHED_ALL if all_ok else DID_NOT_MATCH_ALL, "allOf")
            ok = ok and all_ok
        if n.any_of is not None:
            any_ok = False
            for i, b in enumerate(n.any_of):
                if self.eval_in_place_child(b, f"anyOf/{i}", x, bits, False)[0]:
                    any_ok = True
            c.evaluated_keyword(any_ok, MATCHED_AT_LEAST_ONE if any_ok else DID_NOT_MATCH_AT_LEAST_ONE, "anyOf")
            ok = ok and any_ok
        if n.one_of is not None:
            matched = 0
            only: set[Any] | None = None
            for i, b in enumerate(n.one_of):
                # Evaluated properties/items are merged only when exactly one branch matched, so collect them aside.
                r_ok, r_scratch = self.eval_in_place_child(b, f"oneOf/{i}", x, None if bits is None else set(), False)
                if r_ok:
                    matched += 1
                    only = r_scratch
            if matched == 1 and bits is not None and only is not None:
                bits |= only
            message = (
                MATCHED_NO_SCHEMA if matched == 0 else MATCHED_EXACTLY_ONE if matched == 1 else MATCHED_MORE_THAN_ONE
            )
            c.evaluated_keyword(matched == 1, message, "oneOf")
            ok = ok and matched == 1
        if n.not_ >= 0:
            # Not elided, never contributes results or evaluated properties/items.
            c.begin_child_context("not", self.nodes[n.not_].pointer, None)
            inner = self.eval_node(n.not_, x, None)
            c.pop_child_context()
            c.evaluated_keyword(not inner, MATCHED_NOT if inner else DID_NOT_MATCH_NOT, "not")
            ok = ok and not inner
        if n.if_ >= 0:
            cond = self.eval_in_place_child(n.if_, "if", x, bits, False)[0]
            c.evaluated_keyword(True, MATCHED_IF_FOR_THEN if cond else MATCHED_IF_FOR_ELSE, "if")
            if cond and n.then >= 0:
                m = self.eval_in_place_child(n.then, "then", x, bits, True)[0]
                c.evaluated_keyword(m, MATCHED_THEN if m else DID_NOT_MATCH_THEN, "then")
                ok = ok and m
            elif not cond and n.else_ >= 0:
                m = self.eval_in_place_child(n.else_, "else", x, bits, True)[0]
                c.evaluated_keyword(m, MATCHED_ELSE if m else DID_NOT_MATCH_ELSE, "else")
                ok = ok and m
        return ok


def _dependency_message(name: str) -> Message:
    return lambda: f"The value did match the schema applied because it contained the property '{name}'"


def _required_message(present: bool, name: str) -> Message:
    return lambda: f"Required property {'' if present else 'not '}present '{name}'"


# ---------------------------------------------------------------------------------------------------------------------
# Program images: the node graph as JSON, so that standalone modules can collect results without the compiler.

_SKIPPED = frozenset(("one_of_discriminator", "any_of_discriminator", "location"))


def serialize_program(program: CollectingProgram) -> str:
    """Serialises a program for collecting-mode evaluation (custom formats are not included)."""
    defaults = SchemaNode(0, 0, Dialect.DRAFT202012, "", "")
    nodes: list[dict[str, Any]] = []
    for n in program.nodes:
        o: dict[str, Any] = {}
        for f in fields(SchemaNode):
            v = getattr(n, f.name)
            if f.name in _SKIPPED or (
                f.name not in ("id", "resource_id", "dialect", "pointer") and v == getattr(defaults, f.name)
            ):
                continue
            if f.name == "properties":
                o[f.name] = list(v.items())
            elif f.name == "pattern_properties":
                o[f.name] = [[p.pattern, p.node] for p in v]
            elif f.name == "dependencies":
                o[f.name] = [[d.keyword, d.name, d.required, d.schema] for d in v]
            elif f.name == "dynamic_ref":
                o[f.name] = [v.anchor, v.is_recursive, v.fallback, list(v.by_resource.items())]
            elif f.name == "annotations":
                o[f.name] = [[a.keyword, a.value, a.strings_only] for a in v]
            else:
                o[f.name] = int(v) if f.name == "dialect" else v
        nodes.append(o)
    image = {
        "root": program.root,
        "usesDynamicScope": program.uses_dynamic_scope,
        "maxDepth": program.max_depth,
        "nodes": nodes,
    }
    return json.dumps(image, ensure_ascii=False, separators=(",", ":"))


def load_program(image: str) -> CollectingProgram:
    """Loads a program image produced by ``serialize_program``."""
    from .node import AnnotationEntry

    data = json.loads(image)
    nodes: list[SchemaNode] = []
    for o in data["nodes"]:
        n = SchemaNode(o["id"], o["resource_id"], Dialect(o["dialect"]), "", o["pointer"])
        for k, v in o.items():
            if k in ("id", "resource_id", "dialect", "pointer"):
                continue
            if k == "properties":
                v = {name: node for name, node in v}
            elif k == "pattern_properties":
                v = [PatternProperty(p, node) for p, node in v]
            elif k == "dependencies":
                v = [DependencyEntry(kw, name, required, schema) for kw, name, required, schema in v]
            elif k == "dynamic_ref":
                v = DynamicRefTarget(v[0], v[1], v[2], {r: t for r, t in v[3]})
            elif k == "annotations":
                v = [AnnotationEntry(kw, value, strings_only) for kw, value, strings_only in v]
            setattr(n, k, v)
        nodes.append(n)
    return CollectingProgram(nodes, data["root"], data["usesDynamicScope"], data["maxDepth"])
