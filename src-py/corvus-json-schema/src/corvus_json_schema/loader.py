"""Loads schema documents, identifies resources and anchors, and resolves references.

A port of Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaLoader: elements are identified by (document, JSON
pointer) rather than by parsed-document row index.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, Generic, TypeVar

from . import dialect as d
from ._metaschemas import METASCHEMAS
from .dialect import Dialect, Vocab, known_dialect, subschema_kind, vocabulary_flag
from .options import CompileOptions, SchemaCompilationError
from .uri import decode_fragment, normalize, resolve, resolve_pointer, split

DEFAULT_ROOT_URI = "https://corvus-oss.org/runtime-evaluator/root.json"

T = TypeVar("T")


class LocationMap(Generic[T]):
    """Values by schema location.

    Schema objects (dicts and lists) are keyed by identity, which avoids hashing long pointer strings; the pointer
    decides for scalars and for objects in ``shared``, which appear at more than one location (possible in a schema
    built in code, never in parsed JSON). The loader's walk records those as it meets them.
    """

    __slots__ = ("_by_object", "_by_pointer", "_shared")

    def __init__(self, shared: set[int]) -> None:
        self._by_object: dict[int, T] = {}
        self._by_pointer: dict[str, T] = {}
        self._shared = shared

    def get(self, schema: Any, pointer: str) -> T | None:
        if (type(schema) is dict or type(schema) is list) and id(schema) not in self._shared:
            return self._by_object.get(id(schema))
        return self._by_pointer.get(pointer)

    def set(self, schema: Any, pointer: str, value: T) -> None:
        if (type(schema) is dict or type(schema) is list) and id(schema) not in self._shared:
            self._by_object[id(schema)] = value
        else:
            self._by_pointer[pointer] = value


class SchemaDocument:
    def __init__(self, doc_id: int, root: Any, retrieval_uri: str) -> None:
        self.id = doc_id
        self.root = root
        self.retrieval_uri = retrieval_uri
        # Schema objects met at more than one location (by identity), with the location where each was first met.
        self.shared: set[int] = set()
        self.first_location: dict[int, str] = {}
        # The resource that owns each visited schema location.
        self.resource_of: LocationMap[SchemaResource] = LocationMap(self.shared)
        # The compiled node of each schema location, filled by the compiler.
        self.node_of: LocationMap[int] = LocationMap(self.shared)
        # Resolved references, by resource and reference text.
        self.reference_cache: dict[str, SchemaTarget | None] = {}


class SchemaResource:
    def __init__(
        self,
        resource_id: int,
        document: SchemaDocument,
        root_pointer: str,
        uri: str,
        dialect: Dialect,
        vocabularies: Vocab,
    ) -> None:
        self.id = resource_id
        self.document = document
        self.root_pointer = root_pointer
        self.uri = uri
        self.dialect = dialect
        self.vocabularies = vocabularies
        self.recursive_anchor = False
        self.anchors: dict[str, str] | None = None
        self.dynamic_anchors: dict[str, str] | None = None

    @property
    def root(self) -> Any:
        return _value_at(self.document, self.root_pointer)


@dataclass(frozen=True)
class SchemaTarget:
    """The target of a reference: an element in a document, and the resource it belongs to."""

    document: SchemaDocument
    pointer: str
    value: Any
    resource: SchemaResource


def escape_pointer_token(token: str) -> str:
    if "~" not in token and "/" not in token:
        return token
    return token.replace("~", "~0").replace("/", "~1")


def _value_at(doc: SchemaDocument, pointer: str) -> Any:
    return resolve_pointer(doc.root, pointer)[1]


def _is_schema_value(v: Any) -> bool:
    return type(v) is bool or type(v) is dict


class SchemaLoader:
    def __init__(self, options: CompileOptions) -> None:
        self.options = options
        self.documents: list[SchemaDocument] = []
        self.resources: list[SchemaResource] = []
        self._resources_by_uri: dict[str, SchemaResource] = {}
        self._documents_by_uri: dict[str, SchemaDocument] = {}
        self._metaschema_info: dict[str, tuple[Dialect, Vocab]] = {}
        self._metaschema_loading: set[str] = set()

    def load_root(self, schema: Any, base_uri: str | None) -> SchemaResource:
        uri = DEFAULT_ROOT_URI if base_uri is None else normalize(base_uri)
        doc = self._add_document(uri, schema)
        resource = doc.resource_of.get(doc.root, "")
        assert resource is not None
        return resource

    def load_root_from_uri(self, uri: str) -> SchemaResource:
        normalized = normalize(uri)
        if not self._try_load_document(normalized):
            raise SchemaCompilationError(f"Unable to resolve the schema document '{uri}'.")
        doc = self._documents_by_uri[normalized]
        resource = doc.resource_of.get(doc.root, "")
        assert resource is not None
        return resource

    def resource_of(self, document: SchemaDocument, value: Any, pointer: str) -> SchemaResource | None:
        return document.resource_of.get(value, pointer)

    def try_resolve_reference(self, source: SchemaResource, reference: str) -> SchemaTarget | None:
        cache = source.document.reference_cache
        key = f"{source.id} {reference}"
        if key in cache:
            return cache[key]
        target = self._resolve_reference(source, reference)
        cache[key] = target
        return target

    def _resolve_reference(self, source: SchemaResource, reference: str) -> SchemaTarget | None:
        uri_part, fragment = split(reference)
        absolute = resolve(source.uri, uri_part)
        resource = self._resources_by_uri.get(absolute)
        if resource is None:
            if not self._try_load_document(absolute):
                return None
            resource = self._resources_by_uri.get(absolute)
            if resource is None:
                return None
        return self.try_resolve_fragment(resource, decode_fragment(fragment))

    def try_resolve_fragment(self, resource: SchemaResource, fragment: str) -> SchemaTarget | None:
        if len(fragment) == 0:
            return SchemaTarget(resource.document, resource.root_pointer, resource.root, resource)
        if fragment[0] == "/":
            found, value, path = resolve_pointer(resource.root, fragment)
            if not found:
                return None
            pointer = resource.root_pointer
            for seg in path:
                pointer += "/" + escape_pointer_token(str(seg))
            owner = resource.document.resource_of.get(value, pointer) or resource
            return SchemaTarget(resource.document, pointer, value, owner)
        anchor = resource.anchors.get(fragment) if resource.anchors is not None else None
        if anchor is not None:
            return SchemaTarget(resource.document, anchor, _value_at(resource.document, anchor), resource)
        return None

    def get_dialect_info(self, schema_uri: str) -> tuple[Dialect, Vocab]:
        # The standard metaschema URIs, as usually written, need no URI normalisation.
        plain = known_dialect(schema_uri[:-1] if schema_uri.endswith("#") else schema_uri)
        if plain is not None:
            return plain, Vocab.ALL_ANNOTATING_FORMAT
        normalized = normalize(schema_uri)
        known = known_dialect(normalized)
        if known is not None:
            return known, Vocab.ALL_ANNOTATING_FORMAT
        cached = self._metaschema_info.get(normalized)
        if cached is not None:
            return cached
        if normalized in self._metaschema_loading:
            return self.options.default_dialect, Vocab.ALL_ANNOTATING_FORMAT
        self._metaschema_loading.add(normalized)
        try:
            meta_resource = self._resources_by_uri.get(normalized)
            if meta_resource is None:
                if not self._try_load_document(normalized):
                    info = (self.options.default_dialect, Vocab.ALL_ANNOTATING_FORMAT)
                    self._metaschema_info[normalized] = info
                    return info
                meta_resource = self._resources_by_uri.get(normalized)
                if meta_resource is None:
                    info = (self.options.default_dialect, Vocab.ALL_ANNOTATING_FORMAT)
                    self._metaschema_info[normalized] = info
                    return info
            meta = meta_resource.root
            vocabularies = Vocab.ALL_ANNOTATING_FORMAT
            if type(meta) is dict and type(meta.get("$vocabulary")) is dict:
                vocabularies = Vocab.NONE
                for name in meta["$vocabulary"]:
                    vocabularies |= vocabulary_flag(name)
                vocabularies |= Vocab.CORE
            info = (meta_resource.dialect, vocabularies)
            self._metaschema_info[normalized] = info
            return info
        finally:
            self._metaschema_loading.discard(normalized)

    def _try_load_document(self, absolute_uri: str) -> bool:
        if absolute_uri in self._documents_by_uri:
            return True
        resolver = self.options.resolve_document
        resolved = resolver(absolute_uri) if resolver is not None else None
        if resolved is not None:
            self._add_document(absolute_uri, json.loads(resolved) if isinstance(resolved, (str, bytes)) else resolved)
            return True
        meta = METASCHEMAS.get(absolute_uri)
        if meta is not None:
            self._add_document(absolute_uri, json.loads(meta))
            return True
        return False

    def _add_document(self, uri: str, root: Any) -> SchemaDocument:
        doc = SchemaDocument(len(self.documents), root, uri)
        self.documents.append(doc)
        self._documents_by_uri[uri] = doc
        dialect, vocabularies = self._get_root_dialect(root)
        resource = self._create_resource(doc, "", uri, dialect, vocabularies)
        self._walk(doc, root, "", resource, True)
        return doc

    def _get_root_dialect(self, root: Any) -> tuple[Dialect, Vocab]:
        if type(root) is dict and type(root.get("$schema")) is str:
            return self.get_dialect_info(root["$schema"])
        return self.options.default_dialect, Vocab.ALL_ANNOTATING_FORMAT

    def _create_resource(
        self, doc: SchemaDocument, pointer: str, uri: str, dialect: Dialect, vocabularies: Vocab
    ) -> SchemaResource:
        resource = SchemaResource(len(self.resources), doc, pointer, uri, dialect, vocabularies)
        self.resources.append(resource)
        if uri not in self._resources_by_uri:
            self._resources_by_uri[uri] = resource
        return resource

    def _walk(
        self, doc: SchemaDocument, element: Any, pointer: str, resource: SchemaResource, is_resource_root: bool
    ) -> None:
        if type(element) is dict or type(element) is list:
            key = id(element)
            first = doc.first_location.get(key)
            if first is None:
                doc.first_location[key] = pointer
            elif first != pointer and key not in doc.shared:
                # Re-key what was recorded for the first location by its pointer.
                owner = doc.resource_of.get(element, first)
                doc.shared.add(key)
                if owner is not None:
                    doc.resource_of.set(element, first, owner)
        if type(element) is not dict:
            doc.resource_of.set(element, pointer, resource)
            return

        dialect = resource.dialect
        vocabularies = resource.vocabularies
        if not is_resource_root and type(element.get("$schema")) is str:
            dialect, vocabularies = self.get_dialect_info(element["$schema"])

        legacy_ref_overrides_siblings = dialect <= Dialect.DRAFT7 and type(element.get("$ref")) is str
        if not legacy_ref_overrides_siblings:
            id_value = element.get("id") if dialect == Dialect.DRAFT4 else element.get("$id")
            if type(id_value) is str:
                uri_part, fragment = split(id_value)
                if len(uri_part) == 0:
                    if len(fragment) > 0 and dialect <= Dialect.DRAFT7:
                        _add_anchor(resource, fragment, pointer)
                else:
                    absolute = resolve(resource.uri, uri_part)
                    if not is_resource_root or absolute != resource.uri:
                        if is_resource_root:
                            if absolute not in self._resources_by_uri:
                                self._resources_by_uri[absolute] = resource
                            resource.uri = absolute
                        else:
                            resource = self._create_resource(doc, pointer, absolute, dialect, vocabularies)
                            is_resource_root = True
                    if len(fragment) > 0 and dialect <= Dialect.DRAFT7:
                        _add_anchor(resource, fragment, pointer)
            anchor = element.get("$anchor")
            if dialect >= Dialect.DRAFT201909 and type(anchor) is str:
                _add_anchor(resource, anchor, pointer)
            dynamic_anchor = element.get("$dynamicAnchor")
            if dialect >= Dialect.DRAFT202012 and type(dynamic_anchor) is str:
                if resource.dynamic_anchors is None:
                    resource.dynamic_anchors = {}
                if dynamic_anchor not in resource.dynamic_anchors:
                    resource.dynamic_anchors[dynamic_anchor] = pointer
                _add_anchor(resource, dynamic_anchor, pointer)
            if dialect == Dialect.DRAFT201909 and is_resource_root and element.get("$recursiveAnchor") is True:
                resource.recursive_anchor = True

        doc.resource_of.set(element, pointer, resource)

        for name, value in element.items():
            base = pointer + "/" + escape_pointer_token(name)
            kind = subschema_kind(name, dialect, legacy_ref_overrides_siblings)
            if kind == d.SINGLE:
                if _is_schema_value(value):
                    self._walk(doc, value, base, resource, False)
            elif kind == d.SINGLE_OR_ARRAY:
                if type(value) is list:
                    for i, v in enumerate(value):
                        self._walk(doc, v, f"{base}/{i}", resource, False)
                elif _is_schema_value(value):
                    self._walk(doc, value, base, resource, False)
            elif kind == d.ARRAY:
                if type(value) is list:
                    for i, v in enumerate(value):
                        self._walk(doc, v, f"{base}/{i}", resource, False)
            elif kind == d.MAP:
                if type(value) is dict:
                    for entry, v in value.items():
                        if _is_schema_value(v):
                            self._walk(doc, v, base + "/" + escape_pointer_token(entry), resource, False)


def _add_anchor(resource: SchemaResource, name: str, pointer: str) -> None:
    if resource.anchors is None:
        resource.anchors = {}
    if name not in resource.anchors:
        resource.anchors[name] = pointer
