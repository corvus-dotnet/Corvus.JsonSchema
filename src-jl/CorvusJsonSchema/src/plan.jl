# Fail-fast evaluation plans: each node compiled to only the keywords it has, as a flat list of operations, with the
# object keywords fused into one pass over the instance's properties (declared names resolved through a lookup
# table, required checked as a bit mask of the declared properties seen) and children that are nothing but a type
# check tested inline. Ported from plan.go.
#
# A node that tracks evaluated properties or items (unevaluatedProperties, unevaluatedItems) runs through the general
# evaluator, whose children come back to their plans.

function DiscriminatorIndex(d::Discriminator)
    list = String[]
    string_entries = UInt32[]
    others = UInt32[]
    for (i, entry) in enumerate(d.known)
        if entry.value.kind == KIND_STRING
            push!(list, String(copy(entry.value.text)))
            push!(string_entries, UInt32(i - 1))
        else
            push!(others, UInt32(i - 1))
        end
    end
    return DiscriminatorIndex(Names(list), string_entries, others)
end

# The branches a discriminator value selects.
function select_branches(index::DiscriminatorIndex, disc::Discriminator, d::Document, v::Int)
    if kind(d, v) == KIND_STRING
        i = find(index.strings, str(d, v))
        return i >= 0 ? disc.known[index.string_entries[i+1]+1].branches : disc.unknown
    end
    for i in index.others
        entry = disc.known[i+1]
        matches(entry.value, d, v) && return entry.branches
    end
    return disc.unknown
end

function Branches(children::Vector{Child}, disc::Union{Nothing,Discriminator})
    return Branches(children, [UInt32[] for _ in 1:6], disc, disc === nothing ? nothing : DiscriminatorIndex(disc))
end

# The one-based index of a kind in Branches.by_kind.
@inline kind_index(k::UInt8) = trailing_zeros(k) + 1

const KIND_TYPES = (TYPE_NULL, TYPE_BOOLEAN, TYPE_OBJECT, TYPE_ARRAY, TYPE_NUMBER | TYPE_INTEGER, TYPE_STRING)

# Computes the dispatch table once the children's types are known.
function dispatch!(b::Branches)
    for (k, types) in enumerate(KIND_TYPES)
        list = b.by_kind[k]
        empty!(list)
        for (i, child) in enumerate(b.children)
            (child.types & types) != 0 && push!(list, UInt32(i - 1))
        end
    end
    return nothing
end

# ----------------------------------------------------------------------------------------------------------------------
# Compilation

# Applies f to every child of an object plan in place.
function map_children!(f, o::ObjectPlan)
    for j in eachindex(o.children)
        o.children[j] = f(o.children[j])
    end
    for j in eachindex(o.patterns)
        o.patterns[j] = PatternChild(o.patterns[j].pattern, f(o.patterns[j].child))
    end
    if o.has_additional
        o.additional = f(o.additional)
    end
    return nothing
end

function map_children!(f, list::Vector{Child})
    for j in eachindex(list)
        list[j] = f(list[j])
    end
    return nothing
end

function compile_plans(p::Program)
    plans = Plan[plan_node(p, NodeId(id - 1), n) for (id, n) in enumerate(p.nodes)]
    # Hoist the children's type checks now that every plan is known.
    summary = [(pl.types, shape_of(pl, p.uses_dynamic_scope)) for pl in plans]
    function resolved(id::NodeId)
        types, shape = summary[id+1]
        # Not SHAPE_OBJECT: a node may still take a fused plan below.
        return with_shape(Child(id), types, shape == SHAPE_OBJECT ? SHAPE_GENERAL : shape)
    end
    # Fused object plans, for nodes whose object semantics span in-place applicators. A fused plan applies its
    # contributors' keywords without entering them as nodes, so the dynamic scope below it would differ from the
    # general path's where a contributor is in another resource: nodes that can reach a live dynamic reference fuse
    # only contributors in their own resource (which the general path would not push again).
    reaches_dynamic = reaches_dynamic_reference(p)
    fused_nodes = fill(false, length(plans))
    for id in eachindex(plans)
        f = try_fuse(p, NodeId(id - 1), reaches_dynamic[id], resolved)
        f === nothing && continue
        b = plans[id].body
        if b === nothing
            b = Body(NodeId(id - 1))
            plans[id].body = b
        end
        b.fused = f
        fused_nodes[id] = true
    end
    function fix(c::Child)
        c.id < 0 && return c
        types, shape = summary[c.id+1]
        return with_shape(c, types, shape == SHAPE_OBJECT && fused_nodes[c.id+1] ? SHAPE_GENERAL : shape)
    end
    for pl in plans
        b = pl.body
        b === nothing && continue
        o = b.object
        o === nothing || map_children!(fix, o)
        a = b.array
        if a !== nothing
            map_children!(fix, a.prefix)
            item_types, type_only = ANY_TYPE, true
            if a.has_items
                a.items = fix(a.items)
                item_types, type_only = a.items.types, a.items.shape == SHAPE_TRIVIAL
            end
            a.is_simple = type_only && isempty(a.prefix) && a.contains < 0 && !a.unique
            a.simple = item_types
        end
        if b.has_unevaluated_items
            b.unevaluated_child = fix(b.unevaluated_child)
        end
        for o in b.apply
            if o.kind == OP_ALL_OF
                map_children!(fix, o.children)
            elseif o.kind == OP_ANY_OF || o.kind == OP_ONE_OF
                map_children!(fix, o.branches.children)
                dispatch!(o.branches)
            end
        end
    end
    # Arrays whose items are simple arrays check them inline, without entering each one.
    simple = fill(SimpleArray(false, 0, 0, 0x00), length(plans))
    for (i, pl) in enumerate(plans)
        b = pl.body
        b === nothing && continue
        a = b.array
        (a === nothing || !a.is_simple || shape_of(pl, p.uses_dynamic_scope) != SHAPE_ARRAY) && continue
        simple[i] = SimpleArray(true, a.min, a.max, a.simple)
    end
    for pl in plans
        b = pl.body
        b === nothing && continue
        a = b.array
        a === nothing && continue
        if a.has_items && a.items.shape == SHAPE_ARRAY && simple[a.items.id+1].set
            a.has_nested, a.nested = true, simple[a.items.id+1]
        end
    end
    for (i, pl) in enumerate(plans)
        pl.shape = shape_of(pl, p.uses_dynamic_scope)
        if strict_plan(pl, pl.shape) !== NO_OBJECT_PLAN
            pl.shape = SHAPE_STRICT
        end
        pl.self = with_shape(Child(NodeId(i - 1)), pl.types, pl.shape)
    end
    # Every child now takes the final shape of its node, which a fused plan may have changed since the child was
    # made: a child that kept the shape its node had before it was fused would enter the node's applicators one by
    # one where the fused plan decides them in one pass.
    final(c::Child) = c.id >= 0 ? with_shape(c, plans[c.id+1].types, plans[c.id+1].shape) : c
    final(c::OptChild) = c.set ? OptChild(true, final(c.child)) : c
    for pl in plans
        b = pl.body
        b === nothing && continue
        o = b.object
        o === nothing || map_children!(final, o)
        a = b.array
        if a !== nothing
            map_children!(final, a.prefix)
            if a.has_items
                a.items = final(a.items)
            end
        end
        if b.has_unevaluated_items
            b.unevaluated_child = final(b.unevaluated_child)
        end
        for o in b.apply
            if o.kind == OP_ALL_OF
                map_children!(final, o.children)
            elseif o.kind == OP_ANY_OF || o.kind == OP_ONE_OF
                map_children!(final, o.branches.children)
            end
        end
        f = b.fused
        f === nothing && continue
        flat = f.flat
        flat === nothing || map_children!(final, flat)
        for entry in f.entries
            for k in eachindex(entry.apps)
                app = entry.apps[k]
                if app.has_child
                    entry.apps[k] = FusedApp(app.contributor, true, final(app.child), app.others, app.then_, app.els)
                end
            end
        end
        for j in eachindex(f.contributors)
            c = f.contributors[j]
            for k in eachindex(c.patterns)
                c.patterns[k] = FusedPattern(c.patterns[k].pattern, final(c.patterns[k].child))
            end
            if c.has_additional
                f.contributors[j] = FusedContributor(c.condition, c.alt, c.patterns, true, final(c.additional),
                    c.required, c.min, c.max, c.then_, c.els)
            end
        end
        if f.has_unevaluated
            f.unevaluated = final(f.unevaluated)
        end
    end
    return plans
