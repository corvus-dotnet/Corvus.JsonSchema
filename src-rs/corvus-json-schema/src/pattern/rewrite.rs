//! What regress is given for a pattern, where its own answer for the pattern as written is not ECMA-262's.
//!
//! regress is the engine for the patterns no other matcher takes. It differs from ECMA-262 (and from V8) in a few
//! places, and each is patched here by handing it another pattern with the same meaning:
//!
//! - **A repetition inside a repetition.** On entering a loop regress sets the loop's iteration count to zero and
//!   leaves nothing on its backtrack stack to put the old count back. A loop that is entered again (it is inside
//!   another loop) and then backtracked into where it ran before has a count of zero there. It then runs more
//!   iterations than its maximum, refuses to leave before its minimum, and no longer refuses an iteration that
//!   matches nothing, so `(?:(?:a*)*)*x` on `a` loops for ever and takes memory without bound. The count only
//!   matters when the loop can be resumed inside its body, when it is not `X*` over a body that cannot match
//!   nothing. Such a loop is written without a count: `X{m,n}` as `m` copies of `X` and then `n - m` optional
//!   copies, each an alternation (`(?:X(?:X|)|)`), and `X{m,}` as `m` copies and then `(?:X)*`. ECMA-262 refuses an
//!   iteration that matches nothing once the minimum is met. So each optional copy of a body that can match nothing
//!   is written to take a character where that is plain (`a*` as `a+`, `(?:a|)` as `(?:a)`), and otherwise as
//!   `(?=([^]*))X(?!\k)`. The group takes the rest of the text, and the rest of the text matches again only at
//!   the position it was taken from.
//! - **A reference to a group from inside the group.** regress does not take the end of a group back on
//!   backtracking, so `(x|\1)x` reads the `x` of the alternative it has left. In ECMA-262 a group has no value until
//!   it closes. Every reference is resolved here from the place of its group: one that can never be read after its
//!   group has matched is written as nothing.
//! - **Two groups with one name.** regress matches nothing for a reference to the name. `(?:(?<a>x)|(?<a>y))\k<a>`
//!   is `(?:(x)|(y))(?:\1)(?:\2)` here. At most one of the groups has a value, and a group without one matches
//!   nothing.
//! - **Case-insensitive groups.** In a pattern valid only without the `u` flag, `(?i:...)` compares by the
//!   uppercase form of a UTF-16 code unit, where regress folds as with the flag. The literals and classes of such a
//!   group are written out as the classes they stand for, and the modifier is taken off. A backreference in such a
//!   group cannot be written out, and is still compared by folding. With the flag, `\W` in a class of such a group
//!   does not hold the two characters that fold to a word character.
//!
//! **A repetition that is too large to write out.** The copies multiply with the repetitions around them, and each
//! optional copy is a group inside the one before. regress is given at most [`MAX_REWRITTEN`] characters and reads
//! at most [`MAX_DEPTH`] levels. The rule for a pattern that comes to more is by repetition, not by pattern:
//!
//! - A repetition whose body cannot match nothing is left as it is written, with its count, and the rest of the
//!   pattern (its own body too) is rewritten as before. regress may then lose its count: it can run more
//!   iterations than its maximum, or leave before its minimum. It cannot go round for ever, as each iteration
//!   takes a character. This is what regress was given before there was a rewriter.
//! - A repetition whose body can match nothing is never left so. With a lost count regress does not refuse its
//!   empty iterations, and that is the match that takes memory without bound. If it does not fit, the pattern is
//!   refused ([`Rewrite::TooLarge`]).
//!
//! The rule can be by repetition because each loop of regress has a count of its own, and the fault is in how one
//! loop's count is kept. regress's backtrack stack is as long as the path it is on, and a path is long without
//! bound only when some loop goes round without taking a character. That loop has a body that can match nothing
//! and a count that was lost, so it is one of those written out here. A loop left with its count is not it.
//! [`Emitter::limit`] says which repetition is left when a limit is met.
//!
//! One more rewriting is for time, not for the answer: a repetition of a body that can only match nothing is its
//! body once (see `Emitter::repeat`).
//!
//! A pattern that needs none of this goes to regress as it is written, and the only cost is one parse when the
//! schema is compiled. Nothing here runs when a string is matched.
//!
//! The parser reads the grammar of the `u` flag or the Annex B grammar, as far as the structure of a pattern goes.
//! regress has already said the pattern is valid. The one thing the parser adds to that is that an assertion
//! (`\b`, `\B`) takes no quantifier, which regress allows. A pattern the parser does not read (a group name
//! written with an escape) goes to regress as it is written.
//!
//! The first two are faults of regress 0.12.0 that a change to it would mend: an undo record for the count where
//! `EnterLoop` resets it, and one for the end of a group in `EndCaptureGroup`
//! (`classicalbacktrack.rs`). With those, the rewriting of repetitions and of references could go.

/// The most text a rewritten pattern may have. A repetition of repetitions multiplies out, and past this a
/// repetition is left with its count, or the pattern is refused.
const MAX_REWRITTEN: usize = 65_536;

/// The deepest pattern regress reads (its `MAX_NESTING_DEPTH`): the pattern is level 1, and what is in a group is a
/// level below the group.
const MAX_DEPTH: u32 = 256;

/// How many repetitions are left with their counts one at a time, each with the pattern written again, before
/// every repetition that may be left so is.
const MAX_LEFT_ONE_AT_A_TIME: usize = 8;

pub(super) enum Rewrite {
    /// regress takes the pattern as written.
    Unchanged,
    Rewritten(String),
    /// The rewritten pattern would be too large, and regress may not have the pattern as written: it has a
    /// repetition that is too large to write out or stands with one, and a body that can match nothing.
    TooLarge,
}

/// Why reading or rewriting a pattern stopped.
#[derive(Debug)]
enum Stop {
    /// A quantifier follows something that takes none.
    Invalid,
    /// The parser does not read the pattern. It is left to regress as written.
    Unread,
    TooLarge,
}

/// Whether the pattern has a quantifier on an assertion. regress accepts `\b+` and ECMA-262 does not.
pub(super) fn quantified_assertion(pattern: &str, unicode: bool) -> bool {
    // A quantifier right after `\b` or `\B` is needed for it (an escaped backslash or a class can also read so).
    let b = pattern.as_bytes();
    let candidate =
        b.windows(3).any(|w| w[0] == b'\\' && (w[1] | 0x20) == b'b' && matches!(w[2], b'*' | b'+' | b'?' | b'{'));
    candidate && with_stack(pattern, || matches!(Parser::parse(pattern, unicode), Err(Stop::Invalid))) == Some(true)
}

/// Groups nested deeper than this are read and written on a stack of their own.
const NESTED_ON_THE_CALLERS_STACK: usize = 32;

/// Runs `f`, which reads `pattern`, where the stack holds it. The parser and the writer go down a level for each
/// group inside a group. regress reads 256 levels, which is more than a thread with a small stack has room for
/// here, so a pattern nested deeper than a pattern is ever written by hand gets a thread with a stack for it.
/// `None` when there is no such thread to be had.
fn with_stack<T: Send>(pattern: &str, f: impl FnOnce() -> T + Send) -> Option<T> {
    let (mut depth, mut deepest) = (0usize, 0);
    for b in pattern.bytes() {
        match b {
            b'(' => {
                depth += 1;
                deepest = deepest.max(depth);
            }
            b')' => depth = depth.saturating_sub(1),
            _ => {}
        }
    }
    if deepest <= NESTED_ON_THE_CALLERS_STACK {
        return Some(f());
    }
    std::thread::scope(|scope| {
        std::thread::Builder::new().stack_size(32 << 20).spawn_scoped(scope, f).ok().and_then(|t| t.join().ok())
    })
}

/// The pattern regress should compile for `pattern`, which it has accepted (with the `u` flag when `unicode`).
pub(super) fn for_engine(pattern: &str, unicode: bool) -> Rewrite {
    with_stack(pattern, || rewrite(pattern, unicode, false)).unwrap_or(Rewrite::TooLarge)
}

/// `always`: the pattern is written again even when regress would answer it as written.
fn rewrite(pattern: &str, unicode: bool, always: bool) -> Rewrite {
    let Ok(parsed) = Parser::parse(pattern, unicode) else { return Rewrite::Unchanged };
    let mut scan =
        Scan { refs: vec![0; parsed.groups as usize + 1], needed: parsed.special, unbounded: false, open: Vec::new() };
    scan.node(&parsed.root, false);
    if !scan.needed && !always {
        return Rewrite::Unchanged;
    }
    let mut emitter = Emitter {
        refs: scan.refs,
        read_outside: std::collections::HashMap::new(),
        bind: Vec::new(),
        instances: 0,
        spent: 0,
        depth: 1,
        deepest: 1,
        left: std::collections::HashSet::new(),
        leave_all: false,
        writing: Vec::new(),
        leave: None,
    };
    // Each pass writes the whole pattern. One that meets a limit names a repetition to leave with its count
    // (`Emitter::limit`), and the next pass leaves it.
    loop {
        emitter.bind = vec![Binding::Unset; parsed.groups as usize + 1];
        (emitter.instances, emitter.spent, emitter.depth, emitter.deepest) = (0, 0, 1, 1);
        emitter.writing.clear();
        emitter.leave = None;
        match emitter.node(&parsed.root, false, false) {
            Ok(out) => {
                let written = out.render(emitter.instances);
                debug_assert!(nesting(&written) <= emitter.deepest, "{pattern} as {written}");
                return Rewrite::Rewritten(written);
            }
            Err(_) => match emitter.leave.take() {
                Some(repetition) if !emitter.leave_all => {
                    emitter.left.insert(repetition);
                    emitter.leave_all = emitter.left.len() > MAX_LEFT_ONE_AT_A_TIME;
                }
                // No repetition that may be left with its count is being written where the limit is met. With no
                // repetition in the whole pattern that goes round for ever in regress, regress has the pattern as
                // it is written, which is what it had before there was a rewriter.
                _ => return if scan.unbounded { Rewrite::TooLarge } else { Rewrite::Unchanged },
            },
        }
    }
}

