//! The parser: a pattern to its syntax tree, in either grammar, with the limits of size (`parse.go`).

use std::collections::HashMap;
use std::fmt;
use std::sync::{Arc, LazyLock};

use super::Rune;
use super::charset::{CharSet, DIGIT_PAIRS, DOT_SET, MAX_RUNE, SPACE_PAIRS, WORD_PAIRS, property_set};
use super::fold::{FOLD_WORD_SET, NOT_FOLD_WORD_SET, fold_closure};
use super::ucd;
use super::units::{high_surrogate, low_surrogate};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum NodeKind {
    Empty,
    Char,
    Set,
    Cat,
    Alt,
    Group,
    Repeat,
    Bol,
    Eol,
    /// `LineStart` and `LineEnd` are `^` and `$` inside a group with the m modifier.
    LineStart,
    LineEnd,
    WordBoundary,
    NotWordBoundary,
    Look,
    Backref,
}

/// The max of a quantifier with no upper bound.
pub(crate) const UNBOUNDED: i32 = -1;

/// Caps the bounds of a counted quantifier. No text that fits in memory reaches a larger count.
const MAX_COUNT: i64 = i32::MAX as i64;

// The limits on the size of one pattern. A pattern may come from outside, so reading and compiling one must take
// bounded time and memory whatever it holds. A pattern over a limit is refused with a `TooLargeError`, which is not
// the error of a pattern that is not valid.

/// The longest pattern that is read, in bytes of UTF-8.
pub const MAX_PATTERN_LENGTH: usize = 1 << 20;
/// Caps the depth of groups, which bounds the recursion of the parser and of the compilers.
pub const MAX_NESTING: usize = 1000;
/// The largest size a pattern may have, in the bytes the parser counts for it: `NODE_SIZE` for each node of its
/// syntax tree, and `RANGE_SIZE` for each range of a class as its members are read, for each range a case
/// insensitive group or a negated property adds, for each alternative that encloses a named group and for each
/// group that shares its name. The tree, the program compiled from it and the time to make both are proportional to
/// that size, so it bounds them all: a pattern at the limit takes a few milliseconds and a few megabytes.
pub const MAX_SIZE: usize = 1 << 20;
const NODE_SIZE: usize = 32;
const RANGE_SIZE: usize = 8;

/// Which limit of size a pattern is over.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Limit {
    /// [`MAX_PATTERN_LENGTH`].
    Length,
    /// [`MAX_NESTING`].
    Nesting,
    /// [`MAX_SIZE`].
    Size,
}

impl Limit {
    /// The name of the limit: "length", "nesting" or "size".
    pub fn name(self) -> &'static str {
        match self {
            Limit::Length => "length",
            Limit::Nesting => "nesting",
            Limit::Size => "size",
        }
    }
}

/// Reports a pattern over one of the limits of size. `limit` names the limit and `value` is its value. The pattern
/// was not read to its end, so the error says nothing of whether it is valid.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct TooLargeError {
    pub limit: Limit,
    pub value: usize,
}

impl fmt::Display for TooLargeError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self.limit {
            Limit::Length => write!(f, "the pattern is longer than {} bytes", self.value),
            Limit::Nesting => write!(f, "the groups of the pattern nest more than {} deep", self.value),
            Limit::Size => write!(f, "the pattern has a size of more than {} bytes when it is read", self.value),
        }
    }
}

/// Why a pattern was not read: it is over a limit of size, or it is not valid under the chosen grammar.
#[derive(Clone, Debug, PartialEq, Eq)]
pub(crate) enum ParseError {
    TooLarge(TooLargeError),
    /// The reason, and the offset in the pattern's characters.
    Syntax(&'static str, usize),
}

type Parse<T> = Result<T, ParseError>;

/// One element of the syntax tree of a pattern.
#[derive(Clone, Debug)]
pub(crate) struct Node {
    pub kind: NodeKind,
    /// The code point of a `Char`.
    pub r: Rune,
    /// The class of a `Set`.
    pub set: Option<Arc<CharSet>>,
    /// The children of `Cat` and `Alt`, and the one child of `Group`, `Repeat` and `Look`.
    pub subs: Vec<Node>,
    /// `min`, `max` and `lazy` describe a `Repeat`.
    pub min: i32,
    pub max: i32,
    pub lazy: bool,
    /// The number of the capturing group of a `Group`, counted from 1.
    pub group: i32,
    /// The groups a `Backref` names. A name can belong to several groups in separate alternatives, of which at most
    /// one has taken part at any time.
    pub groups: Vec<i32>,
    /// The group name of a named backreference until it is resolved.
    pub name: Vec<Rune>,
    /// Marks a `Backref`, `WordBoundary` or `NotWordBoundary` inside a case-insensitive group.
    pub fold: bool,
    /// `behind` and `negative` describe a `Look`.
    pub behind: bool,
    pub negative: bool,
}

impl Node {
    pub(crate) fn new(kind: NodeKind) -> Node {
        Node {
            kind,
            r: 0,
            set: None,
            subs: Vec::new(),
            min: 0,
            max: 0,
            lazy: false,
            group: 0,
            groups: Vec::new(),
            name: Vec::new(),
            fold: false,
            behind: false,
            negative: false,
        }
    }

