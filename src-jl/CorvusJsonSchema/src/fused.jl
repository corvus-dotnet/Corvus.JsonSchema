# The fused object plan: one pass over an object's properties for a schema whose object semantics are spread over
# in-place applicators ($ref, allOf, if/then/else, dependencies, required-only oneOf/anyOf) and possibly finished by
# unevaluatedProperties. Ported from fused.go.
#
# The general path evaluates each applicator branch as a separate pass over the instance, each with its own property
# lookups, and then walks the instance once more for unevaluatedProperties. The fused plan resolves every property
# name known to any branch at compile time to the list of child schemas that apply to it (a branch's own property
# schema, its matching pattern-property schemas, or its additionalProperties when neither matched), so evaluation is
# one lookup per instance property, and it tracks which properties some branch covered so that the unevaluated check
# needs no second analysis.
#
# Branches under then/else (or a dependency's schema) apply only when their condition holds. The plan supports
# conditions the pass itself decides (required names, property values tested against constants or a pattern, and
# names no property may match), and defers those branches' applications to a second step over the properties they
# touch.
#
# Fail-fast evaluation only. The fused pass does not enter its contributors as nodes (and so would not push their
# resources on the dynamic scope): below a live dynamic reference, only contributors in the node's own resource
# fuse.

# The mask of the constant tests that allow the value.
function allowed_tests(m::MergedTests, d::Document, v::Int)
    if kind(d, v) == KIND_STRING
        i = find(m.strings, str(d, v))
        return i >= 0 ? m.string_masks[i+1] : UInt64(0)
    end
    for other in m.others
        values_equal(d, v, other.value.d, other.value.n) && return other.mask
    end
    return UInt64(0)
end

function test_holds(t::ValueTest, d::Document, v::Int)
    if t.is_pattern
        kind(d, v) == KIND_STRING && return pattern_match(t.pattern, str(d, v), str_ascii(d, v))
        return !t.requires_string
    end
    for a in t.allowed
        values_equal(d, v, a.d, a.n) && return true
    end
    return false
end

# ----------------------------------------------------------------------------------------------------------------------
# Construction

# A contributor as collected: the branch, its condition, its alternative group and branch.
struct CollectedContributor
    node::NodeId
    condition::Gate
    alt::AltBranch
end

struct PendingCondition
    # The if schema, or the dependency's property name.
    is_test::Bool
    test::NodeId
    dependency::String
    gate::Gate
end

# What a collection gathers.
mutable struct FuseCollector
    p::Program
    contributors::Vector{CollectedContributor}
    conditions::Vector{PendingCondition}
    # Dependencies' required names, each with its condition.
    extras::Vector{Tuple{UInt16,Vector{String}}}
    # Required-only alternatives: the condition, whether exactly one must match, and the branches' names.
    alternatives::Vector{Tuple{Gate,Bool,Vector{Vector{String}}}}
    alt_groups::Vector{FusedAltGroup}
    forbidden::Vector{Tuple{Gate,Vector{String}}}
    # Alternative groups with object keywords only where coverage is not tracked (a failed branch must not cover).
    allow_alt_groups::Bool
    # Every contributor must belong to this resource (below a live dynamic reference, where the general path would
    # push a contributor's own resource on the dynamic scope and the fused pass does not enter contributors).
    same_resource::Bool
    resource::UInt32
end

other_resource(c::FuseCollector, n::SchemaNode) = c.same_resource && n.resource_id != c.resource

