//! The compiler: a syntax tree to a program of the backtracking matcher, with what the matcher needs to give up
//! early (`compile.go`).

use std::collections::HashMap;
use std::sync::Arc;

use super::Rune;
use super::charset::CharSet;
use super::parse::{Node, NodeKind, UNBOUNDED};
use super::units::RUNE_ERROR;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum Op {
    /// `Char` and `Set` consume one code point going forward. The `Back` forms consume the one before the position,
    /// which is how the body of a lookbehind runs.
    Char,
    CharBack,
    Set,
    SetBack,
    /// Consumes a run of literal bytes going forward.
    Lit,
    Fail,
    Bol,
    Eol,
    WordBoundary,
    NotWordBoundary,
    /// `LineStart` and `LineEnd` are `^` and `$` with the m modifier. The `Fold` forms of the word boundaries and of
    /// the backreferences belong to a case-insensitive group.
    LineStart,
    LineEnd,
    WordBoundaryFold,
    NotWordBoundaryFold,
    Jmp,
    /// Continues at `x` and leaves `y` as the alternative.
    Split,
    /// `SaveTmp` notes where a group was entered. `Capture` records the group once it has closed.
    SaveTmp,
    Capture,
    /// Resets the groups `a` to `b`, as ECMA-262 does at the start of each iteration of a quantified body.
    Clear,
    Backref,
    BackrefBack,
    BackrefFold,
    BackrefFoldBack,
    /// `BackrefUnits` and `BackrefUnitsBack` compare a group a code unit at a time, for a pattern matched by code
    /// unit.
    BackrefUnits,
    BackrefUnitsBack,
    /// `Look` opens a lookaround. `LookOk` ends the body of a positive one and `LookFail` the body of a negative
    /// one.
    Look,
    LookOk,
    LookFail,
    /// `Rep` and `RepLazy` repeat one character or class without entering the general loop.
    Rep,
    RepLazy,
    /// `LoopInit`, `Loop` and `LoopIter` run a counted or possibly empty quantified body.
    LoopInit,
    Loop,
    LoopIter,
    Match,
}

/// One instruction of the backtracking matcher.
#[derive(Clone, Debug)]
pub(crate) struct Inst {
    pub op: Op,
    /// Marks a lazy `Loop`, a backward `Capture`, a negative `Look`, and a `BackrefFold` that folds case as the u
    /// flag grammar does.
    pub flag: bool,
    pub r: Rune,
    pub set: Option<Arc<CharSet>>,
    pub lit: Box<[u8]>,
    /// `x` and `y` are jump targets.
    pub x: i32,
    pub y: i32,
    /// `a` and `b` are a group, a loop or a range of groups, depending on the instruction.
    pub a: i32,
    pub b: i32,
    /// `min` and `max` bound a repetition. A max of -1 is no bound.
    pub min: i32,
    pub max: i32,
    /// The least number of bytes of text that every way from this instruction to a match consumes going forward
    /// (see `set_needs`). It is set for a `Loop`, and counts none of the iterations the loop has still to make.
    pub need: i32,
    /// The least number of bytes one iteration of the body of a `Loop` consumes going forward.
    pub iter: i32,
    /// The number of the instruction's memo point, counted from 1, or 0 when it has none (see [`MemoPoint`]). It is
    /// set for a `Split`, a `Loop`, a `Rep` and a `RepLazy`.
    pub m: i32,
}

impl Inst {
    fn new(op: Op) -> Inst {
        Inst {
            op,
            flag: false,
            r: 0,
            set: None,
            lit: Box::default(),
            x: 0,
            y: 0,
            a: 0,
            b: 0,
            min: 0,
            max: 0,
            need: 0,
            iter: 0,
            m: 0,
        }
    }
}

/// The number of loops whose state a memo point can tell apart.
pub(crate) const MAX_MEMO_LOOPS: usize = 2;

/// A place where the matcher makes a choice and can remember that the choice has failed: a split, a loop that may
/// be entered or left, and what follows a single character loop that gives characters back or takes more. Whether a
/// way from such a place leads to a match depends on the place and the position alone when nothing else the matcher
/// keeps can change what follows. So a pattern with a backreference has no memo points (what a group holds changes
/// what follows), and neither has a place inside a loop that counts its iterations up to a bound. Inside a loop
/// with no upper bound and a least count of zero or one, one thing more decides what follows: whether the loop
/// would refuse the iteration as empty if it ended here. That is one bit for each such loop the place is in
/// (`loops`, innermost last), and a place in more than `MAX_MEMO_LOOPS` of them has no memo point.
#[derive(Clone, Copy, Debug)]
pub(crate) struct MemoPoint {
    pub loops: [i32; MAX_MEMO_LOOPS],
    pub mins: [i32; MAX_MEMO_LOOPS],
}