    fn with_subs(kind: NodeKind, subs: Vec<Node>) -> Node {
        Node { subs, ..Node::new(kind) }
    }

    fn char(r: Rune) -> Node {
        Node { r, ..Node::new(NodeKind::Char) }
    }

    fn set(set: Arc<CharSet>) -> Node {
        Node { set: Some(set), ..Node::new(NodeKind::Set) }
    }

    /// The class of a `Set`.
    pub(crate) fn class(&self) -> &Arc<CharSet> {
        self.set.as_ref().expect("a set node has a class")
    }
}

/// Names one alternative of one disjunction.
#[derive(Clone, Copy)]
struct Branch {
    disjunction: usize,
    alternative: usize,
}

/// A capturing group with a name, and the alternatives that enclose it.
struct NamedGroup {
    group: i32,
    path: Vec<Branch>,
}

/// Reads a pattern by recursive descent. With `unicode` set it applies the grammar of the u flag. Without it, it
/// applies the Annex B grammar of patterns with no flag.
pub(crate) struct Parser {
    /// The pattern as code points with the u flag, and as UTF-16 code units without it (see `code_units`).
    p: Vec<Rune>,
    i: usize,
    unicode: bool,
    depth: usize,
    /// `group_count` and `has_names` come from a scan of the whole pattern before parsing, because the meaning of
    /// `\1` and `\k` depends on groups that may follow them.
    group_count: i64,
    has_names: bool,
    groups: i32,
    names: HashMap<Vec<Rune>, Vec<NamedGroup>>,
    named_refs: bool,
    /// The alternatives that enclose the current position, innermost last.
    path: Vec<Branch>,
    disjunctions: usize,
    /// The modifiers in force, which only a group such as `(?i:...)` changes.
    icase: bool,
    multiline: bool,
    dot_all: bool,
    /// Records that the pattern has a construct only the backtracking matcher can run.
    pub(crate) needs_backtracking: bool,
    /// The size of what has been read so far (see [`MAX_SIZE`]).
    pub(crate) size: usize,
}

static ANY_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(&[0, MAX_RUNE], false));
static DIGIT_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(DIGIT_PAIRS, false));
static NOT_DIGIT_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(DIGIT_PAIRS, true));
static WORD_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(WORD_PAIRS, false));
static NOT_WORD_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(WORD_PAIRS, true));
static SPACE_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(SPACE_PAIRS, false));
static NOT_SPACE_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(SPACE_PAIRS, true));

/// Reads the pattern. It returns an error for a pattern that is not valid under the chosen grammar, and a
/// `TooLargeError` for a pattern over a limit of size, which it stops reading at the limit.
pub(crate) fn parse(pattern: &str, unicode: bool) -> Parse<(Node, Parser)> {
    if pattern.len() > MAX_PATTERN_LENGTH {
        return Err(ParseError::TooLarge(TooLargeError { limit: Limit::Length, value: MAX_PATTERN_LENGTH }));
    }
    let mut p: Vec<Rune> = pattern.chars().map(|c| c as Rune).collect();
    if !unicode {
        p = code_units(p);
    }
    let mut ps = Parser {
        p,
        i: 0,
        unicode,
        depth: 0,
        group_count: 0,
        has_names: false,
        groups: 0,
        names: HashMap::new(),
        named_refs: false,
        path: Vec::new(),
        disjunctions: 0,
        icase: false,
        multiline: false,
        dot_all: false,
        needs_backtracking: false,
        size: 0,
    };
    ps.scan_groups();
    let mut root = ps.disjunction()?;
    if ps.i < ps.p.len() {
        if ps.p[ps.i] == ')' as Rune {
            return ps.fail("unmatched ')'");
        }
        return ps.fail("unexpected character");
    }
    if ps.named_refs {
        ps.resolve(&mut root)?;
    }
    Ok((root, ps))
}