# Walks the in-place applicators of a branch, adding every object branch with its condition. It fails when a branch
# cannot be fused.
function collect!(c::FuseCollector, id::NodeId, condition::Gate)
    p = c.p
    n = node(p, id)
    n.always_true && return true
    if n.always_false || n.in_place_cycle || !is_object_branch(p, n, isempty(c.contributors))
        return false
    end
    (length(c.contributors) >= MAX_FUSED_CONTRIBUTORS || other_resource(c, n)) && return false
    push!(c.contributors, CollectedContributor(id, condition, AltBranch()))
    if n.not >= 0
        names = forbidden_names(p, n.not)
        push!(c.forbidden, (condition, names === nothing ? String[] : names))
    end
    for r in (n.ref, n.static_dynamic_ref)
        r >= 0 && !collect!(c, target(p, r), condition) && return false
    end
    for child in n.all_of
        collect!(c, target(p, child), condition) || return false
    end
    if n.has_one_of
        discriminated = n.one_of_discriminator !== nothing || is_type_union(c, n.one_of)
        collect_alternative!(c, n.one_of, condition, true, discriminated) || return false
    end
    if n.has_any_of
        discriminated = n.any_of_discriminator !== nothing || is_type_union(c, n.any_of)
        collect_alternative!(c, n.any_of, condition, false, discriminated) || return false
    end
    for dep in n.dependencies
        length(c.conditions) >= MAX_FUSED_CONDITIONS && return false
        index = UInt16(length(c.conditions))
        push!(c.conditions, PendingCondition(false, NO_NODE, dep.name, condition))
        isempty(dep.required) || push!(c.extras, (index, dep.required))
        if dep.schema >= 0 && !collect!(c, target(p, dep.schema), Gate(true, index, true))
            return false
        end
    end
    if n.if_ >= 0
        test_id = target(p, n.if_)
        test = node(p, test_id)
        then_ = n.then_ >= 0 ? target(p, n.then_) : NO_NODE
        else_ = n.else_ >= 0 ? target(p, n.else_) : NO_NODE
        test.always_true && return then_ < 0 || collect!(c, then_, condition)
        test.always_false && return else_ < 0 || collect!(c, else_, condition)
        (!supported_condition(c, test) || length(c.conditions) >= MAX_FUSED_CONDITIONS) && return false
        index = UInt16(length(c.conditions))
        push!(c.conditions, PendingCondition(true, test_id, "", condition))
        # When the condition holds, the if schema's own properties count as evaluated, so it contributes under the
        # same condition as then.
        test.has_properties && !collect!(c, test_id, Gate(true, index, true)) && return false
        then_ >= 0 && !collect!(c, then_, Gate(true, index, true)) && return false
        else_ >= 0 && !collect!(c, else_, Gate(true, index, false)) && return false
    end
    return true
end

# Reports that every branch only tests the type (the general path decides the keyword by one mask test).
function is_type_union(c::FuseCollector, list::Vector{NodeId})
    for id in list
        b = node(c.p, target(c.p, id))
        (b.has_type && is_type_only(b)) || return false
    end
    return true
end

# Fuses a oneOf/anyOf when every branch is a plain required list (decided from the seen names after the pass), or as
# an alternative group of object branches where coverage is not tracked.
function collect_alternative!(c::FuseCollector, list::Vector{NodeId}, condition::Gate, exactly_one::Bool,
    discriminated::Bool)
    p = c.p
    branches = NodeId[target(p, id) for id in list]
    required_only = true
    required = Vector{String}[]
    for b in branches
        n = node(p, b)
        if !isempty(n.required) && is_required_list_only(n)
            push!(required, n.required)
        else
            push!(required, String[])
            required_only = false
        end
    end
    if required_only
        push!(c.alternatives, (condition, exactly_one, required))
        return true
    end
    # Branches with object keywords: each a contributor whose failure marks the branch rather than the object. The
    # pass applies every branch that knows a name to that property, where the general path stops at the first branch
    # that passes, so a name with an expensive child in more than one branch is refused. A keyword the general path
    # decides by discriminator or type stays with it.
    if !c.allow_alt_groups || discriminated || condition.set || length(c.alt_groups) >= MAX_FUSED_ALT_GROUPS ||
       length(branches) > 64 || !expensive_children_disjoint(c, branches)
        return false
    end
    group = UInt16(length(c.alt_groups))
    for (i, b) in enumerate(branches)
        n = node(p, b)
        if n.always_true || n.always_false || n.in_place_cycle || has_in_place_applicators(n) ||
           n.has_dependencies || n.not >= 0 || !is_object_branch(p, n, false) || other_resource(c, n) ||
           length(c.contributors) >= MAX_FUSED_CONTRIBUTORS
            return false
        end
        push!(c.contributors, CollectedContributor(b, Gate(), AltBranch(true, group, UInt16(i - 1))))
    end
    push!(c.alt_groups, FusedAltGroup(exactly_one, UInt32(length(branches))))
    return true
end

# Reports whether no property name gets an expensive child (anything but a leaf or a boolean schema) from more than
# one branch. Pattern and additional properties count as every name.
function expensive_children_disjoint(c::FuseCollector, branches::Vector{NodeId})
    p = c.p
    function cheap(id::NodeId)
        n = node(p, target(p, id))
        return n.always_true || n.always_false || is_leaf(n) || is_simple_array(c, n)
    end
    expensive = String[]
    wildcards = 0
    for b in branches
        n = node(p, b)
        for property in n.properties
            if !cheap(property.node)
                property.name in expensive && return false
                push!(expensive, property.name)
            end
        end
        wildcard = n.additional_properties >= 0 && !cheap(n.additional_properties)
        for pp in n.pattern_properties
            wildcard = wildcard || !cheap(pp.node)
        end
        if wildcard
            wildcards += 1
            wildcards > 1 && return false
        end
    end
    return wildcards == 0 || isempty(expensive)
