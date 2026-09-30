//! Fail-fast evaluation plans: each node compiled to only the keywords it has, as a flat list of operations, with the
//! object keywords fused into one pass over the instance's properties (declared names resolved through a lookup table,
//! `required` checked as a bit mask of the declared properties seen) and children that are nothing but a type check
//! tested inline. The idea of the C# evaluator's fused plans (`FusedObjectPlan`, leaf and type-dispatch plans), in the
//! shape a Rust interpreter wants.
//!
//! Plans only drive fail-fast evaluation without a dynamic scope. A node that tracks evaluated properties or items
//! (`unevaluatedProperties`/`unevaluatedItems`) runs through the general evaluator, whose children come back to their
//! plans.

use std::sync::Arc;

use serde_json::{Map, Number, Value};

mod fused;

use super::{Evaluator, Fast, Program, all_unique, code_points, content_ok, json_equal};
use crate::dialect::Dialect;
use crate::formats::FormatKind;
use crate::node::*;
use crate::numbers::{Divisor, Num, cmp};
use crate::options::FormatValidator;
use crate::pattern::Pattern;

/// Every JSON type (a node without `type`).
const ANY: u8 = 0x7f;

/// A child that accepts anything and is never entered (an undeclared name without additionalProperties).
const NO_CHILD: Child = Child { id: u32::MAX, types: ANY, shape: Shape::Trivial };

/// A child application, with its type check hoisted so that a child that is only a type check needs no call.
#[derive(Clone, Copy)]
pub(crate) struct Child {
    id: NodeId,
    /// The types the child accepts (0 for `false`).
    types: u8,
    /// What the child's keywords come to, so that entering it skips the dispatch it does not need.
    shape: Shape,
}

/// The shape of a node's plan, as its callers enter it.
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
enum Shape {
    /// Nothing to check beyond the types.
    Trivial,
    /// Only its own value (const, enum, number and string keywords): no children, no calls.
    Leaf,
    /// Only an enum of strings (the C# StrictEntry's InlineEnum).
    StringEnum,
    /// Only string keywords (length, pattern, format).
    Strings,
    /// Nothing but an object plan: an object value enters its loop directly (the C# NestedObject).
    Object,
    /// Nothing but an array plan: an array value enters its loop directly.
    Array,
    /// Nothing but in-place applicators (the C# TypeDispatch/Forward entries): straight to them.
    Apply,
    /// Anything else, or on an in-place cycle (entered through the guarded path).
    General,
}

impl Child {
    #[inline(always)]
    fn trivial(&self) -> bool {
        self.shape == Shape::Trivial
    }
}

pub(crate) struct Plan {
    types: u8,
    /// Evaluated in place under the depth guard (part of an in-place cycle).
    guard: bool,
    /// Everything beyond the type check (none for `true`, `false` and type-only schemas).
    body: Option<Box<Body>>,
}

/// A node's keywords, grouped so that only the ones for the instance's type are looked at.
#[derive(Default)]
struct Body {
    /// For an object instance, the whole node in one pass (see `fused`), in place of everything below.
    fused: Option<Box<fused::FusedObject>>,
    /// Instance kinds (`type_mask::OBJECT`, `type_mask::ARRAY`) whose evaluated properties/items the general
    /// evaluator must track (unevaluatedProperties/unevaluatedItems), and the node it runs.
    general: u8,
    node: NodeId,
    /// unevaluatedItems with a static coverage: the items from this index on, against the child.
    unevaluated_items: Option<(usize, Child)>,
    /// const and enum.
    values: Box<[Op]>,
    number: Box<[NumberOp]>,
    string: Box<[StringOp]>,
    object: Option<ObjectPlan>,
    array: Option<ArrayPlan>,
    /// In-place applicators (children resolved through pure-$ref hops), in the general evaluator's order.
    apply: Box<[Op]>,
}

enum NumberOp {
    Minimum(Num),
    Maximum(Num),
    ExclusiveMinimum(Num),
    ExclusiveMaximum(Num),
    MultipleOf(Divisor),
    Format(FormatCheck),
}

enum StringOp {
    Length { min: u64, max: u64 },
    Pattern(Arc<Pattern>),
    Format(FormatCheck),
    Content(ContentKind),
}

enum Op {
    Const(Value),
    /// An enum of strings only.
    EnumStrings(Names),
    Enum(Box<[Value]>),
    Ref(NodeId),
    AllOf(Box<[Child]>),
    AnyOf(Box<Branches>),
    OneOf(Box<Branches>),
    Not(NodeId),
    /// A `$dynamicRef`/`$recursiveRef` resolved against the dynamic scope at run time (the C# DynamicRef plan).
    DynamicRef(Box<DynamicRefTarget>),
    If {
        cond: NodeId,
        then: Option<NodeId>,
        else_: Option<NodeId>,
    },
}

enum FormatCheck {
    Custom(FormatValidator),
    Builtin(FormatKind, bool),
}

impl FormatCheck {
    fn string(&self, s: &str) -> bool {
        match self {
            FormatCheck::Custom(f) => f(s),
            FormatCheck::Builtin(kind, legacy) => kind.check_string(s, *legacy),
        }
    }

    fn number(&self, n: &Number) -> bool {
        match self {
            FormatCheck::Custom(f) => f(&n.to_string()),
            FormatCheck::Builtin(kind, _) => kind.check_number(n),
        }
    }
}

struct ObjectPlan {
    min: u64,
    max: u64,
    /// How the properties are visited (for properties, patternProperties, additionalProperties, propertyNames).
    visit: Visit,
    /// The declared names, then the names only `required` and dependencies mention (so that the pass sees them too;
    /// C# registers required names as property entries): the first `declared` have a schema.
    names: Names,
    declared: usize,
    /// Per name: the declared schema, or for the others the additionalProperties child (or nothing, `NO_CHILD`).
    children: Box<[Child]>,
    /// Bit `i` set: name `i` is required (the names checked by the mask: at most 64 names in all).
    required_mask: u64,
    /// Required names checked by lookup (when the mask cannot cover them).
    required: Box<[Box<str>]>,
    patterns: Box<[(Arc<Pattern>, Child)]>,
    /// For each declared name, the patterns (indexes into `patterns`) it matches, worked out at compile time: only
    /// undeclared names are tested against the patterns at run time.
    name_patterns: Box<[Box<[u16]>]>,
    additional: Option<Child>,
    property_names: Option<NodeId>,
    dependencies: Box<[Dependency]>,
    /// No required names by lookup and no dependencies: nothing after the property loop but the required mask.
    rest_free: bool,
    /// `Visit::Names` and `rest_free`: the strict loop (`run_strict_object`) decides it.
    strict: bool,
}

/// The property loop, specialised by which keywords apply.
#[derive(Clone, Copy, PartialEq, Eq)]
enum Visit {
    /// No keyword looks at the properties.
    None,
    /// Only additionalProperties (a map): every value against one child.
    Values,
    /// Declared properties, and additionalProperties for the rest.
    Names,
    /// One patternProperties entry and nothing declared: each name against the pattern, else additionalProperties
    /// (the C# PatternMap).
    Pattern,
    /// Anything else (patternProperties, propertyNames).
    General,
}

