//! The size of the program a linear matcher compiles for a tree (`re2Cost` of `re2.go`).
//!
//! The Go engine writes a tree with no lookaround and no backreference in the syntax of the standard library's
//! regexp package, and bounds the program that package is asked to compile. This crate keeps its own translation
//! for the `regex` crate (see `pattern.rs`), and takes the same bound from here, for the `regex` crate and for the
//! automaton of `dfa.rs`.

use super::parse::{Node, NodeKind, UNBOUNDED};

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