/// One counted loop that the instruction being written is inside of: its number, its least count, and whether its
/// state is one bit to a memo point (no upper bound, and a least count of zero or one).
#[derive(Clone, Copy)]
struct LoopContext {
    loop_: i32,
    min: i32,
    simple: bool,
}

/// A pattern compiled for the backtracking matcher.
#[derive(Debug)]
pub(crate) struct Program {
    pub insts: Vec<Inst>,
    /// The number of capturing groups some backreference names, and the number of counted loops.
    pub groups: usize,
    pub loops: usize,
    /// The pattern was read without the u flag, so the text is matched by UTF-16 code unit (see `units`).
    pub units: bool,
    /// Every match starts at the start of the text.
    pub anchored: bool,
    /// Literal text every match starts with.
    pub prefix: String,
    /// The least number of bytes a match consumes (see `set_needs`).
    pub need: usize,
    /// The memo points of the program (see [`MemoPoint`]).
    pub points: Vec<MemoPoint>,
    /// For each instruction, which characters can come next on a way from it to the end of its match (see
    /// `set_quick`).
    pub quick: Vec<Quick>,
    /// Every code point a match can start with. `None` when a match can be empty or when the set is not known.
    pub first: Option<CharSet>,
}

struct Compiler {
    insts: Vec<Inst>,
    /// Maps a capturing group's number to its slot, for the groups some backreference names. The others are not
    /// recorded at all.
    tracked: HashMap<i32, i32>,
    loops: usize,
    /// The pattern was read with the u flag grammar, which decides how a backreference folds case.
    unicode: bool,
    /// `memo` says the pattern has no backreference, so its choices can have memo points. `open` lists the counted
    /// loops around the instruction being written, outermost first, inside the innermost lookaround: what a
    /// lookaround's body does depends on nothing outside it. `points` collects the memo points.
    memo: bool,
    open: Vec<LoopContext>,
    points: Vec<MemoPoint>,
}

/// Compiles a tree. Where the Go compiler copies the nodes it changes, because its tree is shared, this one takes
/// the tree and changes it in place.
pub(crate) fn compile_program(root: Node, unicode: bool) -> Program {
    let mut open = Vec::new();
    let root = simplify(root, &mut open);
    let mut c = Compiler {
        insts: Vec::new(),
        tracked: HashMap::new(),
        loops: 0,
        unicode,
        memo: false,
        open: Vec::new(),
        points: Vec::new(),
    };
    let mut referenced = Vec::new();
    collect_backrefs(&root, &mut referenced);
    // Slots follow group order, so the groups inside any one subtree take a contiguous range of slots.
    referenced.sort_unstable();
    referenced.dedup();
    for (slot, g) in referenced.into_iter().enumerate() {
        c.tracked.insert(g, slot as i32);
    }
    c.memo = c.tracked.is_empty();
    c.emit(&root, false);
    c.add(Inst::new(Op::Match));
    let mut insts = c.insts;
    let need = set_needs(&mut insts);
    let quick = set_quick(&insts);
    let mut p = Program {
        insts,
        groups: c.tracked.len(),
        loops: c.loops,
        units: !unicode,
        anchored: starts_at_bol(&root),
        prefix: String::new(),
        need,
        points: c.points,
        quick,
        first: None,
    };
    if !p.anchored {
        literal_prefix(&root, &mut p.prefix);
        if p.prefix.is_empty() {
            let mut pairs = Vec::new();
            if let Some(nullable) = first_chars(&root, &mut pairs)
                && !nullable
            {
                p.first = Some(CharSet::new(&pairs, false));
            }
        }
    }
    p
}