/// anyOf/oneOf branches, dispatched by the instance's type: only the branches whose `type` admits it are tried (the
/// C# evaluator's type-dispatch plans), and a branch that is only a type check is decided without a call.
struct Branches {
    children: Box<[Child]>,
    /// For each instance kind (see `kind`), the branches that can accept it.
    by_kind: [Box<[u32]>; 6],
    discriminator: Option<Box<(Discriminator, DiscriminatorIndex)>>,
}

/// A discriminator's known values by lookup (C# keys them in a hashed map): string values through a name table,
/// the few others (numbers, booleans, null) by scan.
struct DiscriminatorIndex {
    strings: Names,
    /// For each string in `strings`, its entry in `Discriminator::known`.
    string_entries: Box<[u32]>,
    others: Box<[u32]>,
}

impl DiscriminatorIndex {
    fn new(d: &Discriminator) -> DiscriminatorIndex {
        let mut names: Vec<Box<str>> = Vec::new();
        let mut string_entries = Vec::new();
        let mut others = Vec::new();
        for (i, (value, _)) in d.known.iter().enumerate() {
            match value {
                DiscriminatorValue::String(s) => {
                    names.push(s.as_str().into());
                    string_entries.push(i as u32);
                }
                _ => others.push(i as u32),
            }
        }
        DiscriminatorIndex {
            strings: Names::new(names),
            string_entries: string_entries.into_boxed_slice(),
            others: others.into_boxed_slice(),
        }
    }

    /// The branches a discriminator value selects.
    #[inline]
    fn select<'d>(&self, d: &'d Discriminator, v: &Value) -> &'d [u32] {
        let entry = match v {
            Value::String(s) => self.strings.contains_at(s).map(|i| self.string_entries[i]),
            _ => self.others.iter().copied().find(|&i| d.known[i as usize].0.matches(v)),
        };
        entry.map_or(&d.unknown, |i| &d.known[i as usize].1)
    }
}

impl Branches {
    fn new(children: Box<[Child]>, discriminator: Option<Box<Discriminator>>) -> Branches {
        let discriminator = discriminator.map(|d| {
            let index = DiscriminatorIndex::new(&d);
            Box::new((*d, index))
        });
        Branches { children, by_kind: Default::default(), discriminator }
    }

    /// Recomputes the dispatch table once the children's types are known.
    fn dispatch(&mut self) {
        const KIND_TYPES: [u8; 6] = [
            type_mask::NULL,
            type_mask::BOOLEAN,
            type_mask::NUMBER | type_mask::INTEGER,
            type_mask::STRING,
            type_mask::ARRAY,
            type_mask::OBJECT,
        ];
        for (k, types) in KIND_TYPES.iter().enumerate() {
            self.by_kind[k] =
                (0..self.children.len() as u32).filter(|&i| self.children[i as usize].types & types != 0).collect();
        }
    }
}

/// The index of an instance's kind in `Branches::by_kind`.
#[inline(always)]
fn kind(x: &Value) -> usize {
    match x {
        Value::Null => 0,
        Value::Bool(_) => 1,
        Value::Number(_) => 2,
        Value::String(_) => 3,
        Value::Array(_) => 4,
        Value::Object(_) => 5,
    }
}

struct Dependency {
    name: Box<str>,
    required: Box<[Box<str>]>,
    schema: Option<NodeId>,
    /// The seen bits of the name and of the names it requires, when every one is a known name.
    bits: Option<(u64, u64)>,
}

struct ArrayPlan {
    min: u64,
    max: u64,
    prefix: Box<[Child]>,
    items: Option<Child>,
    contains: Option<(NodeId, u64, Option<u64>)>,
    unique: bool,
    /// Bounds and type-only items, nothing else (the C# SimpleArray): the items' type mask (`ANY`: no test).
    simple: Option<u8>,
}

// ---------------------------------------------------------------------------------------------------------------------
// Property name lookup

const LINEAR_NAMES: usize = 8;

/// A cheap hash of a property name: its length and its first and last (up to) eight bytes.
#[inline(always)]
fn name_hash(s: &str) -> u64 {
    let b = s.as_bytes();
    let n = b.len();
    let (head, tail) = if n >= 8 {
        (u64::from_le_bytes(b[..8].try_into().unwrap()), u64::from_le_bytes(b[n - 8..].try_into().unwrap()))
    } else if n >= 4 {
        let h = u32::from_le_bytes(b[..4].try_into().unwrap()) as u64;
        let t = u32::from_le_bytes(b[n - 4..].try_into().unwrap()) as u64;
        (h, t)
    } else if n > 0 {
        ((b[0] as u64) | (b[n / 2] as u64) << 8 | (b[n - 1] as u64) << 16, 0)
    } else {
        (0, 0)
    };
    (head ^ tail.rotate_left(23) ^ (n as u64).wrapping_mul(0x9e37_79b9_7f4a_7c15)).wrapping_mul(0xff51_afd7_ed55_8ccd)
}

/// An open-addressing table of property names (the declared names of one schema).
struct NameTable {
    names: Box<[Box<str>]>,
    /// Index + 1 into `names` (0: empty), at `hash >> shift`, probing linearly.
    slots: Box<[u32]>,
    shift: u32,
}

impl NameTable {
    fn new(names: Vec<Box<str>>) -> NameTable {
        let size = (names.len() * 2).next_power_of_two().max(16);
        let shift = 64 - size.trailing_zeros();
        let mut slots = vec![0u32; size];
        for (i, name) in names.iter().enumerate() {
            let mut at = (name_hash(name) >> shift) as usize;
            while slots[at] != 0 {
                at = (at + 1) & (size - 1);
            }
            slots[at] = i as u32 + 1;
        }
        NameTable { names: names.into_boxed_slice(), slots: slots.into_boxed_slice(), shift }
    }

    #[inline]
    fn find(&self, name: &str) -> Option<usize> {
        let mask = self.slots.len() - 1;
        let mut at = (name_hash(name) >> self.shift) as usize;
        loop {
            let slot = self.slots[at];
            if slot == 0 {
                return None;
            }
            let i = slot as usize - 1;
            if str_eq(&self.names[i], name) {
                return Some(i);
            }
            at = (at + 1) & mask;
        }
    }
}

/// Declared property names to their index.
struct Names {
    /// Bit `n` set: some name has length `n` (lengths of 63 and more share bit 63). A name whose length is not in the
    /// set is not declared, which settles most misses without a search.
    lengths: u64,
    lookup: Lookup,
}

enum Lookup {
    Linear(Box<[Box<str>]>),
    Hashed(NameTable),
}

#[inline(always)]
fn length_bit(len: usize) -> u64 {
    1 << len.min(63)
}

impl Names {
    fn new(names: Vec<Box<str>>) -> Names {
        let lengths = names.iter().fold(0, |m, n| m | length_bit(n.len()));
        let lookup = if names.len() <= LINEAR_NAMES {
            Lookup::Linear(names.into_boxed_slice())
        } else {
            Lookup::Hashed(NameTable::new(names))
        };
        Names { lengths, lookup }
    }

    fn find(&self, name: &str) -> Option<usize> {
        self.find_from(name, &mut 0)
    }

