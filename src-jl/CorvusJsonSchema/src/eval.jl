# Evaluation. One general evaluator serves two modes. Without a collector it fails fast at the first violation and
# reports nothing. With a collector it is exhaustive and reports every keyword with its evaluation path, schema
# location and instance location, reproducing the C# collecting mode (keyword order, paths, messages, which subschema
# results are committed or discarded). Fail-fast evaluation mostly runs through plans (see plan.jl) and comes here
# only for nodes that track evaluated properties or items. Ported from eval.go.

function Program(c::CompiledSchema, options::CompileOptions)
    nodes = c.nodes
    names = sort!(collect(keys(options.formats)))
    p = Program(nodes, c.root, c.root, c.uses_dynamic_scope, options.max_depth, names,
        Any[options.formats[name] for name in names], NodeId[], c.annotation_sources, ReentrantLock(), nothing,
        options.assert_format != 0, Plan[])
    for id in eachindex(nodes)
        current = NodeId(id - 1)
        for _ in 1:16
            n = nodes[current+1]
            # Pure $ref hops, and forwards: a node whose only assertion is one allOf branch, unless either end is on
            # an in-place cycle, which keeps its guard.
            next = pure_ref_target(n)
            if next < 0
                next = forward_target(n)
                (next < 0 || n.in_place_cycle || nodes[next+1].in_place_cycle || next == current) && break
            end
            p.uses_dynamic_scope && nodes[next+1].resource_id != n.resource_id && break
            current = next
        end
        push!(p.fast_target, current)
    end
    p.entry = target(p, p.root)
    p.plans = compile_plans(p)
    return p
end

# The annotation keywords of every node (computed on the first evaluation with a collector).
function node_annotations(p::Program)
    return lock(p.annotations_lock) do
        existing = p.annotations
        existing === nothing || return existing
        built = Vector{Vector{AnnotationEntry}}(undef, length(p.nodes))
        for (id, n) in enumerate(p.nodes)
            src = p.annotation_sources[id]
            built[id] = src.set ?
                        annotation_entries(src.doc, src.value, n.dialect, src.vocab, src.content,
                p.assert_format_set) : AnnotationEntry[]
        end
        p.annotations = built
        return built
    end::Vector{Vector{AnnotationEntry}}
end

# The node of a declared property of a node, or NO_NODE.
function property_node(n::SchemaNode, name::Bytes)
    properties = n.properties
    names = n.property_names
    if names !== nothing
        i = find(names, name)
        return i >= 0 ? properties[i+1].node : NO_NODE
    end
    for i in eachindex(properties)
        bytes_equal(name, properties[i].bytes) && return properties[i].node
    end
    return NO_NODE
end

# Reports a node with no keyword but references and allOf.
function has_no_assertions_besides_references(n::SchemaNode)
    return !(n.has_type || is_set(n.const_value) || n.has_enum || has_number_keywords(n) ||
             has_string_keywords(n) || has_object_keywords(n) || has_array_keywords(n) ||
             n.dynamic_ref !== nothing || n.has_any_of || n.has_one_of || n.not >= 0 || n.if_ >= 0)
end

# The single reference of a node that is nothing but $ref (or a static $dynamicRef), for elision.
function pure_ref_target(n::SchemaNode)
    ((n.ref >= 0) == (n.static_dynamic_ref >= 0) || n.always_true || n.always_false) && return NO_NODE
    (n.has_all_of || !has_no_assertions_besides_references(n)) && return NO_NODE
    return n.ref >= 0 ? n.ref : n.static_dynamic_ref
end

# The branch of a node that is nothing but a one-branch allOf, for fail-fast forwarding.
function forward_target(n::SchemaNode)
    if length(n.all_of) != 1 || n.always_true || n.always_false || n.ref >= 0 || n.static_dynamic_ref >= 0 ||
       !has_no_assertions_besides_references(n)
        return NO_NODE
    end
    return n.all_of[1]
end

# Messages (the C# evaluator's text).
const MSG_EVALUATED_SUBSCHEMA = "The value was expected to match the subschema."
const MSG_MATCHED_ALL = "The value matched all subschema."
const MSG_DID_NOT_MATCH_ALL = "The value did not match all subschema."
const MSG_MATCHED_AT_LEAST_ONE = "The value matched at least one subschema."
const MSG_DID_NOT_MATCH_AT_LEAST_ONE = "The value did not match at least one subschema."
const MSG_MATCHED_NO_SCHEMA = "The instance matched no schema."
const MSG_MATCHED_EXACTLY_ONE = "The value matched exactly one subschema."
const MSG_MATCHED_MORE_THAN_ONE = "The instance matched more than one schema."
const MSG_MATCHED_NOT =
    "The value matched the subschema in a not composition, which means the evaluation was not a match."
const MSG_DID_NOT_MATCH_NOT =
    "The value did not match the subschema in a not composition, which means the evaluation was a match."