/// How deep the groups of a written pattern go, as regress counts it. No class of a written pattern has a class
/// inside it. It is a check on the level the writer counts, in a debug build.
fn nesting(written: &str) -> u32 {
    let (mut depth, mut deepest, mut in_class, mut escaped) = (1, 1, false, false);
    for c in written.chars() {
        match c {
            _ if escaped => escaped = false,
            '\\' => escaped = true,
            '[' => in_class = true,
            ']' => in_class = false,
            '(' if !in_class => {
                depth += 1;
                deepest = deepest.max(depth);
            }
            ')' if !in_class => depth -= 1,
            _ => {}
        }
    }
    deepest
}

// ---------------------------------------------------------------------------------------------------------------------
// The syntax tree

/// The capturing groups inside a node, by number: `start..end`.
type Groups = std::ops::Range<u32>;

enum Node {
    Empty,
    /// One character: a literal, an escape, a class or the dot, as the text regress is to read.
    Atom(String),
    /// `^`, `$`, `\b` or `\B`.
    Assertion(String),
    Cat(Vec<Node>),
    Alt(Vec<(Node, Groups)>),
    Group {
        /// The number of a capturing group, from 1, or 0.
        index: u32,
        /// The modifiers of a group such as `(?i-m:...)`: `i-m`.
        modifiers: Option<String>,
        body: Box<Node>,
    },
    Look {
        behind: bool,
        negative: bool,
        body: Box<Node>,
        groups: Groups,
    },
    Repeat {
        min: u32,
        max: Option<u32>,
        lazy: bool,
        /// The quantifier as written.
        text: String,
        body: Box<Node>,
        groups: Groups,
    },
    Backref {
        /// The groups it names: one for a number, and every group of the name for a name.
        groups: Vec<u32>,
        /// Whether it is compared without case and the modifier that said so has been taken off.
        icase: bool,
    },
}

/// Whether the node can match without taking a character. It may say yes for a node that cannot.
fn nullable(n: &Node) -> bool {
    match n {
        Node::Atom(_) => false,
        Node::Empty | Node::Assertion(_) | Node::Look { .. } | Node::Backref { .. } => true,
        Node::Cat(v) => v.iter().all(nullable),
        Node::Alt(v) => v.iter().any(|(n, _)| nullable(n)),
        Node::Group { body, .. } => nullable(body),
        Node::Repeat { min, body, .. } => *min == 0 || nullable(body),
    }
}

/// Whether regress, once it is through the node, has nothing on its backtrack stack that resumes inside it. A
/// lookaround runs on a stack of its own, which is dropped when it ends. A repetition of an exact count has no
/// choice to come back to. Any other repetition and any alternation has.
fn choice_free(n: &Node) -> bool {
    match n {
        Node::Empty | Node::Atom(_) | Node::Assertion(_) | Node::Look { .. } | Node::Backref { .. } => true,
        Node::Cat(v) => v.iter().all(choice_free),
        Node::Alt(_) => false,
        Node::Group { body, .. } => choice_free(body),
        Node::Repeat { min, max, body, .. } => *max == Some(*min) && choice_free(body),
    }
}

/// Whether the node matches nothing wherever it matches: it has assertions and lookarounds, and no character and
/// no reference.
fn zero_width(n: &Node) -> bool {
    match n {
        Node::Empty | Node::Assertion(_) | Node::Look { .. } => true,
        Node::Atom(_) | Node::Backref { .. } => false,
        Node::Cat(v) => v.iter().all(zero_width),
        Node::Alt(v) => v.iter().all(|(n, _)| zero_width(n)),
        Node::Group { body, .. } => zero_width(body),
        Node::Repeat { max, body, .. } => *max == Some(0) || zero_width(body),
    }
}

/// Whether the node can be written so that it matches the same as it does now, except that it does not match
/// nothing: `a*` as `a+`, `(?:a|)` as `(?:a)`, `(\ba?)` as `(\ba)`. The emitter writes it so when asked for
/// `some`. A node that never matches nothing is such a node as it is.
fn can_take_some(n: &Node) -> bool {
    if !nullable(n) {
        return true;
    }
    match n {
        // An alternative that can only match nothing goes, and each of the others is written so.
        Node::Alt(v) => {
            v.iter().any(|(n, _)| !zero_width(n)) && v.iter().all(|(n, _)| zero_width(n) || can_take_some(n))
        }
        Node::Group { body, .. } => can_take_some(body),
        // One part takes the characters, and the rest are assertions.
        Node::Cat(v) => {
            let mut taking = v.iter().filter(|n| !zero_width(n));
            matches!((taking.next(), taking.next()), (Some(n), None) if can_take_some(n))
        }
        // A character repeated from zero times is repeated from once.
        Node::Repeat { min: 0, body, .. } => matches!(**body, Node::Atom(_)),
        _ => false,
    }
}

/// Whether regress loses the count of this repetition when the repetition is inside another one: it can be resumed
/// inside its body, and the count then decides something.
fn loses_count(min: u32, max: Option<u32>, body: &Node) -> bool {
    !choice_free(body) && !(min == 0 && max.is_none() && !nullable(body))
}

// ---------------------------------------------------------------------------------------------------------------------
// The parser

struct Parsed {
    root: Node,
    groups: u32,
    /// The pattern has something that is rewritten wherever it is: a case-insensitive group that regress reads
    /// with another meaning.
    special: bool,
}

struct Parser {
    c: Vec<char>,
    i: usize,
    unicode: bool,
    /// The capturing groups of the whole pattern and whether any has a name, from a scan before parsing: what `\1`
    /// and `\k` mean depends on groups that may follow them.
    group_count: u32,
    has_names: bool,
    groups: u32,
    names: Vec<(String, u32)>,
    icase: bool,
    special: bool,
    depth: u32,
}

/// The least and the most counts of a quantifier (no most for `*`, `+` and `{n,}`), whether it is lazy, and its
/// text.
type Quantifier = (u32, Option<u32>, bool, String);

/// What an escape or a literal stands for.
enum Escape {
    /// One character (a UTF-16 code unit without the `u` flag).
    Char(u32),
    /// A set: `\d`, `\w`, `\s`, their negations, or a property.
    Set,
}

impl Parser {
    fn parse(pattern: &str, unicode: bool) -> Result<Parsed, Stop> {
        let mut p = Parser {
            c: pattern.chars().collect(),
            i: 0,
            unicode,
            group_count: 0,
            has_names: false,
            groups: 0,
            names: Vec::new(),
            icase: false,
            special: false,
            depth: 0,
        };
        p.scan_groups();
        let mut root = p.disjunction()?;
        if p.i < p.c.len() {
            return Err(Stop::Unread);
        }
        p.resolve(&mut root)?;
        Ok(Parsed { root, groups: p.groups, special: p.special })
    }

    fn scan_groups(&mut self) {
        let s = &self.c;
        let (mut in_class, mut k) = (false, 0);
        while k < s.len() {
            match s[k] {
                '\\' => k += 1,
                '[' => in_class = true,
                ']' => in_class = false,
                '(' if !in_class => {
                    if s.get(k + 1) != Some(&'?') {
                        self.group_count += 1;
                    } else if s.get(k + 2) == Some(&'<') && !matches!(s.get(k + 3), Some('=' | '!') | None) {
                        self.group_count += 1;
                        self.has_names = true;
                    }
                }
                _ => {}
            }
            k += 1;
        }
    }

    /// Gives every named reference the groups of its name.
    fn resolve(&self, n: &mut Node) -> Result<(), Stop> {
        match n {
            Node::Cat(v) => v.iter_mut().try_for_each(|n| self.resolve(n)),
            Node::Alt(v) => v.iter_mut().try_for_each(|(n, _)| self.resolve(n)),
            Node::Group { body, .. } | Node::Look { body, .. } | Node::Repeat { body, .. } => self.resolve(body),
            Node::Backref { groups, .. } => {
                // A named reference holds the index of its name until here, above the group numbers.
                if let Some(name) = groups.first().and_then(|g| g.checked_sub(NAME_BASE)) {
                    let name = &self.names[name as usize].0;
                    *groups = self.names.iter().filter(|(n, g)| n == name && *g != 0).map(|(_, g)| *g).collect();
                    if groups.is_empty() {
                        return Err(Stop::Unread);
                    }
                }
                Ok(())
            }
            _ => Ok(()),
        }
    }

    fn peek(&self) -> Option<char> {
        self.c.get(self.i).copied()
    }

    fn eat(&mut self, c: char) -> bool {
        let found = self.peek() == Some(c);
        self.i += usize::from(found);
        found
    }

    fn disjunction(&mut self) -> Result<Node, Stop> {
        self.depth += 1;
        if self.depth > 300 {
            return Err(Stop::Unread);
        }
        let start = self.groups;
        let first = self.alternative()?;
        if self.peek() != Some('|') {
            self.depth -= 1;
            return Ok(first);
        }
        let mut alternatives = vec![(first, start..self.groups)];
        while self.eat('|') {
            let start = self.groups;
            let alternative = self.alternative()?;
            alternatives.push((alternative, start..self.groups));
        }
        self.depth -= 1;
        Ok(Node::Alt(alternatives))
    }

    fn alternative(&mut self) -> Result<Node, Stop> {
        let mut terms = Vec::new();
        while !matches!(self.peek(), None | Some('|' | ')')) {
            terms.push(self.term()?);
        }
        Ok(match terms.len() {
            0 => Node::Empty,
            1 => terms.pop().unwrap(),
            _ => Node::Cat(terms),
        })
    }