    /// The index of a name, without the ordering hint.
    #[inline]
    fn contains_at(&self, name: &str) -> Option<usize> {
        if self.lengths & length_bit(name.len()) == 0 {
            return None;
        }
        match &self.lookup {
            Lookup::Linear(names) => names.iter().position(|n| str_eq(n, name)),
            Lookup::Hashed(table) => table.find(name),
        }
    }

    /// Membership only (for string enums: no order to hint at).
    #[inline]
    fn contains(&self, name: &str) -> bool {
        if self.lengths & length_bit(name.len()) == 0 {
            return false;
        }
        match &self.lookup {
            Lookup::Linear(names) => names.iter().any(|n| str_eq(n, name)),
            Lookup::Hashed(table) => table.find(name).is_some(),
        }
    }

    /// Finds a name, trying the one after the previous match first: instances tend to list their properties in the
    /// schema's order, so the next name is usually the next one declared.
    #[inline(always)]
    fn find_from(&self, name: &str, hint: &mut usize) -> Option<usize> {
        if self.lengths & length_bit(name.len()) == 0 {
            return None;
        }
        match &self.lookup {
            Lookup::Linear(names) => {
                if let Some(expected) = names.get(*hint)
                    && str_eq(expected, name)
                {
                    *hint += 1;
                    return Some(*hint - 1);
                }
                let i = names.iter().position(|n| str_eq(n, name))?;
                *hint = i + 1;
                Some(i)
            }
            Lookup::Hashed(table) => {
                if let Some(expected) = table.names.get(*hint)
                    && str_eq(expected, name)
                {
                    *hint += 1;
                    return Some(*hint - 1);
                }
                let i = table.find(name)?;
                *hint = i + 1;
                Some(i)
            }
        }
    }
}

/// String equality with the length test and short comparisons inline (property names are short).
#[inline(always)]
fn str_eq(a: &str, b: &str) -> bool {
    let (a, b) = (a.as_bytes(), b.as_bytes());
    if a.len() != b.len() {
        return false;
    }
    let n = a.len();
    if n <= 8 {
        if n >= 4 {
            // Two overlapping four-byte words cover every byte (one load each, no per-byte bounds checks).
            let word = |s: &[u8], i: usize| u32::from_le_bytes(s[i..i + 4].try_into().unwrap());
            return word(a, 0) == word(b, 0) && word(a, n - 4) == word(b, n - 4);
        }
        return a == b;
    }
    // Eight-byte words from the start, then one ending at the last byte (overlapping the previous one): property names
    // are short enough that a call to memcmp costs more than the compare.
    let word = |s: &[u8], i: usize| u64::from_le_bytes(s[i..i + 8].try_into().unwrap());
    let mut i = 0;
    while i + 8 < n {
        if word(a, i) != word(b, i) {
            return false;
        }
        i += 8;
    }
    word(a, n - 8) == word(b, n - 8)
}

/// Objects up to this size are searched by scanning their keys: comparing lengths first, that is cheaper than hashing
/// the name with serde_json's SipHash.
const LINEAR_KEYS: usize = 32;

/// A property of an object.
#[inline]
pub(super) fn get_key<'a>(o: &'a Map<String, Value>, name: &str) -> Option<&'a Value> {
    if o.len() <= LINEAR_KEYS { o.iter().find(|(k, _)| str_eq(k, name)).map(|(_, v)| v) } else { o.get(name) }
}

/// Whether an object has a property.
#[inline]
fn has_key(o: &Map<String, Value>, name: &str) -> bool {
    if o.len() <= LINEAR_KEYS { o.keys().any(|k| str_eq(k, name)) } else { o.contains_key(name) }
}

// ---------------------------------------------------------------------------------------------------------------------
// Compilation

#[inline(always)]
fn type_ok(mask: u8, x: &Value) -> bool {
    let bit = match x {
        Value::Null => type_mask::NULL,
        Value::Bool(_) => type_mask::BOOLEAN,
        Value::Number(_) => type_mask::NUMBER,
        Value::String(_) => type_mask::STRING,
        Value::Array(_) => type_mask::ARRAY,
        Value::Object(_) => type_mask::OBJECT,
    };
    if mask & bit != 0 {
        return true;
    }
    // A number that is not accepted as a number may still be an integer.
    match x {
        Value::Number(n) => mask & type_mask::INTEGER != 0 && super::is_integer(n),
        _ => false,
    }
}

pub(crate) fn compile_plans(p: &Program) -> Vec<Plan> {
    let nodes = &p.nodes;
    let target = |id: NodeId| p.fast_target[id as usize];
    let mut plans: Vec<Plan> = nodes.iter().enumerate().map(|(id, n)| plan_node(p, id as NodeId, n, &target)).collect();
    // Hoist the children's type checks now that every plan is known.
    let summary: Vec<(u8, Shape)> = plans.iter().map(|pl| (pl.types, shape_of(pl, p.uses_dynamic_scope))).collect();
    let resolved = |id: NodeId| {
        let (types, shape) = summary[id as usize];
        // Not `Object`: a node may still take a fused plan below.
        Child { id, types, shape: if shape == Shape::Object { Shape::General } else { shape } }
    };
    // Fused object plans, for nodes whose object semantics span in-place applicators.
    // A fused plan applies its contributors' keywords without entering them as nodes, so the dynamic scope below it
    // would differ from the general path's: nodes that can reach a live dynamic reference are not fused.
    let reaches_dynamic = reaches_dynamic_reference(p);
    for (id, plan) in plans.iter_mut().enumerate() {
        if reaches_dynamic[id] {
            continue;
        }
        if let Some(f) = fused::try_fuse(p, id as NodeId, &resolved) {
            let body = plan.body.get_or_insert_with(|| Box::new(Body { node: id as NodeId, ..Body::default() }));
            body.fused = Some(Box::new(f));
        }
    }
    let fused_nodes: Vec<bool> = plans.iter().map(|pl| pl.body.as_ref().is_some_and(|b| b.fused.is_some())).collect();
    let fix = |c: &mut Child| {
        if c.id == u32::MAX {
            return;
        }
        let (types, shape) = summary[c.id as usize];
        c.types = types;
        c.shape = if shape == Shape::Object && fused_nodes[c.id as usize] { Shape::General } else { shape };
    };
    for body in plans.iter_mut().filter_map(|pl| pl.body.as_deref_mut()) {
        if let Some(o) = &mut body.object {
            o.children.iter_mut().for_each(fix);
            o.patterns.iter_mut().for_each(|(_, c)| fix(c));
            o.additional.iter_mut().for_each(fix);
        }
        if let Some(a) = &mut body.array {
            a.prefix.iter_mut().for_each(fix);
            a.items.iter_mut().for_each(fix);
            let items = a.items.map_or(Some(ANY), |c| c.trivial().then_some(c.types));
            a.simple = items.filter(|_| a.prefix.is_empty() && a.contains.is_none() && !a.unique);
        }
        if let Some((_, c)) = &mut body.unevaluated_items {
            fix(c);
        }
        for op in body.apply.iter_mut() {
            match op {
                Op::AllOf(list) => list.iter_mut().for_each(fix),
                Op::AnyOf(b) | Op::OneOf(b) => {
                    b.children.iter_mut().for_each(fix);
                    b.dispatch();
                }
                _ => {}
            }
        }
    }
    plans
}