const MSG_MATCHED_IF_FOR_THEN = "The value matched the subschema in a binary or ternay if, which means the " *
                                "evaluation will go on to match the then subschema."
const MSG_MATCHED_IF_FOR_ELSE = "The value did not match the subschema in a ternary if, which means the " *
                                "evaluation will go on to match the else subschema."
const MSG_MATCHED_THEN = "The value matched the then subschema corresponding to a binary or ternary if."
const MSG_DID_NOT_MATCH_THEN = "The value did not match the then subschema corresponding to a binary or ternary if."
const MSG_MATCHED_ELSE = "The value matched the else subschema corresponding to a ternary if."
const MSG_DID_NOT_MATCH_ELSE = "The value did not match the else subschema corresponding to a ternary if."
const MSG_UNIQUE_ITEMS = "The array was expected to contain unique items."
const MSG_PROPERTY_NAME_FAILED = "The property name did not match the schema."

# " 'v'", or nothing for an empty value.
quoted(v::String) = v == "" ? "" : " '" * v * "'"

const TYPE_ORDER = ((TYPE_ARRAY, "array"), (TYPE_OBJECT, "object"), (TYPE_NULL, "null"), (TYPE_BOOLEAN, "boolean"),
    (TYPE_NUMBER, "number"), (TYPE_INTEGER, "integer"), (TYPE_STRING, "string"))

function type_message(mask::UInt8)
    names = String[name for (bit, name) in TYPE_ORDER if (mask & bit) != 0]
    isempty(names) && return ""
    length(names) == 1 && return "The value was expected to be of type '" * names[1] * "'"
    return "The value was expected to be of type '[\"" * join(names, "\", \"") * "\"]'"
end

function const_message(v::ValueRef)
    k = kind(v.d, v.n)
    k == KIND_STRING && return "Expected the value to be the string" * quoted(String(str(v.d, v.n)))
    k == KIND_NUMBER && return "The value was expected to be equal to" * quoted(String(number_text(v.d, v.n)))
    k == KIND_BOOL && return "Expected the value to be '" * (boolean(v.d, v.n) ? "true" : "false") * "'"
    k == KIND_NULL && return "Expected the value to be 'null'"
    return ""
end

# The length of a string value in code points (what minLength and maxLength count).
function code_point_count(d::Document, x::Int)
    str_ascii(d, x) && return UInt64(count(d, x))
    return UInt64(rune_count(str(d, x)))
end

function base64_value(c::UInt8)
    UInt8('A') <= c <= UInt8('Z') && return Int(c - UInt8('A'))
    UInt8('a') <= c <= UInt8('z') && return Int(c - UInt8('a')) + 26
    UInt8('0') <= c <= UInt8('9') && return Int(c - UInt8('0')) + 52
    c == UInt8('+') && return 62
    c == UInt8('/') && return 63
    return -1
end

# Decodes padded standard base64 into out (reused). It accepts no line breaks. It reports whether the text was valid.
function base64_decode!(out::Vector{UInt8}, b::Bytes)
    empty!(out)
    b.len % 4 != 0 && return false
    i = 0
    while i < b.len
        pad = 0
        while pad < 4 && at(b, i + 3 - pad) == UInt8('=')
            pad += 1
        end
        (pad > 2 || (pad > 0 && i + 4 != b.len)) && return false
        acc = 0
        for k in 0:3-pad
            v = base64_value(at(b, i + k))
            v < 0 && return false
            acc = (acc << 6) | v
        end
        acc <<= 6 * pad
        push!(out, (acc >> 16) % UInt8)
        pad < 2 && push!(out, (acc >> 8) % UInt8)
        pad < 1 && push!(out, acc % UInt8)
        i += 4
    end
    return true
end

# ----------------------------------------------------------------------------------------------------------------------
# Evaluated properties and items

# A set of evaluated properties (by index in the instance object) or items, held in the evaluator's arena. The zero
# offset is the arena's start, so "no set" is a negative offset.
struct Bitset
    off::Int32
    words::Int32
end

const NO_BITS = Bitset(-1, 0)

@inline tracked(b::Bitset) = b.off >= 0

# Takes a cleared set for n members from the arena. Sets are released in the reverse order.
function new_bits!(e::Evaluator, n::Int)
    arena = e.arena
    words = max((n + 63) >> 6, 1)
    off = length(arena)
    resize!(arena, off + words)
    for i in off+1:off+words
        arena[i] = 0
    end
    return Bitset(off % Int32, words % Int32)
end

function free_bits!(e::Evaluator, b::Bitset)
    resize!(e.arena, b.off)
    return nothing
end

@inline function set_bit!(e::Evaluator, b::Bitset, i::Int)
    e.arena[b.off+(i>>6)+1] |= UInt64(1) << (i & 63)
    return nothing
end