end

# Reports an array of leaf items with at most size bounds: cheap to apply more than once.
function is_simple_array(c::FuseCollector, n::SchemaNode)
    if !has_array_keywords(n) || has_object_keywords(n) || has_in_place_applicators(n) ||
       n.dynamic_ref !== nothing || is_set(n.const_value) || n.has_enum || n.has_prefix_items || n.contains >= 0 ||
       n.unique_items || n.unevaluated_items >= 0 || n.items < 0
        return false
    end
    item = node(c.p, target(c.p, n.items))
    return item.always_true || is_leaf(item)
end

# Reports a condition the pass decides alone: required names, and properties whose schemas are value tests,
# optionally with type: object (which the object plan has established). At least one of the two.
function supported_condition(c::FuseCollector, test::SchemaNode)
    p = c.p
    some = !isempty(test.required)
    if is_set(test.const_value) || test.has_enum || has_number_keywords(test) || has_string_keywords(test) ||
       has_array_keywords(test) || has_in_place_applicators(test) || test.dynamic_ref !== nothing ||
       (test.has_type && (test.type_mask & TYPE_OBJECT) == 0) || test.additional_properties >= 0 ||
       test.property_names_schema >= 0 || test.unevaluated_properties >= 0 || test.has_dependencies ||
       test.min_properties.set || test.max_properties.set
        return false
    end
    for property in test.properties
        value_test_of(node(p, target(p, property.node))) === nothing && return false
        some = true
    end
    # patternProperties: {P: false}: no property name may match P.
    for pp in test.pattern_properties
        node(p, target(p, pp.node)).always_false || return false
        some = true
    end
    return some
end

# Reports a branch whose only effect on an object instance is through object keywords and fusable in-place
# applicators.
function is_object_branch(p::Program, n::SchemaNode, allow_unevaluated::Bool)
    if n.not >= 0 && forbidden_names(p, n.not) === nothing
        return false
    end
    return !(is_set(n.const_value) || n.has_enum || has_number_keywords(n) || has_string_keywords(n) ||
             (n.has_type && (n.type_mask & TYPE_OBJECT) == 0) || n.property_names_schema >= 0 ||
             n.dynamic_ref !== nothing || (!allow_unevaluated && n.unevaluated_properties >= 0))
end

function is_type_only(n::SchemaNode)
    return is_leaf(n) && !is_set(n.const_value) && !n.has_enum && !has_number_keywords(n) && !has_string_keywords(n)
end

# Reports only local keywords (type, const, enum, number and string keywords).
function is_leaf(n::SchemaNode)
    return !n.always_true && !n.always_false && !has_object_keywords(n) && !has_array_keywords(n) &&
           !has_in_place_applicators(n) && n.dynamic_ref === nothing
end

# The names of a not whose schema is a non-empty required list (an object fails when all are present), or nothing.
function forbidden_names(p::Program, not::NodeId)
    n = node(p, target(p, not))
    (n.always_true || n.always_false || !is_required_list_only(n) || isempty(n.required)) && return nothing
    return n.required
end

function is_required_list_only(n::SchemaNode)
    return !(is_set(n.const_value) || n.has_enum || has_number_keywords(n) || has_string_keywords(n) ||
             has_array_keywords(n) || has_in_place_applicators(n) || n.dynamic_ref !== nothing ||
             n.unevaluated_properties >= 0 || n.unevaluated_items >= 0 ||
             (n.has_type && (n.type_mask & TYPE_OBJECT) == 0) || n.has_pattern_properties ||
             n.additional_properties >= 0 || n.property_names_schema >= 0 || n.has_dependencies ||
             n.min_properties.set || n.max_properties.set || !isempty(n.properties))
end

