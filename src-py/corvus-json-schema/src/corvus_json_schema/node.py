"""The compiled schema graph: one SchemaNode per distinct (document, pointer), with pre-digested keyword data.

A port of Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaNode, trimmed to what flag-mode code generation needs.
"""

from __future__ import annotations

from dataclasses import dataclass
from functools import cached_property
from typing import Any

from .dialect import Dialect
from .formats import is_numeric_format

# JSON types as bits.
T_NONE = 0
T_NULL = 1
T_BOOLEAN = 2
T_OBJECT = 4
T_ARRAY = 8
T_NUMBER = 16
T_STRING = 32
T_INTEGER = 64
T_ALL = 127

# Content kinds.
CONTENT_NONE = 0
CONTENT_BASE64 = 1
CONTENT_JSON = 2
CONTENT_BASE64_JSON = 3


@dataclass(frozen=True)
class AnnotationEntry:
    """An annotation-producing keyword and its value (reported in verbose results)."""

    keyword: str
    value: Any
    strings_only: bool
    """Reported only when the instance is a string (content keywords)."""


@dataclass(frozen=True)
class PatternProperty:
    pattern: str
    node: int


@dataclass(frozen=True)
class DependencyEntry:
    keyword: str
    """The keyword the entry came from (dependencies, dependentSchemas or dependentRequired), which names its result
    rows."""
    name: str
    required: list[str] | None = None
    schema: int | None = None


@dataclass
class DynamicRefTarget:
    """A ``$dynamicRef``/``$recursiveRef`` that stays dynamic after compile-time analysis."""

    anchor: str
    is_recursive: bool
    fallback: int
    by_resource: dict[int, int]
    """resource id -> node id of that resource's matching anchor (only resources that define it)."""


@dataclass
class Discriminator:
    """Selects oneOf/anyOf branches by the value of one property (see SchemaCompiler.BuildDiscriminator)."""

    property: str
    known: list[tuple[Any, list[int]]]
    """Known discriminator values (strings, numbers, booleans) and the branches each can select."""
    unknown: list[int]
    """Branches that stay candidates for any value not in ``known`` (negative and wildcard branches)."""
    all_require: bool
    """Every branch requires the property, so its absence fails the keyword at once."""