fn plan_node(p: &Program, id: NodeId, n: &SchemaNode, target: &dyn Fn(NodeId) -> NodeId) -> Plan {
    let guard = n.in_place_cycle;
    if n.always_true {
        return Plan { types: ANY, guard, body: None };
    }
    if n.always_false {
        return Plan { types: 0, guard, body: None };
    }
    let child = |id: NodeId| Child { id: target(id), types: ANY, shape: Shape::General };
    let mut ops = Vec::new();
    let mut number = Vec::new();
    let mut string = Vec::new();

    if let Some(c) = &n.const_value {
        ops.push(Op::Const(c.clone()));
    }
    if let Some(values) = &n.enum_values {
        if values.iter().all(Value::is_string) {
            ops.push(Op::EnumStrings(Names::new(values.iter().map(|v| v.as_str().unwrap().into()).collect())));
        } else {
            ops.push(Op::Enum(values.clone().into_boxed_slice()));
        }
    }

    let format_check = |numeric: bool| -> Option<FormatCheck> {
        if !n.assert_format || n.format_kind.is_numeric() != numeric {
            return None;
        }
        let f = n.format.as_ref()?;
        match p.formats.get(f) {
            Some(custom) => Some(FormatCheck::Custom(custom.clone())),
            None if n.format_kind == FormatKind::Unknown => None,
            None => Some(FormatCheck::Builtin(n.format_kind, n.dialect <= Dialect::Draft6)),
        }
    };

    // Numbers.
    if let Some(f) = format_check(true) {
        number.push(NumberOp::Format(f));
    }
    if let Some(b) = &n.minimum {
        number.push(NumberOp::Minimum(Num::of(b)));
    }
    if let Some(b) = &n.maximum {
        number.push(NumberOp::Maximum(Num::of(b)));
    }
    if let Some(b) = &n.exclusive_minimum {
        number.push(NumberOp::ExclusiveMinimum(Num::of(b)));
    }
    if let Some(b) = &n.exclusive_maximum {
        number.push(NumberOp::ExclusiveMaximum(Num::of(b)));
    }
    if let Some(d) = &n.multiple_of {
        number.push(NumberOp::MultipleOf(Divisor::new(d)));
    }

    // Strings.
    if n.min_length.is_some() || n.max_length.is_some() {
        string.push(StringOp::Length { min: n.min_length.unwrap_or(0), max: n.max_length.unwrap_or(u64::MAX) });
    }
    if let Some(pattern) = &n.pattern {
        string.push(StringOp::Pattern(pattern.clone()));
    }
    if let Some(f) = format_check(false) {
        string.push(StringOp::Format(f));
    }
    if n.assert_content {
        string.push(StringOp::Content(n.content));
    }

    let values = std::mem::take(&mut ops);

    // Objects (unless the type excludes objects: then the keywords apply to nothing and must not cost the node its
    // shape, as C#'s LiveObjectKeywords).
    let own_types = if n.has_type { n.type_mask } else { ANY };
    let mut object = None;
    if n.has_object_keywords() && own_types & type_mask::OBJECT != 0 {
        let declared: Vec<(String, NodeId)> = n.properties.clone().unwrap_or_default();
        let required = n.required.clone().unwrap_or_default();
        // Names only required or dependencies mention join the declared ones, up to 64 names in all (one mask word).
        let mut known: Vec<String> = declared.iter().map(|(k, _)| k.clone()).collect();
        let mut extras: Vec<String> = Vec::new();
        let dependency_names =
            n.dependencies.iter().flatten().flat_map(|d| std::iter::once(&d.name).chain(d.required.iter().flatten()));
        for name in required.iter().chain(dependency_names) {
            if !known.contains(name) && !extras.contains(name) {
                extras.push(name.clone());
            }
        }
        let with_extras = known.len() + extras.len() <= 64;
        if with_extras {
            known.extend(extras);
        }
        let names = Names::new(known.iter().map(|k| k.as_str().into()).collect());
        let undeclared = n.additional_properties.map_or(NO_CHILD, child);
        let children: Box<[Child]> = declared
            .iter()
            .map(|&(_, c)| child(c))
            .chain(std::iter::repeat_n(undeclared, known.len() - declared.len()))
            .collect();
        let has_names = !known.is_empty();
        let visit = if n.property_names.is_none()
            && !has_names
            && n.pattern_properties.as_ref().is_some_and(|p| p.len() == 1)
        {
            Visit::Pattern
        } else if n.pattern_properties.is_some() || n.property_names.is_some() {
            Visit::General
        } else if has_names {
            Visit::Names
        } else if n.additional_properties.is_some() {
            Visit::Values
        } else {
            Visit::None
        };
        let visited = matches!(visit, Visit::Names | Visit::General) && known.len() <= 64;
        let bit = |name: &str| names.find(name).filter(|_| visited).map(|i| 1u64 << i);
        let mut required_mask = 0u64;
        let mut by_lookup = Vec::new();
        for r in &required {
            match bit(r) {
                Some(b) => required_mask |= b,
                None => by_lookup.push(r.as_str().into()),
            }
        }
        let dependencies: Box<[Dependency]> = n
            .dependencies
            .iter()
            .flatten()
            .map(|d| {
                let required: Option<u64> =
                    d.required.iter().flatten().try_fold(0u64, |mask, r| bit(r).map(|b| mask | b));
                Dependency {
                    name: d.name.as_str().into(),
                    required: d.required.iter().flatten().map(|r| r.as_str().into()).collect(),
                    schema: d.schema.map(target),
                    bits: bit(&d.name).zip(required),
                }
            })
            .collect();
        let by_lookup_empty = by_lookup.is_empty();
        object = Some(ObjectPlan {
            min: n.min_properties.unwrap_or(0),
            max: n.max_properties.unwrap_or(u64::MAX),
            visit,
            names,
            declared: declared.len(),
            children,
            required_mask,
            required: by_lookup.into_boxed_slice(),
            patterns: n.pattern_properties.iter().flatten().map(|pp| (pp.pattern.clone(), child(pp.node))).collect(),
            name_patterns: known
                .iter()
                .map(|name| {
                    let patterns = n.pattern_properties.iter().flatten().enumerate();
                    patterns.filter(|(_, pp)| pp.pattern.is_match(name)).map(|(j, _)| j as u16).collect()
                })
                .collect(),
            additional: n.additional_properties.map(child),
            property_names: n.property_names.map(target),
            dependencies,
            rest_free: by_lookup_empty && n.dependencies.is_none(),
            strict: visit == Visit::Names && by_lookup_empty && n.dependencies.is_none(),
        });
    }

    // Arrays.
    let mut array = None;
    if n.has_array_keywords() && own_types & type_mask::ARRAY != 0 {
        array = Some(ArrayPlan {
            min: n.min_items.unwrap_or(0),
            max: n.max_items.unwrap_or(u64::MAX),
            prefix: n.prefix_items.iter().flatten().map(|&c| child(c)).collect(),
            items: n.items.map(child),
            contains: n.contains.map(|c| (target(c), n.min_contains, n.max_contains)),
            unique: n.unique_items,
            simple: None,
        });
    }

    // In-place applicators, in the general evaluator's order.
    if let Some(r) = n.ref_ {
        ops.push(Op::Ref(target(r)));
    }
    if let Some(r) = n.static_dynamic_ref {
        ops.push(Op::Ref(target(r)));
    }
    if let Some(d) = &n.dynamic_ref {
        if p.uses_dynamic_scope {
            ops.push(Op::DynamicRef(d.clone()));
        } else {
            // Without a dynamic scope the reference always takes its fallback.
            ops.push(Op::Ref(target(d.fallback)));
        }
    }
    if let Some(list) = &n.all_of {
        ops.push(Op::AllOf(list.iter().map(|&c| child(c)).collect()));
    }
    // An anyOf of type-only branches, or a oneOf of type-only branches with no type in common, is one type test
    // (the C# evaluator's TypeUnion plan): it narrows the node's own types instead of adding a keyword.
    let mut union = ANY;
    if let Some(list) = &n.any_of {
        match type_union(p, list, false) {
            Some(mask) => union = meet(union, mask),
            None => ops.push(Op::AnyOf(Box::new(Branches::new(
                list.iter().map(|&c| child(c)).collect(),
                n.any_of_discriminator.clone(),
            )))),
        }
    }
    if let Some(list) = &n.one_of {
        match type_union(p, list, true) {
            Some(mask) => union = meet(union, mask),
            None => ops.push(Op::OneOf(Box::new(Branches::new(
                list.iter().map(|&c| child(c)).collect(),
                n.one_of_discriminator.clone(),
            )))),
        }
    }
    if let Some(not) = n.not {
        ops.push(Op::Not(target(not)));
    }
    if let Some(cond) = n.if_ {
        let (then, else_) = (n.then.map(target), n.else_.map(target));
        if then.is_some() || else_.is_some() {
            ops.push(Op::If { cond: target(cond), then, else_ });
        }
    }

    // unevaluatedProperties is left to the general evaluator (or a fused object plan); unevaluatedItems takes the
    // items after a static prefix when every contribution to the evaluated items is unconditional.
    let mut general = 0;
    if n.unevaluated_properties.is_some() {
        general |= type_mask::OBJECT;
    }
    let mut unevaluated_items = None;
    if let Some(u) = n.unevaluated_items {
        match static_item_coverage(p, id) {
            Some(Coverage::All) => {}
            Some(Coverage::From(from)) => unevaluated_items = Some((from, child(u))),
            None => general |= type_mask::ARRAY,
        }
    }

    let types = meet(if n.has_type { n.type_mask } else { ANY }, union);
    if values.is_empty()
        && number.is_empty()
        && string.is_empty()
        && object.is_none()
        && array.is_none()
        && ops.is_empty()
        && general == 0
        && unevaluated_items.is_none()
    {
        return Plan { types, guard, body: None };
    }
    let body = Body {
        fused: None,
        general,
        node: id,
        unevaluated_items,
        values: values.into_boxed_slice(),
        number: number.into_boxed_slice(),
        string: string.into_boxed_slice(),
        object,
        array,
        apply: ops.into_boxed_slice(),
    };
    Plan { types, guard, body: Some(Box::new(body)) }
}