/// The UTF-16 code units of a pattern. Without the u flag ECMA-262 reads a pattern, and matches a text, a code unit
/// at a time. A character beyond the Basic Multilingual Plane is then two atoms, a quantifier after it repeats the
/// second, and in a class it is two members, so a range between two such characters runs from a low surrogate to a
/// high one and is an error.
fn code_units(p: Vec<Rune>) -> Vec<Rune> {
    let n = p.iter().filter(|&&c| c > 0xFFFF).count();
    if n == 0 {
        return p;
    }
    let mut units = Vec::with_capacity(p.len() + n);
    for c in p {
        if c > 0xFFFF {
            units.push(high_surrogate(c));
            units.push(low_surrogate(c));
        } else {
            units.push(c);
        }
    }
    units
}

const fn ch(c: char) -> Rune {
    c as Rune
}

fn is_digit(c: Rune) -> bool {
    (ch('0')..=ch('9')).contains(&c)
}

/// Whether the first of two runs of decimal digits is the greater number. The runs are compared as written, because
/// a count has no limit of size in the grammar and `number` stops at `MAX_COUNT`.
fn greater_as_written(mut a: &[Rune], mut b: &[Rune]) -> bool {
    while a.first() == Some(&ch('0')) {
        a = &a[1..];
    }
    while b.first() == Some(&ch('0')) {
        b = &b[1..];
    }
    if a.len() != b.len() {
        return a.len() > b.len();
    }
    for (x, y) in a.iter().zip(b) {
        if x != y {
            return x > y;
        }
    }
    false
}

/// Whether two positions lie in different alternatives of some disjunction.
fn separate_alternatives(a: &[Branch], b: &[Branch]) -> bool {
    for (x, y) in a.iter().zip(b) {
        if x.disjunction != y.disjunction {
            break;
        }
        if x.alternative != y.alternative {
            return true;
        }
    }
    false
}

fn hex_value(c: Rune) -> i32 {
    match c {
        0x30..=0x39 => c - 0x30,
        0x61..=0x66 => c - 0x61 + 10,
        0x41..=0x46 => c - 0x41 + 10,
        _ => -1,
    }
}

fn is_ascii_letter(c: Rune) -> bool {
    (ch('a')..=ch('z')).contains(&c) || (ch('A')..=ch('Z')).contains(&c)
}

/// Whether `c` is one of the characters the u flag lets an identity escape name.
fn syntax_character(c: Rune) -> bool {
    u8::try_from(c).is_ok_and(|c| b"^$\\.*+?()[]{}|/".contains(&c))
}

static ID_START: LazyLock<&'static ucd::Table> =
    LazyLock::new(|| ucd::binary_table("ID_Start").expect("the tables have ID_Start"));
static ID_CONTINUE: LazyLock<&'static ucd::Table> =
    LazyLock::new(|| ucd::binary_table("ID_Continue").expect("the tables have ID_Continue"));

fn append_class_atom(pairs: &mut Vec<Rune>, c: Rune, set: Option<&Arc<CharSet>>) {
    match set {
        Some(set) => pairs.extend_from_slice(&set.ranges),
        None => {
            pairs.push(c);
            pairs.push(c);
        }
    }
}

impl Parser {
    /// Adds to the size of the pattern, and stops the parser when the size is over its limit.
    fn charge(&mut self, bytes: usize) -> Parse<()> {
        self.size += bytes;
        if self.size > MAX_SIZE {
            return Err(ParseError::TooLarge(TooLargeError { limit: Limit::Size, value: MAX_SIZE }));
        }
        Ok(())
    }

    fn fail<T>(&self, msg: &'static str) -> Parse<T> {
        Err(ParseError::Syntax(msg, self.i))
    }

    /// Gives every named backreference the groups of its name. A name no group has is an error.
    fn resolve(&self, n: &mut Node) -> Parse<()> {
        if n.kind == NodeKind::Backref && !n.name.is_empty() {
            let Some(named) = self.names.get(&n.name) else {
                return self.fail("reference to an unknown group name");
            };
            n.groups.extend(named.iter().map(|g| g.group));
        }
        for sub in &mut n.subs {
            self.resolve(sub)?;
        }
        Ok(())
    }

    /// Counts the capturing groups of the pattern and notes whether any is named.
    fn scan_groups(&mut self) {
        let mut in_class = false;
        let s = &self.p;
        let mut k = 0;
        while k < s.len() {
            let c = s[k];
            if c == ch('\\') {
                k += 1;
            } else if c == ch('[') {
                in_class = true;
            } else if c == ch(']') {
                in_class = false;
            } else if c == ch('(') && !in_class {
                if k + 1 >= s.len() || s[k + 1] != ch('?') {
                    self.group_count += 1;
                } else if k + 3 < s.len() && s[k + 2] == ch('<') && s[k + 3] != ch('=') && s[k + 3] != ch('!') {
                    self.group_count += 1;
                    self.has_names = true;
                }
            }
            k += 1;
        }
    }

    fn more(&self) -> bool {
        self.i < self.p.len()
    }

