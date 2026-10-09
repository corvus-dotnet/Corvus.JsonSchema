# Loads schema documents, identifies resources and anchors, and resolves references. Elements are identified by
# (document, value index). Documents and resources are numbered from one. Ported from loader.go.

const DEFAULT_ROOT_URI = "https://corvus-oss.org/runtime-evaluator/root.json"

struct SchemaDocument
    doc::Document
    # The resource that owns each visited schema value.
    resource_of::Dict{Int,UInt32}
end

mutable struct SchemaResource
    document::UInt32
    root_pointer::String
    uri::String
    dialect::Dialect
    vocabularies::UInt32
    recursive_anchor::Bool
    anchors::Dict{String,String}
    dynamic_anchors::Dict{String,String}
end

# The target of a reference: a value in a document, and the resource it belongs to.
struct SchemaTarget
    document::UInt32
    pointer::String
    value::Int
    resource::UInt32
end

struct DialectInfo
    dialect::Dialect
    vocabularies::UInt32
end

mutable struct SchemaLoader
    documents::Vector{SchemaDocument}
    resources::Vector{SchemaResource}
    resources_by_uri::Dict{String,UInt32}
    documents_by_uri::Dict{String,UInt32}
    metaschema_info::Dict{String,DialectInfo}
    metaschema_loading::Vector{String}
    reference_cache::Dict{Tuple{UInt32,String},Union{Nothing,SchemaTarget}}
    options::CompileOptions
end

function SchemaLoader(options::CompileOptions)
    return SchemaLoader(SchemaDocument[], SchemaResource[], Dict{String,UInt32}(), Dict{String,UInt32}(),
        Dict{String,DialectInfo}(), String[], Dict{Tuple{UInt32,String},Union{Nothing,SchemaTarget}}(), options)
end

# The text of a string value, or nothing when n is not one.
function string_value(d::Document, n::Int)
    (n < 0 || kind(d, n) != KIND_STRING) && return nothing
    return String(str(d, n))
end

# The value of a property of n, or -1 when n is not an object or has no such property.
function member(d::Document, n::Int, name::String)
    (n < 0 || kind(d, n) != KIND_OBJECT) && return -1
    return property(d, n, name)
end

is_kind(d::Document, n::Int, k::UInt8) = n >= 0 && kind(d, n) == k

function is_schema_value(d::Document, n::Int)
    k = kind(d, n)
    return k == KIND_BOOL || k == KIND_OBJECT
end

function root_resource(l::SchemaLoader, doc::UInt32)
    d = l.documents[doc]
    return d.resource_of[d.doc.root]
end

function load_root!(l::SchemaLoader, schema::Document)
    uri = l.options.base_uri != "" ? normalize_uri(l.options.base_uri) : DEFAULT_ROOT_URI
    return root_resource(l, add_document!(l, uri, schema))
end

function load_root_from_uri!(l::SchemaLoader, uri::String)
    normalized = normalize_uri(uri)
    try_load_document!(l, normalized) ||
        throw(CompileError("Unable to resolve the schema document '" * uri * "'."))
    return root_resource(l, l.documents_by_uri[normalized])
end

# The root value of a resource.
function resource_root(l::SchemaLoader, resource::UInt32)
    r = l.resources[resource]
    d = l.documents[r.document].doc
    v, _ = resolve_pointer(d, d.root, r.root_pointer)
    return v >= 0 ? v : d.root
end

function root_target(l::SchemaLoader, resource::UInt32)
    r = l.resources[resource]
    return SchemaTarget(r.document, r.root_pointer, resource_root(l, resource), resource)
end

resource_of(l::SchemaLoader, document::UInt32, value::Int) = get(l.documents[document].resource_of, value, nothing)

# Resolves a reference from a resource. The target is nothing when it does not resolve.
function try_resolve_reference!(l::SchemaLoader, from::UInt32, reference::String)
    key = (from, reference)
    haskey(l.reference_cache, key) && return l.reference_cache[key]
    target = resolve_reference!(l, from, reference)
    l.reference_cache[key] = target
    return target
end

function resolve_reference!(l::SchemaLoader, from::UInt32, reference::String)
    uri_part, fragment = split_fragment(reference)
    absolute = resolve_uri(l.resources[from].uri, uri_part)
    if !haskey(l.resources_by_uri, absolute)
        try_load_document!(l, absolute) || return nothing
        haskey(l.resources_by_uri, absolute) || return nothing
    end
    return try_resolve_fragment(l, l.resources_by_uri[absolute], decode_fragment(fragment))
