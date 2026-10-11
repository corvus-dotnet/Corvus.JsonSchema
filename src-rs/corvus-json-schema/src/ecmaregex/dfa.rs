//! A deterministic automaton over ASCII text, for the patterns that need no backtracking (`dfa.go`).
//!
//! The strings a schema tests are short and nearly always ASCII, and most patterns in schemas are small. For such a
//! pattern every state set the text can lead to is worked out once, and a match is then one table read per byte.
//!
//! The automaton reads bytes below 0x80 only. A text with any other byte is handed back to the backtracking matcher
//! from the start, so nothing here needs to know UTF-8 or the Unicode classes: a class is its ASCII members. A
//! pattern with a word boundary, or whose automaton has more states than a byte can number, has no automaton. The
//! automaton is built on the first match, not when the pattern is compiled, so a schema pays nothing for the
//! patterns its instances never reach.

use std::collections::HashMap;

use super::parse::{Node, NodeKind, UNBOUNDED};

// The states every automaton has. DEAD: no match can follow. ACCEPT: a match was found. BAIL: the text is not
// ASCII.
const DFA_DEAD: u8 = 0;
const DFA_ACCEPT: u8 = 1;
const DFA_BAIL: u8 = 2;
const DFA_FIRST: usize = 3;

// An automaton has at most this many states (they are numbered in a byte), built from at most this many states of
// the pattern, in at most this many steps (a step is one state of the pattern looked at while a set is made). The
// steps bound what a pattern too large for an automaton costs before that is known, to about a millisecond.
const MAX_DFA_STATES: usize = 256;
const MAX_NFA_STATES: usize = 512;
const MAX_DFA_STEPS: i64 = 400_000;

#[derive(Clone, Copy, PartialEq, Eq)]
enum NfaKind {
    /// Reads one byte of its set and goes to `out`.
    Set,
    /// Goes to `out` and to `alt`, reading nothing.
    Split,
    /// `Bol` and `Eol` go to `out` at the start and at the end of the text.
    Bol,
    Eol,
    Match,
}

#[derive(Clone, Copy)]
struct NfaState {
    kind: NfaKind,
    set: [u64; 2],
    out: usize,
    alt: usize,
}

impl NfaState {
    fn new(kind: NfaKind, out: usize) -> NfaState {
        NfaState { kind, set: [0; 2], out, alt: 0 }
    }
}

#[derive(Default)]
struct NfaBuilder {
    states: Vec<NfaState>,
    /// The pattern has something an automaton over bytes cannot decide, or is too large.
    failed: bool,
}

impl NfaBuilder {
    fn add(&mut self, s: NfaState) -> usize {
        if self.states.len() >= MAX_NFA_STATES {
            self.failed = true;
            return 0;
        }
        self.states.push(s);
        self.states.len() - 1
    }

    /// Adds the states of a node that go on to `next`, and returns the state they start at.
    fn compile(&mut self, n: &Node, mut next: usize) -> usize {
        if self.failed {
            return 0;
        }
        match n.kind {
            NodeKind::Empty => next,
            NodeKind::Char => {
                let mut set = [0u64; 2];
                if (0..128).contains(&n.r) {
                    set[(n.r >> 6) as usize] = 1 << (n.r & 63);
                }
                self.add(NfaState { set, ..NfaState::new(NfaKind::Set, next) })
            }
            NodeKind::Set => self.add(NfaState { set: n.class().ascii, ..NfaState::new(NfaKind::Set, next) }),
            NodeKind::Cat => {
                for sub in n.subs.iter().rev() {
                    next = self.compile(sub, next);
                }
                next
            }
            NodeKind::Alt => {
                let mut start = self.compile(&n.subs[n.subs.len() - 1], next);
                for sub in n.subs[..n.subs.len() - 1].iter().rev() {
                    let out = self.compile(sub, next);
                    start = self.add(NfaState { alt: start, ..NfaState::new(NfaKind::Split, out) });
                }
                start
            }
            NodeKind::Group => self.compile(&n.subs[0], next),
            NodeKind::Repeat => {
                let sub = &n.subs[0];
                if n.max == UNBOUNDED {
                    // The loop, then the copies that must match before it.
                    let loop_ = self.add(NfaState { alt: next, ..NfaState::new(NfaKind::Split, 0) });
                    if self.failed {
                        return 0;
                    }
                    let out = self.compile(sub, loop_);
                    self.states[loop_].out = out;
                    next = loop_;
                } else {
                    // The copies that may match, innermost last.
                    let mut i = n.min;
                    while i < n.max && !self.failed {
                        let out = self.compile(sub, next);
                        next = self.add(NfaState { alt: next, ..NfaState::new(NfaKind::Split, out) });
                        i += 1;
                    }
                }
                let mut i = 0;
                while i < n.min && !self.failed {
                    next = self.compile(sub, next);
                    i += 1;
                }
                next
            }
            NodeKind::Bol => self.add(NfaState::new(NfaKind::Bol, next)),
            NodeKind::Eol => self.add(NfaState::new(NfaKind::Eol, next)),
            // A word boundary, or anything that needs the backtracking matcher.
            _ => {
                self.failed = true;
                0
            }
        }
    }
}

