"""Generates specialised Python from a compiled schema graph.

A port of the TypeScript evaluator's code generator. Each node becomes one function whose body is exactly the checks
that node needs (the C# evaluator's plan, unrolled into straight-line code). The analyses that pick C# plans pick the
shape of the emitted code here: pure-$ref elision, type-only children tested inline, discriminator and type dispatch
for oneOf/anyOf, flat object compositions merged into one section, unevaluatedProperties decided statically (with
guards for conditional contributors), dynamic scope only where a $dynamicRef stays dynamic, and evaluated-property/item
tracking only where unevaluatedProperties/unevaluatedItems consume it.

Where Python differs from JavaScript the code takes Python's fastest form: a type test is ``type(x) is dict`` (with
``type(x)`` taken once per function), property names are tested with set operations on ``x.keys()`` where the
JavaScript loops, and a large set of declared properties dispatches through a dict of check functions.
"""

from __future__ import annotations

import json
import math
import os
import re
from collections.abc import Callable, Iterable
from dataclasses import dataclass, field
from typing import Any

from .compiler import CompiledSchema
from .dialect import Dialect
from .formats import FORMAT_VALIDATORS, is_numeric_format
from .node import (
    CONTENT_BASE64,
    CONTENT_JSON,
    T_ALL,
    T_ARRAY,
    T_BOOLEAN,
    T_INTEGER,
    T_NULL,
    T_NUMBER,
    T_OBJECT,
    T_STRING,
    Discriminator,
    SchemaNode,
)
from .options import SchemaCompilationError
from .pattern import compile_pattern

KINDS = ("string", "number", "object", "array", "boolean", "null")

REF_START = "\x01"
REF_END = "\x02"
_REF_RE = re.compile("\x01([^\x02]+)\x02")


def _env(name: str, fallback: int) -> int:
    v = os.environ.get(name)
    return fallback if v is None else int(v)


# Objects with at most this many declared properties (or only required ones) are checked by direct lookups.
MAX_PROBED_PROPERTIES = _env("CORVUS_PY_PROBE", 8)
# Required-only objects are probed up to this many properties.
MAX_PROBED_REQUIRED = _env("CORVUS_PY_PROBE_REQUIRED", 32)
# The number of declared names above which property dispatch goes through a dict of check functions.
MAX_CHAIN_NAMES = _env("CORVUS_PY_CHAIN", 4)
# Required names above which presence is tested as one superset test.
MAX_REQUIRED_TESTS = _env("CORVUS_PY_REQUIRED_TESTS", 3)
# At most this many guarded coverages decide unevaluatedProperties statically; more falls back to tracking.
MAX_GUARDED_COVERAGES = 32
# A guarded coverage with more names than this tests them with a set.
MAX_GUARD_NAMES = 6
# A leaf schema with at most this many keywords to test is tested at its call site instead of by a call.
MAX_INLINE_CONDITIONS = _env("CORVUS_PY_INLINE", 6)
# Dispatch tables map a schema that accepts everything to True (no call) rather than to its function.
TRIVIAL_ENTRIES = _env("CORVUS_PY_TRIVIAL_ENTRIES", 1) != 0
# Enums of more values than this are tested by set lookup.
MAX_CHAINED_VALUES = _env("CORVUS_PY_CHAINED_VALUES", 3)


@dataclass
class Coverage:
    names: set[str] = field(default_factory=set)
    patterns: list[str] = field(default_factory=list)
    prefix: int = 0
    all: bool = False


@dataclass
class GuardedCoverage:
    """A coverage that applies only when every guard (a Python expression over the instance ``x``) holds."""

    guards: list[str]
    coverage: Coverage


@dataclass
class GeneratedCode:
    declarations: str
    """Constant and function definitions; ``root`` names the entry function."""
    root: str
    custom_formats: list[Callable[[str], bool]]
    """Custom format functions referenced as ``F[i]`` (only when custom formats are used)."""
    uses_dynamic_scope: bool
    uses_depth: bool
    root_resource: int
    max_depth: int


def kind_allowed(mask: int, kind: str) -> bool:
    if kind == "null":
        return (mask & T_NULL) != 0
    if kind == "boolean":
        return (mask & T_BOOLEAN) != 0
    if kind == "object":
        return (mask & T_OBJECT) != 0
    if kind == "array":
        return (mask & T_ARRAY) != 0
    if kind == "number":
        return (mask & (T_NUMBER | T_INTEGER)) != 0
    return (mask & T_STRING) != 0


def _is_primitive(x: Any) -> bool:
    t = type(x)
    return x is None or t is str or t is bool or t is int or (t is float and math.isfinite(x))


def lit(v: Any) -> str:
    """A Python literal for a JSON scalar."""
    t = type(v)
    if v is None or t is bool or t is int:
        return repr(v)
    if t is float:
        if math.isfinite(v):
            return repr(v)
        return "R.INF" if v > 0 else "(-R.INF)"
    if t is str:
        return repr(v)
    raise TypeError(t)


def eq_test(v: str, value: Any) -> str:
    """A test that ``v`` is JSON-equal to the primitive ``value``."""
    if value is None:
        return f"{v} is None"
    if value is True or value is False:
        return f"{v} is {value!r}"
    if type(value) is str:
        return f"{v} == {lit(value)}"
    # Numbers: Python's equality is JSON's, except that True == 1 and False == 0.
    if value == 1:
        return f"({v} == 1 and {v} is not True)"
    if value == 0:
        return f"({v} == 0 and {v} is not False)"
    return f"{v} == {lit(value)}"


def indent(lines: Iterable[str], depth: int) -> list[str]:
    pad = "    " * depth
    return [pad + line for line in lines]


_SIMPLE_RE = re.compile(r"^[\w.\x01\x02]+\([^()]*\)$|^[\w.]+$")


def _balanced(expr: str) -> bool:
    depth = 0
    for i, ch in enumerate(expr):
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0 and i < len(expr) - 1:
                return False
    return depth == 0


def wrap(expr: str) -> str:
    """The expression parenthesised unless it is a name, a simple call or already parenthesised."""
    if _SIMPLE_RE.match(expr) or (expr.startswith("(") and _balanced(expr)):
        return expr
    return f"({expr})"


def push(out: list[str], check: str) -> None:
    if check == "True":
        return
    if check == "False":
        out.append("return False")
    else:
        out.append(f"if not {wrap(check)}: return False")


