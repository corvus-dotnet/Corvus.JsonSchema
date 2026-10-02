//! The fused object plan: one pass over an object's properties for a schema whose object semantics are spread over
//! in-place applicators (`$ref`, `allOf`, `if`/`then`/`else`, dependencies, required-only `oneOf`/`anyOf`) and
//! possibly finished by `unevaluatedProperties`. A port of the C# evaluator's `FusedObjects`.
//!
//! The general path evaluates each applicator branch as a separate pass over the instance, each with its own
//! property lookups, and then walks the instance once more for `unevaluatedProperties`. The fused plan resolves every
//! property name known to any branch at compile time to the list of child schemas that apply to it (a branch's own
//! property schema, its matching pattern-property schemas, or its `additionalProperties` when neither matched), so
//! evaluation is one lookup per instance property, and it tracks which properties some branch covered so that the
//! unevaluated check needs no second analysis.
//!
//! Branches under `then`/`else` (or a dependency's schema) apply only when their condition holds; the plan supports
//! conditions the pass itself decides (required names, property values tested against constants or a pattern, and
//! names no property may match),
//! and defers those branches' applications to a second step over the properties they touch.
//!
//! Fail-fast evaluation only. The fused pass does not enter its contributors as nodes (and so would not push their
//! resources on the dynamic scope): below a live dynamic reference, only contributors in the node's own resource fuse.

use std::sync::Arc;

use serde_json::Value;

use super::{Child, Evaluator, LOOKUP_NAMES, NO_CHILD, Names, ObjectPlan, Program, Visit};
use crate::eval::{json_equal, matches_type};
use crate::instance::{Instance, ObjectView, View};
use crate::node::*;
use crate::numbers::Num;
use crate::pattern::Pattern;

const MAX_NAMES: usize = 256;
const MAX_CONDITIONS: usize = 64;
const MAX_CONTRIBUTORS: usize = 64;
const MAX_ALT_GROUPS: usize = 8;

pub(crate) struct FusedObject {
    /// The branches merge into one strict object loop: every one applies unconditionally with declared properties and
    /// required names only (and count bounds), and each name resolves to one schema.
    flat: Option<Box<ObjectPlan>>,
    /// Every property name any branch or condition knows, to its entry.
    names: Names,
    entries: Box<[Entry]>,
    /// The branches, the node itself first, then the required lists of dependencies.
    contributors: Box<[Contributor]>,
    conditions: Box<[Condition]>,
    /// Required-only `oneOf`/`anyOf` keywords, decided from the seen names after the pass.
    alternatives: Box<[Alternative]>,
    /// `oneOf`/`anyOf` groups whose branches carry object keywords (each branch is a contributor).
    alt_groups: Box<[AltGroup]>,
    /// `not: {required: [...]}`: names that must not all be present, under a condition.
    forbidden: Box<[(Gate, Box<[u16]>)]>,
    /// Conditions that fail when some property name matches a pattern (an `if` with `patternProperties: {P: false}`),
    /// for names no entry knows; a known name that matches carries a test that never holds.
    absent: Box<[(u16, Arc<Pattern>)]>,
    /// Some branch has pattern properties or additional properties, or some condition absent patterns, so names no
    /// entry knows need resolving.
    resolves_unknown: bool,
    has_count_bounds: bool,
    unevaluated: Option<Child>,
}

struct Entry {
    apps: Box<[App]>,
    /// The conditions' tests on this property's value.
    tests: Box<[ValueTest]>,
    /// The constant tests merged (the C# MergeValueTests): one lookup decides them all.
    merged: Option<Merged>,
}

/// Each constant any of an entry's constant tests allows, with the mask of the tests (by index in `tests`) that allow
/// it. Strings are looked up by name; other constants compared in turn.
struct Merged {
    strings: Names,
    string_masks: Box<[u64]>,
    others: Box<[(Value, u64)]>,
    /// The tests that are constant sets; the others (patterns) are tested one by one.
    keyed: u64,
}

impl Merged {
    /// The mask of the constant tests that allow the value.
    #[inline]
    fn allowed<'x, I: Instance<'x>>(&self, v: I) -> u64 {
        match v.view() {
            View::String(s) => self.strings.contains_at(s).map_or(0, |i| self.string_masks[i]),
            _ => self.others.iter().find(|(m, _)| json_equal(m, v)).map_or(0, |&(_, mask)| mask),
        }
    }
}

/// One child schema applying to a property on behalf of a contributor (`None`: `true`, which covers without a test).
struct App {
    contributor: u16,
    child: Option<Child>,
    /// Other contributors whose resolution of the property is the same test: applied once when any is active.
    others: Box<[u16]>,
}

struct Contributor {
    /// The condition and the polarity under which it applies (`None`: always).
    condition: Option<(u16, bool)>,
    /// The alternative group and the branch in it.
    alt: Option<(u16, u16)>,
    patterns: Box<[(Arc<Pattern>, Option<Child>)]>,
    /// additionalProperties (`Some(None)`: `true`).
    additional: Option<Option<Child>>,
    required: Box<[u16]>,
    min: Option<u64>,
    max: Option<u64>,
}

/// An `if` the pass decides (required names and value tests), or the presence of a dependency's property.
struct Condition {
    required: Box<[u16]>,
    /// The enclosing condition and the polarity under which this one is reached.
    gate: Option<(u16, bool)>,
}

/// A condition's test on a property's value: absent passes, present must hold.
struct ValueTest {
    condition: u16,
    kind: TestKind,
}