# The value test for a property schema inside an if: const/enum of scalars (the values the schema's type admits), or
# a pattern with at most type: string. Nothing for anything else.
function value_test_of(n::SchemaNode)
    if n.always_true || n.always_false || has_number_keywords(n) || has_object_keywords(n) ||
       has_array_keywords(n) || has_in_place_applicators(n) || n.dynamic_ref !== nothing
        return nothing
    end
    pattern = n.pattern
    if pattern !== nothing
        only_pattern = !is_set(n.const_value) && !n.has_enum && !n.min_length.set && !n.max_length.set &&
                       !(n.assert_format && n.has_format) && !n.assert_content &&
                       (!n.has_type || n.type_mask == TYPE_STRING)
        return only_pattern ? ValueTest(0, true, ValueRef[], pattern, n.has_type) : nothing
    end
    has_string_keywords(n) && return nothing
    values = if is_set(n.const_value)
        ValueRef[n.const_value]
    elseif !isempty(n.enum_values)
        n.enum_values
    else
        return nothing
    end
    allowed = ValueRef[]
    for v in values
        k = kind(v.d, v.n)
        if k == KIND_NUMBER
            is_integer_number(flags(v.d, v.n), data(v.d, v.n)) || return nothing
        elseif !(k == KIND_STRING || k == KIND_BOOL || k == KIND_NULL)
            return nothing
        end
        # A value the schema's type rejects never passes, so it is not allowed.
        if !n.has_type || type_ok(n.type_mask, v.d, v.n)
            push!(allowed, v)
        end
    end
    return ValueTest(0, false, allowed, NO_PATTERN, false)
end

# One resolution of a property by a contributor, before identical ones are merged.
struct CollectedApp
    contributor::UInt16
    child::OptChild
    target::NodeId
end