/// Returns the tree with two rewrites, both of which V8 makes. Neither changes what the pattern matches or what any
/// group holds. Without them a pattern can take time exponential in its counts for no reason that ECMA-262 gives.
///
/// A backreference to a group it is inside of is the empty string. The group is not set until it closes, and the
/// repetition around a group clears it at the start of each iteration, so inside the group it is never set.
///
/// A repetition whose body cannot consume a character, such as `(?:\b|(?=a)|){4,5}`, is the body once when its
/// minimum is one or more, and nothing when its minimum is zero. Every iteration of such a body is empty. ECMA-262
/// refuses an empty iteration after the minimum, so the iterations are exactly the minimum. Each of them starts
/// with the groups of the body cleared and at the same position, so what the last one can do does not depend on the
/// ones before it, and the ways through the last one come in the same order whatever the others chose. Run as
/// written, the matcher would try every combination of the ways through every iteration.
pub(crate) fn simplify(mut n: Node, open: &mut Vec<i32>) -> Node {
    if n.kind == NodeKind::Backref {
        if n.groups.iter().all(|g| open.contains(g)) {
            return Node::new(NodeKind::Empty);
        }
        return n;
    }
    if n.subs.is_empty() {
        return n;
    }
    let opened = n.kind == NodeKind::Group && n.group > 0;
    if opened {
        open.push(n.group);
    }
    n.subs = std::mem::take(&mut n.subs).into_iter().map(|sub| simplify(sub, open)).collect();
    if opened {
        open.pop();
    }
    if n.kind == NodeKind::Repeat && n.max != 0 && !consumes(&n.subs[0]) {
        if n.min == 0 {
            return Node::new(NodeKind::Empty);
        }
        return n.subs.pop().expect("a repetition has a body");
    }
    n
}

/// Whether `n` can match one character or more. It errs towards true.
fn consumes(n: &Node) -> bool {
    match n.kind {
        NodeKind::Char | NodeKind::Set | NodeKind::Backref => true,
        NodeKind::Cat | NodeKind::Alt | NodeKind::Group => n.subs.iter().any(consumes),
        NodeKind::Repeat => n.max != 0 && consumes(&n.subs[0]),
        _ => false,
    }
}

fn collect_backrefs(n: &Node, out: &mut Vec<i32>) {
    if n.kind == NodeKind::Backref {
        out.extend_from_slice(&n.groups);
    }
    for sub in &n.subs {
        collect_backrefs(sub, out);
    }
}

/// The UTF-8 bytes of a code point that [`literal_rune`] accepts.
fn push_rune(out: &mut String, r: Rune) {
    out.push(char::from_u32(r as u32).expect("a literal code point is a character"));
}

impl Compiler {
    fn add(&mut self, i: Inst) -> i32 {
        self.insts.push(i);
        (self.insts.len() - 1) as i32
    }

    fn pc(&self) -> i32 {
        self.insts.len() as i32
    }

    /// The number of a new memo point for an instruction written here, or 0 when a choice here cannot be
    /// remembered.
    fn point(&mut self) -> i32 {
        if !self.memo || self.open.len() > MAX_MEMO_LOOPS {
            return 0;
        }
        let mut p = MemoPoint { loops: [-1; MAX_MEMO_LOOPS], mins: [0; MAX_MEMO_LOOPS] };
        for (k, l) in self.open.iter().enumerate() {
            if !l.simple {
                return 0;
            }
            (p.loops[k], p.mins[k]) = (l.loop_, l.min);
        }
        self.points.push(p);
        self.points.len() as i32
    }

    /// The first and last slot of the tracked groups inside `n`, or `None` when it has none.
    fn group_range(&self, n: &Node) -> Option<(i32, i32)> {
        fn walk(c: &Compiler, n: &Node, range: &mut Option<(i32, i32)>) {
            if n.kind == NodeKind::Group
                && n.group > 0
                && let Some(&t) = c.tracked.get(&n.group)
            {
                *range = Some(match *range {
                    Some((lo, hi)) => (lo.min(t), hi.max(t)),
                    None => (t, t),
                });
            }
            for sub in &n.subs {
                walk(c, sub, range);
            }
        }
        let mut range = None;
        walk(self, n, &mut range);
        range
    }