/// How callers can enter a plan (see `Shape`). The shortcuts skip the scope push, so a program that keeps a dynamic
/// scope takes them only for leaves; a node on an in-place cycle is entered through its guard.
fn shape_of(pl: &Plan, dynamic_scope: bool) -> Shape {
    if pl.guard {
        return Shape::General;
    }
    let Some(b) = pl.body.as_deref() else { return Shape::Trivial };
    let plain = b.fused.is_none() && b.general == 0 && b.unevaluated_items.is_none();
    let values = !b.values.is_empty() || !b.number.is_empty() || !b.string.is_empty();
    let (object, array, apply) = (b.object.is_some(), b.array.is_some(), !b.apply.is_empty());
    match (plain, values, object, array, apply) {
        (true, true, false, false, false)
            if b.number.is_empty() && b.string.is_empty() && matches!(&*b.values, [Op::EnumStrings(_)]) =>
        {
            Shape::StringEnum
        }
        (true, true, false, false, false) if b.number.is_empty() && b.values.is_empty() => Shape::Strings,
        (true, _, false, false, false) => Shape::Leaf,
        (true, false, true, false, false) if !dynamic_scope => Shape::Object,
        (true, false, false, true, false) if !dynamic_scope => Shape::Array,
        (true, false, false, false, true) if !dynamic_scope => Shape::Apply,
        _ => Shape::General,
    }
}

/// Marks every node from which a live dynamic reference is reachable through any child.
fn reaches_dynamic_reference(p: &Program) -> Vec<bool> {
    let mut reaches: Vec<bool> = p.nodes.iter().map(|n| p.uses_dynamic_scope && n.dynamic_ref.is_some()).collect();
    if !p.uses_dynamic_scope {
        return reaches;
    }
    let children: Vec<Vec<NodeId>> = p.nodes.iter().map(|n| n.children()).collect();
    let mut changed = true;
    while changed {
        changed = false;
        for (i, c) in children.iter().enumerate() {
            if !reaches[i] && c.iter().any(|&c| reaches[c as usize]) {
                reaches[i] = true;
                changed = true;
            }
        }
    }
    reaches
}

/// A type mask with `integer` made explicit wherever `number` is (every integer is a number).
fn expand(mask: u8) -> u8 {
    if mask & type_mask::NUMBER != 0 { mask | type_mask::INTEGER } else { mask }
}

/// The types both masks admit.
fn meet(a: u8, b: u8) -> u8 {
    let m = expand(a) & expand(b);
    // `number` in both keeps `number`; `integer` alone stays `integer`.
    if a & b & type_mask::NUMBER == 0 && m & type_mask::NUMBER != 0 { m & !type_mask::NUMBER } else { m }
}

/// A schema that tests only the type (`true` admits everything, `false` nothing).
fn type_only_mask(n: &SchemaNode) -> Option<u8> {
    if n.always_true {
        return Some(ANY);
    }
    if n.always_false {
        return Some(0);
    }
    let only_type = n.has_type
        && !n.in_place_cycle
        && n.const_value.is_none()
        && n.enum_values.is_none()
        && !n.has_number_keywords()
        && !n.has_string_keywords()
        && !n.has_object_keywords()
        && !n.has_array_keywords()
        && !n.has_in_place_applicators()
        && n.dynamic_ref.is_none();
    only_type.then_some(n.type_mask)
}

/// The union of anyOf/oneOf branches that all test only the type; for oneOf, only when no two branches admit a
/// common value (so that "exactly one" is "any").
fn type_union(p: &Program, list: &[NodeId], exactly_one: bool) -> Option<u8> {
    let masks: Vec<u8> =
        list.iter().map(|&c| type_only_mask(&p.nodes[p.fast_target[c as usize] as usize])).collect::<Option<_>>()?;
    if exactly_one {
        for (i, a) in masks.iter().enumerate() {
            if masks[i + 1..].iter().any(|b| expand(*a) & expand(*b) != 0) {
                return None;
            }
        }
    }
    Some(masks.iter().fold(0, |u, m| u | m))
}

enum Coverage {
    All,
    From(usize),
}