# Builds the fused plan for a node, or nothing when it cannot be fused or fusing does not pay.
function try_fuse(p::Program, id::NodeId, same_resource::Bool, child_of)
    n = node(p, id)
    (!is_object_branch(p, n, true) || n.in_place_cycle || n.always_true || n.always_false) && return nothing
    ctx = FuseCollector(p, CollectedContributor[], PendingCondition[], Tuple{UInt16,Vector{String}}[],
        Tuple{Gate,Bool,Vector{Vector{String}}}[], FusedAltGroup[], Tuple{Gate,Vector{String}}[],
        n.unevaluated_properties < 0, same_resource, n.resource_id)
    collect!(ctx, id, Gate()) || return nothing
    if length(ctx.contributors) + length(ctx.extras) > MAX_FUSED_CONTRIBUTORS ||
       length(ctx.conditions) > MAX_FUSED_CONDITIONS
        return nothing
    end

    # Fusing pays when a pass over every property is unavoidable (unevaluatedProperties) or when it replaces several
    # passes: two or more branches with object keywords, an if the seen names decide, or alternatives. A node whose
    # object keywords are all its own keeps its object plan.
    has_if = any(condition -> condition.is_test, ctx.conditions)
    if n.unevaluated_properties < 0 && !has_if && isempty(ctx.alternatives) && isempty(ctx.alt_groups)
        effective = 0
        for contributor in ctx.contributors
            b = node(p, contributor.node)
            if b.has_properties || b.has_pattern_properties || b.additional_properties >= 0 ||
               !isempty(b.required) || b.min_properties.set || b.max_properties.set
                effective += 1
            end
        end
        effective < 2 && return nothing
    end

    # Every name any branch or condition knows gets an index.
    known = String[]
    function bit(name::String)
        for (i, k) in enumerate(known)
            k == name && return UInt16(i - 1)
        end
        push!(known, name)
        return UInt16(length(known) - 1)
    end
    bits_of(list::Vector{String}) = UInt16[bit(name) for name in list]
    for contributor in ctx.contributors
        b = node(p, contributor.node)
        for property in b.properties
            bit(property.name)
        end
        foreach(bit, b.required)
    end
    absent = FusedAbsent[]
    conditions = FusedCondition[]
    tests_by_entry = Tuple{UInt16,ValueTest}[]
    for (i, pending) in enumerate(ctx.conditions)
        condition = UInt16(i - 1)
        required = UInt16[]
        if pending.is_test
            test = node(p, pending.test)
            for pp in test.pattern_properties
                push!(absent, FusedAbsent(condition, pp.pattern))
            end
            for property in test.properties
                vt = value_test_of(node(p, target(p, property.node)))::ValueTest
                vt = ValueTest(condition, vt.is_pattern, vt.allowed, vt.pattern, vt.requires_string)
                push!(tests_by_entry, (bit(property.name), vt))
            end
            required = bits_of(test.required)
        else
            required = UInt16[bit(pending.dependency)]
        end
        push!(conditions, FusedCondition(required, pending.gate))
    end
    forbidden = FusedForbidden[FusedForbidden(gate, bits_of(names)) for (gate, names) in ctx.forbidden]
    extras = Tuple{UInt16,Vector{UInt16}}[(condition, bits_of(names)) for (condition, names) in ctx.extras]
    alternatives = FusedAlternative[]
    for (condition, exactly_one, branches) in ctx.alternatives
        push!(alternatives, FusedAlternative(condition, exactly_one, Vector{UInt16}[bits_of(b) for b in branches]))
    end
    length(known) > MAX_FUSED_NAMES && return nothing

    function app_child(c::NodeId)
        t = target(p, c)
        return node(p, t).always_true ? OptChild() : OptChild(true, child_of(t))
    end
    # A contributor's condition as masks: then_ when it applies by holding, els when by not holding.
    function gate_masks(g::Gate)
        g.set || return UInt64(0), UInt64(0)
        return g.polarity ? (UInt64(1) << g.condition, UInt64(0)) : (UInt64(0), UInt64(1) << g.condition)
    end
    contributors = FusedContributor[]
    for contributor in ctx.contributors
        b = node(p, contributor.node)
        patterns = FusedPattern[FusedPattern(pp.pattern, app_child(pp.node)) for pp in b.pattern_properties]
        has_additional = b.additional_properties >= 0
        additional = has_additional ? app_child(b.additional_properties) : OptChild()
        then_, els = gate_masks(contributor.condition)
        push!(contributors, FusedContributor(contributor.condition, contributor.alt, patterns, has_additional,
            additional, bits_of(b.required), b.min_properties, b.max_properties, then_, els))
    end
    for (condition, names) in extras
        gate = Gate(true, condition, true)
        then_, els = gate_masks(gate)
        push!(contributors, FusedContributor(gate, AltBranch(), FusedPattern[], false, OptChild(), names,
            OptCount(), OptCount(), then_, els))
    end

    tests = [ValueTest[] for _ in known]
    for (entry, test) in tests_by_entry
        push!(tests[entry+1], test)
    end
    # A known name matching an absent pattern fails its condition whatever its value: no constant is allowed.
    for (e, name) in enumerate(known)
        for a in absent
            if match_string(a.pattern, name)
                push!(tests[e], ValueTest(a.condition, false, ValueRef[], NO_PATTERN, false))
            end
        end
    end

    # Resolve every known name against every branch now.
    entries = FusedEntry[]
    for (i, name) in enumerate(known)
        apps = CollectedApp[]
        for (c, contributor) in enumerate(ctx.contributors)
            b = node(p, contributor.node)
            matched = false
            for property in b.properties
                if property.name == name
                    matched = true
                    push!(apps, CollectedApp(UInt16(c - 1), app_child(property.node), target(p, property.node)))
                    break
                end
            end
            for pp in b.pattern_properties
                if match_string(pp.pattern, name)
                    matched = true
                    push!(apps, CollectedApp(UInt16(c - 1), app_child(pp.node), target(p, pp.node)))
                end
            end
            if !matched && b.additional_properties >= 0
                a = b.additional_properties
                push!(apps, CollectedApp(UInt16(c - 1), app_child(a), target(p, a)))
            end
        end
        push!(entries, FusedEntry(coalesce_apps(apps, contributors), tests[i], merge_value_tests(tests[i])))
    end

    flat = isempty(conditions) && isempty(alternatives) && isempty(ctx.alt_groups) && isempty(forbidden) &&
           n.unevaluated_properties < 0 && length(known) <= 64
    resolves_unknown, has_count_bounds = false, false
    finals = UInt16[]
    for (i, c) in enumerate(contributors)
        flat = flat && !c.condition.set && isempty(c.patterns) && !c.has_additional
        resolves_unknown = resolves_unknown || !isempty(c.patterns) || c.has_additional
        has_count_bounds = has_count_bounds || c.min.set || c.max.set
        if !isempty(c.required) || c.min.set || c.max.set
            push!(finals, UInt16(i - 1))
        end
    end
    for entry in entries
        flat = flat && length(entry.apps) <= 1 && isempty(entry.tests)
    end
    resolves_unknown = resolves_unknown || !isempty(absent)
    names = Names(known)
    for entry in entries
        for j in eachindex(entry.apps)
            app = entry.apps[j]
            primary = contributors[app.contributor+1]
            then_, els = primary.then_, primary.els
            for other in app.others
                then_ |= contributors[other+1].then_
                els |= contributors[other+1].els
            end
            entry.apps[j] = FusedApp(app.contributor, app.has_child, app.child, app.others, then_, els)
        end
    end
    flat_plan = nothing
    if flat
        o = ObjectPlan(UInt64(0), typemax(UInt64), VISIT_NAMES, names, length(known),
            Child[NO_CHILD for _ in known], UInt64(0), String[], PatternChild[], [UInt16[] for _ in known],
            # No contributor has additionalProperties (flat requires it).
            false, NO_CHILD, NO_NODE, PlanDependency[], true, true, length(known) <= LOOKUP_NAMES)
        for c in contributors
            for r in c.required
                o.required_mask |= UInt64(1) << r
            end
            if c.min.set && c.min.n > o.min
                o.min = c.min.n
            end
            if c.max.set && c.max.n < o.max
                o.max = c.max.n
            end
        end
        for (i, entry) in enumerate(entries)
            if !isempty(entry.apps) && entry.apps[1].has_child
                o.children[i] = entry.apps[1].child
            end
        end
        flat_plan = o
    end
    has_unevaluated = n.unevaluated_properties >= 0
    unevaluated = has_unevaluated ? child_of(target(p, n.unevaluated_properties)) : NO_CHILD
    return FusedObject(flat_plan, names, entries, contributors, conditions, alternatives, ctx.alt_groups, forbidden,
        absent, finals, resolves_unknown, has_count_bounds, has_unevaluated, unevaluated)