end

# The index of a custom format in the program, or 0.
function custom_format(p::Program, name::String)
    for (i, n) in enumerate(p.format_names)
        n == name && return Int32(i)
    end
    return Int32(0)
end

function plan_node(p::Program, id::NodeId, n::SchemaNode)
    guard = n.in_place_cycle
    n.always_true && return Plan(ANY_TYPE, guard, nothing, SHAPE_GENERAL, NO_CHILD)
    n.always_false && return Plan(0x00, guard, nothing, SHAPE_GENERAL, NO_CHILD)
    child_of(c::NodeId) = Child(target(p, c))
    b = Body(id)

    if is_set(n.const_value)
        push!(b.values, Op(OP_CONST; value=n.const_value))
    end
    if n.has_enum
        if all(v -> kind(v.d, v.n) == KIND_STRING, n.enum_values)
            list = String[String(str(v.d, v.n)) for v in n.enum_values]
            push!(b.values, Op(OP_ENUM_STRINGS; names=Names(list)))
        else
            push!(b.values, Op(OP_ENUM; values=n.enum_values))
        end
    end

    # The format check of the node for numbers or for strings, or nothing.
    function format_check_for(numeric::Bool)
        (!n.assert_format || !n.has_format || is_numeric_format(n.format_kind) != numeric) && return nothing
        custom = custom_format(p, n.format)
        custom != 0 && return FormatCheck(custom, FORMAT_UNKNOWN, false)
        n.format_kind == FORMAT_UNKNOWN && return nothing
        return FormatCheck(Int32(0), n.format_kind, n.dialect <= Draft6)
    end

    # Numbers.
    f = format_check_for(true)
    f === nothing || push!(b.number, NumberOp(NUMBER_FORMAT, 0x00, 0, NO_DIVISOR, f))
    function bound(k::UInt8, v::ValueRef)
        is_set(v) && push!(b.number, NumberOp(k, flags(v.d, v.n), data(v.d, v.n), NO_DIVISOR, FormatCheck()))
        return nothing
    end
    bound(NUMBER_MINIMUM, n.minimum)
    bound(NUMBER_MAXIMUM, n.maximum)
    bound(NUMBER_EXCLUSIVE_MINIMUM, n.exclusive_minimum)
    bound(NUMBER_EXCLUSIVE_MAXIMUM, n.exclusive_maximum)
    if is_set(n.multiple_of)
        push!(b.number, NumberOp(NUMBER_MULTIPLE_OF, 0x00, 0, n.divisor::Divisor, FormatCheck()))
    end

    # Strings.
    if n.min_length.set || n.max_length.set
        push!(b.str, StringOp(STRING_LENGTH, n.min_length.set ? n.min_length.n : UInt64(0),
            n.max_length.set ? n.max_length.n : typemax(UInt64), NO_PATTERN, FormatCheck(), CONTENT_NONE))
    end
    pattern = n.pattern
    if pattern !== nothing
        push!(b.str, StringOp(STRING_PATTERN, 0, 0, pattern, FormatCheck(), CONTENT_NONE))
    end
    f = format_check_for(false)
    f === nothing || push!(b.str, StringOp(STRING_FORMAT, 0, 0, NO_PATTERN, f, CONTENT_NONE))
    if n.assert_content
        push!(b.str, StringOp(STRING_CONTENT, 0, 0, NO_PATTERN, FormatCheck(), n.content))
    end

    # Objects (unless the type excludes objects: then the keywords apply to nothing and must not cost the node its
    # shape).
    own_types = n.has_type ? n.type_mask : ANY_TYPE
    if has_object_keywords(n) && (own_types & TYPE_OBJECT) != 0
        b.object = plan_object(p, n, child_of)
    end

    # Arrays.
    if has_array_keywords(n) && (own_types & TYPE_ARRAY) != 0
        a = ArrayPlan(n.min_items.set ? n.min_items.n : UInt64(0), n.max_items.set ? n.max_items.n : typemax(UInt64),
            Child[child_of(c) for c in n.prefix_items], false, NO_CHILD, NO_NODE, 0, OptCount(), n.unique_items,
            false, 0x00, false, SimpleArray(false, 0, 0, 0x00))
        if n.items >= 0
            a.has_items, a.items = true, child_of(n.items)
        end
        if n.contains >= 0
            a.contains, a.min_contains, a.max_contains = target(p, n.contains), n.min_contains, n.max_contains
        end
        b.array = a
    end

    # In-place applicators, in the general evaluator's order.
    if n.ref >= 0
        push!(b.apply, Op(OP_REF; node=target(p, n.ref)))
    end
    if n.static_dynamic_ref >= 0
        push!(b.apply, Op(OP_REF; node=target(p, n.static_dynamic_ref)))
    end
    dynamic = n.dynamic_ref
    if dynamic !== nothing
        if p.uses_dynamic_scope
            push!(b.apply, Op(OP_DYNAMIC_REF; dynamic=dynamic))
        else
            # Without a dynamic scope the reference always takes its fallback.
            push!(b.apply, Op(OP_REF; node=target(p, dynamic.fallback)))
        end
    end
    children_of(list::Vector{NodeId}) = Child[child_of(c) for c in list]
    if n.has_all_of
        push!(b.apply, Op(OP_ALL_OF; children=children_of(n.all_of)))
    end
    # An anyOf of type-only branches, or a oneOf of type-only branches with no type in common, is one type test: it
    # narrows the node's own types instead of adding a keyword.
    union = ANY_TYPE
    if n.has_any_of
        mask = type_union(p, n.any_of, false)
        if mask !== nothing
            union = meet_types(union, mask)
        else
            push!(b.apply, Op(OP_ANY_OF; branches=Branches(children_of(n.any_of), n.any_of_discriminator)))
        end
    end
    if n.has_one_of
        mask = type_union(p, n.one_of, true)
        if mask !== nothing
            union = meet_types(union, mask)
        else
            push!(b.apply, Op(OP_ONE_OF; branches=Branches(children_of(n.one_of), n.one_of_discriminator)))
        end
    end
    if n.not >= 0
        push!(b.apply, Op(OP_NOT; node=target(p, n.not)))
    end
    if n.if_ >= 0 && (n.then_ >= 0 || n.else_ >= 0)
        push!(b.apply, Op(OP_IF; node=target(p, n.if_), then_=n.then_ >= 0 ? target(p, n.then_) : NO_NODE,
            else_=n.else_ >= 0 ? target(p, n.else_) : NO_NODE))
    end

    # unevaluatedProperties is left to the general evaluator (or a fused object plan). unevaluatedItems takes the
    # items after a static prefix when every contribution to the evaluated items is unconditional.
    if n.unevaluated_properties >= 0
        b.general |= TYPE_OBJECT
    end
    if n.unevaluated_items >= 0
        coverage = static_item_coverage(p, id)
        if coverage === nothing
            b.general |= TYPE_ARRAY
        elseif !coverage[1]
            b.has_unevaluated_items, b.unevaluated_from = true, coverage[2]
            b.unevaluated_child = child_of(n.unevaluated_items)
        end
    end

    types = meet_types(own_types, union)
    if isempty(b.values) && isempty(b.number) && isempty(b.str) && b.object === nothing && b.array === nothing &&
       isempty(b.apply) && b.general == 0 && !b.has_unevaluated_items
        return Plan(types, guard, nothing, SHAPE_GENERAL, NO_CHILD)
    end
    return Plan(types, guard, b, SHAPE_GENERAL, NO_CHILD)