/// The items a node's evaluation always marks evaluated, when that is static: the longest prefixItems of the node and
/// the contributors it always applies ($ref and allOf chains), or all of them when one has items (or, below the node,
/// unevaluatedItems). `None` when an in-place child that applies conditionally (anyOf, oneOf, if/then/else, a
/// dependent schema, a dynamic reference) can mark items, or contains marks them.
fn static_item_coverage(p: &Program, id: NodeId) -> Option<Coverage> {
    let mut prefix = 0;
    let mut all = false;
    let mut stack = vec![(id, true)];
    let mut visited = vec![id];
    while let Some((at, root)) = stack.pop() {
        let n = &p.nodes[at as usize];
        if n.always_true || n.always_false {
            continue;
        }
        if n.in_place_cycle || n.dynamic_ref.is_some() || (n.contains.is_some() && n.contains_marks_evaluated) {
            return None;
        }
        prefix = prefix.max(n.prefix_items.as_ref().map_or(0, Vec::len));
        all |= n.items.is_some() || (!root && n.unevaluated_items.is_some());
        let conditional: Vec<NodeId> = n
            .any_of
            .iter()
            .flatten()
            .chain(n.one_of.iter().flatten())
            .copied()
            .chain([n.if_, n.then, n.else_].into_iter().flatten())
            .chain(n.dependencies.iter().flatten().filter_map(|d| d.schema))
            .collect();
        for c in conditional {
            if p.nodes[c as usize].marks_items {
                return None;
            }
        }
        for c in [n.ref_, n.static_dynamic_ref].into_iter().flatten().chain(n.all_of.iter().flatten().copied()) {
            if !visited.contains(&c) {
                visited.push(c);
                stack.push((c, false));
            }
        }
    }
    Some(if all { Coverage::All } else { Coverage::From(prefix) })
}

// ---------------------------------------------------------------------------------------------------------------------
// Evaluation

impl Evaluator<'_, '_> {
    /// Evaluates a node's plan (at a new instance location, or where no depth guard applies).
    #[inline]
    pub(super) fn run(&mut self, id: NodeId, x: &Value) -> bool {
        let plan = &self.p.plans[id as usize];
        (plan.types == ANY || type_ok(plan.types, x)) && plan.body.as_deref().is_none_or(|b| self.run_body(b, x))
    }

    /// A child at a new instance location: its type check inline, its other keywords (if any) by call.
    #[inline(always)]
    fn run_child(&mut self, c: Child, x: &Value) -> bool {
        if c.types != ANY && !type_ok(c.types, x) {
            return false;
        }
        if c.trivial() {
            return true;
        }
        match self.p.plans[c.id as usize].body.as_deref() {
            None => true,
            Some(b) => self.enter(c.shape, b, x),
        }
    }

    /// A body entered by shape (its types already tested, and not on an in-place cycle unless `General`).
    #[inline(always)]
    fn enter(&mut self, shape: Shape, b: &Body, x: &Value) -> bool {
        match shape {
            Shape::Leaf => run_leaf(b, x),
            Shape::StringEnum => match (&b.values[0], x) {
                (Op::EnumStrings(values), Value::String(s)) => values.contains(s),
                _ => false,
            },
            Shape::Strings => match x {
                Value::String(s) => run_string(&b.string, s),
                _ => true,
            },
            Shape::Object => match x {
                Value::Object(o) => {
                    let plan = b.object.as_ref().unwrap();
                    if plan.strict { self.run_strict_object(plan, o) } else { self.run_object(plan, o, x) }
                }
                _ => true,
            },
            Shape::Array => match x {
                Value::Array(a) => self.run_array(b.array.as_ref().unwrap(), a),
                _ => true,
            },
            Shape::Apply => self.run_apply(&b.apply, x),
            Shape::Trivial | Shape::General => self.run_body(b, x),
        }
    }

    /// Evaluates an in-place child under the depth guard.
    #[inline]
    fn run_in_place(&mut self, id: NodeId, x: &Value) -> bool {
        if !self.p.plans[id as usize].guard {
            return self.run(id, x);
        }
        self.depth += 1;
        if self.depth > self.p.max_depth {
            self.depth_exceeded = true;
            self.depth -= 1;
            return false;
        }
        let ok = self.run(id, x);
        self.depth -= 1;
        ok
    }

    /// Evaluates a property name as a string instance, in a buffer reused across names.
    fn run_name(&mut self, id: NodeId, name: &str) -> bool {
        // A name is a string: a plan with nothing beyond its types is decided without building the value.
        let plan = &self.p.plans[id as usize];
        if plan.body.is_none() {
            return plan.types & type_mask::STRING != 0;
        }
        let mut buffer = std::mem::take(&mut self.name_buffer);
        if let Value::String(s) = &mut buffer {
            s.clear();
            s.push_str(name);
        } else {
            buffer = Value::String(name.to_string());
        }
        let ok = self.run(id, &buffer);
        self.name_buffer = buffer;
        ok
    }

    /// An in-place child with its type check inline, then its keywords under the depth guard (without testing the
    /// type again).
    #[inline(always)]
    fn run_branch(&mut self, c: Child, x: &Value) -> bool {
        if c.types != ANY && !type_ok(c.types, x) {
            return false;
        }
        if c.trivial() {
            return true;
        }
        let plan = &self.p.plans[c.id as usize];
        let Some(b) = plan.body.as_deref() else { return self.run_in_place(c.id, x) };
        if plan.guard {
            return self.run_in_place(c.id, x);
        }
        self.enter(c.shape, b, x)
    }

