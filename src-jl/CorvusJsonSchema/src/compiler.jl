# Compiles loaded schema documents into a SchemaNode graph and runs the compile-time analyses. Ported from
# compiler.go.

# The keywords compile_node! handles itself. Anything else is an unknown keyword (an annotation from 2019-09).
const KNOWN_KEYWORDS = Set{String}([
    "if", "not", "type", "enum", "\$ref", "then", "else", "const", "items", "allOf", "anyOf", "oneOf", "title",
    "\$defs", "format", "pattern", "maximum", "minimum", "default", "\$schema", "\$anchor", "required", "contains",
    "maxItems", "minItems", "examples", "readOnly", "\$comment", "maxLength", "minLength", "writeOnly", "properties",
    "multipleOf", "deprecated", "uniqueItems", "prefixItems", "minContains", "maxContains", "description",
    "\$vocabulary", "definitions", "\$dynamicRef", "dependencies", "propertyNames", "maxProperties", "minProperties",
    "contentSchema", "\$recursiveRef", "\$dynamicAnchor", "contentEncoding", "additionalItems", "exclusiveMaximum",
    "exclusiveMinimum", "unevaluatedItems", "contentMediaType", "\$recursiveAnchor", "dependentSchemas",
    "patternProperties", "dependentRequired", "additionalProperties", "unevaluatedProperties", "id", "\$id",
])

mutable struct PendingDynamicRef
    node::NodeId
    anchor::String
    is_recursive::Bool
    initial_target::SchemaTarget
    seen_resources::Vector{Bool}
    candidates::Vector{ResourceNode}
end

# What a node's annotations are computed from, on the first evaluation with a collector.
struct AnnotationSource
    set::Bool
    doc::Document
    value::Int
    vocab::UInt32
    content::Bool
end

AnnotationSource() = AnnotationSource(false, EMPTY_DOCUMENT, -1, VOCAB_NONE, false)

# The compiled program: the node graph, its entry node and whether it keeps a dynamic scope.
struct CompiledSchema
    nodes::Vector{SchemaNode}
    root::NodeId
    uses_dynamic_scope::Bool
    annotation_sources::Vector{AnnotationSource}
end

mutable struct SchemaCompiler
    loader::SchemaLoader
    options::CompileOptions
    nodes::Vector{SchemaNode}
    targets::Vector{SchemaTarget}
    node_of::Dict{Tuple{UInt32,Int},NodeId}
    worklist_head::Int
    pending_dynamic_refs::Vector{PendingDynamicRef}
    annotation_sources::Vector{AnnotationSource}
    entry_node::NodeId
end

node(c::SchemaCompiler, id::NodeId) = c.nodes[id+1]

# A non-negative integer keyword value.
function uint_value(d::Document, n::Int)
    (n < 0 || kind(d, n) != KIND_NUMBER) && return OptCount()
    f = flags(d, n)
    if f == NUM_INT
        v = data(d, n) % Int64
        return v >= 0 ? OptCount(true, v % UInt64) : OptCount()
    elseif f == NUM_UINT
        return OptCount(true, data(d, n))
    end
    x = reinterpret(Float64, data(d, n))
    if x >= 0 && x == floor(x) && x < 1.8e19
        return OptCount(true, unsafe_trunc(UInt64, x))
    end
    return OptCount()
end

function type_mask_of(d::Document, n::Int)
    kind(d, n) != KIND_STRING && return 0x00
    s = String(str(d, n))
    s == "null" && return TYPE_NULL
    s == "boolean" && return TYPE_BOOLEAN
    s == "object" && return TYPE_OBJECT
    s == "array" && return TYPE_ARRAY
    s == "number" && return TYPE_NUMBER
    s == "string" && return TYPE_STRING
    s == "integer" && return TYPE_INTEGER
    return 0x00
end

# The strings of an array (other items are skipped).
function string_items(d::Document, array::Int)
    out = String[]
    c = first(d, array)
    for i in 0:count(d, array)-1
        kind(d, c + i) == KIND_STRING && push!(out, String(str(d, c + i)))
    end
    return out
end

function compile_document(schema::Document, options::CompileOptions)
    loader = SchemaLoader(options)
    root = load_root!(loader, schema)
    return compile_loaded(loader, root, options)
end