@inline get_bit(e::Evaluator, b::Bitset, i::Int) = (e.arena[b.off+(i>>6)+1] & (UInt64(1) << (i & 63))) != 0

function merge_bits!(e::Evaluator, into::Bitset, from::Bitset)
    arena = e.arena
    for i in 1:min(into.words, from.words)
        arena[into.off+i] |= arena[from.off+i]
    end
    return nothing
end

function clear_bits!(e::Evaluator, b::Bitset)
    arena = e.arena
    for i in 1:b.words
        arena[b.off+i] = 0
    end
    return nothing
end

function copy_bits!(e::Evaluator, into::Bitset, from::Bitset)
    arena = e.arena
    for i in 1:min(into.words, from.words)
        arena[into.off+i] = arena[from.off+i]
    end
    return nothing
end

# ----------------------------------------------------------------------------------------------------------------------
# The evaluator

# Evaluates the program's entry, failing fast.
validate!(e::Evaluator) = run(e, e.p.entry, e.d.root)

# Evaluates the program's entry, reporting to the collector.
function evaluate!(e::Evaluator, c::ResultsCollector)
    e.annotations = node_annotations(e.p)
    # A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
    root, _ = resolve(e, e.p.root)
    begin_child_context!(c, false, "", node(e.p, root).pointer, false, "")
    ok = eval_node(e, root, e.d.root, NO_BITS)
    commit_child_context!(c, false, ok, MSG_EVALUATED_SUBSCHEMA)
    return ok
end

# Reports whether a keyword result with the given outcome carries message text.
function wants(e::Evaluator, is_match::Bool)
    c = e.c
    return c !== nothing && with_text(c, is_match)
end

# Records a keyword's result when collecting. It reports whether evaluation stops here (failing fast).
function keyword!(e::Evaluator, is_match::Bool, message::String, keyword::String)
    c = e.c
    c === nothing && return !is_match
    evaluated_keyword!(c, is_match, message, keyword)
    return false
end

# Records a keyword whose message ends in a quoted count.
function count_keyword!(e::Evaluator, is_match::Bool, prefix::String, n::UInt64, keyword::String)
    c = e.c
    c === nothing && return !is_match
    message = with_text(c, is_match) ? prefix * " '" * string(n) * "'" : ""
    evaluated_keyword!(c, is_match, message, keyword)
    return false
end

# The single reference of a pure-$ref node, for collecting (a node with annotations is not elided).
function collect_pure_ref(e::Evaluator, id::NodeId)
    if id < length(e.annotations) && !isempty(e.annotations[id+1])
        return NO_NODE
    end
    return pure_ref_target(node(e.p, id))
end

# Follows pure-reference hops (at most 16, and not across resources when a dynamic scope is kept), returning the
# target and the evaluation-path suffix of the hops (/$ref, /$dynamicRef, /$recursiveRef).
function resolve(e::Evaluator, id::NodeId)
    current = id
    suffix = ""
    for _ in 1:16
        n = node(e.p, current)
        next = collect_pure_ref(e, current)
        next < 0 && break
        e.p.uses_dynamic_scope && node(e.p, next).resource_id != n.resource_id && break
        suffix *= n.ref >= 0 ? "/\$ref" : "/" * n.static_dynamic_keyword
        current = next
    end
    return current, suffix
end

function eval_node(e::Evaluator, id::NodeId, x::Int, bits::Bitset)::Bool
    n = node(e.p, id)
    if n.always_true || n.always_false
        c = e.c
        c === nothing || evaluated_boolean_schema!(c, n.always_true)
        return n.always_true
    end
    pushed = e.p.uses_dynamic_scope && push_scope!(e, n.resource_id)
    k = kind(e.d, x)
    ok = false
    if !tracked(bits) && ((n.unevaluated_properties >= 0 && k == KIND_OBJECT) ||
                          (n.unevaluated_items >= 0 && k == KIND_ARRAY))
        own = new_bits!(e, count(e.d, x))
        ok = eval_core(e, id, n, x, own)
        free_bits!(e, own)
    else
        ok = eval_core(e, id, n, x, bits)
    end
    pushed && pop_scope!(e)
    return ok
end

# Enters a resource in the dynamic scope, unless it is the innermost one already. It reports whether it did.
function push_scope!(e::Evaluator, resource::UInt32)
    scope = e.scope
    (!isempty(scope) && scope[end] == resource) && return false
    push!(scope, resource)
    return true
end

function pop_scope!(e::Evaluator)
    pop!(e.scope)
    return nothing
end

