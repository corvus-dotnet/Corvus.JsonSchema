# The compiled schema graph: one SchemaNode per distinct (document, pointer), with keyword data digested in advance.
# Ported from node.go. A node id is a zero-based index, and NO_NODE is "none".

const NodeId = Int32
const NO_NODE = Int32(-1)

# JSON types as bits. The first six are the kinds of a document's values.
const TYPE_NULL = KIND_NULL
const TYPE_BOOLEAN = KIND_BOOL
const TYPE_OBJECT = KIND_OBJECT
const TYPE_ARRAY = KIND_ARRAY
const TYPE_NUMBER = KIND_NUMBER
const TYPE_STRING = KIND_STRING
const TYPE_INTEGER = 0x40

const CONTENT_NONE = 0x00
const CONTENT_BASE64 = 0x01
const CONTENT_JSON = 0x02
const CONTENT_BASE64_JSON = 0x03

const EMPTY_DOCUMENT = Document()

# A value in a schema document (a constant, a bound). Not set when n is negative.
struct ValueRef
    d::Document
    n::Int
end

const NO_VALUE = ValueRef(EMPTY_DOCUMENT, -1)

is_set(v::ValueRef) = v.n >= 0

# An optional non-negative integer keyword value.
struct OptCount
    set::Bool
    n::UInt64
end

OptCount() = OptCount(false, 0)

# An annotation-producing keyword and its value (reported in verbose results).
struct AnnotationEntry
    keyword::String
    # The value as JSON text.
    value::String
    # Reported only when the instance is a string (content keywords).
    strings_only::Bool
end

struct NamedNode
    name::String
    bytes::Vector{UInt8}
    node::NodeId
end

struct PatternProperty
    pattern::Pattern
    node::NodeId
end

# The keyword a dependency entry came from, which names its result rows.
const KEYWORD_DEPENDENCIES = "dependencies"
const KEYWORD_DEPENDENT_SCHEMAS = "dependentSchemas"
const KEYWORD_DEPENDENT_REQUIRED = "dependentRequired"

struct DependencyEntry
    keyword::String
    name::String
    has_required::Bool
    required::Vector{String}
    schema::NodeId
end

struct ResourceNode
    resource::UInt32
    node::NodeId
end

# A $dynamicRef/$recursiveRef that stays dynamic after compile-time analysis.
struct DynamicRefTarget
    is_recursive::Bool
    fallback::NodeId
    # The node of the matching anchor in each resource that defines it.
    by_resource::Vector{ResourceNode}
end

# A discriminator value: one of the JSON scalars const and enum can name.
struct DiscriminatorValue
    kind::UInt8
    # A string's text.
    text::Vector{UInt8}
    # A number, or a boolean.
    value::ValueRef
end

# Reports whether an instance value equals this one (numbers by value: 1 and 1.0 are the same key).
function matches(v::DiscriminatorValue, d::Document, x::Int)
    kind(d, x) != v.kind && return false
    v.kind == KIND_STRING && return bytes_equal(str(d, x), v.text)
    v.kind == KIND_NULL && return true
    return values_equal(d, x, v.value.d, v.value.n)
end

# Key equality, with numbers by value.
function same(v::DiscriminatorValue, other::DiscriminatorValue)
    v.kind != other.kind && return false
    v.kind == KIND_STRING && return v.text == other.text
    v.kind == KIND_NULL && return true
    return values_equal(v.value.d, v.value.n, other.value.d, other.value.n)
end

struct DiscriminatorEntry
    value::DiscriminatorValue
    # The branches (zero-based indexes into the keyword's list) the value can select.
    branches::Vector{UInt32}
end

# Selects oneOf/anyOf branches by the value of one property.
struct Discriminator
    property::String
    # Known discriminator values and the branches each can select.
    known::Vector{DiscriminatorEntry}
    # Branches that stay candidates for a value not in known (negative and wildcard branches).
    unknown::Vector{UInt32}
    # Every branch requires the property, so its absence fails the keyword at once.
    all_require::Bool
end

mutable struct SchemaNode
    resource_id::UInt32
    dialect::Dialect
    # The JSON pointer of the schema within its document.
    pointer::String

    always_true::Bool
    always_false::Bool

    # Assertions.
    type_mask::UInt8
    has_type::Bool
    const_value::ValueRef
    has_enum::Bool
    enum_values::Vector{ValueRef}

    # References.
    ref::NodeId
    # A $dynamicRef/$recursiveRef that compile-time analysis resolved statically, and its keyword.
    static_dynamic_ref::NodeId
    static_dynamic_keyword::String
    dynamic_ref::Union{Nothing,DynamicRefTarget}

    # In-place applicators.
    has_all_of::Bool
    has_any_of::Bool
    has_one_of::Bool
    all_of::Vector{NodeId}
    any_of::Vector{NodeId}
    one_of::Vector{NodeId}
    not::NodeId
    if_::NodeId
    then_::NodeId
    else_::NodeId

    # Objects.
    has_properties::Bool
    properties::Vector{NamedNode}
    # The properties by name, for a node with many of them.
    property_names::Union{Nothing,Names}
    has_pattern_properties::Bool
    pattern_properties::Vector{PatternProperty}
    additional_properties::NodeId
    property_names_schema::NodeId
    has_required::Bool
    # Without duplicates.
    required::Vector{String}
    # As written (duplicates kept), for results.
    required_list::Vector{String}
    has_dependencies::Bool
    dependencies::Vector{DependencyEntry}
    min_properties::OptCount
    max_properties::OptCount
    unevaluated_properties::NodeId

    # Arrays.
    has_prefix_items::Bool
    prefix_items::Vector{NodeId}
    # The keywords behind prefixItems/items: prefixItems/items (2020-12) or items/additionalItems (legacy).
    prefix_keyword::String
    items_keyword::String
    items::NodeId
    contains::NodeId
    min_contains::UInt64
    max_contains::OptCount
    contains_marks_evaluated::Bool
    min_items::OptCount
    max_items::OptCount
    unique_items::Bool
    unevaluated_items::NodeId

    # Strings.
    min_length::OptCount
    max_length::OptCount
    pattern::Union{Nothing,Pattern}
    has_format::Bool
    format::String
    format_kind::UInt8
    assert_format::Bool
    content::UInt8
    assert_content::Bool

    # Numbers.
    minimum::ValueRef
    maximum::ValueRef
    exclusive_minimum::ValueRef
    exclusive_maximum::ValueRef
    multiple_of::ValueRef
    # The multipleOf divisor, digested.
    divisor::Union{Nothing,Divisor}

    # Analysis.
    marks_properties::Bool
    marks_items::Bool
    in_place_cycle::Bool
    one_of_discriminator::Union{Nothing,Discriminator}
    any_of_discriminator::Union{Nothing,Discriminator}