function compile_from_uri(uri::String, options::CompileOptions)
    loader = SchemaLoader(options)
    root = load_root_from_uri!(loader, uri)
    return compile_loaded(loader, root, options)
end

function compile_loaded(loader::SchemaLoader, root::UInt32, options::CompileOptions)
    target = root_target(loader, root)
    if options.has_entry_point
        t = try_resolve_reference!(loader, root, options.entry_point)
        t === nothing && throw(CompileError("Unable to resolve the entry point '" * options.entry_point * "'."))
        target = t
    end
    c = SchemaCompiler(loader, options, SchemaNode[], SchemaTarget[], Dict{Tuple{UInt32,Int},NodeId}(), 0,
        PendingDynamicRef[], AnnotationSource[], NO_NODE)
    c.entry_node = get_node!(c, target)
    compile_all!(c)
    uses_dynamic_scope = any(n -> n.dynamic_ref !== nothing, c.nodes)
    analyse!(c)
    return CompiledSchema(c.nodes, c.entry_node, uses_dynamic_scope, c.annotation_sources)
end

function get_node!(c::SchemaCompiler, target::SchemaTarget)
    key = (target.document, target.value)
    existing = get(c.node_of, key, NO_NODE)
    existing >= 0 && return existing
    id = NodeId(length(c.nodes))
    resource = c.loader.resources[target.resource]
    push!(c.nodes, SchemaNode(target.resource, resource.dialect, target.pointer))
    push!(c.targets, target)
    push!(c.annotation_sources, AnnotationSource())
    c.node_of[key] = id
    return id
end

function drain_worklist!(c::SchemaCompiler)
    while c.worklist_head < length(c.nodes)
        id = c.worklist_head
        c.worklist_head += 1
        compile_node!(c, NodeId(id), c.targets[id+1])
    end
    return nothing
end

function compile_all!(c::SchemaCompiler)
    while true
        drain_worklist!(c)
        if isempty(c.pending_dynamic_refs) || (!expand_dynamic_refs!(c) && c.worklist_head == length(c.nodes))
            break
        end
    end
    # Most schemas have no dynamic reference: skip the finalisation.
    isempty(c.pending_dynamic_refs) || finalize_dynamic_refs!(c)
    return nothing
end

function child_node!(c::SchemaCompiler, parent::SchemaTarget, value::Int, relative::String)
    resource = resource_of(c.loader, parent.document, value)
    return get_node!(c, SchemaTarget(parent.document, parent.pointer * relative, value,
        resource === nothing ? parent.resource : resource))
end

# The children of an array keyword, or nothing when the value is not an array.
function child_array!(c::SchemaCompiler, parent::SchemaTarget, d::Document, value::Int, keyword::String)
    is_kind(d, value, KIND_ARRAY) || return nothing
    first_item = first(d, value)
    return NodeId[child_node!(c, parent, first_item + i, "/" * keyword * "/" * string(i))
                  for i in 0:count(d, value)-1]
end