function eval_core(e::Evaluator, id::NodeId, n::SchemaNode, x::Int, bits::Bitset)::Bool
    d = e.d
    collector = e.c
    ok = true
    if n.has_type
        m = type_ok(n.type_mask, d, x)
        keyword!(e, m, wants(e, m) ? type_message(n.type_mask) : "", "type") && return false
        ok = ok && m
    end
    if is_set(n.const_value)
        m = values_equal(d, x, n.const_value.d, n.const_value.n)
        keyword!(e, m, wants(e, m) ? const_message(n.const_value) : "", "const") && return false
        ok = ok && m
    end
    if n.has_enum
        m = enum_contains(e, n.enum_values, x)
        keyword!(e, m, m ? MSG_MATCHED_AT_LEAST_ONE : MSG_DID_NOT_MATCH_AT_LEAST_ONE, "enum") && return false
        ok = ok && m
    end
    k = kind(d, x)
    m = true
    if k == KIND_NUMBER && has_number_keywords(n)
        m = eval_number(e, n, x)
    elseif k == KIND_STRING && has_string_keywords(n)
        m = eval_string(e, n, x)
    elseif k == KIND_OBJECT && has_object_keywords(n)
        m = eval_object(e, n, x, bits)
    elseif k == KIND_ARRAY && has_array_keywords(n)
        m = eval_array(e, n, x, bits)
    end
    (!m && collector === nothing) && return false
    ok = ok && m
    m = eval_in_place(e, n, x, bits)
    (!m && collector === nothing) && return false
    ok = ok && m
    m = true
    if k == KIND_OBJECT && n.unevaluated_properties >= 0
        m = eval_unevaluated_properties(e, n, x, bits)
    elseif k == KIND_ARRAY && n.unevaluated_items >= 0
        m = eval_unevaluated_items(e, n, x, bits)
    end
    (!m && collector === nothing) && return false
    ok = ok && m
    if collector !== nothing
        for a in e.annotations[id+1]
            (a.strings_only && k != KIND_STRING) && continue
            ignored_keyword!(collector, a.value, a.keyword)
        end
    end
    return ok
end

# ----------------------------------------------------------------------------------------------------------------------
# Numbers and strings

# Records a bound's result, with the bound's text in the message.
function bound_keyword!(e::Evaluator, b::ValueRef, m::Bool, text::String, keyword::String)
    message = wants(e, m) ? text * quoted(String(number_text(b.d, b.n))) : ""
    return keyword!(e, m, message, keyword)
end

function eval_number(e::Evaluator, n::SchemaNode, x::Int)::Bool
    d = e.d
    ok = true
    if n.assert_format && is_numeric_format(n.format_kind) && n.has_format
        custom = custom_format(e.p, n.format)
        m = custom != 0 ? call_custom_format(e.p, custom, String(number_text(d, x))) :
            check_number_format(n.format_kind, d, x)
        message = wants(e, m) ?
                  "The value was expected to be in a supported format, and within bounds for '" *
                  format_name(n.format_kind) * "'" : ""
        keyword!(e, m, message, "format") && return false
        ok = ok && m
    end
    flag, dat = flags(d, x), data(d, x)
    b = n.minimum
    if is_set(b)
        m = compare_numbers(flag, dat, flags(b.d, b.n), data(b.d, b.n)) >= 0
        bound_keyword!(e, b, m, "The value was expected to be greater than or equal to", "minimum") && return false
        ok = ok && m
    end
    b = n.maximum
    if is_set(b)
        m = compare_numbers(flag, dat, flags(b.d, b.n), data(b.d, b.n)) <= 0
        bound_keyword!(e, b, m, "The value was expected to be less than or equal to", "maximum") && return false
        ok = ok && m
    end
    b = n.exclusive_minimum
    if is_set(b)
        m = compare_numbers(flag, dat, flags(b.d, b.n), data(b.d, b.n)) > 0
        bound_keyword!(e, b, m, "The value was expected to be greater than", "exclusiveMinimum") && return false
        ok = ok && m
    end
    b = n.exclusive_maximum
    if is_set(b)
        m = compare_numbers(flag, dat, flags(b.d, b.n), data(b.d, b.n)) < 0
        bound_keyword!(e, b, m, "The value was expected to be less than", "exclusiveMaximum") && return false
        ok = ok && m
    end
    b = n.multiple_of
    if is_set(b)
        m = divides(n.divisor::Divisor, d, x)
        bound_keyword!(e, b, m, "The value was expected to be a multiple of", "multipleOf") && return false
        ok = ok && m
    end
    return ok
end