    fn term(&mut self) -> Result<Node, Stop> {
        let start = self.groups;
        let c = self.peek().ok_or(Stop::Unread)?;
        let (atom, quantifiable) = match c {
            '^' | '$' => {
                self.i += 1;
                (Node::Assertion(c.to_string()), false)
            }
            '(' => self.group()?,
            '.' => {
                self.i += 1;
                (Node::Atom(".".into()), true)
            }
            '[' => (self.class()?, true),
            '\\' => self.atom_escape()?,
            '*' | '+' | '?' => return Err(Stop::Unread),
            '{' if self.unicode || self.quantifier_ahead() => return Err(Stop::Unread),
            ']' | '}' if self.unicode => return Err(Stop::Unread),
            _ => {
                self.i += 1;
                // Annex B reads a lone bracket or brace as itself. Escaped, it stays one wherever it lands.
                let text = if matches!(c, '{' | '}' | ']') { format!("\\{c}") } else { c.to_string() };
                (self.character(c as u32, text), true)
            }
        };
        let Some((min, max, lazy, text)) = self.quantifier()? else { return Ok(atom) };
        if !quantifiable {
            return Err(Stop::Invalid);
        }
        Ok(Node::Repeat { min, max, lazy, text, body: Box::new(atom), groups: start..self.groups })
    }

    /// Whether the text at the current `{` reads as `{n}`, `{n,}` or `{n,m}`.
    fn quantifier_ahead(&self) -> bool {
        let s = &self.c;
        let mut k = self.i + 1;
        let start = k;
        while s.get(k).is_some_and(char::is_ascii_digit) {
            k += 1;
        }
        if k == start {
            return false;
        }
        match s.get(k) {
            Some('}') => true,
            Some(',') => {
                k += 1;
                while s.get(k).is_some_and(char::is_ascii_digit) {
                    k += 1;
                }
                s.get(k) == Some(&'}')
            }
            _ => false,
        }
    }

    /// A run of decimal digits, at most `u32::MAX`.
    fn number(&mut self) -> u32 {
        let mut v = 0u32;
        while let Some(d) = self.peek().and_then(|c| c.to_digit(10)) {
            v = v.saturating_mul(10).saturating_add(d);
            self.i += 1;
        }
        v
    }

    /// A quantifier, if one is next: its least and most counts, whether it is lazy, and its text.
    fn quantifier(&mut self) -> Result<Option<Quantifier>, Stop> {
        let start = self.i;
        let (min, max) = match self.peek() {
            Some('*') => {
                self.i += 1;
                (0, None)
            }
            Some('+') => {
                self.i += 1;
                (1, None)
            }
            Some('?') => {
                self.i += 1;
                (0, Some(1))
            }
            Some('{') if self.quantifier_ahead() => {
                self.i += 1;
                let min = self.number();
                let max = if !self.eat(',') {
                    Some(min)
                } else if self.peek() == Some('}') {
                    None
                } else {
                    Some(self.number())
                };
                self.i += 1;
                if max.is_some_and(|max| max < min) {
                    return Err(Stop::Unread);
                }
                (min, max)
            }
            _ => return Ok(None),
        };
        let lazy = self.eat('?');
        Ok(Some((min, max, lazy, self.c[start..self.i].iter().collect())))
    }

    /// A group or a lookaround, and whether a quantifier may follow it.
    fn group(&mut self) -> Result<(Node, bool), Stop> {
        self.i += 1;
        if !self.eat('?') {
            return Ok((self.capture(None)?, true));
        }
        if self.eat(':') {
            let body = Box::new(self.disjunction()?);
            self.close()?;
            return Ok((Node::Group { index: 0, modifiers: None, body }, true));
        }
        if matches!(self.peek(), Some('i' | 'm' | 's' | '-')) {
            return Ok((self.modifier_group()?, true));
        }
        let behind = self.eat('<');
        if matches!(self.peek(), Some('=' | '!')) {
            let negative = self.peek() == Some('!');
            self.i += 1;
            let start = self.groups;
            let body = Box::new(self.disjunction()?);
            self.close()?;
            // Annex B lets a quantifier follow a lookahead. The `u` flag does not, and nothing lets one follow a
            // lookbehind.
            return Ok((Node::Look { behind, negative, body, groups: start..self.groups }, !behind && !self.unicode));
        }
        if !behind {
            return Err(Stop::Unread);
        }
        let name = self.group_name()?;
        Ok((self.capture(Some(name))?, true))
    }

    /// A group name up to and including its `>`. A name written with an escape is not read.
    fn group_name(&mut self) -> Result<String, Stop> {
        let start = self.i;
        while self.peek().is_some_and(|c| c != '>' && c != '\\') {
            self.i += 1;
        }
        let name: String = self.c[start..self.i].iter().collect();
        if !self.eat('>') || name.is_empty() {
            return Err(Stop::Unread);
        }
        Ok(name)
    }

    fn capture(&mut self, name: Option<String>) -> Result<Node, Stop> {
        self.groups += 1;
        let index = self.groups;
        if let Some(name) = name {
            self.names.push((name, index));
        }
        let body = Box::new(self.disjunction()?);
        self.close()?;
        Ok(Node::Group { index, modifiers: None, body })
    }

    fn close(&mut self) -> Result<(), Stop> {
        if self.eat(')') { Ok(()) } else { Err(Stop::Unread) }
    }

    /// A group that turns modifiers on or off for its body, such as `(?i:...)` or `(?s-i:...)`, from after `(?`.
    fn modifier_group(&mut self) -> Result<Node, Stop> {
        let outer = self.icase;
        let (mut on, mut off, mut removing) = (String::new(), String::new(), false);
        while !self.eat(':') {
            match self.peek() {
                Some('-') if !removing => removing = true,
                Some(c @ ('i' | 'm' | 's')) => {
                    if c == 'i' {
                        self.icase = !removing;
                    }
                    // Without the `u` flag a case-insensitive group is written out, and the modifier goes.
                    if c != 'i' || self.unicode {
                        if removing { &mut off } else { &mut on }.push(c);
                    }
                }
                _ => return Err(Stop::Unread),
            }
            self.i += 1;
        }
        if self.icase && !self.unicode {
            self.special = true;
        }
        let body = Box::new(self.disjunction()?);
        self.close()?;
        self.icase = outer;
        let modifiers = match (on.is_empty(), off.is_empty()) {
            (true, true) => None,
            (_, true) => Some(on),
            _ => Some(format!("{on}-{off}")),
        };
        Ok(Node::Group { index: 0, modifiers, body })
    }

    /// The node of one character with the code `value`, written `text`.
    fn character(&mut self, value: u32, text: String) -> Node {
        if self.icase && !self.unicode && value <= 0xFFFF {
            let mut set = CodeUnits::new();
            set.insert(value as u16, value as u16);
            return Node::Atom(set.fold().write(false));
        }
        Node::Atom(text)
    }

    /// An escape outside a class, and whether a quantifier may follow it.
    fn atom_escape(&mut self) -> Result<(Node, bool), Stop> {
        let start = self.i;
        self.i += 1;
        let c = self.peek().ok_or(Stop::Unread)?;
        match c {
            'b' | 'B' => {
                self.i += 1;
                return Ok((Node::Assertion(format!("\\{c}")), false));
            }
            // Annex B reads `\k` as the letter in a pattern with no named group.
            'k' if self.unicode || self.has_names => {
                self.i += 1;
                if !self.eat('<') {
                    return Err(Stop::Unread);
                }
                let name = self.group_name()?;
                self.names.push((name, 0));
                let groups = vec![NAME_BASE + self.names.len() as u32 - 1];
                return Ok((Node::Backref { groups, icase: self.icase && !self.unicode }, true));
            }
            '1'..='9' => {
                let n = self.number();
                if n <= self.group_count {
                    return Ok((Node::Backref { groups: vec![n], icase: self.icase && !self.unicode }, true));
                }
                if self.unicode {
                    return Err(Stop::Unread);
                }
                // Annex B reads it as an octal escape, or as the digit itself. Written as a number it would
                // become a reference if the rewritten pattern had that many groups.
                self.i = start + 1;
                let value = self.legacy_octal();
                return Ok((self.character(value, unit_text(value)), true));
            }
            _ => {}
        }
        Ok(match self.escape(false)? {
            Escape::Set => (Node::Atom(self.c[start..self.i].iter().collect()), true),
            Escape::Char(value) => {
                // With the `u` flag the escape is valid as written. Without it, an escape Annex B reads in a way
                // of its own (`\c` before no letter, `\x` before no digits) is written as what it stands for.
                let text = if self.unicode || value > 0xFFFF {
                    self.c[start..self.i].iter().collect()
                } else {
                    unit_text(value)
                };
                (self.character(value, text), true)
            }
        })
    }

    /// An Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one (8 or 9)
    /// stands for itself.
    fn legacy_octal(&mut self) -> u32 {
        let (mut v, mut k) = (0, 0);
        while k < 3
            && let Some(d) = self.peek().and_then(|c| c.to_digit(8))
            && v * 8 + d <= 0o377
        {
            v = v * 8 + d;
            self.i += 1;
            k += 1;
        }
        if k == 0 {
            let d = self.peek().map_or(0, |c| c as u32);
            self.i += 1;
            return d;
        }
        v
    }

    /// Exactly `n` hexadecimal digits, or nothing consumed.
    fn hex(&mut self, n: usize) -> Option<u32> {
        let digits = self.c.get(self.i..self.i + n)?;
        let mut v = 0;
        for d in digits {
            v = v * 16 + d.to_digit(16)?;
        }
        self.i += n;
        Some(v)
    }

