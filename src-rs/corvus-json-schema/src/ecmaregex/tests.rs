//! The tests of the engine, ported from the tests of the Go package. The answers they expect are V8's, in the files
//! of the Go package's `testdata` directory, which the Object Pascal and OCaml ports read too. So the tests run from
//! a checkout of the repository, as the rest of the crate's tests do.

use std::collections::{BTreeSet, HashMap};
use std::path::{Path, PathBuf};
use std::time::{Duration, Instant};

use serde_json::Value;

use super::charset::{CharSet, property_set};
use super::compile::{MAX_MEMO_LOOPS, NO_MATCH, Op, Program, compile_program, needs, quicks};
use super::fold::canonicalize;
use super::parse::{self, Limit, MAX_NESTING, MAX_PATTERN_LENGTH, MAX_SIZE};
use super::units::{has_supplementary, unit_at, unit_before};
use super::vm::{MatchError, set_memo_from_the_start};
use super::{Error, Regexp, Rune, check, compile, ucd, valid};

const NODE_SIZE: usize = 32;
const RANGE_SIZE: usize = 8;

/// A time no test should come near, for the bounds that must not depend on a quiet machine.
const GENEROUS: Duration = Duration::from_secs(20);

fn go_file(name: &str) -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("../../src-go/corvus-json-schema/internal").join(name)
}

fn testdata(name: &str) -> Value {
    let path = go_file("ecmaregex/testdata").join(name);
    let text = std::fs::read_to_string(&path).unwrap_or_else(|e| panic!("{}: {e}", path.display()));
    serde_json::from_str(&text).unwrap_or_else(|e| panic!("{}: {e}", path.display()))
}

/// One pattern of `v8_cases.json` with what V8 says of it: whether it is valid with the u flag and with no flag,
/// and whether it matches each text. `gen_cases.js` of the Go package writes the file. Where V8 departs from
/// ECMA-262 the answers are ECMA-262's, and `ecma262` names the departure.
struct V8Case {
    p: String,
    u: bool,
    l: bool,
    ecma262: String,
    tests: Vec<(String, bool)>,
}

fn cases(set: &str) -> Vec<V8Case> {
    let all = testdata("v8_cases.json");
    all[set]
        .as_array()
        .unwrap_or_else(|| panic!("v8_cases.json has no set {set}"))
        .iter()
        .map(|c| V8Case {
            p: c["p"].as_str().unwrap().to_string(),
            u: c["u"].as_bool().unwrap(),
            l: c["l"].as_bool().unwrap(),
            ecma262: c["ecma262"].as_str().unwrap_or("").to_string(),
            tests: c["tests"]
                .as_array()
                .unwrap()
                .iter()
                .map(|t| (t["t"].as_str().unwrap().to_string(), t["want"].as_bool().unwrap()))
                .collect(),
        })
        .collect()
}

/// Compares the validity of a pattern with V8's. It returns the pattern compiled, or `None` when the pattern is
/// not valid.
fn check_verdict(p: &str, valid_unicode: bool, valid_legacy: bool) -> Option<Regexp> {
    assert_eq!(valid(p), valid_unicode, "valid({p:?})");
    let re = compile(p);
    assert_eq!(re.is_ok(), valid_unicode || valid_legacy, "compile({p:?}): {:?}", re.as_ref().err());
    let re = re.ok()?;
    assert_eq!(re.unicode(), valid_unicode, "compile({p:?}).unicode()");
    assert_eq!(check(p).map(|facts| (facts.unicode, facts.size)), Ok((valid_unicode, re.size())), "check({p:?})");
    assert_eq!(re.as_str(), p);
    Some(re)
}

/// Compares the answers of the pattern with V8's on one text: through the automaton when the pattern has one, on
/// the backtracking matcher alone, and with a limit of a nanosecond, where a match that reaches its first check is
/// abandoned and says nothing. A match has `limit`, and one that does not answer within it fails the test.
fn check_answer(re: &Regexp, text: &str, want: bool, limit: Duration) {
    let p = re.as_str();
    assert_eq!(re.match_limit(text, Some(limit)), Ok(want), "{p:?} on {text:?}");
    assert_eq!(re.match_backtrack(text, Some(limit)), Ok(want), "{p:?} on {text:?} (the backtracking matcher)");
    if let Ok(got) = re.match_limit(text, Some(Duration::from_nanos(1))) {
        assert_eq!(got, want, "{p:?} on {text:?} (with a limit)");
    }
}

struct Counts {
    answers: usize,
    matched: usize,
    legacy: usize,
    invalid: usize,
}

/// Compares the validity of each pattern and its answers with V8's. A pattern has `limit` to answer all its texts.
fn check_cases(cases: &[V8Case], limit: Duration) -> Counts {
    let mut counts = Counts { answers: 0, matched: 0, legacy: 0, invalid: 0 };
    for c in cases {
        let Some(re) = check_verdict(&c.p, c.u, c.l) else {
            counts.invalid += 1;
            continue;
        };
        if !c.u {
            counts.legacy += 1;
        }
        let start = Instant::now();
        for (text, want) in &c.tests {
            check_answer(&re, text, *want, limit);
            counts.answers += 1;
            counts.matched += usize::from(*want);
        }
        assert!(start.elapsed() < limit, "{:?} did not answer its {} texts in {limit:?}", c.p, c.tests.len());
    }
    counts
}

/// Runs a test that reads patterns nested as deeply as the limits allow, and walks their trees, on a thread with
/// the stack for it. `compile`, `check` and `valid` see to that themselves. The parser and the compiler, which
/// these tests call directly, do not.
fn deep(test: impl FnOnce() + Send + 'static) {
    std::thread::Builder::new().stack_size(64 << 20).spawn(test).unwrap().join().unwrap();
}

/// The start of a pattern, for a message about a long one.
fn short(pattern: &str) -> String {
    pattern.chars().take(30).collect()
}

fn parse_u(pattern: &str) -> parse::Node {
    parse::parse(pattern, true).unwrap_or_else(|e| panic!("parse({}): {e:?}", short(pattern))).0
}

fn program(pattern: &str) -> Program {
    compile_program(parse_u(pattern), true)
}

/// Matching by UTF-16 code unit, against V8. A pattern that is valid only with no flag is matched a code unit at a
/// time. A character beyond the Basic Multilingual Plane is two units to the dot, to a class, to a quantifier, to a
/// group and to a backreference, a match can start and end between them, and such a character in the pattern is two
/// atoms. A class range between two of them is an error in both grammars.
#[test]
fn code_units() {
    let units = cases("units");
    let c = check_cases(&units, Duration::from_secs(60));
    assert!(c.legacy >= 90 && c.invalid >= 3 && c.matched >= 500, "the cases are not the ones this test is about");
}

/// Checks one of the sets of what is a pattern: which patterns are valid, in each grammar, and what the valid ones
/// match. The expectations are V8's, and ECMA-262's where V8 13.6 departs from it. It returns the number of
/// patterns that are not valid, and the number marked for each departure.
fn check_syntax(set: &str) -> (usize, usize, HashMap<String, usize>) {
    let cases = cases(set);
    let c = check_cases(&cases, Duration::from_secs(10));
    let mut departures = HashMap::new();
    for case in &cases {
        if !case.ecma262.is_empty() {
            *departures.entry(case.ecma262.clone()).or_insert(0) += 1;
        }
    }
    (cases.len(), c.invalid, departures)
}

/// `\k` in a class. In a pattern with no named group and no u flag it is the letter k. In a pattern with a named
/// group it is an error, in a class as outside one.
#[test]
fn escaped_k_in_a_class() {
    let (patterns, invalid, _) = check_syntax("classK");
    assert!(patterns >= 30 && invalid >= 9);
}

/// The two counts of a quantifier. A first count greater than the second is an error whatever the size of the
/// numbers. V8 13.6 keeps a count as a number that stops at 2^31 - 1 and so takes `a{2147483648,2147483647}`. Those
/// cases have ECMA-262's answer.
#[test]
fn counts_are_compared_as_written() {
    let (_, invalid, departures) = check_syntax("counts");
    assert!(invalid >= 20 && departures.get("count-order").copied().unwrap_or(0) >= 10);
}

/// The names and values of `\p{...}` where V8 13.6 departs from ECMA-262. WSpace is not a name ECMA-262 lists.
/// Katakana_Or_Hiragana (Hrkt) is a Script value, which no character has.
#[test]
fn property_names_and_values() {
    let (_, _, departures) = check_syntax("properties");
    assert!(departures.get("property-name").copied().unwrap_or(0) >= 3);
    assert!(departures.get("script-value").copied().unwrap_or(0) >= 20);
}

/// A name on two groups. It is valid only when the groups are in separate alternatives of one disjunction, so that
/// no match goes through both. V8 13.6 takes some patterns where both can take part. Those cases have ECMA-262's
/// answer.
#[test]
fn a_group_name_used_twice() {
    let (_, invalid, departures) = check_syntax("names");
    assert!(invalid >= 15 && departures.get("duplicate-name").copied().unwrap_or(0) >= 4);
}

/// Repetitions whose body can match only the empty string, and backreferences inside the group they name, against
/// V8. Run as written, the first patterns of the set take time exponential in their counts.
#[test]
fn repetitions_that_cannot_consume() {
    check_cases(&cases("loops"), Duration::from_secs(60));
}