end

# The merged constants of an entry's value tests, when some (of at most 64) are constant sets and there is more than
# one constant to look for. Nothing otherwise.
function merge_value_tests(tests::Vector{ValueTest})
    constants = 0
    for t in tests
        t.is_pattern || (constants += length(t.allowed))
    end
    (length(tests) > 64 || constants < 2) && return nothing
    strings = String[]
    string_masks = UInt64[]
    others = MaskedValue[]
    keyed = UInt64(0)
    for (index, t) in enumerate(tests)
        t.is_pattern && continue
        mask = UInt64(1) << (index - 1)
        keyed |= mask
        for v in t.allowed
            if kind(v.d, v.n) == KIND_STRING
                s = String(str(v.d, v.n))
                at_index = findfirst(==(s), strings)
                if at_index === nothing
                    push!(strings, s)
                    push!(string_masks, mask)
                else
                    string_masks[at_index] |= mask
                end
                continue
            end
            at_index = findfirst(o -> values_equal(o.value.d, o.value.n, v.d, v.n), others)
            if at_index === nothing
                push!(others, MaskedValue(v, mask))
            else
                others[at_index] = MaskedValue(others[at_index].value, others[at_index].mask | mask)
            end
        end
    end
    return MergedTests(Names(strings), string_masks, others, keyed)
end

# Merges identical resolutions of a property from several branches (the same child, or both true) into one
# application listing every branch, applied once when any of them is active. Branches of an alternative group merge
# only within the same branch. The primary contributor is an unconditional one when there is one, so the pass applies
# it at once.
function coalesce_apps(apps::Vector{CollectedApp}, contributors::Vector{FusedContributor})
    function same(a::CollectedApp, b::CollectedApp)
        (!a.child.set || !b.child.set) && return a.child.set == b.child.set
        x, y = a.child.child, b.child.child
        return a.target == b.target || (x.shape == SHAPE_TRIVIAL && y.shape == SHAPE_TRIVIAL && x.types == y.types)
    end
    used = fill(false, length(apps))
    out = FusedApp[]
    for i in eachindex(apps)
        used[i] && continue
        primary = i
        others = UInt16[]
        for j in i+1:length(apps)
            a, b = contributors[apps[primary].contributor+1], contributors[apps[j].contributor+1]
            (used[j] || !same(apps[primary], apps[j]) || a.alt != b.alt) && continue
            used[j] = true
            if a.condition.set && !b.condition.set
                push!(others, apps[primary].contributor)
                primary = j
            else
                push!(others, apps[j].contributor)
            end
        end
        chosen = apps[primary]
        push!(out, FusedApp(chosen.contributor, chosen.child.set, chosen.child.child, others, 0, 0))
    end
    return out
end

# ----------------------------------------------------------------------------------------------------------------------
# Evaluation

function reset!(s::FusedPass)
    fill!(s.seen, 0)
    fill!(s.alt_failed, 0)
    s.failed = 0
    s.holds = 0
    s.gate_ok = 0
    s.then_ = 0
    s.els = 0
    return s
end

# Reports whether one of the conditions in the masks applies what they guard: one of then_ that holds, or one of els
# that does not.
@inline applies(s::FusedPass, then_::UInt64, els::UInt64) = ((s.then_ & then_) | (s.els & els)) != 0

function all_seen(s::FusedPass, names::Vector{UInt16})
    seen = s.seen
    for i in names
        (seen[(i>>6)+1] & (UInt64(1) << (i & 63))) == 0 && return false
    end
    return true
end

function gate_active(s::FusedPass, g::Gate)
    g.set || return true
    bit = UInt64(1) << g.condition
    return (s.gate_ok & bit) != 0 && ((s.holds & bit) != 0) == g.polarity
end