end

function plan_object(p::Program, n::SchemaNode, child_of)
    # Names only required or dependencies mention join the declared ones, up to 64 names in all (one mask word).
    known = String[property.name for property in n.properties]
    extras = String[]
    extra(name::String) = (name in known || name in extras) ? nothing : (push!(extras, name); nothing)
    foreach(extra, n.required)
    for dep in n.dependencies
        extra(dep.name)
        foreach(extra, dep.required)
    end
    if length(known) + length(extras) <= 64
        append!(known, extras)
    end
    undeclared = NO_CHILD
    has_additional = false
    if n.additional_properties >= 0
        undeclared = child_of(n.additional_properties)
        has_additional = true
    end
    o = ObjectPlan(n.min_properties.set ? n.min_properties.n : UInt64(0),
        n.max_properties.set ? n.max_properties.n : typemax(UInt64), VISIT_NONE, Names(known),
        length(n.properties),
        Child[i <= length(n.properties) ? child_of(n.properties[i].node) : undeclared for i in eachindex(known)],
        UInt64(0), String[], PatternChild[], Vector{UInt16}[], has_additional, undeclared, NO_NODE,
        PlanDependency[], false, false, false, -1)
    has_names = !isempty(known)
    if n.property_names_schema < 0 && !has_names && length(n.pattern_properties) == 1
        o.visit = VISIT_PATTERN
    elseif n.has_pattern_properties || n.property_names_schema >= 0
        o.visit = VISIT_GENERAL
    elseif has_names
        o.visit = VISIT_NAMES
    elseif n.additional_properties >= 0
        o.visit = VISIT_VALUES
    end
    visited = (o.visit == VISIT_NAMES || o.visit == VISIT_GENERAL) && length(known) <= 64
    # The seen bit of a name, or nothing.
    function bit(name::String)
        visited || return nothing
        i = find(o.names, name)
        return i >= 0 ? UInt64(1) << i : nothing
    end
    for r in n.required
        b = bit(r)
        if b !== nothing
            o.required_mask |= b
        else
            push!(o.required, r)
        end
    end
    for dep in n.dependencies
        name_bit = bit(dep.name)
        ok = name_bit !== nothing
        required_bits = UInt64(0)
        for r in dep.required
            b = bit(r)
            if b === nothing
                ok = false
            else
                required_bits |= b
            end
        end
        push!(o.dependencies, PlanDependency(dep.name, dep.required, dep.schema >= 0 ? target(p, dep.schema) : NO_NODE,
            ok, name_bit === nothing ? UInt64(0) : name_bit, required_bits))
    end
    for pp in n.pattern_properties
        push!(o.patterns, PatternChild(pp.pattern, child_of(pp.node)))
    end
    for name in known
        matched = UInt16[]
        for (j, pp) in enumerate(n.pattern_properties)
            match_string(pp.pattern, name) && push!(matched, UInt16(j - 1))
        end
        push!(o.name_patterns, matched)
    end
    if n.property_names_schema >= 0
        o.property_names = target(p, n.property_names_schema)
    end
    o.rest_free = isempty(o.required) && !n.has_dependencies
    o.strict = o.visit == VISIT_NAMES && o.rest_free
    o.lookup = o.visit == VISIT_NAMES && n.additional_properties < 0 && length(known) <= LOOKUP_NAMES
    o.lookup_max = lookup_limit(o.lookup, length(known))
    return o