/// The two rewrites of `simplify`, by the counted loops and the backreferences of the program.
#[test]
fn simplify() {
    for (pattern, loops, backrefs) in [
        (r"(?:\b|(?=a)|){4,5}", 0, 0),
        (r"(?:(?=(a))|){2}\1", 0, 1),
        (r"(?:(?=a))*b", 0, 0),
        (r"(?:a|(?=b)){2,3}", 1, 0),
        (r"(a\1)+", 0, 0),
        (r"(?:(a)|b\1){2}", 1, 1),
        (r"((\1|\2|)){3}", 0, 0),
        (r"(?<n>a\k<n>)", 0, 0),
        (r"(?:(?<n>a)|(?<n>b\k<n>)){2}", 1, 2),
        (r"(?:(\1){3}|(?!a)){2,}(?<=b)", 0, 0),
        (r"(?:\1(a)){2}", 1, 1),
    ] {
        let p = program(pattern);
        let found = p.insts.iter().filter(|i| i.op == Op::Backref).count();
        assert_eq!((p.loops, found), (loops, backrefs), "{pattern:?}");
    }
}

/// Counted loops whose iterations can be empty, before a rest of the pattern that needs more text than is left,
/// against V8. Without the needs, the first three patterns of the set do not finish.
#[test]
fn loops_give_up_when_the_rest_cannot_fit() {
    let needs = cases("needs");
    let c = check_cases(&needs, Duration::from_secs(60));
    assert!(needs.len() >= 20 && c.matched >= 100);
}

/// The least number of bytes a match consumes, which `set_needs` works out for a program and for each of its
/// counted loops, and the least one iteration of each loop consumes.
#[test]
fn needs_of_a_program() {
    const NONE: i64 = NO_MATCH;
    let table: &[(&str, i64, &[i64], &[i64])] = &[
        (r"abc", 3, &[], &[]),
        (r"a+b{2,}c?", 3, &[], &[]),
        (r"(?:a|bc)d", 2, &[], &[]),
        (r"(?=abc)a", 1, &[], &[]),
        (r"(?<=abc)d", 1, &[], &[]),
        (r"\u00e9{2}\u{10400}", 3, &[], &[]),
        (r"(?:a|){3}xy$", 2, &[2], &[0]),
        (r"(?:a|){3}", 0, &[0], &[0]),
        (r"(?:(?:a|){2}b){2}c", 3, &[1, 2], &[1, 0]),
        (r"(?=(?:a|){2}xyz)(?:b|){2}c", 1, &[3, 1], &[0, 0]),
        (r"(?<=(?:ab|){2}c)(?:d|){2}ef", 2, &[0, 2], &[0, 0]),
        (r"(?!(?:a|){2}xx)(?:(x)|){2}\1y", 1, &[2, 1], &[0, 0]),
        (r"(?:a|){2}[]", NONE, &[NONE], &[0]),
        (r"(?:(?:a|){2}|bcd)e", 1, &[1], &[0]),
        // The least count of a loop times the least of its body.
        (r"(?:a|b){5}c", 6, &[1], &[1]),
        (r"(?:ab|cde){3,}?x", 7, &[1], &[2]),
        (r"(?:a{1000}){1000}", 1000000, &[0], &[1000]),
        (r"(?:\u00e9|xyz){2}", 4, &[0], &[2]),
        (r"(?:a|b){2147483647}c", NONE, &[1], &[1]),
        (r"(?:(?:a|bc){2}d){3}", 9, &[0, 1], &[3, 1]),
        // A loop that cannot reach a match, with a count times a body over the cap: the sum of the two is capped
        // too, and does not wrap to a negative need.
        (r"(?:ab){2147483647}[]", NONE, &[NONE], &[2]),
        (r"(?:(?:ab){2147483647}[]|c)d", 2, &[NONE], &[2]),
        // The body of a lookbehind is matched backwards and counts nothing.
        (r"(?<=(?:ab|c){2}d)e", 1, &[0], &[0]),
        (r"(?:a|(?=(?:b|c){3}))x{2}", 2, &[0], &[1]),
    ];
    for &(pattern, need, loops, iters) in table {
        let p = program(pattern);
        let found: Vec<i64> = p.insts.iter().filter(|i| i.op == Op::Loop).map(|i| i.need as i64).collect();
        let found_iters: Vec<i64> = p.insts.iter().filter(|i| i.op == Op::Loop).map(|i| i.iter as i64).collect();
        assert_eq!((p.need as i64, &found[..], &found_iters[..]), (need, loops, iters), "{pattern:?}");
    }
}

/// The matcher uses the least count of a counted loop: a pattern that needs a million characters, or two thousand
/// million, answers a text of a hundred thousand at once. The limit of the match here is a small part of what the
/// work would take, so a match that does the work is abandoned and the test fails on its error.
#[test]
fn a_loop_gives_up_when_its_iterations_cannot_fit() {
    let text = "a".repeat(100000);
    for pattern in
        [r"(?:a{1000}){1000}", r"(?:a|b){2147483647}c", r"(?=.)(?:(?:ab|a){500}){500}", r"x?(?:[ab]c?){100001}$"]
    {
        let re = compile(pattern).unwrap();
        assert_eq!(re.match_backtrack(&text, Some(Duration::from_millis(200))), Ok(false), "{pattern:?}");
    }
    // A text that is long enough still matches.
    let re = compile(r"^(?:a{10}){100}$").unwrap();
    assert_eq!(re.match_backtrack(&text[..1000], Some(Duration::from_secs(60))), Ok(true));
    assert_eq!(re.match_backtrack(&text[..999], Some(Duration::from_secs(60))), Ok(false));
}