enum TestKind {
    /// One of these constants (strings, integers, booleans, null); none: the property must be absent.
    Allowed(Box<[Value]>),
    Pattern {
        pattern: Arc<Pattern>,
        requires_string: bool,
    },
}

impl ValueTest {
    fn holds<'x, I: Instance<'x>>(&self, v: I) -> bool {
        match &self.kind {
            TestKind::Allowed(values) => values.iter().any(|a| json_equal(a, v)),
            TestKind::Pattern { pattern, requires_string } => match v.view() {
                View::String(s) => pattern.is_match(s),
                _ => !requires_string,
            },
        }
    }
}

struct Alternative {
    condition: Option<(u16, bool)>,
    exactly_one: bool,
    branches: Box<[Box<[u16]>]>,
}

struct AltGroup {
    exactly_one: bool,
    count: u32,
}

// ---------------------------------------------------------------------------------------------------------------------
// Construction

/// A condition and the polarity under which something applies.
type Gate = Option<(u16, bool)>;

/// A contributor as collected: the branch, its condition, its alternative group and branch.
type Collected = (NodeId, Gate, Option<(u16, u16)>);

/// What a collection gathers.
struct Collect<'a> {
    p: &'a Program,
    contributors: Vec<Collected>,
    conditions: Vec<Pending>,
    /// Dependencies' required names: (condition, names).
    extras: Vec<(u16, Vec<String>)>,
    alternatives: Vec<(Gate, bool, Vec<Vec<String>>)>,
    alt_groups: Vec<(bool, u32)>,
    forbidden: Vec<(Gate, Vec<String>)>,
    /// Alternative groups with object keywords only where coverage is not tracked (a failed branch must not cover).
    allow_alt_groups: bool,
    /// Every contributor must belong to this resource (below a live dynamic reference, where the general path would
    /// push a contributor's own resource on the dynamic scope and the fused pass does not enter contributors).
    resource: Option<u32>,
}

struct Pending {
    /// The `if` schema, or the dependency's property name.
    test: Result<NodeId, String>,
    gate: Option<(u16, bool)>,
}