    /// Writes the instructions for `n`. With `back` set the instructions match right to left, as the body of a
    /// lookbehind must.
    fn emit(&mut self, n: &Node, back: bool) {
        match n.kind {
            NodeKind::Empty => {}
            NodeKind::Char => {
                self.add(Inst { r: n.r, ..Inst::new(if back { Op::CharBack } else { Op::Char }) });
            }
            NodeKind::Set => {
                let set = n.class();
                if set.is_empty() {
                    self.add(Inst::new(Op::Fail));
                } else {
                    self.add(Inst { set: Some(set.clone()), ..Inst::new(if back { Op::SetBack } else { Op::Set }) });
                }
            }
            NodeKind::Cat => {
                if back {
                    for sub in n.subs.iter().rev() {
                        self.emit(sub, true);
                    }
                    return;
                }
                let mut i = 0;
                while i < n.subs.len() {
                    // A run of literal characters becomes one comparison of bytes.
                    let mut j = i;
                    let mut lit = String::new();
                    while j < n.subs.len() && n.subs[j].kind == NodeKind::Char && literal_rune(n.subs[j].r) {
                        push_rune(&mut lit, n.subs[j].r);
                        j += 1;
                    }
                    if j - i >= 2 {
                        self.add(Inst { lit: lit.into_bytes().into_boxed_slice(), ..Inst::new(Op::Lit) });
                        i = j;
                        continue;
                    }
                    self.emit(&n.subs[i], false);
                    i += 1;
                }
            }
            NodeKind::Alt => {
                let mut jumps = Vec::new();
                for (i, sub) in n.subs.iter().enumerate() {
                    if i == n.subs.len() - 1 {
                        self.emit(sub, back);
                        break;
                    }
                    let m = self.point();
                    let split = self.add(Inst { m, ..Inst::new(Op::Split) });
                    self.insts[split as usize].x = self.pc();
                    self.emit(sub, back);
                    jumps.push(self.add(Inst::new(Op::Jmp)));
                    self.insts[split as usize].y = self.pc();
                }
                for j in jumps {
                    self.insts[j as usize].x = self.pc();
                }
            }
            NodeKind::Group => {
                let tracked = if n.group == 0 { None } else { self.tracked.get(&n.group).copied() };
                let Some(t) = tracked else {
                    self.emit(&n.subs[0], back);
                    return;
                };
                self.add(Inst { a: t, ..Inst::new(Op::SaveTmp) });
                self.emit(&n.subs[0], back);
                self.add(Inst { a: t, flag: back, ..Inst::new(Op::Capture) });
            }
            NodeKind::Bol => {
                self.add(Inst::new(Op::Bol));
            }
            NodeKind::Eol => {
                self.add(Inst::new(Op::Eol));
            }
            NodeKind::LineStart => {
                self.add(Inst::new(Op::LineStart));
            }
            NodeKind::LineEnd => {
                self.add(Inst::new(Op::LineEnd));
            }
            NodeKind::WordBoundary => {
                self.add(Inst::new(if n.fold { Op::WordBoundaryFold } else { Op::WordBoundary }));
            }
            NodeKind::NotWordBoundary => {
                self.add(Inst::new(if n.fold { Op::NotWordBoundaryFold } else { Op::NotWordBoundary }));
            }
            NodeKind::Backref => {
                let op = match (n.fold, back, self.unicode) {
                    (true, true, _) => Op::BackrefFoldBack,
                    (true, false, _) => Op::BackrefFold,
                    (false, true, false) => Op::BackrefUnitsBack,
                    (false, true, true) => Op::BackrefBack,
                    (false, false, false) => Op::BackrefUnits,
                    (false, false, true) => Op::Backref,
                };
                // When several groups share the name, at most one has taken part, and the others match the empty
                // string. So matching each in turn matches the one that took part.
                for g in &n.groups {
                    self.add(Inst { a: self.tracked[g], flag: self.unicode, ..Inst::new(op) });
                }
            }
            NodeKind::Look => {
                let look = self.add(Inst { flag: n.negative, ..Inst::new(Op::Look) });
                // Whether the body matches depends on nothing outside it.
                let outer = std::mem::take(&mut self.open);
                self.emit(&n.subs[0], n.behind);
                self.open = outer;
                self.add(Inst::new(if n.negative { Op::LookFail } else { Op::LookOk }));
                self.insts[look as usize].x = self.pc();
            }
            NodeKind::Repeat => self.emit_repeat(n, back),
        }
    }

    fn emit_body(&mut self, body: &Node, back: bool, clear: Option<(i32, i32)>) {
        if let Some((lo, hi)) = clear {
            self.add(Inst { a: lo, b: hi, ..Inst::new(Op::Clear) });
        }
        self.emit(body, back);
    }