    /// The anyOf/oneOf branches that can match: those a discriminator selects, or those admitting the instance type.
    #[inline(always)]
    fn candidates<'b>(&self, b: &'b Branches, x: &Value) -> &'b [u32] {
        let (Some(disc), Value::Object(o)) = (b.discriminator.as_deref(), x) else { return &b.by_kind[kind(x)] };
        let (d, index) = disc;
        match get_key(o, &d.property) {
            Some(v) => index.select(d, v),
            None if d.all_require => &[],
            None => &b.by_kind[kind(x)],
        }
    }

    /// A node's keywords. Where the program keeps a dynamic scope, entering a node of another resource pushes that
    /// resource (as the general evaluator does), for the dynamic references below it.
    #[inline]
    fn run_body(&mut self, b: &Body, x: &Value) -> bool {
        if !self.p.uses_dynamic_scope {
            return self.run_keywords(b, x);
        }
        let resource = self.p.nodes[b.node as usize].resource_id;
        let pushed = self.scope.last() != Some(&resource);
        if pushed {
            self.scope.push(resource);
        }
        let ok = self.run_keywords(b, x);
        if pushed {
            self.scope.pop();
        }
        ok
    }

    fn run_keywords(&mut self, b: &Body, x: &Value) -> bool {
        match x {
            Value::Object(o) => {
                if let Some(f) = &b.fused {
                    return self.run_fused(f, o);
                }
                if b.general & type_mask::OBJECT != 0 {
                    return self.eval_node::<Fast>(b.node, x, None);
                }
            }
            Value::Array(_) if b.general & type_mask::ARRAY != 0 => return self.eval_node::<Fast>(b.node, x, None),
            _ => {}
        }
        if !b.values.is_empty() {
            for op in b.values.iter() {
                if !self.run_op(op, x) {
                    return false;
                }
            }
        }
        let ok = match x {
            Value::Number(n) => b.number.is_empty() || run_number(&b.number, n),
            Value::String(s) => b.string.is_empty() || run_string(&b.string, s),
            Value::Object(o) => b.object.as_ref().is_none_or(|plan| self.run_object(plan, o, x)),
            Value::Array(a) => {
                b.array.as_ref().is_none_or(|plan| self.run_array(plan, a))
                    && b.unevaluated_items
                        .is_none_or(|(from, c)| a.iter().skip(from).all(|item| self.run_child(c, item)))
            }
            _ => true,
        };
        ok && (b.apply.is_empty() || self.run_apply(&b.apply, x))
    }

    fn run_apply(&mut self, ops: &[Op], x: &Value) -> bool {
        for op in ops {
            if !self.run_op(op, x) {
                return false;
            }
        }
        true
    }

    #[inline]
    fn run_op(&mut self, op: &Op, x: &Value) -> bool {
        match op {
            Op::Const(c) => json_equal(x, c),
            Op::EnumStrings(values) => match x {
                Value::String(s) => values.contains(s),
                _ => false,
            },
            Op::Enum(values) => values.iter().any(|v| json_equal(x, v)),
            Op::Ref(r) => self.run_in_place(*r, x),
            Op::AllOf(list) => list.iter().all(|&c| self.run_branch(c, x)),
            Op::AnyOf(b) => self.candidates(b, x).iter().any(|&i| self.run_branch(b.children[i as usize], x)),
            Op::OneOf(b) => {
                let candidates = self.candidates(b, x);
                // The instance's type (or the discriminator) leaves one branch: that branch decides.
                if let [only] = candidates {
                    return self.run_branch(b.children[*only as usize], x);
                }
                let mut matched = 0;
                for &i in candidates {
                    if self.run_branch(b.children[i as usize], x) {
                        matched += 1;
                        if matched > 1 {
                            break;
                        }
                    }
                }
                matched == 1
            }
            Op::Not(c) => !self.run(*c, x),
            Op::DynamicRef(d) => {
                let target = self.p.fast_target[self.resolve_dynamic(d) as usize];
                self.run_in_place(target, x)
            }
            Op::If { cond, then, else_ } => {
                let next = if self.run_in_place(*cond, x) { then } else { else_ };
                next.is_none_or(|c| self.run_in_place(c, x))
            }
        }
    }

    /// An object plan: the size bounds, the property loop for its shape (each in its own function, so that this entry
    /// stays small), then the required names and dependencies.
    fn run_object(&mut self, plan: &ObjectPlan, o: &Map<String, Value>, x: &Value) -> bool {
        let len = o.len() as u64;
        if len < plan.min || len > plan.max {
            return false;
        }
        let seen = match plan.visit {
            Visit::None => Some(0),
            Visit::Values => self.visit_values(plan, o).then_some(0),
            Visit::Names => self.visit_names(plan, o),
            Visit::Pattern => self.visit_pattern(plan, o).then_some(0),
            Visit::General => self.visit_general(plan, o),
        };
        let Some(seen) = seen else { return false };
        if seen & plan.required_mask != plan.required_mask {
            return false;
        }
        plan.rest_free || self.object_rest(plan, o, x, seen)
    }

    /// The C# StrictObject loop: bounds, declared names (additionalProperties for the rest), and the required mask;
    /// a small function of its own, since nested objects enter it directly.
    fn run_strict_object(&mut self, plan: &ObjectPlan, o: &Map<String, Value>) -> bool {
        let len = o.len() as u64;
        if len < plan.min || len > plan.max {
            return false;
        }
        let mut seen = 0u64;
        let mut hint = 0;
        for (k, v) in o {
            match plan.names.find_from(k, &mut hint) {
                Some(i) => {
                    seen |= 1 << (i & 63);
                    if !self.run_child(plan.children[i], v) {
                        return false;
                    }
                }
                None => {
                    if let Some(c) = plan.additional
                        && !self.run_child(c, v)
                    {
                        return false;
                    }
                }
            }
        }
        seen & plan.required_mask == plan.required_mask
    }

    /// Required names checked by lookup, and dependencies.
    #[inline(never)]
    fn object_rest(&mut self, plan: &ObjectPlan, o: &Map<String, Value>, x: &Value, seen: u64) -> bool {
        if !plan.required.iter().all(|r| has_key(o, r)) {
            return false;
        }
        for d in plan.dependencies.iter() {
            match d.bits {
                Some((name, required)) => {
                    if seen & name == 0 {
                        continue;
                    }
                    if seen & required != required {
                        return false;
                    }
                }
                None => {
                    if !has_key(o, &d.name) {
                        continue;
                    }
                    if !d.required.iter().all(|r| has_key(o, r)) {
                        return false;
                    }
                }
            }
            if let Some(s) = d.schema
                && !self.run_in_place(s, x)
            {
                return false;
            }
        }
        true
    }

    /// Only additionalProperties: every value against one child.
    fn visit_values(&mut self, plan: &ObjectPlan, o: &Map<String, Value>) -> bool {
        let c = plan.additional.unwrap();
        if c.trivial() {
            return c.types == ANY || o.values().all(|v| type_ok(c.types, v));
        }
        o.values().all(|v| self.run_child(c, v))
    }

    /// Declared properties, and additionalProperties for the rest; the declared names seen, or `None` on failure.
    fn visit_names(&mut self, plan: &ObjectPlan, o: &Map<String, Value>) -> Option<u64> {
        let mut seen = 0u64;
        let mut hint = 0;
        for (k, v) in o {
            match plan.names.find_from(k, &mut hint) {
                Some(i) => {
                    seen |= 1 << (i & 63);
                    if !self.run_child(plan.children[i], v) {
                        return None;
                    }
                }
                None => {
                    if let Some(c) = plan.additional
                        && !self.run_child(c, v)
                    {
                        return None;
                    }
                }
            }
        }
        Some(seen)
    }

    fn visit_pattern(&mut self, plan: &ObjectPlan, o: &Map<String, Value>) -> bool {
        let (pattern, c) = &plan.patterns[0];
        for (k, v) in o {
            let applies = if pattern.is_match(k) { Some(*c) } else { plan.additional };
            if let Some(c) = applies
                && !self.run_child(c, v)
            {
                return false;
            }
        }
        true
    }

    fn visit_general(&mut self, plan: &ObjectPlan, o: &Map<String, Value>) -> Option<u64> {
        let mut seen = 0u64;
        let mut hint = 0;
        for (k, v) in o {
            let mut matched = false;
            if let Some(i) = plan.names.find_from(k, &mut hint) {
                seen |= 1 << (i & 63);
                // A name only required (or a dependency) mentions is undeclared: patterns, else additionalProperties.
                if i < plan.declared {
                    matched = true;
                    if !self.run_child(plan.children[i], v) {
                        return None;
                    }
                }
                for &j in plan.name_patterns[i].iter() {
                    matched = true;
                    if !self.run_child(plan.patterns[j as usize].1, v) {
                        return None;
                    }
                }
            } else {
                for (pattern, c) in plan.patterns.iter() {
                    if pattern.is_match(k) {
                        matched = true;
                        if !self.run_child(*c, v) {
                            return None;
                        }
                    }
                }
            }
            if !matched
                && let Some(c) = plan.additional
                && !self.run_child(c, v)
            {
                return None;
            }
            if let Some(pn) = plan.property_names
                && !self.run_name(pn, k)
            {
                return None;
            }
        }
        Some(seen)
    }

    fn run_array(&mut self, plan: &ArrayPlan, a: &[Value]) -> bool {
        let len = a.len() as u64;
        if len < plan.min || len > plan.max {
            return false;
        }
        if let Some(types) = plan.simple {
            return all_of_type(a, types);
        }
        let prefix = plan.prefix.len().min(a.len());
        for (c, item) in plan.prefix.iter().zip(a) {
            if !self.run_child(*c, item) {
                return false;
            }
        }
        if let Some(items) = plan.items {
            let rest = &a[prefix..];
            if items.trivial() {
                // A type-only items schema: one tight loop (the C# evaluator's SimpleArray plan), or none for `true`.
                if !all_of_type(rest, items.types) {
                    return false;
                }
            } else {
                for item in rest {
                    if !self.run_child(items, item) {
                        return false;
                    }
                }
            }
        }
        if let Some((c, min, max)) = plan.contains {
            let mut count = 0u64;
            for item in a {
                if self.run(c, item) {
                    count += 1;
                    if max.is_none() && count >= min {
                        break;
                    }
                }
            }
            if count < min || max.is_some_and(|m| count > m) {
                return false;
            }
        }
        !plan.unique || all_unique(a)
    }
}