impl<'a> Collect<'a> {
    fn node(&self, id: NodeId) -> &'a SchemaNode {
        &self.p.nodes[id as usize]
    }

    fn target(&self, id: NodeId) -> NodeId {
        self.p.fast_target[id as usize]
    }

    /// Walks the in-place applicators of a branch, adding every object branch with its condition. Fails when a branch
    /// cannot be fused.
    fn collect(&mut self, id: NodeId, condition: Option<(u16, bool)>) -> bool {
        let n = self.node(id);
        if n.always_true {
            return true;
        }
        if n.always_false || n.in_place_cycle || !is_object_branch(self.p, n, self.contributors.is_empty()) {
            return false;
        }
        if self.contributors.len() >= MAX_CONTRIBUTORS || self.resource.is_some_and(|r| r != n.resource_id) {
            return false;
        }
        self.contributors.push((id, condition, None));
        if let Some(not) = n.not {
            self.forbidden.push((condition, forbidden_names(self.p, not).unwrap().to_vec()));
        }
        for r in [n.ref_, n.static_dynamic_ref].into_iter().flatten() {
            if !self.collect(self.target(r), condition) {
                return false;
            }
        }
        for &c in n.all_of.iter().flatten() {
            if !self.collect(self.target(c), condition) {
                return false;
            }
        }
        if let Some(list) = &n.one_of {
            let discriminated = n.one_of_discriminator.is_some() || self.type_union(list);
            if !self.collect_alternative(list, condition, true, discriminated) {
                return false;
            }
        }
        if let Some(list) = &n.any_of {
            let discriminated = n.any_of_discriminator.is_some() || self.type_union(list);
            if !self.collect_alternative(list, condition, false, discriminated) {
                return false;
            }
        }
        for d in n.dependencies.iter().flatten() {
            if self.conditions.len() >= MAX_CONDITIONS {
                return false;
            }
            let at = self.conditions.len() as u16;
            self.conditions.push(Pending { test: Err(d.name.clone()), gate: condition });
            if let Some(required) = &d.required
                && !required.is_empty()
            {
                self.extras.push((at, required.clone()));
            }
            if let Some(schema) = d.schema
                && !self.collect(self.target(schema), Some((at, true)))
            {
                return false;
            }
        }
        if let Some(cond) = n.if_ {
            let test_id = self.target(cond);
            let test = self.node(test_id);
            let (then, else_) = (n.then.map(|t| self.target(t)), n.else_.map(|e| self.target(e)));
            if test.always_true {
                return then.is_none_or(|t| self.collect(t, condition));
            }
            if test.always_false {
                return else_.is_none_or(|e| self.collect(e, condition));
            }
            if !self.supported_condition(test) || self.conditions.len() >= MAX_CONDITIONS {
                return false;
            }
            let at = self.conditions.len() as u16;
            self.conditions.push(Pending { test: Ok(test_id), gate: condition });
            // When the condition holds, the if schema's own properties count as evaluated, so it contributes under
            // the same condition as then.
            if test.properties.is_some() && !self.collect(test_id, Some((at, true))) {
                return false;
            }
            if let Some(t) = then
                && !self.collect(t, Some((at, true)))
            {
                return false;
            }
            if let Some(e) = else_
                && !self.collect(e, Some((at, false)))
            {
                return false;
            }
        }
        true
    }

    /// Every branch only tests the type (the general path decides the keyword by one mask test).
    fn type_union(&self, list: &[NodeId]) -> bool {
        list.iter().all(|&c| {
            let b = self.node(self.target(c));
            b.has_type && is_type_only(b)
        })
    }

    /// A `oneOf`/`anyOf` fuses when every branch is a plain `required` list (decided from the seen names after the
    /// pass), or as an alternative group of object branches where coverage is not tracked.
    fn collect_alternative(
        &mut self,
        list: &[NodeId],
        condition: Option<(u16, bool)>,
        exactly_one: bool,
        discriminated: bool,
    ) -> bool {
        let branches: Vec<NodeId> = list.iter().map(|&c| self.target(c)).collect();
        let required_only: Option<Vec<Vec<String>>> = branches
            .iter()
            .map(|&b| {
                let n = self.node(b);
                n.required.clone().filter(|r| !r.is_empty() && is_required_list_only(n))
            })
            .collect();
        if let Some(required) = required_only {
            self.alternatives.push((condition, exactly_one, required));
            return true;
        }
        // Branches with object keywords: each a contributor whose failure marks the branch rather than the object.
        // The pass applies every branch that knows a name to that property, where the general path stops at the
        // first branch that passes, so a name with an expensive child in more than one branch is refused; and a
        // keyword the general path decides by discriminator or type stays with it.
        if !self.allow_alt_groups
            || discriminated
            || condition.is_some()
            || self.alt_groups.len() >= MAX_ALT_GROUPS
            || branches.len() > 64
            || !self.expensive_children_disjoint(&branches)
        {
            return false;
        }
        let group = self.alt_groups.len() as u16;
        for (i, &b) in branches.iter().enumerate() {
            let n = self.node(b);
            if n.always_true
                || n.always_false
                || n.in_place_cycle
                || n.has_in_place_applicators()
                || n.dependencies.is_some()
                || n.not.is_some()
                || !is_object_branch(self.p, n, false)
                || self.resource.is_some_and(|r| r != n.resource_id)
                || self.contributors.len() >= MAX_CONTRIBUTORS
            {
                return false;
            }
            self.contributors.push((b, None, Some((group, i as u16))));
        }
        self.alt_groups.push((exactly_one, branches.len() as u32));
        true
    }

    /// Whether no property name gets an expensive child (anything but a leaf or a boolean schema) from more than one
    /// branch; pattern and additional properties count as every name.
    fn expensive_children_disjoint(&self, branches: &[NodeId]) -> bool {
        let cheap = |id: NodeId| {
            let n = self.node(self.target(id));
            n.always_true || n.always_false || is_leaf(n) || self.is_simple_array(n)
        };
        let mut expensive: Vec<&str> = Vec::new();
        let mut wildcards = 0;
        for &b in branches {
            let n = self.node(b);
            for (name, c) in n.properties.iter().flatten() {
                if !cheap(*c) {
                    if expensive.contains(&name.as_str()) {
                        return false;
                    }
                    expensive.push(name);
                }
            }
            let wildcard = n.pattern_properties.iter().flatten().any(|pp| !cheap(pp.node))
                || n.additional_properties.is_some_and(|a| !cheap(a));
            if wildcard {
                wildcards += 1;
                if wildcards > 1 {
                    return false;
                }
            }
        }
        wildcards == 0 || expensive.is_empty()
    }

    /// An array of leaf items with at most size bounds (C#'s IsSimpleArray): cheap to apply more than once.
    fn is_simple_array(&self, n: &SchemaNode) -> bool {
        n.has_array_keywords()
            && !n.has_object_keywords()
            && !n.has_in_place_applicators()
            && n.dynamic_ref.is_none()
            && n.const_value.is_none()
            && n.enum_values.is_none()
            && n.prefix_items.is_none()
            && n.contains.is_none()
            && !n.unique_items
            && n.unevaluated_items.is_none()
            && n.items.is_some_and(|i| {
                let item = self.node(self.target(i));
                item.always_true || is_leaf(item)
            })
    }

    /// A condition the pass decides alone: required names, and properties whose schemas are value tests, optionally
    /// with `type: object` (which the object plan has established). At least one of the two.
    fn supported_condition(&self, test: &SchemaNode) -> bool {
        let mut any = test.required.as_ref().is_some_and(|r| !r.is_empty());
        if test.const_value.is_some()
            || test.enum_values.is_some()
            || test.has_number_keywords()
            || test.has_string_keywords()
            || test.has_array_keywords()
            || test.has_in_place_applicators()
            || test.dynamic_ref.is_some()
            || (test.has_type && test.type_mask & type_mask::OBJECT == 0)
            || test.additional_properties.is_some()
            || test.property_names.is_some()
            || test.unevaluated_properties.is_some()
            || test.dependencies.is_some()
            || test.min_properties.is_some()
            || test.max_properties.is_some()
        {
            return false;
        }
        for (_, c) in test.properties.iter().flatten() {
            if value_test(self.node(self.target(*c))).is_none() {
                return false;
            }
            any = true;
        }
        // `patternProperties: {P: false}`: no property name may match P.
        for pp in test.pattern_properties.iter().flatten() {
            if !self.node(self.target(pp.node)).always_false {
                return false;
            }
            any = true;
        }
        any
    }
}