    /// Writes a choice between entering the body and leaving the loop, to be completed by `set_split`.
    fn split(&mut self) -> i32 {
        let m = self.point();
        self.add(Inst { m, ..Inst::new(Op::Split) })
    }

    /// Completes a split in the order the quantifier prefers.
    fn set_split(&mut self, lazy: bool, at: i32, enter: i32, exit: i32) {
        let i = &mut self.insts[at as usize];
        if lazy {
            (i.x, i.y) = (exit, enter);
        } else {
            (i.x, i.y) = (enter, exit);
        }
    }

    fn emit_repeat(&mut self, n: &Node, back: bool) {
        let body = &n.subs[0];
        if n.max == 0 {
            return;
        }
        let (min, max) = (n.min, n.max);
        if !back && (body.kind == NodeKind::Char || body.kind == NodeKind::Set && !body.class().is_empty()) {
            let m = self.point();
            self.add(Inst {
                r: body.r,
                set: body.set.clone(),
                min,
                max,
                m,
                ..Inst::new(if n.lazy { Op::RepLazy } else { Op::Rep })
            });
            return;
        }
        let clear = self.group_range(body);
        // A body that always consumes something needs no check for empty iterations, so the unbounded forms and the
        // optional form are plain choices.
        if !nullable(body) {
            if n.min == 0 && n.max == UNBOUNDED {
                let at = self.split();
                let enter = self.pc();
                self.emit_body(body, back, clear);
                self.add(Inst { x: at, ..Inst::new(Op::Jmp) });
                let exit = self.pc();
                self.set_split(n.lazy, at, enter, exit);
                return;
            }
            if n.min == 1 && n.max == UNBOUNDED {
                let enter = self.pc();
                self.emit_body(body, back, clear);
                let at = self.split();
                let exit = self.pc();
                self.set_split(n.lazy, at, enter, exit);
                return;
            }
            if n.min == 0 && n.max == 1 {
                let at = self.split();
                let enter = self.pc();
                self.emit_body(body, back, clear);
                let exit = self.pc();
                self.set_split(n.lazy, at, enter, exit);
                return;
            }
        }
        let k = self.loops as i32;
        self.loops += 1;
        self.add(Inst { a: k, ..Inst::new(Op::LoopInit) });
        let iter = if back { 0 } else { min_length(body) };
        let simple = max == UNBOUNDED && min <= 1;
        // The loop is left or entered from here, and what follows either depends on the loops around this one.
        let point = if simple { self.point() } else { 0 };
        let loop_ = self.add(Inst { a: k, min, max, flag: n.lazy, iter: iter as i32, m: point, ..Inst::new(Op::Loop) });
        self.add(Inst { a: k, ..Inst::new(Op::LoopIter) });
        self.open.push(LoopContext { loop_: k, min, simple });
        self.emit_body(body, back, clear);
        self.open.pop();
        self.add(Inst { x: loop_, ..Inst::new(Op::Jmp) });
        self.insts[loop_ as usize].x = self.pc();
    }
}

/// The need of an instruction from which no way leads to a match. Every need is capped at it.
pub(crate) const NO_MATCH: i64 = 1 << 30;

/// The number of bytes of UTF-8 a code point takes.
fn rune_len(r: Rune) -> i64 {
    match r {
        0..=0x7F => 1,
        0x80..=0x7FF => 2,
        0x800..=0xFFFF => 3,
        _ => 4,
    }
}

/// The least number of bytes a match of `n` consumes, capped at `NO_MATCH`. Like the needs, it counts a character
/// as its UTF-8 bytes when the matcher compares bytes for it and as one byte otherwise, so it can only be too
/// small.
fn min_length(n: &Node) -> i64 {
    match n.kind {
        NodeKind::Char => {
            if literal_rune(n.r) {
                rune_len(n.r)
            } else {
                1
            }
        }
        NodeKind::Set => {
            if n.class().is_empty() {
                NO_MATCH
            } else {
                1
            }
        }
        NodeKind::Cat => {
            let mut total = 0;
            for sub in &n.subs {
                total += min_length(sub);
                if total >= NO_MATCH {
                    return NO_MATCH;
                }
            }
            total
        }
        NodeKind::Alt => n.subs.iter().map(min_length).min().unwrap_or(NO_MATCH).min(NO_MATCH),
        NodeKind::Group => min_length(&n.subs[0]),
        NodeKind::Repeat => times_need(n.min as i64, min_length(&n.subs[0])),
        _ => 0,
    }
}