@inline function fail_alt!(s::FusedPass, alt::AltBranch)
    s.alt_failed[alt.group+1] |= UInt64(1) << alt.branch
    return nothing
end

# The outcome of a property in the first step. The object fails, some branch covered the property, or conditional
# applications are pending.
const FUSED_FAILED = 0x01
const FUSED_COVER = 0x02
const FUSED_DEFER = 0x04

@inline apply_opt(e::Evaluator, c::OptChild, v::Int) = !c.set || run_child(e, c.child, v)

# Resolves a name no entry knows against one branch's pattern and additional properties. It returns whether it
# matched (the property is covered), and false when the application failed.
function resolve_unknown(e::Evaluator, c::FusedContributor, name::Bytes, ascii::Bool, v::Int)::Tuple{Bool,Bool}
    matched = false
    patterns = c.patterns
    for i in eachindex(patterns)
        fp = patterns[i]
        if pattern_match(fp.pattern, name, ascii)
            matched = true
            apply_opt(e, fp.child, v) || return false, false
        end
    end
    if !matched && c.has_additional
        matched = true
        apply_opt(e, c.additional, v) || return false, false
    end
    return matched, true
end

function fused_entry(e::Evaluator, f::FusedObject, index::Int, v::Int, pass::FusedPass)::UInt8
    d = e.d
    entry = f.entries[index+1]
    pass.seen[(index>>6)+1] |= UInt64(1) << (index & 63)
    tests = entry.tests
    if !isempty(tests)
        allowed, keyed = UInt64(0), UInt64(0)
        merged = entry.merged
        if merged !== nothing
            allowed, keyed = allowed_tests(merged, d, v), merged.keyed
        end
        for t in eachindex(tests)
            test = tests[t]
            bit = UInt64(1) << (t - 1)
            holds = (keyed & bit) != 0 ? (allowed & bit) != 0 : test_holds(test, d, v)
            if !holds
                pass.failed |= UInt64(1) << test.condition
            end
        end
    end
    outcome = 0x00
    apps = entry.apps
    contributors = f.contributors
    for i in eachindex(apps)
        app = apps[i]
        c = contributors[app.contributor+1]
        if c.condition.set
            outcome |= FUSED_DEFER
            continue
        end
        if app.has_child && !run_child(e, app.child, v)
            c.alt.set || return FUSED_FAILED
            fail_alt!(pass, c.alt)
            continue
        end
        outcome |= FUSED_COVER
    end
    return outcome
end

function fused_unknown(e::Evaluator, f::FusedObject, name::Bytes, ascii::Bool, v::Int, pass::FusedPass)::UInt8
    f.resolves_unknown || return 0x00
    for a in f.absent
        bit = UInt64(1) << a.condition
        if (pass.failed & bit) == 0 && pattern_match(a.pattern, name, ascii)
            pass.failed |= bit
        end
    end
    outcome = 0x00
    contributors = f.contributors
    for i in eachindex(contributors)
        c = contributors[i]
        if c.condition.set
            if !isempty(c.patterns) || c.has_additional
                outcome |= FUSED_DEFER
            end
            continue
        end
        matched, ok = resolve_unknown(e, c, name, ascii, v)
        if !ok && !c.alt.set
            return FUSED_FAILED
        elseif !ok
            fail_alt!(pass, c.alt)
        elseif matched
            outcome |= FUSED_COVER
        end
    end
    return outcome
end

# Takes the state of a pass from the evaluator's stack of them.
function take_pass!(e::Evaluator)
    e.pass_depth += 1
    if e.pass_depth > length(e.passes)
        push!(e.passes, FusedPass())
    end
    return reset!(e.passes[e.pass_depth])
end

function run_fused(e::Evaluator, f::FusedObject, x::Int)::Bool
    flat = f.flat
    flat === nothing || return run_strict_object(e, flat, x)
    pass = take_pass!(e)
    mark = length(e.arena)
    ok = run_fused_pass(e, f, x, pass)
    length(e.arena) == mark || resize!(e.arena, mark)
    e.pass_depth -= 1
    return ok
end

@inline count_ok(c::FusedContributor, n::UInt64) = (!c.min.set || n >= c.min.n) && (!c.max.set || n <= c.max.n)