/// A branch whose only effect on an object instance is through object keywords and fusable in-place applicators.
fn is_object_branch(p: &Program, n: &SchemaNode, allow_unevaluated: bool) -> bool {
    !(n.const_value.is_some()
        || n.enum_values.is_some()
        || n.has_number_keywords()
        || n.has_string_keywords()
        || (n.has_type && n.type_mask & type_mask::OBJECT == 0)
        || n.not.is_some_and(|t| forbidden_names(p, t).is_none())
        || n.property_names.is_some()
        || n.dynamic_ref.is_some()
        || (!allow_unevaluated && n.unevaluated_properties.is_some()))
}

fn is_type_only(n: &SchemaNode) -> bool {
    is_leaf(n)
        && n.const_value.is_none()
        && n.enum_values.is_none()
        && !n.has_number_keywords()
        && !n.has_string_keywords()
}

/// Only local keywords (type, const, enum, number and string keywords).
fn is_leaf(n: &SchemaNode) -> bool {
    !n.always_true
        && !n.always_false
        && !n.has_object_keywords()
        && !n.has_array_keywords()
        && !n.has_in_place_applicators()
        && n.dynamic_ref.is_none()
}

/// The names of a `not` whose schema is a non-empty `required` list: an object fails when all are present.
fn forbidden_names(p: &Program, not: NodeId) -> Option<&[String]> {
    let n = &p.nodes[p.fast_target[not as usize] as usize];
    if n.always_true || n.always_false || !is_required_list_only(n) {
        return None;
    }
    n.required.as_deref().filter(|r| !r.is_empty())
}

fn is_required_list_only(n: &SchemaNode) -> bool {
    !(n.const_value.is_some()
        || n.enum_values.is_some()
        || n.has_number_keywords()
        || n.has_string_keywords()
        || n.has_array_keywords()
        || n.has_in_place_applicators()
        || n.dynamic_ref.is_some()
        || n.unevaluated_properties.is_some()
        || n.unevaluated_items.is_some()
        || (n.has_type && n.type_mask & type_mask::OBJECT == 0)
        || n.pattern_properties.is_some()
        || n.additional_properties.is_some()
        || n.property_names.is_some()
        || n.dependencies.is_some()
        || n.min_properties.is_some()
        || n.max_properties.is_some()
        || n.properties.as_ref().is_some_and(|p| !p.is_empty()))
}

/// The value test for a property schema inside an `if`: `const`/`enum` of scalars (the values the schema's `type`
/// admits), or a `pattern` with at most `type: string`; `None` for anything else.
fn value_test(n: &SchemaNode) -> Option<TestKind> {
    if n.always_true
        || n.always_false
        || n.has_number_keywords()
        || n.has_object_keywords()
        || n.has_array_keywords()
        || n.has_in_place_applicators()
        || n.dynamic_ref.is_some()
    {
        return None;
    }
    if let Some(pattern) = &n.pattern {
        let only_pattern = n.const_value.is_none()
            && n.enum_values.is_none()
            && n.min_length.is_none()
            && n.max_length.is_none()
            && !(n.assert_format && n.format.is_some())
            && !n.assert_content
            && (!n.has_type || n.type_mask == type_mask::STRING);
        return only_pattern.then(|| TestKind::Pattern { pattern: pattern.clone(), requires_string: n.has_type });
    }
    if n.has_string_keywords() {
        return None;
    }
    let values: Vec<Value> = match (&n.const_value, &n.enum_values) {
        (Some(c), _) => vec![c.clone()],
        (None, Some(e)) if !e.is_empty() => e.clone(),
        _ => return None,
    };
    let scalar = |v: &Value| match v {
        Value::Number(x) => matches!(Num::of(x), Num::I(_)),
        Value::String(_) | Value::Bool(_) | Value::Null => true,
        _ => false,
    };
    if !values.iter().all(scalar) {
        return None;
    }
    // A value the schema's type rejects never passes, so it is not allowed.
    let allowed = values.into_iter().filter(|v| !n.has_type || matches_type(n.type_mask, v)).collect();
    Some(TestKind::Allowed(allowed))
}