function eval_string(e::Evaluator, n::SchemaNode, x::Int)::Bool
    d = e.d
    ok = true
    if n.min_length.set || n.max_length.set
        len = code_point_count(d, x)
        if n.min_length.set
            m = len >= n.min_length.n
            count_keyword!(e, m, "Expected the length of the value to be greater than or equal to", n.min_length.n,
                "minLength") && return false
            ok = ok && m
        end
        if n.max_length.set
            m = len <= n.max_length.n
            count_keyword!(e, m, "Expected the length of the value to be less than or equal to", n.max_length.n,
                "maxLength") && return false
            ok = ok && m
        end
    end
    p = n.pattern
    if p !== nothing
        m = pattern_match(p, str(d, x), str_ascii(d, x))
        message = wants(e, m) ? "Expected the value to match the regular expression" * quoted(p.source) : ""
        keyword!(e, m, message, "pattern") && return false
        ok = ok && m
    end
    if n.assert_format && !is_numeric_format(n.format_kind) && n.has_format
        m, message = true, ""
        custom = custom_format(e.p, n.format)
        if custom != 0
            m = call_custom_format(e.p, custom, String(str(d, x)))
            if wants(e, m)
                message = "Expected a string in the '" * n.format * "' format."
            end
        elseif n.format_kind != FORMAT_UNKNOWN
            m = check_string_format(n.format_kind, str(d, x), n.dialect <= Draft6)
            message = format_message(n.format_kind)
        end
        keyword!(e, m, message, "format") && return false
        ok = ok && m
    end
    if n.assert_content
        m = content_ok(e, str(d, x), n.content)
        message, keyword = "Expected valid Base64-encoded JSON content.", "contentMediaType"
        if n.content == CONTENT_BASE64
            message, keyword = "Expected a valid Base64-encoded string.", "contentEncoding"
        elseif n.content == CONTENT_JSON
            message = "Expected valid JSON content."
        end
        keyword!(e, m, message, keyword) && return false
        ok = ok && m
    end
    return ok
end

# The draft 7 content assertion.
function content_ok(e::Evaluator, s::Bytes, content::UInt8)
    content == CONTENT_NONE && return true
    if content != CONTENT_JSON
        ok = base64_decode!(e.content, s)
        (!ok || content == CONTENT_BASE64) && return ok
        return is_valid_json!(e.content_parser, e.content)
    end
    # The parser reads a whole vector, so text inside a document is copied to the content buffer first.
    buffer = e.content
    resize!(buffer, s.len)
    copyto!(buffer, 1, s.b, s.off + 1, s.len)
    return is_valid_json!(e.content_parser, buffer)
end

# ----------------------------------------------------------------------------------------------------------------------
# Objects

# Applies a child at a new instance location (a property value or an array item), when collecting.
function eval_at(e::Evaluator, c::ResultsCollector, child::NodeId, path::String, value::Int, doc_segment::String)
    resolved, suffix = resolve(e, child)
    begin_child_context!(c, true, path * suffix, node(e.p, resolved).pointer, true, doc_segment)
    ok = eval_node(e, resolved, value, NO_BITS)
    commit_child_context!(c, ok, ok, MSG_EVALUATED_SUBSCHEMA)
    return ok
end

# Records whether a required property is present. It reports whether evaluation stops here.
function required_property!(e::Evaluator, present::Bool, name::String, keyword::String)
    c = e.c
    c === nothing && return !present
    message = with_text(c, present) ? "Required property " * (present ? "" : "not ") * "present '" * name * "'" : ""
    evaluated_keyword_for_property!(c, present, message, name, keyword)
    return false
end

# Applies a child to the value of property i of an object.
function eval_property(e::Evaluator, child::NodeId, keyword::String, entry::String, bits::Bitset, i::Int, v::Int,
    segment::String)
    tracked(bits) && set_bit!(e, bits, i)
    c = e.c
    c === nothing && return run(e, target(e.p, child), v)
    path = (entry != "" || keyword != "additionalProperties") ? keyword * "/" * entry : keyword
    return eval_at(e, c, child, path, v, segment)
end

