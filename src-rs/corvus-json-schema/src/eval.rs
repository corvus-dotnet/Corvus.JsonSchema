//! Evaluation: one implementation, monomorphised twice (like the C# `Evaluator.Eval<TMode>`). `Fast` has every
//! reporting call compiled away and fails fast at the first violation; `Collect` is exhaustive and reports every
//! keyword to a `JsonSchemaResultsCollector` with evaluation path, schema location and instance location, reproducing
//! the C# collecting mode (keyword order, paths, messages, which subschema results are committed or discarded).

use std::collections::HashMap;
use std::sync::OnceLock;

use serde_json::{Number, Value};

use crate::compiler::{AnnotationSource, collect_annotations};
use crate::dialect::Dialect;
use crate::formats::FormatKind;
use crate::instance::{ArrayView, Instance, Kind, ObjectView, View};
use crate::node::*;
use crate::numbers::{Num, cmp, multiple_of, num_eq, number_text};
use crate::options::FormatValidator;
use crate::results::{JsonSchemaResultsCollector, Message, encode_pointer_segment};
use crate::uri::resolve_pointer;

mod plan;

/// The compiled program an evaluator runs.
pub(crate) struct Program {
    pub nodes: Vec<SchemaNode>,
    pub root: NodeId,
    pub uses_dynamic_scope: bool,
    pub max_depth: u32,
    pub formats: HashMap<String, FormatValidator>,
    /// For fast mode: each node's target after following pure `$ref` hops.
    pub fast_target: Vec<NodeId>,
    /// Name lookup tables for nodes with many properties.
    pub property_maps: Vec<Option<HashMap<String, NodeId>>>,
    #[allow(clippy::vec_box)]
    pub documents: Vec<Box<Value>>,
    pub annotation_sources: Vec<Option<AnnotationSource>>,
    pub annotations: OnceLock<Vec<Option<Vec<AnnotationEntry>>>>,
    pub assert_format_set: bool,
    /// Fail-fast plans (with a dynamic scope kept, bodies push their resource on entry).
    plans: Vec<plan::Plan>,
}

const PROPERTY_MAP_THRESHOLD: usize = 8;

impl Program {
    #[allow(clippy::too_many_arguments, clippy::vec_box)]
    pub fn new(
        nodes: Vec<SchemaNode>,
        root: NodeId,
        uses_dynamic_scope: bool,
        max_depth: u32,
        formats: HashMap<String, FormatValidator>,
        documents: Vec<Box<Value>>,
        annotation_sources: Vec<Option<AnnotationSource>>,
        assert_format_set: bool,
    ) -> Program {
        let fast_target = (0..nodes.len() as NodeId)
            .map(|id| {
                let mut current = id;
                for _ in 0..16 {
                    let n = &nodes[current as usize];
                    // Pure-$ref hops, and forwards: a node whose only assertion is one allOf branch (C#'s
                    // NodePlan.Forward), unless either end is on an in-place cycle, which keeps its guard.
                    let next = match pure_ref_target(n) {
                        Some(next) => next,
                        None => match forward_target(n) {
                            Some(next)
                                if !n.in_place_cycle && !nodes[next as usize].in_place_cycle && next != current =>
                            {
                                next
                            }
                            _ => break,
                        },
                    };
                    if uses_dynamic_scope && nodes[next as usize].resource_id != n.resource_id {
                        break;
                    }
                    current = next;
                }
                current
            })
            .collect();
        let property_maps = nodes
            .iter()
            .map(|n| {
                n.properties.as_ref().filter(|p| p.len() > PROPERTY_MAP_THRESHOLD).map(|p| p.iter().cloned().collect())
            })
            .collect();
        let mut program = Program {
            nodes,
            root,
            uses_dynamic_scope,
            max_depth,
            formats,
            fast_target,
            property_maps,
            documents,
            annotation_sources,
            annotations: OnceLock::new(),
            assert_format_set,
            plans: Vec::new(),
        };
        program.plans = plan::compile_plans(&program);
        program
    }

    /// The annotation keywords of every node (computed on the first evaluation with a collector).
    pub fn annotations(&self) -> &[Option<Vec<AnnotationEntry>>] {
        self.annotations.get_or_init(|| {
            self.nodes
                .iter()
                .zip(&self.annotation_sources)
                .map(|(n, src)| {
                    let src = src.as_ref()?;
                    let (value, _) = resolve_pointer(&self.documents[src.document as usize], &n.pointer)?;
                    let Value::Object(e) = value else { return None };
                    collect_annotations(e, n.dialect, src.vocab, src.content, self.assert_format_set)
                })
                .collect()
        })
    }

    #[inline]
    fn property(&self, id: NodeId, n: &SchemaNode, name: &str) -> Option<NodeId> {
        match &self.property_maps[id as usize] {
            Some(map) => map.get(name).copied(),
            None => n.properties.as_ref()?.iter().find(|(k, _)| k == name).map(|&(_, c)| c),
        }
    }
}

/// The single reference of a node that is nothing but `$ref` (or a static `$dynamicRef`), for elision.
fn pure_ref_target(n: &SchemaNode) -> Option<NodeId> {
    let refs = n.ref_.is_some() as u8 + n.static_dynamic_ref.is_some() as u8;
    if refs != 1 || n.always_true || n.always_false {
        return None;
    }
    if n.has_type
        || n.const_value.is_some()
        || n.enum_values.is_some()
        || n.has_number_keywords()
        || n.has_string_keywords()
        || n.has_object_keywords()
        || n.has_array_keywords()
        || n.dynamic_ref.is_some()
        || n.all_of.is_some()
        || n.any_of.is_some()
        || n.one_of.is_some()
        || n.not.is_some()
        || n.if_.is_some()
    {
        return None;
    }
    n.ref_.or(n.static_dynamic_ref)
}

/// The branch of a node that is nothing but a one-branch `allOf`, for fail-fast forwarding.
fn forward_target(n: &SchemaNode) -> Option<NodeId> {
    let [only] = n.all_of.as_deref()? else { return None };
    if n.always_true
        || n.always_false
        || n.ref_.is_some()
        || n.static_dynamic_ref.is_some()
        || n.has_type
        || n.const_value.is_some()
        || n.enum_values.is_some()
        || n.has_number_keywords()
        || n.has_string_keywords()
        || n.has_object_keywords()
        || n.has_array_keywords()
        || n.dynamic_ref.is_some()
        || n.any_of.is_some()
        || n.one_of.is_some()
        || n.not.is_some()
        || n.if_.is_some()
    {
        return None;
    }
    Some(*only)
}