/// Builds the fused plan for a node, or `None` when it cannot be fused or fusing does not pay.
pub(super) fn try_fuse(
    p: &Program,
    id: NodeId,
    same_resource: bool,
    child: &dyn Fn(NodeId) -> Child,
) -> Option<FusedObject> {
    let n = &p.nodes[id as usize];
    if !is_object_branch(p, n, true) || n.in_place_cycle || n.always_true || n.always_false {
        return None;
    }
    let mut ctx = Collect {
        p,
        contributors: Vec::new(),
        conditions: Vec::new(),
        extras: Vec::new(),
        alternatives: Vec::new(),
        alt_groups: Vec::new(),
        forbidden: Vec::new(),
        allow_alt_groups: n.unevaluated_properties.is_none(),
        resource: same_resource.then_some(n.resource_id),
    };
    if !ctx.collect(id, None) {
        return None;
    }
    if ctx.contributors.len() + ctx.extras.len() > MAX_CONTRIBUTORS || ctx.conditions.len() > MAX_CONDITIONS {
        return None;
    }

    // Fusing pays when a pass over every property is unavoidable (unevaluatedProperties) or when it replaces several
    // passes: two or more branches with object keywords, an if the seen names decide, or alternatives. A node whose
    // object keywords are all its own keeps its object plan.
    let has_if = ctx.conditions.iter().any(|c| c.test.is_ok());
    if n.unevaluated_properties.is_none() && !has_if && ctx.alternatives.is_empty() && ctx.alt_groups.is_empty() {
        let effective = ctx
            .contributors
            .iter()
            .filter(|&&(b, ..)| {
                let b = &p.nodes[b as usize];
                b.properties.is_some()
                    || b.pattern_properties.is_some()
                    || b.additional_properties.is_some()
                    || b.required.as_ref().is_some_and(|r| !r.is_empty())
                    || b.min_properties.is_some()
                    || b.max_properties.is_some()
            })
            .count();
        if effective < 2 {
            return None;
        }
    }

    // Every name any branch or condition knows gets an index.
    let mut names: Vec<String> = Vec::new();
    let mut bit = |name: &str| -> u16 {
        match names.iter().position(|n| n == name) {
            Some(i) => i as u16,
            None => {
                names.push(name.to_string());
                (names.len() - 1) as u16
            }
        }
    };
    for &(b, ..) in &ctx.contributors {
        let b = &p.nodes[b as usize];
        for (name, _) in b.properties.iter().flatten() {
            bit(name);
        }
        for name in b.required.iter().flatten() {
            bit(name);
        }
    }
    let mut conditions = Vec::new();
    let mut tests_by_entry: Vec<(u16, ValueTest)> = Vec::new();
    let mut absent: Vec<(u16, Arc<Pattern>)> = Vec::new();
    for (i, pending) in ctx.conditions.iter().enumerate() {
        let required: Vec<u16> = match &pending.test {
            Ok(test) => {
                let test = &p.nodes[*test as usize];
                absent.extend(test.pattern_properties.iter().flatten().map(|pp| (i as u16, pp.pattern.clone())));
                for (name, c) in test.properties.iter().flatten() {
                    let kind = value_test(&p.nodes[p.fast_target[*c as usize] as usize]).unwrap();
                    tests_by_entry.push((bit(name), ValueTest { condition: i as u16, kind }));
                }
                test.required.iter().flatten().map(|r| bit(r)).collect()
            }
            Err(dependency) => vec![bit(dependency)],
        };
        conditions.push(Condition { required: required.into_boxed_slice(), gate: pending.gate });
    }
    let forbidden: Vec<(Gate, Box<[u16]>)> =
        ctx.forbidden.iter().map(|(gate, names)| (*gate, names.iter().map(|n| bit(n)).collect())).collect();
    let extras: Vec<(u16, Vec<u16>)> =
        ctx.extras.iter().map(|(c, names)| (*c, names.iter().map(|n| bit(n)).collect())).collect();
    let alternatives: Vec<Alternative> = ctx
        .alternatives
        .iter()
        .map(|(condition, exactly_one, branches)| Alternative {
            condition: *condition,
            exactly_one: *exactly_one,
            branches: branches.iter().map(|b| b.iter().map(|n| bit(n)).collect()).collect(),
        })
        .collect();
    if names.len() > MAX_NAMES {
        return None;
    }

    let app_child = |c: NodeId| -> Option<Child> {
        let t = p.fast_target[c as usize];
        if p.nodes[t as usize].always_true { None } else { Some(child(t)) }
    };
    let mut contributors: Vec<Contributor> = ctx
        .contributors
        .iter()
        .map(|&(b, condition, alt)| {
            let b = &p.nodes[b as usize];
            Contributor {
                condition,
                alt,
                patterns: b
                    .pattern_properties
                    .iter()
                    .flatten()
                    .map(|pp| (pp.pattern.clone(), app_child(pp.node)))
                    .collect(),
                additional: b.additional_properties.map(app_child),
                required: b
                    .required
                    .iter()
                    .flatten()
                    .map(|r| names.iter().position(|n| n == r).unwrap() as u16)
                    .collect(),
                min: b.min_properties,
                max: b.max_properties,
            }
        })
        .collect();
    let branch_count = contributors.len();
    for (condition, required) in extras {
        contributors.push(Contributor {
            condition: Some((condition, true)),
            alt: None,
            patterns: Box::new([]),
            additional: None,
            required: required.into_boxed_slice(),
            min: None,
            max: None,
        });
    }

    let mut tests: Vec<Vec<ValueTest>> = (0..names.len()).map(|_| Vec::new()).collect();
    for (e, test) in tests_by_entry {
        tests[e as usize].push(test);
    }
    // A known name matching an absent pattern fails its condition whatever its value: no constant is allowed.
    for (e, name) in names.iter().enumerate() {
        for (condition, pattern) in &absent {
            if pattern.is_match(name) {
                tests[e].push(ValueTest { condition: *condition, kind: TestKind::Allowed(Box::new([])) });
            }
        }
    }

    // Resolve every known name against every branch now.
    let mut entries = Vec::with_capacity(names.len());
    for (i, name) in names.iter().enumerate() {
        let mut apps: Vec<(u16, Option<Child>, Option<NodeId>)> = Vec::new();
        for (c, &(b, ..)) in ctx.contributors.iter().enumerate() {
            let b = &p.nodes[b as usize];
            let mut matched = false;
            if let Some(&(_, schema)) = b.properties.iter().flatten().find(|(k, _)| k == name) {
                matched = true;
                apps.push((c as u16, app_child(schema), Some(p.fast_target[schema as usize])));
            }
            for pp in b.pattern_properties.iter().flatten() {
                if pp.pattern.is_match(name) {
                    matched = true;
                    apps.push((c as u16, app_child(pp.node), Some(p.fast_target[pp.node as usize])));
                }
            }
            if !matched && let Some(a) = b.additional_properties {
                apps.push((c as u16, app_child(a), Some(p.fast_target[a as usize])));
            }
        }
        let apps = coalesce(apps, &contributors);
        let entry_tests = std::mem::take(&mut tests[i]);
        let merged = merge_value_tests(&entry_tests);
        entries.push(Entry { apps: apps.into_boxed_slice(), tests: entry_tests.into_boxed_slice(), merged });
    }
    debug_assert_eq!(branch_count + ctx.extras.len(), contributors.len());

    let flat = (conditions.is_empty()
        && alternatives.is_empty()
        && ctx.alt_groups.is_empty()
        && forbidden.is_empty()
        && n.unevaluated_properties.is_none()
        && names.len() <= 64
        && contributors.iter().all(|c| c.condition.is_none() && c.patterns.is_empty() && c.additional.is_none())
        && entries.iter().all(|e| e.apps.len() <= 1 && e.tests.is_empty()))
    .then(|| {
        let names: Names = Names::new(names.iter().map(|n| n.as_str().into()).collect());
        let required_mask = contributors.iter().flat_map(|c| c.required.iter()).fold(0u64, |m, &i| m | 1 << i);
        Box::new(ObjectPlan {
            min: contributors.iter().filter_map(|c| c.min).max().unwrap_or(0),
            max: contributors.iter().filter_map(|c| c.max).min().unwrap_or(u64::MAX),
            visit: Visit::Names,
            declared: entries.len(),
            children: entries.iter().map(|e| e.apps.first().and_then(|a| a.child).unwrap_or(NO_CHILD)).collect(),
            required_mask,
            required: Box::new([]),
            patterns: Box::new([]),
            name_patterns: entries.iter().map(|_| Box::default()).collect(),
            additional: None,
            property_names: None,
            dependencies: Box::new([]),
            rest_free: true,
            strict: true,
            // No contributor has additionalProperties (`flat` requires it).
            lookup: names.map.names.len() <= LOOKUP_NAMES,
            names,
        })
    });

    Some(FusedObject {
        flat,
        names: Names::new(names.into_iter().map(Into::into).collect()),
        entries: entries.into_boxed_slice(),
        resolves_unknown: !absent.is_empty()
            || contributors.iter().any(|c| !c.patterns.is_empty() || c.additional.is_some()),
        has_count_bounds: contributors.iter().any(|c| c.min.is_some() || c.max.is_some()),
        contributors: contributors.into_boxed_slice(),
        conditions: conditions.into_boxed_slice(),
        alternatives: alternatives.into_boxed_slice(),
        alt_groups: ctx.alt_groups.iter().map(|&(exactly_one, count)| AltGroup { exactly_one, count }).collect(),
        forbidden: forbidden.into_boxed_slice(),
        absent: absent.into_boxed_slice(),
        unevaluated: n.unevaluated_properties.map(|u| child(p.fast_target[u as usize])),
    })
}