    fn peek(&self) -> Rune {
        self.p.get(self.i).copied().unwrap_or(-1)
    }

    fn eat(&mut self, c: char) -> bool {
        if self.i < self.p.len() && self.p[self.i] == c as Rune {
            self.i += 1;
            return true;
        }
        false
    }

    fn disjunction(&mut self) -> Parse<Node> {
        self.depth += 1;
        if self.depth > MAX_NESTING {
            return Err(ParseError::TooLarge(TooLargeError { limit: Limit::Nesting, value: MAX_NESTING }));
        }
        self.disjunctions += 1;
        self.path.push(Branch { disjunction: self.disjunctions, alternative: 0 });
        let last = self.path.len() - 1;
        let first = self.alternative()?;
        if self.peek() != ch('|') {
            self.path.truncate(last);
            self.depth -= 1;
            return Ok(first);
        }
        let mut alt = Node::with_subs(NodeKind::Alt, vec![first]);
        while self.eat('|') {
            self.charge(NODE_SIZE)?;
            self.path[last].alternative += 1;
            let sub = self.alternative()?;
            alt.subs.push(sub);
        }
        self.path.truncate(last);
        self.depth -= 1;
        Ok(alt)
    }

    fn alternative(&mut self) -> Parse<Node> {
        let mut terms = Vec::new();
        while self.more() && self.peek() != ch('|') && self.peek() != ch(')') {
            terms.push(self.term()?);
        }
        Ok(match terms.len() {
            0 => Node::new(NodeKind::Empty),
            1 => terms.pop().expect("one term"),
            _ => Node::with_subs(NodeKind::Cat, terms),
        })
    }

    /// The node for one literal character. Inside a case-insensitive group that is the class of the characters
    /// equivalent to it.
    fn char_node(&mut self, c: Rune) -> Node {
        if self.icase {
            let (set, _) = fold_closure(&CharSet::shared(&[c, c], false), self.unicode);
            if set.ranges.len() != 2 || set.ranges[0] != set.ranges[1] {
                return Node::set(set);
            }
        }
        Node::char(c)
    }

    /// The node for a class. Inside a case-insensitive group the class also takes every character equivalent to one
    /// of its members.
    fn set_node(&mut self, set: Arc<CharSet>) -> Parse<Node> {
        let set = if self.icase { self.fold(&set)? } else { set };
        Ok(Node::set(set))
    }

    /// The set with every character equivalent to one of its members. It charges the pattern for the work and for
    /// the set: a byte for each character with a case variant that the set holds, and its ranges.
    fn fold(&mut self, set: &Arc<CharSet>) -> Parse<Arc<CharSet>> {
        let (closed, variants) = fold_closure(set, self.unicode);
        self.charge(variants + RANGE_SIZE * closed.ranges.len() / 2)?;
        Ok(closed)
    }

    fn term(&mut self) -> Parse<Node> {
        // The atom, and the repetition or the group around it.
        self.charge(2 * NODE_SIZE)?;
        let c = self.peek();
        let mut quantifiable = true;
        let atom = match u8::try_from(c).unwrap_or(0) {
            b'^' => {
                self.i += 1;
                quantifiable = false;
                if self.multiline {
                    self.needs_backtracking = true;
                    Node::new(NodeKind::LineStart)
                } else {
                    Node::new(NodeKind::Bol)
                }
            }
            b'$' => {
                self.i += 1;
                quantifiable = false;
                if self.multiline {
                    self.needs_backtracking = true;
                    Node::new(NodeKind::LineEnd)
                } else {
                    Node::new(NodeKind::Eol)
                }
            }
            b'(' => {
                let (atom, q) = self.group()?;
                quantifiable = q;
                atom
            }
            b'.' => {
                self.i += 1;
                Node::set(if self.dot_all { ANY_SET.clone() } else { DOT_SET.clone() })
            }
            b'[' => self.class()?,
            b'\\' => {
                let (atom, q) = self.atom_escape()?;
                quantifiable = q;
                atom
            }
            b'*' | b'+' | b'?' => return self.fail("nothing to repeat"),
            b'{' => {
                if self.unicode || self.looks_like_quantifier() {
                    return self.fail("nothing to repeat");
                }
                self.i += 1;
                self.char_node(ch('{'))
            }
            b']' | b'}' => {
                if self.unicode {
                    return self.fail("lone bracket");
                }
                self.i += 1;
                self.char_node(c)
            }
            _ => {
                self.i += 1;
                self.char_node(c)
            }
        };
        let Some((min, max, lazy)) = self.quantifier()? else { return Ok(atom) };
        if !quantifiable {
            return self.fail("nothing to repeat");
        }
        Ok(Node { min, max, lazy, ..Node::with_subs(NodeKind::Repeat, vec![atom]) })
    }