/// `count` times `need`, capped at `NO_MATCH`.
fn times_need(count: i64, need: i64) -> i64 {
    if count == 0 || need == 0 {
        return 0;
    }
    if need >= NO_MATCH || count >= NO_MATCH / need {
        return NO_MATCH;
    }
    count * need
}

/// Works out, for every instruction, the least number of bytes of text that any way from it to a match consumes
/// going forward, and writes it into the counted loops. It returns the need of the whole program. The matcher gives
/// up at a counted loop when fewer bytes than that are left, as V8 does at every choice. A loop whose iterations
/// can be empty would otherwise try every combination of its ways at a position from which the rest of the pattern
/// cannot match at all.
///
/// A counted loop that is entered needs its least count of iterations, each of them the least its body consumes
/// (`Inst::iter`), and then what follows it. The loop instruction itself is also reached after each iteration, so
/// its own need counts only what follows, and the matcher adds the iterations it has still to make.
///
/// A character is counted as one byte, a literal run as its bytes and a single character loop as its minimum, so
/// the figure can only be too small. The body of a lookaround ends at its own last instruction and the instruction
/// that opens one goes straight to what follows it, so a body is counted by itself. An instruction that matches
/// right to left, which is the body of a lookbehind, counts nothing.
pub(crate) fn set_needs(insts: &mut [Inst]) -> usize {
    needs(insts).0
}

/// [`set_needs`], and also the number of passes over the instructions it made, the last of which changed nothing.
pub(crate) fn needs(insts: &mut [Inst]) -> (usize, usize) {
    let mut need = vec![NO_MATCH as i32; insts.len()];
    let mut passes = 0;
    // The needs only fall, and nearly every way goes forward, so going through the instructions from the last to
    // the first settles all but the loops in one pass.
    let mut changed = true;
    while changed {
        changed = false;
        passes += 1;
        for pc in (0..insts.len()).rev() {
            let i = &insts[pc];
            let mut n = NO_MATCH;
            // Both figures can be NO_MATCH, so the sum is made in 64 bits: in 32 it would wrap to a negative need.
            let mut next = |to: i32, weight: i64| {
                let v = need[to as usize] as i64 + weight;
                if v < n {
                    n = v;
                }
            };
            let after = (pc + 1) as i32;
            match i.op {
                Op::Match | Op::LookOk | Op::LookFail => n = 0,
                Op::Fail => {}
                Op::Char | Op::Set => next(after, 1),
                Op::Lit => next(after, i.lit.len() as i64),
                Op::Rep | Op::RepLazy => next(after, i.min as i64),
                Op::Jmp => {
                    // The one way that goes back is the jump at the end of the body of a loop, to the instruction
                    // at its head, which this pass has not reached. The need of that instruction is the need of
                    // what follows the loop, since its other way, through the body, comes back to it and cannot
                    // need less. So the jump takes what follows the loop, which this pass has settled. Taking the
                    // need of the head would settle one loop of a nest for each pass: a thousand passes for loops a
                    // thousand deep.
                    let head = &insts[i.x as usize];
                    if (i.x as usize) < pc && head.op == Op::Loop {
                        next(head.x, 0);
                    } else if (i.x as usize) < pc && head.op == Op::Split {
                        next(head.x, 0);
                        next(head.y, 0);
                    } else {
                        next(i.x, 0);
                    }
                }
                Op::Look => next(i.x, 0),
                Op::Split => {
                    next(i.x, 0);
                    next(i.y, 0);
                }
                Op::Loop => {
                    next(after, 0);
                    next(i.x, 0);
                }
                Op::LoopInit => {
                    let loop_ = &insts[pc + 1];
                    next(after, times_need(loop_.min as i64, loop_.iter as i64));
                }
                _ => next(after, 0),
            }
            let n = n.min(NO_MATCH) as i32;
            if n < need[pc] {
                need[pc] = n;
                changed = true;
            }
        }
    }
    for (pc, i) in insts.iter_mut().enumerate() {
        if i.op == Op::Loop {
            i.need = need[pc];
        }
    }
    (need[0] as usize, passes)
}

