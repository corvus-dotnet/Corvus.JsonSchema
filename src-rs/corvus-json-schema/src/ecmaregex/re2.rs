//! The size of the program a linear matcher compiles for a tree (`re2Cost` of `re2.go`).
//!
//! The Go engine writes a tree with no lookaround and no backreference in the syntax of the standard library's
//! regexp package, and bounds the program that package is asked to compile. This crate keeps its own translation
//! for the `regex` crate (see `pattern.rs`), and takes the same bound from here, for the `regex` crate and for the
//! automaton of `dfa.rs`.

use super::parse::{Node, NodeKind, UNBOUNDED};

/// Estimates the number of instructions of the program a linear matcher runs for a tree, which is what a match on
/// it can cost for each byte of a text when every instruction has a thread on it. It is [`re2_cost`] with one thing
/// a linear matcher does to a tree before it compiles it: alternatives that are each one character or one class are
/// one class, so `(?:a|b|c)*` is the two instructions of `[abc]*`. (The Go engine asks the regexp package what it
/// compiled. The `regex` crate does not say, so this is worked out from the tree.)
pub(crate) fn linear_cost(n: &Node) -> i64 {
    match n.kind {
        NodeKind::Empty => 0,
        NodeKind::Alt if n.subs.iter().all(|sub| matches!(sub.kind, NodeKind::Char | NodeKind::Set)) => 1,
        NodeKind::Cat | NodeKind::Alt | NodeKind::Group => {
            let mut cost = n.subs.len() as i64 - 1;
            for sub in &n.subs {
                cost += linear_cost(sub);
                if cost > MAX_RE2_COST {
                    return MAX_RE2_COST + 1;
                }
            }
            cost
        }
        NodeKind::Repeat => {
            let copies = (if n.max == UNBOUNDED { n.min } else { n.max }).max(1) as i64;
            let body = linear_cost(&n.subs[0]) + 1;
            if copies > MAX_RE2_COST || body * copies > MAX_RE2_COST {
                return MAX_RE2_COST + 1;
            }
            body * copies
        }
        _ => 1,
    }
}

/// The cap on [`linear_size`]: a program of that size is never compiled.
const MAX_LINEAR_SIZE: i64 = 1 << 30;

/// Estimates the memory the `regex` crate holds for the program it compiles for a tree, in bytes. That crate does
/// not say what a compiled pattern holds, so this is what the cache of compiled patterns counts for one. A
/// character is a state or a few, a class of ASCII characters is one, and a class with characters beyond ASCII is
/// the automaton of their UTF-8 forms, which grows with its ranges. A counted repetition is written out. Measured
/// against what the matcher of that crate reports (`regex-automata`'s `memory_usage`), the estimate was above it
/// for every pattern of the benchmark corpora that the crate is given and for classes of Unicode properties.
pub(crate) fn linear_size(n: &Node) -> i64 {
    /// The bytes of one state, with its share of what the matcher holds beside the program.
    const STATE: i64 = 96;
    /// The states of a class with characters beyond ASCII, before its ranges are counted.
    const WIDE_CLASS: i64 = 64;
    /// What the matcher holds for any pattern, in states.
    const FIXED: i64 = 56;
    fn states(n: &Node) -> i64 {
        match n.kind {
            NodeKind::Empty => 0,
            NodeKind::Char => {
                if n.r < 0x80 {
                    1
                } else {
                    4
                }
            }
            NodeKind::Set => {
                let ranges = &n.class().ranges;
                if ranges.last().is_none_or(|&last| last < 0x80) { 1 } else { WIDE_CLASS + ranges.len() as i64 / 2 }
            }
            NodeKind::Cat | NodeKind::Alt | NodeKind::Group => {
                let mut total = n.subs.len() as i64;
                for sub in &n.subs {
                    total += states(sub);
                    if total > MAX_LINEAR_SIZE {
                        return MAX_LINEAR_SIZE;
                    }
                }
                total
            }
            NodeKind::Repeat => {
                let copies = (if n.max == UNBOUNDED { n.min } else { n.max }).max(1) as i64;
                (copies * (states(&n.subs[0]) + 1)).min(MAX_LINEAR_SIZE)
            }
            _ => 1,
        }
    }
    (STATE * (states(n) + FIXED)).min(MAX_LINEAR_SIZE)
}

/// The largest program a linear matcher is asked to compile, in the instructions estimated for it before it is
/// written (see [`re2_cost`]). A counted repetition is written out, so `a{1000}` is a thousand instructions, and a
/// linear matcher takes time proportional to its program to compile and, for every character of a text, to match.
pub(crate) const MAX_RE2_COST: i64 = 10_000;

/// Estimates the number of instructions of the program a linear matcher compiles for a tree. A counted repetition
/// is written out: `x{n,m}` is m copies of x, and `x{n,}` is n copies and a loop. The estimate stops counting above
/// `MAX_RE2_COST`.
pub(crate) fn re2_cost(n: &Node) -> i64 {
    match n.kind {
        NodeKind::Empty => 0,
        NodeKind::Cat | NodeKind::Alt | NodeKind::Group => {
            let mut cost = n.subs.len() as i64 - 1;
            for sub in &n.subs {
                cost += re2_cost(sub);
                if cost > MAX_RE2_COST {
                    return MAX_RE2_COST + 1;
                }
            }
            cost
        }
        NodeKind::Repeat => {
            let mut copies = n.max as i64;
            if n.max == UNBOUNDED {
                copies = n.min as i64;
            }
            if copies < 1 {
                copies = 1;
            }
            // Each copy has the body and one instruction for the choice to enter it.
            let body = re2_cost(&n.subs[0]) + 1;
            if copies > MAX_RE2_COST || body * copies > MAX_RE2_COST {
                return MAX_RE2_COST + 1;
            }
            body * copies
        }
        _ => 1,
    }
}
