# The data of the fail-fast evaluation plans (see plan.jl and fused.jl), the compiled program and the evaluator's
# state. Ported from the type declarations of plan.go, fused.go and eval.go.

# Every JSON type (a node without type).
const ANY_TYPE = 0x7f

# The shape of a node's plan, as its callers enter it.
# Anything else, or on an in-place cycle (entered through the guarded path).
const SHAPE_GENERAL = 0x00
# Nothing to check beyond the types.
const SHAPE_TRIVIAL = 0x01
# Checks only its own value (const, enum, number and string keywords): no children, no calls.
const SHAPE_LEAF = 0x02
# Only an enum of strings.
const SHAPE_STRING_ENUM = 0x03
# Only string keywords (length, pattern, format).
const SHAPE_STRINGS = 0x04
# Nothing but an object plan: an object value enters its loop directly.
const SHAPE_OBJECT = 0x05
# Nothing but an array plan: an array value enters its loop directly.
const SHAPE_ARRAY = 0x06
# Nothing but in-place applicators: straight to them.
const SHAPE_APPLY = 0x07
# A fused object plan (see fused.jl): an object value goes to it directly, and a value of any other kind to the
# node's keywords.
const SHAPE_FUSED = 0x08

# A child application, with its type check hoisted so that a child that is only a type check needs no call.
struct Child
    id::NodeId
    # The types the child accepts (0 for false).
    types::UInt8
    # What the child's keywords come to, so that entering it skips the dispatch it does not need.
    shape::UInt8
    # For a child that is only a type test (SHAPE_TRIVIAL), the types again, and 0 for any other child: a value
    # whose kind is in pass needs nothing more, which is one test where the child is applied.
    pass::UInt8
end

# A child's types and shape set together, with what follows from them.
with_shape(c::Child, types::UInt8, shape::UInt8) = Child(c.id, types, shape, shape == SHAPE_TRIVIAL ? types : 0x00)

Child(id::NodeId) = Child(id, ANY_TYPE, SHAPE_GENERAL, 0x00)

# A child that accepts anything and is never entered (an undeclared name without additionalProperties).
const NO_CHILD = Child(NO_NODE, ANY_TYPE, SHAPE_TRIVIAL, ANY_TYPE)

const NUMBER_MINIMUM = 0x00
const NUMBER_MAXIMUM = 0x01
const NUMBER_EXCLUSIVE_MINIMUM = 0x02
const NUMBER_EXCLUSIVE_MAXIMUM = 0x03
const NUMBER_MULTIPLE_OF = 0x04
const NUMBER_FORMAT = 0x05

# A format assertion: a custom validator of the program (by its one-based index, 0 for none), or a built-in format.
struct FormatCheck
    custom::Int32
    kind::UInt8
    legacy::Bool
end

FormatCheck() = FormatCheck(Int32(0), FORMAT_UNKNOWN, false)

const NO_DIVISOR = Divisor(false, 0, -1, 0, Rational{BigInt}(0))

struct NumberOp
    kind::UInt8
    # The bound's representation.
    flag::UInt8
    data::UInt64
    divisor::Divisor
    format::FormatCheck
end

const STRING_LENGTH = 0x00
const STRING_PATTERN = 0x01
const STRING_FORMAT = 0x02
const STRING_CONTENT = 0x03

const NO_PATTERN = Pattern(MATCH_EVERYTHING)

struct StringOp
    kind::UInt8
    min::UInt64
    max::UInt64
    pattern::Pattern
    format::FormatCheck
    content::UInt8
end

const OP_CONST = 0x00
# An enum of strings only.
const OP_ENUM_STRINGS = 0x01
const OP_ENUM = 0x02
const OP_REF = 0x03
const OP_ALL_OF = 0x04
const OP_ANY_OF = 0x05
const OP_ONE_OF = 0x06
const OP_NOT = 0x07
# A $dynamicRef/$recursiveRef resolved against the dynamic scope at run time.
const OP_DYNAMIC_REF = 0x08
const OP_IF = 0x09

const EMPTY_NAMES = Names(String[])
const EMPTY_U32 = UInt32[]

# A discriminator's known values by lookup: string values through a name table, the few others (numbers, booleans,
# null) by scan.
struct DiscriminatorIndex
    strings::Names
    # For each string in strings, its entry (zero-based) in the discriminator's known values.
    string_entries::Vector{UInt32}
    others::Vector{UInt32}
end

# anyOf/oneOf branches, dispatched by the instance's type: only the branches whose type admits it are tried, and a
# branch that is only a type check is decided without a call.
struct Branches
    children::Vector{Child}
    # For each instance kind (by the position of its bit), the branches that can accept it.
    by_kind::Vector{Vector{UInt32}}
    # The discriminator, and its known values by lookup.
    discriminator::Union{Nothing,Discriminator}
    index::Union{Nothing,DiscriminatorIndex}