end

# Says how callers can enter a plan (see the shapes). The shortcuts skip the scope push, so a program that keeps a
# dynamic scope takes them only for leaves. A node on an in-place cycle is entered through its guard.
function shape_of(pl::Plan, dynamic_scope::Bool)
    pl.guard && return SHAPE_GENERAL
    b = pl.body
    b === nothing && return SHAPE_TRIVIAL
    (b.general != 0 || b.has_unevaluated_items) && return SHAPE_GENERAL
    if b.fused !== nothing
        # Without the scope push of the general entry, so not in a program that keeps a dynamic scope.
        return dynamic_scope ? SHAPE_GENERAL : SHAPE_FUSED
    end
    values = !isempty(b.values) || !isempty(b.number) || !isempty(b.str)
    object, array, apply = b.object !== nothing, b.array !== nothing, !isempty(b.apply)
    if !object && !array && !apply
        if values && isempty(b.number) && isempty(b.str) && length(b.values) == 1 &&
           b.values[1].kind == OP_ENUM_STRINGS
            return SHAPE_STRING_ENUM
        end
        values && isempty(b.number) && isempty(b.values) && return SHAPE_STRINGS
        return SHAPE_LEAF
    elseif values || dynamic_scope
        return SHAPE_GENERAL
    elseif object && !array && !apply
        return SHAPE_OBJECT
    elseif !object && array && !apply
        return SHAPE_ARRAY
    elseif !object && !array && apply
        return SHAPE_APPLY
    end
    return SHAPE_GENERAL
end

# The plan the strict loop runs for a node of a shape: an object plan that is strict, or the flat plan of a fused
# object. NO_OBJECT_PLAN for any other node.
function strict_plan(pl::Plan, shape::UInt8)
    b = pl.body
    b === nothing && return NO_OBJECT_PLAN
    if shape == SHAPE_OBJECT
        o = b.object
        return o !== nothing && o.strict ? o : NO_OBJECT_PLAN
    elseif shape == SHAPE_FUSED
        f = b.fused
        flat = f === nothing ? nothing : f.flat
        return flat === nothing ? NO_OBJECT_PLAN : flat
    end
    return NO_OBJECT_PLAN
end

# strict_plan for a plan whose shape is final.
function strict_plan(pl::Plan)
    pl.shape == SHAPE_STRICT || return NO_OBJECT_PLAN
    b = pl.body::Body
    f = b.fused
    return f === nothing ? b.object::ObjectPlan : f.flat::ObjectPlan
end

# Marks every node from which a live dynamic reference is reachable through any child.
function reaches_dynamic_reference(p::Program)
    reaches = fill(false, length(p.nodes))
    p.uses_dynamic_scope || return reaches
    lists = [children(n) for n in p.nodes]
    for (i, n) in enumerate(p.nodes)
        reaches[i] = n.dynamic_ref !== nothing
    end
    changed = true
    while changed
        changed = false
        for (i, list) in enumerate(lists)
            reaches[i] && continue
            if any(c -> reaches[c+1], list)
                reaches[i] = true
                changed = true
            end
        end
    end
    return reaches
end

# A type mask with integer made explicit wherever number is (every integer is a number).
expand_types(mask::UInt8) = (mask & TYPE_NUMBER) != 0 ? mask | TYPE_INTEGER : mask

# The types both masks admit.
function meet_types(a::UInt8, b::UInt8)
    m = expand_types(a) & expand_types(b)
    # number in both keeps number. integer alone stays integer.
    if (a & b & TYPE_NUMBER) == 0 && (m & TYPE_NUMBER) != 0
        return m & ~TYPE_NUMBER
    end
    return m
end

# The mask of a schema that tests only the type (true admits everything, false nothing), or nothing.
function type_only_mask(n::SchemaNode)
    n.always_true && return ANY_TYPE
    n.always_false && return 0x00
    only_type = n.has_type && !n.in_place_cycle && !is_set(n.const_value) && !n.has_enum &&
                !has_number_keywords(n) && !has_string_keywords(n) && !has_object_keywords(n) &&
                !has_array_keywords(n) && !has_in_place_applicators(n) && n.dynamic_ref === nothing
    return only_type ? n.type_mask : nothing
end

# The union of anyOf/oneOf branches that all test only the type, or nothing. For oneOf, only when no two branches
# admit a common value (so that "exactly one" is "any").
function type_union(p::Program, list::Vector{NodeId}, exactly_one::Bool)
    masks = UInt8[]
    for c in list
        mask = type_only_mask(node(p, target(p, c)))
        mask === nothing && return nothing
        push!(masks, mask)
    end
    union = 0x00
    for (i, a) in enumerate(masks)
        if exactly_one
            for j in i+1:length(masks)
                (expand_types(a) & expand_types(masks[j])) != 0 && return nothing
            end
        end
        union |= a
    end
    return union
end