/// Whether every value is of the types in a mask, with the common masks as tight loops.
#[inline]
fn all_of_type(a: &[Value], types: u8) -> bool {
    const NUMBERS: u8 = type_mask::NUMBER | type_mask::INTEGER;
    match types {
        ANY => true,
        type_mask::STRING => a.iter().all(Value::is_string),
        t if t & !NUMBERS == 0 && t & type_mask::NUMBER != 0 => a.iter().all(Value::is_number),
        t => a.iter().all(|v| type_ok(t, v)),
    }
}

/// A leaf's keywords: its value constraints, then those for the instance's type.
#[inline]
fn run_leaf(b: &Body, x: &Value) -> bool {
    for op in b.values.iter() {
        let ok = match op {
            Op::Const(c) => json_equal(x, c),
            Op::EnumStrings(values) => matches!(x, Value::String(s) if values.contains(s)),
            Op::Enum(values) => values.iter().any(|v| json_equal(x, v)),
            _ => unreachable!("a leaf has only value keywords"),
        };
        if !ok {
            return false;
        }
    }
    match x {
        Value::Number(n) => b.number.is_empty() || run_number(&b.number, n),
        Value::String(s) => b.string.is_empty() || run_string(&b.string, s),
        _ => true,
    }
}

#[inline(never)]
fn run_number(ops: &[NumberOp], n: &Number) -> bool {
    let v = Num::of(n);
    ops.iter().all(|op| match op {
        NumberOp::Minimum(b) => cmp(v, *b).is_ge(),
        NumberOp::Maximum(b) => cmp(v, *b).is_le(),
        NumberOp::ExclusiveMinimum(b) => cmp(v, *b).is_gt(),
        NumberOp::ExclusiveMaximum(b) => cmp(v, *b).is_lt(),
        NumberOp::MultipleOf(d) => d.divides(n),
        NumberOp::Format(f) => f.number(n),
    })
}

#[inline(never)]
fn run_string(ops: &[StringOp], s: &str) -> bool {
    ops.iter().all(|op| match op {
        StringOp::Length { min, max } => length_ok(s, *min, *max),
        StringOp::Pattern(p) => p.is_match(s),
        StringOp::Format(f) => f.string(s),
        StringOp::Content(kind) => content_ok(s, *kind),
    })
}

/// minLength/maxLength, counting code points only when the byte length cannot decide (a code point is 1–4 bytes).
#[inline]
fn length_ok(s: &str, min: u64, max: u64) -> bool {
    let bytes = s.len() as u64;
    if bytes < min {
        return false;
    }
    if bytes <= max && bytes.div_ceil(4) >= min {
        return true;
    }
    // Every code point is at most four bytes: more than `max` of them for sure.
    if bytes.div_ceil(4) > max {
        return false;
    }
    let chars = code_points(s);
    chars >= min && chars <= max
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn meet_treats_integers_as_numbers() {
        use type_mask::*;
        assert_eq!(meet(INTEGER, NUMBER), INTEGER);
        assert_eq!(meet(NUMBER, NUMBER | STRING), NUMBER | INTEGER);
        assert_eq!(meet(ANY, STRING | ARRAY), STRING | ARRAY);
        assert_eq!(meet(STRING, INTEGER), 0);
        for x in [serde_json::json!(1), serde_json::json!(1.5), serde_json::json!("a")] {
            for (a, b) in [(INTEGER, NUMBER), (NUMBER, INTEGER), (NUMBER | STRING, INTEGER | STRING), (ANY, NUMBER)] {
                assert_eq!(type_ok(meet(a, b), &x), type_ok(a, &x) && type_ok(b, &x), "{a} {b} {x}");
            }
        }
    }

    #[test]
    fn str_eq_agrees_with_equality() {
        let words =
            ["", "a", "ab", "abc", "abcd", "abcde", "abcdefgh", "abcdefghi", "abcdefghijklmnop", "abcdefghijklmnopq"];
        for a in words {
            for b in words {
                assert_eq!(str_eq(a, b), a == b, "{a:?} {b:?}");
                let mut c = b.to_string();
                if !c.is_empty() {
                    let mid = c.len() / 2;
                    c.replace_range(mid..mid + 1, "Z");
                    assert_eq!(str_eq(a, &c), a == c, "{a:?} {c:?}");
                }
            }
        }
    }

    #[test]
    fn name_table_finds_every_name() {
        let names: Vec<Box<str>> = (0..200).map(|i| format!("p{i}{}", "x".repeat(i % 13)).into()).collect();
        let table = NameTable::new(names.clone());
        for (i, n) in names.iter().enumerate() {
            assert_eq!(table.find(n), Some(i));
        }
        for miss in ["", "p", "q1", "p1000", "p0x"] {
            assert_eq!(table.find(miss), None, "{miss}");
        }
    }

    #[test]
    fn linear_names_find_from_any_hint() {
        let names = Names::new(vec!["a".into(), "b".into(), "c".into()]);
        for start in 0..3 {
            for (i, n) in ["a", "b", "c"].iter().enumerate() {
                let mut hint = start;
                assert_eq!(names.find_from(n, &mut hint), Some(i));
            }
            assert_eq!(names.find_from("d", &mut 0), None);
        }
    }

    #[test]
    fn length_ok_agrees_with_counting() {
        for s in ["", "a", "abcd", "é", "éé", "😀😀", "a😀b"] {
            let chars = s.chars().count() as u64;
            for min in 0..6 {
                for max in 0..6 {
                    assert_eq!(length_ok(s, min, max), chars >= min && chars <= max, "{s:?} {min} {max}");
                }
            }
        }
    }
}