end

const NO_DYNAMIC = DynamicRefTarget(false, NO_NODE, ResourceNode[])
const NO_BRANCHES = Branches(Child[], [UInt32[] for _ in 1:6], nothing, nothing)

struct Op
    kind::UInt8
    value::ValueRef
    names::Names
    values::Vector{ValueRef}
    node::NodeId
    children::Vector{Child}
    branches::Branches
    dynamic::DynamicRefTarget
    then_::NodeId
    else_::NodeId
end

function Op(kind::UInt8; value::ValueRef=NO_VALUE, names::Names=EMPTY_NAMES, values::Vector{ValueRef}=ValueRef[],
    node::NodeId=NO_NODE, children::Vector{Child}=Child[], branches::Branches=NO_BRANCHES,
    dynamic::DynamicRefTarget=NO_DYNAMIC, then_::NodeId=NO_NODE, else_::NodeId=NO_NODE)
    return Op(kind, value, names, values, node, children, branches, dynamic, then_, else_)
end

struct PatternChild
    pattern::Pattern
    child::Child
end

struct PlanDependency
    name::String
    required::Vector{String}
    schema::NodeId
    # The seen bits of the name and of the names it requires, when every one is a known name.
    has_bits::Bool
    name_bit::UInt64
    required_bits::UInt64
end

# Plans with at most this many names may look them up in the instance instead of visiting its properties...
const LOOKUP_NAMES = 4
# ...when the instance's properties times the names are at most this (each lookup scans the properties).
const LOOKUP_BUDGET = 24

# The property loop, specialised by which keywords apply.
# No keyword looks at the properties.
const VISIT_NONE = 0x00
# Only additionalProperties (a map): every value against one child.
const VISIT_VALUES = 0x01
# Declared properties, and additionalProperties for the rest.
const VISIT_NAMES = 0x02
# One patternProperties entry and nothing declared: each name against the pattern, else additionalProperties.
const VISIT_PATTERN = 0x03
# Anything else (patternProperties, propertyNames).
const VISIT_GENERAL = 0x04

mutable struct ObjectPlan
    min::UInt64
    max::UInt64
    # How the properties are visited (for properties, patternProperties, additionalProperties, propertyNames).
    visit::UInt8
    # The declared names, then the names only required and dependencies mention (so that the pass sees them too):
    # the first declared have a schema.
    names::Names
    declared::Int
    # Per name: the declared schema, or for the others the additionalProperties child (or nothing, NO_CHILD).
    children::Vector{Child}
    # Bit i set: name i is required (the names checked by the mask: at most 64 names in all).
    required_mask::UInt64
    # Required names checked by lookup (when the mask cannot cover them).
    required::Vector{String}
    patterns::Vector{PatternChild}
    # For each declared name, the patterns (zero-based indexes into patterns) it matches, worked out at compile
    # time: only undeclared names are tested against the patterns at run time.
    name_patterns::Vector{Vector{UInt16}}
    has_additional::Bool
    additional::Child
    property_names::NodeId
    dependencies::Vector{PlanDependency}
    # No required names by lookup and no dependencies: nothing after the property loop but the required mask.
    rest_free::Bool
    # VISIT_NAMES and rest_free: the strict loop decides it.
    strict::Bool
    # VISIT_NAMES with at most LOOKUP_NAMES names and no additionalProperties: undeclared properties need no visit,
    # so a small object is decided by looking each name up in it (see visit_lookup).
    lookup::Bool
end

struct SimpleArray
    set::Bool
    min::UInt64
    max::UInt64
    types::UInt8
end

mutable struct ArrayPlan
    min::UInt64
    max::UInt64
    prefix::Vector{Child}
    has_items::Bool
    items::Child
    # contains: the node, and the bounds on the count.
    contains::NodeId
    min_contains::UInt64
    max_contains::OptCount
    unique::Bool
    # Bounds and type-only items, nothing else: the items' type mask (ANY_TYPE: no test).
    is_simple::Bool
    simple::UInt8
    # Items that are themselves simple arrays (GeoJSON's positions): their bounds and item types, checked inline.
    has_nested::Bool
    nested::SimpleArray
end

# ----------------------------------------------------------------------------------------------------------------------
# The fused object plan (see fused.jl)

const MAX_FUSED_NAMES = 256
const MAX_FUSED_CONDITIONS = 64
const MAX_FUSED_CONTRIBUTORS = 64
const MAX_FUSED_ALT_GROUPS = 8