# The items a node's evaluation always marks evaluated, when that is static: the longest prefixItems of the node and
# the contributors it always applies ($ref and allOf chains) as from, or all of them when one has items (or, below
# the node, unevaluatedItems). The result is (all, from), or nothing when an in-place child that applies
# conditionally (anyOf, oneOf, if/then/else, a dependent schema, a dynamic reference) can mark items, or contains
# marks them.
function static_item_coverage(p::Program, id::NodeId)
    stack = Tuple{NodeId,Bool}[(id, true)]
    visited = NodeId[id]
    all_items, from = false, 0
    while !isempty(stack)
        at_id, root = pop!(stack)
        n = node(p, at_id)
        (n.always_true || n.always_false) && continue
        if n.in_place_cycle || n.dynamic_ref !== nothing || (n.contains >= 0 && n.contains_marks_evaluated)
            return nothing
        end
        from = max(from, length(n.prefix_items))
        all_items = all_items || n.items >= 0 || (!root && n.unevaluated_items >= 0)
        conditional = vcat(n.any_of, n.one_of)
        append_node!(conditional, n.if_, n.then_, n.else_)
        for dep in n.dependencies
            append_node!(conditional, dep.schema)
        end
        any(c -> node(p, c).marks_items, conditional) && return nothing
        for c in vcat(append_node!(NodeId[], n.ref, n.static_dynamic_ref), n.all_of)
            if !(c in visited)
                push!(visited, c)
                push!(stack, (c, false))
            end
        end
    end
    return all_items, from
end

# ----------------------------------------------------------------------------------------------------------------------
# Evaluation

# Reports whether a value is of one of the types in a mask. A kind is its type bit. The mask test is small enough to
# inline at every call site, and the integer test, which few values reach, is a call.
@inline type_ok(mask::UInt8, d::Document, x::Int) = (mask & kind(d, x)) != 0 || integer_ok(mask, d, x)

# Reports a number that the mask does not accept as a number but accepts as an integer.
@noinline function integer_ok(mask::UInt8, d::Document, x::Int)
    return kind(d, x) == KIND_NUMBER && (mask & TYPE_INTEGER) != 0 && is_integer_number(flags(d, x), data(d, x))
end

# @run_child past its test: the type test in full, then the child's keywords by its shape. An object for a strict
# object plan has its loop called from here, with no function in between and without the node's keywords being read,
# since that is what most values with keywords are.
function enter_child(e::Evaluator, c::Child, x::Int)::Bool
    d = e.d
    # The value's header is read once here, and what the loops need of it is handed to them.
    h = header(d, x)
    k = h % UInt8
    if (c.types & k) == 0 && !integer_ok(c.types, d, x)
        return false
    end
    shape = c.shape
    shape == SHAPE_TRIVIAL && return true
    if shape == SHAPE_STRICT && k == KIND_OBJECT
        # The strict loop (run_strict_object).
        pl = e.strict[c.id+1]
        n = hcount(h)
        (n % UInt64 < pl.min || n % UInt64 > pl.max) && return false
        return n <= pl.lookup_max ? visit_lookup(e, pl, first(d, x), n) : visit_names(e, pl, first(d, x), n)
    end
    b = e.bodies[c.id+1]
    b === nothing && return true
    if shape == SHAPE_LEAF
        return run_leaf(e, b, x)
    elseif shape == SHAPE_STRING_ENUM
        return k == KIND_STRING && find(b.values[1].names, str(d, x)) >= 0
    elseif shape == SHAPE_STRINGS
        return k != KIND_STRING || run_string(e, b.str, x)
    elseif shape == SHAPE_OBJECT
        return k != KIND_OBJECT || run_object(e, b.object::ObjectPlan, x)
    elseif shape == SHAPE_FUSED
        return k == KIND_OBJECT ? run_fused(e, b.fused::FusedObject, x) : run_keywords(e, b, x)
    elseif shape == SHAPE_ARRAY
        return k != KIND_ARRAY || run_array(e, b.array::ArrayPlan, x)
    elseif shape == SHAPE_APPLY
        return run_apply(e, b.apply, x)
    elseif shape == SHAPE_STRICT
        # Not an object: what the node has for other values (a fused plan may have such keywords).
        return run_keywords(e, b, x)
    end
    return run_body(e, b, x)
end

# Evaluates a body by its shape (its types already tested, and not on an in-place cycle unless general).
function enter(e::Evaluator, shape::UInt8, b::Body, x::Int)::Bool
    d = e.d
    if shape == SHAPE_LEAF
        return run_leaf(e, b, x)
    elseif shape == SHAPE_STRING_ENUM
        return kind(d, x) == KIND_STRING && find(b.values[1].names, str(d, x)) >= 0
    elseif shape == SHAPE_STRINGS
        return kind(d, x) != KIND_STRING || run_string(e, b.str, x)
    elseif shape == SHAPE_STRICT
        return kind(d, x) == KIND_OBJECT ? run_strict_object(e, e.strict[b.node+1], x) : run_keywords(e, b, x)
    elseif shape == SHAPE_OBJECT
        return kind(d, x) != KIND_OBJECT || run_object(e, b.object::ObjectPlan, x)
    elseif shape == SHAPE_FUSED
        kind(d, x) != KIND_OBJECT && return run_keywords(e, b, x)
        return run_fused(e, b.fused::FusedObject, x)
    elseif shape == SHAPE_ARRAY
        return kind(d, x) != KIND_ARRAY || run_array(e, b.array::ArrayPlan, x)
    elseif shape == SHAPE_APPLY
        return run_apply(e, b.apply, x)
    end
    return run_body(e, b, x)
end

# Evaluates an in-place child under the depth guard.
function run_in_place(e::Evaluator, id::NodeId, x::Int)::Bool
    e.guards[id+1] || return @run(e, id, x)
    e.depth += 1
    if e.depth > e.p.max_depth
        e.depth_exceeded = true
        e.depth -= 1
        return false
    end
    ok = @run(e, id, x)
    e.depth -= 1
    return ok
end