function eval_object(e::Evaluator, n::SchemaNode, x::Int, bits::Bitset)::Bool
    d = e.d
    collector = e.c
    collect = collector !== nothing
    ok = true
    len = count(d, x)
    if n.min_properties.set
        m = UInt64(len) >= n.min_properties.n
        count_keyword!(e, m, "Expected the property count to be greater than or equal to", n.min_properties.n,
            "minProperties") && return false
        ok = ok && m
    end
    if n.max_properties.set
        m = UInt64(len) <= n.max_properties.n
        count_keyword!(e, m, "Expected the property count to be less than or equal to", n.max_properties.n,
            "maxProperties") && return false
        ok = ok && m
    end
    first_child = first(d, x)
    if n.has_properties || n.has_pattern_properties || n.additional_properties >= 0 || n.property_names_schema >= 0
        for i in 0:len-1
            k, v = first_child + 2i, first_child + 2i + 1
            name = str(d, k)
            matched = false
            segment = collect ? escape_pointer_token(String(name)) : ""
            p = property_node(n, name)
            if p >= 0
                matched = true
                if !eval_property(e, p, "properties", segment, bits, i, v, segment)
                    collect || return false
                    ok = false
                end
            end
            for pp in n.pattern_properties
                pattern_match(pp.pattern, name, str_ascii(d, k)) || continue
                matched = true
                entry = collect ? escape_pointer_token(pp.pattern.source) : ""
                if !eval_property(e, pp.node, "patternProperties", entry, bits, i, v, segment)
                    collect || return false
                    ok = false
                end
            end
            if n.additional_properties >= 0 && !matched
                if !eval_property(e, n.additional_properties, "additionalProperties", "", bits, i, v, segment)
                    collect || return false
                    ok = false
                end
            end
            pn = n.property_names_schema
            if pn >= 0
                # The name is a string value of the document: it is evaluated where it is.
                if collector !== nothing
                    # Not elided. The document path stays the object's.
                    begin_child_context!(collector, true, "propertyNames", node(e.p, pn).pointer, false, "")
                    m = eval_node(e, pn, k, NO_BITS)
                    commit_child_context!(collector, m, m, MSG_EVALUATED_SUBSCHEMA)
                    if !m
                        evaluated_keyword!(collector, false, MSG_PROPERTY_NAME_FAILED, "propertyNames")
                        ok = false
                    end
                elseif !run(e, target(e.p, pn), k)
                    return false
                end
            end
        end
    end
    for r in n.required_list
        present = property(d, x, r) >= 0
        required_property!(e, present, r, "required") && return false
        ok = ok && present
    end
    # Rows are reported under the keyword the schema used (dependencies, dependentRequired, dependentSchemas).
    for dep in n.dependencies
        property(d, x, dep.name) < 0 && continue
        for r in dep.required
            present = property(d, x, r) >= 0
            required_property!(e, present, r, dep.keyword) && return false
            ok = ok && present
        end
        if dep.schema >= 0
            path = collect ? dep.keyword * "/" * escape_pointer_token(dep.name) : ""
            m = eval_in_place_child(e, dep.schema, path, x, bits, true, true)
            if collector === nothing
                m || return false
                continue
            end
            message = with_text(collector, m) ?
                      "The value did match the schema applied because it contained the property '" * dep.name * "'" :
                      ""
            evaluated_keyword_for_property!(collector, m, message, dep.name, dep.keyword)
            ok = ok && m
        end
    end
    return ok
end

function eval_unevaluated_properties(e::Evaluator, n::SchemaNode, x::Int, bits::Bitset)::Bool
    d = e.d
    collector = e.c
    child = n.unevaluated_properties
    ok = true
    first_child = first(d, x)
    for i in 0:count(d, x)-1
        get_bit(e, bits, i) && continue
        set_bit!(e, bits, i)
        v = first_child + 2i + 1
        if collector === nothing
            run(e, target(e.p, child), v) || return false
        elseif !eval_at(e, collector, child, "unevaluatedProperties", v,
            escape_pointer_token(String(str(d, first_child + 2i))))
            ok = false
        end
    end
    collector === nothing || evaluated_keyword!(collector, ok, "", "unevaluatedProperties")
    return ok
end

# ----------------------------------------------------------------------------------------------------------------------
# Arrays

function eval_array(e::Evaluator, n::SchemaNode, x::Int, bits::Bitset)::Bool
    d = e.d
    collector = e.c
    ok = true
    len = count(d, x)
    if n.min_items.set
        m = UInt64(len) >= n.min_items.n
        count_keyword!(e, m, "Expected the item count to be greater than or equal to", n.min_items.n, "minItems") &&
            return false
        ok = ok && m
    end
    if n.max_items.set
        m = UInt64(len) <= n.max_items.n
        count_keyword!(e, m, "Expected the item count to be less than or equal to", n.max_items.n, "maxItems") &&
            return false
        ok = ok && m
    end
    (!n.has_prefix_items && n.items < 0 && n.contains < 0 && !n.unique_items) && return ok
    found = UInt64(0)
    first_item = first(d, x)
    for i in 0:len-1
        item = first_item + i
        child, path = NO_NODE, ""
        if i < length(n.prefix_items)
            child = n.prefix_items[i+1]
            if collector !== nothing
                path = n.prefix_keyword * "/" * string(i)
            end
        elseif n.items >= 0
            child, path = n.items, n.items_keyword
        end
        if child >= 0
            tracked(bits) && set_bit!(e, bits, i)
            if collector === nothing
                run(e, target(e.p, child), item) || return false
            elseif !eval_at(e, collector, child, path, item, string(i))
                ok = false
            end
        end
        if n.contains >= 0
            matched = false
            if collector !== nothing
                resolved, suffix = resolve(e, n.contains)
                begin_child_context!(collector, true, "contains" * suffix, node(e.p, resolved).pointer, true,
                    string(i))
                matched = eval_node(e, resolved, item, NO_BITS)
                if matched
                    commit_child_context!(collector, true, true, MSG_EVALUATED_SUBSCHEMA)
                else
                    pop_child_context!(collector)
                end
            else
                matched = run(e, target(e.p, n.contains), item)
            end
            if matched
                found += 1
                n.contains_marks_evaluated && tracked(bits) && set_bit!(e, bits, i)
            end
        end
    end
    if n.unique_items
        m = all_unique(d, x, e.unique)
        keyword!(e, m, MSG_UNIQUE_ITEMS, "uniqueItems") && return false
        ok = ok && m
    end
    if n.contains >= 0
        over = n.max_contains.set && found > n.max_contains.n
        m = found >= n.min_contains && !over
        stop = over ?
               count_keyword!(e, m, "Expected the contains count to be less than or equal to", n.max_contains.n,
            "contains") :
               count_keyword!(e, m, "Expected the contains count to be greater than or equal to", n.min_contains,
            "contains")
        stop && return false
        ok = ok && m
    end
    return ok