// Messages (Corvus.Text.Json Strings.resx).
const EVALUATED_SUBSCHEMA: &str = "The value was expected to match the subschema.";
const MATCHED_ALL: &str = "The value matched all subschema.";
const DID_NOT_MATCH_ALL: &str = "The value did not match all subschema.";
const MATCHED_AT_LEAST_ONE: &str = "The value matched at least one subschema.";
const DID_NOT_MATCH_AT_LEAST_ONE: &str = "The value did not match at least one subschema.";
const MATCHED_NO_SCHEMA: &str = "The instance matched no schema.";
const MATCHED_EXACTLY_ONE: &str = "The value matched exactly one subschema.";
const MATCHED_MORE_THAN_ONE: &str = "The instance matched more than one schema.";
const MATCHED_NOT: &str =
    "The value matched the subschema in a not composition, which means the evaluation was not a match.";
const DID_NOT_MATCH_NOT: &str =
    "The value did not match the subschema in a not composition, which means the evaluation was a match.";
const MATCHED_IF_FOR_THEN: &str = "The value matched the subschema in a binary or ternay if, which means the evaluation will go on to match the then subschema.";
const MATCHED_IF_FOR_ELSE: &str = "The value did not match the subschema in a ternary if, which means the evaluation will go on to match the else subschema.";
const MATCHED_THEN: &str = "The value matched the then subschema corresponding to a binary or ternary if.";
const DID_NOT_MATCH_THEN: &str = "The value did not match the then subschema corresponding to a binary or ternary if.";
const MATCHED_ELSE: &str = "The value matched the else subschema corresponding to a ternary if.";
const DID_NOT_MATCH_ELSE: &str = "The value did not match the else subschema corresponding to a ternary if.";
const UNIQUE_ITEMS: &str = "The array was expected to contain unique items.";
const PROPERTY_NAME_FAILED: &str = "The property name did not match the schema.";

/// `" 'v'"`, or nothing for an empty value (`JsonSchemaEvaluation.AppendSingleQuotedValue`).
fn q(v: &str) -> String {
    if v.is_empty() { String::new() } else { format!(" '{v}'") }
}

const TYPE_ORDER: [(u8, &str); 7] = [
    (type_mask::ARRAY, "array"),
    (type_mask::OBJECT, "object"),
    (type_mask::NULL, "null"),
    (type_mask::BOOLEAN, "boolean"),
    (type_mask::NUMBER, "number"),
    (type_mask::INTEGER, "integer"),
    (type_mask::STRING, "string"),
];

fn type_message(mask: u8) -> String {
    let names: Vec<&str> = TYPE_ORDER.iter().filter(|(m, _)| mask & m != 0).map(|(_, n)| *n).collect();
    match names.len() {
        0 => String::new(),
        1 => format!("The value was expected to be of type '{}'", names[0]),
        _ => format!(
            "The value was expected to be of type '[{}]'",
            names.iter().map(|n| format!("\"{n}\"")).collect::<Vec<_>>().join(", ")
        ),
    }
}

fn const_message(value: &Value) -> String {
    match value {
        Value::String(s) => format!("Expected the value to be the string{}", q(s)),
        Value::Number(n) => format!("The value was expected to be equal to{}", q(&number_text(n))),
        Value::Bool(b) => format!("Expected the value to be '{b}'"),
        Value::Null => "Expected the value to be 'null'".to_string(),
        _ => String::new(),
    }
}

/// Whether a value is of one of the types in a mask.
#[inline]
pub(crate) fn matches_type<'a, I: Instance<'a>>(mask: u8, x: I) -> bool {
    match x.kind() {
        Kind::Null => mask & type_mask::NULL != 0,
        Kind::Bool => mask & type_mask::BOOLEAN != 0,
        Kind::Number => {
            mask & type_mask::NUMBER != 0
                || (mask & type_mask::INTEGER != 0 && matches!(x.view(), View::Number(n) if is_integer(&n)))
        }
        Kind::String => mask & type_mask::STRING != 0,
        Kind::Array => mask & type_mask::ARRAY != 0,
        Kind::Object => mask & type_mask::OBJECT != 0,
    }
}

#[inline]
fn is_integer(n: &Number) -> bool {
    n.is_i64() || n.is_u64() || n.as_f64().is_some_and(|f| f.is_finite() && f.fract() == 0.0)
}

/// JSON equality: numbers by value, objects by their property sets, arrays element-wise. The two values may be read
/// differently (an instance against a schema's constant).
pub(crate) fn json_equal<'a, 'b, A: Instance<'a>, B: Instance<'b>>(a: A, b: B) -> bool {
    match (a.view(), b.view()) {
        (View::Null, View::Null) => true,
        (View::Bool(x), View::Bool(y)) => x == y,
        (View::Number(x), View::Number(y)) => num_eq(&x, &y),
        (View::String(x), View::String(y)) => x == y,
        (View::Array(x), View::Array(y)) => x.len() == y.len() && x.iter().zip(y.iter()).all(|(p, q)| json_equal(p, q)),
        (View::Object(x), View::Object(y)) => {
            if x.len() != y.len() {
                return false;
            }
            // Objects usually list their members in the same order: compare position by position, and look names up
            // (a hash each) only from the first position where the names differ.
            for (i, ((k, v), (l, w))) in x.iter().zip(y.iter()).enumerate() {
                if k != l {
                    return x.iter().skip(i).all(|(k, v)| y.get(k).is_some_and(|w| json_equal(v, w)));
                }
                if !json_equal(v, w) {
                    return false;
                }
            }
            true
        }
        _ => false,
    }
}

/// A hash of a JSON value that agrees with JSON equality (object hashing is order-independent).
fn json_hash<'a, I: Instance<'a>>(v: I) -> u64 {
    const K: u64 = 0x9e37_79b9_7f4a_7c15;
    match v.view() {
        View::Null => 0x53,
        View::Bool(b) => 0x51 + b as u64,
        View::Number(n) => match Num::of(&n) {
            Num::I(i) => (i as u64).wrapping_mul(K) ^ 0x1234,
            Num::F(f) => f.to_bits().wrapping_mul(K) ^ 0x4321,
        },
        View::String(s) => str_hash(s),
        View::Array(a) => a.iter().fold(0x54 + a.len() as u64, |h, x| h.wrapping_mul(31).wrapping_add(json_hash(x))),
        View::Object(o) => {
            // Equal objects have the same member values, so a sum of the values' hashes (whatever the order) agrees with
            // equality; hashing the names too would cost more than the collisions it saves.
            o.iter().fold(0x55 + o.len() as u64, |h, (_, x)| h.wrapping_add(json_hash(x).wrapping_mul(0x2c1b_3c6d)))
        }
    }
}