# Evaluates an in-place child with its type check inline, then its keywords under the depth guard (without testing
# the type again).
function run_branch(e::Evaluator, c::Child, x::Int)::Bool
    if c.types != ANY_TYPE && !type_ok(c.types, e.d, x)
        return false
    end
    c.shape == SHAPE_TRIVIAL && return true
    b = e.bodies[c.id+1]
    (b === nothing || e.guards[c.id+1]) && return run_in_place(e, c.id, x)
    return enter(e, c.shape, b, x)
end

# The anyOf/oneOf branches that can match: those a discriminator selects, or those admitting the instance type.
function candidates(e::Evaluator, b::Branches, x::Int)
    d = e.d
    k = kind(d, x)
    disc = b.discriminator
    (disc === nothing || k != KIND_OBJECT) && return b.by_kind[kind_index(k)]
    v = property(d, x, disc.property)
    v >= 0 && return select_branches(b.index::DiscriminatorIndex, disc, d, v)
    disc.all_require && return EMPTY_U32
    return b.by_kind[kind_index(k)]
end

# Evaluates a node's keywords. Where the program keeps a dynamic scope, entering a node of another resource pushes
# that resource (as the general evaluator does), for the dynamic references below it.
function run_body(e::Evaluator, b::Body, x::Int)::Bool
    e.p.uses_dynamic_scope || return run_keywords(e, b, x)
    pushed = push_scope!(e, node(e.p, b.node).resource_id)
    ok = run_keywords(e, b, x)
    pushed && pop_scope!(e)
    return ok
end

function run_keywords(e::Evaluator, b::Body, x::Int)::Bool
    d = e.d
    k = kind(d, x)
    if k == KIND_OBJECT
        f = b.fused
        f === nothing || return run_fused(e, f, x)
        (b.general & TYPE_OBJECT) != 0 && return eval_node(e, b.node, x, NO_BITS)
    elseif k == KIND_ARRAY
        (b.general & TYPE_ARRAY) != 0 && return eval_node(e, b.node, x, NO_BITS)
    end
    values = b.values
    for i in eachindex(values)
        run_op(e, values[i], x) || return false
    end
    if k == KIND_NUMBER
        (isempty(b.number) || run_number(e, b.number, x)) || return false
    elseif k == KIND_STRING
        (isempty(b.str) || run_string(e, b.str, x)) || return false
    elseif k == KIND_OBJECT
        o = b.object
        (o === nothing || run_object(e, o, x)) || return false
    elseif k == KIND_ARRAY
        a = b.array
        (a === nothing || run_array(e, a, x)) || return false
        if b.has_unevaluated_items
            first_item = first(d, x)
            for i in b.unevaluated_from:count(d, x)-1
                @run_child(e, e.d, b.unevaluated_child, first_item + i) || return false
            end
        end
    end
    return isempty(b.apply) || run_apply(e, b.apply, x)
end

function run_apply(e::Evaluator, ops::Vector{Op}, x::Int)::Bool
    for i in eachindex(ops)
        run_op(e, ops[i], x) || return false
    end
    return true
end

function enum_contains(e::Evaluator, values::Vector{ValueRef}, x::Int)
    for v in values
        values_equal(e.d, x, v.d, v.n) && return true
    end
    return false
end

function run_op(e::Evaluator, o::Op, x::Int)::Bool
    d = e.d
    k = o.kind
    if k == OP_CONST
        return values_equal(d, x, o.value.d, o.value.n)
    elseif k == OP_ENUM_STRINGS
        return kind(d, x) == KIND_STRING && find(o.names, str(d, x)) >= 0
    elseif k == OP_ENUM
        return enum_contains(e, o.values, x)
    elseif k == OP_REF
        return run_in_place(e, o.node, x)
    elseif k == OP_ALL_OF
        list = o.children
        for i in eachindex(list)
            run_branch(e, list[i], x) || return false
        end
        return true
    elseif k == OP_ANY_OF
        list = o.branches.children
        for i in candidates(e, o.branches, x)
            run_branch(e, list[i+1], x) && return true
        end
        return false
    elseif k == OP_ONE_OF
        list = o.branches.children
        selected = candidates(e, o.branches, x)
        # The instance's type (or the discriminator) leaves one branch: that branch decides.
        length(selected) == 1 && return run_branch(e, list[selected[1]+1], x)
        matched = 0
        for i in selected
            if run_branch(e, list[i+1], x)
                matched += 1
                matched > 1 && break
            end
        end
        return matched == 1
    elseif k == OP_NOT
        return !@run(e, o.node, x)
    elseif k == OP_DYNAMIC_REF
        return run_in_place(e, target(e.p, resolve_dynamic(e, o.dynamic)), x)
    end
    next = run_in_place(e, o.node, x) ? o.then_ : o.else_
    return next < 0 || run_in_place(e, next, x)
end

# Evaluates an object plan: the size bounds, the property loop for its shape, then the required names and
# dependencies.
function run_object(e::Evaluator, pl::ObjectPlan, x::Int)::Bool
    d = e.d
    n = count(d, x)
    (n % UInt64 < pl.min || n % UInt64 > pl.max) && return false
    seen, ok = UInt64(0), true
    visit = pl.visit
    if visit == VISIT_VALUES
        ok = visit_values(e, pl, x)
    elseif visit == VISIT_NAMES
        ok = n <= pl.lookup_max ? visit_lookup(e, pl, first(d, x), n) : visit_names(e, pl, first(d, x), n)
        seen = e.seen
    elseif visit == VISIT_PATTERN
        ok = visit_pattern(e, pl, x)
    elseif visit == VISIT_GENERAL
        seen, ok = visit_general(e, pl, x)
    end
    (!ok || (seen & pl.required_mask) != pl.required_mask) && return false
    return pl.rest_free || object_rest(e, pl, x, seen)
end

# The strict loop: bounds, declared names (additionalProperties for the rest), and the required mask. A small
# function of its own for the callers that are not enter_child, which has the same lines in it.
function run_strict_object(e::Evaluator, pl::ObjectPlan, x::Int)::Bool
    d = e.d
    n = count(d, x)
    (n % UInt64 < pl.min || n % UInt64 > pl.max) && return false
    return n <= pl.lookup_max ? visit_lookup(e, pl, first(d, x), n) : visit_names(e, pl, first(d, x), n)