function compile_node!(c::SchemaCompiler, id::NodeId, target::SchemaTarget)
    d = c.loader.documents[target.document].doc
    e = target.value
    n = node(c, id)
    k = kind(d, e)
    if k == KIND_BOOL
        n.always_true = boolean(d, e)
        n.always_false = !n.always_true
        return nothing
    elseif k != KIND_OBJECT
        n.always_true = true
        return nothing
    end
    prop(name::String) = property(d, e, name)
    has(name::String) = property(d, e, name) >= 0

    resource = c.loader.resources[target.resource]
    dialect, voc = resource.dialect, resource.vocabularies
    legacy = is_legacy(dialect)

    if legacy
        r = string_value(d, prop("\$ref"))
        if r !== nothing
            # In draft 7 and earlier, $ref replaces every sibling keyword.
            compile_ref!(c, id, target, r)
            return nothing
        end
    end

    applicator = legacy || (voc & VOCAB_APPLICATOR) != 0
    validation = legacy || (voc & VOCAB_VALIDATION) != 0
    unevaluated = dialect == Draft201909 ? applicator : (voc & VOCAB_UNEVALUATED) != 0
    content = legacy || (voc & VOCAB_CONTENT) != 0
    format_assert = (legacy && c.options.assert_format_in_legacy_drafts) || (voc & VOCAB_FORMAT_ASSERTION) != 0
    if c.options.assert_format != 0
        format_assert = c.options.assert_format > 0
    end
    function single(name::String)
        v = prop(name)
        return v >= 0 ? child_node!(c, target, v, "/" * escape_pointer_token(name)) : NO_NODE
    end
    function array_of(value::Int, keyword::String)
        list = child_array!(c, target, d, value, keyword)
        return list === nothing ? (NodeId[], false) : (list, true)
    end

    dependencies = DependencyEntry[]
    has_dependencies = false

    # References.
    r = string_value(d, prop("\$ref"))
    r === nothing || compile_ref!(c, id, target, r)
    if dialect >= Draft202012
        r = string_value(d, prop("\$dynamicRef"))
        r === nothing || compile_dynamic_ref!(c, id, target, r, false)
    end
    if dialect == Draft201909
        r = string_value(d, prop("\$recursiveRef"))
        r === nothing || compile_dynamic_ref!(c, id, target, r, true)
    end

    # Applicators.
    if applicator
        n.all_of, n.has_all_of = array_of(prop("allOf"), "allOf")
        n.any_of, n.has_any_of = array_of(prop("anyOf"), "anyOf")
        n.one_of, n.has_one_of = array_of(prop("oneOf"), "oneOf")
        n.not = single("not")
        if dialect >= Draft7
            n.if_ = single("if")
            if n.if_ >= 0
                n.then_ = single("then")
                n.else_ = single("else")
            end
        end
        props = prop("properties")
        if is_kind(d, props, KIND_OBJECT)
            n.has_properties = true
            p = first(d, props)
            for i in 0:count(d, props)-1
                name = String(str(d, p + 2i))
                child = child_node!(c, target, p + 2i + 1, "/properties/" * escape_pointer_token(name))
                push!(n.properties, NamedNode(name, Vector{UInt8}(codeunits(name)), child))
            end
        end
        pp = prop("patternProperties")
        if is_kind(d, pp, KIND_OBJECT)
            n.has_pattern_properties = true
            p = first(d, pp)
            for i in 0:count(d, pp)-1
                source = String(str(d, p + 2i))
                compiled = compile_pattern(source)
                compiled === nothing &&
                    throw(CompileError("Invalid regular expression '" * source * "' in patternProperties."))
                child = child_node!(c, target, p + 2i + 1, "/patternProperties/" * escape_pointer_token(source))
                push!(n.pattern_properties, PatternProperty(compiled, child))
            end
        end
        n.additional_properties = single("additionalProperties")
        if dialect >= Draft6
            n.property_names_schema = single("propertyNames")
            n.contains = single("contains")
        end

        # "dependencies" is honoured in every dialect: in 2019-09 and later it is an optional compatibility keyword.
        if is_kind(d, prop("dependencies"), KIND_OBJECT) ||
           (dialect >= Draft201909 && is_kind(d, prop("dependentSchemas"), KIND_OBJECT))
            dependencies = compile_dependency_schemas!(c, target, d, e, dialect)
            has_dependencies = true
        end

        # Array applicators.
        if dialect >= Draft202012
            n.prefix_items, n.has_prefix_items = array_of(prop("prefixItems"), "prefixItems")
            v = prop("items")
            if v >= 0 && kind(d, v) != KIND_ARRAY
                n.items = single("items")
            end
        else
            v = prop("items")
            if v >= 0
                if kind(d, v) == KIND_ARRAY
                    n.prefix_items, n.has_prefix_items = array_of(v, "items")
                    n.prefix_keyword = "items"
                    n.items = single("additionalItems")
                    if n.items >= 0
                        n.items_keyword = "additionalItems"
                    end
                else
                    n.items = single("items")
                end
            end
        end
        n.contains_marks_evaluated = dialect >= Draft202012
    end

    if unevaluated && dialect >= Draft201909
        n.unevaluated_properties = single("unevaluatedProperties")
        n.unevaluated_items = single("unevaluatedItems")
    end

    if validation
        t = prop("type")
        if t >= 0
            if kind(d, t) == KIND_ARRAY
                first_type = first(d, t)
                for i in 0:count(d, t)-1
                    n.type_mask |= type_mask_of(d, first_type + i)
                end
            else
                n.type_mask = type_mask_of(d, t)
            end
            n.has_type = true
        end
        if dialect >= Draft6
            v = prop("const")
            if v >= 0
                n.const_value = ValueRef(d, v)
            end
        end
        values = prop("enum")
        if is_kind(d, values, KIND_ARRAY)
            n.has_enum = true
            first_value = first(d, values)
            for i in 0:count(d, values)-1
                push!(n.enum_values, ValueRef(d, first_value + i))
            end
        end
        req = prop("required")
        if is_kind(d, req, KIND_ARRAY)
            n.has_required = true
            n.required_list = string_items(d, req)
            n.required = unique(n.required_list)
        end
        if dialect >= Draft201909
            dr = prop("dependentRequired")
            if is_kind(d, dr, KIND_OBJECT)
                has_dependencies = true
                p = first(d, dr)
                for i in 0:count(d, dr)-1
                    v = p + 2i + 1
                    if kind(d, v) == KIND_ARRAY
                        push!(dependencies, DependencyEntry(KEYWORD_DEPENDENT_REQUIRED, String(str(d, p + 2i)), true,
                            string_items(d, v), NO_NODE))
                    end
                end
            end
        end
        n.min_properties = uint_value(d, prop("minProperties"))
        n.max_properties = uint_value(d, prop("maxProperties"))
        n.min_items = uint_value(d, prop("minItems"))
        n.max_items = uint_value(d, prop("maxItems"))
        v = prop("uniqueItems")
        if is_kind(d, v, KIND_BOOL)
            n.unique_items = boolean(d, v)
        end
        n.min_length = uint_value(d, prop("minLength"))
        n.max_length = uint_value(d, prop("maxLength"))
        p = string_value(d, prop("pattern"))
        if p !== nothing
            compiled = compile_pattern(p)
            compiled === nothing && throw(CompileError("Invalid regular expression '" * p * "' in pattern."))
            n.pattern = compiled
        end
        function num(name::String)
            v = prop(name)
            return is_kind(d, v, KIND_NUMBER) ? ValueRef(d, v) : NO_VALUE
        end
        n.multiple_of = num("multipleOf")
        if is_set(n.multiple_of)
            n.divisor = Divisor(d, n.multiple_of.n)
        end
        function is_true(name::String)
            v = prop(name)
            return is_kind(d, v, KIND_BOOL) && boolean(d, v)
        end
        if dialect == Draft4
            # Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive.
            if is_true("exclusiveMaximum")
                n.exclusive_maximum = num("maximum")
            else
                n.maximum = num("maximum")
            end
            if is_true("exclusiveMinimum")
                n.exclusive_minimum = num("minimum")
            else
                n.minimum = num("minimum")
            end
        else
            n.maximum = num("maximum")
            n.minimum = num("minimum")
            n.exclusive_maximum = num("exclusiveMaximum")
            n.exclusive_minimum = num("exclusiveMinimum")
        end
        if dialect >= Draft201909 && n.contains >= 0
            if has("minContains")
                n.min_contains = 1
                v = uint_value(d, prop("minContains"))
                if v.set
                    n.min_contains = v.n
                end
            end
            if has("maxContains")
                n.max_contains = uint_value(d, prop("maxContains"))
            end
        end
    end

    f = string_value(d, prop("format"))
    if f !== nothing
        n.has_format = true
        n.format = f
        n.format_kind = format_kind_of(f, dialect)
        n.assert_format = format_assert
    end

    # Content keywords are asserted only in draft 7 (and annotations elsewhere).
    if content && dialect >= Draft7 && (has("contentEncoding") || has("contentMediaType"))
        base64 = string_value(d, prop("contentEncoding")) == "base64"
        json = string_value(d, prop("contentMediaType")) == "application/json"
        if base64 && json
            n.content = CONTENT_BASE64_JSON
        elseif base64
            n.content = CONTENT_BASE64
        elseif json
            n.content = CONTENT_JSON
        end
        n.assert_content = dialect == Draft7 && c.options.assert_content && n.content != CONTENT_NONE
    end

    if has_dependencies
        # In the order the three keywords appear in the schema.
        function order(keyword::String)
            p = first(d, e)
            for i in 0:count(d, e)-1
                bytes_equal(str(d, p + 2i), keyword) && return i
            end
            return typemax(Int)
        end
        sort!(dependencies; by=dep -> order(dep.keyword), alg=MergeSort)
    end
    n.dependencies, n.has_dependencies = dependencies, has_dependencies
    if length(n.properties) > PROPERTY_MAP_THRESHOLD
        n.property_names = Names(String[p.name for p in n.properties])
    end
    c.annotation_sources[id+1] = AnnotationSource(true, d, e, voc, content)
    return nothing