# A condition and the polarity under which something applies. Without set, it always applies.
struct Gate
    set::Bool
    condition::UInt16
    polarity::Bool
end

Gate() = Gate(false, 0, false)

struct FusedForbidden
    gate::Gate
    names::Vector{UInt16}
end

struct FusedAbsent
    condition::UInt16
    pattern::Pattern
end

struct MaskedValue
    value::ValueRef
    mask::UInt64
end

# Each constant any of an entry's constant tests allows, with the mask of the tests (by index in tests) that allow
# it. Strings are looked up by name. Other constants are compared in turn.
struct MergedTests
    strings::Names
    string_masks::Vector{UInt64}
    others::Vector{MaskedValue}
    # The tests that are constant sets. The others (patterns) are tested one by one.
    keyed::UInt64
end

# One child schema applying to a property on behalf of a contributor.
struct FusedApp
    # Zero-based, as every index of a fused plan.
    contributor::UInt16
    # Not set: true, which covers without a test.
    has_child::Bool
    child::Child
    # Other contributors whose resolution of the property is the same test: applied once when any is active.
    others::Vector{UInt16}
    # For an application under conditions: the conditions that apply it when they hold, and when they do not, over
    # its contributors (see applies).
    then_::UInt64
    els::UInt64
end

struct AltBranch
    set::Bool
    group::UInt16
    branch::UInt16
end

AltBranch() = AltBranch(false, 0, 0)

# A child schema, or true (not set), which covers without a test.
struct OptChild
    set::Bool
    child::Child
end

OptChild() = OptChild(false, NO_CHILD)

struct FusedPattern
    pattern::Pattern
    child::OptChild
end

struct FusedContributor
    # The condition and the polarity under which it applies.
    condition::Gate
    # The alternative group and the branch in it.
    alt::AltBranch
    patterns::Vector{FusedPattern}
    # additionalProperties.
    has_additional::Bool
    additional::OptChild
    required::Vector{UInt16}
    min::OptCount
    max::OptCount
    # The condition as a mask: the bit of the condition in then_ when it applies the contributor by holding, in els
    # when by not holding.
    then_::UInt64
    els::UInt64
end

# An if the pass decides (required names and value tests), or the presence of a dependency's property.
struct FusedCondition
    required::Vector{UInt16}
    # The enclosing condition and the polarity under which this one is reached.
    gate::Gate
end

# A condition's test on a property's value: absent passes, present must hold.
struct ValueTest
    condition::UInt16
    # One of these constants (strings, integers, booleans, null). None: the property must be absent.
    is_pattern::Bool
    allowed::Vector{ValueRef}
    pattern::Pattern
    requires_string::Bool
end

struct FusedEntry
    apps::Vector{FusedApp}
    # The conditions' tests on this property's value.
    tests::Vector{ValueTest}
    # The constant tests merged: one lookup decides them all.
    merged::Union{Nothing,MergedTests}
end

struct FusedAlternative
    condition::Gate
    exactly_one::Bool
    branches::Vector{Vector{UInt16}}
end

struct FusedAltGroup
    exactly_one::Bool
    count::UInt32
end

mutable struct FusedObject
    # The branches merge into one strict object loop: every one applies unconditionally with declared properties and
    # required names only (and count bounds), and each name resolves to one schema.
    flat::Union{Nothing,ObjectPlan}
    # Every property name any branch or condition knows, to its entry.
    names::Names
    entries::Vector{FusedEntry}
    # The branches, the node itself first, then the required lists of dependencies.
    contributors::Vector{FusedContributor}
    conditions::Vector{FusedCondition}
    # Required-only oneOf/anyOf keywords, decided from the seen names after the pass.
    alternatives::Vector{FusedAlternative}
    # oneOf/anyOf groups whose branches carry object keywords (each branch is a contributor).
    alt_groups::Vector{FusedAltGroup}
    # not: {required: [...]}: names that must not all be present, under a condition.
    forbidden::Vector{FusedForbidden}
    # Conditions that fail when some property name matches a pattern (an if with patternProperties: {P: false}),
    # for names no entry knows. A known name that matches carries a test that never holds.
    absent::Vector{FusedAbsent}
    # The contributors with something to check after the pass (required names or count bounds).
    finals::Vector{UInt16}
    # Some branch has pattern properties or additional properties, or some condition absent patterns, so names no
    # entry knows need resolving.
    resolves_unknown::Bool
    has_count_bounds::Bool
    has_unevaluated::Bool
    unevaluated::Child
end

# ----------------------------------------------------------------------------------------------------------------------
# Plans