end

function eval_unevaluated_items(e::Evaluator, n::SchemaNode, x::Int, bits::Bitset)::Bool
    d = e.d
    collector = e.c
    child = n.unevaluated_items
    ok = true
    first_item = first(d, x)
    for i in 0:count(d, x)-1
        get_bit(e, bits, i) && continue
        set_bit!(e, bits, i)
        if collector === nothing
            run(e, target(e.p, child), first_item + i) || return false
        elseif !eval_at(e, collector, child, "unevaluatedItems", first_item + i, string(i))
            ok = false
        end
    end
    collector === nothing || evaluated_keyword!(collector, ok, "", "unevaluatedItems")
    return ok
end

# ----------------------------------------------------------------------------------------------------------------------
# In-place applicators

function can_mark(e::Evaluator, id::NodeId, x::Int)
    n = node(e.p, id)
    return kind(e.d, x) == KIND_OBJECT ? n.marks_properties : n.marks_items
end

# Evaluates an in-place child: a new context at the same instance location, on a fresh scratch set of evaluated
# properties or items merged into the parent's on success. A failing child is committed or popped.
function eval_in_place_child(e::Evaluator, child::NodeId, path::String, x::Int, bits::Bitset,
    commit_on_failure::Bool, elide::Bool)::Bool
    collector = e.c
    resolved, suffix = child, ""
    if elide
        if collector !== nothing
            resolved, suffix = resolve(e, child)
        else
            resolved = target(e.p, child)
        end
    end
    scratch = NO_BITS
    if tracked(bits) && can_mark(e, child, x)
        scratch = new_bits!(e, count(e.d, x))
    end
    guarded = node(e.p, resolved).in_place_cycle
    if guarded
        e.depth += 1
        if e.depth > e.p.max_depth
            e.depth_exceeded = true
            e.depth -= 1
            tracked(scratch) && free_bits!(e, scratch)
            return false
        end
    end
    ok = false
    if collector !== nothing
        begin_child_context!(collector, true, path * suffix, node(e.p, resolved).pointer, false, "")
        ok = eval_node(e, resolved, x, scratch)
        if ok || commit_on_failure
            commit_child_context!(collector, ok, ok, MSG_EVALUATED_SUBSCHEMA)
        else
            pop_child_context!(collector)
        end
    elseif tracked(scratch)
        ok = eval_node(e, resolved, x, scratch)
    else
        ok = run(e, resolved, x)
    end
    if guarded
        e.depth -= 1
    end
    if tracked(scratch)
        ok && merge_bits!(e, bits, scratch)
        free_bits!(e, scratch)
    end
    return ok
end

function resolve_dynamic(e::Evaluator, d::DynamicRefTarget)
    by_resource = d.by_resource
    for resource in e.scope
        for i in eachindex(by_resource)
            by_resource[i].resource == resource && return by_resource[i].node
        end
    end
    return d.fallback
end

# The oneOf/anyOf branches a discriminator leaves as candidates for an instance: all of them (true), or the listed
# ones.
function select_branches(e::Evaluator, disc::Union{Nothing,Discriminator}, x::Int)
    d = e.d
    (disc === nothing || kind(d, x) != KIND_OBJECT) && return true, EMPTY_U32
    value = property(d, x, disc.property)
    value < 0 && return !disc.all_require, EMPTY_U32
    for entry in disc.known
        matches(entry.value, d, value) && return false, entry.branches
    end
    return false, disc.unknown
end