/// The automaton. A state is a byte, a byte of the text is one of a few classes (bytes the pattern does not tell
/// apart), and `trans[state << shift | class]` is the state after the byte.
pub(crate) struct Dfa {
    class: [u8; 256],
    trans: Vec<u8>,
    shift: u32,
    start: u8,
    /// For each state: the pattern matches if the text ends there.
    at_end: Vec<bool>,
    /// The pattern matches the empty text.
    empty: bool,
}

impl Dfa {
    /// Reports whether the pattern matches the text. `None` when the text is not ASCII, which this automaton does
    /// not decide.
    #[inline]
    pub(crate) fn is_match(&self, text: &[u8]) -> Option<bool> {
        if text.is_empty() {
            return Some(self.empty);
        }
        let mut s = self.start;
        if s == DFA_ACCEPT {
            return Some(true);
        }
        let (trans, shift) = (&self.trans[..], self.shift);
        for &c in text {
            s = trans[(s as usize) << shift | self.class[c as usize] as usize];
            if (s as usize) < DFA_FIRST {
                return if s == DFA_BAIL { None } else { Some(s == DFA_ACCEPT) };
            }
        }
        Some(self.at_end[s as usize])
    }
}

/// Makes the automaton's states from sets of the pattern's states.
struct DfaBuilder<'a> {
    nfa: &'a [NfaState],
    // Scratch for closure: the states visited, by generation, and the stack.
    mark: Vec<u32>,
    generation: u32,
    stack: Vec<usize>,
    /// The steps left (see `MAX_DFA_STEPS`).
    steps: i64,
}

impl DfaBuilder<'_> {
    /// Adds to `set` every state that reading nothing reaches from the states in `from`, keeping the states that
    /// read a byte, the end anchors still to pass, and the match. A start anchor is passed only `at_start`, and an
    /// end anchor only `at_end`.
    fn closure(&mut self, set: &mut Vec<usize>, from: &[usize], at_start: bool, at_end: bool) {
        self.generation += 1;
        self.stack.clear();
        self.stack.extend_from_slice(from);
        while let Some(i) = self.stack.pop() {
            self.steps -= 1;
            if self.mark[i] == self.generation {
                continue;
            }
            self.mark[i] = self.generation;
            let s = &self.nfa[i];
            match s.kind {
                NfaKind::Split => {
                    self.stack.push(s.out);
                    self.stack.push(s.alt);
                }
                NfaKind::Bol => {
                    if at_start {
                        self.stack.push(s.out);
                    }
                }
                NfaKind::Eol => {
                    if at_end {
                        self.stack.push(s.out);
                    } else {
                        set.push(i);
                    }
                }
                _ => set.push(i),
            }
        }
    }

    fn has_match(&self, set: &[usize]) -> bool {
        set.iter().any(|&i| self.nfa[i].kind == NfaKind::Match)
    }
}

/// A set as a map key: its states in increasing order.
fn set_key(set: &mut [usize]) -> Vec<u16> {
    set.sort_unstable();
    set.iter().map(|&s| s as u16).collect()
}