    /// Whether the text at the current `{` reads as `{n}`, `{n,}` or `{n,m}`.
    fn looks_like_quantifier(&self) -> bool {
        let s = &self.p;
        let mut k = self.i + 1;
        let start = k;
        while k < s.len() && is_digit(s[k]) {
            k += 1;
        }
        if k == start || k >= s.len() {
            return false;
        }
        if s[k] == ch('}') {
            return true;
        }
        if s[k] != ch(',') {
            return false;
        }
        k += 1;
        while k < s.len() && is_digit(s[k]) {
            k += 1;
        }
        k < s.len() && s[k] == ch('}')
    }

    /// Reads a run of decimal digits, saturating at `MAX_COUNT`.
    fn number(&mut self) -> Parse<i64> {
        let start = self.i;
        let mut v: i64 = 0;
        while self.i < self.p.len() && is_digit(self.p[self.i]) {
            if v < MAX_COUNT {
                v = v * 10 + (self.p[self.i] - ch('0')) as i64;
                if v > MAX_COUNT {
                    v = MAX_COUNT;
                }
            }
            self.i += 1;
        }
        if start == self.i {
            return self.fail("expected a number");
        }
        Ok(v)
    }

    /// Reads a quantifier, when one is here: its bounds, and whether it is lazy.
    fn quantifier(&mut self) -> Parse<Option<(i32, i32, bool)>> {
        let c = self.peek();
        let (min, max);
        if c == ch('*') {
            self.i += 1;
            (min, max) = (0, UNBOUNDED);
        } else if c == ch('+') {
            self.i += 1;
            (min, max) = (1, UNBOUNDED);
        } else if c == ch('?') {
            self.i += 1;
            (min, max) = (0, 1);
        } else if c == ch('{') && self.looks_like_quantifier() {
            self.i += 1;
            let first = self.i;
            let lo = self.number()? as i32;
            let mut hi = lo;
            let first_end = self.i;
            if self.eat(',') {
                if self.peek() == ch('}') {
                    hi = UNBOUNDED;
                } else {
                    let second = self.i;
                    hi = self.number()? as i32;
                    // Both counts stop at MAX_COUNT, so the numbers are compared as they are written.
                    if greater_as_written(&self.p[first..first_end], &self.p[second..self.i]) {
                        return self.fail("numbers out of order in quantifier");
                    }
                }
            }
            if !self.eat('}') {
                return self.fail("malformed quantifier");
            }
            (min, max) = (lo, hi);
        } else {
            return Ok(None);
        }
        Ok(Some((min, max, self.eat('?'))))
    }

    /// Reads a group or a lookaround. It also reports whether a quantifier may follow.
    fn group(&mut self) -> Parse<(Node, bool)> {
        self.i += 1;
        if !self.eat('?') {
            return Ok((self.capture(None)?, true));
        }
        if self.eat(':') {
            let body = self.disjunction()?;
            self.close()?;
            return Ok((Node::with_subs(NodeKind::Group, vec![body]), true));
        }
        let c = self.peek();
        if c == ch('i') || c == ch('m') || c == ch('s') || c == ch('-') {
            return Ok((self.modifier_group()?, true));
        }
        if self.eat('=') || self.eat('!') {
            let negative = self.p[self.i - 1] == ch('!');
            let body = self.disjunction()?;
            self.close()?;
            self.needs_backtracking = true;
            // Annex B lets a quantifier follow a lookahead. The u flag does not.
            return Ok((Node { negative, ..Node::with_subs(NodeKind::Look, vec![body]) }, !self.unicode));
        }
        if self.eat('<') {
            if self.eat('=') || self.eat('!') {
                let negative = self.p[self.i - 1] == ch('!');
                let body = self.disjunction()?;
                self.close()?;
                self.needs_backtracking = true;
                return Ok((Node { negative, behind: true, ..Node::with_subs(NodeKind::Look, vec![body]) }, false));
            }
            let name = self.group_name()?;
            // Two groups may share a name only when they are in separate alternatives of one disjunction, so that
            // no match can go through both.
            if let Some(others) = self.names.get(&name) {
                for other in others {
                    if !separate_alternatives(&other.path, &self.path) {
                        return self.fail("duplicate group name");
                    }
                }
            }
            return Ok((self.capture(Some(name))?, true));
        }
        self.fail("invalid group")
    }