end

# A node with more properties than this finds them through a name table.
const PROPERTY_MAP_THRESHOLD = 8

function compile_dependency_schemas!(c::SchemaCompiler, target::SchemaTarget, d::Document, e::Int, dialect::Dialect)
    out = DependencyEntry[]
    deps = property(d, e, "dependencies")
    if is_kind(d, deps, KIND_OBJECT)
        k = first(d, deps)
        for i in 0:count(d, deps)-1
            name = String(str(d, k + 2i))
            v = k + 2i + 1
            if kind(d, v) == KIND_ARRAY
                push!(out, DependencyEntry(KEYWORD_DEPENDENCIES, name, true, string_items(d, v), NO_NODE))
            else
                child = child_node!(c, target, v, "/dependencies/" * escape_pointer_token(name))
                push!(out, DependencyEntry(KEYWORD_DEPENDENCIES, name, false, String[], child))
            end
        end
    end
    if dialect >= Draft201909
        ds = property(d, e, "dependentSchemas")
        if is_kind(d, ds, KIND_OBJECT)
            k = first(d, ds)
            for i in 0:count(d, ds)-1
                name = String(str(d, k + 2i))
                child = child_node!(c, target, k + 2i + 1, "/dependentSchemas/" * escape_pointer_token(name))
                push!(out, DependencyEntry(KEYWORD_DEPENDENT_SCHEMAS, name, false, String[], child))
            end
        end
    end
    return out
