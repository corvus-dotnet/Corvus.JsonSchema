//! The backtracking matcher (`vm.go`).

use std::cell::RefCell;
use std::time::{Duration, Instant};

use super::Rune;
use super::charset::is_word_byte;
use super::compile::{Inst, MAX_MEMO_LOOPS, Op, Program};
use super::fold::{canonicalize, is_fold_word_rune};
use super::units::{RUNE_SELF, decode, decode_last, decode_last_rune, decode_rune, unit_at, unit_before};

/// Why a match was abandoned. Neither is an answer.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum MatchError {
    /// The match ran for longer than its limit.
    Time,
    /// The match needed more backtracking memory than its limit: its stack reached [`MAX_STACK`] entries.
    Memory,
}

impl std::fmt::Display for MatchError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(match self {
            MatchError::Time => "ecmaregex: the match ran for longer than its limit",
            MatchError::Memory => "ecmaregex: the match needed more backtracking memory than its limit",
        })
    }
}

impl std::error::Error for MatchError {}

/// The number of steps the matcher takes between two looks at the clock and at the depth of its stack. A step is
/// one instruction, and eight characters of a single character loop or of a backreference. A step takes a few
/// nanoseconds, so the clock is read every ten to twenty microseconds, a match that ends before its first check
/// never reads it, and a match goes past its limit by that much at most.
pub(crate) const CHECK_EVERY: i64 = 1 << 12;
/// The number of entries the backtracking stack of a match with a limit may hold (24 bytes each). A match that
/// needs more is abandoned. A pattern that keeps a choice for every character of its text needs one entry or a few
/// for each, so this is room for a text of some hundred thousand characters, and a pattern that pushes without
/// consuming, such as `(?:a?){2147483647}`, reaches it in a few milliseconds. It is looked at with the clock, so
/// the stack can go past it by `CHECK_EVERY` entries.
pub const MAX_STACK: usize = 1 << 20;

// Why a match was abandoned.
const STOP_NONE: u8 = 0;
const STOP_TIME: u8 = 1;
const STOP_MEMORY: u8 = 2;

// The kinds of entry on the backtracking stack. A choice entry says where to resume. An undo entry restores a value
// that was overwritten after the entries below it were pushed.

/// Resumes at `pc` with the position `pos`.
const E_CHOICE: u8 = 0;
/// Belongs to a greedy single character loop that stopped at `pos`. Resuming gives back one character, down to the
/// position `a`.
const E_REP: u8 = 1;
/// Belongs to a lazy single character loop that has taken `a` characters and stands at `pos`. Resuming takes one
/// more.
const E_REP_LAZY: u8 = 2;
/// `E_LOOK` marks the start of a positive lookaround and `E_LOOK_NEG` of a negative one. Both hold the position to
/// restore, and `E_LOOK_NEG` the instruction to continue at when its body has failed.
const E_LOOK: u8 = 3;
const E_LOOK_NEG: u8 = 4;
/// Lies under the choices of a memo point. When it is reached, every way from the point has failed, and the bit
/// `pos` of the memory is set (see `memo` in [`State`]).
const E_MARK: u8 = 5;
/// Restores the entry position of group `pc` to `pos`.
const E_TMP: u8 = 6;
/// Restores the start and end of group `pc` to `pos` and `a`.
const E_CAP: u8 = 7;
/// Restores the count and the iteration start of loop `pc` to `pos` and `a`.
const E_COUNTER: u8 = 8;

#[derive(Clone, Copy)]
struct Entry {
    kind: u8,
    pc: i32,
    pos: usize,
    a: usize,
}

/// The value of a group that has not taken part, and of the start of an iteration that has not started.
const NONE: usize = usize::MAX;