/// The merged constants of an entry's value tests, when some (of at most 64) are constant sets and there is more than
/// one constant to look for.
fn merge_value_tests(tests: &[ValueTest]) -> Option<Merged> {
    let constants: usize =
        tests.iter().map(|t| if let TestKind::Allowed(values) = &t.kind { values.len() } else { 0 }).sum();
    if tests.len() > 64 || constants < 2 {
        return None;
    }
    let (mut strings, mut string_masks): (Vec<Box<str>>, Vec<u64>) = (Vec::new(), Vec::new());
    let mut others: Vec<(Value, u64)> = Vec::new();
    let mut keyed = 0;
    for (t, test) in tests.iter().enumerate() {
        let TestKind::Allowed(values) = &test.kind else { continue };
        keyed |= 1 << t;
        for v in values.iter() {
            if let Value::String(s) = v {
                match strings.iter().position(|m| **m == **s) {
                    Some(i) => string_masks[i] |= 1 << t,
                    None => {
                        strings.push(s.as_str().into());
                        string_masks.push(1 << t);
                    }
                }
            } else {
                match others.iter_mut().find(|(m, _)| json_equal(m, v)) {
                    Some((_, mask)) => *mask |= 1 << t,
                    None => others.push((v.clone(), 1 << t)),
                }
            }
        }
    }
    Some(Merged { strings: Names::new(strings), string_masks: string_masks.into(), others: others.into(), keyed })
}

/// Identical resolutions of a property from several branches (the same child, or both `true`) become one application
/// listing every branch, applied once when any of them is active. Branches of an alternative group merge only within
/// the same branch. The primary contributor is an unconditional one when there is one, so the pass applies it at
/// once.
fn coalesce(apps: Vec<(u16, Option<Child>, Option<NodeId>)>, contributors: &[Contributor]) -> Vec<App> {
    let same = |a: &(u16, Option<Child>, Option<NodeId>), b: &(u16, Option<Child>, Option<NodeId>)| match (&a.1, &b.1) {
        (None, None) => true,
        (Some(x), Some(y)) => a.2 == b.2 || (x.trivial() && y.trivial() && x.types == y.types),
        _ => false,
    };
    let mut used = vec![false; apps.len()];
    let mut out = Vec::new();
    for i in 0..apps.len() {
        if used[i] {
            continue;
        }
        let mut primary = i;
        let mut others = Vec::new();
        for j in i + 1..apps.len() {
            let (a, b) = (&contributors[apps[primary].0 as usize], &contributors[apps[j].0 as usize]);
            if used[j] || !same(&apps[primary], &apps[j]) || a.alt != b.alt {
                continue;
            }
            used[j] = true;
            if a.condition.is_some() && b.condition.is_none() {
                others.push(apps[primary].0);
                primary = j;
            } else {
                others.push(apps[j].0);
            }
        }
        out.push(App { contributor: apps[primary].0, child: apps[primary].1, others: others.into_boxed_slice() });
    }
    out
}