end

function try_resolve_fragment(l::SchemaLoader, resource::UInt32, fragment::String)
    fragment == "" && return root_target(l, resource)
    r = l.resources[resource]
    d = l.documents[r.document].doc
    if codeunit(fragment, 1) == UInt8('/')
        value, path = resolve_pointer(d, resource_root(l, resource), fragment)
        value < 0 && return nothing
        owner = resource_of(l, r.document, value)
        return SchemaTarget(r.document, r.root_pointer * path, value, owner === nothing ? resource : owner)
    end
    anchor = get(r.anchors, fragment, nothing)
    anchor === nothing && return nothing
    value, _ = resolve_pointer(d, d.root, anchor)
    value < 0 && return nothing
    return SchemaTarget(r.document, anchor, value, resource)
end

default_dialect_info(l::SchemaLoader) = DialectInfo(l.options.default_dialect, VOCAB_ALL_ANNOTATING)

function get_dialect_info!(l::SchemaLoader, schema_uri::String)
    # The standard metaschema URIs, as usually written, need no URI normalisation.
    plain = endswith(schema_uri, "#") ? bytes_sub(schema_uri, 1, ncodeunits(schema_uri) - 1) : schema_uri
    d = known_dialect(plain)
    d === nothing || return DialectInfo(d, VOCAB_ALL_ANNOTATING)
    normalized = normalize_uri(schema_uri)
    d = known_dialect(normalized)
    d === nothing || return DialectInfo(d, VOCAB_ALL_ANNOTATING)
    haskey(l.metaschema_info, normalized) && return l.metaschema_info[normalized]
    normalized in l.metaschema_loading && return default_dialect_info(l)
    push!(l.metaschema_loading, normalized)
    info = try
        load_metaschema_info!(l, normalized)
    finally
        pop!(l.metaschema_loading)
    end
    l.metaschema_info[normalized] = info
    return info
end

function load_metaschema_info!(l::SchemaLoader, normalized::String)
    if !haskey(l.resources_by_uri, normalized)
        try_load_document!(l, normalized) || return default_dialect_info(l)
        haskey(l.resources_by_uri, normalized) || return default_dialect_info(l)
    end
    meta_resource = l.resources_by_uri[normalized]
    vocabularies = VOCAB_ALL_ANNOTATING
    r = l.resources[meta_resource]
    d = l.documents[r.document].doc
    v = member(d, resource_root(l, meta_resource), "\$vocabulary")
    if is_kind(d, v, KIND_OBJECT)
        vocabularies = VOCAB_CORE
        k = first(d, v)
        for i in 0:count(d, v)-1
            vocabularies |= vocabulary_flag(String(str(d, k + 2i)))
        end
    end
    return DialectInfo(l.resources[meta_resource].dialect, vocabularies)
end

# The document a resolver returned, as a Document, or nothing.
resolved_document(doc::Document) = doc
resolved_document(::Nothing) = nothing
resolved_document(text::Union{AbstractString,AbstractVector{UInt8}}) = parse_document(text)

function try_load_document!(l::SchemaLoader, absolute_uri::String)
    haskey(l.documents_by_uri, absolute_uri) && return true
    resolver = l.options.resolver
    if resolver !== nothing
        doc = resolved_document(resolver(absolute_uri))
        if doc !== nothing
            add_document!(l, absolute_uri, doc)
            return true
        end
    end
    text = metaschema(absolute_uri)
    if text !== nothing
        doc = try
            parse_document(text)
        catch
            throw(CompileError("The embedded metaschema '" * absolute_uri * "' is not valid JSON."))
        end
        add_document!(l, absolute_uri, doc)
        return true
    end
    return false
end

function add_document!(l::SchemaLoader, uri::String, doc::Document)
    push!(l.documents, SchemaDocument(doc, Dict{Int,UInt32}()))
    id = UInt32(length(l.documents))
    l.documents_by_uri[uri] = id
    info = default_dialect_info(l)
    s = string_value(doc, member(doc, doc.root, "\$schema"))
    if s !== nothing
        info = get_dialect_info!(l, s)
    end
    resource = create_resource!(l, id, "", uri, info)
    walk!(l, id, doc.root, "", resource, true)
    return id
end