    /// The escape after a backslash, in or out of a class, from its first character.
    fn escape(&mut self, in_class: bool) -> Result<Escape, Stop> {
        let c = self.peek().ok_or(Stop::Unread)?;
        self.i += 1;
        Ok(Escape::Char(match c {
            'd' | 'D' | 'w' | 'W' | 's' | 'S' => return Ok(Escape::Set),
            'p' | 'P' if self.unicode => {
                while self.peek().ok_or(Stop::Unread)? != '}' {
                    self.i += 1;
                }
                self.i += 1;
                return Ok(Escape::Set);
            }
            'f' => 0x0C,
            'n' => 0x0A,
            'r' => 0x0D,
            't' => 0x09,
            'v' => 0x0B,
            'c' => match self.peek() {
                Some(l) if l.is_ascii_alphabetic() => {
                    self.i += 1;
                    l as u32 % 32
                }
                Some(l) if !self.unicode && in_class && (l.is_ascii_digit() || l == '_') => {
                    self.i += 1;
                    l as u32 % 32
                }
                _ if self.unicode => return Err(Stop::Unread),
                // Annex B reads a `\c` that is not a control escape as a backslash, and the `c` comes next.
                _ => {
                    self.i -= 1;
                    '\\' as u32
                }
            },
            '0' if self.peek().is_some_and(|d| d.is_ascii_digit()) => {
                if self.unicode {
                    return Err(Stop::Unread);
                }
                self.i -= 1;
                self.legacy_octal()
            }
            '0' => 0,
            'x' => match self.hex(2) {
                Some(v) => v,
                None if self.unicode => return Err(Stop::Unread),
                None => 'x' as u32,
            },
            'u' => match self.unicode_escape() {
                Some(v) => v,
                None if self.unicode => return Err(Stop::Unread),
                None => 'u' as u32,
            },
            '1'..='9' if !self.unicode && in_class => {
                self.i -= 1;
                self.legacy_octal()
            }
            _ => c as u32,
        }))
    }

    /// What follows `\u`, or nothing consumed. With the `u` flag it may be `{...}`, and two escapes that are a
    /// surrogate pair are one character.
    fn unicode_escape(&mut self) -> Option<u32> {
        let start = self.i;
        if self.unicode && self.eat('{') {
            let mut v = 0u32;
            let digits = self.i;
            while let Some(d) = self.peek().and_then(|c| c.to_digit(16)) {
                v = v.saturating_mul(16).saturating_add(d);
                self.i += 1;
            }
            if digits == self.i || !self.eat('}') || v > 0x10FFFF {
                self.i = start;
                return None;
            }
            return Some(v);
        }
        let u = self.hex(4)?;
        if self.unicode && (0xD800..0xDC00).contains(&u) && self.c.get(self.i..self.i + 2) == Some(&['\\', 'u']) {
            let save = self.i;
            self.i += 2;
            match self.hex(4) {
                Some(low) if (0xDC00..0xE000).contains(&low) => {
                    return Some(0x10000 + ((u - 0xD800) << 10) + (low - 0xDC00));
                }
                _ => self.i = save,
            }
        }
        Some(u)
    }

    /// A class from its `[` to its `]`.
    fn class(&mut self) -> Result<Node, Stop> {
        let start = self.i;
        self.i += 1;
        let negated = self.eat('^');
        // The members as code units, for a class of a case-insensitive group without the `u` flag.
        let mut set = (self.icase && !self.unicode).then(CodeUnits::new);
        // Whether the members are all read as code units, which a class with a character beyond the Basic
        // Multilingual Plane is not.
        let mut units = true;
        // Where `\W` is, for a class of a case-insensitive group with the `u` flag.
        let mut not_word = Vec::new();
        loop {
            if self.eat(']') {
                break;
            }
            let at = self.i;
            let first = self.class_atom(&mut set)?;
            if self.c[at..self.i] == ['\\', 'W'] {
                not_word.push(at - start);
            }
            if self.peek() == Some('-') && self.c.get(self.i + 1).is_some_and(|&c| c != ']') {
                self.i += 1;
                let second = self.class_atom(&mut set)?;
                let Some(set) = &mut set else { continue };
                match (first, second) {
                    (Some(lo), Some(hi)) if lo <= hi && hi <= 0xFFFF => set.insert(lo as u16, hi as u16),
                    (Some(_), Some(_)) => units = false,
                    // Annex B reads the `-` as itself when a set is at either end.
                    (lo, hi) => {
                        set.insert(b'-' as u16, b'-' as u16);
                        for end in [lo, hi].into_iter().flatten() {
                            units &= end <= 0xFFFF;
                            set.insert(end as u16, end as u16);
                        }
                    }
                }
            } else if let (Some(c), Some(set)) = (first, &mut set) {
                units &= c <= 0xFFFF;
                set.insert(c as u16, c as u16);
            }
        }
        let text: String = self.c[start..self.i].iter().collect();
        if !self.icase {
            return Ok(Node::Atom(text));
        }
        if let Some(set) = set {
            // Compared by the uppercase form of each code unit: the class of everything equal to a member.
            return Ok(Node::Atom(if units { set.fold().write(negated) } else { format!("(?i:{text})") }));
        }
        if not_word.is_empty() {
            return Ok(Node::Atom(text));
        }
        // With the `u` flag and no case, `\W` leaves out the two characters that fold to a word character (U+017F
        // and U+212A) as well. regress keeps them in, and they then match `s` and `k`.
        self.special = true;
        let chars: Vec<char> = text.chars().collect();
        let mut out = String::new();
        let mut k = 0;
        while k < chars.len() {
            if not_word.contains(&k) {
                out.push_str(NOT_WORD_FOLDED);
                k += 2;
            } else {
                out.push(chars[k]);
                k += 1;
            }
        }
        Ok(Node::Atom(out))
    }

    /// One member of a class: its character, or `None` for a set, which goes into `set`.
    fn class_atom(&mut self, set: &mut Option<CodeUnits>) -> Result<Option<u32>, Stop> {
        let c = self.peek().ok_or(Stop::Unread)?;
        self.i += 1;
        if c != '\\' {
            return Ok(Some(c as u32));
        }
        let e = self.peek().ok_or(Stop::Unread)?;
        if e == 'b' {
            self.i += 1;
            return Ok(Some(8));
        }
        Ok(match self.escape(true)? {
            Escape::Char(v) => Some(v),
            Escape::Set => {
                if let Some(set) = set {
                    set.escape(e);
                }
                None
            }
        })
    }
}

/// `\W` in a class, in a case-insensitive group with the `u` flag: everything but the word characters and the two
/// characters that fold to one.
const NOT_WORD_FOLDED: &str =
    r"\u0000-\u002f\u003a-\u0040\u005b-\u005e\u0060\u007b-\u017e\u0180-\u2129\u212b-\u{10ffff}";

/// Named references hold `NAME_BASE` plus the index of their name until the pattern is read to its end.
const NAME_BASE: u32 = 1 << 24;

/// A UTF-16 code unit as regress reads it without the `u` flag.
fn unit_text(value: u32) -> String {
    format!("\\u{value:04x}")
}

// ---------------------------------------------------------------------------------------------------------------------
// Case-insensitive matching without the `u` flag

/// A set of UTF-16 code units.
struct CodeUnits(Box<[u64; 1024]>);

/// ECMA-262's `Canonicalize` without the `u` flag: the uppercase form of a code unit when that is one code unit,
/// except that nothing outside ASCII becomes ASCII. The uppercase forms are the standard library's.
fn canonicalize(unit: u16) -> u16 {
    let Some(c) = char::from_u32(unit as u32) else { return unit };
    let mut upper = c.to_uppercase();
    match (upper.next(), upper.next()) {
        (Some(u), None) if (u as u32) <= 0xFFFF && !(unit >= 128 && (u as u32) < 128) => u as u16,
        _ => unit,
    }
}

impl CodeUnits {
    fn new() -> CodeUnits {
        CodeUnits(Box::new([0; 1024]))
    }

    fn insert(&mut self, lo: u16, hi: u16) {
        for u in lo..=hi {
            self.0[u as usize / 64] |= 1 << (u % 64);
        }
    }

    fn contains(&self, u: u16) -> bool {
        self.0[u as usize / 64] >> (u % 64) & 1 != 0
    }

    /// The set of `\d`, `\w` or `\s` (or, for the capital, everything else) joins the set.
    fn escape(&mut self, e: char) {
        let mut members = CodeUnits::new();
        match e.to_ascii_lowercase() {
            'd' => members.insert(b'0' as u16, b'9' as u16),
            'w' => {
                for (lo, hi) in [(b'0', b'9'), (b'A', b'Z'), (b'a', b'z'), (b'_', b'_')] {
                    members.insert(lo as u16, hi as u16);
                }
            }
            _ => {
                for (lo, hi) in [
                    (9, 13),
                    (0x20, 0x20),
                    (0xA0, 0xA0),
                    (0x1680, 0x1680),
                    (0x2000, 0x200A),
                    (0x2028, 0x2029),
                    (0x202F, 0x202F),
                    (0x205F, 0x205F),
                    (0x3000, 0x3000),
                    (0xFEFF, 0xFEFF),
                ] {
                    members.insert(lo, hi);
                }
            }
        }
        let negated = e.is_ascii_uppercase();
        for (word, member) in self.0.iter_mut().zip(members.0.iter()) {
            *word |= if negated { !member } else { *member };
        }
    }

    /// Every code unit that is equal, without case, to a member.
    fn fold(&self) -> CodeUnits {
        let mut canonical = CodeUnits::new();
        for u in 0..=u16::MAX {
            if self.contains(u) {
                let c = canonicalize(u);
                canonical.insert(c, c);
            }
        }
        let mut out = CodeUnits::new();
        for u in 0..=u16::MAX {
            if canonical.contains(canonicalize(u)) {
                out.insert(u, u);
            }
        }
        out
    }