end

function SchemaNode(resource::UInt32, dialect::Dialect, pointer::String)
    return SchemaNode(resource, dialect, pointer, false, false,
        0x00, false, NO_VALUE, false, ValueRef[],
        NO_NODE, NO_NODE, "\$dynamicRef", nothing,
        false, false, false, NodeId[], NodeId[], NodeId[], NO_NODE, NO_NODE, NO_NODE, NO_NODE,
        false, NamedNode[], nothing, false, PatternProperty[], NO_NODE, NO_NODE, false, String[], String[], false,
        DependencyEntry[], OptCount(), OptCount(), NO_NODE,
        false, NodeId[], "prefixItems", "items", NO_NODE, NO_NODE, UInt64(1), OptCount(), false, OptCount(),
        OptCount(), false, NO_NODE,
        OptCount(), OptCount(), nothing, false, "", FORMAT_UNKNOWN, false, CONTENT_NONE, false,
        NO_VALUE, NO_VALUE, NO_VALUE, NO_VALUE, NO_VALUE, nothing,
        false, false, false, nothing, nothing)
end

# Reports keywords that apply only to objects.
function has_object_keywords(n::SchemaNode)
    return n.has_properties || n.has_pattern_properties || n.additional_properties >= 0 ||
           n.property_names_schema >= 0 || n.has_required || n.has_dependencies || n.min_properties.set ||
           n.max_properties.set || n.unevaluated_properties >= 0
end

function has_array_keywords(n::SchemaNode)
    return n.has_prefix_items || n.items >= 0 || n.contains >= 0 || n.min_items.set || n.max_items.set ||
           n.unique_items || n.unevaluated_items >= 0
end

function has_string_keywords(n::SchemaNode)
    return n.min_length.set || n.max_length.set || n.pattern !== nothing ||
           (n.assert_format && n.has_format && !is_numeric_format(n.format_kind)) || n.assert_content
end

function has_number_keywords(n::SchemaNode)
    return is_set(n.minimum) || is_set(n.maximum) || is_set(n.exclusive_minimum) || is_set(n.exclusive_maximum) ||
           is_set(n.multiple_of) || (n.assert_format && n.has_format && is_numeric_format(n.format_kind))
end

has_dependency_schema(n::SchemaNode) = any(dep -> dep.schema >= 0, n.dependencies)

function has_in_place_applicators(n::SchemaNode)
    return n.ref >= 0 || n.static_dynamic_ref >= 0 || n.dynamic_ref !== nothing || n.has_all_of || n.has_any_of ||
           n.has_one_of || n.not >= 0 || n.if_ >= 0 || has_dependency_schema(n)
end

# Reports a node that is nothing but $ref: no other keyword that asserts.
function is_pure_ref(n::SchemaNode)
    return n.ref >= 0 && !n.has_type && !is_set(n.const_value) && !n.has_enum && !has_object_keywords(n) &&
           !has_array_keywords(n) && !has_string_keywords(n) && !has_number_keywords(n) &&
           n.dynamic_ref === nothing && n.static_dynamic_ref < 0 && !n.has_all_of && !n.has_any_of &&
           !n.has_one_of && n.not < 0 && n.if_ < 0 && !n.has_dependencies
end

function append_node!(out::Vector{NodeId}, ids::NodeId...)
    for id in ids
        id >= 0 && push!(out, id)
    end
    return out
end

# The in-place children (the instance is evaluated at the same location).
function in_place_children(n::SchemaNode, include_not::Bool)
    out = append_node!(NodeId[], n.ref, n.static_dynamic_ref)
    d = n.dynamic_ref
    if d !== nothing
        push!(out, d.fallback)
        for r in d.by_resource
            push!(out, r.node)
        end
    end
    append!(out, n.all_of)
    append!(out, n.any_of)
    append!(out, n.one_of)
    include_not && append_node!(out, n.not)
    append_node!(out, n.if_, n.then_, n.else_)
    for dep in n.dependencies
        append_node!(out, dep.schema)
    end
    return out
end

# Every child node.
function children(n::SchemaNode)
    out = in_place_children(n, true)
    for p in n.properties
        push!(out, p.node)
    end
    for p in n.pattern_properties
        push!(out, p.node)
    end
    append_node!(out, n.additional_properties, n.property_names_schema, n.unevaluated_properties, n.items,
        n.contains, n.unevaluated_items)
    append!(out, n.prefix_items)
    return out
end
