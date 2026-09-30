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

use super::{Evaluator, Fast, Program, Selection, all_unique, code_points, content_ok, json_equal, select};
use crate::dialect::Dialect;
use crate::formats::FormatKind;
use crate::node::*;
use crate::numbers::{Num, cmp, multiple_of};
use crate::options::FormatValidator;
use crate::pattern::Pattern;

/// Every JSON type (a node without `type`).
const ANY: u8 = 0x7f;

/// A child application, with its type check hoisted so that a child that is only a type check needs no call.
#[derive(Clone, Copy)]
pub(crate) struct Child {
    id: NodeId,
    /// The types the child accepts (0 for `false`).
    types: u8,
    /// The child has nothing to check beyond its types.
    trivial: bool,
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
    MultipleOf(Number),
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
    EnumStrings(Box<[Box<str>]>),
    Enum(Box<[Value]>),
    Ref(NodeId),
    AllOf(Box<[Child]>),
    AnyOf(Box<Branches>),
    OneOf(Box<Branches>),
    Not(NodeId),
    If {
        cond: NodeId,
        then: Option<NodeId>,
        else_: Option<NodeId>,
    },
    /// Evaluated properties/items are tracked: the general evaluator runs the node.
    General(NodeId),
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
    names: Names,
    children: Box<[Child]>,
    /// Bit `i` set: `children[i]`'s name is required. Only when every required name is declared (and at most 64).
    required_mask: u64,
    /// Required names checked by lookup (when the mask cannot cover them).
    required: Box<[Box<str>]>,
    patterns: Box<[(Arc<Pattern>, Child)]>,
    additional: Option<Child>,
    property_names: Option<NodeId>,
    dependencies: Box<[Dependency]>,
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
    /// Anything else (patternProperties, propertyNames).
    General,
}

/// anyOf/oneOf branches, dispatched by the instance's type: only the branches whose `type` admits it are tried (the
/// C# evaluator's type-dispatch plans), and a branch that is only a type check is decided without a call.
struct Branches {
    children: Box<[Child]>,
    /// For each instance kind (see `kind`), the branches that can accept it.
    by_kind: [Box<[u32]>; 6],
    discriminator: Option<Box<Discriminator>>,
}