end

function resolve_or_fail!(c::SchemaCompiler, target::SchemaTarget, reference::String)
    t = try_resolve_reference!(c.loader, target.resource, reference)
    t === nothing && throw(CompileError("Unable to resolve reference '" * reference * "' from '" *
                                        c.loader.resources[target.resource].uri * "'."))
    return t
end

function compile_ref!(c::SchemaCompiler, id::NodeId, target::SchemaTarget, reference::String)
    resolved = resolve_or_fail!(c, target, reference)
    child = get_node!(c, resolved)
    node(c, id).ref = child
    return nothing
end

dynamic_keyword(is_recursive::Bool) = is_recursive ? "\$recursiveRef" : "\$dynamicRef"

function compile_dynamic_ref!(c::SchemaCompiler, id::NodeId, target::SchemaTarget, reference::String,
    is_recursive::Bool)
    resolved = resolve_or_fail!(c, target, reference)
    _, raw_fragment = split_fragment(reference)
    fragment = decode_fragment(raw_fragment)
    res = c.loader.resources[resolved.resource]
    dynamic = false
    if is_recursive
        dynamic = res.recursive_anchor && resolved.pointer == res.root_pointer
    elseif fragment != "" && codeunit(fragment, 1) != UInt8('/')
        dynamic = get(res.dynamic_anchors, fragment, nothing) == resolved.pointer
    end
    if !dynamic
        # A static reference, kept apart from any sibling $ref (both apply).
        child = get_node!(c, resolved)
        n = node(c, id)
        n.static_dynamic_ref = child
        n.static_dynamic_keyword = dynamic_keyword(is_recursive)
        return nothing
    end
    push!(c.pending_dynamic_refs, PendingDynamicRef(id, fragment, is_recursive, resolved, Bool[], ResourceNode[]))
    return nothing
end

function expand_dynamic_refs!(c::SchemaCompiler)
    added = false
    for pending in c.pending_dynamic_refs
        resource = 1
        while resource <= length(c.loader.resources)
            while length(pending.seen_resources) < resource
                push!(pending.seen_resources, false)
            end
            if !pending.seen_resources[resource]
                pending.seen_resources[resource] = true
                r = c.loader.resources[resource]
                pointer = nothing
                if pending.is_recursive
                    pointer = r.recursive_anchor ? r.root_pointer : nothing
                else
                    pointer = get(r.dynamic_anchors, pending.anchor, nothing)
                end
                if pointer !== nothing
                    before = length(c.nodes)
                    child = get_node!(c, target_in(c, UInt32(resource), pointer))
                    added = added || length(c.nodes) != before
                    push!(pending.candidates, ResourceNode(UInt32(resource), child))
                end
            end
            resource += 1
        end
    end
    return added