/// The working memory of one match. Each thread keeps one and every match on the thread uses it, so a match
/// allocates only while the stack is still growing to the depth the pattern and the text need. (The Go matcher
/// pools one for each program. A thread matches one pattern at a time, so one for each thread serves, and the
/// memory a process keeps does not grow with the number of its patterns.)
#[derive(Default)]
struct State {
    stack: Vec<Entry>,
    /// `caps` holds the start and end of each tracked group, or `NONE`, and `tmp` the position at which each was
    /// entered.
    caps: Vec<usize>,
    tmp: Vec<usize>,
    /// For each loop, the number of iterations started and the position at which the last one started.
    counts: Vec<usize>,
    /// The time the match may take, or `None` for no limit. The clock starts at the first check, so `deadline` is
    /// set when `timed` is.
    limit: Option<Duration>,
    deadline: Option<Instant>,
    /// The number of steps left before the next check, and why the match was abandoned.
    steps: i64,
    stop: u8,
    /// The memory of the places where a way has failed: one bit for each memo point of the program, each position
    /// of the text and each state of the loops around the point (see `MemoPoint`). A bit is set when every way from
    /// the place has failed, and a way that comes to a place whose bit is set fails at once. So no place is gone
    /// through twice, and a pattern of repetitions inside repetitions takes time proportional to its size times the
    /// text and not time that doubles with each character.
    ///
    /// The memory costs its size to clear, and a mark on the stack for each place gone through, so a match starts
    /// without it. It takes it at the check by which it has made as many steps as the memory has words and
    /// `MEMO_STEPS_PER_POSITION` more for each position of the text, which a match that goes through its text once
    /// or a few times never makes. A place whose way failed before then is not remembered, which loses time and
    /// nothing else. `memo_on` says the memory is in use, `memo_off` that it never will be (the program has no memo
    /// points, or the memory would be larger than `MAX_MEMO_WORDS`), and `width` is the number of positions of the
    /// text.
    memo: Vec<u64>,
    memo_on: bool,
    memo_off: bool,
    width: usize,
    checks: usize,
    points: usize,
}

/// The largest memory of failed places, in words of 64 bits: 8 MB. A match that would need more goes without it.
pub const MAX_MEMO_WORDS: usize = 1 << 20;
/// The number of steps for each position of the text that a match makes before it takes the memory into use.
const MEMO_STEPS_PER_POSITION: usize = 64;
/// The largest memory kept between matches.
const MAX_RETAINED_MEMO: usize = 1 << 16;
/// The depth of the backtracking stack beyond which a memo point is not marked. A mark is an entry of the stack,
/// and a pattern that keeps no choice for a character still marks a place for each, so on a long text the marks
/// alone would reach `MAX_STACK`. A place that is not marked is not remembered, which loses time and nothing else,
/// and the places below this depth are remembered when the stack unwinds, so a later start has the room to mark the
/// next ones.
const MAX_MARK_DEPTH: usize = MAX_STACK / 2;
/// The largest stack kept between matches.
const MAX_RETAINED_STACK: usize = 1 << 16;

thread_local! {
    static STATE: RefCell<State> = RefCell::new(State::default());
    /// Makes every match on this thread take its memory of failed places before its first step. The tests set it,
    /// to run every pattern they have with the memory in use.
    #[cfg(test)]
    static MEMO_FROM_THE_START: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
}

/// Sets whether every match on this thread takes its memory of failed places before its first step.
#[cfg(test)]
pub(crate) fn set_memo_from_the_start(on: bool) {
    MEMO_FROM_THE_START.with(|m| m.set(on));
}

/// Whether every match takes its memory of failed places before its first step. A build with
/// `--cfg corvus_memo_from_the_start` does, which is how the test data is run with the memory in use for every
/// pattern.
#[inline(always)]
fn memo_from_the_start() -> bool {
    #[cfg(test)]
    if MEMO_FROM_THE_START.with(|m| m.get()) {
        return true;
    }
    cfg!(corvus_memo_from_the_start)
}

impl State {
    /// Takes the memory of failed places into use when the match has made enough steps to pay for it.
    fn remember(&mut self) {
        let words = (((self.points * self.width) << MAX_MEMO_LOOPS) + 63) >> 6;
        if words > MAX_MEMO_WORDS {
            self.memo_off = true;
            return;
        }
        if self.checks * (CHECK_EVERY as usize) < words + MEMO_STEPS_PER_POSITION * self.width && !memo_from_the_start()
        {
            return;
        }
        self.memo.clear();
        self.memo.resize(words, 0);
        self.memo_on = true;
    }

    /// Called every `CHECK_EVERY` steps. It reports whether the match may go on.
    #[cold]
    #[inline(never)]
    fn check(&mut self, depth: usize) -> bool {
        if !self.memo_on && !self.memo_off {
            self.checks += 1;
            self.remember();
        }
        let Some(limit) = self.limit else { return true };
        if depth > MAX_STACK {
            self.stop = STOP_MEMORY;
            return false;
        }
        let now = Instant::now();
        match self.deadline {
            None => self.deadline = Some(now + limit),
            Some(deadline) => {
                if now > deadline {
                    self.stop = STOP_TIME;
                    return false;
                }
            }
        }
        true
    }