end

# Looks each name up in the object, whose n properties start at first_child (a plan with lookup: the other
# properties need no visit). It reports whether the properties are valid and every required name was seen, and
# leaves the names seen in the evaluator. The properties are four words each on the tape (a name and a value), so
# the search for a name reads the lengths of the property names side by side.
function visit_lookup(e::Evaluator, pl::ObjectPlan, first_child::Int, n::Int)::Bool
    d = e.d
    tape = d.tape
    seen = UInt64(0)
    ns = pl.names
    keys = ns.keys
    for i in eachindex(keys)
        key = keys[i]
        len = key.length
        for j in 0:n-1
            at = first_child + 2j
            hcount(tape[at+1].h) == len || continue
            k = first_child + 2j
            name = str(d, k)
            (name_word(name) != key.word || (len > 8 && !name_rest(ns, i - 1, name))) && continue
            seen |= UInt64(1) << ((i - 1) & 63)
            c = pl.children[i]
            if (c.pass & (tape[at+2].h % UInt8)) == 0 && !enter_child(e, c, k + 1)
                return false
            end
            break
        end
    end
    e.seen = seen
    return (seen & pl.required_mask) == pl.required_mask
end

# Checks the required names checked by lookup, and the dependencies.
function object_rest(e::Evaluator, pl::ObjectPlan, x::Int, seen::UInt64)::Bool
    d = e.d
    for r in pl.required
        property(d, x, r) < 0 && return false
    end
    for dep in pl.dependencies
        if dep.has_bits
            (seen & dep.name_bit) == 0 && continue
            (seen & dep.required_bits) != dep.required_bits && return false
        else
            property(d, x, dep.name) < 0 && continue
            for r in dep.required
                property(d, x, r) < 0 && return false
            end
        end
        dep.schema >= 0 && !run_in_place(e, dep.schema, x) && return false
    end
    return true
end

# For only additionalProperties: every value against one child.
function visit_values(e::Evaluator, pl::ObjectPlan, x::Int)::Bool
    d = e.d
    c = pl.additional
    first_child, n = first(d, x), count(d, x)
    if c.shape == SHAPE_TRIVIAL
        c.types == ANY_TYPE && return true
        # The values are every second entry of the properties' run of the tape.
        tape = d.tape
        for j in 0:n-1
            if (c.types & (tape[first_child+2j+2].h % UInt8)) == 0 && !integer_ok(c.types, d, first_child + 2j + 1)
                return false
            end
        end
        return true
    end
    for i in 0:n-1
        @run_child(e, d, c, first_child + 2i + 1) || return false
    end
    return true
end

# For declared properties, and additionalProperties for the rest, over the n properties that start at first_child.
# It reports whether the properties are valid and every required name was seen, and leaves the declared names seen
# in the evaluator. The result is one value in a register: a pair of the names and the outcome would be returned
# through memory.
function visit_names(e::Evaluator, pl::ObjectPlan, first_child::Int, n::Int)::Bool
    d = e.d
    ns = pl.names
    children = pl.children
    seen = UInt64(0)
    hint = 0
    k = first_child
    stop = k + 2n
    while k < stop
        i, hint = find_next(ns, str(d, k), hint)
        if i >= 0
            seen |= UInt64(1) << (i & 63)
            @run_child(e, d, children[i+1], k + 1) || return false
        elseif pl.has_additional && !@run_child(e, d, pl.additional, k + 1)
            return false
        end
        k += 2
    end
    e.seen = seen
    return (seen & pl.required_mask) == pl.required_mask
end

function visit_pattern(e::Evaluator, pl::ObjectPlan, x::Int)::Bool
    d = e.d
    p = pl.patterns[1]
    k = first(d, x)
    stop = k + 2 * count(d, x)
    while k < stop
        if pattern_match(p.pattern, str(d, k), str_ascii(d, k))
            @run_child(e, d, p.child, k + 1) || return false
        elseif pl.has_additional && !@run_child(e, d, pl.additional, k + 1)
            return false
        end
        k += 2
    end
    return true
end

function visit_general(e::Evaluator, pl::ObjectPlan, x::Int)::Tuple{UInt64,Bool}
    d = e.d
    ns = pl.names
    patterns = pl.patterns
    seen = UInt64(0)
    hint = 0
    k = first(d, x)
    stop = k + 2 * count(d, x)
    while k < stop
        name = str(d, k)
        matched = false
        i, hint = find_next(ns, name, hint)
        if i >= 0
            seen |= UInt64(1) << (i & 63)
            # A name only required (or a dependency) mentions is undeclared: patterns, else additionalProperties.
            if i < pl.declared
                matched = true
                @run_child(e, d, pl.children[i+1], k + 1) || return UInt64(0), false
            end
            for j in pl.name_patterns[i+1]
                matched = true
                @run_child(e, d, patterns[j+1].child, k + 1) || return UInt64(0), false
            end
        else
            for j in eachindex(patterns)
                if pattern_match(patterns[j].pattern, name, str_ascii(d, k))
                    matched = true
                    @run_child(e, d, patterns[j].child, k + 1) || return UInt64(0), false
                end
            end
        end
        if !matched && pl.has_additional && !@run_child(e, d, pl.additional, k + 1)
            return UInt64(0), false
        end
        # The name is a string value of the document: it is evaluated where it is.
        if pl.property_names >= 0 && !@run(e, pl.property_names, k)
            return UInt64(0), false
        end
        k += 2
    end
    return seen, true
end