function eval_in_place(e::Evaluator, n::SchemaNode, x::Int, bits::Bitset)::Bool
    collector = e.c
    collect = collector !== nothing
    ok = true
    if n.ref >= 0
        m = eval_in_place_child(e, n.ref, "\$ref", x, bits, true, true)
        keyword!(e, m, m ? MSG_MATCHED_ALL : MSG_DID_NOT_MATCH_ALL, "\$ref") && return false
        ok = ok && m
    end
    if n.static_dynamic_ref >= 0
        keyword = n.static_dynamic_keyword
        m = eval_in_place_child(e, n.static_dynamic_ref, keyword, x, bits, true, true)
        keyword!(e, m, m ? MSG_MATCHED_ALL : MSG_DID_NOT_MATCH_ALL, keyword) && return false
        ok = ok && m
    end
    dr = n.dynamic_ref
    if dr !== nothing
        keyword = dynamic_keyword(dr.is_recursive)
        # The resolved target is elided, with no hops in the path.
        resolved = resolve_dynamic(e, dr)
        if collect
            resolved, _ = resolve(e, resolved)
        else
            resolved = target(e.p, resolved)
        end
        m = eval_in_place_child(e, resolved, keyword, x, bits, true, false)
        keyword!(e, m, m ? MSG_MATCHED_ALL : MSG_DID_NOT_MATCH_ALL, keyword) && return false
        ok = ok && m
    end
    if n.has_all_of
        all_match = true
        list = n.all_of
        for i in eachindex(list)
            path = collect ? "allOf/" * string(i - 1) : ""
            if !eval_in_place_child(e, list[i], path, x, bits, true, true)
                collect || return false
                all_match = false
            end
        end
        keyword!(e, all_match, all_match ? MSG_MATCHED_ALL : MSG_DID_NOT_MATCH_ALL, "allOf") && return false
        ok = ok && all_match
    end
    if n.has_any_of
        any_match = false
        # Every branch runs when results are collected or evaluated properties or items are tracked.
        exhaustive = collect || tracked(bits)
        # Fail-fast evaluation only tries the branches a discriminator property can select.
        all_branches, subset = true, EMPTY_U32
        if !exhaustive
            all_branches, subset = select_branches(e, n.any_of_discriminator, x)
        end
        list = n.any_of
        for j in 1:(all_branches ? length(list) : length(subset))
            i = all_branches ? j - 1 : Int(subset[j])
            path = collect ? "anyOf/" * string(i) : ""
            if eval_in_place_child(e, list[i+1], path, x, bits, false, true)
                any_match = true
                exhaustive || break
            end
        end
        keyword!(e, any_match, any_match ? MSG_MATCHED_AT_LEAST_ONE : MSG_DID_NOT_MATCH_AT_LEAST_ONE, "anyOf") &&
            return false
        ok = ok && any_match
    end
    if n.has_one_of
        matched = 0
        track = tracked(bits)
        # Evaluated properties or items are merged only when exactly one branch matched, so they are collected
        # aside, and the matching branch's kept.
        aside, only = NO_BITS, NO_BITS
        if track
            only = new_bits!(e, count(e.d, x))
            aside = new_bits!(e, count(e.d, x))
        end
        # Branches a discriminator rules out cannot match, so fail-fast evaluation skips them.
        all_branches, subset = true, EMPTY_U32
        if !collect && !track
            all_branches, subset = select_branches(e, n.one_of_discriminator, x)
        end
        list = n.one_of
        for j in 1:(all_branches ? length(list) : length(subset))
            i = all_branches ? j - 1 : Int(subset[j])
            path = collect ? "oneOf/" * string(i) : ""
            track && clear_bits!(e, aside)
            if eval_in_place_child(e, list[i+1], path, x, aside, false, true)
                matched += 1
                track && copy_bits!(e, only, aside)
                (!collect && matched > 1) && break
            end
        end
        if track
            matched == 1 && merge_bits!(e, bits, only)
            free_bits!(e, only)
        end
        message = matched == 0 ? MSG_MATCHED_NO_SCHEMA :
                  (matched == 1 ? MSG_MATCHED_EXACTLY_ONE : MSG_MATCHED_MORE_THAN_ONE)
        keyword!(e, matched == 1, message, "oneOf") && return false
        ok = ok && matched == 1
    end
    if n.not >= 0
        # Not elided, never contributes results or evaluated properties or items.
        inner = false
        if collector !== nothing
            begin_child_context!(collector, true, "not", node(e.p, n.not).pointer, false, "")
            inner = eval_node(e, n.not, x, NO_BITS)
            pop_child_context!(collector)
        else
            inner = run(e, target(e.p, n.not), x)
        end
        keyword!(e, !inner, inner ? MSG_MATCHED_NOT : MSG_DID_NOT_MATCH_NOT, "not") && return false
        ok = ok && !inner
    end
    if n.if_ >= 0
        cond = eval_in_place_child(e, n.if_, "if", x, bits, false, true)
        if collector !== nothing
            evaluated_keyword!(collector, true, cond ? MSG_MATCHED_IF_FOR_THEN : MSG_MATCHED_IF_FOR_ELSE, "if")
        end
        if cond
            if n.then_ >= 0
                m = eval_in_place_child(e, n.then_, "then", x, bits, true, true)
                keyword!(e, m, m ? MSG_MATCHED_THEN : MSG_DID_NOT_MATCH_THEN, "then") && return false
                ok = ok && m
            end
        elseif n.else_ >= 0
            m = eval_in_place_child(e, n.else_, "else", x, bits, true, true)
            keyword!(e, m, m ? MSG_MATCHED_ELSE : MSG_DID_NOT_MATCH_ELSE, "else") && return false
            ok = ok && m
        end
    end
    return ok
end