impl Branches {
    fn new(children: Box<[Child]>, discriminator: Option<Box<Discriminator>>) -> Branches {
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
}

struct ArrayPlan {
    min: u64,
    max: u64,
    prefix: Box<[Child]>,
    items: Option<Child>,
    contains: Option<(NodeId, u64, Option<u64>)>,
    unique: bool,
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
            Lookup::Hashed(table) => table.find(name),
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
    if a.len() <= 8 {
        if a.len() >= 4 {
            let n = a.len();
            let head = |s: &[u8]| u32::from_le_bytes([s[0], s[1], s[2], s[3]]);
            let tail = |s: &[u8]| u32::from_le_bytes([s[n - 4], s[n - 3], s[n - 2], s[n - 1]]);
            return head(a) == head(b) && tail(a) == tail(b);
        }
        return a.iter().zip(b).all(|(x, y)| x == y);
    }
    let n = a.len();
    let word = |s: &[u8], i: usize| u64::from_le_bytes(s[i..i + 8].try_into().unwrap());
    word(a, 0) == word(b, 0) && word(a, n - 8) == word(b, n - 8) && (n <= 16 || a[8..n - 8] == b[8..n - 8])
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
    let summary: Vec<(u8, bool)> = plans.iter().map(|pl| (pl.types, pl.body.is_none() && !pl.guard)).collect();
    let resolved = |id: NodeId| {
        let (types, trivial) = summary[id as usize];
        Child { id, types, trivial }
    };
    // Fused object plans, for nodes whose object semantics span in-place applicators.
    for (id, plan) in plans.iter_mut().enumerate() {
        if let Some(f) = fused::try_fuse(p, id as NodeId, &resolved) {
            plan.body.get_or_insert_with(Box::default).fused = Some(Box::new(f));
        }
    }
    let fix = |c: &mut Child| {
        let (types, trivial) = summary[c.id as usize];
        c.types = types;
        c.trivial = trivial;
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
    if n.unevaluated_properties.is_some() || n.unevaluated_items.is_some() {
        let body = Body { apply: Box::new([Op::General(id)]), ..Body::default() };
        return Plan { types: ANY, guard, body: Some(Box::new(body)) };
    }
    let child = |id: NodeId| Child { id: target(id), types: ANY, trivial: false };
    let mut ops = Vec::new();
    let mut number = Vec::new();
    let mut string = Vec::new();

    if let Some(c) = &n.const_value {
        ops.push(Op::Const(c.clone()));
    }
    if let Some(values) = &n.enum_values {
        if values.iter().all(Value::is_string) {
            ops.push(Op::EnumStrings(values.iter().map(|v| v.as_str().unwrap().into()).collect()));
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
        number.push(NumberOp::MultipleOf(d.clone()));
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

    // Objects.
    let mut object = None;
    if n.has_object_keywords() {
        let declared: Vec<(String, NodeId)> = n.properties.clone().unwrap_or_default();
        let names = Names::new(declared.iter().map(|(k, _)| k.as_str().into()).collect());
        let children: Box<[Child]> = declared.iter().map(|&(_, c)| child(c)).collect();
        let required = n.required.clone().unwrap_or_default();
        let visit = if n.pattern_properties.is_some() || n.property_names.is_some() {
            Visit::General
        } else if n.properties.is_some() {
            Visit::Names
        } else if n.additional_properties.is_some() {
            Visit::Values
        } else {
            Visit::None
        };
        let mut required_mask = 0u64;
        let mut by_lookup = Vec::new();
        let masked = matches!(visit, Visit::Names | Visit::General)
            && declared.len() <= 64
            && required.iter().all(|r| names.find(r).is_some());
        for r in &required {
            match names.find(r) {
                Some(i) if masked => required_mask |= 1 << i,
                _ => by_lookup.push(r.as_str().into()),
            }
        }
        object = Some(ObjectPlan {
            min: n.min_properties.unwrap_or(0),
            max: n.max_properties.unwrap_or(u64::MAX),
            visit,
            names,
            children,
            required_mask,
            required: by_lookup.into_boxed_slice(),
            patterns: n.pattern_properties.iter().flatten().map(|pp| (pp.pattern.clone(), child(pp.node))).collect(),
            additional: n.additional_properties.map(child),
            property_names: n.property_names.map(target),
            dependencies: n
                .dependencies
                .iter()
                .flatten()
                .map(|d| Dependency {
                    name: d.name.as_str().into(),
                    required: d.required.iter().flatten().map(|r| r.as_str().into()).collect(),
                    schema: d.schema.map(target),
                })
                .collect(),
        });
    }

    // Arrays.
    let mut array = None;
    if n.has_array_keywords() {
        array = Some(ArrayPlan {
            min: n.min_items.unwrap_or(0),
            max: n.max_items.unwrap_or(u64::MAX),
            prefix: n.prefix_items.iter().flatten().map(|&c| child(c)).collect(),
            items: n.items.map(child),
            contains: n.contains.map(|c| (target(c), n.min_contains, n.max_contains)),
            unique: n.unique_items,
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
        // Without a dynamic scope the reference always takes its fallback.
        ops.push(Op::Ref(target(d.fallback)));
    }
    if let Some(list) = &n.all_of {
        ops.push(Op::AllOf(list.iter().map(|&c| child(c)).collect()));
    }
    if let Some(list) = &n.any_of {
        ops.push(Op::AnyOf(Box::new(Branches::new(
            list.iter().map(|&c| child(c)).collect(),
            n.any_of_discriminator.clone(),
        ))));
    }
    if let Some(list) = &n.one_of {
        ops.push(Op::OneOf(Box::new(Branches::new(
            list.iter().map(|&c| child(c)).collect(),
            n.one_of_discriminator.clone(),
        ))));
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

    let types = if n.has_type { n.type_mask } else { ANY };
    if values.is_empty()
        && number.is_empty()
        && string.is_empty()
        && object.is_none()
        && array.is_none()
        && ops.is_empty()
    {
        return Plan { types, guard, body: None };
    }
    let body = Body {
        fused: None,
        values: values.into_boxed_slice(),
        number: number.into_boxed_slice(),
        string: string.into_boxed_slice(),
        object,
        array,
        apply: ops.into_boxed_slice(),
    };
    Plan { types, guard, body: Some(Box::new(body)) }
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
        (c.types == ANY || type_ok(c.types, x))
            && (c.trivial || self.p.plans[c.id as usize].body.as_deref().is_none_or(|b| self.run_body(b, x)))
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

    /// An in-place child with its type check inline.
    #[inline(always)]
    fn run_branch(&mut self, c: Child, x: &Value) -> bool {
        (c.types == ANY || type_ok(c.types, x)) && (c.trivial || self.run_in_place(c.id, x))
    }

    /// The anyOf/oneOf branches that can match: those a discriminator selects, or those admitting the instance type.
    #[inline]
    fn candidates<'b>(&self, b: &'b Branches, x: &Value) -> &'b [u32] {
        match select(b.discriminator.as_deref(), x) {
            Selection::All => &b.by_kind[kind(x)],
            Selection::None => &[],
            Selection::Subset(s) => s,
        }
    }

    fn run_body(&mut self, b: &Body, x: &Value) -> bool {
        if let (Some(f), Value::Object(o)) = (&b.fused, x) {
            return self.run_fused(f, o);
        }
        for op in b.values.iter() {
            if !self.run_op(op, x) {
                return false;
            }
        }
        let ok = match x {
            Value::Number(n) => b.number.is_empty() || run_number(&b.number, n),
            Value::String(s) => b.string.is_empty() || run_string(&b.string, s),
            Value::Object(o) => b.object.as_ref().is_none_or(|plan| self.run_object(plan, o, x)),
            Value::Array(a) => b.array.as_ref().is_none_or(|plan| self.run_array(plan, a)),
            _ => true,
        };
        if !ok {
            return false;
        }
        for op in b.apply.iter() {
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
                Value::String(s) => values.iter().any(|v| str_eq(v, s)),
                _ => false,
            },
            Op::Enum(values) => values.iter().any(|v| json_equal(x, v)),
            Op::Ref(r) => self.run_in_place(*r, x),
            Op::AllOf(list) => list.iter().all(|&c| self.run_branch(c, x)),
            Op::AnyOf(b) => self.candidates(b, x).iter().any(|&i| self.run_branch(b.children[i as usize], x)),
            Op::OneOf(b) => {
                let mut matched = 0;
                for &i in self.candidates(b, x) {
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
            Op::If { cond, then, else_ } => {
                let next = if self.run_in_place(*cond, x) { then } else { else_ };
                next.is_none_or(|c| self.run_in_place(c, x))
            }
            Op::General(id) => self.eval_node::<Fast>(*id, x, None),
        }
    }

    fn run_object(&mut self, plan: &ObjectPlan, o: &Map<String, Value>, x: &Value) -> bool {
        let len = o.len() as u64;
        if len < plan.min || len > plan.max {
            return false;
        }
        let mut seen = 0u64;
        match plan.visit {
            Visit::None => {}
            Visit::Values => {
                let c = plan.additional.unwrap();
                for v in o.values() {
                    if !self.run_child(c, v) {
                        return false;
                    }
                }
            }
            Visit::Names => {
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
            }
            Visit::General => {
                let mut hint = 0;
                for (k, v) in o {
                    let mut matched = false;
                    if let Some(i) = plan.names.find_from(k, &mut hint) {
                        matched = true;
                        seen |= 1 << (i & 63);
                        if !self.run_child(plan.children[i], v) {
                            return false;
                        }
                    }
                    for (pattern, c) in plan.patterns.iter() {
                        if pattern.is_match(k) {
                            matched = true;
                            if !self.run_child(*c, v) {
                                return false;
                            }
                        }
                    }
                    if !matched
                        && let Some(c) = plan.additional
                        && !self.run_child(c, v)
                    {
                        return false;
                    }
                    if let Some(pn) = plan.property_names {
                        if !self.run(pn, &Value::String(k.clone())) {
                            return false;
                        }
                    }
                }
            }
        }
        if seen & plan.required_mask != plan.required_mask {
            return false;
        }
        if !plan.required.iter().all(|r| has_key(o, r)) {
            return false;
        }
        for d in plan.dependencies.iter() {
            if !has_key(o, &d.name) {
                continue;
            }
            if !d.required.iter().all(|r| has_key(o, r)) {
                return false;
            }
            if let Some(s) = d.schema {
                if !self.run_in_place(s, x) {
                    return false;
                }
            }
        }
        true
    }

    fn run_array(&mut self, plan: &ArrayPlan, a: &[Value]) -> bool {
        let len = a.len() as u64;
        if len < plan.min || len > plan.max {
            return false;
        }
        let prefix = plan.prefix.len().min(a.len());
        for (c, item) in plan.prefix.iter().zip(a) {
            if !self.run_child(*c, item) {
                return false;
            }
        }
        if let Some(items) = plan.items {
            for item in &a[prefix..] {
                if !self.run_child(items, item) {
                    return false;
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

#[inline(never)]
fn run_number(ops: &[NumberOp], n: &Number) -> bool {
    let v = Num::of(n);
    ops.iter().all(|op| match op {
        NumberOp::Minimum(b) => cmp(v, *b).is_ge(),
        NumberOp::Maximum(b) => cmp(v, *b).is_le(),
        NumberOp::ExclusiveMinimum(b) => cmp(v, *b).is_gt(),
        NumberOp::ExclusiveMaximum(b) => cmp(v, *b).is_lt(),
        NumberOp::MultipleOf(d) => multiple_of(n, d),
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
    let chars = code_points(s);
    chars >= min && chars <= max
}

#[cfg(test)]
mod tests {
    use super::*;

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
