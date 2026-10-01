"""Compiles loaded schema documents into a SchemaNode graph and runs the compile-time analyses.

A port of Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaCompiler (flag-mode subset).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from .dialect import Dialect, Vocab
from .formats import format_kind
from .loader import SchemaLoader, SchemaResource, SchemaTarget, escape_pointer_token
from .node import (
    CONTENT_BASE64,
    CONTENT_BASE64_JSON,
    CONTENT_JSON,
    CONTENT_NONE,
    T_ARRAY,
    T_BOOLEAN,
    T_INTEGER,
    T_NONE,
    T_NULL,
    T_NUMBER,
    T_OBJECT,
    T_STRING,
    AnnotationEntry,
    DependencyEntry,
    Discriminator,
    DynamicRefTarget,
    PatternProperty,
    SchemaNode,
)
from .options import CompileOptions, SchemaCompilationError
from .uri import decode_fragment, split

# Keywords SchemaCompiler.CompileNode handles itself; anything else is an unknown keyword (an annotation from 2019-09).
KNOWN_KEYWORDS = frozenset(
    (
        "if", "not", "type", "enum", "$ref", "then", "else", "const", "items", "allOf", "anyOf", "oneOf", "title",
        "$defs", "format", "pattern", "maximum", "minimum", "default", "$schema", "$anchor", "required", "contains",
        "maxItems", "minItems", "examples", "readOnly", "$comment", "maxLength", "minLength", "writeOnly",
        "properties", "multipleOf", "deprecated", "uniqueItems", "prefixItems", "minContains", "maxContains",
        "description", "$vocabulary", "definitions", "$dynamicRef", "dependencies", "propertyNames", "maxProperties",
        "minProperties", "contentSchema", "$recursiveRef", "$dynamicAnchor", "contentEncoding", "additionalItems",
        "exclusiveMaximum", "exclusiveMinimum", "unevaluatedItems", "contentMediaType", "$recursiveAnchor",
        "dependentSchemas", "patternProperties", "dependentRequired", "additionalProperties", "unevaluatedProperties",
        "id", "$id",
    )
)  # fmt: skip

_TYPE_MASKS = {
    "null": T_NULL,
    "boolean": T_BOOLEAN,
    "object": T_OBJECT,
    "array": T_ARRAY,
    "number": T_NUMBER,
    "string": T_STRING,
    "integer": T_INTEGER,
}


@dataclass
class _PendingDynamicRef:
    node_id: int
    anchor: str
    is_recursive: bool
    initial_target: SchemaTarget
    seen_resources: set[int] = field(default_factory=set)
    candidates: list[tuple[int, int]] = field(default_factory=list)


@dataclass(frozen=True)
class AnnotationSource:
    """The inputs to a node's annotation keywords."""

    e: dict[str, Any]
    dialect: Dialect
    vocab: Vocab
    legacy: bool
    content: bool


@dataclass
class CompiledSchema:
    """The compiled program: the node graph, its entry node and whether it maintains a dynamic scope."""

    nodes: list[SchemaNode]
    root: int
    root_resource: int
    uses_dynamic_scope: bool
    options: CompileOptions
    annotation_sources: list[AnnotationSource | None] | None
    """What each node's annotations are computed from, indexed by node id, until resolve_annotations fills
    SchemaNode.annotations. Only results collection needs annotations, so flag-mode compilation skips them."""


def resolve_annotations(program: CompiledSchema) -> None:
    """Fills every node's annotations (once), for results collection."""
    sources = program.annotation_sources
    if sources is None:
        return
    program.annotation_sources = None
    assert_format_set = program.options.assert_format is not None
    for node_id, src in enumerate(sources):
        if src is not None:
            program.nodes[node_id].annotations = _collect_annotations(src, assert_format_set)