/// What can come next on a way from an instruction to the end of its match, which is the end of the pattern or of
/// the lookaround the instruction is in. The matcher looks at it before it takes a way: when the next character of
/// the text is none that the way can start with, and the way cannot end without one, the way fails, so it is not
/// taken, and where it was one of two no choice is kept for it. V8 makes the same check ("quick check") at its
/// choices. It is what keeps a choice between ways that need different characters from being a choice.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub(crate) struct Quick {
    /// The characters below 128 a way can consume next. `other` says it can consume a character that is not ASCII.
    pub ascii: [u64; 2],
    pub other: bool,
    /// `mid` says a way reaches the end of its match consuming nothing more while text is left, and `end` that one
    /// does at the end of the text.
    pub mid: bool,
    pub end: bool,
}

impl Quick {
    /// Whether a way can go on from the position.
    #[inline(always)]
    pub(crate) fn allows(&self, input: &[u8], pos: usize) -> bool {
        if pos >= input.len() {
            return self.end;
        }
        if self.mid {
            return true;
        }
        let c = input[pos];
        if c < 0x80 {
            return (self.ascii[(c >> 6) as usize] >> (c & 63)) & 1 != 0;
        }
        self.other
    }

    fn add_rune(&mut self, r: Rune) {
        if (0..0x80).contains(&r) {
            self.ascii[(r >> 6) as usize] |= 1 << (r & 63);
        } else {
            self.other = true;
        }
    }

    fn add_set(&mut self, s: &CharSet) {
        self.ascii[0] |= s.ascii[0];
        self.ascii[1] |= s.ascii[1];
        if s.ranges.last().is_some_and(|&last| last >= 0x80) {
            self.other = true;
        }
    }

    fn union(&mut self, o: Quick) {
        self.ascii[0] |= o.ascii[0];
        self.ascii[1] |= o.ascii[1];
        self.other = self.other || o.other;
        self.mid = self.mid || o.mid;
        self.end = self.end || o.end;
    }
}

/// Works out the quick of every instruction. Every figure is a set that can only be too large: an assertion is
/// taken to hold, a backreference and an instruction that matches right to left (the body of a lookbehind) to allow
/// anything. A positive lookahead needs both its body and what follows it to go on from the same position, so it
/// allows what both allow.
pub(crate) fn set_quick(insts: &[Inst]) -> Vec<Quick> {
    quicks(insts).0
}

/// [`set_quick`], and also the number of passes over the instructions it made, the last of which changed nothing.
pub(crate) fn quicks(insts: &[Inst]) -> (Vec<Quick>, usize) {
    let mut q = vec![Quick::default(); insts.len()];
    let all = Quick { ascii: [!0, !0], other: true, mid: true, end: true };
    let mut passes = 0;
    // The sets only grow, and nearly every way goes forward, so going through the instructions from the last to the
    // first settles all but the loops in one pass.
    let mut changed = true;
    while changed {
        changed = false;
        passes += 1;
        for pc in (0..insts.len()).rev() {
            let i = &insts[pc];
            let mut n = Quick::default();
            match i.op {
                Op::Match | Op::LookOk | Op::LookFail => n = all,
                Op::Fail => {}
                Op::Char => n.add_rune(i.r),
                Op::Set => n.add_set(i.set.as_ref().expect("a set instruction has a class")),
                Op::Lit => n.add_rune(i.lit[0] as Rune),
                Op::Rep | Op::RepLazy => {
                    match &i.set {
                        Some(set) => n.add_set(set),
                        None => n.add_rune(i.r),
                    }
                    if i.min == 0 {
                        n.union(q[pc + 1]);
                    }
                }
                Op::Jmp => {
                    // The jump at the end of the body of a loop goes back to the head of the loop, which this pass
                    // has not reached. The head allows what follows the loop, which this pass has settled, and what
                    // the body starts with. So the jump takes what follows the loop too, and the first pass settles
                    // the head of every loop, the second every instruction, and the third finds nothing to change,
                    // however deep the loops nest.
                    n = q[i.x as usize];
                    let head = &insts[i.x as usize];
                    if (i.x as usize) < pc && head.op == Op::Loop {
                        n.union(q[head.x as usize]);
                    } else if (i.x as usize) < pc && head.op == Op::Split {
                        n.union(q[head.x as usize]);
                        n.union(q[head.y as usize]);
                    }
                }
                Op::Split => {
                    n = q[i.x as usize];
                    n.union(q[i.y as usize]);
                }
                Op::Loop => {
                    n = q[pc + 1];
                    n.union(q[i.x as usize]);
                }
                Op::Eol => n.end = q[pc + 1].end,
                Op::Look => {
                    n = q[i.x as usize];
                    if !i.flag {
                        let b = q[pc + 1];
                        n.ascii[0] &= b.ascii[0];
                        n.ascii[1] &= b.ascii[1];
                        (n.other, n.mid, n.end) = (n.other && b.other, n.mid && b.mid, n.end && b.end);
                    }
                }
                Op::CharBack
                | Op::SetBack
                | Op::Backref
                | Op::BackrefBack
                | Op::BackrefFold
                | Op::BackrefFoldBack
                | Op::BackrefUnits
                | Op::BackrefUnitsBack => n = all,
                _ => n = q[pc + 1],
            }
            if n != q[pc] {
                q[pc] = n;
                changed = true;
            }
        }
    }
    (q, passes)
}