/// The two readers of a code unit on every position of a text, forwards and backwards.
#[test]
fn code_unit_positions() {
    let text = "a\u{e9}\u{20ac}\u{10400}\u{10FFFF}z".as_bytes();
    let want: [Rune; 8] = [0x61, 0xE9, 0x20AC, 0xD801, 0xDC00, 0xDBFF, 0xDFFF, 0x7A];
    let mut ends = Vec::new();
    let mut pos = 0;
    for unit in want {
        let (got, w) = unit_at(text, pos);
        assert_eq!(got, unit, "unit_at({pos})");
        pos += w;
        ends.push(pos);
    }
    assert_eq!(pos, text.len());
    for k in (0..want.len()).rev() {
        let (got, w) = unit_before(text, ends[k]);
        assert_eq!(got, want[k], "unit_before({})", ends[k]);
        if k > 0 {
            assert_eq!(ends[k] - w, ends[k - 1], "unit_before({}) spans {w} bytes", ends[k]);
        }
    }
    assert!(has_supplementary(text) && !has_supplementary("a\u{e9}\u{20ac}\u{ffff}".as_bytes()));
    // Text that is not UTF-8 is read a byte at a time, as it is when matching by code point.
    for bad in [&b"\xf0\x9f"[..], b"\xf0\x9f\x98", b"\x80\x80\x80", b"\xf0a\x80\x80", b"\xf8\x88\x80\x80\x80"] {
        let mut pos = 0;
        while pos < bad.len() {
            let (r, w) = unit_at(bad, pos);
            assert!(w == 1 && (r == 0xFFFD || r == bad[pos] as Rune), "unit_at({bad:?}, {pos}) = {r:#x}, {w}");
            pos += w;
        }
        let mut pos = bad.len();
        while pos > 0 {
            let (r, w) = unit_before(bad, pos);
            assert!(w == 1 && (r == 0xFFFD || r == bad[pos - 1] as Rune), "unit_before({bad:?}, {pos}) = {r:#x}, {w}");
            pos -= w;
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The oracle: what V8 answers for a set of patterns and texts. gen_oracle.js of the Go package writes it.

struct Oracle {
    inputs: Vec<String>,
    short_inputs: Vec<String>,
    verdicts: String,
    random_matches: Vec<String>,
    /// The pattern, whether it is valid with the u flag and with no flag, and one bit per input, set for a match,
    /// written as hexadecimal digits of four bits each.
    hand: Vec<(String, bool, bool, String)>,
    /// The pattern, the flags it was matched with (out of i, m and s), whether it is valid with the u flag, and
    /// the bits.
    flagged: Vec<(String, String, bool, String)>,
}

fn oracle() -> Oracle {
    let o = testdata("v8_oracle.json");
    let strings =
        |v: &Value| -> Vec<String> { v.as_array().unwrap().iter().map(|s| s.as_str().unwrap().into()).collect() };
    // A pattern that is not valid has no answers.
    let text = |v: &Value| v.as_str().unwrap_or("").to_string();
    Oracle {
        inputs: strings(&o["inputs"]),
        short_inputs: strings(&o["shortInputs"]),
        verdicts: text(&o["verdicts"]),
        random_matches: strings(&o["randomMatches"]),
        hand: o["hand"]
            .as_array()
            .unwrap()
            .iter()
            .map(|h| (text(&h["p"]), h["u"].as_bool().unwrap(), h["l"].as_bool().unwrap(), text(&h["m"])))
            .collect(),
        flagged: o["flagged"]
            .as_array()
            .unwrap()
            .iter()
            .map(|f| (text(&f["p"]), text(&f["f"]), f["u"].as_bool().unwrap(), text(&f["m"])))
            .collect(),
    }
}

/// Bit `k` of a string of the oracle's bits.
fn bit(hex: &str, k: usize) -> bool {
    let digit = (hex.as_bytes()[k / 4] as char).to_digit(16).unwrap();
    (digit >> (3 - k % 4)) & 1 == 1
}

/// The patterns for which V8 13.6 answers a case-insensitive modifier group differently from the same pattern with
/// the i flag. ECMA-262 gives the two the same meaning. Only the validity of these patterns is taken from the
/// oracle. `oracle_flags` checks the behaviour of each through the i flag, where V8 agrees with the engine.
const V8_MODIFIER_BUGS: &[&str] =
    &[r"^(?i:\u212a)$", r"^(?i:\u017f)$", r"^(?i:\u1e9e)$", r"^(?i:\u03bc)$", r"^(?i:\u03bc)\&?$", r"(?i:\Bs)"];

/// Compares the answers of the pattern with the oracle's on every text, the texts with a character beyond the Basic
/// Multilingual Plane among them. It returns the number of answers compared.
fn check_answers(re: &Regexp, inputs: &[String], bits: &str) -> usize {
    for (i, s) in inputs.iter().enumerate() {
        check_answer(re, s, bit(bits, i), GENEROUS);
    }
    inputs.len()
}

/// The hand-written patterns (the Java port's validator cases, the Rust port's pattern cases and the Go package's
/// own) against V8, for validity and for the answer on every text.
#[test]
fn oracle_hand_written() {
    let o = oracle();
    let mut answers = 0;
    for (p, u, l, m) in &o.hand {
        let Some(re) = check_verdict(p, *u, *l) else { continue };
        if !V8_MODIFIER_BUGS.contains(&p.as_str()) {
            answers += check_answers(&re, &o.inputs, m);
        }
    }
    assert!(o.hand.len() > 300 && answers > 50000, "{} patterns, {answers} answers", o.hand.len());
}

/// Case-insensitive, multiline and dot-all matching. V8 matched each pattern with flags set for the whole pattern,
/// and here the pattern is wrapped in the modifier group that means the same.
#[test]
fn oracle_flags() {
    let o = oracle();
    let mut answers = 0;
    for (p, f, u, m) in &o.flagged {
        let p = format!("(?{f}:{p})");
        if let Some(re) = check_verdict(&p, *u, !*u) {
            answers += check_answers(&re, &o.inputs, m);
        }
    }
    assert!(answers > 5000, "{answers} answers");
}

/// The generator `gen_oracle.js` uses, so the test builds the patterns the oracle's verdicts are about.
struct Xorshift(u32);

impl Xorshift {
    fn next(&mut self) -> u32 {
        let mut s = self.0;
        s ^= s << 13;
        s ^= s >> 17;
        s ^= s << 5;
        self.0 = s;
        s
    }
}

/// Random patterns over an alphabet of syntax characters (the Java port's generated test). Each one's validity is
/// checked against V8, and the first few thousand are also matched.
#[test]
fn oracle_generated() {
    let o = oracle();
    let alphabet = br"ab0\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L";
    let mut seed = Xorshift(0x9e3779b9);
    let mut counts = [0; 3];
    let mut answers = 0;
    for (i, verdict) in o.verdicts.bytes().enumerate() {
        let n = 1 + seed.next() % 8;
        let p: String = (0..n).map(|_| alphabet[(seed.next() % alphabet.len() as u32) as usize] as char).collect();
        counts[(verdict - b'0') as usize] += 1;
        let Some(re) = check_verdict(&p, verdict == b'1', verdict == b'2') else { continue };
        if let Some(bits) = o.random_matches.get(i) {
            answers += check_answers(&re, &o.short_inputs, bits);
        }
    }
    assert!(counts.iter().all(|&c| c > 100) && answers > 10000, "{counts:?}, {answers} answers");
}

// ---------------------------------------------------------------------------------------------------------------------
// The size of a pattern.

/// The size the parser counts for a pattern under one grammar, or -1 when the grammar does not accept it.
fn size_of(pattern: &str, unicode: bool) -> i64 {
    parse::parse(pattern, unicode).map_or(-1, |(_, ps)| ps.size as i64)
}

/// The size the parser counts for each pattern of `size_cases.json`, which the Go, Object Pascal and OCaml ports
/// read too. The size of a pattern decides whether it is refused as too large, so the ports must count the same
/// figure for every pattern, or a schema one port compiles would be refused by another.
#[test]
fn the_size_of_a_pattern() {
    let cases = testdata("size_cases.json");
    let cases = cases.as_array().unwrap();
    let (mut valid, mut legacy, mut invalid) = (0, 0, 0);
    for c in cases {
        let (p, u, l) = (c["p"].as_str().unwrap(), c["u"].as_i64().unwrap(), c["l"].as_i64().unwrap());
        assert_eq!((size_of(p, true), size_of(p, false)), (u, l), "the size of {p:?} with the u flag and without");
        if u >= 0 {
            valid += 1;
        } else if l >= 0 {
            legacy += 1;
        } else {
            invalid += 1;
        }
    }
    assert_eq!(cases.len(), 122);
    assert!(valid >= 90 && legacy >= 10 && invalid >= 5, "the cases are not the ones this test is about");
}

// ---------------------------------------------------------------------------------------------------------------------
// The next character.

/// The patterns of the quick set against V8. They have a choice, a loop or a lookaround where the next character of
/// the text rules a way out, with everything a way can end with or go through without a character, which is where
/// ruling a way out wrongly would show.
#[test]
fn the_next_character_rules_out_a_way() {
    let quick = cases("quick");
    let c = check_cases(&quick, Duration::from_secs(60));
    assert!(quick.len() >= 75 && c.matched >= 2000);
}

/// What `set_quick` works out for the first instruction of a program: the ASCII characters a match can start with,
/// whether it can start with one that is not ASCII, and whether it can end without one while text is left and at
/// the end of the text.
#[test]
fn quick_of_a_program() {
    for (pattern, ascii, other, mid, end) in [
        (r"abc", "a", false, false, false),
        (r"[a-c]x", "abc", false, false, false),
        (r"a|b|\u00e9", "ab", true, false, false),
        (r"a?b", "ab", false, false, false),
        (r"a*$", "a", false, false, true),
        (r"(?:a|)b", "ab", false, false, false),
        (r"$", "", false, false, true),
        (r"(?:a|$)b", "a", false, false, false),
        (r"(?=a)[a-c]", "a", false, false, false),
        (r"(?=[ab])(?:b|x)", "b", false, false, false),
        (r"(?!a)[a-c]", "abc", false, false, false),
        (r"(?=a|$)(?:a|)$", "a", false, false, true),
        (r"(?<=a)b", "b", false, false, false),
        (r"(?<=a|b)$", "", false, false, true),
        (r"\bx", "x", false, false, false),
        (r"^a", "a", false, false, false),
        (r"(?:a|b)*c", "abc", false, false, false),
        (r"(?:a?b?)*c", "abc", false, false, false),
        (r"(?:a|){2}x", "ax", false, false, false),
        (r"[^a]", "bcd", true, false, false),
        (r"(?=)a", "a", false, false, false),
        (r"[]|a", "a", false, false, false),
        (r"(?!)[]", "", false, false, false),
    ] {
        let q = program(pattern).quick[0];
        let has = |b: u8| (q.ascii[(b >> 6) as usize] >> (b & 63)) & 1 != 0;
        let ok = (q.other, q.mid, q.end) == (other, mid, end)
            && ascii.bytes().all(has)
            // A character the pattern cannot start with: none of the cases starts with a digit.
            && has(b'0') == (pattern == r"[^a]");
        assert!(ok, "{pattern:?}: quick {q:?}, want {ascii:?}, {other}, {mid}, {end}");
    }
    // A backreference and the body of a lookbehind allow anything.
    for pattern in [r"(a)\1x", r"(?<=ab)"] {
        let p = program(pattern);
        let mut found = false;
        for (pc, i) in p.insts.iter().enumerate() {
            if i.op == Op::Backref || i.op == Op::CharBack {
                found = true;
                let q = p.quick[pc];
                assert!(q.ascii == [!0, !0] && q.other && q.mid && q.end, "{pattern:?}: instruction {pc}");
            }
        }
        assert!(found, "{pattern:?} has no instruction this is about");
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The tables of the Go package's own tests.

/// The Java port's hand-written patterns: the pattern, whether it is valid with the u flag, and whether it is valid
/// at all.
#[test]
fn java_hand_written_patterns() {
    let table: &[(&str, bool, bool)] = &[
        ("", true, true),
        ("a", true, true),
        ("^a$", true, true),
        ("a|b", true, true),
        ("(a)", true, true),
        ("(?:a)", true, true),
        ("(?=a)", true, true),
        ("(?!a)", true, true),
        ("(?<=a)", true, true),
        ("(?<!a)", true, true),
        (r"(?<n>a)\k<n>", true, true),
        (r"\k<n>", false, true),
        ("(?<n>a)(?<n>b)", false, false),
        ("(?<$x_1>a)", true, true),
        ("(?<1a>a)", false, false),
        ("a{2}", true, true),
        ("a{2,}", true, true),
        ("a{2,3}", true, true),
        ("a{3,2}", false, false),
        ("a{", false, true),
        ("a{,2}", false, true),
        ("{", false, true),
        ("}", false, true),
        ("]", false, true),
        ("[", false, false),
        ("[]", true, true),
        ("[^]", true, true),
        ("[a-z]", true, true),
        ("[z-a]", false, false),
        (r"[\d-z]", false, true),
        (r"[a-\d]", false, true),
        (r"[\b]", true, true),
        (r"[\-]", true, true),
        (r"\-", false, true),
        (r"\a", false, true),
        (r"\c", false, true),
        (r"\cA", true, true),
        (r"\c1", false, true),
        (r"\0", true, true),
        (r"\01", false, true),
        (r"\1", false, true),
        (r"(a)\1", true, true),
        (r"(a)\2", false, true),
        (r"\x4", false, true),
        (r"\x41", true, true),
        (r"\u004", false, true),
        (r"\u0041", true, true),
        (r"\u{1F600}", true, true),
        (r"\u{110000}", false, true),
        ("\u{1F600}", true, true),
        ("[\u{1F600}-\u{1F64F}]", true, true),
        (r"\p{L}", true, true),
        (r"\p{Letter}", true, true),
        (r"\p{digit}", true, true),
        (r"\p{Nope}", false, true),
        (r"\p{gc=Lu}", true, true),
        (r"\p{Script=Greek}", true, true),
        (r"\p{sc=Grek}", true, true),
        (r"\p{Script=Nope}", false, true),
        (r"\P{ASCII}", true, true),
        (r"\p", false, true),
        (r"\p{", false, true),
        ("a**", false, false),
        ("a+?", true, true),
        ("*a", false, false),
        ("(?=a)*", false, true),
        ("^*", false, false),
        ("$+", false, false),
        (r"\b+", false, false),
        ("a)", false, false),
        ("(a", false, false),
        ("(?a)", false, false),
        (r"\/", true, true),
        (r"\.", true, true),
        ("a\\", false, false),
        ("[a", false, false),
        ("x{1}{2}", false, false),
        (r"\w+@\w+\.\w+", true, true),
        ("^[a-z][a-z0-9_]*$", true, true),
        (r"(?<a>.)\k<a>", true, true),
        (r"[\p{L}\d]", true, true),
        (r"\s\S\w\W\d\D", true, true),
    ];
    for &(pattern, unicode, is_valid) in table {
        assert_eq!(valid(pattern), unicode, "valid({pattern:?})");
        let re = compile(pattern);
        assert_eq!(re.is_ok(), is_valid, "compile({pattern:?}): {:?}", re.as_ref().err());
        if let Ok(re) = re {
            assert_eq!(re.unicode(), unicode, "compile({pattern:?}).unicode()");
        }
    }
}

/// Every string of the JSON-Schema-Test-Suite that is a pattern, a key of patternProperties or an instance.
fn suite_corpus() -> Option<BTreeSet<String>> {
    fn walk(v: &Value, seen: &mut BTreeSet<String>) {
        match v {
            Value::Array(items) => items.iter().for_each(|item| walk(item, seen)),
            Value::Object(object) => {
                for (name, value) in object {
                    if let Value::String(s) = value
                        && (name == "pattern" || name == "data")
                    {
                        seen.insert(s.clone());
                    }
                    if let Value::Object(patterns) = value
                        && name == "patternProperties"
                    {
                        seen.extend(patterns.keys().cloned());
                    }
                    walk(value, seen);
                }
            }
            _ => {}
        }
    }
    fn files(dir: &Path, seen: &mut BTreeSet<String>) {
        let Ok(entries) = std::fs::read_dir(dir) else { return };
        for entry in entries.flatten() {
            let path = entry.path();
            if path.is_dir() {
                files(&path, seen);
            } else if path.extension().is_some_and(|e| e == "json")
                && let Ok(text) = std::fs::read_to_string(&path)
                && let Ok(v) = serde_json::from_str::<Value>(&text)
            {
                walk(&v, seen);
            }
        }
    }
    let root = std::env::var_os("JSON_SCHEMA_TEST_SUITE")
        .map(PathBuf::from)
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")).join("../../JSON-Schema-Test-Suite"));
    if !root.join("tests").is_dir() {
        eprintln!("JSON-Schema-Test-Suite not found at {}; skipping", root.display());
        return None;
    }
    let mut seen = BTreeSet::new();
    files(&root.join("tests"), &mut seen);
    Some(seen)
}

/// `valid` and `compile` agree on every string of the JSON-Schema-Test-Suite.
#[test]
fn suite_corpus_is_read_the_same_by_valid_and_compile() {
    let Some(corpus) = suite_corpus() else { return };
    let (mut valid_count, mut compiled) = (0, 0);
    for p in &corpus {
        let ok = valid(p);
        let re = compile(p);
        if ok {
            valid_count += 1;
            assert!(re.as_ref().is_ok_and(Regexp::unicode), "valid({p:?}) but compile gives {:?}", re.as_ref().err());
        }
        if let Ok(re) = re {
            compiled += 1;
            assert!(ok || !re.unicode(), "compile({p:?}) used the u flag grammar but valid rejects it");
        }
    }
    assert!(valid_count > 500 && compiled >= valid_count, "{} strings, {valid_count} valid", corpus.len());
}

fn differential_inputs(n: usize) -> Vec<String> {
    let alphabet = [
        "a",
        "b",
        "c",
        "z",
        "A",
        "X",
        "Z",
        "0",
        "1",
        "5",
        "9",
        "_",
        "-",
        ".",
        ":",
        "/",
        "@",
        "#",
        "$",
        "*",
        "!",
        "{",
        "}",
        "|",
        " ",
        "\n",
        "\r",
        "\t",
        "\u{a0}",
        "\u{e9}",
        "\u{b5}",
        "\u{1F600}",
        "\u{1F432}",
        "\u{2028}",
        "\u{FFFD}",
        "x-",
        "es",
        "ES",
        "ms",
        "txt",
        "Au",
        "to",
        "No",
        "ne",
        "2015",
        "Co",
        "re",
        "20",
        "15",
        "22",
        "2",
        ",",
        "a,",
        "ab,",
        "a-",
        "foo",
        "bar",
        "\u{3b1}",
        "\u{3a9}",
        "\u{663}",
        "\\",
        "&",
        "(",
        ")",
        "[",
        "]",
        "^",
        "+",
        "?",
        "<",
        ">",
        "=",
        "%",
    ];
    let mut seed = Xorshift(0x2545f491);
    let mut inputs: Vec<String> = std::iter::once("").chain(alphabet).map(String::from).collect();
    while inputs.len() < n {
        let k = 1 + seed.next() % 8;
        inputs.push((0..k).map(|_| alphabet[(seed.next() % alphabet.len() as u32) as usize]).collect());
    }
    inputs
}

/// The automaton and the backtracking matcher give the same answer, for every pattern of the oracle and of the
/// JSON-Schema-Test-Suite that has an automaton, on random texts. (The Go test compares the regexp package too, and
/// has texts that are not UTF-8, which a `&str` cannot hold.)
#[test]
fn the_automaton_and_the_matcher_agree() {
    let o = oracle();
    let mut patterns: BTreeSet<String> = o.hand.iter().map(|h| h.0.clone()).collect();
    patterns.extend(o.flagged.iter().map(|(p, f, _, _)| format!("(?{f}:{p})")));
    patterns.extend(
        [
            "\u{FFFD}",
            "^[^\u{FFFD}]+$",
            "^\u{FFFD}+$",
            "^.$",
            "^..$",
            "^[^a]$",
            r"\P{Any}",
            r"^\p{Any}*$",
            r"^[\s\S]{2,4}$",
            r"^(?:\p{L}|\p{N})+$",
            r"\b\w+\b",
            r"\B",
            "^$",
            "$",
            "^",
            "(?:)",
            "^(?:a{2}){2,3}$",
            "^(?:a|b|ab){1,4}?c",
            "^.{0,300}$",
            r"^(?:[a-z]{1,3}\d?){2,5}$",
            "^[\\u0000-\u{FFFF}]+$",
            r"^[\u{10000}-\u{10ffff}]+$",
            "(?:a{50}){30}",
            "^a{1001}$",
            "^(?:a?){1001}$",
            r"\x{41}",
            r"^\u{2}$",
        ]
        .map(String::from),
    );
    patterns.extend(suite_corpus().unwrap_or_default());
    let inputs = differential_inputs(1500);
    let (mut regular, mut automata, mut decided) = (0, 0, 0);
    for p in &patterns {
        let Ok(re) = compile(p) else { continue };
        if !re.regular {
            continue;
        }
        regular += 1;
        for s in &inputs {
            let (a, b) = (re.match_limit(s, None), re.match_backtrack(s, None));
            assert_eq!(a, b, "{p:?} on {s:?}: the automaton and the backtracking matcher");
            if let Some(Some(dfa)) = re.dfa.get()
                && dfa.is_match(s.as_bytes()).is_some()
            {
                decided += 1;
            }
        }
        automata += usize::from(matches!(re.dfa.get(), Some(Some(_))));
    }
    assert!(regular > 200 && automata > 100 && decided > 100000, "{regular} patterns, {automata} automata, {decided}");
}

#[test]
fn backtracking_semantics() {
    for (pattern, text, want) in [
        (r"^\1(a)$", "a", true),
        (r"^(a)?\1b$", "b", true),
        (r"^(?:(a)|b)\1$", "b", true),
        (r"^(?:(a)|b)*\1$", "ab", true),
        (r"^(?:(a)|b)*\1$", "aba", false),
        (r"^(?:(a)|b)*\1$", "baa", true),
        (r"^(?:a*)*$(?<=a)", "aaa", true),
        (r"^(?:a*)*b(?<=b)", "aaa", false),
        (r"^(?:(?=(a)))?\1b", "b", true),
        (r"^(?:(?=(a)))?\1a", "a", true),
        (r"^(?:a?){3,5}b(?<=b)", "aab", true),
        (r"^(?:|a)+b(?<=b)", "aab", true),
        (r"^(?=(a+))a*b\1$", "aaaba", false),
        (r"^(?=(a+))a*b\1$", "aaabaaa", true),
        (r"(?!(a))\1b", "b", true),
        (r"(?<=(\d+)(\d+))$", "1053", true),
        (r"(?<=\1(a))b", "aab", true),
        (r"(?<=\1(a))b", "ab", false),
        (r"(?<=^a*)b", "aab", true),
        (r"(?<!^a*)b", "aab", false),
        (r"(?<=a|bc)x", "bcx", true),
        (r"^(?<n>.)\k<n>$", "\u{1F432}\u{1F432}", true),
        (r"^(?<n>.)\k<n>$", "\u{1F432}\u{1F409}", false),
        (r"(?<=^.)$", "\u{1F432}", true),
        ("(?<=[\u{1F600}-\u{1F60E}])x", "\u{1F603}x", true),
        (r"(?<!\u{1F600})x", "\u{1F600}x", false),
        (r"^(?:a|ab)+?b$(?<=b)", "aabb", true),
        (r"^(?:(a)|b){2,}?\1$", "abbaa", true),
        (r"^(?:(a)|b){2,}?\1$", "abba", false),
        (r"^(.+?)\1+$", "abcabcabc", true),
        (r"^(.+?)\1+$", "abcabcab", false),
        (r"^(?:(\w)(?!\1))+$", "abab", true),
        (r"^(?:(\w)(?!\1))+$", "abba", false),
        (r"^(?!.*(.).*\1)[a-c]+$", "abc", true),
        (r"^(?!.*(.).*\1)[a-c]+$", "abca", false),
    ] {
        let re = compile(pattern).unwrap();
        assert!(!re.regular, "{pattern:?} needs no backtracking");
        assert_eq!(re.is_match(text), want, "{pattern:?} on {text:?}");
    }
}

/// A long text does not overflow the stack of the thread: the choices are on a stack of the matcher's own.
#[test]
fn long_input() {
    let long = "ab".repeat(1 << 20);
    let longer = |tail: &str| format!("{long}{tail}");
    for (pattern, text, want) in [
        (r"^(?:a|b)*$(?<=b)", long.clone(), true),
        (r"^(?:a|b)*c(?<=c)", long.clone(), false),
        (r"^(?:ab)+(?!.)", long.clone(), true),
        (r"^(?=a)(?:\w\w)*?$", long.clone(), true),
        (r"^(a|b)*\1$", longer("b"), true),
        (r"(?<=^(?:ab)*)c", longer("c"), true),
        (r"^(?!b).*b$", long.clone(), true),
        (r"^[ab]{2097152}(?<=b)$", long.clone(), true),
        (r"^(?:[ab]c?){2097152}(?<=b)$", long.clone(), true),
    ] {
        let re = compile(pattern).unwrap();
        assert!(!re.regular, "{pattern:?} needs no backtracking");
        assert_eq!(re.is_match(&text), want, "{pattern:?} on {} bytes", text.len());
    }
}

/// A compiled pattern is shared between threads, each of which matches with a working memory of its own.
#[test]
fn concurrent_use() {
    for p in [r"^(?:[a-z]+\d)+$", r"^(?:([a-z])\1?\d)+(?<=\d)$"] {
        let re = std::sync::Arc::new(compile(p).unwrap());
        let threads: Vec<_> = (0..16)
            .map(|g| {
                let re = re.clone();
                std::thread::spawn(move || {
                    let yes = "aa1b2".repeat(50 + g);
                    let no = format!("{yes}!");
                    (0..500).all(|_| re.is_match(&yes) && !re.is_match(&no))
                })
            })
            .collect();
        for thread in threads {
            assert!(thread.join().unwrap(), "{p:?} gave a wrong answer under concurrent use");
        }
    }
}

/// The classes of equivalent characters: every member of a class has the canonical form of the class, and every
/// two characters with one canonical form are in a class.
#[test]
fn fold_tables() {
    for unicode_mode in [true, false] {
        // Every character with a case variant, with the closure of the set that holds it alone.
        let mut seen: HashMap<Rune, Rune> = HashMap::new();
        for r in 0..=ucd::MAX_RUNE {
            let key = canonicalize(r, unicode_mode);
            let other = seen.get(&key).copied().or((key != r).then_some(key));
            if let Some(other) = other {
                for (a, b) in [(r, other), (other, r)] {
                    let (closed, variants) = super::fold::fold_closure(&CharSet::shared(&[a, a], false), unicode_mode);
                    assert!(variants == 1 && closed.contains(b), "u flag {unicode_mode}: U+{a:04X} and U+{b:04X}");
                }
            }
            seen.insert(key, r);
        }
        // The closure of everything holds nothing more, and every member of a closure has one canonical form.
        for r in [0x4B, 0x6B, 0x212A, 0x53, 0x17F, 0xDF, 0x1E9E, 0x3A3, 0x3C2, 0x3C3, 0x130, 0x131, 0x1C4, 0x10400] {
            let (closed, _) = super::fold::fold_closure(&CharSet::shared(&[r, r], false), unicode_mode);
            for &[lo, hi] in closed.ranges.as_chunks::<2>().0 {
                for member in lo..=hi {
                    assert_eq!(canonicalize(member, unicode_mode), canonicalize(r, unicode_mode), "U+{member:04X}");
                }
            }
        }
    }
}

#[test]
fn modifiers() {
    for (pattern, text, want) in [
        (r"^(?i:abc)$", "aBc", true),
        (r"^a(?i:b)c$", "aBc", true),
        (r"^a(?i:b)c$", "ABc", false),
        (r"^(?i:a(?-i:b)c)$", "AbC", true),
        (r"^(?i:a(?-i:b)c)$", "ABC", false),
        (r"^(?i:k)$", "\u{212a}", true),
        (r"^(?i:k)\&?$", "\u{212a}", false),
        (r"^(?i:s)$", "\u{17f}", true),
        (r"^(?i:s)\&?$", "\u{17f}", false),
        (r"^(?i:\w)$", "\u{17f}", true),
        (r"^(?i:\W)$", "\u{17f}", false),
        (r"(?i:\b)\u212a", "\u{212a}", true),
        (r"\b\u212a", "\u{212a}", false),
        (r"^(?i:[^a-z])$", "K", false),
        (r"^(?i:[^a-z])$", "1", true),
        ("^(?i:\u{df})$", "\u{1e9e}", true),
        ("^(?i:\u{df})$", "SS", false),
        (r"^(.)(?i:\1)$", "aA", true),
        (r"^(.)\1$", "aA", false),
        (r"^(.)(?i:\1)$", "k\u{212a}", true),
        (r"(?<=(?i:\1)(.))x", "Aax", true),
        (r"^(?s:.)$", "\n", true),
        (r"^.$", "\n", false),
        (r"^(?s:a.)b.$", "a\nb\n", false),
        (r"(?m:^)b", "a\nb", true),
        (r"^b", "a\nb", false),
        (r"a(?m:$)", "a\u{2028}b", true),
        (r"a(?m:$)", "ab", false),
        (r"(?m:^)b", "a\rb", true),
        (r"(?m:^)b", "a\u{2029}b", true),
        (r"^(?:(?<a>x)|(?<a>y))\k<a>$", "yy", true),
        (r"^(?:(?<a>x)|(?<a>y))\k<a>$", "xy", false),
        (r"^(?:(?<a>x)|(?<a>y))+\k<a>$", "xyy", true),
        (r"^(?:(?<a>x)|(?<a>y))+\k<a>$", "xyx", false),
        (r"^(?<\u0061b>.)\k<ab>$", "zz", true),
    ] {
        let re = compile(pattern).unwrap();
        assert_eq!(re.is_match(text), want, "{pattern:?} on {text:?}");
        assert_eq!(re.match_backtrack(text, None), Ok(want), "{pattern:?} on {text:?} (the backtracking matcher)");
    }
    for p in [
        r"(?i)a",
        r"(?-:a)",
        r"(?ii:a)",
        r"(?i-i:a)",
        r"(?x:a)",
        r"(?i-m-s:a)",
        r"(?<a>x)(?<a>y)",
        r"(?<a>x|(?<a>y))",
        r"(?:(?<a>x)|b)(?:(?<a>y)|c)",
        r"(?<a\u0020>x)",
        r"(?<\u0030>x)",
    ] {
        assert!(!valid(p), "valid({p:?})");
        assert!(compile(p).is_err(), "compile({p:?}) gives no error");
    }
}

#[test]
fn properties() {
    let mut expressions = 0;
    let mut resolve = |expr: &str| {
        expressions += 1;
        assert!(property_set(expr).is_some(), "\\p{{{expr}}} does not resolve");
    };
    for name in ucd::binary_names() {
        resolve(name);
    }
    for name in
        ["ASCII", "Any", "Assigned", "Alpha", "AHex", "space", "XIDS", "L", "Letter", "LC", "digit", "punct", "Cn"]
    {
        resolve(name);
    }
    for name in
        ["L", "Lu", "Uppercase_Letter", "Cased_Letter", "Mark", "Combining_Mark", "digit", "punct", "cntrl", "Cn"]
    {
        resolve(&format!("gc={name}"));
        resolve(&format!("General_Category={name}"));
    }
    for names in ucd::script_names() {
        for value in *names {
            for property in ["sc", "Script", "scx", "Script_Extensions"] {
                resolve(&format!("{property}={value}"));
            }
        }
    }
    for value in ["Unknown", "Zzzz", "Katakana_Or_Hiragana", "Hrkt"] {
        resolve(&format!("sc={value}"));
        resolve(&format!("scx={value}"));
    }
    assert!(expressions > 1000, "{expressions} property expressions");
    for (pattern, text, want) in [
        (r"^\p{L}$", "\u{e9}", true),
        (r"^\p{L}$", "1", false),
        (r"^\p{Lu}$", "\u{c9}", true),
        (r"^\P{Lu}$", "\u{c9}", false),
        (r"^\p{Nd}$", "\u{9ea}", true),
        (r"^\p{digit}$", "\u{9ea}", true),
        (r"^\p{sc=Grek}$", "\u{3b1}", true),
        (r"^\p{Script=Greek}$", "a", false),
        (r"^\p{sc=Grek}$", "\u{342}", false),
        (r"^\p{scx=Grek}$", "\u{342}", true),
        (r"^\p{sc=Zinh}$", "\u{342}", true),
        (r"^\p{Emoji}$", "\u{1F432}", true),
        (r"^\p{Emoji_Presentation}$", "#", false),
        (r"^\p{Emoji}$", "#", true),
        (r"^\p{Alphabetic}$", "\u{2177}", true),
        (r"^\p{Uppercase}$", "\u{24b6}", true),
        (r"^\p{ID_Start}$", "_", false),
        (r"^\p{ID_Continue}$", "_", true),
        (r"^\p{White_Space}$", "\u{85}", true),
        (r"^\s$", "\u{85}", false),
        (r"^\p{White_Space}$", "\u{feff}", false),
        (r"^\s$", "\u{feff}", true),
        (r"^\p{Any}$", "\u{10ffff}", true),
        (r"^\p{Assigned}$", "\u{10ffff}", false),
        (r"^\p{Script=Unknown}$", "\u{e0080}", true),
        (r"^\p{ASCII}$", "\u{e9}", false),
        (r"^[\p{Lu}\p{Nd}]+$", "A1\u{c9}\u{9ea}", true),
        (r"^[^\p{L}]+$", "1 -", true),
        (r"^[^\p{L}]+$", "1a", false),
    ] {
        let re = compile(pattern).unwrap();
        assert_eq!(re.is_match(text), want, "{pattern:?} on {text:?}");
    }
    for p in
        [r"\p{Nope}", r"\p{L=x}", r"\p{gc=Greek}", r"\p{sc=L}", r"\p{}", r"\p{Lu", r"\pL", r"\p{sc=}", r"\p{ascii}"]
    {
        assert!(!valid(p), "valid({p:?})");
    }
}

#[test]
fn automaton() {
    for (p, want) in [
        (r"^[a-z][a-z0-9_]*$", true),
        (r"(base64key|awskms)://(.*)", true),
        (r"^[Ee][Ss]20(1[5-9]|2[0-2])(\.[a-z]+)?$", true),
        (r"^$", true),
        (r"$", true),
        (r"a*", true),
        (r"^(?:a?){40}$", true),
        ("\u{e9}+", true),
        (r"\bfoo", false),
        (r"^a{600}$", false),
        (r"(?:a{50}){12}", false),
        (r"^(?:[a-z]{1,3}\d?){2,90}$", false),
        (r"(?=a)b", false),
        (r"(a)\1", false),
    ] {
        let re = compile(p).unwrap();
        re.is_match("");
        assert_eq!(matches!(re.dfa.get(), Some(Some(_))), want, "{p:?}: automaton");
    }
    for (p, s, want) in [
        (r"^$", "", true),
        (r"^$", "a", false),
        (r"$", "abc", true),
        (r"^", "abc", true),
        (r"$^", "", true),
        (r"$^", "a", false),
        (r"a$", "ba", true),
        (r"a$", "ab", false),
        (r"^a", "ab", true),
        (r"^a", "ba", false),
        (r"b", "aaab", true),
        (r"^(a|ab)(c|bcd)$", "abcd", true),
        (r"^(a|ab)(c|bcd)$", "abc", true),
        (r"^(a|ab)(c|bcd)$", "abcc", false),
        (r"x{2,3}", "axxb", true),
        (r"^x{2,3}$", "xxxx", false),
        (r"^.$", "\n", false),
        (r"^[^a]$", "\n", true),
        ("\u{e9}", "caf", false),
        (r"^a*$", "", true),
        (r"^(a$|b)c?$", "a", true),
        (r"^(a$|b)c?$", "ac", false),
    ] {
        let re = compile(p).unwrap();
        assert_eq!(re.is_match(s), want, "{p:?} on {s:?}");
        assert_eq!(re.match_backtrack(s, None), Ok(want), "{p:?} on {s:?} (the backtracking matcher)");
        let Some(Some(dfa)) = re.dfa.get() else { panic!("{p:?}: no automaton") };
        assert_eq!(dfa.is_match(s.as_bytes()), Some(want), "{p:?} on {s:?}: the automaton");
    }
    let re = compile(r"^[a-z]+$").unwrap();
    assert!(re.is_match("abc") && !re.is_match("caf\u{e9}"));
    assert_eq!(re.dfa.get().unwrap().as_ref().unwrap().is_match("caf\u{e9}".as_bytes()), None);
    let re = compile(r"^\p{L}+$").unwrap();
    assert!(re.is_match("caf\u{e9}") && !re.is_match("caf\u{e9} 1") && re.is_match("cafe"));
}

// ---------------------------------------------------------------------------------------------------------------------
// The limit of a match.

/// Patterns and texts whose match takes longer than any test waits: a group read back inside a repetition, which
/// the memory of failed places does not cover.
fn forever() -> Vec<(&'static str, String)> {
    vec![
        (r"^(?:(a+)\1*)+$", "a".repeat(60) + "b"),
        (r"(a*)*\1b", "a".repeat(60)),
        (r"^(?:(\w+)\s?\1?)+$", "ab ".repeat(30) + "!"),
        (r"(?=(?:(a+)\1*)+b)a", "a".repeat(60)),
    ]
}

#[test]
fn a_match_is_abandoned_at_its_limit() {
    for limit in [Duration::from_millis(20), Duration::from_millis(300)] {
        for (pattern, text) in forever() {
            let re = compile(pattern).unwrap();
            let start = Instant::now();
            let result = re.match_limit(&text, Some(limit));
            let took = start.elapsed();
            assert_eq!(result, Err(MatchError::Time), "{pattern:?} with a limit of {limit:?}");
            assert!(
                took >= limit && took < limit + GENEROUS,
                "{pattern:?} with {limit:?} was abandoned after {took:?}"
            );
        }
    }
}

#[test]
fn a_match_with_no_limit_is_not_abandoned() {
    let re = compile(r"^(?:(a+)\1*)+$").unwrap();
    let text = "a".repeat(14) + "b";
    assert_eq!(re.match_limit(&text, Some(Duration::from_nanos(1))), Err(MatchError::Time));
    assert_eq!(re.match_limit(&text, None), Ok(false));
    // A limit of zero is no limit.
    assert_eq!(re.match_limit(&text, Some(Duration::ZERO)), Ok(false));
    assert!(!re.is_match(&text));
    assert_eq!(re.match_limit(&text[..14], Some(GENEROUS)), Ok(true));
}

#[test]
fn a_match_is_abandoned_at_its_memory_limit() {
    for (pattern, text) in [
        (r"(?:a?){2147483647}", String::new()),
        (r"(?:a?){100000000}", "a".repeat(100)),
        (r"(?:(?:(?:a?){1000}){1000}){1000}", "a".repeat(20)),
        (r"(?:(a)|b|){2147483647}\1", String::new()),
    ] {
        let re = compile(pattern).unwrap();
        let start = Instant::now();
        let result = re.match_limit(&text, Some(Duration::from_secs(3600)));
        assert_eq!(result, Err(MatchError::Memory), "{pattern:?}");
        assert!(start.elapsed() < GENEROUS, "{pattern:?} was abandoned after {:?}", start.elapsed());
    }
}

#[test]
fn a_limit_does_not_change_an_answer() {
    let long = "ab".repeat(50000);
    let accented = format!("{long}\u{e9}");
    for (pattern, text, want) in [
        (r"^[a-z]+$", "abc", true),
        (r"^[a-z]+$", "abc1", false),
        (r"^\p{L}+$", "caf\u{e9}", true),
        (r"^\p{L}+$", "caf\u{e9} ", false),
        (r"^(?=.*\d)\w+$", "abc1", true),
        (r"^(?=.*\d)\w+$", "abcd", false),
        (r"^(a+)\1$", "aaaa", true),
        (r"^(a+)\1$", "aaa", false),
        (r"(?<=\$)\d+", "cost $42", true),
        (r"^(?=.)(?:a|b)*$", &long, true),
        (r"^(?=.)(?:a|b)*c$", &long, false),
        (r"^[ab]*$", &long, true),
        (r"^[ab]*c$", &long, false),
        ("^[ab\u{e9}]*$", &accented, true),
        ("^[ab\u{e9}]*c$", &accented, false),
    ] {
        let re = compile(pattern).unwrap();
        for limit in [None, Some(Duration::from_secs(60))] {
            assert_eq!(re.match_limit(text, limit), Ok(want), "{pattern:?} with a limit of {limit:?}");
            assert_eq!(re.match_backtrack(text, limit), Ok(want), "{pattern:?} with a limit of {limit:?}");
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The memory of failed places.

struct MemoFromTheStart;

impl MemoFromTheStart {
    fn on() -> MemoFromTheStart {
        set_memo_from_the_start(true);
        MemoFromTheStart
    }
}

impl Drop for MemoFromTheStart {
    fn drop(&mut self) {
        set_memo_from_the_start(false);
    }
}

/// The patterns of the memo set against V8, and then every set and the hand-written patterns of the oracle with the
/// memory in use from the first step.
#[test]
fn failed_places_are_remembered() {
    let memo = cases("memo");
    let c = check_cases(&memo, Duration::from_secs(60));
    let with_points = memo.iter().filter(|m| !program(&m.p).points.is_empty()).count();
    assert!(memo.len() >= 50 && with_points >= 45 && c.matched >= 500, "{with_points} with memo points");
    let _memo = MemoFromTheStart::on();
    let mut total = 0;
    for set in ["memo", "quick", "loops", "needs", "units", "fast"] {
        total += check_cases(&cases(set), Duration::from_secs(120)).answers;
    }
    let o = oracle();
    for (p, u, l, m) in &o.hand {
        let Some(re) = check_verdict(p, *u, *l) else { continue };
        if !V8_MODIFIER_BUGS.contains(&p.as_str()) {
            total += check_answers(&re, &o.inputs, m);
        }
    }
    assert!(total > 60000, "{total} answers with the memory in use from the first step");
}

#[test]
fn a_repetition_in_a_repetition_is_answered() {
    let a = "a".repeat(60);
    let ab = "ab".repeat(30);
    for (pattern, text, want) in [
        (r"(?=a)(a*)*b", a.clone(), false),
        (r"(?=a)(a*)*b", a.clone() + "b", true),
        (r"^(?=.)(?:a+)+$", a.clone() + "x", false),
        (r"^(?=.)(?:a+)+$", a.clone(), true),
        (r"^(?=.)(?:a|aa)+$", a.clone() + "b", false),
        (r"^(?=.)(?:a|aa)+$", a.clone(), true),
        (r"^(?=.)(?:a?b?)*x", ab.clone(), false),
        (r"^(?=.)(?:a?b?)*x", ab.clone() + "x", true),
        (r"^(?=.)(?:(?:a?b?)*x?)*$", ab.clone() + "!", false),
        (r"^(?=.)(?:(?:a*)+b?)+$", a.clone() + "!", false),
        (r"^(?=.)(?:(?:a*)+b?)+$", a.clone() + "b", true),
        (r"^(?=.)(?:(?:a?)+?b?)*?x", a.clone(), false),
        (r"(?=.)(?:a*?b*?)*?x$", a.clone() + "b", false),
        (r"(?i:\b(?:\w+\W?)+\b!)", "ab ".repeat(40), false),
        (r"(?i:\b(?:\w+\W?)+\b!)", "ab ".repeat(40) + "ab!", true),
        (r"(?m:^(?:\w+\s?)+$)", "a ".repeat(30) + "!", false),
        (r"^(?!(?:a+)+x)a*b", a.clone() + "b", true),
        (r"^(?!(?:a+)+x)a*b", a.clone(), false),
        (r"(?<!(?:a|aa)+b)x$", a.clone() + "x", true),
        (r"(?=.).*.*.*x", "a".repeat(3000), false),
        (r"(?=.)a*?a*?a*?b", "a".repeat(3000), false),
        (r"(?=.)(?:a|b)*c", "ab".repeat(50000), false),
        (r"(?=.)(?:a|\u00e9)*c", "a\u{e9}".repeat(30000), false),
    ] {
        let re = compile(pattern).unwrap();
        assert!(!re.regular && !re.prog.points.is_empty(), "{pattern:?} has {} memo points", re.prog.points.len());
        for limit in [Some(GENEROUS), None] {
            assert_eq!(re.match_limit(&text, limit), Ok(want), "{pattern:?} on {} characters, {limit:?}", text.len());
        }
    }
}

#[test]
fn a_backreference_turns_the_memory_off() {
    for pattern in [r"(a*)*\1b", r"^(?:(a+)\1*)+$", r"(a)(?:b|c)*\1", r"(?<n>a)(?:b+)+\k<n>"] {
        assert!(program(pattern).points.is_empty(), "{pattern:?} has memo points");
    }
    let re = compile(r"(a*)*\1b").unwrap();
    assert_eq!(re.match_limit(&"a".repeat(60), Some(Duration::from_millis(50))), Err(MatchError::Time));
}

#[test]
fn memo_points() {
    let table: &[(&str, &[&str])] = &[
        (r"(?:a|b)*c", &["split", "split"]),
        (r"(?:a?b?)*c", &["loop", "rep 0", "rep 0"]),
        (r"(?:(?:a?)*b)*", &["split", "loop", "rep 0"]),
        (r"(?:(?:a?)+b?)+x", &["loop", "loop 0", "rep 0 1", "rep 0"]),
        (r"(?:(?:(?:a?)*b?)*x?)*", &["loop", "loop 0", "loop 0 1", "rep 0 1", "rep 0"]),
        (r"(?:a|){2,3}(?:b|c)", &["split"]),
        (r"(?:(?:a|b){2}x?)*", &["split", "rep"]),
        (r"(?:(?:a?b?)*?(?:c|d)){3}", &[]),
        (r"(?:a?(?=(?:b|c)*x))*", &["loop", "rep 0", "split", "split"]),
        (r"(?:(?:a|){2}(?=b|c))*", &["loop", "split"]),
        (r"(?<=(?:a?b?)*)x", &["loop", "split 0", "split 0"]),
        (r"a*b*?c{2,3}", &["rep", "lazy", "rep"]),
        (r"(?:a+)+$", &["rep", "split"]),
        (r"(a)\1(?:b|c)", &[]),
    ];
    for &(pattern, want) in table {
        let p = program(pattern);
        let mut points = Vec::new();
        for i in &p.insts {
            if i.m == 0 {
                continue;
            }
            let mut s = match i.op {
                Op::Split => "split",
                Op::Loop => "loop",
                Op::Rep => "rep",
                Op::RepLazy => "lazy",
                _ => "?",
            }
            .to_string();
            for l in p.points[(i.m - 1) as usize].loops {
                if l >= 0 {
                    s += &format!(" {l}");
                }
            }
            points.push(s);
        }
        assert_eq!(points, want, "{pattern:?}");
        assert_eq!(p.points.len(), want.len(), "{pattern:?}");
    }
    assert_eq!(MAX_MEMO_LOOPS, 2);
}

/// A match too large for the memory goes without it, and ends in an answer or at its limit.
#[test]
fn a_match_too_large_for_the_memory_goes_without_it() {
    let alternatives: Vec<String> = (0..400).map(|k| format!("a{}", "b".repeat(k % 7))).collect();
    let re = compile(&format!("^(?=.)(?:{})+$", alternatives.join("|"))).unwrap();
    let result = re.match_limit(&("ab".repeat(50000) + "x"), Some(Duration::from_millis(100)));
    assert!(matches!(result, Ok(false) | Err(MatchError::Time)), "{result:?}");
}

// ---------------------------------------------------------------------------------------------------------------------
// The limits of size.

fn ranges_of(name: &str) -> usize {
    property_set(name).unwrap().ranges.len() / 2
}

/// Patterns just under the limit of size, of every shape that is costly for its size.
fn at_the_limit() -> Vec<(&'static str, String)> {
    let fit = |each: usize| (MAX_SIZE - 4096) / each;
    let l_ranges = ranges_of("L");
    let nest = |open: &str, body: &str, close: &str, depth: usize| open.repeat(depth) + body + &close.repeat(depth);
    let named: Vec<String> = (0..400).map(|k| format!("(?<n>{k})")).collect();
    vec![
        ("literal", "a".repeat(fit(2 * NODE_SIZE))),
        ("alternatives", "a|".repeat(fit(3 * NODE_SIZE)).trim_end_matches('|').to_string()),
        ("groups", "(a)".repeat(fit(4 * NODE_SIZE))),
        ("groups read back", "(a)".repeat(4000) + &r"\1".repeat(4000) + &r"\4000".repeat(300)),
        ("nested groups", nest("(", &"a".repeat(fit(2 * NODE_SIZE) - 2 * MAX_NESTING), ")", MAX_NESTING - 1)),
        ("nested counted loops", nest("(?:", &"a?b".repeat(2000), "){2,3}", MAX_NESTING - 1)),
        ("nested lookbehinds", nest("(?<=", &"a".repeat(2000), ")", MAX_NESTING - 1)),
        ("counted loops", "(?:a|){2}".repeat(fit(7 * NODE_SIZE))),
        ("lazy loops", "(?:a|b)*?".repeat(fit(7 * NODE_SIZE))),
        ("properties", r"\p{L}".repeat(fit(2 * NODE_SIZE))),
        ("negated properties", r"\P{L}".repeat(fit(2 * NODE_SIZE + RANGE_SIZE * (l_ranges + 1)))),
        ("classes of properties", r"[\p{L}\p{N}]".repeat(fit(2 * NODE_SIZE + RANGE_SIZE * (l_ranges + 200)))),
        ("folded negated classes", format!("(?i:{})", "[^a]".repeat(fit(2 * NODE_SIZE + 32)))),
        ("folded dots", format!("(?i:{})", ".".repeat(fit(2 * NODE_SIZE)))),
        ("folded wide classes", format!("(?i:{})", r"[\u0000-\u{10FFFF}]".repeat(fit(3200)))),
        ("folded wide negations", format!("(?i:{})", r"[^\u0100-\u{10FFFF}]".repeat(fit(3300)))),
        ("folded properties", format!("(?i:{})", r"\p{L}".repeat(fit(8600)))),
        ("folded literal", format!("(?i:{})", "k".repeat(fit(2 * NODE_SIZE)))),
        ("one large class", format!("[{}]", "\u{100}\u{102}".repeat(fit(2 * RANGE_SIZE)))),
        ("groups of one name", named.join("|")),
        ("named groups, nested", nest("(?:x|", &"(?<a>b)|(?<c>d)".repeat(40), ")", 600)),
        ("counted, for regexp", "(?:a?){1000}".repeat(fit(4 * NODE_SIZE))),
        ("quantified everything", "a{2,5}?".repeat(fit(2 * NODE_SIZE))),
        ("long escapes", r"\u{0000000000000000000000000000000000000000000041}".repeat(fit(2 * NODE_SIZE))),
    ]
}

/// A pattern at the limit of size compiles, and matches for the first time, in a time far below the one a test
/// waits. The thread has the stack of a thread the standard library starts, so the deepest nesting the limits allow
/// is also shown not to overflow it.
#[test]
fn a_pattern_at_the_limit_of_size_is_cheap() {
    for (name, pattern) in at_the_limit() {
        let start = Instant::now();
        let re = compile(&pattern).unwrap_or_else(|e| panic!("{name} ({} bytes): {e}", pattern.len()));
        let compiled = start.elapsed();
        let start = Instant::now();
        let result = re.match_limit("abc", Some(Duration::from_secs(60)));
        let matched = start.elapsed();
        eprintln!("{name:<24} {:>7} bytes: compile {compiled:?}, first match {matched:?} ({result:?})", pattern.len());
        assert!(
            compiled < GENEROUS && matched < GENEROUS,
            "{name} took {compiled:?} to compile and {matched:?} to match"
        );
    }
}

/// The deepest nesting the limits allow is read, compiled, matched and dropped by a thread with a small stack: the
/// work that goes a level deeper for each level of groups is done on a stack of its own.
#[test]
fn the_deepest_nesting_does_not_overflow_a_small_stack() {
    let small = std::thread::Builder::new().stack_size(128 << 10);
    let test = small.spawn(|| {
        let nest =
            |open: &str, body: &str, close: &str| open.repeat(MAX_NESTING - 1) + body + &close.repeat(MAX_NESTING - 1);
        for (pattern, text, want) in [
            (nest("(", "a", ")"), "a", true),
            (nest("(?:", "a?b", "){1,2}"), "ab", true),
            (nest("(?=", "a", ")") + "b", "a", false),
            (nest("(?:a|", "b", ")"), "b", true),
            (nest("(?:x|", "a", ")?") + "y$", "xxay", true),
        ] {
            assert!(valid(&pattern), "{}", short(&pattern));
            assert!(check(&pattern).is_ok());
            let re = compile(&pattern).unwrap();
            assert_eq!(re.match_limit(text, Some(GENEROUS)), Ok(want), "{}", short(&pattern));
            assert_eq!(re.match_backtrack(text, Some(GENEROUS)), Ok(want), "{}", short(&pattern));
        }
        // One level more is the limit of nesting, and that many open groups with nothing to close them is an error.
        assert!(matches!(compile(&nest("((", "a", "))")), Err(Error::TooLarge(_))));
        assert!(matches!(compile(&"(".repeat(900)), Err(Error::Invalid(_))));
        assert!(!valid(&"(".repeat(100000)));
    });
    test.unwrap().join().unwrap();
}

#[test]
fn a_pattern_over_a_limit_of_size_is_too_large() {
    let nest = |depth: usize| "(?:".repeat(depth) + "a" + &")".repeat(depth);
    let terms = MAX_SIZE / (2 * NODE_SIZE);
    let escape = |zeros: usize| format!("\\u{{{}41}}", "0".repeat(zeros));
    for (name, at, over, limit, value) in [
        ("length", escape(MAX_PATTERN_LENGTH - 6), escape(MAX_PATTERN_LENGTH - 5), Limit::Length, MAX_PATTERN_LENGTH),
        ("nesting", nest(MAX_NESTING - 1), nest(MAX_NESTING), Limit::Nesting, MAX_NESTING),
        ("size of terms", "a".repeat(terms), "a".repeat(terms + 1), Limit::Size, MAX_SIZE),
        (
            "size of classes",
            format!("[{}]", "\u{100}\u{102}".repeat(60000)),
            format!("[{}]", "\u{100}\u{102}".repeat(70000)),
            Limit::Size,
            MAX_SIZE,
        ),
        ("size without the u flag", r"\-".repeat(terms), r"\-".repeat(terms + 1), Limit::Size, MAX_SIZE),
        (
            "nesting without the u flag",
            format!(r"\-{}", nest(MAX_NESTING - 1)),
            format!(r"\-{}", nest(MAX_NESTING)),
            Limit::Nesting,
            MAX_NESTING,
        ),
        ("nesting, and not valid", nest(MAX_NESTING - 1) + "x", nest(MAX_NESTING) + "*+)", Limit::Nesting, MAX_NESTING),
        ("size, and not valid", "a".repeat(terms), "a".repeat(terms + 1) + "(", Limit::Size, MAX_SIZE),
    ] {
        assert!(compile(&at).is_ok(), "{name}: the pattern at the limit ({} bytes)", at.len());
        assert!(check(&at).is_ok(), "{name}: check of the pattern at the limit");
        for result in [compile(&over).map(|_| ()), check(&over).map(|_| ())] {
            match result {
                Err(Error::TooLarge(large)) => {
                    assert_eq!((large.limit, large.value), (limit, value), "{name}");
                    assert_eq!(large.limit.name(), ["length", "nesting", "size"][limit as usize]);
                    assert!(large.to_string().contains(&value.to_string()), "{large}");
                }
                other => panic!("{name}: the pattern over the limit ({} bytes): {other:?}", over.len()),
            }
        }
        assert!(!valid(&over), "{name}: valid of the pattern over the limit");
    }
    for pattern in ["(".to_string(), "a**".into(), r"(?<n>a)\k<m>".into(), "[b-a]".into(), nest(MAX_NESTING - 1) + ")"]
    {
        assert!(matches!(compile(&pattern), Err(Error::Invalid(_))), "compile({})", short(&pattern));
        assert!(matches!(check(&pattern), Err(Error::Invalid(_))), "check({})", short(&pattern));
    }
}

/// The needs settle in two passes over the instructions and the next characters in three, however deep the loops
/// nest. A pass for each loop of a nest would be a thousand passes for loops a thousand deep.
#[test]
fn the_needs_settle_in_few_passes() {
    deep(needs_settle);
}

fn needs_settle() {
    let nest = |open: &str, body: &str, close: &str, depth: usize| open.repeat(depth) + body + &close.repeat(depth);
    for pattern in [
        "abc".to_string(),
        "(?:a|b)*c".into(),
        "(?:a?b?)*c".into(),
        "(?:(?:a+)+b?)*".into(),
        nest("(?:", "a?b", "){2,3}", 999),
        nest("(?:", "a?b", ")*", 999),
        nest("(?:", "a?b?", ")*", 999),
        nest("(?:", "a?b", ")+?", 999),
        nest("(?:x|", "a?b", ")*y?", 500),
        nest("(?:(?=", "a", ")b?)*", 400),
        nest("(?:", "a|b", "){0,5}?c", 999),
    ] {
        let mut p = program(&pattern);
        let (_, need_passes) = needs(&mut p.insts);
        let (_, quick_passes) = quicks(&p.insts);
        assert!(need_passes <= 2 && quick_passes <= 3, "{}: {need_passes} and {quick_passes} passes", short(&pattern));
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The tables.

/// The tables of `tables_gen.rs` are those of the Go module: every table of `tables_gen.go`, by its name, has the
/// same ranges here, and the two case mappings have the same rows. `tools/gen-ucd-tables.ps1` writes one file from
/// the other, and this is what fails when one of them is generated again and the other is not.
#[test]
fn tables_are_those_of_the_go_module() {
    let go = std::fs::read_to_string(go_file("ucd/tables_gen.go")).unwrap();
    assert!(go.contains(&format!("const Version = \"{}\"", ucd::VERSION)));
    let number = |s: &str| -> i64 {
        let s = s.trim();
        match s.strip_prefix("0x") {
            Some(hex) => i64::from_str_radix(hex, 16).unwrap(),
            None => s.parse().unwrap(),
        }
    };
    // The tables, by the name of the Go variable.
    let mut tables: HashMap<&str, Vec<Rune>> = HashMap::new();
    let mut cases: HashMap<&str, Vec<[i64; 4]>> = HashMap::new();
    let mut lookups: Vec<(&str, Vec<&str>, &str)> = Vec::new();
    let (mut table, mut lookup, mut names) = ("", "", Vec::new());
    for line in go.lines() {
        if let Some(rest) = line.strip_prefix("var ") {
            table = rest.split(' ').next().unwrap();
        } else if let Some(rest) = line.strip_prefix("func ") {
            lookup = rest.split('(').next().unwrap();
        } else if let Some(rest) = line.strip_prefix("\tcase ") {
            names = rest.trim_end_matches(':').split(", ").map(|n| n.trim_matches('"')).collect();
        } else if let Some(rest) = line.strip_prefix("\t\treturn &") {
            lookups.push((lookup, std::mem::take(&mut names), rest));
        } else if let Some(row) = line.strip_prefix("\t{").and_then(|r| r.strip_suffix("},"))
            && (table == "caseFolds" || table == "upperCases")
        {
            let row: Vec<i64> = row.split(',').map(number).collect();
            cases.entry(table).or_default().push([row[0], row[1], row[2], row[3]]);
        } else if line.starts_with("\t\t0x") {
            tables
                .entry(table)
                .or_default()
                .extend(line.split(',').filter(|s| !s.trim().is_empty()).map(|s| number(s) as Rune));
        }
    }
    assert!(tables.len() > 300 && lookups.len() > 400, "{} tables, {} lookups", tables.len(), lookups.len());
    for (lookup, names, table) in lookups {
        // The ranges of the Go table, the part up to U+FFFF and the part beyond it joined where they touch.
        let mut want: Vec<Rune> = Vec::new();
        for &[lo, hi] in tables.get(table).map_or(&[][..], |t| &t[..]).as_chunks::<2>().0 {
            match want.last_mut() {
                Some(last) if *last == 0xFFFF && lo == 0x10000 => *last = hi,
                _ => want.extend_from_slice(&[lo, hi]),
            }
        }
        for name in names {
            let found = match lookup {
                "categoryTable" => ucd::category(name),
                "binaryTable" => ucd::binary(name),
                "scriptTable" => ucd::script(name),
                "scriptExtensionsTable" => ucd::script_extensions(name),
                other => panic!("an unknown lookup {other}"),
            };
            assert_eq!(
                found.map(|t| t.pairs()),
                Some(want.clone()),
                "{lookup}({name:?}) is not {table} of the Go module"
            );
        }
    }
    // The case mappings, by what they do to every code point.
    let map = |rows: &[[i64; 4]], r: i64| -> i64 {
        let row = rows.iter().rev().find(|row| row[0] <= r);
        match row {
            Some(row) if r - row[0] < row[2] && (r - row[0]) & row[3] == 0 => r + row[1],
            _ => r,
        }
    };
    for r in 0x80..=ucd::MAX_RUNE {
        assert_eq!(ucd::fold(r) as i64, map(&cases["caseFolds"], r as i64), "the folding of U+{r:04X}");
        assert_eq!(ucd::to_upper(r) as i64, map(&cases["upperCases"], r as i64), "the uppercase of U+{r:04X}");
    }
}