    /// Applies one undo entry.
    #[inline(always)]
    fn undo(&mut self, e: Entry) {
        let k = e.pc as usize;
        match e.kind {
            E_TMP => self.tmp[k] = e.pos,
            E_CAP => (self.caps[2 * k], self.caps[2 * k + 1]) = (e.pos, e.a),
            E_COUNTER => (self.counts[2 * k], self.counts[2 * k + 1]) = (e.pos, e.a),
            _ => {}
        }
    }
}

impl Program {
    /// The bit of the memory for the memo point `m` at a position, with the state of the loops around the point:
    /// for each, whether it would refuse an iteration that ended here as empty.
    #[inline]
    fn memo_index(&self, st: &State, m: i32, pos: usize) -> usize {
        let pt = &self.points[(m - 1) as usize];
        let mut idx = ((m - 1) as usize * st.width + pos) << MAX_MEMO_LOOPS;
        for k in 0..MAX_MEMO_LOOPS {
            let l = pt.loops[k];
            if l >= 0 && st.counts[2 * l as usize] > pt.mins[k] as usize && st.counts[2 * l as usize + 1] == pos {
                idx |= 1 << k;
            }
        }
        idx
    }

    /// Reports whether the program matches anywhere in the text. With a limit the match is abandoned when it has
    /// run for longer than that, or when its stack has reached `MAX_STACK`, and the error says which.
    pub(crate) fn is_match(&self, text: &str, limit: Option<Duration>) -> Result<bool, MatchError> {
        STATE.with(|cell| match cell.try_borrow_mut() {
            Ok(mut st) => self.match_with(&mut st, text, limit),
            // The thread is already inside a match, which nothing in the crate does. A match is then given a
            // memory of its own.
            Err(_) => self.match_with(&mut State::default(), text, limit),
        })
    }

    fn match_with(&self, st: &mut State, text: &str, limit: Option<Duration>) -> Result<bool, MatchError> {
        st.caps.clear();
        st.caps.resize(2 * self.groups, NONE);
        st.tmp.clear();
        st.tmp.resize(self.groups, 0);
        st.counts.clear();
        st.counts.resize(2 * self.loops, 0);
        (st.limit, st.deadline, st.steps, st.stop) = (limit.filter(|l| !l.is_zero()), None, CHECK_EVERY, STOP_NONE);
        (st.memo_on, st.memo_off, st.checks) = (false, self.points.is_empty(), 0);
        (st.width, st.points) = (text.len() + 1, self.points.len());
        if !st.memo_off && memo_from_the_start() {
            st.remember();
        }
        let ok = self.search(st, text);
        let stop = st.stop;
        if st.stack.capacity() > MAX_RETAINED_STACK {
            st.stack = Vec::new();
        }
        if st.memo.capacity() > MAX_RETAINED_MEMO {
            st.memo = Vec::new();
        }
        match stop {
            STOP_TIME => Err(MatchError::Time),
            STOP_MEMORY => Err(MatchError::Memory),
            _ => Ok(ok),
        }
    }

    fn search(&self, st: &mut State, text: &str) -> bool {
        let input = text.as_bytes();
        if input.len() < self.need {
            return false;
        }
        if self.anchored {
            return self.run(st, input, 0);
        }
        if !self.prefix.is_empty() {
            let mut at = 0;
            loop {
                // The search for the literal takes time proportional to the text (the standard library searches for
                // a string in linear time). `at` is at the start of a character: a literal starts with one.
                let Some(i) = text[at..].find(&self.prefix) else { return false };
                at += i;
                if input.len() - at < self.need {
                    return false;
                }
                if self.run(st, input, at) {
                    return true;
                }
                if st.stop != STOP_NONE {
                    return false;
                }
                at += 1;
                while !text.is_char_boundary(at) {
                    at += 1;
                }
            }
        }
        let mut at = 0;
        loop {
            if input.len() - at < self.need {
                return false;
            }
            if at == input.len() {
                return self.first.is_none() && self.run(st, input, at);
            }
            let (mut r, mut w) = (input[at] as Rune, 1);
            if input[at] >= RUNE_SELF {
                (r, w) = if self.units { unit_at(input, at) } else { decode_rune(&input[at..]) };
            }
            if self.first.as_ref().is_none_or(|first| first.contains(r)) && self.run(st, input, at) {
                return true;
            }
            if st.stop != STOP_NONE {
                return false;
            }
            at += w;
        }
    }