// ---------------------------------------------------------------------------------------------------------------------
// Evaluation

/// Set bits over at most `MAX_NAMES` entries.
#[derive(Default)]
struct Seen([u64; MAX_NAMES / 64]);

impl Seen {
    #[inline]
    fn set(&mut self, i: u16) {
        self.0[i as usize >> 6] |= 1 << (i & 63);
    }

    #[inline]
    fn all(&self, bits: &[u16]) -> bool {
        bits.iter().all(|&i| self.0[i as usize >> 6] & (1 << (i & 63)) != 0)
    }
}

/// Covered property ordinals (for unevaluatedProperties), inline up to 256 properties.
enum Covered {
    None,
    Inline([u64; 4]),
    Heap(Vec<u64>),
}

impl Covered {
    fn new(track: bool, len: usize) -> Covered {
        match (track, len) {
            (false, _) => Covered::None,
            (true, 0..=256) => Covered::Inline([0; 4]),
            (true, _) => Covered::Heap(vec![0; len.div_ceil(64)]),
        }
    }

    #[inline]
    fn set(&mut self, i: usize) {
        match self {
            Covered::None => {}
            Covered::Inline(w) => w[i >> 6] |= 1 << (i & 63),
            Covered::Heap(w) => w[i >> 6] |= 1 << (i & 63),
        }
    }

    fn get(&self, i: usize) -> bool {
        match self {
            Covered::None => true,
            Covered::Inline(w) => w[i >> 6] & (1 << (i & 63)) != 0,
            Covered::Heap(w) => w[i >> 6] & (1 << (i & 63)) != 0,
        }
    }
}

/// The per-evaluation state of the pass.
struct Pass {
    seen: Seen,
    /// Bit `i`: condition `i`'s value test failed.
    failed: u64,
    alt_failed: [u64; MAX_ALT_GROUPS],
    holds: u64,
    gate_ok: u64,
}

impl Pass {
    #[inline]
    fn active(&self, condition: Option<(u16, bool)>) -> bool {
        match condition {
            None => true,
            Some((c, polarity)) => self.gate_ok & (1 << c) != 0 && (self.holds & (1 << c) != 0) == polarity,
        }
    }
}

enum Outcome {
    Failed,
    /// Whether some branch covered the property, and whether conditional applications are pending.
    Done {
        cover: bool,
        defer: bool,
    },
}

impl Evaluator<'_, '_> {
    #[inline]
    fn apply<'x, I: Instance<'x>>(&mut self, child: Option<Child>, v: I) -> bool {
        child.is_none_or(|c| self.run_child(c, v))
    }

    /// Resolves a name no entry knows against one branch's pattern and additional properties; returns whether it
    /// matched (the property is covered), or `None` when the application failed.
    fn resolve_unknown<'x, I: Instance<'x>>(&mut self, c: &Contributor, name: &str, v: I) -> Option<bool> {
        let mut matched = false;
        for (pattern, child) in c.patterns.iter() {
            if pattern.is_match(name) {
                matched = true;
                if !self.apply(*child, v) {
                    return None;
                }
            }
        }
        if !matched && let Some(child) = c.additional {
            matched = true;
            if !self.apply(child, v) {
                return None;
            }
        }
        Some(matched)
    }

    fn fused_entry<'x, I: Instance<'x>>(&mut self, f: &FusedObject, e: usize, v: I, pass: &mut Pass) -> Outcome {
        let entry = &f.entries[e];
        pass.seen.set(e as u16);
        match &entry.merged {
            Some(merged) => {
                let allowed = merged.allowed(v);
                for (t, test) in entry.tests.iter().enumerate() {
                    let holds = if merged.keyed & (1 << t) != 0 { allowed & (1 << t) != 0 } else { test.holds(v) };
                    if !holds {
                        pass.failed |= 1 << test.condition;
                    }
                }
            }
            None => {
                for test in entry.tests.iter() {
                    if !test.holds(v) {
                        pass.failed |= 1 << test.condition;
                    }
                }
            }
        }
        let (mut cover, mut defer) = (false, false);
        for app in entry.apps.iter() {
            let c = &f.contributors[app.contributor as usize];
            if c.condition.is_some() {
                defer = true;
                continue;
            }
            if !self.apply(app.child, v) {
                match c.alt {
                    None => return Outcome::Failed,
                    Some((group, branch)) => pass.alt_failed[group as usize] |= 1 << branch,
                }
                continue;
            }
            cover = true;
        }
        Outcome::Done { cover, defer }
    }

    fn fused_unknown<'x, I: Instance<'x>>(&mut self, f: &FusedObject, name: &str, v: I, pass: &mut Pass) -> Outcome {
        let (mut cover, mut defer) = (false, false);
        if !f.resolves_unknown {
            return Outcome::Done { cover, defer };
        }
        for (c, pattern) in f.absent.iter() {
            if pass.failed & (1 << c) == 0 && pattern.is_match(name) {
                pass.failed |= 1 << c;
            }
        }
        for c in f.contributors.iter() {
            if c.condition.is_some() {
                defer |= !c.patterns.is_empty() || c.additional.is_some();
                continue;
            }
            match self.resolve_unknown(c, name, v) {
                Some(matched) => cover |= matched,
                None => match c.alt {
                    None => return Outcome::Failed,
                    Some((group, branch)) => pass.alt_failed[group as usize] |= 1 << branch,
                },
            }
        }
        Outcome::Done { cover, defer }
    }