    /// Reads a group that turns modifiers on or off for its body, such as `(?i:...)` or `(?s-i:...)`. The position
    /// is after the `(?`.
    fn modifier_group(&mut self) -> Parse<Node> {
        let (icase, multiline, dot_all) = (self.icase, self.multiline, self.dot_all);
        let mut seen = [false; 3];
        let (mut removing, mut count) = (false, 0);
        while !self.eat(':') {
            let c = self.peek();
            self.i += 1;
            let flag;
            if c == ch('i') {
                (flag, self.icase) = (0, !removing);
            } else if c == ch('m') {
                (flag, self.multiline) = (1, !removing);
            } else if c == ch('s') {
                (flag, self.dot_all) = (2, !removing);
            } else if c == ch('-') {
                if removing {
                    return self.fail("invalid group modifier");
                }
                removing = true;
                continue;
            } else {
                return self.fail("invalid group modifier");
            }
            if seen[flag] {
                return self.fail("repeated group modifier");
            }
            seen[flag] = true;
            count += 1;
        }
        if count == 0 {
            return self.fail("invalid group modifier");
        }
        let body = self.disjunction()?;
        self.close()?;
        (self.icase, self.multiline, self.dot_all) = (icase, multiline, dot_all);
        Ok(Node::with_subs(NodeKind::Group, vec![body]))
    }

    fn capture(&mut self, name: Option<Vec<Rune>>) -> Parse<Node> {
        self.groups += 1;
        let group = self.groups;
        if let Some(name) = name {
            // The path is kept, and every group of the same name is compared with it.
            let same = self.names.get(&name).map_or(0, Vec::len);
            self.charge(RANGE_SIZE * (self.path.len() + same))?;
            let path = self.path.clone();
            self.names.entry(name).or_default().push(NamedGroup { group, path });
        }
        let body = self.disjunction()?;
        self.close()?;
        Ok(Node { group, ..Node::with_subs(NodeKind::Group, vec![body]) })
    }

    fn close(&mut self) -> Parse<()> {
        if !self.eat(')') {
            return self.fail("unterminated group");
        }
        Ok(())
    }

    /// Reads a group name up to and including its `>`. A character of the name may be written as a `\u` escape.
    fn group_name(&mut self) -> Parse<Vec<Rune>> {
        let mut name: Vec<Rune> = Vec::new();
        while !self.eat('>') {
            if !self.more() {
                return self.fail("invalid group name");
            }
            let mut c = self.peek();
            self.i += 1;
            if c == ch('\\') {
                if !self.eat('u') {
                    return self.fail("invalid group name");
                }
                c = self.unicode_escape(true);
                if c < 0 {
                    return self.fail("invalid group name");
                }
            } else if (0xD800..=0xDBFF).contains(&c) && (0xDC00..=0xDFFF).contains(&self.peek()) {
                // Without the u flag the pattern is code units, and a name still takes a surrogate pair as one
                // character.
                c = 0x10000 + ((c - 0xD800) << 10) + (self.peek() - 0xDC00);
                self.i += 1;
            }
            if name.is_empty() {
                if !(c == ch('$') || c == ch('_') || ID_START.contains(c)) {
                    return self.fail("invalid group name");
                }
            } else if !(c == ch('$') || c == 0x200C || c == 0x200D || ID_CONTINUE.contains(c)) {
                return self.fail("invalid group name");
            }
            name.push(c);
        }
        if name.is_empty() {
            return self.fail("invalid group name");
        }
        Ok(name)
    }

    /// Reads an escape outside a class. It also reports whether a quantifier may follow.
    fn atom_escape(&mut self) -> Parse<(Node, bool)> {
        self.i += 1;
        if !self.more() {
            return self.fail("\\ at end of pattern");
        }
        let c = self.peek();
        if c == ch('b') || c == ch('B') {
            self.i += 1;
            let mut boundary = Node::new(if c == ch('B') { NodeKind::NotWordBoundary } else { NodeKind::WordBoundary });
            // With the u flag, a case-insensitive word boundary counts two more characters as word characters.
            if self.unicode && self.icase {
                boundary.fold = true;
                self.needs_backtracking = true;
            }
            return Ok((boundary, false));
        }
        if c == ch('k') {
            // Annex B reads \k as the letter k in a pattern with no named group.
            if self.unicode || self.has_names {
                self.i += 1;
                if !self.eat('<') {
                    return self.fail("invalid named reference");
                }
                let name = self.group_name()?;
                self.named_refs = true;
                self.needs_backtracking = true;
                return Ok((Node { name, fold: self.icase, ..Node::new(NodeKind::Backref) }, true));
            }
            self.i += 1;
            return Ok((self.char_node(ch('k')), true));
        }
        if (ch('1')..=ch('9')).contains(&c) {
            let start = self.i;
            let n = self.number()?;
            if n <= self.group_count {
                self.needs_backtracking = true;
                return Ok((Node { groups: vec![n as i32], fold: self.icase, ..Node::new(NodeKind::Backref) }, true));
            }
            if self.unicode {
                return self.fail("reference to a group that does not exist");
            }
            // Annex B reads it as an octal escape, or as the digit itself.
            self.i = start;
            let octal = self.legacy_octal();
            return Ok((self.char_node(octal), true));
        }
        if let Some(set) = self.class_escape(c)? {
            return Ok((self.set_node(set)?, true));
        }
        let escaped = self.character_escape(false)?;
        Ok((self.char_node(escaped), true))
    }