    /// Reports whether the program matches at the position `start`. It keeps its choices on an explicit stack, so
    /// the depth of the search is bounded by memory and not by the stack of the thread. When it fails, every group
    /// and counter is back to the value it had on entry. It counts its steps, and when a check says the match has
    /// reached a limit it returns false with the reason in `st.stop`.
    fn run(&self, st: &mut State, input: &[u8], start: usize) -> bool {
        let mut stack = std::mem::take(&mut st.stack);
        stack.clear();
        let matched = self.run_on(st, &mut stack, input, start);
        st.stack = stack;
        matched
    }

    fn run_on(&self, st: &mut State, stack: &mut Vec<Entry>, input: &[u8], start: usize) -> bool {
        let insts = &self.insts[..];
        let quick = &self.quick[..];
        // The text is matched by UTF-16 code unit (see `units`).
        let units = self.units;
        let len = input.len();
        let (mut pc, mut pos) = (0usize, start);
        let mut steps = st.steps;
        // Whether the match has taken the memory of failed places into use.
        let mut memo = st.memo_on;
        'run: loop {
            steps -= 1;
            if steps < 0 {
                if !st.check(stack.len()) {
                    st.steps = 0;
                    return false;
                }
                steps = CHECK_EVERY;
                memo = st.memo_on;
            }
            let i = &insts[pc];
            match i.op {
                Op::Char => {
                    if pos < len {
                        let c = input[pos];
                        if c < RUNE_SELF {
                            if c as Rune == i.r {
                                pos += 1;
                                pc += 1;
                                continue 'run;
                            }
                        } else {
                            let (r, w) = if units { unit_at(input, pos) } else { decode_rune(&input[pos..]) };
                            if r == i.r {
                                pos += w;
                                pc += 1;
                                continue 'run;
                            }
                        }
                    }
                }
                Op::Set => {
                    if pos < len {
                        let set = set_of(i);
                        let c = input[pos];
                        if c < RUNE_SELF {
                            if set.has_ascii(c) {
                                pos += 1;
                                pc += 1;
                                continue 'run;
                            }
                        } else {
                            let (r, w) = if units { unit_at(input, pos) } else { decode_rune(&input[pos..]) };
                            if set.contains(r) {
                                pos += w;
                                pc += 1;
                                continue 'run;
                            }
                        }
                    }
                }
                Op::Lit => {
                    let n = i.lit.len();
                    if len - pos >= n && input[pos..pos + n] == *i.lit {
                        pos += n;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::CharBack | Op::SetBack => {
                    if pos > 0 {
                        let (mut r, mut w) = (input[pos - 1] as Rune, 1);
                        if input[pos - 1] >= RUNE_SELF {
                            (r, w) = if units { unit_before(input, pos) } else { decode_last_rune(&input[..pos]) };
                        }
                        if if i.op == Op::CharBack { r == i.r } else { set_of(i).contains(r) } {
                            pos -= w;
                            pc += 1;
                            continue 'run;
                        }
                    }
                }
                Op::Fail => {}
                Op::Bol => {
                    if pos == 0 {
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::Eol => {
                    if pos == len {
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::WordBoundary | Op::NotWordBoundary => {
                    let before = pos > 0 && is_word_byte(input[pos - 1]);
                    let after = pos < len && is_word_byte(input[pos]);
                    if (before != after) == (i.op == Op::WordBoundary) {
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::LineStart => {
                    if pos == 0 || line_terminator_before(input, pos) {
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::LineEnd => {
                    if pos == len || line_terminator_at(input, pos) {
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::WordBoundaryFold | Op::NotWordBoundaryFold => {
                    let before = pos > 0 && is_fold_word_rune(decode_last_rune(&input[..pos]).0);
                    let after = pos < len && is_fold_word_rune(decode_rune(&input[pos..]).0);
                    if (before != after) == (i.op == Op::WordBoundaryFold) {
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::Jmp => {
                    pc = i.x as usize;
                    continue 'run;
                }
                Op::Split => 'split: {
                    // A place from which every way has failed before fails at once. The mark is under whatever
                    // comes of this place, a choice or not, so that the place is remembered when it has all failed.
                    if memo && i.m != 0 {
                        let idx = self.memo_index(st, i.m, pos);
                        if (st.memo[idx >> 6] >> (idx & 63)) & 1 != 0 {
                            break 'split;
                        }
                        if stack.len() < MAX_MARK_DEPTH {
                            stack.push(Entry { kind: E_MARK, pc: 0, pos: idx, a: 0 });
                        }
                    }
                    // A way the next character rules out is not taken, and no choice is kept for it.
                    if !quick[i.y as usize].allows(input, pos) {
                        if !quick[i.x as usize].allows(input, pos) {
                            break 'split;
                        }
                        pc = i.x as usize;
                        continue 'run;
                    }
                    if !quick[i.x as usize].allows(input, pos) {
                        pc = i.y as usize;
                        continue 'run;
                    }
                    stack.push(Entry { kind: E_CHOICE, pc: i.y, pos, a: 0 });
                    pc = i.x as usize;
                    continue 'run;
                }
                Op::SaveTmp => {
                    let a = i.a as usize;
                    stack.push(Entry { kind: E_TMP, pc: i.a, pos: st.tmp[a], a: 0 });
                    st.tmp[a] = pos;
                    pc += 1;
                    continue 'run;
                }
                Op::Capture => {
                    let a = i.a as usize;
                    stack.push(Entry { kind: E_CAP, pc: i.a, pos: st.caps[2 * a], a: st.caps[2 * a + 1] });
                    if i.flag {
                        (st.caps[2 * a], st.caps[2 * a + 1]) = (pos, st.tmp[a]);
                    } else {
                        (st.caps[2 * a], st.caps[2 * a + 1]) = (st.tmp[a], pos);
                    }
                    pc += 1;
                    continue 'run;
                }
                Op::Clear => {
                    steps -= (i.b - i.a) as i64;
                    for g in i.a..=i.b {
                        let k = g as usize;
                        if st.caps[2 * k] != NONE {
                            stack.push(Entry { kind: E_CAP, pc: g, pos: st.caps[2 * k], a: st.caps[2 * k + 1] });
                            (st.caps[2 * k], st.caps[2 * k + 1]) = (NONE, NONE);
                        }
                    }
                    pc += 1;
                    continue 'run;
                }
                Op::Backref => {
                    // A group that has not taken part matches the empty string.
                    let a = i.a as usize;
                    let (from, to) = (st.caps[2 * a], st.caps[2 * a + 1]);
                    if from == NONE {
                        pc += 1;
                        continue 'run;
                    }
                    let n = to - from;
                    if len - pos >= n && input[pos..pos + n] == input[from..to] {
                        steps -= (n >> 3) as i64;
                        pos += n;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::BackrefBack => {
                    let a = i.a as usize;
                    let (from, to) = (st.caps[2 * a], st.caps[2 * a + 1]);
                    if from == NONE {
                        pc += 1;
                        continue 'run;
                    }
                    let n = to - from;
                    if pos >= n && input[pos - n..pos] == input[from..to] {
                        steps -= (n >> 3) as i64;
                        pos -= n;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::BackrefFold | Op::BackrefUnits => {
                    // A character at a time: to fold case, or because a group matched by code unit can begin or end
                    // between the halves of a character, where its bytes do not say what it holds.
                    let a = i.a as usize;
                    let (mut from, to) = (st.caps[2 * a], st.caps[2 * a + 1]);
                    if from == NONE {
                        pc += 1;
                        continue 'run;
                    }
                    steps -= ((to - from) >> 3) as i64;
                    let (mut at, mut ok) = (pos, true);
                    while from < to && ok {
                        let (want, w) = decode(input, from, units);
                        from += w;
                        if at >= len {
                            ok = false;
                            break;
                        }
                        let (got, w) = decode(input, at, units);
                        at += w;
                        ok = got == want
                            || i.op == Op::BackrefFold && canonicalize(got, i.flag) == canonicalize(want, i.flag);
                    }
                    if ok {
                        pos = at;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::BackrefFoldBack | Op::BackrefUnitsBack => {
                    let a = i.a as usize;
                    let (from, mut to) = (st.caps[2 * a], st.caps[2 * a + 1]);
                    if from == NONE {
                        pc += 1;
                        continue 'run;
                    }
                    steps -= ((to - from) >> 3) as i64;
                    let (mut at, mut ok) = (pos, true);
                    while from < to && ok {
                        let (want, w) = decode_last(input, to, units);
                        to -= w;
                        if at == 0 {
                            ok = false;
                            break;
                        }
                        let (got, w) = decode_last(input, at, units);
                        at -= w;
                        ok = got == want
                            || i.op == Op::BackrefFoldBack && canonicalize(got, i.flag) == canonicalize(want, i.flag);
                    }
                    if ok {
                        pos = at;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::Look => {
                    // What follows a lookaround starts where the lookaround does, and so does the body of a
                    // lookahead.
                    if quick[pc].allows(input, pos) {
                        stack.push(Entry { kind: if i.flag { E_LOOK_NEG } else { E_LOOK }, pc: i.x, pos, a: 0 });
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::LookOk => {
                    // The body matched. A lookaround is atomic, so the choices made inside it are dropped. The undo
                    // entries stay, because the groups it set remain set until something below this point is
                    // retried.
                    let mut f = stack.len() - 1;
                    while stack[f].kind != E_LOOK {
                        f -= 1;
                    }
                    pos = stack[f].pos;
                    let mut keep = f;
                    for k in f + 1..stack.len() {
                        if stack[k].kind >= E_TMP {
                            stack[keep] = stack[k];
                            keep += 1;
                        }
                    }
                    stack.truncate(keep);
                    pc += 1;
                    continue 'run;
                }
                Op::LookFail => {
                    // The body of a negative lookaround matched, so the lookaround fails and everything it did is
                    // undone.
                    while let Some(e) = stack.pop() {
                        if e.kind == E_LOOK_NEG {
                            break;
                        }
                        st.undo(e);
                    }
                }
                Op::Rep => 'rep: {
                    let (mut at, mut n, mut floor) = (pos, 0i32, pos);
                    if let Some(set) = &i.set {
                        while n != i.max && at < len {
                            let c = input[at];
                            if c < RUNE_SELF {
                                if !set.has_ascii(c) {
                                    break;
                                }
                                at += 1;
                            } else {
                                let (r, w) = if units { unit_at(input, at) } else { decode_rune(&input[at..]) };
                                if !set.contains(r) {
                                    break;
                                }
                                at += w;
                            }
                            n += 1;
                            if n == i.min {
                                floor = at;
                            }
                        }
                    } else {
                        while n != i.max && at < len {
                            let (mut r, mut w) = (input[at] as Rune, 1);
                            if input[at] >= RUNE_SELF {
                                (r, w) = if units { unit_at(input, at) } else { decode_rune(&input[at..]) };
                            }
                            if r != i.r {
                                break;
                            }
                            at += w;
                            n += 1;
                            if n == i.min {
                                floor = at;
                            }
                        }
                    }
                    steps -= ((at - pos) >> 3) as i64;
                    if n >= i.min {
                        if memo && i.m != 0 {
                            let idx = self.memo_index(st, i.m, at);
                            if (st.memo[idx >> 6] >> (idx & 63)) & 1 != 0 {
                                // What follows the loop has failed from here before. The loop gives a character
                                // back.
                                if at > floor {
                                    stack.push(Entry { kind: E_REP, pc: (pc + 1) as i32, pos: at, a: floor });
                                }
                                break 'rep;
                            }
                            if at == floor && stack.len() < MAX_MARK_DEPTH {
                                stack.push(Entry { kind: E_MARK, pc: 0, pos: idx, a: 0 });
                            }
                        }
                        if at > floor {
                            stack.push(Entry { kind: E_REP, pc: (pc + 1) as i32, pos: at, a: floor });
                        }
                        pos = at;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::RepLazy => 'rep: {
                    let (mut at, mut n) = (pos, 0i32);
                    while n < i.min {
                        let w = one(i, input, at, units);
                        if w == 0 {
                            break;
                        }
                        at += w;
                        n += 1;
                    }
                    steps -= (n >> 3) as i64;
                    if n == i.min {
                        if memo && i.m != 0 {
                            let idx = self.memo_index(st, i.m, at);
                            if (st.memo[idx >> 6] >> (idx & 63)) & 1 != 0 {
                                // What follows the loop has failed from here before. The loop takes one more
                                // character.
                                if n != i.max {
                                    stack.push(Entry { kind: E_REP_LAZY, pc: pc as i32, pos: at, a: n as usize });
                                }
                                break 'rep;
                            }
                            if n == i.max && stack.len() < MAX_MARK_DEPTH {
                                stack.push(Entry { kind: E_MARK, pc: 0, pos: idx, a: 0 });
                            }
                        }
                        if n != i.max {
                            stack.push(Entry { kind: E_REP_LAZY, pc: pc as i32, pos: at, a: n as usize });
                        }
                        pos = at;
                        pc += 1;
                        continue 'run;
                    }
                }
                Op::LoopInit => {
                    let a = i.a as usize;
                    stack.push(Entry { kind: E_COUNTER, pc: i.a, pos: st.counts[2 * a], a: st.counts[2 * a + 1] });
                    (st.counts[2 * a], st.counts[2 * a + 1]) = (0, NONE);
                    pc += 1;
                    continue 'run;
                }
                Op::Loop => 'looping: {
                    let a = i.a as usize;
                    let n = st.counts[2 * a] as i64;
                    let (min, need, left) = (i.min as i64, i.need as i64, (len - pos) as i64);
                    // Fewer bytes are left than any way from here to a match consumes: what follows the loop, and
                    // the iterations it has still to make.
                    if left < need || n < min && i.iter != 0 && left - need < (min - n) * i.iter as i64 {
                        break 'looping;
                    }
                    // Once the minimum is met, ECMA-262 rejects an iteration that consumed nothing. That is what
                    // stops a loop over a body that can be empty.
                    if n > min && pos == st.counts[2 * a + 1] {
                        break 'looping;
                    }
                    if n < min {
                        pc += 1;
                        continue 'run;
                    }
                    if i.max >= 0 && n >= i.max as i64 {
                        pc = i.x as usize;
                        continue 'run;
                    }
                    // The loop may be left or entered from here. A place from which both have failed before fails
                    // at once.
                    if memo && i.m != 0 {
                        let idx = self.memo_index(st, i.m, pos);
                        if (st.memo[idx >> 6] >> (idx & 63)) & 1 != 0 {
                            break 'looping;
                        }
                        if stack.len() < MAX_MARK_DEPTH {
                            stack.push(Entry { kind: E_MARK, pc: 0, pos: idx, a: 0 });
                        }
                    }
                    if !quick[i.x as usize].allows(input, pos) {
                        // The next character rules out leaving the loop, so there is no choice.
                        pc += 1;
                    } else if !quick[pc + 1].allows(input, pos) {
                        pc = i.x as usize;
                    } else if i.flag {
                        stack.push(Entry { kind: E_CHOICE, pc: (pc + 1) as i32, pos, a: 0 });
                        pc = i.x as usize;
                    } else {
                        stack.push(Entry { kind: E_CHOICE, pc: i.x, pos, a: 0 });
                        pc += 1;
                    }
                    continue 'run;
                }
                Op::LoopIter => {
                    let a = i.a as usize;
                    stack.push(Entry { kind: E_COUNTER, pc: i.a, pos: st.counts[2 * a], a: st.counts[2 * a + 1] });
                    st.counts[2 * a] += 1;
                    st.counts[2 * a + 1] = pos;
                    pc += 1;
                    continue 'run;
                }
                Op::Match => {
                    st.steps = steps;
                    return true;
                }
            }
            // The instruction failed. Take up the most recent choice, undoing what was recorded since.
            loop {
                let Some(&e) = stack.last() else {
                    st.steps = steps;
                    return false;
                };
                let top = stack.len() - 1;
                match e.kind {
                    E_CHOICE => {
                        (pc, pos) = (e.pc as usize, e.pos);
                        stack.pop();
                        break;
                    }
                    E_REP => {
                        // What followed the loop from the position it stood at has failed.
                        let rep = &insts[(e.pc - 1) as usize];
                        let marked = memo && rep.m != 0;
                        if marked {
                            let idx = self.memo_index(st, rep.m, e.pos);
                            st.memo[idx >> 6] |= 1 << (idx & 63);
                        }
                        // Give back the last character taken. The characters between a and pos were decoded going
                        // forward, and decoding the last of them going backward finds the same boundary.
                        let mut w = 1;
                        if input[e.pos - 1] >= RUNE_SELF {
                            w = if units {
                                unit_before(input, e.pos).1
                            } else {
                                decode_last_rune(&input[e.a..e.pos]).1
                            };
                        }
                        let at = e.pos - w;
                        stack[top].pos = at;
                        (pc, pos) = (e.pc as usize, at);
                        let last = at <= e.a;
                        // A position from which what follows the loop cannot go on is passed over, and so is one
                        // from which it has failed before.
                        let mut pass = !quick[pc].allows(input, pos);
                        if marked && !pass {
                            let idx = self.memo_index(st, rep.m, pos);
                            if (st.memo[idx >> 6] >> (idx & 63)) & 1 != 0 {
                                pass = true;
                            } else if last {
                                // The loop has nothing more to give back. The entry stays to note this last way.
                                stack[top] = Entry { kind: E_MARK, pc: 0, pos: idx, a: 0 };
                                break;
                            }
                        }
                        if last {
                            stack.pop();
                        }
                        if pass {
                            steps -= 1;
                            continue;
                        }
                        break;
                    }
                    E_REP_LAZY => {
                        // What followed the loop from the position it stood at has failed.
                        let rep = &insts[e.pc as usize];
                        let marked = memo && rep.m != 0;
                        if marked {
                            let idx = self.memo_index(st, rep.m, e.pos);
                            st.memo[idx >> 6] |= 1 << (idx & 63);
                        }
                        let w = one(rep, input, e.pos, units);
                        if w == 0 {
                            stack.pop();
                            continue;
                        }
                        let (at, taken) = (e.pos + w, e.a + 1);
                        (stack[top].pos, stack[top].a) = (at, taken);
                        (pc, pos) = (e.pc as usize + 1, at);
                        let last = taken as i64 == rep.max as i64;
                        if marked {
                            let idx = self.memo_index(st, rep.m, pos);
                            if (st.memo[idx >> 6] >> (idx & 63)) & 1 != 0 {
                                if last {
                                    stack.pop();
                                }
                                steps -= 1;
                                continue;
                            }
                            if last {
                                stack[top] = Entry { kind: E_MARK, pc: 0, pos: idx, a: 0 };
                                break;
                            }
                        }
                        if last {
                            stack.pop();
                        }
                        break;
                    }
                    E_MARK => {
                        // Every way from a memo point has failed.
                        st.memo[e.pos >> 6] |= 1 << (e.pos & 63);
                        stack.pop();
                    }
                    E_LOOK => {
                        // The body of a positive lookaround has no way left to match.
                        stack.pop();
                    }
                    E_LOOK_NEG => {
                        // The body of a negative lookaround has no way to match, so the lookaround holds.
                        (pc, pos) = (e.pc as usize, e.pos);
                        stack.pop();
                        break;
                    }
                    _ => {
                        st.undo(e);
                        stack.pop();
                    }
                }
            }
        }
    }
}

#[inline(always)]
fn set_of(i: &Inst) -> &super::charset::CharSet {
    i.set.as_deref().expect("a set instruction has a class")
}

/// Whether the character before the position is an ECMA-262 line terminator, and `line_terminator_at` whether the
/// character at the position is one. U+2028 and U+2029 are E2 80 A8 and E2 80 A9.
fn line_terminator_before(input: &[u8], pos: usize) -> bool {
    match input[pos - 1] {
        b'\n' | b'\r' => true,
        0xA8 | 0xA9 => pos >= 3 && input[pos - 3] == 0xE2 && input[pos - 2] == 0x80,
        _ => false,
    }
}

fn line_terminator_at(input: &[u8], pos: usize) -> bool {
    match input[pos] {
        b'\n' | b'\r' => true,
        0xE2 => pos + 2 < input.len() && input[pos + 1] == 0x80 && (input[pos + 2] == 0xA8 || input[pos + 2] == 0xA9),
        _ => false,
    }
}

/// The width of the character at the position `at` when the instruction's character or class matches it, and 0
/// otherwise. The character is a code unit with `units` set.
#[inline]
fn one(i: &Inst, input: &[u8], at: usize, units: bool) -> usize {
    if at >= input.len() {
        return 0;
    }
    let (mut r, mut w) = (input[at] as Rune, 1);
    if input[at] >= RUNE_SELF {
        (r, w) = if units { unit_at(input, at) } else { decode_rune(&input[at..]) };
    }
    match &i.set {
        Some(set) => {
            if !set.contains(r) {
                return 0;
            }
        }
        None => {
            if r != i.r {
                return 0;
            }
        }
    }
    w
}