    pub(super) fn run_fused<'x, I: Instance<'x>>(&mut self, f: &FusedObject, o: I::Object) -> bool {
        if let Some(plan) = &f.flat {
            return self.run_strict_object::<I>(plan, o);
        }
        let count = o.len() as u64;
        if f.has_count_bounds {
            for c in f.contributors.iter() {
                if c.condition.is_none() && c.alt.is_none() && !count_ok(c, count) {
                    return false;
                }
            }
        }
        let mut pass = Pass { seen: Seen::default(), failed: 0, alt_failed: [0; MAX_ALT_GROUPS], holds: 0, gate_ok: 0 };
        let mut covered = Covered::new(f.unevaluated.is_some(), o.len());
        // The ordinals of the properties with conditional applications pending: a mask for the first 64, a vector
        // (allocated only for objects that large) for the rest. A vector of the properties themselves cost an
        // allocation and a free per evaluation.
        let mut deferred = 0u64;
        let mut deferred_beyond: Vec<usize> = Vec::new();
        let mut hint = 0;
        for (ordinal, (k, v)) in o.iter().enumerate() {
            let e = f.names.find_from(k, &mut hint);
            let outcome = match e {
                Some(e) => self.fused_entry(f, e, v, &mut pass),
                None => self.fused_unknown(f, k, v, &mut pass),
            };
            match outcome {
                Outcome::Failed => return false,
                Outcome::Done { cover, defer } => {
                    if cover {
                        covered.set(ordinal);
                    }
                    if defer {
                        if ordinal < 64 {
                            deferred |= 1 << ordinal;
                        } else {
                            deferred_beyond.push(ordinal);
                        }
                    }
                }
            }
        }

        // Decide the conditions, then which apply along their gates (a gate precedes the conditions under it).
        for (i, c) in f.conditions.iter().enumerate() {
            if pass.failed & (1 << i) == 0 && pass.seen.all(&c.required) {
                pass.holds |= 1 << i;
            }
        }
        for (i, c) in f.conditions.iter().enumerate() {
            if pass.active(c.gate) {
                pass.gate_ok |= 1 << i;
            }
        }

        let mut pending = deferred.count_ones() as usize + deferred_beyond.len();
        for (ordinal, (name, v)) in o.iter().enumerate() {
            if pending == 0 {
                break;
            }
            let is_deferred = if ordinal < 64 {
                deferred & (1 << ordinal) != 0
            } else {
                deferred_beyond.binary_search(&ordinal).is_ok()
            };
            if !is_deferred {
                continue;
            }
            pending -= 1;
            let e = f.names.find(name);
            let mut cover = false;
            match e {
                Some(e) => {
                    for app in f.entries[e].apps.iter() {
                        let c = &f.contributors[app.contributor as usize];
                        if c.condition.is_none() {
                            continue;
                        }
                        let active = pass.active(c.condition)
                            || app.others.iter().any(|&o| pass.active(f.contributors[o as usize].condition));
                        if active {
                            if !self.apply(app.child, v) {
                                return false;
                            }
                            cover = true;
                        }
                    }
                }
                None => {
                    for c in f.contributors.iter() {
                        if c.condition.is_some() && pass.active(c.condition) {
                            match self.resolve_unknown(c, name, v) {
                                Some(matched) => cover |= matched,
                                None => return false,
                            }
                        }
                    }
                }
            }
            if cover {
                covered.set(ordinal);
            }
        }

        for c in f.contributors.iter() {
            if c.condition.is_some() {
                if !pass.active(c.condition) {
                    continue;
                }
                if !count_ok(c, count) {
                    return false;
                }
            }
            let satisfied = (c.alt.is_none() || count_ok(c, count)) && pass.seen.all(&c.required);
            if !satisfied {
                match c.alt {
                    None => return false,
                    Some((group, branch)) => pass.alt_failed[group as usize] |= 1 << branch,
                }
            }
        }

        for (g, group) in f.alt_groups.iter().enumerate() {
            let all = if group.count == 64 { u64::MAX } else { (1u64 << group.count) - 1 };
            let survivors = !pass.alt_failed[g] & all;
            if survivors == 0 || (group.exactly_one && survivors & (survivors - 1) != 0) {
                return false;
            }
        }

        for (gate, names) in f.forbidden.iter() {
            if pass.active(*gate) && pass.seen.all(names) {
                return false;
            }
        }

        for a in f.alternatives.iter() {
            if a.condition.is_some() && !pass.active(a.condition) {
                continue;
            }
            let matches = a.branches.iter().filter(|b| pass.seen.all(b)).count();
            if matches == 0 || (a.exactly_one && matches != 1) {
                return false;
            }
        }

        if let Some(u) = f.unevaluated {
            for (ordinal, v) in o.values().enumerate() {
                if !covered.get(ordinal) && !self.run_child(u, v) {
                    return false;
                }
            }
        }
        true
    }
}

#[inline]
fn count_ok(c: &Contributor, count: u64) -> bool {
    c.min.is_none_or(|m| count >= m) && c.max.is_none_or(|m| count <= m)
}