end

function target_in(c::SchemaCompiler, resource::UInt32, pointer::String)
    r = c.loader.resources[resource]
    fragment = pointer != r.root_pointer ? bytes_sub(pointer, ncodeunits(r.root_pointer) + 1, ncodeunits(pointer)) : ""
    t = try_resolve_fragment(c.loader, resource, fragment)
    return t === nothing ? root_target(c.loader, resource) : t
end

function finalize_dynamic_refs!(c::SchemaCompiler)
    reachable = nothing
    pending = c.pending_dynamic_refs
    c.pending_dynamic_refs = PendingDynamicRef[]
    for p in pending
        fallback = get_node!(c, p.initial_target)
        function set_static(t::NodeId)
            n = node(c, p.node)
            n.static_dynamic_ref = t
            n.static_dynamic_keyword = dynamic_keyword(p.is_recursive)
        end
        if length(p.candidates) <= 1
            # Only the initial target's resource defines the anchor: resolution is static.
            set_static(fallback)
            continue
        end
        # The dynamic scope is searched outermost first and its outermost entry is always the resource evaluation
        # started in. When the entry resource defines the anchor, that target is the answer on every path.
        if reachable === nothing
            reachable = compute_reachability(c, pending)
        end
        if !reachable[p.node+1]
            set_static(fallback)
            continue
        end
        entry_resource = node(c, c.entry_node).resource_id
        uniform = NO_NODE
        for candidate in p.candidates
            if candidate.resource == entry_resource
                uniform = candidate.node
                break
            end
        end
        if uniform >= 0
            set_static(uniform)
            continue
        end
        node(c, p.node).dynamic_ref = DynamicRefTarget(p.is_recursive, fallback, p.candidates)
    end
    drain_worklist!(c)
    return nothing
end

# Marks the nodes reachable from the entry, counting every candidate of a pending dynamic reference as a child.
function compute_reachability(c::SchemaCompiler, pending::Vector{PendingDynamicRef})
    extra = Dict{NodeId,Vector{NodeId}}()
    for p in pending
        list = get!(() -> NodeId[], extra, p.node)
        push!(list, get_node!(c, p.initial_target))
        for candidate in p.candidates
            push!(list, candidate.node)
        end
    end
    reached = fill(false, length(c.nodes))
    stack = NodeId[c.entry_node]
    reached[c.entry_node+1] = true
    while !isempty(stack)
        id = pop!(stack)
        for child in vcat(children(node(c, id)), get(extra, id, NodeId[]))
            if child < length(reached) && !reached[child+1]
                reached[child+1] = true
                push!(stack, child)
            end
        end
    end
    return reached
end

# ----------------------------------------------------------------------------------------------------------------------
# Analyses

function analyse!(c::SchemaCompiler)
    edges = Vector{Vector{NodeId}}(undef, length(c.nodes))
    in_place, branches = false, false
    for (i, n) in enumerate(c.nodes)
        edges[i] = in_place_children(n, true)
        in_place = in_place || !isempty(edges[i])
        branches = branches || n.has_one_of || n.has_any_of
    end
    if in_place
        compute_marking!(c, edges)
        compute_in_place_cycles!(c, edges)
    else
        compute_marking!(c, nothing)
    end
    branches && compute_discriminators!(c)
    return nothing
end

# Works out which nodes can contribute evaluated-property/item annotations: a node marks if it has the keywords
# itself or any in-place child (not counting not) marks.
function compute_marking!(c::SchemaCompiler, edges::Union{Nothing,Vector{Vector{NodeId}}})
    for n in c.nodes
        n.marks_properties = n.has_properties || n.has_pattern_properties || n.additional_properties >= 0 ||
                             n.unevaluated_properties >= 0
        n.marks_items = n.has_prefix_items || n.items >= 0 || (n.contains >= 0 && n.contains_marks_evaluated) ||
                        n.unevaluated_items >= 0
    end
    edges === nothing && return nothing
    parents = [NodeId[] for _ in c.nodes]
    for (i, edge) in enumerate(edges)
        # not does not contribute annotations: nodes with one use the edges without it.
        list = c.nodes[i].not >= 0 ? in_place_children(c.nodes[i], false) : edge
        for child in list
            push!(parents[child+1], NodeId(i - 1))
        end
    end
    for which in 1:2
        flag(n::SchemaNode) = which == 1 ? n.marks_properties : n.marks_items
        work = NodeId[NodeId(i - 1) for (i, n) in enumerate(c.nodes) if flag(n)]
        while !isempty(work)
            w = pop!(work)
            for p in parents[w+1]
                n = node(c, p)
                if !flag(n)
                    if which == 1
                        n.marks_properties = true
                    else
                        n.marks_items = true
                    end
                    push!(work, p)
                end
            end
        end
    end
    return nothing