/// Whether a code point can be matched by comparing its UTF-8 bytes. A surrogate has no encoding, and U+FFFD is
/// also what a malformed byte of the text decodes to.
pub(crate) fn literal_rune(r: Rune) -> bool {
    (0..=0x10FFFF).contains(&r) && !(0xD800..=0xDFFF).contains(&r) && r != RUNE_ERROR
}

/// Whether `n` can match without consuming anything. It errs towards true.
fn nullable(n: &Node) -> bool {
    match n.kind {
        NodeKind::Char | NodeKind::Set => false,
        NodeKind::Cat => n.subs.iter().all(nullable),
        NodeKind::Alt => n.subs.iter().any(nullable),
        NodeKind::Group => nullable(&n.subs[0]),
        NodeKind::Repeat => n.min == 0 || nullable(&n.subs[0]),
        _ => true,
    }
}

/// Whether every way through `n` begins with `^`.
fn starts_at_bol(n: &Node) -> bool {
    match n.kind {
        NodeKind::Bol => true,
        NodeKind::Cat => starts_at_bol(&n.subs[0]),
        NodeKind::Alt => n.subs.iter().all(starts_at_bol),
        NodeKind::Group => starts_at_bol(&n.subs[0]),
        NodeKind::Repeat => n.min >= 1 && starts_at_bol(&n.subs[0]),
        _ => false,
    }
}

/// Appends the literal text every match of `n` starts with.
fn literal_prefix(n: &Node, prefix: &mut String) {
    match n.kind {
        NodeKind::Char => {
            if literal_rune(n.r) {
                push_rune(prefix, n.r);
            }
        }
        NodeKind::Cat => {
            for sub in &n.subs {
                if sub.kind != NodeKind::Char || !literal_rune(sub.r) {
                    if prefix.is_empty() && sub.kind == NodeKind::Group {
                        literal_prefix(sub, prefix);
                    }
                    break;
                }
                push_rune(prefix, sub.r);
            }
        }
        NodeKind::Group => literal_prefix(&n.subs[0], prefix),
        _ => {}
    }
}

/// Appends, as inclusive pairs, the code points a match of `n` can start with. It returns whether `n` can match the
/// empty string, and `None` when the set is not known. Assertions are treated as matching the empty string, which
/// can only make the set larger than it need be.
fn first_chars(n: &Node, pairs: &mut Vec<Rune>) -> Option<bool> {
    match n.kind {
        NodeKind::Char => {
            pairs.push(n.r);
            pairs.push(n.r);
            Some(false)
        }
        NodeKind::Set => {
            pairs.extend_from_slice(&n.class().ranges);
            Some(false)
        }
        NodeKind::Cat => {
            for sub in &n.subs {
                if !first_chars(sub, pairs)? {
                    return Some(false);
                }
            }
            Some(true)
        }
        NodeKind::Alt => {
            let mut empty = false;
            for sub in &n.subs {
                empty |= first_chars(sub, pairs)?;
            }
            Some(empty)
        }
        NodeKind::Group => first_chars(&n.subs[0], pairs),
        NodeKind::Repeat => {
            if n.max == 0 {
                return Some(true);
            }
            let empty = first_chars(&n.subs[0], pairs)?;
            Some(empty || n.min == 0)
        }
        NodeKind::Backref => None,
        _ => Some(true),
    }
}