function create_resource!(l::SchemaLoader, document::UInt32, pointer::String, uri::String, info::DialectInfo)
    push!(l.resources, SchemaResource(document, pointer, uri, info.dialect, info.vocabularies, false,
        Dict{String,String}(), Dict{String,String}()))
    id = UInt32(length(l.resources))
    haskey(l.resources_by_uri, uri) || (l.resources_by_uri[uri] = id)
    return id
end

function add_anchor!(l::SchemaLoader, resource::UInt32, name::String, pointer::String)
    anchors = l.resources[resource].anchors
    haskey(anchors, name) || (anchors[name] = pointer)
    return nothing
end

function walk!(l::SchemaLoader, doc::UInt32, element::Int, pointer::String, resource::UInt32,
    is_resource_root::Bool)
    d = l.documents[doc].doc
    if kind(d, element) != KIND_OBJECT
        l.documents[doc].resource_of[element] = resource
        return nothing
    end
    info = DialectInfo(l.resources[resource].dialect, l.resources[resource].vocabularies)
    if !is_resource_root
        s = string_value(d, property(d, element, "\$schema"))
        if s !== nothing
            info = get_dialect_info!(l, s)
        end
    end
    dialect = info.dialect

    legacy_ref_overrides_siblings = is_legacy(dialect) && is_kind(d, property(d, element, "\$ref"), KIND_STRING)
    if !legacy_ref_overrides_siblings
        id = string_value(d, property(d, element, dialect == Draft4 ? "id" : "\$id"))
        if id !== nothing
            uri_part, fragment = split_fragment(id)
            if uri_part == ""
                if fragment != "" && is_legacy(dialect)
                    add_anchor!(l, resource, fragment, pointer)
                end
            else
                absolute = resolve_uri(l.resources[resource].uri, uri_part)
                if !is_resource_root || absolute != l.resources[resource].uri
                    if is_resource_root
                        haskey(l.resources_by_uri, absolute) || (l.resources_by_uri[absolute] = resource)
                        l.resources[resource].uri = absolute
                    else
                        resource = create_resource!(l, doc, pointer, absolute, info)
                        is_resource_root = true
                    end
                end
                if fragment != "" && is_legacy(dialect)
                    add_anchor!(l, resource, fragment, pointer)
                end
            end
        end
        if dialect >= Draft201909
            a = string_value(d, property(d, element, "\$anchor"))
            a === nothing || add_anchor!(l, resource, a, pointer)
        end
        if dialect >= Draft202012
            name = string_value(d, property(d, element, "\$dynamicAnchor"))
            if name !== nothing
                dynamic = l.resources[resource].dynamic_anchors
                haskey(dynamic, name) || (dynamic[name] = pointer)
                add_anchor!(l, resource, name, pointer)
            end
        end
        if dialect == Draft201909 && is_resource_root
            v = property(d, element, "\$recursiveAnchor")
            if is_kind(d, v, KIND_BOOL) && boolean(d, v)
                l.resources[resource].recursive_anchor = true
            end
        end
    end

    l.documents[doc].resource_of[element] = resource

    k = first(d, element)
    for i in 0:count(d, element)-1
        name = String(str(d, k + 2i))
        value = k + 2i + 1
        sub_kind = subschema_kind_of(name, dialect, legacy_ref_overrides_siblings)
        sub_kind == SUBSCHEMA_NONE && continue
        base = pointer * "/" * escape_pointer_token(name)
        if sub_kind == SUBSCHEMA_SINGLE
            is_schema_value(d, value) && walk!(l, doc, value, base, resource, false)
        elseif sub_kind == SUBSCHEMA_SINGLE_OR_ARRAY || sub_kind == SUBSCHEMA_ARRAY
            if kind(d, value) == KIND_ARRAY
                c = first(d, value)
                for j in 0:count(d, value)-1
                    walk!(l, doc, c + j, base * "/" * string(j), resource, false)
                end
            elseif sub_kind == SUBSCHEMA_SINGLE_OR_ARRAY && is_schema_value(d, value)
                walk!(l, doc, value, base, resource, false)
            end
        elseif sub_kind == SUBSCHEMA_MAP
            if kind(d, value) == KIND_OBJECT
                c = first(d, value)
                for j in 0:count(d, value)-1
                    entry = c + 2j
                    if is_schema_value(d, entry + 1)
                        p = base * "/" * escape_pointer_token(String(str(d, entry)))
                        walk!(l, doc, entry + 1, p, resource, false)
                    end
                end
            end
        end
    end
    return nothing
end