end

# Marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need a depth guard.
function compute_in_place_cycles!(c::SchemaCompiler, edges::Vector{Vector{NodeId}})
    n = length(c.nodes)
    index = fill(Int32(-1), n)
    low = zeros(Int32, n)
    on_stack = fill(false, n)
    stack = Int[]
    next = Int32(0)
    # Frames of (node, next edge), zero-based.
    work = Tuple{Int,Int}[]
    for start in 0:n-1
        index[start+1] >= 0 && continue
        push!(work, (start, 0))
        index[start+1] = low[start+1] = next
        next += Int32(1)
        push!(stack, start)
        on_stack[start+1] = true
        while !isempty(work)
            v, edge = work[end]
            if edge < length(edges[v+1])
                w = Int(edges[v+1][edge+1])
                work[end] = (v, edge + 1)
                if index[w+1] < 0
                    index[w+1] = low[w+1] = next
                    next += Int32(1)
                    push!(stack, w)
                    on_stack[w+1] = true
                    push!(work, (w, 0))
                elseif on_stack[w+1]
                    low[v+1] = min(low[v+1], index[w+1])
                end
                continue
            end
            pop!(work)
            if !isempty(work)
                parent = work[end][1]
                low[parent+1] = min(low[parent+1], low[v+1])
            end
            if low[v+1] == index[v+1]
                component = Int[]
                while true
                    w = pop!(stack)
                    on_stack[w+1] = false
                    push!(component, w)
                    w == v && break
                end
                self_loop = any(w -> Int(w) == v, edges[v+1])
                if length(component) > 1 || self_loop
                    for w in component
                        c.nodes[w+1].in_place_cycle = true
                    end
                end
            end
        end
    end
    return nothing
end

# Follows pure $ref nodes to the node that carries constraints.
function effective_node(c::SchemaCompiler, id::NodeId)
    n = node(c, id)
    hops = 0
    while hops < 16 && is_pure_ref(n)
        n = node(c, n.ref)
        hops += 1
    end
    return n
end

function compute_discriminators!(c::SchemaCompiler)
    for n in c.nodes
        if length(n.one_of) > 1
            n.one_of_discriminator = build_discriminator(c, n.one_of)
        end
        if length(n.any_of) > 1
            n.any_of_discriminator = build_discriminator(c, n.any_of)
        end
    end
    return nothing
end

# The constraint a branch's properties[X] places on the value: positive (const/enum of primitives), negative (a
# string not in an enum) or wildcard (anything else).
const CLASS_WILDCARD = 0
const CLASS_POSITIVE = 1
const CLASS_NEGATIVE = 2

struct ValueClass
    kind::Int
    set::Vector{DiscriminatorValue}
end

ValueClass() = ValueClass(CLASS_WILDCARD, DiscriminatorValue[])

class_contains(c::ValueClass, v::DiscriminatorValue) = any(x -> same(x, v), c.set)

# Classifies branches by the constraint their properties[X] places on the value, and builds the table from values to
# branches.
function build_discriminator(c::SchemaCompiler, branches::Vector{NodeId})
    candidates = String[]
    for b in branches
        eff = effective_node(c, b)
        eff.has_properties || continue
        for p in eff.properties
            classify(c, eff, p.name).kind != CLASS_WILDCARD && push!(candidates, p.name)
        end
        break
    end
    for name in candidates
        classes = [classify(c, effective_node(c, b), name) for b in branches]
        constrained = Base.count(class -> class.kind != CLASS_WILDCARD, classes)
        constrained < 2 && continue
        values = DiscriminatorValue[]
        for class in classes, v in class.set
            any(x -> same(x, v), values) || push!(values, v)
        end
        known = DiscriminatorEntry[]
        for v in values
            selected = UInt32[]
            for (b, class) in enumerate(classes)
                contains = class_contains(class, v)
                pick = class.kind == CLASS_POSITIVE ? contains : (class.kind == CLASS_NEGATIVE ? !contains : true)
                pick && push!(selected, UInt32(b - 1))
            end
            push!(known, DiscriminatorEntry(v, selected))
        end
        unknown = UInt32[UInt32(b - 1) for (b, class) in enumerate(classes) if class.kind != CLASS_POSITIVE]
        all_require = all(b -> name in effective_node(c, b).required, branches)
        return Discriminator(name, known, unknown, all_require)
    end
    return nothing