def _collect_annotations(src: AnnotationSource, assert_format_set: bool) -> list[AnnotationEntry] | None:
    """The annotation keywords of a schema object, in the order SchemaCompiler.CompileNode records them."""
    e, dialect, vocab, legacy, content = src.e, src.dialect, src.vocab, src.legacy, src.content
    meta_data = legacy or (vocab & Vocab.META_DATA) != 0
    format_annotate = legacy or (vocab & (Vocab.FORMAT_ANNOTATION | Vocab.FORMAT_ASSERTION)) != 0 or assert_format_set
    out: list[AnnotationEntry] = []

    def add(keyword: str, strings_only: bool = False) -> None:
        out.append(AnnotationEntry(keyword, e[keyword], strings_only))

    for name in e:
        if name in ("title", "description", "default"):
            if meta_data:
                add(name)
        elif name == "examples":
            if meta_data and dialect >= Dialect.DRAFT6:
                add(name)
        elif name in ("readOnly", "writeOnly"):
            if meta_data and dialect >= Dialect.DRAFT7:
                add(name)
        elif name == "deprecated":
            if meta_data and dialect >= Dialect.DRAFT201909:
                add(name)
        elif name == "format":
            if type(e["format"]) is str and format_annotate:
                add(name)
        elif dialect >= Dialect.DRAFT201909 and name not in KNOWN_KEYWORDS:
            # Unknown keywords are collected as annotations from 2019-09 onwards.
            add(name)
    if (
        content
        and dialect >= Dialect.DRAFT7
        and ("contentEncoding" in e or "contentMediaType" in e or "contentSchema" in e)
    ):
        if "contentEncoding" in e:
            add("contentEncoding", True)
        if "contentMediaType" in e:
            add("contentMediaType", True)
            # contentSchema is only meaningful alongside contentMediaType.
            if "contentSchema" in e and dialect >= Dialect.DRAFT201909:
                add("contentSchema", True)
    return out if out else None


def _propagate_marks(nodes: list[SchemaNode], edges: list[list[int]]) -> None:
    """Spreads marks_properties/marks_items from each marking node to its in-place parents (excluding ``not`` edges)."""
    parents: list[list[int]] = [[] for _ in nodes]
    for i, n in enumerate(nodes):
        # `not` does not contribute annotations: nodes with one use the edges without it.
        children = n.in_place_children(False) if n.not_ >= 0 else edges[i]
        for c in children:
            parents[c].append(i)
    work = [i for i, n in enumerate(nodes) if n.marks_properties]
    while work:
        for p in parents[work.pop()]:
            if not nodes[p].marks_properties:
                nodes[p].marks_properties = True
                work.append(p)
    work = [i for i, n in enumerate(nodes) if n.marks_items]
    while work:
        for p in parents[work.pop()]:
            if not nodes[p].marks_items:
                nodes[p].marks_items = True
                work.append(p)


def _is_number(v: Any) -> bool:
    return type(v) is int or type(v) is float


def _num(v: Any) -> int | float | None:
    return v if _is_number(v) else None


def _compile_draft4_bounds(node: SchemaNode, e: dict[str, Any]) -> None:
    """Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive."""
    maximum = _num(e.get("maximum"))
    minimum = _num(e.get("minimum"))
    if maximum is not None:
        if e.get("exclusiveMaximum") is True:
            node.exclusive_maximum = maximum
        else:
            node.maximum = maximum
    if minimum is not None:
        if e.get("exclusiveMinimum") is True:
            node.exclusive_minimum = minimum
        else:
            node.minimum = minimum


def _get_int(v: Any, fallback: int) -> int:
    if type(v) is int:
        return v
    if type(v) is float and v.is_integer():
        return int(v)
    return fallback


def _strings(v: list[Any]) -> list[str]:
    return [x for x in v if type(x) is str]