/// A string hash, eight bytes at a time.
fn str_hash(s: &str) -> u64 {
    const K: u64 = 0x9e37_79b9_7f4a_7c15;
    let b = s.as_bytes();
    let mut h = (b.len() as u64).wrapping_mul(K);
    let mut chunks = b.chunks_exact(8);
    for c in &mut chunks {
        h = (h.rotate_left(5) ^ u64::from_le_bytes(c.try_into().unwrap())).wrapping_mul(K);
    }
    // The tail as one word without copying it out: the last eight bytes when there are that many (overlapping the
    // chunks already hashed), else the overlapping first and last four, else the bytes themselves.
    let n = b.len();
    let tail = if n >= 8 {
        u64::from_le_bytes(b[n - 8..].try_into().unwrap())
    } else if n >= 4 {
        u32::from_le_bytes(b[..4].try_into().unwrap()) as u64
            | (u32::from_le_bytes(b[n - 4..].try_into().unwrap()) as u64) << 32
    } else {
        b.iter().fold(0, |w, &c| w << 8 | c as u64)
    };
    (h.rotate_left(5) ^ tail).wrapping_mul(K)
}

/// `uniqueItems`: pairwise for short arrays, by hash + equality otherwise.
pub(crate) fn all_unique<'a, A: ArrayView<'a>>(a: A) -> bool {
    let n = a.len();
    if n < 2 {
        return true;
    }
    // Arrays of strings (the common case: lists of names) compare by length first, without hashing or allocating.
    if n <= 32 && a.iter().all(|x| x.kind() == Kind::String) {
        let s = |i: usize| match a.get(i).view() {
            View::String(t) => t,
            _ => unreachable!("every item is a string"),
        };
        for i in 1..n {
            for j in 0..i {
                let (x, y) = (s(i), s(j));
                if x.len() == y.len() && x == y {
                    return false;
                }
            }
        }
        return true;
    }
    if n <= 16 {
        for i in 1..n {
            for j in 0..i {
                if json_equal(a.get(i), a.get(j)) {
                    return false;
                }
            }
        }
        return true;
    }
    // Sort by hash: equal values have equal hashes, so only runs of equal hashes need comparing.
    let mut hashed: Vec<(u64, u32)> = a.iter().enumerate().map(|(i, x)| (json_hash(x), i as u32)).collect();
    hashed.sort_unstable();
    let mut start = 0;
    for end in 1..=n {
        if end == n || hashed[end].0 != hashed[start].0 {
            for i in start + 1..end {
                for j in start..i {
                    if json_equal(a.get(hashed[i].1 as usize), a.get(hashed[j].1 as usize)) {
                        return false;
                    }
                }
            }
            start = end;
        }
    }
    true
}

/// The length of a string in code points (what `minLength`/`maxLength` count).
#[inline]
fn code_points(s: &str) -> u64 {
    if s.is_ascii() { s.len() as u64 } else { s.chars().count() as u64 }
}

fn base64_decode(s: &str) -> Option<Vec<u8>> {
    let b = s.as_bytes();
    if b.len() % 4 != 0 {
        return None;
    }
    let val = |c: u8| -> Option<u32> {
        Some(match c {
            b'A'..=b'Z' => (c - b'A') as u32,
            b'a'..=b'z' => (c - b'a' + 26) as u32,
            b'0'..=b'9' => (c - b'0' + 52) as u32,
            b'+' => 62,
            b'/' => 63,
            _ => return None,
        })
    };
    let mut out = Vec::with_capacity(b.len() / 4 * 3);
    for (ci, chunk) in b.chunks(4).enumerate() {
        let last = ci == b.len() / 4 - 1;
        let pad = chunk.iter().rev().take_while(|&&c| c == b'=').count();
        if pad > 2 || (pad > 0 && !last) {
            return None;
        }
        let mut acc = 0u32;
        for &c in &chunk[..4 - pad] {
            acc = (acc << 6) | val(c)?;
        }
        acc <<= 6 * pad as u32;
        let bytes = [(acc >> 16) as u8, (acc >> 8) as u8, acc as u8];
        out.extend_from_slice(&bytes[..3 - pad]);
    }
    Some(out)
}

/// Draft 7 content assertion.
fn content_ok(s: &str, kind: ContentKind) -> bool {
    match kind {
        ContentKind::Base64 => base64_decode(s).is_some(),
        ContentKind::Json => serde_json::from_str::<Value>(s).is_ok(),
        ContentKind::Base64Json => base64_decode(s).is_some_and(|d| serde_json::from_slice::<Value>(&d).is_ok()),
        ContentKind::None => true,
    }
}

// --------------------------------------------------------------------------------------------------------------------
// Evaluated properties/items

/// Evaluated-property (by index in the instance object) or evaluated-item bits.
pub(crate) enum Bits {
    /// Up to 256 properties or items, without an allocation.
    Inline([u64; 4]),
    Heap(Vec<u64>),
}

impl Bits {
    fn new(len: usize) -> Bits {
        if len <= 256 { Bits::Inline([0; 4]) } else { Bits::Heap(vec![0; len.div_ceil(64)]) }
    }

    #[inline]
    fn words(&self) -> &[u64] {
        match self {
            Bits::Inline(w) => w,
            Bits::Heap(w) => w,
        }
    }

    #[inline]
    fn words_mut(&mut self) -> &mut [u64] {
        match self {
            Bits::Inline(w) => w,
            Bits::Heap(w) => w,
        }
    }

    #[inline]
    fn set(&mut self, i: usize) {
        self.words_mut()[i >> 6] |= 1 << (i & 63);
    }

    #[inline]
    fn get(&self, i: usize) -> bool {
        self.words()[i >> 6] & (1 << (i & 63)) != 0
    }

    fn merge(&mut self, other: &Bits) {
        for (a, b) in self.words_mut().iter_mut().zip(other.words()) {
            *a |= b;
        }
    }
}

fn container_len<'a, I: Instance<'a>>(x: I) -> usize {
    match x.view() {
        View::Object(o) => o.len(),
        View::Array(a) => a.len(),
        _ => 0,
    }
}

#[inline(always)]
fn is_object<'a, I: Instance<'a>>(x: I) -> bool {
    x.kind() == Kind::Object
}

#[inline(always)]
fn is_array<'a, I: Instance<'a>>(x: I) -> bool {
    x.kind() == Kind::Array
}