/// Builds the automaton of a pattern for an unanchored search, or `None` when it has none.
pub(crate) fn build_dfa(root: &Node) -> Option<Dfa> {
    let mut nb = NfaBuilder::default();
    let match_ = nb.add(NfaState::new(NfaKind::Match, 0));
    let start = nb.compile(root, match_);
    if nb.failed {
        return None;
    }
    let nfa = &nb.states[..];
    let mut b = DfaBuilder { nfa, mark: vec![0; nfa.len()], generation: 0, stack: Vec::new(), steps: MAX_DFA_STEPS };

    // The classes of bytes: two bytes are in one class when every set of the pattern has both or neither. Class 0
    // is the bytes that are not ASCII.
    let mut d = Dfa { class: [0; 256], trans: Vec::new(), shift: 0, start: 0, at_end: Vec::new(), empty: false };
    let mut classes = 1usize;
    let mut signatures: HashMap<Vec<u8>, u8> = HashMap::new();
    for c in 0..128usize {
        let mut signature = Vec::with_capacity(nfa.len() / 8 + 1);
        let (mut acc, mut n) = (0u8, 0u8);
        for s in nfa {
            if s.kind != NfaKind::Set {
                continue;
            }
            acc = acc << 1 | ((s.set[c >> 6] >> (c & 63)) & 1) as u8;
            n += 1;
            if n == 8 {
                signature.push(acc);
                (acc, n) = (0, 0);
            }
        }
        signature.push(acc);
        let class = *signatures.entry(signature).or_insert_with(|| {
            classes += 1;
            (classes - 1) as u8
        });
        d.class[c] = class;
    }
    while 1usize << d.shift < classes {
        d.shift += 1;
    }
    // One byte of each class, to move a set by.
    let mut sample = [0usize; 129];
    for c in (0..128usize).rev() {
        sample[d.class[c] as usize] = c;
    }

    // The search is unanchored: a match may start at any position, so after every byte the states the pattern
    // starts in (not at the start of the text) join the set.
    let mut restart = Vec::new();
    b.closure(&mut restart, &[start], false, false);
    let mut at_both = Vec::new();
    b.closure(&mut at_both, &[start], true, true);
    d.empty = b.has_match(&at_both);

    let mut ids: HashMap<Vec<u16>, u8> = HashMap::new();
    let mut sets: Vec<Vec<usize>> = vec![Vec::new(); DFA_FIRST];
    let stride = 1usize << d.shift;
    d.trans = vec![0; DFA_FIRST * stride];
    d.at_end = vec![false; DFA_FIRST];
    for class in 0..stride {
        d.trans[DFA_ACCEPT as usize * stride + class] = DFA_ACCEPT;
        d.trans[DFA_BAIL as usize * stride + class] = DFA_BAIL;
    }
    // Gives a set its state. `None` when there are too many.
    fn intern(
        b: &mut DfaBuilder<'_>,
        d: &mut Dfa,
        ids: &mut HashMap<Vec<u16>, u8>,
        sets: &mut Vec<Vec<usize>>,
        stride: usize,
        set: &mut [usize],
    ) -> Option<u8> {
        if b.has_match(set) {
            return Some(DFA_ACCEPT);
        }
        if set.is_empty() {
            return Some(DFA_DEAD);
        }
        let key = set_key(set);
        if let Some(&id) = ids.get(&key) {
            return Some(id);
        }
        if sets.len() >= MAX_DFA_STATES {
            return None;
        }
        let id = sets.len() as u8;
        ids.insert(key, id);
        sets.push(set.to_vec());
        d.trans.resize(d.trans.len() + stride, 0);
        // At the end of the text the end anchors are passed.
        let ends: Vec<usize> = set.iter().filter(|&&i| b.nfa[i].kind == NfaKind::Eol).map(|&i| b.nfa[i].out).collect();
        let mut closed = Vec::new();
        if !ends.is_empty() {
            b.closure(&mut closed, &ends, false, true);
        }
        d.at_end.push(!ends.is_empty() && b.has_match(&closed));
        Some(id)
    }
    let mut first = Vec::new();
    b.closure(&mut first, &[start], true, false);
    d.start = intern(&mut b, &mut d, &mut ids, &mut sets, stride, &mut first)?;
    let (mut moved, mut next) = (Vec::new(), Vec::new());
    let mut id = DFA_FIRST;
    while id < sets.len() {
        for class in 0..stride {
            if class == 0 || class >= classes {
                // Not ASCII (or no such class).
                d.trans[id * stride + class] = DFA_BAIL;
                continue;
            }
            let c = *sample.get(class).expect("a class has a byte");
            moved.clear();
            for &i in &sets[id] {
                let s = &nfa[i];
                if s.kind == NfaKind::Set && (s.set[c >> 6] >> (c & 63)) & 1 != 0 {
                    moved.push(s.out);
                }
            }
            moved.extend_from_slice(&restart);
            next.clear();
            b.closure(&mut next, &moved, false, false);
            let to = intern(&mut b, &mut d, &mut ids, &mut sets, stride, &mut next)?;
            if b.steps < 0 {
                return None;
            }
            d.trans[id * stride + class] = to;
        }
        id += 1;
    }
    Some(d)
}