function run_fused_pass(e::Evaluator, f::FusedObject, x::Int, pass::FusedPass)::Bool
    d = e.d
    n = count(d, x)
    contributors = f.contributors
    if f.has_count_bounds
        for i in eachindex(contributors)
            c = contributors[i]
            if !c.condition.set && !c.alt.set && !count_ok(c, UInt64(n))
                return false
            end
        end
    end
    # The covered properties (for unevaluatedProperties) and, beyond the first 64 (which a mask holds), the
    # properties with conditional applications pending, by ordinal.
    covered, deferred_beyond = NO_BITS, NO_BITS
    if f.has_unevaluated
        covered = new_bits!(e, n)
    end
    if n > 64
        deferred_beyond = new_bits!(e, n)
    end
    deferred = UInt64(0)
    pending = 0
    hint = 0
    ns = f.names
    first_child = first(d, x)
    for ordinal in 0:n-1
        k = first_child + 2 * ordinal
        name = str(d, k)
        w = name_word(name)
        index = hint
        if name_at(ns, hint, name.len, w) && (name.len <= 8 || name_rest(ns, hint, name))
            hint += 1
        else
            index, hint = find_after(ns, name, w, hint)
        end
        outcome = index >= 0 ? fused_entry(e, f, index, k + 1, pass) :
                  fused_unknown(e, f, name, str_ascii(d, k), k + 1, pass)
        (outcome & FUSED_FAILED) != 0 && return false
        if (outcome & FUSED_COVER) != 0 && tracked(covered)
            set_bit!(e, covered, ordinal)
        end
        if (outcome & FUSED_DEFER) != 0
            pending += 1
            if ordinal < 64
                deferred |= UInt64(1) << ordinal
            else
                set_bit!(e, deferred_beyond, ordinal)
            end
        end
    end

    # Decide the conditions, then which apply along their gates (a gate precedes the conditions under it).
    conditions = f.conditions
    for i in eachindex(conditions)
        bit = UInt64(1) << (i - 1)
        if (pass.failed & bit) == 0 && all_seen(pass, conditions[i].required)
            pass.holds |= bit
        end
    end
    for i in eachindex(conditions)
        if gate_active(pass, conditions[i].gate)
            pass.gate_ok |= UInt64(1) << (i - 1)
        end
    end
    pass.then_, pass.els = pass.gate_ok & pass.holds, pass.gate_ok & ~pass.holds

    ordinal = 0
    while ordinal < n && pending > 0
        if ordinal < 64
            if (deferred & (UInt64(1) << ordinal)) == 0
                ordinal += 1
                continue
            end
        elseif !get_bit(e, deferred_beyond, ordinal)
            ordinal += 1
            continue
        end
        pending -= 1
        k = first_child + 2 * ordinal
        name, v = str(d, k), k + 1
        cover = false
        index = find(ns, name)
        if index >= 0
            apps = f.entries[index+1].apps
            for i in eachindex(apps)
                # An application whose first contributor has no condition was made in the first step.
                app = apps[i]
                contributors[app.contributor+1].condition.set || continue
                if applies(pass, app.then_, app.els)
                    app.has_child && !run_child(e, app.child, v) && return false
                    cover = true
                end
            end
        else
            for i in eachindex(contributors)
                c = contributors[i]
                if c.condition.set && applies(pass, c.then_, c.els)
                    matched, ok = resolve_unknown(e, c, name, str_ascii(d, k), v)
                    ok || return false
                    cover = cover || matched
                end
            end
        end
        if cover && tracked(covered)
            set_bit!(e, covered, ordinal)
        end
        ordinal += 1
    end

    for i in f.finals
        c = contributors[i+1]
        if c.condition.set
            applies(pass, c.then_, c.els) || continue
            count_ok(c, UInt64(n)) || return false
        end
        if (c.alt.set && !count_ok(c, UInt64(n))) || !all_seen(pass, c.required)
            c.alt.set || return false
            fail_alt!(pass, c.alt)
        end
    end

    groups = f.alt_groups
    for g in eachindex(groups)
        group = groups[g]
        all_branches = group.count < 64 ? (UInt64(1) << group.count) - 1 : typemax(UInt64)
        survivors = ~pass.alt_failed[g] & all_branches
        (survivors == 0 || (group.exactly_one && count_ones(survivors) != 1)) && return false
    end

    for forbidden in f.forbidden
        gate_active(pass, forbidden.gate) && all_seen(pass, forbidden.names) && return false
    end

    for a in f.alternatives
        gate_active(pass, a.condition) || continue
        found = 0
        for branch in a.branches
            found += all_seen(pass, branch)
        end
        (found == 0 || (a.exactly_one && found != 1)) && return false
    end

    if f.has_unevaluated
        for ordinal in 0:n-1
            if !get_bit(e, covered, ordinal) && !run_child(e, f.unevaluated, first_child + 2 * ordinal + 1)
                return false
            end
        end
    end
    return true
end