function run_array(e::Evaluator, pl::ArrayPlan, x::Int)::Bool
    d = e.d
    n = count(d, x)
    (UInt64(n) < pl.min || UInt64(n) > pl.max) && return false
    first_item = first(d, x)
    pl.is_simple && return all_of_type(d, first_item, n, pl.simple)
    prefix = min(length(pl.prefix), n)
    for i in 0:prefix-1
        @run_child(e, d, pl.prefix[i+1], first_item + i) || return false
    end
    if pl.has_items
        items = pl.items
        if pl.has_nested
            nested = pl.nested
            for item in first_item+prefix:first_item+n-1
                if kind(d, item) != KIND_ARRAY
                    # Not an array: only the items' type test applies.
                    type_ok(items.types, d, item) || return false
                    continue
                end
                len = UInt64(count(d, item))
                if (items.types & TYPE_ARRAY) == 0 || len < nested.min || len > nested.max ||
                   !all_of_type(d, first(d, item), Int(len), nested.types)
                    return false
                end
            end
        elseif items.shape == SHAPE_TRIVIAL
            # A type-only items schema: one tight loop, or none for true.
            all_of_type(d, first_item + prefix, n - prefix, items.types) || return false
        else
            for item in first_item+prefix:first_item+n-1
                @run_child(e, d, items, item) || return false
            end
        end
    end
    if pl.contains >= 0
        found = UInt64(0)
        for i in 0:n-1
            if @run(e, pl.contains, first_item + i)
                found += 1
                !pl.max_contains.set && found >= pl.min_contains && break
            end
        end
        (found < pl.min_contains || (pl.max_contains.set && found > pl.max_contains.n)) && return false
    end
    return !pl.unique || all_unique(d, x, e.unique)
end

# Reports whether each of n consecutive values is of the types in a mask.
function all_of_type(d::Document, first_item::Int, n::Int, types::UInt8)
    types == ANY_TYPE && return true
    tape = d.tape
    if (types & TYPE_INTEGER) != 0 && (types & TYPE_NUMBER) == 0
        # Integers that are not numbers in general: the number's value decides.
        for i in 0:n-1
            v = tape[first_item+i+1]
            bit = v.h % UInt8
            if (types & bit) == 0 && !(bit == KIND_NUMBER && is_integer_number(hflags(v.h), v.d))
                return false
            end
        end
        return true
    end
    # A kind is its type bit. Up to three items (a position, a pair, most lists of tags) are tested one by one: for
    # the loop the compiler works out, before the first item, the range of the items it can read without a check,
    # which costs more than the checks of so few.
    if n <= 3
        n <= 0 && return true
        ((tape[first_item+1].h % UInt8) & types) == 0 && return false
        n == 1 && return true
        ((tape[first_item+2].h % UInt8) & types) == 0 && return false
        n == 2 && return true
        return ((tape[first_item+3].h % UInt8) & types) != 0
    end
    for i in 0:n-1
        ((tape[first_item+i+1].h % UInt8) & types) == 0 && return false
    end
    return true
end

# Evaluates a leaf's keywords: its value constraints, then those for the instance's type.
function run_leaf(e::Evaluator, b::Body, x::Int)::Bool
    d = e.d
    values = b.values
    for i in eachindex(values)
        o = values[i]
        ok = if o.kind == OP_CONST
            values_equal(d, x, o.value.d, o.value.n)
        elseif o.kind == OP_ENUM_STRINGS
            kind(d, x) == KIND_STRING && find(o.names, str(d, x)) >= 0
        else
            enum_contains(e, o.values, x)
        end
        ok || return false
    end
    k = kind(d, x)
    k == KIND_NUMBER && return isempty(b.number) || run_number(e, b.number, x)
    k == KIND_STRING && return isempty(b.str) || run_string(e, b.str, x)
    return true
end

# Calls a custom format assertion, which the user gave as any callable.
@noinline function call_custom_format(p::Program, index::Int32, text::String)
    return p.format_validators[index](text)::Bool
end

function check_format_string(e::Evaluator, f::FormatCheck, s::Bytes)
    f.custom != 0 && return call_custom_format(e.p, f.custom, String(s))
    return check_string_format(f.kind, s, f.legacy)
end

function check_format_number(e::Evaluator, f::FormatCheck, x::Int)
    f.custom != 0 && return call_custom_format(e.p, f.custom, String(number_text(e.d, x)))
    return check_number_format(f.kind, e.d, x)
end

function run_number(e::Evaluator, ops::Vector{NumberOp}, x::Int)::Bool
    d = e.d
    flag, dat = flags(d, x), data(d, x)
    for i in eachindex(ops)
        o = ops[i]
        k = o.kind
        ok = if k == NUMBER_MINIMUM
            compare_numbers(flag, dat, o.flag, o.data) >= 0
        elseif k == NUMBER_MAXIMUM
            compare_numbers(flag, dat, o.flag, o.data) <= 0
        elseif k == NUMBER_EXCLUSIVE_MINIMUM
            compare_numbers(flag, dat, o.flag, o.data) > 0
        elseif k == NUMBER_EXCLUSIVE_MAXIMUM
            compare_numbers(flag, dat, o.flag, o.data) < 0
        elseif k == NUMBER_MULTIPLE_OF
            divides(o.divisor, d, x)
        else
            check_format_number(e, o.format, x)
        end
        ok || return false
    end
    return true
end

function run_string(e::Evaluator, ops::Vector{StringOp}, x::Int)::Bool
    d = e.d
    for i in eachindex(ops)
        o = ops[i]
        k = o.kind
        ok = if k == STRING_LENGTH
            length_ok(d, x, o.min, o.max)
        elseif k == STRING_PATTERN
            pattern_match(o.pattern, str(d, x), str_ascii(d, x))
        elseif k == STRING_FORMAT
            check_format_string(e, o.format, str(d, x))
        else
            content_ok(e, str(d, x), o.content)
        end
        ok || return false
    end
    return true
end

# Decides minLength/maxLength, counting code points only when the byte length cannot decide (a code point is one to
# four bytes).
function length_ok(d::Document, x::Int, lo::UInt64, hi::UInt64)
    len = UInt64(count(d, x))
    len < lo && return false
    str_ascii(d, x) && return len <= hi
    quarter = (len + 3) >> 2
    (len <= hi && quarter >= lo) && return true
    # Every code point is at most four bytes: more than max of them for sure.
    quarter > hi && return false
    chars = code_point_count(d, x)
    return chars >= lo && chars <= hi
end