// --------------------------------------------------------------------------------------------------------------------
// The evaluator

/// The oneOf/anyOf branches a discriminator leaves as candidates for an instance.
enum Selection<'a> {
    All,
    None,
    Subset(&'a [u32]),
}

impl Selection<'_> {
    fn indexes(&self, len: usize) -> impl Iterator<Item = usize> + '_ {
        let (range, subset) = match self {
            Selection::All => (0..len, None),
            Selection::None => (0..0, None),
            Selection::Subset(s) => (0..0, Some(s.iter().map(|&i| i as usize))),
        };
        range.chain(subset.into_iter().flatten())
    }
}

fn select<'a, 'x, I: Instance<'x>>(d: Option<&'a Discriminator>, x: I) -> Selection<'a> {
    let (Some(d), View::Object(o)) = (d, x.view()) else { return Selection::All };
    let Some(value) = o.get(&d.property) else {
        return if d.all_require { Selection::None } else { Selection::All };
    };
    let hit = d.known.iter().find(|(k, _)| k.matches(value));
    Selection::Subset(hit.map_or(&d.unknown, |(_, branches)| branches))
}

pub(crate) trait Mode {
    const COLLECT: bool;
}

pub(crate) struct Fast;
impl Mode for Fast {
    const COLLECT: bool = false;
}

pub(crate) struct Collect;
impl Mode for Collect {
    const COLLECT: bool = true;
}

pub(crate) struct Evaluator<'p, 'c> {
    p: &'p Program,
    annotations: &'p [Option<Vec<AnnotationEntry>>],
    c: Option<&'c mut JsonSchemaResultsCollector>,
    scope: Vec<u32>,
    depth: u32,
    pub depth_exceeded: bool,
    /// A string reused to evaluate property names against propertyNames (a `String`, not a `Value`: dropping a
    /// `Value` is an out-of-line call, which every evaluation paid).
    name_buffer: String,
}

/// Records a keyword result: in collecting mode reports it and accumulates `ok`; in fast mode returns on failure.
macro_rules! check {
    ($self:ident, $M:ty, $ok:ident, $m:expr, $msg:expr, $kw:expr) => {{
        let m: bool = $m;
        if <$M>::COLLECT {
            $self.col().evaluated_keyword(m, $msg, $kw);
            $ok &= m;
        } else if !m {
            return false;
        }
    }};
}

/// Accumulates a sub-result: in fast mode returns on failure.
macro_rules! and {
    ($M:ty, $ok:ident, $m:expr) => {{
        let m: bool = $m;
        if <$M>::COLLECT {
            $ok &= m;
        } else if !m {
            return false;
        }
    }};
}

impl<'p, 'c> Evaluator<'p, 'c> {
    pub fn new(p: &'p Program, c: Option<&'c mut JsonSchemaResultsCollector>) -> Self {
        let annotations: &'p [Option<Vec<AnnotationEntry>>] = if c.is_some() { p.annotations() } else { &[] };
        Evaluator { p, annotations, c, scope: Vec::new(), depth: 0, depth_exceeded: false, name_buffer: String::new() }
    }

    #[inline(always)]
    fn col(&mut self) -> &mut JsonSchemaResultsCollector {
        self.c.as_deref_mut().unwrap()
    }