class SchemaCompiler:
    def __init__(self, loader: SchemaLoader, options: CompileOptions) -> None:
        self.loader = loader
        self.options = options
        self.nodes: list[SchemaNode] = []
        self.targets: list[SchemaTarget] = []
        self.worklist: list[int] = []
        self.worklist_head = 0
        self.pending_dynamic_refs: list[_PendingDynamicRef] = []
        self.annotation_sources: list[AnnotationSource | None] = []
        self.entry_node = -1

    @staticmethod
    def compile(schema: Any, options: CompileOptions) -> CompiledSchema:
        loader = SchemaLoader(options)
        root_resource = loader.load_root(schema, options.base_uri)
        return SchemaCompiler._compile_from(loader, options, root_resource, schema)

    @staticmethod
    def compile_from_uri(uri: str, options: CompileOptions) -> CompiledSchema:
        loader = SchemaLoader(options)
        root_resource = loader.load_root_from_uri(uri)
        return SchemaCompiler._compile_from(loader, options, root_resource, root_resource.root)

    @staticmethod
    def _compile_from(
        loader: SchemaLoader, options: CompileOptions, root_resource: SchemaResource, schema: Any
    ) -> CompiledSchema:
        compiler = SchemaCompiler(loader, options)
        target = SchemaTarget(root_resource.document, "", schema, root_resource)
        if options.entry_point is not None:
            resolved = loader.try_resolve_reference(root_resource, options.entry_point)
            if resolved is None:
                raise SchemaCompilationError(f"Unable to resolve the entry point '{options.entry_point}'.")
            target = resolved
        compiler.entry_node = compiler._get_node(target)
        compiler._compile_all()
        uses_dynamic_scope = any(n.dynamic_ref is not None for n in compiler.nodes)
        compiler._analyse()
        return CompiledSchema(
            nodes=compiler.nodes,
            root=compiler.entry_node,
            root_resource=compiler.nodes[compiler.entry_node].resource_id,
            uses_dynamic_scope=uses_dynamic_scope,
            options=options,
            annotation_sources=compiler.annotation_sources,
        )

    def _get_node(self, target: SchemaTarget) -> int:
        node_id = target.document.node_of.get(target.value, target.pointer)
        if node_id is None:
            node_id = len(self.nodes)
            location = target.document.retrieval_uri + "#" + target.pointer
            self.nodes.append(
                SchemaNode(node_id, target.resource.id, target.resource.dialect, location, target.pointer)
            )
            self.targets.append(target)
            self.annotation_sources.append(None)
            target.document.node_of.set(target.value, target.pointer, node_id)
            self.worklist.append(node_id)
        return node_id

    def _drain(self) -> None:
        while self.worklist_head < len(self.worklist):
            node_id = self.worklist[self.worklist_head]
            self.worklist_head += 1
            self._compile_node(self.nodes[node_id], self.targets[node_id])

    def _compile_all(self) -> None:
        while True:
            self._drain()
            if not self.pending_dynamic_refs or (
                not self._expand_dynamic_refs() and self.worklist_head == len(self.worklist)
            ):
                break
        # Most schemas have no dynamic reference: skip the finalisation.
        if self.pending_dynamic_refs:
            self._finalize_dynamic_refs()

    def _child(self, parent: SchemaTarget, value: Any, relative: str) -> int:
        pointer = parent.pointer + relative
        resource = self.loader.resource_of(parent.document, value, pointer) or parent.resource
        return self._get_node(SchemaTarget(parent.document, pointer, value, resource))

    def _child_array(self, parent: SchemaTarget, value: Any, keyword: str) -> list[int] | None:
        if type(value) is not list:
            return None
        return [self._child(parent, v, f"/{keyword}/{i}") for i, v in enumerate(value)]

    def _compile_node(self, node: SchemaNode, target: SchemaTarget) -> None:
        element = target.value
        if element is True:
            node.always_true = True
            return
        if element is False:
            node.always_false = True
            return
        if type(element) is not dict:
            node.always_true = True
            return

        dialect = target.resource.dialect
        vocab = target.resource.vocabularies
        legacy = dialect <= Dialect.DRAFT7
        e = element

        if legacy and type(e.get("$ref")) is str:
            # In draft 7 and earlier, $ref replaces every sibling keyword.
            self._compile_ref(node, target, e["$ref"])
            return

        applicator = legacy or (vocab & Vocab.APPLICATOR) != 0
        validation = legacy or (vocab & Vocab.VALIDATION) != 0
        unevaluated = applicator if dialect == Dialect.DRAFT201909 else (vocab & Vocab.UNEVALUATED) != 0
        content = legacy or (vocab & Vocab.CONTENT) != 0
        assert_format = self.options.assert_format
        format_assert = (
            assert_format
            if assert_format is not None
            else (legacy and self.options.assert_format_in_legacy_drafts) or (vocab & Vocab.FORMAT_ASSERTION) != 0
        )

        def kw(name: str) -> str:
            return "/" + escape_pointer_token(name)

        dependencies: list[DependencyEntry] | None = None

        # References.
        if type(e.get("$ref")) is str:
            self._compile_ref(node, target, e["$ref"])
        if dialect >= Dialect.DRAFT202012 and type(e.get("$dynamicRef")) is str:
            self._compile_dynamic_ref(node, target, e["$dynamicRef"], False)
        if dialect == Dialect.DRAFT201909 and type(e.get("$recursiveRef")) is str:
            self._compile_dynamic_ref(node, target, e["$recursiveRef"], True)

        # Applicators.
        if applicator:
            if "allOf" in e:
                node.all_of = self._child_array(target, e["allOf"], "allOf")
            if "anyOf" in e:
                node.any_of = self._child_array(target, e["anyOf"], "anyOf")
            if "oneOf" in e:
                node.one_of = self._child_array(target, e["oneOf"], "oneOf")
            if "not" in e:
                node.not_ = self._child(target, e["not"], kw("not"))
            if dialect >= Dialect.DRAFT7 and "if" in e:
                node.if_ = self._child(target, e["if"], kw("if"))
                if "then" in e:
                    node.then = self._child(target, e["then"], kw("then"))
                if "else" in e:
                    node.else_ = self._child(target, e["else"], kw("else"))
            props = e.get("properties")
            if type(props) is dict:
                node.properties = {
                    name: self._child(target, value, "/properties/" + escape_pointer_token(name))
                    for name, value in props.items()
                }
            pattern_props = e.get("patternProperties")
            if type(pattern_props) is dict:
                node.pattern_properties = [
                    PatternProperty(
                        pattern, self._child(target, value, "/patternProperties/" + escape_pointer_token(pattern))
                    )
                    for pattern, value in pattern_props.items()
                ]
            if "additionalProperties" in e:
                node.additional_properties = self._child(target, e["additionalProperties"], kw("additionalProperties"))
            if dialect >= Dialect.DRAFT6 and "propertyNames" in e:
                node.property_names = self._child(target, e["propertyNames"], kw("propertyNames"))
            if dialect >= Dialect.DRAFT6 and "contains" in e:
                node.contains = self._child(target, e["contains"], kw("contains"))

            # "dependencies" is honoured in every dialect: in 2019-09+ it is an optional compatibility keyword.
            if type(e.get("dependencies")) is dict or (
                dialect >= Dialect.DRAFT201909 and type(e.get("dependentSchemas")) is dict
            ):
                dependencies = self._compile_dependency_schemas(target, e, dialect)

            # Array applicators.
            if dialect >= Dialect.DRAFT202012:
                if type(e.get("prefixItems")) is list:
                    node.prefix_items = self._child_array(target, e["prefixItems"], "prefixItems")
                if "items" in e and type(e["items"]) is not list:
                    node.items = self._child(target, e["items"], kw("items"))
            elif "items" in e:
                if type(e["items"]) is list:
                    self._compile_items_array(node, target, e)
                else:
                    node.items = self._child(target, e["items"], kw("items"))
            node.contains_marks_evaluated = dialect >= Dialect.DRAFT202012

        if unevaluated and dialect >= Dialect.DRAFT201909:
            if "unevaluatedProperties" in e:
                node.unevaluated_properties = self._child(
                    target, e["unevaluatedProperties"], kw("unevaluatedProperties")
                )
            if "unevaluatedItems" in e:
                node.unevaluated_items = self._child(target, e["unevaluatedItems"], kw("unevaluatedItems"))

        if validation:
            if "type" in e:
                self._compile_type(node, e["type"])
            if dialect >= Dialect.DRAFT6 and "const" in e:
                node.has_const = True
                node.const_value = e["const"]
            if type(e.get("enum")) is list:
                node.enum_values = e["enum"]
            if type(e.get("required")) is list:
                node.required_list = _strings(e["required"])
                node.required = list(dict.fromkeys(node.required_list))
            if dialect >= Dialect.DRAFT201909 and type(e.get("dependentRequired")) is dict:
                if dependencies is None:
                    dependencies = []
                for name, v in e["dependentRequired"].items():
                    if type(v) is list:
                        dependencies.append(DependencyEntry("dependentRequired", name, required=_strings(v)))
            node.min_properties = _get_int(e.get("minProperties"), -1)
            node.max_properties = _get_int(e.get("maxProperties"), -1)
            node.min_items = _get_int(e.get("minItems"), -1)
            node.max_items = _get_int(e.get("maxItems"), -1)
            node.unique_items = e.get("uniqueItems") is True
            node.min_length = _get_int(e.get("minLength"), -1)
            node.max_length = _get_int(e.get("maxLength"), -1)
            if type(e.get("pattern")) is str:
                node.pattern = e["pattern"]
            if _is_number(e.get("multipleOf")):
                node.multiple_of = e["multipleOf"]

            if dialect == Dialect.DRAFT4:
                _compile_draft4_bounds(node, e)
            else:
                node.maximum = _num(e.get("maximum"))
                node.minimum = _num(e.get("minimum"))
                node.exclusive_maximum = _num(e.get("exclusiveMaximum"))
                node.exclusive_minimum = _num(e.get("exclusiveMinimum"))

            if dialect >= Dialect.DRAFT201909 and node.contains >= 0:
                if "minContains" in e:
                    node.min_contains = _get_int(e["minContains"], 1)
                if "maxContains" in e:
                    node.max_contains = _get_int(e["maxContains"], -1)

        if type(e.get("format")) is str:
            node.format = e["format"]
            node.format_kind = format_kind(e["format"], dialect)
            node.assert_format = bool(format_assert)

        # Content keywords are asserted only in draft 7 (and annotations elsewhere).
        if content and dialect >= Dialect.DRAFT7 and ("contentEncoding" in e or "contentMediaType" in e):
            self._compile_content(node, e, dialect)

        if dependencies is not None:
            # In the order the three keywords appear in the schema, as SchemaCompiler.CompileNode meets them.
            order = list(e)
            dependencies.sort(key=lambda dep: order.index(dep.keyword))
        node.dependencies = dependencies
        self.annotation_sources[node.id] = AnnotationSource(e, dialect, vocab, legacy, content)

    # The less common keyword groups, out of _compile_node.

    def _compile_dependency_schemas(
        self, target: SchemaTarget, e: dict[str, Any], dialect: Dialect
    ) -> list[DependencyEntry]:
        dependencies: list[DependencyEntry] = []
        deps = e.get("dependencies")
        if type(deps) is dict:
            for name, v in deps.items():
                if type(v) is list:
                    dependencies.append(DependencyEntry("dependencies", name, required=_strings(v)))
                else:
                    child = self._child(target, v, "/dependencies/" + escape_pointer_token(name))
                    dependencies.append(DependencyEntry("dependencies", name, schema=child))
        schemas = e.get("dependentSchemas")
        if dialect >= Dialect.DRAFT201909 and type(schemas) is dict:
            for name, v in schemas.items():
                child = self._child(target, v, "/dependentSchemas/" + escape_pointer_token(name))
                dependencies.append(DependencyEntry("dependentSchemas", name, schema=child))
        return dependencies

    def _compile_items_array(self, node: SchemaNode, target: SchemaTarget, e: dict[str, Any]) -> None:
        """Array-form items (before 2020-12): positional schemas, then additionalItems for the rest."""
        node.prefix_items = self._child_array(target, e["items"], "items")
        node.prefix_keyword = "items"
        if "additionalItems" in e:
            node.items = self._child(target, e["additionalItems"], "/additionalItems")
            node.items_keyword = "additionalItems"

    def _compile_content(self, node: SchemaNode, e: dict[str, Any], dialect: Dialect) -> None:
        base64 = e.get("contentEncoding") == "base64"
        is_json = e.get("contentMediaType") == "application/json"
        node.content = (
            (CONTENT_BASE64_JSON if is_json else CONTENT_BASE64)
            if base64
            else CONTENT_JSON
            if is_json
            else CONTENT_NONE
        )
        node.assert_content = dialect == Dialect.DRAFT7 and self.options.assert_content and node.content != CONTENT_NONE

    def _compile_type(self, node: SchemaNode, value: Any) -> None:
        mask = T_NONE
        if type(value) is list:
            for t in value:
                mask |= _TYPE_MASKS.get(t, T_NONE) if type(t) is str else T_NONE
        elif type(value) is str:
            mask = _TYPE_MASKS.get(value, T_NONE)
        node.type = mask
        node.has_type = True

    def _compile_ref(self, node: SchemaNode, target: SchemaTarget, reference: str) -> None:
        resolved = self.loader.try_resolve_reference(target.resource, reference)
        if resolved is None:
            raise SchemaCompilationError(f"Unable to resolve reference '{reference}' from '{target.resource.uri}'.")
        node.ref = self._get_node(resolved)

    def _compile_dynamic_ref(self, node: SchemaNode, target: SchemaTarget, reference: str, is_recursive: bool) -> None:
        resolved = self.loader.try_resolve_reference(target.resource, reference)
        if resolved is None:
            raise SchemaCompilationError(f"Unable to resolve reference '{reference}' from '{target.resource.uri}'.")
        fragment = decode_fragment(split(reference)[1])
        if is_recursive:
            dynamic = resolved.resource.recursive_anchor and resolved.pointer == resolved.resource.root_pointer
        else:
            anchors = resolved.resource.dynamic_anchors
            dynamic = (
                len(fragment) > 0
                and fragment[0] != "/"
                and anchors is not None
                and anchors.get(fragment) == resolved.pointer
            )
        if not dynamic:
            # A static reference, kept apart from any sibling $ref (C# overwrites the $ref; both apply here).
            node.static_dynamic_ref = self._get_node(resolved)
            node.static_dynamic_keyword = "$recursiveRef" if is_recursive else "$dynamicRef"
            return
        self.pending_dynamic_refs.append(_PendingDynamicRef(node.id, fragment, is_recursive, resolved))

    def _expand_dynamic_refs(self) -> bool:
        added = False
        for pending in self.pending_dynamic_refs:
            for resource in self.loader.resources:
                if resource.id in pending.seen_resources:
                    continue
                pending.seen_resources.add(resource.id)
                if pending.is_recursive:
                    if not resource.recursive_anchor:
                        continue
                    pointer = resource.root_pointer
                else:
                    anchored = (
                        resource.dynamic_anchors.get(pending.anchor) if resource.dynamic_anchors is not None else None
                    )
                    if anchored is None:
                        continue
                    pointer = anchored
                before = len(self.nodes)
                node_id = self._get_node(self._target_in(resource, pointer))
                added = added or len(self.nodes) != before
                pending.candidates.append((resource.id, node_id))
        return added

    def _target_in(self, resource: SchemaResource, pointer: str) -> SchemaTarget:
        t = self.loader.try_resolve_fragment(
            resource, "" if pointer == resource.root_pointer else pointer[len(resource.root_pointer) :]
        )
        return t or SchemaTarget(resource.document, pointer, resource.root, resource)

    def _finalize_dynamic_refs(self) -> None:
        reachable: list[bool] | None = None
        for pending in self.pending_dynamic_refs:
            node = self.nodes[pending.node_id]
            fallback = self._get_node(pending.initial_target)

            def set_static(target: int, node: SchemaNode = node, pending: _PendingDynamicRef = pending) -> None:
                node.static_dynamic_ref = target
                node.static_dynamic_keyword = "$recursiveRef" if pending.is_recursive else "$dynamicRef"

            if len(pending.candidates) <= 1:
                # Only the initial target's resource defines the anchor: resolution is static.
                set_static(fallback)
                continue

            # The dynamic scope is searched outermost-first and its outermost entry is always the resource evaluation
            # started in. When the entry resource defines the anchor, that target is the answer on every path.
            if reachable is None:
                reachable = self._compute_reachability()
            if not reachable[pending.node_id]:
                set_static(fallback)
                continue
            entry_resource = self.nodes[self.entry_node].resource_id
            uniform = next((c for c in pending.candidates if c[0] == entry_resource), None)
            if uniform is not None:
                set_static(uniform[1])
                continue

            node.dynamic_ref = DynamicRefTarget(
                pending.anchor, pending.is_recursive, fallback, dict(pending.candidates)
            )
        self._drain()

    def _compute_reachability(self) -> list[bool]:
        """Nodes reachable from the entry, counting every candidate of a pending dynamic reference as a child."""
        extra: dict[int, list[int]] = {}
        for p in self.pending_dynamic_refs:
            extra.setdefault(p.node_id, []).extend([self._get_node(p.initial_target), *(n for _, n in p.candidates)])
        reached = [False] * len(self.nodes)
        stack = [self.entry_node]
        reached[self.entry_node] = True
        while stack:
            node_id = stack.pop()
            for c in [*self.nodes[node_id].children(), *extra.get(node_id, [])]:
                if c >= len(reached):
                    reached.extend([False] * (c + 1 - len(reached)))
                if not reached[c]:
                    reached[c] = True
                    stack.append(c)
        return reached

    # ---------------------------------------------------------------------------------------------------------------
    # Analyses

    def _analyse(self) -> None:
        # The in-place edges, built once for both analyses. Discriminators only where some node has a oneOf/anyOf.
        edges: list[list[int]] = []
        in_place = False
        branches = False
        for n in self.nodes:
            children = n.in_place_children(True)
            edges.append(children)
            if children:
                in_place = True
            if n.one_of is not None or n.any_of is not None:
                branches = True
        self._compute_marking(edges if in_place else None)
        if in_place:
            self._compute_in_place_cycles(edges)
        if branches:
            self._compute_discriminators()

    def _compute_marking(self, edges: list[list[int]] | None) -> None:
        """Which nodes can contribute evaluated-property/item annotations (ComputeMarking): a node marks if it has the
        keywords itself or any in-place child (not counting ``not``) marks."""
        for n in self.nodes:
            n.marks_properties = (
                n.properties is not None
                or n.pattern_properties is not None
                or n.additional_properties >= 0
                or n.unevaluated_properties >= 0
            )
            n.marks_items = (
                n.prefix_items is not None
                or n.items >= 0
                or (n.contains >= 0 and n.contains_marks_evaluated)
                or n.unevaluated_items >= 0
            )
        if edges is not None:
            _propagate_marks(self.nodes, edges)

    def _compute_in_place_cycles(self, edges: list[list[int]]) -> None:
        """Marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need a depth guard."""
        count = len(self.nodes)
        index = [-1] * count
        low = [0] * count
        on_stack = [False] * count
        stack: list[int] = []
        next_index = 0
        for start in range(count):
            if index[start] >= 0:
                continue
            work: list[list[int]] = [[start, 0]]
            index[start] = low[start] = next_index
            next_index += 1
            stack.append(start)
            on_stack[start] = True
            while work:
                frame = work[-1]
                v, ei = frame
                if ei < len(edges[v]):
                    frame[1] += 1
                    w = edges[v][ei]
                    if index[w] < 0:
                        index[w] = low[w] = next_index
                        next_index += 1
                        stack.append(w)
                        on_stack[w] = True
                        work.append([w, 0])
                    elif on_stack[w]:
                        low[v] = min(low[v], index[w])
                else:
                    work.pop()
                    if work:
                        parent = work[-1][0]
                        low[parent] = min(low[parent], low[v])
                    if low[v] == index[v]:
                        component: list[int] = []
                        while True:
                            w = stack.pop()
                            on_stack[w] = False
                            component.append(w)
                            if w == v:
                                break
                        if len(component) > 1 or v in edges[v]:
                            for c in component:
                                self.nodes[c].in_place_cycle = True

    def effective_node(self, node_id: int) -> SchemaNode:
        """Follows pure ``$ref`` nodes to the node that carries constraints."""
        node = self.nodes[node_id]
        for _ in range(16):
            if not node.is_pure_ref:
                break
            node = self.nodes[node.ref]
        return node

    def _compute_discriminators(self) -> None:
        for n in self.nodes:
            if n.one_of and len(n.one_of) > 1:
                n.one_of_discriminator = self._build_discriminator(n.one_of)
            if n.any_of and len(n.any_of) > 1:
                n.any_of_discriminator = self._build_discriminator(n.any_of)

    def _build_discriminator(self, branches: list[int]) -> Discriminator | None:
        """Classifies branches by the constraint their ``properties[X]`` places on the value (positive: const/enum of
        primitives; negative: string not in an enum; wildcard: anything else), and builds the value -> branches
        table."""
        candidates: list[str] = []
        for b in branches:
            eff = self.effective_node(b)
            if eff.properties is None:
                continue
            for name in eff.properties:
                if self._classify(eff, name)[0] != "wildcard":
                    candidates.append(name)
            break
        for name in candidates:
            classes = [self._classify(self.effective_node(b), name) for b in branches]
            if sum(1 for c in classes if c[0] != "wildcard") < 2:
                continue
            values: list[Any] = []
            for _, s in classes:
                for v in s:
                    if not any(_same_primitive(v, x) for x in values):
                        values.append(v)
            known: list[tuple[Any, list[int]]] = []
            for value in values:
                selected: list[int] = []
                for i, (kind, s) in enumerate(classes):
                    contains = any(_same_primitive(value, x) for x in s)
                    if contains if kind == "positive" else not contains if kind == "negative" else True:
                        selected.append(i)
                known.append((value, selected))
            unknown = [i for i, (kind, _) in enumerate(classes) if kind != "positive"]
            all_require = all(name in (self.effective_node(b).required or ()) for b in branches)
            return Discriminator(name, known, unknown, all_require)
        return None

    def _classify(self, branch: SchemaNode, name: str) -> tuple[str, list[Any]]:
        child = branch.properties.get(name) if branch.properties is not None else None
        if child is None:
            return "wildcard", []
        p = self.effective_node(child)
        if p.has_const and _is_discriminator_primitive(p.const_value):
            return "positive", [p.const_value]
        if (
            p.enum_values is not None
            and len(p.enum_values) > 0
            and all(map(_is_discriminator_primitive, p.enum_values))
        ):
            return "positive", list(p.enum_values)
        if p.not_ >= 0 and p.has_type and p.type == T_STRING:
            not_node = self.effective_node(p.not_)
            if (
                not_node.enum_values is not None
                and all(type(v) is str for v in not_node.enum_values)
                and not not_node.has_type
                and not not_node.has_const
                and not not_node.has_string_keywords
                and not not_node.has_in_place_applicators
            ):
                return "negative", list(not_node.enum_values)
        return "wildcard", []


def _is_discriminator_primitive(v: Any) -> bool:
    return type(v) is str or type(v) is int or type(v) is float or type(v) is bool


def _same_primitive(a: Any, b: Any) -> bool:
    """JSON equality of two discriminator primitives (a boolean never equals a number)."""
    if type(a) is bool or type(b) is bool:
        return a is b
    return bool(a == b)