    /// Reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one (8
    /// or 9) stands for itself.
    fn legacy_octal(&mut self) -> Rune {
        let mut v: Rune = 0;
        let mut k = 0;
        while k < 3
            && self.more()
            && (ch('0')..=ch('7')).contains(&self.peek())
            && v * 8 + (self.peek() - ch('0')) <= 0o377
        {
            v = v * 8 + (self.peek() - ch('0'));
            self.i += 1;
            k += 1;
        }
        if k == 0 {
            let d = self.peek();
            self.i += 1;
            return d;
        }
        v
    }

    /// Reads a class escape such as `\d` or `\p{...}` whose letter is `c`. It returns `None`, consuming nothing,
    /// when `c` does not start one.
    fn class_escape(&mut self, c: Rune) -> Parse<Option<Arc<CharSet>>> {
        let set = match u8::try_from(c).unwrap_or(0) {
            b'd' => &DIGIT_SET,
            b'D' => &NOT_DIGIT_SET,
            b'w' => {
                if self.unicode && self.icase {
                    &FOLD_WORD_SET
                } else {
                    &WORD_SET
                }
            }
            b'W' => {
                if self.unicode && self.icase {
                    &NOT_FOLD_WORD_SET
                } else {
                    &NOT_WORD_SET
                }
            }
            b's' => &SPACE_SET,
            b'S' => &NOT_SPACE_SET,
            b'p' | b'P' => {
                // Without the u flag \p is the letter p.
                if !self.unicode {
                    return Ok(None);
                }
                self.i += 1;
                if !self.eat('{') {
                    return self.fail("invalid property name");
                }
                let start = self.i;
                while self.more() && self.peek() != ch('}') {
                    self.i += 1;
                }
                // A name ECMA-262 defines is ASCII. Any other character makes a name no table has.
                let expr: String =
                    self.p[start..self.i].iter().map(|&r| char::from_u32(r as u32).unwrap_or('\u{FFFD}')).collect();
                if !self.eat('}') {
                    return self.fail("invalid property name");
                }
                let Some(set) = property_set(&expr) else {
                    return self.fail("invalid property name");
                };
                if c == ch('P') {
                    self.charge(RANGE_SIZE * set.ranges.len() / 2)?;
                    return Ok(Some(CharSet::shared(&set.ranges, true)));
                }
                return Ok(Some(set));
            }
            _ => return Ok(None),
        };
        self.i += 1;
        Ok(Some((**set).clone()))
    }

    /// Reads exactly `n` hexadecimal digits. It returns -1, consuming nothing, when they are not there.
    fn hex(&mut self, n: usize) -> Rune {
        if self.i + n > self.p.len() {
            return -1;
        }
        let mut v: Rune = 0;
        for k in 0..n {
            let d = hex_value(self.p[self.i + k]);
            if d < 0 {
                return -1;
            }
            v = v * 16 + d;
        }
        self.i += n;
        v
    }

    /// Reads the escape after a backslash, in or out of a class, and returns the code point it names.
    fn character_escape(&mut self, in_class: bool) -> Parse<Rune> {
        let c = self.peek();
        self.i += 1;
        match u8::try_from(c).unwrap_or(0) {
            b'f' => return Ok(0x0C),
            b'n' => return Ok(0x0A),
            b'r' => return Ok(0x0D),
            b't' => return Ok(0x09),
            b'v' => return Ok(0x0B),
            b'c' => {
                let l = self.peek();
                if is_ascii_letter(l) {
                    self.i += 1;
                    return Ok(l % 32);
                }
                if !self.unicode && in_class && (is_digit(l) || l == ch('_')) {
                    self.i += 1;
                    return Ok(l % 32);
                }
                if self.unicode {
                    return self.fail("invalid control escape");
                }
                // Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
                self.i -= 1;
                return Ok(ch('\\'));
            }
            b'0' => {
                if self.more() && is_digit(self.peek()) {
                    if self.unicode {
                        return self.fail("invalid decimal escape");
                    }
                    self.i -= 1;
                    return Ok(self.legacy_octal());
                }
                return Ok(0);
            }
            b'x' => {
                let h = self.hex(2);
                if h < 0 {
                    if self.unicode {
                        return self.fail("invalid \\x escape");
                    }
                    return Ok(ch('x'));
                }
                return Ok(h);
            }
            b'u' => {
                let u = self.unicode_escape(self.unicode);
                if u < 0 {
                    if self.unicode {
                        return self.fail("invalid \\u escape");
                    }
                    return Ok(ch('u'));
                }
                return Ok(u);
            }
            _ => {}
        }
        if (ch('1')..=ch('9')).contains(&c) && !self.unicode && in_class {
            self.i -= 1;
            return Ok(self.legacy_octal());
        }
        // Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B allows
        // any character, except k in a pattern with a named group. Outside a class atom_escape reads \k before this
        // is reached, so here the \k of such a pattern is in a class, where it is not a reference either.
        if self.unicode && !syntax_character(c) && !(in_class && c == ch('-')) {
            return self.fail("invalid escape");
        }
        if c == ch('k') && self.has_names {
            return self.fail("invalid escape");
        }
        Ok(c)
    }