class CodeGenerator:
    def __init__(self, program: CompiledSchema) -> None:
        self.program = program
        self.nodes = program.nodes
        self.templates: dict[str, str] = {}
        self.queue: list[str] = []
        self.constants: dict[str, str] = {}
        self.custom_formats: list[Callable[[str], bool]] = []
        self.custom_format_index: dict[int, int] = {}
        self.uses_depth = False
        self.tmp = 0
        # Whether the function being generated tests ``tx`` (``type(x)``, taken once at its start).
        self.uses_tx = False
        self._elided: dict[int, int] = {}

    def generate(self) -> GeneratedCode:
        root_name = self.request(self.entry_name(self.program.root))
        q = 0
        while q < len(self.queue):
            name = self.queue[q]
            q += 1
            self.templates[name] = self.generate_function(name)
        declarations, root = self.link(root_name)
        return GeneratedCode(
            declarations=declarations,
            root=root,
            custom_formats=self.custom_formats,
            uses_dynamic_scope=self.program.uses_dynamic_scope,
            uses_depth=self.uses_depth,
            root_resource=self.program.root_resource,
            max_depth=self.program.options.max_depth,
        )

    # -------------------------------------------------------------------------------------------------------------
    # Names and references

    def entry_name(self, node_id: int) -> str:
        return "v" + str(self.elide(node_id))

    def request(self, name: str) -> str:
        if name not in self.templates:
            self.templates[name] = ""
            self.queue.append(name)
        return name

    def ref(self, name: str) -> str:
        return REF_START + self.request(name) + REF_END

    def constant(self, init: str) -> str:
        name = self.constants.get(init)
        if name is None:
            name = "c" + str(len(self.constants))
            self.constants[init] = name
        return name

    def elide(self, node_id: int) -> int:
        """Follows pure-$ref chains (Blaze's jump-target inlining); not across resources when a dynamic scope is
        kept."""
        cached = self._elided.get(node_id)
        if cached is not None:
            return cached
        n = self.nodes[node_id]
        for _ in range(64):
            if not n.is_pure_ref or n.in_place_cycle:
                break
            nxt = self.nodes[n.ref]
            if self.program.uses_dynamic_scope and nxt.resource_id != n.resource_id:
                break
            n = nxt
        self._elided[node_id] = n.id
        return n.id

    def type_of(self, v: str) -> str:
        if v == "x":
            self.uses_tx = True
            return "tx"
        return f"type({v})"

    def kind_test(self, kind: str, v: str) -> str:
        t = self.type_of(v)
        if kind == "null":
            return f"{v} is None"
        if kind == "boolean":
            return f"{t} is bool"
        if kind == "object":
            return f"{t} is dict"
        if kind == "array":
            return f"{t} is list"
        if kind == "number":
            return f"({t} is int or {t} is float)"
        return f"{t} is str"

    def type_expr(self, mask: int, v: str, dialect: Dialect) -> str:
        """A boolean expression that is true when ``v`` has one of the types in the mask. A float with no fractional
        part is an integer from draft 6; in draft 4 an integer is a number written without one, which Python keeps
        apart (``json.loads`` gives ``1.0`` a float)."""
        t = self.type_of(v)
        types: list[str] = []
        if mask & T_STRING:
            types.append("str")
        if mask & T_NUMBER:
            types.extend(("int", "float"))
        elif mask & T_INTEGER:
            types.append("int")
        if mask & T_OBJECT:
            types.append("dict")
        if mask & T_ARRAY:
            types.append("list")
        if mask & T_BOOLEAN:
            types.append("bool")
        if mask & T_NULL:
            types.append("NoneType")
        parts: list[str] = []
        if types == ["NoneType"]:
            parts.append(f"{v} is None")
        elif len(types) == 1:
            parts.append(f"{t} is {types[0]}")
        elif len(types) > 1:
            parts.append(f"{t} in {self.constant('(' + ', '.join(types) + ')')}")
        if (mask & T_INTEGER) and not (mask & T_NUMBER) and dialect != Dialect.DRAFT4:
            parts.append(f"({t} is float and {v}.is_integer())")
        if not parts:
            return "False"
        return parts[0] if len(parts) == 1 else "(" + " or ".join(parts) + ")"

    def call(self, node_id: int, v: str, source: int, e: str | None = None) -> str:
        """An expression that evaluates child ``node_id`` against ``v`` from a node in resource ``source``. ``e`` is
        the evaluated set to mark (tracking variant) or None (flag variant)."""
        target = self.elide(node_id)
        n = self.nodes[target]
        if n.always_true:
            return "True"
        if n.always_false:
            return "False"
        crosses = self.program.uses_dynamic_scope and n.resource_id != source
        if e is None and not crosses and n.is_type_only:
            return self.type_expr(n.type, v, n.dialect)
        if e is None and not crosses and self.is_inline_leaf(n):
            return self.inline_leaf(n, v)
        variant = "v" if e is None else "t"
        name = ("s" + variant + str(target)) if crosses else (variant + str(target))
        return f"{self.ref(name)}({v}{'' if e is None else ', ' + e})"

    def function_ref(self, node_id: int, source: int) -> str:
        """A reference to the flag function of child ``node_id`` (for dispatch tables), or 'True' for a schema that
        accepts everything (no call needed)."""
        target = self.elide(node_id)
        n = self.nodes[target]
        if n.always_true or (TRIVIAL_ENTRIES and self.call(node_id, "v", source) == "True"):
            return "True"
        if n.always_false:
            return "R.never"
        crosses = self.program.uses_dynamic_scope and n.resource_id != source
        return self.ref(("sv" if crosses else "v") + str(target))

    def is_inline_leaf(self, n: SchemaNode) -> bool:
        """A leaf (type, const/enum of primitives, string and number keywords, nothing that applies a subschema)
        small enough to test at the call site, without a call."""
        if n.has_object_keywords or n.has_array_keywords or n.has_in_place_applicators or n.in_place_cycle:
            return False
        if n.has_const and not _is_primitive(n.const_value):
            return False
        if n.enum_values is not None and not all(map(_is_primitive, n.enum_values)):
            return False
        conditions = len(self.string_conditions_count(n)) + n.has_const + (n.enum_values is not None)
        numbers = sum(
            x is not None for x in (n.minimum, n.maximum, n.exclusive_minimum, n.exclusive_maximum, n.multiple_of)
        )
        return conditions + numbers <= MAX_INLINE_CONDITIONS

    def string_conditions_count(self, n: SchemaNode) -> list[bool]:
        """The string keywords present (without generating their tests)."""
        return [
            k
            for k in (
                n.min_length > 0,
                n.max_length >= 0,
                n.pattern is not None,
                n.assert_format and n.format is not None and not is_numeric_format(n.format_kind),
                n.assert_content,
            )
            if k
        ]

    def inline_leaf(self, n: SchemaNode, v: str) -> str:
        """A leaf's keywords as one expression over ``v``: per kind, its type test and the conditions for that kind,
        then const/enum."""
        sections = {
            "string": self.string_conditions(n, v),
            "number": [c for c in (self.integer_condition(n, v),) if c] + self.number_conditions(n, v),
        }
        parts: list[str] = []
        if n.has_type:
            clauses = []
            for kind in KINDS:
                if not kind_allowed(n.type, kind):
                    continue
                test = self.kind_test(kind, v)
                conditions = sections.get(kind, [])
                clauses.append(test if not conditions else "(" + " and ".join([test, *conditions]) + ")")
            if not clauses:
                return "False"
            parts.append(clauses[0] if len(clauses) == 1 else "(" + " or ".join(clauses) + ")")
        else:
            for kind, conditions in sections.items():
                if conditions:
                    parts.append(
                        f"(not {wrap(self.kind_test(kind, v))} or " + " and ".join(map(wrap, conditions)) + ")"
                    )
        if n.has_const:
            parts.append(eq_test(v, n.const_value))
        if n.enum_values is not None:
            parts.append(self.enum_test(n.enum_values, v))
        if not parts:
            return "True"
        return parts[0] if len(parts) == 1 else "(" + " and ".join(parts) + ")"

    def enum_test(self, values: list[Any], v: str) -> str:
        """A test that ``v`` is one of a list of primitives: a set lookup for more than a few strings (a string is
        hashable, which an object or array instance is not), a chain of tests otherwise."""
        if not values:
            return "False"
        if len(values) > MAX_CHAINED_VALUES and all(type(x) is str for x in values):
            return f"({self.type_of(v)} is str and {v} in {self.constant(f'frozenset({self.json_constant(values)})')})"
        if len(values) > MAX_CHAINED_VALUES:
            return f"R.has({self.constant(f'R.key_set({self.json_constant(values)})')}, {v})"
        return "(" + " or ".join(eq_test(v, x) for x in values) + ")"

    def scratch(self, prefix: str) -> str:
        name = prefix + str(self.tmp)
        self.tmp += 1
        return name

    # -------------------------------------------------------------------------------------------------------------
    # Functions

    def generate_function(self, name: str) -> str:
        kind = name[0]
        if kind == "s":
            variant = name[1]
            node_id = int(name[2:])
            n = self.nodes[node_id]
            params = "x" if variant == "v" else "x, ev"
            return (
                f"def @({params}):\n    DS.append({n.resource_id})\n    r = {self.ref(variant + str(node_id))}({params})\n"
                "    DS.pop()\n    return r"
            )
        if kind == "d":
            return self.generate_dynamic_resolver(int(name[2:]), name[1])
        if kind == "b":
            return self.generate_node_function(int(name[2:]), name[1])
        variant = kind
        node_id = int(name[1:])
        n = self.nodes[node_id]
        if n.in_place_cycle:
            self.uses_depth = True
            params = "x" if variant == "v" else "x, ev"
            return (
                f"def @({params}):\n    global depth\n    depth += 1\n    if depth > MAXDEPTH: R.depth_exceeded()\n"
                f"    r = {self.ref('b' + variant + str(node_id))}({params})\n    depth -= 1\n    return r"
            )
        return self.generate_node_function(node_id, variant)

    def generate_dynamic_resolver(self, node_id: int, variant: str) -> str:
        n = self.nodes[node_id]
        d = n.dynamic_ref
        assert d is not None
        e = "ev" if variant == "t" else None
        self.uses_tx = False
        lines = ["for r in DS:"]
        first = True
        for resource, target in d.by_resource.items():
            lines.append(
                f"    {'if' if first else 'elif'} r == {resource}: return {self.call(target, 'x', n.resource_id, e)}"
            )
            first = False
        lines.append(f"return {self.call(d.fallback, 'x', n.resource_id, e)}")
        if self.uses_tx:
            lines.insert(0, "tx = type(x)")
        return f"def @(x{', ev' if e else ''}):\n" + "\n".join(indent(lines, 1))

    def generate_node_function(self, node_id: int, variant: str) -> str:
        n = self.nodes[node_id]
        # Scratch names restart per function so that structurally identical functions produce identical text.
        self.tmp = 0
        self.uses_tx = False
        tracking = variant == "t" or n.unevaluated_properties >= 0 or n.unevaluated_items >= 0
        body = self.tracking_body(n, variant) if tracking else self.flag_body(n)
        if self.uses_tx:
            body.insert(0, "tx = type(x)")
        params = "x" if variant == "v" else "x, ev"
        return f"def @({params}):\n" + "\n".join(indent(body, 1))

    def flag_body(self, n: SchemaNode) -> list[str]:
        """Flag mode: type-directed blocks, then const/enum, then in-place applicators."""
        out: list[str] = []
        if n.always_false:
            return ["return False"]
        if n.always_true:
            return ["return True"]
        flat = self.flat_object(n)
        if flat is not None:
            # Objects take the merged section and return; the code below still serves every other kind.
            out.append(f"if {self.kind_test('object', 'x')}:")
            out.extend(indent([*self.object_section(flat, None, True), "return True"], 1))
        out.extend(self.kind_blocks(n, lambda kind: self.kind_section(n, kind, None, True)))
        out.extend(self.const_enum(n))
        out.extend(self.in_place(n, None, None))
        out.append("return True")
        return out

    def flat_object(self, n: SchemaNode) -> SchemaNode | None:
        """A flat composition, for object values: a node whose in-place applicators are ``$ref``/``allOf`` chains,
        where the node and every branch are plain object schemas (declared properties, ``required`` and count bounds,
        with no type that excludes objects) and each property name resolves to one schema. Returns a node holding the
        merged object keywords, so an object takes one object section instead of a call per branch (the C# and Rust
        flat fused plan). None when the node does not qualify, or when fewer than two branches have object keywords.

        Below a live dynamic reference every branch must be in the node's own resource: calling a branch in another
        resource pushes that resource on the dynamic scope, which the merged section would skip.
        """
        if n.ref < 0 and n.static_dynamic_ref < 0 and n.all_of is None:
            return None
        branches: list[SchemaNode] = []
        visited: set[int] = set()

        def collect(m: SchemaNode) -> bool:
            if m.id in visited:
                return True
            visited.add(m.id)
            if m.always_true:
                return True
            if m.always_false or m.in_place_cycle:
                return False
            if self.program.uses_dynamic_scope and m.resource_id != n.resource_id:
                return False
            if m.has_const or m.enum_values is not None or (m.has_type and not kind_allowed(m.type, "object")):
                return False
            if (
                m.pattern_properties is not None
                or m.additional_properties >= 0
                or m.property_names >= 0
                or m.dependencies is not None
            ):
                return False
            if m.unevaluated_properties >= 0 or m.unevaluated_items >= 0:
                return False
            if m.dynamic_ref is not None or m.any_of is not None or m.one_of is not None or m.not_ >= 0 or m.if_ >= 0:
                return False
            branches.append(m)
            for c in [m.ref, m.static_dynamic_ref, *(m.all_of or ())]:
                if c >= 0 and not collect(self.nodes[self.elide(c)]):
                    return False
            return True

        if not collect(n):
            return None
        effective = [
            b
            for b in branches
            if b.properties is not None
            or (b.required is not None and len(b.required) > 0)
            or b.min_properties >= 0
            or b.max_properties >= 0
        ]
        if len(effective) < 2:
            return None

        def same(a: int, b: int) -> bool:
            # Two branches' schemas for a name are the same check when they are one node, or type-only tests of one
            # type.
            x = self.nodes[self.elide(a)]
            y = self.nodes[self.elide(b)]
            return x.id == y.id or (x.is_type_only and y.is_type_only and x.type == y.type and x.dialect == y.dialect)

        merged = SchemaNode(n.id, n.resource_id, n.dialect, n.location, n.pointer)
        properties: dict[str, int] = {}
        required: list[str] = []
        for b in branches:
            for name, child in (b.properties or {}).items():
                existing = properties.get(name)
                if existing is None or self.nodes[self.elide(existing)].always_true:
                    properties[name] = child
                elif not same(existing, child) and not self.nodes[self.elide(child)].always_true:
                    return None
            for name in b.required or ():
                if name not in required:
                    required.append(name)
            merged.min_properties = max(merged.min_properties, b.min_properties)
            if b.max_properties >= 0:
                merged.max_properties = (
                    b.max_properties if merged.max_properties < 0 else min(merged.max_properties, b.max_properties)
                )
        if properties:
            merged.properties = properties
        if required:
            merged.required = required
        return merged

    def kind_blocks(self, n: SchemaNode, section: Callable[[str], list[str]]) -> list[str]:
        """Emits the per-kind sections. Kinds with work get a branch; allowed kinds without work fall through; a type
        restriction fails every other kind."""
        mask = n.type if n.has_type else T_ALL
        allowed = [k for k in KINDS if kind_allowed(mask, k)]
        with_work = [(k, lines) for k in allowed for lines in (section(k),) if lines]
        out: list[str] = []
        if not allowed:
            return ["return False"]
        if n.has_type and len(allowed) == 1:
            out.append(f"if not {wrap(self.kind_test(allowed[0], 'x'))}: return False")
            if with_work:
                out.extend(with_work[0][1])
            return out
        rest_ok = [k for k in allowed if not any(w == k for w, _ in with_work)]
        for i, (k, lines) in enumerate(with_work):
            out.append(f"{'if' if i == 0 else 'elif'} {self.kind_test(k, 'x')}:")
            out.extend(indent(lines, 1))
        restricted = n.has_type and len(allowed) < len(KINDS)
        if with_work:
            if restricted:
                if not rest_ok:
                    out.append("else:")
                else:
                    out.append(f"elif not ({' or '.join(self.kind_test(k, 'x') for k in rest_ok)}):")
                out.append("    return False")
        elif restricted:
            out.append(f"if not ({' or '.join(self.kind_test(k, 'x') for k in rest_ok)}): return False")
        return out

    def kind_section(self, n: SchemaNode, kind: str, e: str | None, flag_layout: bool) -> list[str]:
        integer_only = n.has_type and (n.type & T_INTEGER) != 0 and (n.type & T_NUMBER) == 0
        if kind == "object":
            return self.object_section(n, e, flag_layout)
        if kind == "array":
            return self.array_section(n, e)
        if kind == "string":
            return self.string_section(n)
        if kind == "number":
            lines: list[str] = []
            if integer_only:
                test = "" if n.dialect == Dialect.DRAFT4 else " and not x.is_integer()"
                lines.append(f"if {self.type_of('x')} is float{test}: return False")
            return [*lines, *self.number_section(n)]
        return []

    def json_constant(self, value: Any) -> str:
        return self.constant(f"JSON.loads({json.dumps(json.dumps(value, ensure_ascii=False), ensure_ascii=False)})")

    def const_enum(self, n: SchemaNode) -> list[str]:
        out: list[str] = []
        if n.has_const:
            if _is_primitive(n.const_value):
                out.append(f"if not {wrap(eq_test('x', n.const_value))}: return False")
            else:
                out.append(f"if not R.equal(x, {self.json_constant(n.const_value)}): return False")
        if n.enum_values is not None:
            values = n.enum_values
            if not values:
                out.append("return False")
            elif all(map(_is_primitive, values)):
                out.append(f"if not {wrap(self.enum_test(values, 'x'))}: return False")
            else:
                out.append(f"if not R.includes({self.json_constant(values)}, x): return False")
        return out

    # -------------------------------------------------------------------------------------------------------------
    # Objects

    def object_section(self, n: SchemaNode, e: str | None, flag_layout: bool) -> list[str]:
        out: list[str] = []
        props = n.properties
        required = n.required or []
        # A pattern property whose schema is `true` does nothing unless marking or additionalProperties needs it.
        all_patterns = n.pattern_properties or []
        additional_needs_match = (
            n.additional_properties >= 0 and not self.nodes[self.elide(n.additional_properties)].always_true
        )
        patterns = (
            all_patterns
            if e is not None or additional_needs_match
            else [p for p in all_patterns if not self.nodes[self.elide(p.node)].always_true]
        )
        ap = self.nodes[self.elide(n.additional_properties)] if n.additional_properties >= 0 else None
        pn = self.nodes[self.elide(n.property_names)] if n.property_names >= 0 else None
        ap_needs_loop = ap is not None and not ap.always_true
        pn_needs_loop = pn is not None and not pn.always_true
        prop_count = len(props) if props is not None else 0
        all_required = props is None or all(k in required for k in props)
        probeable = prop_count <= MAX_PROBED_PROPERTIES or (all_required and prop_count <= MAX_PROBED_REQUIRED)

        # Counts and required names first: they fail fast without looking at any value.
        if n.min_properties > 0:
            out.append(f"if len(x) < {n.min_properties}: return False")
        if n.max_properties >= 0:
            out.append(f"if len(x) > {n.max_properties}: return False")
        out.extend(self.required_test("x", required))

        simple = e is None and not patterns and not pn_needs_loop
        if simple and not ap_needs_loop and probeable:
            out.extend(self.object_probe(n, props, required))
        elif simple and ap is not None and ap.always_false and probeable:
            # additionalProperties: false: the names must all be declared (one C-level subset test), then each declared
            # name is probed.
            if props:
                out.append(
                    f"if not {self.constant(f'frozenset({self.json_constant(list(props))})')}.issuperset(x): return False"
                )
            else:
                out.append("if x: return False")
            out.extend(self.object_probe(n, props, required))
        elif simple and prop_count == 0 and ap is not None and ap_needs_loop:
            out.extend(self.object_values(n, ap))
        else:
            out.extend(self.object_loop(n, e, patterns, ap, pn, ap_needs_loop, pn_needs_loop))

        if n.dependencies:
            out.extend(self.object_dependencies(n, e, flag_layout))
        return out

    def required_test(self, v: str, required: list[str]) -> list[str]:
        if not required:
            return []
        if len(required) <= MAX_REQUIRED_TESTS:
            return [f"if {' or '.join(f'{lit(r)} not in {v}' for r in required)}: return False"]
        names = self.constant(f"frozenset({self.json_constant(required)})")
        return [f"if not {v}.keys() >= {names}: return False"]

    def object_probe(self, n: SchemaNode, props: dict[str, int] | None, required: list[str]) -> list[str]:
        """Each declared name looked up directly (required names are already known to be present)."""
        out: list[str] = []
        required_set = set(required)
        for name, child in (props or {}).items():
            check = self.call(child, "p", n.resource_id)
            if check == "True":
                continue
            key = lit(name)
            if check == "False":
                out.append(f"if {key} in x: return False")
            elif name in required_set:
                out.append(f"p = x[{key}]")
                out.append(f"if not {wrap(check)}: return False")
            else:
                out.append(f"if {key} in x:")
                out.append(f"    p = x[{key}]")
                out.append(f"    if not {wrap(check)}: return False")
        return out

    def object_values(self, n: SchemaNode, ap: SchemaNode) -> list[str]:
        """A map: only the values matter."""
        check = self.call(ap.id, "v", n.resource_id)
        if check == "False":
            return ["if x: return False"]
        return ["for v in x.values():", f"    if not {wrap(check)}: return False"]

    def object_loop(
        self,
        n: SchemaNode,
        e: str | None,
        patterns: list[Any],
        ap: SchemaNode | None,
        pn: SchemaNode | None,
        ap_needs_loop: bool,
        pn_needs_loop: bool,
    ) -> list[str]:
        """One pass over the instance's properties: names dispatched to their subschemas, then patterns and
        additional."""
        out: list[str] = []
        props = n.properties or {}
        names = list(props)
        needs_value = bool(names) or bool(patterns) or ap_needs_loop
        out.append("for k, v in x.items():" if needs_value else "for k in x:")
        body: list[str] = []
        if pn_needs_loop:
            assert pn is not None
            body.append(f"if not {wrap(self.call(pn.id, 'k', n.resource_id))}: return False")
        mark = f"{e}.add(k)" if e is not None else None
        has_ap_tail = ap is not None and (ap_needs_loop or mark is not None)
        # When nothing follows the declared names (no patterns), a declared name continues the loop.
        matched = self.scratch("m") if patterns and has_ap_tail else None
        if matched:
            body.append(f"{matched} = False")
        if names:
            continue_after = not patterns

            def case_body(name: str) -> list[str]:
                lines: list[str] = []
                check = self.call(props[name], "v", n.resource_id)
                if check != "True":
                    lines.append(f"if not {wrap(check)}: return False")
                if mark:
                    lines.append(mark)
                if matched:
                    lines.append(f"{matched} = True")
                if continue_after and (has_ap_tail):
                    lines.append("continue")
                if not lines:
                    lines.append("pass")
                return lines

            if len(names) <= MAX_CHAIN_NAMES:
                for i, name in enumerate(names):
                    body.append(f"{'if' if i == 0 else 'elif'} k == {lit(name)}:")
                    body.extend(indent(case_body(name), 1))
            else:
                # A dict from names to check functions: one hash lookup instead of a chain of comparisons.
                table, trivial = self.dispatch_table(n, names)
                body.append(f"f = {table}.get(k)")
                body.append("if f is not None:")
                lines: list[str] = [
                    "if f is not True and not f(v): return False" if trivial else "if not f(v): return False"
                ]
                if mark:
                    lines.append(mark)
                if matched:
                    lines.append(f"{matched} = True")
                if continue_after and has_ap_tail:
                    lines.append("continue")
                body.extend(indent(lines, 1))
        for p in patterns:
            check = self.call(p.node, "v", n.resource_id)
            lines = []
            if check != "True":
                lines.append(f"if not {wrap(check)}: return False")
            if mark:
                lines.append(mark)
            if matched:
                lines.append(f"{matched} = True")
            if lines:
                body.append(f"if {self.pattern_test(p.pattern, 'k')}:")
                body.extend(indent(lines, 1))
        if ap is not None and (ap_needs_loop or mark):
            lines = []
            check = self.call(ap.id, "v", n.resource_id)
            if check == "False":
                lines.append("return False")
            elif check != "True":
                lines.append(f"if not {wrap(check)}: return False")
            if mark:
                lines.append(mark)
            if lines:
                if matched:
                    body.append(f"if not {matched}:")
                    body.extend(indent(lines, 1))
                elif names and not patterns:
                    # Declared names continued above; what reaches here is additional.
                    body.extend(lines)
                else:
                    body.extend(lines)
        if not body:
            return []
        out.extend(indent(body, 1))
        return out

    def dispatch_table(self, n: SchemaNode, names: list[str]) -> tuple[str, bool]:
        """A module-level dict from declared names to their check functions (``True`` for a schema that accepts
        everything), defined after the functions; and whether any entry is ``True``."""
        assert n.properties is not None
        refs = [self.function_ref(n.properties[name], n.resource_id) for name in names]
        entries = ", ".join(f"{lit(name)}: {ref}" for name, ref in zip(names, refs, strict=True))
        return self.constant("{" + entries + "}"), "True" in refs

    def object_dependencies(self, n: SchemaNode, e: str | None, flag_layout: bool) -> list[str]:
        """Dependencies: required lists always; schemas here only in the flag layout (tracking evaluates them in
        place)."""
        out: list[str] = []
        for d in n.dependencies or []:
            lines: list[str] = []
            lines.extend(self.required_test("x", d.required or []))
            if d.schema is not None and flag_layout and e is None:
                check = self.call(d.schema, "x", n.resource_id)
                if check != "True":
                    lines.append(f"if not {wrap(check)}: return False")
            if lines:
                out.append(f"if {lit(d.name)} in x:")
                out.extend(indent(lines, 1))
        return out

    # -------------------------------------------------------------------------------------------------------------
    # Arrays

    def array_section(self, n: SchemaNode, e: str | None) -> list[str]:
        out: list[str] = []
        prefix = n.prefix_items or []
        has_length = n.min_items > 0 or n.max_items >= 0 or prefix or n.items >= 0 or n.contains >= 0 or e is not None
        if not has_length and not n.unique_items:
            return out
        length = self.scratch("n")
        out.append(f"{length} = len(x)")
        if n.min_items > 0:
            out.append(f"if {length} < {n.min_items}: return False")
        if n.max_items >= 0:
            out.append(f"if {length} > {n.max_items}: return False")
        for i, child in enumerate(prefix):
            check = self.call(child, f"x[{i}]", n.resource_id)
            if check == "True":
                continue
            out.append(f"if {length} > {i} and not {wrap(check)}: return False")
        if e is not None and prefix:
            out.append(f"{e}.update(range(min({length}, {len(prefix)})))")
        if n.items >= 0:
            item = self.nodes[self.elide(n.items)]
            if item.always_false:
                out.append(f"if {length} > {len(prefix)}: return False")
            else:
                check = self.call(item.id, "v", n.resource_id)
                if check != "True":
                    source = "x" if not prefix else f"x[{len(prefix)}:]"
                    out.append(f"for v in {source}:")
                    out.append(f"    if not {wrap(check)}: return False")
                if e is not None:
                    out.append(f"{e}.update(range({len(prefix)}, {length}))")
        if n.contains >= 0:
            check = self.call(n.contains, "v", n.resource_id)
            lo = n.min_contains
            hi = n.max_contains
            mark_contains = e is not None and n.contains_marks_evaluated
            if lo <= 0 and hi < 0 and not mark_contains:
                pass  # minContains 0 and no maximum: contains always holds.
            else:
                c = self.scratch("c")
                out.append(f"{c} = 0")
                out.append("for i, v in enumerate(x):" if mark_contains else "for v in x:")
                out.append(f"    if {check}:")
                out.append(f"        {c} += 1")
                if mark_contains:
                    out.append(f"        {e}.add(i)")
                if hi < 0 and not mark_contains:
                    out.append(f"        if {c} >= {lo}: break")
                if lo > 0:
                    out.append(f"if {c} < {lo}: return False")
                if hi >= 0:
                    out.append(f"if {c} > {hi}: return False")
        if n.unique_items:
            if self.items_hash_as_json(n):
                # Every item passed its schema above, so all are strings, numbers or null: Python's equality and
                # hashing are JSON's for them.
                out.append(f"if {length} > 1 and len(set(x)) != {length}: return False")
            else:
                out.append(f"if {length} > 1 and not R.unique(x): return False")
        return out

    def items_hash_as_json(self, n: SchemaNode) -> bool:
        """Whether every item of an array this node accepts is a string, a number or null (by its items schema)."""
        if n.prefix_items or n.items < 0:
            return False
        item = self.nodes[self.elide(n.items)]
        if item.has_type and item.type & (T_OBJECT | T_ARRAY | T_BOOLEAN) == 0:
            return True
        return item.enum_values is not None and all(type(v) is str for v in item.enum_values)

    # -------------------------------------------------------------------------------------------------------------
    # Strings and numbers

    def string_conditions(self, n: SchemaNode, v: str) -> list[str]:
        """The string keywords as conditions on ``v`` (a string), each true when it holds."""
        out: list[str] = []
        if n.min_length > 0:
            out.append(f"len({v}) >= {n.min_length}")
        if n.max_length >= 0:
            out.append(f"len({v}) <= {n.max_length}")
        if n.pattern is not None:
            out.append(self.pattern_test(n.pattern, v))
        if n.assert_format and n.format is not None and not is_numeric_format(n.format_kind):
            f = self.format_function(n.format, n.format_kind, n.dialect)
            if f is not None:
                out.append(f"{f}({v})")
        if n.assert_content:
            kind = 1 if n.content == CONTENT_BASE64 else 2 if n.content == CONTENT_JSON else 3
            out.append(f"R.content({v}, {kind})")
        return out

    def number_conditions(self, n: SchemaNode, v: str) -> list[str]:
        """The number keywords as conditions on ``v`` (an int or a float), each true when it holds."""
        out: list[str] = []
        if n.assert_format and is_numeric_format(n.format_kind) and n.format not in self.program.options.formats:
            f = self.constant(f"R.NUMERIC_FORMAT_VALIDATORS[{lit(n.format_kind)}]")
            out.append(f"{f}({v})")
        if n.minimum is not None:
            out.append(f"{v} >= {lit(n.minimum)}")
        if n.maximum is not None:
            out.append(f"{v} <= {lit(n.maximum)}")
        if n.exclusive_minimum is not None:
            out.append(f"{v} > {lit(n.exclusive_minimum)}")
        if n.exclusive_maximum is not None:
            out.append(f"{v} < {lit(n.exclusive_maximum)}")
        if n.multiple_of is not None:
            d = n.multiple_of
            if type(d) is int and d != 0:
                # An integer divisor: the remainder is exact for integers and floats alike (fmod is exact).
                out.append(f"not {v} % {d}")
            else:
                out.append(f"R.multiple_of({v}, {lit(d)})")
        return out

    def integer_condition(self, n: SchemaNode, v: str) -> str | None:
        """For a number restricted to integers, the condition that it is one (a float with no fractional part is an
        integer from draft 6; in draft 4 only an int is)."""
        if not (n.has_type and (n.type & T_INTEGER) != 0 and (n.type & T_NUMBER) == 0):
            return None
        if n.dialect == Dialect.DRAFT4:
            return f"type({v}) is int"
        return f"(type({v}) is int or {v}.is_integer())"

    def string_section(self, n: SchemaNode) -> list[str]:
        return [f"if not {wrap(c)}: return False" for c in self.string_conditions(n, "x")]

    def number_section(self, n: SchemaNode) -> list[str]:
        return [f"if not {wrap(c)}: return False" for c in self.number_conditions(n, "x")]

    def pattern_test(self, pattern: str, v: str) -> str:
        """A test of ``v`` (a string) against an ECMAScript pattern. Common shapes match without a regular expression
        (as PatternMatcher does in C#): a literal prefix, an exact literal or literal alternation, and a contained
        literal. Anything else uses a compiled regular expression."""
        shape = _PATTERN_SHAPES.get(pattern)
        if shape is None:
            shape = _pattern_shape(pattern)
            _PATTERN_SHAPES[pattern] = shape
        kind, text = shape
        if kind == "expr":
            return text.replace("$V", v)
        if kind == "set":
            return f"{v} in {self.constant(text)}"
        if compile_pattern(pattern) is None:
            raise SchemaCompilationError(f"Invalid regular expression '{pattern}'.")
        return f"{self.constant(f'R.pattern({lit(pattern)})')}({v})"

    def format_function(self, fmt: str, kind: str, dialect: Dialect) -> str | None:
        custom = self.program.options.formats.get(fmt)
        if custom is not None:
            i = self.custom_format_index.get(id(custom))
            if i is None:
                i = len(self.custom_formats)
                self.custom_formats.append(custom)
                self.custom_format_index[id(custom)] = i
            return f"F[{i}]"
        # Draft 4 and 6 host names are RFC 1123 names; later drafts apply the IDNA rules.
        if kind == "hostname" and dialect <= Dialect.DRAFT6:
            return self.constant("R.legacy_hostname")
        # Names the dialect does not define are unknown formats, which always match.
        if kind not in FORMAT_VALIDATORS:
            return None
        return self.constant(f"R.FORMAT_VALIDATORS[{lit(kind)}]")

    # -------------------------------------------------------------------------------------------------------------
    # In-place applicators

    def in_place(self, n: SchemaNode, e: str | None, kind: str | None) -> list[str]:
        """In-place applicators. ``e`` is the evaluated set being marked (property names for objects, indexes for
        arrays), with ``kind`` saying which; None for flag evaluation."""
        out: list[str] = []

        def marks(node_id: int) -> bool:
            if e is None:
                return False
            c = self.nodes[self.elide(node_id)]
            return c.marks_properties if kind == "object" else c.marks_items

        def direct(node_id: int) -> str:
            return self.call(node_id, "x", n.resource_id, e if marks(node_id) else None)

        if n.ref >= 0:
            push(out, direct(n.ref))
        if n.static_dynamic_ref >= 0:
            push(out, direct(n.static_dynamic_ref))
        if n.dynamic_ref is not None:
            variant = "t" if e is not None and kind is not None and self.dynamic_marks(n, kind) else "v"
            out.append(
                f"if not {self.ref('d' + variant + str(n.id))}(x{', ' + e if variant == 't' and e else ''}): return False"
            )
        for c in n.all_of or []:
            push(out, direct(c))

        if n.any_of is not None:
            if e is not None and any(map(marks, n.any_of)):
                # Every passing branch contributes its annotations, so every branch is evaluated.
                any_ok = self.scratch("any")
                out.append(f"{any_ok} = False")
                for c in n.any_of:
                    if marks(c):
                        s = self.scratch("s")
                        out.append(f"{s} = set()")
                        out.append(f"if {self.call(c, 'x', n.resource_id, s)}:")
                        out.append(f"    {any_ok} = True")
                        out.append(f"    {e} |= {s}")
                    else:
                        out.append(f"if not {any_ok} and {wrap(self.call(c, 'x', n.resource_id))}: {any_ok} = True")
                out.append(f"if not {any_ok}: return False")
            else:
                out.extend(
                    self.select_branches(
                        n, n.any_of, n.any_of_discriminator, lambda branches: self.any_of_check(n, branches)
                    )
                )

        if n.one_of is not None:
            if e is not None and any(map(marks, n.one_of)):
                count = self.scratch("one")
                out.append(f"{count} = 0")
                for c in n.one_of:
                    if marks(c):
                        s = self.scratch("s")
                        out.append(f"{s} = set()")
                        out.append(f"if {self.call(c, 'x', n.resource_id, s)}:")
                        out.append(f"    {count} += 1")
                        out.append(f"    {e} |= {s}")
                    else:
                        out.append(f"if {wrap(self.call(c, 'x', n.resource_id))}: {count} += 1")
                out.append(f"if {count} != 1: return False")
            else:
                out.extend(
                    self.select_branches(
                        n, n.one_of, n.one_of_discriminator, lambda branches: self.one_of_check(n, branches)
                    )
                )

        if n.not_ >= 0:
            check = self.call(n.not_, "x", n.resource_id)
            if check == "True":
                out.append("return False")
            elif check != "False":
                out.append(f"if {check}: return False")

        if n.if_ >= 0:
            then_check = direct(n.then) if n.then >= 0 else "True"
            else_check = direct(n.else_) if n.else_ >= 0 else "True"
            if then_check != "True" or else_check != "True" or marks(n.if_):
                if marks(n.if_):
                    s = self.scratch("s")
                    out.append(f"{s} = set()")
                    cond = self.call(n.if_, "x", n.resource_id, s)
                    out.append(f"if {cond}:")
                    out.append(f"    {e} |= {s}")
                else:
                    cond = self.call(n.if_, "x", n.resource_id)
                    out.append(f"if {cond}:")
                    if then_check == "True":
                        out.append("    pass")
                if then_check != "True":
                    out.append(f"    if not {wrap(then_check)}: return False")
                if else_check != "True":
                    out.append("else:")
                    out.append(f"    if not {wrap(else_check)}: return False")

        # Dependent schemas are in-place applicators; the flag layout emits them in the object section.
        if e is not None and kind == "object":
            for d in n.dependencies or []:
                if d.schema is None:
                    continue
                check = direct(d.schema)
                if check != "True":
                    out.append(f"if {lit(d.name)} in x and not {wrap(check)}: return False")
        return out

    def dynamic_marks(self, n: SchemaNode, kind: str) -> bool:
        d = n.dynamic_ref
        assert d is not None
        for node_id in [d.fallback, *d.by_resource.values()]:
            c = self.nodes[self.elide(node_id)]
            if c.marks_properties if kind == "object" else c.marks_items:
                return True
        return False

    def any_of_check(self, n: SchemaNode, branches: list[int]) -> list[str]:
        if not branches:
            return ["return False"]
        dispatch = self.type_dispatch(n, branches)
        if dispatch is not None:
            return dispatch
        checks = [self.call(b, "x", n.resource_id) for b in branches]
        if "True" in checks:
            return []
        return [f"if not ({' or '.join(checks)}): return False"]

    def one_of_check(self, n: SchemaNode, branches: list[int]) -> list[str]:
        if not branches:
            return ["return False"]
        if len(branches) == 1:
            check = self.call(branches[0], "x", n.resource_id)
            return [] if check == "True" else [f"if not {wrap(check)}: return False"]
        dispatch = self.type_dispatch(n, branches)
        if dispatch is not None:
            return dispatch
        count = self.scratch("one")
        out = [f"{count} = 0"]
        for i, b in enumerate(branches):
            check = self.call(b, "x", n.resource_id)
            if i == 0:
                out.append(f"if {check}: {count} = 1")
            else:
                out.append(f"if {check}:")
                out.append(f"    if {count}: return False")
                out.append(f"    {count} = 1")
        out.append(f"if not {count}: return False")
        return out

    def type_dispatch(self, n: SchemaNode, branches: list[int]) -> list[str] | None:
        """When every branch asserts a type and no two accept the same kind, the instance's kind selects the only
        branch that can pass (SchemaCompiler.ComputeTypeDispatch). Correct for anyOf and oneOf alike."""
        owner: dict[str, int] = {}
        for b in branches:
            c = self.nodes[self.elide(b)]
            if c.always_false:
                continue
            if c.always_true or not c.has_type:
                return None
            for k in KINDS:
                if not kind_allowed(c.type, k):
                    continue
                if k in owner:
                    return None
                owner[k] = b
        if len(branches) < 2:
            return None
        out: list[str] = []
        first = True
        for b in branches:
            kinds = [k for k in KINDS if owner.get(k) == b]
            if not kinds:
                continue
            check = self.call(b, "x", n.resource_id)
            test = " or ".join(self.kind_test(k, "x") for k in kinds)
            out.append(f"{'if' if first else 'elif'} {test}:")
            out.append("    pass" if check == "True" else f"    if not {wrap(check)}: return False")
            first = False
        if first:
            return ["return False"]
        out.append("else:")
        out.append("    return False")
        return out

    def select_branches(
        self, n: SchemaNode, branches: list[int], disc: Discriminator | None, check: Callable[[list[int]], list[str]]
    ) -> list[str]:
        """Narrows oneOf/anyOf branches by a discriminator property when the instance is an object that has it."""
        if disc is None or any(type(v) is float and not math.isfinite(v) for v, _ in disc.known):
            return check(branches)
        d = self.scratch("d")
        out: list[str] = []
        out.append(f"if {self.kind_test('object', 'x')}:")
        out.append(f"    if {lit(disc.property)} not in x:")
        out.extend(indent(["return False"] if disc.all_require else check(branches) or ["pass"], 2))
        out.append("    else:")
        out.append(f"        {d} = x[{lit(disc.property)}]")
        # Group values selecting the same branch subset.
        groups: dict[tuple[int, ...], list[Any]] = {}
        for value, subset in disc.known:
            groups.setdefault(tuple(subset), []).append(value)
        first = True
        for subset_key, values in groups.items():
            subset = [branches[i] for i in subset_key]
            test = " or ".join(eq_test(d, v) for v in values)
            out.append(f"        {'if' if first else 'elif'} {test}:")
            out.extend(indent(check(subset) or ["pass"], 3))
            first = False
        out.append("        else:" if not first else "        if True:")
        out.extend(indent(check([branches[i] for i in disc.unknown]) or ["pass"], 3))
        out.append("else:")
        out.extend(indent(check(branches) or ["pass"], 1))
        return out

    # -------------------------------------------------------------------------------------------------------------
    # Tracking layout (unevaluatedProperties/unevaluatedItems)

    def static_coverage(self, n: SchemaNode, kind: str) -> Coverage | None:
        """When every in-place contributor of evaluated properties/items is unconditional ($ref and allOf chains, with
        no marking anyOf/oneOf/if/dependentSchemas/$dynamicRef), the evaluated set is static: declared names,
        patterns, a prefix length, or everything. unevaluatedProperties/unevaluatedItems then needs no tracking at run
        time (the idea behind the C# fused object plan). None when some contribution is conditional."""
        main = Coverage()
        conditional: list[Coverage] = []

        def marks(node_id: int) -> bool:
            c = self.nodes[self.elide(node_id)]
            return c.marks_properties if kind == "object" else c.marks_items

        def visit(node_id: int, root: bool, coverage: Coverage, visited: set[int]) -> bool:
            m = self.nodes[node_id]
            if node_id in visited:
                return True
            visited.add(node_id)
            if m.always_true or m.always_false:
                return True
            # Conditional contributors (a branch may pass or fail) must have a static coverage of their own; they are
            # harmless when it adds nothing to the unconditional coverage (checked by the caller).
            conditional_children = [
                *(m.any_of or ()),
                *(m.one_of or ()),
                *(c for c in (m.if_, m.then, m.else_) if c >= 0),
                *(dep.schema for dep in (m.dependencies or ()) if dep.schema is not None),
                *((m.dynamic_ref.fallback, *m.dynamic_ref.by_resource.values()) if m.dynamic_ref is not None else ()),
            ]
            for c in conditional_children:
                if not marks(c):
                    continue
                sub = Coverage()
                if not visit(self.elide(c), False, sub, set()):
                    return False
                conditional.append(sub)
            if kind == "object":
                coverage.names.update(m.properties or ())
                for p in m.pattern_properties or ():
                    if p.pattern not in coverage.patterns:
                        coverage.patterns.append(p.pattern)
                if m.additional_properties >= 0 or (not root and m.unevaluated_properties >= 0):
                    coverage.all = True
            else:
                coverage.prefix = max(coverage.prefix, len(m.prefix_items or ()))
                if m.items >= 0 or (not root and m.unevaluated_items >= 0):
                    coverage.all = True
                if m.contains >= 0 and m.contains_marks_evaluated:
                    return False
            for c in [m.ref, m.static_dynamic_ref, *(m.all_of or ())]:
                if c >= 0 and marks(c) and not visit(self.elide(c), False, coverage, visited):
                    return False
            return True

        if not visit(n.id, True, main, set()):
            return None

        def within(c: Coverage) -> bool:
            return main.all or (
                not c.all
                and c.prefix <= main.prefix
                and c.names <= main.names
                and all(x in main.patterns for x in c.patterns)
            )

        return main if all(map(within, conditional)) else None

    def guarded_coverage(self, n: SchemaNode) -> tuple[Coverage, list[GuardedCoverage]] | None:
        """Static coverage for objects with guards. Like static_coverage, but a contribution under ``if``/``then``/
        ``else`` or a dependency's schema is kept with the conditions it applies under (the ``if`` schema evaluated
        against the instance, or the dependency's property being present) instead of having to add nothing. An
        object's evaluated names are then the unconditional coverage plus every guarded coverage whose guards hold,
        all tested statically, so unevaluatedProperties needs no run-time set. ``anyOf``/``oneOf``/``$dynamicRef``
        contributions must still add nothing, since whether a branch passed is not a guard this can test. None when
        that fails, when a contributor is on an in-place cycle, when there are too many guarded coverages, or, below a
        live dynamic reference, when a contributor is in another resource."""
        main = Coverage()
        guarded: list[GuardedCoverage] = []
        unguarded: list[Coverage] = []

        def marks(node_id: int) -> bool:
            return self.nodes[self.elide(node_id)].marks_properties

        # `guards` is None inside an anyOf/oneOf/$dynamicRef branch, where a guard would also need the branch to pass.
        def visit(node_id: int, root: bool, coverage: Coverage, guards: list[str] | None, visited: set[int]) -> bool:
            m = self.nodes[node_id]
            if node_id in visited:
                return True
            visited.add(node_id)
            if m.always_true or m.always_false:
                return True
            if m.in_place_cycle or (self.program.uses_dynamic_scope and m.resource_id != n.resource_id):
                return False

            def branch(c: int, g: list[str] | None) -> bool:
                sub = Coverage()
                if g is None:
                    unguarded.append(sub)
                else:
                    guarded.append(GuardedCoverage(g, sub))
                return visit(self.elide(c), False, sub, g, set())

            alternatives = [
                *(m.any_of or ()),
                *(m.one_of or ()),
                *((m.dynamic_ref.fallback, *m.dynamic_ref.by_resource.values()) if m.dynamic_ref is not None else ()),
            ]
            for c in alternatives:
                if marks(c) and not branch(c, None):
                    return False
            if m.if_ >= 0:
                test = self.call(m.if_, "x", n.resource_id)
                for c, holds in ((m.if_, True), (m.then, True), (m.else_, False)):
                    if c >= 0 and marks(c):
                        g = None if guards is None else [*guards, test if holds else f"not {wrap(test)}"]
                        if not branch(c, g):
                            return False
            for dep in m.dependencies or ():
                if dep.schema is None or not marks(dep.schema):
                    continue
                g = None if guards is None else [*guards, f"{lit(dep.name)} in x"]
                if not branch(dep.schema, g):
                    return False
            coverage.names.update(m.properties or ())
            for p in m.pattern_properties or ():
                if p.pattern not in coverage.patterns:
                    coverage.patterns.append(p.pattern)
            if m.additional_properties >= 0 or (not root and m.unevaluated_properties >= 0):
                coverage.all = True
            for c in [m.ref, m.static_dynamic_ref, *(m.all_of or ())]:
                if c >= 0 and marks(c) and not visit(self.elide(c), False, coverage, guards, visited):
                    return False
            return True

        if not visit(n.id, True, main, [], set()):
            return None

        def within(c: Coverage) -> bool:
            return main.all or (not c.all and c.names <= main.names and all(x in main.patterns for x in c.patterns))

        if not all(map(within, unguarded)):
            return None
        adding = [g for g in guarded if not within(g.coverage)]
        return (main, adding) if len(adding) <= MAX_GUARDED_COVERAGES else None

    def fused_unevaluated(
        self, n: SchemaNode, kind: str, coverage: Coverage, variant: str, guarded: list[GuardedCoverage] | None = None
    ) -> list[str]:
        """The object or array branch of a node whose unevaluated keyword is decided by static coverage."""
        # Other-kind tracking for an enclosing consumer is not needed here: this branch only runs for `kind`.
        lines: list[str] = []
        lines.extend(self.kind_section(n, kind, None, True))
        lines.extend(self.in_place(n, None, None))
        if not coverage.all:
            if kind == "object":
                check = self.call(n.unevaluated_properties, "v", n.resource_id)
                if check != "True":
                    lines.extend(self.uncovered_properties(coverage, guarded or [], check))
            else:
                check = self.call(n.unevaluated_items, "v", n.resource_id)
                if check != "True":
                    if check == "False":
                        lines.append(f"if len(x) > {coverage.prefix}: return False")
                    else:
                        source = "x" if coverage.prefix == 0 else f"x[{coverage.prefix}:]"
                        lines.append(f"for v in {source}:")
                        lines.append(f"    if not {wrap(check)}: return False")
        if variant == "t":
            lines.append("ev.update(x)" if kind == "object" else "ev.update(range(len(x)))")
        lines.append("return True")
        return lines

    def uncovered_properties(self, coverage: Coverage, guarded: list[GuardedCoverage], check: str) -> list[str]:
        """The test of every property no coverage evaluated against unevaluatedProperties."""
        lines: list[str] = []
        names = self.constant(f"frozenset({self.json_constant(sorted(coverage.names))})") if coverage.names else None
        if not coverage.patterns and not guarded and check == "False":
            # Nothing beyond the declared names may appear: one C-level subset test.
            lines.append(f"if not {names}.issuperset(x): return False" if names else "if x: return False")
            return lines
        # Each distinct guard is decided once per object, before the pass.
        guard_names: dict[str, str] = {}
        for g in guarded:
            for expr in g.guards:
                if expr in guard_names:
                    continue
                name = self.scratch("g")
                guard_names[expr] = name
                lines.append(f"{name} = {expr}")
        lines.append("for k, v in x.items():")
        body: list[str] = []
        if names:
            body.append(f"if k in {names}: continue")
        for p in coverage.patterns:
            body.append(f"if {self.pattern_test(p, 'k')}: continue")
        for g in guarded:
            when = " and ".join(guard_names[expr] for expr in g.guards)
            covered: list[str] = []
            extra = sorted(g.coverage.names)
            if 0 < len(extra) <= MAX_GUARD_NAMES:
                covered.extend(f"k == {lit(name)}" for name in extra)
            elif extra:
                covered.append(f"k in {self.constant(f'frozenset({self.json_constant(extra)})')}")
            covered.extend(self.pattern_test(p, "k") for p in g.coverage.patterns)
            if g.coverage.all:
                body.append(f"if {when or 'True'}: continue")
            elif covered:
                body.append(f"if {when + ' and ' if when else ''}({' or '.join(covered)}): continue")
        body.append("return False" if check == "False" else f"if not {wrap(check)}: return False")
        lines.extend(indent(body, 1))
        return lines

    def tracking_body(self, n: SchemaNode, variant: str) -> list[str]:
        out: list[str] = []
        if n.always_false:
            return ["return False"]
        if n.always_true:
            return ["return True"]
        out.extend(self.const_enum(n))
        mask = n.type if n.has_type else T_ALL

        for kind in ("array", "object"):
            test = self.kind_test(kind, "x")
            if not kind_allowed(mask, kind):
                out.append(f"if {test}: return False")
                continue
            own = n.unevaluated_properties >= 0 if kind == "object" else n.unevaluated_items >= 0
            coverage = self.static_coverage(n, kind) if own else None
            if coverage is not None:
                out.append(f"if {test}:")
                out.extend(indent(self.fused_unevaluated(n, kind, coverage, variant), 1))
                continue
            guarded = self.guarded_coverage(n) if own and kind == "object" else None
            if guarded is not None:
                out.append(f"if {test}:")
                out.extend(indent(self.fused_unevaluated(n, kind, guarded[0], variant, guarded[1]), 1))
                continue
            e = self.scratch("e") if own else "ev" if variant == "t" else None
            lines: list[str] = []
            if own:
                lines.append(f"{e} = set()")
            lines.extend(self.kind_section(n, kind, e, e is None))
            lines.extend(self.in_place(n, e, None if e is None else kind))
            if own:
                if kind == "object":
                    check = self.call(n.unevaluated_properties, "v", n.resource_id)
                    if check != "True":
                        lines.append("for k, v in x.items():")
                        lines.append(f"    if k not in {e} and not {wrap(check)}: return False")
                    if variant == "t":
                        lines.append("ev.update(x)")
                else:
                    check = self.call(n.unevaluated_items, "v", n.resource_id)
                    if check != "True":
                        lines.append("for i, v in enumerate(x):")
                        lines.append(f"    if i not in {e} and not {wrap(check)}: return False")
                    if variant == "t":
                        lines.append("ev.update(range(len(x)))")
            lines.append("return True")
            out.append(f"if {test}:")
            out.extend(indent(lines, 1))

        # Scalars: flag evaluation.
        scalar_kinds = [k for k in KINDS if k not in ("object", "array")]
        allowed_scalars = [k for k in scalar_kinds if kind_allowed(mask, k)]
        if not allowed_scalars:
            out.append("return False")
            return out
        for k in allowed_scalars:
            lines = self.kind_section(n, k, None, True)
            if lines:
                out.append(f"if {self.kind_test(k, 'x')}:")
                out.extend(indent(lines, 1))
        if n.has_type and len(allowed_scalars) < len(scalar_kinds):
            out.append(f"if not ({' or '.join(self.kind_test(k, 'x') for k in allowed_scalars)}): return False")
        out.extend(self.in_place(n, None, None))
        out.append("return True")
        return out

    # -------------------------------------------------------------------------------------------------------------
    # Linking: merge structurally identical functions, then emit the reachable ones.

    def link(self, root_name: str) -> tuple[str, str]:
        names = list(self.templates)
        index_of = {name: i for i, name in enumerate(names)}
        # Split each template once: literal parts (even indices) around references (odd indices).
        parts = [_REF_RE.split(self.templates[name]) for name in names]
        refs = [[index_of[p[k]] for k in range(1, len(p), 2)] for p in parts]

        # Partition refinement: start from the literal parts alone, refine by the classes of the references until the
        # number of classes stops growing. Bisimilar functions (identical text up to equivalent callees) merge.
        intern: dict[str, int] = {}
        class_of: list[int] = []
        for p in parts:
            sig = REF_START.join(p[0::2])
            c = intern.get(sig)
            if c is None:
                c = len(intern)
                intern[sig] = c
            class_of.append(c)
        count = len(intern)
        class_of = _refine_classes(class_of, count, refs) if count < len(names) else list(range(len(names)))

        root = index_of[root_name]
        representative = [-1] * len(names)
        representative[class_of[root]] = root
        for i in range(len(names)):
            if representative[class_of[i]] < 0:
                representative[class_of[i]] = i

        # Emit the reachable representatives: from the root, and from the functions that dispatch tables name.
        emitted = [False] * len(names)
        out: list[str] = []
        stack = [representative[class_of[root]]]
        for init in self.constants:
            stack.extend(representative[class_of[index_of[m.group(1)]]] for m in _REF_RE.finditer(init))
        while stack:
            i = stack.pop()
            if emitted[i]:
                continue
            emitted[i] = True
            p = parts[i]
            text = p[0].replace("def @(", f"def {names[i]}(", 1)
            for k in range(1, len(p), 2):
                target = representative[class_of[refs[i][(k - 1) // 2]]]
                text += names[target] + p[k + 1]
                if not emitted[target]:
                    stack.append(target)
            out.append(text)

        # Constants after the functions: a dispatch table names functions, and nothing runs before validate is called.
        def canonical_refs(text: str) -> str:
            return _REF_RE.sub(lambda m: names[representative[class_of[index_of[m.group(1)]]]], text)

        for init, name in self.constants.items():
            out.append(f"{name} = {canonical_refs(init)}")
        return "\n".join(out), names[representative[class_of[root]]]


def _refine_classes(initial: list[int], initial_count: int, refs: list[list[int]]) -> list[int]:
    """Partition refinement: from classes of equal literal text, refines by the classes of the references until the
    number of classes stops growing, so that bisimilar functions share a class."""
    class_of = initial
    count = initial_count
    n = len(class_of)
    size = [0] * count
    for c in class_of:
        size[c] += 1
    while True:
        intern: dict[tuple[int, ...], int] = {}
        nxt = [0] * n
        next_count = 0
        singleton = [-1] * count
        for i in range(n):
            cls = class_of[i]
            if size[cls] == 1:
                if singleton[cls] < 0:
                    singleton[cls] = next_count
                    next_count += 1
                nxt[i] = singleton[cls]
                continue
            sig = (cls, *(class_of[r] for r in refs[i]))
            found = intern.get(sig)
            if found is None:
                found = next_count
                next_count += 1
                intern[sig] = found
            nxt[i] = found
        class_of = nxt
        if next_count == count:
            return class_of
        count = next_count
        size = [0] * count
        for c in class_of:
            size[c] += 1


# How a pattern is tested (see CodeGenerator.pattern_test): ('expr', text with $V for the value), ('set', the
# frozenset's initialiser), or ('regex', '').
_PATTERN_SHAPES: dict[str, tuple[str, str]] = {}
_PLAIN_RE = re.compile(r"^[A-Za-z0-9 _\-/:@,;=!%&'\"<>~`#]*\Z")
_ALTERNATION_RE = re.compile(r"^\^\(\?:([^()]*)\)\$\Z|^\^\(([^()]*)\)\$\Z")


def _pattern_shape(pattern: str) -> tuple[str, str]:
    def plain(t: str) -> bool:
        return _PLAIN_RE.match(t) is not None

    if pattern.startswith("^") and plain(pattern[1:]) and not pattern.endswith("$"):
        return "expr", f"$V.startswith({lit(pattern[1:])})"
    if pattern.startswith("^") and pattern.endswith("$") and len(pattern) >= 2 and plain(pattern[1:-1]):
        return "expr", f"$V == {lit(pattern[1:-1])}"
    m = _ALTERNATION_RE.match(pattern)
    if m is not None:
        alternatives = (m.group(1) if m.group(1) is not None else m.group(2)).split("|")
        if all(map(plain, alternatives)):
            if len(alternatives) <= 6:
                return "expr", "(" + " or ".join(f"$V == {lit(a)}" for a in alternatives) + ")"
            return "set", f"frozenset({alternatives!r})"
    if len(pattern) > 0 and plain(pattern):
        return "expr", f"{lit(pattern)} in $V"
    return "regex", ""