@dataclass(eq=False)
class SchemaNode:
    """A compiled schema location. Its keyword fields are set by the compiler and fixed once compilation finishes,
    so the derived ``has_*``/``is_*`` properties are computed once (code generation asks for them many times)."""

    id: int
    resource_id: int
    dialect: Dialect
    location: str
    pointer: str
    """The JSON pointer of the schema within its document (C#'s SchemaLocation)."""

    always_true: bool = False
    always_false: bool = False

    # Assertions.
    type: int = T_NONE
    has_type: bool = False
    has_const: bool = False
    const_value: Any = None
    enum_values: list[Any] | None = None

    # References.
    ref: int = -1
    static_dynamic_ref: int = -1
    """A ``$dynamicRef``/``$recursiveRef`` that compile-time analysis resolved statically, and its keyword."""
    static_dynamic_keyword: str | None = None
    dynamic_ref: DynamicRefTarget | None = None

    # In-place applicators.
    all_of: list[int] | None = None
    any_of: list[int] | None = None
    one_of: list[int] | None = None
    not_: int = -1
    if_: int = -1
    then: int = -1
    else_: int = -1

    # Objects.
    properties: dict[str, int] | None = None
    pattern_properties: list[PatternProperty] | None = None
    additional_properties: int = -1
    property_names: int = -1
    required: list[str] | None = None
    required_list: list[str] | None = None
    """``required`` as written (duplicates kept), for results."""
    dependencies: list[DependencyEntry] | None = None
    min_properties: int = -1
    max_properties: int = -1
    unevaluated_properties: int = -1

    # Arrays.
    prefix_items: list[int] | None = None
    prefix_keyword: str = "prefixItems"
    """The keywords behind prefix_items/items: ``prefixItems``/``items`` (2020-12) or ``items``/``additionalItems``
    (legacy)."""
    items_keyword: str = "items"
    items: int = -1
    contains: int = -1
    min_contains: int = 1
    max_contains: int = -1
    contains_marks_evaluated: bool = False
    min_items: int = -1
    max_items: int = -1
    unique_items: bool = False
    unevaluated_items: int = -1

    # Strings.
    min_length: int = -1
    max_length: int = -1
    pattern: str | None = None
    format: str | None = None
    format_kind: str = "unknown"
    """The format this dialect recognises (see format_kind), or 'unknown'."""
    assert_format: bool = False
    content: int = CONTENT_NONE
    assert_content: bool = False

    # Numbers.
    minimum: int | float | None = None
    maximum: int | float | None = None
    exclusive_minimum: int | float | None = None
    exclusive_maximum: int | float | None = None
    multiple_of: int | float | None = None

    # Annotations, in schema order (SchemaCompiler.CompileNode).
    annotations: list[AnnotationEntry] | None = None

    # Analysis.
    marks_properties: bool = False
    marks_items: bool = False
    in_place_cycle: bool = False
    one_of_discriminator: Discriminator | None = None
    any_of_discriminator: Discriminator | None = None

    @cached_property
    def has_object_keywords(self) -> bool:
        """Keywords that apply only to objects."""
        return (
            self.properties is not None
            or self.pattern_properties is not None
            or self.additional_properties >= 0
            or self.property_names >= 0
            or self.required is not None
            or self.dependencies is not None
            or self.min_properties >= 0
            or self.max_properties >= 0
            or self.unevaluated_properties >= 0
        )

    @cached_property
    def has_array_keywords(self) -> bool:
        return (
            self.prefix_items is not None
            or self.items >= 0
            or self.contains >= 0
            or self.min_items >= 0
            or self.max_items >= 0
            or self.unique_items
            or self.unevaluated_items >= 0
        )

    @cached_property
    def has_string_keywords(self) -> bool:
        return (
            self.min_length >= 0
            or self.max_length >= 0
            or self.pattern is not None
            or (self.assert_format and self.format is not None and not is_numeric_format(self.format_kind))
            or self.assert_content
        )

    @cached_property
    def has_number_keywords(self) -> bool:
        return (
            self.minimum is not None
            or self.maximum is not None
            or self.exclusive_minimum is not None
            or self.exclusive_maximum is not None
            or self.multiple_of is not None
            or (self.assert_format and is_numeric_format(self.format_kind))
        )

    @cached_property
    def has_in_place_applicators(self) -> bool:
        return (
            self.ref >= 0
            or self.static_dynamic_ref >= 0
            or self.dynamic_ref is not None
            or self.all_of is not None
            or self.any_of is not None
            or self.one_of is not None
            or self.not_ >= 0
            or self.if_ >= 0
            or (self.dependencies is not None and any(d.schema is not None for d in self.dependencies))
        )

    @cached_property
    def is_type_only(self) -> bool:
        """Only ``type`` (a type test at the call site)."""
        return (
            self.has_type
            and not self.has_const
            and self.enum_values is None
            and not self.has_object_keywords
            and not self.has_array_keywords
            and not self.has_string_keywords
            and not self.has_number_keywords
            and not self.has_in_place_applicators
        )

    @cached_property
    def is_pure_ref(self) -> bool:
        """Nothing but ``$ref`` (plus, possibly, nothing else that asserts)."""
        return (
            self.ref >= 0
            and not self.has_type
            and not self.has_const
            and self.enum_values is None
            and not self.has_object_keywords
            and not self.has_array_keywords
            and not self.has_string_keywords
            and not self.has_number_keywords
            and self.dynamic_ref is None
            and self.static_dynamic_ref < 0
            and self.all_of is None
            and self.any_of is None
            and self.one_of is None
            and self.not_ < 0
            and self.if_ < 0
            and self.dependencies is None
        )

    def in_place_children(self, include_not: bool) -> list[int]:
        """In-place children (the instance is evaluated at the same location)."""
        out: list[int] = []
        if self.ref >= 0:
            out.append(self.ref)
        if self.static_dynamic_ref >= 0:
            out.append(self.static_dynamic_ref)
        if self.dynamic_ref is not None:
            out.append(self.dynamic_ref.fallback)
            out.extend(self.dynamic_ref.by_resource.values())
        if self.all_of:
            out.extend(self.all_of)
        if self.any_of:
            out.extend(self.any_of)
        if self.one_of:
            out.extend(self.one_of)
        if include_not and self.not_ >= 0:
            out.append(self.not_)
        if self.if_ >= 0:
            out.append(self.if_)
        if self.then >= 0:
            out.append(self.then)
        if self.else_ >= 0:
            out.append(self.else_)
        if self.dependencies:
            for d in self.dependencies:
                if d.schema is not None:
                    out.append(d.schema)
        return out

    def children(self) -> list[int]:
        """Every child node."""
        out = self.in_place_children(True)
        if self.properties:
            out.extend(self.properties.values())
        if self.pattern_properties:
            out.extend(p.node for p in self.pattern_properties)
        for c in (
            self.additional_properties,
            self.property_names,
            self.unevaluated_properties,
            self.items,
            self.contains,
            self.unevaluated_items,
        ):
            if c >= 0:
                out.append(c)
        if self.prefix_items:
            out.extend(self.prefix_items)
        return out