# A node's keywords, grouped so that only the ones for the instance's type are looked at.
mutable struct Body
    # For an object instance, the whole node in one pass (see fused.jl), in place of everything below.
    fused::Union{Nothing,FusedObject}
    # Instance kinds (TYPE_OBJECT, TYPE_ARRAY) whose evaluated properties or items the general evaluator must track
    # (unevaluatedProperties, unevaluatedItems), and the node it runs.
    general::UInt8
    node::NodeId
    # unevaluatedItems with a static coverage: the items from this index on, against the child.
    has_unevaluated_items::Bool
    unevaluated_from::Int
    unevaluated_child::Child
    # const and enum.
    values::Vector{Op}
    number::Vector{NumberOp}
    str::Vector{StringOp}
    object::Union{Nothing,ObjectPlan}
    array::Union{Nothing,ArrayPlan}
    # In-place applicators (children resolved through pure-$ref hops), in the general evaluator's order.
    apply::Vector{Op}
end

Body(node::NodeId) = Body(nothing, 0x00, node, false, 0, NO_CHILD, Op[], NumberOp[], StringOp[], nothing, nothing,
    Op[])

mutable struct Plan
    types::UInt8
    # Evaluated in place under the depth guard (part of an in-place cycle).
    guard::Bool
    # Everything beyond the type check (nothing for true, false and type-only schemas).
    body::Union{Nothing,Body}
    # The body's shape, for entering it directly.
    shape::UInt8
    # The node as a child, for entering it by its id (see run).
    self::Child
end

# The compiled program an evaluator runs.
mutable struct Program
    nodes::Vector{SchemaNode}
    root::NodeId
    # The entry for fail-fast evaluation: the root's target.
    entry::NodeId
    uses_dynamic_scope::Bool
    max_depth::Int
    # Custom format assertions: the names, and the validators (callables given by the user) at the same indexes.
    format_names::Vector{String}
    format_validators::Vector{Any}
    # For fail-fast evaluation: each node's target after following pure $ref hops.
    fast_target::Vector{NodeId}
    annotation_sources::Vector{AnnotationSource}
    annotations_lock::ReentrantLock
    annotations::Union{Nothing,Vector{Vector{AnnotationEntry}}}
    assert_format_set::Bool
    # Fail-fast plans.
    plans::Vector{Plan}
end

@inline node(p::Program, id::NodeId) = p.nodes[id+1]
@inline plan(p::Program, id::NodeId) = p.plans[id+1]
@inline target(p::Program, id::NodeId) = p.fast_target[id+1]

# The state of one fused pass. Passes nest with the objects of the instance, so an evaluator keeps a stack of them.
mutable struct FusedPass
    # The entries seen.
    seen::Vector{UInt64}
    # Bit i: condition i's value test failed.
    failed::UInt64
    alt_failed::Vector{UInt64}
    holds::UInt64
    gate_ok::UInt64
    # Once the conditions are decided: those that apply and hold, and those that apply and do not.
    then_::UInt64
    els::UInt64
end

FusedPass() = FusedPass(zeros(UInt64, MAX_FUSED_NAMES >> 6), 0, zeros(UInt64, MAX_FUSED_ALT_GROUPS), 0, 0, 0, 0)

# The state of one evaluation, with the buffers it may need. An evaluator is taken from its validator for one
# evaluation at a time and reused, so an evaluation allocates nothing once the buffers have grown.
mutable struct Evaluator
    p::Program
    # The instance document.
    d::Document
    # The collector, or nothing to fail fast.
    c::Union{Nothing,ResultsCollector}
    annotations::Vector{Vector{AnnotationEntry}}
    depth::Int
    # Evaluation recursed in place beyond the maximum depth.
    depth_exceeded::Bool
    # The dynamic scope: the resources entered, outermost first.
    scope::Vector{UInt32}
    # The evaluated-property and evaluated-item sets in use.
    arena::Vector{UInt64}
    # Scratch for uniqueItems.
    unique::Vector{UInt64}
    # For validating JSON text: the document it is parsed into, the parser, and the bytes of a string.
    text::Document
    parser::Parser
    source::Vector{UInt8}
    # For content assertions: the decoded bytes, and the parser that checks them.
    content::Vector{UInt8}
    content_parser::Parser
    # The fused passes in progress, and those kept for reuse.
    passes::Vector{FusedPass}
    pass_depth::Int
end

const NO_ANNOTATIONS = Vector{AnnotationEntry}[]

Evaluator(p::Program) = Evaluator(p, EMPTY_DOCUMENT, nothing, NO_ANNOTATIONS, 0, false, UInt32[], UInt64[],
    UInt64[], Document(), Parser(), UInt8[], UInt8[], Parser(), FusedPass[], 0)