end

function classify(c::SchemaCompiler, branch::SchemaNode, name::String)
    child = NO_NODE
    for p in branch.properties
        if p.name == name
            child = p.node
            break
        end
    end
    child < 0 && return ValueClass()
    p = effective_node(c, child)
    if is_set(p.const_value)
        v = primitive(p.const_value)
        v === nothing || return ValueClass(CLASS_POSITIVE, [v])
    end
    if p.has_enum && !isempty(p.enum_values)
        prims = primitives(p.enum_values)
        length(prims) == length(p.enum_values) && return ValueClass(CLASS_POSITIVE, prims)
    end
    if p.not >= 0 && p.has_type && p.type_mask == TYPE_STRING
        not = effective_node(c, p.not)
        if not.has_enum && !not.has_type && !is_set(not.const_value) && !has_string_keywords(not) &&
           !has_in_place_applicators(not)
            if all(v -> kind(v.d, v.n) == KIND_STRING, not.enum_values)
                return ValueClass(CLASS_NEGATIVE, primitives(not.enum_values))
            end
        end
    end
    return ValueClass()
end

function primitives(values::Vector{ValueRef})
    out = DiscriminatorValue[]
    for v in values
        p = primitive(v)
        p === nothing || push!(out, p)
    end
    return out
end

function primitive(v::ValueRef)
    k = kind(v.d, v.n)
    k == KIND_STRING && return DiscriminatorValue(k, Vector{UInt8}(str(v.d, v.n)), NO_VALUE)
    (k == KIND_NUMBER || k == KIND_BOOL || k == KIND_NULL) && return DiscriminatorValue(k, EMPTY_BYTES, v)
    return nothing
end

# Lists the annotation keywords of a schema object, in the order the C# compiler records them.
function annotation_entries(d::Document, e::Int, dialect::Dialect, voc::UInt32, content::Bool,
    assert_format_set::Bool)
    legacy = is_legacy(dialect)
    meta_data = legacy || (voc & VOCAB_META_DATA) != 0
    format_annotate = legacy || (voc & (VOCAB_FORMAT_ANNOTATION | VOCAB_FORMAT_ASSERTION)) != 0 || assert_format_set
    out = AnnotationEntry[]
    function add(keyword::String, strings_only::Bool)
        value = String(append_json!(UInt8[], d, property(d, e, keyword)))
        push!(out, AnnotationEntry(keyword, value, strings_only))
    end
    has(name::String) = property(d, e, name) >= 0
    k = first(d, e)
    for i in 0:count(d, e)-1
        name = String(str(d, k + 2i))
        if name == "title" || name == "description" || name == "default"
            meta_data && add(name, false)
        elseif name == "examples"
            meta_data && dialect >= Draft6 && add(name, false)
        elseif name == "readOnly" || name == "writeOnly"
            meta_data && dialect >= Draft7 && add(name, false)
        elseif name == "deprecated"
            meta_data && dialect >= Draft201909 && add(name, false)
        elseif name == "format"
            kind(d, k + 2i + 1) == KIND_STRING && format_annotate && add(name, false)
        elseif dialect >= Draft201909 && !(name in KNOWN_KEYWORDS)
            # Unknown keywords are collected as annotations from 2019-09 onwards.
            add(name, false)
        end
    end
    if content && dialect >= Draft7
        has("contentEncoding") && add("contentEncoding", true)
        if has("contentMediaType")
            add("contentMediaType", true)
            # contentSchema is only meaningful alongside contentMediaType.
            has("contentSchema") && dialect >= Draft201909 && add("contentSchema", true)
        end
    end
    return out
end