    /// The set as a class.
    fn write(&self, negated: bool) -> String {
        let mut out = String::from(if negated { "[^" } else { "[" });
        let mut u = 0u32;
        while u <= 0xFFFF {
            if !self.contains(u as u16) {
                u += 1;
                continue;
            }
            let lo = u;
            while u < 0xFFFF && self.contains(u as u16 + 1) {
                u += 1;
            }
            out.push_str(&unit_text(lo));
            if u > lo {
                out.push('-');
                out.push_str(&unit_text(u));
            }
            u += 1;
        }
        out.push(']');
        out
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Whether a pattern is rewritten

struct Scan {
    /// How many references name each group.
    refs: Vec<u32>,
    needed: bool,
    /// The pattern has a repetition that regress, given the pattern as written, can go round for ever in: it
    /// loses the count and the body can match nothing.
    unbounded: bool,
    /// The capturing groups the scan is inside.
    open: Vec<u32>,
}

impl Scan {
    /// `in_loop`: the node is inside a repetition that can run twice (and not inside a lookaround inside that).
    fn node(&mut self, n: &Node, in_loop: bool) {
        match n {
            Node::Empty | Node::Atom(_) | Node::Assertion(_) => {}
            Node::Cat(v) => v.iter().for_each(|n| self.node(n, in_loop)),
            Node::Alt(v) => v.iter().for_each(|(n, _)| self.node(n, in_loop)),
            Node::Group { index, body, .. } => {
                self.open.push(*index);
                self.node(body, in_loop);
                self.open.pop();
            }
            // A lookaround runs on a backtrack stack of its own: nothing comes back into a loop inside it.
            Node::Look { body, .. } => self.node(body, false),
            Node::Repeat { min, max, body, .. } => {
                self.needed |= in_loop && loses_count(*min, *max, body);
                self.unbounded |= in_loop && loses_count(*min, *max, body) && nullable(body);
                // Not a fault of regress: it tries every way through every iteration of a body that matches
                // nothing, and one iteration says as much (see `Emitter::repeat`).
                self.needed |= zero_width(body) && !choice_free(body);
                self.node(body, in_loop || max.is_none_or(|max| max >= 2));
            }
            Node::Backref { groups, .. } => {
                self.needed |= groups.len() > 1;
                for &g in groups {
                    self.refs[g as usize] += 1;
                    self.needed |= self.open.contains(&g);
                }
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The rewritten pattern

/// A piece of the rewritten pattern. Groups are numbered when the whole is written, as a copy of a repetition has
/// groups of its own and a lookbehind is put together from its end.
enum Piece {
    Text(String),
    /// The `(` of a capturing group.
    Open(u32),
    /// A reference to a group, compared without case when the flag is set.
    Ref(u32, bool),
}

#[derive(Default)]
struct Out(Vec<Piece>);

impl Out {
    fn text(&mut self, s: &str) {
        match self.0.last_mut() {
            Some(Piece::Text(t)) => t.push_str(s),
            _ => self.0.push(Piece::Text(s.to_string())),
        }
    }

    fn append(&mut self, other: Out) {
        for piece in other.0 {
            match piece {
                Piece::Text(t) => self.text(&t),
                piece => self.0.push(piece),
            }
        }
    }

    fn render(&self, instances: u32) -> String {
        let mut numbers = vec![0u32; instances as usize];
        let mut next = 0;
        for piece in &self.0 {
            if let Piece::Open(instance) = piece {
                next += 1;
                numbers[*instance as usize] = next;
            }
        }
        let mut out = String::new();
        for piece in &self.0 {
            match piece {
                Piece::Text(t) => out.push_str(t),
                Piece::Open(_) => out.push('('),
                // In a group, so that a digit after it is not read as part of the number.
                Piece::Ref(instance, icase) => {
                    out.push_str(if *icase { "(?i:\\" } else { "(?:\\" });
                    out.push_str(&numbers[*instance as usize].to_string());
                    out.push(')');
                }
            }
        }
        out
    }
}

/// What a reference to a group reads, from where the rewriting has got to.
#[derive(Clone)]
enum Binding {
    /// The group cannot have matched before a reference here is read.
    Unset,
    /// One group of the rewritten pattern.
    Group(u32),
    /// The group is in a repetition that is written as copies, and the reference reads the last copy that ran.
    Iterations(Vec<Iteration>),
}

/// One iteration of a repetition written as copies: `value` is read when the marker `ran` has a value and the marker `next` has none. A
/// marker is a group that takes one character when its copy has run.
#[derive(Clone)]
struct Iteration {
    ran: Option<u32>,
    next: Option<u32>,
    value: Binding,
}

/// "At the start of the text", without `^`, which a modifier can change.
const START: &str = "(?<![^])";

struct Emitter {
    /// How many references name each group.
    refs: Vec<u32>,
    /// What [`Emitter::read_outside`] said of each repetition, which is asked once for every copy of it.
    read_outside: std::collections::HashMap<*const Node, bool>,
    /// By group number of the pattern as written.
    bind: Vec<Binding>,
    /// The groups made so far.
    instances: u32,
    spent: usize,
    /// The level, as regress counts it, of what is being written, and the deepest level written.
    depth: u32,
    deepest: u32,
    /// The repetitions that are left with their counts, by body, and whether every one that may be is.
    left: std::collections::HashSet<*const Node>,
    leave_all: bool,
    /// The repetitions being written out that may be left with their counts instead, from the outermost: the body
    /// and how many copies it comes to.
    writing: Vec<(*const Node, u64)>,
    /// The repetition to leave with its count, once a limit has been met.
    leave: Option<*const Node>,
}

impl Emitter {
    /// A limit is met. Of the repetitions being written out here whose bodies cannot match nothing, the one with
    /// the most copies is to be left with its count (the outermost of two with as many): the copies are what
    /// multiplies, and what nests. A repetition whose body can match nothing is never chosen.
    fn limit(&mut self) -> Stop {
        if self.leave.is_none() {
            let mut most = 0;
            for &(body, copies) in &self.writing {
                if copies > most {
                    (most, self.leave) = (copies, Some(body));
                }
            }
        }
        Stop::TooLarge
    }

    fn spend(&mut self, n: usize) -> Result<(), Stop> {
        self.spent += n;
        if self.spent > MAX_REWRITTEN { Err(self.limit()) } else { Ok(()) }
    }

    /// Checks that regress reads groups `levels` deep from here.
    fn fits(&mut self, levels: u32) -> Result<(), Stop> {
        let depth = self.depth.saturating_add(levels);
        self.deepest = self.deepest.max(depth);
        if depth > MAX_DEPTH { Err(self.limit()) } else { Ok(()) }
    }

    /// Writes with `f` what is `levels` groups below here.
    fn down<T>(&mut self, levels: u32, f: impl FnOnce(&mut Self) -> Result<T, Stop>) -> Result<T, Stop> {
        self.fits(levels)?;
        self.depth += levels;
        let written = f(self);
        self.depth -= levels;
        written
    }

    fn text(&mut self, out: &mut Out, s: &str) -> Result<(), Stop> {
        self.spend(s.len())?;
        out.text(s);
        Ok(())
    }

    fn instance(&mut self) -> u32 {
        self.instances += 1;
        self.instances - 1
    }

    fn unset(&mut self, groups: &Groups) {
        for g in groups.clone() {
            self.bind[g as usize + 1] = Binding::Unset;
        }
    }

    /// The parts, given in the order they are matched in, as they are written: a lookbehind matches from its end.
    fn sequence(&mut self, mut parts: Vec<Out>, back: bool) -> Out {
        if back {
            parts.reverse();
        }
        let mut out = Out::default();
        parts.into_iter().for_each(|part| out.append(part));
        out
    }

    /// `back`: the node is matched backwards (it is in a lookbehind). `in_loop`: as for [`Scan::node`].
    fn node(&mut self, n: &Node, back: bool, in_loop: bool) -> Result<Out, Stop> {
        self.write(n, back, in_loop, false)
    }

    /// `some`: the node is written so that it does not match nothing, which [`can_take_some`] has said it can be.
    fn write(&mut self, n: &Node, back: bool, in_loop: bool, some: bool) -> Result<Out, Stop> {
        let some = some && nullable(n);
        let mut out = Out::default();
        match n {
            Node::Empty => {}
            Node::Atom(t) | Node::Assertion(t) => self.text(&mut out, t)?,
            Node::Cat(v) => {
                // The parts in the order they are matched in. The one part that takes characters takes some.
                let mut parts = Vec::with_capacity(v.len());
                for k in 0..v.len() {
                    let n = &v[if back { v.len() - 1 - k } else { k }];
                    parts.push(self.write(n, back, in_loop, some && !zero_width(n))?);
                }
                return Ok(self.sequence(parts, back));
            }
            Node::Alt(v) => {
                // A group of one alternative has no value in another.
                let mut seen = Vec::with_capacity(v.len());
                let mut first = true;
                for (n, groups) in v {
                    let range = groups.start as usize + 1..groups.end as usize + 1;
                    // An alternative that can only match nothing is no part of a match of something.
                    if some && zero_width(n) {
                        seen.push(self.bind[range].to_vec());
                        continue;
                    }
                    if !first {
                        self.text(&mut out, "|")?;
                    }
                    first = false;
                    out.append(self.write(n, back, in_loop, some)?);
                    seen.push(self.bind[range].to_vec());
                    self.unset(groups);
                }
                for ((_, groups), bindings) in v.iter().zip(seen) {
                    self.bind[groups.start as usize + 1..groups.end as usize + 1].clone_from_slice(&bindings);
                }
            }
            Node::Group { index, modifiers, body } => {
                // A group that nothing refers to need not capture.
                let instance = (self.refs[*index as usize] > 0 && *index > 0).then(|| self.instance());
                match (instance, modifiers) {
                    (Some(instance), _) => {
                        self.spend(1)?;
                        out.0.push(Piece::Open(instance));
                    }
                    (None, Some(modifiers)) => self.text(&mut out, &format!("(?{modifiers}:"))?,
                    (None, None) => self.text(&mut out, "(?:")?,
                }
                out.append(self.down(1, |e| e.write(body, back, in_loop, some))?);
                self.text(&mut out, ")")?;
                // Until here the group has no value: a reference inside it reads nothing.
                if let Some(instance) = instance {
                    self.bind[*index as usize] = Binding::Group(instance);
                }
            }
            Node::Look { behind, negative, body, groups } => {
                self.text(
                    &mut out,
                    match (behind, negative) {
                        (false, false) => "(?=",
                        (false, true) => "(?!",
                        (true, false) => "(?<=",
                        (true, true) => "(?<!",
                    },
                )?;
                out.append(self.down(1, |e| e.node(body, *behind, false))?);
                self.text(&mut out, ")")?;
                // The groups of a negative lookaround have no value after it.
                if *negative {
                    self.unset(groups);
                }
            }
            Node::Repeat { max, lazy, body, .. } if some => {
                // One character, from zero times: from once.
                out.append(self.node(body, back, in_loop)?);
                let lazy = if *lazy { "?" } else { "" };
                match max {
                    None => self.text(&mut out, &format!("+{lazy}"))?,
                    Some(1) => {}
                    Some(max) => self.text(&mut out, &format!("{{1,{max}}}{lazy}"))?,
                }
            }
            Node::Repeat { min, max, lazy, text, body, groups } => {
                return self.repeat((*min, *max, *lazy, text), body, groups, back, in_loop);
            }
            Node::Backref { groups, icase } => {
                for &g in groups {
                    let binding = self.bind[g as usize].clone();
                    self.reference(&mut out, &binding, *icase)?;
                }
            }
        }
        Ok(out)
    }

    fn reference(&mut self, out: &mut Out, binding: &Binding, icase: bool) -> Result<(), Stop> {
        match binding {
            Binding::Unset => {}
            Binding::Group(instance) => {
                self.fits(1)?;
                self.spend(8)?;
                out.0.push(Piece::Ref(*instance, icase));
            }
            Binding::Iterations(copies) => self.down(1, |e| e.iterations(out, copies, icase))?,
        }
        Ok(())
    }

    /// A reference to the last copy that ran, of `copies`: an alternation, in a group.
    fn iterations(&mut self, out: &mut Out, copies: &[Iteration], icase: bool) -> Result<(), Stop> {
        // The deepest of it: a lookbehind, a lookahead in it, and the group of a reference in that.
        self.fits(3)?;
        self.text(out, "(?:")?;
        for (k, copy) in copies.iter().enumerate() {
            if k > 0 {
                self.text(out, "|")?;
            }
            // A marker has no value when, at the start of the text, a reference to it matches and the
            // position is still the start of the text. The lookbehind goes there from wherever it is.
            self.text(out, "(?<=")?;
            self.text(out, START)?;
            for (marker, open) in [(copy.next, "(?="), (copy.ran, "(?!")] {
                if let Some(marker) = marker {
                    self.text(out, open)?;
                    self.spend(8)?;
                    out.0.push(Piece::Ref(marker, false));
                    self.text(out, START)?;
                    self.text(out, ")")?;
                }
            }
            self.text(out, "[^]*)")?;
            self.reference(out, &copy.value, icase)?;
        }
        self.text(out, ")")?;
        Ok(())
    }

    /// `body` made to refuse a match of nothing: a group takes the rest of the text before it (the text up to
    /// here, when matching backwards), and what it took does not match again after it unless the position is the
    /// same.
    fn not_empty(&mut self, body: Out, back: bool) -> Result<Out, Stop> {
        let instance = self.instance();
        self.fits(2)?;
        self.spend(32)?;
        let (mut before, mut after) = (Out::default(), Out::default());
        before.text(if back { "(?<=" } else { "(?=" });
        before.0.push(Piece::Open(instance));
        before.text("[^]*))");
        after.text(if back { "(?<!" } else { "(?!" });
        after.0.push(Piece::Ref(instance, false));
        after.text(")");
        Ok(self.sequence(vec![before, body, after], back))
    }

    /// A marker after `body`: a group that takes the character the body ended on, so it has a value when the body
    /// has run. Only a body that takes at least one character is given one.
    fn marked(&mut self, body: Out, back: bool) -> Result<(Out, u32), Stop> {
        let instance = self.instance();
        self.fits(2)?;
        self.spend(16)?;
        let mut marker = Out::default();
        marker.text(if back { "(?=" } else { "(?<=" });
        marker.0.push(Piece::Open(instance));
        marker.text("[^]))");
        Ok((self.sequence(vec![body, marker], back), instance))
    }

    /// Whether a reference outside `body` names a group inside it.
    fn read_outside(&mut self, body: &Node, groups: &Groups) -> bool {
        if let Some(&known) = self.read_outside.get(&std::ptr::from_ref(body)) {
            return known;
        }
        let read = self.count_outside(body, groups);
        self.read_outside.insert(std::ptr::from_ref(body), read);
        read
    }

    fn count_outside(&self, body: &Node, groups: &Groups) -> bool {
        fn count(n: &Node, inside: &mut [u32]) {
            match n {
                Node::Cat(v) => v.iter().for_each(|n| count(n, inside)),
                Node::Alt(v) => v.iter().for_each(|(n, _)| count(n, inside)),
                Node::Group { body, .. } | Node::Look { body, .. } | Node::Repeat { body, .. } => count(body, inside),
                Node::Backref { groups, .. } => groups.iter().for_each(|&g| inside[g as usize] += 1),
                _ => {}
            }
        }
        if groups.clone().all(|g| self.refs[g as usize + 1] == 0) {
            return false;
        }
        let mut inside = vec![0; self.refs.len()];
        count(body, &mut inside);
        groups.clone().any(|g| self.refs[g as usize + 1] > inside[g as usize + 1])
    }

    fn repeat(
        &mut self,
        (min, max, lazy, text): (u32, Option<u32>, bool, &str),
        body: &Node,
        groups: &Groups,
        back: bool,
        in_loop: bool,
    ) -> Result<Out, Stop> {
        // A body that can only match nothing. ECMA-262 refuses an iteration that matches nothing once the minimum
        // is met, so there is none after the minimum. The iterations up to the minimum all match at one position,
        // each with the groups of the one before taken back, so they match when one does and leave what one
        // leaves. The repetition is its body once, or nothing for a minimum of zero. Tried as written, every
        // iteration multiplies the ways through the body, to no effect.
        if zero_width(body) {
            self.unset(groups);
            return if min == 0 || max == Some(0) { Ok(Out::default()) } else { self.node(body, back, in_loop) };
        }
        let can_be_empty = nullable(body);
        // regress keeps the count of a repetition that is entered once, and of one that cannot be resumed. One
        // that is too large to write out has its count too, where no iteration of it can match nothing.
        let left = !can_be_empty && (self.leave_all || self.left.contains(&std::ptr::from_ref(body)));
        if !in_loop || !loses_count(min, max, body) || left {
            self.unset(groups);
            let in_loop = in_loop || max.is_none_or(|max| max >= 2);
            // `X*` over a body that cannot match nothing is left as it is too: no count decides anything in it.
            let mut out = Out::default();
            if matches!(body, Node::Backref { .. }) {
                self.text(&mut out, "(?:")?;
                out.append(self.down(1, |e| e.node(body, back, in_loop))?);
                self.text(&mut out, ")")?;
            } else {
                out.append(self.node(body, back, in_loop)?);
            }
            self.text(&mut out, text)?;
            return Ok(out);
        }
        // Written out, it may come to more than regress is given. If its body cannot match nothing it is one of
        // those that can be left with its count instead.
        if !can_be_empty {
            let copies = u64::from(min) + max.map_or(1, |max| u64::from(max - min));
            self.writing.push((std::ptr::from_ref(body), copies));
        }
        let written = self.write_out((min, max, lazy), body, groups, back)?;
        if !can_be_empty {
            self.writing.pop();
        }
        Ok(written)
    }

    /// A repetition that loses its count in regress, without a count: see the top of the file.
    fn write_out(
        &mut self,
        (min, max, lazy): (u32, Option<u32>, bool),
        body: &Node,
        groups: &Groups,
        back: bool,
    ) -> Result<Out, Stop> {
        let range = groups.start as usize + 1..groups.end as usize + 1;
        let read_outside = self.read_outside(body, groups);
        let can_be_empty = nullable(body);
        // The iterations up to the minimum, each of which may match nothing.
        let mut parts = Vec::new();
        for _ in 0..min {
            self.spend(1)?;
            self.unset(groups);
            parts.push(self.node(body, back, true)?);
        }
        let required = self.bind[range.clone()].to_vec();
        // The iterations after the minimum, none of which may match nothing.
        let mut copies: Vec<(Out, Option<u32>, Vec<Binding>)> = Vec::new();
        for k in 0..max.map_or(1, |max| max - min) {
            self.spend(1)?;
            self.unset(groups);
            // Each optional copy is in the group of the one before it, and the one copy of `(?:X)*` is in a group.
            let copy = self.down(k.saturating_add(1), |e| {
                // Written so that it takes a character where the body can be, and held to it by a group otherwise.
                let some = can_be_empty && can_take_some(body);
                let mut copy = e.write(body, back, true, some)?;
                if can_be_empty && !some {
                    copy = e.not_empty(copy, back)?;
                }
                let mut marker = None;
                if read_outside {
                    let (marked, instance) = e.marked(copy, back)?;
                    (copy, marker) = (marked, Some(instance));
                }
                Ok((copy, marker))
            })?;
            copies.push((copy.0, copy.1, self.bind[range.clone()].to_vec()));
        }
        // A reference after the repetition reads the last iteration that ran.
        if read_outside && !copies.is_empty() {
            for (k, g) in range.clone().enumerate() {
                let mut cases: Vec<Iteration> = (0..copies.len())
                    .rev()
                    .map(|j| Iteration {
                        ran: copies[j].1,
                        next: copies.get(j + 1).and_then(|c| c.1),
                        value: copies[j].2[k].clone(),
                    })
                    .collect();
                cases.push(Iteration { ran: None, next: copies[0].1, value: required[k].clone() });
                self.bind[g] = if cases.iter().all(|c| matches!(c.value, Binding::Unset)) {
                    Binding::Unset
                } else {
                    Binding::Iterations(cases)
                };
            }
        } else {
            self.bind[range].clone_from_slice(&required);
        }
        let mut tail = Out::default();
        if max.is_none() {
            let (copy, ..) = copies.pop().unwrap();
            self.text(&mut tail, "(?:")?;
            tail.append(copy);
            self.text(&mut tail, if lazy { ")*?" } else { ")*" })?;
        } else {
            // From the last optional copy outwards: `(?:X(?:X|)|)`, and `(?:|X(?:|X))` for a lazy quantifier.
            for (copy, ..) in copies.into_iter().rev() {
                let inner = self.sequence(vec![copy, tail], back);
                tail = Out::default();
                self.text(&mut tail, if lazy { "(?:|" } else { "(?:" })?;
                tail.append(inner);
                self.text(&mut tail, if lazy { ")" } else { "|)" })?;
            }
        }
        parts.push(tail);
        Ok(self.sequence(parts, back))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn rewritten(pattern: &str) -> Option<String> {
        let unicode = regress::Regex::with_flags(pattern, "u").is_ok();
        match for_engine(pattern, unicode) {
            Rewrite::Rewritten(text) => {
                let flags = if unicode { "u" } else { "" };
                assert!(regress::Regex::with_flags(&text, flags).is_ok(), "{pattern} as {text}");
                Some(text)
            }
            Rewrite::Unchanged => None,
            Rewrite::TooLarge => panic!("{pattern} is too large"),
        }
    }

    /// regress keeps the count of a repetition that is entered once, of one whose body it cannot come back into,
    /// and `X*` over a body that cannot match nothing needs no count. These go to regress as they are written.
    #[test]
    fn a_pattern_regress_answers_as_written_is_not_rewritten() {
        for pattern in [
            r"^((\.(?!\.)\/)?\w+\/?)+$",
            r"^(?!-)(?:[a-z0-9]+(?:-[a-z0-9]+)*\.)+[a-z]{2,}$",
            r"^(a)\1(?:b|c){2,3}$",
            r"^(?:(a)|b)+\1$",
            r"^(?:(a)|\1x)+$",
            r"^(?!x)(?:(?:ab){2,3}\d{4}(?:-[a-f]{2}){2})+$",
            r"^(?!x)(?:(?:a|b)*c)+$",
            r"^(?!x)(?:a(?=(?:b|c){2})..)+$",
            r"(?<=(?:a|b){2,3})x",
            r"^(?:(?<n>a)|(?<n>b))+$",
            r"\1(a)",
            r"^(?i:[\w]s)$",
            r"^(?:\-)?(?:a|b){2,3}c+$",
        ] {
            assert_eq!(rewritten(pattern), None, "{pattern}");
        }
    }

    #[test]
    fn a_repetition_inside_a_repetition_is_written_without_a_count() {
        for (pattern, expected) in [
            (r"^(?!-)(?:(?:a|x){1,3}-)+$", r"^(?!-)(?:(?:a|x)(?:(?:a|x)(?:(?:a|x)|)|)-)+$"),
            (r"^(?!-)(?:(?:a|x){1,3}?-)+$", r"^(?!-)(?:(?:a|x)(?:|(?:a|x)(?:|(?:a|x)))-)+$"),
            (r"^(?!-)(?:(?:a|x){2,}-)+$", r"^(?!-)(?:(?:a|x)(?:a|x)(?:(?:a|x))*-)+$"),
            (r"^(?!x)(?:a(?:\.[a-z]+)?,)+$", r"^(?!x)(?:a(?:(?:\.[a-z]+)|),)+$"),
            // An iteration after the minimum may not match nothing.
            (r"^(?!-)(?:(?:a*)*)*x", r"^(?!-)(?:(?:(?:a+))*)*x"),
            (r"^(?!-)(?:(?:a|)+-)+$", r"^(?!-)(?:(?:a|)(?:(?:a))*-)+$"),
            (r"^(?!-)(?:(\ba{0,3}?$|^|x?)*-)+$", r"^(?!-)(?:(?:(?:\ba{1,3}?$|x))*-)+$"),
            // A body with more than one part that may be empty is held to a character by a group.
            (r"^(?!-)(?:(?:a*x*)*-)+$", r"^(?!-)(?:(?:(?=([^]*))(?:a*x*)(?!(?:\1)))*-)+$"),
            (r"^(?!-)(?:(a|\1)+-)+$", r"^(?!-)(?:(a|)(?:(?=([^]*))(a|)(?!(?:\2)))*-)+$"),
            // A lookbehind matches from its end, so the optional copies are written first.
            (r"(?<=(?:(?:a|){1,2}-)+)x", r"(?<=(?:(?:(?:a)|)(?:a|)-)+)x"),
            (r"(?<=(?:(?:a*x*){1,2}-)+)x", r"(?<=(?:(?:(?<!(?:\1))(?:a*x*)(?<=([^]*))|)(?:a*x*)-)+)x"),
        ] {
            assert_eq!(rewritten(pattern).as_deref(), Some(expected), "{pattern}");
        }
    }

    /// A body that can only match nothing is matched once, or not at all for a minimum of zero.
    #[test]
    fn a_repetition_of_a_body_that_matches_nothing_is_its_body_once() {
        for (pattern, expected) in [
            (r"^(?:(?=a)|\b){3,}a", r"^(?:(?=a)|\b)a"),
            (r"^(?:(?=a)|\b)*a", r"^a"),
            (r"^(?:(?:^|\B){2,3}?|(?!x)){4}(a)\1", r"^(?:(?:^|\B)|(?!x))(a)(?:\1)"),
            (r"(?:(?=(a))|^){2}\1", r"(?:(?=(a))|^)(?:\1)"),
            (r"(?:(?=(a))|^){0,2}\1", r""),
        ] {
            assert_eq!(rewritten(pattern).as_deref(), Some(expected), "{pattern}");
        }
        // With one way through the body there is nothing to multiply, and regress is given it as written.
        assert_eq!(rewritten(r"^(?:\b(?=a)){3}a"), None);
    }

    /// Deeply nested groups are read and written within the stack of a thread. A repetition written without a
    /// count has a group more than the one it was, and regress reads 256 deep: past that the pattern is refused,
    /// as the bodies here can match nothing.
    #[test]
    fn deeply_nested_groups() {
        let nested = |depth: usize| format!("^(?!-){}a{}$", "(?:".repeat(depth), ")*".repeat(depth));
        let written = rewritten(&nested(120)).unwrap();
        assert!(written.len() < MAX_REWRITTEN, "{}", written.len());
        assert!(regress::Regex::with_flags(&nested(250), "u").is_ok());
        assert!(matches!(for_engine(&nested(250), true), Rewrite::TooLarge));
    }

    /// The level the writer counts is the level regress counts: a repetition written out to the last level regress
    /// reads is read, and one copy more is not written.
    #[test]
    fn the_deepest_pattern_regress_reads() {
        // The pattern, the group of the outer repetition, an optional copy inside each one before, and the group
        // of the body.
        let pattern = |copies: u32| format!("^(?!-)(?:(?:a|x){{1,{copies}}}-)+$");
        for copies in [200, MAX_DEPTH - 2] {
            let written = rewritten(&pattern(copies)).unwrap();
            assert!(!written.contains('{'), "{written}");
            assert_eq!(nesting(&written), copies + 2);
        }
        assert_eq!(rewritten(&pattern(MAX_DEPTH - 1)), Some(pattern(MAX_DEPTH - 1)));
        let groups = |depth: usize| format!("{}a{}", "(?:".repeat(depth), ")".repeat(depth));
        assert_eq!(nesting(&groups(MAX_DEPTH as usize - 1)), MAX_DEPTH);
        assert!(regress::Regex::with_flags(&groups(MAX_DEPTH as usize - 1), "u").is_ok());
        assert!(regress::Regex::with_flags(&groups(MAX_DEPTH as usize), "u").is_err());
    }

    #[test]
    fn a_reference_reads_the_group_of_its_own_copy_or_of_the_last_one_that_ran() {
        for (pattern, expected) in [
            (r"^(?:(?:(a)|x){2}\1-)+$", r"^(?:(?:(a)|x)(?:(a)|x)(?:\2)-)+$"),
            (r"^(?:(?:(a)\1|x){2}-)+$", r"^(?:(?:(a)(?:\1)|x)(?:(a)(?:\2)|x)-)+$"),
            // Each optional copy ends with a marker, a group that takes one character. The reference is an
            // alternation: the second copy when its marker has a value, and the first when it has none.
            (
                r"^(?:(?:(a)|x){1,2}\1-)+$",
                r"^(?:(?:(a)|x)(?:(?:(a)|x)(?<=([^]))|)(?:(?<=(?<![^])(?!(?:\3)(?<![^]))[^]*)(?:\2)|(?<=(?<![^])(?=(?:\3)(?<![^]))[^]*)(?:\1))-)+$",
            ),
            (r"(x|\1)x", r"(x|)x"),
            (r"(?:(?<n>a)|(?<n>x))\k<n>", r"(?:(a)|(x))(?:\1)(?:\2)"),
        ] {
            assert_eq!(rewritten(pattern).as_deref(), Some(expected), "{pattern}");
        }
    }

    #[test]
    fn a_case_insensitive_group_is_written_out() {
        for (pattern, expected) in [
            (r"^(?i:s[k-l]\w)\&", r"^(?:[\u0053\u0073][\u004b-\u004c\u006b-\u006c]\w)\u0026"),
            (r"^(?i-m:(k)\1)\&", r"^(?-m:([\u004b\u006b])(?i:\1))\u0026"),
            (r"^(?i:a(?-i:b)[^b])\&", r"^(?:[\u0041\u0061](?:b)[^\u0042\u0062])\u0026"),
            (
                r"^(?i:[\W])$",
                r"^(?i:[\u0000-\u002f\u003a-\u0040\u005b-\u005e\u0060\u007b-\u017e\u0180-\u2129\u212b-\u{10ffff}])$",
            ),
        ] {
            assert_eq!(rewritten(pattern).as_deref(), Some(expected), "{pattern}");
        }
    }

    /// Annex B reads an escape of a number that is no group as an octal escape or as the digit. It is written as
    /// the character, as the rewritten pattern may have that many groups.
    #[test]
    fn a_number_that_is_no_group_stays_a_character() {
        assert_eq!(
            rewritten(r"^(?:\-|(?:a|x\1){2}\8)+$").as_deref(),
            Some(r"^(?:\u002d|(?:a|x\u0001)(?:a|x\u0001)\u0038)+$")
        );
    }

    #[test]
    fn a_quantifier_on_an_assertion() {
        for pattern in [r"\b+", r"a\B?", r"\b{2}", r"(?:a|\b*)", r"(?<=\B+)"] {
            assert!(quantified_assertion(pattern, true), "{pattern}");
            assert!(quantified_assertion(pattern, false), "{pattern}");
        }
        for pattern in [r"\\b+", r"[\b+]", r"(?:\b)+", r"a\b", r"\b\{", r"\b{", r"\b{a}"] {
            assert!(!quantified_assertion(pattern, false), "{pattern}");
        }
    }

    /// A repetition that is too large to write out is left with its count when its body cannot match nothing. Of
    /// those being written where the limit is met, it is the one with the most copies, and the outermost of two
    /// with as many. The rest is written as it would be.
    #[test]
    fn a_repetition_too_large_to_write_out_is_left_with_its_count() {
        for (pattern, expected) in [
            (r"^(?!-)(?:(?:a|x){1,300}-)+$", r"^(?!-)(?:(?:a|x){1,300}-)+$"),
            (r"^(?!-)(?:(?:a|x){4294967295}?-)+$", r"^(?!-)(?:(?:a|x){4294967295}?-)+$"),
            // The one with the most copies is left, and the other is written out, whichever is inside.
            (r"^(?!-)(?:(?:(?:a|x){1,2}-?){1,300}-)+$", r"^(?!-)(?:(?:(?:a|x)(?:(?:a|x)|)-?){1,300}-)+$"),
            (r"^(?!-)(?:(?:(?:a|x){1,300}-?){1,2}-)+$", r"^(?!-)(?:(?:(?:a|x){1,300}-?)(?:(?:(?:a|x){1,300}-?)|)-)+$"),
            // 200 copies of 200 copies: the outer one is left, and its body has the inner one written out.
            (
                r"^(?!-)(?:(?:(?:a|x){200}-|a){200}x)+$",
                &format!(r"^(?!-)(?:(?:{}-|a){{200}}x)+$", "(?:a|x)".repeat(200)),
            ),
            // One whose body can match nothing is written out inside one that is left, and around one.
            (r"^(?!-)(?:(?:(?:a|){1,2}x){1,300}-)+$", r"^(?!-)(?:(?:(?:a|)(?:(?:a)|)x){1,300}-)+$"),
            (r"^(?!-)(?:(?:(?:a|x){1,300}|){1,2}-)+$", r"^(?!-)(?:(?:(?:a|x){1,300}|)(?:(?:(?:a|x){1,300})|)-)+$"),
            // A reference to a group of the one that is left reads what it read before there was a rewriter.
            (r"^(?!-)(?:(?:(a)|x){1,300}\1-)+$", r"^(?!-)(?:(?:(a)|x){1,300}(?:\1)-)+$"),
        ] {
            assert_eq!(rewritten(pattern).as_deref(), Some(expected), "{pattern}");
        }
    }

    /// A repetition whose body can match nothing is not left with its count, whatever is inside it or around it.
    #[test]
    fn a_repetition_too_large_to_write_out_whose_body_can_be_empty_is_too_large() {
        for pattern in [
            r"^(?!-)(?:(?:a|){4294967295}-)+$",
            r"^(?!-)(?:(?:a|){1,300}-)+$",
            r"^(?!-)(?:(?:(?:a|x|){200}-|a?){200}x)+$",
            r"^(?!-)(?:(?:(?:a|x){1,300}|){1,300}-)+$",
            r"^(?!-)(?:(?:(?:a|){1,300}x){1,300}-)+$",
        ] {
            assert!(matches!(for_engine(pattern, true), Rewrite::TooLarge), "{pattern}");
        }
    }

    /// With more repetitions to leave than are left one at a time, every one that may be left is.
    #[test]
    fn many_repetitions_too_large_to_write_out() {
        let pattern = format!("^(?!-)(?:{}-)+$", "(?:a|x){1,300}".repeat(MAX_LEFT_ONE_AT_A_TIME + 4));
        assert_eq!(rewritten(&pattern).as_deref(), Some(pattern.as_str()));
        let pattern = format!("^(?!-)(?:{}(?:a|){{1,2}}-)+$", "(?:a|x){1,300}".repeat(MAX_LEFT_ONE_AT_A_TIME + 4));
        assert!(rewritten(&pattern).unwrap().ends_with("{1,300}(?:a|)(?:(?:a)|)-)+$"));
        // Each of a hundred repetitions doubles the ones inside it.
        let pattern = format!("^(?!-)(?:{}(?:a|x){}-)+$", "(?:".repeat(100), "{1,2}-)".repeat(100));
        assert_eq!(rewritten(&pattern).as_deref(), Some(pattern.as_str()));
    }

    /// A pattern that is too large for a reason no repetition can mend goes to regress as it is written, unless
    /// it has a repetition that regress would not end on.
    #[test]
    fn a_pattern_too_large_with_no_repetition_to_leave() {
        let long = format!(r"^(x|\1){}$", "a".repeat(MAX_REWRITTEN));
        assert!(matches!(for_engine(&long, true), Rewrite::Unchanged));
        let long = format!(r"^(?!-)(?:(?:a|)+-)+(x|\1){}$", "a".repeat(MAX_REWRITTEN));
        assert!(matches!(for_engine(&long, true), Rewrite::TooLarge));
    }
}

#[cfg(test)]
mod statistics {
    use super::*;

    /// Says what happens to each pattern of `CORVUS_PATTERNS` (a file of JSON Lines records with a `pattern`, as in
    /// the ECMA-262 regular expression test data): how many are rewritten, and how many use each part of the
    /// rewriting. With `CORVUS_PATTERNS_SHOW`, prints the first rewritten patterns that have that text in them.
    #[test]
    #[ignore]
    fn count_rewritten_patterns() {
        let Ok(path) = std::env::var("CORVUS_PATTERNS") else { return };
        let show = std::env::var("CORVUS_PATTERNS_SHOW").ok();
        let (mut total, mut rewritten, mut too_large, mut shown) = (0, 0, 0, 0);
        let parts = [
            ("an iteration that may not be empty", "(?=([^]*))"),
            ("the same in a lookbehind", "(?<=([^]*))"),
            ("a reference to the last iteration that ran", "(?<=(?<![^])"),
            ("an optional copy", "|)"),
            ("a lazy optional copy", "(?:|"),
        ];
        let mut counts = [0; 5];
        for line in std::fs::read_to_string(path).unwrap().lines() {
            let record: serde_json::Value = serde_json::from_str(line).unwrap();
            let pattern = record["pattern"].as_str().unwrap();
            let unicode = regress::Regex::with_flags(pattern, "u").is_ok();
            if !unicode && regress::Regex::new(pattern).is_err() {
                continue;
            }
            total += 1;
            match for_engine(pattern, unicode) {
                Rewrite::Unchanged => {}
                Rewrite::TooLarge => too_large += 1,
                Rewrite::Rewritten(text) => {
                    rewritten += 1;
                    for (k, (_, part)) in parts.iter().enumerate() {
                        counts[k] += usize::from(text.contains(part));
                    }
                    if show.as_ref().is_some_and(|s| text.contains(s.as_str())) && shown < 20 {
                        shown += 1;
                        println!("{pattern}\n    {text}");
                    }
                }
            }
        }
        println!("{total} valid patterns, {rewritten} rewritten, {too_large} too large");
        for (k, (name, _)) in parts.iter().enumerate() {
            println!("{:6} with {name}", counts[k]);
        }
    }

    /// Reads every pattern of `CORVUS_PATTERNS` with the parser, writes it again whether or not it needs it, and
    /// checks regress's answers for what was written against V8's answers in the records. It covers the parser
    /// and the writer on patterns the crate leaves alone. A record's flags go around the pattern as a modifier
    /// group. `CORVUS_PATTERNS_SKIP` names a file of patterns to leave out, as JSON strings (the ones that take
    /// regress minutes). Run it in a release build.
    #[test]
    #[ignore]
    fn every_pattern_written_again_answers_as_v8() {
        let Ok(path) = std::env::var("CORVUS_PATTERNS") else { return };
        let skip: std::collections::HashSet<String> = std::env::var("CORVUS_PATTERNS_SKIP")
            .map(|path| {
                let text = std::fs::read_to_string(path).unwrap();
                text.lines().map(|line| serde_json::from_str(line).unwrap()).collect()
            })
            .unwrap_or_default();
        let (mut patterns, mut answers, mut unread, mut wrong) = (0, 0, 0, 0);
        for line in std::fs::read_to_string(path).unwrap().lines() {
            let record: serde_json::Value = serde_json::from_str(line).unwrap();
            let source = record["pattern"].as_str().unwrap();
            if skip.contains(source) || record["v8_deviation"] == true {
                continue;
            }
            let flags = record["flags"].as_str().unwrap_or("");
            let pattern = if flags.is_empty() { source.to_string() } else { format!("(?{flags}:{source})") };
            let unicode = record["valid"]["unicode"] == true;
            if !unicode && record["valid"]["legacy"] != true {
                continue;
            }
            let written = match rewrite(&pattern, unicode, true) {
                Rewrite::Rewritten(written) => written,
                Rewrite::Unchanged => {
                    unread += 1;
                    println!("not read: {pattern}");
                    continue;
                }
                Rewrite::TooLarge => panic!("{pattern} is too large"),
            };
            let engine = regress::Regex::with_flags(&written, if unicode { "u" } else { "" })
                .unwrap_or_else(|e| panic!("{pattern} as {written}: {e}"));
            patterns += 1;
            let Some(tests) = record["tests"].as_array() else { continue };
            for test in tests {
                let text = test["text"].as_str().unwrap();
                // Without the `u` flag V8 matches code units, and a character beyond the BMP is two of them.
                if !unicode && text.chars().any(|c| c as u32 > 0xFFFF) {
                    continue;
                }
                let want = test[if unicode { "unicode" } else { "legacy" }] == true;
                answers += 1;
                if engine.find(text).is_some() != want {
                    wrong += 1;
                    if wrong <= 40 {
                        println!("{pattern} as {written} on {text:?}: V8 says {want}");
                    }
                }
            }
        }
        println!("{patterns} patterns written again, {answers} answers, {wrong} wrong, {unread} not read");
        assert_eq!(wrong, 0);
    }
}