    #[inline(always)]
    fn node(&self, id: NodeId) -> &'p SchemaNode {
        &self.p.nodes[id as usize]
    }

    /// Evaluates the program's entry in fast mode.
    pub fn validate<'x, I: Instance<'x>>(&mut self, x: I) -> bool {
        let root = self.p.fast_target[self.p.root as usize];
        // An evaluation that recursed in place beyond the maximum depth is not valid, whatever it came to: the
        // branch that was abandoned counts as false, which a not above it turns into true.
        self.fast(root, x) && !self.depth_exceeded
    }

    /// Fail-fast evaluation of a node, through its plan when there are plans.
    #[inline]
    fn fast<'x, I: Instance<'x>>(&mut self, id: NodeId, x: I) -> bool {
        if self.p.plans.is_empty() { self.eval_node::<Fast, I>(id, x, None) } else { self.run(id, x) }
    }

    /// Evaluates the program's entry, reporting to the collector.
    pub fn evaluate<'x, I: Instance<'x>>(&mut self, x: I) -> bool {
        // A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
        let (root, _) = self.resolve(self.p.root);
        let pointer = &self.node(root).pointer;
        self.col().begin_child_context(None, Some(pointer), None);
        let ok = self.eval_node::<Collect, I>(root, x, None);
        self.col().commit_child_context(false, ok, Message::Static(EVALUATED_SUBSCHEMA));
        ok
    }

    /// The single reference of a pure-$ref node, for collecting mode (a node with annotations is not elided).
    fn collect_pure_ref(&self, id: NodeId) -> Option<NodeId> {
        if self.annotations.get(id as usize).is_some_and(Option::is_some) {
            return None;
        }
        pure_ref_target(self.node(id))
    }

    /// Follows pure-reference hops (at most 16; not across resources when a dynamic scope is kept), returning the
    /// target and the evaluation-path suffix of the hops (`/$ref`, `/$dynamicRef`, `/$recursiveRef`).
    fn resolve(&self, id: NodeId) -> (NodeId, String) {
        let mut current = id;
        let mut suffix = String::new();
        for _ in 0..16 {
            let n = self.node(current);
            let Some(next) = self.collect_pure_ref(current) else { break };
            if self.p.uses_dynamic_scope && self.node(next).resource_id != n.resource_id {
                break;
            }
            suffix.push_str(if n.ref_.is_some() { "/$ref" } else { "/" });
            if n.ref_.is_none() {
                suffix.push_str(n.static_dynamic_keyword.name());
            }
            current = next;
        }
        (current, suffix)
    }

    fn eval_node<'x, M: Mode, I: Instance<'x>>(&mut self, id: NodeId, x: I, bits: Option<&mut Bits>) -> bool {
        let n = self.node(id);
        if n.always_true || n.always_false {
            if M::COLLECT {
                self.col().evaluated_boolean_schema(n.always_true);
            }
            return n.always_true;
        }
        let pushed = self.p.uses_dynamic_scope && self.scope.last() != Some(&n.resource_id);
        if pushed {
            self.scope.push(n.resource_id);
        }
        let needs_own = bits.is_none()
            && ((n.unevaluated_properties.is_some() && is_object(x)) || (n.unevaluated_items.is_some() && is_array(x)));
        let ok = if needs_own {
            let mut own = Bits::new(container_len(x));
            self.eval_core::<M, I>(id, n, x, Some(&mut own))
        } else {
            self.eval_core::<M, I>(id, n, x, bits)
        };
        if pushed {
            self.scope.pop();
        }
        ok
    }

    fn eval_core<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        id: NodeId,
        n: &'p SchemaNode,
        x: I,
        mut bits: Option<&mut Bits>,
    ) -> bool {
        let mut ok = true;
        if n.has_type {
            check!(self, M, ok, matches_type(n.type_mask, x), Message::Lazy(&|| type_message(n.type_mask)), "type");
        }
        if let Some(c) = &n.const_value {
            check!(self, M, ok, json_equal(x, c), Message::Lazy(&|| const_message(c)), "const");
        }
        if let Some(values) = &n.enum_values {
            let m = values.iter().any(|v| json_equal(x, v));
            check!(
                self,
                M,
                ok,
                m,
                Message::Static(if m { MATCHED_AT_LEAST_ONE } else { DID_NOT_MATCH_AT_LEAST_ONE }),
                "enum"
            );
        }
        match x.view() {
            View::Number(num) if n.has_number_keywords() => {
                and!(M, ok, self.eval_number::<M>(n, &num));
            }
            View::String(s) if n.has_string_keywords() => {
                and!(M, ok, self.eval_string::<M>(n, s));
            }
            View::Object(o) if n.has_object_keywords() => {
                and!(M, ok, self.eval_object::<M, I>(id, n, o, x, bits.as_deref_mut()));
            }
            View::Array(a) if n.has_array_keywords() => {
                and!(M, ok, self.eval_array::<M, I>(n, a, bits.as_deref_mut()));
            }
            _ => {}
        }
        and!(M, ok, self.eval_in_place::<M, I>(n, x, bits.as_deref_mut()));
        match x.view() {
            View::Object(o) if n.unevaluated_properties.is_some() => {
                and!(M, ok, self.eval_unevaluated_properties::<M, I>(n, o, bits.as_deref_mut().unwrap()));
            }
            View::Array(a) if n.unevaluated_items.is_some() => {
                and!(M, ok, self.eval_unevaluated_items::<M, I>(n, a, bits.unwrap()));
            }
            _ => {}
        }
        if M::COLLECT {
            if let Some(list) = &self.annotations[id as usize] {
                for a in list {
                    if a.strings_only && x.kind() != Kind::String {
                        continue;
                    }
                    let v = &a.value;
                    self.col().ignored_keyword(Message::Lazy(&|| v.to_string()), &a.keyword);
                }
            }
        }
        ok
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Numbers and strings

    fn eval_number<M: Mode>(&mut self, n: &'p SchemaNode, x: &Number) -> bool {
        let mut ok = true;
        if n.assert_format && n.format_kind.is_numeric() {
            if let Some(f) = &n.format {
                let m = match self.p.formats.get(f) {
                    Some(custom) => custom(&x.to_string()),
                    None => n.format_kind.check_number(x),
                };
                let kind = n.format_kind.name();
                check!(
                    self,
                    M,
                    ok,
                    m,
                    Message::Lazy(&|| format!(
                        "The value was expected to be in a supported format, and within bounds for '{kind}'"
                    )),
                    "format"
                );
            }
        }
        let xv = Num::of(x);
        use std::cmp::Ordering::*;
        if let Some(b) = &n.minimum {
            let m = cmp(xv, Num::of(b)) != Less;
            check!(
                self,
                M,
                ok,
                m,
                Message::Lazy(&|| format!(
                    "The value was expected to be greater than or equal to{}",
                    q(&number_text(b))
                )),
                "minimum"
            );
        }
        if let Some(b) = &n.maximum {
            let m = cmp(xv, Num::of(b)) != Greater;
            check!(
                self,
                M,
                ok,
                m,
                Message::Lazy(&|| format!("The value was expected to be less than or equal to{}", q(&number_text(b)))),
                "maximum"
            );
        }
        if let Some(b) = &n.exclusive_minimum {
            let m = cmp(xv, Num::of(b)) == Greater;
            check!(
                self,
                M,
                ok,
                m,
                Message::Lazy(&|| format!("The value was expected to be greater than{}", q(&number_text(b)))),
                "exclusiveMinimum"
            );
        }
        if let Some(b) = &n.exclusive_maximum {
            let m = cmp(xv, Num::of(b)) == Less;
            check!(
                self,
                M,
                ok,
                m,
                Message::Lazy(&|| format!("The value was expected to be less than{}", q(&number_text(b)))),
                "exclusiveMaximum"
            );
        }
        if let Some(d) = &n.multiple_of {
            let m = multiple_of(x, d);
            check!(
                self,
                M,
                ok,
                m,
                Message::Lazy(&|| format!("The value was expected to be a multiple of{}", q(&number_text(d)))),
                "multipleOf"
            );
        }
        ok
    }

    fn eval_string<M: Mode>(&mut self, n: &'p SchemaNode, s: &str) -> bool {
        let mut ok = true;
        if n.min_length.is_some() || n.max_length.is_some() {
            let len = code_points(s);
            if let Some(min) = n.min_length {
                check!(
                    self,
                    M,
                    ok,
                    len >= min,
                    Message::Lazy(&|| format!(
                        "Expected the length of the value to be greater than or equal to '{min}'"
                    )),
                    "minLength"
                );
            }
            if let Some(max) = n.max_length {
                check!(
                    self,
                    M,
                    ok,
                    len <= max,
                    Message::Lazy(&|| format!("Expected the length of the value to be less than or equal to '{max}'")),
                    "maxLength"
                );
            }
        }
        if let Some(p) = &n.pattern {
            check!(
                self,
                M,
                ok,
                p.is_match(s),
                Message::Lazy(&|| format!("Expected the value to match the regular expression{}", q(&p.source))),
                "pattern"
            );
        }
        if n.assert_format && !n.format_kind.is_numeric() {
            if let Some(f) = &n.format {
                let (m, message): (bool, Message) = match self.p.formats.get(f) {
                    Some(custom) => (custom(s), Message::Lazy(&|| format!("Expected a string in the '{f}' format."))),
                    None if n.format_kind == FormatKind::Unknown => (true, Message::None),
                    None => (
                        n.format_kind.check_string(s, n.dialect <= Dialect::Draft6),
                        n.format_kind.message().map_or(Message::None, Message::Static),
                    ),
                };
                check!(self, M, ok, m, message, "format");
            }
        }
        if n.assert_content {
            let m = content_ok(s, n.content);
            let (message, keyword) = match n.content {
                ContentKind::Base64 => ("Expected a valid Base64-encoded string.", "contentEncoding"),
                ContentKind::Json => ("Expected valid JSON content.", "contentMediaType"),
                _ => ("Expected valid Base64-encoded JSON content.", "contentMediaType"),
            };
            check!(self, M, ok, m, Message::Static(message), keyword);
        }
        ok
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Objects

    /// A child application at a new instance location (a property value or an array item).
    #[inline]
    fn eval_at<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        child: NodeId,
        path: &dyn Fn() -> String,
        value: I,
        doc_segment: &dyn Fn() -> String,
    ) -> bool {
        if !M::COLLECT {
            return self.fast(self.p.fast_target[child as usize], value);
        }
        let (target, suffix) = self.resolve(child);
        let pointer = &self.node(target).pointer;
        let eval_segment = format!("{}{}", path(), suffix);
        let doc = doc_segment();
        self.col().begin_child_context(Some(&eval_segment), Some(pointer), Some(&doc));
        let ok = self.eval_node::<Collect, I>(target, value, None);
        self.col().commit_child_context(ok, ok, Message::Static(EVALUATED_SUBSCHEMA));
        ok
    }

    fn eval_object<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        id: NodeId,
        n: &'p SchemaNode,
        o: I::Object,
        _x: I,
        mut bits: Option<&mut Bits>,
    ) -> bool {
        let mut ok = true;
        let len = o.len() as u64;
        if let Some(min) = n.min_properties {
            check!(
                self,
                M,
                ok,
                len >= min,
                Message::Lazy(&|| format!("Expected the property count to be greater than or equal to '{min}'")),
                "minProperties"
            );
        }
        if let Some(max) = n.max_properties {
            check!(
                self,
                M,
                ok,
                len <= max,
                Message::Lazy(&|| format!("Expected the property count to be less than or equal to '{max}'")),
                "maxProperties"
            );
        }
        if n.properties.is_some()
            || n.pattern_properties.is_some()
            || n.additional_properties.is_some()
            || n.property_names.is_some()
        {
            for (i, (k, v)) in o.iter().enumerate() {
                let mut matched = false;
                if let Some(p) = self.p.property(id, n, k) {
                    matched = true;
                    if let Some(b) = bits.as_deref_mut() {
                        b.set(i);
                    }
                    and!(
                        M,
                        ok,
                        self.eval_at::<M, I>(p, &|| format!("properties/{}", encode_pointer_segment(k)), v, &|| {
                            encode_pointer_segment(k).into_owned()
                        })
                    );
                }
                if let Some(pps) = &n.pattern_properties {
                    for pp in pps {
                        if !pp.pattern.is_match(k) {
                            continue;
                        }
                        matched = true;
                        if let Some(b) = bits.as_deref_mut() {
                            b.set(i);
                        }
                        let source = &pp.pattern.source;
                        and!(
                            M,
                            ok,
                            self.eval_at::<M, I>(
                                pp.node,
                                &|| format!("patternProperties/{}", encode_pointer_segment(source)),
                                v,
                                &|| encode_pointer_segment(k).into_owned()
                            )
                        );
                    }
                }
                if let Some(ap) = n.additional_properties {
                    if !matched {
                        if let Some(b) = bits.as_deref_mut() {
                            b.set(i);
                        }
                        and!(
                            M,
                            ok,
                            self.eval_at::<M, I>(ap, &|| "additionalProperties".to_string(), v, &|| {
                                encode_pointer_segment(k).into_owned()
                            })
                        );
                    }
                }
                if let Some(pn) = n.property_names {
                    let name = Value::String(k.to_string());
                    if M::COLLECT {
                        // Not elided; the document path stays the object's.
                        let pointer = &self.node(pn).pointer;
                        self.col().begin_child_context(Some("propertyNames"), Some(pointer), None);
                        let m = self.eval_node::<Collect, &Value>(pn, &name, None);
                        self.col().commit_child_context(m, m, Message::Static(EVALUATED_SUBSCHEMA));
                        if !m {
                            self.col().evaluated_keyword(false, Message::Static(PROPERTY_NAME_FAILED), "propertyNames");
                            ok = false;
                        }
                    } else if !self.fast(self.p.fast_target[pn as usize], &name) {
                        return false;
                    }
                }
            }
        }
        if let Some(required) = &n.required_list {
            for r in required {
                let present = o.contains_key(r);
                if M::COLLECT {
                    self.col().evaluated_keyword_for_property(
                        present,
                        Message::Lazy(&|| {
                            format!("Required property {}present '{r}'", if present { "" } else { "not " })
                        }),
                        r,
                        "required",
                    );
                    ok &= present;
                } else if !present {
                    return false;
                }
            }
        }
        if let Some(deps) = &n.dependencies {
            // Rows are reported under the keyword the schema used (dependencies, dependentRequired, dependentSchemas).
            for d in deps {
                if !o.contains_key(&d.name) {
                    continue;
                }
                let keyword = d.keyword.name();
                if let Some(required) = &d.required {
                    for r in required {
                        let present = o.contains_key(r);
                        if M::COLLECT {
                            self.col().evaluated_keyword_for_property(
                                present,
                                Message::Lazy(&|| {
                                    format!("Required property {}present '{r}'", if present { "" } else { "not " })
                                }),
                                r,
                                keyword,
                            );
                            ok &= present;
                        } else if !present {
                            return false;
                        }
                    }
                }
                if let Some(schema) = d.schema {
                    let name = &d.name;
                    let (m, _) = self.eval_in_place_child::<M, I>(
                        schema,
                        &|| format!("{keyword}/{}", encode_pointer_segment(name)),
                        _x,
                        bits.as_deref_mut(),
                        true,
                        true,
                    );
                    if M::COLLECT {
                        self.col().evaluated_keyword_for_property(
                            m,
                            Message::Lazy(&|| {
                                format!(
                                    "The value did match the schema applied because it contained the property '{name}'"
                                )
                            }),
                            name,
                            keyword,
                        );
                        ok &= m;
                    } else if !m {
                        return false;
                    }
                }
            }
        }
        ok
    }

    fn eval_unevaluated_properties<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        n: &'p SchemaNode,
        o: I::Object,
        bits: &mut Bits,
    ) -> bool {
        let child = n.unevaluated_properties.unwrap();
        let mut ok = true;
        for (i, (k, v)) in o.iter().enumerate() {
            if bits.get(i) {
                continue;
            }
            bits.set(i);
            and!(
                M,
                ok,
                self.eval_at::<M, I>(child, &|| "unevaluatedProperties".to_string(), v, &|| encode_pointer_segment(k)
                    .into_owned())
            );
        }
        if M::COLLECT {
            self.col().evaluated_keyword(ok, Message::None, "unevaluatedProperties");
        }
        ok
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Arrays

    fn eval_array<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        n: &'p SchemaNode,
        a: I::Array,
        mut bits: Option<&mut Bits>,
    ) -> bool {
        let mut ok = true;
        let len = a.len() as u64;
        if let Some(min) = n.min_items {
            check!(
                self,
                M,
                ok,
                len >= min,
                Message::Lazy(&|| format!("Expected the item count to be greater than or equal to '{min}'")),
                "minItems"
            );
        }
        if let Some(max) = n.max_items {
            check!(
                self,
                M,
                ok,
                len <= max,
                Message::Lazy(&|| format!("Expected the item count to be less than or equal to '{max}'")),
                "maxItems"
            );
        }
        if n.prefix_items.is_none() && n.items.is_none() && n.contains.is_none() && !n.unique_items {
            return ok;
        }
        let mut count = 0u64;
        let prefix_len = n.prefix_items.as_ref().map_or(0, Vec::len);
        for (i, item) in a.iter().enumerate() {
            if i < prefix_len {
                if let Some(b) = bits.as_deref_mut() {
                    b.set(i);
                }
                let child = n.prefix_items.as_ref().unwrap()[i];
                let kw = n.prefix_keyword;
                and!(M, ok, self.eval_at::<M, I>(child, &|| format!("{kw}/{i}"), item, &|| i.to_string()));
            } else if let Some(items) = n.items {
                if let Some(b) = bits.as_deref_mut() {
                    b.set(i);
                }
                let kw = n.items_keyword;
                and!(M, ok, self.eval_at::<M, I>(items, &|| kw.to_string(), item, &|| i.to_string()));
            }
            if let Some(contains) = n.contains {
                let matched = if M::COLLECT {
                    let (target, suffix) = self.resolve(contains);
                    let pointer = &self.node(target).pointer;
                    let seg = format!("contains{suffix}");
                    let doc = i.to_string();
                    self.col().begin_child_context(Some(&seg), Some(pointer), Some(&doc));
                    if self.eval_node::<Collect, I>(target, item, None) {
                        self.col().commit_child_context(true, true, Message::Static(EVALUATED_SUBSCHEMA));
                        true
                    } else {
                        self.col().pop_child_context();
                        false
                    }
                } else {
                    self.fast(self.p.fast_target[contains as usize], item)
                };
                if matched {
                    count += 1;
                    if n.contains_marks_evaluated {
                        if let Some(b) = bits.as_deref_mut() {
                            b.set(i);
                        }
                    }
                }
            }
        }
        if n.unique_items {
            let unique = all_unique(a);
            check!(self, M, ok, unique, Message::Static(UNIQUE_ITEMS), "uniqueItems");
        }
        if n.contains.is_some() {
            let max = n.max_contains;
            let min = n.min_contains;
            let m = count >= min && max.is_none_or(|mx| count <= mx);
            let over = max.is_some_and(|mx| count > mx);
            check!(
                self,
                M,
                ok,
                m,
                Message::Lazy(&|| if over {
                    format!("Expected the contains count to be less than or equal to '{}'", max.unwrap())
                } else {
                    format!("Expected the contains count to be greater than or equal to '{min}'")
                }),
                "contains"
            );
        }
        ok
    }

    fn eval_unevaluated_items<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        n: &'p SchemaNode,
        a: I::Array,
        bits: &mut Bits,
    ) -> bool {
        let child = n.unevaluated_items.unwrap();
        let mut ok = true;
        for (i, item) in a.iter().enumerate() {
            if bits.get(i) {
                continue;
            }
            bits.set(i);
            and!(M, ok, self.eval_at::<M, I>(child, &|| "unevaluatedItems".to_string(), item, &|| i.to_string()));
        }
        if M::COLLECT {
            self.col().evaluated_keyword(ok, Message::None, "unevaluatedItems");
        }
        ok
    }

    // ----------------------------------------------------------------------------------------------------------------
    // In-place applicators

    fn can_mark<'x, I: Instance<'x>>(&self, id: NodeId, x: I) -> bool {
        let n = self.node(id);
        if is_object(x) { n.marks_properties } else { n.marks_items }
    }

    /// Evaluates an in-place child: a new context at the same instance location, on a fresh scratch set of evaluated
    /// properties/items merged into the parent's on success. A failing child is committed or popped. Returns the
    /// result and the scratch bits (for oneOf, which merges only a single match).
    fn eval_in_place_child<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        child: NodeId,
        path: &dyn Fn() -> String,
        x: I,
        bits: Option<&mut Bits>,
        commit_on_failure: bool,
        elide: bool,
    ) -> (bool, Option<Bits>) {
        let (target, suffix) = if !elide {
            (child, String::new())
        } else if M::COLLECT {
            self.resolve(child)
        } else {
            (self.p.fast_target[child as usize], String::new())
        };
        let mut scratch =
            if bits.is_some() && self.can_mark(child, x) { Some(Bits::new(container_len(x))) } else { None };
        let guarded = self.node(target).in_place_cycle;
        if guarded {
            self.depth += 1;
            if self.depth > self.p.max_depth {
                self.depth_exceeded = true;
                self.depth -= 1;
                return (false, None);
            }
        }
        let ok = if M::COLLECT {
            let pointer = &self.node(target).pointer;
            let seg = format!("{}{}", path(), suffix);
            self.col().begin_child_context(Some(&seg), Some(pointer), None);
            let ok = self.eval_node::<Collect, I>(target, x, scratch.as_mut());
            if ok || commit_on_failure {
                self.col().commit_child_context(ok, ok, Message::Static(EVALUATED_SUBSCHEMA));
            } else {
                self.col().pop_child_context();
            }
            ok
        } else if scratch.is_some() {
            self.eval_node::<Fast, I>(target, x, scratch.as_mut())
        } else {
            self.fast(target, x)
        };
        if guarded {
            self.depth -= 1;
        }
        if ok {
            if let (Some(s), Some(b)) = (&scratch, bits) {
                b.merge(s);
            }
        }
        (ok, scratch)
    }

    fn resolve_dynamic(&self, d: &DynamicRefTarget) -> NodeId {
        for resource in &self.scope {
            if let Some(&(_, target)) = d.by_resource.iter().find(|(r, _)| r == resource) {
                return target;
            }
        }
        d.fallback
    }

    fn eval_in_place<'x, M: Mode, I: Instance<'x>>(
        &mut self,
        n: &'p SchemaNode,
        x: I,
        mut bits: Option<&mut Bits>,
    ) -> bool {
        let mut ok = true;
        if let Some(r) = n.ref_ {
            let (m, _) =
                self.eval_in_place_child::<M, I>(r, &|| "$ref".to_string(), x, bits.as_deref_mut(), true, true);
            check!(self, M, ok, m, Message::Static(if m { MATCHED_ALL } else { DID_NOT_MATCH_ALL }), "$ref");
        }
        if let Some(r) = n.static_dynamic_ref {
            let keyword = n.static_dynamic_keyword.name();
            let (m, _) =
                self.eval_in_place_child::<M, I>(r, &|| keyword.to_string(), x, bits.as_deref_mut(), true, true);
            check!(self, M, ok, m, Message::Static(if m { MATCHED_ALL } else { DID_NOT_MATCH_ALL }), keyword);
        }
        if let Some(d) = &n.dynamic_ref {
            let keyword = if d.is_recursive { "$recursiveRef" } else { "$dynamicRef" };
            // The resolved target is elided, with no hops in the path.
            let dynamic_target = self.resolve_dynamic(d);
            let target =
                if M::COLLECT { self.resolve(dynamic_target).0 } else { self.p.fast_target[dynamic_target as usize] };
            let (m, _) =
                self.eval_in_place_child::<M, I>(target, &|| keyword.to_string(), x, bits.as_deref_mut(), true, false);
            check!(self, M, ok, m, Message::Static(if m { MATCHED_ALL } else { DID_NOT_MATCH_ALL }), keyword);
        }
        if let Some(list) = &n.all_of {
            let mut all = true;
            for (i, &b) in list.iter().enumerate() {
                let (m, _) =
                    self.eval_in_place_child::<M, I>(b, &|| format!("allOf/{i}"), x, bits.as_deref_mut(), true, true);
                if !m {
                    if !M::COLLECT {
                        return false;
                    }
                    all = false;
                }
            }
            check!(self, M, ok, all, Message::Static(if all { MATCHED_ALL } else { DID_NOT_MATCH_ALL }), "allOf");
        }
        if let Some(list) = &n.any_of {
            let mut any = false;
            // Every branch runs when results are collected or evaluated properties/items are tracked.
            let exhaustive = M::COLLECT || bits.is_some();
            // Fail-fast evaluation only tries the branches a discriminator property can select.
            let selection = if exhaustive { Selection::All } else { select(n.any_of_discriminator.as_deref(), x) };
            for i in selection.indexes(list.len()) {
                let b = list[i];
                let (m, _) =
                    self.eval_in_place_child::<M, I>(b, &|| format!("anyOf/{i}"), x, bits.as_deref_mut(), false, true);
                if m {
                    any = true;
                    if !exhaustive {
                        break;
                    }
                }
            }
            check!(
                self,
                M,
                ok,
                any,
                Message::Static(if any { MATCHED_AT_LEAST_ONE } else { DID_NOT_MATCH_AT_LEAST_ONE }),
                "anyOf"
            );
        }
        if let Some(list) = &n.one_of {
            let mut matched = 0;
            let mut only: Option<Bits> = None;
            let track = bits.is_some();
            // Branches a discriminator rules out cannot match, so fail-fast evaluation skips them.
            let selection =
                if M::COLLECT || track { Selection::All } else { select(n.one_of_discriminator.as_deref(), x) };
            for i in selection.indexes(list.len()) {
                let b = list[i];
                // Evaluated properties/items are merged only when exactly one branch matched, so collect them aside.
                let mut aside = if track { Some(Bits::new(container_len(x))) } else { None };
                let (m, scratch) =
                    self.eval_in_place_child::<M, I>(b, &|| format!("oneOf/{i}"), x, aside.as_mut(), false, true);
                if m {
                    matched += 1;
                    only = if track { aside.or(scratch) } else { None };
                    if !M::COLLECT && matched > 1 {
                        return false;
                    }
                }
            }
            if matched == 1 {
                if let (Some(b), Some(o)) = (bits.as_deref_mut(), &only) {
                    b.merge(o);
                }
            }
            let message = match matched {
                0 => MATCHED_NO_SCHEMA,
                1 => MATCHED_EXACTLY_ONE,
                _ => MATCHED_MORE_THAN_ONE,
            };
            check!(self, M, ok, matched == 1, Message::Static(message), "oneOf");
        }
        if let Some(not) = n.not {
            // Not elided, never contributes results or evaluated properties/items. A not on an in-place cycle is
            // under the depth guard, like every other in-place applicator.
            let target = if M::COLLECT { not } else { self.p.fast_target[not as usize] };
            let guarded = self.node(target).in_place_cycle;
            if guarded {
                self.depth += 1;
            }
            let inner = if guarded && self.depth > self.p.max_depth {
                self.depth_exceeded = true;
                false
            } else if M::COLLECT {
                let pointer = &self.node(not).pointer;
                self.col().begin_child_context(Some("not"), Some(pointer), None);
                let inner = self.eval_node::<Collect, I>(not, x, None);
                self.col().pop_child_context();
                inner
            } else {
                self.fast(target, x)
            };
            if guarded {
                self.depth -= 1;
            }
            check!(self, M, ok, !inner, Message::Static(if inner { MATCHED_NOT } else { DID_NOT_MATCH_NOT }), "not");
        }
        if let Some(cond_node) = n.if_ {
            let (cond, _) =
                self.eval_in_place_child::<M, I>(cond_node, &|| "if".to_string(), x, bits.as_deref_mut(), false, true);
            if M::COLLECT {
                self.col().evaluated_keyword(
                    true,
                    Message::Static(if cond { MATCHED_IF_FOR_THEN } else { MATCHED_IF_FOR_ELSE }),
                    "if",
                );
            }
            if cond {
                if let Some(t) = n.then {
                    let (m, _) =
                        self.eval_in_place_child::<M, I>(t, &|| "then".to_string(), x, bits.as_deref_mut(), true, true);
                    check!(self, M, ok, m, Message::Static(if m { MATCHED_THEN } else { DID_NOT_MATCH_THEN }), "then");
                }
            } else if let Some(e) = n.else_ {
                let (m, _) = self.eval_in_place_child::<M, I>(e, &|| "else".to_string(), x, bits, true, true);
                check!(self, M, ok, m, Message::Static(if m { MATCHED_ELSE } else { DID_NOT_MATCH_ELSE }), "else");
            }
        }
        ok
    }
}