    /// Reads what follows a `\u` and returns the code point, or -1, consuming nothing, when it is not a well-formed
    /// escape. With `unicode_mode` set, as it is with the u flag and always in a group name, it accepts the
    /// `\u{...}` form and reads a surrogate pair written as two escapes as one code point. Without it each escape
    /// is one code unit.
    fn unicode_escape(&mut self, unicode_mode: bool) -> Rune {
        let start = self.i;
        if unicode_mode && self.eat('{') {
            let digits = self.i;
            let mut v: Rune = 0;
            while self.more() && hex_value(self.peek()) >= 0 {
                if v <= MAX_RUNE {
                    v = v * 16 + hex_value(self.peek());
                }
                self.i += 1;
            }
            if digits == self.i || !self.eat('}') || v > MAX_RUNE {
                self.i = start;
                return -1;
            }
            return v;
        }
        let u = self.hex(4);
        if u < 0 {
            return -1;
        }
        if unicode_mode
            && (0xD800..=0xDBFF).contains(&u)
            && self.i + 6 <= self.p.len()
            && self.p[self.i] == ch('\\')
            && self.p[self.i + 1] == ch('u')
        {
            let save = self.i;
            self.i += 2;
            let low = self.hex(4);
            if (0xDC00..=0xDFFF).contains(&low) {
                return 0x10000 + ((u - 0xD800) << 10) + (low - 0xDC00);
            }
            self.i = save;
        }
        u
    }

    /// Reads a character class from its `[` to its `]`.
    fn class(&mut self) -> Parse<Node> {
        self.i += 1;
        let negated = self.eat('^');
        let mut pairs: Vec<Rune> = Vec::new();
        // The ranges read so far that the size of the pattern has been charged for.
        let mut charged = 0;
        loop {
            if !self.more() {
                return self.fail("unterminated character class");
            }
            if self.eat(']') {
                break;
            }
            self.charge(RANGE_SIZE * (pairs.len() / 2 - charged))?;
            charged = pairs.len() / 2;
            let (first, first_set) = self.class_atom()?;
            if self.peek() == ch('-') && self.i + 1 < self.p.len() && self.p[self.i + 1] != ch(']') {
                self.i += 1;
                let (second, second_set) = self.class_atom()?;
                if first_set.is_some() || second_set.is_some() {
                    if self.unicode {
                        return self.fail("invalid character class range");
                    }
                    // Annex B reads the '-' as itself when a class escape is at either end.
                    append_class_atom(&mut pairs, first, first_set.as_ref());
                    pairs.push(ch('-'));
                    pairs.push(ch('-'));
                    append_class_atom(&mut pairs, second, second_set.as_ref());
                    continue;
                }
                if second < first {
                    return self.fail("range out of order in character class");
                }
                pairs.push(first);
                pairs.push(second);
                continue;
            }
            append_class_atom(&mut pairs, first, first_set.as_ref());
        }
        // Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a
        // negated class excludes exactly those characters.
        self.charge(RANGE_SIZE * (pairs.len() / 2 - charged))?;
        let mut set = CharSet::shared(&pairs, false);
        if self.icase {
            set = self.fold(&set)?;
        }
        if negated {
            set = CharSet::shared(&set.ranges, true);
        }
        Ok(Node::set(set))
    }

    /// Reads one member of a class. It returns a code point, or a set for a class escape.
    fn class_atom(&mut self) -> Parse<(Rune, Option<Arc<CharSet>>)> {
        let c = self.peek();
        self.i += 1;
        if c != ch('\\') {
            return Ok((c, None));
        }
        if !self.more() {
            return self.fail("\\ at end of pattern");
        }
        let e = self.peek();
        if e == ch('b') {
            self.i += 1;
            return Ok((0x08, None));
        }
        if let Some(set) = self.class_escape(e)? {
            return Ok((0, Some(set)));
        }
        Ok((self.character_escape(true)?, None))
    }
}
